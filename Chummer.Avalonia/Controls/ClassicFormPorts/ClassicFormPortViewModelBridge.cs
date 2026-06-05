using System.Windows.Input;

namespace Chummer.Avalonia.Controls;

public sealed record ClassicCareerPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<ClassicPortLineItem> Snapshot,
    IReadOnlyList<ClassicPortLineItem> Advancement,
    IReadOnlyList<ClassicPortLineItem> Gear,
    IReadOnlyList<ClassicPortLineItem> Armor,
    IReadOnlyList<ClassicPortLineItem> Weapons,
    IReadOnlyList<ClassicPortLineItem> Contacts,
    IReadOnlyList<ClassicPortLineItem> Notes,
    IReadOnlyList<ClassicPortLineItem> Actions);

public sealed record ClassicCreatePortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> Priorities,
    IReadOnlyList<ClassicPortLineItem> PrioritySummary,
    IReadOnlyList<ClassicPortLineItem> Attributes,
    IReadOnlyList<ClassicPortLineItem> Skills,
    IReadOnlyList<ClassicPortLineItem> Gear,
    IReadOnlyList<ClassicPortLineItem> Spells,
    IReadOnlyList<ClassicPortLineItem> FinalSummary,
    IReadOnlyList<ClassicPortLineItem> Actions);

public sealed record ClassicGearPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> Categories,
    IReadOnlyList<ClassicPortLineItem> CategoryRows,
    IReadOnlyList<ClassicPortLineItem> Filters,
    IReadOnlyList<ClassicPortLineItem> Details,
    IReadOnlyList<ClassicPortLineItem> PurchaseActions);

public sealed record ClassicIndexPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> BrowseLabels,
    IReadOnlyList<ClassicPortLineItem> BrowseRows,
    IReadOnlyList<ClassicPortLineItem> SearchActions,
    IReadOnlyList<ClassicPortLineItem> SourceFacts);

public sealed record ClassicSettingsPortViewModel(
    ClassicFormPortActionCommands Commands,
    IReadOnlyList<string> GlobalLabels,
    IReadOnlyList<ClassicPortLineItem> GlobalRows,
    IReadOnlyList<ClassicPortLineItem> CustomDataActions,
    IReadOnlyList<ClassicPortLineItem> GitHubIssueChannels,
    IReadOnlyList<ClassicPortLineItem> Plugins);

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
        ClassicFormPortActionCommands? commands = null)
    {
        commands ??= ClassicFormPortActionCommands.NoOp;
        ClassicFormPortDomainModel domain = state.DomainModel;
        IReadOnlyList<string> actions = ClassicFormPortSurfaceControl.CollectActionLabelsForBridge(state);

        return new ClassicFormPortViewModels(
            Career: new ClassicCareerPortViewModel(
                Commands: commands,
                Snapshot: domain.SnapshotFacts,
                Advancement: domain.AdvancementFacts,
                Gear: domain.GearFacts,
                Armor: domain.ArmorFacts,
                Weapons: domain.WeaponFacts,
                Contacts: domain.ContactFacts,
                Notes: domain.NoteFacts,
                Actions: ActionLines(actions, "Action")),
            Create: new ClassicCreatePortViewModel(
                Commands: commands,
                Priorities: domain.PriorityFacts.Select(static item => item.Label).ToArray(),
                PrioritySummary: domain.PrioritySummaryFacts,
                Attributes: domain.AttributeFacts,
                Skills: domain.SkillFacts,
                Gear: domain.CreationGearFacts,
                Spells: domain.SpellFacts,
                FinalSummary: domain.FinalSummaryFacts,
                Actions: ActionLines(actions, "Action")),
            Gear: new ClassicGearPortViewModel(
                Commands: commands,
                Categories: domain.GearCategoryFacts.Select(static item => item.Label).ToArray(),
                CategoryRows: domain.GearCategoryFacts,
                Filters: domain.FilterFacts.Concat(ActionLines(actions, "Action")).ToArray(),
                Details: domain.GearDetailFacts,
                PurchaseActions: ActionLines(actions, "Purchase Action")),
            Index: new ClassicIndexPortViewModel(
                Commands: commands,
                BrowseLabels: domain.IndexFacts.Select(static item => item.Label).ToArray(),
                BrowseRows: domain.IndexFacts,
                SearchActions: ActionLines(actions, "Search Action"),
                SourceFacts: ClassicFormPortSurfaceControl.DesignerChromeFactsForBridge(snapshot, 12)),
            Settings: new ClassicSettingsPortViewModel(
                Commands: commands,
                GlobalLabels: domain.SettingFacts.Select(static item => item.Label).ToArray(),
                GlobalRows: domain.SettingFacts,
                CustomDataActions: ActionLines(actions, "Action"),
                GitHubIssueChannels: ClassicFormPortSurfaceControl.MergeLegacyTabsForBridge(["Global", "Custom Data", "GitHub Issues", "Plugins"], snapshot)
                    .Select(static label => new ClassicPortLineItem("Issue Channel", label))
                    .ToArray(),
                Plugins: snapshot.ContextMenus.Select(static menu => new ClassicPortLineItem("Plugin", menu)).ToArray()));
    }

    private static IReadOnlyList<ClassicPortLineItem> ActionLines(IEnumerable<string> actions, string label)
        => actions.Select(action => new ClassicPortLineItem(label, action)).ToArray();
}

