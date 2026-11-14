using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop modal for recording run rewards in career mode: manual
/// karma awards and nuyen adjustments. Mirrors Chummer5a's expense recording
/// with explicit Gain/Spend semantics; the authoritative validation lives in
/// the presenter's revision-guarded manual-karma/nuyen editors.
/// </summary>
public sealed class CareerRewardsWindow : Window
{
    public sealed record CareerKarmaAwardOutcome(
        string DisplayLabel,
        CareerManualKarmaEditRequest Request);

    public sealed record CareerNuyenAdjustOutcome(
        string DisplayLabel,
        CareerManualNuyenEditRequest Request);

    private const string ApplyAutomationId = "career-rewards-apply";
    private const string CancelAutomationId = "career-rewards-cancel";
    private const string AmountAutomationId = "career-rewards-amount";
    private const string ReasonAutomationId = "career-rewards-reason";
    private const string GainRadioAutomationId = "career-rewards-gain";
    private const string SpendRadioAutomationId = "career-rewards-spend";

    private readonly bool _isKarmaMode;
    private CareerManualKarmaEditorState _karmaState;
    private CareerManualNuyenEditorState? _nuyenState;
    private readonly Func<CareerManualKarmaEditRequest, CancellationToken, Task<CareerRewardApplyResult>>? _applyKarma;
    private readonly Func<CareerManualNuyenEditRequest, CancellationToken, Task<CareerRewardApplyResult>>? _applyNuyen;
    private readonly Func<CancellationToken, Task<(CareerManualKarmaEditorState? Karma, CareerManualNuyenEditorState? Nuyen)>>? _reprepare;
    private readonly TextBox _amountBox;
    private readonly TextBox _reasonBox;
    private readonly RadioButton _gainRadio;
    private readonly RadioButton _spendRadio;
    private readonly Button _applyButton;
    private readonly Button _checkButton;
    private readonly Button _acknowledgeButton;
    private readonly Func<Guid, CancellationToken, Task<CareerRewardApplyResult>>? _reconcile;
    private readonly Func<Guid, bool>? _acknowledge;
    public sealed record CareerRewardReconciledOutcome;
    public sealed record CareerRewardAcknowledgedOutcome;
    private readonly TextBlock _balanceText;
    private readonly Func<bool> _contextIsCurrent;
    private bool _applyInFlight;
    private bool _closed;
    private bool _entryInvalidated;
    private bool _acknowledged;

    public CareerRewardsWindow(bool isKarmaMode, CareerManualKarmaEditorState karmaState, CareerManualNuyenEditorState? nuyenState)
        : this(isKarmaMode, karmaState, nuyenState, applyKarma: null, applyNuyen: null, reprepare: null)
    {
    }

    /// <summary>
    /// Keeps refused entries for review. An unconfirmed dispatch retains its operation
    /// and disables recording; only a read-only check may confirm the stored result.
    /// </summary>
    public CareerRewardsWindow(
        bool isKarmaMode,
        CareerManualKarmaEditorState karmaState,
        CareerManualNuyenEditorState? nuyenState,
        Func<CareerManualKarmaEditRequest, CancellationToken, Task<CareerRewardApplyResult>>? applyKarma,
        Func<CareerManualNuyenEditRequest, CancellationToken, Task<CareerRewardApplyResult>>? applyNuyen,
        Func<CancellationToken, Task<(CareerManualKarmaEditorState? Karma, CareerManualNuyenEditorState? Nuyen)>>? reprepare,
        Func<bool>? contextIsCurrent = null,
        Func<Guid, CancellationToken, Task<CareerRewardApplyResult>>? reconcile = null,
        CareerRewardApplyResult? pendingResult = null,
        Func<Guid, bool>? acknowledge = null)
    {
        _isKarmaMode = isKarmaMode;
        _karmaState = karmaState;
        _nuyenState = nuyenState;
        _reconcile = reconcile;
        _acknowledge = acknowledge;
        _applyKarma = applyKarma;
        _applyNuyen = applyNuyen;
        _reprepare = reprepare;
        _contextIsCurrent = contextIsCurrent ?? (() => true);
        Closed += (_, _) => _closed = true;
        Title = isKarmaMode ? "Award Karma" : "Adjust Nuyen";
        Width = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        StackPanel root = new() { Margin = new Thickness(14), Spacing = 10 };

        _balanceText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_balanceText, "career-rewards-balance");
        RenderBalance();
        root.Children.Add(_balanceText);

