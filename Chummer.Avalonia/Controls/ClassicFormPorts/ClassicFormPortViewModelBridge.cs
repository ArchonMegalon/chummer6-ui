using System.Text.Json.Nodes;
using System.Windows.Input;

namespace Chummer.Avalonia.Controls;

public sealed record CareerSnapshotEntry(string Label, string Value);

public sealed record AdvancementEntry(string Label, string Value);

public sealed record GearEntry(string Label, string Value);

public sealed record ArmorEntry(string Label, string Value);

public sealed record WeaponEntry(string Label, string Value);

public sealed record ContactEntry(string Label, string Value);

public sealed record NoteEntry(string Label, string Value);

public sealed record PriorityChoice(string Label, string Value);

public sealed record AttributeEntry(string Label, string Value);

public sealed record SkillEntry(string Label, string Value);

public sealed record SpellEntry(string Label, string Value);

public sealed record GearCategoryEntry(string Label, string Value);

public sealed record GearFilterEntry(string Label, string Value);

public sealed record GearDetailEntry(string Label, string Value);

public sealed record BrowseEntry(string Label, string Value);

public sealed record ChromeEntry(string Label, string Value);

public sealed record SettingEntry(string Label, string Value);

public sealed record ActionEntry(string Label, string Value);

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
        _canExecute = canExecute ?? (static _ => false);
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
    public static ClassicFormPortDocument CreateFromPreview(string previewJson, string runtimeSectionId)
    {
        JsonObject? root = ClassicFormPortPreviewReader.Parse(previewJson);
        string sectionId = string.IsNullOrWhiteSpace(runtimeSectionId)
            ? ClassicFormPortPreviewReader.FirstNonBlank(
                ClassicFormPortPreviewReader.ReadString(root, "sectionId"),
                ClassicFormPortPreviewReader.ReadString(root, "section"))
                ?? string.Empty
            : runtimeSectionId;

        IReadOnlyList<CareerSnapshotEntry> careerSnapshot = ClassicFormPortPreviewReader.ReadCareerSnapshot(root);
        IReadOnlyList<AdvancementEntry> advancement = ClassicFormPortPreviewReader.ReadAdvancement(root);
        IReadOnlyList<GearEntry> gear = ClassicFormPortPreviewReader.ReadGear(root);
        IReadOnlyList<ArmorEntry> armor = ClassicFormPortPreviewReader.ReadArmor(root);
        IReadOnlyList<WeaponEntry> weapons = ClassicFormPortPreviewReader.ReadWeapons(root);
        IReadOnlyList<ContactEntry> contacts = ClassicFormPortPreviewReader.ReadContacts(root);
        IReadOnlyList<NoteEntry> notes = ClassicFormPortPreviewReader.ReadNotes(root, sectionId);
        IReadOnlyList<PriorityChoice> priorityFacts = ClassicFormPortPreviewReader.ReadPriorityFacts(root, sectionId);
        IReadOnlyList<PriorityChoice> prioritySummary = ClassicFormPortPreviewReader.ReadPrioritySummary(root, priorityFacts);
        IReadOnlyList<AttributeEntry> attributes = ClassicFormPortPreviewReader.ReadAttributes(root);
        IReadOnlyList<SkillEntry> skills = ClassicFormPortPreviewReader.ReadSkills(root);
        IReadOnlyList<SpellEntry> spells = ClassicFormPortPreviewReader.ReadSpells(root);
        IReadOnlyList<GearEntry> creationGear = ClassicFormPortPreviewReader.ReadCreationGear(root, gear, armor, weapons, sectionId);
        IReadOnlyList<CareerSnapshotEntry> finalSummary = ClassicFormPortPreviewReader.ReadFinalSummary(root, prioritySummary, careerSnapshot);
        IReadOnlyList<GearCategoryEntry> gearCategories = ClassicFormPortPreviewReader.ReadGearCategories(root, gear, armor, weapons, sectionId);
        IReadOnlyList<GearFilterEntry> filters = ClassicFormPortPreviewReader.ReadFilters(root, sectionId);
        IReadOnlyList<GearDetailEntry> details = ClassicFormPortPreviewReader.ReadGearDetails(root, gear, armor, weapons, sectionId);
        IReadOnlyList<BrowseEntry> indexRows = ClassicFormPortPreviewReader.ReadIndexRows(root, sectionId);
        IReadOnlyList<SettingEntry> settings = ClassicFormPortPreviewReader.ReadSettings(root, sectionId);

        return new ClassicFormPortDocument(
            CareerSnapshot: careerSnapshot,
            Advancement: advancement,
            CareerGear: gear,
            Armor: armor,
            Weapons: weapons,
            Contacts: contacts,
            Notes: notes,
            PriorityFacts: priorityFacts,
            PrioritySummary: prioritySummary,
            Attributes: attributes,
            Skills: skills,
            CreationGear: creationGear,
            Spells: spells,
            FinalSummary: finalSummary,
            GearCategories: gearCategories,
            Filters: filters,
            GearDetails: details,
            IndexRows: indexRows,
            Settings: settings);
    }
}

