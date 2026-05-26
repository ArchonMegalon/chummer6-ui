#nullable enable annotations

#nullable enable annotations

using System;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class Next90M141UiImportRouteProofTests
{
    [TestMethod]
    public void Direct_import_route_proof_receipt_keeps_translator_xml_and_hero_lab_screenshot_contracts_green()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = document.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(
            "next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment",
            evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(2354698282, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual("141.1", evidence.GetProperty("workTaskId").GetString());

        JsonElement routeChecks = evidence.GetProperty("routeReceiptChecks");
        AssertRouteReceiptPass(routeChecks.GetProperty("translator_xml_custom_data"));
        AssertRouteReceiptPass(routeChecks.GetProperty("hero_lab_import_oracle"));

        JsonElement screenshotFiles = evidence.GetProperty("screenshotFiles");
        Assert.IsTrue(screenshotFiles.GetProperty("38-translator-dialog-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("39-xml-editor-dialog-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("40-hero-lab-importer-dialog-light.png").GetBoolean());
    }

    private static void AssertRouteReceiptPass(JsonElement route)
    {
        Assert.IsTrue(route.GetProperty("exists").GetBoolean());
        Assert.IsTrue(route.GetProperty("status_pass").GetBoolean());
        Assert.IsTrue(route.GetProperty("screenshots_exact").GetBoolean());
        Assert.IsTrue(route.GetProperty("route_ids_exact").GetBoolean());
        Assert.IsTrue(route.GetProperty("workflow_family_matches").GetBoolean());
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int index = 0; index < 8; index += 1)
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}
