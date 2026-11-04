using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TextAlignment = global::Avalonia.Media.TextAlignment;
using TextWrapping = global::Avalonia.Media.TextWrapping;
using ScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility;
using SelectionMode = global::Avalonia.Controls.SelectionMode;
using VAlign = global::Avalonia.Layout.VerticalAlignment;
using HAlign = global::Avalonia.Layout.HorizontalAlignment;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style career skill advancement dialog: a left step-navigation pane
/// (advancement categories), a quote list per category, and a classic footer with
/// Close / Advance buttons plus a status line.
/// </summary>
internal sealed class CareerSkillAdvanceWindow : Window
{
    private enum CategoryId
    {
        ActiveSkills,
        SkillGroups,
        KnowledgeSkills,
        Specializations,
    }

    private sealed record Row(
        string IdentityKey,
        string DisplayName,
        string Detail,
        string Cost,
        bool CanAdvance,
        string? BlockedReason,
        Func<CancellationToken, Task<bool>> ApplyAsync,
        Func<CancellationToken, Task<Row?>>? ReviewAsync = null);

    private readonly CharacterWorkspaceId _workspaceId;
    private readonly Func<CancellationToken, Task<CareerActiveSkillAdvanceEditorState?>> _prepareActive;
    private readonly Func<CareerActiveSkillAdvanceRequest, CancellationToken, Task<bool>> _applyActive;
    private readonly Func<CancellationToken, Task<CareerKnowledgeSkillAdvanceEditorState?>> _prepareKnowledge;
    private readonly Func<CareerKnowledgeSkillAdvanceRequest, CancellationToken, Task<CharacterCareerKnowledgeSkillAdvanceReceipt?>> _applyKnowledge;
    private readonly Func<CancellationToken, Task<CareerSkillGroupAdvanceEditorState?>> _prepareGroup;
    private readonly Func<CareerSkillGroupAdvanceRequest, CancellationToken, Task<bool>> _applyGroup;
    private readonly Func<CancellationToken, Task<CareerSkillSpecializationEditorState?>> _prepareSpecialization;
    private readonly Func<CareerSkillSpecializationQuoteRequest, CancellationToken, Task<CharacterCareerSkillSpecializationQuote?>> _prepareSpecializationQuote;
    private readonly Func<CareerSkillSpecializationRequest, CancellationToken, Task<bool>> _applySpecialization;

    private CareerActiveSkillAdvanceEditorState? _activeState;
    private CareerKnowledgeSkillAdvanceEditorState? _knowledgeState;
    private CareerSkillGroupAdvanceEditorState? _groupState;
    private CareerSkillSpecializationEditorState? _specializationState;

    private readonly StackPanel _navPane;
    private readonly ContentControl _contentHost;
    private ListBox? _skillList;
    private readonly TextBlock _statusLine;
    private readonly Button _applyButton;
    private CategoryId _selectedCategory = CategoryId.ActiveSkills;
    private Row? _selectedRow;
    private bool _busy;
    private bool _dispatchStarted;
    private bool _closed;
    private Row? _reviewedRow;
    private readonly Func<bool> _isCurrentContext;
    private readonly Func<bool> _isCurrentOwner;
    private readonly long _expectedRevision;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Guid _expenseId = Guid.NewGuid();
    private readonly Guid _specializationId = Guid.NewGuid();
    private readonly DateTime _expenseDate = DateTime.Now;

    public bool AdvanceApplied { get; private set; }

