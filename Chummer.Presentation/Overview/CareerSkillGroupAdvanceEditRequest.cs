using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerSkillGroupAdvanceEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> SkillGroups,
    int OmittedSkillGroupCount);

public sealed record CareerSkillGroupAdvanceRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerSkillGroupAdvanceQuote ExpectedSkillGroup,
    string ExpectedRuleDigest,
    bool Confirmed,
    Guid ExpenseId,
    DateTime ExpenseDateLocal);

internal static class CareerSkillGroupAdvanceEditorProjector
{
    private const int DefaultMaximumActiveSkillRating = 12;

    public static CareerSkillGroupAdvanceEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for skill-group advancement.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before advancing a skill group.");
        }

        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> groups, int omitted) =
            ProjectState(xml, settingsCatalogJson, sourceDataResolver);
        return new CareerSkillGroupAdvanceEditorState(
            workspaceId,
            contentRevision,
            groups,
            omitted);
    }

    internal static (
        IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> SkillGroups,
        int OmittedSkillGroupCount) ProjectState(
        string xml,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = RequireCharacterRoot(document);
        if (!ReadRequiredBool(root, "created"))
        {
            throw new InvalidOperationException(
                "Skill-group advancement is available only for career runners.");
        }

        ICharacterSourceDataContext sourceContext = sourceDataResolver?
            .TryCreateContext(xml)
            ?? throw new InvalidOperationException(
                "The exact Chummer5 skill source profile is unavailable.");
        XElement settings = ResolveExactSettings(root, settingsCatalogJson);
        CharacterCareerSkillGroupAdvanceSettings rules = new(
            ReadRequiredNonNegativeInt(settings, "karmacost", "karmanewskillgroup"),
            ReadRequiredNonNegativeInt(settings, "karmacost", "karmaimproveskillgroup"));
        int maximumRating = ReadOptionalNonNegativeInt(
            settings,
            "maxskillrating",
            DefaultMaximumActiveSkillRating);
        bool usePointsOnBrokenGroups = ReadOptionalBool(
            settings,
            "usepointsonbrokengroups",
            false);
        int availableKarma = ReadRequiredNonNegativeInt(root, "karma");
        XElement newSkills = RequireSingle(
            root,
            "newskills",
            "The saved runner must have one <newskills> container.");
        XElement skillContainer = RequireSingle(
            newSkills,
            "skills",
            "The saved runner must have one active <skills> container.");
        XElement groupContainer = RequireSingle(
            newSkills,
            "groups",
            "The saved runner must have one <groups> container.");
        XElement[] improvementsContainers = root.Elements("improvements").Take(2).ToArray();
        if (improvementsContainers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <improvements> containers.");
        }
        XElement? improvements = improvementsContainers.SingleOrDefault();
        string rawRuleState = settings.ToString(SaveOptions.DisableFormatting)
            + "\n"
            + (improvements?.ToString(SaveOptions.DisableFormatting) ?? "<improvements />");

        XElement[] savedGroups = groupContainer.Elements("group").ToArray();
        HashSet<Guid> groupIds = [];
        HashSet<string> groupNames = new(StringComparer.Ordinal);
        foreach (XElement savedGroup in savedGroups)
        {
            Guid id = ReadRequiredGuid(savedGroup, "id", "A skill group");
            string name = ReadRequiredText(savedGroup, "name", "A skill group");
            if (!groupIds.Add(id) || !groupNames.Add(name))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate skill-group identities or names.");
            }
        }

        XElement[] savedSkills = skillContainer.Elements("skill").ToArray();
        HashSet<Guid> skillIds = [];
        foreach (XElement savedSkill in savedSkills)
        {
            Guid id = ReadRequiredGuid(savedSkill, "guid", "An active skill");
            if (!skillIds.Add(id))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate active-skill instance GUIDs.");
            }
        }

        List<CharacterCareerSkillGroupAdvanceQuote> projected = [];
        int omitted = 0;
        foreach (XElement savedGroup in savedGroups)
        {
            if (!TryProjectGroup(
                    savedGroup,
                    savedSkills,
                    availableKarma,
                    maximumRating,
                    usePointsOnBrokenGroups,
                    rules,
                    rawRuleState,
                    improvements,
                    sourceContext,
                    out CharacterCareerSkillGroupAdvanceQuote quote))
            {
                omitted++;
                continue;
            }
            projected.Add(quote);
        }

        return (
            projected
                .OrderBy(static candidate => candidate.Name, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Identity.InternalId)
                .ToArray(),
            omitted);
    }

    private static bool TryProjectGroup(
        XElement savedGroup,
        IReadOnlyList<XElement> savedSkills,
        int availableKarma,
        int maximumRating,
        bool usePointsOnBrokenGroups,
        CharacterCareerSkillGroupAdvanceSettings rules,
        string rawRuleState,
        XElement? improvements,
        ICharacterSourceDataContext sourceContext,
        out CharacterCareerSkillGroupAdvanceQuote quote)
    {
        quote = null!;
        Guid groupId = ReadRequiredGuid(savedGroup, "id", "A skill group");
        string groupName = ReadRequiredText(savedGroup, "name", "A skill group");
        int groupBase = ReadRequiredNonNegativeInt(savedGroup, "base");
        int groupKarma = ReadRequiredNonNegativeInt(savedGroup, "karma");
        bool broken = ReadRequiredBool(savedGroup, "isbroken");

        List<CharacterCareerSkillGroupMember> members = [];
        List<string> rawSources = [];
        foreach (XElement savedSkill in savedSkills)
        {
            if (ReadRequiredBool(savedSkill, "isknowledge"))
            {
                return false;
            }
            Guid skillId = ReadRequiredGuid(savedSkill, "guid", "An active skill");
            Guid sourceId = ReadRequiredGuid(savedSkill, "suid", "An active skill");
            if (!sourceContext.TryResolveActiveSkillSource(
                    sourceId.ToString("D"),
                    out CharacterActiveSkillSource source)
                || !Guid.TryParse(source.SourceSkillId, out Guid resolvedSourceId)
                || resolvedSourceId != sourceId)
            {
                return false;
            }
            if (!string.Equals(source.SkillGroup, groupName, StringComparison.Ordinal))
            {
                continue;
            }
            string savedCategory = ReadRequiredText(savedSkill, "skillcategory", "An active skill");
            if (!string.Equals(savedCategory, source.SkillCategory, StringComparison.Ordinal)
                || source.IsExotic
                || source.RequiresGroundMovement
                || source.RequiresSwimMovement
                || source.RequiresFlyMovement
                || HasUnsupportedRatingAuthority(improvements, source.Name, groupName))
            {
                return false;
            }

            int skillBasePoints = ReadRequiredNonNegativeInt(savedSkill, "base");
            int skillKarmaPoints = ReadRequiredNonNegativeInt(savedSkill, "karma");
            int effectiveBase = groupBase > 0 && !usePointsOnBrokenGroups
                ? groupBase
                : checked(groupBase + skillBasePoints);
            int effectiveKarma = checked(groupKarma + skillKarmaPoints);
            int totalBaseRating = checked(
                Math.Min(effectiveBase, maximumRating)
                + Math.Min(effectiveKarma, maximumRating));
            if (totalBaseRating > maximumRating)
            {
                return false;
            }

            bool enabled = !IsSkillDisabled(improvements, source.Name, savedCategory);
            members.Add(new CharacterCareerSkillGroupMember(
                skillId,
                totalBaseRating,
                enabled,
                savedCategory));
            rawSources.Add(source.RawSourceXml);
        }
        if (members.Count == 0)
        {
            return false;
        }

        string[] categories = members
            .Select(static member => member.SkillCategory)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bool disabled = IsGroupDisabled(improvements, groupName, categories);
        if (!TryResolveCostModifiers(
                improvements,
                groupName,
                categories,
                out IReadOnlyList<CharacterCareerSkillGroupKarmaModifier> modifiers))
        {
            return false;
        }

        CharacterCareerSkillGroupAdvanceInput input = new(
            Identity: new CharacterCareerSkillGroupIdentity(groupId),
            Created: true,
            RulesetId: CharacterCareerSkillGroupAdvanceRules.RulesetId,
            TargetOwnedByCharacter: true,
            MemberProjectionIsExact: true,
            Name: groupName,
            BasePoints: groupBase,
            KarmaPoints: groupKarma,
            RatingMaximum: maximumRating,
            AvailableKarma: availableKarma,
            Disabled: disabled,
            Broken: broken,
            Settings: rules,
            Members: members,
            Modifiers: modifiers,
            RawSourceState: string.Join("\n", rawSources.OrderBy(static value => value, StringComparer.Ordinal)),
            RawRuleState: rawRuleState);
        return CharacterCareerSkillGroupAdvanceRules.TryCreateQuote(input, out quote);
    }

    private static bool HasUnsupportedRatingAuthority(
        XElement? improvements,
        string skillName,
        string groupName)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledInCareer(improvement)
                && ReadOptionalText(improvement, "improvementttype", string.Empty) is
                    "Skill" or "SkillBase" or "SkillLevel" or "SkillGroup" or "SkillGroupLevel"
                && (TargetMatches(
                        ReadOptionalText(improvement, "improvedname", string.Empty),
                        skillName)
                    || TargetMatches(
                        ReadOptionalText(improvement, "improvedname", string.Empty),
                        groupName)));

    private static bool IsSkillDisabled(
        XElement? improvements,
        string skillName,
        string category)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledInCareer(improvement)
                && ((ReadOptionalText(improvement, "improvementttype", string.Empty) == "SkillDisable"
                        && TargetMatches(
                            ReadOptionalText(improvement, "improvedname", string.Empty),
                            skillName))
                    || (ReadOptionalText(improvement, "improvementttype", string.Empty) == "SkillCategoryDisable"
                        && TargetMatches(
                            ReadOptionalText(improvement, "improvedname", string.Empty),
                            category))));

    private static bool IsGroupDisabled(
        XElement? improvements,
        string groupName,
        IReadOnlyCollection<string> categories)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledInCareer(improvement)
                && ((ReadOptionalText(improvement, "improvementttype", string.Empty) == "SkillGroupDisable"
                        && TargetMatches(
                            ReadOptionalText(improvement, "improvedname", string.Empty),
                            groupName))
                    || (ReadOptionalText(improvement, "improvementttype", string.Empty) == "SkillGroupCategoryDisable"
                        && categories.Contains(
                            ReadOptionalText(improvement, "improvedname", string.Empty),
                            StringComparer.Ordinal))));

    private static bool TryResolveCostModifiers(
        XElement? improvements,
        string groupName,
        IReadOnlyCollection<string> categories,
        out IReadOnlyList<CharacterCareerSkillGroupKarmaModifier> modifiers)
    {
        List<CharacterCareerSkillGroupKarmaModifier> result = [];
        int ordinal = 0;
        foreach (XElement improvement in improvements?.Elements("improvement") ?? [])
        {
            string type = ReadOptionalText(improvement, "improvementttype", string.Empty);
            if (!TryMapModifierKind(type, out CharacterCareerSkillGroupKarmaModifierKind kind)
                || !IsEnabledCareerImprovement(improvement))
            {
                ordinal++;
                continue;
            }

            string target = ReadOptionalText(improvement, "improvedname", string.Empty);
            bool targetMatches = kind is CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCost
                    or CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCostMultiplier
                ? TargetMatches(target, groupName)
                : string.IsNullOrEmpty(target) || categories.Contains(target, StringComparer.Ordinal);
            if (!targetMatches)
            {
                ordinal++;
                continue;
            }
            if (!string.IsNullOrEmpty(ReadOptionalText(improvement, "unique", string.Empty))
                || !TryReadNonNegativeInt(improvement, "min", 0, out int minimum)
                || !TryReadNonNegativeInt(improvement, "max", 0, out int maximum)
                || maximum != 0 && maximum < minimum
                || !TryReadDecimal(improvement, "val", out decimal value))
            {
                modifiers = [];
                return false;
            }

            string raw = ordinal.ToString(CultureInfo.InvariantCulture)
                + "\0"
                + improvement.ToString(SaveOptions.DisableFormatting);
            string identity = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
                .ToLowerInvariant();
            result.Add(new CharacterCareerSkillGroupKarmaModifier(
                identity,
                kind,
                target,
                minimum,
                maximum,
                value));
            ordinal++;
        }

        modifiers = result;
        return true;
    }

    private static bool TryMapModifierKind(
        string type,
        out CharacterCareerSkillGroupKarmaModifierKind kind)
    {
        switch (type)
        {
            case "SkillGroupKarmaCost":
                kind = CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCost;
                return true;
            case "SkillGroupKarmaCostMultiplier":
                kind = CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCostMultiplier;
                return true;
            case "SkillGroupCategoryKarmaCost":
                kind = CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCategoryCost;
                return true;
            case "SkillGroupCategoryKarmaCostMultiplier":
                kind = CharacterCareerSkillGroupKarmaModifierKind.SkillGroupCategoryCostMultiplier;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool IsEnabledCareerImprovement(XElement improvement)
        => IsEnabledInCareer(improvement)
            && !ReadOptionalBool(improvement, "addtorating", false);

    private static bool IsEnabledInCareer(XElement improvement)
    {
        string condition = ReadOptionalText(improvement, "condition", string.Empty);
        return ReadOptionalBool(improvement, "enabled", true)
            && (string.IsNullOrEmpty(condition)
                || string.Equals(condition, "career", StringComparison.Ordinal));
    }

    private static bool TargetMatches(string target, string expected)
        => string.IsNullOrEmpty(target)
            || string.Equals(target, expected, StringComparison.Ordinal);

    private static XElement ResolveExactSettings(XElement root, string? settingsCatalogJson)
    {
        string settingsId = ReadRequiredText(root, "settings", "The saved runner");
        Chummer5CharacterSettingsCatalog catalog =
            Chummer5CharacterSettingsProfiles.ParseCatalog(settingsCatalogJson);
        Chummer5CharacterSettingsProfile[] matches = catalog.Profiles
            .Where(candidate => string.Equals(candidate.Id, settingsId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "The saved runner's exact Chummer5 settings profile is unavailable.");
        }
        XElement settings = XElement.Parse(matches[0].Xml, LoadOptions.PreserveWhitespace);
        return settings.Name == XName.Get("settings")
            ? settings
            : throw new InvalidOperationException(
                "The active Chummer5 settings profile has an invalid root node.");
    }

    internal static XElement RequireCharacterRoot(XDocument document)
        => document.Root is { } root && root.Name == XName.Get("character")
            ? root
            : throw new InvalidOperationException(
                "Workspace XML must use <character> as the root node.");

    internal static XElement RequireSingle(XElement parent, string name, string error)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(error);
    }

    internal static bool ReadRequiredBool(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    internal static Guid ReadRequiredGuid(XElement parent, string name, string owner)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParse(values[0].Value.Trim(), out Guid value)
            || value == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{owner} has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    internal static string ReadRequiredText(XElement parent, string name, string owner)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value))
        {
            throw new InvalidOperationException(
                $"{owner} has an invalid or duplicate <{name}> value.");
        }
        return values[0].Value.Trim();
    }

    internal static int ReadRequiredNonNegativeInt(XElement parent, string name)
        => TryReadNonNegativeInt(parent, name, fallback: -1, out int value) && value >= 0
            ? value
            : throw new InvalidOperationException(
                $"The saved runner has an invalid or duplicate <{name}> value.");

    private static int ReadRequiredNonNegativeInt(
        XElement parent,
        string containerName,
        string name)
    {
        XElement container = RequireSingle(
            parent,
            containerName,
            $"The active settings profile must have one <{containerName}> container.");
        return ReadRequiredNonNegativeInt(container, name);
    }

    private static int ReadOptionalNonNegativeInt(XElement parent, string name, int fallback)
        => TryReadNonNegativeInt(parent, name, fallback, out int value)
            ? value
            : throw new InvalidOperationException(
                $"The active settings profile has an invalid or duplicate <{name}> value.");

    private static bool TryReadNonNegativeInt(
        XElement parent,
        string name,
        int fallback,
        out int value)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        value = fallback;
        if (values.Length == 0 || values.Length == 1 && string.IsNullOrWhiteSpace(values[0].Value))
        {
            return fallback >= 0;
        }
        return values.Length == 1
            && int.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
            && value >= 0;
    }

    private static bool TryReadDecimal(XElement parent, string name, out decimal value)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        value = 0m;
        return values.Length == 1
            && decimal.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out value);
    }

    private static bool ReadOptionalBool(XElement parent, string name, bool fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0 || values.Length == 1 && string.IsNullOrWhiteSpace(values[0].Value))
        {
            return fallback;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static string ReadOptionalText(XElement parent, string name, string fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => fallback,
            1 => values[0].Value.Trim(),
            _ => throw new InvalidOperationException(
                $"The saved runner has duplicate <{name}> values.")
        };
    }
}
