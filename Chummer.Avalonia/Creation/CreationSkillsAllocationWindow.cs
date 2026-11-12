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
    /// Modal skills-allocation editor for the desktop creation wizard. Presents
    /// every authoritative skill and skill-group row with rating steppers,
    /// re-previews through Core on every change
    /// (<see cref="CharacterCreationSkillsInteractionPresenter.Prepare"/>), and
    /// confirms only after an explicit click; the fresh overview is re-captured
    /// at confirm time so a stale binding cannot be confirmed (Chain-B discipline).
    /// </summary>
    public sealed class CreationSkillsAllocationWindow : Window
    {
        public const string ConfirmButtonAutomationId = "creation-skills-allocate-confirm";
        public const string CancelButtonAutomationId = "creation-skills-cancel";
        public const string SkillRowAutomationIdPrefix = "creation-skills-row-";
        public const string SkillUpAutomationIdPrefix = "creation-skills-up-";
        public const string SkillDownAutomationIdPrefix = "creation-skills-down-";
        public const string GroupUpAutomationIdPrefix = "creation-skills-group-up-";
        public const string GroupDownAutomationIdPrefix = "creation-skills-group-down-";

        private readonly CharacterCreationSkillsInteractionPresenter _presenter;
        private readonly Func<CharacterOverviewState> _overview;
        private readonly List<SkillRowControls> _skillRows = new();
        private readonly List<GroupRowControls> _groupRows = new();
        private readonly Dictionary<string, int> _skillRatings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _groupRatings = new(StringComparer.Ordinal);

        private readonly TextBlock _budgetSummary;
        private readonly TextBlock _previewCaption;
        private readonly TextBlock _statusCaption;
        private readonly Button _confirmButton;
        private CharacterCreationSkillsInteractionPreparedPreview? _prepared;
        private readonly Dictionary<string, CharacterCreationSkillAllocation> _selections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterCreationSkillGroupAllocation> _groups = new(StringComparer.Ordinal);
        private CharacterCreationSkillsAuthority _authority = null!;

        /// <summary>True when Core accepted the final confirm.</summary>
        public bool Confirmed { get; private set; }

        internal CharacterCreationSkillsReceipt? ConfirmationReceipt { get; private set; }

        /// <summary>Last interaction status, mirrored to the wizard status line.</summary>
        public string Status { get; private set; } = "Skills loaded.";

        private sealed class SkillRowControls
        {
            public SkillRowControls(
                string skillId,
                string kind,
                bool isNativeLanguage,
                int maxRating,
                TextBlock value, TextBlock projection)
            {
                SkillId = skillId;
                Kind = kind;
                IsNativeLanguage = isNativeLanguage;
                MaxRating = maxRating;
                Value = value;
                Projection = projection;
            }

            public string SkillId { get; }
            public string Kind { get; }
            public bool IsNativeLanguage { get; }
            public int MaxRating { get; }
            public TextBlock Value { get; }
            public TextBlock Projection { get; }
        }

        private sealed class GroupRowControls
        {
            public GroupRowControls(string groupId, int maxRating, TextBlock value)
            {
                GroupId = groupId;
                MaxRating = maxRating;
                Value = value;
            }

            public string GroupId { get; }
            public int MaxRating { get; }
            public TextBlock Value { get; }
        }

        public CreationSkillsAllocationWindow(
            CharacterCreationSkillsInteractionPresenter presenter,
            CharacterCreationSkillsInteractionState state,
            Func<CharacterOverviewState> overview)
        {
            _presenter = presenter;
            _overview = overview;
            if (!presenter.IsCurrentContext(overview()))
                throw new InvalidOperationException("The character changed while skills were loading.");
            Title = "Allocate Skills";
            Width = 720;
            Height = 640;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var scroll = new ScrollViewer { Padding = new Thickness(16) };
            var panel = new StackPanel { Spacing = 10 };
            scroll.Content = panel;
            Content = scroll;

            panel.Children.Add(Caption(
                "Adjust skills, specializations and native languages below. Review the resulting ratings and budgets before confirming."));
            _budgetSummary = Caption(string.Empty);
            _previewCaption = Caption(string.Empty);
            panel.Children.Add(_budgetSummary);
            panel.Children.Add(_previewCaption);
            panel.Children.Add(new Separator());

            CharacterCreationSkillsState core = state.State;
            CharacterCreationSkillsDraft? draft = core.PendingDraft;
            _authority = core.Authority;
            if (draft is not null)
            {
                foreach (var allocation in draft.Allocations) _selections.Add(allocation.SourceSkillId, allocation);
                foreach (var group in draft.GroupAllocations) _groups.Add(group.GroupId, group);
            }

            var active = new List<CharacterCreationSkillProjection>();
            var knowledge = new List<CharacterCreationSkillProjection>();
            foreach (CharacterCreationSkillProjection skill in core.Skills)
            {
                if (string.Equals(skill.Kind, "knowledge", StringComparison.OrdinalIgnoreCase))
                {
                    knowledge.Add(skill);
                }
                else
                {
                    active.Add(skill);
                }
            }

            int activeMax = Math.Max(1, core.Authority.MaxActiveSkillRatingCreate);
            int knowledgeMax = Math.Max(1, core.Authority.MaxKnowledgeSkillRatingCreate);
            int groupMax = Math.Max(1, core.Authority.MaxSkillGroupRatingCreate);

            panel.Children.Add(SectionHeader("Active skills"));
            if (active.Count == 0)
            {
                panel.Children.Add(Caption("No authoritative active-skill rows are available for this workspace."));
            }
            foreach (CharacterCreationSkillProjection row in active)
            {
                panel.Children.Add(BuildSkillRow(row, activeMax, draft));
            }

            panel.Children.Add(SectionHeader("Knowledge skills"));
            if (knowledge.Count == 0)
            {
                panel.Children.Add(Caption("No authoritative knowledge-skill rows are available for this workspace."));
            }
            foreach (CharacterCreationSkillProjection row in knowledge)
            {
                panel.Children.Add(BuildSkillRow(row, knowledgeMax, draft));
            }

            panel.Children.Add(SectionHeader("Skill groups"));
            if (core.SkillGroups.Count == 0)
            {
                panel.Children.Add(Caption("No authoritative skill-group rows are available for this workspace."));
            }
            foreach (CharacterCreationSkillGroupProjection row in core.SkillGroups)
            {
                panel.Children.Add(BuildGroupRow(row, groupMax, draft));
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
            AutomationProperties.SetAutomationId(_confirmButton, ConfirmButtonAutomationId);
            _confirmButton.Click += OnConfirmClicked;
            panel.Children.Add(_confirmButton);
            var cancelButton = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetAutomationId(cancelButton, CancelButtonAutomationId);
            cancelButton.Click += (_, _) => Close();
            panel.Children.Add(cancelButton);

            SetStatus(string.Empty);
            Reprepare();
        }

        private static TextBlock SectionHeader(string text) => new()
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 6, 0, 0),
        };

        private Control BuildSkillRow(
            CharacterCreationSkillProjection row,
            int maxRating,
            CharacterCreationSkillsDraft? draft)
        {
            if (draft is { } d)
            {
                CharacterCreationSkillAllocation? existing = d.Allocations
                    .FirstOrDefault(a => string.Equals(a.SourceSkillId, row.SourceSkillId, StringComparison.Ordinal));
                if (existing?.Rating is { } seeded)
                {
                    _skillRatings[row.SourceSkillId] = seeded;
                }
            }
            _skillRatings.TryAdd(row.SourceSkillId, 0);

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,Auto,26,Auto") };
            var name = new TextBlock
            {
                Text = row.Name + (row.IsNativeLanguage ? " (native language)" : string.Empty),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(name, 0);

            var down = new Button { Content = "\u2212", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(down, SkillDownAutomationIdPrefix + row.SourceSkillId);
            down.Click += (_, _) => AdjustSkill(row.SourceSkillId, -1, maxRating);
            Grid.SetColumn(down, 1);

            var value = new TextBlock
            {
                Text = _skillRatings[row.SourceSkillId].ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 24,
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(value, 2);

            var up = new Button { Content = "+", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(up, SkillUpAutomationIdPrefix + row.SourceSkillId);
            up.Click += (_, _) => AdjustSkill(row.SourceSkillId, 1, maxRating);
            Grid.SetColumn(up, 3);

            grid.Children.Add(name);
            grid.Children.Add(down);
            grid.Children.Add(value);
            grid.Children.Add(up);

            var wrapper = new StackPanel { Spacing = 2 };
            AutomationProperties.SetAutomationId(wrapper, SkillRowAutomationIdPrefix + row.SourceSkillId);
            wrapper.Children.Add(grid);
            var projection = Caption(string.Empty);
            AutomationProperties.SetAutomationId(projection, "creation-skills-projection-" + row.SourceSkillId);
            wrapper.Children.Add(projection);
            var catalog = _authority.ActiveSkills.Concat(_authority.KnowledgeSkills)
                .FirstOrDefault(x => x.SourceSkillId == row.SourceSkillId);
            var selected = _selections.GetValueOrDefault(row.SourceSkillId);
            if (catalog is not null && catalog.Specializations.Count > 0)
            {
                var choices = catalog.Specializations.Select(x => new SpecializationChoice(x.OptionId, x.Name)).ToList();
                choices.Insert(0, new(null, "No specialization"));
                if (selected?.SpecializationOptionId is { } existingId && !choices.Any(x => x.Id == existingId))
                    choices.Add(new(existingId, row.SpecializationName ?? "Retained specialization"));
                var specializations = new ComboBox { ItemsSource = choices, SelectedItem = choices.FirstOrDefault(x => x.Id == selected?.SpecializationOptionId), IsEnabled = row.IsEnabled };
                AutomationProperties.SetAutomationId(specializations, "creation-skills-specialization-" + row.SourceSkillId);
                specializations.SelectionChanged += (_, _) =>
                {
                    if (specializations.SelectedItem is not SpecializationChoice choice || InvalidateIfContextChanged()) return;
                    var current = Selection(row.SourceSkillId, row.Kind);
                    _selections[row.SourceSkillId] = current with { SpecializationOptionId = choice.Id };
                    Reprepare();
                };
                wrapper.Children.Add(specializations);
            }
            if (catalog?.CanBeNativeLanguage == true || selected?.IsNativeLanguage == true)
            {
                var native = new CheckBox { Content = "Native language", IsChecked = selected?.IsNativeLanguage == true, IsEnabled = row.IsEnabled };
                AutomationProperties.SetAutomationId(native, "creation-skills-native-" + row.SourceSkillId);
                native.IsCheckedChanged += (_, _) =>
                {
                    if (InvalidateIfContextChanged()) return;
                    var current = Selection(row.SourceSkillId, row.Kind);
                    _selections[row.SourceSkillId] = current with { IsNativeLanguage = native.IsChecked == true, Rating = native.IsChecked == true ? null : _skillRatings[row.SourceSkillId] };
                    Reprepare();
                };
                wrapper.Children.Add(native);
            }
            if (!row.IsEnabled && row.Blockers.Count > 0)
                wrapper.Children.Add(Caption("Disabled: " + string.Join(", ", row.Blockers)));
            _skillRows.Add(new SkillRowControls(row.SourceSkillId, row.Kind, row.IsNativeLanguage, maxRating, value, projection));
            return wrapper;
        }

        private Control BuildGroupRow(
            CharacterCreationSkillGroupProjection row,
            int maxRating,
            CharacterCreationSkillsDraft? draft)
        {
            if (draft is { } d)
            {
                CharacterCreationSkillGroupAllocation? existing = d.GroupAllocations
                    .FirstOrDefault(a => string.Equals(a.GroupId, row.GroupId, StringComparison.Ordinal));
                if (existing is not null)
                {
                    _groupRatings[row.GroupId] = existing.Rating;
                }
            }
            _groupRatings.TryAdd(row.GroupId, 0);

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("260,Auto,26,Auto") };
            var name = new TextBlock
            {
                Text = row.Name,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(name, 0);

            var down = new Button { Content = "\u2212", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(down, GroupDownAutomationIdPrefix + row.GroupId);
            down.Click += (_, _) => AdjustGroup(row.GroupId, -1, maxRating);
            Grid.SetColumn(down, 1);

            var value = new TextBlock
            {
                Text = _groupRatings[row.GroupId].ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 24,
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(value, 2);

            var up = new Button { Content = "+", Width = 32, IsEnabled = row.IsEnabled };
            AutomationProperties.SetAutomationId(up, GroupUpAutomationIdPrefix + row.GroupId);
            up.Click += (_, _) => AdjustGroup(row.GroupId, 1, maxRating);
            Grid.SetColumn(up, 3);

            grid.Children.Add(name);
            grid.Children.Add(down);
            grid.Children.Add(value);
            grid.Children.Add(up);

            var wrapper = new StackPanel { Spacing = 2 };
            wrapper.Children.Add(grid);
            wrapper.Children.Add(Caption(string.Format(
                CultureInfo.InvariantCulture,
                "current {0} · point cost {1} · members {2}",
                row.Rating,
                row.PointCost,
                row.MemberSkillSourceIds.Count)));
            if (!row.IsEnabled && row.Blockers.Count > 0)
                wrapper.Children.Add(Caption("Disabled: " + string.Join(", ", row.Blockers)));
            _groupRows.Add(new GroupRowControls(row.GroupId, maxRating, value));
            return wrapper;
        }

        private void AdjustSkill(string skillId, int delta, int maxRating)
        {
            if (InvalidateIfContextChanged()) return;
            int next = Math.Clamp(_skillRatings[skillId] + delta, 0, maxRating);
            if (next == _skillRatings[skillId])
                return;
            _skillRatings[skillId] = next;
            var row = _skillRows.Single(x => x.SkillId == skillId);
            var current = Selection(skillId, row.Kind);
            if (next == 0 && !current.IsNativeLanguage && current.SpecializationOptionId is null)
                _selections.Remove(skillId);
            else
                _selections[skillId] = current with { Rating = current.IsNativeLanguage ? null : next };
            Reprepare();
        }

        private void AdjustGroup(string groupId, int delta, int maxRating)
        {
            if (InvalidateIfContextChanged()) return;
            int next = Math.Clamp(_groupRatings[groupId] + delta, 0, maxRating);
            if (next == _groupRatings[groupId])
                return;
            _groupRatings[groupId] = next;
            if (next == 0) _groups.Remove(groupId); else _groups[groupId] = new(groupId, next);
            Reprepare();
        }

        /// <summary>Previews the current allocation through Core and refreshes the summary.</summary>
        private void Reprepare()
        {
            if (InvalidateIfContextChanged()) return;
            foreach (SkillRowControls row in _skillRows)
            {
                row.Value.Text = _skillRatings[row.SkillId].ToString(CultureInfo.InvariantCulture);
            }
            foreach (GroupRowControls row in _groupRows)
            {
                row.Value.Text = _groupRatings[row.GroupId].ToString(CultureInfo.InvariantCulture);
            }

            CharacterOverviewState overview = _overview();
            CharacterCreationSkillsInteractionPrepareResult prepared =
                _presenter.Prepare(overview, BuildSkillAllocations(), BuildGroupAllocations());
            if (prepared is not { Outcome: "ok", Preview: { } wrapper })
            {
                _prepared = null;
                _confirmButton.IsEnabled = false;
                _budgetSummary.Text = string.Empty;
                _previewCaption.Text = string.Empty;
                SetStatus("Preview failed: " + (prepared.Error ?? prepared.Outcome));
                return;
            }

            CharacterCreationSkillsPreview preview = wrapper.Preview;
            _prepared = wrapper;
            foreach (var row in _skillRows)
            {
                var result = preview.Skills.FirstOrDefault(x => x.SourceSkillId == row.SkillId);
                row.Projection.Text = result is null ? "No current result available" :
                    $"Result {(result.IsNativeLanguage ? "native" : result.Rating?.ToString(CultureInfo.InvariantCulture) ?? "–")} · point cost {result.PointCost} · specialization {result.SpecializationName ?? "none"}";
            }
            _budgetSummary.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Active points {0}/{1} (left {2}) · group points {3}/{4} (left {5}) · knowledge points {6}/{7} (left {8})",
                preview.ActiveSkillPointBudget.Used, preview.ActiveSkillPointBudget.Total, preview.ActiveSkillPointBudget.Remaining,
                preview.SkillGroupPointBudget.Used, preview.SkillGroupPointBudget.Total, preview.SkillGroupPointBudget.Remaining,
                preview.KnowledgeSkillPointBudget.Used, preview.KnowledgeSkillPointBudget.Total, preview.KnowledgeSkillPointBudget.Remaining);
            _previewCaption.Text = (preview.KnowledgePointOverflowToActive > 0
                ? "knowledge overflow to active: " + preview.KnowledgePointOverflowToActive.ToString(CultureInfo.InvariantCulture) + " · "
                : string.Empty)
                + (preview.Blockers.Count > 0
                    ? "Preview blockers: " + string.Join(", ", preview.Blockers)
                    : "Preview " + preview.PreviewDigest[..Math.Min(12, preview.PreviewDigest.Length)]
                        + (preview.RequiresExplicitConfirmation ? " · explicit confirmation required" : string.Empty));
            _confirmButton.IsEnabled = preview.CanConfirm;
            SetStatus(preview.CanConfirm
                ? "Preview accepted. Confirm to apply this allocation."
                : "Blocked: " + string.Join(", ", preview.Blockers));
        }

        private sealed record SpecializationChoice(string? Id, string Label)
        {
            public override string ToString() => Label;
        }

        private CharacterCreationSkillAllocation Selection(string id, string kind)
            => _selections.TryGetValue(id, out var current) ? current : new(id, kind, _skillRatings[id], null, false);

        // Keep the complete saved intent, including native languages with a null/zero
        // rating and entries not shown by this catalog. Only explicit edits change it.
        private List<CharacterCreationSkillAllocation> BuildSkillAllocations()
            => _selections.Values.ToList();

        private List<CharacterCreationSkillGroupAllocation> BuildGroupAllocations()
            => _groups.Values.ToList();

        public bool InvalidateIfContextChanged()
        {
            if (_presenter.IsCurrentContext(_overview())) return false;
            _prepared = null;
            _confirmButton.IsEnabled = false;
            Status = "The character or account changed. Reopen Skills for the current character.";
            Content = null;
            Close();
            return true;
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (InvalidateIfContextChanged()) return;
            if (_prepared is not { } prepared || !prepared.Preview.CanConfirm)
                return; // wrapper.Preview.CanConfirm — unchanged semantics.

            CharacterCreationSkillsInteractionState interactionState = prepared.State;
            var confirmation = new CharacterCreationSkillsInteractionConfirmation(
                interactionState,
                BuildSkillAllocations(),
                BuildGroupAllocations(),
                prepared.Preview.PreviewDigest,
                prepared.IdempotencyKey,
                ExplicitlyConfirmed: true);
            CharacterOverviewState overview = _overview();
            CharacterCreationSkillsInteractionConfirmResult result = _presenter.Confirm(overview, confirmation);
            if (result is { Outcome: "ok", Receipt: { } receipt })
            {
                ConfirmationReceipt = receipt;
                Confirmed = true;
                Status = "Skills confirmed (revision " + receipt.ContentRevision.ToString(CultureInfo.InvariantCulture) + ").";
                Close();
                return;
            }

            _confirmButton.IsEnabled = false;
            SetStatus("Confirm failed: " + (result.Error ?? result.Outcome));
        }

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
