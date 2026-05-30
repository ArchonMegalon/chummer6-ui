namespace Chummer.Avalonia.Controls;

public sealed record ClassicCareerPortViewModel(
    IReadOnlyList<ClassicPortLineItem> Snapshot,
    IReadOnlyList<ClassicPortLineItem> Advancement,
    IReadOnlyList<ClassicPortLineItem> Gear,
    IReadOnlyList<ClassicPortLineItem> Armor,
    IReadOnlyList<ClassicPortLineItem> Weapons,
    IReadOnlyList<ClassicPortLineItem> Contacts,
    IReadOnlyList<ClassicPortLineItem> Notes,
    IReadOnlyList<ClassicPortLineItem> Actions);

public sealed record ClassicCreatePortViewModel(
    IReadOnlyList<string> Priorities,
    IReadOnlyList<ClassicPortLineItem> PrioritySummary,
    IReadOnlyList<ClassicPortLineItem> Attributes,
    IReadOnlyList<ClassicPortLineItem> Skills,
    IReadOnlyList<ClassicPortLineItem> Gear,
    IReadOnlyList<ClassicPortLineItem> Spells,
    IReadOnlyList<ClassicPortLineItem> FinalSummary,
    IReadOnlyList<ClassicPortLineItem> Actions);

public sealed record ClassicGearPortViewModel(
    IReadOnlyList<string> Categories,
    IReadOnlyList<ClassicPortLineItem> CategoryRows,
    IReadOnlyList<ClassicPortLineItem> Filters,
    IReadOnlyList<ClassicPortLineItem> Details,
    IReadOnlyList<ClassicPortLineItem> PurchaseActions);

public sealed record ClassicIndexPortViewModel(
    IReadOnlyList<string> BrowseLabels,
    IReadOnlyList<ClassicPortLineItem> BrowseRows,
    IReadOnlyList<ClassicPortLineItem> SearchActions,
    IReadOnlyList<ClassicPortLineItem> SourceFacts);

