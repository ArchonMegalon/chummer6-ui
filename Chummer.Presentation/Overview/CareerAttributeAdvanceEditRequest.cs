using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerAttributeAdvanceEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterCareerAttributeAdvanceQuote> Attributes,
    int OmittedAttributeCount,
    IReadOnlyList<CharacterCareerAttributeAdvanceReceipt> RecoverableReceipts,
    int OmittedReceiptCount);

public sealed record CareerAttributeAdvanceRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerAttributeAdvanceQuote ExpectedAttribute,
    string ExpectedLogicalRevision,
    string ExpectedSourceRevision,
    string ExpectedRuleDigest,
    bool Confirmed,
    Guid ExpenseId,
    DateTime ExpenseDateLocal);

public sealed record CareerAttributeCorrectionRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerAttributeAdvanceReceipt OriginalReceipt,
    string ExpectedReceiptDigest,
    bool Confirmed,
    Guid CorrectionId,
    string Reason);

public sealed record CareerAttributeAdvanceMutationResult(
    string Xml,
    CharacterCareerAttributeAdvanceReceipt Receipt);

public sealed record CareerAttributeCorrectionMutationResult(
    string Xml,
    CharacterCareerAttributeCorrectionPlan Correction);

internal static class CareerAttributeAdvanceEditorProjector
{
    internal sealed record AttributeFacts(
        CharacterCareerAttributeAdvanceQuote Quote,
        int CalculatedTotalValue);

    private sealed record ImprovementFacts(
        XElement Element,
        int Ordinal,
        string Type,
        string ImprovedName,
        string Source,
        int Minimum,
        int Maximum,
        decimal Augmented,
        int AugmentedMaximum,
        decimal Value,
        int Rating);

    public static CareerAttributeAdvanceEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        string? settingsCatalogJson)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for attribute advancement.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before advancing an attribute.");
        }

        (IReadOnlyList<AttributeFacts> facts, int omitted) =
            ProjectFacts(xml, settingsCatalogJson, requireSavedTotalValueMatch: true);
        CareerAttributeAdvanceMutation.ReceiptRecovery recovery =
            CareerAttributeAdvanceMutation.RecoverReceipts(xml, facts);
        return new CareerAttributeAdvanceEditorState(
            workspaceId,
            contentRevision,
            facts.Select(static value => value.Quote).ToArray(),
            omitted,
            recovery.Receipts,
            recovery.OmittedCount);
    }

    internal static (
        IReadOnlyList<AttributeFacts> Attributes,
        int OmittedAttributeCount) ProjectFacts(
        string xml,
        string? settingsCatalogJson,
        bool requireSavedTotalValueMatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(document);
        bool created = CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(root, "created");
        if (!created)
        {
            throw new InvalidOperationException(
                "Attribute advancement is available only for career runners.");
        }

        XElement settings = ResolveExactSettings(root, settingsCatalogJson);
        XElement karmaCost = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            settings,
            "karmacost",
            "The active settings profile must have one <karmacost> container.");
        CharacterCareerAttributeAdvanceSettings rules = new(
            CareerActiveSkillAdvanceEditorProjector.ReadRequiredNonNegativeInt(
                karmaCost,
                "karmaattribute"),
            ReadOptionalBool(settings, "alternatemetatypeattributekarma", false));
        bool unclampMinimum = ReadOptionalBool(settings, "unclampattributeminimum", false);
        bool secondMagicAttribute = ReadOptionalBool(
            settings,
            "mysadeptsecondmagattribute",
            false);
        bool dontUseCyberlimbCalculation = ReadOptionalBool(
            settings,
            "dontusecyberlimbcalculation",
            false);
        bool hasApplicableCyberlimb = !dontUseCyberlimbCalculation
            && HasApplicableCyberlimb(root, settings);

        int availableKarma = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredNonNegativeInt(root, "karma");
        bool magicEnabled = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredBool(root, "magenabled");
        bool resonanceEnabled = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredBool(root, "resenabled");
        bool depthEnabled = ReadOptionalBool(root, "depenabled", false);
        bool mysticAdept = CareerActiveSkillAdvanceEditorProjector
            .ReadRequiredBool(root, "adept")
            && CareerActiveSkillAdvanceEditorProjector.ReadRequiredBool(root, "magician");
        bool critter = ReadOptionalBool(root, "critter", false);
        string metatypeCategory = ReadOptionalText(root, "metatypecategory", string.Empty);

        XElement attributes = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            root,
            "attributes",
            "The saved runner must have one <attributes> container.");
        XElement[] rows = attributes.Elements("attribute").ToArray();
        Dictionary<string, XElement> catalogRows = new(StringComparer.Ordinal);
        HashSet<string> catalog = CharacterCareerAttributeAdvanceRules.GetTargetCatalog()
            .Select(static identity => identity.Abbreviation)
            .ToHashSet(StringComparer.Ordinal);
        foreach (XElement row in rows)
        {
            string name = CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
                row,
                "name",
                "An attribute");
            if (catalog.Contains(name) && !catalogRows.TryAdd(name, row))
            {
                throw new InvalidOperationException(
                    $"The saved runner has duplicate {name} attribute rows.");
            }
        }

        XElement[] improvementsContainers = root.Elements("improvements").Take(2).ToArray();
        if (improvementsContainers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <improvements> containers.");
        }
        XElement? improvements = improvementsContainers.SingleOrDefault();
        string rawRuleState = settings.ToString(SaveOptions.DisableFormatting)
            + "\n"
            + (improvements?.ToString(SaveOptions.DisableFormatting) ?? "<improvements />")
            + "\n"
            + string.Join("|", new[]
            {
                magicEnabled.ToString(CultureInfo.InvariantCulture),
                resonanceEnabled.ToString(CultureInfo.InvariantCulture),
                mysticAdept.ToString(CultureInfo.InvariantCulture),
                metatypeCategory,
                critter.ToString(CultureInfo.InvariantCulture),
                depthEnabled.ToString(CultureInfo.InvariantCulture)
            });

        List<AttributeFacts> projected = [];
        int omitted = 0;
        foreach (CharacterCareerAttributeIdentity identity in
                 CharacterCareerAttributeAdvanceRules.GetTargetCatalog())
        {
            if (!catalogRows.TryGetValue(identity.Abbreviation, out XElement? row)
                || !TryProjectAttribute(
                    row,
                    identity,
                    availableKarma,
                    magicEnabled,
                    mysticAdept,
                    secondMagicAttribute,
                    resonanceEnabled,
                    depthEnabled,
                    critter,
                    metatypeCategory,
                    unclampMinimum,
                    hasApplicableCyberlimb,
                    rules,
                    rawRuleState,
                    improvements,
                    requireSavedTotalValueMatch,
                    out AttributeFacts facts))
            {
                omitted++;
                continue;
            }
            projected.Add(facts);
        }

        return (projected, omitted);
    }

    private static bool TryProjectAttribute(
        XElement row,
        CharacterCareerAttributeIdentity identity,
        int availableKarma,
        bool magicEnabled,
        bool mysticAdept,
        bool secondMagicAttribute,
        bool resonanceEnabled,
        bool depthEnabled,
        bool critter,
        string metatypeCategory,
        bool unclampMinimum,
        bool hasApplicableCyberlimb,
        CharacterCareerAttributeAdvanceSettings rules,
        string rawRuleState,
        XElement? improvements,
        bool requireSavedTotalValueMatch,
        out AttributeFacts facts)
    {
        facts = null!;
        try
        {
            if (hasApplicableCyberlimb
                && identity.Abbreviation is "AGI" or "STR")
            {
                return false;
            }
            if (identity.Kind == CharacterCareerAttributeKind.Edge && depthEnabled)
            {
                return false;
            }

            int basePoints = ReadRequiredNonNegativeInt(row, "base", "An attribute");
            int karmaPoints = ReadRequiredNonNegativeInt(row, "karma", "An attribute");
            int metatypeMinimum = ReadRequiredNonNegativeInt(row, "metatypemin", "An attribute");
            int metatypeMaximum = ReadRequiredNonNegativeInt(row, "metatypemax", "An attribute");
            int metatypeAugmentedMaximum = ReadRequiredNonNegativeInt(
                row,
                "metatypeaugmax",
                "An attribute");
            int savedTotalValue = ReadRequiredNonNegativeInt(row, "totalvalue", "An attribute");
            if (metatypeMaximum < metatypeMinimum
                || metatypeAugmentedMaximum < metatypeMaximum)
            {
                return false;
            }
            if (!TryResolveImprovementFacts(
                    improvements,
                    identity.Abbreviation,
                    out IReadOnlyList<ImprovementFacts> relevant,
                    out IReadOnlyList<CharacterCareerAttributeKarmaModifier> costModifiers,
                    out int burnedEdgePoints))
            {
                return false;
            }

            ImprovementFacts[] attributeRows = relevant
                .Where(static candidate => candidate.Type == "Attribute")
                .ToArray();
            int minimumModifiers = checked(attributeRows.Sum(candidate =>
                candidate.Minimum * candidate.Rating));
            int maximumModifiers = checked(attributeRows.Sum(candidate =>
                candidate.Maximum * candidate.Rating));
            int augmentedMaximumModifiers = checked(attributeRows
                .Where(candidate => string.Equals(
                    candidate.ImprovedName,
                    identity.Abbreviation,
                    StringComparison.Ordinal))
                .Sum(candidate => candidate.AugmentedMaximum * candidate.Rating));
            decimal freeBaseRaw = relevant
                .Where(static candidate => candidate.Type == "Attributelevel")
                .Sum(static candidate => candidate.Value);
            int freeBase = StandardRound(Math.Min(
                freeBaseRaw,
                metatypeMaximum - metatypeMinimum));
            int rawMinimum = checked(metatypeMinimum + minimumModifiers);
            if (!unclampMinimum)
            {
                rawMinimum = Math.Max(rawMinimum, 0);
            }

            bool cyberzombieMagic = string.Equals(
                    metatypeCategory,
                    "Cyberzombie",
                    StringComparison.Ordinal)
                && identity.Abbreviation is "MAG" or "MAGAdept";
            int naturalMaximum = cyberzombieMagic
                ? 1
                : Math.Max(0, checked(metatypeMaximum + maximumModifiers));
            int totalMinimum = cyberzombieMagic
                ? 1
                : ResolveTotalMinimum(
                    rawMinimum,
                    naturalMaximum,
                    critter,
                    identity.Abbreviation);
            decimal baseValueModifiersRaw = attributeRows
                .Where(candidate => string.Equals(
                    candidate.ImprovedName,
                    identity.Abbreviation + "Base",
                    StringComparison.Ordinal))
                .Sum(static candidate => candidate.Augmented * candidate.Rating);
            int baseValueModifiers = StandardRound(baseValueModifiersRaw);
            int effectiveValue = Math.Min(
                checked(Math.Max(
                    checked(basePoints + freeBase + rawMinimum + baseValueModifiers),
                    totalMinimum) + karmaPoints),
                naturalMaximum);
            if (identity.Kind == CharacterCareerAttributeKind.Edge
                && burnedEdgePoints > 0)
            {
                int repairedRawMinimum = checked(
                    metatypeMinimum + minimumModifiers + 1);
                if (!unclampMinimum)
                {
                    repairedRawMinimum = Math.Max(repairedRawMinimum, 0);
                }
                int repairedTotalMinimum = ResolveTotalMinimum(
                    repairedRawMinimum,
                    naturalMaximum,
                    critter,
                    identity.Abbreviation);
                int repairedEffectiveValue = Math.Min(
                    checked(Math.Max(
                        checked(basePoints + freeBase + repairedRawMinimum + baseValueModifiers),
                        repairedTotalMinimum) + karmaPoints),
                    naturalMaximum);
                if (repairedEffectiveValue != checked(effectiveValue + 1))
                {
                    return false;
                }
            }

            decimal augmentedRaw = attributeRows
                .Where(candidate => string.Equals(
                    candidate.ImprovedName,
                    identity.Abbreviation,
                    StringComparison.Ordinal))
                .Sum(static candidate => candidate.Augmented * candidate.Rating);
            int attributeModifiers = StandardRound(augmentedRaw);
            int modifiersClamp = checked(
                metatypeAugmentedMaximum - metatypeMaximum + augmentedMaximumModifiers);
            bool maximumClamp = relevant.Any(static candidate =>
                candidate.Type == "AttributeMaxClamp");
            if (maximumClamp)
            {
                modifiersClamp = Math.Min(
                    modifiersClamp,
                    checked(naturalMaximum - effectiveValue));
            }
            attributeModifiers = Math.Min(attributeModifiers, modifiersClamp);
            int totalAugmentedMaximum = cyberzombieMagic
                ? 1
                : maximumClamp
                    ? naturalMaximum
                    : Math.Max(0, checked(
                        metatypeAugmentedMaximum
                        + maximumModifiers
                        + augmentedMaximumModifiers));
            int calculatedTotalValue = Math.Min(
                checked(effectiveValue + attributeModifiers),
                totalAugmentedMaximum);
            if (calculatedTotalValue < 1)
            {
                bool encumbranceAllowsZero = identity.Abbreviation is "AGI" or "REA"
                    && attributeRows
                        .Where(candidate => string.Equals(
                                candidate.ImprovedName,
                                identity.Abbreviation,
                                StringComparison.Ordinal)
                            && string.Equals(
                                candidate.Source,
                                "ArmorEncumbrance",
                                StringComparison.Ordinal))
                        .Sum(static candidate => candidate.Augmented * candidate.Rating) < 0m;
                calculatedTotalValue = critter
                    || metatypeMaximum == 0
                    || identity.Kind is CharacterCareerAttributeKind.Edge
                        or CharacterCareerAttributeKind.Magic
                        or CharacterCareerAttributeKind.Resonance
                    || encumbranceAllowsZero
                    ? 0
                    : 1;
            }
            if (requireSavedTotalValueMatch && savedTotalValue != calculatedTotalValue)
            {
                return false;
            }

            CharacterCareerAttributeAdvanceInput input = new(
                identity,
                Created: true,
                RulesetId: CharacterCareerAttributeAdvanceRules.RulesetId,
                DisplayName: identity.Abbreviation,
                basePoints,
                karmaPoints,
                effectiveValue,
                naturalMaximum,
                metatypeMinimum,
                availableKarma,
                magicEnabled,
                mysticAdept,
                secondMagicAttribute,
                resonanceEnabled,
                burnedEdgePoints,
                rules,
                costModifiers,
                RawSourceState: row.ToString(SaveOptions.DisableFormatting),
                rawRuleState);
            if (!CharacterCareerAttributeAdvanceRules.TryCreateQuote(input, out var quote))
            {
                return false;
            }

            facts = new AttributeFacts(quote, calculatedTotalValue);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasApplicableCyberlimb(XElement root, XElement settings)
    {
        XElement[] containers = root.Elements("cyberwares").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <cyberwares> containers.");
        }

        string excludedLimbSlots = ReadOptionalText(
            settings,
            "excludelimbslot",
            string.Empty);
        return containers.SingleOrDefault()?.Elements("cyberware")
            .Any(candidate => HasApplicableCyberlimb(
                candidate,
                [],
                excludedLimbSlots)) == true;
    }

    private static bool HasApplicableCyberlimb(
        XElement cyberware,
        IReadOnlyList<XElement> parents,
        string excludedLimbSlots)
    {
        if (!IsModularCurrentlyEquipped(cyberware, parents))
        {
            return false;
        }

        if (IsCyberlimb(cyberware))
        {
            string limbSlot = ReadOptionalText(cyberware, "limbslot", string.Empty);
            return !excludedLimbSlots.Contains(limbSlot, StringComparison.Ordinal);
        }

        XElement[] containers = cyberware.Elements("children").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "Saved cyberware has duplicate <children> containers.");
        }
        if (containers.Length == 0)
        {
            return false;
        }

        XElement[] lineage = [cyberware, .. parents];
        return containers[0].Elements("cyberware")
            .Any(candidate => HasApplicableCyberlimb(
                candidate,
                lineage,
                excludedLimbSlots));
    }

    private static bool IsCyberlimb(XElement cyberware)
    {
        string limbSlot = ReadOptionalText(cyberware, "limbslot", string.Empty);
        string modularMount = ReadOptionalText(
            cyberware,
            "plugsintomodularmount",
            string.Empty);
        if (!string.IsNullOrWhiteSpace(limbSlot)
            || modularMount.Equals("WRIST", StringComparison.OrdinalIgnoreCase)
            || modularMount.Equals("ELBOW", StringComparison.OrdinalIgnoreCase)
            || modularMount.Equals("SHOULDER", StringComparison.OrdinalIgnoreCase)
            || modularMount.Equals("ANKLE", StringComparison.OrdinalIgnoreCase)
            || modularMount.Equals("KNEE", StringComparison.OrdinalIgnoreCase)
            || modularMount.Equals("HIP", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!ReadOptionalBool(cyberware, "inheritattributes", false))
        {
            return false;
        }
        XElement[] containers = cyberware.Elements("children").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "Saved cyberware has duplicate <children> containers.");
        }
        return containers.SingleOrDefault()?.Elements("cyberware")
            .Any(IsCyberlimb) == true;
    }

    private static bool IsModularCurrentlyEquipped(
        XElement cyberware,
        IReadOnlyList<XElement> parents)
    {
        bool equipped = string.IsNullOrEmpty(ReadOptionalText(
            cyberware,
            "plugsintomodularmount",
            string.Empty));
        foreach (XElement parent in parents)
        {
            if (!string.IsNullOrEmpty(ReadOptionalText(
                    parent,
                    "hasmodularmount",
                    string.Empty)))
            {
                equipped = true;
            }
            if (!string.IsNullOrEmpty(ReadOptionalText(
                    parent,
                    "plugsintomodularmount",
                    string.Empty)))
            {
                equipped = false;
            }
        }
        return equipped;
    }

    private static bool TryResolveImprovementFacts(
        XElement? improvements,
        string abbreviation,
        out IReadOnlyList<ImprovementFacts> relevant,
        out IReadOnlyList<CharacterCareerAttributeKarmaModifier> costModifiers,
        out int burnedEdgePoints)
    {
        List<ImprovementFacts> resolved = [];
        List<CharacterCareerAttributeKarmaModifier> costs = [];
        burnedEdgePoints = 0;
        int ordinal = 0;
        foreach (XElement improvement in improvements?.Elements("improvement") ?? [])
        {
            string type = ReadOptionalText(improvement, "improvementttype", string.Empty);
            string target = ReadOptionalText(improvement, "improvedname", string.Empty);
            string source = ReadOptionalText(improvement, "improvementsource", string.Empty);
            bool enabled = ReadOptionalBool(improvement, "enabled", true);
            bool addToRating = ReadOptionalBool(improvement, "addtorating", false);
            string condition = ReadOptionalText(improvement, "condition", string.Empty);
            bool careerActive = enabled
                && !addToRating
                && (condition.Length == 0
                    || string.Equals(condition, "career", StringComparison.Ordinal));
            bool targetExact = string.Equals(target, abbreviation, StringComparison.Ordinal);
            bool targetBase = string.Equals(
                target,
                abbreviation + "Base",
                StringComparison.Ordinal);
            bool costType = type is "AttributeKarmaCost" or "AttributeKarmaCostMultiplier";
            bool relevantCost = costType
                && careerActive
                && (target.Length == 0
                    || string.Equals(target, abbreviation, StringComparison.Ordinal));
            bool relevantRating = careerActive
                && (type == "Attribute" && (targetExact || targetBase)
                    || type == "Attributelevel" && targetExact
                    || type == "AttributeMaxClamp" && targetExact);
            bool unsupportedReplacement = enabled
                && type == "ReplaceAttribute"
                && targetExact;
            bool burnedSource = string.Equals(source, "BurnedEdge", StringComparison.Ordinal);
            if (!relevantCost && !relevantRating && !burnedSource && !unsupportedReplacement)
            {
                ordinal++;
                continue;
            }
            if (unsupportedReplacement)
            {
                relevant = [];
                costModifiers = [];
                return false;
            }
            if (burnedSource && abbreviation != "EDG")
            {
                ordinal++;
                continue;
            }
            if (burnedSource && !careerActive)
            {
                ordinal++;
                continue;
            }
            if (burnedSource
                && (type != "Attribute"
                    || target != "EDG"))
            {
                relevant = [];
                costModifiers = [];
                return false;
            }
            if (!string.IsNullOrEmpty(ReadOptionalText(improvement, "unique", string.Empty))
                || ReadOptionalBool(improvement, "custom", false))
            {
                relevant = [];
                costModifiers = [];
                return false;
            }

            int minimum = ReadRequiredInt(improvement, "min", "An attribute improvement");
            int maximum = ReadRequiredInt(improvement, "max", "An attribute improvement");
            decimal augmented = ReadRequiredDecimal(improvement, "aug", "An attribute improvement");
            int augmentedMaximum = ReadRequiredInt(
                improvement,
                "augmax",
                "An attribute improvement");
            decimal value = ReadRequiredDecimal(improvement, "val", "An attribute improvement");
            int rating = ReadRequiredNonNegativeInt(
                improvement,
                "rating",
                "An attribute improvement");
            ImprovementFacts facts = new(
                improvement,
                ordinal,
                type,
                target,
                source,
                minimum,
                maximum,
                augmented,
                augmentedMaximum,
                value,
                rating);
            if (relevantRating)
            {
                resolved.Add(facts);
            }
            if (relevantCost)
            {
                if (minimum < 0 || maximum < 0 || maximum != 0 && maximum < minimum)
                {
                    relevant = [];
                    costModifiers = [];
                    return false;
                }
                string raw = ordinal.ToString(CultureInfo.InvariantCulture)
                    + "\0"
                    + improvement.ToString(SaveOptions.DisableFormatting);
                costs.Add(new CharacterCareerAttributeKarmaModifier(
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
                        .ToLowerInvariant(),
                    type == "AttributeKarmaCost"
                        ? CharacterCareerAttributeKarmaModifierKind.AttributeKarmaCost
                        : CharacterCareerAttributeKarmaModifierKind.AttributeKarmaCostMultiplier,
                    target,
                    minimum,
                    maximum,
                    value));
            }
            if (burnedSource)
            {
                int burned = checked(-(minimum * rating));
                if (burned <= 0)
                {
                    relevant = [];
                    costModifiers = [];
                    return false;
                }
                burnedEdgePoints = checked(burnedEdgePoints + burned);
            }
            ordinal++;
        }

        relevant = resolved;
        costModifiers = costs;
        return true;
    }

    internal static XElement ResolveExactAttribute(
        XElement root,
        CharacterCareerAttributeIdentity identity)
    {
        XElement attributes = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            root,
            "attributes",
            "The saved runner must have one <attributes> container.");
        XElement[] matches = attributes.Elements("attribute")
            .Where(candidate => string.Equals(
                ReadOptionalText(candidate, "name", string.Empty),
                identity.Abbreviation,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "The selected attribute identity is ambiguous or missing.");
    }

    internal static XElement? ResolveSingleImprovements(XElement root)
    {
        XElement[] matches = root.Elements("improvements").Take(2).ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                "The saved runner has duplicate <improvements> containers.")
        };
    }

    private static int ResolveTotalMinimum(
        int rawMinimum,
        int totalMaximum,
        bool critter,
        string abbreviation)
    {
        if (rawMinimum >= 1)
        {
            return rawMinimum;
        }
        return critter
            || totalMaximum == 0
            || abbreviation is "EDG" or "MAG" or "MAGAdept" or "RES" or "DEP"
            ? 0
            : 1;
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

    internal static int ReadRequiredNonNegativeInt(
        XElement parent,
        string name,
        string owner)
    {
        int value = ReadRequiredInt(parent, name, owner);
        return value >= 0
            ? value
            : throw new InvalidOperationException(
                $"{owner} has a negative <{name}> value.");
    }

    internal static int ReadRequiredInt(XElement parent, string name, string owner)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length == 1
            && int.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
            ? value
            : throw new InvalidOperationException(
                $"{owner} has an invalid or duplicate <{name}> value.");
    }

    internal static decimal ReadRequiredDecimal(XElement parent, string name, string owner)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length == 1
            && decimal.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value)
            ? value
            : throw new InvalidOperationException(
                $"{owner} has an invalid or duplicate <{name}> value.");
    }

    internal static bool ReadOptionalBool(XElement parent, string name, bool fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0
            || values.Length == 1 && string.IsNullOrWhiteSpace(values[0].Value))
        {
            return fallback;
        }
        return values.Length == 1
            && bool.TryParse(values[0].Value.Trim(), out bool value)
            ? value
            : throw new InvalidOperationException(
                $"The saved authority has an invalid or duplicate <{name}> value.");
    }

    internal static string ReadOptionalText(XElement parent, string name, string fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => fallback,
            1 => values[0].Value.Trim(),
            _ => throw new InvalidOperationException(
                $"The saved authority has duplicate <{name}> values.")
        };
    }

    private static int StandardRound(decimal value)
        => decimal.ToInt32(value >= 0m ? decimal.Ceiling(value) : decimal.Floor(value));
}
