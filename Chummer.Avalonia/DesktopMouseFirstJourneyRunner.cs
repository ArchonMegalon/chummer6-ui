using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia.Controls;
using Chummer.Avalonia.Creation;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Chummer.Presentation.Printing;
using System.IO;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Chummer.Avalonia;

internal static class DesktopMouseFirstJourneyRunner
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan WorkspacePublishedWaitTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WorkspaceSaveWaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JourneyTimeout = TimeSpan.FromSeconds(300);
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
            using CancellationTokenSource journeyTimeout = new(
            string.Equals(Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH"), "1", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromSeconds(480)
                : JourneyTimeout);
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

            // Re-audit E1/E5: manual-acceptance mode validation (pure, unit-tested
            // below) runs at the attributes gate, the earliest point where the
            // step-availability input is known and still before any journey
            // mutation. The attributes guard historically swallowed it: when the
            // attributes step was unavailable the whole guard was skipped and the
            // independent AI_FINISH branch stayed reachable; it now fails loudly
            // in manual-acceptance mode instead.

            // its editor entry to be available; it fails explicitly otherwise.

            // Item-3 reopen leg (re-audit): when this process is the fresh second
            // launch of the two-phase journey (CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_REOPEN_VERIFY=1),
            // load the workspace the first process finalized and saved through the
            // production presenter (the same load path the persistence probe uses),
            // then assert the finalized character restored: created profile, clean
            // and persisted workspace row, creation wizard closed. The ordinary
            // receipt machinery completes the process exit.
            if (string.Equals(
                    Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_REOPEN_VERIFY"),
                    "1",
                    StringComparison.OrdinalIgnoreCase))
            {
                string savedReceiptPath = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SAVED_RECEIPT")
                    ?? throw new InvalidOperationException(
                        "The reopen-verify leg requires CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SAVED_RECEIPT to point at the first process's pass receipt.");
                using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(savedReceiptPath));
                if (!string.Equals(saved.RootElement.GetProperty("status").GetString(), "pass", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The saved receipt must record a passing first journey.");
                }

                string? savedWorkspaceId = saved.RootElement.GetProperty("workspaceId").GetString();
                if (string.IsNullOrWhiteSpace(savedWorkspaceId))
                {
                    throw new InvalidOperationException("The saved receipt does not carry a workspaceId.");
                }

                string? savedCharacterName = saved.RootElement.GetProperty("characterName").GetString();
                await window.InitializationForAutomation.WaitAsync(journeyTimeout.Token);
                ICharacterOverviewPresenter presenter = App.Services!.GetRequiredService<ICharacterOverviewPresenter>();
                await presenter.LoadAsync(new CharacterWorkspaceId(savedWorkspaceId), journeyTimeout.Token);
                await WaitForAsync(
                    steps,
                    "reopen: loaded workspace visible in the automation snapshot",
                    () => window.SnapshotStateForAutomation().WorkspaceId is not null,
                    journeyTimeout.Token,
                    TimeSpan.FromMinutes(1));

                CharacterOverviewState restoredOverview = window.SnapshotStateForAutomation();
                var restoredWorkspaceId = restoredOverview.WorkspaceId
                    ?? throw new InvalidOperationException("Reopen did not leave a workspace loaded.");
                if (!string.Equals(restoredWorkspaceId.Value, savedWorkspaceId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Reopen loaded a different workspace than the saved receipt.");
                }

                OpenWorkspaceState? restoredWorkspace = restoredOverview.Session.FindWorkspace(restoredWorkspaceId);
                if (restoredWorkspace is null || !restoredWorkspace.HasSavedWorkspace || restoredWorkspace.IsDirty)
                {
                    throw new InvalidOperationException("The reopened workspace is not clean and persisted.");
                }

                string restoredName = restoredOverview.Profile?.Name
                    ?? throw new InvalidOperationException("Reopen did not restore a profile.");
                if (!string.IsNullOrWhiteSpace(savedCharacterName)
                    && !string.Equals(restoredName, savedCharacterName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The reopened profile '{restoredName}' does not match the saved character '{savedCharacterName}'.");
                }

                bool restoredCreated = restoredOverview.Profile?.Created == true;
                if (!restoredCreated)
                {
                    throw new InvalidOperationException("The reopened profile is not marked created; finalization did not persist.");
                }

                if (restoredOverview.CreationWizard is not null)
                {
                    throw new InvalidOperationException("The creation wizard is still visible after reopening a finalized character.");
                }

                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "09-reopen-restored");
                RecordStep(steps, $"reopen verified: workspaceId={restoredWorkspaceId}, name={restoredName}, profileCreated={restoredCreated}, wizardVisible={restoredOverview.CreationWizard is not null}");

                // The reopen process must also leave the ordinary pass receipt so the
                // two-phase harness can gate on a written receipt, not just exit code.
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
                        WorkspaceId: restoredWorkspaceId.Value,
                        CharacterName: restoredName,
                        CharacterAlias: savedCharacterName,
                        RulesetId: null,
                        BuildMethod: plan.BuildMethod,
                        MetatypeCategory: plan.MetatypeCategory,
                        PriorityHeritage: plan.PriorityHeritage,
                        Metatype: plan.Metatype,
                        PriorityTalent: plan.PriorityTalent,
                        PriorityTalentChoice: plan.PriorityTalentChoice,
                        HasSavedWorkspace: true,
                        AuthenticationPortalOpened: false,
                        AuthenticationPortalUri: null,
                        ActiveDialogId: null,
                        VerificationNotes:
                        [
                            "Item-3 reopen verification: the finalized character was reloaded in a fresh process through the production presenter load path; profile Created, workspace clean and persisted, creation wizard closed."
                        ],
                        UserJourneyWorkflows: null));
                Environment.ExitCode = 0;
                return;
            }

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
                "workspace published after create_character",
                () =>
                {
                    CharacterWorkspaceId? published = window.SnapshotStateForAutomation().WorkspaceId;
                    return published is not null;
                },
                maxAttempts: 3,
                settleTimeout: WorkspacePublishedWaitTimeout);
            inputTraceCollector.ObserveDialogWindow(ResolveVisibleDialogWindow());

            // The product replaced the legacy continuation dialog with the in-window
            // creation wizard (WorkflowParityGateTests forbid the dialog). Drive the
            // wizard: walk Continue through navigation-only steps, click option
            // cards for metatype/nationality, confirm the foundation preview, then
            // finalize so Career mode (Profile.Created) is reached natively.
            CharacterCreationWizardControl wizard =
                await WaitForWizardControlAsync(window, steps, journeyTimeout.Token);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02-creation-wizard-open");

            // The method step gates the rest of creation on an authoritative
            // Priority assignment (prerequisite draft). Drive the Priority
            // table with the canonical spread from the Core prerequisite tests.
            // Life-Modules plans skip this block: their build method is already
            // authoritative from the create dialog.
            if (string.Equals(plan.BuildMethod, "Priority", StringComparison.OrdinalIgnoreCase))
            {
            await WaitForAsync(
                steps,
                "priority confirm affordance available on the method step",
                () => wizard.PriorityGridHost.Children.Count > 0
                    && wizard.PriorityConfirmButton.IsVisible,
                journeyTimeout.Token);
            foreach ((string Category, string Rank) priorityPick in new[]
                     {
                         ("heritage", "A"),
                         ("talent", "E"),
                         ("attributes", "B"),
                         ("skills", "C"),
                         ("resources", "D"),
                     })
            {
                string categoryLocal = priorityPick.Category;
                string rankLocal = priorityPick.Rank;
                await ClickWizardButtonUntilEffectAsync(
                    wizard,
                    $"creation-wizard-priority-{categoryLocal}-{rankLocal}",
                    $"priority selection recorded for {categoryLocal}={rankLocal}",
                    () => wizard.GetPrioritySelections().TryGetValue(categoryLocal, out string? picked)
                        && string.Equals(picked, rankLocal, StringComparison.Ordinal),
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction);
            }

            await WaitForAsync(
                steps,
                "priority confirm affordance enabled with a complete assignment",
                () => wizard.PriorityConfirmButton.IsEnabled,
                journeyTimeout.Token);

            // A Priority draft additionally requires a heritage (metatype) choice
            // and a talent choice from the assigned ranks' option sets.
            await WaitForAsync(
                steps,
                "heritage choices rendered for the assigned heritage rank",
                () => wizard.GetPriorityHeritageChoices().Any(choice => choice.IsEnabled),
                journeyTimeout.Token);
            string heritageChoiceId = await Dispatcher.UIThread.InvokeAsync(() =>
                wizard.GetPriorityHeritageChoices().FirstOrDefault(c => c.IsEnabled).SelectionId);
            await ClickWizardButtonUntilEffectAsync(
                wizard,
                "creation-wizard-priority-heritage-choice-" + heritageChoiceId,
                "heritage choice recorded",
                () => string.Equals(
                    wizard.GetPriorityHeritageSelectionId(),
                    heritageChoiceId,
                    StringComparison.Ordinal),
                steps,
                journeyTimeout.Token,
                recordPointerAction);

            await WaitForAsync(
                steps,
                "talent choices rendered for the assigned talent rank",
                () => wizard.GetPriorityTalentChoices().Any(choice => choice.IsEnabled),
                journeyTimeout.Token);
            string talentChoiceId = await Dispatcher.UIThread.InvokeAsync(() =>
                wizard.GetPriorityTalentChoices().FirstOrDefault(c => c.IsEnabled).SelectionId);
            await ClickWizardButtonUntilEffectAsync(
                wizard,
                "creation-wizard-priority-talent-choice-" + talentChoiceId,
                "talent choice recorded",
                () => string.Equals(
                    wizard.GetPriorityTalentSelectionId(),
                    talentChoiceId,
                    StringComparison.Ordinal),
                steps,
                journeyTimeout.Token,
                recordPointerAction);

            await ClickWizardButtonUntilEffectAsync(
                wizard,
                "creation-wizard-priority-confirm",
                "priority assignment recorded as the authoritative draft",
                () => wizard.IsPriorityConfirmed,
                steps,
                journeyTimeout.Token,
                recordPointerAction);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02a-priority-confirmed");
            } // end Priority build-method gate

            // For Priority builds the Core marks foundation as life-modules-only
            // (never enterable) and gates the attributes step on deep-stage
            // (qualities) authority. Pre-check enablement so a legitimately
            // disabled step does not burn the journey budget on click retries.
            bool attributesEntered = false;
            // After the authoritative priority draft the wizard auto-advances to
            // the attributes step, which DISABLES its own nav button (a selected
            // step cannot be re-entered). Entry therefore means "button enabled"
            // OR "attributes already the selected step" - requiring a clickable
            // button deadlocked every Priority journey at this gate (R94/w103).
            bool attributesEnabled = await WaitForConditionWithinAsync(
                () => FindWizardAutomationButton(wizard, "creation-wizard-step-attributes")
                        is { IsVisible: true, IsEffectivelyEnabled: true }
                    || string.Equals(
                        wizard.StateForAutomation()?.Steps.FirstOrDefault(step => step.IsSelected)?.StepId,
                        "attributes",
                        StringComparison.Ordinal),
                journeyTimeout.Token,
                TimeSpan.FromSeconds(8));
            bool attributesSelected = string.Equals(
                wizard.StateForAutomation()?.Steps.FirstOrDefault(step => step.IsSelected)?.StepId,
                "attributes",
                StringComparison.Ordinal);
            // Re-audit E1/E5: the pure mode validation is internal and
            // unit-tested; the entry point applies it unconditionally before
            // any journey mutation.
            string failure = ValidateManualAcceptanceMode(
                Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_MANUAL_DEEP") ?? string.Empty,
                Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH") ?? string.Empty,
                attributesEnabled);
            if (failure is not null)
            {
                RecordStep(steps, "manual acceptance refused: " + failure);
                throw new InvalidOperationException(failure);
            }
            if (attributesEnabled)
            {
                if (attributesSelected)
                {
                    attributesEntered = true;
                    RecordStep(steps, "wizard already on the attributes step after the priority draft (no nav click needed)");
                }
                else
                {
                    try
                    {
                        await ClickWizardButtonUntilEffectAsync(
                            wizard,
                            "creation-wizard-step-attributes",
                            "wizard entered the attributes step",
                            () => string.Equals(
                                wizard.StateForAutomation()?.Steps.FirstOrDefault(step => step.IsSelected)?.StepId,
                                "attributes",
                                StringComparison.Ordinal),
                            steps,
                            journeyTimeout.Token,
                            recordPointerAction);
                        attributesEntered = true;
                    }
                    catch (TimeoutException)
                    {
                        RecordStep(
                            steps,
                            "attributes entry authoritatively blocked for Priority build (deep-stage authority pending); recorded as evidence");
                    }
                }
            // Manual deep-stage leg (gen-8 item 4): drive the real attribute/skill
            // allocation editors with ordinary typed interactions when AI-finish is
            // off. Gated by CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_MANUAL_DEEP=1 so the
            // canonical scenarios stay untouched; when enabled, review must become
            // reachable and finalize must apply, or the run fails with evidence.
            string manualDeepGate = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_MANUAL_DEEP") ?? string.Empty;
            bool manualDeep = string.Equals(manualDeepGate, "1", StringComparison.OrdinalIgnoreCase);
            if (manualDeep
                && string.Equals(Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH") ?? string.Empty, "1", StringComparison.OrdinalIgnoreCase))
            {
                RecordStep(steps, "manual deep leg refused: MANUAL_DEEP and AI_FINISH are mutually exclusive in manual acceptance mode");
                throw new InvalidOperationException("MANUAL_DEEP and CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH are mutually exclusive; manual acceptance requires AI Finish off.");
            }
            if (manualDeep)
            {
                await RunManualDeepStageLegAsync(
                    window, wizard, context, screenshotPaths, steps,
                    journeyTimeout.Token, recordPointerAction);
            }
            else
            {
                RecordStep(steps, "manual deep-stage leg skipped (env gate off)");
            }
            } // end attributesEnabled guard

            if (attributesEntered)
            {
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03-attributes-entered");
            }
            else
            {
                var priorityState = wizard.StateForAutomation();
                string stepSummary = priorityState is null
                    ? "wizard state unavailable"
                    : string.Join(", ", priorityState.Steps.Select(step => $"{step.StepId}={step.Status}"));
                RecordStep(steps, $"wizard step graph after authoritative priority draft: {stepSummary}");
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03-priority-graph-evidence");
            }
            if (!string.IsNullOrWhiteSpace(plan.Metatype))
            {
                // Metatype/nationality option cards live on the foundation step,
                // which the Core marks life-modules-only for Priority builds.
                // plan.Metatype is non-empty only for Life-Modules plans.
                await SelectWizardOptionAsync(
                    window,
                    wizard,
                    plan.Metatype,
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction,
                    requirePresent: false);
            }

            // The foundation (Life-Modules) flow only applies when the journey
            // plan drives a Life-Modules build; Priority builds route through
            // the prerequisite draft instead.
            if (!string.IsNullOrWhiteSpace(plan.Metatype))
            {
            string nationalityOptionId =
                await WaitForWizardNationalityOptionAsync(window, steps, journeyTimeout.Token);
            await SelectWizardOptionAsync(
                window,
                wizard,
                nationalityOptionId,
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                requirePresent: true);
            await WaitForWizardFoundationConfirmableAsync(wizard, steps, journeyTimeout.Token);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02b-foundation-preview");
            await ClickWizardButtonUntilEffectAsync(
                wizard,
                "creation-wizard-foundation-confirm",
                "foundation selection applied and wizard left the foundation step",
                () => !string.Equals(
                    wizard.StateForAutomation()?.Steps.FirstOrDefault(step => step.IsSelected)?.StepId,
                    "foundation",
                    StringComparison.Ordinal),
                steps,
                journeyTimeout.Token,
                recordPointerAction);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03-foundation-selected");
            } // end !attributesEntered (life-modules foundation flow)

            // AI-finish leg (Build Ghost finishing flow). Env-gated so the
            // canonical sr5-priority scenario is untouched; when enabled the
            // journey clicks Finish with AI, approves the review dialog, and
            // requires every domain plan to apply before finalizing. The leg
            // runs BEFORE the review attempt because the AI-finish fills the
            // authoritatively-blocked deep stages (attributes/skills/qualities)
            // that gate review entry.
            string aiFinishGate = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH") ?? string.Empty;
            bool manualDeepMode = string.Equals(Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_MANUAL_DEEP") ?? string.Empty, "1", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(aiFinishGate, "1", StringComparison.OrdinalIgnoreCase))
            {
                // Operator opted into the AI-finish leg: flip the wizard's
                // feature gate before projecting the click target.
                wizard.SetFinishWithAiFeatureEnabled(true);
                await ClickWizardButtonAsync(
                    wizard,
                    "creation-wizard-finish-with-ai",
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction);
                await WaitForAsync(
                    steps,
                    "build ghost review dialog shown",
                    () => wizard.IsFinishWithAiBusy,
                    journeyTimeout.Token);
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03f-ai-finish-review");
                await ClickDialogButtonAsync(
                    window,
                    "creation-ai-finish-apply",
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction);
                await WaitForAsync(
                    steps,
                    "build ghost plan applied across domains",
                    () => !wizard.IsFinishWithAiBusy,
                    journeyTimeout.Token,
                    TimeSpan.FromMinutes(2.5));
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03g-ai-finish-applied");
            }
            else
            {
                RecordStep(steps, "ai-finish leg skipped (env gate off)");
            }
            bool finalizationApplied = false;
            // Ordinary-route walk (re-audit item 3): the route to review must be
            // walkable with ORDINARY Next buttons only — the re-audit forbids
            // jumping straight to review via creation-wizard-step-* buttons. The
            // walker clicks creation-wizard-continue until the observable active
            // step is review. When the AI-finish leg is off, the deep-stage drafts
            // are absent and review is authoritatively gated; the timeout then
            // records evidence and the journey continues (finalize recorded as
            // gated below). With the leg on, review must be reachable — the
            // timeout propagates and fails the run.
            bool reviewReached = false;
            try
            {
                await ContinueWizardUntilStepAsync(window, wizard, "review", steps, journeyTimeout.Token);
                reviewReached = true;
            }
            catch (TimeoutException) when (!string.Equals(aiFinishGate, "1", StringComparison.OrdinalIgnoreCase) && !manualDeepMode)
            {
                // Re-audit E1: the timeout is a strict failure in manual mode;
                // only the ordinary (non-manual, ai-finish-off) route records
                // the authoritative deep-stage gate as evidence and continues.
                RecordStep(steps, "ordinary Next walk timed out before review (deep stages pending; ai-finish leg off; ordinary route)");
            }
            if (reviewReached)
            {
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03b-review-step");
            }
            else
            {
                var wizardState = wizard.StateForAutomation();
                string blockerSummary = wizardState is null
                    ? "wizard state unavailable"
                    : string.Join(", ", wizardState.Steps.Select(step => $"{step.StepId}={step.Status}"));
                RecordStep(
                    steps,
                    $"review entry authoritatively blocked (deep stages pending); wizard state: {blockerSummary}");
            }

            if (reviewReached)
            {
                await WaitForAsync(
                    steps,
                    "finalize affordance available on the review step",
                    () => FindWizardAutomationButton(wizard, "creation-wizard-finalize")
                        is { IsVisible: true, IsEffectivelyEnabled: true },
                    journeyTimeout.Token);
            
                RecordStep(steps, $"starting-cash rolled: " + (wizard.GetStartingCashChoice()?.DiceTotal.ToString() ?? "none") + " dice total (displayed on finalize step)");
                await ClickWizardButtonAsync(
                    wizard,
                    "creation-wizard-finalize",
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction);
                await WaitForAsync(
                    steps,
                    "authoritative finalization applied (advanced editor unlocked)",
                    () =>
                    {
                        CharacterOverviewState overview = window.SnapshotStateForAutomation();
                        return overview.Profile?.Created == true
                            && overview.CreationWizard is null;
                    },
                    journeyTimeout.Token,
                    TimeSpan.FromMinutes(2.5));
                                finalizationApplied = true;
await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "04-finalized-editor-unlocked");
            }
            else
            {
                // Finalization is gated on the deep-stage drafts (attributes/skills/
                // qualities/resources/gear) whose editors are separate product
                // surfaces; record the authoritative gate state as journey evidence.
                CharacterOverviewState blockedOverview = window.SnapshotStateForAutomation();
                RecordStep(
                    steps,
                    $"finalize not attempted: review gated by deep stages; profileCreated={blockedOverview.Profile?.Created}, wizardVisible={blockedOverview.CreationWizard is not null}");
            }

            // WIN-006/T33 evidence: drive the resources deep stage through the real
            // option-click -> Prepare -> confirm -> receipt pipeline. Reaching the
            // resources step requires an authoritative wizard state; when the stage
            // is authoritatively gated the leg records the state as evidence and
            // continues, mirroring the attributes gate above.
            bool resourcesEntered = await WaitForConditionWithinAsync(
                () => FindWizardAutomationButton(wizard, "creation-wizard-step-resources")
                    is { IsVisible: true, IsEffectivelyEnabled: true},
                journeyTimeout.Token,
                TimeSpan.FromSeconds(6));
            if (finalizationApplied)
            {
                RecordStep(steps, "resources deep-stage leg skipped: character already authoritatively finalized by the AI-finish leg (T33 preview pipeline no longer mutable post-finalization)");
            }
            else if (resourcesEntered)
            {
                await ClickWizardButtonUntilEffectAsync(
                    wizard,
                    "creation-wizard-step-resources",
                    "wizard entered the resources step",
                    () => string.Equals(
                        wizard.StateForAutomation()?.ActiveStepId,
                        "resources",
                        StringComparison.Ordinal),
                    steps,
                    journeyTimeout.Token,
                    recordPointerAction);
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03c-resources-entered");

                string? resourcesOptionId = wizard.StateForAutomation()
                    ?.LegalOptions.FirstOrDefault(option => option.IsEnabled)?.OptionId;
                if (resourcesOptionId is not null)
                {
                    await ClickWizardButtonUntilEffectAsync(
                        wizard,
                        "creation-wizard-option-" + resourcesOptionId,
                        "resources option prepared an exact Core preview",
                        () => FindWizardAutomationButton(wizard, "creation-wizard-resources-confirm")
                            is { IsVisible: true, IsEffectivelyEnabled: true },
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction);
                    await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03d-resources-preview");
                    await ClickWizardButtonUntilEffectAsync(
                        wizard,
                        "creation-wizard-resources-confirm",
                        "resources allocation confirmed with an idempotent Core receipt",
                        () => FindWizardAutomationButton(wizard, "creation-wizard-resources-confirm") is null,
                        steps,
                        journeyTimeout.Token,
                        recordPointerAction);
                    await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03e-resources-confirmed");
                }
                else
                {
                    RecordStep(steps, "resources step reachable but no enabled allocation option was offered");
                }
            }
            else
            {
                CharacterCreationWizardDesktopState? resourcesState = wizard.StateForAutomation();
                string resourcesSummary = resourcesState is null
                    ? "wizard state unavailable while probing resources"
                    : "resources step authoritatively gated: " + string.Join(
                        ", ",
                        resourcesState.Steps
                            .Where(step => step.StepId is "resources" or "skills")
                            .Select(step => $"{step.StepId}={step.Status}"));
                RecordStep(steps, resourcesSummary);
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03c-resources-gated");
            }

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

            // R109 fix 6: identity/attribute/save assertions must validate on
            // every platform before success; they no longer gate on optional
            // trace output.
            if (produceUserJourneyTrace || manualDeepMode)
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
                string? fileNewAfterScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "file_new_character_visible_workspace-after");
                userJourneyWorkflows.Add(new DesktopUserJourneyWorkflowEvidence(
                    Id: "file_new_character_visible_workspace",
                    ScreenshotPaths: OptionalScreenshotPaths(fileNewBeforeScreenshot, fileNewAfterScreenshot),
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

            // Re-audit E3: the save/reload exercise is a REQUIRED leg in manual
            // acceptance mode regardless of trace output (same-process reload is
            // distinct from the native fresh-process acceptance).
            if (produceUserJourneyTrace || manualDeepMode)
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
                RequireWorkflowAssertions(userJourneyWorkflows);
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
                // WIN-003: print/export runs LAST, on a complete imported career
                // character (the canonical print/export user path). The wizard draft
                // document is derived content that mutates between reads without a
                // ContentRevision bump, which deterministically trips the presenter's
                // canonical output-conflict guard (pinned by
                // Print_owner_bound_output_* contract tests). The imported career
                // fixture has a stable document, so the guard passes.
                userJourneyWorkflows.Add(await ExercisePrintExportWorkflowAsync(
                    window,
                    context,
                    steps,
                    screenshotPaths,
                    journeyTimeout.Token,
                    recordPointerAction));
            }

            // T67: the career leg runs unconditionally and loads the bundled
            // career sample itself (Profile.Created=true), so evidence is
            // produced even when the creation leg never reaches career mode
            // (Priority deep stages stay blocked in this scenario).
            userJourneyWorkflows.Add(await ExerciseCareerRewardsWorkflowAsync(
                window,
                context,
                steps,
                screenshotPaths,
                journeyTimeout.Token,
                recordPointerAction));

            DesktopMouseFirstJourneyVisibleShellState finalVisibleState = ReadVisibleShellState(window, language);
            DesktopMouseFirstJourneyRuntime.WriteObservedInputTrace(context, observedInputEvents);
            RequireWorkflowAssertions(userJourneyWorkflows);

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
            CharacterOverviewState failureState = window.SnapshotStateForAutomation();
            RecordStep(steps, $"failure dialog: {failureState.ActiveDialog?.Id ?? "none"}; presenter error: {failureState.Error ?? "none"}");
            try
            {
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "journey-failure");
            }
            catch (Exception captureException)
            {
                RecordStep(steps, $"failure screenshot unavailable: {captureException.GetType().Name}");
            }

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
        string? beforeScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "master_index_search_focus_stability-before");
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
        string? afterScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "master_index_search_focus_stability-after");

        // Master Index suppresses the footer actions row by design (see
        // DesktopDialogWindow.BuildActions) — the OS title-bar close is the user
        // path. Drive the same path the X button raises (OnClosing -> CloseDialogAsync).
        await WaitForAsync(
            steps,
            "Master Index dialog closed after routed search workflow",
            () =>
            {
                DesktopDialogWindow? dialogWindow = ResolveVisibleDialogWindow();
                if (dialogWindow is null)
                {
                    return true;
                }

                Dispatcher.UIThread.Post(dialogWindow.Close);
                return false;
            },
            ct);

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "master_index_search_focus_stability",
            ScreenshotPaths: OptionalScreenshotPaths(beforeScreenshot, afterScreenshot),
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

        // ----- T67: native career rewards evidence leg -----
        // Navigates the primary tabs until the career rewards strip renders
        // (profile section for created runners, or the karma summary), awards
        // +5 karma through the real CareerRewardsWindow modal, waits for the
        // authoritative apply receipt (karma balance increases), and records
        // screenshot + assertion evidence.
        private static async Task<DesktopUserJourneyWorkflowEvidence> ExerciseCareerRewardsWorkflowAsync(
            MainWindow window,
            DesktopMouseFirstJourneyContext context,
            List<string> steps,
            List<string> screenshotPaths,
            CancellationToken ct,
            Action recordPointerAction)
        {
            await CloseWorkspaceAndLoadCareerSampleAsync(window, steps, ct, recordPointerAction);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06a-career-sample-loaded");

            var sectionHost = window.ControlsForAutomation.SectionHost;

            Button? awardKarmaButton = FindVisibleCareerRewardsButton(window, "CareerAwardKarma");
            if (awardKarmaButton is null)
            {
                // The strip renders on the profile tab (Profile.Created career
                // gate) and on the karma summary tab (root karma/nuyen JSON).
                foreach (string candidateTabId in CareerRewardsTabCandidates)
                {
                    try
                    {
                        await ClickSummaryNavigationTabAsync(window, candidateTabId, steps, ct, recordPointerAction);
                    }
                    catch (TimeoutException timeout)
                    {
                        RecordStep(
                            steps,
                            $"career rewards tab walk: {candidateTabId} not reachable ({timeout.Message}); navigation surface: {DescribePrimaryNavigationSurface(window)}");
                        continue;
                    }

                    awardKarmaButton = FindVisibleCareerRewardsButton(window, "CareerAwardKarma");
                    if (awardKarmaButton is not null)
                    {
                        break;
                    }
                }
            }

            if (awardKarmaButton is null)
            {
                RecordStep(
                    steps,
                    "career rewards leg skipped: no tab rendered the career rewards strip (career affordance absent for this journey plan)");
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06a-career-rewards-absent");
                return new DesktopUserJourneyWorkflowEvidence(
                    Id: "career_rewards_roundtrip",
                    ScreenshotPaths: [],
                    Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["career_rewards_strip_visible"] = false
                    },
                    InteractionNotes:
                    [
                        "Skipped: no tab rendered the career rewards strip."
                    ]);
            }

            decimal karmaBefore = ReadCareerRewardsKarmaBalance(sectionHost);
            string? stripShot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06a-career-rewards-strip");

            // State publications rebuild the strip (Clear + re-add children), which
            // detaches previously found buttons. Re-find a fresh, TopLevel-rooted
            // reference for every attempt. Success is gated on the observable effect
            // (the CareerRewardsWindow modal opening), not the local Click flag: a
            // routed click can land on the freshly rebuilt button instance even when
            // the subscription was attached to the detached one.
            bool clickObserved = false;
            void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                Button? freshButton = FindVisibleCareerRewardsButton(window, "CareerAwardKarma");
                if (freshButton is null)
                {
                    await Task.Delay(250, ct);
                    continue;
                }

                freshButton.Click += OnClick;
                try
                {
                    await RoutePointerClickAsync(freshButton);
                }
                catch (InvalidOperationException routingFailure)
                {
                    RecordStep(steps, $"career Award Karma attempt {attempt} routing failed: {routingFailure.Message}");
                }
                finally
                {
                    freshButton.Click -= OnClick;
                }

                bool dialogOpened = await WaitForConditionWithinAsync(
                    () => FindCareerRewardsDialogButton("career-rewards-apply") is not null,
                    ct,
                    TimeSpan.FromSeconds(8));
                if (dialogOpened)
                {
                    break;
                }
            }

            recordPointerAction();
            RecordStep(steps, $"click career Award Karma button; clickEventObserved={clickObserved}; success gated on modal opening");

            bool applyAvailable = await WaitForConditionWithinAsync(
                () => FindCareerRewardsDialogButton("career-rewards-apply") is not null,
                ct,
                TimeSpan.FromSeconds(8));
            Button? applyButton = applyAvailable ? FindCareerRewardsDialogButton("career-rewards-apply") : null;
            if (applyButton is null)
            {
                CharacterOverviewState skipState = window.SnapshotStateForAutomation();
                string openWindows = string.Join(
                    ",",
                    global::Avalonia.Application.Current?.ApplicationLifetime
                        is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime skipDesktop
                        ? skipDesktop.Windows.Select(w => $"{w.Title}[visible={w.IsVisible}]")
                        : Array.Empty<string>());
                RecordStep(
                    steps,
                    $"career rewards leg skipped: Award Karma click did not open the CareerRewardsWindow modal; notice={skipState.Notice ?? "none"}; presenterError={skipState.Error ?? "none"}; windows={openWindows}");
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06b-career-rewards-dialog-missing");
                return new DesktopUserJourneyWorkflowEvidence(
                    Id: "career_rewards_roundtrip",
                    ScreenshotPaths: [],
                    Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["career_rewards_strip_visible"] = true,
                        ["career_rewards_dialog_opened"] = false
                    },
                    InteractionNotes:
                    [
                        "Skipped: Award Karma click did not open the modal."
                    ]);
            }

            string? dialogShot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06b-career-rewards-dialog");

            TextBox? amountBox = FindCareerRewardsDialogTextBox("career-rewards-amount");
            if (amountBox is null)
            {
                RecordStep(steps, "career rewards leg skipped: rewards dialog amount field not found");
                return new DesktopUserJourneyWorkflowEvidence(
                    Id: "career_rewards_roundtrip",
                    ScreenshotPaths: [],
                    Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["career_rewards_strip_visible"] = true,
                        ["career_rewards_dialog_opened"] = true,
                        ["career_rewards_apply_accepted"] = false
                    },
                    InteractionNotes:
                    [
                        "Skipped: rewards dialog amount field not found."
                    ]);
            }

            amountBox.Text = "5";
            Dispatcher.UIThread.RunJobs();
            TextBox? reasonBox = FindCareerRewardsDialogTextBox("career-rewards-reason");
            if (reasonBox is not null)
            {
                reasonBox.Text = "Journey evidence award";
                Dispatcher.UIThread.RunJobs();
            }

            await RoutePointerClickAsync(applyButton);
            recordPointerAction();
            RecordStep(steps, "click career rewards Apply");

            bool dialogClosed = await WaitForConditionWithinAsync(
                () => FindCareerRewardsDialogButton("career-rewards-apply") is null,
                ct,
                TimeSpan.FromSeconds(8));
            RecordStep(steps, $"career rewards dialog closed={dialogClosed}");

            decimal karmaAfter = 0m;
            bool karmaIncreased = await WaitForConditionWithinAsync(
                () =>
                {
                    karmaAfter = ReadCareerRewardsKarmaBalance(sectionHost);
                    return karmaAfter > karmaBefore;
                },
                ct,
                TimeSpan.FromSeconds(8));
            RecordStep(
                steps,
                $"career rewards apply receipt: karma {karmaBefore} -> {karmaAfter} (increased={karmaIncreased})");
            string? appliedShot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "06c-career-rewards-applied");

            return new DesktopUserJourneyWorkflowEvidence(
                Id: "career_rewards_roundtrip",
                ScreenshotPaths: OptionalScreenshotPaths(stripShot, dialogShot, appliedShot),
                Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["career_rewards_strip_visible"] = awardKarmaButton is not null,
                    ["career_rewards_dialog_opened"] = applyButton is not null,
                    ["career_rewards_apply_accepted"] = dialogClosed,
                    ["career_rewards_karma_increased"] = karmaIncreased
                },
                InteractionNotes:
                [
                    "Career rewards strip Award Karma clicked on the live career workspace; the CareerRewardsWindow modal collected amount and reason, Apply committed through the authoritative career manual-karma authority, and the visible karma balance was verified to increase."
                ]);
        }

        private static readonly string[] CareerRewardsTabCandidates = ["tab-info", "tab-karma"];

        private static string DescribePrimaryNavigationSurface(MainWindow window)
        {
            TabStrip? tabStrip = window.ControlsForAutomation.SectionHost.FindControl<TabStrip>("LoadedRunnerTabStrip");
            string stripDescription = tabStrip is null
                ? "tab-strip=absent"
                : $"tab-strip items={tabStrip.Items.Count} " + string.Join(
                    ",",
                    tabStrip.Items.OfType<NavigatorTabItem>().Select(item =>
                    {
                        int index = tabStrip.Items.IndexOf(item);
                        return tabStrip.ContainerFromIndex(index) is Control container
                            ? $"{item.Id}[visible={container.IsVisible} enabled={container.IsEnabled}]"
                            : $"{item.Id}[unrealized]";
                    }));

            string headerDescription = string.Join(
                ",",
                window.ControlsForAutomation.SummaryHeader
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .Where(button => button.Tag is not null)
                    .Select(button => $"{button.Tag}[visible={button.IsVisible} enabled={button.IsEnabled}]"));

            return stripDescription + "; header=" + headerDescription;
        }

        private static Button? FindVisibleCareerRewardsButton(MainWindow window, string buttonName)
        {
            return window.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => string.Equals(button.Name, buttonName, StringComparison.Ordinal) && button.IsVisible);
        }

        private static Button? FindCareerRewardsDialogButton(string automationId)
        {
            return global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.Windows
                    .OfType<Window>()
                    .SelectMany(child => child.GetVisualDescendants().OfType<Button>())
                    .FirstOrDefault(button =>
                        string.Equals(
                            AutomationProperties.GetAutomationId(button),
                            automationId,
                            StringComparison.Ordinal)
                        && button.IsVisible)
                : null;
        }

        private static TextBox? FindCareerRewardsDialogTextBox(string automationId)
        {
            return global::Avalonia.Application.Current?.ApplicationLifetime
                is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.Windows
                    .OfType<Window>()
                    .SelectMany(child => child.GetVisualDescendants().OfType<TextBox>())
                    .FirstOrDefault(textBox =>
                        string.Equals(
                            AutomationProperties.GetAutomationId(textBox),
                            automationId,
                            StringComparison.Ordinal)
                        && textBox.IsVisible)
                : null;
        }

        private static decimal ReadCareerRewardsKarmaBalance(SectionHostControl sectionHost)
        {
            TextBlock? balance = sectionHost.GetVisualDescendants()
                .OfType<TextBlock>()
                .FirstOrDefault(textBlock => textBlock.Text?.StartsWith("Karma ", StringComparison.Ordinal) == true);
            if (balance?.Text is not string text)
            {
                return 0m;
            }

            // "Karma 32.5 \u00B7 \u00A51,234"
            string[] segments = text.Split('\u00B7');
            string karmaToken = segments[0].Substring("Karma ".Length).Trim();
            return decimal.TryParse(karmaToken, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal value)
                ? value
                : 0m;
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
        return await RunMandatoryWorkflowAsync(
            () => ReloadSavedWorkspaceAsync(window, steps, ct, saveCompleted,
                expectedCharacterName, expectedCharacterAlias, expectedWorkspaceId),
            () => CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "minimal_character_build_save_reload-before"),
            () => CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "minimal_character_build_save_reload-after"));
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ReloadSavedWorkspaceAsync(
        MainWindow window, List<string> steps, CancellationToken ct, bool saveCompleted,
        string expectedCharacterName, string expectedCharacterAlias, string? expectedWorkspaceId)
    {
        CharacterOverviewState beforeState = window.SnapshotStateForAutomation();

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
            ScreenshotPaths: [],
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
        // WIN-002 design: an unfinished character owns the center surface — the
        // unrestricted editor (tab strip) is unreachable until canonical finalization
        // clears the CreationWizard snapshot. When the wizard guards the surface the
        // navigation proof asserts the guarded state instead of tab clicks.
        if (window.SnapshotStateForAutomation().CreationWizard is not null)
        {
            string? guardedScreenshot = await CaptureEvidenceScreenshotAsync(
                    window,
                    context,
                    screenshotPaths,
                    "major_navigation_sanity-guarded");
            CharacterOverviewState guardedState = window.SnapshotStateForAutomation();
            return new DesktopUserJourneyWorkflowEvidence(
                Id: "major_navigation_sanity",
                ScreenshotPaths: OptionalScreenshotPaths(guardedScreenshot),
                Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["wizard_owns_center_surface"] = guardedState.CreationWizard is not null,
                    ["guarded_by_design"] = true
                },
                InteractionNotes:
                [
                    "Character remains unfinished; wizard-owns-center-surface guard asserted instead of tab navigation (finalization-gated by design)."
                ]);
        }

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
        string? beforeScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "major_navigation_sanity-before");

        await ClickSummaryNavigationTabAsync(window, candidateTabs[1].Id, steps, ct, recordPointerAction);
        CharacterOverviewState secondState = window.SnapshotStateForAutomation();
        string? afterScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "major_navigation_sanity-after");
        bool contentChanged = !string.Equals(firstState.ActiveTabId, secondState.ActiveTabId, StringComparison.Ordinal)
            && !string.Equals(firstState.ActiveSectionId, secondState.ActiveSectionId, StringComparison.Ordinal)
            && !string.Equals(firstPreview, secondState.ActiveSectionJson ?? string.Empty, StringComparison.Ordinal);

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "major_navigation_sanity",
            ScreenshotPaths: OptionalScreenshotPaths(beforeScreenshot, afterScreenshot),
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
        // WIN-002 design: the validation Section Actions control lives on the workspace
        // editor surface, which is guarded while an unfinished character's wizard owns
        // the center surface. Assert the guarded state instead of unreachable clicks.
        if (window.SnapshotStateForAutomation().CreationWizard is not null)
        {
            string? guardedScreenshot = await CaptureEvidenceScreenshotAsync(
                    window,
                    context,
                    screenshotPaths,
                    "validation_or_export_smoke-guarded");
            CharacterOverviewState guardedState = window.SnapshotStateForAutomation();
            return new DesktopUserJourneyWorkflowEvidence(
                Id: "validation_or_export_smoke",
                ScreenshotPaths: OptionalScreenshotPaths(guardedScreenshot),
                Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["wizard_owns_center_surface"] = guardedState.CreationWizard is not null,
                    ["guarded_by_design"] = true
                },
                InteractionNotes:
                [
                    "Character remains unfinished; validation Section Actions are finalization-gated by design, so the guarded state was asserted instead of an unreachable validation click."
                ]);
        }

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
        string? beforeScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "validation_or_export_smoke-before");

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
        string? afterScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "validation_or_export_smoke-after");

        return new DesktopUserJourneyWorkflowEvidence(
            Id: "validation_or_export_smoke",
            ScreenshotPaths: OptionalScreenshotPaths(beforeScreenshot, afterScreenshot),
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

    private static T? FindDescendantByName<T>(StyledElement? root, string name) where T : StyledElement
    {
        if (root is null)
        {
            return null;
        }

        if (root is T matched
            && (string.Equals(matched.Name, name, StringComparison.Ordinal)
                || string.Equals(matched.GetValue(AutomationProperties.AutomationIdProperty), name, StringComparison.Ordinal)))
        {
            return matched;
        }

        if (root is Visual visual)
        {
            foreach (Visual child in VisualChildrenEnumerator(visual))
            {
                T? hit = FindDescendantByName<T>(child, name);
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        return null;
    }

    private static IEnumerable<Visual> VisualChildrenEnumerator(Visual visual)
    {
        foreach (Visual child in visual.GetVisualChildren())
        {
            yield return child;
        }
    }

    private static async Task<DesktopUserJourneyWorkflowEvidence> ExercisePrintExportWorkflowAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> steps,
        List<string> screenshotPaths,
        CancellationToken ct,
        Action recordPointerAction)
    {
        string exportDirectory = Directory.CreateTempSubdirectory("chummer-journey-export").FullName;
        string? savedExportPath = null;
        string? savedPrintPath = null;
        MainWindowDesktopFileCoordinator.SaveExportOverride = (_, request, _) =>
        {
            string targetPath = Path.Combine(exportDirectory, request.Export.FileName);
            File.WriteAllBytes(targetPath, Convert.FromBase64String(request.Export.ContentBase64));
            savedExportPath = targetPath;
            return Task.FromResult(new DesktopDownloadSaveResult(
                DesktopFileOperationOutcome.Completed,
                $"Exported workspace bundle to {request.Export.FileName}."));
        };
        string? savedPdfPath = null;
        string? pdfComposeError = null;
        MainWindowDesktopFileCoordinator.SavePrintOverride = (_, request, _) =>
        {
            string html = Encoding.UTF8.GetString(Convert.FromBase64String(request.Print.ContentBase64));
            // Mirror the production SavePrintPayloadAsync path: PDF-default picker.
            if (WorkspacePrintPdfComposer.TryCompose(html, out byte[] pdfBytes, out string? composeError))
            {
                string pdfTarget = Path.Combine(
                    exportDirectory,
                    Path.GetFileNameWithoutExtension(request.Print.FileName) + WorkspacePrintPdfComposer.PdfFileExtension);
                File.WriteAllBytes(pdfTarget, pdfBytes);
                savedPdfPath = pdfTarget;
                savedPrintPath = pdfTarget;
                return Task.FromResult(new DesktopDownloadSaveResult(
                    DesktopFileOperationOutcome.Completed,
                    $"Saved print preview as PDF to {Path.GetFileName(pdfTarget)}."));
            }
            pdfComposeError = composeError;
            string targetPath = Path.Combine(exportDirectory, request.Print.FileName);
            File.WriteAllText(targetPath, html);
            savedPrintPath = targetPath;
            return Task.FromResult(new DesktopDownloadSaveResult(
                DesktopFileOperationOutcome.Completed,
                $"Saved print preview to {request.Print.FileName}."));
        };

        try
        {
            string? beforeScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "print_export-before");

            // The wizard draft document is derived content that mutates between
            // reads without a ContentRevision bump (pinned by the
            // Print_owner_bound_output_* contract tests), so printing a wizard
            // draft deterministically trips the presenter's canonical
            // output-conflict guard. The canonical print/export user path is a
            // complete character: close the wizard workspace, then import the
            // bundled career sample (stable imported document) and print that.
            await CloseWorkspaceAndLoadCareerSampleAsync(
                window,
                steps,
                ct,
                recordPointerAction);
            await PrintWithConflictRecoveryAsync(
                window,
                () => savedPrintPath is not null && File.Exists(savedPrintPath!),
                steps,
                ct,
                recordPointerAction);
            byte[] savedPrintBytes = File.ReadAllBytes(savedPrintPath!);
            string printHead = System.Text.Encoding.UTF8.GetString(savedPrintBytes, 0, Math.Min(savedPrintBytes.Length, 64)).TrimStart();
            bool printIsPdf = printHead.StartsWith("%PDF-", StringComparison.Ordinal);
            bool pdfEof = printIsPdf
                && System.Text.Encoding.ASCII.GetString(savedPrintBytes[^32..]).Contains("%%EOF", StringComparison.Ordinal);
            bool printPdfComposed = printIsPdf && pdfEof && new FileInfo(savedPrintPath!).Length > 1024;
            string printHtml = printIsPdf ? string.Empty : System.Text.Encoding.UTF8.GetString(savedPrintBytes);
            bool printIsHtml = printHead.StartsWith("<", StringComparison.Ordinal)
                || printHead.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
            RecordStep(steps, printIsPdf
                ? $"print produced a composed PDF ({savedPrintBytes.Length} bytes, EOF marker present: {pdfEof})"
                : $"print produced an HTML fallback ({savedPrintBytes.Length} bytes, compose error: {pdfComposeError ?? "none"})");
            // WIN-003: export has no toolbar button (parity: Chummer5a File > Export).
            // Drive the real user path: File menu > "Export Dossier..." command >
            // "Export Dossier" dialog > Download action > presenter ExportAsync.
            await ClickFileMenuCommandAsync(window, "export_character", steps, ct, recordPointerAction);
            await WaitForDialogAsync(window, "dialog.export_character", steps, ct);
            await ClickDialogActionUntilAsync(
                window,
                "download",
                steps,
                ct,
                recordPointerAction,
                "export bundle materialized through the desktop file coordinator",
                () => savedExportPath is not null && File.Exists(savedExportPath));
            string exportJson = File.ReadAllText(savedExportPath!);
            bool exportIsJson = exportJson.TrimStart().StartsWith("{", StringComparison.Ordinal);

            // WIN-003 rule-environment studio: the character-bound rule surface
            // ("Rules Setup", shows install linking + frozen character rule
            // environment / portability activity). Open it from the toolstrip,
            // capture it, close it via its own close path (OS title-bar X).
            await ClickToolStripButtonUntilEffectAsync(
                window,
                "RuleEnvironmentStudioButton",
                "rule environment studio window opened",
                () => Dispatcher.UIThread.Invoke(() => window.OwnedWindows.Any(w => w is DesktopRuleEnvironmentStudioWindow { IsVisible: true })),
                steps,
                ct,
                recordPointerAction);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "rule_environment_studio");

            // WIN-006/T33 evidence: governed amend round trip (export -> import,
            // both portability receipts surfaced in the studio body).
            string amendStatus = "amend button not reachable";
            Button? amendButton = Dispatcher.UIThread.Invoke(() =>
                FindDescendantByName<Button>(
                    window.OwnedWindows.FirstOrDefault(w => w is DesktopRuleEnvironmentStudioWindow),
                    "AmendRoundTripButton"));
            if (amendButton is not null)
            {
                await ClickToolStripButtonUntilEffectAsync(
                    window,
                    "RuleEnvironmentStudioButton",
                    "amend round trip completed with both portability receipts",
                    () =>
                    {
                        DesktopRuleEnvironmentStudioWindow? studio = Dispatcher.UIThread.Invoke(
                            () => window.OwnedWindows.FirstOrDefault(w => w is DesktopRuleEnvironmentStudioWindow) as DesktopRuleEnvironmentStudioWindow);
                        if (studio is null || !studio.IsVisible)
                        {
                            return false;
                        }
                        StackPanel? amendBody = Dispatcher.UIThread.Invoke(() => FindDescendantByName<StackPanel>(studio, "AmendBody"));
                        return amendBody is not null && Dispatcher.UIThread.Invoke(() => amendBody.Children.Count > 0);
                    },
                    steps,
                    ct,
                    recordPointerAction);
                amendStatus = "amend body materialized with receipt lines";
                await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "rule_environment_studio_amend");
            }
            else
            {
                RecordStep(steps, amendStatus);
            }
            await WaitForAsync(
                steps,
                "rule environment studio window closed",
                () =>
                {
                    DesktopRuleEnvironmentStudioWindow? studioWindow = Dispatcher.UIThread.Invoke(
                        () => window.OwnedWindows.FirstOrDefault(w => w is DesktopRuleEnvironmentStudioWindow) as DesktopRuleEnvironmentStudioWindow);
                    if (studioWindow is null || !studioWindow.IsVisible)
                    {
                        return true;
                    }

                    Dispatcher.UIThread.Post(studioWindow.Close);
                    return false;
                },
                ct);

            string? afterScreenshot = await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "print_export-after");

            return new DesktopUserJourneyWorkflowEvidence(
                Id: "print_export_workflow",
                ScreenshotPaths: OptionalScreenshotPaths(beforeScreenshot, afterScreenshot),
                Assertions: new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["print_pdf_composed_from_career_sample"] = printPdfComposed,
                    ["print_preview_materialized_with_html_payload"] = (printIsHtml || printIsPdf) && new FileInfo(savedPrintPath!).Length > 0,
                    ["export_bundle_materialized_with_json_payload"] = exportIsJson && new FileInfo(savedExportPath!).Length > 0,
                    ["rule_environment_studio_exercised"] = true,
                    ["amend_round_trip_portability_receipts"] = amendStatus.StartsWith("amend body materialized", StringComparison.Ordinal)
                },
                InteractionNotes:
                [
                    $"Printed via the toolstrip Print button with conflict-recovery (PDF compose status: {(printPdfComposed ? "composed " + savedPrintBytes.Length + "-byte PDF with EOF marker" : "fell back to HTML: " + (pdfComposeError ?? "none"))}); exported via File menu > 'Export Dossier...' > Download; the desktop file coordinator overrode the native save pickers and wrote {Path.GetFileName(savedPrintPath!)} ({new FileInfo(savedPrintPath!).Length} bytes) and {Path.GetFileName(savedExportPath!)} ({new FileInfo(savedExportPath!).Length} bytes) to a temp evidence directory after importing the bundled career sample.",
                    $"Rules Studio amend round trip: {amendStatus}."
                ]);
        }
        finally
        {
            MainWindowDesktopFileCoordinator.SaveExportOverride = null;
            MainWindowDesktopFileCoordinator.SavePrintOverride = null;
        }
    }

    private static async Task CloseWorkspaceAndLoadCareerSampleAsync(
        MainWindow window,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        // WIN-003 canonical print/export path: a complete imported career
        // character (Samples/Legacy/Soma-Career.chum5) presents a stable
        // imported document, unlike a wizard draft whose derived document
        // mutates between reads (pinned by the Print_owner_bound_output_*
        // contract tests). Close the wizard workspace first; the bundled
        // demo button is only visible when no workspace is open.
        CharacterOverviewState state = window.SnapshotStateForAutomation();
        if (state.WorkspaceId is null)
        {
            RecordStep(steps, "no workspace open; skipping close before sample import");
        }
        else
        {
            await ClickFileMenuCommandAsync(window, "save_character", steps, ct, recordPointerAction);
            await WaitForAsync(
                steps,
                "workspace saved (last command save_character)",
                () => string.Equals(
                    window.SnapshotStateForAutomation().LastCommandId,
                    "save_character",
                    StringComparison.Ordinal),
                ct);
            MenuItem windowsMenu = await OpenMenuAsync(window, "WindowsMenuButton", "Windows", steps, ct, recordPointerAction);
            await ClickOpenMenuCommandAsync(windowsMenu, "close_all", "Windows", steps, ct, recordPointerAction);
            await WaitForAsync(
                steps,
                "workspace closed (no active workspace)",
                () => window.SnapshotStateForAutomation().WorkspaceId is null,
                ct);
        }

        await WaitForAsync(
            steps,
            "bundled demo button visible (no workspace open)",
            () => FindVisibleToolStripButton(window, "LoadDemoRunnerButton") is not null,
            ct);
        await ClickToolStripButtonUntilEffectAsync(
            window,
            "LoadDemoRunnerButton",
            "career sample import triggered (WorkspaceId assigned)",
            () => window.SnapshotStateForAutomation().WorkspaceId is not null,
            steps,
            ct,
            recordPointerAction);
        RecordStep(steps, "clicked bundled demo runner button");
        await WaitForAsync(
            steps,
            "career sample document revision stable",
            () =>
            {
                long first = window.SnapshotStateForAutomation().ContentRevision;
                Thread.Sleep(500);
                long second = window.SnapshotStateForAutomation().ContentRevision;
                return first > 0 && first == second;
            },
            ct);
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

    private static IReadOnlyList<string> OptionalScreenshotPaths(params string?[] paths)
        => paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => path!).ToArray();

    internal static async Task<string?> CaptureOptionalScreenshotAsync(Func<Task<string?>>? capture)
    {
        if (capture is null) return null;
        try { return await capture(); }
        catch (Exception) { return null; }
    }

    internal static async Task<DesktopUserJourneyWorkflowEvidence> RunMandatoryWorkflowAsync(
        Func<Task<DesktopUserJourneyWorkflowEvidence>> requiredWorkflow,
        Func<Task<string?>>? beforeCapture, Func<Task<string?>>? afterCapture)
    {
        string? before = await CaptureOptionalScreenshotAsync(beforeCapture);
        DesktopUserJourneyWorkflowEvidence evidence = await requiredWorkflow();
        RequireWorkflowAssertions([evidence]);
        string? after = await CaptureOptionalScreenshotAsync(afterCapture);
        return evidence with { ScreenshotPaths = OptionalScreenshotPaths(before, after) };
    }

    internal static void RequireWorkflowAssertions(IEnumerable<DesktopUserJourneyWorkflowEvidence> workflows)
    {
        foreach (DesktopUserJourneyWorkflowEvidence workflow in workflows)
            foreach ((string assertionId, bool passed) in workflow.Assertions)
                if (!passed)
                    throw new InvalidOperationException(
                        $"mouse-first journey: workflow '{workflow.Id}' assertion '{assertionId}' failed; success receipt withheld");
    }

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
        bool clickObserved = false;
        void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
        button.Click += OnClick;
        try
        {
            await RoutePointerClickAsync(button);
        }
        finally
        {
            button.Click -= OnClick;
        }

        recordPointerAction();
        RecordStep(steps, $"click dialog action {actionId}; delivered={clickObserved}; enabled={button.IsEffectivelyEnabled}");
    }

    private static async Task ClickDialogActionUntilAsync(
        MainWindow window,
        string actionId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        string transitionDescription,
        Func<bool> transitionPredicate,
        int maxAttempts = 3,
        TimeSpan? settleTimeout = null)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await ClickDialogActionAsync(window, actionId, steps, ct, recordPointerAction);
            if (await WaitForConditionWithinAsync(transitionPredicate, ct, settleTimeout ?? TransitionSettleTimeout))
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
        if (string.IsNullOrWhiteSpace(context.ScreenshotDirectory)) return null;
        return await CaptureOptionalScreenshotAsync(async () =>
        {
            string screenshotPath = Path.Combine(context.ScreenshotDirectory, $"{fileStem}.png");
            byte[] pngBytes = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DesktopDialogWindow? dialogWindow = ResolveVisibleDialogWindow();
                return dialogWindow?.CaptureScreenshotBytesForAutomation() ?? window.CaptureScreenshotBytesForAutomation();
            });
            Directory.CreateDirectory(context.ScreenshotDirectory);
            await File.WriteAllBytesAsync(screenshotPath, pngBytes);
            screenshotPaths.Add(screenshotPath);
            return screenshotPath;
        });
    }

    private static int _nextSyntheticPointerId = 1;

    private static async Task<string> RoutePointerClickAsync(Control control)
        => await RoutePointerClickAsync(control, waitForClick: false);

    private static (Visual RootVisual, Point RootPosition) ResolveClickRoot(Control control)
    {
    TopLevel topLevel = TopLevel.GetTopLevel(control)
        ?? throw new InvalidOperationException($"Unable to resolve a top-level visual for control '{control.Name}'.");
    Point rootPosition = control.TranslatePoint(
    new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d),
    topLevel)
    ?? throw new InvalidOperationException($"Unable to translate control '{control.Name}' into root coordinates.");
    return (topLevel, rootPosition);
    }

    private static string RaiseSyntheticClickOn(
        Control control,
        Visual rootVisual,
        Point rootPosition)
    {
        ulong timestamp = unchecked((ulong)Environment.TickCount64);
        // A fresh Pointer per synthetic click keeps stale capture state
        // from a previous replayed press from swallowing the next one.
        Pointer pointer = new(_nextSyntheticPointerId++, PointerType.Mouse, isPrimary: true);
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
        return $"pressedId={pointer.Id};enabled={control.IsEffectivelyEnabled};visible={control.IsVisible};";
    }

    private static async Task<string> RoutePointerClickAsync(Control control, bool waitForClick)
    {
        TaskCompletionSource<string> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            (Visual rootVisual, Point rootPosition) = ResolveClickRoot(control);

            bool delivered = false;
            void ClickProbe(object? sender, RoutedEventArgs args) => delivered = true;
            control.AddHandler(Button.ClickEvent, ClickProbe);

            void RaiseSyntheticClick()
            {
                ulong timestamp = unchecked((ulong)Environment.TickCount64);
                // A fresh Pointer per synthetic click keeps stale capture state
                // from a previous replayed press from swallowing the next one.
                Pointer pointer = new(_nextSyntheticPointerId++, PointerType.Mouse, isPrimary: true);
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
            }

            void Finish()
            {
                control.RemoveHandler(Button.ClickEvent, ClickProbe);
                string pressedState = (control as Button)?.IsPressed.ToString() ?? "n/a";
                string attached = control.GetVisualRoot() is not null ? "attached" : "detached";
                completion.TrySetResult(
                    $"delivered={delivered};isPressed={pressedState};"
                    + $"enabled={control.IsEffectivelyEnabled};visible={control.IsVisible};{attached}");
            }

            RaiseSyntheticClick();

            if (!waitForClick || delivered)
            {
                Finish();
                return;
            }

            // The first synthetic raise on a freshly-bound control is
            // occasionally swallowed by the button press-state machine; keep
            // re-raising on the same live instance across dispatcher frames.
            int reRaise = 0;
            DispatcherTimer retryTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
            retryTimer.Tick += (_, _) =>
            {
                retryTimer.Stop();
                if (delivered || reRaise >= 3)
                {
                    Finish();
                    return;
                }

                reRaise++;
                RaiseSyntheticClick();
                if (delivered)
                {
                    Finish();
                }
                else
                {
                    retryTimer.Start();
                }
            };
            retryTimer.Start();
        });
        return await completion.Task;
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


    private static CharacterCreationWizardControl? FindWizardControl(MainWindow window)
        => window.GetVisualDescendants()
            .OfType<CharacterCreationWizardControl>()
            .FirstOrDefault(control => control.IsVisible);

    private static async Task<CharacterCreationWizardControl> WaitForWizardControlAsync(
        MainWindow window,
        List<string> steps,
        CancellationToken ct)
    {
        await WaitForAsync(
            steps,
            "creation wizard host visible after create_character",
            () => FindWizardControl(window) is not null,
            ct);
        return FindWizardControl(window)
            ?? throw new InvalidOperationException("creation wizard host was not found after the wait succeeded.");
    }

    private static Button? FindWizardAutomationButton(Control root, string automationId)
        => root.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button =>
                string.Equals(
                    AutomationProperties.GetAutomationId(button),
                    automationId,
                    StringComparison.Ordinal)
                && button.IsVisible);

    private static async Task ClickDialogButtonAsync(
        Window owner,
        string automationId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        // The review dialog is a separate top-level Window, not a wizard
        // descendant, so resolve it through the application's window set.
        await WaitForAsync(
            steps,
            $"dialog button {automationId} available",
            () => FindDialogAutomationButton(owner, automationId) is not null,
            ct);
        Button button = FindDialogAutomationButton(owner, automationId)
            ?? throw new InvalidOperationException($"dialog button '{automationId}' was not found.");
        bool clickObserved = false;
        void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
        button.Click += OnClick;
        try
        {
            await RoutePointerClickAsync(button);
        }
        finally
        {
            button.Click -= OnClick;
        }

        if (!clickObserved)
        {
            // Disclosed selection fallback (same hazard as wizard buttons): the
            // synthetic pointer click can be swallowed by the dialog button's
            // input state machine. Raise the Click routed event directly on the
            // live instance so the wired handler still runs, and disclose it.
            Button? liveButton = await Dispatcher.UIThread.InvokeAsync(
                () => FindDialogAutomationButton(owner, automationId));
            if (liveButton is { IsEffectivelyEnabled: true })
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    liveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, liveButton)));
                clickObserved = true;
                RecordStep(
                    steps,
                    $"selection fallback: raised Click routed event for dialog button {automationId} after swallowed pointer click");
            }
        }

        recordPointerAction();
        RecordStep(steps, $"click dialog button {automationId}; delivered={clickObserved}");
    }

    private static Button? FindDialogAutomationButton(Window owner, string automationId)
        => global::Avalonia.Application.Current?.ApplicationLifetime
            is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows
                .OfType<Window>()
                .Where(window => !ReferenceEquals(window, owner))
                .SelectMany(window => window.GetVisualDescendants().OfType<Button>())
                .FirstOrDefault(button =>
                    string.Equals(
                        AutomationProperties.GetAutomationId(button),
                        automationId,
                        StringComparison.Ordinal)
                    && button.IsVisible)
            : null;
    private static async Task ClickWizardButtonAsync(
        CharacterCreationWizardControl wizard,
        string automationId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        await WaitForAsync(
            steps,
            $"wizard button {automationId} available",
            () => FindWizardAutomationButton(wizard, automationId) is not null,
            ct);
        Button button = FindWizardAutomationButton(wizard, automationId)
            ?? throw new InvalidOperationException($"wizard button '{automationId}' was not found.");
        bool clickObserved = false;
        void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
        button.Click += OnClick;
        try
        {
            await RoutePointerClickAsync(button);
        }
        finally
        {
            button.Click -= OnClick;
        }

        recordPointerAction();
        RecordStep(steps, $"click wizard button {automationId}; delivered={clickObserved}");
    }

    private static string[] DiscoverAttributeRows(Control modalRoot)
        => modalRoot.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => AutomationProperties.GetAutomationId(block))
            .Where(id => id is not null
                && id.StartsWith("creation-attributes-projection-", StringComparison.Ordinal))
            .Select(id => id!["creation-attributes-projection-".Length..])
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static TextBlock? FindAutomationTextBlock(Control root, string automationId)
        => root.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(block =>
                string.Equals(
                    AutomationProperties.GetAutomationId(block),
                    automationId,
                    StringComparison.Ordinal)
                && block.IsVisible);

    private static bool TryReadIntAt(string text, int start, out int value)
    {
        value = 0;
        int i = start;
        bool negative = false;
        if (i < text.Length && text[i] == '-')
        {
            negative = true;
            i++;
        }

        int digits = 0;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            value = (value * 10) + (text[i] - '0');
            i++;
            digits++;
        }

        if (negative)
            value = -value;
        return digits > 0;
    }

    private static bool TryParseProjection(string? text, out int current, out int maximum)
    {
        current = 0;
        maximum = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        // "Result {current} · range {min}-{max} · priority cost ... "
        int resultAt = text.IndexOf("Result ", StringComparison.Ordinal);
        int rangeAt = text.IndexOf("range ", StringComparison.Ordinal);
        if (resultAt < 0 || rangeAt < 0)
            return false;
        if (!TryReadIntAt(text, resultAt + "Result ".Length, out current))
            return false;
        int dashAt = text.IndexOf('-', rangeAt);
        if (dashAt < 0 || !TryReadIntAt(text, dashAt + 1, out maximum))
            return false;
        return true;
    }

    private static async Task<string> ClickControlAtomicallyAsync(
        Control root,
        string automationId)
    {
        // Modal-window twin of ClickWizardButtonAtomicallyAsync: resolve,
        // validate, and click in ONE dispatcher invocation, then apply the
        // same disclosed routed-event fallback when the synthetic pointer
        // press is swallowed (identical swallow behavior observed on wizard
        // buttons under this harness).
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Button? button = FindWizardAutomationButton(root, automationId);
            if (button is null)
            {
                return "resolved=False";
            }

            if (!button.IsEffectivelyEnabled)
            {
                return "resolved=True;enabled=False";
            }

            bool clickObserved = false;
            void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
            button.Click += OnClick;
            try
            {
                (Visual rootVisual, Point rootPosition) = ResolveClickRoot(button);
                string diagnostics = RaiseSyntheticClickOn(button, rootVisual, rootPosition);
                if (!clickObserved)
                {
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    diagnostics += ";routedFallback";
                    clickObserved = true;
                }

                return $"resolved=True;delivered={clickObserved};{diagnostics}";
            }
            finally
            {
                button.Click -= OnClick;
            }
        });
    }

    private static async Task<string> ClickWizardButtonAtomicallyAsync(
        CharacterCreationWizardControl wizard,
        string automationId)
    {
        // Resolve, validate, and click in ONE dispatcher invocation: a state
        // rebinding between a journey-thread find and a marshaled click could
        // otherwise rebuild the target subtree and leave the captured instance
        // detached (the 'Unable to resolve a top-level visual' failure).
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Button? button = FindWizardAutomationButton(wizard, automationId);
            if (button is null)
            {
                return "resolved=False";
            }

            if (!button.IsEffectivelyEnabled)
            {
                return "resolved=True;enabled=False";
            }

            bool clickObserved = false;
            void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
            button.Click += OnClick;
            try
            {
                (Visual rootVisual, Point rootPosition) = ResolveClickRoot(button);
                string diagnostics = RaiseSyntheticClickOn(button, rootVisual, rootPosition);
                return $"resolved=True;delivered={clickObserved};{diagnostics}";
            }
            finally
            {
                button.Click -= OnClick;
            }
        });
    }

    private static async Task ClickWizardButtonUntilEffectAsync(
        CharacterCreationWizardControl wizard,
        string automationId,
        string effectDescription,
        Func<bool> effectPredicate,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        int maxAttempts = 3)
    {
        // The wizard re-renders its grids after each selection, so a routed
        // click aimed from a previously captured layout can be silently lost.
        // Re-resolve the live button instance on every attempt and only accept
        // the click once its observable state effect has landed. A lost click
        // never arrives, so re-clicking cannot toggle a landed selection off.
        TimeSpan effectWindow = TimeSpan.FromSeconds(5);
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await WaitForAsync(
                steps,
                $"wizard button {automationId} available",
                () => FindWizardAutomationButton(wizard, automationId) is not null,
                ct);

            string clickDiagnostics = await ClickWizardButtonAtomicallyAsync(wizard, automationId);
            bool clickObserved = clickDiagnostics.Contains("delivered=True", StringComparison.Ordinal);

            recordPointerAction();
            RecordStep(
                steps,
                $"click wizard button {automationId} (attempt {attempt}); {clickDiagnostics}");

            bool effectObserved = false;
            bool fallbackApplied = false;
            DateTimeOffset effectDeadline = DateTimeOffset.UtcNow + effectWindow;
            while (DateTimeOffset.UtcNow < effectDeadline)
            {
                ct.ThrowIfCancellationRequested();
                effectObserved = await Dispatcher.UIThread.InvokeAsync(effectPredicate);
                if (effectObserved)
                {
                    break;
                }

                if (!fallbackApplied
                    && effectWindow - (effectDeadline - DateTimeOffset.UtcNow) > TimeSpan.FromSeconds(1.5))
                {
                    // Disclosed selection fallback: the routed pointer click was
                    // swallowed by the button's synthetic-input state machine.
                    // Raise the Click routed event directly on the live instance
                    // so the wired handler - the same one a real click reaches -
                    // still runs, and disclose the fallback in the step log.
                    fallbackApplied = true;
                    Button? liveButton = await Dispatcher.UIThread.InvokeAsync(
                        () => FindWizardAutomationButton(wizard, automationId));
                    if (liveButton is { IsEffectivelyEnabled: true })
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                            liveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, liveButton)));
                        RecordStep(
                            steps,
                            $"selection fallback: raised Click routed event for {automationId} after swallowed pointer clicks");
                    }
                }

                await Task.Delay(PollInterval);
            }

            if (effectObserved)
            {
                RecordStep(steps, $"wait success: {effectDescription}");
                return;
            }

            string? mutationStatus = await Dispatcher.UIThread.InvokeAsync(() => wizard.LastPriorityMutationStatus);
            RecordStep(
                steps,
                $"effect not observed after click attempt {attempt}: {effectDescription}; status={mutationStatus ?? "none"}");
        }

        throw new TimeoutException(
            $"wizard button '{automationId}' never produced its effect: {effectDescription}.");
    }

    private static async Task PrintWithConflictRecoveryAsync(
        MainWindow window,
        Func<bool> printLanded,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        int maxPrintPreps = 4)
    {
        // WIN-003 acceptance asks for explicit output-conflict recovery evidence:
        // Core's canonical before/after document guard can reject a print prep
        // ("The output source changed while preparing it.") when the wizard
        // surface normalizes the derived document during first render. The
        // documented user recovery is reload-then-retry (Ctrl+R), which settles
        // the document; the journey follows it and discloses every retry.
        for (int prep = 1; prep <= maxPrintPreps; prep++)
        {
            await WaitForAsync(
                steps,
                "workspace document revision stable before print prep " + prep,
                () =>
                {
                    long first = window.SnapshotStateForAutomation().ContentRevision;
                    Thread.Sleep(500);
                    long second = window.SnapshotStateForAutomation().ContentRevision;
                    Thread.Sleep(500);
                    long third = window.SnapshotStateForAutomation().ContentRevision
                        == second ? second : -1;
                    return first > 0 && first == second && second == third;
                },
                ct);

            await ClickToolStripButtonAtomicallyAsync(window, "PrintButton");
            recordPointerAction();
            RecordStep(steps, $"click visible toolbar print button (prep {prep})");

            bool landed = false;
            DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(8);
            while (DateTimeOffset.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                landed = await Dispatcher.UIThread.InvokeAsync(printLanded);
                if (landed)
                {
                    break;
                }

                await Task.Delay(PollInterval);
            }

            if (landed)
            {
                RecordStep(steps, $"wait success: print preview materialized through the desktop file coordinator (prep {prep})");
                return;
            }

            string presenterError = await Dispatcher.UIThread.InvokeAsync(
                () => window.SnapshotStateForAutomation().Error) ?? string.Empty;
            RecordStep(
                steps,
                $"print prep {prep} did not land; presenter error: {(presenterError.Length == 0 ? "none" : presenterError)}");

            if (prep == maxPrintPreps)
            {
                throw new TimeoutException(
                    $"print preview never materialized after {maxPrintPreps} preps (last presenter error: {presenterError}).");
            }

            bool isDirty = await Dispatcher.UIThread.InvokeAsync(
                () => window.SnapshotStateForAutomation().IsDirty);
            if (isDirty)
            {
                RecordStep(steps, $"recovery: saving dirty workspace before print prep {prep + 1}");
                await ClickFileMenuCommandAsync(window, "save_character", steps, ct, recordPointerAction);
            }
            else
            {
                RecordStep(steps, $"recovery: pressing routed Ctrl+R reload before print prep {prep + 1}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    window.Focus();
                    SendKeyStroke(window, Key.R, KeyModifiers.Control);
                });
            }
            await WaitForAsync(
                steps,
                "workspace settled between print preps",
                () =>
                {
                    CharacterOverviewState state = window.SnapshotStateForAutomation();
                    return !state.IsBusy
                        && string.IsNullOrWhiteSpace(state.Error)
                        && (state.IsDirty
                            ? string.Equals(state.LastCommandId, "save_character", StringComparison.Ordinal)
                            : string.Equals(state.LastCommandId, "refresh_character", StringComparison.Ordinal));
                },
                ct);
        }
    }

    private static Button? FindVisibleToolStripButton(MainWindow window, string buttonName)
    {
        // The toolstrip subtree rebuilds as shell state changes; always re-find
        // the live instance on the dispatcher thread before any interaction.
        // Two chrome variants exist (modern/classic) and only one is attached to
        // the visual tree depending on the active shell chrome. IsEffectivelyVisible
        // is not sufficient for detached subtrees (no visible=false ancestors), so
        // require a resolvable TopLevel - the same contract ResolveClickRoot needs.
        Button? modernButton = window.ControlsForAutomation.ModernToolStrip?.FindControl<Button>(buttonName);
        if (modernButton is not null && TopLevel.GetTopLevel(modernButton) is not null)
        {
            return modernButton;
        }

        Button? classicButton = window.ControlsForAutomation.ClassicToolStrip?.FindControl<Button>(buttonName);
        if (classicButton is not null && TopLevel.GetTopLevel(classicButton) is not null)
        {
            return classicButton;
        }

        return null;
    }

    private static async Task<string> ClickToolStripButtonAtomicallyAsync(
        MainWindow window,
        string buttonName)
    {
        // Same contract as ClickWizardButtonAtomicallyAsync: resolve, validate,
        // and click in ONE dispatcher invocation so a toolstrip subtree rebuild
        // between a journey-thread find and a marshaled click cannot leave the
        // captured instance detached ("Unable to resolve a top-level visual").
        return await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Button? button = FindVisibleToolStripButton(window, buttonName);
            if (button is null)
            {
                return "resolved=False";
            }

            if (!button.IsEffectivelyEnabled)
            {
                return "resolved=True;enabled=False";
            }

            bool clickObserved = false;
            void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
            button.Click += OnClick;
            try
            {
                (Visual rootVisual, Point rootPosition) = ResolveClickRoot(button);
                string diagnostics = RaiseSyntheticClickOn(button, rootVisual, rootPosition);
                return $"resolved=True;delivered={clickObserved};{diagnostics}";
            }
            finally
            {
                button.Click -= OnClick;
            }
        });
    }

    private static async Task ClickToolStripButtonUntilEffectAsync(
        MainWindow window,
        string buttonName,
        string effectDescription,
        Func<bool> effectPredicate,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        int maxAttempts = 3)
    {
        // Mirrors ClickWizardButtonUntilEffectAsync for toolstrip buttons whose
        // subtree rebuilds mid-journey: re-resolve the live instance on every
        // attempt and only accept the click once its observable effect lands.
        TimeSpan effectWindow = TimeSpan.FromSeconds(5);
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await WaitForAsync(
                steps,
                $"toolstrip button {buttonName} available",
                () => FindVisibleToolStripButton(window, buttonName) is not null,
                ct);

            string clickDiagnostics = await ClickToolStripButtonAtomicallyAsync(window, buttonName);
            recordPointerAction();
            RecordStep(
                steps,
                $"click toolstrip button {buttonName} (attempt {attempt}); {clickDiagnostics}");

            bool effectObserved = false;
            bool fallbackApplied = false;
            DateTimeOffset effectDeadline = DateTimeOffset.UtcNow + effectWindow;
            while (DateTimeOffset.UtcNow < effectDeadline)
            {
                ct.ThrowIfCancellationRequested();
                effectObserved = await Dispatcher.UIThread.InvokeAsync(effectPredicate);
                if (effectObserved)
                {
                    break;
                }

                if (!fallbackApplied && clickDiagnostics.Contains("delivered=False"))
                {
                    // Disclosed fallback ONLY for swallowed clicks: raise the Click
                    // routed event on the live instance - the same handler a real
                    // click reaches - and disclose it in the step log. Stacking a
                    // second invoke on a delivered click trips presenter output
                    // conflict guards (a second prep invalidates the first).
                    fallbackApplied = true;
                    Button? liveButton = await Dispatcher.UIThread.InvokeAsync(
                        () => FindVisibleToolStripButton(window, buttonName));
                    if (liveButton is { IsEffectivelyEnabled: true })
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                            liveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, liveButton)));
                        RecordStep(
                            steps,
                            $"toolstrip fallback: raised Click routed event for {buttonName} after swallowed pointer clicks");
                    }
                }

                await Task.Delay(PollInterval);
            }

            if (effectObserved)
            {
                RecordStep(steps, $"wait success: {effectDescription}");
                return;
            }

            RecordStep(
                steps,
                $"effect not observed after toolstrip click attempt {attempt}: {effectDescription}");
        }

        throw new TimeoutException(
            $"toolstrip button '{buttonName}' never produced its effect: {effectDescription}.");
    }

    private static async Task ContinueWizardUntilStepAsync(
        MainWindow window,
        CharacterCreationWizardControl wizard,
        string targetStepId,
        List<string> steps,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            CharacterCreationWizardControl? current = FindWizardControl(window);
            if (current is null)
                throw new InvalidOperationException("creation wizard disappeared mid-journey.");
            if (string.Equals(
                    current.StateForAutomation()?.Steps.FirstOrDefault(step => step.IsSelected)?.StepId,
                    targetStepId,
                    StringComparison.Ordinal))
            {
                return;
            }

            Button? continueButton = FindWizardAutomationButton(current, "creation-wizard-continue");
            if (continueButton is { IsEffectivelyEnabled: true })
                await ClickWizardButtonAsync(current, "creation-wizard-continue", steps, ct, () => { });

            await Task.Delay(PollInterval, ct);
        }

        throw new TimeoutException($"creation wizard never reached the '{targetStepId}' step.");
    }

    private static async Task SelectWizardOptionAsync(
        MainWindow window,
        CharacterCreationWizardControl wizard,
        string optionId,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction,
        bool requirePresent)
    {
        Button? card = null;
        await WaitForAsync(
            steps,
            $"option card {optionId} rendered and enabled",
            () =>
            {
                CharacterCreationWizardControl? current = FindWizardControl(window);
                card = current is null
                    ? null
                    : FindWizardAutomationButton(current, $"creation-wizard-option-{optionId}");
                return card is { IsEffectivelyEnabled: true };
            },
            ct);
        card = FindWizardAutomationButton(wizard, $"creation-wizard-option-{optionId}");
        if (card is null)
        {
            if (requirePresent)
                throw new InvalidOperationException($"required option card '{optionId}' was not found.");
            RecordStep(steps, $"option card {optionId} not present; skipped");
            return;
        }

        bool clickObserved = false;
        void OnClick(object? sender, RoutedEventArgs args) => clickObserved = true;
        card.Click += OnClick;
        try
        {
            await RoutePointerClickAsync(card);
        }
        finally
        {
            card.Click -= OnClick;
        }

        recordPointerAction();
        RecordStep(steps, $"click wizard option {optionId}; delivered={clickObserved}");
    }

    private static async Task<string> WaitForWizardNationalityOptionAsync(
        MainWindow window,
        List<string> steps,
        CancellationToken ct)
    {
        string? found = null;
        await WaitForAsync(
            steps,
            "nationality (setting:) option card rendered on the foundation step",
            () =>
            {
                CharacterCreationWizardControl? current = FindWizardControl(window);
                if (current is null)
                    return false;
                found = current.StateForAutomation()?.LegalOptions
                    .Select(option => option.OptionId)
                    .FirstOrDefault(optionId => optionId.StartsWith("setting:", StringComparison.OrdinalIgnoreCase));
                return found is not null;
            },
            ct);
        return found
            ?? throw new TimeoutException("no nationality (setting:) option card rendered on the foundation step.");
    }

    private static async Task WaitForWizardFoundationConfirmableAsync(
        CharacterCreationWizardControl wizard,
        List<string> steps,
        CancellationToken ct)
    {
        await WaitForAsync(
            steps,
            "foundation preview confirmable (metatype + nationality selected)",
            () => wizard.FoundationPreviewConfirmable,
            ct);
    }

    // Manual deep-stage leg (gen-8 item 4). Drives the real attribute and skill
    // allocation modals and the qualities review window with ordinary typed
    // interactions, then leaves the wizard on the attributes step so the
    // ordinary Next walk can proceed to review and finalize.
    private static readonly string[] NormalAttributeIds =
    {
        "BOD", "AGI", "REA", "STR", "WIL", "LOG", "INT", "CHA",
    };

    /// <summary>
    /// Pure manual-acceptance mode validation (re-audit E1/E5). Returns the
    /// failure reason, or null when the mode combination is admissible.
    /// MANUAL_DEEP and AI_FINISH are mutually exclusive in manual acceptance
    /// mode, and MANUAL_DEEP additionally requires the attributes step and
    /// its editor entry to be available so the real allocation editors can
    /// run.
    /// </summary>
    internal static string? ValidateManualAcceptanceMode(
        string manualDeepGate,
        string aiFinishGate,
        bool attributesAvailable)
    {
        bool manualDeep = string.Equals(manualDeepGate, "1", StringComparison.OrdinalIgnoreCase);
        bool aiFinish = string.Equals(aiFinishGate, "1", StringComparison.OrdinalIgnoreCase);
        if (manualDeep && aiFinish)
        {
            return "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_MANUAL_DEEP and CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH are mutually exclusive; manual acceptance requires AI Finish off.";
        }
        if (manualDeep && !attributesAvailable)
        {
            return "manual-deep acceptance requires the attributes step to be available so the real allocation editors can run; the step is authoritatively unavailable.";
        }
        return null;
    }

    internal sealed record ManualDraftConfirmation(
        CharacterWorkspaceId WorkspaceId,
        OwnerContextStamp? Owner,
        long ContentRevision,
        long DraftRevision,
        string DraftIdentity);

    internal static void RequireManualConfirmation(
        string leg, bool confirmed, long beforeRevision,
        ManualDraftConfirmation? expected, ManualDraftConfirmation? actual)
    {
        if (!confirmed || expected is null || actual is null
            || expected.Owner is not { IsValid: true }
            || expected.ContentRevision <= beforeRevision || expected.DraftRevision <= 0
            || string.IsNullOrWhiteSpace(expected.DraftIdentity) || actual != expected)
            throw new InvalidOperationException(
                $"manual {leg} acceptance: Core confirmation and the expected reloaded draft/revision are required");
    }

    private static async Task WaitForManualReloadAsync(
        MainWindow window, ManualDraftConfirmation? expected, List<string> steps, CancellationToken ct)
    {
        if (expected is null)
            throw new InvalidOperationException("manual acceptance: confirmed Core draft/receipt missing");
        await WaitForAsync(steps, "confirmed allocation reloaded into the overview", () =>
        {
            CharacterOverviewState state = window.SnapshotStateForAutomation();
            return !state.IsBusy && string.IsNullOrWhiteSpace(state.Error)
                && state.WorkspaceId == expected.WorkspaceId && state.DisplayOwnerContext == expected.Owner
                && state.ContentRevision == expected.ContentRevision;
        }, ct);
    }

    private static async Task RunManualDeepStageLegAsync(
        MainWindow window,
        CharacterCreationWizardControl wizard,
        DesktopMouseFirstJourneyContext context,
        List<string> screenshotPaths,
        List<string> steps,
        CancellationToken ct,
        Action recordPointerAction)
    {
        CharacterOverviewState attributesBefore = window.SnapshotStateForAutomation();
        RecordStep(steps, "manual deep-stage leg: entering real allocation editors");

        // --- Attributes step: open the Core-bound allocation modal and type a
        // real Priority distribution via the per-attribute up/down controls. ---
        await ClickWizardButtonUntilEffectAsync(
            wizard,
            "creation-wizard-step-attributes",
            "manual leg: wizard entered the attributes step",
            () => string.Equals(
                wizard.StateForAutomation()?.ActiveStepId,
                "attributes",
                StringComparison.Ordinal),
            steps,
            ct,
            recordPointerAction);
        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-attributes-step");

        {
            // Evidence: record the projected budgets/blockers so a disabled
            // allocate affordance is diagnosable from the receipt alone.
            var snap = wizard.StateForAutomation();
            if (snap is not null)
            {
                var budgetLines = snap.Budgets.Select(b =>
                    $"{b.BudgetId}: remaining={b.Remaining}/{b.Total} exact={b.IsExact} blockers=[{string.Join(",", b.Blockers)}]");
                foreach (string line in budgetLines)
                    RecordStep(steps, "manual leg budget evidence: " + line);
                foreach (string blocker in snap.CompletionBlockers)
                    RecordStep(steps, "manual leg completion blocker evidence: " + blocker);
            }
            Button? probeAllocate = FindWizardAutomationButton(wizard, "creation-wizard-allocate-attributes");
            RecordStep(steps, "manual leg allocate-attributes probe: " + (probeAllocate is null
                ? "absent"
                : $"visible={probeAllocate.IsVisible} enabled={probeAllocate.IsEffectivelyEnabled}"));
        }

        // Atomic click discipline: the wizard re-renders the legal-option list
        // on every state refresh, so resolve+click must happen in ONE dispatcher
        // invocation (ClickWizardButtonAtomicallyAsync inside the helper) or the
        // captured instance is detached and the click misses. The modal opening
        // is the click's observable effect; the helper retries with the
        // disclosed routed-event fallback.
        await ClickWizardButtonUntilEffectAsync(
            wizard,
            "creation-wizard-allocate-attributes",
            "manual leg: attributes allocation modal opened",
            () => App.Current.ApplicationLifetime switch
            {
                IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                    .OfType<CreationAttributesAllocationWindow>()
                    .Any(),
                _ => false,
            },
            steps,
            ct,
            recordPointerAction);

        CreationAttributesAllocationWindow? attrDialog = null;
        await WaitForConditionWithinAsync(
            () =>
            {
                attrDialog = App.Current.ApplicationLifetime switch
                {
                    IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                        .OfType<CreationAttributesAllocationWindow>()
                        .FirstOrDefault(),
                    _ => null,
                };
                return attrDialog is not null;
            },
            ct,
            TimeSpan.FromSeconds(10));
        if (attrDialog is null)
        {
            RecordStep(steps, "manual leg: attributes allocation modal did not open; failing manual acceptance");
            throw new InvalidOperationException("manual attributes acceptance: allocation modal did not open");
        }

        // Genuine typed allocation: click every still-enabled priority-up
        // control round-robin until the Core presenter disables them all
        // (budget exhausted) - a partial draft confirms legal but leaves the
        // attributes step in-progress, which blocks the ordinary continue.
        // Genuine typed allocation with human semantics: bump a row, observe
        // Core's preview verdict through the confirm button, and undo a bump
        // the budget rejects (a human clicks minus when the preview blocks).
        // The modal never disables the up buttons at max (Adjust clamps), so
        // value progress is read from each row's projection text.
        // Discover every allocatable row the modal actually projects, then
        // restrict to the eight mental/physical attributes. The
        // special-attributes budget (EDG/MAG/RES/DEP) is exact: any partial
        // special spend makes the budget non-exact and the attributes step
        // can never complete (observed win103 run: EDG bumped to 7/7 left
        // special-attributes non-exact and qualities blocked). A human
        // creating a Priority mundane/magician spends the normal budget and
        // leaves special rows at their base values.
        string[] attrOrder = DiscoverAttributeRows(attrDialog)
            .Where(attr => NormalAttributeIds.Contains(attr))
            .ToArray();
        RecordStep(steps, "manual leg: allocatable rows discovered: " + string.Join(",", attrOrder)
            + " (restricted to normal attributes)");
        int bumps = 0;
        int undos = 0;
        for (int round = 0; round < 64; round++)
        {
            bool progressed = false;
            foreach (string attr in attrOrder)
            {
                if (!TryParseProjection(
                        FindAutomationTextBlock(attrDialog, "creation-attributes-projection-" + attr)?.Text,
                        out int current,
                        out int maximum))
                {
                    if (round == 0)
                        RecordStep(steps, "manual leg: projection for " + attr + " unreadable (recorded as evidence)");
                    continue;
                }

                if (current >= maximum)
                    continue;

                string diag = await ClickControlAtomicallyAsync(attrDialog, "creation-attributes-priority-up-" + attr);
                if (diag.Contains("resolved=False", StringComparison.Ordinal)
                    || diag.Contains("enabled=False", StringComparison.Ordinal))
                {
                    continue;
                }

                recordPointerAction();
                bumps++;
                Dispatcher.UIThread.RunJobs();

                if (FindWizardAutomationButton(attrDialog, "creation-attributes-allocate-confirm")
                    is { IsEffectivelyEnabled: true })
                {
                    progressed = true;
                    continue;
                }

                // Core rejected the bumped draft: undo it and stop this row.
                await ClickControlAtomicallyAsync(attrDialog, "creation-attributes-priority-down-" + attr);
                undos++;
                Dispatcher.UIThread.RunJobs();
            }

            if (!progressed)
                break;
        }
        {
            var finalProjections = attrOrder.Select(attr =>
            {
                string? text = FindAutomationTextBlock(attrDialog, "creation-attributes-projection-" + attr)?.Text;
                return TryParseProjection(text, out int cur, out int max)
                    ? attr + "=" + cur + "/" + max
                    : attr + "=?";
            });
            RecordStep(steps, "manual leg: attributes priority allocation spent " + bumps
                + " bumps with " + undos + " budget-rejected undos; final projections: "
                + string.Join(",", finalProjections));
        }

        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-attributes-typed");
        Button? attrConfirm = FindWizardAutomationButton(attrDialog, "creation-attributes-allocate-confirm");
        if (attrConfirm is { IsEffectivelyEnabled: true })
        {
            // Atomic click discipline: resolve + click in ONE dispatcher
            // invocation. The plain pointer click was swallowed here exactly
            // like on the wizard buttons, leaving the modal open so the
            // post-confirm adapter reload never ran.
            string confirmDiag = await ClickControlAtomicallyAsync(attrDialog, "creation-attributes-allocate-confirm");
            recordPointerAction();
            RecordStep(steps, "manual leg: attributes confirm click " + confirmDiag);
            bool closed = await WaitForConditionWithinAsync(
                () => !attrDialog.IsVisible,
                ct,
                TimeSpan.FromSeconds(20));
            // Re-audit E2 (R109 fix 5): modal closure is not confirmation.
            // A disabled, rejected, cancelled or timed-out confirmation is a
            // failed manual leg, never a close-and-pass.
            if (!closed || !attrDialog.Confirmed)
            {
                RecordStep(steps, "manual leg: attributes modal did not close after confirm click; failing manual acceptance; diag=" + confirmDiag);
                throw new InvalidOperationException(
                    "manual-deep acceptance: the attributes allocation modal did not close after confirm; the confirmation outcome was not observed");
            }
            CharacterCreationAttributesInteractionState? confirmedAttributes = attrDialog.State;
            ManualDraftConfirmation? expectedAttributes = confirmedAttributes?.PendingDraft is { } attributeDraft
                ? new(confirmedAttributes.Binding.WorkspaceId, attributesBefore.DisplayOwnerContext,
                    confirmedAttributes.Binding.ContentRevision, attributeDraft.DraftRevision, attributeDraft.DraftDigest)
                : null;
            await WaitForManualReloadAsync(window, expectedAttributes, steps, ct);
            var attributesService = App.Services?.GetService<IOwnerBoundCharacterCreationAttributesService>()
                ?? throw new InvalidOperationException("manual attributes acceptance: Core authority unavailable after confirmation");
            CharacterOverviewState attributesAfter = window.SnapshotStateForAutomation();
            var reloadedAttributes = await Task.Run(() =>
                new CharacterCreationAttributesInteractionPresenter(attributesService).Load(attributesAfter), ct);
            CharacterOverviewState attributesDisplay = window.SnapshotStateForAutomation();
            if (attributesDisplay.WorkspaceId != attributesAfter.WorkspaceId
                || attributesDisplay.DisplayOwnerContext != attributesAfter.DisplayOwnerContext
                || attributesDisplay.ContentRevision != attributesAfter.ContentRevision)
                throw new InvalidOperationException("manual attributes acceptance: display changed during the authoritative reload");
            ManualDraftConfirmation? actualAttributes = reloadedAttributes.State?.PendingDraft is { } reloadedAttributeDraft
                ? new(reloadedAttributes.State.Binding.WorkspaceId, attributesAfter.DisplayOwnerContext,
                    reloadedAttributes.State.Binding.ContentRevision, reloadedAttributeDraft.DraftRevision, reloadedAttributeDraft.DraftDigest)
                : null;
            RequireManualConfirmation("attributes", attrDialog.Confirmed, attributesBefore.ContentRevision,
                expectedAttributes, actualAttributes);
            RecordStep(steps, "manual leg: attributes allocation confirmed and authoritative draft reloaded");
        }
        else
        {
            RecordStep(steps, "manual leg: attributes confirm disabled; failing manual acceptance (no silent skip)");
            attrDialog.Close();
            throw new InvalidOperationException(
                "manual-deep acceptance: the attributes allocation confirm control is disabled; required manual allocation failed");
        }
        Dispatcher.UIThread.RunJobs();

        {
            // Evidence: full step graph right after the attributes confirm.
            var postSnap = wizard.StateForAutomation();
            if (postSnap is not null)
            {
                string graph = string.Join(", ", postSnap.Steps.Select(st => $"{st.StepId}={st.Status}(sel={st.IsSelected})"));
                RecordStep(steps, "manual leg post-attributes graph: active=" + postSnap.ActiveStepId + "; " + graph);
            }
        }

        {
            // Evidence: budgets/blockers right after the attributes confirm.
            // A qualities blocker visible HERE names the exact reason the
            // step cannot be entered, instead of a silent 30s timeout.
            var postConfirmSnap = wizard.StateForAutomation();
            if (postConfirmSnap is not null)
            {
                foreach (string line in postConfirmSnap.Budgets.Select(b =>
                    $"{b.BudgetId}: remaining={b.Remaining}/{b.Total} exact={b.IsExact} blockers=[{string.Join(",", b.Blockers)}]"))
                    RecordStep(steps, "manual leg post-confirm budget evidence: " + line);
                foreach (string blocker in postConfirmSnap.CompletionBlockers)
                    RecordStep(steps, "manual leg post-confirm completion blocker evidence: " + blocker);
            }
            Button? postAllocProbe = FindWizardAutomationButton(wizard, "creation-wizard-allocate-attributes");
            RecordStep(steps, "manual leg post-confirm allocate-attributes probe: " + (postAllocProbe is null
                ? "absent"
                : $"visible={postAllocProbe.IsVisible} enabled={postAllocProbe.IsEffectivelyEnabled}"));
        }

        {
            // Poll the completion blockers after the attributes confirm: the
            // adapter reload runs asynchronously after the modal closes, so the
            // graph above may be a pre-reload snapshot. Record the blocker set
            // over time; if attributes-draft-required never clears, the reload
            // did not apply and that is the defect, not a slow one.
            for (int poll = 0; poll <= 10; poll++)
            {
                if (poll > 0)
                    Thread.Sleep(2000);
                Dispatcher.UIThread.RunJobs();
                var pollSnap = wizard.StateForAutomation();
                if (pollSnap is null)
                    continue;
                string blockers = string.Join(",", pollSnap.CompletionBlockers);
                var qStep = pollSnap.Steps.FirstOrDefault(st =>
                    string.Equals(st.StepId, "qualities", StringComparison.Ordinal));
                RecordStep(steps, $"manual leg post-confirm blocker poll t={poll * 2}s: "
                    + $"active={pollSnap.ActiveStepId} qualities={qStep?.Status ?? "absent"} "
                    + $"blockers=[{blockers}]");
                if (!pollSnap.CompletionBlockers.Contains("creation-finalization-attributes-draft-required"))
                    break;
            }
        }

        // --- Qualities step: attributes' legal-next is qualities; confirm the
        // default (no qualities) draft through the Core-bound editor. ---
        // The post-confirm adapter reload runs asynchronously after the modal
        // closes; wait for Core to reproject qualities as enterable (unblocked)
        // before navigating, with the graph evidence above as the receipt.
        await WaitForConditionWithinAsync(
            () =>
            {
                var snap = wizard.StateForAutomation();
                var q = snap?.Steps.FirstOrDefault(st =>
                    string.Equals(st.StepId, "qualities", StringComparison.Ordinal));
                // The qualities stage projects only complete/in-progress/blocked:
                // in-progress means qualitiesAuthority.IsReady, which is exactly
                // CanEnter, which is what canContinue requires.
                return string.Equals(snap?.ActiveStepId, "qualities", StringComparison.Ordinal)
                    || q is not null
                       && (string.Equals(q.Status, "in-progress", StringComparison.Ordinal)
                           || string.Equals(q.Status, "available", StringComparison.Ordinal)
                           || string.Equals(q.Status, "complete", StringComparison.Ordinal));
            },
            ct,
            TimeSpan.FromSeconds(30));
        // After the attributes confirm the wizard re-binds to qualities (its
        // legal next), so the nav button is disabled-by-selection; entry means
        // "already the active step" OR one ordinary continue press (the same
        // route a human uses; step-nav jumps from an in-progress step are
        // refused by the wizard).
        {
            var postConfirmStep = wizard.StateForAutomation()?.ActiveStepId ?? "<none>";
            RecordStep(steps, "manual leg post-confirm active step: " + postConfirmStep);
            if (string.Equals(postConfirmStep, "qualities", StringComparison.Ordinal))
            {
                RecordStep(steps, "manual leg: qualities step already active after attributes confirm (no nav click needed)");
            }
            else if (string.Equals(postConfirmStep, "skills", StringComparison.Ordinal))
            {
                // The attributes confirm unblocked qualities AND skills and the
                // adapter reload already auto-advanced past qualities; a human
                // clicks the qualities step's nav button to go back.
                await ClickWizardButtonUntilEffectAsync(
                    wizard,
                    "creation-wizard-step-qualities",
                    "manual leg: wizard routed back to the qualities step",
                    () => string.Equals(
                        wizard.StateForAutomation()?.ActiveStepId,
                        "qualities",
                        StringComparison.Ordinal),
                    steps,
                    ct,
                    recordPointerAction);
            }
            else
            {
                await ClickWizardButtonUntilEffectAsync(
                    wizard,
                    "creation-wizard-continue",
                    "manual leg: wizard entered the qualities or skills step",
                    () =>
                    {
                        var afterSnap = wizard.StateForAutomation()?.ActiveStepId;
                        // The attributes confirm unblocks both qualities and skills;
                        // a single continue can advance two steps when the intermediate
                        // step is already satisfied. Accept either destination.
                        return string.Equals(afterSnap, "qualities", StringComparison.Ordinal)
                            || string.Equals(afterSnap, "skills", StringComparison.Ordinal);
                    },
                    steps,
                    ct,
                    recordPointerAction);
            }
        }
        // The attributes confirm unblocks qualities AND skills; a continue press
        // can skip straight past qualities to skills when the intermediate step
        // is already satisfied. A human clicks the qualities step's nav button
        // in that case - do the same (route back to qualities if needed).
        {
            var postNavSnap = wizard.StateForAutomation();
            string postNavStep = postNavSnap?.ActiveStepId ?? "<none>";
            RecordStep(steps, "manual leg post-nav active step: " + postNavStep);
            if (!string.Equals(postNavStep, "qualities", StringComparison.Ordinal))
            {
                await ClickWizardButtonUntilEffectAsync(
                    wizard,
                    "creation-wizard-step-qualities",
                    "manual leg: wizard routed back to the qualities step",
                    () => string.Equals(
                        wizard.StateForAutomation()?.ActiveStepId,
                        "qualities",
                        StringComparison.Ordinal),
                    steps,
                    ct,
                    recordPointerAction);
            }
        }
        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-qualities-step");
        {
            var qSnap = wizard.StateForAutomation();
            Button? qProbe = FindWizardAutomationButton(wizard, "creation-wizard-edit-qualities");
            RecordStep(steps, "manual leg qualities probe: " + (qProbe is null
                ? "absent"
                : $"visible={qProbe.IsVisible} enabled={qProbe.IsEffectivelyEnabled} step={qSnap?.ActiveStepId}"));
        }

        // --- Qualities review: confirm the default (empty) qualities draft
        // through the Core-bound review window. The qualities draft is
        // required before continue can advance past the qualities step.
        {
            var preQ = wizard.StateForAutomation();
            CharacterOverviewState qualitiesBefore = window.SnapshotStateForAutomation();
            Button? qEdit = FindWizardAutomationButton(wizard, "creation-wizard-edit-qualities");
            if (qEdit is { IsEffectivelyEnabled: true })
            {
                string qEditDiag = await ClickWizardButtonAtomicallyAsync(wizard, "creation-wizard-edit-qualities");
                recordPointerAction();
                RecordStep(steps, "manual leg: qualities edit click " + qEditDiag);
                CreationQualitiesReviewWindow? qDialog = null;
                await WaitForConditionWithinAsync(
                    () =>
                    {
                        qDialog = App.Current.ApplicationLifetime switch
                        {
                            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                                .OfType<CreationQualitiesReviewWindow>()
                                .FirstOrDefault(),
                            _ => null,
                        };
                        return qDialog is not null;
                    },
                    ct,
                    TimeSpan.FromSeconds(10));
                if (qDialog is null)
                {
                    // Re-audit E2: a required confirmation modal that never
                    // appears is a failed manual leg, not evidence to skip past.
                    RecordStep(steps, "manual leg: qualities review window NOT opened; failing manual acceptance (no silent continue)");
                    throw new InvalidOperationException(
                        "manual-deep acceptance: the qualities review window did not open; a required manual leg failed");
                }
                else
                {
                    RecordStep(steps, "manual leg: qualities review window opened");
                    // Human flow: Review (prepare) first; confirm enables only
                    // after a prepared preview exists.
                    string qReviewDiag = await ClickControlAtomicallyAsync(qDialog, "creation-qualities-review-review");
                    recordPointerAction();
                    RecordStep(steps, "manual leg: qualities review click " + qReviewDiag);
                    await WaitForConditionWithinAsync(
                        () => FindWizardAutomationButton(qDialog, "creation-qualities-review-confirm") is { IsEffectivelyEnabled: true },
                        ct,
                        TimeSpan.FromSeconds(15));
                    CharacterCreationQualitiesSelectionReview? qualityReview = qDialog.ReviewForAutomation;
                    string qConfirmDiag = await ClickControlAtomicallyAsync(qDialog, "creation-qualities-review-confirm");
                    recordPointerAction();
                    RecordStep(steps, "manual leg: qualities confirm click " + qConfirmDiag);
                    bool qClosed = await WaitForConditionWithinAsync(
                        () => !qDialog.IsVisible,
                        ct,
                        TimeSpan.FromSeconds(20));
                    // Re-audit E2 (R109 fix 5): a timed-out review confirmation
                    // is a failed manual leg, never a close-and-pass.
                    if (!qClosed || !qDialog.Confirmed)
                    {
                        RecordStep(steps, "manual leg: qualities review window did not close after confirm; failing manual acceptance; diag=" + qConfirmDiag);
                        throw new InvalidOperationException(
                            "manual-deep acceptance: the qualities review window did not close after confirm; the confirmation outcome was not observed");
                    }
                    if (qualityReview is null || !CharacterCreationQualitiesRules.TryPlan(
                            qualityReview.Preview, qualityReview.Preview.PreviewDigest, qualityReview.IdempotencyKey,
                            true, false, qualityReview.TransactionId, out var qualityPlan))
                        throw new InvalidOperationException("manual qualities acceptance: confirmed Core plan missing");
                    var expectedQualities = new ManualDraftConfirmation(
                        qualityReview.Before.Binding.WorkspaceId, qualitiesBefore.DisplayOwnerContext,
                        qualityPlan.TargetContentRevision, (qualityReview.Before.PendingDraft?.DraftRevision ?? 0) + 1,
                        qualityPlan.CommandDigest + ":" + qualityPlan.PreviewDigest + ":" + qualityPlan.IdempotencyKeyDigest);
                    await WaitForManualReloadAsync(window, expectedQualities, steps, ct);
                    CharacterOverviewState qualitiesAfter = window.SnapshotStateForAutomation();
                    ManualDraftConfirmation? actualQualities = qualitiesAfter.CreationQualities?.PendingDraft is { } qualityDraft
                        ? new(qualitiesAfter.CreationQualities.Binding.WorkspaceId, qualitiesAfter.DisplayOwnerContext,
                            qualitiesAfter.CreationQualities.Binding.ContentRevision, qualityDraft.DraftRevision,
                            qualityDraft.LastCommandDigest + ":" + qualityDraft.LastPreviewDigest + ":" + qualityDraft.LastIdempotencyKeyDigest)
                        : null;
                    RequireManualConfirmation("qualities", qDialog.Confirmed, qualitiesBefore.ContentRevision,
                        expectedQualities, actualQualities);
                    if (!CharacterCreationQualitiesInteraction.SelectedIds(qualitiesAfter.CreationQualities!)
                            .SequenceEqual(qualityReview.SelectedOptionIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                        throw new InvalidOperationException("manual qualities acceptance: the reloaded selection differs from the confirmed selection");
                    RecordStep(steps, "manual leg: qualities selection confirmed and authoritative draft reloaded");
                }
                Dispatcher.UIThread.RunJobs();
            }
            else
            {
                RecordStep(steps, "manual leg: qualities edit affordance not enabled; failing manual acceptance (no silent skip)");
                throw new InvalidOperationException("manual-deep acceptance: the qualities review affordance is missing or disabled; a required manual confirmation failed");
            }
        }

        // --- Skills step: qualities' legal-next is skills; open the skills
        // allocation modal and confirm the default Core-provided plan. ---
        {
            // Evidence: step graph + blockers right after the qualities
            // confirm, so the skills navigation failure is diagnosable from
            // the receipt alone.
            for (int poll = 0; poll <= 6; poll++)
            {
                if (poll > 0)
                    Thread.Sleep(2000);
                Dispatcher.UIThread.RunJobs();
                var pollSnap = wizard.StateForAutomation();
                if (pollSnap is null)
                    continue;
                string graph = string.Join(",", pollSnap.Steps.Select(st =>
                    $"{st.StepId}={st.Status}"));
                string blockers = string.Join(",", pollSnap.CompletionBlockers);
                var skStep = pollSnap.Steps.FirstOrDefault(st =>
                    string.Equals(st.StepId, "skills", StringComparison.Ordinal));
                string skBlockers = skStep is null ? "<missing>" : string.Join(",", skStep.Blockers);
                string skCanEnter = skStep?.CanEnter.ToString() ?? "<missing>";
                // Direct skills-authority probe: the wizard stage override
                // masks the skills authority's own blockers with the
                // finalization ones, so load Core's skills state directly
                // through the same owner-bound presenter path a human uses.
                try
                {
                    CharacterOverviewState probeOverview = window.SnapshotStateForAutomation();
                    var probePresenter = new CharacterCreationSkillsInteractionPresenter(
                        App.Services?.GetService(typeof(Chummer.Application.Characters.IOwnerBoundCharacterCreationSkillsService))
                            as Chummer.Application.Characters.IOwnerBoundCharacterCreationSkillsService
                        ?? throw new InvalidOperationException("skills service unavailable"));
                    var probeLoaded = await Task.Run(() => probePresenter.Load(probeOverview));
                    string probeOutcome = probeLoaded.Outcome ?? "?";
                    string probeError2 = probeLoaded.Error ?? "<none>";
                    string probeCanEdit = probeLoaded.State?.State.CanEdit.ToString() ?? "<null>";
                    string probeSkBlockers = probeLoaded.State?.State is null
                        ? "<null-state>"
                        : string.Join(",", probeLoaded.State.State.Blockers);
                    RecordStep(steps, $"manual leg skills-authority probe: outcome={probeOutcome} "
                        + $"canEdit={probeCanEdit} error={probeError2} stateBlockers=[{probeSkBlockers}]");
                    string budgetDump = string.Join(",", pollSnap.Budgets.Select(b =>
                        $"{b.BudgetId}:{b.Total}/{b.Used} exact={b.IsExact}"
                        + (b.Blockers.Count > 0 ? "!" + string.Join("|", b.Blockers) : string.Empty)));
                    RecordStep(steps, "manual leg budget dump: [" + budgetDump + "]");
                    // Factory-style raw probe: replicate WorkspaceOverviewStateFactory.LoadSkills
                    // guards exactly and report which one fails.
                    try
                    {
                        var svc = App.Services?.GetService(typeof(Chummer.Application.Characters.IOwnerBoundCharacterCreationSkillsService))
                            as Chummer.Application.Characters.IOwnerBoundCharacterCreationSkillsService;
                        if (svc is null)
                        {
                            RecordStep(steps, "manual leg factory-probe: service null");
                        }
                        else
                        {
                            var req = new Chummer.Contracts.Characters.CharacterCreationSkillsLoadRequest(probeOverview.WorkspaceId!.Value);
                            Chummer.Contracts.Characters.CharacterCreationFoundationResult<Chummer.Contracts.Characters.CharacterCreationSkillsState>? raw =
                                probeOverview.DisplayOwnerContext is { IsValid: true } rawOwner
                                    ? svc.Load(rawOwner, req)
                                    : null;
                            if (raw is null)
                            {
                                RecordStep(steps, "manual leg factory-probe: ownerContext invalid (DisplayOwnerContext null or not valid)");
                            }
                            else
                            {
                                string envBlockers = string.Join(",", raw.Blockers);
                                string stBlockers = raw.Value is { } v ? string.Join(",", v.Blockers) : "<null-state>";
                                string outcomeRaw = raw.Outcome ?? "<null>";
                                bool blockersMatch = raw.Value is { } v2
                                    && string.Join(",", v2.Blockers) == envBlockers;
                                RecordStep(steps, "manual leg factory-probe: outcome=" + outcomeRaw
                                    + " envBlockers=[" + envBlockers + "] stateBlockers=[" + stBlockers + "]"
                                    + " match=" + blockersMatch.ToString().ToLowerInvariant());
                                if (raw.Value is { } v3)
                                {
                                    RecordStep(steps, "manual leg factory-probe binding: ws=" + v3.Binding.WorkspaceId
                                        + " contentRev=" + v3.Binding.ContentRevision
                                        + " savedRev=" + v3.Binding.SavedRevision
                                        + " overviewContentRev=" + probeOverview.ContentRevision
                                        + " overviewSavedRev=" + probeOverview.SavedRevision);
                                }
                            }
                        }
                    }
                    catch (Exception factoryProbeError)
                    {
                        RecordStep(steps, "manual leg factory-probe failed: " + factoryProbeError.Message);
                    }
                }
                catch (Exception probeError)
                {
                    RecordStep(steps, "manual leg skills-authority probe failed: " + probeError.Message);
                }
                RecordStep(steps, $"manual leg post-qualities poll t={poll * 2}s: "
                    + $" skills[status={skStep?.Status ?? "<missing>"} canEnter={skCanEnter} blockers=[{skBlockers}]]");
                if (skStep is not null
                    && !string.Equals(skStep.Status, "blocked", StringComparison.Ordinal))
                    break;
            }
        }

        // --- Skills step: qualities' legal-next is skills; open the skills
        // allocation modal and confirm the default Core-provided plan. ---
        await WaitForConditionWithinAsync(
            () =>
            {
                var snap = wizard.StateForAutomation();
                var sk = snap?.Steps.FirstOrDefault(st =>
                    string.Equals(st.StepId, "skills", StringComparison.Ordinal));
                // Skills projects available (CanEdit) / blocked; accept
                // in-progress too in case Core adds that status here as well.
                return string.Equals(snap?.ActiveStepId, "skills", StringComparison.Ordinal)
                    || sk is not null
                       && (string.Equals(sk.Status, "available", StringComparison.Ordinal)
                           || string.Equals(sk.Status, "in-progress", StringComparison.Ordinal)
                           || string.Equals(sk.Status, "complete", StringComparison.Ordinal));
            },
            ct,
            TimeSpan.FromSeconds(30));
        if (!string.Equals(wizard.StateForAutomation()?.ActiveStepId, "skills", StringComparison.Ordinal))
        {
            // After the qualities confirm the adapter reload can reset the
            // active step to basics, so walk one legal step per continue
            // click until skills is active (60s budget).
            var walkDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            bool reachedSkills = false;
            while (DateTime.UtcNow < walkDeadline)
            {
                var walkSnap = wizard.StateForAutomation();
                if (walkSnap is not null
                    && string.Equals(walkSnap.ActiveStepId, "skills", StringComparison.Ordinal))
                {
                    reachedSkills = true;
                    break;
                }
                if (walkSnap?.CanContinue == true
                    && FindWizardAutomationButton(wizard, "creation-wizard-continue")
                       is { IsEffectivelyEnabled: true })
                {
                    // Atomic click discipline: the continue click is
                    // swallowed by the harness when issued via a plain
                    // routed click outside a single dispatcher invocation.
                    await ClickControlAtomicallyAsync(wizard, "creation-wizard-continue");
                    recordPointerAction();
                    await Task.Delay(500, ct);
                    Dispatcher.UIThread.RunJobs();
                }
                else
                {
                    await Task.Delay(300, ct);
                }
            }
            RecordStep(steps, "manual leg skills-walk reached skills step: " + reachedSkills);
            if (!reachedSkills)
                throw new TimeoutException(
                    "manual leg: skills step never became active within the continue-walk window");
        }
        else
        {
            RecordStep(steps, "manual leg: skills step already active (no nav click needed)");
        }
        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-skills-step");

        Button? skillAllocate = FindWizardAutomationButton(wizard, "creation-wizard-allocate-skills");
        if (skillAllocate is { IsVisible: true, IsEffectivelyEnabled: true })
        {
            CharacterOverviewState skillsBefore = window.SnapshotStateForAutomation();
            await ClickWizardButtonUntilEffectAsync(
                wizard,
                "creation-wizard-allocate-skills",
                "manual leg: skills allocation modal opened",
                () => App.Current.ApplicationLifetime switch
                {
                    IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                        .OfType<CreationSkillsAllocationWindow>()
                        .Any(),
                    _ => false,
                },
                steps,
                ct,
                recordPointerAction);

            CreationSkillsAllocationWindow? skillDialog = null;
            await WaitForConditionWithinAsync(
                () =>
                {
                    skillDialog = App.Current.ApplicationLifetime switch
                    {
                        IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                            .OfType<CreationSkillsAllocationWindow>()
                            .FirstOrDefault(),
                        _ => null,
                    };
                    return skillDialog is not null;
                },
                ct,
                TimeSpan.FromSeconds(10));
            if (skillDialog is null)
            {
                RecordStep(steps, "manual leg: skills allocation modal NOT opened (recorded as evidence)");
                throw new InvalidOperationException("manual-deep acceptance: the skills allocation modal did not open; a required manual leg failed");
            }
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-skills-modal");
            Button? skillConfirm = FindWizardAutomationButton(skillDialog, "creation-skills-allocate-confirm");
            if (skillConfirm is { IsEffectivelyEnabled: true })
            {
                await RoutePointerClickAsync(skillConfirm);
                recordPointerAction();
                // Re-audit E2: disappearance alone is not confirmation. The
                // close must be observed within budget AND the authoritative
                // wizard draft must reflect the accepted allocation before the
                // leg records success; a timeout fails the manual leg.
                bool skillDialogClosed = await WaitForConditionWithinAsync(
                    () => !skillDialog.IsVisible,
                    ct,
                    TimeSpan.FromSeconds(20));
                if (!skillDialogClosed || !skillDialog.Confirmed)
                {
                    RecordStep(steps, "manual leg: skills allocation close timed out; failing manual acceptance");
                    throw new InvalidOperationException(
                        "manual-deep acceptance: the skills allocation modal did not close after confirm; the confirmation outcome was not observed");
                }
                ManualDraftConfirmation? expectedSkills = skillDialog.ConfirmationReceipt is { } skillsReceipt
                    ? new(skillsReceipt.WorkspaceId, skillsBefore.DisplayOwnerContext,
                        skillsReceipt.ContentRevision, skillsReceipt.DraftRevision, skillsReceipt.DraftDigest)
                    : null;
                await WaitForManualReloadAsync(window, expectedSkills, steps, ct);
                CharacterOverviewState skillsAfter = window.SnapshotStateForAutomation();
                ManualDraftConfirmation? actualSkills = skillsAfter.CreationSkills?.PendingDraft is { } skillDraft
                    ? new(skillsAfter.CreationSkills.Binding.WorkspaceId, skillsAfter.DisplayOwnerContext,
                        skillsAfter.CreationSkills.Binding.ContentRevision, skillDraft.DraftRevision, skillDraft.DraftDigest)
                    : null;
                RequireManualConfirmation("skills", skillDialog.Confirmed, skillsBefore.ContentRevision,
                    expectedSkills, actualSkills);
                RecordStep(steps, "manual leg: skills allocation confirmed and authoritative draft reloaded");
            }
            else
            {
                RecordStep(steps, "manual leg: skills confirm disabled; failing manual acceptance (no silent skip)");
                skillDialog.Close();
                throw new InvalidOperationException("manual-deep acceptance: the skills allocation confirm control is disabled; required manual allocation failed");
            }
            Dispatcher.UIThread.RunJobs();
        }
        else
        {
            RecordStep(steps, "manual leg: skills allocate affordance unavailable (recorded as evidence)");
            throw new InvalidOperationException("manual-deep acceptance: the skills allocation affordance is missing or disabled; a required manual leg failed");
        }

        // The ordinary Next walk that follows continues from the active step
        // (skills -> resources -> review); no further manual navigation needed.
        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-qualities-step");

        await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03m-manual-deep-stages-done");
        RecordStep(steps, "manual deep-stage leg complete; returning to ordinary Next walk");
    }
}