        StackPanel actionRow = new() { Orientation = Orientation.Horizontal, Spacing = 14 };
        _gainRadio = new RadioButton
        {
            Content = isKarmaMode ? "Gain (award)" : "Gain (payment)",
            IsChecked = true,
            GroupName = "career-rewards-action",
        };
        _spendRadio = new()
        {
            Content = "Spend / loss",
            GroupName = "career-rewards-action",
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_gainRadio, GainRadioAutomationId);
        actionRow.Children.Add(_gainRadio);
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_spendRadio, SpendRadioAutomationId);
        actionRow.Children.Add(_spendRadio);
        root.Children.Add(actionRow);

        root.Children.Add(new TextBlock { Text = isKarmaMode ? "Karma amount:" : "Nuyen amount (¥):", FontSize = 12 });
        _amountBox = new TextBox
        {
            Watermark = "0",
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_amountBox, AmountAutomationId);
        root.Children.Add(_amountBox);

        root.Children.Add(new TextBlock { Text = "Reason / run notes:", FontSize = 12 });
        _reasonBox = new TextBox
        {
            Watermark = "e.g. Extraction run for Mr. Johnson",
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_reasonBox, ReasonAutomationId);
        root.Children.Add(_reasonBox);

        _applyButton = new()
        {
            Content = "Record",
            MinWidth = 92,
            Height = 26,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_applyButton, ApplyAutomationId);
        _applyButton.Click += (_, _) => _ = ApplyInlineAsync();

        Button cancelButton = new()
        {
            Content = "Cancel",
            MinWidth = 92,
            Height = 26,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(cancelButton, CancelAutomationId);
        cancelButton.Click += (_, _) => Close(null);

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 6, 0, 0)
        };
        _checkButton = new Button { Content = "Check recorded reward", IsVisible = false };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_checkButton, "career-rewards-check");
        _checkButton.Click += async (_, _) => await CheckRecordedRewardAsync().ConfigureAwait(true);
        buttonRow.Children.Add(_checkButton);
        _acknowledgeButton = new Button { Content = "Acknowledge recorded reward", IsVisible = false };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_acknowledgeButton, "career-rewards-acknowledge");
        _acknowledgeButton.Click += (_, _) => AcknowledgeRecordedReward();
        buttonRow.Children.Add(_acknowledgeButton);
        buttonRow.Children.Add(_applyButton);
        buttonRow.Children.Add(cancelButton);
        root.Children.Add(buttonRow);

        Content = root;
        if (pendingResult?.Status is CareerRewardApplyStatus.Uncertain or CareerRewardApplyStatus.Confirmed)
        {
            LastResult = pendingResult;
            if (pendingResult.Status == CareerRewardApplyStatus.Confirmed) ShowConfirmation();
            else ShowUncertainty();
        }
        _amountBox.AttachedToVisualTree += (_, _) => _amountBox.Focus();
    }

    /// <summary>
    /// True when this dialog applies mutations inline (UX-03): a refused apply
    /// keeps the dialog open instead of discarding the reviewed entry.
    /// </summary>
    public bool AppliesInline => _applyKarma is not null || _applyNuyen is not null;

    /// <summary>The most recent definite non-commit refusal, cleared before another attempt.</summary>
    public string? LastRefusal { get; private set; }
    public CareerRewardApplyResult? LastResult { get; private set; }

    public string? CloseNotice => LastResult?.Status switch
    {
        CareerRewardApplyStatus.Uncertain => "Recording could not be confirmed. The reward may already be recorded. Do not enter it again. "
            + "Reopen the reward dialog and use Check recorded reward.",
        CareerRewardApplyStatus.Confirmed => _acknowledged
            ? "The reward was recorded and acknowledged."
            : "The reward was recorded. Reopen the reward dialog to review and acknowledge it before entering another reward.",
        CareerRewardApplyStatus.Refused => "Not recorded: " + LastRefusal,
        _ => null
    };

    private void RenderBalance()
    {
        decimal availableNuyen = _isKarmaMode
            ? _karmaState.Karma.AvailableNuyen
            : (_nuyenState?.Nuyen.AvailableNuyen ?? 0m);
        int availableKarma = _isKarmaMode
            ? _karmaState.Karma.AvailableKarma
            : (_nuyenState?.Nuyen.AvailableKarma ?? 0);
        _balanceText.Text = _isKarmaMode
            ? "Available karma: " + availableKarma.ToString(CultureInfo.InvariantCulture)
                + "   \u00b7   Nuyen: \u00a5" + availableNuyen.ToString("N0", CultureInfo.InvariantCulture)
            : "Available nuyen: \u00a5" + availableNuyen.ToString("N0", CultureInfo.InvariantCulture)
                + "   \u00b7   Karma: " + availableKarma.ToString(CultureInfo.InvariantCulture);
    }

    private async Task ApplyInlineAsync()
    {
        if (_closed || !IsVisible || _entryInvalidated || _applyInFlight
            || LastResult?.Status is CareerRewardApplyStatus.Uncertain or CareerRewardApplyStatus.Confirmed)
        {
            return;
        }

        _applyInFlight = true;
        SetEntryEnabled(false);
        try
        {
            RequireCurrentContext();
            string amountText = _amountBox.Text?.Trim() ?? string.Empty;
            if (!int.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)
                || amount <= 0)
            {
                RenderBalance();
                _balanceText.Text = "Enter a positive whole-number amount.";
                return;
            }

            bool gain = _gainRadio.IsChecked == true;
            string reason = _reasonBox.Text?.Trim() ?? string.Empty;
            DateTime expenseDate = DateTime.Now.Date;

            if (_isKarmaMode)
            {
                // The dialog binds to the prepared editor state: workspace id,
                // expected content revision and the projected karma state are all
                // carried from Prepare, so the mutation validates against what
                // the user reviewed.
                var request = new CareerManualKarmaEditRequest(
                    _karmaState.WorkspaceId,
                    _karmaState.ContentRevision,
                    _karmaState.Karma,
                    gain ? CharacterCareerManualKarmaAction.Gain : CharacterCareerManualKarmaAction.Spend,
                    amount,
                    reason,
                    expenseDate,
                    Refund: false,
                    KarmaNuyenExchange: false,
                    ForceCareerVisible: false);
                var outcome = new CareerKarmaAwardOutcome(
                    (gain ? "+" : "-") + amount + " karma" + (reason.Length > 0 ? " (" + reason + ")" : string.Empty),
                    request);
                if (_applyKarma is null)
                {
                    Close(outcome);
                    return;
                }

                await ApplyPreparedOutcomeAsync(outcome, request.OperationId, () => _applyKarma(request, CancellationToken.None)).ConfigureAwait(true);
                return;
            }

            var nuyenRequest = new CareerManualNuyenEditRequest(
                _nuyenState!.WorkspaceId,
                _nuyenState.ContentRevision,
                _nuyenState.Nuyen,
                gain ? CharacterCareerManualNuyenAction.Gain : CharacterCareerManualNuyenAction.Spend,
                amount,
                Percent: 0m,
                reason,
                expenseDate,
                Refund: false,
                KarmaNuyenExchange: false,
                ForceCareerVisible: false);
            var nuyenOutcome = new CareerNuyenAdjustOutcome(
                (gain ? "+" : "-") + "\u00a5" + amount.ToString("N0", CultureInfo.InvariantCulture)
                    + (reason.Length > 0 ? " (" + reason + ")" : string.Empty),
                nuyenRequest);
            if (_applyNuyen is null)
            {
                Close(nuyenOutcome);
                return;
            }

            await ApplyPreparedOutcomeAsync(nuyenOutcome, nuyenRequest.OperationId, () => _applyNuyen(nuyenRequest, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception applicationFailure)
        {
            if (!_closed)
                _balanceText.Text = _entryInvalidated ? applicationFailure.Message
                    : LastResult?.Status == CareerRewardApplyStatus.Confirmed
                        ? "The reward was recorded. This dialog could not finish: " + applicationFailure.Message
                        : "Recording could not be confirmed: " + applicationFailure.Message;
        }
        finally
        {
            _applyInFlight = false;
            if (!_closed)
                SetEntryEnabled(!_entryInvalidated
                    && LastResult?.Status is not (CareerRewardApplyStatus.Uncertain or CareerRewardApplyStatus.Confirmed));
        }
    }

    private void SetEntryEnabled(bool enabled)
    {
        _applyButton.IsEnabled = enabled;
        _amountBox.IsEnabled = enabled;
        _reasonBox.IsEnabled = enabled;
        _gainRadio.IsEnabled = enabled;
        _spendRadio.IsEnabled = enabled;
    }

    private void RequireCurrentContext()
    {
        if (_entryInvalidated || !_contextIsCurrent())
        {
            _entryInvalidated = true;
            throw new InvalidOperationException("The runner or account changed. Close this dialog and reopen it before recording.");
        }
    }

    private async Task ApplyPreparedOutcomeAsync(object outcome, Guid operationId, Func<Task<CareerRewardApplyResult>> apply)
    {
        _balanceText.Text = "Recording...";
        LastRefusal = null;
        // Until the joined presenter result proves otherwise, close/cancellation cannot claim refusal.
        LastResult = new(operationId, CareerRewardApplyStatus.Uncertain, CareerRewardApplyResult.UncertainMessage);
        try { LastResult = await apply().ConfigureAwait(true); }
        catch (Exception) { /* Dispatch may have committed. Retain uncertainty and the operation identity. */ }
        if (_closed) return;
        RequireCurrentContext();
        if (LastResult.Status == CareerRewardApplyStatus.Confirmed)
        {
            CloseConfirmed(LastResult.OperationId == operationId ? outcome : new CareerRewardReconciledOutcome());
            return;
        }
        if (LastResult.Status == CareerRewardApplyStatus.Uncertain)
        {
            ShowUncertainty();
            return;
        }

        await ShowRefusalAsync().ConfigureAwait(true);
    }

    private async Task ShowRefusalAsync()
    {
        // Only a definite non-commit refusal permits a fresh, explicitly reviewed attempt.
        _checkButton.IsVisible = false;
        LastRefusal = LastResult!.Message ?? "Recording stopped before it was sent.";
        await ReprepareAfterRefusalAsync().ConfigureAwait(true);
        if (_closed) return;
        _balanceText.Text = "Not recorded: " + LastRefusal
            + " Review the entry and press Record again to confirm.";
    }

    private void ShowConfirmation(string? closeFailure = null)
    {
        SetEntryEnabled(false);
        _applyButton.IsVisible = false;
        _checkButton.IsVisible = false;
        _acknowledgeButton.IsVisible = _acknowledge is not null;
        _acknowledgeButton.IsEnabled = !_acknowledged && !_entryInvalidated;
        _balanceText.Text = "The reward is confirmed recorded. "
            + (_acknowledged ? "Close this dialog before entering another reward."
                : "Acknowledge this result before entering another reward.")
            + (closeFailure is null ? string.Empty : " The dialog could not close: " + closeFailure);
    }

    private void CloseConfirmed(object outcome)
    {
        // UI teardown is separate from the committed operation. Even if Closing throws or
        // cancels, this entry stays terminal and must never be dispatched a second time.
        SetEntryEnabled(false);
        try
        {
            Close(outcome);
            if (!_closed) ShowConfirmation("Close was cancelled.");
        }
        catch (Exception closeFailure)
        {
            if (!_closed && _contextIsCurrent()) ShowConfirmation(closeFailure.Message);
        }
    }

    private void AcknowledgeRecordedReward()
    {
        // Construction, an automatic close, and a late callback never acknowledge a result.
        if (_closed || !IsVisible || _entryInvalidated || _applyInFlight || _acknowledged
            || LastResult?.Status != CareerRewardApplyStatus.Confirmed || _acknowledge is null)
            return;
        _applyInFlight = true;
        try
        {
            RequireCurrentContext();
            if (!_acknowledge(LastResult.OperationId))
            {
                _entryInvalidated = true;
                _acknowledgeButton.IsEnabled = false;
                _balanceText.Text = "This result could not be acknowledged in the current view. Close and reopen the reward dialog.";
                return;
            }
            _acknowledged = true;
            _acknowledgeButton.IsEnabled = false;
            CloseConfirmed(new CareerRewardAcknowledgedOutcome());
        }
        catch (Exception)
        {
            _entryInvalidated = true;
            _acknowledgeButton.IsEnabled = false;
            _balanceText.Text = "The runner or account changed. Close and reopen the reward dialog.";
        }
        finally { _applyInFlight = false; }
    }

    private void ShowUncertainty()
    {
        _balanceText.Text = CareerRewardApplyResult.UncertainMessage;
        _checkButton.IsVisible = _reconcile is not null;
        _checkButton.IsEnabled = true;
        SetEntryEnabled(false);
    }

    private async Task CheckRecordedRewardAsync()
    {
        if (_closed || _applyInFlight || LastResult?.Status != CareerRewardApplyStatus.Uncertain || _reconcile is null)
            return;
        _applyInFlight = true;
        _checkButton.IsEnabled = false;
        try
        {
            RequireCurrentContext();
            CareerRewardApplyResult result = await _reconcile(LastResult.OperationId, CancellationToken.None).ConfigureAwait(true);
            if (_closed) return;
            RequireCurrentContext();
            if (result.OperationId == LastResult.OperationId && result.Status == CareerRewardApplyStatus.Confirmed)
            {
                LastResult = result;
                CloseConfirmed(new CareerRewardReconciledOutcome());
                return;
            }
            if (result.OperationId == LastResult.OperationId && result.Status == CareerRewardApplyStatus.Refused)
            {
                LastResult = result;
                await ShowRefusalAsync().ConfigureAwait(true);
                return;
            }
            ShowUncertainty();
        }
        catch (Exception)
        {
            if (!_closed) ShowUncertainty();
        }
        finally
        {
            _applyInFlight = false;
            if (!_closed)
            {
                _checkButton.IsEnabled = true;
                SetEntryEnabled(!_entryInvalidated && LastResult?.Status == CareerRewardApplyStatus.Refused);
            }
        }
    }

    private async Task ReprepareAfterRefusalAsync()
    {
        if (_reprepare is null)
        {
            return;
        }

        (CareerManualKarmaEditorState? karma, CareerManualNuyenEditorState? nuyen) =
            await _reprepare(CancellationToken.None).ConfigureAwait(true);
        if (_closed)
            return;
        RequireCurrentContext();
        if (karma is null || karma.WorkspaceId != _karmaState.WorkspaceId
            || (!_isKarmaMode && (nuyen is null || nuyen.WorkspaceId != _nuyenState!.WorkspaceId)))
        {
            _entryInvalidated = true;
            throw new InvalidOperationException("The original runner could not be prepared. Close this dialog and reopen it before recording.");
        }
        if (karma is not null)
        {
            _karmaState = karma;
        }

        if (!_isKarmaMode && nuyen is not null)
        {
            _nuyenState = nuyen;
        }

        RenderBalance();
    }
}