public sealed record ClassicSettingsPortViewModel(
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
    public static ClassicFormPortViewModels Create(ClassicFormPortState state, ClassicFormDesignerSnapshot snapshot)
    {
        ClassicFormPortDomainModel domain = ClassicFormPortDomainModel.Create(state);
        IReadOnlyList<string> actions = ClassicFormPortSurfaceControl.CollectActionLabelsForBridge(state);

        return new ClassicFormPortViewModels(
            Career: new ClassicCareerPortViewModel(
                Snapshot: domain.SnapshotFacts,
                Advancement: domain.AdvancementFacts,
                Gear: domain.GearFacts,
                Armor: domain.ArmorFacts,
                Weapons: domain.WeaponFacts,
                Contacts: domain.ContactFacts,
                Notes: domain.NoteFacts,
                Actions: ActionLines(actions, "Action")),
            Create: new ClassicCreatePortViewModel(
                Priorities: domain.PriorityFacts.Select(static item => item.Label).ToArray(),
                PrioritySummary: domain.PrioritySummaryFacts,
                Attributes: domain.AttributeFacts,
                Skills: domain.SkillFacts,
                Gear: domain.CreationGearFacts,
                Spells: domain.SpellFacts,
                FinalSummary: domain.FinalSummaryFacts,
                Actions: ActionLines(actions, "Action")),
            Gear: new ClassicGearPortViewModel(
                Categories: domain.GearCategoryFacts.Select(static item => item.Label).ToArray(),
                CategoryRows: domain.GearCategoryFacts,
                Filters: domain.FilterFacts.Concat(ActionLines(actions, "Action")).ToArray(),
                Details: domain.GearDetailFacts,
                PurchaseActions: ActionLines(actions, "Purchase Action")),
            Index: new ClassicIndexPortViewModel(
                BrowseLabels: domain.IndexFacts.Select(static item => item.Label).ToArray(),
                BrowseRows: domain.IndexFacts,
                SearchActions: ActionLines(actions, "Search Action"),
                SourceFacts: ClassicFormPortSurfaceControl.DesignerChromeFactsForBridge(snapshot, 12)),
            Settings: new ClassicSettingsPortViewModel(
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

internal sealed record ClassicFormPortDomainModel(
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
    private enum DomainBucket
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

    public static ClassicFormPortDomainModel Create(ClassicFormPortState state)
    {
        IReadOnlyList<ClassicDomainFact> facts = ClassicDomainFactSet.FromState(state);
        IReadOnlyList<ClassicPortLineItem> Snapshot(params string[] keys) => Keys(facts, keys);
        IReadOnlyList<ClassicPortLineItem> Bucket(DomainBucket bucket, int maxCount) => Items(facts, bucket, maxCount);

        return new ClassicFormPortDomainModel(
            SnapshotFacts: Snapshot("name", "lifestyle", "buildmethod", "streetcred", "essence", "karma", "nuyen"),
            AdvancementFacts: Bucket(DomainBucket.Advancement, 12),
            GearFacts: Bucket(DomainBucket.Gear, 12),
            ArmorFacts: Bucket(DomainBucket.Armor, 12),
            WeaponFacts: Bucket(DomainBucket.Weapon, 12),
            ContactFacts: Bucket(DomainBucket.Contact, 12),
            NoteFacts: Bucket(DomainBucket.Note, 12),
            PriorityFacts: Bucket(DomainBucket.Priority, 10),
            PrioritySummaryFacts: Snapshot("gameedition", "buildmethod", "metatype", "priority"),
            AttributeFacts: Bucket(DomainBucket.Attribute, 20),
            SkillFacts: Bucket(DomainBucket.Skill, 20),
            CreationGearFacts: Items(facts, [DomainBucket.Gear, DomainBucket.Armor, DomainBucket.Weapon], 15),
            SpellFacts: Bucket(DomainBucket.Spell, 10),
            FinalSummaryFacts: Snapshot("buildmethod", "metatype", "settings"),
            GearCategoryFacts: Items(facts, [DomainBucket.Gear, DomainBucket.Armor, DomainBucket.Weapon], 18),
            FilterFacts: Bucket(DomainBucket.Filter, 8),
            GearDetailFacts: Bucket(DomainBucket.Detail, 16),
            IndexFacts: Bucket(DomainBucket.Index, 12),
            SettingFacts: Bucket(DomainBucket.Setting, 32));
    }

    private static IReadOnlyList<ClassicPortLineItem> Keys(IReadOnlyList<ClassicDomainFact> facts, params string[] keys)
        => keys.Select(key =>
            {
                ClassicDomainFact? fact = facts.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
                return new ClassicPortLineItem(Title(key), fact?.Value ?? "n/a");
            })
            .ToArray();

    private static IReadOnlyList<ClassicPortLineItem> Items(IReadOnlyList<ClassicDomainFact> facts, DomainBucket bucket, int maxCount)
        => Items(facts, [bucket], maxCount);

    private static IReadOnlyList<ClassicPortLineItem> Items(IReadOnlyList<ClassicDomainFact> facts, IReadOnlyList<DomainBucket> buckets, int maxCount)
        => facts
            .Where(item => buckets.Contains(item.Bucket))
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

    private sealed record ClassicDomainFact(string Key, string Label, string Value, DomainBucket Bucket);

    private static class ClassicDomainFactSet
    {
        public static IReadOnlyList<ClassicDomainFact> FromState(ClassicFormPortState state)
        {
            List<ClassicDomainFact> facts = [];
            foreach (var row in state.Rows)
            {
                string label = Clean(row.DisplayPath);
                string value = Clean(row.DisplayValue);
                string key = NormalizeKey(label);
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                facts.Add(new ClassicDomainFact(key, label, value, Classify(key, label, value)));
            }

            if (facts.Count == 0)
            {
                facts.Add(new ClassicDomainFact("empty", "Classic surface", "No active character data", DomainBucket.Snapshot));
            }

            return facts;
        }

        private static DomainBucket Classify(string key, string label, string value)
        {
            string haystack = $"{key} {label} {value}".ToLowerInvariant();
            if (ContainsAny(haystack, "karma", "improvement", "advancement", "metatype", "special")) return DomainBucket.Advancement;
            if (ContainsAny(haystack, "armor", "plate", "clothing")) return DomainBucket.Armor;
            if (ContainsAny(haystack, "weapon", "guns", "firearm", "blade", "ranged", "melee")) return DomainBucket.Weapon;
            if (ContainsAny(haystack, "gear", "cyberware", "bioware", "mod")) return DomainBucket.Gear;
            if (ContainsAny(haystack, "contact", "ally", "familiar")) return DomainBucket.Contact;
            if (ContainsAny(haystack, "note", "comment", "memo")) return DomainBucket.Note;
            if (ContainsAny(haystack, "priority", "resource")) return DomainBucket.Priority;
            if (ContainsAny(haystack, "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance")) return DomainBucket.Attribute;
            if (ContainsAny(haystack, "skill", "knowledge", "language")) return DomainBucket.Skill;
            if (ContainsAny(haystack, "spell", "tradition")) return DomainBucket.Spell;
            if (ContainsAny(haystack, "filter", "search", "sort")) return DomainBucket.Filter;
            if (ContainsAny(haystack, "detail", "quality", "availability", "cost")) return DomainBucket.Detail;
            if (ContainsAny(haystack, "settings", "global", "language", "ruleset", "version")) return DomainBucket.Setting;
            return DomainBucket.Index;
        }

        private static bool ContainsAny(string text, params string[] tokens)
            => tokens.Any(text.Contains);

        private static string Clean(string value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizeKey(string value)
            => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
