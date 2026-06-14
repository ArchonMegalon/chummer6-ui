using System.Windows.Input;

namespace Chummer.Avalonia.Controls;

public sealed record CareerSnapshotEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record AdvancementEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record GearEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record ArmorEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record WeaponEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record ContactEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record NoteEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record PriorityChoice(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record AttributeEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record SkillEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record SpellEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record GearCategoryEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record GearFilterEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record GearDetailEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record BrowseEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record ChromeEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record SettingEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record ActionEntry(string Label, string Value)
{
    public ClassicPortLineItem ToLineItem() => new(Label, Value);
}

public sealed record ClassicCareerPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<CareerSnapshotEntry> Snapshot,
    IReadOnlyList<AdvancementEntry> Advancement,
    IReadOnlyList<GearEntry> Gear,
    IReadOnlyList<ArmorEntry> Armor,
    IReadOnlyList<WeaponEntry> Weapons,
    IReadOnlyList<ContactEntry> Contacts,
    IReadOnlyList<NoteEntry> Notes,
    IReadOnlyList<ActionEntry> Actions);

public sealed record ClassicCreatePortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<PriorityChoice> PrioritySummary,
    IReadOnlyList<AttributeEntry> Attributes,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<GearEntry> Gear,
    IReadOnlyList<SpellEntry> Spells,
    IReadOnlyList<CareerSnapshotEntry> FinalSummary,
    IReadOnlyList<ActionEntry> Actions);

public sealed record ClassicGearPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> Categories,
    IReadOnlyList<GearCategoryEntry> CategoryRows,
    IReadOnlyList<GearFilterEntry> Filters,
    IReadOnlyList<GearDetailEntry> Details,
    IReadOnlyList<ActionEntry> PurchaseActions);

public sealed record ClassicIndexPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> BrowseLabels,
    IReadOnlyList<BrowseEntry> BrowseRows,
    IReadOnlyList<ActionEntry> SearchActions,
    IReadOnlyList<ChromeEntry> SourceFacts);

public sealed record ClassicSettingsPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> GlobalLabels,
    IReadOnlyList<SettingEntry> GlobalRows,
    IReadOnlyList<ActionEntry> CustomDataActions,
    IReadOnlyList<ChromeEntry> GitHubIssueChannels,
    IReadOnlyList<ChromeEntry> Plugins);

public sealed record ClassicFormPortViewModels(
    ClassicCareerPortViewModel Career,
    ClassicCreatePortViewModel Create,
    ClassicGearPortViewModel Gear,
    ClassicIndexPortViewModel Index,
    ClassicSettingsPortViewModel Settings);

public static class ClassicFormPortViewModelBridge
{
    public static ClassicFormPortViewModels Create(
        ClassicFormPortState state,
        ClassicFormDesignerSnapshot snapshot,
        ClassicFormPortActionCommands commands)
    {
        ClassicFormPortDocument document = state.Document;
        IReadOnlyList<string> actions = ClassicFormPortSurfaceControl.CollectActionLabelsForBridge(state);

        return new ClassicFormPortViewModels(
            Career: new ClassicCareerPortViewModel(
                Commands: commands,
                Snapshot: document.CareerSnapshot,
                Advancement: document.Advancement,
                Gear: document.CareerGear,
                Armor: document.Armor,
                Weapons: document.Weapons,
                Contacts: document.Contacts,
                Notes: document.Notes,
                Actions: ActionEntries(actions, "Action")),
            Create: new ClassicCreatePortViewModel(
                Commands: commands,
                Priorities: document.PriorityFacts.Select(static item => item.Label).ToArray(),
                PrioritySummary: document.PrioritySummary,
                Attributes: document.Attributes,
                Skills: document.Skills,
                Gear: document.CreationGear,
                Spells: document.Spells,
                FinalSummary: document.FinalSummary,
                Actions: ActionEntries(actions, "Action")),
            Gear: new ClassicGearPortViewModel(
                Commands: commands,
                Categories: document.GearCategories.Select(static item => item.Label).ToArray(),
                CategoryRows: document.GearCategories,
                Filters: document.Filters.Concat(ActionEntries(actions, "Action").Select(static item => new GearFilterEntry(item.Label, item.Value))).ToArray(),
                Details: document.GearDetails,
                PurchaseActions: ActionEntries(actions, "Purchase Action")),
            Index: new ClassicIndexPortViewModel(
                Commands: commands,
                BrowseLabels: document.IndexRows.Select(static item => item.Label).ToArray(),
                BrowseRows: document.IndexRows,
                SearchActions: ActionEntries(actions, "Search Action"),
                SourceFacts: ClassicFormPortSurfaceControl.DesignerChromeFactsForBridge(snapshot, 12)
                    .Select(static item => new ChromeEntry(item.Label, item.Detail))
                    .ToArray()),
            Settings: new ClassicSettingsPortViewModel(
                Commands: commands,
                GlobalLabels: document.Settings.Select(static item => item.Label).ToArray(),
                GlobalRows: document.Settings,
                CustomDataActions: ActionEntries(actions, "Action"),
                GitHubIssueChannels: ClassicFormPortSurfaceControl.MergeLegacyTabsForBridge(["Global", "Custom Data", "GitHub Issues", "Plugins"], snapshot)
                    .Select(static label => new ChromeEntry("Issue Channel", label))
                    .ToArray(),
                Plugins: snapshot.ContextMenus.Select(static menu => new ChromeEntry("Plugin", menu)).ToArray()));
    }

