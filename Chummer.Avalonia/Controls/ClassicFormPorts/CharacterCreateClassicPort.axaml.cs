using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCreateClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly WrapPanel? _tabsPanel;
    private readonly WrapPanel? _factsPanel;
    private readonly StackPanel? _priorityPanel;
    private readonly StackPanel? _attributePanel;
    private readonly StackPanel? _actionPanel;

    public CharacterCreateClassicPort()
        : base(
            "character_create",
            "Character Create Classic",
            ["Priorities", "Attributes", "Skills", "Gear", "Spells", "Final"],
            "Chummer/Forms/Character Forms/CharacterCreate.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("CreateNoticeText");
        _tabsPanel = this.FindControl<WrapPanel>("CreateTabsPanel");
        _factsPanel = this.FindControl<WrapPanel>("CreateFactsPanel");
        _priorityPanel = this.FindControl<StackPanel>("CreatePriorityPanel");
        _attributePanel = this.FindControl<StackPanel>("CreateAttributePanel");
        _actionPanel = this.FindControl<StackPanel>("CreateActionPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetNotice(_noticeText, state.Notice, "Classic chargen is routing through the legacy-first creation workbench.");
        }

        if (_tabsPanel is not null)
        {
            PopulateChipStrip(_tabsPanel, ResolveTabLabels(Tabs, snapshot), state.ActiveTabId);
        }

        if (_factsPanel is not null)
        {
            PopulateFactStrip(
                _factsPanel,
                [
                    new ClassicSheetFactDisplayItem("Edition", FindValue(state.Rows, "gameEdition")),
                    new ClassicSheetFactDisplayItem("Build", FindValue(state.Rows, "buildMethod")),
                    new ClassicSheetFactDisplayItem("Priority", FindValue(state.Rows, "priority")),
                    new ClassicSheetFactDisplayItem("Metatype", FindValue(state.Rows, "metatype")),
                ]);
        }

        if (_priorityPanel is not null)
        {
            _priorityPanel.Children.Clear();
            StackPanel priorities = new();
            PopulateLineStack(priorities, SelectRows(state.Rows, 10, "priority", "metatype", "magic", "resonance", "resources"), "Priority selections will appear here once a creation route is active.");
            _priorityPanel.Children.Add(CreateSectionCard("Priority Picks", priorities));

            StackPanel skills = new();
            PopulateLineStack(skills, SelectRows(state.Rows, 10, "skill", "knowledge"), "Skills summary is waiting for runtime values.");
            _priorityPanel.Children.Add(CreateSectionCard("Skill Summary", skills));
        }

        if (_attributePanel is not null)
        {
            _attributePanel.Children.Clear();
            StackPanel attributes = new();
            PopulateLineStack(attributes, SelectRows(state.Rows, 12, "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance"), "Attribute ladder is not populated yet.");
            _attributePanel.Children.Add(CreateSectionCard("Attribute Ladder", attributes));

            StackPanel gearPrep = new();
            PopulateLineStack(gearPrep, SelectRows(state.Rows, 8, "gear", "weapon", "armor", "spell"), "Gear and spell preparation will appear here.");
            _attributePanel.Children.Add(CreateSectionCard("Loadout Preparation", gearPrep));
        }

        if (_actionPanel is not null)
        {
            _actionPanel.Children.Clear();
            StackPanel actions = new();
            PopulateLineStack(actions, ResolveActionLabels(state).Select(label => new ClassicPortLineItem("Action", label)), "No creation actions are available yet.");
            _actionPanel.Children.Add(CreateSectionCard("Creation Actions", actions));
        }
    }
}
