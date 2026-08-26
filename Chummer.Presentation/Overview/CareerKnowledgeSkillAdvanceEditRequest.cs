using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerKnowledgeSkillAdvanceEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> Skills,
    int OmittedSkillCount,
    IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceReceipt> RecoverableReceipts,
    int OmittedReceiptCount);

public sealed record CareerKnowledgeSkillAdvanceRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerKnowledgeSkillAdvanceQuote ExpectedSkill,
    string ExpectedCharacterRevision,
    string ExpectedLogicalRevision,
    string ExpectedSourceRevision,
    string ExpectedRuleDigest,
    bool Confirmed,
    Guid ExpenseId,
    DateTime ExpenseDateLocal);

public sealed record CareerKnowledgeSkillAdvanceMutationResult(
    string Xml,
    CharacterCareerKnowledgeSkillAdvanceReceipt Receipt);

internal static class CareerKnowledgeSkillAdvanceEditorProjector
{
    private const int DefaultMaximumKnowledgeSkillRating = 12;

    public static CareerKnowledgeSkillAdvanceEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for knowledge-skill advancement.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before advancing a knowledge or language skill.");
        }

        (IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> skills, int omitted) =
            ProjectState(xml, settingsCatalogJson, sourceDataResolver);
        CareerKnowledgeSkillAdvanceMutation.ReceiptRecovery recovery =
            CareerKnowledgeSkillAdvanceMutation.RecoverReceipts(xml, skills);
        return new CareerKnowledgeSkillAdvanceEditorState(
            workspaceId,
            contentRevision,
            skills,
            omitted,
            recovery.Receipts,
            recovery.OmittedCount);
    }

    internal static (
        IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> Skills,
        int OmittedSkillCount) ProjectState(
        string xml,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(document);
        bool created = CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(root, "created");
        ICharacterSourceDataContext sourceContext = sourceDataResolver?.TryCreateContext(xml)
            ?? throw new InvalidOperationException(
                "The exact Chummer5 knowledge-skill source profile is unavailable.");
        XElement settings = ResolveExactSettings(root, settingsCatalogJson);
        XElement karmaCost = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            settings,
            "karmacost",
            "The active settings profile must have one <karmacost> container.");
        CharacterCareerKnowledgeSkillAdvanceSettings rules = new(
            CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(
                karmaCost,
                "karmanewknowledgeskill"),
            CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(
                karmaCost,
                "karmaimproveknowledgeskill"));
        int maximumRating = ReadOptionalNonNegativeInt(
            settings,
            "maxknowledgeskillrating",
            DefaultMaximumKnowledgeSkillRating);
        int availableKarma = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredNonNegativeInt(root, "karma");

        XElement newSkills = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            root,
            "newskills",
            "The saved runner must have one <newskills> container.");
        XElement knowledgeContainer = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            newSkills,
            "knoskills",
            "The saved runner must have one knowledge <knoskills> container.");
        XElement[] improvementContainers = root.Elements("improvements").Take(2).ToArray();
        if (improvementContainers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <improvements> containers.");
        }
        XElement? improvements = improvementContainers.SingleOrDefault();
        string rawRuleState = settings.ToString(SaveOptions.DisableFormatting)
            + "\n"
            + (improvements?.ToString(SaveOptions.DisableFormatting) ?? "<improvements />");
        string rawCharacterState = CanonicalCharacterState(root);

        XElement[] savedSkills = knowledgeContainer.Elements("skill").ToArray();
        HashSet<Guid> instanceIds = [];
        foreach (XElement savedSkill in savedSkills)
        {
            Guid id = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
                savedSkill,
                "guid",
                "A knowledge skill");
            if (!instanceIds.Add(id))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate knowledge-skill instance GUIDs.");
            }
        }

        List<CharacterCareerKnowledgeSkillAdvanceQuote> projected = [];
        int omitted = 0;
        foreach (XElement savedSkill in savedSkills)
        {
            if (!TryProjectSkill(
                    savedSkill,
                    created,
                    availableKarma,
                    maximumRating,
                    rules,
                    rawCharacterState,
                    rawRuleState,
                    improvements,
                    sourceContext,
                    out CharacterCareerKnowledgeSkillAdvanceQuote quote))
            {
                omitted++;
                continue;
            }
            projected.Add(quote);
        }

        return (
            projected
                .OrderBy(static candidate => candidate.SkillType, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Name, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Identity.SkillId)
                .ToArray(),
            omitted);
    }

    private static bool TryProjectSkill(
        XElement savedSkill,
        bool created,
        int availableKarma,
        int maximumRating,
        CharacterCareerKnowledgeSkillAdvanceSettings rules,
        string rawCharacterState,
        string rawRuleState,
        XElement? improvements,
        ICharacterSourceDataContext sourceContext,
        out CharacterCareerKnowledgeSkillAdvanceQuote quote)
    {
        quote = null!;
        Guid instanceId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
            savedSkill,
            "guid",
            "A knowledge skill");
        if (!CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(savedSkill, "isknowledge"))
        {
            return false;
        }

        Guid? sourceSkillId = ReadNullableSourceId(savedSkill);
        string savedName = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
            savedSkill,
            "name",
            "A knowledge skill");
        string skillType = ReadOptionalText(savedSkill, "type", string.Empty);
        string skillCategory = ReadOptionalText(savedSkill, "skillcategory", skillType);
        string dictionaryKey = savedName;
        string rawSourceState;
        if (sourceSkillId is { } sourceId)
        {
            if (!sourceContext.TryResolveKnowledgeSkillSource(
                    sourceId.ToString("D"),
                    out CharacterKnowledgeSkillSource source)
                || !Guid.TryParse(source.SourceSkillId, out Guid resolvedSourceId)
                || resolvedSourceId != sourceId
                || !string.Equals(savedName, source.Name, StringComparison.Ordinal)
                || !string.Equals(skillCategory, source.SkillCategory, StringComparison.Ordinal))
            {
                return false;
            }
            dictionaryKey = source.Name;
            rawSourceState = source.RawSourceXml;
        }
        else
        {
            rawSourceState = savedSkill.ToString(SaveOptions.DisableFormatting);
        }

        if (HasUnsupportedRatingAuthority(improvements, dictionaryKey))
        {
            return false;
        }
        int basePoints = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredNonNegativeInt(savedSkill, "base");
        int karmaPoints = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredNonNegativeInt(savedSkill, "karma");
        int totalBaseRating = checked(basePoints + karmaPoints);
        if (totalBaseRating > maximumRating
            || !TryResolveCostModifiers(
                improvements,
                dictionaryKey,
                skillCategory,
                out IReadOnlyList<CharacterCareerKnowledgeSkillKarmaModifier> modifiers))
        {
            return false;
        }

        CharacterCareerKnowledgeSkillAdvanceInput input = new(
            new CharacterCareerKnowledgeSkillIdentity(instanceId, sourceSkillId),
            created,
            CharacterCareerKnowledgeSkillAdvanceRules.RulesetId,
            IsKnowledgeSkill: true,
            AllowUpgrade: !ReadOptionalBool(savedSkill, "disableupgrades", false),
            IsNativeLanguage: ReadRequiredBool(savedSkill, "isnativelanguage"),
            savedName,
            skillType,
            skillCategory,
            dictionaryKey,
            basePoints,
            karmaPoints,
            totalBaseRating,
            maximumRating,
            availableKarma,
            rules,
            modifiers,
            rawCharacterState,
            rawSourceState,
            rawRuleState);
        return CharacterCareerKnowledgeSkillAdvanceRules.TryCreateQuote(input, out quote);
    }

    private static Guid? ReadNullableSourceId(XElement skill)
    {
        XElement[] values = skill.Elements("suid").Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParse(values[0].Value.Trim(), out Guid sourceId))
        {
            throw new InvalidOperationException(
                "A knowledge skill has an invalid or duplicate <suid> value.");
        }
        return sourceId == Guid.Empty ? null : sourceId;
    }

    private static string CanonicalCharacterState(XElement root)
    {
        XElement canonical = new(root);
        canonical.Elements(CareerKnowledgeSkillAdvanceMutation.ReceiptContainerName).Remove();
        return canonical.ToString(SaveOptions.DisableFormatting);
    }

    private static bool HasUnsupportedRatingAuthority(
        XElement? improvements,
        string dictionaryKey)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledInCareer(improvement)
                && ReadOptionalText(improvement, "improvementttype", string.Empty) is
                    "Skill" or "SkillBase" or "SkillLevel" or "KnowledgeSkillLevel"
                && TargetMatches(
                    ReadOptionalText(improvement, "improvedname", string.Empty),
                    dictionaryKey));

    private static bool TryResolveCostModifiers(
        XElement? improvements,
        string dictionaryKey,
        string skillCategory,
        out IReadOnlyList<CharacterCareerKnowledgeSkillKarmaModifier> modifiers)
    {
        List<CharacterCareerKnowledgeSkillKarmaModifier> result = [];
        int ordinal = 0;
        foreach (XElement improvement in improvements?.Elements("improvement") ?? [])
        {
            string type = ReadOptionalText(improvement, "improvementttype", string.Empty);
            if (!TryMapModifierKind(
                    type,
                    out CharacterCareerKnowledgeSkillKarmaModifierKind kind)
                || !IsEnabledCareerCostImprovement(improvement))
            {
                ordinal++;
                continue;
            }

            string target = ReadOptionalText(improvement, "improvedname", string.Empty);
            bool targetMatches = kind switch
            {
                CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCostMinimum =>
                    TargetMatches(target, dictionaryKey)
                    || TargetMatches(target, skillCategory),
                CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCost
                    or CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCostMultiplier =>
                    TargetMatches(target, dictionaryKey),
                _ => TargetMatches(target, skillCategory)
            };
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
            result.Add(new CharacterCareerKnowledgeSkillKarmaModifier(
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
        out CharacterCareerKnowledgeSkillKarmaModifierKind kind)
    {
        switch (type)
        {
            case "KnowledgeSkillKarmaCostMinimum":
                kind = CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCostMinimum;
                return true;
            case "KnowledgeSkillKarmaCost":
                kind = CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCost;
                return true;
            case "KnowledgeSkillKarmaCostMultiplier":
                kind = CharacterCareerKnowledgeSkillKarmaModifierKind.KnowledgeSkillCostMultiplier;
                return true;
            case "SkillCategoryKarmaCost":
                kind = CharacterCareerKnowledgeSkillKarmaModifierKind.SkillCategoryCost;
                return true;
            case "SkillCategoryKarmaCostMultiplier":
                kind = CharacterCareerKnowledgeSkillKarmaModifierKind.SkillCategoryCostMultiplier;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool IsEnabledCareerCostImprovement(XElement improvement)
        => IsEnabledInCareer(improvement)
            && !ReadOptionalBool(improvement, "addtorating", false);

    private static bool IsEnabledInCareer(XElement improvement)
    {
        string condition = ReadOptionalText(improvement, "condition", string.Empty);
        return ReadOptionalBool(improvement, "enabled", true)
            && (condition.Length == 0
                || string.Equals(condition, "career", StringComparison.Ordinal));
    }

    private static bool TargetMatches(string target, string expected)
        => target.Length == 0 || string.Equals(target, expected, StringComparison.Ordinal);

    private static XElement ResolveExactSettings(XElement root, string? settingsCatalogJson)
    {
        string settingsId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
            root,
            "settings",
            "The saved runner");
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

    private static bool ReadRequiredBool(XElement parent, string name)
        => CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(parent, name);

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
        if (values.Length == 0
            || values.Length == 1 && string.IsNullOrWhiteSpace(values[0].Value))
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

    internal static bool ReadOptionalBool(XElement parent, string name, bool fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0
            || values.Length == 1 && string.IsNullOrWhiteSpace(values[0].Value))
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

    internal static string ReadOptionalText(XElement parent, string name, string fallback)
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