internal static class ClassicFormPortPreviewReader
{
    public static JsonObject? Parse(string previewJson)
    {
        if (string.IsNullOrWhiteSpace(previewJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(previewJson) as JsonObject
                ?? ParseError("Preview data was not an object.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            return ParseError($"Preview data could not be read: {ex.Message}");
        }
    }

    private static JsonObject ParseError(string message)
        => new()
        {
            ["classicPortParseError"] = message
        };

    public static IReadOnlyList<CareerSnapshotEntry> ReadCareerSnapshot(JsonObject? root)
    {
        List<CareerSnapshotEntry> entries = [];
        Append(entries, "Name", FirstNonBlank(ReadString(root, "alias"), ReadString(root, "name")));
        Append(entries, "Metatype", ReadString(root, "metatype"));
        Append(entries, "Concept", ReadString(root, "concept"));
        Append(entries, "Role", ReadString(root, "role"));
        Append(entries, "Build Method", FirstNonBlank(ReadString(root, "buildMethod"), ReadString(root, "buildmethod"), ReadString(root, "priority")));
        Append(entries, "Ruleset", FirstNonBlank(ReadString(root, "gameEdition"), ReadString(root, "ruleset"))?.ToUpperInvariant());
        Append(entries, "Lifestyle", ReadString(root, "lifestyle"));
        Append(entries, "Street Cred", ReadScalar(root, "streetCred"));
        Append(entries, "Essence", FirstNonBlank(ReadScalar(root, "essence"), ReadScalar(ReadObject(root, "combat"), "essence")));
        Append(entries, "Karma", ReadScalar(root, "karma"));
        Append(entries, "Nuyen", ReadScalar(root, "nuyen"));

        Append(entries, "Data issue", ReadString(root, "classicPortParseError"));

        return entries.Count == 0
            ? [new CareerSnapshotEntry("Classic surface", "No active character data")]
            : entries.ToArray();
    }

    public static IReadOnlyList<AdvancementEntry> ReadAdvancement(JsonObject? root)
    {
        List<AdvancementEntry> entries = [];
        Append(entries, "Karma", ReadScalar(root, "karma"));
        Append(entries, "Total Karma", ReadScalar(root, "totalKarma"));
        Append(entries, "Street Cred", ReadScalar(root, "streetCred"));
        Append(entries, "Notoriety", ReadScalar(root, "notoriety"));
        Append(entries, "Public Awareness", ReadScalar(root, "publicAwareness"));
        Append(entries, "Special Attributes", ReadScalar(root, "specialAttributes"));

        JsonObject? combat = ReadObject(root, "combat");
        Append(entries, "Initiative", ReadString(combat, "initiative"));
        Append(entries, "Armor", ReadScalar(combat, "armor"));

        return entries.ToArray();
    }

    public static IReadOnlyList<GearEntry> ReadGear(JsonObject? root)
        => ReadLabelValueEntries(root, "gear", "gear", "name", "label", fallbackKeys: ["cyberware", "bioware", "augmentation"]);

    public static IReadOnlyList<ArmorEntry> ReadArmor(JsonObject? root)
        => ReadLabelValueEntries(root, "armor", "armor", "name", "label")
            .Select(static item => new ArmorEntry(item.Label, item.Value))
            .ToArray();

    public static IReadOnlyList<WeaponEntry> ReadWeapons(JsonObject? root)
        => ReadLabelValueEntries(root, "weapons", "weapon", "name", "label")
            .Select(static item => new WeaponEntry(item.Label, item.Value))
            .ToArray();

    public static IReadOnlyList<ContactEntry> ReadContacts(JsonObject? root)
        => ReadLabelValueEntries(root, "contacts", "contact", "name", "label")
            .Select(static item => new ContactEntry(item.Label, item.Value))
            .ToArray();

    public static IReadOnlyList<NoteEntry> ReadNotes(JsonObject? root, string sectionId)
    {
        List<NoteEntry> entries = ReadLabelValueEntries(root, "notes", "note", "name", "label")
            .Select(static item => new NoteEntry(item.Label, item.Value))
            .ToList();

        if (entries.Count == 0)
        {
            foreach (string value in ReadStringArray(root, "rows"))
            {
                entries.Add(new NoteEntry(FormatSectionLabel(sectionId), value));
            }
        }

        return entries;
    }

    public static IReadOnlyList<PriorityChoice> ReadPriorityFacts(JsonObject? root, string sectionId)
    {
        List<PriorityChoice> entries = [];
        Append(entries, "Ruleset", FirstNonBlank(ReadString(root, "gameEdition"), ReadString(root, "ruleset"))?.ToUpperInvariant());
        Append(entries, "Build Method", FirstNonBlank(ReadString(root, "buildMethod"), ReadString(root, "buildmethod")));
        Append(entries, "Metatype", ReadString(root, "metatype"));
        Append(entries, "Priority", ReadString(root, "priority"));

        if (entries.Count == 0 && IsCreateSection(sectionId))
        {
            foreach (string value in ReadStringArray(root, "rows"))
            {
                entries.Add(new PriorityChoice("Workflow", value));
            }
        }

        return entries;
    }

    public static IReadOnlyList<PriorityChoice> ReadPrioritySummary(JsonObject? root, IReadOnlyList<PriorityChoice> priorityFacts)
    {
        List<PriorityChoice> entries = [];
        Append(entries, "Ruleset", FirstNonBlank(ReadString(root, "gameEdition"), ReadString(root, "ruleset"))?.ToUpperInvariant());
        Append(entries, "Build Method", FirstNonBlank(ReadString(root, "buildMethod"), ReadString(root, "buildmethod")));
        Append(entries, "Metatype", ReadString(root, "metatype"));
        Append(entries, "Priority", ReadString(root, "priority"));

        return entries.Count > 0 ? entries : priorityFacts.ToArray();
    }

    public static IReadOnlyList<AttributeEntry> ReadAttributes(JsonObject? root)
    {
        List<AttributeEntry> entries = [];
        if (ReadArray(root, "attributes") is { Count: > 0 } attributeArray)
        {
            foreach (JsonNode? node in attributeArray)
            {
                if (node is not JsonObject attribute)
                {
                    continue;
                }

                string? name = FirstNonBlank(ReadString(attribute, "name"), ReadString(attribute, "label"));
                string? value = FirstNonBlank(
                    ReadScalar(attribute, "totalValue"),
                    ReadScalar(attribute, "baseValue"),
                    ReadScalar(attribute, "value"),
                    ReadScalar(attribute, "base"));
                Append(entries, name, value);
            }
        }

        if (entries.Count == 0 && ReadObject(root, "attributes") is { } attributesObject)
        {
            foreach (string key in OrderedAttributes)
            {
                Append(entries, key, ReadScalar(attributesObject, key));
            }
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<SkillEntry> ReadSkills(JsonObject? root)
        => ReadLabelValueEntries(root, "skills", "skill", "name", "label")
            .Select(static item => new SkillEntry(item.Label, item.Value))
            .ToArray();

    public static IReadOnlyList<SpellEntry> ReadSpells(JsonObject? root)
        => ReadLabelValueEntries(root, "spells", "spell", "name", "label")
            .Select(static item => new SpellEntry(item.Label, item.Value))
            .ToArray();

    public static IReadOnlyList<GearEntry> ReadCreationGear(
        JsonObject? root,
        IReadOnlyList<GearEntry> gear,
        IReadOnlyList<ArmorEntry> armor,
        IReadOnlyList<WeaponEntry> weapons,
        string sectionId)
    {
        List<GearEntry> entries = [];
        entries.AddRange(gear);
        entries.AddRange(armor.Select(static item => new GearEntry(item.Label, item.Value)));
        entries.AddRange(weapons.Select(static item => new GearEntry(item.Label, item.Value)));

        if (entries.Count == 0 && IsGearSection(sectionId))
        {
            entries.AddRange(ReadStringArray(root, "rows").Select(value => new GearEntry("Selection", value)));
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<CareerSnapshotEntry> ReadFinalSummary(
        JsonObject? root,
        IReadOnlyList<PriorityChoice> prioritySummary,
        IReadOnlyList<CareerSnapshotEntry> snapshot)
    {
        List<CareerSnapshotEntry> entries = [];
        entries.AddRange(prioritySummary.Select(static item => new CareerSnapshotEntry(item.Label, item.Value)));

        foreach (CareerSnapshotEntry item in snapshot)
        {
            if (entries.Any(existing => string.Equals(existing.Label, item.Label, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            entries.Add(item);
            if (entries.Count >= 6)
            {
                break;
            }
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<GearCategoryEntry> ReadGearCategories(
        JsonObject? root,
        IReadOnlyList<GearEntry> gear,
        IReadOnlyList<ArmorEntry> armor,
        IReadOnlyList<WeaponEntry> weapons,
        string sectionId)
    {
        List<GearCategoryEntry> entries = [];
        entries.AddRange(gear.Select(static item => new GearCategoryEntry(item.Label, item.Value)));
        entries.AddRange(armor.Select(static item => new GearCategoryEntry(item.Label, item.Value)));
        entries.AddRange(weapons.Select(static item => new GearCategoryEntry(item.Label, item.Value)));

        if (entries.Count == 0 && IsGearSection(sectionId))
        {
            entries.AddRange(ReadStringArray(root, "rows").Select(value => new GearCategoryEntry("Category", value)));
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<GearFilterEntry> ReadFilters(JsonObject? root, string sectionId)
    {
        List<GearFilterEntry> entries = [];
        JsonObject? filters = ReadObject(root, "filters");
        if (filters is not null)
        {
            foreach ((string key, JsonNode? valueNode) in filters)
            {
                Append(entries, Title(key), DescribeNode(valueNode));
            }
        }

        Append(entries, "Category", ReadString(root, "category"));
        Append(entries, "Source", ReadString(root, "source"));
        Append(entries, "Search", ReadString(root, "search"));

        if (entries.Count == 0 && IsGearSection(sectionId))
        {
            entries.AddRange(ReadStringArray(root, "rows").Select(value => new GearFilterEntry("Filter", value)));
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<GearDetailEntry> ReadGearDetails(
        JsonObject? root,
        IReadOnlyList<GearEntry> gear,
        IReadOnlyList<ArmorEntry> armor,
        IReadOnlyList<WeaponEntry> weapons,
        string sectionId)
    {
        List<GearDetailEntry> entries = [];
        Append(entries, "Availability", ReadScalar(root, "availability"));
        Append(entries, "Cost", FirstNonBlank(ReadScalar(root, "cost"), ReadScalar(root, "nuyen")));
        Append(entries, "License", ReadString(root, "license"));
        Append(entries, "Sourcebook", ReadString(root, "sourcebook"));

        if (entries.Count == 0)
        {
            IEnumerable<string> sourceItems = gear.Select(static item => item.Value)
                .Concat(armor.Select(static item => item.Value))
                .Concat(weapons.Select(static item => item.Value));
            entries.AddRange(sourceItems.Select(value => new GearDetailEntry("Detail", value)));
        }

        if (entries.Count == 0 && IsGearSection(sectionId))
        {
            entries.AddRange(ReadStringArray(root, "rows").Select(value => new GearDetailEntry("Detail", value)));
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<BrowseEntry> ReadIndexRows(JsonObject? root, string sectionId)
    {
        List<BrowseEntry> entries = [];
        foreach (string value in ReadStringArray(root, "rows"))
        {
            entries.Add(new BrowseEntry(FormatSectionLabel(sectionId), value));
        }

        string? sectionValue = FirstNonBlank(ReadString(root, "sectionId"), ReadString(root, "section"));
        if (!string.IsNullOrWhiteSpace(sectionValue))
        {
            entries.Add(new BrowseEntry("Section", sectionValue));
        }

        string? rulesetValue = FirstNonBlank(ReadString(root, "gameEdition"), ReadString(root, "ruleset"))?.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(rulesetValue))
        {
            entries.Add(new BrowseEntry("Ruleset", rulesetValue));
        }

        return entries.ToArray();
    }

    public static IReadOnlyList<SettingEntry> ReadSettings(JsonObject? root, string sectionId)
    {
        List<SettingEntry> entries = [];
        if (ReadObject(root, "settings") is { } settingsObject)
        {
            foreach ((string key, JsonNode? valueNode) in settingsObject)
            {
                Append(entries, Title(key), DescribeNode(valueNode));
            }
        }

        Append(entries, "Ruleset", FirstNonBlank(ReadString(root, "gameEdition"), ReadString(root, "ruleset"))?.ToUpperInvariant());
        Append(entries, "Language", ReadString(root, "language"));
        Append(entries, "Version", ReadString(root, "version"));

        if (entries.Count == 0 && IsSettingsSection(sectionId))
        {
            entries.AddRange(ReadStringArray(root, "rows").Select(value => new SettingEntry("Setting", value)));
        }

        return entries.ToArray();
    }

    private static IReadOnlyList<GearEntry> ReadLabelValueEntries(
        JsonObject? root,
        string arrayProperty,
        string singularLabel,
        string preferredNameProperty,
        string fallbackNameProperty,
        params IReadOnlyList<string> fallbackKeys)
    {
        List<GearEntry> entries = [];
        if (ReadArray(root, arrayProperty) is { Count: > 0 } items)
        {
            foreach (JsonNode? node in items)
            {
                Append(entries, EntryFromNode(node, singularLabel, preferredNameProperty, fallbackNameProperty));
            }
        }

        if (entries.Count == 0 && ReadObject(root, arrayProperty) is { } obj)
        {
            foreach ((string key, JsonNode? valueNode) in obj)
            {
                Append(entries, new GearEntry(Title(key), DescribeNode(valueNode)));
            }
        }

        foreach (string fallbackKey in fallbackKeys)
        {
            string? value = ReadScalar(root, fallbackKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                entries.Add(new GearEntry(Title(fallbackKey), value));
            }
        }

        return Deduplicate(entries);
    }

    private static GearEntry? EntryFromNode(JsonNode? node, string singularLabel, string preferredNameProperty, string fallbackNameProperty)
    {
        return node switch
        {
            JsonObject obj => new GearEntry(
                FirstNonBlank(ReadString(obj, preferredNameProperty), ReadString(obj, fallbackNameProperty), Title(singularLabel)) ?? Title(singularLabel),
                FirstNonBlank(
                    ReadScalar(obj, "summary"),
                    ReadScalar(obj, "value"),
                    ReadScalar(obj, "rating"),
                    ReadScalar(obj, "description"),
                    DescribeObject(obj)) ?? string.Empty),
            JsonValue value => new GearEntry(Title(singularLabel), DescribeNode(value)),
            _ => null
        };
    }

    private static string? DescribeObject(JsonObject obj)
    {
        List<string> parts = [];
        foreach (string key in new[] { "value", "summary", "rating", "cost", "availability", "description" })
        {
            string? value = ReadScalar(obj, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{Title(key)} {value}");
            }
        }

        return parts.Count > 0 ? string.Join(" • ", parts) : null;
    }

    private static IReadOnlyList<GearEntry> Deduplicate(IEnumerable<GearEntry> entries)
        => entries
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Value))
            .GroupBy(static entry => $"{entry.Label}|{entry.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

    private static void Append(List<CareerSnapshotEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new CareerSnapshotEntry(label, value));
        }
    }

    private static void Append(List<AdvancementEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new AdvancementEntry(label, value));
        }
    }

    private static void Append(List<PriorityChoice> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new PriorityChoice(label, value));
        }
    }

    private static void Append(List<AttributeEntry> entries, string? label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new AttributeEntry(label, value));
        }
    }

    private static void Append(List<GearFilterEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new GearFilterEntry(label, value));
        }
    }

    private static void Append(List<GearDetailEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new GearDetailEntry(label, value));
        }
    }

    private static void Append(List<SettingEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new SettingEntry(label, value));
        }
    }

    private static void Append(List<NoteEntry> entries, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            entries.Add(new NoteEntry(label, value));
        }
    }