public sealed record ClassicFormPortActionCommands(
    ICommand AddCommand,
    ICommand EditCommand,
    ICommand DeleteCommand,
    ICommand SearchCommand,
    ICommand CommitCommand)
{
    public static ClassicFormPortActionCommands NoOp { get; } = new(
        new ClassicFormPortCommand(static _ => { }),
        new ClassicFormPortCommand(static _ => { }),
        new ClassicFormPortCommand(static _ => { }),
        new ClassicFormPortCommand(static _ => { }),
        new ClassicFormPortCommand(static _ => { }));
}

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

public sealed record ClassicFormPortDomainModel(
    IReadOnlyList<ClassicPortLineItem> SnapshotFacts,
    IReadOnlyList<ClassicPortLineItem> AdvancementFacts,
    IReadOnlyList<ClassicPortLineItem> GearFacts,
    IReadOnlyList<ClassicPortLineItem> ArmorFacts,
    IReadOnlyList<ClassicPortLineItem> WeaponFacts,
    IReadOnlyList<ClassicPortLineItem> ContactFacts,
    IReadOnlyList<ClassicPortLineItem> NoteFacts,
    IReadOnlyList<ClassicPortLineItem> PriorityFacts,
    IReadOnlyList<ClassicPortLineItem> PrioritySummaryFacts,
    IReadOnlyList<ClassicPortLineItem> AttributeFacts,
    IReadOnlyList<ClassicPortLineItem> SkillFacts,
    IReadOnlyList<ClassicPortLineItem> CreationGearFacts,
    IReadOnlyList<ClassicPortLineItem> SpellFacts,
    IReadOnlyList<ClassicPortLineItem> FinalSummaryFacts,
    IReadOnlyList<ClassicPortLineItem> GearCategoryFacts,
    IReadOnlyList<ClassicPortLineItem> FilterFacts,
    IReadOnlyList<ClassicPortLineItem> GearDetailFacts,
    IReadOnlyList<ClassicPortLineItem> IndexFacts,
    IReadOnlyList<ClassicPortLineItem> SettingFacts)
{
    public static ClassicFormPortDomainModel CreateFromRows(IReadOnlyList<SectionRowDisplayItem> rows)
    {
        IReadOnlyList<ClassicPortRowFact> facts = ClassicPortRowFactSet.FromRows(rows);
        IReadOnlyList<ClassicPortLineItem> Snapshot(params string[] keys) => Keys(facts, keys);
        IReadOnlyList<ClassicPortLineItem> Bucket(ClassicPortRowKind bucket, int maxCount) => Items(facts, bucket, maxCount);

        return new ClassicFormPortDomainModel(
            SnapshotFacts: Snapshot("name", "lifestyle", "buildmethod", "streetcred", "essence", "karma", "nuyen"),
            AdvancementFacts: Bucket(ClassicPortRowKind.Advancement, 12),
            GearFacts: Bucket(ClassicPortRowKind.Gear, 12),
            ArmorFacts: Bucket(ClassicPortRowKind.Armor, 12),
            WeaponFacts: Bucket(ClassicPortRowKind.Weapon, 12),
            ContactFacts: Bucket(ClassicPortRowKind.Contact, 12),
            NoteFacts: Bucket(ClassicPortRowKind.Note, 12),
            PriorityFacts: Bucket(ClassicPortRowKind.Priority, 10),
            PrioritySummaryFacts: Snapshot("gameedition", "buildmethod", "metatype", "priority"),
            AttributeFacts: Bucket(ClassicPortRowKind.Attribute, 20),
            SkillFacts: Bucket(ClassicPortRowKind.Skill, 20),
            CreationGearFacts: Items(facts, [ClassicPortRowKind.Gear, ClassicPortRowKind.Armor, ClassicPortRowKind.Weapon], 15),
            SpellFacts: Bucket(ClassicPortRowKind.Spell, 10),
            FinalSummaryFacts: Snapshot("buildmethod", "metatype", "settings"),
            GearCategoryFacts: Items(facts, [ClassicPortRowKind.Gear, ClassicPortRowKind.Armor, ClassicPortRowKind.Weapon], 18),
            FilterFacts: Bucket(ClassicPortRowKind.Filter, 8),
            GearDetailFacts: Bucket(ClassicPortRowKind.Detail, 16),
            IndexFacts: Bucket(ClassicPortRowKind.Index, 12),
            SettingFacts: Bucket(ClassicPortRowKind.Setting, 32));
    }

    private static IReadOnlyList<ClassicPortLineItem> Keys(IReadOnlyList<ClassicPortRowFact> facts, params string[] keys)
        => keys.Select(key =>
            {
                ClassicPortRowFact? fact = facts.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                return new ClassicPortLineItem(Title(key), fact?.Value ?? "n/a");
            })
            .ToArray();

    private static IReadOnlyList<ClassicPortLineItem> Items(IReadOnlyList<ClassicPortRowFact> facts, ClassicPortRowKind bucket, int maxCount)
        => Items(facts, [bucket], maxCount);

    private static IReadOnlyList<ClassicPortLineItem> Items(IReadOnlyList<ClassicPortRowFact> facts, IReadOnlyList<ClassicPortRowKind> buckets, int maxCount)
        => facts
            .Where(item => buckets.Contains(item.Kind))
            .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Take(maxCount)
            .Select(static item => new ClassicPortLineItem(item.Label, item.Value))
            .ToArray();

    private static string Title(string key)
        => key switch
        {
            "gameedition" => "Ruleset",
            "buildmethod" => "Build Method",
            "streetcred" => "Street Cred",
            _ => string.Concat(key[..1].ToUpperInvariant(), key[1..])
        };

    private enum ClassicPortRowKind
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

    private sealed record ClassicPortRowFact(string Key, string Label, string Value, ClassicPortRowKind Kind);

    private static class ClassicPortRowFactSet
    {
        public static IReadOnlyList<ClassicPortRowFact> FromRows(IReadOnlyList<SectionRowDisplayItem> rows)
        {
            List<ClassicPortRowFact> facts = [];
            foreach (SectionRowDisplayItem row in rows)
            {
                string path = row.Path ?? string.Empty;
                string value = Clean(row.Value ?? string.Empty);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                string key = NormalizeKey(path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? path);
                facts.Add(new ClassicPortRowFact(key, BuildDisplayLabel(path), value, ClassifyBySchemaKey(key, path)));
            }

            if (facts.Count == 0)
            {
                facts.Add(new ClassicPortRowFact("empty", "Classic surface", "No active character data", ClassicPortRowKind.Snapshot));
            }

            return facts;
        }

        private static string BuildDisplayLabel(string path)
            => new SectionRowDisplayItem(path, string.Empty).DisplayPath;

        private static ClassicPortRowKind ClassifyBySchemaKey(string key, string path)
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
}
