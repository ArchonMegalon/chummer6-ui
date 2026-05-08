#nullable enable annotations

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M141UiImportRouteProofGuardTests
{
    [TestMethod]
    public void M141_import_route_guard_pins_queue_identity_and_route_proof_markers()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m141-ui-import-route-proof-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m141-ui-capture-direct-screenshot-and-runtime-proof-for-translator-xml-amendment\"");
        StringAssert.Contains(scriptText, "TITLE = \"Capture direct screenshot and runtime proof for translator, XML amendment editor, Hero Lab importer, and adjacent import-oracle routes.\"");
        StringAssert.Contains(scriptText, "owner: chummer6-ui");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "\"capture_direct_screenshot_and_runtime_proof_for_translat:ui\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m141-ui-import-route-proof-check.sh\"");
        StringAssert.Contains(scriptText, "\"38-translator-dialog-light.png\"");
        StringAssert.Contains(scriptText, "\"39-xml-editor-dialog-light.png\"");
        StringAssert.Contains(scriptText, "\"40-hero-lab-importer-dialog-light.png\"");
        StringAssert.Contains(scriptText, "\"translator_xml_custom_data\"");
        StringAssert.Contains(scriptText, "\"hero_lab_import_oracle\"");
        StringAssert.Contains(scriptText, "\"Runtime_backed_translator_xml_editor_and_hero_lab_importer_routes_surface_governed_posture\"");
        StringAssert.Contains(scriptText, "\"ExecuteCommandAsync_translator_opens_dialog_with_master_index_lane_posture\"");
        StringAssert.Contains(scriptText, "\"ExecuteCommandAsync_xml_editor_opens_dialog_with_xml_bridge_posture\"");
        StringAssert.Contains(scriptText, "\"ExecuteCommandAsync_hero_lab_importer_opens_dialog_with_import_oracle_lane_posture\"");
        StringAssert.Contains(scriptText, "\"Avalonia_and_Blazor_translator_and_xml_editor_dialogs_preserve_matching_lane_posture\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs\": [");
        StringAssert.Contains(scriptText, "\"ImportRouteReviewSteps\"");
        StringAssert.Contains(scriptText, "\"Chummer.Presentation/Overview/DesktopDialogFactory.cs\": [");
        StringAssert.Contains(scriptText, "\"xmlEditorXmlBridgePosture\"");
        StringAssert.Contains(scriptText, "\"heroLabImportOracleLanePosture\"");
        StringAssert.Contains(scriptText, "\"scripts/ai/milestones/b14-flagship-ui-release-gate.sh\": [");
        StringAssert.Contains(scriptText, "\"checks\"");
        StringAssert.Contains(scriptText, "\"sourceChecks\"");
        StringAssert.Contains(scriptText, "\"reviewSurfaceOrder\"");
        StringAssert.Contains(scriptText, "\"expectedScreenshots\"");
        StringAssert.Contains(scriptText, "\"runtimeProofTokens\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not failed else \"fail\"");
    }

    private static string FindRepoRoot()
    {
        string? current = TestContext.DeploymentDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate repository root from deployment directory.");
        return string.Empty;
    }

    public TestContext TestContext { get; set; } = null!;
}