    public CareerSkillAdvanceWindow(
        CharacterWorkspaceId workspaceId,
        Func<CancellationToken, Task<CareerActiveSkillAdvanceEditorState?>> prepareActive,
        Func<CareerActiveSkillAdvanceRequest, CancellationToken, Task<bool>> applyActive,
        Func<CancellationToken, Task<CareerKnowledgeSkillAdvanceEditorState?>> prepareKnowledge,
        Func<CareerKnowledgeSkillAdvanceRequest, CancellationToken, Task<CharacterCareerKnowledgeSkillAdvanceReceipt?>> applyKnowledge,
        Func<CancellationToken, Task<CareerSkillGroupAdvanceEditorState?>> prepareGroup,
        Func<CareerSkillGroupAdvanceRequest, CancellationToken, Task<bool>> applyGroup,
        Func<CancellationToken, Task<CareerSkillSpecializationEditorState?>> prepareSpecialization,
        Func<CareerSkillSpecializationQuoteRequest, CancellationToken, Task<CharacterCareerSkillSpecializationQuote?>> prepareSpecializationQuote,
        Func<CareerSkillSpecializationRequest, CancellationToken, Task<bool>> applySpecialization,
        Func<bool> isCurrentContext, long expectedContentRevision, Func<bool>? isCurrentOwner = null)
    {
        _workspaceId = workspaceId;
        _isCurrentContext = isCurrentContext;
        _isCurrentOwner = isCurrentOwner ?? isCurrentContext;
        _expectedRevision = expectedContentRevision;
        Closed += (_, _) => { _closed = true; _lifetime.Cancel(); };
        _prepareActive = prepareActive;
        _applyActive = applyActive;
        _prepareKnowledge = prepareKnowledge;
        _applyKnowledge = applyKnowledge;
        _prepareGroup = prepareGroup;
        _applyGroup = applyGroup;
        _prepareSpecialization = prepareSpecialization;
        _prepareSpecializationQuote = prepareSpecializationQuote;
        _applySpecialization = applySpecialization;

        Title = "Advance Skills";
        Width = 900;
        Height = 620;
        MinWidth = 640;
        MinHeight = 440;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
        };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("200,*"),
        };
        body.SetValue(Grid.RowProperty, 0);

        _navPane = new StackPanel { Margin = new Thickness(12, 12, 6, 12) };
        var navScroll = new ScrollViewer
        {
            Content = _navPane,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        navScroll.SetValue(Grid.ColumnProperty, 0);
        body.Children.Add(navScroll);

        _contentHost = new ContentControl { Margin = new Thickness(6, 12, 12, 12) };
        _contentHost.SetValue(Grid.ColumnProperty, 1);
        body.Children.Add(_contentHost);

        root.Children.Add(body);

        var footer = new Border
        {
            BorderBrush = Brush.Parse("#CCCEDB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 10, 12, 10),
        };
        footer.SetValue(Grid.RowProperty, 1);
        var footerPanel = new DockPanel();
        _statusLine = new TextBlock
        {
            VerticalAlignment = VAlign.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        DockPanel.SetDock(_statusLine, Dock.Left);
        footerPanel.Children.Add(_statusLine);
        var footerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HAlign.Right,
            Spacing = 8,
        };
        var closeButton = new Button { Content = "Close", Name = "career-skill-advance-close", MinWidth = 88 };
        closeButton.Click += (_, _) => Close();
        footerButtons.Children.Add(closeButton);
        _applyButton = new Button { Content = "Review advancement", Name = "career-skill-advance-apply", MinWidth = 110 };
        _applyButton.Click += async (_, _) => await ApplySelectedAsync();
        footerButtons.Children.Add(_applyButton);
        footerPanel.Children.Add(footerButtons);
        footer.Child = footerPanel;
        root.Children.Add(footer);

        Content = root;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _applyButton.IsEnabled = false;
            _statusLine.Text = "Loading skill advancement choices…";
            if (InvalidateIfContextChanged()) return;
            _activeState = await _prepareActive(_lifetime.Token);
            if (InvalidateIfContextChanged() || !Matches(_activeState?.WorkspaceId, _activeState?.ContentRevision)) return;
            _knowledgeState = await _prepareKnowledge(_lifetime.Token);
            if (InvalidateIfContextChanged() || !Matches(_knowledgeState?.WorkspaceId, _knowledgeState?.ContentRevision)) return;
            _groupState = await _prepareGroup(_lifetime.Token);
            if (InvalidateIfContextChanged() || !Matches(_groupState?.WorkspaceId, _groupState?.ContentRevision)) return;
            _specializationState = await _prepareSpecialization(_lifetime.Token);
            if (InvalidateIfContextChanged() || !Matches(_specializationState?.WorkspaceId, _specializationState?.ContentRevision)) return;
        }
        catch (Exception)
        {
            _statusLine.Text = "Skill advancement could not be loaded. Close this window and reload the character.";
            return;
        }

        RebuildNavPane();
        if (_activeState is not null)
        {
            SelectCategory(CategoryId.ActiveSkills);
        }
        else if (_groupState is not null)
        {
            SelectCategory(CategoryId.SkillGroups);
        }
        else if (_knowledgeState is not null)
        {
            SelectCategory(CategoryId.KnowledgeSkills);
        }
        else if (_specializationState is not null)
        {
            SelectCategory(CategoryId.Specializations);
        }
        else
        {
            _statusLine.Text = "No saved career runner is open.";
            _applyButton.IsEnabled = false;
        }
    }

    private void RebuildNavPane()
    {
        _navPane.Children.Clear();
        AddNavItem(CategoryId.ActiveSkills, "Active Skills", _activeState is not null);
        AddNavItem(CategoryId.SkillGroups, "Skill Groups", _groupState is not null);
        AddNavItem(CategoryId.KnowledgeSkills, "Knowledge Skills", _knowledgeState is not null);
        AddNavItem(CategoryId.Specializations, "Specializations", _specializationState is not null);
    }

    private void AddNavItem(CategoryId id, string label, bool enabled)
    {
        var item = new Button
        {
            Content = label,
            HorizontalAlignment = HAlign.Stretch,
            HorizontalContentAlignment = HAlign.Left,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 2),
            IsEnabled = enabled,
            Tag = id,
        };
        item.Click += (_, _) => SelectCategory(id);
        _navPane.Children.Add(item);
    }

    private void SelectCategory(CategoryId id)
    {
        if (_busy || _dispatchStarted || InvalidateIfContextChanged()) return;
        _reviewedRow = null;
        _applyButton.Content = "Review advancement";
        _selectedCategory = id;
        _selectedRow = null;
        _applyButton.IsEnabled = false;

        Panel list = BuildQuoteList(id);
        _contentHost.Content = list;
        _statusLine.Text = DescribeCategory(id);
    }

    private string DescribeCategory(CategoryId id) => id switch
    {
        CategoryId.ActiveSkills => "Select an active skill, then choose Advance to buy the next rating with karma.",
        CategoryId.SkillGroups => "Select a skill group, then choose Advance to buy the next group rating with karma.",
        CategoryId.KnowledgeSkills => "Select a knowledge skill, then choose Advance to buy the next rating with karma.",
        CategoryId.Specializations => "Select a skill, pick a specialization option, then choose Advance to add it with karma.",
        _ => string.Empty,
    };

    private Panel BuildQuoteList(CategoryId id)
    {
        var panel = new StackPanel { Spacing = 4 };

        IReadOnlyList<Row> rows = id switch
        {
            CategoryId.ActiveSkills => BuildActiveRows(),
            CategoryId.SkillGroups => BuildGroupRows(),
            CategoryId.KnowledgeSkills => BuildKnowledgeRows(),
            CategoryId.Specializations => BuildSpecializationRows(),
            _ => new List<Row>(),
        };

        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Nothing available to advance.",
                FontStyle = FontStyle.Italic,
                Opacity = 0.75,
            });
            _skillList = null;
            return panel;
        }

        // The list must take the REMAINING window height (never the StackPanel's
        // unbounded measure): host it in a DockPanel so the ListBox is measured
        // against the bounded viewport and scrolls instead of growing forever.
        var list = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            Name = "career-skill-advance-list",
        };
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        list.SelectionChanged += OnSkillListSelectionChanged;
        foreach (Row row in rows)
        {
            list.Items.Add(new ListBoxItem
            {
                Content = BuildRowControl(row),
                Tag = row,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 4),
                Focusable = true,
            });
        }

        _skillList = list;
        var listHost = new DockPanel { LastChildFill = true };
        foreach (var child in panel.Children)
        {
            DockPanel.SetDock(child, Dock.Top);
        }
        listHost.Children.Add(panel);
        listHost.Children.Add(list);
        return listHost;
    }

    private StackPanel BuildRowControl(Row row)
    {
        var line1 = new TextBlock
        {
            Text = row.DisplayName,
            FontWeight = FontWeight.SemiBold,
        };
        var line2 = new TextBlock
        {
            Text = row.Detail,
            FontSize = 11.5,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap,
        };
        var line3 = new TextBlock
        {
            Text = row.CanAdvance ? row.Cost : row.BlockedReason ?? "Blocked",
            FontSize = 11.5,
            Foreground = row.CanAdvance ? Brushes.Green : Brushes.Gray,
        };

        var textStack = new StackPanel { Spacing = 1 };
        textStack.Children.Add(line1);
        textStack.Children.Add(line2);
        textStack.Children.Add(line3);

        var border = new Border
        {
            Child = textStack,
            Name = row.IdentityKey,
            Padding = new Thickness(8, 6, 8, 6),
            BorderBrush = Brush.Parse("#D0D3E0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Tag = row,
        };

        return new StackPanel { Children = { border } };
    }

    private void OnSkillListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_skillList?.SelectedItem is ListBoxItem item
            && item.Content is StackPanel wrapper
            && wrapper.Children[0] is Border border
            && border.Tag is Row row)
        {
            SelectRow(row, border);
        }
    }

    /// <summary>
    /// Programmatic selection entry point (test + accessibility path). Falls back to
    /// clearing the highlight when the row is not part of the rendered list.
    /// </summary>
    public bool SelectRowByKey(string identityKey)
    {
        if (_contentHost.Content is not Panel list || _skillList is null)
        {
            return false;
        }

        foreach (var o in _skillList.Items)
        {
            if (o is ListBoxItem item && item.Tag is Row r && r.IdentityKey == identityKey
                && item.Content is StackPanel wrapper && wrapper.Children[0] is Border b)
            {
                _skillList.SelectedItem = item;
                SelectRow(r, b);
                return true;
            }
        }

        return false;
    }

    private void SelectRow(Row row, Border border)
    {
        if (_busy || _dispatchStarted || InvalidateIfContextChanged()) return;
        _reviewedRow = null;
        SetSkillListLocked(false);
        _applyButton.Content = "Review advancement";
        if (_skillList is not null)
        {
            foreach (var o in _skillList.Items)
            {
                if (o is ListBoxItem it && it.Content is StackPanel w && w.Children[0] is Border b)
                {
                    b.Background = Brushes.Transparent;
                }
            }
        }

        border.Background = Brush.Parse("#CDE8FF");
        _selectedRow = row;
        _applyButton.IsEnabled = row.CanAdvance && !_busy;
        if (!row.CanAdvance)
        {
            _statusLine.Text = row.BlockedReason ?? "This entry cannot be advanced.";
        }
        else
        {
            _statusLine.Text = DescribeCategory(_selectedCategory);
        }
    }

    internal bool InvalidateIfContextChanged()
    {
        if (!_closed && (_dispatchStarted ? _isCurrentOwner() : _isCurrentContext())) return false;
        _reviewedRow = null;
        _applyButton.IsEnabled = false;
        _contentHost.Content = null;
        _statusLine.Text = "The runner or account changed. Reopen advancement for the current runner.";
        if (IsVisible) Close();
        return true;
    }

    private bool Matches(CharacterWorkspaceId? workspace, long? revision)
    {
        if (workspace is null || (workspace == _workspaceId && revision == _expectedRevision)) return true;
        _contentHost.Content = null;
        _applyButton.IsEnabled = false;
        _statusLine.Text = "The loaded advancement choices belong to a different runner revision. Reload the runner.";
        return false;
    }

    /// <summary>
    /// Audit 2026-11-04 P2-1: the visible selection may never diverge from the
    /// reviewed or submitted identity. While a quote is pending or a dispatch
    /// started, the ListBox stops accepting selection changes (input lock, not
    /// a data change); when idle again, selection follows the row the user picks.
    /// </summary>
    private void SetSkillListLocked(bool locked)
    {
        if (_skillList is null) return;
        _skillList.IsEnabled = !locked;
        _skillList.Focusable = !locked;
    }

    private async Task ApplySelectedAsync()
    {
        if (_busy || _dispatchStarted || InvalidateIfContextChanged() || _selectedRow is not { CanAdvance: true } row) return;
        _busy = true;
        _applyButton.IsEnabled = false;
        SetSkillListLocked(true);
        try
        {
            if (_reviewedRow is null)
            {
                var review = row.ReviewAsync is null ? row : await row.ReviewAsync(_lifetime.Token);
                if (InvalidateIfContextChanged()) return;
                if (review is not { CanAdvance: true })
                {
                    _statusLine.Text = "This advancement could not be quoted. Nothing was changed; reload the runner.";
                    return;
                }
                _reviewedRow = review;
                _statusLine.Text = review.DisplayName + " · " + review.Cost + ". Confirm this exact purchase?";
                _applyButton.Content = "Confirm purchase";
                return;
            }
            // Audit 2026-11-04 P2-1 identity triple-check: selected identity,
            // reviewed identity and the operation submitted must be the same row
            // before any purchase dispatch. A stale reviewed row from an earlier
            // selection is discarded, never submitted.
            if (_selectedRow is null || _reviewedRow is null
                || !string.Equals(_selectedRow.IdentityKey, _reviewedRow.IdentityKey, StringComparison.Ordinal))
            {
                _statusLine.Text = "The selected entry changed while the review was pending. That review was discarded; start the review again for the entry you want.";
                return;
            }
            _dispatchStarted = true;
            bool applied = await _reviewedRow.ApplyAsync(_lifetime.Token);
            AdvanceApplied = applied;
            _statusLine.Text = applied ? "Advancement saved." :
                "The advancement could not be verified. Close this window and reload the runner to check its saved result before another change.";
            if (applied) Close();
        }
        catch (Exception)
        {
            _statusLine.Text = _dispatchStarted
                ? "The result could not be read. Reload the runner and check the saved advancement before another change."
                : "The review could not be loaded. Nothing was changed; close this window and try again.";
        }
        finally
        {
            _busy = false;
            SetSkillListLocked(_dispatchStarted);
            _applyButton.IsEnabled = !_closed && !_dispatchStarted && _selectedRow?.CanAdvance == true && _isCurrentContext();
        }
    }

    private IReadOnlyList<Row> BuildActiveRows()
    {
        var rows = new List<Row>();
        if (_activeState is not { } state)
        {
            return rows;
        }

        foreach (var quote in state.Skills)
        {
            var captured = quote;
            rows.Add(new Row(
                IdentityKey: "A:" + quote.Identity.SkillId,
                DisplayName: quote.Name,
                Detail: string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · rating {1} (base {2}, karma {3}) · max {4}",
                    quote.SkillCategory,
                    quote.TotalBaseRating,
                    quote.BasePoints,
                    quote.KarmaPoints,
                    quote.RatingMaximum),
                Cost: string.Format(
                    CultureInfo.InvariantCulture,
                    "Next rating: {0} karma (available {1})",
                    quote.KarmaCost,
                    quote.AvailableKarma),
                CanAdvance: quote.CanAdvance,
                BlockedReason: quote.CanAdvance ? null : "Blocked: " + quote.Blocker,
                ApplyAsync: ct => ApplyActiveAsync(captured, ct)));
        }

        return rows;
    }

    private async Task<bool> ApplyActiveAsync(CharacterCareerActiveSkillAdvanceQuote quote, CancellationToken ct)
    {
        var request = new CareerActiveSkillAdvanceRequest(
            _workspaceId,
            _activeState!.ContentRevision,
            quote,
            quote.RuleDigest,
            Confirmed: true,
            ExpenseId: _expenseId,
            ExpenseDateLocal: _expenseDate);
        return await _applyActive(request, ct);
    }

    private IReadOnlyList<Row> BuildGroupRows()
    {
        var rows = new List<Row>();
        if (_groupState is not { } state)
        {
            return rows;
        }

        foreach (var quote in state.SkillGroups)
        {
            var captured = quote;
            rows.Add(new Row(
                IdentityKey: "G:" + quote.Identity.InternalId,
                DisplayName: quote.Name,
                Detail: string.Format(
                    CultureInfo.InvariantCulture,
                    "group rating {0} ({1} enabled members) · base {2}, karma {3} · max {4}",
                    quote.GroupRating,
                    quote.EnabledMemberCount,
                    quote.BasePoints,
                    quote.KarmaPoints,
                    quote.RatingMaximum),
                Cost: string.Format(
                    CultureInfo.InvariantCulture,
                    "Next rating: {0} karma (available {1})",
                    quote.KarmaCost,
                    quote.AvailableKarma),
                CanAdvance: quote.CanAdvance,
                BlockedReason: quote.CanAdvance ? null : "Blocked: " + quote.Blocker,
                ApplyAsync: ct => ApplyGroupAsync(captured, ct)));
        }

        return rows;
    }

    private async Task<bool> ApplyGroupAsync(CharacterCareerSkillGroupAdvanceQuote quote, CancellationToken ct)
    {
        var request = new CareerSkillGroupAdvanceRequest(
            _workspaceId,
            _groupState!.ContentRevision,
            quote,
            quote.RuleDigest,
            Confirmed: true,
            ExpenseId: _expenseId,
            ExpenseDateLocal: _expenseDate);
        return await _applyGroup(request, ct);
    }

    private IReadOnlyList<Row> BuildKnowledgeRows()
    {
        var rows = new List<Row>();
        if (_knowledgeState is not { } state)
        {
            return rows;
        }

        foreach (var quote in state.Skills)
        {
            var captured = quote;
            rows.Add(new Row(
                IdentityKey: "K:" + quote.Identity.SkillId,
                DisplayName: quote.Name,
                Detail: string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · {1} · rating {2} (base {3}, karma {4}) · max {5}",
                    quote.SkillType,
                    quote.SkillCategory,
                    quote.TotalBaseRating,
                    quote.BasePoints,
                    quote.KarmaPoints,
                    quote.RatingMaximum),
                Cost: quote.AllowUpgrade
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "Next rating: {0} karma (available {1})",
                        quote.KarmaCost,
                        quote.AvailableKarma)
                    : "Specialization only",
                CanAdvance: quote.CanAdvance,
                BlockedReason: quote.CanAdvance ? null : "Blocked: " + quote.Blocker,
                ApplyAsync: ct => ApplyKnowledgeAsync(captured, ct)));
        }

        return rows;
    }

    private async Task<bool> ApplyKnowledgeAsync(CharacterCareerKnowledgeSkillAdvanceQuote quote, CancellationToken ct)
    {
        var request = new CareerKnowledgeSkillAdvanceRequest(
            _workspaceId,
            _knowledgeState!.ContentRevision,
            quote,
            quote.CharacterRevision,
            quote.LogicalRevision,
            quote.SourceRevision,
            quote.RuleDigest,
            Confirmed: true,
            ExpenseId: _expenseId,
            ExpenseDateLocal: _expenseDate);
        CharacterCareerKnowledgeSkillAdvanceReceipt? receipt = await _applyKnowledge(request, ct);
        return receipt is not null;
    }

    private IReadOnlyList<Row> BuildSpecializationRows()
    {
        var rows = new List<Row>();
        if (_specializationState is not { } state)
        {
            return rows;
        }

        foreach (var candidate in state.Skills)
        {
            foreach (var option in candidate.AvailableOptions)
            {
                var capturedCandidate = candidate;
                var capturedOption = option;
                rows.Add(new Row(
                    IdentityKey: "S:" + candidate.Identity.SkillId + ":" + option.OptionIdentity,
                    DisplayName: candidate.SkillName + " — " + option.Name,
                    Detail: string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} · rating {1} · group {2} · existing specializations: {3}",
                        candidate.SkillCategory,
                        candidate.TotalBaseRating,
                        string.IsNullOrEmpty(candidate.SkillGroup) ? "none" : candidate.SkillGroup,
                        candidate.ExistingSpecializationCount),
                    Cost: "Review to see the exact Karma cost",
                    CanAdvance: true,
                    BlockedReason: null,
                    ApplyAsync: _ => Task.FromResult(false),
                    ReviewAsync: ct => ReviewSpecializationAsync(capturedCandidate, capturedOption, ct)));
            }
        }

        return rows;
    }

    private async Task<Row?> ReviewSpecializationAsync(
        CareerSkillSpecializationCandidate candidate,
        CharacterCareerSkillSpecializationOption option,
        CancellationToken ct)
    {
        var quoteRequest = new CareerSkillSpecializationQuoteRequest(
            _workspaceId,
            _specializationState!.ContentRevision,
            candidate.Identity,
            new CharacterCareerSkillSpecializationSelection(
                option.Name,
                option.Kind,
                option.OptionIdentity));
        CharacterCareerSkillSpecializationQuote? quote =
            await _prepareSpecializationQuote(quoteRequest, ct);
        if (InvalidateIfContextChanged() || quote is not { CanAdd: true }) return null;
        return new Row("S:" + candidate.Identity.SkillId + ":" + option.OptionIdentity,
            candidate.SkillName + " — " + option.Name, "Add specialization",
            $"Cost: {quote.KarmaCost} Karma (available {quote.AvailableKarma})", true, null,
            token => ApplySpecializationQuoteAsync(quote, token));
    }

    private async Task<bool> ApplySpecializationQuoteAsync(CharacterCareerSkillSpecializationQuote quote, CancellationToken ct)
    {
        var request = new CareerSkillSpecializationRequest(
            _workspaceId,
            _specializationState!.ContentRevision,
            quote,
            quote.CharacterRevision,
            quote.SourceRevision,
            quote.RuleDigest,
            quote.LogicalRevision,
            Confirmed: true,
            SpecializationId: _specializationId,
            ExpenseId: _expenseId,
            ExpenseDateLocal: _expenseDate);
        return await _applySpecialization(request, ct);
    }
}
