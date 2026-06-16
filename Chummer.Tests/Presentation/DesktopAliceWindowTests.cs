using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopAliceWindowTests
{
    [TestMethod]
    public void DesktopAliceWindow_source_uses_build_handoffs_and_account_alice_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "GetAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "GetBuildPathSuggestionsAsync");
        StringAssert.Contains(source, "GetBuildPathPreviewAsync");
        StringAssert.Contains(source, "BuildLabHandoffs");
        StringAssert.Contains(source, "HasHandoffContext");
        StringAssert.Contains(source, "HasBuildPathContext");
        StringAssert.Contains(source, "AliceBuildPathCombo");
        StringAssert.Contains(source, "AliceProposalModeCombo");
        StringAssert.Contains(source, "OrderByDescending(item => item.UpdatedAtUtc)");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/alice\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/alice\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal($\"/account/alice/{Uri.EscapeDataString(lead.HandoffId)}\")");
    }

    [TestMethod]
    public void DesktopAliceWindow_source_keeps_ruleset_aware_build_path_resolution_for_sr4()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "ResolveRulesetId(workspaces)");
        StringAssert.Contains(source, "RulesetDefaults.NormalizeOptional(workspaces.FirstOrDefault()?.RulesetId)");
        StringAssert.Contains(source, "GetBuildPathSuggestionsAsync(effectiveRulesetId, CancellationToken.None)");
        StringAssert.Contains(source, "GetBuildPathPreviewAsync(");
        StringAssert.Contains(source, "Build path compare stays native");
        StringAssert.Contains(source, "No preview-backed build path suggestions are currently available");
    }
}
