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
        ClassicCreatePortViewModel viewModel = ClassicFormPortViewModelBridge.Create(state, snapshot, CreateCommandSet(_noticeText)).Create;
        DataContext = viewModel;
        PopulateClassicSelector(_prioritySelector, viewModel.Priorities, "No priority routing");

        PopulateClassicList(
            _prioritiesList,
            ProjectLines(viewModel.PrioritySummary, static item => item.Label, static item => item.Value),
            "No priority values are ready yet.");

        PopulateClassicList(_attributesList, ProjectLines(viewModel.Attributes, static item => item.Label, static item => item.Value), "Attribute ladder is not populated yet.");

        PopulateClassicTree(_skillsTree, ProjectLines(viewModel.Skills, static item => item.Label, static item => item.Value), "Skill and specialization values are not yet available.");

        PopulateClassicList(_gearList, ProjectLines(viewModel.Gear, static item => item.Label, static item => item.Value), "Starting gear is not loaded yet.");

        PopulateClassicTree(_spellsTree, ProjectLines(viewModel.Spells, static item => item.Label, static item => item.Value), "No spell list is visible yet.");

        PopulateClassicList(
            _finalList,
            ProjectLines(viewModel.FinalSummary, static item => item.Label, static item => item.Value),
            "No finalization summary yet.");

        PopulateClassicList(_actionsList, ProjectLines(viewModel.Actions, static item => item.Label, static item => item.Value), "No creation actions are currently available.");
    }
}
