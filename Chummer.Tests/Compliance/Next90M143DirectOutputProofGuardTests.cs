#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M143DirectOutputProofGuardTests
{
    [TestMethod]
    public void M143_direct_output_proof_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string guardScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m143-ui-direct-output-proof-check.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));

        StringAssert.Contains(verifyScript, "checking next-90 M143 direct output proof guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh");

        StringAssert.Contains(guardScript, "PACKAGE_ID = \"next90-m143-ui-capture-direct-screenshot-and-runtime-proof-for-print-export-exchange-sr6\"");
        StringAssert.Contains(guardScript, "FRONTIER_ID = 6764868619");
        StringAssert.Contains(guardScript, "WORK_TASK_ID = \"143.1\"");
        StringAssert.Contains(guardScript, "EXPECTED_STATUS = \"complete\"");
        StringAssert.Contains(guardScript, "EXPECTED_COMPLETION_ACTION = \"verify_closed_package_only\"");
        StringAssert.Contains(guardScript, "M143 chummer6-ui print/export/exchange and SR6 supplement/house-rule direct proof is complete;");
        StringAssert.Contains(guardScript, "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json");
        StringAssert.Contains(guardScript, "Next90M143DirectOutputProofGuardTests");
        StringAssert.Contains(guardScript, "\"family:sheet_export_print_viewer_and_exchange\"");
        StringAssert.Contains(guardScript, "\"family:sr6_supplements_designers_and_house_rules\"");
        StringAssert.Contains(guardScript, "\"print_export_exchange\"");
        StringAssert.Contains(guardScript, "\"sr6_supplements_and_house_rules\"");
        StringAssert.Contains(guardScript, "\"19-workflow-file-menu-loaded-light.png\"");
        StringAssert.Contains(guardScript, "\"34-workflow-validate-section-light.png\"");
        StringAssert.Contains(guardScript, "\"35-workflow-rules-section-light.png\"");
        StringAssert.Contains(guardScript, "\"operator telemetry\"");
        StringAssert.Contains(projectText, "Compliance\\Next90M143DirectOutputProofGuardTests.cs");
    }

    [TestMethod]
    public void M143_direct_output_proof_receipt_proves_route_local_output_coverage()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("unresolved").GetArrayLength());
        Assert.AreEqual("chummer6-ui.next90_m143_ui_direct_output_proof", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("next90-m143-ui-capture-direct-screenshot-and-runtime-proof-for-print-export-exchange-sr6", evidence.GetProperty("packageId").GetString());
        Assert.AreEqual(6764868619, evidence.GetProperty("frontierId").GetInt64());
        Assert.AreEqual(143, evidence.GetProperty("milestoneId").GetInt32());
        Assert.AreEqual("143.1", evidence.GetProperty("workTaskId").GetString());
        Assert.AreEqual("W22P", evidence.GetProperty("wave").GetString());
        Assert.AreEqual("chummer6-ui", evidence.GetProperty("repo").GetString());

        CollectionAssert.AreEquivalent(
            new[] { "Chummer.Avalonia", "Chummer.Desktop.Runtime", "Chummer.Tests", "scripts" },
            ReadStringArray(evidence.GetProperty("allowedPaths")));
        CollectionAssert.AreEquivalent(
            new[] { "capture_direct_screenshot_and_runtime_proof_for_print_ex:ui" },
            ReadStringArray(evidence.GetProperty("ownedSurfaces")));

        AssertAllBooleansAreTrue(evidence.GetProperty("queueChecks"));
        AssertAllBooleansAreTrue(evidence.GetProperty("parityAuditChecks"));
        AssertAllBooleansAreTrue(evidence.GetProperty("receiptChecks"));

        JsonElement routeReceiptChecks = evidence.GetProperty("routeReceiptChecks");
        AssertAllBooleansAreTrue(routeReceiptChecks.GetProperty("print_export_exchange"));
        AssertAllBooleansAreTrue(routeReceiptChecks.GetProperty("sr6_supplements_and_house_rules"));

        JsonElement sourceChecks = evidence.GetProperty("sourceChecks");
        foreach (JsonProperty sourceCheck in sourceChecks.EnumerateObject())
        {
            AssertAllBooleansAreTrue(sourceCheck.Value);
        }

        JsonElement screenshotFiles = evidence.GetProperty("screenshotFiles");
        Assert.IsTrue(screenshotFiles.GetProperty("18-import-dialog-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("19-workflow-file-menu-loaded-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("34-workflow-validate-section-light.png").GetBoolean());
        Assert.IsTrue(screenshotFiles.GetProperty("35-workflow-rules-section-light.png").GetBoolean());

        CollectionAssert.AreEquivalent(
            new[]
            {
                Path.Combine(repoRoot, "Chummer.Tests", "Compliance", "Next90M143DirectOutputProofGuardTests.cs"),
                Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m143-ui-direct-output-proof-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "chummer5a-screenshot-review-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "section-host-ruleset-parity-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "generated-dialog-element-parity-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m114-ui-rule-studio-check.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"),
                Path.Combine(repoRoot, "scripts", "ai", "verify.sh"),
                Path.Combine(repoRoot, ".codex-studio", "published", "CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "SECTION_HOST_RULESET_PARITY.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "GENERATED_DIALOG_ELEMENT_PARITY.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M114_UI_RULE_STUDIO.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "UI_FLAGSHIP_RELEASE_GATE.generated.json"),
                Path.Combine(repoRoot, ".codex-studio", "published", "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json"),
            },
            ReadStringArray(evidence.GetProperty("proofFiles")));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "bash scripts/ai/milestones/next90-m143-ui-direct-output-proof-check.sh",
                "dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"FullyQualifiedName~Next90M143DirectOutputProofGuardTests\" --no-restore",
            },
            ReadStringArray(evidence.GetProperty("proofCommands")));
    }

    private static void AssertAllBooleansAreTrue(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.True:
                    break;
                case JsonValueKind.Object:
                    AssertAllBooleansAreTrue(property.Value);
                    break;
                default:
                    Assert.Fail($"Expected '{property.Name}' to be true.");
                    break;
            }
        }
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        var values = new List<string>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            values.Add(item.GetString() ?? string.Empty);
        }

        return values.ToArray();
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")) || Directory.Exists(Path.Combine(current, "scripts")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Unable to locate repository root.");
        return string.Empty;
    }
}
