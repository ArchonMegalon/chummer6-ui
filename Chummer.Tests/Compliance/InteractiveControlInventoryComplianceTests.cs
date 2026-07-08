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
        StringAssert.Contains(scriptText, "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters");
        StringAssert.Contains(scriptText, "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus");
        StringAssert.Contains(scriptText, "Standalone_toolstrip_buttons_raise_expected_events");
        StringAssert.Contains(scriptText, "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events");
        StringAssert.Contains(scriptText, "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events");
        StringAssert.Contains(scriptText, "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions");
        StringAssert.Contains(scriptText, "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available");
        StringAssert.Contains(scriptText, "Keyboard_shortcuts_resolve_to_the_same_shell_commands");
        StringAssert.Contains(scriptText, "Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces");
        StringAssert.Contains(scriptText, "Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches");
        StringAssert.Contains(scriptText, "File_menu_new_character_creates_runtime_workspace");
        StringAssert.Contains(scriptText, "Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive");
        StringAssert.Contains(scriptText, "Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters");
        StringAssert.Contains(scriptText, "Workspace_strip_quick_start_hides_after_runtime_backed_runner_load");
        StringAssert.Contains(scriptText, "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end");
        StringAssert.Contains(scriptText, "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks");
        StringAssert.Contains(scriptText, "Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards");
        StringAssert.Contains(scriptText, "Horizons_hub_launches_native_karma_forge_alice_run_control_and_black_ledger_workbenches");
        StringAssert.Contains(scriptText, "Horizons_core_native_workbenches_surface_runtime_backed_detail_interactions");
        StringAssert.Contains(scriptText, "Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback");
        StringAssert.Contains(scriptText, "Horizons_remaining_native_workbenches_surface_runtime_backed_detail_interactions");
        StringAssert.Contains(scriptText, "Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow");
        StringAssert.Contains(scriptText, "does not preserve the expected empty-or-four codex root labels posture");
        StringAssert.Contains(scriptText, "reports no codex root labels without the expected empty-workspace marker");
        StringAssert.Contains(scriptText, "\"Dossier: none (open: 0, n/a)\"");
        StringAssert.Contains(scriptText, "\"Workspace: none (open: 0, n/a)\"");
        StringAssert.Contains(scriptText, "\"State: ready, dossier=none, open=0, saved=unsaved, last-command=close_window\"");
        StringAssert.Contains(scriptText, "\"State: ready, workspace=none, open=0, saved=unsaved, last-command=close_window\"");
        StringAssert.Contains(scriptText, "\"delegate_route_receipt\": repo_root / \".codex-studio/published/DELEGATE_COMMAND_ROUTE_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"generated_dialog_receipt\": repo_root / \".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"section_host_ruleset_receipt\": repo_root / \".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/test.sh\"");
        StringAssert.Contains(scriptText, "\"sourceArtifactReview\"");
        StringAssert.Contains(scriptText, "\"standaloneControlReview\"");
        StringAssert.Contains(scriptText, "\"mainWindowInteractionReview\"");
        StringAssert.Contains(scriptText, "\"keyboardAndTooltipReview\"");
        StringAssert.Contains(scriptText, "\"runtimeRouteInventoryReview\"");
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
    public void Interactive_control_inventory_guard_uses_alias_safe_repo_root_resolution()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "interactive-control-inventory-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "repo_root_physical=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")/../../..\" && pwd -P)\"");
        StringAssert.Contains(scriptText, "repo_root_alias_candidate=\"${CHUMMER_UI_REPO_ROOT_ALIAS:-$repo_root_physical}\"");
        StringAssert.Contains(scriptText, "repo_root=\"$(cd -L \"$repo_root_alias_candidate\" && pwd -L)\"");
        Assert.IsFalse(
            scriptText.Contains("repo_root=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")/../../..\" && pwd)\"", StringComparison.Ordinal),
            "Interactive control inventory guard should not fall back to the older non-alias-aware repo root resolution.");
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
        Assert.AreEqual("pass", root.GetProperty("keyboardAndTooltipReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("runtimeRouteInventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("dependencyReceiptReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("b14ConsumptionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"Standalone_toolstrip_buttons_raise_expected_events\": true");
        StringAssert.Contains(receiptText, "\"Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters\": true");
        StringAssert.Contains(receiptText, "\"Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus\": true");
        StringAssert.Contains(receiptText, "\"Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions\": true");
        StringAssert.Contains(receiptText, "\"Keyboard_shortcuts_resolve_to_the_same_shell_commands\": true");
        StringAssert.Contains(receiptText, "\"Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces\": true");
        StringAssert.Contains(receiptText, "\"Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches\": true");
        StringAssert.Contains(receiptText, "\"File_menu_new_character_creates_runtime_workspace\": true");
        StringAssert.Contains(receiptText, "\"Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive\": true");
        StringAssert.Contains(receiptText, "\"Workspace_strip_quick_start_hides_after_runtime_backed_runner_load\": true");
        StringAssert.Contains(receiptText, "\"Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end\": true");
        StringAssert.Contains(receiptText, "\"Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks\": true");
        StringAssert.Contains(receiptText, "\"Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards\": true");
        StringAssert.Contains(receiptText, "\"Horizons_hub_launches_native_karma_forge_alice_run_control_and_black_ledger_workbenches\": true");
        StringAssert.Contains(receiptText, "\"Horizons_core_native_workbenches_surface_runtime_backed_detail_interactions\": true");
        StringAssert.Contains(receiptText, "\"Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback\": true");
        StringAssert.Contains(receiptText, "\"Horizons_remaining_native_workbenches_surface_runtime_backed_detail_interactions\": true");
        StringAssert.Contains(receiptText, "\"Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow\": true");
        StringAssert.Contains(receiptText, "Name~Standalone_toolstrip_buttons_raise_expected_events");
        StringAssert.Contains(receiptText, "Name~Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters");
        StringAssert.Contains(receiptText, "Name~Keyboard_shortcuts_resolve_to_the_same_shell_commands");
        StringAssert.Contains(receiptText, "Name~Recursive_runtime_control_inventory_records_widget_classes_tooltips_and_dense_editor_surfaces");
        StringAssert.Contains(receiptText, "Name~Interactive_runtime_route_inventory_receipt_captures_recursive_shell_dialog_popup_and_ruleset_branches");
        StringAssert.Contains(receiptText, "Name~File_menu_new_character_creates_runtime_workspace");
        StringAssert.Contains(receiptText, "Name~Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks");
        StringAssert.Contains(receiptText, "Name~Settings_click_opens_interactive_inline_dialog_and_window_stays_responsive");
        StringAssert.Contains(receiptText, "Name~Workspace_strip_quick_start_hides_after_runtime_backed_runner_load");
        StringAssert.Contains(receiptText, "Name~Load_demo_runner_button_restores_workspace_using_runtime_backed_presenters");
        StringAssert.Contains(receiptText, "Name~Horizons_shell_entry_opens_native_hub_with_filterable_runtime_backed_cards");
        StringAssert.Contains(receiptText, "Name~Horizons_hub_launches_remaining_native_workbenches_without_browser_only_fallback");
        StringAssert.Contains(receiptText, "Name~Alice_supports_blank_state_build_help_and_gm_steered_origin_dossier_flow");
        StringAssert.Contains(receiptText, "\"routeFamilies\": [");
        StringAssert.Contains(receiptText, "\"rulesetLanes\": [");
        StringAssert.Contains(receiptText, "\"section-attributes-editor\"");
        StringAssert.Contains(receiptText, "\"dialog-priority-workflow-priority\"");
        StringAssert.Contains(receiptText, "\"ruleset-sr4-codex-tree\"");
        StringAssert.Contains(receiptText, "\"ruleset-sr5-codex-tree\"");
        StringAssert.Contains(receiptText, "\"ruleset-sr6-codex-tree\"");
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
