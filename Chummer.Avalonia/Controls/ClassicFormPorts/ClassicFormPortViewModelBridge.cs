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
        IReadOnlyList<SectionRowDisplayItem> rows = state.Rows;
        IReadOnlyList<string> actions = ClassicFormPortSurfaceControl.CollectActionLabelsForBridge(state);

        return new ClassicFormPortViewModels(
            Career: new ClassicCareerPortViewModel(
                Snapshot:
                [
                    new("Name", FindValue(rows, "name")),
                    new("Lifestyle", FindValue(rows, "lifestyle")),
                    new("Build Method", FindValue(rows, "buildMethod", "settings")),
                    new("Street Cred", FindValue(rows, "streetCred", "street")),
                    new("Essence", FindValue(rows, "essence")),
                    new("Karma", FindValue(rows, "karma")),
                    new("Nuyen", FindValue(rows, "nuyen")),
                ],
                Advancement: ToLineItems(MatchRows(rows, 12, "karma", "xp", "nextlevel", "improvement", "advancement", "metatype", "special")),
                Gear: ToLineItems(MatchRows(rows, 12, "gear", "cyberware", "mod")),
                Armor: ToLineItems(MatchRows(rows, 12, "armor", "plate", "clothing")),
                Weapons: ToLineItems(MatchRows(rows, 12, "weapon", "guns", "firearm", "blade")),
                Contacts: ToLineItems(MatchRows(rows, 12, "contact", "ally", "familiar")),
                Notes: ToLineItems(MatchRows(rows, 12, "note", "comment", "memo")),
                Actions: actions.Select(action => new ClassicPortLineItem("Action", action)).ToArray()),
            Create: new ClassicCreatePortViewModel(
                Priorities: MatchRows(rows, 10, "priority", "metatype", "resource").Select(static row => row.DisplayPath).ToArray(),
                PrioritySummary:
                [
                    new("Ruleset", FindValue(rows, "gameEdition")),
                    new("Build", FindValue(rows, "buildMethod")),
                    new("Metatype", FindValue(rows, "metatype")),
                    new("Priority Path", FindValue(rows, "priority")),
                ],
                Attributes: ToLineItems(MatchRows(rows, 20, "body", "agility", "reaction", "strength", "willpower", "logic", "intuition", "charisma", "edge", "magic", "resonance")),
                Skills: ToLineItems(MatchRows(rows, 20, "skill", "knowledge", "language")),
                Gear: ToLineItems(MatchRows(rows, 15, "gear", "armor", "weapon", "ranged", "melee")),
                Spells: ToLineItems(MatchRows(rows, 10, "spell", "magic", "tradition")),
                FinalSummary:
                [
                    new("Build Method", FindValue(rows, "buildMethod")),
                    new("Metatype", FindValue(rows, "metatype")),
                    new("Primary Source", FindValue(rows, "settings")),
                ],
                Actions: actions.Select(action => new ClassicPortLineItem("Action", action)).ToArray()),
            Gear: BuildGear(rows, actions),
            Index: BuildIndex(rows, actions, snapshot),
            Settings: BuildSettings(rows, actions, snapshot));
    }

    private static ClassicGearPortViewModel BuildGear(IReadOnlyList<SectionRowDisplayItem> rows, IReadOnlyList<string> actions)
    {
        IReadOnlyList<SectionRowDisplayItem> categoryRows = MatchRows(rows, 18, "category", "gear", "weapon", "armor", "cyberware");
        return new ClassicGearPortViewModel(
            Categories: categoryRows.Select(static row => row.DisplayPath).ToArray(),
            CategoryRows: ToLineItems(categoryRows),
            Filters:
            [
                new("Filter Group", FindValue(rows, "filter")),
                new("Search Text", FindValue(rows, "search")),
                new("Sort", FindValue(rows, "sort")),
                .. actions.Select(action => new ClassicPortLineItem("Action", action)),
            ],
            Details: ToLineItems(MatchRows(rows, 16, "detail", "quality", "availability", "cost")),
            PurchaseActions: actions.Select(action => new ClassicPortLineItem("Purchase Action", action)).ToArray());
    }

    private static ClassicIndexPortViewModel BuildIndex(IReadOnlyList<SectionRowDisplayItem> rows, IReadOnlyList<string> actions, ClassicFormDesignerSnapshot snapshot)
    {
        IReadOnlyList<SectionRowDisplayItem> browseRows = MatchRows(rows, 12);
        return new ClassicIndexPortViewModel(
            BrowseLabels: browseRows.Select(static row => row.DisplayPath).ToArray(),
            BrowseRows: ToLineItems(browseRows),
            SearchActions: actions.Select(action => new ClassicPortLineItem("Search Action", action)).ToArray(),
            SourceFacts: ClassicFormPortSurfaceControl.DesignerChromeFactsForBridge(snapshot, 12));
    }

    private static ClassicSettingsPortViewModel BuildSettings(IReadOnlyList<SectionRowDisplayItem> rows, IReadOnlyList<string> actions, ClassicFormDesignerSnapshot snapshot)
    {
        IReadOnlyList<SectionRowDisplayItem> globalRows = MatchRows(rows, 32, "settings", "global", "language", "ruleset", "version")
            .Distinct(new SectionRowComparer())
            .ToArray();

        return new ClassicSettingsPortViewModel(
            GlobalLabels: globalRows.Select(static row => row.DisplayPath).ToArray(),
            GlobalRows: ToLineItems(globalRows),
            CustomDataActions: actions.Select(action => new ClassicPortLineItem("Action", action)).ToArray(),
            GitHubIssueChannels: ClassicFormPortSurfaceControl.MergeLegacyTabsForBridge(["Global", "Custom Data", "GitHub Issues", "Plugins"], snapshot)
                .Select(label => new ClassicPortLineItem("Issue Channel", label))
                .ToArray(),
            Plugins: snapshot.ContextMenus.Select(menu => new ClassicPortLineItem("Plugin", menu)).ToArray());
    }

    private static IReadOnlyList<ClassicPortLineItem> ToLineItems(IEnumerable<SectionRowDisplayItem> rows)
        => rows.Select(row => new ClassicPortLineItem(row.DisplayPath, row.DisplayValue)).ToArray();

    private static IReadOnlyList<SectionRowDisplayItem> MatchRows(
        IReadOnlyList<SectionRowDisplayItem> rows,
        int maxCount,
        params string[] pathTokens)
    {
        IEnumerable<SectionRowDisplayItem> filtered = rows;
        if (pathTokens.Length > 0)
        {
            filtered = rows.Where(row => pathTokens.Any(token =>
                row.Path.Contains(token, StringComparison.OrdinalIgnoreCase)
                || row.DisplayPath.Contains(token, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered.Take(maxCount).ToArray();
    }

    private static string FindValue(IReadOnlyList<SectionRowDisplayItem> rows, params string[] pathTokens)
    {
        foreach (string token in pathTokens)
        {
            SectionRowDisplayItem? row = rows.FirstOrDefault(candidate =>
                candidate.Path.Contains(token, StringComparison.OrdinalIgnoreCase)
                || candidate.DisplayPath.Contains(token, StringComparison.OrdinalIgnoreCase));
            if (row is not null && !string.IsNullOrWhiteSpace(row.DisplayValue))
            {
                return row.DisplayValue;
            }
        }

        return "n/a";
    }

    private sealed class SectionRowComparer : IEqualityComparer<SectionRowDisplayItem>
    {
        public bool Equals(SectionRowDisplayItem? x, SectionRowDisplayItem? y)
            => ReferenceEquals(x, y)
               || (x is not null
                   && y is not null
                   && string.Equals(x.Path, y.Path, StringComparison.Ordinal)
                   && string.Equals(x.DisplayPath, y.DisplayPath, StringComparison.Ordinal)
                   && string.Equals(x.DisplayValue, y.DisplayValue, StringComparison.Ordinal));

        public int GetHashCode(SectionRowDisplayItem obj)
            => HashCode.Combine(obj.Path, obj.DisplayPath, obj.DisplayValue);
    }
}
