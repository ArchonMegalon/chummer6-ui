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
        StringAssert.Contains(source, "CreateAssistantCard()");
        StringAssert.Contains(source, "AliceConversationModeCombo");
        StringAssert.Contains(source, "AliceConversationList");
        StringAssert.Contains(source, "AliceStarterPromptRow");
        StringAssert.Contains(source, "AliceQuestionTextBox");
        StringAssert.Contains(source, "AliceAskButton");
        StringAssert.Contains(source, "BuildConversationTurnView(");
        StringAssert.Contains(source, "BuildStarterPrompts(");
        StringAssert.Contains(source, "\"Origin draft\"");
        StringAssert.Contains(source, "BuildNarrativePacket(");
        StringAssert.Contains(source, "BuildSeededAssistantMessage(");
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

    [TestMethod]
    public void DesktopAliceWindow_source_routes_rules_and_build_questions_through_coach_sidecar()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "IAvaloniaCoachSidecarClient");
        StringAssert.Contains(source, "AiConversationTurnRequest");
        StringAssert.Contains(source, "SendCoachTurnAsync");
        StringAssert.Contains(source, "SendBuildTurnAsync");
        StringAssert.Contains(source, "AiRouteTypes.Coach");
        StringAssert.Contains(source, "AiRouteTypes.Build");
    }
}
