using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCreateClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly ComboBox? _prioritySelector;
    private readonly ListBox? _prioritiesList;
    private readonly ListBox? _attributesList;
    private readonly TreeView? _skillsTree;
    private readonly ListBox? _gearList;
    private readonly TreeView? _spellsTree;
    private readonly ListBox? _finalList;
    private readonly ListBox? _actionsList;

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
        _prioritySelector = this.FindControl<ComboBox>("CreatePrioritySelector");
        _prioritiesList = this.FindControl<ListBox>("CreatePrioritiesList");
        _attributesList = this.FindControl<ListBox>("CreateAttributesList");
        _skillsTree = this.FindControl<TreeView>("CreateSkillsTree");
        _gearList = this.FindControl<ListBox>("CreateGearList");
        _spellsTree = this.FindControl<TreeView>("CreateSpellsTree");
        _finalList = this.FindControl<ListBox>("CreateFinalList");
        _actionsList = this.FindControl<ListBox>("CreateActionsList");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Classic chargen is routing through the legacy-first creation workbench.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Priorities", "Attributes", "Skills", "Gear", "Spells", "Final");
        IReadOnlyList<SectionRowDisplayItem> rows = state.Rows;
        IReadOnlyList<string> actions = CollectActionLabels(state);
        PopulateClassicSelector(_prioritySelector, MatchRows(rows, 10, "priority", "metatype", "resource").Select(static row => row.DisplayPath), "No priority routing");

        PopulateClassicList(
            _prioritiesList,
            [
                new ClassicPortLineItem("Ruleset", FindValue(rows, "gameEdition")),
                new ClassicPortLineItem("Build", FindValue(rows, "buildMethod")),
                new ClassicPortLineItem("Metatype", FindValue(rows, "metatype")),
                new ClassicPortLineItem("Priority Path", FindValue(rows, "priority")),
            ],
            "No priority values are ready yet.");

        PopulateClassicList(_attributesList, MatchRows(rows, 20, "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance"), "Attribute ladder is not populated yet.");

        PopulateClassicTree(_skillsTree, MatchRows(rows, 20, "skill", "knowledge", "language").Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue)), "Skill and specialization values are not yet available.");

        PopulateClassicList(_gearList, MatchRows(rows, 15, "gear", "armor", "weapon", "ranged", "melee"), "Starting gear is not loaded yet.");

        PopulateClassicTree(_spellsTree, MatchRows(rows, 10, "spell", "magic", "tradition").Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue)), "No spell list is visible yet.");

        PopulateClassicList(
            _finalList,
            [
                new ClassicPortLineItem("Build Method", FindValue(rows, "buildMethod")),
                new ClassicPortLineItem("Metatype", FindValue(rows, "metatype")),
                new ClassicPortLineItem("Primary Source", FindValue(rows, "settings")),
            ],
            "No finalization summary yet.");

        PopulateClassicList(_actionsList, actions.Select(action => new ClassicPortLineItem("Action", action)), "No creation actions are currently available.");
    }
}
