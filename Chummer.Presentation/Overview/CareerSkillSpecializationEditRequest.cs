using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerSkillSpecializationCandidate(
    CharacterCareerSkillIdentity Identity,
    string SkillName,
    string SkillCategory,
    string SkillGroup,
    int TotalBaseRating,
    int ExistingSpecializationCount,
    IReadOnlyList<CharacterCareerSkillSpecializationOption> AvailableOptions);

public sealed record CareerSkillSpecializationEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CareerSkillSpecializationCandidate> Skills,
    int OmittedSkillCount);

public sealed record CareerSkillSpecializationQuoteRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerSkillIdentity SkillIdentity,
    CharacterCareerSkillSpecializationSelection Selection);

public sealed record CareerSkillSpecializationRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerSkillSpecializationQuote ExpectedQuote,
    string ExpectedCharacterRevision,
    string ExpectedSourceRevision,
    string ExpectedRuleDigest,
    string ExpectedLogicalRevision,
    bool Confirmed,
    Guid SpecializationId,
    Guid ExpenseId,
    DateTime ExpenseDateLocal);

internal static class CareerSkillSpecializationEditorProjector
{
    private const int DefaultMaximumActiveSkillRating = 12;

    private sealed record ProjectedSkill(
        CharacterCareerSkillIdentity Identity,
        bool Enabled,
        bool IsExoticSkill,
        bool KarmaUnlocked,
        bool AllowUpgrade,
        bool IsNativeLanguage,
        string SkillName,
        string SkillCategory,
        string DictionaryKey,
        string SkillGroup,
        int TotalBaseRating,
        int ExistingSpecializationCount,
        int AvailableKarma,
        int EnabledSkillGroupMemberCount,
        bool SkillSpecializationsBlocked,
        bool SkillCategorySpecializationsBlocked,
        CharacterCareerSkillSpecializationSettings Settings,
        IReadOnlyList<CharacterCareerSkillSpecializationModifier> Modifiers,
        IReadOnlyList<CharacterCareerSkillSpecializationOption> AvailableOptions,
        string RawCharacterState,
        string RawSourceState,
        string RawRuleState)
    {
        public CareerSkillSpecializationCandidate Candidate => new(
            Identity,
            SkillName,
            SkillCategory,
            SkillGroup,
            TotalBaseRating,
            ExistingSpecializationCount,
            AvailableOptions);

        public bool TryQuote(
            CharacterCareerSkillSpecializationSelection selection,
            out CharacterCareerSkillSpecializationQuote quote)
            => CharacterCareerSkillSpecializationRules.TryCreateQuote(
                new CharacterCareerSkillSpecializationInput(
                    Identity,
                    Created: true,
                    Enabled,
                    IsExoticSkill,
                    KarmaUnlocked,
                    AllowUpgrade,
                    IsNativeLanguage,
                    SkillName,
                    SkillCategory,
                    DictionaryKey,
                    SkillGroup,
                    TotalBaseRating,
                    ExistingSpecializationCount,
                    AvailableKarma,
                    EnabledSkillGroupMemberCount,
                    SkillSpecializationsBlocked,
                    SkillCategorySpecializationsBlocked,
                    Settings,
                    Modifiers,
                    AvailableOptions,
                    selection,
                    RawCharacterState,
                    RawSourceState,
                    RawRuleState),
                out quote);
    }

    private sealed record ProjectionState(
        IReadOnlyList<ProjectedSkill> Skills,
        int OmittedSkillCount);