    private static void Append(List<GearEntry> entries, GearEntry? entry)
    {
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.Value))
        {
            entries.Add(entry);
        }
    }

    public static string? ReadString(JsonObject? node, string propertyName)
        => node?[propertyName]?.GetValue<string?>();

    public static string? ReadScalar(JsonObject? node, string propertyName)
        => node?[propertyName] switch
        {
            null => null,
            JsonValue value => value.ToJsonString().Trim('"'),
            JsonObject obj => DescribeObject(obj),
            JsonArray array => string.Join(", ", array.Select(DescribeNode).Where(static item => !string.IsNullOrWhiteSpace(item))),
            _ => node[propertyName]?.ToJsonString()
        };

    public static JsonObject? ReadObject(JsonObject? node, string propertyName)
        => node?[propertyName] as JsonObject;

    public static JsonArray? ReadArray(JsonObject? node, string propertyName)
        => node?[propertyName] as JsonArray;

    public static IReadOnlyList<string> ReadStringArray(JsonObject? node, string propertyName)
        => ReadArray(node, propertyName)?
            .Select(DescribeNode)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray()
            ?? Array.Empty<string>();

    public static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static string DescribeNode(JsonNode? node)
        => node switch
        {
            null => string.Empty,
            JsonValue value => value.ToJsonString().Trim('"'),
            JsonObject obj => DescribeObject(obj) ?? string.Join(", ", obj.Select(pair => $"{Title(pair.Key)} {DescribeNode(pair.Value)}").Where(static value => !string.IsNullOrWhiteSpace(value))),
            JsonArray array => string.Join(", ", array.Select(DescribeNode).Where(static value => !string.IsNullOrWhiteSpace(value))),
            _ => node.ToJsonString()
        };

    private static string Title(string key)
        => key switch
        {
            "gameEdition" => "Ruleset",
            "buildMethod" => "Build Method",
            "streetCred" => "Street Cred",
            "totalKarma" => "Total Karma",
            "publicAwareness" => "Public Awareness",
            _ => string.Concat(key[..1].ToUpperInvariant(), key[1..])
        };

    private static string FormatSectionLabel(string sectionId)
        => string.IsNullOrWhiteSpace(sectionId) ? "Section" : Title(sectionId.Replace("_", " ", StringComparison.Ordinal));

    private static bool IsCreateSection(string sectionId)
        => sectionId.Contains("create", StringComparison.OrdinalIgnoreCase)
            || sectionId.Contains("priority", StringComparison.OrdinalIgnoreCase)
            || sectionId.Contains("metatype", StringComparison.OrdinalIgnoreCase);

    private static bool IsGearSection(string sectionId)
        => sectionId.Contains("gear", StringComparison.OrdinalIgnoreCase);

    private static bool IsSettingsSection(string sectionId)
        => sectionId.Contains("settings", StringComparison.OrdinalIgnoreCase)
            || sectionId.Contains("global", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] OrderedAttributes =
    [
        "Body",
        "Agility",
        "Reaction",
        "Strength",
        "Willpower",
        "Logic",
        "Intuition",
        "Charisma",
        "Edge",
        "Magic",
        "Resonance",
        "Essence",
        "Initiative"
    ];
}
