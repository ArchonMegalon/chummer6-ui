using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCreateClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly StackPanel? _prioritiesPanel;
    private readonly StackPanel? _attributesPanel;
    private readonly StackPanel? _skillsPanel;
    private readonly StackPanel? _gearPanel;
    private readonly StackPanel? _spellsPanel;
    private readonly StackPanel? _finalPanel;
    private readonly StackPanel? _actionsPanel;

    public CharacterCreateClassicPort()
        : base(
            "character_create",
            "Character Create Classic",
            ["Priorities", "Attributes", "Skills", "Gear", "Spells", "Final"],
            "Chummer/Forms/Character Forms/CharacterCreate.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("CreateNoticeText");
        _tabs = this.FindControl<TabControl>("CreateTabs");
        _prioritiesPanel = this.FindControl<StackPanel>("CreatePrioritiesPanel");
        _attributesPanel = this.FindControl<StackPanel>("CreateAttributesPanel");
        _skillsPanel = this.FindControl<StackPanel>("CreateSkillsPanel");
        _gearPanel = this.FindControl<StackPanel>("CreateGearPanel");
        _spellsPanel = this.FindControl<StackPanel>("CreateSpellsPanel");
        _finalPanel = this.FindControl<StackPanel>("CreateFinalPanel");
        _actionsPanel = this.FindControl<StackPanel>("CreateActionsPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic chargen is routing through the legacy-first creation workbench.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Priorities", "Attributes", "Skills", "Gear", "Spells", "Final");

        if (_prioritiesPanel is not null)
        {
            RenderFieldRows(
                _prioritiesPanel,
                [
                    new SectionRowDisplayItem("Ruleset", FindValue(state.Rows, "gameEdition")),
                    new SectionRowDisplayItem("Build", FindValue(state.Rows, "buildMethod")),
                    new SectionRowDisplayItem("Metatype", FindValue(state.Rows, "metatype")),
                    new SectionRowDisplayItem("Priority Path", FindValue(state.Rows, "priority")),
                ],
                "No priority values are ready yet.");

            RenderFieldRows(
                _prioritiesPanel,
                MatchRows(state.Rows, 10, "priority", "metatype", "resource"),
                "No priority routing detail is available.");
        }

        if (_attributesPanel is not null)
        {
            RenderFieldRows(
                _attributesPanel,
                MatchRows(state.Rows, 20, "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance"),
                "Attribute ladder is not populated yet.");
        }

        if (_skillsPanel is not null)
        {
            RenderFieldRows(
                _skillsPanel,
                MatchRows(state.Rows, 20, "skill", "knowledge", "language"),
                "Skill and specialization values are not yet available.");
        }

        if (_gearPanel is not null)
        {
            RenderFieldRows(
                _gearPanel,
                MatchRows(state.Rows, 15, "gear", "armor", "weapon", "ranged", "melee"),
                "Starting gear is not loaded yet.");
        }

        if (_spellsPanel is not null)
        {
            RenderFieldRows(
                _spellsPanel,
                MatchRows(state.Rows, 10, "spell", "magic", "tradition"),
                "No spell list is visible yet.");
        }

        if (_finalPanel is not null)
        {
            RenderFieldRows(
                _finalPanel,
                [
                    new SectionRowDisplayItem("Build Method", FindValue(state.Rows, "buildMethod")),
                    new SectionRowDisplayItem("Metatype", FindValue(state.Rows, "metatype")),
                    new SectionRowDisplayItem("Primary Source", FindValue(state.Rows, "settings")),
                ],
                "No finalization summary yet.");
        }

        if (_actionsPanel is not null)
        {
            RenderActionRows(_actionsPanel, CollectActionLabels(state), "No creation actions are currently available.");
        }
    }
}