    private static IReadOnlyList<ActionEntry> ActionEntries(IEnumerable<string> actions, string label)
        => actions.Select(action => new ActionEntry(label, action)).ToArray();
}

public sealed record ClassicFormPortActionCommands(
    ICommand AddCommand,
    ICommand EditCommand,
    ICommand DeleteCommand,
    ICommand SearchCommand,
    ICommand CommitCommand);

public sealed class ClassicFormPortCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool> _canExecute;

    public ClassicFormPortCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute ?? (static _ => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    public void RefreshCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed record ClassicFormPortDocument(
    IReadOnlyList<CareerSnapshotEntry> CareerSnapshot,
    IReadOnlyList<AdvancementEntry> Advancement,
    IReadOnlyList<GearEntry> CareerGear,
    IReadOnlyList<ArmorEntry> Armor,
    IReadOnlyList<WeaponEntry> Weapons,
    IReadOnlyList<ContactEntry> Contacts,
    IReadOnlyList<NoteEntry> Notes,
    IReadOnlyList<PriorityChoice> PriorityFacts,
    IReadOnlyList<PriorityChoice> PrioritySummary,
    IReadOnlyList<AttributeEntry> Attributes,
    IReadOnlyList<SkillEntry> Skills,
    IReadOnlyList<GearEntry> CreationGear,
    IReadOnlyList<SpellEntry> Spells,
    IReadOnlyList<CareerSnapshotEntry> FinalSummary,
    IReadOnlyList<GearCategoryEntry> GearCategories,
    IReadOnlyList<GearFilterEntry> Filters,
    IReadOnlyList<GearDetailEntry> GearDetails,
    IReadOnlyList<BrowseEntry> IndexRows,
    IReadOnlyList<SettingEntry> Settings)
{
    public static ClassicFormPortDocument CreateFromSectionRows(IReadOnlyList<SectionRowDisplayItem> sourceRows)
    {
        IReadOnlyList<ClassicPortRowFact> facts = ClassicFormPortDocumentFacts.ParseDocumentRows(sourceRows);

        return new ClassicFormPortDocument(
            CareerSnapshot: SelectKeys(facts, "name", "lifestyle", "buildmethod", "streetcred", "essence", "karma", "nuyen")
                .Select(static item => new CareerSnapshotEntry(item.Label, item.Value))
                .ToArray(),
            Advancement: SelectBucket(facts, ClassicPortRowKind.Advancement, 12)
                .Select(static item => new AdvancementEntry(item.Label, item.Value))
                .ToArray(),
            CareerGear: SelectBucket(facts, ClassicPortRowKind.Gear, 12)
                .Select(static item => new GearEntry(item.Label, item.Value))
                .ToArray(),
            Armor: SelectBucket(facts, ClassicPortRowKind.Armor, 12)
                .Select(static item => new ArmorEntry(item.Label, item.Value))
                .ToArray(),
            Weapons: SelectBucket(facts, ClassicPortRowKind.Weapon, 12)
                .Select(static item => new WeaponEntry(item.Label, item.Value))
                .ToArray(),
            Contacts: SelectBucket(facts, ClassicPortRowKind.Contact, 12)
                .Select(static item => new ContactEntry(item.Label, item.Value))
                .ToArray(),
            Notes: SelectBucket(facts, ClassicPortRowKind.Note, 12)
                .Select(static item => new NoteEntry(item.Label, item.Value))
                .ToArray(),
            PriorityFacts: SelectBucket(facts, ClassicPortRowKind.Priority, 10)
                .Select(static item => new PriorityChoice(item.Label, item.Value))
                .ToArray(),
            PrioritySummary: SelectKeys(facts, "gameedition", "buildmethod", "metatype", "priority")
                .Select(static item => new PriorityChoice(item.Label, item.Value))
                .ToArray(),
            Attributes: SelectBucket(facts, ClassicPortRowKind.Attribute, 20)
                .Select(static item => new AttributeEntry(item.Label, item.Value))
                .ToArray(),
            Skills: SelectBucket(facts, ClassicPortRowKind.Skill, 20)
                .Select(static item => new SkillEntry(item.Label, item.Value))
                .ToArray(),
            CreationGear: SelectBuckets(facts, [ClassicPortRowKind.Gear, ClassicPortRowKind.Armor, ClassicPortRowKind.Weapon], 15)
                .Select(static item => new GearEntry(item.Label, item.Value))
                .ToArray(),
            Spells: SelectBucket(facts, ClassicPortRowKind.Spell, 10)
                .Select(static item => new SpellEntry(item.Label, item.Value))
                .ToArray(),
            FinalSummary: SelectKeys(facts, "buildmethod", "metatype", "settings")
                .Select(static item => new CareerSnapshotEntry(item.Label, item.Value))
                .ToArray(),
            GearCategories: SelectBuckets(facts, [ClassicPortRowKind.Gear, ClassicPortRowKind.Armor, ClassicPortRowKind.Weapon], 18)
                .Select(static item => new GearCategoryEntry(item.Label, item.Value))
                .ToArray(),
            Filters: SelectBucket(facts, ClassicPortRowKind.Filter, 8)
                .Select(static item => new GearFilterEntry(item.Label, item.Value))
                .ToArray(),
            GearDetails: SelectBucket(facts, ClassicPortRowKind.Detail, 16)
                .Select(static item => new GearDetailEntry(item.Label, item.Value))
                .ToArray(),
            IndexRows: SelectBucket(facts, ClassicPortRowKind.Index, 12)
                .Select(static item => new BrowseEntry(item.Label, item.Value))
                .ToArray(),
            Settings: SelectBucket(facts, ClassicPortRowKind.Setting, 32)
                .Select(static item => new SettingEntry(item.Label, item.Value))
                .ToArray());
    }

    private static IReadOnlyList<ClassicPortRowFact> SelectKeys(IReadOnlyList<ClassicPortRowFact> facts, params string[] keys)
        => keys.Select(key =>
            {
                ClassicPortRowFact? fact = facts.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                return fact ?? new ClassicPortRowFact(key, Title(key), "n/a", ClassicPortRowKind.Snapshot);
            })
            .ToArray();

    private static IReadOnlyList<ClassicPortRowFact> SelectBucket(IReadOnlyList<ClassicPortRowFact> facts, ClassicPortRowKind bucket, int maxCount)
        => SelectBuckets(facts, [bucket], maxCount);

    private static IReadOnlyList<ClassicPortRowFact> SelectBuckets(IReadOnlyList<ClassicPortRowFact> facts, IReadOnlyList<ClassicPortRowKind> buckets, int maxCount)
        => facts
            .Where(item => buckets.Contains(item.Kind))
            .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(maxCount)
            .ToArray();

    private static string Title(string key)
        => key switch
        {
            "gameedition" => "Ruleset",
            "buildmethod" => "Build Method",
            "streetcred" => "Street Cred",
            _ => string.Concat(key[..1].ToUpperInvariant(), key[1..])
        };
}

internal enum ClassicPortRowKind
{
    Snapshot,
    Advancement,
    Gear,
    Armor,
    Weapon,
    Contact,
    Note,
    Priority,
    Attribute,
    Skill,
    Spell,
    Filter,
    Detail,
    Index,
    Setting
}

internal sealed record ClassicPortRowFact(string Key, string Label, string Value, ClassicPortRowKind Kind);

internal static class ClassicFormPortDocumentFacts
{
    public static IReadOnlyList<ClassicPortRowFact> ParseDocumentRows(IReadOnlyList<SectionRowDisplayItem> sourceRows)
    {
        List<ClassicPortRowFact> facts = [];
        foreach (SectionRowDisplayItem sourceRow in sourceRows)
        {
            string path = sourceRow.Path ?? string.Empty;
            string value = Clean(sourceRow.Value ?? string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            string key = NormalizeKey(path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? path);
            facts.Add(new ClassicPortRowFact(key, BuildDisplayLabel(path), value, ResolveRowKind(key, path)));
        }

        if (facts.Count == 0)
        {
            facts.Add(new ClassicPortRowFact("empty", "Classic surface", "No active character data", ClassicPortRowKind.Snapshot));
        }

        return facts;
    }

    private static string BuildDisplayLabel(string path)
        => new SectionRowDisplayItem(path, string.Empty).DisplayPath;

    private static ClassicPortRowKind ResolveRowKind(string key, string path)
    {
        if (AdvancementKeys.Contains(key)) return ClassicPortRowKind.Advancement;
        if (ArmorKeys.Contains(key)) return ClassicPortRowKind.Armor;
        if (WeaponKeys.Contains(key)) return ClassicPortRowKind.Weapon;
        if (GearKeys.Contains(key)) return ClassicPortRowKind.Gear;
        if (ContactKeys.Contains(key)) return ClassicPortRowKind.Contact;
        if (NoteKeys.Contains(key)) return ClassicPortRowKind.Note;
        if (PriorityKeys.Contains(key)) return ClassicPortRowKind.Priority;
        if (AttributeKeys.Contains(key)) return ClassicPortRowKind.Attribute;
        if (SkillKeys.Contains(key)) return ClassicPortRowKind.Skill;
        if (SpellKeys.Contains(key)) return ClassicPortRowKind.Spell;
        if (FilterKeys.Contains(key)) return ClassicPortRowKind.Filter;
        if (DetailKeys.Contains(key)) return ClassicPortRowKind.Detail;
        if (SettingKeys.Contains(key)) return ClassicPortRowKind.Setting;
        return path.Count(static character => character == '/') <= 1 ? ClassicPortRowKind.Snapshot : ClassicPortRowKind.Index;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeKey(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static readonly HashSet<string> AdvancementKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "karma", "totalkarma", "streetcred", "notoriety", "publicawareness", "metatype", "specialattributes"
    };

    private static readonly HashSet<string> ArmorKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "armor", "armorname", "armorvalue", "armorjackets", "clothing", "protection"
    };

    private static readonly HashSet<string> WeaponKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "weapon", "weaponname", "damage", "accuracy", "ap", "mode", "recoil", "ranged", "melee"
    };

    private static readonly HashSet<string> GearKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "gear", "gearname", "cyberware", "bioware", "augmentation", "device", "rating", "quantity"
    };

    private static readonly HashSet<string> ContactKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "contact", "contacts", "loyalty", "connection", "name", "role"
    };

    private static readonly HashSet<string> NoteKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "notes", "description", "memo", "comment", "summary"
    };

    private static readonly HashSet<string> PriorityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "priority", "prioritytable", "resources", "buildmethod", "gameedition"
    };

    private static readonly HashSet<string> AttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance", "essence", "initiative"
    };

    private static readonly HashSet<string> SkillKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "skill", "skills", "knowledge", "language", "group", "dicepool"
    };

    private static readonly HashSet<string> SpellKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "spell", "spells", "tradition", "drain", "force", "ritual"
    };

    private static readonly HashSet<string> FilterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "filter", "search", "sort", "category", "source"
    };

    private static readonly HashSet<string> DetailKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "detail", "quality", "availability", "cost", "nuyen", "license", "sourcebook"
    };

    private static readonly HashSet<string> SettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "settings", "setting", "global", "language", "ruleset", "version", "option"
    };
}
