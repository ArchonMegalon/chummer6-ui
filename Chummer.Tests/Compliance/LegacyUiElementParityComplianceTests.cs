#nullable enable annotations

using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class LegacyUiElementParityComplianceTests
{
    [TestMethod]
    public void Legacy_ui_element_parity_guard_scans_designer_runtime_and_dynamic_interactive_surfaces()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "chummer5a-legacy-ui-element-parity-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json");
        StringAssert.Contains(scriptText, "DESIGNER_EVENT_RE");
        StringAssert.Contains(scriptText, "RUNTIME_EVENT_RE");
        StringAssert.Contains(scriptText, "DYNAMIC_INTERACTIVE_RE");
        StringAssert.Contains(scriptText, "AVALONIA_DYNAMIC_INTERACTIVE_RE");
        StringAssert.Contains(scriptText, "AVALONIA_NAMED_AXAML_RE");
        StringAssert.Contains(scriptText, "PARITY_FAMILIES");
        StringAssert.Contains(scriptText, "resolve_legacy_element_counterparts");
        StringAssert.Contains(scriptText, "legacyElementDispositionReview");
        StringAssert.Contains(scriptText, "missingLegacyElementDispositionCount");
        StringAssert.Contains(scriptText, "familyFallbackLegacyElementDispositionCount");
        StringAssert.Contains(scriptText, "Individual {legacy_subject} legacy UI elements still rely on behavior-family fallback");
        StringAssert.Contains(scriptText, "unclassifiedLegacyEvents");
        StringAssert.Contains(scriptText, "unclassifiedLegacyDynamicElements");
        StringAssert.Contains(scriptText, "Runtime_backed_file_menu_new_character_opens_creation_dialog");
        StringAssert.Contains(scriptText, "Runtime_backed_global_settings_visible_selects_and_toggles_remain_live");
        StringAssert.Contains(scriptText, "Runtime_generated_dialog_controls_expose_stable_inventory_names");
        StringAssert.Contains(scriptText, "Avalonia_and_Blazor_legacy_ui_controls_expose_matching_dialog_contracts");
    }

    [TestMethod]
    public void Chummer4_legacy_ui_element_parity_wrapper_scans_external_oracle_and_uses_distinct_contract()
    {
        string repoRoot = FindRepoRoot();
        string wrapperPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "chummer4-legacy-ui-element-parity-check.sh");
        string wrapperText = File.ReadAllText(wrapperPath);

        StringAssert.Contains(wrapperText, "CHUMMER4_LEGACY_SOURCE_ROOT");
        StringAssert.Contains(wrapperText, "/docker/fleet/repos/chummer4/Chummer");
        StringAssert.Contains(wrapperText, "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json");
        StringAssert.Contains(wrapperText, "LEGACY_UI_PARITY_LEGACY_ROOTS");
        StringAssert.Contains(wrapperText, "chummer6-ui.chummer4_legacy_ui_element_parity");
        StringAssert.Contains(wrapperText, "chummer4-legacy-ui-element-parity");
    }

    [TestMethod]
    public void Legacy_ui_element_parity_guard_blocks_standard_verify_and_b14_release_gate()
    {
        string repoRoot = FindRepoRoot();
        string verifyText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string b14Text = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh"));

        StringAssert.Contains(verifyText, "checking Chummer5a legacy UI element parity guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/chummer5a-legacy-ui-element-parity-check.sh");
        StringAssert.Contains(b14Text, "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json");
        StringAssert.Contains(b14Text, "running explicit Chummer5a legacy UI element parity gate");
        StringAssert.Contains(b14Text, "chummer5a-legacy-ui-element-parity-check.sh");
        StringAssert.Contains(b14Text, "legacy_ui_element_parity_receipt = json.load(handle)");
        StringAssert.Contains(b14Text, "legacyUiElementParityReceiptPath");
        StringAssert.Contains(b14Text, "explicitLegacyUiElementParityReceiptPath");

        StringAssert.Contains(verifyText, "checking Chummer4 legacy UI element parity guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/chummer4-legacy-ui-element-parity-check.sh");
        StringAssert.Contains(b14Text, "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json");
        StringAssert.Contains(b14Text, "running explicit Chummer4 legacy UI element parity gate");
        StringAssert.Contains(b14Text, "chummer4-legacy-ui-element-parity-check.sh");
        StringAssert.Contains(b14Text, "chummer4_legacy_ui_element_parity_receipt = json.load(handle)");
        StringAssert.Contains(b14Text, "chummer4LegacyUiElementParityReceiptPath");
        StringAssert.Contains(b14Text, "explicitChummer4LegacyUiElementParityReceiptPath");
    }

    [TestMethod]
    public void Legacy_ui_element_parity_receipt_records_extracted_legacy_handlers_dynamic_elements_and_current_counterparts()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "CHUMMER5A_LEGACY_UI_ELEMENT_PARITY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;
        JsonElement evidence = root.GetProperty("evidence");

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.chummer5a_legacy_ui_element_parity", root.GetProperty("contract_name").GetString());
        Assert.IsTrue(evidence.GetProperty("legacyDesignerEventHookCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("legacyRuntimeEventHookCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("legacyDynamicInteractiveElementCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("currentDynamicInteractiveElementCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("currentNamedAxamlInteractiveElementCount").GetInt32() > 0);
        Assert.AreEqual(0, evidence.GetProperty("unclassifiedLegacyEventCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("unclassifiedLegacyDynamicElementCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("legacyElementDispositionCount").GetInt32() > 2000);
        Assert.AreEqual(0, evidence.GetProperty("missingLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("familyFallbackLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("b14ConsumesReceipt").GetBoolean());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"file_new\"");
        StringAssert.Contains(receiptText, "\"settings_global\"");
        StringAssert.Contains(receiptText, "\"character_creation\"");
        StringAssert.Contains(receiptText, "\"search_filter_category\"");
        StringAssert.Contains(receiptText, "\"Runtime_generated_dialog_controls_expose_stable_inventory_names\": true");
        StringAssert.Contains(receiptText, "\"legacyElementDispositionReview\"");
        StringAssert.Contains(receiptText, "\"legacyElementDispositionCount\"");
        Assert.AreEqual("pass", root.GetProperty("legacyExtractionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("currentMappingReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("legacyElementDispositionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("dynamicElementReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());
    }

    [TestMethod]
    public void Chummer4_legacy_ui_element_parity_receipt_records_external_handlers_dynamic_elements_and_current_counterparts()
    {
        string repoRoot = FindRepoRoot();
        string receiptPath = Path.Combine(
            repoRoot,
            ".codex-studio",
            "published",
            "CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json");

        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        JsonElement root = receipt.RootElement;
        JsonElement evidence = root.GetProperty("evidence");

        Assert.AreEqual("pass", root.GetProperty("status").GetString());
        Assert.AreEqual("chummer6-ui.chummer4_legacy_ui_element_parity", root.GetProperty("contract_name").GetString());
        Assert.AreEqual("Chummer4", evidence.GetProperty("legacySubject").GetString());
        Assert.AreEqual("chummer4", evidence.GetProperty("legacySubjectSlug").GetString());
        StringAssert.Contains(evidence.GetProperty("sourcePaths").GetRawText(), "/docker/fleet/repos/chummer4/Chummer");
        Assert.IsTrue(evidence.GetProperty("legacyDesignerEventHookCount").GetInt32() > 1000);
        Assert.IsTrue(evidence.GetProperty("legacyRuntimeEventHookCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("legacyDynamicInteractiveElementCount").GetInt32() > 1000);
        Assert.IsTrue(evidence.GetProperty("currentDynamicInteractiveElementCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("currentNamedAxamlInteractiveElementCount").GetInt32() > 0);
        Assert.IsTrue(evidence.GetProperty("legacyElementDispositionCount").GetInt32() > 2500);
        Assert.AreEqual(0, evidence.GetProperty("unclassifiedLegacyEventCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("unclassifiedLegacyDynamicElementCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("missingLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("familyFallbackLegacyElementDispositionCount").GetInt32());
        Assert.AreEqual(0, evidence.GetProperty("failureCount").GetInt32());
        Assert.IsTrue(evidence.GetProperty("wiredIntoStandardVerify").GetBoolean());
        Assert.IsTrue(evidence.GetProperty("b14ConsumesReceipt").GetBoolean());

        string receiptText = root.GetRawText();
        StringAssert.Contains(receiptText, "\"tools_utilities\"");
        StringAssert.Contains(receiptText, "\"magic_matrix\"");
        StringAssert.Contains(receiptText, "\"inventory_progression\"");
        StringAssert.Contains(receiptText, "\"dynamicElementReview\"");
        Assert.AreEqual("pass", root.GetProperty("legacyExtractionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("currentMappingReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("legacyElementDispositionReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("dynamicElementReview").GetProperty("status").GetString());
        Assert.AreEqual("pass", root.GetProperty("executionReview").GetProperty("status").GetString());
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
