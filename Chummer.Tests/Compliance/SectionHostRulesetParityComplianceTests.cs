#nullable enable annotations

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class SectionHostRulesetParityComplianceTests
{
    [TestMethod]
    public void Section_host_ruleset_guard_pins_section_catalog_shell_inventory_and_projector_markers()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "section-host-ruleset-parity-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "SECTION_HOST_RULESET_PARITY.generated.json");
        StringAssert.Contains(scriptText, "\"gear\"");
        StringAssert.Contains(scriptText, "quick_action_control_ids_found");
        StringAssert.Contains(scriptText, "\"unknownQuickActionControls\"");
        StringAssert.Contains(scriptText, "\"switch_ruleset\"");
        StringAssert.Contains(scriptText, "\"tab-notes.metadata\"");
        StringAssert.Contains(scriptText, "\"tab-info.spelldefense\"");
        StringAssert.Contains(scriptText, "\"tab-combat.conditionmonitor\"");
        StringAssert.Contains(scriptText, "\"tab-technomancer.sprites\"");
        StringAssert.Contains(scriptText, "\"tab-streetgear.gear\"");
        StringAssert.Contains(scriptText, "\"tab-relationships.relationships\"");
        StringAssert.Contains(scriptText, "\"tab-karma.summary\"");
        StringAssert.Contains(scriptText, "SectionQuickActionCatalog_backed_sections_keep_only_real_primary_actions");
        StringAssert.Contains(scriptText, "SectionQuickActionCatalog_unbacked_sections_stay_hidden");
        StringAssert.Contains(scriptText, "ResolveCommands_and_navigation_tabs_clone_requested_ruleset");
        StringAssert.Contains(scriptText, "ResolveWorkspaceActionsForTab_returns_ruleset_cloned_tab_scoped_inventory");
        StringAssert.Contains(scriptText, "ResolveCommands_tabs_and_workspace_actions_keep_provider_backed_contract_shape_for_sr5_and_sr6");
        StringAssert.Contains(scriptText, "Project_hides_unbacked_section_quick_actions");
        StringAssert.Contains(scriptText, "Project_formats_ruleset_conditioned_navigator_section_action_labels");
        StringAssert.Contains(scriptText, "ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset");
        StringAssert.Contains(scriptText, "Sr6_shell_directives_keep_authored_pendants_where_sr5_already_has_authored_labels");
        StringAssert.Contains(scriptText, "SectionPane_renders_sr6_attribute_workbench_and_emits_attribute_edits");
        StringAssert.Contains(scriptText, "SectionPane_projects_sr6_attribute_limits_from_legacy_limits_string_payloads");
        StringAssert.Contains(scriptText, "SectionPane_orders_sr6_attribute_rows_and_disables_out_of_range_increase_controls");
        StringAssert.Contains(scriptText, "Sr6_attribute_editor_uses_authored_labels_instead_of_generic_shared_shorthand");
        StringAssert.Contains(scriptText, "Sr6_ruleset_keeps_sr5_section_target_hosting_groups");
        StringAssert.Contains(scriptText, "\"quick_action_projection\": \"ProjectSectionQuickActions(shellSurface.ActiveRulesetId, state.ActiveSectionId)\"");
        StringAssert.Contains(scriptText, "\"section_action_label_projection\": \"RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(\"");
        StringAssert.Contains(scriptText, "\"buildCommand\"");
        StringAssert.Contains(scriptText, "\"testCommands\"");
        StringAssert.Contains(scriptText, "\"testApplicationPath\"");
        StringAssert.Contains(scriptText, "\"--filter\"");
        StringAssert.Contains(scriptText, "\"sourceArtifactReview\"");
        StringAssert.Contains(scriptText, "\"sectionInventoryReview\"");
        StringAssert.Contains(scriptText, "\"shellInventoryReview\"");
        StringAssert.Contains(scriptText, "\"testMarkerReview\"");
        StringAssert.Contains(scriptText, "\"projectorReview\"");
        StringAssert.Contains(scriptText, "\"verifyWiringReview\"");
        StringAssert.Contains(scriptText, "\"rulesetReceiptReview\"");
        StringAssert.Contains(scriptText, "\"executionReview\"");
        StringAssert.Contains(scriptText, "\"failureCount\"");
    }

    [TestMethod]
    public void Section_host_ruleset_guard_stays_in_standard_verify_path()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(verifyText, "checking section host and ruleset parity guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/section-host-ruleset-parity-check.sh");
    }

    [TestMethod]
    public void Section_host_ruleset_receipt_records_passed_inventory_and_test_execution()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "SECTION_HOST_RULESET_PARITY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.section_host_ruleset_parity", root.GetProperty("contract_name").GetString());

        JsonElement evidence = root.GetProperty("evidence");
        Assert.AreEqual(3, evidence.GetProperty("standardSectionCount").GetInt32());
        Assert.AreEqual(34, evidence.GetProperty("sr6AdaptedSectionCount").GetInt32());
        JsonElement commandIdsFound = evidence.GetProperty("commandIdsFound");
        Assert.AreEqual(JsonValueKind.Array, commandIdsFound.ValueKind);
        Assert.AreEqual(51, evidence.GetProperty("commandCount").GetInt32());
        Assert.AreEqual(51, commandIdsFound.GetArrayLength());
        Assert.AreEqual(22, evidence.GetProperty("tabCount").GetInt32());
        Assert.AreEqual(102, evidence.GetProperty("workspaceActionCount").GetInt32());
        Assert.AreEqual("pass", evidence.GetProperty("rulesetAdaptationStatus").GetString());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("reasonCount").GetInt32());
        Assert.AreEqual("pass", root.GetProperty("sourceArtifactReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("sectionInventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("shellInventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("testMarkerReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("projectorReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("attributeParityReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("interactiveSurfaceReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("rulesetReceiptReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"gear\"");
        StringAssert.Contains(receiptText, "\"tab-info.validate\"");
        StringAssert.Contains(receiptText, "\"new_critter\"");
        StringAssert.Contains(receiptText, "\"open_custom_data\"");
        StringAssert.Contains(receiptText, "\"Name~SectionQuickActionCatalog_\"");
        StringAssert.Contains(receiptText, "\"Name~ResolveWorkspaceActionsForTab_\"");
        StringAssert.Contains(receiptText, "\"Name~ResolveCommands_tabs_and_workspace_actions_keep_provider_backed_contract_shape_for_sr5_and_sr6\"");
        StringAssert.Contains(receiptText, "\"Name~Project_hides_unbacked_section_quick_actions\"");
        StringAssert.Contains(receiptText, "\"Name~Project_formats_ruleset_conditioned_navigator_section_action_labels\"");
        StringAssert.Contains(receiptText, "\"Name~ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset\"");
        StringAssert.Contains(receiptText, "\"Name~Sr6_shell_directives_keep_authored_pendants_where_sr5_already_has_authored_labels\"");
        StringAssert.Contains(receiptText, "\"Name~SectionPane_renders_sr6_attribute_workbench_and_emits_attribute_edits\"");
        StringAssert.Contains(receiptText, "\"Name~Sr6_attribute_editor_\"");
        StringAssert.Contains(receiptText, "\"tab-info.spelldefense\"");
        StringAssert.Contains(receiptText, "\"tab-combat.conditionmonitor\"");
        StringAssert.Contains(receiptText, "\"tab-technomancer.sprites\"");
        StringAssert.Contains(receiptText, "\"tab-streetgear.gear\"");
        StringAssert.Contains(receiptText, "\"tab-relationships.relationships\"");
        StringAssert.Contains(receiptText, "\"tab-karma.summary\"");
        StringAssert.Contains(receiptText, "\"Sr6_ruleset_keeps_sr5_section_target_hosting_groups\": true");
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
