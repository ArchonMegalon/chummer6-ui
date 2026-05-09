#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M112CampaignMemoryGuardTests
{
    [TestMethod]
    public void M112_campaign_memory_guard_pins_queue_identity_and_desktop_markers()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(
            repoRoot,
            "scripts",
            "ai",
            "milestones",
            "next90-m112-ui-campaign-memory-check.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m112-ui-campaign-memory\"");
        StringAssert.Contains(scriptText, "TITLE = \"Surface campaign memory and consequences on desktop\"");
        StringAssert.Contains(scriptText, "TASK = \"Make campaign consequences, stale state, and next-session return actions visible on the promoted desktop route.\"");
        StringAssert.Contains(scriptText, "owner: chummer6-ui");
        StringAssert.Contains(scriptText, "EXPECTED_ALLOWED_PATHS = [");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia\"");
        StringAssert.Contains(scriptText, "\"Chummer.Desktop.Runtime\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests\"");
        StringAssert.Contains(scriptText, "\"scripts\"");
        StringAssert.Contains(scriptText, "EXPECTED_SURFACES = [");
        StringAssert.Contains(scriptText, "\"campaign_workspace:memory\"");
        StringAssert.Contains(scriptText, "\"campaign_return_loop:desktop\"");
        StringAssert.Contains(scriptText, "EXPECTED_DIRECT_PROOF_COMMAND = \"bash scripts/ai/milestones/next90-m112-ui-campaign-memory-check.sh\"");
        StringAssert.Contains(scriptText, "EXPECTED_TARGETED_TEST_COMMAND = 'dotnet test Chummer.Tests/Chummer.Tests.csproj --filter \"Next90M112CampaignMemoryGuardTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_PRESENTATION_TEST_COMMAND = 'dotnet test Chummer.Tests/Presentation/Chummer.Presentation.Signoff.Tests.csproj --filter \"AccessibilitySignoffSmokeTests\" --no-restore'");
        StringAssert.Contains(scriptText, "EXPECTED_DESIGN_QUEUE_PATH = \"/docker/chummercomplete/chummer-design/products/chummer/NEXT_90_DAY_QUEUE_STAGING.generated.yaml\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopHomeWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceEvidenceSummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryEvidence()\"");
        StringAssert.Contains(scriptText, "\"Review campaign consequences\"");
        StringAssert.Contains(scriptText, "\"Review next-session return\"");
        StringAssert.Contains(scriptText, "\"Campaign consequence proof:\"");
        StringAssert.Contains(scriptText, "\"Chummer.Avalonia/DesktopCampaignWorkspaceWindow.cs\": [");
        StringAssert.Contains(scriptText, "\"BuildCampaignNextSessionReturnActionSummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemorySummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryReturnSummary()\"");
        StringAssert.Contains(scriptText, "\"ResolveCampaignMemoryNextSafeAction()\"");
        StringAssert.Contains(scriptText, "\"Chummer.Tests/Presentation/AccessibilitySignoffSmokeTests.cs\": [");
        StringAssert.Contains(scriptText, "\"BuildCampaignConsequenceEvidenceSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignNextSessionReturnActionSummary()\"");
        StringAssert.Contains(scriptText, "\"registry_has_m112_ui_task\"");
        StringAssert.Contains(scriptText, "\"queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"design_queue_package_unique\"");
        StringAssert.Contains(scriptText, "\"allowed_paths_exact\"");
        StringAssert.Contains(scriptText, "\"owned_surfaces_exact\"");
        StringAssert.Contains(scriptText, "\"design_queue_path_matches\"");
        StringAssert.Contains(scriptText, "\"proofCommands\"");
        StringAssert.Contains(scriptText, "\"proofFiles\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not failed else \"fail\"");
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
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
}
