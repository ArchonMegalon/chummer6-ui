#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M121GmRunboardRouteGuardTests
{
    [TestMethod]
    public void M121_gm_runboard_route_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m121-ui-gm-runboard-route-check.sh"));

        StringAssert.Contains(verifyScript, "checking next-90 M121 desktop GM Runboard route guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m121-ui-gm-runboard-route-check.sh");
        StringAssert.Contains(projectText, "Compliance\\Next90M121GmRunboardRouteGuardTests.cs");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m121-ui-add-the-desktop-gm-runboard-route-with-initiative-action\"");
        StringAssert.Contains(scriptText, "FRONTIER_ID = 7834909683");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"121.3\"");
        StringAssert.Contains(scriptText, "\"add_the_desktop_gm_runboard:ui\"");
        StringAssert.Contains(scriptText, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(scriptText, "EXPECTED_DO_NOT_REOPEN_REASON = \"M121 chummer6-ui desktop GM Runboard route is complete;");
        StringAssert.Contains(scriptText, "\"DesktopCampaignWorkspaceSurface.GmRunboard\"");
        StringAssert.Contains(scriptText, "\\\"Open GM Runboard\\\"");
        StringAssert.Contains(scriptText, "\"public const string GmRunboard = \\\"gm_runboard\\\";\"");
    }

    [TestMethod]
    public void M121_gm_runboard_route_receipt_proves_desktop_surface_slice()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M121_UI_GM_RUNBOARD_ROUTE.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.next90_m121_ui_gm_runboard_route", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m121-ui-add-the-desktop-gm-runboard-route-with-initiative-action", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(7834909683, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(121, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("121.3", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W15", evidence.GetProperty("wave").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "add_the_desktop_gm_runboard:ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        JsonElement checks = evidence.GetProperty("queueChecks");
        Assert.IsTrue(checks.GetProperty("registry_task_status_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("registry_task_evidence_is_queue_managed").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_status_complete").GetBoolean());
        Assert.IsTrue(checks.GetProperty("queue_proof_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_queue_proof_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("owned_surfaces_exact").GetBoolean());
        Assert.IsTrue(checks.GetProperty("design_owned_surfaces_exact").GetBoolean());

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopHomeWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/DesktopOrganizerOperationsWindow.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Avalonia/App.axaml.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Desktop.Runtime/DesktopStartupSurfaceCatalog.cs"));
        AssertSourceMarkersPass(sourceChecks.GetProperty("Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs"));

        JsonElement closedPackage = evidence.GetProperty("closedPackage");
        Assert.AreEqual("verify_closed_package_only", closedPackage.GetProperty("completionAction").GetString());
        StringAssert.Contains(closedPackage.GetProperty("doNotReopenReason").GetString(), "M121 chummer6-ui desktop GM Runboard route is complete");
    }

    private static void AssertSourceMarkersPass(JsonElement sourceChecks)
    {
        foreach (JsonProperty markerCheck in sourceChecks.EnumerateObject())
        {
            Assert.IsTrue(markerCheck.Value.GetBoolean(), $"Expected source marker to pass: {markerCheck.Name}");
        }
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        List<string> values = new();
        foreach (JsonElement item in element.EnumerateArray())
        {
            string? value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }
}
