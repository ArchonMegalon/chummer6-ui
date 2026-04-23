#nullable enable annotations

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class GeneratedDialogElementParityComplianceTests
{
    [TestMethod]
    public void Generated_dialog_element_guard_pins_command_control_and_rebuild_inventory()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "generated-dialog-element-parity-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "GENERATED_DIALOG_ELEMENT_PARITY.generated.json");
        StringAssert.Contains(scriptText, "\"runtime_inspector\"");
        StringAssert.Contains(scriptText, "\"open_character\"");
        StringAssert.Contains(scriptText, "\"new_character\"");
        StringAssert.Contains(scriptText, "\"quality_delete\"");
        StringAssert.Contains(scriptText, "\"dialog.global_settings\"");
        StringAssert.Contains(scriptText, "\"dialog.dice_roller\"");
        StringAssert.Contains(scriptText, "\"dialog.ui.vehicle_edit\"");
        StringAssert.Contains(scriptText, "CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions");
        StringAssert.Contains(scriptText, "CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions");
        StringAssert.Contains(scriptText, "RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions");
        StringAssert.Contains(scriptText, "ExecuteCommandAsync_all_catalog_commands_are_handled");
        StringAssert.Contains(scriptText, "HandleUiControlAsync_all_catalog_controls_are_non_generic");
        StringAssert.Contains(scriptText, "\"scripts/ai/test.sh\"");
        StringAssert.Contains(scriptText, "\"m103_receipt\": repo_root / \".codex-studio/published/NEXT90_M103_UI_VETERAN_CERTIFICATION.generated.json\"");
        StringAssert.Contains(scriptText, "\"sourceArtifactReview\"");
        StringAssert.Contains(scriptText, "\"inventoryReview\"");
        StringAssert.Contains(scriptText, "\"testMarkerReview\"");
        StringAssert.Contains(scriptText, "\"verifyWiringReview\"");
        StringAssert.Contains(scriptText, "\"m103EvidenceReview\"");
        StringAssert.Contains(scriptText, "\"executionReview\"");
        StringAssert.Contains(scriptText, "\"failureCount\"");
    }

    [TestMethod]
    public void Generated_dialog_element_guard_stays_in_standard_verify_path()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(verifyText, "checking generated dialog element parity guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/generated-dialog-element-parity-check.sh");
    }

    [TestMethod]
    public void Generated_dialog_element_receipt_records_passed_inventory_and_test_execution()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "GENERATED_DIALOG_ELEMENT_PARITY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.generated_dialog_element_parity", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(28, evidence.GetProperty("commandDialogCount").GetInt32());
        Assert.AreEqual(47, evidence.GetProperty("legacyControlCount").GetInt32());
        Assert.AreEqual(13, evidence.GetProperty("rebuildableDialogCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("reasonCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("m103DialogEvidence").GetProperty("auditedDialogSurfaceCount").GetInt32() > 0);
        Assert.AreEqual("pass", root.GetProperty("sourceArtifactReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("inventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("testMarkerReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("m103EvidenceReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"runtime_inspector\"");
        StringAssert.Contains(receiptText, "\"new_character\"");
        StringAssert.Contains(receiptText, "\"quality_delete\"");
        StringAssert.Contains(receiptText, "\"dialog.dice_roller\"");
        StringAssert.Contains(receiptText, "\"dialog.ui.vehicle_edit\"");
        StringAssert.Contains(receiptText, "\"Name~CreateCommandDialog_all_factory_mapped_commands_surface_named_fields_and_actions\"");
        StringAssert.Contains(receiptText, "\"Name~CreateUiControlDialog_all_catalog_controls_surface_named_fields_and_actions\"");
        StringAssert.Contains(receiptText, "\"Name~RebuildDynamicDialog_all_rebuildable_dialogs_preserve_named_fields_and_actions\"");
        StringAssert.Contains(receiptText, "\"Name~ExecuteCommandAsync_all_catalog_commands_are_handled\"");
        StringAssert.Contains(receiptText, "\"Name~HandleUiControlAsync_all_catalog_controls_are_non_generic\"");
        StringAssert.Contains(receiptText, "\"exitCode\": 0");
        StringAssert.Contains(receiptText, "\"noMatches\": false");
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate Chummer.sln from the current test directory.");
        return string.Empty;
    }
}
