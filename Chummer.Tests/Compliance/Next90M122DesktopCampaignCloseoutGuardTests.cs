#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M122DesktopCampaignCloseoutGuardTests
{
    [TestMethod]
    public void M122_desktop_campaign_closeout_guard_is_wired_into_standard_verify()
    {
        string repoRoot = FindRepoRoot();
        string verifyScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "verify.sh"));
        string projectText = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj"));
        string scriptText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m122-ui-desktop-campaign-closeout-check.sh"));

        StringAssert.Contains(verifyScript, "checking next-90 M122 desktop campaign closeout guard");
        StringAssert.Contains(verifyScript, "bash scripts/ai/milestones/next90-m122-ui-desktop-campaign-closeout-check.sh");
        StringAssert.Contains(projectText, "Compliance\\Next90M122DesktopCampaignCloseoutGuardTests.cs");

        StringAssert.Contains(scriptText, "PACKAGE_ID = \"next90-m122-ui-surface-campaign-adoption-runner-goal-pins-resolutionrep\"");
        StringAssert.Contains(scriptText, "WORK_TASK_ID = \"122.3\"");
        StringAssert.Contains(scriptText, "\"surface_campaign_adoption_runner_goal:ui\"");
        StringAssert.Contains(scriptText, "\"BuildCampaignAdoptionSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildRunnerGoalPinSummary()\"");
        StringAssert.Contains(scriptText, "\"BuildResolutionReportCloseoutSummary()\"");
        StringAssert.Contains(scriptText, "\"Chummer.Presentation/Overview/DesktopHomeCampaignProjector.cs\"");
        StringAssert.Contains(scriptText, "\"Adoption proof:\"");
        StringAssert.Contains(scriptText, "\"BLACK LEDGER consequence proof:\"");
        StringAssert.Contains(scriptText, "\"Campaign adoption proof:\"");
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
