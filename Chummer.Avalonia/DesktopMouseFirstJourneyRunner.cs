using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using System.IO;
using System.Runtime.CompilerServices;

namespace Chummer.Avalonia;

internal static class DesktopMouseFirstJourneyRunner
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan WorkspacePublishedWaitTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WorkspaceSaveWaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JourneyTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan HardTimeoutGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TransitionSettleTimeout = TimeSpan.FromSeconds(2);
    public static async Task RunAsync(MainWindow window, string headId)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopMouseFirstJourneyContext context = DesktopMouseFirstJourneyRuntime.BuildContext(headId, DateTimeOffset.UtcNow);
        DesktopMouseFirstJourneyPlan plan = DesktopMouseFirstJourneyRuntime.ReadPlan();
        List<string> steps = [];
        List<string> screenshotPaths = [];
        List<DesktopUserJourneyWorkflowEvidence> userJourneyWorkflows = [];
        List<DesktopMouseFirstJourneyObservedInputEvent> observedInputEvents = [];
        int pointerActionCount = 0;
        int textEntryActionCount = 0;
        int directTextMutationCount = 0;
        bool usedForcedComboDropdownOpen = false;
        bool usedComboSelectionFallback = false;
        MouseFirstJourneyAuthenticationPortalState authenticationPortalState = new();
        using ObservedInputTraceCollector inputTraceCollector = new(window, observedInputEvents);
        Task journeyTask = RunJourneyAsync(
            window,
            context,
            plan,
            steps,
            screenshotPaths,
            userJourneyWorkflows,
            inputTraceCollector,
            observedInputEvents,
            () => pointerActionCount++,
            () => textEntryActionCount++,
            () => usedForcedComboDropdownOpen = true,
            () => usedComboSelectionFallback = true,
            () => pointerActionCount,
            () => textEntryActionCount,
            () => directTextMutationCount,
            () => usedForcedComboDropdownOpen,
            () => usedComboSelectionFallback,
            authenticationPortalState);
        Task completedTask = await Task.WhenAny(journeyTask, Task.Delay(JourneyTimeout + HardTimeoutGrace));
        if (!ReferenceEquals(completedTask, journeyTask))
        {
            RecordStep(steps, "hard timeout expired before journey completed");
            DesktopMouseFirstJourneyRuntime.WriteObservedInputTrace(context, observedInputEvents);
            DesktopMouseFirstJourneyRuntime.WriteFailureArtifacts(
                context,
                new TimeoutException($"Desktop mouse-first journey exceeded hard timeout of {(JourneyTimeout + HardTimeoutGrace).TotalSeconds:0} seconds."),
                steps,
                screenshotPaths: screenshotPaths,
                pointerActionCount: pointerActionCount,
                textEntryActionCount: textEntryActionCount,
                directTextMutationCount: directTextMutationCount,
                usedForcedComboDropdownOpen: usedForcedComboDropdownOpen,
                usedComboSelectionFallback: usedComboSelectionFallback,
                scenarioId: plan.ScenarioId,
                buildMethod: plan.BuildMethod,
                metatypeCategory: plan.MetatypeCategory,
                priorityHeritage: plan.PriorityHeritage,
                metatype: plan.Metatype,
                priorityTalent: plan.PriorityTalent,
                priorityTalentChoice: plan.PriorityTalentChoice,
                authenticationPortalOpened: authenticationPortalState.Opened,
                authenticationPortalUri: authenticationPortalState.Uri,
                observedInputEvents: observedInputEvents);
            Console.Error.WriteLine("Desktop mouse-first journey failed: hard timeout expired.");
            Environment.Exit(1);
            return;
        }

        await journeyTask;
    }

    private static async Task RunJourneyAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        DesktopMouseFirstJourneyPlan plan,
        List<string> steps,
        List<string> screenshotPaths,
        List<DesktopUserJourneyWorkflowEvidence> userJourneyWorkflows,
        ObservedInputTraceCollector inputTraceCollector,
        List<DesktopMouseFirstJourneyObservedInputEvent> observedInputEvents,
        Action recordPointerAction,
        Action recordTextEntryAction,
        Action markForcedComboDropdownOpen,
        Action markSelectionFallback,
        Func<int> getPointerActionCount,
        Func<int> getTextEntryActionCount,
        Func<int> getDirectTextMutationCount,
        Func<bool> getUsedForcedComboDropdownOpen,
        Func<bool> getUsedComboSelectionFallback,
        MouseFirstJourneyAuthenticationPortalState authenticationPortalState)
    {
        try
        {
            using CancellationTokenSource journeyTimeout = new(JourneyTimeout);
            DesktopMouseFirstJourneyRuntime.PrepareUserJourneyTraceOutput(context);
            bool produceUserJourneyTrace = !string.IsNullOrWhiteSpace(context.UserJourneyTraceOutputPath);
            string language = DesktopLocalizationCatalog.GetCurrentLanguage();
            string expectedCharacterName = plan.CharacterName;
            string expectedCharacterAlias = plan.CharacterAlias;
            string expectedRulesetId = plan.RulesetId;
            RecordStep(steps, $"start mouse-first live binary journey ({plan.ScenarioId})");
            string initialWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);
            string? initialWorkspaceId = ReadActiveWorkspaceId(window);
            IReadOnlyList<OpenWorkspaceState> initialOpenWorkspaces = ReadOpenWorkspaces(window);
            DesktopMouseFirstJourneyVisibleShellState initialVisibleState = ReadVisibleShellState(window, language);

            await WaitForAsync(
                steps,
                "desktop shell initialized",
                () => window.IsVisible
                    && window.Bounds.Width > 0d
                    && window.Bounds.Height > 0d
                    && window.ControlsForAutomation.MenuBar is not null
                    && window.ControlsForAutomation.CommandDialogPane is not null,
                journeyTimeout.Token);

            try
            {
                DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState("avalonia");
                if (DesktopInstallLinkingRuntime.ShouldPromptForStartup(installState))
                {
                    string relativeClaimPath = DesktopInstallLinkingRuntime.BuildClaimPortalRelativePathForInstall(installState);
                    authenticationPortalState.Uri = DesktopInstallLinkingRuntime.BuildPublicPortalAbsoluteUri(relativeClaimPath);
                    authenticationPortalState.Opened = DesktopInstallLinkingRuntime.TryOpenClaimPortalForInstall(installState);
                    if (authenticationPortalState.Opened)
                    {
                        await Task.Delay(300, journeyTimeout.Token);
                    }
                }
                else
                {
                    authenticationPortalState.Uri = null;
                    authenticationPortalState.Opened = false;
                }
                string portalStep = authenticationPortalState.Opened
                    ? "open authentication portal (success)"
                    : authenticationPortalState.Uri is null
                        ? "skip authentication portal (not required for current release channel)"
                        : "open authentication portal (failed)";
                RecordStep(steps, portalStep);
            }
            catch (Exception ex)
            {
                authenticationPortalState.Opened = false;
                authenticationPortalState.Uri = null;
                RecordStep(steps, $"authentication portal open failed: {ex.Message}");
            }

            if (produceUserJourneyTrace)
            {
                userJourneyWorkflows.Add(await ExerciseMasterIndexSearchWorkflowAsync(
                    window,
                    context,
                    steps,
                    screenshotPaths,
                    inputTraceCollector,
                    journeyTimeout.Token,
                    recordPointerAction,
                    recordTextEntryAction));
            }

            await ClickFileMenuCommandAsync(window, "new_character", steps, journeyTimeout.Token, recordPointerAction);
            await WaitForDialogAsync(window, "dialog.new_character", steps, journeyTimeout.Token);
            inputTraceCollector.ObserveDialogWindow(ResolveVisibleDialogWindow());
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "01-new-character-dialog");
            string? fileNewBeforeScreenshot = produceUserJourneyTrace
                ? await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "file_new_character_visible_workspace-before")
                : null;

            await SetDialogTextFieldAsync(window, "newCharacterName", expectedCharacterName, steps, journeyTimeout.Token);
            recordTextEntryAction();
            await SetDialogTextFieldAsync(window, "newCharacterAlias", expectedCharacterAlias, steps, journeyTimeout.Token);
            recordTextEntryAction();
            await SetDialogSelectFieldAsync(
                window,
                "newCharacterRulesetId",
                plan.RulesetId,
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                markForcedComboDropdownOpen,
                markSelectionFallback);
            await SetDialogSelectFieldAsync(
                window,
                "newCharacterBuildMethod",
                plan.BuildMethod,
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                markForcedComboDropdownOpen,
                markSelectionFallback);
            await ClickDialogActionUntilAsync(
                window,
                "create_character",
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                "new character continuation dialog rendered",
                () =>
                {
                    Visual dialogRoot = ResolveDialogRoot(window);
                    return FindVisibleDescendant<Button>(
                               dialogRoot,
                               DesktopDialogAccessibility.BuildActionName("complete_new_character_workflow")) is not null;
                });
            inputTraceCollector.ObserveDialogWindow(ResolveVisibleDialogWindow());
            string continuationScreenshotStem = string.Equals(plan.BuildMethod, "Priority", StringComparison.OrdinalIgnoreCase)
                ? "02-priority-workflow"
                : "02-karma-workflow";
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, continuationScreenshotStem);

            if (string.Equals(plan.BuildMethod, "Priority", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(plan.MetatypeCategory))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterMetatypeCategory",
                        plan.MetatypeCategory,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                if (!string.IsNullOrWhiteSpace(plan.PriorityHeritage))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterPriorityHeritage",
                        plan.PriorityHeritage,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                if (!string.IsNullOrWhiteSpace(plan.PriorityTalent))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterPriorityTalent",
                        plan.PriorityTalent,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                if (!string.IsNullOrWhiteSpace(plan.PriorityTalentChoice))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterPriorityTalentChoice",
                        plan.PriorityTalentChoice,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                if (!string.IsNullOrWhiteSpace(plan.Metatype))
                {
                    await SetDialogListFieldAsync(
                        window,
                        "newCharacterMetatype",
                        plan.Metatype,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markSelectionFallback);
                }

                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02b-priority-configured");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(plan.MetatypeCategory))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterMetatypeCategory",
                        plan.MetatypeCategory,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                if (!string.IsNullOrWhiteSpace(plan.Metatype))
                {
                    await SetDialogSelectFieldAsync(
                        window,
                        "newCharacterMetatype",
                        plan.Metatype,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction,
                        markForcedComboDropdownOpen,
                        markSelectionFallback);
                }

                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02b-karma-configured");
            }

            await ClickDialogActionUntilAsync(
                window,
                "complete_new_character_workflow",
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                "workspace creation dialog closed after mouse-first creation flow",
                () => ResolveVisibleDialogWindow() is null);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03-post-dialog-close");
            OpenWorkspaceState? createdWorkspaceState = null;
            await WaitForAsync(
                steps,
                "character workspace published after mouse-first creation flow",
                () =>
                {
                    bool hasOpened = HasOpenedCharacterEvidence(
                        window,
                        language,
                        expectedCharacterName,
                        expectedCharacterAlias,
                        expectedRulesetId,
                        initialWorkspaceId,
                        initialWorkspaceStripText,
                        initialVisibleState,
                        initialOpenWorkspaces,
                        out OpenWorkspaceState? openedWorkspaceState);
                    createdWorkspaceState = openedWorkspaceState;
                    return hasOpened;
                },
                journeyTimeout.Token,
                WorkspacePublishedWaitTimeout);
            string openedWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);
            DesktopMouseFirstJourneyVisibleShellState openedVisibleState = ReadVisibleShellState(window, language);
            IReadOnlyList<OpenWorkspaceState> openedWorkspaces = ReadOpenWorkspaces(window);
            createdWorkspaceState ??= ResolveCreatedWorkspaceState(
                initialOpenWorkspaces,
                openedWorkspaces,
                expectedCharacterName,
                expectedCharacterAlias,
                expectedRulesetId,
                expectedWorkspaceId: null);
            string? createdWorkspaceId = createdWorkspaceState?.Id.Value
                ?? ReadActiveWorkspaceId(window);
            RecordStep(
                steps,
                HasWorkspaceStripTransition(initialWorkspaceStripText, openedWorkspaceStripText)
                    ? $"workspace strip changed to {openedWorkspaceStripText}"
                    : $"workspace strip stayed stable while live shell content confirmed opened character {expectedCharacterName} ({openedVisibleState.RulesetId ?? expectedRulesetId})");
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "04-workspace-opened");

            if (produceUserJourneyTrace)
            {
                await WaitForAsync(
                    steps,
                    "seeded character workspace projection visible",
                    () =>
                    {
                        CharacterOverviewState state = window.SnapshotStateForAutomation();
                        return state.Profile is not null
                            && state.Build is not null
                            && !string.IsNullOrWhiteSpace(state.ActiveSectionJson)
                            && string.IsNullOrWhiteSpace(state.Error);
                    },
                    journeyTimeout.Token);
                CharacterOverviewState openedOverviewState = window.SnapshotStateForAutomation();
                Control? sectionReviewPanel = window.ControlsForAutomation.SectionHost
                    .GetVisualDescendants()
                    .OfType<Control>()
                    .FirstOrDefault(control => string.Equals(control.Name, "SectionReviewPanel", StringComparison.Ordinal));
                bool profileIdentityMatches = openedOverviewState.Profile is not null
                    && string.Equals(openedOverviewState.Profile.Name, expectedCharacterName, StringComparison.Ordinal)
                    && string.Equals(openedOverviewState.Profile.Alias, expectedCharacterAlias, StringComparison.Ordinal);
                bool newCharacterActionOpenedVisibleWorkspace = !string.IsNullOrWhiteSpace(createdWorkspaceId)
                    && openedVisibleState.HasActiveWorkspace
                    && profileIdentityMatches;
                bool starterAttributesMatchSeededWorkspace = openedOverviewState.Build is { TotalAttributes: 28 };
                bool sectionPreviewOmitsReviewCopy = sectionReviewPanel is null || !sectionReviewPanel.IsVisible;
                bool visibleWorkspaceNonblank = !string.IsNullOrWhiteSpace(ReadWindowTextSnapshot(window))
                    && !string.IsNullOrWhiteSpace(openedOverviewState.ActiveSectionJson);
                RecordStep(
                    steps,
                    "file-new evidence: "
                    + $"workspace_id={(string.IsNullOrWhiteSpace(createdWorkspaceId) ? "missing" : "present")}; "
                    + $"visible_active={openedVisibleState.HasActiveWorkspace.ToString().ToLowerInvariant()}; "
                    + $"character_loaded={openedVisibleState.CharacterLoaded.ToString().ToLowerInvariant()}; "
                    + $"visible_open_count={openedVisibleState.OpenCount}; "
                    + $"profile_identity={profileIdentityMatches.ToString().ToLowerInvariant()}; "
                    + $"total_attributes={openedOverviewState.Build?.TotalAttributes}; "
                    + $"state_open_count={openedOverviewState.OpenWorkspaces.Count}; "
                    + $"session_open_count={openedOverviewState.Session.OpenWorkspaces.Count}");
                string fileNewAfterScreenshot = RequireScreenshotPath(
                    await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "file_new_character_visible_workspace-after"),
                    "file_new_character_visible_workspace",
                    "after");
                userJourneyWorkflows.Add(new DesktopUserJourneyWorkflowEvidence(
                    Id: "file_new_character_visible_workspace",
                    ScreenshotPaths:
                    [
                        RequireScreenshotPath(fileNewBeforeScreenshot, "file_new_character_visible_workspace", "before"),
                        fileNewAfterScreenshot
                    ],
                    Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["new_character_action_opened_visible_workspace"] = newCharacterActionOpenedVisibleWorkspace,
                        ["visible_workspace_nonblank"] = visibleWorkspaceNonblank,
                        ["starter_attributes_match_seeded_workspace"] = starterAttributesMatchSeededWorkspace,
                        ["section_preview_omits_review_copy"] = sectionPreviewOmitsReviewCopy
                    },
                    InteractionNotes:
                    [
                        "File → New used visible dialog fields and completion actions; the loaded profile, active status, workspace identity, and deterministic starter attributes were then verified."
                    ]));
            }

            await ClickFileMenuCommandAsync(window, "save_character", steps, journeyTimeout.Token, recordPointerAction);
            bool hasSavedWorkspaceEvidence = false;
            await WaitForAsync(
                steps,
                "workspace saved after pointer-first flow",
                () =>
                {
                    bool hasSaved = HasWorkspaceSavedEvidence(
                        window,
                        language,
                        expectedCharacterName,
                        expectedCharacterAlias,
                        expectedRulesetId,
                        initialOpenWorkspaces,
                        createdWorkspaceId);
                    hasSavedWorkspaceEvidence = hasSaved;
                    return hasSaved;
                },
                journeyTimeout.Token,
                WorkspaceSaveWaitTimeout);
            string savedWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);
            DesktopMouseFirstJourneyVisibleShellState savedVisibleState = ReadVisibleShellState(window, language);
            createdWorkspaceState = ResolveCreatedWorkspaceState(
                    initialOpenWorkspaces,
                    ReadOpenWorkspaces(window),
                    expectedCharacterName,
                    expectedCharacterAlias,
                    expectedRulesetId,
                    createdWorkspaceId)
                ?? createdWorkspaceState;
            RecordStep(
                steps,
                HasWorkspaceStripTransition(openedWorkspaceStripText, savedWorkspaceStripText)
                    ? $"workspace strip changed to {savedWorkspaceStripText}"
                    : $"workspace strip stayed stable while visible shell state confirmed saved workspace {savedVisibleState.WorkspaceId ?? "(missing)"}");
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "05-workspace-saved");

            if (produceUserJourneyTrace)
            {
                userJourneyWorkflows.Add(await ExerciseSaveReloadWorkflowAsync(
                    window,
                    context,
                    steps,
                    screenshotPaths,
                    journeyTimeout.Token,
                    hasSavedWorkspaceEvidence || savedVisibleState.IsSaved,
                    expectedCharacterName,
                    expectedCharacterAlias,
                    createdWorkspaceId));
                userJourneyWorkflows.Add(await ExerciseMajorNavigationWorkflowAsync(
                    window,
                    context,
                    steps,
                    screenshotPaths,
                    journeyTimeout.Token,
                    recordPointerAction));
                userJourneyWorkflows.Add(await ExerciseValidationWorkflowAsync(
                    window,
                    context,
                    steps,
                    screenshotPaths,
                    journeyTimeout.Token,
                    recordPointerAction));
            }

            DesktopMouseFirstJourneyVisibleShellState finalVisibleState = ReadVisibleShellState(window, language);
            DesktopMouseFirstJourneyRuntime.WriteObservedInputTrace(context, observedInputEvents);
            DesktopMouseFirstJourneyRuntime.WriteSuccessReceipt(
                context,
                new DesktopMouseFirstJourneyEvidence(
                    Steps: steps,
                    ScreenshotPaths: screenshotPaths,
                    PointerActionCount: getPointerActionCount(),
                    TextEntryActionCount: getTextEntryActionCount(),
                    DirectTextMutationCount: getDirectTextMutationCount(),
                    UsedForcedComboDropdownOpen: getUsedForcedComboDropdownOpen(),
                    UsedComboSelectionFallback: getUsedComboSelectionFallback(),
                    ObservedInputEvents: observedInputEvents,
                    ScenarioId: plan.ScenarioId,
                    WorkspaceId: createdWorkspaceState?.Id.Value
                        ?? createdWorkspaceId
                        ?? finalVisibleState.WorkspaceId,
                    CharacterName: expectedCharacterName,
                    CharacterAlias: expectedCharacterAlias,
                    RulesetId: finalVisibleState.RulesetId ?? expectedRulesetId,
                    BuildMethod: plan.BuildMethod,
                    MetatypeCategory: plan.MetatypeCategory,
                    PriorityHeritage: plan.PriorityHeritage,
                    Metatype: plan.Metatype,
                    PriorityTalent: plan.PriorityTalent,
                    PriorityTalentChoice: plan.PriorityTalentChoice,
                    HasSavedWorkspace: hasSavedWorkspaceEvidence || finalVisibleState.IsSaved,
                    AuthenticationPortalOpened: authenticationPortalState.Opened,
                    AuthenticationPortalUri: authenticationPortalState.Uri,
                    ActiveDialogId: ResolveVisibleDialogWindow()?.BoundDialogId,
                    VerificationNotes:
                    [
                        "Binary was launched in mouse-first live journey mode.",
                        "Character creation used menu clicks, dialog clicks, and only rare text entry for name/alias.",
                        produceUserJourneyTrace
                            ? "Workspace save/reload proof used the registered routed Ctrl+R keyboard shortcut; the presenter reloaded the current workspace and preserved identity."
                            : "Workspace reached a saved state without invoking file-picker shortcuts or internal API-only test routes."
                    ],
                    UserJourneyWorkflows: produceUserJourneyTrace ? userJourneyWorkflows : null));
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            DesktopMouseFirstJourneyVisibleShellState visibleState = ReadVisibleShellState(window, DesktopLocalizationCatalog.GetCurrentLanguage());
            DesktopMouseFirstJourneyRuntime.WriteObservedInputTrace(context, observedInputEvents);
            DesktopMouseFirstJourneyRuntime.WriteFailureArtifacts(
                context,
                ex,
                steps,
                screenshotPaths: screenshotPaths,
                pointerActionCount: getPointerActionCount(),
                textEntryActionCount: getTextEntryActionCount(),
                directTextMutationCount: getDirectTextMutationCount(),
                usedForcedComboDropdownOpen: getUsedForcedComboDropdownOpen(),
                usedComboSelectionFallback: getUsedComboSelectionFallback(),
                scenarioId: plan.ScenarioId,
                buildMethod: plan.BuildMethod,
                metatypeCategory: plan.MetatypeCategory,
                priorityHeritage: plan.PriorityHeritage,
                metatype: plan.Metatype,
                priorityTalent: plan.PriorityTalent,
                priorityTalentChoice: plan.PriorityTalentChoice,
                authenticationPortalOpened: authenticationPortalState.Opened,
                authenticationPortalUri: authenticationPortalState.Uri,
                observedInputEvents: observedInputEvents,
                activeDialogId: ResolveVisibleDialogWindow()?.BoundDialogId,
                workspaceId: visibleState.WorkspaceId);
            Console.Error.WriteLine($"Desktop mouse-first journey failed: {ex}");
            Environment.ExitCode = 1;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(window.Close);
        }
    }

    private sealed class MouseFirstJourneyAuthenticationPortalState
    {
        public bool Opened { get; set; }
        public string? Uri { get; set; }
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ExerciseMasterIndexSearchWorkflowAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> steps,
        List<string> screenshotPaths,
        ObservedInputTraceCollector inputTraceCollector,
        CancellationToken ct,
        Action recordPointerAction,
        Action recordTextEntryAction)
    {
        MenuItem toolsMenu = await OpenMenuAsync(
            window,
            "ToolsMenuButton",
            "tools",
            steps,
            ct,
            recordPointerAction);
        string beforeScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "master_index_search_focus_stability-before"),
            "master_index_search_focus_stability",
            "before");
        await ClickOpenMenuCommandAsync(toolsMenu, "master_index", "tools", steps, ct, recordPointerAction);
        await WaitForDialogAsync(window, "dialog.master_index", steps, ct);
        inputTraceCollector.ObserveDialogWindow(ResolveVisibleDialogWindow());

        string searchFieldName = DesktopDialogAccessibility.BuildFieldInputName("masterIndexSearch");
        await WaitForAsync(
            steps,
            "master index search field visible",
            () => FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName) is not null,
            ct);
        TextBox searchBox = FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName)
            ?? throw new InvalidOperationException("The Master Index search field does not have a visible routed input control.");
        await RoutePointerClickAsync(searchBox);
        recordPointerAction();
        await EnterTextAsync(searchBox, "ade");
        recordTextEntryAction();
        RecordStep(steps, "type Master Index search prefix 'ade' through routed text input");
        await WaitForAsync(
            steps,
            "master index search prefix committed",
            () => string.Equals(
                DesktopDialogFieldValueParser.GetValue(
                    window.SnapshotStateForAutomation().ActiveDialog!,
                    "masterIndexSearch"),
                "ade",
                StringComparison.Ordinal),
            ct);

        await WaitForAsync(
            steps,
            "master index search focus restored after prefix update",
            () => FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName)?.IsFocused == true,
            ct);
        TextBox refreshedSearchBox = FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName)
            ?? throw new InvalidOperationException("The Master Index search field disappeared after the routed prefix input.");
        bool focusPreservedAfterPrefix = refreshedSearchBox.IsFocused;
        await AppendTextAsync(refreshedSearchBox, "pt");
        recordTextEntryAction();
        RecordStep(steps, "append Master Index search suffix 'pt' through routed text input");
        await WaitForAsync(
            steps,
            "master index search text accumulated",
            () =>
            {
                CharacterOverviewState state = window.SnapshotStateForAutomation();
                TextBox? currentSearchBox = FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName);
                return state.ActiveDialog is not null
                    && string.Equals(
                        DesktopDialogFieldValueParser.GetValue(state.ActiveDialog, "masterIndexSearch"),
                        "adept",
                        StringComparison.Ordinal)
                    && string.Equals(currentSearchBox?.Text, "adept", StringComparison.Ordinal);
            },
            ct);
        await WaitForAsync(
            steps,
            "master index search focus restored after accumulated input",
            () => FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName)?.IsFocused == true,
            ct);
        TextBox finalSearchBox = FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), searchFieldName)
            ?? throw new InvalidOperationException("The Master Index search field disappeared after routed text accumulation.");
        bool focusPreservedAfterTyping = focusPreservedAfterPrefix && finalSearchBox.IsFocused;
        bool searchTextAccumulated = string.Equals(finalSearchBox.Text, "adept", StringComparison.Ordinal);
        string afterScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "master_index_search_focus_stability-after"),
            "master_index_search_focus_stability",
            "after");

        await ClickDialogActionUntilAsync(
            window,
            "close",
            steps,
            ct,
            recordPointerAction,
            "Master Index dialog closed after routed search workflow",
            () => ResolveVisibleDialogWindow() is null);

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "master_index_search_focus_stability",
            ScreenshotPaths: [beforeScreenshot, afterScreenshot],
            Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["focus_preserved_after_typing"] = focusPreservedAfterTyping,
                ["search_text_accumulates_keyboard_input"] = searchTextAccumulated
            },
            InteractionNotes:
            [
                "Opened Master Index through the visible Tools menu, clicked its visible search box, and entered 'ade' then 'pt' as routed text input."
            ]);
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ExerciseSaveReloadWorkflowAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> steps,
        List<string> screenshotPaths,
        CancellationToken ct,
        bool saveCompleted,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string? expectedWorkspaceId)
    {
        CharacterOverviewState beforeState = window.SnapshotStateForAutomation();
        string beforeScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "minimal_character_build_save_reload-before"),
            "minimal_character_build_save_reload",
            "before");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            window.Focus();
            SendKeyStroke(window, Key.R, KeyModifiers.Control);
        });
        RecordStep(steps, "press routed Ctrl+R refresh shortcut to reload the saved current workspace");
        await WaitForAsync(
            steps,
            "saved workspace reloaded through refresh_character presenter route",
            () =>
            {
                CharacterOverviewState state = window.SnapshotStateForAutomation();
                return string.Equals(state.LastCommandId, "refresh_character", StringComparison.Ordinal)
                    && !state.IsBusy
                    && string.IsNullOrWhiteSpace(state.Error)
                    && state.Profile is not null;
            },
            ct);

        CharacterOverviewState afterState = window.SnapshotStateForAutomation();
        string afterScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "minimal_character_build_save_reload-after"),
            "minimal_character_build_save_reload",
            "after");
        bool identityPreserved = beforeState.Profile is not null
            && afterState.Profile is not null
            && string.Equals(beforeState.Profile.Name, expectedCharacterName, StringComparison.Ordinal)
            && string.Equals(beforeState.Profile.Alias, expectedCharacterAlias, StringComparison.Ordinal)
            && string.Equals(afterState.Profile.Name, beforeState.Profile.Name, StringComparison.Ordinal)
            && string.Equals(afterState.Profile.Alias, beforeState.Profile.Alias, StringComparison.Ordinal)
            && string.Equals(afterState.WorkspaceId?.Value, beforeState.WorkspaceId?.Value, StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(expectedWorkspaceId)
                || string.Equals(afterState.WorkspaceId?.Value, expectedWorkspaceId, StringComparison.Ordinal))
            && afterState.Build?.TotalAttributes == beforeState.Build?.TotalAttributes;

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "minimal_character_build_save_reload",
            ScreenshotPaths: [beforeScreenshot, afterScreenshot],
            Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["character_created_saved_reloaded"] = saveCompleted
                    && string.Equals(afterState.LastCommandId, "refresh_character", StringComparison.Ordinal)
                    && afterState.HasSavedWorkspace,
                ["reload_preserved_character_identity"] = identityPreserved
            },
            InteractionNotes:
            [
                "Saved through the visible File menu, then reloaded with the registered routed Ctrl+R shortcut (refresh_character -> presenter LoadAsync) and compared the resulting workspace identity."
            ]);
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ExerciseMajorNavigationWorkflowAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> steps,
        List<string> screenshotPaths,
        CancellationToken ct,
        Action recordPointerAction)
    {
        await WaitForAsync(
            steps,
            "two distinct primary navigation controls outside the Info tab visible",
            () => window.SnapshotStateForAutomation().NavigationTabs
                .Where(tab => !string.Equals(tab.Id, "tab-info", StringComparison.Ordinal)
                    && FindVisiblePrimaryNavigationControl(window, tab.Id) is not null)
                .DistinctBy(tab => tab.SectionId, StringComparer.Ordinal)
                .Take(2)
                .Count() == 2,
            ct);
        NavigationTabDefinition[] candidateTabs = window.SnapshotStateForAutomation().NavigationTabs
            .Where(tab => !string.Equals(tab.Id, "tab-info", StringComparison.Ordinal)
                && FindVisiblePrimaryNavigationControl(window, tab.Id) is not null)
            .DistinctBy(tab => tab.SectionId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (candidateTabs.Length != 2)
        {
            throw new InvalidOperationException("Major navigation proof requires two distinct visible enabled primary navigation controls outside the Info tab.");
        }

        await ClickSummaryNavigationTabAsync(window, candidateTabs[0].Id, steps, ct, recordPointerAction);
        CharacterOverviewState firstState = window.SnapshotStateForAutomation();
        string firstPreview = firstState.ActiveSectionJson ?? string.Empty;
        string beforeScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "major_navigation_sanity-before"),
            "major_navigation_sanity",
            "before");

        await ClickSummaryNavigationTabAsync(window, candidateTabs[1].Id, steps, ct, recordPointerAction);
        CharacterOverviewState secondState = window.SnapshotStateForAutomation();
        string afterScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "major_navigation_sanity-after"),
            "major_navigation_sanity",
            "after");
        bool contentChanged = !string.Equals(firstState.ActiveTabId, secondState.ActiveTabId, StringComparison.Ordinal)
            && !string.Equals(firstState.ActiveSectionId, secondState.ActiveSectionId, StringComparison.Ordinal)
            && !string.Equals(firstPreview, secondState.ActiveSectionJson ?? string.Empty, StringComparison.Ordinal);

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "major_navigation_sanity",
            ScreenshotPaths: [beforeScreenshot, afterScreenshot],
            Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["primary_navigation_clicks_change_visible_content"] = contentChanged,
                ["no_unhandled_errors"] = string.IsNullOrWhiteSpace(firstState.Error)
                    && string.IsNullOrWhiteSpace(secondState.Error)
            },
            InteractionNotes:
            [
                $"Clicked visible primary navigation buttons '{candidateTabs[0].Id}' and '{candidateTabs[1].Id}' and compared their rendered section projections."
            ]);
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ExerciseValidationWorkflowAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> steps,
        List<string> screenshotPaths,
        CancellationToken ct,
        Action recordPointerAction)
    {
        await ClickSummaryNavigationTabAsync(window, "tab-info", steps, ct, recordPointerAction);
        await WaitForAsync(
            steps,
            "visible validation action available",
            () => FindVisibleValidationActionControl(window) is not null,
            ct);
        Control validationControl = FindVisibleValidationActionControl(window)
            ?? throw new InvalidOperationException("The validation workflow has no visible routed Section Actions control.");
        TabStrip validationTabStrip = window.ControlsForAutomation.SectionHost.FindControl<TabStrip>("SectionActionTabStrip")
            ?? throw new InvalidOperationException("The validation workflow has no Section Actions tab strip.");
        NavigatorSectionActionItem validationAction = validationTabStrip.Items
            .OfType<NavigatorSectionActionItem>()
            .FirstOrDefault(action => action.Kind == WorkspaceSurfaceActionKind.Validate)
            ?? throw new InvalidOperationException("The visible validation control is not bound to a section action.");
        string beforeScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "validation_or_export_smoke-before"),
            "validation_or_export_smoke",
            "before");

        await RoutePointerClickAsync(validationControl);
        recordPointerAction();
        RecordStep(steps, $"click visible validation action {validationAction.Id}");
        await WaitForAsync(
            steps,
            "validation result rendered",
            () =>
            {
                CharacterOverviewState state = window.SnapshotStateForAutomation();
                return string.Equals(state.ActiveActionId, validationAction.Id, StringComparison.Ordinal)
                    && string.Equals(state.ActiveSectionId, "validate", StringComparison.Ordinal)
                    && !state.IsBusy
                    && string.IsNullOrWhiteSpace(state.Error)
                    && !string.IsNullOrWhiteSpace(state.ActiveSectionJson);
            },
            ct);
        CharacterOverviewState validatedState = window.SnapshotStateForAutomation();
        string afterScreenshot = RequireScreenshotPath(
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "validation_or_export_smoke-after"),
            "validation_or_export_smoke",
            "after");

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "validation_or_export_smoke",
            ScreenshotPaths: [beforeScreenshot, afterScreenshot],
            Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["validation_or_export_action_completed"] = string.Equals(validatedState.ActiveActionId, validationAction.Id, StringComparison.Ordinal)
                    && string.Equals(validatedState.ActiveSectionId, "validate", StringComparison.Ordinal),
                ["result_visible_or_file_created"] = !string.IsNullOrWhiteSpace(validatedState.ActiveSectionJson)
                    && !string.IsNullOrWhiteSpace(ReadWindowTextSnapshot(window))
            },
            InteractionNotes:
            [
                $"Clicked the visible '{validationAction.Label}' Section Actions control and waited for the validation projection to render."
            ]);
    }

    private static Control? FindVisibleValidationActionControl(MainWindow window)
    {
        TabStrip? tabStrip = window.ControlsForAutomation.SectionHost.FindControl<TabStrip>("SectionActionTabStrip");
        return tabStrip is null
            ? null
            : FindVisibleTabContainer(
                tabStrip,
                item => item is NavigatorSectionActionItem { Kind: WorkspaceSurfaceActionKind.Validate });
    }

    private static async Task ClickSummaryNavigationTabAsync(
        MainWindow window,
        string tabId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        await WaitForAsync(
            steps,
            $"primary navigation control {tabId} visible",
            () => FindVisiblePrimaryNavigationControl(window, tabId) is not null,
            ct);
        Control control = FindVisiblePrimaryNavigationControl(window, tabId)
            ?? throw new InvalidOperationException($"Primary navigation control '{tabId}' is not visible.");
        await RoutePointerClickAsync(control);
        recordPointerAction();
        RecordStep(steps, $"click primary navigation control {tabId}");
        await WaitForAsync(
            steps,
            $"primary navigation {tabId} rendered",
            () =>
            {
                CharacterOverviewState state = window.SnapshotStateForAutomation();
                return string.Equals(state.ActiveTabId, tabId, StringComparison.Ordinal)
                    && !state.IsBusy
                    && string.IsNullOrWhiteSpace(state.Error)
                    && !string.IsNullOrWhiteSpace(state.ActiveSectionJson);
            },
            ct);
    }

    private static Control? FindVisiblePrimaryNavigationControl(MainWindow window, string tabId)
    {
        TabStrip? loadedRunnerTabStrip = window.ControlsForAutomation.SectionHost.FindControl<TabStrip>("LoadedRunnerTabStrip");
        Control? loadedRunnerTab = loadedRunnerTabStrip is null
            ? null
            : FindVisibleTabContainer(
                loadedRunnerTabStrip,
                item => item is NavigatorTabItem tab && string.Equals(tab.Id, tabId, StringComparison.Ordinal));
        if (loadedRunnerTab is not null)
        {
            return loadedRunnerTab;
        }

        return window.ControlsForAutomation.SummaryHeader
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button is { IsVisible: true, IsEnabled: true }
                && string.Equals(button.Tag?.ToString(), tabId, StringComparison.Ordinal));
    }

    private static Control? FindVisibleTabContainer(TabStrip tabStrip, Func<object?, bool> predicate)
    {
        int index = 0;
        foreach (object? item in tabStrip.Items)
        {
            if (predicate(item)
                && tabStrip.ContainerFromIndex(index) is Control { IsVisible: true, IsEnabled: true } control)
            {
                return control;
            }

            index++;
        }

        return null;
    }

    private static string RequireScreenshotPath(string? path, string workflowId, string frame)
        => !string.IsNullOrWhiteSpace(path)
            ? path
            : throw new InvalidOperationException($"Workflow '{workflowId}' could not capture its {frame} screenshot frame.");

    private static async Task<MenuItem> OpenMenuAsync(
        MainWindow window,
        string menuButtonName,
        string menuLabel,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        IMenuBarSurface menuSurface = window.ControlsForAutomation.MenuBar;
        Control host = menuSurface switch
        {
            Control control => control,
            _ => throw new InvalidOperationException("Active menu bar surface does not expose a control.")
        };

        MenuItem menu = FindVisibleMenuButton(host, menuButtonName)
            ?? throw new InvalidOperationException($"The visible {menuLabel} menu button was not found.");
        await RoutePointerClickAsync(menu);
        recordPointerAction();
        RecordStep(steps, $"click {menuLabel} menu");
        await WaitForAsync(
            steps,
            $"{menuLabel} menu commands visible",
            () => menu.Items.OfType<MenuItem>().Any(item => item.IsEnabled),
            ct);
        return menu;
    }

    private static async Task ClickOpenMenuCommandAsync(
        MenuItem menu,
        string commandId,
        string menuLabel,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        await WaitForAsync(
            steps,
            $"menu command {commandId} available",
            () => menu.Items.OfType<MenuItem>().Any(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled),
            ct);
        MenuItem commandItem = menu.Items.OfType<MenuItem>()
            .First(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled);
        await RoutePointerClickAsync(commandItem);
        recordPointerAction();
        RecordStep(steps, $"click {menuLabel} menu command {commandId}");
    }

    private static async Task ClickFileMenuCommandAsync(MainWindow window, string commandId, List<string> steps, CancellationToken ct, Action recordPointerAction)
    {
        MenuItem fileMenu = await OpenMenuAsync(window, "FileMenuButton", "file", steps, ct, recordPointerAction);
        await ClickOpenMenuCommandAsync(fileMenu, commandId, "file", steps, ct, recordPointerAction);
    }

    private static async Task WaitForDialogAsync(MainWindow window, string dialogId, List<string> steps, CancellationToken ct)
    {
        await WaitForAsync(
            steps,
            $"dialog {dialogId} visible",
            () => string.Equals(ResolveVisibleDialogWindow()?.BoundDialogId, dialogId, StringComparison.Ordinal)
                && ResolveDialogRoot(window) is not null,
            ct);
    }

    private static async Task ClickDialogActionAsync(MainWindow window, string actionId, List<string> steps, CancellationToken ct, Action recordPointerAction)
    {
        string actionName = DesktopDialogAccessibility.BuildActionName(actionId);
        await WaitForAsync(
            steps,
            $"dialog action {actionId} available",
            () => FindVisibleDescendant<Button>(ResolveDialogRoot(window), actionName) is not null,
            ct);

        Button button = FindVisibleDescendant<Button>(ResolveDialogRoot(window), actionName)
            ?? throw new InvalidOperationException($"Dialog action '{actionId}' was not found.");
        await RoutePointerClickAsync(button);
        recordPointerAction();
        RecordStep(steps, $"click dialog action {actionId}");
    }

    private static async Task ClickDialogActionUntilAsync(
        MainWindow window,
        string actionId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        string transitionDescription,
        Func<bool> transitionPredicate,
        int maxAttempts = 3)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await ClickDialogActionAsync(window, actionId, steps, ct, recordPointerAction);
            if (await WaitForConditionWithinAsync(transitionPredicate, ct, TransitionSettleTimeout))
            {
                RecordStep(steps, $"wait success: {transitionDescription}");
                return;
            }

            if (attempt < maxAttempts)
            {
                RecordStep(steps, $"retry dialog action {actionId} after attempt {attempt} did not trigger {transitionDescription}");
            }
        }

        throw new TimeoutException($"Timed out while waiting for {transitionDescription}.");
    }

    private static async Task SetDialogTextFieldAsync(MainWindow window, string fieldId, string value, List<string> steps, CancellationToken ct)
    {
        string controlName = DesktopDialogAccessibility.BuildFieldInputName(fieldId);
        await WaitForAsync(
            steps,
            $"dialog field {fieldId} available",
            () => FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), controlName) is not null,
            ct);

        TextBox textBox = FindVisibleDescendant<TextBox>(ResolveDialogRoot(window), controlName)
            ?? throw new InvalidOperationException($"Dialog text field '{fieldId}' was not found.");
        await EnterTextAsync(textBox, value);

        bool matches = await Dispatcher.UIThread.InvokeAsync(() => string.Equals(textBox.Text, value, StringComparison.Ordinal));
        if (!matches)
        {
            throw new InvalidOperationException($"Dialog text field '{fieldId}' did not accept routed text input.");
        }

        RecordStep(steps, $"type dialog field {fieldId} = {value}");
    }

    private static async Task SetDialogSelectFieldAsync(
        MainWindow window,
        string fieldId,
        string value,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        Action markForcedComboDropdownOpen,
        Action markSelectionFallback)
    {
        string controlName = DesktopDialogAccessibility.BuildFieldInputName(fieldId);
        await WaitForAsync(
            steps,
            $"dialog select field {fieldId} available",
            () => FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName) is not null,
            ct);

        ComboBox comboBox = FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName)
            ?? throw new InvalidOperationException($"Dialog select field '{fieldId}' was not found.");

        if (await Dispatcher.UIThread.InvokeAsync(() => ComboBoxSelectionMatches(comboBox, value)))
        {
            RecordStep(steps, $"confirm dialog field {fieldId} = {value}");
            return;
        }

        await RoutePointerClickAsync(comboBox);
        recordPointerAction();

        ComboBoxItem? comboBoxItem = await FindComboBoxItemAsync(comboBox, value);
        if (comboBoxItem is null)
        {
            await Task.Delay(200, ct);
            comboBoxItem = await FindComboBoxItemAsync(comboBox, value);
        }

        if (comboBoxItem is null)
        {
            ToggleButton? toggleButton = await FindComboBoxToggleButtonAsync(comboBox);
            if (toggleButton is not null)
            {
                await RoutePointerClickAsync(toggleButton);
                recordPointerAction();
                await Task.Delay(200, ct);
                comboBoxItem = await FindComboBoxItemAsync(comboBox, value);
            }
        }

        if (comboBoxItem is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => comboBox.IsDropDownOpen = true);
            markForcedComboDropdownOpen();
            comboBoxItem = await FindComboBoxItemAsync(comboBox, value);
        }

        if (comboBoxItem is not null)
        {
            await RoutePointerClickAsync(comboBoxItem);
            recordPointerAction();
            bool itemClickSelected = await WaitForConditionWithinAsync(
                () =>
                {
                    ComboBox? activeComboBox = FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName);
                    return activeComboBox is not null && ComboBoxSelectionMatches(activeComboBox, value);
                },
                ct,
                TransitionSettleTimeout);
            if (itemClickSelected)
            {
                RecordStep(steps, $"dialog field {fieldId} selected with pointer = {value}");
                return;
            }
        }

        bool selected = await Dispatcher.UIThread.InvokeAsync(() => TrySetComboBoxSelectedValue(comboBox, value));
        if (!selected)
        {
            throw new InvalidOperationException($"Dialog select field '{fieldId}' does not expose option '{value}'.");
        }

        markSelectionFallback();

        await WaitForAsync(
            steps,
            $"dialog field {fieldId} updated to {value}",
            () =>
            {
                ComboBox? activeComboBox = FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName);
                return activeComboBox is not null && ComboBoxSelectionMatches(activeComboBox, value);
            },
            ct);
    }

    private static async Task SetDialogListFieldAsync(
        MainWindow window,
        string fieldId,
        string value,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        Action markSelectionFallback)
    {
        string controlName = DesktopDialogAccessibility.BuildFieldInputName(fieldId);
        await WaitForAsync(
            steps,
            $"dialog list field {fieldId} available",
            () => FindVisibleDescendant<ListBox>(ResolveDialogRoot(window), controlName) is not null,
            ct);

        ListBox listBox = FindVisibleDescendant<ListBox>(ResolveDialogRoot(window), controlName)
            ?? throw new InvalidOperationException($"Dialog list field '{fieldId}' was not found.");

        if (await Dispatcher.UIThread.InvokeAsync(() => ListBoxSelectionMatches(listBox, value)))
        {
            RecordStep(steps, $"confirm dialog list field {fieldId} = {value}");
            return;
        }

        ListBoxItem? listBoxItem = await Dispatcher.UIThread.InvokeAsync(() =>
            listBox.GetVisualDescendants()
                .OfType<ListBoxItem>()
                .FirstOrDefault(item => MatchesOptionValue(item, value)));
        if (listBoxItem is not null)
        {
            await RoutePointerClickAsync(listBoxItem);
            recordPointerAction();
        }
        else
        {
            bool selected = await Dispatcher.UIThread.InvokeAsync(() => TrySetListBoxSelectedValue(listBox, value));
            if (!selected)
            {
                throw new InvalidOperationException($"Dialog list field '{fieldId}' does not expose option '{value}'.");
            }

            markSelectionFallback();
        }

        await WaitForAsync(
            steps,
            $"dialog list field {fieldId} updated to {value}",
            () =>
            {
                ListBox? activeListBox = FindVisibleDescendant<ListBox>(ResolveDialogRoot(window), controlName);
                return activeListBox is not null && ListBoxSelectionMatches(activeListBox, value);
            },
            ct);
    }

    private static async Task WaitForAsync(
        List<string> steps,
        string description,
        Func<bool> predicate,
        CancellationToken ct,
        TimeSpan? timeout = null)
    {
        if (await WaitForConditionWithinAsync(predicate, ct, timeout ?? WaitTimeout))
        {
            RecordStep(steps, $"wait success: {description}");
            return;
        }

        throw new TimeoutException($"Timed out while waiting for {description}.");
    }

    private static async Task<ComboBoxItem?> FindComboBoxItemAsync(ComboBox comboBox, string value)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ComboBoxItem? localItem = comboBox.GetLogicalDescendants()
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.IsVisible && MatchesOptionValue(item, value));
            if (localItem is not null)
            {
                return localItem;
            }

            localItem = comboBox.GetVisualDescendants()
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.IsVisible && MatchesOptionValue(item, value));
            if (localItem is not null)
            {
                return localItem;
            }

            if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return null;
            }

            return desktop.Windows
                .OfType<Window>()
                .SelectMany(window => window.GetVisualDescendants().OfType<ComboBoxItem>())
                .FirstOrDefault(item => item.IsVisible && MatchesOptionValue(item, value));
        });
    }

    private static async Task<ToggleButton?> FindComboBoxToggleButtonAsync(ComboBox comboBox)
    {
        return await Dispatcher.UIThread.InvokeAsync(() =>
            comboBox.GetVisualDescendants()
                .OfType<ToggleButton>()
                .FirstOrDefault(button => button.IsVisible && button.IsEnabled));
    }

    private static bool ComboBoxSelectionMatches(ComboBox comboBox, string expectedValue)
        => string.Equals(
            ReadDialogOptionValue(comboBox.SelectedItem)
            ?? ReadDialogOptionValue(comboBox.SelectionBoxItem),
            expectedValue,
            StringComparison.Ordinal);

    private static bool ListBoxSelectionMatches(ListBox listBox, string expectedValue)
        => string.Equals(
            ReadDialogOptionValue(listBox.SelectedItem),
            expectedValue,
            StringComparison.Ordinal);

    private static bool TrySetComboBoxSelectedValue(ComboBox comboBox, string value)
    {
        object? option = ResolveItemByValue(comboBox.ItemsSource, value);
        if (option is null)
        {
            return false;
        }

        comboBox.SelectedItem = option;
        comboBox.IsDropDownOpen = false;
        return true;
    }

    private static bool TrySetListBoxSelectedValue(ListBox listBox, string value)
    {
        object? option = ResolveItemByValue(listBox.ItemsSource, value);
        if (option is null)
        {
            return false;
        }

        listBox.SelectedItem = option;
        return true;
    }

    private static object? ResolveItemByValue(object? itemsSource, string value)
    {
        if (itemsSource is not System.Collections.IEnumerable enumerable)
        {
            return null;
        }

        foreach (object? item in enumerable)
        {
            if (MatchesOptionValue(item, value))
            {
                return item;
            }
        }

        return null;
    }

    private static bool MatchesOptionValue(object? candidate, string expectedValue)
    {
        if (candidate is null)
        {
            return false;
        }

        if (candidate is Control control && control.DataContext is not null)
        {
            return MatchesOptionValue(control.DataContext, expectedValue);
        }

        string? directValue = ReadDialogOptionValue(candidate);
        if (string.Equals(directValue, expectedValue, StringComparison.Ordinal))
        {
            return true;
        }

        string? label = candidate.GetType().GetProperty("Label")?.GetValue(candidate)?.ToString();
        if (string.Equals(label, expectedValue, StringComparison.Ordinal))
        {
            return true;
        }

        if (candidate is ContentControl contentControl)
        {
            string? contentText = contentControl.Content?.ToString();
            if (string.Equals(contentText, expectedValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string?> CaptureEvidenceScreenshotAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> screenshotPaths,
        string fileStem)
    {
        if (string.IsNullOrWhiteSpace(context.ScreenshotDirectory))
        {
            return null;
        }

        string screenshotDirectory = context.ScreenshotDirectory;
        string screenshotPath = Path.Combine(screenshotDirectory, $"{fileStem}.png");
        byte[] pngBytes = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DesktopDialogWindow? dialogWindow = ResolveVisibleDialogWindow();
            return dialogWindow?.CaptureScreenshotBytesForAutomation() ?? window.CaptureScreenshotBytesForAutomation();
        });
        Directory.CreateDirectory(screenshotDirectory);
        await File.WriteAllBytesAsync(screenshotPath, pngBytes);
        screenshotPaths.Add(screenshotPath);
        return screenshotPath;
    }

    private static async Task RoutePointerClickAsync(Control control)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            TopLevel topLevel = TopLevel.GetTopLevel(control)
                ?? throw new InvalidOperationException($"Unable to resolve a top-level visual for control '{control.Name}'.");
            Visual rootVisual = topLevel;
            Point rootPosition = control.TranslatePoint(
                new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
                rootVisual)
                ?? throw new InvalidOperationException($"Unable to translate control '{control.Name}' into root coordinates.");
            ulong timestamp = unchecked((ulong)Environment.TickCount64);
            Pointer pointer = new(1, PointerType.Mouse, isPrimary: true);
            control.Focus();

            PointerPressedEventArgs pressed = new(
                control,
                pointer,
                rootVisual,
                rootPosition,
                timestamp,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 1);
            control.RaiseEvent(pressed);

            PointerReleasedEventArgs released = new(
                control,
                pointer,
                rootVisual,
                rootPosition,
                timestamp + 1,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None,
                MouseButton.Left);
            control.RaiseEvent(released);

            TappedEventArgs tapped = new(InputElement.TappedEvent, released);
            control.RaiseEvent(tapped);
        });
    }

    private static async Task<string> ReadWorkspaceStripTextAsync(MainWindow window)
        => await Dispatcher.UIThread.InvokeAsync(() => ReadWorkspaceStripText(window));

    private static string ReadWorkspaceStripText(MainWindow window)
    {
        TextBlock? workspaceText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(control => string.Equals(control.Name, "WorkspaceText", StringComparison.Ordinal));
        return workspaceText?.Text?.Trim() ?? string.Empty;
    }

    private static string ReadCharacterStateText(MainWindow window)
    {
        TextBlock? characterStateText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(control => string.Equals(control.Name, "CharacterStateText", StringComparison.Ordinal));
        return characterStateText?.Text?.Trim() ?? string.Empty;
    }

    private static string ReadToolStripStatusText(MainWindow window)
    {
        TextBlock? toolStripStatusText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(control => string.Equals(control.Name, "StatusText", StringComparison.Ordinal));
        return toolStripStatusText?.Text?.Trim() ?? string.Empty;
    }

    private static string ReadComplianceStateText(MainWindow window)
    {
        TextBlock? complianceStateText = window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(control => string.Equals(control.Name, "ComplianceStateText", StringComparison.Ordinal));
        return complianceStateText?.Text?.Trim() ?? string.Empty;
    }

    private static DesktopMouseFirstJourneyVisibleShellState ReadVisibleShellState(MainWindow window, string language)
    {
        return DesktopMouseFirstJourneyVisibleShellStateReader.Read(
            ReadWorkspaceStripText(window),
            ReadToolStripStatusText(window),
            ReadCharacterStateText(window),
            ReadComplianceStateText(window),
            language);
    }

    private static bool HasOpenedCharacterEvidence(
        MainWindow window,
        string language,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId,
        string? baselineWorkspaceId,
        string baselineWorkspaceStripText,
        DesktopMouseFirstJourneyVisibleShellState baselineVisibleState,
        IReadOnlyList<OpenWorkspaceState> baselineOpenWorkspaces,
        out OpenWorkspaceState? createdWorkspace)
    {
        DesktopMouseFirstJourneyVisibleShellState visibleState = ReadVisibleShellState(window, language);
        IReadOnlyList<OpenWorkspaceState> currentOpenWorkspaces = ReadOpenWorkspaces(window);
        createdWorkspace = ResolveCreatedWorkspaceState(
            baselineOpenWorkspaces,
            currentOpenWorkspaces,
            expectedCharacterName,
            expectedCharacterAlias,
            expectedRulesetId,
            expectedWorkspaceId: null);
        string? activeWorkspaceId = ReadActiveWorkspaceId(window);
        string windowTextSnapshot = ReadWindowTextSnapshot(window);

        bool hasWorkspaceTransition = !string.IsNullOrWhiteSpace(activeWorkspaceId)
            && (string.IsNullOrWhiteSpace(baselineWorkspaceId)
                || !string.Equals(activeWorkspaceId, baselineWorkspaceId, StringComparison.Ordinal));
        bool hasWorkspaceStripTransition = HasWorkspaceStripTransition(
            baselineWorkspaceStripText,
            visibleState.WorkspaceStripText);
        bool hasWorkspaceEvidence = hasWorkspaceTransition
            || hasWorkspaceStripTransition
            || visibleState.HasActiveWorkspace
            || visibleState.OpenCount > baselineVisibleState.OpenCount;
        hasWorkspaceEvidence = hasWorkspaceEvidence
            || createdWorkspace is not null
            || hasSessionCreatedWorkspaceEvidence(
                currentOpenWorkspaces,
                baselineOpenWorkspaces,
                baselineWorkspaceId,
                expectedCharacterName,
                expectedCharacterAlias,
                expectedRulesetId);

        bool hasTextEvidence = ContainsWindowText(windowTextSnapshot, expectedCharacterName)
            && ContainsWindowText(windowTextSnapshot, expectedCharacterAlias)
            && ContainsWindowText(windowTextSnapshot, expectedRulesetId.ToUpperInvariant());
        bool hasSessionEvidence = createdWorkspace is not null
            && IsLikelyCreatedWorkspace(
                createdWorkspace,
                expectedCharacterName,
                expectedCharacterAlias,
                expectedRulesetId);
        bool hasRulesetEvidence = string.Equals(visibleState.RulesetId, expectedRulesetId, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(createdWorkspace?.RulesetId)
                && string.Equals(createdWorkspace.RulesetId, expectedRulesetId, StringComparison.OrdinalIgnoreCase));

        return hasWorkspaceEvidence
            && (hasTextEvidence || hasRulesetEvidence || hasSessionEvidence || visibleState.CharacterLoaded)
            && (hasWorkspaceEvidence || string.IsNullOrWhiteSpace(baselineWorkspaceId));
    }

    private static bool HasWorkspaceSavedEvidence(
        MainWindow window,
        string language,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId,
        IReadOnlyList<OpenWorkspaceState> baselineOpenWorkspaces,
        string? expectedWorkspaceId)
    {
        DesktopMouseFirstJourneyVisibleShellState visibleState = ReadVisibleShellState(window, language);
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = ReadOpenWorkspaces(window);
        OpenWorkspaceState? savedWorkspace = ResolveCreatedWorkspaceState(
            baselineOpenWorkspaces,
            openWorkspaces,
            expectedCharacterName,
            expectedCharacterAlias,
            expectedRulesetId,
            expectedWorkspaceId);

        if (savedWorkspace?.HasSavedWorkspace == true)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(expectedWorkspaceId))
        {
            return visibleState is { HasActiveWorkspace: true, IsSaved: true }
                || openWorkspaces.Any(workspace => workspace.HasSavedWorkspace);
        }

        OpenWorkspaceState? expectedWorkspace = openWorkspaces.FirstOrDefault(workspace =>
            string.Equals(workspace.Id.Value, expectedWorkspaceId, StringComparison.Ordinal));
        if (expectedWorkspace?.HasSavedWorkspace == true)
        {
            return true;
        }

        return visibleState is { HasActiveWorkspace: true, IsSaved: true }
            && string.Equals(visibleState.WorkspaceId, expectedWorkspaceId, StringComparison.Ordinal);
    }

    private static IReadOnlyList<OpenWorkspaceState> ReadOpenWorkspaces(MainWindow window)
    {
        return Dispatcher.UIThread.Invoke(() =>
            window.SnapshotStateForAutomation().Session.OpenWorkspaces.ToArray());
    }

    private static OpenWorkspaceState? ResolveCreatedWorkspaceState(
        IReadOnlyList<OpenWorkspaceState> baselineOpenWorkspaces,
        IReadOnlyList<OpenWorkspaceState> openWorkspaces,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId,
        string? expectedWorkspaceId)
    {
        if (string.IsNullOrWhiteSpace(expectedWorkspaceId) is false)
        {
            OpenWorkspaceState? exact = openWorkspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id.Value, expectedWorkspaceId, StringComparison.Ordinal));
            if (exact is not null)
            {
                return exact;
            }
        }

        if (openWorkspaces.Count == 0)
        {
            return null;
        }

        HashSet<string> baselineWorkspaceIds = baselineOpenWorkspaces.Select(workspace => workspace.Id.Value).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<OpenWorkspaceState> addedWorkspaces = openWorkspaces
            .Where(workspace => !baselineWorkspaceIds.Contains(workspace.Id.Value))
            .ToArray();

        OpenWorkspaceState? bestAdded = SelectMostLikelyWorkspaceCandidate(
            addedWorkspaces,
            expectedCharacterName,
            expectedCharacterAlias,
            expectedRulesetId);
        if (bestAdded is not null)
        {
            return bestAdded;
        }

        OpenWorkspaceState[] likelyWorkspaces = openWorkspaces
            .Select(workspace => workspace)
            .Where(workspace => IsLikelyCreatedWorkspace(workspace, expectedCharacterName, expectedCharacterAlias, expectedRulesetId))
            .OrderByDescending(workspace => workspace.LastOpenedUtc)
            .ToArray();
        return likelyWorkspaces.FirstOrDefault();
    }

    private static OpenWorkspaceState? SelectMostLikelyWorkspaceCandidate(
        IReadOnlyList<OpenWorkspaceState> candidates,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId)
        {
        return candidates
            .Where(workspace =>
                IsLikelyCreatedWorkspace(workspace, expectedCharacterName, expectedCharacterAlias, expectedRulesetId))
            .OrderByDescending(workspace =>
                CalculateWorkspaceMatchScore(workspace, expectedCharacterName, expectedCharacterAlias, expectedRulesetId))
            .ThenByDescending(workspace => workspace.LastOpenedUtc)
            .FirstOrDefault()
            ?? candidates
            .OrderByDescending(workspace => workspace.LastOpenedUtc)
            .FirstOrDefault();
    }

    private static bool IsLikelyCreatedWorkspace(
        OpenWorkspaceState workspace,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId)
    {
        return CalculateWorkspaceMatchScore(workspace, expectedCharacterName, expectedCharacterAlias, expectedRulesetId) > 0;
    }

    private static int CalculateWorkspaceMatchScore(
        OpenWorkspaceState workspace,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId)
    {
        int score = 0;
        if (Matches(workspace.Name, expectedCharacterName))
        {
            score += 2;
        }

        if (Matches(workspace.Alias, expectedCharacterAlias))
        {
            score += 2;
        }

        if (Matches(workspace.RulesetId, expectedRulesetId))
        {
            score += 1;
        }

        if (score > 0)
        {
            return score;
        }

        return Matches(workspace.Name, expectedCharacterName)
            || Matches(workspace.Alias, expectedCharacterAlias)
            ? 1
            : 0;
    }

    private static bool Matches(string? value, string expected)
        => !string.IsNullOrWhiteSpace(value)
            && !string.IsNullOrWhiteSpace(expected)
            && (string.Equals(value.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase)
                || value.Contains(expected, StringComparison.OrdinalIgnoreCase));

    private static bool hasSessionCreatedWorkspaceEvidence(
        IReadOnlyList<OpenWorkspaceState> currentOpenWorkspaces,
        IReadOnlyList<OpenWorkspaceState> baselineOpenWorkspaces,
        string? baselineWorkspaceId,
        string expectedCharacterName,
        string expectedCharacterAlias,
        string expectedRulesetId)
    {
        OpenWorkspaceState? currentCreated = ResolveCreatedWorkspaceState(
            baselineOpenWorkspaces,
            currentOpenWorkspaces,
            expectedCharacterName,
            expectedCharacterAlias,
            expectedRulesetId,
            expectedWorkspaceId: baselineWorkspaceId);
        return currentCreated is not null
            || (!string.IsNullOrWhiteSpace(baselineWorkspaceId)
                && !currentOpenWorkspaces.Any(workspace => string.Equals(workspace.Id.Value, baselineWorkspaceId, StringComparison.Ordinal)));
    }

    private static string? ReadActiveWorkspaceId(MainWindow window)
    {
        CharacterOverviewState state = Dispatcher.UIThread.Invoke(() => window.SnapshotStateForAutomation());
        return state.Session.ActiveWorkspaceId?.Value
            ?? state.WorkspaceId?.Value;
    }

    private static string ReadWindowTextSnapshot(MainWindow window)
    {
        return string.Join(
            Environment.NewLine,
            window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(control => control.Text?.Trim())
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal));
    }

    private static bool ContainsWindowText(string snapshot, string expectedText)
        => snapshot.Contains(expectedText, StringComparison.OrdinalIgnoreCase);

    private static bool HasWorkspaceStripTransition(string previousText, string currentText)
        => !string.IsNullOrWhiteSpace(currentText)
            && !string.Equals(previousText, currentText, StringComparison.Ordinal);

    private static async Task<bool> WaitForConditionWithinAsync(Func<bool> predicate, CancellationToken ct, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            bool matched = await Dispatcher.UIThread.InvokeAsync(predicate);
            if (matched)
            {
                return true;
            }

            await Task.Delay(PollInterval);
        }

        return false;
    }

    private static async Task EnterTextAsync(TextBox textBox, string value)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(value);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            textBox.Focus();
            textBox.SelectAll();
            SendKeyStroke(textBox, Key.A, KeyModifiers.Control);
            SendKeyStroke(textBox, Key.Back);

            foreach (char character in value)
            {
                SendTextInput(textBox, character);
            }
        });
    }

    private static async Task AppendTextAsync(TextBox textBox, string value)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(value);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
            foreach (char character in value)
            {
                SendTextInput(textBox, character);
            }
        });
    }

    private static void SendKeyStroke(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        KeyEventArgs keyDown = new()
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = target,
            Key = key,
            KeyModifiers = modifiers
        };
        target.RaiseEvent(keyDown);

        KeyEventArgs keyUp = new()
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Source = target,
            Key = key,
            KeyModifiers = modifiers
        };
        target.RaiseEvent(keyUp);
    }

    private static void SendTextInput(InputElement target, char character)
    {
        TextInputEventArgs textInput = new()
        {
            RoutedEvent = InputElement.TextInputEvent,
            Source = target,
            Text = character.ToString()
        };
        target.RaiseEvent(textInput);
    }

    private static Visual ResolveDialogRoot(MainWindow window)
    {
        return (Visual?)ResolveVisibleDialogWindow() ?? window.ControlsForAutomation.CommandDialogPane;
    }

    private static void RecordStep(List<string> steps, string description)
    {
        steps.Add(description);
        Console.Error.WriteLine($"[mouse-first-journey] {description}");
    }

    private static string? ReadDialogOptionValue(object? candidate)
    {
        if (candidate is null)
        {
            return null;
        }

        return candidate.GetType()
            .GetProperty("Value")?
            .GetValue(candidate)?
            .ToString();
    }

    private sealed class ObservedInputTraceCollector : IDisposable
    {
        private readonly MainWindow _window;
        private readonly List<DesktopMouseFirstJourneyObservedInputEvent> _events;
        private readonly HashSet<int> _observedRoots = [];
        private DesktopDialogWindow? _dialogWindow;

        public ObservedInputTraceCollector(MainWindow window, List<DesktopMouseFirstJourneyObservedInputEvent> events)
        {
            _window = window;
            _events = events;
            ObserveRoot(window);
        }

        public void ObserveDialogWindow(DesktopDialogWindow? dialogWindow)
        {
            if (dialogWindow is null || ReferenceEquals(_dialogWindow, dialogWindow))
            {
                return;
            }

            _dialogWindow = dialogWindow;
            ObserveRoot(dialogWindow);
        }

        public void Dispose()
        {
            DetachRoot(_window);
            if (_dialogWindow is not null)
            {
                DetachRoot(_dialogWindow);
            }
        }

        private void ObserveRoot(InputElement root)
        {
            int rootId = RuntimeHelpers.GetHashCode(root);
            if (!_observedRoots.Add(rootId))
            {
                return;
            }

            root.AddHandler(InputElement.PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            root.AddHandler(InputElement.PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            root.AddHandler(InputElement.TappedEvent, HandleTapped, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        private void DetachRoot(InputElement root)
        {
            root.RemoveHandler(InputElement.PointerPressedEvent, HandlePointerPressed);
            root.RemoveHandler(InputElement.PointerReleasedEvent, HandlePointerReleased);
            root.RemoveHandler(InputElement.TappedEvent, HandleTapped);
        }

        private void HandlePointerPressed(object? sender, PointerPressedEventArgs e) => RecordEvent("pointer_pressed", e.Source);

        private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e) => RecordEvent("pointer_released", e.Source);

        private void HandleTapped(object? sender, TappedEventArgs e) => RecordEvent("tapped", e.Source);

        private void RecordEvent(string eventKind, object? source)
        {
            if (source is not Control control)
            {
                return;
            }

            _events.Add(
                new DesktopMouseFirstJourneyObservedInputEvent(
                    EventKind: eventKind,
                    ControlType: control.GetType().Name,
                    ControlName: string.IsNullOrWhiteSpace(control.Name) ? null : control.Name,
                    ControlTag: control.Tag?.ToString(),
                    DialogId: ResolveVisibleDialogWindow()?.BoundDialogId,
                    RecordedAtUtc: DateTimeOffset.UtcNow));
        }
    }

    private static DesktopDialogWindow? ResolveVisibleDialogWindow()
    {
        return global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.OfType<DesktopDialogWindow>().FirstOrDefault(window => window.IsVisible)
            : null;
    }

    private static MenuItem? FindVisibleMenuButton(Visual root, string menuButtonName)
    {
        return root.GetVisualDescendants()
            .OfType<MenuItem>()
            .FirstOrDefault(item =>
                string.Equals(item.Name, menuButtonName, StringComparison.Ordinal)
                && item.IsVisible);
    }

    private static T? FindVisibleDescendant<T>(Visual root, string controlName)
        where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control =>
                string.Equals(control.Name, controlName, StringComparison.Ordinal)
                && control.IsVisible);
    }

}
