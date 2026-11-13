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
    private readonly Func<CareerManualKarmaEditRequest, CancellationToken, Task<string?>>? _applyKarma;
    private readonly Func<CareerManualNuyenEditRequest, CancellationToken, Task<string?>>? _applyNuyen;
    private readonly Func<CancellationToken, Task<(CareerManualKarmaEditorState? Karma, CareerManualNuyenEditorState? Nuyen)>>? _reprepare;
    private readonly TextBox _amountBox;
    private readonly TextBox _reasonBox;
    private readonly RadioButton _gainRadio;
    private readonly RadioButton _spendRadio;
    private readonly Button _applyButton;
    private readonly TextBlock _balanceText;
    private readonly Func<bool> _contextIsCurrent;
    private bool _applyInFlight;
    private bool _closed;

    public CareerRewardsWindow(bool isKarmaMode, CareerManualKarmaEditorState karmaState, CareerManualNuyenEditorState? nuyenState)
        : this(isKarmaMode, karmaState, nuyenState, applyKarma: null, applyNuyen: null, reprepare: null)
    {
    }

    /// <summary>
    /// Inline-apply constructor (UX-03): the dialog applies the mutation itself,
    /// so an authority refusal keeps the dialog open with the amount, direction
    /// and notes intact, discloses the refusal reason, swaps in a freshly
    /// prepared editor state, and requires an explicit second Record press.
    /// </summary>
    public CareerRewardsWindow(
        bool isKarmaMode,
        CareerManualKarmaEditorState karmaState,
        CareerManualNuyenEditorState? nuyenState,
        Func<CareerManualKarmaEditRequest, CancellationToken, Task<string?>>? applyKarma,
        Func<CareerManualNuyenEditRequest, CancellationToken, Task<string?>>? applyNuyen,
        Func<CancellationToken, Task<(CareerManualKarmaEditorState? Karma, CareerManualNuyenEditorState? Nuyen)>>? reprepare,
        Func<bool>? contextIsCurrent = null)
    {
        _isKarmaMode = isKarmaMode;
        _karmaState = karmaState;
        _nuyenState = nuyenState;
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
        buttonRow.Children.Add(_applyButton);
        buttonRow.Children.Add(cancelButton);
        root.Children.Add(buttonRow);

        Content = root;
        _amountBox.AttachedToVisualTree += (_, _) => _amountBox.Focus();
    }

    /// <summary>
    /// True when this dialog applies mutations inline (UX-03): a refused apply
    /// keeps the dialog open instead of discarding the reviewed entry.
    /// </summary>
    public bool AppliesInline => _applyKarma is not null || _applyNuyen is not null;

    /// <summary>The most recent authority-refusal reason, cleared on the next Record press.</summary>
    public string? LastRefusal { get; private set; }

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
        if (_closed || _applyInFlight)
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

                await ApplyPreparedOutcomeAsync(outcome, _applyKarma(request, CancellationToken.None)).ConfigureAwait(true);
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

            await ApplyPreparedOutcomeAsync(nuyenOutcome, _applyNuyen(nuyenRequest, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception applicationFailure)
        {
            if (!_closed)
                _balanceText.Text = "Recording could not be confirmed: " + applicationFailure.Message;
        }
        finally
        {
            _applyInFlight = false;
            if (!_closed)
                SetEntryEnabled(true);
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
        if (!_contextIsCurrent())
            throw new InvalidOperationException("The runner or account changed. Close this dialog and reopen it before recording.");
    }

    private async Task ApplyPreparedOutcomeAsync(object outcome, Task<string?> applyTask)
    {
        _balanceText.Text = "Recording...";
        LastRefusal = null;
        string? refusal = await applyTask.ConfigureAwait(true);
        if (_closed)
            return;
        RequireCurrentContext();
        if (string.IsNullOrWhiteSpace(refusal))
        {
            Close(outcome);
            return;
        }

        // Authority refusal (UX-03): keep the dialog open with the amount,
        // direction and notes intact, disclose the refusal reason, swap in a
        // freshly prepared editor state, and require an explicit second Record
        // press before anything else is committed.
        LastRefusal = refusal.Trim();
        await ReprepareAfterRefusalAsync().ConfigureAwait(true);
        if (_closed)
            return;
        _balanceText.Text = "Not recorded: " + refusal.Trim()
            + " Review the entry and press Record again to confirm.";
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
