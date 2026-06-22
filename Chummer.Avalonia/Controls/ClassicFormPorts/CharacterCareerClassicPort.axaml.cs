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
        if (_actionSelector is not null)
        {
            DesktopShellTheme.ApplyShellComboBoxTheme(_actionSelector);
        }

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
        ClassicCareerPortViewModel viewModel = ClassicFormPortViewModelBridge.Create(state, snapshot, CreateCommandSet(_noticeText)).Career;
        DataContext = viewModel;
        PopulateClassicSelector(_actionSelector, viewModel.Actions.Select(static action => action.Value), "No actions");

        PopulateClassicList(
            _snapshotList,
            ProjectLines(viewModel.Snapshot, static item => item.Label, static item => item.Value),
            "No core career metadata is currently available.");

        PopulateClassicTree(
            _advancementTree,
            ProjectLines(viewModel.Advancement, static item => item.Label, static item => item.Value),
            "Advancement details are not ready.");

        PopulateClassicList(_gearList, ProjectLines(viewModel.Gear, static item => item.Label, static item => item.Value), "No gear values are available yet.");

        PopulateClassicList(_armorList, ProjectLines(viewModel.Armor, static item => item.Label, static item => item.Value), "Armor fields are currently empty.");

        PopulateClassicList(_weaponsList, ProjectLines(viewModel.Weapons, static item => item.Label, static item => item.Value), "No weapon items are loaded yet.");

        PopulateClassicTree(
            _contactsTree,
            ProjectLines(viewModel.Contacts, static item => item.Label, static item => item.Value),
            "No contacts have been loaded for this surface.");

        PopulateClassicList(_notesList, ProjectLines(viewModel.Notes, static item => item.Label, static item => item.Value), "No notes are visible yet.");

        PopulateClassicList(
            _actionsList,
            ProjectLines(viewModel.Actions, static item => item.Label, static item => item.Value),
            "No live actions are currently exposed for this surface.");
    }
}
