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
        StringAssert.Contains(scriptText, "SectionQuickActionCatalog_backed_sections_keep_only_real_primary_actions");
        StringAssert.Contains(scriptText, "SectionQuickActionCatalog_unbacked_sections_stay_hidden");
        StringAssert.Contains(scriptText, "ResolveCommands_and_navigation_tabs_clone_requested_ruleset");
        StringAssert.Contains(scriptText, "ResolveWorkspaceActionsForTab_returns_ruleset_cloned_tab_scoped_inventory");
        StringAssert.Contains(scriptText, "Project_hides_unbacked_section_quick_actions");
        StringAssert.Contains(scriptText, "Project_formats_ruleset_conditioned_navigator_section_action_labels");
        StringAssert.Contains(scriptText, "ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset");
        StringAssert.Contains(scriptText, "\"QuickActions: ProjectSectionQuickActions(shellSurface.ActiveRulesetId, state.ActiveSectionId),\"");
        StringAssert.Contains(scriptText, "\"RulesetUiDirectiveCatalog.FormatWorkspaceActionLabel(\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/test.sh\"");
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
        Assert.AreEqual(23, evidence.GetProperty("standardSectionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("sr6AdaptedSectionCount").GetInt32());
        Assert.AreEqual(39, evidence.GetProperty("commandCount").GetInt32());
        Assert.AreEqual(10, evidence.GetProperty("tabCount").GetInt32());
        Assert.AreEqual(13, evidence.GetProperty("workspaceActionCount").GetInt32());
        Assert.AreEqual("pass", evidence.GetProperty("rulesetAdaptationStatus").GetString());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("reasonCount").GetInt32());
        Assert.AreEqual("pass", root.GetProperty("sourceArtifactReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("sectionInventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("shellInventoryReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("testMarkerReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("projectorReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("verifyWiringReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("rulesetReceiptReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"gear\"");
        StringAssert.Contains(receiptText, "\"tab-info.validate\"");
        StringAssert.Contains(receiptText, "\"new_critter\"");
        StringAssert.Contains(receiptText, "\"Name~SectionQuickActionCatalog_\"");
        StringAssert.Contains(receiptText, "\"Name~ResolveWorkspaceActionsForTab_\"");
        StringAssert.Contains(receiptText, "\"Name~Project_hides_unbacked_section_quick_actions\"");
        StringAssert.Contains(receiptText, "\"Name~Project_formats_ruleset_conditioned_navigator_section_action_labels\"");
        StringAssert.Contains(receiptText, "\"Name~ShellDirectives_distinguish_headings_and_tab_action_labels_per_ruleset\"");
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
