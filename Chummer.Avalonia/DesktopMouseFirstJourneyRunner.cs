using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia.Controls;
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
    private static readonly TimeSpan JourneyTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan HardTimeoutGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TransitionSettleTimeout = TimeSpan.FromSeconds(2);
    public static async Task RunAsync(MainWindow window, string headId)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopMouseFirstJourneyContext context = DesktopMouseFirstJourneyRuntime.BuildContext(headId, DateTimeOffset.UtcNow);
        List<string> steps = [];
        List<string> screenshotPaths = [];
        List<DesktopMouseFirstJourneyObservedInputEvent> observedInputEvents = [];
        int pointerActionCount = 0;
        int textEntryActionCount = 0;
        int directTextMutationCount = 0;
        bool usedForcedComboDropdownOpen = false;
        bool usedComboSelectionFallback = false;
        using ObservedInputTraceCollector inputTraceCollector = new(window, observedInputEvents);
        Task journeyTask = RunJourneyAsync(
            window,
            context,
            steps,
            screenshotPaths,
            inputTraceCollector,
            observedInputEvents,
            () => pointerActionCount++,
            () => textEntryActionCount++,
            () => pointerActionCount,
            () => textEntryActionCount,
            () => directTextMutationCount,
            () => usedForcedComboDropdownOpen,
            () => usedComboSelectionFallback);
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
        List<string> steps,
        List<string> screenshotPaths,
        ObservedInputTraceCollector inputTraceCollector,
        List<DesktopMouseFirstJourneyObservedInputEvent> observedInputEvents,
        Action recordPointerAction,
        Action recordTextEntryAction,
        Func<int> getPointerActionCount,
        Func<int> getTextEntryActionCount,
        Func<int> getDirectTextMutationCount,
        Func<bool> getUsedForcedComboDropdownOpen,
        Func<bool> getUsedComboSelectionFallback)
    {
        try
        {
            using CancellationTokenSource journeyTimeout = new(JourneyTimeout);
            string language = DesktopLocalizationCatalog.GetCurrentLanguage();
            const string expectedCharacterName = "Mouse Journey Runner";
            const string expectedCharacterAlias = "MouseRoute";
            const string expectedRulesetId = "sr5";
            RecordStep(steps, "start mouse-first live binary journey");
            string initialWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);

            await WaitForAsync(
                steps,
                "desktop shell initialized",
                () => window.IsVisible
                    && window.Bounds.Width > 0d
                    && window.Bounds.Height > 0d
                    && window.ControlsForAutomation.MenuBar is not null
                    && window.ControlsForAutomation.CommandDialogPane is not null,
                journeyTimeout.Token);

            await ClickFileMenuCommandAsync(window, "new_character", steps, journeyTimeout.Token, recordPointerAction);
            await WaitForDialogAsync(window, "dialog.new_character", steps, journeyTimeout.Token);
            inputTraceCollector.ObserveDialogWindow(ResolveVisibleDialogWindow());
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "01-new-character-dialog");

            await SetDialogTextFieldAsync(window, "newCharacterName", "Mouse Journey Runner", steps, journeyTimeout.Token);
            recordTextEntryAction();
            await SetDialogTextFieldAsync(window, "newCharacterAlias", "MouseRoute", steps, journeyTimeout.Token);
            recordTextEntryAction();
            await VerifyDialogSelectFieldValueAsync(window, "newCharacterRulesetId", "sr5", steps, journeyTimeout.Token);
            await VerifyDialogSelectFieldValueAsync(window, "newCharacterBuildMethod", "Priority", steps, journeyTimeout.Token);
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
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "02-priority-workflow");

            await ClickDialogActionUntilAsync(
                window,
                "complete_new_character_workflow",
                steps,
                journeyTimeout.Token,
                recordPointerAction,
                "workspace creation dialog closed after mouse-first creation flow",
                () => ResolveVisibleDialogWindow() is null);
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "03-post-dialog-close");
            await WaitForAsync(
                steps,
                "character workspace published after mouse-first creation flow",
                () => HasOpenedCharacterEvidence(window, language, expectedCharacterName, expectedCharacterAlias, expectedRulesetId),
                journeyTimeout.Token);
            string openedWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);
            DesktopMouseFirstJourneyVisibleShellState openedVisibleState = ReadVisibleShellState(window, language);
            RecordStep(
                steps,
                HasWorkspaceStripTransition(initialWorkspaceStripText, openedWorkspaceStripText)
                    ? $"workspace strip changed to {openedWorkspaceStripText}"
                    : $"workspace strip stayed stable while live shell content confirmed opened character {expectedCharacterName} ({openedVisibleState.RulesetId ?? expectedRulesetId})");
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "04-workspace-opened");

            await ClickFileMenuCommandAsync(window, "save_character", steps, journeyTimeout.Token, recordPointerAction);
            await WaitForAsync(
                steps,
                "workspace saved after pointer-first flow",
                () => ReadVisibleShellState(window, language) is { HasActiveWorkspace: true, IsSaved: true, CharacterLoaded: true },
                journeyTimeout.Token);
            string savedWorkspaceStripText = await ReadWorkspaceStripTextAsync(window);
            DesktopMouseFirstJourneyVisibleShellState savedVisibleState = ReadVisibleShellState(window, language);
            RecordStep(
                steps,
                HasWorkspaceStripTransition(openedWorkspaceStripText, savedWorkspaceStripText)
                    ? $"workspace strip changed to {savedWorkspaceStripText}"
                    : $"workspace strip stayed stable while visible shell state confirmed saved workspace {savedVisibleState.WorkspaceId ?? "(missing)"}");
            await CaptureEvidenceScreenshotAsync(window, context, screenshotPaths, "05-workspace-saved");

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
                    WorkspaceId: finalVisibleState.WorkspaceId,
                    CharacterName: expectedCharacterName,
                    CharacterAlias: expectedCharacterAlias,
                    RulesetId: finalVisibleState.RulesetId ?? expectedRulesetId,
                    HasSavedWorkspace: finalVisibleState.IsSaved,
                    ActiveDialogId: ResolveVisibleDialogWindow()?.BoundDialogId,
                    VerificationNotes:
                    [
                        "Binary was launched in mouse-first live journey mode.",
                        "Character creation used menu clicks, dialog clicks, and only rare text entry for name/alias.",
                        "Workspace reached a saved state without invoking file-picker shortcuts or internal API-only test routes."
                    ]));
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

    private static async Task ClickFileMenuCommandAsync(MainWindow window, string commandId, List<string> steps, CancellationToken ct, Action recordPointerAction)
    {
        IMenuBarSurface menuSurface = window.ControlsForAutomation.MenuBar;
        Control host = menuSurface switch
        {
            Control control => control,
            _ => throw new InvalidOperationException("Active menu bar surface does not expose a control.")
        };

        MenuItem fileMenu = FindVisibleMenuButton(host, "FileMenuButton")
            ?? throw new InvalidOperationException("File menu button was not found.");

        await RoutePointerClickAsync(fileMenu);
        recordPointerAction();
        RecordStep(steps, "click file menu");

        await WaitForAsync(
            steps,
            $"menu command {commandId} available",
            () => fileMenu.Items.OfType<MenuItem>().Any(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled),
            ct);

        MenuItem commandItem = fileMenu.Items.OfType<MenuItem>()
            .First(item => string.Equals(item.Tag?.ToString(), commandId, StringComparison.Ordinal) && item.IsEnabled);
        await RoutePointerClickAsync(commandItem);
        recordPointerAction();
        RecordStep(steps, $"click file menu command {commandId}");
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

    private static async Task VerifyDialogSelectFieldValueAsync(MainWindow window, string fieldId, string value, List<string> steps, CancellationToken ct)
    {
        string controlName = DesktopDialogAccessibility.BuildFieldInputName(fieldId);
        await WaitForAsync(
            steps,
            $"dialog select field {fieldId} available",
            () => FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName) is not null,
            ct);

        ComboBox comboBox = FindVisibleDescendant<ComboBox>(ResolveDialogRoot(window), controlName)
            ?? throw new InvalidOperationException($"Dialog select field '{fieldId}' was not found.");
        bool matches = await Dispatcher.UIThread.InvokeAsync(() => string.Equals(ReadDialogOptionValue(comboBox.SelectedItem ?? comboBox.SelectionBoxItem ?? comboBox), value, StringComparison.Ordinal));
        if (!matches)
        {
            throw new InvalidOperationException($"Dialog select field '{fieldId}' was expected to default to '{value}'.");
        }
        RecordStep(steps, $"confirm dialog field {fieldId} = {value}");
    }

    private static async Task WaitForAsync(List<string> steps, string description, Func<bool> predicate, CancellationToken ct)
    {
        if (await WaitForConditionWithinAsync(predicate, ct, WaitTimeout))
        {
            RecordStep(steps, $"wait success: {description}");
            return;
        }

        throw new TimeoutException($"Timed out while waiting for {description}.");
    }

    private static async Task CaptureEvidenceScreenshotAsync(
        MainWindow window,
        DesktopMouseFirstJourneyContext context,
        List<string> screenshotPaths,
        string fileStem)
    {
        if (string.IsNullOrWhiteSpace(context.ScreenshotDirectory))
        {
            return;
        }

        string screenshotDirectory = context.ScreenshotDirectory;
        string screenshotPath = Path.Combine(screenshotDirectory, $"{fileStem}.png");
        byte[] pngBytes = await Dispatcher.UIThread.InvokeAsync(window.CaptureScreenshotBytesForAutomation);
        Directory.CreateDirectory(screenshotDirectory);
        await File.WriteAllBytesAsync(screenshotPath, pngBytes);
        screenshotPaths.Add(screenshotPath);
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
        string expectedRulesetId)
    {
        string windowTextSnapshot = ReadWindowTextSnapshot(window);
        string expectedRulesetToken = expectedRulesetId.ToUpperInvariant();
        return ContainsWindowText(windowTextSnapshot, expectedCharacterName)
            && ContainsWindowText(windowTextSnapshot, expectedCharacterAlias)
            && ContainsWindowText(windowTextSnapshot, expectedRulesetToken);
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

    private static string? ReadDialogOptionValue(object candidate)
    {
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
