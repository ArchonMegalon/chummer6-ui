#nullable enable annotations

using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Sr5Sr6UiParityAuditComplianceTests
{
    [TestMethod]
    public void Sr5_sr6_ui_parity_audit_materializer_pins_the_direct_legacy_and_provider_proof_paths()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize_sr5_sr6_ui_parity_audit.py");
        string wrapperPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "sr5-sr6-ui-parity-audit-check.sh");
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string b14Path = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string signoffPath = Path.Combine(repoRoot, "docs", "WORKBENCH_RELEASE_SIGNOFF.md");
        string scriptText = File.ReadAllText(scriptPath);
        string wrapperText = File.ReadAllText(wrapperPath);
        string verifyText = File.ReadAllText(verifyPath);
        string b14Text = File.ReadAllText(b14Path);
        string signoffText = File.ReadAllText(signoffPath);

        StringAssert.Contains(scriptText, "SR5_SR6_UI_PARITY_AUDIT.generated.json");
        StringAssert.Contains(scriptText, "LegacySr5DesktopParityAuditTests.cs");
        StringAssert.Contains(scriptText, "Sr5Sr6RulesetParityAuditTests.cs");
        StringAssert.Contains(scriptText, "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY_DETAILS.generated.json");
        StringAssert.Contains(scriptText, "chummer-core-engine");
        StringAssert.Contains(scriptText, "legacyElementDispositions");
        StringAssert.Contains(scriptText, "legacyElementsMissingExplicitSr6Pendants");
        StringAssert.Contains(scriptText, "familyReviewsWithUnavailableMappedCurrentIds");
        StringAssert.Contains(scriptText, "legacyElementsWithUnavailableMappedCurrentIds");
        StringAssert.Contains(scriptText, "unavailableMappedCurrentIdCount");
        StringAssert.Contains(scriptText, "nonPendantMappedCurrentIdCount");
        StringAssert.Contains(scriptText, "unsupportedMappedCurrentIdCount");
        StringAssert.Contains(scriptText, "FullyQualifiedName~LegacySr5DesktopParityAuditTests|FullyQualifiedName~Sr5Sr6RulesetParityAuditTests");
        StringAssert.Contains(scriptText, "legacy utility-control dialog, utility-dialog action execution, workflow, and shared-command execution parity tests");
        StringAssert.Contains(scriptText, "legacyTabs");
        StringAssert.Contains(scriptText, "legacyControls");
        StringAssert.Contains(scriptText, "providerParityTests");

        StringAssert.Contains(wrapperText, "scripts/materialize_sr5_sr6_ui_parity_audit.py");
        StringAssert.Contains(wrapperText, "SR5_SR6_UI_PARITY_AUDIT.generated.json");
        StringAssert.Contains(wrapperText, "SR5/SR6 UI parity audit still reports explicit legacy-to-SR6 gaps.");
        StringAssert.Contains(wrapperText, "SR5/SR6 UI parity audit still reports explicit full-spectrum SR5-to-SR6 gaps.");

        StringAssert.Contains(verifyText, "checking direct SR5/SR6 UI parity audit");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/sr5-sr6-ui-parity-audit-check.sh");

        StringAssert.Contains(b14Text, "running explicit direct SR5/SR6 UI parity audit");
        StringAssert.Contains(b14Text, "sr5_sr6_ui_parity_audit_receipt_path");
        StringAssert.Contains(b14Text, "explicit direct SR5/SR6 UI parity audit is not passed");
        StringAssert.Contains(b14Text, "\"sr5Sr6UiParityAuditProof\"");

        StringAssert.Contains(signoffText, "scripts/ai/milestones/sr5-sr6-ui-parity-audit-check.sh");
        StringAssert.Contains(signoffText, "SR5_SR6_UI_PARITY_AUDIT.generated.json");
    }

    [TestMethod]
    public void Sr5_sr6_ui_parity_audit_receipt_records_no_explicit_sr6_gaps()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "SR5_SR6_UI_PARITY_AUDIT.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;
        JsonElement evidence = root.GetProperty("evidence");
        JsonElement legacyTabs = root.GetProperty("legacyTabs");
        JsonElement legacyControls = root.GetProperty("legacyControls");
        JsonElement providerParityTests = root.GetProperty("providerParityTests");

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.sr5_sr6_ui_parity_audit", root.GetProperty("contract_name").GetString());
        Assert.IsTrue(evidence.GetProperty("legacyTabCount").GetInt32() >= 35);
        Assert.IsTrue(evidence.GetProperty("legacyControlCount").GetInt32() >= 50);
        Assert.AreEqual(0, evidence.GetProperty("partialTabCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("missingTabCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("partialControlCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("missingControlCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("legacyElementDispositionCount").GetInt32() >= 2800);
        Assert.AreEqual(0, evidence.GetProperty("missingLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("familyFallbackLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("familyReviewsWithUnavailableMappedCurrentIds").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("legacyElementsWithUnavailableMappedCurrentIds").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("unavailableMappedCurrentIdCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("nonPendantMappedCurrentIdCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("legacyElementsMissingExplicitSr6Pendants").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("unsupportedMappedCurrentIdCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("testResult").GetProperty("exitCode").GetInt32());
        Assert.AreEqual(evidence.GetProperty("legacyTabCount").GetInt32(), legacyTabs.GetArrayLength());
        Assert.AreEqual(evidence.GetProperty("legacyControlCount").GetInt32(), legacyControls.GetArrayLength());
        Assert.IsTrue(providerParityTests.GetArrayLength() >= 8);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "Sr6_ruleset_provider_keeps_sr5_command_tab_action_and_workflow_pendants",
                "Sr6_ruleset_quick_action_pendants_exist_for_every_sr5_workspace_section",
                "Sr6_ruleset_keeps_sr5_section_target_hosting_groups",
                "Sr6_ruleset_rendered_section_surfaces_keep_sr5_pendants_without_placeholder_fallback",
                "Sr6_ruleset_command_dialog_contracts_keep_sr5_field_and_action_pendants",
                "Sr6_ruleset_new_character_workflow_dialog_contracts_keep_sr5_field_and_action_pendants",
                "Sr6_ruleset_origin_and_priority_rebuild_dialog_contracts_keep_sr5_field_and_action_pendants",
                "Sr6_ruleset_shared_command_execution_contracts_keep_sr5_function_pendants"
            },
            providerParityTests.EnumerateArray().Select(static row => row.GetString() ?? string.Empty).ToArray());

        foreach (JsonElement row in legacyTabs.EnumerateArray())
        {
            string disposition = row.GetProperty("disposition").GetString() ?? string.Empty;
            Assert.AreNotEqual("Partial", disposition);
            Assert.AreNotEqual("Missing", disposition);
            Assert.IsTrue(row.GetProperty("modernPendants").GetArrayLength() > 0);
        }

        foreach (JsonElement row in legacyControls.EnumerateArray())
        {
            string disposition = row.GetProperty("disposition").GetString() ?? string.Empty;
            Assert.AreNotEqual("Partial", disposition);
            Assert.AreNotEqual("Missing", disposition);
            Assert.IsTrue(row.GetProperty("modernPendants").GetArrayLength() > 0);
        }
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
