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
    /// Modal Magic/Resonance editor for the desktop creation wizard. Presents the
    /// authoritative talent context, tradition/stream catalogs, adept powers, spells
    /// and complex forms with real selection inputs, re-previews through Core on
    /// every change (the Presentation workflow's CreateDraft/Review path), and
    /// confirms only after an explicit click; the fresh overview is re-captured at
    /// confirm time so a stale binding cannot be confirmed (Chain-B discipline).
    /// </summary>
    public sealed class CreationMagicResonanceWindow : Window
    {
        public const string ConfirmButtonAutomationId = "creation-magic-allocate-confirm";
        public const string CancelButtonAutomationId = "creation-magic-cancel";
        public const string TraditionComboAutomationId = "creation-magic-tradition";
        public const string StreamComboAutomationId = "creation-magic-stream";
        public const string AdeptPowerRowAutomationIdPrefix = "creation-magic-power-row-";
        public const string AdeptPowerUpAutomationIdPrefix = "creation-magic-power-up-";
        public const string AdeptPowerDownAutomationIdPrefix = "creation-magic-power-down-";
        public const string SpellCheckAutomationIdPrefix = "creation-magic-spell-";
        public const string ComplexFormCheckAutomationIdPrefix = "creation-magic-form-";
        public const string MysticPointsSpinnerAutomationId = "creation-magic-mystic-points";

        private readonly CharacterCreationMagicResonanceInteractionPresenter _presenter;
        private readonly Func<CharacterOverviewState> _overview;

        private readonly TextBlock _budgetSummary;
        private readonly TextBlock _previewCaption;
        private readonly TextBlock _statusCaption;
        private readonly Button _confirmButton;

        private ComboBox? _traditionCombo;
        private ComboBox? _streamCombo;
        private readonly Dictionary<string, TextBlock> _powerValues = new(StringComparer.Ordinal);
        private readonly HashSet<string> _spellIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _formIds = new(StringComparer.Ordinal);
        private NumericUpDown? _mysticPoints;
        private int _mysticPointsValue;

        private CharacterCreationMagicResonanceEditorState _editor = null!;
        private CharacterCreationMagicResonanceInteractionPreparedPreview? _prepared;

        /// <summary>True when Core accepted the final confirm.</summary>
        public bool Confirmed { get; private set; }

        /// <summary>Last interaction status, mirrored to the wizard status line.</summary>
        public string Status { get; private set; } = "Magic/Resonance loaded.";

        public CreationMagicResonanceWindow(
            CharacterCreationMagicResonanceInteractionPresenter presenter,
            CharacterCreationMagicResonanceInteractionState state,
            Func<CharacterOverviewState> overview)
        {
            _presenter = presenter;
            _overview = overview;
            if (!presenter.IsCurrentContext(overview()))
                throw new InvalidOperationException("The character changed while Magic/Resonance was loading.");

            Title = "Magic and Resonance";
            Width = 780;
            Height = 720;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var scroll = new ScrollViewer { Padding = new Thickness(16) };
            var panel = new StackPanel { Spacing = 10 };
            scroll.Content = panel;
            Content = scroll;

            CharacterCreationMagicResonanceState core = state.State;
            _editor = CharacterCreationMagicResonanceWorkflow.Project(core);

            panel.Children.Add(Caption(
                "Choose a tradition or stream, then spend your spell, complex-form and adept-power budgets. "
                + "Core prices every change; confirm applies the whole selection."));
            _budgetSummary = Caption(string.Empty);
            _previewCaption = Caption(string.Empty);
            panel.Children.Add(_budgetSummary);
            panel.Children.Add(_previewCaption);
            panel.Children.Add(new Separator());

            CharacterCreationMagicResonanceSelections saved = _editor.Selections;

            // Seed the interactive collections BEFORE the rows are built so a reopen
            // renders the character's actual state: spell/complex-form checkboxes read
            // the selection sets at construction and adept-power rows TryAdd against
            // these levels.
            foreach (CharacterCreationAdeptPowerAllocation allocation in saved.AdeptPowers)
                _powerLevels[allocation.Identity.SourceId] = allocation.Levels;
            foreach (CharacterCreationMagicResonanceOptionIdentity spell in saved.Spells)
                _spellIds.Add(spell.SourceId);
            foreach (CharacterCreationMagicResonanceOptionIdentity form in saved.ComplexForms)
                _formIds.Add(form.SourceId);

            panel.Children.Add(SectionHeader("Talent"));
            panel.Children.Add(Caption(string.Format(
                CultureInfo.InvariantCulture,
                "{0} · Magic {1} · Resonance {2} · priority depth {3} · spell budget {4} · complex-form budget {5} · adept-power points {6}",
                _editor.Talent.Name,
                _editor.Talent.Magic,
                _editor.Talent.Resonance,
                _editor.Talent.Depth,
                _editor.Talent.SpellBudget,
                _editor.Talent.ComplexFormBudget,
                _editor.Talent.AdeptPowerPointBudget)));

            panel.Children.Add(SectionHeader(_editor.Talent.RequiresStream ? "Tradition and stream" : "Tradition"));
            AddOptionCombo(
                panel,
                TraditionComboAutomationId,
                _editor.Traditions,
                saved.Tradition,
                combo => _traditionCombo = combo,
                _editor.Talent.RequiresTradition ? null : "No tradition required for this talent.");
            if (_editor.Talent.RequiresStream)
            {
                AddOptionCombo(
                    panel,
                    StreamComboAutomationId,
                    _editor.Streams,
                    saved.Stream,
                    combo => _streamCombo = combo,
                    null);
            }

            if (core.MysticAdeptPowerPoints is { } mystic)
            {
                panel.Children.Add(SectionHeader("Mystic Adept power points"));
                panel.Children.Add(Caption(string.Format(
                    CultureInfo.InvariantCulture,
                    "Cap {0} PP · priority slots pay first, then remaining PP cost {1} karma each.",
                    mystic.MaximumPowerPoints,
                    mystic.Policy.KarmaPerPowerPoint)));
                // The current allocation is the authoritative reopen seed.
                _mysticPointsValue = Math.Max(0, mystic.PowerPoints);
                _mysticPoints = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = mystic.MaximumPowerPoints,
                    Increment = 1,
                    Value = _mysticPointsValue,
                    Width = 140,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                DesktopShellTheme.ApplyShellNumericUpDownTheme(_mysticPoints);
                AutomationProperties.SetAutomationId(_mysticPoints, MysticPointsSpinnerAutomationId);
                _mysticPoints.ValueChanged += (_, _) => { if (!InvalidateIfContextChanged()) Reprepare(); };
                panel.Children.Add(_mysticPoints);
            }

            if (_editor.Talent.AllowsAdeptPowers)
            {
                panel.Children.Add(SectionHeader("Adept powers"));
                if (_editor.AdeptPowers.Count == 0)
                    panel.Children.Add(Caption("No authoritative adept-power rows are available for this workspace."));
                foreach (CharacterCreationMagicResonanceOptionProjection power in _editor.AdeptPowers)
                    panel.Children.Add(BuildPowerRow(power));
            }

            if (_editor.Talent.AllowsSpells)
            {
                panel.Children.Add(SectionHeader("Spells"));
                if (_editor.Spells.Count == 0)
                    panel.Children.Add(Caption("No authoritative spell rows are available for this workspace."));
                foreach (CharacterCreationMagicResonanceOptionProjection spell in _editor.Spells)
                    panel.Children.Add(BuildCheckRow(spell, _spellChecks, SpellCheckAutomationIdPrefix, _spellIds));
            }

            if (_editor.Talent.AllowsComplexForms)
            {
                panel.Children.Add(SectionHeader("Complex forms"));
                if (_editor.ComplexForms.Count == 0)
                    panel.Children.Add(Caption("No authoritative complex-form rows are available for this workspace."));
                foreach (CharacterCreationMagicResonanceOptionProjection form in _editor.ComplexForms)
                    panel.Children.Add(BuildCheckRow(form, _formChecks, ComplexFormCheckAutomationIdPrefix, _formIds));
            }

            panel.Children.Add(new Separator());
            _statusCaption = Caption(string.Empty);
            panel.Children.Add(_statusCaption);
            _confirmButton = new Button
            {
                Content = "Confirm Magic/Resonance",
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

        private readonly Dictionary<string, int> _powerLevels = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (CheckBox Check, TextBlock Cost)> _spellChecks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (CheckBox Check, TextBlock Cost)> _formChecks = new(StringComparer.Ordinal);

        private void AddOptionCombo(
            StackPanel panel,
            string automationId,
            IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> options,
            CharacterCreationMagicResonanceOptionIdentity? saved,
            Action<ComboBox?> assign,
            string? caption)
        {
            var choices = new List<OptionChoice>();
            if (saved is { } savedIdentity
                && !options.Any(option => option.Identity == savedIdentity))
                saved = null;
            foreach (CharacterCreationMagicResonanceOptionProjection option in options)
            {
                choices.Add(new OptionChoice(
                    option.Identity,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} · {1} · cost {2} · {3} p. {4}",
                        option.Name,
                        option.Category,
                        option.PointCost,
                        option.SourceBook,
                        option.Page),
                    option.IsEnabled,
                    option.Blockers));
            }
            choices.Insert(0, new OptionChoice(null, "None", true, Array.Empty<string>()));
            var combo = new ComboBox { ItemsSource = choices, HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectedItem = choices.FirstOrDefault(choice => choice.Identity == saved) ?? choices[0];
            AutomationProperties.SetAutomationId(combo, automationId);
            combo.SelectionChanged += (_, _) =>
            {
                if (InvalidateIfContextChanged())
                    return;
                Reprepare();
            };
            DesktopShellTheme.ApplyShellComboBoxTheme(combo);
            assign(combo);
            panel.Children.Add(combo);
            if (caption is not null && options.Count == 0)
                panel.Children.Add(Caption(caption));
        }

        private Control BuildPowerRow(CharacterCreationMagicResonanceOptionProjection power)
        {
            _powerLevels.TryAdd(power.Identity.SourceId, 0);

            var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("300,Auto,30,Auto") };
            var name = new TextBlock
            {
                Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · cost {1} · max level {2}",
                    power.Name,
                    power.PointCost,
                    power.MaximumLevels),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(name, 0);

            var down = new Button { Content = "\u2212", Width = 32, IsEnabled = power.IsEnabled };
            AutomationProperties.SetAutomationId(down, AdeptPowerDownAutomationIdPrefix + power.Identity.SourceId);
            down.Click += (_, _) => AdjustPower(power.Identity.SourceId, -1);
            Grid.SetColumn(down, 1);

            var value = new TextBlock
            {
                Text = _powerLevels[power.Identity.SourceId].ToString(CultureInfo.InvariantCulture),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 30,
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(value, 2);

            var up = new Button { Content = "+", Width = 32, IsEnabled = power.IsEnabled };
            AutomationProperties.SetAutomationId(up, AdeptPowerUpAutomationIdPrefix + power.Identity.SourceId);
            up.Click += (_, _) => AdjustPower(power.Identity.SourceId, 1);
            Grid.SetColumn(up, 3);

            grid.Children.Add(name);
            grid.Children.Add(down);
            grid.Children.Add(value);
            grid.Children.Add(up);

            var wrapper = new StackPanel { Spacing = 2 };
            AutomationProperties.SetAutomationId(wrapper, AdeptPowerRowAutomationIdPrefix + power.Identity.SourceId);
            wrapper.Children.Add(grid);
            if (!power.IsEnabled && power.Blockers.Count > 0)
                wrapper.Children.Add(Caption("Disabled: " + string.Join(", ", power.Blockers)));
            _powerValues[power.Identity.SourceId] = value;
            return wrapper;
        }

        private Control BuildCheckRow(
            CharacterCreationMagicResonanceOptionProjection option,
            Dictionary<string, (CheckBox Check, TextBlock Cost)> sink,
            string prefix,
            HashSet<string> selected)
        {
            var check = new CheckBox
            {
                Content = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} · cost {1} · {2} p. {3}",
                    option.Name,
                    option.PointCost,
                    option.SourceBook,
                    option.Page),
                IsChecked = selected.Contains(option.Identity.SourceId),
                IsEnabled = option.IsEnabled,
            };
            AutomationProperties.SetAutomationId(check, prefix + option.Identity.SourceId);
            check.IsCheckedChanged += (_, _) =>
            {
                if (_reconciling)
                    return;
                if (InvalidateIfContextChanged())
                    return;
                if (check.IsChecked == true)
                    selected.Add(option.Identity.SourceId);
                else
                    selected.Remove(option.Identity.SourceId);
                Reprepare();
            };
            var wrapper = new StackPanel { Spacing = 2 };
            wrapper.Children.Add(check);
            if (!option.IsEnabled && option.Blockers.Count > 0)
                wrapper.Children.Add(Caption("Disabled: " + string.Join(", ", option.Blockers)));
            sink[option.Identity.SourceId] = (check, Caption(option.DrainExpression));
            return wrapper;
        }

        private void AdjustPower(string sourceId, int delta)
        {
            if (InvalidateIfContextChanged())
                return;
            CharacterCreationMagicResonanceOptionProjection power = _editor.AdeptPowers
                .Single(option => string.Equals(option.Identity.SourceId, sourceId, StringComparison.Ordinal));
            bool had = _powerLevels.TryGetValue(sourceId, out int current);
            int next = Math.Clamp((had ? current : 0) + delta, 0, Math.Max(0, power.MaximumLevels));
            if (next == (had ? current : 0))
                return;
            _powerLevels[sourceId] = next;
            if (_powerValues.TryGetValue(sourceId, out TextBlock? value))
                value.Text = next.ToString(CultureInfo.InvariantCulture);
            Reprepare();
        }

        private bool _reconciling;

        /// <summary>
        /// Synchronizes spell/complex-form checkbox states to the current selection sets
        /// under event suppression so re-prepare never re-enters the change handlers.
        /// </summary>
        private void ReconcileCheckStates()
        {
            if (_reconciling)
                return;
            _reconciling = true;
            try
            {
                foreach (KeyValuePair<string, (CheckBox Check, TextBlock Cost)> entry in _spellChecks)
                    entry.Value.Check.IsChecked = _spellIds.Contains(entry.Key);
                foreach (KeyValuePair<string, (CheckBox Check, TextBlock Cost)> entry in _formChecks)
                    entry.Value.Check.IsChecked = _formIds.Contains(entry.Key);
            }
            finally
            {
                _reconciling = false;
            }
        }

        /// <summary>Previews the current selection through Core and refreshes the summary.</summary>
        private void Reprepare()
        {
            if (InvalidateIfContextChanged())
                return;
            ReconcileCheckStates();
            foreach (KeyValuePair<string, TextBlock> entry in _powerValues)
                entry.Value.Text = _powerLevels[entry.Key].ToString(CultureInfo.InvariantCulture);

            CharacterOverviewState overview = _overview();
            CharacterCreationMagicResonanceInteractionPrepareResult prepared =
                _presenter.Prepare(overview, BuildSelections(), CurrentMysticPoints());
            if (prepared is not { Outcome: "ok", Preview: { } wrapper })
            {
                _prepared = null;
                _confirmButton.IsEnabled = false;
                _budgetSummary.Text = string.Empty;
                _previewCaption.Text = string.Empty;
                SetStatus("Preview failed: " + (prepared.Error ?? prepared.Outcome));
                return;
            }

            CharacterCreationMagicResonancePreview preview = wrapper.Preview;
            _prepared = wrapper;
            _budgetSummary.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Tradition {0}/{1} left {2} · stream {3}/{4} left {5} · power points {6}/{7} left {8} · spells {9}/{10} left {11} · complex forms {12}/{13} left {14}",
                preview.TraditionBudget.Used, preview.TraditionBudget.Total, preview.TraditionBudget.Remaining,
                preview.StreamBudget.Used, preview.StreamBudget.Total, preview.StreamBudget.Remaining,
                preview.AdeptPowerPointBudget.Used, preview.AdeptPowerPointBudget.Total, preview.AdeptPowerPointBudget.Remaining,
                preview.SpellBudget.Used, preview.SpellBudget.Total, preview.SpellBudget.Remaining,
                preview.ComplexFormBudget.Used, preview.ComplexFormBudget.Total, preview.ComplexFormBudget.Remaining);
            _previewCaption.Text = (preview.Blockers.Count > 0
                ? "Preview blockers: " + string.Join(", ", preview.Blockers)
                : "Preview " + preview.PreviewDigest[..Math.Min(12, preview.PreviewDigest.Length)]
                    + (preview.RequiresExplicitConfirmation ? " · explicit confirmation required" : string.Empty));
            _confirmButton.IsEnabled = preview.CanConfirm;
            SetStatus(preview.CanConfirm
                ? "Preview accepted. Confirm to apply these choices."
                : "Blocked: " + string.Join(", ", preview.Blockers));
        }

        private int CurrentMysticPoints()
        {
            if (_mysticPoints is null)
                return _mysticPointsValue;
            decimal? raw = _mysticPoints.Value;
            if (raw is not { } points)
                return _mysticPointsValue;
            return (int)Math.Round(
                Math.Max(0m, Math.Min(_mysticPoints.Maximum, points)),
                MidpointRounding.AwayFromZero);
        }

        private CharacterCreationMagicResonanceSelections BuildSelections()
        {
            CharacterCreationMagicResonanceOptionIdentity? tradition =
                (_traditionCombo?.SelectedItem as OptionChoice)?.Identity;
            CharacterCreationMagicResonanceOptionIdentity? stream =
                (_streamCombo?.SelectedItem as OptionChoice)?.Identity;
            List<CharacterCreationAdeptPowerAllocation> powers = new();
            foreach (KeyValuePair<string, int> level in _powerLevels)
            {
                if (level.Value <= 0)
                    continue;
                powers.Add(new CharacterCreationAdeptPowerAllocation(
                    IdentityForKind(CharacterCreationMagicResonanceKinds.AdeptPower, level.Key),
                    level.Value));
            }
            List<CharacterCreationMagicResonanceOptionIdentity> spells = new();
            foreach (string id in _spellIds)
                spells.Add(IdentityForKind(CharacterCreationMagicResonanceKinds.Spell, id));
            List<CharacterCreationMagicResonanceOptionIdentity> forms = new();
            foreach (string id in _formIds)
                forms.Add(IdentityForKind(CharacterCreationMagicResonanceKinds.ComplexForm, id));
            return new CharacterCreationMagicResonanceSelections(
                tradition,
                stream,
                powers,
                spells,
                forms);
        }

        private CharacterCreationMagicResonanceOptionIdentity IdentityForKind(string kind, string sourceId)
        {
            IReadOnlyList<CharacterCreationMagicResonanceOptionProjection> pool = kind switch
            {
                CharacterCreationMagicResonanceKinds.AdeptPower => _editor.AdeptPowers,
                CharacterCreationMagicResonanceKinds.Spell => _editor.Spells,
                CharacterCreationMagicResonanceKinds.ComplexForm => _editor.ComplexForms,
                _ => throw new InvalidOperationException("Unknown option kind " + kind),
            };
            return pool.Single(option =>
                string.Equals(option.Identity.SourceId, sourceId, StringComparison.Ordinal)).Identity;
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (InvalidateIfContextChanged())
                return;
            if (_prepared is not { } prepared || !prepared.Preview.CanConfirm)
                return;

            CharacterOverviewState overview = _overview();
            CharacterCreationMagicResonanceInteractionConfirmResult result =
                _presenter.Confirm(overview, prepared, explicitlyConfirmed: true);
            if (result is { Outcome: "ok", Receipt: { } receipt })
            {
                Confirmed = true;
                Status = "Magic/Resonance confirmed (revision "
                    + receipt.ContentRevision.ToString(CultureInfo.InvariantCulture) + ").";
                Close();
                return;
            }

            _confirmButton.IsEnabled = false;
            SetStatus("Confirm failed: " + (result.Error ?? result.Outcome));
        }

        public bool InvalidateIfContextChanged()
        {
            if (_presenter.IsCurrentContext(_overview()))
                return false;
            _prepared = null;
            _confirmButton.IsEnabled = false;
            Status = "The character or account changed. Reopen Magic/Resonance for the current character.";
            Content = null;
            Close();
            return true;
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

        private static TextBlock SectionHeader(string text) => new()
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 6, 0, 0),
        };

        private sealed record OptionChoice(
            CharacterCreationMagicResonanceOptionIdentity? Identity,
            string Label,
            bool IsEnabled,
            IReadOnlyList<string> Blockers)
        {
            public override string ToString() => Label;
        }
    }
}
