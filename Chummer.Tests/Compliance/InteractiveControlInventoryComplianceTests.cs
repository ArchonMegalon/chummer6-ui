#nullable enable annotations

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class InteractiveControlInventoryComplianceTests
{
    [TestMethod]
    public void Interactive_control_inventory_guard_pins_standalone_controls_main_window_routes_and_b14_consumption()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "interactive-control-inventory-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "INTERACTIVE_CONTROL_INVENTORY.generated.json");
        StringAssert.Contains(scriptText, "Standalone_toolstrip_buttons_raise_expected_events");
        StringAssert.Contains(scriptText, "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events");
        StringAssert.Contains(scriptText, "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events");
        StringAssert.Contains(scriptText, "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions");
        StringAssert.Contains(scriptText, "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available");
        StringAssert.Contains(scriptText, "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters");
        StringAssert.Contains(scriptText, "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end");
        StringAssert.Contains(scriptText, "\"delegate_route_receipt\": repo_root / \".codex-studio/published/DELEGATE_COMMAND_ROUTE_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"generated_dialog_receipt\": repo_root / \".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"section_host_ruleset_receipt\": repo_root / \".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/test.sh\"");
        StringAssert.Contains(scriptText, "\"sourceArtifactReview\"");
        StringAssert.Contains(scriptText, "\"standaloneControlReview\"");
        StringAssert.Contains(scriptText, "\"mainWindowInteractionReview\"");
        StringAssert.Contains(scriptText, "\"dependencyReceiptReview\"");
        StringAssert.Contains(scriptText, "\"verifyWiringReview\"");
        StringAssert.Contains(scriptText, "\"b14ConsumptionReview\"");
        StringAssert.Contains(scriptText, "\"executionReview\"");
        StringAssert.Contains(scriptText, "\"failureCount\"");
    }

    [TestMethod]
    public void Interactive_control_inventory_guard_stays_in_standard_verify_path()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(verifyText, "checking standalone interactive control inventory guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/interactive-control-inventory-check.sh");
    }

    [TestMethod]
    public void Interactive_control_inventory_receipt_records_passed_inventory_and_b14_release_gate_consumption()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "INTERACTIVE_CONTROL_INVENTORY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.interactive_control_inventory", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual("pass", evidence.GetProperty("fullInteractiveControlInventory").GetString());
        Assert.AreEqual("pass", evidence.GetProperty("mainWindowInteractionInventory").GetString());
        Assert.AreEqual("pass", evidence.GetProperty("dependencyReceipts").GetProperty("delegateCommandRouteParity").GetProperty("status").GetString());
        Assert.AreEqual("pass", evidence.GetProperty("dependencyReceipts").GetProperty("generatedDialogElementParity").GetProperty("status").GetString());
        Assert.AreEqual("pass", evidence.GetProperty("dependencyReceipts").GetProperty("sectionHostRulesetParity").GetProperty("status").GetString());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("b14UsesReceipt").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("reasonCount").GetInt32());
        Assert.AreEqual("pass", root.GetProperty("sourceArtifactReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("standaloneControlReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("mainWindowInteractionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("dependencyReceiptReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("b14ConsumptionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"Standalone_toolstrip_buttons_raise_expected_events\": true");
        StringAssert.Contains(receiptText, "\"Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions\": true");
        StringAssert.Contains(receiptText, "\"Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end\": true");
        StringAssert.Contains(receiptText, "\"Name~Standalone_toolstrip_buttons_raise_expected_events");
        StringAssert.Contains(receiptText, "\"Name~Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters");
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