    public static CareerSkillSpecializationEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ValidateWorkspaceAuthority(workspaceId, contentRevision);
        ProjectionState state = ProjectState(xml, settingsCatalogJson, sourceDataResolver);
        return new CareerSkillSpecializationEditorState(
            workspaceId,
            contentRevision,
            state.Skills.Select(static skill => skill.Candidate).ToArray(),
            state.OmittedSkillCount);
    }

    public static CharacterCareerSkillSpecializationQuote ProjectQuote(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        CharacterCareerSkillIdentity identity,
        CharacterCareerSkillSpecializationSelection selection,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ValidateWorkspaceAuthority(workspaceId, contentRevision);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(selection);
        ProjectionState state = ProjectState(xml, settingsCatalogJson, sourceDataResolver);
        ProjectedSkill[] matches = state.Skills
            .Where(candidate => candidate.Identity == identity)
            .Take(2)
            .ToArray();
        if (matches.Length != 1 || !matches[0].TryQuote(selection, out CharacterCareerSkillSpecializationQuote quote))
        {
            throw new InvalidOperationException(
                "The selected skill or specialization option is unavailable under the current Chummer5 authority.");
        }
        return quote;
    }

    private static ProjectionState ProjectState(
        string xml,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(document);
        if (!CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(root, "created"))
        {
            throw new InvalidOperationException(
                "Career specialization purchase is available only for career runners.");
        }

        ICharacterSourceDataContext sourceContext = sourceDataResolver?.TryCreateContext(xml)
            ?? throw new InvalidOperationException(
                "The exact enabled Chummer5 source profile is unavailable.");
        if (!sourceContext.TryResolveCareerSkillSpecializationSettings(
                out CharacterCareerSkillSpecializationSettings settings,
                out string sourceRuleState)
            || string.IsNullOrWhiteSpace(sourceRuleState))
        {
            throw new InvalidOperationException(
                "The exact Chummer5 specialization settings are unavailable.");
        }

        XElement selectedSettings = ResolveExactSettings(root, settingsCatalogJson);
        int maximumActiveRating = ReadOptionalNonNegativeInt(
            selectedSettings,
            "maxskillrating",
            DefaultMaximumActiveSkillRating);
        bool usePointsOnBrokenGroups = ReadOptionalBool(
            selectedSettings,
            "usepointsonbrokengroups",
            false);
        int availableKarma = CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(root, "karma");
        XElement newSkills = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            root,
            "newskills",
            "The saved runner must have one <newskills> container.");
        XElement activeContainer = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            newSkills,
            "skills",
            "The saved runner must have one active <skills> container.");
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
        string rawRuleState = sourceRuleState
            + "\n"
            + (improvements?.ToString(SaveOptions.DisableFormatting) ?? "<improvements />");
        string rawCharacterState = root.ToString(SaveOptions.DisableFormatting);

        XElement[] activeSkills = activeContainer.Elements("skill").ToArray();
        XElement[] knowledgeSkills = knowledgeContainer.Elements("skill").ToArray();
        ValidateUniqueSkillIds(activeSkills.Concat(knowledgeSkills));

        List<ProjectedSkill> projected = [];
        int omitted = 0;
        foreach (XElement savedSkill in activeSkills)
        {
            if (CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(savedSkill, "isknowledge"))
            {
                throw new InvalidOperationException(
                    "The active <skills> container contains a knowledge skill.");
            }
            if (!TryProjectActiveSkill(
                    root,
                    newSkills,
                    activeSkills,
                    savedSkill,
                    availableKarma,
                    maximumActiveRating,
                    usePointsOnBrokenGroups,
                    settings,
                    rawCharacterState,
                    rawRuleState,
                    improvements,
                    sourceContext,
                    out ProjectedSkill skill))
            {
                omitted++;
                continue;
            }
            projected.Add(skill);
        }

        foreach (XElement savedSkill in knowledgeSkills)
        {
            if (!CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(savedSkill, "isknowledge"))
            {
                throw new InvalidOperationException(
                    "The knowledge <knoskills> container contains an active skill.");
            }
            if (!TryProjectKnowledgeSkill(
                    savedSkill,
                    availableKarma,
                    settings,
                    rawCharacterState,
                    rawRuleState,
                    improvements,
                    sourceContext,
                    out ProjectedSkill skill))
            {
                omitted++;
                continue;
            }
            projected.Add(skill);
        }

        return new ProjectionState(
            projected
                .OrderBy(static skill => skill.SkillName, StringComparer.Ordinal)
                .ThenBy(static skill => skill.Identity.Kind)
                .ThenBy(static skill => skill.Identity.SkillId)
                .ToArray(),
            omitted);
    }

    private static bool TryProjectActiveSkill(
        XElement root,
        XElement newSkills,
        IReadOnlyList<XElement> allActiveSkills,
        XElement savedSkill,
        int availableKarma,
        int maximumActiveRating,
        bool usePointsOnBrokenGroups,
        CharacterCareerSkillSpecializationSettings settings,
        string rawCharacterState,
        string rawRuleState,
        XElement? improvements,
        ICharacterSourceDataContext sourceContext,
        out ProjectedSkill projected)
    {
        projected = null!;
        Guid instanceId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
            savedSkill,
            "guid",
            "An active skill");
        Guid sourceId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
            savedSkill,
            "suid",
            "An active skill");
        if (!sourceContext.TryResolveCareerSkillSpecializationSource(
                sourceId.ToString("D"),
                CharacterCareerSkillKind.Active,
                out CharacterCareerSkillSpecializationSource specializationSource)
            || !TryResolveExactActiveSource(sourceContext, sourceId, out CharacterActiveSkillSource activeSource)
            || specializationSource.Kind != CharacterCareerSkillKind.Active
            || !Guid.TryParse(specializationSource.SourceSkillId, out Guid resolvedSourceId)
            || resolvedSourceId != sourceId
            || !string.Equals(specializationSource.Name, activeSource.Name, StringComparison.Ordinal)
            || !string.Equals(specializationSource.SkillCategory, activeSource.SkillCategory, StringComparison.Ordinal))
        {
            return false;
        }

        string savedCategory = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
            savedSkill,
            "skillcategory",
            "An active skill");
        if (!string.Equals(savedCategory, activeSource.SkillCategory, StringComparison.Ordinal)
            || UsesUnsupportedEnabledAuthority(root, activeSource))
        {
            return false;
        }

        string dictionaryKey = activeSource.Name;
        string displayName = activeSource.Name;
        if (activeSource.IsExotic)
        {
            string specific = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
                savedSkill,
                "specific",
                "An exotic active skill");
            dictionaryKey = $"{activeSource.Name} ({specific})";
            displayName = dictionaryKey;
        }
        if (HasUnsupportedRatingAuthority(improvements, dictionaryKey))
        {
            return false;
        }

        int basePoints = CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(savedSkill, "base");
        int karmaPoints = CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(savedSkill, "karma");
        int groupBase = 0;
        int groupKarma = 0;
        if (!string.IsNullOrWhiteSpace(activeSource.SkillGroup))
        {
            XElement? group = ResolveSkillGroup(newSkills, activeSource.SkillGroup);
            if (group is not null)
            {
                groupBase = CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(group, "base");
                groupKarma = CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(group, "karma");
            }
        }
        int effectiveBase = groupBase > 0 && !usePointsOnBrokenGroups
            ? groupBase
            : checked(groupBase + basePoints);
        int totalBaseRating = checked(
            Math.Min(effectiveBase, maximumActiveRating)
            + Math.Min(checked(groupKarma + karmaPoints), maximumActiveRating));
        if (totalBaseRating > maximumActiveRating)
        {
            return false;
        }

        if (!TryResolveEnabledGroupMemberCount(
                root,
                allActiveSkills,
                activeSource.SkillGroup,
                improvements,
                sourceContext,
                out int enabledGroupMemberCount)
            || !TryResolveSpecializationAuthority(
                dictionaryKey,
                savedCategory,
                specializationSource.Options,
                improvements,
                out IReadOnlyList<CharacterCareerSkillSpecializationModifier> modifiers,
                out IReadOnlyList<CharacterCareerSkillSpecializationOption> options,
                out bool skillBlocked,
                out bool categoryBlocked))
        {
            return false;
        }

        projected = new ProjectedSkill(
            new CharacterCareerSkillIdentity(instanceId, sourceId, CharacterCareerSkillKind.Active),
            Enabled: !HasSkillDisable(improvements, dictionaryKey),
            activeSource.IsExotic,
            KarmaUnlocked: true,
            AllowUpgrade: true,
            IsNativeLanguage: false,
            displayName,
            savedCategory,
            dictionaryKey,
            activeSource.SkillGroup,
            totalBaseRating,
            CountAndValidateSpecializations(savedSkill),
            availableKarma,
            enabledGroupMemberCount,
            skillBlocked,
            categoryBlocked,
            settings,
            modifiers,
            options,
            rawCharacterState,
            specializationSource.RawSourceState,
            rawRuleState);
        return true;
    }

    private static bool TryProjectKnowledgeSkill(
        XElement savedSkill,
        int availableKarma,
        CharacterCareerSkillSpecializationSettings settings,
        string rawCharacterState,
        string rawRuleState,
        XElement? improvements,
        ICharacterSourceDataContext sourceContext,
        out ProjectedSkill projected)
    {
        projected = null!;
        Guid instanceId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
            savedSkill,
            "guid",
            "A knowledge skill");
        Guid? sourceId = ReadOptionalSourceGuid(savedSkill, "suid", "A knowledge skill");
        string savedName = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
            savedSkill,
            "name",
            "A knowledge skill");
        string savedCategory = ResolveKnowledgeCategory(savedSkill);
        string rawSourceState;
        IReadOnlyList<CharacterCareerSkillSpecializationOption> sourceOptions;
        if (sourceId.HasValue)
        {
            if (!sourceContext.TryResolveCareerSkillSpecializationSource(
                    sourceId.Value.ToString("D"),
                    CharacterCareerSkillKind.Knowledge,
                    out CharacterCareerSkillSpecializationSource resolved)
                || resolved.Kind != CharacterCareerSkillKind.Knowledge
                || !Guid.TryParse(resolved.SourceSkillId, out Guid resolvedId)
                || resolvedId != sourceId
                || !string.Equals(resolved.Name, savedName, StringComparison.Ordinal)
                || !string.Equals(resolved.SkillCategory, savedCategory, StringComparison.Ordinal))
            {
                return false;
            }
            rawSourceState = resolved.RawSourceState;
            sourceOptions = resolved.Options;
        }
        else
        {
            rawSourceState = savedSkill.ToString(SaveOptions.DisableFormatting);
            sourceOptions = [];
        }

        string dictionaryKey = savedName;
        if (HasUnsupportedRatingAuthority(improvements, dictionaryKey))
        {
            return false;
        }
        int totalBaseRating = checked(
            CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(savedSkill, "base")
            + CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(savedSkill, "karma"));
        bool nativeLanguage = ReadOptionalBool(savedSkill, "isnativelanguage", false);
        bool allowUpgrade = !ReadOptionalBool(savedSkill, "disableupgrades", false)
            && !nativeLanguage;
        if (!TryResolveSpecializationAuthority(
                dictionaryKey,
                savedCategory,
                sourceOptions,
                improvements,
                out IReadOnlyList<CharacterCareerSkillSpecializationModifier> modifiers,
                out IReadOnlyList<CharacterCareerSkillSpecializationOption> options,
                out bool skillBlocked,
                out bool categoryBlocked))
        {
            return false;
        }

        projected = new ProjectedSkill(
            new CharacterCareerSkillIdentity(instanceId, sourceId, CharacterCareerSkillKind.Knowledge),
            Enabled: !HasSkillDisable(improvements, dictionaryKey),
            IsExoticSkill: false,
            KarmaUnlocked: true,
            allowUpgrade,
            nativeLanguage,
            savedName,
            savedCategory,
            dictionaryKey,
            SkillGroup: string.Empty,
            totalBaseRating,
            CountAndValidateSpecializations(savedSkill),
            availableKarma,
            EnabledSkillGroupMemberCount: 0,
            skillBlocked,
            categoryBlocked,
            settings,
            modifiers,
            options,
            rawCharacterState,
            rawSourceState,
            rawRuleState);
        return true;
    }

    private static bool TryResolveExactActiveSource(
        ICharacterSourceDataContext sourceContext,
        Guid sourceId,
        out CharacterActiveSkillSource source)
        => sourceContext.TryResolveActiveSkillSource(sourceId.ToString("D"), out source)
            && Guid.TryParse(source.SourceSkillId, out Guid resolvedId)
            && resolvedId == sourceId;


    private static bool TryResolveEnabledGroupMemberCount(
        XElement root,
        IReadOnlyList<XElement> allActiveSkills,
        string skillGroup,
        XElement? improvements,
        ICharacterSourceDataContext sourceContext,
        out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(skillGroup))
        {
            return true;
        }
        foreach (XElement savedPeer in allActiveSkills)
        {
            Guid sourceId = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
                savedPeer,
                "suid",
                "An active skill");
            if (!TryResolveExactActiveSource(sourceContext, sourceId, out CharacterActiveSkillSource source)
                || UsesUnsupportedEnabledAuthority(root, source))
            {
                return false;
            }
            if (!string.Equals(source.SkillGroup, skillGroup, StringComparison.Ordinal))
            {
                continue;
            }
            string dictionaryKey = source.IsExotic
                ? $"{source.Name} ({CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(savedPeer, "specific", "An exotic active skill")})"
                : source.Name;
            if (!HasSkillDisable(improvements, dictionaryKey))
            {
                count++;
            }
        }
        return true;
    }

    private static bool UsesUnsupportedEnabledAuthority(
        XElement root,
        CharacterActiveSkillSource source)
    {
        if (source.RequiresGroundMovement || source.RequiresSwimMovement || source.RequiresFlyMovement)
        {
            return true;
        }
        return source.DefaultAttribute.ToUpperInvariant() switch
        {
            "MAG" or "MAGADEPT" => !ReadOptionalBool(root, "magenabled", false),
            "RES" => !ReadOptionalBool(root, "resenabled", false),
            "DEP" => !ReadOptionalBool(root, "depenabled", false),
            _ => false
        };
    }

    private static bool TryResolveSpecializationAuthority(
        string dictionaryKey,
        string skillCategory,
        IReadOnlyList<CharacterCareerSkillSpecializationOption> sourceOptions,
        XElement? improvements,
        out IReadOnlyList<CharacterCareerSkillSpecializationModifier> modifiers,
        out IReadOnlyList<CharacterCareerSkillSpecializationOption> options,
        out bool skillBlocked,
        out bool categoryBlocked)
    {
        List<CharacterCareerSkillSpecializationModifier> projectedModifiers = [];
        List<CharacterCareerSkillSpecializationOption> projectedOptions = [.. sourceOptions];
        skillBlocked = false;
        categoryBlocked = false;
        int ordinal = 0;
        foreach (XElement improvement in improvements?.Elements("improvement") ?? [])
        {
            if (!IsEnabledCareerImprovement(improvement))
            {
                ordinal++;
                continue;
            }
            string type = ReadOptionalText(improvement, "improvementttype", string.Empty);
            string target = ReadOptionalText(improvement, "improvedname", string.Empty);
            string raw = ordinal.ToString(CultureInfo.InvariantCulture)
                + "\0"
                + improvement.ToString(SaveOptions.DisableFormatting);
            string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
                .ToLowerInvariant();
            switch (type)
            {
                case "SkillCategorySpecializationKarmaCost":
                case "SkillCategorySpecializationKarmaCostMultiplier":
                    if (!TargetMatches(target, skillCategory, includeGlobal: true))
                    {
                        break;
                    }
                    if (ReadOptionalBool(improvement, "addtorating", false))
                    {
                        break;
                    }
                    if (!string.IsNullOrEmpty(ReadOptionalText(improvement, "unique", string.Empty))
                        || !TryReadNonNegativeInt(improvement, "min", 0, out int minimum)
                        || !TryReadDecimal(improvement, "val", out decimal value))
                    {
                        modifiers = [];
                        options = [];
                        return false;
                    }
                    projectedModifiers.Add(new CharacterCareerSkillSpecializationModifier(
                        identity,
                        type == "SkillCategorySpecializationKarmaCost"
                            ? CharacterCareerSkillSpecializationModifierKind.SkillCategorySpecializationKarmaCost
                            : CharacterCareerSkillSpecializationModifierKind.SkillCategorySpecializationKarmaCostMultiplier,
                        target,
                        minimum,
                        value));
                    break;
                case "SkillSpecializationOption":
                    if (!TargetMatches(target, dictionaryKey, includeGlobal: false))
                    {
                        break;
                    }
                    string optionName = ReadOptionalText(improvement, "unique", string.Empty);
                    if (string.IsNullOrWhiteSpace(optionName))
                    {
                        modifiers = [];
                        options = [];
                        return false;
                    }
                    projectedOptions.Add(new CharacterCareerSkillSpecializationOption(
                        identity,
                        optionName,
                        CharacterCareerSkillSpecializationOptionKind.Improvement,
                        $"saved-improvement:{ordinal.ToString(CultureInfo.InvariantCulture)}"));
                    break;
                case "BlockSkillSpecializations":
                    skillBlocked |= TargetMatches(target, dictionaryKey, includeGlobal: true);
                    break;
                case "BlockSkillCategorySpecializations":
                    categoryBlocked |= TargetMatches(target, skillCategory, includeGlobal: false);
                    break;
            }
            ordinal++;
        }

        if (projectedOptions.Select(static option => option.OptionIdentity)
            .Distinct(StringComparer.Ordinal)
            .Count() != projectedOptions.Count)
        {
            modifiers = [];
            options = [];
            return false;
        }
        modifiers = projectedModifiers;
        options = projectedOptions
            .OrderBy(static option => option.Name, StringComparer.Ordinal)
            .ThenBy(static option => option.OptionIdentity, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool HasUnsupportedRatingAuthority(
        XElement? improvements,
        string dictionaryKey)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledCareerImprovement(improvement)
                && ReadOptionalText(improvement, "improvementttype", string.Empty) is
                    "Skill" or "SkillBase" or "SkillLevel"
                && TargetMatches(
                    ReadOptionalText(improvement, "improvedname", string.Empty),
                    dictionaryKey,
                    includeGlobal: false));

    private static bool HasSkillDisable(XElement? improvements, string dictionaryKey)
        => (improvements?.Elements("improvement") ?? [])
            .Any(improvement => IsEnabledCareerImprovement(improvement)
                && string.Equals(
                    ReadOptionalText(improvement, "improvementttype", string.Empty),
                    "SkillDisable",
                    StringComparison.Ordinal)
                && TargetMatches(
                    ReadOptionalText(improvement, "improvedname", string.Empty),
                    dictionaryKey,
                    includeGlobal: false));

    private static int CountAndValidateSpecializations(XElement savedSkill)
    {
        XElement[] containers = savedSkill.Elements("specs").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "A saved skill has duplicate <specs> containers.");
        }
        if (containers.Length == 0)
        {
            return 0;
        }
        HashSet<Guid> ids = [];
        int count = 0;
        foreach (XElement spec in containers[0].Elements("spec"))
        {
            Guid id = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
                spec,
                "guid",
                "A skill specialization");
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    "A saved skill has duplicate specialization GUIDs.");
            }
            _ = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
                spec,
                "name",
                "A skill specialization");
            _ = CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(spec, "free");
            _ = CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(spec, "expertise");
            count++;
        }
        return count;
    }

    private static void ValidateUniqueSkillIds(IEnumerable<XElement> savedSkills)
    {
        HashSet<Guid> ids = [];
        foreach (XElement skill in savedSkills)
        {
            Guid id = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
                skill,
                "guid",
                "A saved skill");
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    "The saved runner has duplicate active/knowledge skill instance GUIDs.");
            }
        }
    }

    private static Guid? ReadOptionalSourceGuid(XElement parent, string name, string owner)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !Guid.TryParse(values[0].Value.Trim(), out Guid value))
        {
            throw new InvalidOperationException(
                $"{owner} has an invalid or duplicate <{name}> value.");
        }
        return value == Guid.Empty ? null : value;
    }

    private static string ResolveKnowledgeCategory(XElement savedSkill)
    {
        string type = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
            savedSkill,
            "type",
            "A knowledge skill");
        string savedCategory = ReadOptionalText(savedSkill, "skillcategory", type);
        if (!string.Equals(type, savedCategory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A knowledge skill has conflicting <type> and <skillcategory> values.");
        }
        return type;
    }

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

    private static XElement? ResolveSkillGroup(XElement newSkills, string groupName)
    {
        XElement[] containers = newSkills.Elements("groups").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group containers.");
        }
        XElement[] matches = containers.SingleOrDefault()?.Elements("group")
            .Where(group => string.Equals(
                ReadOptionalText(group, "name", string.Empty),
                groupName,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray()
            ?? [];
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"The saved runner has duplicate {groupName} skill-group rows.")
        };
    }

    private static bool IsEnabledCareerImprovement(XElement improvement)
    {
        string condition = ReadOptionalText(improvement, "condition", string.Empty);
        return ReadOptionalBool(improvement, "enabled", true)
            && (string.IsNullOrEmpty(condition)
                || string.Equals(condition, "career", StringComparison.Ordinal));
    }

    private static bool TargetMatches(string target, string expected, bool includeGlobal)
        => includeGlobal && string.IsNullOrEmpty(target)
            || string.Equals(target, expected, StringComparison.Ordinal);

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

    private static void ValidateWorkspaceAuthority(
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for specialization purchase.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before buying a specialization.");
        }
    }
}
