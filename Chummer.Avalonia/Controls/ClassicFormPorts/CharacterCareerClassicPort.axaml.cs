using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCareerClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly WrapPanel? _tabsPanel;
    private readonly WrapPanel? _factsPanel;
    private readonly StackPanel? _summaryPanel;
    private readonly StackPanel? _inventoryPanel;
    private readonly StackPanel? _actionsPanel;

    public CharacterCareerClassicPort()
        : base(
            "character_career",
            "Character Career Classic",
            ["Character", "Gear", "Armor", "Weapons", "Contacts", "Notes"],
            "Chummer/Forms/Character Forms/CharacterCareer.Designer.cs")
    {
        AvaloniaXamlLoader.Load(this);
        _noticeText = this.FindControl<TextBlock>("CareerNoticeText");
        _tabsPanel = this.FindControl<WrapPanel>("CareerTabsPanel");
        _factsPanel = this.FindControl<WrapPanel>("CareerFactsPanel");
        _summaryPanel = this.FindControl<StackPanel>("CareerSummaryPanel");
        _inventoryPanel = this.FindControl<StackPanel>("CareerInventoryPanel");
        _actionsPanel = this.FindControl<StackPanel>("CareerActionsPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        if (_noticeText is not null)
        {
            SetNotice(_noticeText, state.Notice, "Career sheet is using the classic desktop route.");
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
                    new ClassicSheetFactDisplayItem("Karma", FindValue(state.Rows, "karma")),
                    new ClassicSheetFactDisplayItem("Nuyen", FindValue(state.Rows, "nuyen")),
                    new ClassicSheetFactDisplayItem("Street Cred", FindValue(state.Rows, "streetCred")),
                    new ClassicSheetFactDisplayItem("Build", FindValue(state.Rows, "buildMethod", "settings")),
                ]);
        }

        if (_summaryPanel is not null)
        {
            _summaryPanel.Children.Clear();
            StackPanel summary = new();
            PopulateLineStack(summary, SelectRows(state.Rows, 10, "karma", "nuyen", "street", "public", "notoriety", "essence", "armor"), "Career summary is waiting for runtime values.");
            _summaryPanel.Children.Add(CreateSectionCard("Career Summary", summary));
        }

        if (_inventoryPanel is not null)
        {
            _inventoryPanel.Children.Clear();
            StackPanel inventory = new();
            PopulateLineStack(inventory, SelectRows(state.Rows, 12, "gear", "weapon", "armor", "cyberware", "vehicle", "contact", "note"), "Inventory categories will appear here once the workspace is hydrated.");
            _inventoryPanel.Children.Add(CreateSectionCard("Inventory and Contacts", inventory));

            StackPanel chrome = new();
            PopulateLineStack(chrome, BuildLegacyChromeLines(snapshot, 8), "Legacy chrome metadata is unavailable.");
            _inventoryPanel.Children.Add(CreateSectionCard("Legacy Chrome", chrome));
        }

        if (_actionsPanel is not null)
        {
            _actionsPanel.Children.Clear();
            StackPanel actions = new();
            PopulateLineStack(actions, ResolveActionLabels(state).Select(label => new ClassicPortLineItem("Action", label)), "No classic quick actions are available yet.");
            _actionsPanel.Children.Add(CreateSectionCard("Classic Actions", actions));
        }
    }
}
