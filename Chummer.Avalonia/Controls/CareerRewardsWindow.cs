using System;
using System.Globalization;
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
    private readonly CareerManualKarmaEditorState _karmaState;
    private readonly CareerManualNuyenEditorState? _nuyenState;
    private readonly TextBox _amountBox;
    private readonly TextBox _reasonBox;
    private readonly RadioButton _gainRadio;
    private readonly TextBlock _balanceText;

    public CareerRewardsWindow(bool isKarmaMode, CareerManualKarmaEditorState karmaState, CareerManualNuyenEditorState? nuyenState)
    {
        _isKarmaMode = isKarmaMode;
        _karmaState = karmaState;
        _nuyenState = nuyenState;
        Title = isKarmaMode ? "Award Karma" : "Adjust Nuyen";
        Width = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        decimal availableNuyen = isKarmaMode
            ? karmaState.Karma.AvailableNuyen
            : (nuyenState?.Nuyen.AvailableNuyen ?? 0m);
        int availableKarma = isKarmaMode
            ? karmaState.Karma.AvailableKarma
            : (nuyenState?.Nuyen.AvailableKarma ?? 0);

        StackPanel root = new() { Margin = new Thickness(14), Spacing = 10 };

        _balanceText = new TextBlock
        {
            Text = isKarmaMode
                ? "Available karma: " + availableKarma.ToString(CultureInfo.InvariantCulture)
                    + "   ·   Nuyen: ¥" + availableNuyen.ToString("N0", CultureInfo.InvariantCulture)
                : "Available nuyen: ¥" + availableNuyen.ToString("N0", CultureInfo.InvariantCulture)
                    + "   ·   Karma: " + availableKarma.ToString(CultureInfo.InvariantCulture),
            FontSize = 12,
            Foreground = Brushes.Gray
        };
        root.Children.Add(_balanceText);
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_balanceText, "career-rewards-balance");

        StackPanel actionRow = new() { Orientation = Orientation.Horizontal, Spacing = 14 };
        _gainRadio = new RadioButton
        {
            Content = isKarmaMode ? "Gain (award)" : "Gain (payment)",
            IsChecked = true,
            GroupName = "career-rewards-action",
        };
        RadioButton spendRadio = new()
        {
            Content = "Spend / loss",
            GroupName = "career-rewards-action",
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(_gainRadio, GainRadioAutomationId);
        actionRow.Children.Add(_gainRadio);
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(spendRadio, SpendRadioAutomationId);
        actionRow.Children.Add(spendRadio);
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

        Button applyButton = new()
        {
            Content = "Record",
            MinWidth = 92,
            Height = 26,
        };
        global::Avalonia.Automation.AutomationProperties.SetAutomationId(applyButton, ApplyAutomationId);
        applyButton.Click += (_, _) => Apply();

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
        buttonRow.Children.Add(applyButton);
        buttonRow.Children.Add(cancelButton);
        root.Children.Add(buttonRow);

        Content = root;
        _amountBox.AttachedToVisualTree += (_, _) => _amountBox.Focus();
    }

    private void Apply()
    {
        string amountText = _amountBox.Text?.Trim() ?? string.Empty;
        if (!int.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
        {
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
            Close(new CareerKarmaAwardOutcome(
                (gain ? "+" : "-") + amount + " karma" + (reason.Length > 0 ? " (" + reason + ")" : string.Empty),
                request));
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
        Close(new CareerNuyenAdjustOutcome(
            (gain ? "+" : "-") + "¥" + amount.ToString("N0", CultureInfo.InvariantCulture)
                + (reason.Length > 0 ? " (" + reason + ")" : string.Empty),
            nuyenRequest));
    }
}
