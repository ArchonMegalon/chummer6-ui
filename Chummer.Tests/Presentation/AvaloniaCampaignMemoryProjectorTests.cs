#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class AvaloniaCampaignMemoryProjectorTests
{
    [TestMethod]
    public void Desktop_home_campaign_memory_surface_prefers_black_ledger_and_portable_consequence_contracts()
    {
        string repoRoot = FindRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"));

        StringAssert.Contains(source, "BuildCampaignConsequenceSummary()");
        StringAssert.Contains(source, "BuildCampaignConsequenceEvidenceSummary()");
        StringAssert.Contains(source, "BuildCampaignNextSessionReturnSummary()");
        StringAssert.Contains(source, "BuildCampaignReturnActionSummary()");
        StringAssert.Contains(source, "ResolveCampaignMemorySummary()");
        StringAssert.Contains(source, "ResolveCampaignMemoryEvidence()");
        StringAssert.Contains(source, "ResolveCampaignMemoryNextSafeAction()");

        StringAssert.Contains(source, "BLACK LEDGER consequence:");
        StringAssert.Contains(source, "Campaign consequence summary:");
        StringAssert.Contains(source, "BLACK LEDGER consequence details:");
        StringAssert.Contains(source, "Campaign adoption details:");
        StringAssert.Contains(source, "Campaign consequence details: no consequence details are available.");
        StringAssert.Contains(source, "Review next-session return action:");
    }

    [TestMethod]
    public void Desktop_home_campaign_memory_surface_keeps_return_lane_and_empty_state_copy_visible()
    {
        string repoRoot = FindRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"));

        StringAssert.Contains(source, "Campaign next-session return:");
        StringAssert.Contains(source, "Campaign next-session return: no return summary is currently projected.");
        StringAssert.Contains(source, "Campaign consequence summary: no consequence summary is currently projected.");
        StringAssert.Contains(source, "Review next-session return action: no next-session return action is currently projected.");
        StringAssert.Contains(source, "Campaign-ready lane:");
        StringAssert.Contains(source, "Campaign memory details:");
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        for (int index = 0; index < 8; index += 1)
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        Assert.Fail("Could not locate repository root from test base directory.");
        return string.Empty;
    }
}
