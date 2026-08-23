using Chummer.Avalonia.Controls;
using Chummer.Presentation.Overview;
using Chummer.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private const double LeftShellWidth = 264d;
    private const double LeftShellMinWidth = 240d;
    private const double LeftShellMaxWidth = 320d;
    private const double RightShellWidth = 228d;

    private void RefreshState()
    {
        CharacterOverviewState state = PrepareStateForRefresh(_adapter.State);
        ShellSurfaceState shellSurface = _shellSurfaceResolver.Resolve(state, _shellPresenter.State);
        MainWindowShellFrame shellFrame = MainWindowShellFrameProjector.Project(
            state,
            shellSurface,
            _commandAvailabilityEvaluator);

        ApplyShellFrame(shellFrame);
        ApplyCreationWizardState(state);
        BindRosterToWorkspaces(state);
        if (state.Preferences.DisableAiFeatures)
        {
            ResetCoachSidecarForDisabledAi();
            _controls.SetCoachSidecarVisible(false);
        }
        else
        {
            _controls.SetCoachSidecarVisible(true);
            QueueCoachSidecarRefreshIfNeeded(shellSurface);
        }

        ApplyPostRefreshEffects(state);
    }

    private void BindRosterToWorkspaces(CharacterOverviewState state)
    {
        IReadOnlyList<OpenWorkspaceState> rosterWorkspaces = ResolveRosterWorkspaces(state);
        var rosterNodes = CharacterRosterDataBinder.CreateRosterNodes(rosterWorkspaces);
        CharacterRosterControl.RosterItems = rosterNodes;
        CharacterRosterControl.SelectedWorkspaceId =
            state.Session.ActiveWorkspaceId?.Value
            ?? state.WorkspaceId?.Value;
    }

    internal static IReadOnlyList<OpenWorkspaceState> ResolveRosterWorkspaces(CharacterOverviewState state)
        => state.Session.OpenWorkspaces.Count > 0
            ? state.Session.OpenWorkspaces
            : state.OpenWorkspaces;

    private void ApplyShellFrame(MainWindowShellFrame shellFrame)
    {
        shellFrame = _transientStateCoordinator.ApplyShellFrame(shellFrame);
        _controls.ApplyShellFrame(shellFrame);
        ApplyWorkbenchChromeVisibility(shellFrame);
    }

    private void ApplyWorkbenchChromeVisibility(MainWindowShellFrame shellFrame)
    {
        bool useClassicChrome = ClassicModePolicy.ResolveCurrentMode() == DesktopUiMode.Classic;
        _controls.ApplyDesktopModeChrome(useClassicChrome);
        bool showNavigatorPane = shellFrame.ShowNavigatorPane;
        bool showRosterPane = !showNavigatorPane;
        bool showSummaryHeader = shellFrame.ChromeState.SummaryHeader.HasVisibleContent;
        string? selectedCommandId = shellFrame.CommandDialogPaneState.SelectedCommandId;
        string? activeDialogId = shellFrame.CommandDialogPaneState.ActiveDialogId;
        bool shouldSuppressRightCommandPane = IsNewCharacterWorkbenchActive(selectedCommandId, activeDialogId, shellFrame.CommandDialogPaneState);
        bool hasCommandContext = !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.ActiveDialogId)
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.SelectedCommandId)
            || shellFrame.CommandDialogPaneState.Fields.Length > 0
            || shellFrame.CommandDialogPaneState.Actions.Length > 0
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogTitle)
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogMessage);
        bool hasOpenMenu = !string.IsNullOrWhiteSpace(shellFrame.HeaderState.MenuBar.OpenMenuId);
        bool showCommandSurface = (hasCommandContext || hasOpenMenu) && !shouldSuppressRightCommandPane;
        bool showClassicFormPort = ClassicModePolicy.ShouldUseClassicFormPort(
            shellFrame.SectionHostState.SectionId,
            shellFrame.CommandDialogPaneState.SelectedCommandId);
        bool commandPromotedToClassicFormPort =
            showClassicFormPort
            && string.IsNullOrWhiteSpace(shellFrame.SectionHostState.SectionId)
            && !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.SelectedCommandId);
        bool showRightShell = !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogTitle)
            || !string.IsNullOrWhiteSpace(shellFrame.CommandDialogPaneState.DialogMessage)
            || shellFrame.CommandDialogPaneState.Fields.Length > 0
            || shellFrame.CommandDialogPaneState.Actions.Length > 0
            || showCommandSurface;
        if (useClassicChrome)
        {
            // The Chummer5a-equivalent desktop workbench owns menus and dialogs in-place or
            // through the dedicated dialog window. It must not light up an empty inline right rail.
            showRightShell = false;
        }

        if (shouldSuppressRightCommandPane)
        {
            showRightShell = false;
        }

        if (commandPromotedToClassicFormPort)
        {
            showRightShell = false;
        }

        RosterPaneRegion.IsVisible = showRosterPane;
        RosterPaneRegion.IsHitTestVisible = showRosterPane;
        LeftNavigatorRegion.IsVisible = showNavigatorPane;
        LeftNavigatorRegion.IsHitTestVisible = showNavigatorPane;
        SummaryHeaderRegion.IsVisible = showSummaryHeader;
        SummaryHeaderRegion.IsHitTestVisible = showSummaryHeader;
        ClassicFormPortHostControl.IsVisible = showClassicFormPort;
        ClassicFormPortHostControl.IsHitTestVisible = showClassicFormPort;
        SectionHostControl.IsVisible = !showClassicFormPort;
        SectionHostControl.IsHitTestVisible = !showClassicFormPort;
        RightShellRegion.IsVisible = showRightShell;
        RightShellRegion.IsHitTestVisible = showRightShell;
        RightShellRegion.Opacity = showRightShell ? 1 : 0;

        ApplyPaneWidth(RosterPaneRegion, showRosterPane, LeftShellWidth, LeftShellMinWidth, LeftShellMaxWidth);
        ApplyPaneWidth(LeftNavigatorRegion, showNavigatorPane, LeftShellWidth, LeftShellMinWidth, LeftShellMaxWidth);
        ApplyPaneWidth(RightShellRegion, showRightShell, RightShellWidth, 0d, RightShellWidth);

        if (ContentRegion.ColumnDefinitions.Count >= 3)
        {
            ContentRegion.ColumnDefinitions[0].Width = showRosterPane || showNavigatorPane
                ? new GridLength(LeftShellWidth)
                : new GridLength(0);
            ContentRegion.ColumnDefinitions[2].Width = showRightShell
                ? new GridLength(228)
                : new GridLength(0);
            ContentRegion.ColumnSpacing = showRosterPane || showNavigatorPane || showRightShell ? 2 : 0;
        }

        ContentRegion.InvalidateMeasure();
        ContentRegion.InvalidateArrange();
    }

    private static bool IsNewCharacterWorkbenchActive(
        string? selectedCommandId,
        string? activeDialogId,
        CommandDialogPaneState commandDialogPaneState)
    {
        if (IsNewCharacterCommandId(selectedCommandId) || IsNewCharacterDialogId(activeDialogId))
            return true;

        string? dialogTitle = commandDialogPaneState.DialogTitle;
        if (!string.IsNullOrWhiteSpace(dialogTitle)
            && IsNewCharacterDialogTitle(dialogTitle))
        {
            return true;
        }

        bool hasNewCharacterAction = commandDialogPaneState.Actions.Any(action =>
            IsNewCharacterCommandId(action.Id)
            || IsNewCharacterDialogAction(action.Id));

        bool hasNewCharacterField = commandDialogPaneState.Fields.Any(field =>
            IsNewCharacterFieldId(field.Id)
            || IsNewCharacterFieldLabel(field.Label));

        bool hasNewCharacterWorkflowSignature = commandDialogPaneState.Fields.Any(field =>
            IsNewCharacterWorkflowField(field.Id, field.Label));

        return commandDialogPaneState.Fields.Length > 0
            && (hasNewCharacterAction || hasNewCharacterField || hasNewCharacterWorkflowSignature);
    }

    private static bool IsNewCharacterCommandId(string? commandId)
        => MatchesAnyNormalizedToken(
            commandId,
            "newcharacter",
            "newcritter",
            "newrunner",
            "charactergen",
            "chargen");

    private static bool IsNewCharacterDialogId(string? dialogId)
        => MatchesAnyNormalizedToken(
            dialogId,
            "dialognewcharacter",
            "dialognewcritter",
            "dialognewrunner",
            "priorityworkflow",
            "karmaworkflow",
            "sumtotenworkflow",
            "mysticadeptworkflow");

    private static bool IsNewCharacterDialogAction(string? actionId)
        => MatchesAnyNormalizedToken(
            actionId,
            "createcharacter",
            "completenewcharacterworkflow",
            "newcharacterworkflowcancel",
            "newcharacter",
            "newrunner");

    private static bool IsNewCharacterFieldId(string? fieldId)
        => MatchesAnyNormalizedToken(
            fieldId,
            "newcharactername",
            "newcharacteralias",
            "newcharacterbuildmethod",
            "newcharacterrulesetid",
            "newcharactermetatype",
            "newcharacterworkflowbuildmethod",
            "newcharacterworkflowhouserulesenabled",
            "newcharacterprioritylastchangedfieldid",
            "newcharacter");

    private static bool IsNewCharacterFieldLabel(string? label)
        => MatchesAnyNormalizedToken(
            label,
            "newcharacter",
            "buildmethod",
            "ruleset",
            "metatype",
            "talentchoice",
            "houserules");

    private static bool IsNewCharacterWorkflowField(string? fieldId, string? fieldLabel)
    {
        string normalizedId = NormalizeToken(fieldId);
        string normalizedLabel = NormalizeToken(fieldLabel);
        return normalizedId.StartsWith("newcharacterpriority", StringComparison.Ordinal)
            || normalizedId.StartsWith("newcharacterworkflow", StringComparison.Ordinal)
            || normalizedId.StartsWith("newcharacterkarma", StringComparison.Ordinal)
            || normalizedId.Contains("assensing", StringComparison.Ordinal)
            || normalizedLabel.Contains("assensing", StringComparison.Ordinal)
            || normalizedLabel.Contains("buildmethod", StringComparison.Ordinal)
            || normalizedLabel.Contains("mysticadept", StringComparison.Ordinal)
            || normalizedLabel.Contains("priority", StringComparison.Ordinal);
    }

    private static bool IsNewCharacterDialogTitle(string? dialogTitle)
        => MatchesAnyNormalizedToken(
            dialogTitle,
            "newcharacter",
            "selectbuildmethod",
            "prioritybuild",
            "charactercreation",
            "chargen");

    private static bool MatchesAnyNormalizedToken(string? value, params string[] tokens)
    {
        string normalized = NormalizeToken(value);
        if (normalized.Length == 0)
            return false;

        return tokens.Any(token =>
            normalized.Contains(token, StringComparison.Ordinal));
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        Span<char> buffer = stackalloc char[value.Length];
        int index = 0;
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[index++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer[..index]);
    }

    private static void ApplyPaneWidth(Border pane, bool isVisible, double width, double minWidth, double maxWidth)
    {
        if (isVisible)
        {
            pane.Width = width;
            pane.MinWidth = minWidth;
            pane.MaxWidth = maxWidth;
            return;
        }

        pane.Width = 0d;
        pane.MinWidth = 0d;
        pane.MaxWidth = 0d;
    }

    private void ApplyPostRefreshEffects(CharacterOverviewState state)
    {
        MainWindowTransientDispatchSet pendingDispatches = _transientStateCoordinator.ApplyPostRefresh(
            this,
            state,
            _adapter,
            DialogWindow_OnClosed);

        if (pendingDispatches.PendingDownloadRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingDownloadAsync(pendingDispatches.PendingDownloadRequest),
                "pending download");
        }

        if (pendingDispatches.PendingExportRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingExportAsync(pendingDispatches.PendingExportRequest),
                "pending export");
        }

        if (pendingDispatches.PendingPrintRequest is not null)
        {
            _ = RunUiActionAsync(
                () => HandlePendingPrintAsync(pendingDispatches.PendingPrintRequest),
                "pending print");
        }
    }
}
