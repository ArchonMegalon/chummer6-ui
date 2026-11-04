using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia.Creation
{
    /// <summary>
    /// Modal attributes-allocation editor for the desktop creation wizard. Presents
    /// every authoritative attribute row with priority-point and karma-level
    /// steppers, re-previews through Core on every change
    /// (<see cref="CharacterCreationAttributesInteractionPresenter.Prepare"/>), and
    /// confirms only after an explicit click bound to the original character,
    /// account and revision. Core owns the resulting ratings and costs.
    /// </summary>
    public sealed class CreationAttributesAllocationWindow : Window
    {
        public const string AllocateButtonAutomationId = "creation-attributes-allocate-confirm";
        public const string CancelButtonAutomationId = "creation-attributes-cancel";
        public const string RowAutomationIdPrefix = "creation-attributes-row-";
        public const string PriorityUpAutomationIdPrefix = "creation-attributes-priority-up-";
        public const string PriorityDownAutomationIdPrefix = "creation-attributes-priority-down-";
        public const string KarmaUpAutomationIdPrefix = "creation-attributes-karma-up-";
        public const string KarmaDownAutomationIdPrefix = "creation-attributes-karma-down-";
        public const string ProjectionAutomationIdPrefix = "creation-attributes-projection-";

        private readonly CharacterCreationAttributesInteractionPresenter _presenter;
        private readonly Func<CharacterOverviewState> _overview;
        private readonly List<RowControls> _rows = new();
        private readonly Dictionary<string, int> _priority = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _karma = new(StringComparer.Ordinal);

        private readonly TextBlock _budgetSummary;
        private readonly TextBlock _previewCaption;
        private readonly TextBlock _statusCaption;
        private readonly Button _confirmButton;
        private CharacterCreationAttributesPreparedPreview? _prepared;

        /// <summary>True when Core accepted the final confirm.</summary>
        public bool Confirmed { get; private set; }

        /// <summary>Last interaction status, mirrored to the wizard status line.</summary>
        public string Status { get; private set; } = "Attributes loaded.";

        /// <summary>The refreshed interaction state after a confirmed apply.</summary>
        public CharacterCreationAttributesInteractionState? State { get; private set; }

        private sealed class RowControls
        {
            public RowControls(string attributeId, TextBlock priorityValue, TextBlock karmaValue, TextBlock projection)
            {
                AttributeId = attributeId;
                PriorityValue = priorityValue;
                KarmaValue = karmaValue;
                Projection = projection;
            }

            public string AttributeId { get; }
            public TextBlock PriorityValue { get; }
            public TextBlock KarmaValue { get; }
            public TextBlock Projection { get; }
        }

        public CreationAttributesAllocationWindow(
            CharacterCreationAttributesInteractionPresenter presenter,
            CharacterCreationAttributesInteractionState state,
            Func<CharacterOverviewState> overview)
        {
            _presenter = presenter;
            _overview = overview;
            if (!presenter.IsCurrentContext(overview()))
                throw new InvalidOperationException("The character or account changed. Reload before opening Attributes.");
            Title = "Allocate Attributes";
            Width = 660;
            Height = 560;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var scroll = new ScrollViewer { Padding = new Thickness(16) };
            var panel = new StackPanel { Spacing = 10 };
            scroll.Content = panel;
            Content = scroll;

            panel.Children.Add(Caption(
                "Priority points first, karma levels second. Every change previews through Core; the button below applies the shown allocation explicitly."));
            _budgetSummary = Caption(string.Empty);
            _previewCaption = Caption(string.Empty);
            panel.Children.Add(_budgetSummary);
            panel.Children.Add(_previewCaption);
            panel.Children.Add(new Separator());

            if (state.Attributes.Count == 0)
            {
                panel.Children.Add(Caption("No authoritative attribute rows are available for this workspace."));
            }

            foreach (CharacterCreationAttributeProjection row in state.Attributes)
            {
                if (state.PendingDraft is { } draft)
                {
                    CharacterCreationAttributeAllocation? existing = draft.Allocations
                        .FirstOrDefault(a => string.Equals(a.AttributeId, row.AttributeId, StringComparison.Ordinal));
                    if (existing is not null)
                    {
                        _priority[row.AttributeId] = existing.PriorityPoints;
                        _karma[row.AttributeId] = existing.KarmaLevels;
                    }
                }

                _priority.TryAdd(row.AttributeId, 0);
                _karma.TryAdd(row.AttributeId, 0);
                panel.Children.Add(BuildRow(row, state));
            }

            panel.Children.Add(new Separator());
            _statusCaption = Caption(string.Empty);
            panel.Children.Add(_statusCaption);
            _confirmButton = new Button
            {
                Content = "Confirm allocation",
                IsEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            AutomationProperties.SetAutomationId(_confirmButton, AllocateButtonAutomationId);
            _confirmButton.Click += OnConfirmClicked;
            panel.Children.Add(_confirmButton);
            var cancelButton = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetAutomationId(cancelButton, CancelButtonAutomationId);
            cancelButton.Click += (_, _) => Close();
            panel.Children.Add(cancelButton);

            SetStatus(string.Empty);
            Reprepare();
        }

        private Control BuildRow(CharacterCreationAttributeProjection row, CharacterCreationAttributesInteractionState state)
        {
            _ = state;
            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("200,Auto,26,Auto,Auto,26,Auto") };
            var name = new TextBlock
            {
                Text = row.AttributeId,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(name, 0);

            var priorityDown = new Button { Content = "\u2212", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(priorityDown, PriorityDownAutomationIdPrefix + row.AttributeId);
            priorityDown.Click += (_, _) => Adjust(row, -1, 0);
            Grid.SetColumn(priorityDown, 1);

            var priorityValue = new TextBlock
            {
                Text = _priority[row.AttributeId].ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 24,
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(priorityValue, 2);

            var priorityUp = new Button { Content = "+", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(priorityUp, PriorityUpAutomationIdPrefix + row.AttributeId);
            priorityUp.Click += (_, _) => Adjust(row, 1, 0);
            Grid.SetColumn(priorityUp, 3);

            var karmaDown = new Button { Content = "\u2212", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(karmaDown, KarmaDownAutomationIdPrefix + row.AttributeId);
            karmaDown.Click += (_, _) => Adjust(row, 0, -1);
            Grid.SetColumn(karmaDown, 4);

            var karmaValue = new TextBlock
            {
                Text = _karma[row.AttributeId].ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 24,
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(karmaValue, 5);

            var karmaUp = new Button { Content = "+", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(karmaUp, KarmaUpAutomationIdPrefix + row.AttributeId);
            karmaUp.Click += (_, _) => Adjust(row, 0, 1);
            Grid.SetColumn(karmaUp, 6);

            grid.Children.Add(name);
            grid.Children.Add(priorityDown);
            grid.Children.Add(priorityValue);
            grid.Children.Add(priorityUp);
            grid.Children.Add(karmaDown);
            grid.Children.Add(karmaValue);
            grid.Children.Add(karmaUp);

            var wrapper = new StackPanel { Spacing = 2 };
            wrapper.Children.Add(grid);
            var projection = Caption(DescribeProjection(row));
            AutomationProperties.SetAutomationId(projection, ProjectionAutomationIdPrefix + row.AttributeId);
            wrapper.Children.Add(projection);
            if (!row.IsEnabled && row.DisableReasons.Count > 0)
                wrapper.Children.Add(Caption("Disabled: " + string.Join(", ", row.DisableReasons)));
            _rows.Add(new RowControls(row.AttributeId, priorityValue, karmaValue, projection));
            return wrapper;
        }

        private void Adjust(CharacterCreationAttributeProjection row, int priority, int karma)
        {
            int maxLevels = Math.Max(0, row.Maximum - row.Minimum);
            int nextPriority = Math.Clamp(_priority[row.AttributeId] + priority, 0, maxLevels);
            int nextKarma = Math.Clamp(_karma[row.AttributeId] + karma, 0, maxLevels);
            if (nextPriority == _priority[row.AttributeId] && nextKarma == _karma[row.AttributeId])
                return;
            _priority[row.AttributeId] = nextPriority;
            _karma[row.AttributeId] = nextKarma;
            Reprepare();
        }

        /// <summary>Previews the current allocation through Core and refreshes the summary.</summary>
        private void Reprepare()
        {
            if (!RequireCurrentContext())
                return;
            foreach (RowControls row in _rows)
            {
                row.PriorityValue.Text = _priority[row.AttributeId].ToString(CultureInfo.InvariantCulture);
                row.KarmaValue.Text = _karma[row.AttributeId].ToString(CultureInfo.InvariantCulture);
            }

            CharacterOverviewState overview = _overview();
            List<CharacterCreationAttributeAllocation> allocations = BuildAllocations();
            CharacterCreationAttributesInteractionPrepareResult prepared = _presenter.Prepare(overview, allocations);
            if (prepared is not { Outcome: "ok", PreparedPreview: { } preview })
            {
                _prepared = null;
                _confirmButton.IsEnabled = false;
                _budgetSummary.Text = string.Empty;
                _previewCaption.Text = string.Empty;
                SetStatus("Preview failed: " + (prepared.Error ?? prepared.Outcome));
                return;
            }

            _prepared = preview;
            foreach (RowControls row in _rows)
            {
                CharacterCreationAttributeProjection? projected = preview.Attributes
                    .SingleOrDefault(value => value.AttributeId == row.AttributeId);
                row.Projection.Text = projected is null ? "No current preview is available." : DescribeProjection(projected);
            }
            _budgetSummary.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Priority points {0}/{1} (left {2}) · special points {3}/{4} (left {5}) · karma {6}/{7} (left {8})",
                preview.NormalPointBudget.Used, preview.NormalPointBudget.Total, preview.NormalPointBudget.Remaining,
                preview.SpecialPointBudget.Used, preview.SpecialPointBudget.Total, preview.SpecialPointBudget.Remaining,
                preview.CreationKarmaBudget.Used, preview.CreationKarmaBudget.Total, preview.CreationKarmaBudget.Remaining);
            _previewCaption.Text = preview.Blockers.Count > 0
                ? "Preview blockers: " + string.Join(", ", preview.Blockers)
                : "Preview " + preview.PreviewDigest[..Math.Min(12, preview.PreviewDigest.Length)]
                    + (preview.RequiresExplicitConfirmation ? " · explicit confirmation required" : string.Empty);
            _confirmButton.IsEnabled = preview.CanConfirm;
            SetStatus(preview.CanConfirm
                ? "Preview accepted. Confirm to apply this allocation."
                : "Blocked: " + string.Join(", ", preview.Blockers));
        }

        private List<CharacterCreationAttributeAllocation> BuildAllocations()
        {
            return _priority
                .Select(kv => new CharacterCreationAttributeAllocation(
                    kv.Key, kv.Value, _karma.GetValueOrDefault(kv.Key)))
                .ToList();
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (!RequireCurrentContext() || _prepared is not { } prepared || !prepared.CanConfirm)
                return;

            var confirmation = new CharacterCreationAttributesConfirmation(
                BuildAllocations(), prepared.PreviewDigest, ExplicitlyConfirmed: true);
            CharacterOverviewState overview = _overview();
            CharacterCreationAttributesInteractionConfirmResult result = _presenter.Confirm(overview, confirmation);
            if (result is { Outcome: "ok", State: { } state })
            {
                Confirmed = true;
                State = state;
                Status = "Attributes confirmed.";
                Close();
                return;
            }

            SetStatus("Confirm failed: " + (result.Error ?? result.Outcome));
            _prepared = null;
            _confirmButton.IsEnabled = false;
        }

        private bool RequireCurrentContext()
        {
            if (_presenter.IsCurrentContext(_overview()))
                return true;
            _prepared = null;
            _confirmButton.IsEnabled = false;
            Status = "The character or account changed. Reload before editing Attributes.";
            Content = Caption(Status);
            Close();
            return false;
        }

        public void InvalidateIfContextChanged() => RequireCurrentContext();

        private static string DescribeProjection(CharacterCreationAttributeProjection row)
            => string.Format(CultureInfo.InvariantCulture,
                "Result {0} · range {1}-{2} · priority cost {3} · karma cost {4}",
                row.Current, row.Minimum, row.Maximum, row.PriorityPointCost, row.KarmaCost);

        private void SetStatus(string message)
        {
            Status = string.IsNullOrWhiteSpace(message) ? Status : message;
            _statusCaption.Text = Status;
        }

        private static TextBlock Caption(string text) => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.85,
        };
    }
}
