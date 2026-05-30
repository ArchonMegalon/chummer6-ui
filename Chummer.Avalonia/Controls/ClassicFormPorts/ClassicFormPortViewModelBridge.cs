using System.Text.Json;
using System.Text.Json.Nodes;

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
            List<ClassicDomainFact> facts = ReadPreviewFacts(state.PreviewJson);

            if (facts.Count == 0)
            {
                facts.Add(new ClassicDomainFact("empty", "Classic surface", "No active character data", DomainBucket.Snapshot));
            }

            return facts;
        }

        private static List<ClassicDomainFact> ReadPreviewFacts(string previewJson)
        {
            List<ClassicDomainFact> facts = [];
            if (string.IsNullOrWhiteSpace(previewJson))
            {
                return facts;
            }

            try
            {
                JsonNode? root = JsonNode.Parse(previewJson);
                if (root is not null)
                {
                    CollectFacts(root, [], facts);
                }
            }
            catch (JsonException)
            {
                facts.Add(new ClassicDomainFact("preview", "Preview", "Preview JSON could not be parsed", DomainBucket.Note));
            }

            return facts;
        }

        private static void CollectFacts(JsonNode node, IReadOnlyList<string> path, List<ClassicDomainFact> facts)
        {
            if (node is JsonObject obj)
            {
                foreach ((string jsonKey, JsonNode? child) in obj)
                {
                    if (child is null)
                    {
                        continue;
                    }

                    CollectFacts(child, [.. path, jsonKey], facts);
                }

                return;
            }

            if (node is JsonArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    JsonNode? child = array[index];
                    if (child is null)
                    {
                        continue;
                    }

                    CollectFacts(child, [.. path, (index + 1).ToString("00")], facts);
                }

                return;
            }

            string value = Clean(node.ToJsonString().Trim('"'));
            if (string.IsNullOrWhiteSpace(value) || path.Count == 0)
            {
                return;
            }

            string key = NormalizeKey(path[^1]);
            string label = BuildLabel(path);
            facts.Add(new ClassicDomainFact(key, label, value, ClassifyBySchemaKey(key, path)));
        }

        private static DomainBucket ClassifyBySchemaKey(string key, IReadOnlyList<string> path)
        {
            if (AdvancementKeys.Contains(key)) return DomainBucket.Advancement;
            if (ArmorKeys.Contains(key)) return DomainBucket.Armor;
            if (WeaponKeys.Contains(key)) return DomainBucket.Weapon;
            if (GearKeys.Contains(key)) return DomainBucket.Gear;
            if (ContactKeys.Contains(key)) return DomainBucket.Contact;
            if (NoteKeys.Contains(key)) return DomainBucket.Note;
            if (PriorityKeys.Contains(key)) return DomainBucket.Priority;
            if (AttributeKeys.Contains(key)) return DomainBucket.Attribute;
            if (SkillKeys.Contains(key)) return DomainBucket.Skill;
            if (SpellKeys.Contains(key)) return DomainBucket.Spell;
            if (FilterKeys.Contains(key)) return DomainBucket.Filter;
            if (DetailKeys.Contains(key)) return DomainBucket.Detail;
            if (SettingKeys.Contains(key)) return DomainBucket.Setting;
            return path.Count <= 2 ? DomainBucket.Snapshot : DomainBucket.Index;
        }

        private static string BuildLabel(IReadOnlyList<string> path)
            => string.Join(" / ", path.Select(static segment => Title(NormalizeKey(segment))));

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
