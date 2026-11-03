using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Creation;

/// <summary>Edits a complete selection locally, then reviews Core's result before one write.</summary>
public sealed class CreationQualitiesReviewWindow : Window
{
    private readonly CharacterCreationQualitiesInteraction _session;
    private readonly Func<CharacterOverviewState> _current;
    private readonly HashSet<string> _selected;
    private readonly List<CheckBox> _choices = [];
    private readonly TextBlock _reviewText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, IsVisible = false };
    private readonly Button _review = new() { Content = "Review selection" };
    private readonly Button _confirm = new() { Content = "Confirm selection", IsEnabled = false };
    private readonly Button _check = new() { Content = "Check saved quality selection", IsVisible = false };
    private readonly Button _clear = new() { Content = "Remove all selected qualities" };
    private CharacterCreationQualitiesSelectionReview? _prepared;
    private bool _busy;

    public CreationQualitiesReviewWindow(CharacterCreationQualitiesInteraction session,
        CharacterCreationQualitiesState state, Func<CharacterOverviewState> current,
        string? optionId = null, bool remove = false)
    {
        _session = session;
        _current = current;
        _selected = CharacterCreationQualitiesInteraction.SelectedIds(state).ToHashSet(StringComparer.Ordinal);
        string[] displayed = _selected.Concat(optionId is null ? [] : new[] { optionId })
            .Distinct(StringComparer.Ordinal).ToArray();
        if (optionId is not null)
        {
            if (remove) _selected.Remove(optionId);
            else _selected.Add(optionId);
        }
        Title = "Review qualities";
        Width = 640;
        Height = 680;
        MinWidth = 400;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var body = new StackPanel { Spacing = 12, Margin = new global::Avalonia.Thickness(20) };
        body.Children.Add(new TextBlock
        {
            Text = "Keep the qualities you want. Uncheck a quality to remove it, then review the complete selection before saving. Add other qualities from their cards in the wizard.",
            TextWrapping = TextWrapping.Wrap
        });
        foreach (string id in displayed)
        {
            string name = state.Authority.Options.FirstOrDefault(item => item.OptionId == id)?.Name
                ?? state.Preview.Selections.FirstOrDefault(item => item.OptionId == id)?.Name ?? id;
            var choice = new CheckBox { Content = name, IsChecked = _selected.Contains(id), Tag = id };
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true) _selected.Add(id); else _selected.Remove(id);
                _prepared = null;
                _confirm.IsEnabled = false;
                _reviewText.Text = "Selection changed. Review it before confirming.";
            };
            _choices.Add(choice);
            body.Children.Add(choice);
        }
        if (displayed.Length == 0)
            body.Children.Add(new TextBlock { Text = "No optional qualities selected. You can review and confirm an empty selection if the rules allow it." });
        _clear.Click += (_, _) => { foreach (CheckBox choice in _choices) choice.IsChecked = false; };
        _review.Click += async (_, _) => await ReviewAsync();
        _confirm.Click += async (_, _) => await ConfirmAsync();
        _check.Click += async (_, _) => await CheckAsync();
        var cancel = new Button { Content = "Close without further changes" };
        cancel.Click += (_, _) => { if (!_busy) Close(_session.Confirmed); };
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
        body.Children.Add(_clear);
        body.Children.Add(_review);
        body.Children.Add(_reviewText);
        body.Children.Add(_progress);
        body.Children.Add(_status);
        body.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { _confirm, _check, cancel }
        });
        Content = new ScrollViewer { Content = body };
        UpdateAvailability();
        if (session.OutcomeUncertain) _status.Text = session.Status;
    }

    private async Task ReviewAsync()
    {
        if (_busy || _session.OutcomeUncertain) return;
        SetBusy(true, "Checking the complete selection and Karma costs…");
        string[] ids = _selected.ToArray();
        try
        {
            CharacterCreationQualitiesSelectionReview prepared = await Task.Run(() => _session.Prepare(ids));
            // Same rule as the load path: a review prepared across a character/account or
            // revision transition is discarded, never presented (scope item 2, 3 Nov handoff).
            _session.RequireDisplayContext(_current());
            _prepared = prepared;
            _reviewText.Text = Describe(_prepared);
            _status.Text = _prepared.Preview.CanConfirm ? "Review the additions and removals, then confirm." : "These choices are blocked. Adjust the selection before confirming.";
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _prepared = null;
            _status.Text = "The selection could not be reviewed. Nothing was changed. Reload the character and check its prerequisites and Karma.";
        }
        finally { SetBusy(false); }
    }

    private async Task ConfirmAsync()
    {
        if (_busy || _prepared is not { } prepared || _session.OutcomeUncertain) return;
        SetBusy(true, "Saving your quality selection…");
        CharacterOverviewState current = _current();
        try
        {
            bool saved = await Task.Run(() => _session.Confirm(prepared, current));
            _status.Text = _session.Status;
            if (saved) { SetBusy(false); Close(true); }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _status.Text = "The character or account changed during review. No new selection was submitted. Reload the original character and review it again.";
        }
        finally { _prepared = null; SetBusy(false); }
    }

    private async Task CheckAsync()
    {
        if (_busy) return;
        SetBusy(true, "Reading the saved selection. This does not submit another change…");
        CharacterOverviewState current = _current();
        try
        {
            bool saved = await Task.Run(() => _session.Reconcile(current));
            _status.Text = _session.Status;
            if (saved) { SetBusy(false); Close(true); }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _status.Text = "Return to the original character and account, then check its saved selection again.";
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        _progress.IsVisible = busy;
        if (status is not null) _status.Text = status;
        UpdateAvailability();
    }

    private void UpdateAvailability()
    {
        bool editable = !_busy && !_session.OutcomeUncertain && !_session.Confirmed;
        foreach (CheckBox choice in _choices) choice.IsEnabled = editable;
        _clear.IsEnabled = editable;
        _review.IsEnabled = editable;
        _confirm.IsEnabled = editable && _prepared?.Preview is { CanConfirm: true, Blockers.Count: 0 };
        _check.IsVisible = _session.OutcomeUncertain;
        _check.IsEnabled = !_busy;
    }

    public static string Describe(CharacterCreationQualitiesSelectionReview review)
    {
        var before = review.Before.Preview.Selections;
        var after = review.Preview.Selections;
        var beforeIds = before.Select(item => item.OptionId).ToHashSet(StringComparer.Ordinal);
        var afterIds = after.Select(item => item.OptionId).ToHashSet(StringComparer.Ordinal);
        static string Names(IEnumerable<CharacterCreationQualitySelection> items)
            => string.Join(", ", items.Select(item => $"{item.Name} ({item.KarmaCost} Karma)")) is { Length: > 0 } names ? names : "None";
        return string.Join(Environment.NewLine + Environment.NewLine,
            "Kept: " + Names(after.Where(item => beforeIds.Contains(item.OptionId))),
            "Added: " + Names(after.Where(item => !beforeIds.Contains(item.OptionId))),
            "Removed: " + Names(before.Where(item => !afterIds.Contains(item.OptionId))),
            "Complete selection after saving: " + Names(after),
            $"Positive quality Karma: {review.Before.Preview.PositiveQualityBudget.Used} → {review.Preview.PositiveQualityBudget.Used} / {review.Preview.PositiveQualityBudget.Total}",
            $"Negative quality Karma: {review.Before.Preview.NegativeQualityBudget.Used} → {review.Preview.NegativeQualityBudget.Used} / {review.Preview.NegativeQualityBudget.Total}",
            $"Karma remaining: {review.Before.Preview.KarmaRemaining} → {review.Preview.KarmaRemaining}",
            "Granted qualities remain unchanged: " + (review.Preview.GrantedQualities.Count == 0 ? "None" : string.Join(", ", review.Preview.GrantedQualities.Select(item => item.Name))),
            review.Preview.Blockers.Count == 0 ? "No rule blockers." : "Core reports blockers for this selection; adjust it before confirming.");
    }
}
