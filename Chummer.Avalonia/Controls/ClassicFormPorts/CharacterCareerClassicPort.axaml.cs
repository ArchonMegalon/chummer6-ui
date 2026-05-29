using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCareerClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly ComboBox? _actionSelector;
    private readonly ListBox? _snapshotList;
    private readonly TreeView? _advancementTree;
    private readonly ListBox? _gearList;
    private readonly ListBox? _armorList;
    private readonly ListBox? _weaponsList;
    private readonly TreeView? _contactsTree;
    private readonly ListBox? _notesList;
    private readonly ListBox? _actionsList;

    public CharacterCareerClassicPort()
        : base(
            "character_career",
            "Character Career Classic",
            ["Character", "Gear", "Armor", "Weapons", "Contacts", "Notes"],
            "Chummer/Forms/Character Forms/CharacterCareer.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("CareerNoticeText");
        _tabs = this.FindControl<TabControl>("CareerTabs");
        _actionSelector = this.FindControl<ComboBox>("CareerActionSelector");
        _snapshotList = this.FindControl<ListBox>("CareerSnapshotList");
        _advancementTree = this.FindControl<TreeView>("CareerAdvancementTree");
        _gearList = this.FindControl<ListBox>("CareerGearList");
        _armorList = this.FindControl<ListBox>("CareerArmorList");
        _weaponsList = this.FindControl<ListBox>("CareerWeaponsList");
        _contactsTree = this.FindControl<TreeView>("CareerContactsTree");
        _notesList = this.FindControl<ListBox>("CareerNotesList");
        _actionsList = this.FindControl<ListBox>("CareerActionsList");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Career sheet is using the classic desktop route.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Character", "Advancement", "Gear", "Armor", "Weapons", "Contacts", "Notes");
        IReadOnlyList<SectionRowDisplayItem> rows = state.Rows;
        IReadOnlyList<string> actions = CollectActionLabels(state);
        PopulateClassicSelector(_actionSelector, actions, "No actions");

        PopulateClassicList(
            _snapshotList,
            [
                new ClassicPortLineItem("Name", FindValue(rows, "name")),
                new ClassicPortLineItem("Lifestyle", FindValue(rows, "lifestyle")),
                new ClassicPortLineItem("Build Method", FindValue(rows, "buildMethod", "settings")),
                new ClassicPortLineItem("Street Cred", FindValue(rows, "streetCred", "street")),
                new ClassicPortLineItem("Essence", FindValue(rows, "essence")),
                new ClassicPortLineItem("Karma", FindValue(rows, "karma")),
                new ClassicPortLineItem("Nuyen", FindValue(rows, "nuyen")),
            ],
            "No core career metadata is currently available.");

        PopulateClassicTree(
            _advancementTree,
            MatchRows(rows, 12, "karma", "xp", "nextlevel", "improvement", "advancement", "metatype", "special")
                .Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue)),
            "Advancement details are not ready.");

        PopulateClassicList(_gearList, MatchRows(rows, 12, "gear", "cyberware", "mod"), "No gear values are available yet.");

        PopulateClassicList(_armorList, MatchRows(rows, 12, "armor", "plate", "clothing"), "Armor fields are currently empty.");

        PopulateClassicList(_weaponsList, MatchRows(rows, 12, "weapon", "guns", "firearm", "blade"), "No weapon items are loaded yet.");

        PopulateClassicTree(
            _contactsTree,
            MatchRows(rows, 12, "contact", "ally", "familiar")
                .Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue)),
            "No contacts have been loaded for this surface.");

        PopulateClassicList(_notesList, MatchRows(rows, 12, "note", "comment", "memo"), "No notes are visible yet.");

        PopulateClassicList(
            _actionsList,
            actions.Select(action => new ClassicPortLineItem("Action", action)),
            "No live actions are currently exposed for this surface.");
    }
}
