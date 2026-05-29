using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

public partial class CharacterCareerClassicPort : ClassicFormPortSurfaceControl
{
    private readonly TextBlock? _noticeText;
    private readonly TabControl? _tabs;
    private readonly StackPanel? _snapshotPanel;
    private readonly StackPanel? _advancementPanel;
    private readonly StackPanel? _gearPanel;
    private readonly StackPanel? _armorPanel;
    private readonly StackPanel? _weaponsPanel;
    private readonly StackPanel? _contactsPanel;
    private readonly StackPanel? _notesPanel;
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
        _tabs = this.FindControl<TabControl>("CareerTabs");
        _snapshotPanel = this.FindControl<StackPanel>("CareerSnapshotPanel");
        _advancementPanel = this.FindControl<StackPanel>("CareerAdvancementPanel");
        _gearPanel = this.FindControl<StackPanel>("CareerGearPanel");
        _armorPanel = this.FindControl<StackPanel>("CareerArmorPanel");
        _weaponsPanel = this.FindControl<StackPanel>("CareerWeaponsPanel");
        _contactsPanel = this.FindControl<StackPanel>("CareerContactsPanel");
        _notesPanel = this.FindControl<StackPanel>("CareerNotesPanel");
        _actionsPanel = this.FindControl<StackPanel>("CareerActionsPanel");
    }

    protected override void ApplyState(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        _ = snapshot;
        if (_noticeText is not null)
        {
            SetLeadNotice(_noticeText, state.Notice, "Career sheet is using the classic desktop route.");
        }

        SetActiveTab(_tabs, state.ActiveTabId, "Character", "Advancement", "Gear", "Armor", "Weapons", "Contacts", "Notes");

        if (_snapshotPanel is not null)
        {
            RenderFieldRows(
                _snapshotPanel,
                [
                    new SectionRowDisplayItem("Name", FindValue(state.Rows, "name")),
                    new SectionRowDisplayItem("Lifestyle", FindValue(state.Rows, "lifestyle")),
                    new SectionRowDisplayItem("Build Method", FindValue(state.Rows, "buildMethod", "settings")),
                    new SectionRowDisplayItem("Street Cred", FindValue(state.Rows, "streetCred", "street")),
                    new SectionRowDisplayItem("Essence", FindValue(state.Rows, "essence")),
                    new SectionRowDisplayItem("Karma", FindValue(state.Rows, "karma")),
                    new SectionRowDisplayItem("Nuyen", FindValue(state.Rows, "nuyen")),
                ],
                "No core career metadata is currently available.");
        }

        if (_advancementPanel is not null)
        {
            RenderFieldRows(
                _advancementPanel,
                MatchRows(state.Rows, 12, "karma", "xp", "nextlevel", "improvement", "advancement", "metatype", "special"),
                "Advancement details are not ready.");
        }

        if (_gearPanel is not null)
        {
            RenderFieldRows(
                _gearPanel,
                MatchRows(state.Rows, 12, "gear", "cyberware", "mod"),
                "No gear values are available yet.");
        }

        if (_armorPanel is not null)
        {
            RenderFieldRows(
                _armorPanel,
                MatchRows(state.Rows, 12, "armor", "plate", "clothing"),
                "Armor fields are currently empty.");
        }

        if (_weaponsPanel is not null)
        {
            RenderFieldRows(
                _weaponsPanel,
                MatchRows(state.Rows, 12, "weapon", "guns", "firearm", "blade"),
                "No weapon items are loaded yet.");
        }

        if (_contactsPanel is not null)
        {
            RenderFieldRows(
                _contactsPanel,
                MatchRows(state.Rows, 12, "contact", "ally", "familiar"),
                "No contacts have been loaded for this surface.");
        }

        if (_notesPanel is not null)
        {
            RenderFieldRows(
                _notesPanel,
                MatchRows(state.Rows, 12, "note", "comment", "memo"),
                "No notes are visible yet.");
        }

        if (_actionsPanel is not null)
        {
            RenderActionRows(_actionsPanel, CollectActionLabels(state), "No live actions are currently exposed for this surface.");
        }
    }
}
