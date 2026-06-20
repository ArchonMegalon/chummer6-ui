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
        StringAssert.Contains(source, "ShowOriginDraftAsync");
        StringAssert.Contains(source, "AliceModeGuideText");
        StringAssert.Contains(source, "AliceGmAllowanceGuideText");
        StringAssert.Contains(source, "AliceGmAllowanceTextBox");
        StringAssert.Contains(source, "global::Avalonia.Automation.AutomationProperties.SetName(promptBox, \"Ask Alice\")");
        StringAssert.Contains(source, "global::Avalonia.Automation.AutomationProperties.SetHelpText(promptBox");
        StringAssert.Contains(source, "ToolTip.SetTip(promptBox, null)");
        StringAssert.Contains(source, "global::Avalonia.Automation.AutomationProperties.SetName(gmAllowanceBox, \"GM notes for Alice\")");
        StringAssert.Contains(source, "global::Avalonia.Automation.AutomationProperties.SetHelpText(gmAllowanceBox");
        StringAssert.Contains(source, "ToolTip.SetTip(gmAllowanceBox, null)");
        StringAssert.Contains(source, "AliceConversationList");
        StringAssert.Contains(source, "AliceStarterPromptRow");
        StringAssert.Contains(source, "AliceQuestionTextBox");
        StringAssert.Contains(source, "AliceAskButton");
        StringAssert.Contains(source, "BuildConversationTurnView(");
        StringAssert.Contains(source, "BuildStarterPrompts(");
        StringAssert.Contains(source, "OriginDossierMode");
        StringAssert.Contains(source, "BuildNarrativePacket(");
        StringAssert.Contains(source, "BuildSeededAssistantMessage(");
        StringAssert.Contains(source, "OriginDossierBundle");
        StringAssert.Contains(source, "AliceOriginWizardPanel");
        StringAssert.Contains(source, "AliceOriginMetatypeCombo");
        StringAssert.Contains(source, "AliceOriginArchetypeCombo");
        StringAssert.Contains(source, "AliceOriginAdvancedStoryControlsExpander");
        StringAssert.Contains(source, "IsExpanded = false");
        StringAssert.Contains(source, "Build story");
        StringAssert.Contains(source, "Build the story first");
        StringAssert.Contains(source, "AliceGmAllowanceExpander");
        StringAssert.Contains(source, "AliceOriginBuildFrameCombo");
        StringAssert.Contains(source, "AliceOriginPressureCombo");
        StringAssert.Contains(source, "AliceOriginGmRequirementPresetCombo");
        StringAssert.Contains(source, "AliceOriginStartDossierButton");
        StringAssert.Contains(source, "BuildOriginStarterPrompt(");
        StringAssert.Contains(source, "BuildOriginMetatypeOptions()");
        StringAssert.Contains(source, "ResolveOriginMetatypeHint()");
        StringAssert.Contains(source, "Alice should use the finished story as the seed for later suggestions.");
        StringAssert.Contains(source, "Approved origin story prose");
        StringAssert.Contains(source, "Origin story draft prose");
        StringAssert.Contains(source, "starterPromptRow.IsVisible = !IsOriginDossierMode");
        StringAssert.Contains(source, "EnsureOriginDraftReviewPacket(");
        StringAssert.Contains(source, "BuildOriginDraftReviewEvidence()");
        StringAssert.Contains(source, "fliplink-origin-story.packet.json");
        StringAssert.Contains(source, "Open story");
        StringAssert.Contains(source, "Open FlipLink handoff");
        StringAssert.Contains(source, "\"Troll\"");
        StringAssert.Contains(source, "\"Decker\"");
        StringAssert.Contains(source, "SR4 BP");
        StringAssert.Contains(source, "Matrix identity theft");
        StringAssert.Contains(source, "Must be addicted to an illegal drug");
        StringAssert.Contains(source, "Must have Logic or Intuition 2+");
        StringAssert.Contains(source, "Approve story");
        StringAssert.Contains(source, "Render dossier PDF");
        StringAssert.Contains(source, "Create portraits");
        StringAssert.Contains(source, "Create scenes");
        StringAssert.Contains(source, "Create default voice script");
        StringAssert.Contains(source, "Create alternate voice script");
        StringAssert.Contains(source, "Prepare render request");
        StringAssert.Contains(source, "Create dossier video");
        StringAssert.Contains(source, "MarkupGo");
        StringAssert.Contains(source, "FlipLink");
        StringAssert.Contains(source, "Soundmadeseen");
        StringAssert.Contains(source, "Unmixr");
        StringAssert.Contains(source, "vidBoard");
        StringAssert.Contains(source, "EnsureOriginPortraitSet(");
        StringAssert.Contains(source, "EnsureOriginSceneSet(");
        StringAssert.Contains(source, "EnsureOriginDossierVideoPacket(");
        StringAssert.Contains(source, "SelectOriginPortrait(");
        StringAssert.Contains(source, "SelectOriginScene(");
        StringAssert.Contains(source, "PortraitCandidatePaths");
        StringAssert.Contains(source, "SelectedPortraitPath");
        StringAssert.Contains(source, "SceneCandidatePaths");
        StringAssert.Contains(source, "SelectedScenePath");
        StringAssert.Contains(source, "VideoStoryboardPath");
        StringAssert.Contains(source, "VidBoardPacketPath");
        StringAssert.Contains(source, "VideoPosterPath");
        StringAssert.Contains(source, "RenderControlToPng(");
        StringAssert.Contains(source, "BuildOriginPortraitCard(");
        StringAssert.Contains(source, "BuildOriginSceneCard(");
        StringAssert.Contains(source, "BuildOriginVideoPoster(");
        StringAssert.Contains(source, "AliceOriginGeneratePortraitSetButton");
        StringAssert.Contains(source, "AliceOriginGenerateSceneSetButton");
        StringAssert.Contains(source, "AliceOriginGenerateDossierVideoButton");
        StringAssert.Contains(source, "AliceOriginSelectPortrait1Button");
        StringAssert.Contains(source, "AliceOriginSelectScene1Button");
        StringAssert.Contains(source, "EnsureUnmixrNarrationPacket(");
        StringAssert.Contains(source, "EnsureOriginMediaFactoryNarrationRequest(");
        StringAssert.Contains(source, "media-factory-origin-audiobook.request.json");
        StringAssert.Contains(source, "BuildOriginMediaFactoryNarrationRunbook(");
        StringAssert.Contains(source, "ownerRepo = \"chummer6-media-factory\"");
        StringAssert.Contains(source, "AliceOriginGenerateAlternateAudiobookPacketButton");
        StringAssert.Contains(source, "AliceOriginGenerateMediaFactoryNarrationRequestButton");
        StringAssert.Contains(source, "RenderOriginAudiobookNowAsync");
        StringAssert.Contains(source, "RenderOriginDossierVideoNowAsync");
        StringAssert.Contains(source, "AliceOriginRenderAudiobookNowButton");
        StringAssert.Contains(source, "AliceOriginRenderDossierVideoNowButton");
        StringAssert.Contains(source, "AliceOriginOpenMediaFactoryNarrationReceiptButton");
        StringAssert.Contains(source, "AliceOriginOpenMediaFactoryVideoReceiptButton");
        StringAssert.Contains(source, "AliceOriginOpenRenderedVideoButton");
        StringAssert.Contains(source, "ExecuteOriginMediaFactoryNarrationAsync(");
        StringAssert.Contains(source, "ExecuteOriginDossierVideoAsync(");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_REQUEST_PATH");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_VIDEO_REQUEST_PATH");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_REPO_ROOT");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_NARRATION_CLI_PROJECT");
        StringAssert.Contains(source, "CHUMMER_MEDIA_FACTORY_ORIGIN_DOSSIER_VIDEO_CLI_PROJECT");
        StringAssert.Contains(source, "ResolveMediaFactoryRepoRoot(");
        Assert.IsFalse(source.Contains("/docker/fleet/repos/chummer-media-factory", StringComparison.Ordinal));
        StringAssert.Contains(source, "MediaFactoryNarrationReceiptPath");
        StringAssert.Contains(source, "MediaFactoryVideoReceiptPath");
        StringAssert.Contains(source, "RenderedVideoPath");
        StringAssert.Contains(source, "BuildSimplePdfDocument(");
        StringAssert.Contains(source, "Strict avoids restricted picks");
        StringAssert.Contains(source, "Simple stays obvious");
        StringAssert.Contains(source, "Ware suggestions include the rules tradeoff");
        StringAssert.Contains(source, "Finished characters are not changed.");
        StringAssert.Contains(source, "GM notes can require addiction");
        StringAssert.Contains(source, "Must be addicted to an illegal drug");
        StringAssert.Contains(source, "Must be magically active");
        StringAssert.Contains(source, "attribute floors");
        StringAssert.Contains(source, "Clear GM notes");
        StringAssert.Contains(source, "GM notes:");
        StringAssert.Contains(source, "## GM Notes");
        StringAssert.Contains(source, "gmAllowanceNotes");
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
        StringAssert.Contains(source, "Proposal studio");
        StringAssert.Contains(source, "No build suggestions are available");
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

    [TestMethod]
    public void DesktopAliceWindow_source_supports_blank_state_build_help_without_workspace_dead_end()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopAliceWindow.cs"));

        StringAssert.Contains(source, "BuildScratchCharacterAnswer(");
        StringAssert.Contains(source, "BuildScratchCharacterEvidence(");
        StringAssert.Contains(source, "AliceDraftFromScratchButton");
        StringAssert.Contains(source, "Build a complete SR4 BP troll decker from scratch.");
        StringAssert.Contains(source, "InferScratchRuleset(");
        StringAssert.Contains(source, "return \"Karma\";");
        StringAssert.Contains(source, "Blank-state start is supported.");
        StringAssert.Contains(source, "No open workspace is required to draft a first full build proposal.");
        StringAssert.Contains(source, "Use this as a complete first draft");
        StringAssert.Contains(source, "Alice can draft a complete runner from the current settings even when no workspace is open.");
    }

    [TestMethod]
    public void PlayerFacingCopyHumanizer_removes_provider_and_proof_language_from_visible_copy()
    {
        string cleaned = Chummer.Presentation.PlayerFacingCopyHumanizer.Clean(
            "ALICE generated proofs and an Unmixr AI narration receipt from the approved origin canon through a media-factory provider lane after validation checks, audit verdict, registry posture, and available follow-up.");

        StringAssert.Contains(cleaned, "Alice");
        StringAssert.Contains(cleaned, "Unmixr");
        StringAssert.Contains(cleaned, "details");
        StringAssert.Contains(cleaned, "record");
        StringAssert.Contains(cleaned, "approved origin story");
        StringAssert.Contains(cleaned, "review");
        StringAssert.Contains(cleaned, "review decision");
        StringAssert.Contains(cleaned, "app record status");
        StringAssert.Contains(cleaned, "available");
        Assert.IsFalse(cleaned.Contains("Unmixr AI", StringComparison.Ordinal));
        Assert.IsFalse(cleaned.Contains("generated", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("detailss", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("media-factory", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("provider", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("validation", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("audit", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("verdict", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("registry", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(cleaned.Contains("posture", StringComparison.OrdinalIgnoreCase));
    }
}
