using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

internal static class WorkspaceXmlMutationCatalog
{
    private readonly record struct CharacterMatrixImprovementBasis(
        bool OverclockerEnabled,
        bool LivingPersonaDeviceRatingExact,
        string LivingPersonaDeviceRatingSuffix,
        bool LivingPersonaConditionMonitorExact,
        string LivingPersonaConditionMonitorExpression,
        IReadOnlyDictionary<string, int> SavedAttributeTotals);

    private const int MaximumRating = 1000;
    private const decimal MaximumQuantity = 1_000_000m;
    private const int MaximumNameLength = 512;
    private const int MaximumTextLength = 65_536;
    private const int MaximumConditionBoxes = 1000;

    public static string ApplyQuickAdd(string xml, WorkspaceQuickAddRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");

        switch (request.Kind)
        {
            case WorkspaceQuickAddKinds.Gear:
                AddGear(root, request);
                break;
            case WorkspaceQuickAddKinds.Weapon:
                AddWeapon(root, request);
                break;
            case WorkspaceQuickAddKinds.Armor:
                AddArmor(root, request);
                break;
            case WorkspaceQuickAddKinds.Skill:
                AddSkill(root, request);
                break;
            case WorkspaceQuickAddKinds.Contact:
                AddContact(root, request);
                break;
            case WorkspaceQuickAddKinds.Pet:
                AddPet(root, request);
                break;
            case WorkspaceQuickAddKinds.Vehicle:
                AddVehicle(root, request);
                break;
            case WorkspaceQuickAddKinds.Quality:
                AddQuality(root, request);
                break;
            case WorkspaceQuickAddKinds.Drug:
                AddDrug(root, request);
                break;
            case WorkspaceQuickAddKinds.Cyberware:
                AddCyberware(root, request);
                break;
            case WorkspaceQuickAddKinds.Spell:
                AddSpell(root, request);
                break;
            case WorkspaceQuickAddKinds.Power:
                AddPower(root, request);
                break;
            case WorkspaceQuickAddKinds.ComplexForm:
                AddComplexForm(root, request);
                break;
            case WorkspaceQuickAddKinds.MatrixProgram:
                AddMatrixProgram(root, request);
                break;
            case WorkspaceQuickAddKinds.InitiationGrade:
                AddInitiationGrade(root, request);
                break;
            case WorkspaceQuickAddKinds.Spirit:
                AddSpirit(root, request);
                break;
            case WorkspaceQuickAddKinds.CritterPower:
                AddCritterPower(root, request);
                break;
            default:
                throw new InvalidOperationException($"Unsupported quick-add kind '{request.Kind}'.");
        }

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    public static string ApplyAttributeEdit(string xml, AttributeEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");

        XElement attributes = EnsureElement(root, "attributes");
        XElement attribute = attributes.Elements("attribute")
            .FirstOrDefault(candidate => AttributeNamesMatch(candidate.Element("name")?.Value, request.AttributeName))
            ?? throw new InvalidOperationException($"Attribute '{request.AttributeName}' was not found in the workspace XML.");

        string attributeName = FirstNonBlank(attribute.Element("name")?.Value);
        bool isEdgeAttribute = AttributeWorkbenchProjector.IsEdgeAttribute(attributeName)
            || AttributeWorkbenchProjector.IsEdgeAttribute(request.AttributeName);
        string normalizedBucket = FirstNonBlank(request.Bucket).ToLowerInvariant();
        int requestedValue = Math.Max(0, request.Value);
        bool created = ParseBool(root.Element("created")?.Value);
        XElement baseElement = EnsureElement(attribute, "base");
        XElement karmaElement = EnsureElement(attribute, "karma");
        int currentBaseValue = ParseInt(baseElement.Value);
        int currentKarmaValue = ParseInt(karmaElement.Value);
        int currentTotalValue = ParseInt(
            FirstNonBlank(attribute.Element("totalvalue")?.Value, attribute.Element("value")?.Value),
            currentBaseValue + currentKarmaValue);
        if (currentKarmaValue == 0 && currentTotalValue >= currentBaseValue)
        {
            currentKarmaValue = currentTotalValue - currentBaseValue;
        }

        int metatypeMin = Math.Max(0, ParseInt(attribute.Element("metatypemin")?.Value, fallback: 0));
        int metatypeMax = Math.Max(metatypeMin, ParseInt(attribute.Element("metatypemax")?.Value, fallback: Math.Max(currentBaseValue, requestedValue)));
        int metatypeAugMax = Math.Max(metatypeMax, ParseInt(attribute.Element("metatypeaugmax")?.Value, fallback: metatypeMax));

        switch (normalizedBucket)
        {
            case "base":
                currentBaseValue = Math.Clamp(requestedValue, metatypeMin, metatypeMax);
                break;
            case "karma":
                currentKarmaValue = Math.Clamp(requestedValue, 0, Math.Max(0, metatypeAugMax - currentBaseValue));
                break;
            case "improve":
                if (!created)
                {
                    throw new InvalidOperationException($"Attribute '{request.AttributeName}' can only be improved from a created/career workspace.");
                }

                int improveCost = ComputeCareerAttributeUpgradeCost(currentBaseValue + currentKarmaValue, metatypeAugMax);
                if (improveCost <= 0)
                {
                    throw new InvalidOperationException($"Attribute '{request.AttributeName}' is already at its current ceiling.");
                }

                XElement totalKarmaElement = EnsureElement(root, "karma");
                decimal availableKarma = ParseDecimal(totalKarmaElement.Value);
                if (availableKarma < improveCost)
                {
                    throw new InvalidOperationException($"Attribute '{request.AttributeName}' requires {improveCost} Karma but only {availableKarma.ToString(CultureInfo.InvariantCulture)} is available.");
                }

                availableKarma -= improveCost;
                if (isEdgeAttribute && metatypeMin < 1 && currentBaseValue == metatypeMin && currentKarmaValue == 0)
                {
                    metatypeMin += 1;
                    currentBaseValue += 1;
                }
                else
                {
                    currentKarmaValue += 1;
                }

                totalKarmaElement.Value = availableKarma.ToString(CultureInfo.InvariantCulture);
                AppendKarmaExpense(root, improveCost, $"Improve {AttributeWorkbenchProjector.FormatFullLabel(attributeName)}");
                break;
            case "burn":
                if (!isEdgeAttribute)
                {
                    throw new InvalidOperationException($"Attribute '{request.AttributeName}' does not support the burn workflow.");
                }

                if (currentKarmaValue > 0)
                {
                    currentKarmaValue -= 1;
                }
                else if (currentBaseValue > metatypeMin)
                {
                    currentBaseValue -= 1;
                }
                else if (currentBaseValue > 0 && metatypeMin > 0)
                {
                    currentBaseValue -= 1;
                    metatypeMin -= 1;
                }

                break;
            default:
                throw new InvalidOperationException($"Unsupported attribute edit bucket '{request.Bucket}'.");
        }

        if (currentBaseValue + currentKarmaValue > metatypeAugMax)
        {
            if (string.Equals(normalizedBucket, "base", StringComparison.Ordinal))
            {
                currentKarmaValue = Math.Max(0, metatypeAugMax - currentBaseValue);
            }
            else
            {
                currentBaseValue = Math.Max(metatypeMin, metatypeAugMax - currentKarmaValue);
            }
        }

        baseElement.Value = currentBaseValue.ToString(CultureInfo.InvariantCulture);
        karmaElement.Value = currentKarmaValue.ToString(CultureInfo.InvariantCulture);
        EnsureElement(attribute, "metatypemin").Value = metatypeMin.ToString(CultureInfo.InvariantCulture);
        EnsureElement(attribute, "value").Value = currentBaseValue.ToString(CultureInfo.InvariantCulture);
        EnsureElement(attribute, "totalvalue").Value = (currentBaseValue + currentKarmaValue).ToString(CultureInfo.InvariantCulture);

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    public static string ApplyOriginDossierEdit(string xml, OriginDossierEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");

        SetElementValue(root, "name", request.Name);
        SetElementValue(root, "alias", request.Alias);
        SetElementValue(root, "playername", request.PlayerName);
        SetElementValue(root, "sex", request.Sex);
        SetElementValue(root, "age", request.Age);
        SetElementValue(root, "height", request.Height);
        SetElementValue(root, "weight", request.Weight);
        SetElementValue(root, "hair", request.Hair);
        SetElementValue(root, "eyes", request.Eyes);
        SetElementValue(root, "skin", request.Skin);
        SetElementValue(root, "concept", request.Concept);
        SetElementValue(root, "description", request.Description);
        SetElementValue(root, "background", request.Background);

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    public static string ApplyConditionMonitorEdit(string xml, ConditionMonitorEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Condition monitors can only be changed for a created/career runner.");
        }

        (string filledElement, long maximumValue) = request.Track switch
        {
            WorkspaceConditionMonitorTrack.Physical => (
                "physicalcmfilled",
                (long)Math.Max(0, ParseInt(root.Element("physicalcm")?.Value))
                    + Math.Max(0, ParseInt(root.Element("physicalcmoverflow")?.Value))),
            WorkspaceConditionMonitorTrack.Stun => (
                "stuncmfilled",
                (long)Math.Max(0, ParseInt(root.Element("stuncm")?.Value))),
            _ => throw new InvalidOperationException($"Unsupported condition monitor track '{request.Track}'.")
        };
        if (maximumValue <= 0 || maximumValue > MaximumConditionBoxes)
        {
            throw new InvalidOperationException($"The {request.Track} condition monitor is not available for this runner.");
        }
        int maximum = (int)maximumValue;

        if (request.Filled < 0 || request.Filled > maximum)
        {
            throw new InvalidOperationException(
                $"The {request.Track} condition monitor must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(root, filledElement, request.Filled.ToString(CultureInfo.InvariantCulture));
        return Serialize(document);
    }

    public static string ApplyCollectionMutation(
        string xml,
        WorkspaceCollectionMutationRequest request,
        ICharacterSourceDataResolver? sourceDataResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);

        switch (request)
        {
            case WorkspaceSetCollectionTextRequest textRequest:
                ApplyTextMutation(root, textRequest);
                break;
            case WorkspaceSetCollectionRatingRequest ratingRequest:
                ApplyRatingMutation(root, ratingRequest);
                break;
            case WorkspaceSetCollectionQuantityRequest quantityRequest:
                ApplyQuantityMutation(root, quantityRequest);
                break;
            case WorkspaceSetCollectionToggleRequest toggleRequest:
                ApplyToggleMutation(root, toggleRequest);
                break;
            case WorkspacePatchCollectionItemRequest patchRequest:
                ApplyPatchMutation(root, patchRequest, sourceData);
                break;
            case WorkspaceMoveCollectionItemRequest moveRequest:
                ApplyMoveMutation(root, moveRequest);
                break;
            case WorkspaceDeleteCollectionItemRequest deleteRequest:
                ApplyDeleteMutation(root, deleteRequest.Target);
                break;
            case WorkspaceSetLinkedCharacterRequest linkedCharacterRequest:
                ApplyLinkedCharacterMutation(root, linkedCharacterRequest);
                break;
            case WorkspaceRemoveLinkedCharacterRequest removeLinkedCharacterRequest:
                ApplyRemoveLinkedCharacterMutation(root, removeLinkedCharacterRequest.Target);
                break;
            case WorkspaceAddNestedCollectionItemRequest addNestedRequest:
                ApplyAddNestedMutation(root, addNestedRequest);
                break;
            default:
                throw new InvalidOperationException($"Unsupported collection mutation request '{request.GetType().Name}'.");
        }

        return Serialize(document);
    }

    private static void ApplyPatchMutation(
        XElement root,
        WorkspacePatchCollectionItemRequest request,
        ICharacterSourceDataContext? sourceData)
    {
        bool hasChanges = request.TextValues is { Count: > 0 }
            || request.Rating is not null
            || request.Quantity is not null
            || request.ToggleValues is { Count: > 0 }
            || request.VehiclePhysicalDamage is not null
            || request.VehicleMatrixDamage is not null
            || request.GearMatrixDamage is not null
            || request.ArmorMatrixDamage is not null
            || request.WeaponMatrixDamage is not null
            || request.CyberwareMatrixDamage is not null
            || request.ContactConnection is not null
            || request.ContactLoyalty is not null;
        if (!hasChanges)
        {
            throw new InvalidOperationException("A collection item patch must contain at least one changed value.");
        }

        IEnumerable<KeyValuePair<WorkspaceCollectionTextField, string?>> textValues = request.TextValues is null
            ? Enumerable.Empty<KeyValuePair<WorkspaceCollectionTextField, string?>>()
            : request.TextValues.OrderBy(static pair => pair.Key);
        foreach ((WorkspaceCollectionTextField field, string? value) in textValues)
        {
            ApplyTextMutation(root, new WorkspaceSetCollectionTextRequest(request.Target, field, value));
        }

        if (request.Rating is int rating)
        {
            ApplyRatingMutation(root, new WorkspaceSetCollectionRatingRequest(request.Target, rating));
        }

        if (request.Quantity is decimal quantity)
        {
            ApplyQuantityMutation(root, new WorkspaceSetCollectionQuantityRequest(request.Target, quantity));
        }

        IEnumerable<KeyValuePair<WorkspaceCollectionToggleField, bool>> toggleValues = request.ToggleValues is null
            ? Enumerable.Empty<KeyValuePair<WorkspaceCollectionToggleField, bool>>()
            : request.ToggleValues.OrderBy(static pair => pair.Key);
        foreach ((WorkspaceCollectionToggleField field, bool value) in toggleValues)
        {
            ApplyToggleMutation(root, new WorkspaceSetCollectionToggleRequest(request.Target, field, value));
        }

        if (request.VehiclePhysicalDamage is int vehiclePhysicalDamage)
        {
            ApplyVehiclePhysicalDamageMutation(root, request.Target, vehiclePhysicalDamage, sourceData);
        }
        if (request.VehicleMatrixDamage is int vehicleMatrixDamage)
        {
            ApplyVehicleMatrixDamageMutation(root, request.Target, vehicleMatrixDamage, sourceData);
        }
        if (request.GearMatrixDamage is int gearMatrixDamage)
        {
            ApplyGearMatrixDamageMutation(root, request.Target, gearMatrixDamage);
        }
        if (request.ArmorMatrixDamage is int armorMatrixDamage)
        {
            ApplyArmorMatrixDamageMutation(root, request.Target, armorMatrixDamage);
        }
        if (request.WeaponMatrixDamage is int weaponMatrixDamage)
        {
            ApplyWeaponMatrixDamageMutation(
                root,
                request.Target,
                weaponMatrixDamage,
                sourceData);
        }
        if (request.CyberwareMatrixDamage is int cyberwareMatrixDamage)
        {
            ApplyCyberwareMatrixDamageMutation(root, request.Target, cyberwareMatrixDamage, sourceData);
        }
        if (request.ContactConnection is int contactConnection)
        {
            ApplyContactConnectionMutation(root, request.Target, contactConnection);
        }
        if (request.ContactLoyalty is int contactLoyalty)
        {
            ApplyContactLoyaltyMutation(root, request.Target, contactLoyalty);
        }
    }

    private static void ApplyContactConnectionMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int value)
    {
        ResolvedCollectionItem resolved = ResolveContact(root, target);
        CharacterContactEditSemantics semantics = ResolveContactSemantics(root, resolved.Item);
        if (!semantics.ConnectionEditable)
        {
            throw new InvalidOperationException("This contact's Connection rating is read-only.");
        }
        if (value < 1 || value > semantics.ConnectionMaximum)
        {
            throw new InvalidOperationException(
                $"Contact Connection must be between 1 and {semantics.ConnectionMaximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "connection", value.ToString(CultureInfo.InvariantCulture));
    }

    private static void ApplyContactLoyaltyMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int value)
    {
        ResolvedCollectionItem resolved = ResolveContact(root, target);
        CharacterContactEditSemantics semantics = ResolveContactSemantics(root, resolved.Item);
        if (!semantics.LoyaltyEditable)
        {
            throw new InvalidOperationException("This contact's Loyalty rating is read-only.");
        }
        if (value is < 1 or > 6)
        {
            throw new InvalidOperationException("Contact Loyalty must be between 1 and 6.");
        }

        SetElementValue(resolved.Item, "loyalty", value.ToString(CultureInfo.InvariantCulture));
    }

    private static void ApplyVehiclePhysicalDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled,
        ICharacterSourceDataContext? sourceData)
    {
        if (target.Kind != WorkspaceCollectionKind.Vehicle || target.NestedKind is not null)
        {
            throw new InvalidOperationException("Vehicle physical damage requires a top-level vehicle target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Vehicle physical damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        if (!TryParseRequiredInt(resolved.Item.Element("body")?.Value, out int baseBody)
            || !TryBuildVehicleConditionModifiers(
                resolved.Item,
                sourceData,
                out CharacterVehicleConditionModifierBasis[] modifiers)
            || !CharacterVehicleConditionMonitorCalculator.TryCalculatePhysicalMaximum(
                resolved.Item.Element("category")?.Value,
                baseBody,
                modifiers,
                out int maximum))
        {
            throw new InvalidOperationException(
                "Vehicle physical damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Vehicle physical damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "physicalcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static void ApplyVehicleMatrixDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled,
        ICharacterSourceDataContext? sourceData)
    {
        if (target.Kind != WorkspaceCollectionKind.Vehicle || target.NestedKind is not null)
        {
            throw new InvalidOperationException("Vehicle Matrix damage requires a top-level vehicle target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Vehicle Matrix damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        CharacterMatrixImprovementBasis improvementBasis = BuildCharacterMatrixImprovementBasis(
            root,
            careerMode: true);
        if (!TryCalculateVehicleMatrixMaximum(resolved.Item, improvementBasis, sourceData, out int maximum))
        {
            throw new InvalidOperationException(
                "Vehicle Matrix damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Vehicle Matrix damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "matrixcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateVehicleMatrixMaximum(
        XElement vehicle,
        CharacterMatrixImprovementBasis improvementBasis,
        ICharacterSourceDataContext? sourceData,
        out int maximum)
    {
        maximum = 0;
        string deviceRatingText = vehicle.Element("devicerating")?.Value.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(deviceRatingText))
        {
            deviceRatingText = vehicle.Element("pilot")?.Value.Trim() ?? string.Empty;
        }
        if (!TryParseRequiredInt(deviceRatingText, out int baseDeviceRating))
        {
            return false;
        }

        long deviceRatingBonus = 0;
        long conditionBonus = 0;
        foreach (XElement modifier in vehicle.Element("mods")?.Elements("mod") ?? [])
        {
            if (!TryParseOptionalBool(modifier.Element("wirelesson")?.Value, out bool wirelessEnabled)
                || !TryReadEffectiveVehicleModBonuses(
                    modifier,
                    sourceData,
                    requireWireless: wirelessEnabled,
                    out CharacterVehicleModSourceBonuses sourceBonuses))
            {
                return false;
            }

            XElement? bonus = modifier.Element("bonus");
            deviceRatingBonus += ParseInt(
                bonus?.Element("devicerating")?.Value ?? sourceBonuses.DeviceRatingExpression);
            conditionBonus += ParseInt(
                bonus?.Element("matrixcmbonus")?.Value ?? sourceBonuses.MatrixConditionExpression);
            if (wirelessEnabled)
            {
                XElement? wirelessBonus = modifier.Element("wirelessbonus");
                deviceRatingBonus += ParseInt(
                    wirelessBonus?.Element("devicerating")?.Value
                    ?? sourceBonuses.WirelessDeviceRatingExpression);
                conditionBonus += ParseInt(
                    wirelessBonus?.Element("matrixcmbonus")?.Value
                    ?? sourceBonuses.WirelessMatrixConditionExpression);
            }
        }

        foreach (XElement gear in vehicle.Element("gears")?.Elements("gear") ?? [])
        {
            if (!TryParseOptionalBool(gear.Element("equipped")?.Value, out bool equipped))
            {
                return false;
            }
            if (equipped)
            {
                if (!TryCalculateGearTotalBonusMatrixBoxes(
                    gear,
                    improvementBasis,
                    out int gearConditionBonus))
                {
                    return false;
                }
                conditionBonus += gearConditionBonus;
            }
        }

        long totalDeviceRating = baseDeviceRating + deviceRatingBonus;
        if (string.Equals(
                vehicle.Element("overclocked")?.Value.Trim(),
                "Device Rating",
                StringComparison.Ordinal)
            && improvementBasis.OverclockerEnabled)
        {
            totalDeviceRating++;
        }
        if (totalDeviceRating is < int.MinValue or > int.MaxValue
            || conditionBonus is < int.MinValue or > int.MaxValue)
        {
            return false;
        }
        return CharacterMatrixConditionMonitorCalculator.TryCalculateMaximum(
            (int)totalDeviceRating,
            (int)conditionBonus,
            out maximum);
    }

    private static void ApplyGearMatrixDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled)
    {
        if (target.Kind != WorkspaceCollectionKind.Gear
            || target.NestedKind is not null and not WorkspaceNestedCollectionKind.Gear)
        {
            throw new InvalidOperationException("Gear Matrix damage requires a Gear target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Gear Matrix damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        CharacterMatrixImprovementBasis improvementBasis = BuildCharacterMatrixImprovementBasis(
            root,
            careerMode: true);
        if (!TryCalculateGearMatrixMaximum(resolved.Item, improvementBasis, out int maximum))
        {
            throw new InvalidOperationException(
                "Gear Matrix damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Gear Matrix damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "matrixcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateGearMatrixMaximum(
        XElement gear,
        CharacterMatrixImprovementBasis improvementBasis,
        out int maximum)
    {
        maximum = 0;
        if (!TryParseOptionalInt(gear.Element("rating")?.Value, out int rating))
        {
            return false;
        }

        string deviceRatingExpression = gear.Element("devicerating")?.Value.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(deviceRatingExpression))
        {
            bool isCommlink = (gear.Element("canformpersona")?.Value ?? string.Empty)
                    .Contains("Self", StringComparison.Ordinal)
                || (gear.Element("children")?.Elements("gear") ?? [])
                    .Any(child => (child.Element("canformpersona")?.Value ?? string.Empty)
                        .Contains("Parent", StringComparison.Ordinal));
            deviceRatingExpression = isCommlink ? "2" : "0";
        }
        if (string.Equals(gear.Element("name")?.Value.Trim(), "Living Persona", StringComparison.Ordinal))
        {
            if (!improvementBasis.LivingPersonaDeviceRatingExact)
            {
                return false;
            }
            deviceRatingExpression += improvementBasis.LivingPersonaDeviceRatingSuffix;
        }
        if (!CharacterVehicleConditionMonitorCalculator.TryResolveRatingExpression(
                deviceRatingExpression,
                rating,
                improvementBasis.SavedAttributeTotals,
                out int deviceRating))
        {
            return false;
        }

        if (string.Equals(
                gear.Element("overclocked")?.Value.Trim(),
                "Device Rating",
                StringComparison.Ordinal)
            && improvementBasis.OverclockerEnabled)
        {
            if (deviceRating == int.MaxValue)
            {
                return false;
            }
            deviceRating++;
        }

        return TryCalculateGearTotalBonusMatrixBoxes(gear, improvementBasis, out int bonusMatrixBoxes)
            && CharacterMatrixConditionMonitorCalculator.TryCalculateMaximum(
                deviceRating,
                bonusMatrixBoxes,
                out maximum);
    }

    private static bool TryCalculateGearTotalBonusMatrixBoxes(
        XElement gear,
        CharacterMatrixImprovementBasis improvementBasis,
        out int total)
    {
        total = 0;
        if (!TryParseOptionalInt(gear.Element("matrixcmbonus")?.Value, out int ownBonus))
        {
            return false;
        }

        long calculated = ownBonus;
        if (string.Equals(gear.Element("name")?.Value.Trim(), "Living Persona", StringComparison.Ordinal))
        {
            if (!improvementBasis.LivingPersonaConditionMonitorExact)
            {
                return false;
            }
            string expression = improvementBasis.LivingPersonaConditionMonitorExpression;
            if (!string.IsNullOrEmpty(expression))
            {
                if (!TryParseOptionalInt(gear.Element("rating")?.Value, out int rating)
                    || !CharacterVehicleConditionMonitorCalculator.TryResolveRatingExpression(
                        expression,
                        rating,
                        improvementBasis.SavedAttributeTotals,
                        out int livingPersonaBonus))
                {
                    return false;
                }
                calculated += livingPersonaBonus;
            }
        }
        foreach (XElement child in gear.Element("children")?.Elements("gear") ?? [])
        {
            if (!TryParseOptionalBool(child.Element("equipped")?.Value, out bool equipped))
            {
                return false;
            }
            if (!equipped)
            {
                continue;
            }
            if (!TryCalculateGearTotalBonusMatrixBoxes(child, improvementBasis, out int childBonus))
            {
                return false;
            }
            calculated += childBonus;
        }

        if (calculated is < int.MinValue or > int.MaxValue)
        {
            return false;
        }
        total = (int)calculated;
        return true;
    }

    private static void ApplyArmorMatrixDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled)
    {
        if (target.Kind != WorkspaceCollectionKind.Armor || target.NestedKind is not null)
        {
            throw new InvalidOperationException("Armor Matrix damage requires a top-level Armor target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Armor Matrix damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        CharacterMatrixImprovementBasis improvementBasis = BuildCharacterMatrixImprovementBasis(
            root,
            careerMode: true);
        if (!TryCalculateArmorMatrixMaximum(resolved.Item, improvementBasis, out int maximum))
        {
            throw new InvalidOperationException(
                "Armor Matrix damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Armor Matrix damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "matrixcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateArmorMatrixMaximum(
        XElement armor,
        CharacterMatrixImprovementBasis improvementBasis,
        out int maximum)
    {
        maximum = 0;
        if (!TryParseOptionalInt(armor.Element("matrixcmbonus")?.Value, out int ownBonus))
        {
            return false;
        }

        string deviceRatingText = armor.Element("devicerating")?.Value.Trim() ?? string.Empty;
        int deviceRating;
        if (string.IsNullOrWhiteSpace(deviceRatingText))
        {
            deviceRating = 2;
        }
        else if (!TryParseRequiredInt(deviceRatingText, out deviceRating))
        {
            return false;
        }

        if (string.Equals(
                armor.Element("overclocked")?.Value.Trim(),
                "Device Rating",
                StringComparison.Ordinal)
            && improvementBasis.OverclockerEnabled)
        {
            if (deviceRating == int.MaxValue)
            {
                return false;
            }
            deviceRating++;
        }

        long conditionBonus = ownBonus;
        foreach (XElement gear in (armor.Element("gears")?.Elements("gear") ?? [])
                     .Concat(armor.Element("children")?.Elements("gear") ?? []))
        {
            if (!TryParseOptionalBool(gear.Element("equipped")?.Value, out bool equipped))
            {
                return false;
            }
            if (!equipped)
            {
                continue;
            }
            if (!TryCalculateGearTotalBonusMatrixBoxes(gear, improvementBasis, out int gearBonus))
            {
                return false;
            }
            conditionBonus += gearBonus;
        }

        return conditionBonus is >= int.MinValue and <= int.MaxValue
            && CharacterMatrixConditionMonitorCalculator.TryCalculateMaximum(
                deviceRating,
                (int)conditionBonus,
                out maximum);
    }

    private static void ApplyWeaponMatrixDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled,
        ICharacterSourceDataContext? sourceData)
    {
        if (target.Kind != WorkspaceCollectionKind.Weapon || target.NestedKind is not null)
        {
            throw new InvalidOperationException("Weapon Matrix damage requires a top-level Weapon target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Weapon Matrix damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        CharacterMatrixImprovementBasis improvementBasis = BuildCharacterMatrixImprovementBasis(
            root,
            careerMode: true);
        if (!CharacterWeaponMatrixParentResolver.TryResolveOwner(
                root,
                resolved.Item,
                out CharacterMatrixOwner owner)
            || !TryCalculateMatrixOwnerMaximum(
                owner,
                improvementBasis,
                sourceData,
                out int maximum))
        {
            throw new InvalidOperationException(
                "Weapon Matrix damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Weapon Matrix damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(owner.Item, "matrixcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateMatrixOwnerMaximum(
        CharacterMatrixOwner owner,
        CharacterMatrixImprovementBasis improvementBasis,
        ICharacterSourceDataContext? sourceData,
        out int maximum)
        => owner.Kind switch
        {
            CharacterMatrixOwnerKind.Gear => TryCalculateGearMatrixMaximum(
                owner.Item,
                improvementBasis,
                out maximum),
            CharacterMatrixOwnerKind.Armor => TryCalculateArmorMatrixMaximum(
                owner.Item,
                improvementBasis,
                out maximum),
            CharacterMatrixOwnerKind.Weapon => TryCalculateWeaponOwnMatrixMaximum(
                owner.Item,
                improvementBasis,
                out maximum),
            CharacterMatrixOwnerKind.Cyberware => TryCalculateCyberwareMatrixMaximum(
                owner.Item,
                improvementBasis,
                sourceData,
                out maximum),
            CharacterMatrixOwnerKind.Vehicle => TryCalculateVehicleMatrixMaximum(
                owner.Item,
                improvementBasis,
                sourceData,
                out maximum),
            _ => AssignUnavailableMaximum(out maximum)
        };

    private static bool AssignUnavailableMaximum(out int maximum)
    {
        maximum = 0;
        return false;
    }

    private static bool TryCalculateWeaponOwnMatrixMaximum(
        XElement weapon,
        CharacterMatrixImprovementBasis improvementBasis,
        out int maximum)
    {
        maximum = 0;
        if (!TryParseOptionalInt(weapon.Element("rating")?.Value, out int rating))
        {
            return false;
        }

        string deviceRatingExpression = weapon.Element("devicerating")?.Value.Trim() ?? string.Empty;
        int deviceRating;
        if (string.IsNullOrWhiteSpace(deviceRatingExpression))
        {
            deviceRating = 2;
        }
        else if (!CharacterVehicleConditionMonitorCalculator.TryResolveRatingExpression(
            deviceRatingExpression,
            rating,
            improvementBasis.SavedAttributeTotals,
            out deviceRating))
        {
            return false;
        }

        if (string.Equals(
                weapon.Element("overclocked")?.Value.Trim(),
                "Device Rating",
                StringComparison.Ordinal)
            && improvementBasis.OverclockerEnabled)
        {
            if (deviceRating == int.MaxValue)
            {
                return false;
            }
            deviceRating++;
        }

        return CharacterMatrixConditionMonitorCalculator.TryCalculateMaximum(
            deviceRating,
            totalBonusMatrixBoxes: 0,
            out maximum);
    }

    private static void ApplyCyberwareMatrixDamageMutation(
        XElement root,
        WorkspaceCollectionItemTarget target,
        int filled,
        ICharacterSourceDataContext? sourceData)
    {
        if (target.Kind != WorkspaceCollectionKind.Cyberware
            || target.NestedKind is not null and not WorkspaceNestedCollectionKind.CyberwarePlugin)
        {
            throw new InvalidOperationException("Cyberware Matrix damage requires a Cyberware target.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Cyberware Matrix damage can only be changed for a created/career runner.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        CharacterMatrixImprovementBasis improvementBasis = BuildCharacterMatrixImprovementBasis(
            root,
            careerMode: true);
        if (!TryCalculateCyberwareMatrixMaximum(
                resolved.Item,
                improvementBasis,
                sourceData,
                out int maximum))
        {
            throw new InvalidOperationException(
                "Cyberware Matrix damage cannot be changed because its exact condition-monitor maximum is unavailable from the saved runner data.");
        }
        if (filled < 0 || filled > maximum)
        {
            throw new InvalidOperationException(
                $"Cyberware Matrix damage must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "matrixcmfilled", filled.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateCyberwareMatrixMaximum(
        XElement cyberware,
        CharacterMatrixImprovementBasis improvementBasis,
        ICharacterSourceDataContext? sourceData,
        out int maximum)
    {
        maximum = 0;
        if (!TryParseOptionalInt(cyberware.Element("rating")?.Value, out int rating))
        {
            return false;
        }

        string deviceRatingExpression = cyberware.Element("devicerating")?.Value.Trim() ?? string.Empty;
        int deviceRating;
        if (string.IsNullOrWhiteSpace(deviceRatingExpression))
        {
            if (sourceData?.TryResolveCyberwareGradeDeviceRating(
                    cyberware.Element("grade")?.Value.Trim() ?? string.Empty,
                    cyberware.Element("improvementsource")?.Value.Trim() ?? string.Empty,
                    out deviceRating) != true)
            {
                return false;
            }
        }
        else if (!CharacterVehicleConditionMonitorCalculator.TryResolveRatingExpression(
                     deviceRatingExpression,
                     rating,
                     improvementBasis.SavedAttributeTotals,
                     out deviceRating))
        {
            return false;
        }

        if (!TryCalculateCyberwareTotalBonusMatrixBoxes(
                cyberware,
                improvementBasis,
                out int bonusMatrixBoxes))
        {
            return false;
        }

        if (string.Equals(
                cyberware.Element("overclocked")?.Value.Trim(),
                "Device Rating",
                StringComparison.Ordinal)
            && improvementBasis.OverclockerEnabled)
        {
            if (deviceRating == int.MaxValue)
            {
                return false;
            }
            deviceRating++;
        }

        return CharacterMatrixConditionMonitorCalculator.TryCalculateMaximum(
            deviceRating,
            bonusMatrixBoxes,
            out maximum);
    }

    private static bool TryCalculateCyberwareTotalBonusMatrixBoxes(
        XElement cyberware,
        CharacterMatrixImprovementBasis improvementBasis,
        out int total)
    {
        // Chummer5 Cyberware.TotalBonusMatrixBoxes sums descendant ware/gear, not the ware's own
        // saved matrixcmbonus value. Preserve that behavior until the legacy implementation changes.
        long calculated = 0;
        foreach (XElement child in EnumerateCyberwareChildrenForMatrix(cyberware))
        {
            if (!TryCalculateCyberwareTotalBonusMatrixBoxes(
                child,
                improvementBasis,
                out int childBonus))
            {
                total = 0;
                return false;
            }
            calculated += childBonus;
        }

        foreach (XElement gear in cyberware.Element("gears")?.Elements("gear") ?? [])
        {
            if (!TryParseOptionalBool(gear.Element("equipped")?.Value, out bool equipped))
            {
                total = 0;
                return false;
            }
            if (!equipped)
            {
                continue;
            }
            if (!TryCalculateGearTotalBonusMatrixBoxes(gear, improvementBasis, out int gearBonus))
            {
                total = 0;
                return false;
            }
            calculated += gearBonus;
        }

        if (calculated is < int.MinValue or > int.MaxValue)
        {
            total = 0;
            return false;
        }
        total = (int)calculated;
        return true;
    }

    private static IEnumerable<XElement> EnumerateCyberwareChildrenForMatrix(XElement cyberware)
        => cyberware.Elements("cyberware")
            .Concat(cyberware.Element("children")?.Elements("cyberware") ?? [])
            .Concat(cyberware.Element("cyberwares")?.Elements("cyberware") ?? []);

    private static CharacterMatrixImprovementBasis BuildCharacterMatrixImprovementBasis(
        XElement character,
        bool careerMode)
    {
        XElement[] improvements = character
            .Element("improvements")?
            .Elements("improvement")
            .ToArray()
            ?? [];
        bool overclockerEnabled = improvements.Any(improvement =>
            string.Equals(
                improvement.Element("improvementttype")?.Value.Trim(),
                "Overclocker",
                StringComparison.Ordinal)
            && IsApplicableValueImprovement(improvement, careerMode));
        bool deviceRatingExact = TryReadLivingPersonaImprovementExpression(
            improvements,
            "LivingPersonaDeviceRating",
            careerMode,
            out string deviceRatingSuffix);
        bool conditionMonitorExact = TryReadLivingPersonaImprovementExpression(
            improvements,
            "LivingPersonaMatrixCM",
            careerMode,
            out string conditionMonitorExpression);

        return new CharacterMatrixImprovementBasis(
            OverclockerEnabled: overclockerEnabled,
            LivingPersonaDeviceRatingExact: deviceRatingExact,
            LivingPersonaDeviceRatingSuffix: deviceRatingSuffix,
            LivingPersonaConditionMonitorExact: conditionMonitorExact,
            LivingPersonaConditionMonitorExpression: conditionMonitorExpression,
            SavedAttributeTotals: ReadSavedAttributeTotals(character));
    }

    private static IReadOnlyDictionary<string, int> ReadSavedAttributeTotals(XElement character)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        foreach (XElement attribute in character.Element("attributes")?.Elements("attribute") ?? [])
        {
            string name = attribute.Element("name")?.Value.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(name) || unavailable.Contains(name))
            {
                continue;
            }

            if (!int.TryParse(
                    attribute.Element("totalvalue")?.Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int total)
                || !totals.TryAdd(name, total))
            {
                totals.Remove(name);
                unavailable.Add(name);
            }
        }
        return totals;
    }

    private static bool TryReadLivingPersonaImprovementExpression(
        IEnumerable<XElement> improvements,
        string improvementType,
        bool careerMode,
        out string expression)
    {
        List<CharacterMatrixImprovementFragment> fragments = [];
        foreach (XElement improvement in improvements)
        {
            if (!string.Equals(
                    improvement.Element("improvementttype")?.Value.Trim(),
                    improvementType,
                    StringComparison.Ordinal)
                || !IsApplicableValueImprovement(improvement, careerMode))
            {
                continue;
            }

            string valueText = improvement.Element("val")?.Value.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(valueText)
                && !decimal.TryParse(
                    valueText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out _))
            {
                expression = string.Empty;
                return false;
            }

            fragments.Add(new CharacterMatrixImprovementFragment(
                Expression: improvement.Element("improvedname")?.Value.Trim() ?? string.Empty,
                Value: string.IsNullOrEmpty(valueText)
                    ? 0m
                    : decimal.Parse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture),
                UniqueName: improvement.Element("unique")?.Value.Trim() ?? string.Empty,
                Custom: ParseBool(improvement.Element("custom")?.Value)));
        }

        if (!CharacterMatrixImprovementSelector.TrySelectExpressions(fragments, out IReadOnlyList<string> selected))
        {
            expression = string.Empty;
            return false;
        }
        foreach (string fragment in selected)
        {
            if (!string.IsNullOrEmpty(fragment) && fragment[0] is not ('+' or '-'))
            {
                expression = string.Empty;
                return false;
            }
        }

        expression = string.Concat(selected);
        return true;
    }

    private static bool IsApplicableValueImprovement(XElement improvement, bool careerMode)
    {
        if (ReadLegacyImprovementIntegerFlag(improvement, "enabled", defaultValue: 1) <= 0
            || ReadLegacyImprovementIntegerFlag(improvement, "addtorating", defaultValue: 0) > 0)
        {
            return false;
        }

        string condition = improvement.Element("condition")?.Value.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(condition)
            || string.Equals(
                condition,
                careerMode ? "career" : "create",
                StringComparison.Ordinal);
    }

    private static int ReadLegacyImprovementIntegerFlag(
        XElement improvement,
        string nodeName,
        int defaultValue)
    {
        string value = improvement.Element(nodeName)?.Value.Trim() ?? string.Empty;
        return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : defaultValue;
    }

    private static bool TryBuildVehicleConditionModifiers(
        XElement vehicle,
        ICharacterSourceDataContext? sourceData,
        out CharacterVehicleConditionModifierBasis[] modifiers)
    {
        List<CharacterVehicleConditionModifierBasis> result = [];
        foreach (XElement modifier in vehicle.Element("mods")?.Elements("mod") ?? [])
        {
            if (!TryParseOptionalBool(modifier.Element("included")?.Value, out bool included)
                || !TryParseOptionalBool(modifier.Element("equipped")?.Value, out bool equipped)
                || !TryParseOptionalInt(modifier.Element("conditionmonitor")?.Value, out int conditionBonus)
                || !TryParseOptionalInt(modifier.Element("rating")?.Value, out int rating))
            {
                modifiers = [];
                return false;
            }

            int? effectiveBodyBonus = 0;
            if (!included && equipped)
            {
                if (!TryReadEffectiveVehicleBodyBonus(modifier, rating, sourceData, out int bodyBonus))
                {
                    modifiers = [];
                    return false;
                }
                effectiveBodyBonus = bodyBonus;
            }
            result.Add(new CharacterVehicleConditionModifierBasis(
                IncludedInVehicle: included,
                Equipped: equipped,
                ConditionMonitorBonus: conditionBonus,
                EffectiveBodyBonus: effectiveBodyBonus));
        }

        modifiers = result.ToArray();
        return true;
    }

    private static bool TryReadEffectiveVehicleBodyBonus(
        XElement modifier,
        int rating,
        ICharacterSourceDataContext? sourceData,
        out int bodyBonus)
    {
        bodyBonus = 0;
        XElement? bonus = modifier.Element("bonus");
        if (!TryParseOptionalBool(modifier.Element("wirelesson")?.Value, out bool wirelessEnabled)
            || !TryReadEffectiveVehicleModBonuses(
                modifier,
                sourceData,
                requireWireless: wirelessEnabled,
                out CharacterVehicleModSourceBonuses sourceBonuses)
            || !TryResolveOptionalVehicleBodyExpression(
                bonus?.Element("body")?.Value ?? sourceBonuses.BodyExpression,
                rating,
                out int regularBonus))
        {
            return false;
        }

        if (!wirelessEnabled)
        {
            bodyBonus = regularBonus;
            return true;
        }

        XElement? wirelessBonus = modifier.Element("wirelessbonus");
        if (!TryResolveOptionalVehicleBodyExpression(
                wirelessBonus?.Element("body")?.Value ?? sourceBonuses.WirelessBodyExpression,
                rating,
                out int wirelessBodyBonus))
        {
            return false;
        }

        try
        {
            bodyBonus = checked(regularBonus + wirelessBodyBonus);
            return true;
        }
        catch (OverflowException)
        {
            bodyBonus = 0;
            return false;
        }
    }

    private static bool TryReadEffectiveVehicleModBonuses(
        XElement modifier,
        ICharacterSourceDataContext? sourceData,
        bool requireWireless,
        out CharacterVehicleModSourceBonuses bonuses)
    {
        bonuses = CharacterVehicleModSourceBonuses.Empty;
        bool needsRegularSource = modifier.Element("bonus") is null;
        bool needsWirelessSource = requireWireless && modifier.Element("wirelessbonus") is null;
        if (!needsRegularSource && !needsWirelessSource)
        {
            return true;
        }

        return sourceData?.TryResolveVehicleModBonuses(
            modifier.Element("sourceid")?.Value.Trim() ?? string.Empty,
            modifier.Element("name")?.Value.Trim() ?? string.Empty,
            out bonuses) == true;
    }

    private static bool TryResolveOptionalVehicleBodyExpression(
        string? expression,
        int rating,
        out int bonus)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            bonus = 0;
            return true;
        }

        return CharacterVehicleConditionMonitorCalculator.TryResolveRatingExpression(
            expression,
            rating,
            out bonus);
    }

    private static void ApplyTextMutation(XElement root, WorkspaceSetCollectionTextRequest request)
    {
        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        if (resolved.Kind == WorkspaceCollectionKind.Contact && resolved.NestedKind is null)
        {
            CharacterContactEditSemantics semantics = ResolveContactSemantics(root, resolved.Item);
            if (request.Field is WorkspaceCollectionTextField.Name
                    or WorkspaceCollectionTextField.Metatype
                    or WorkspaceCollectionTextField.Gender
                    or WorkspaceCollectionTextField.Age
                && !semantics.IdentityEditable)
            {
                throw new InvalidOperationException(
                    "This contact field is controlled by its linked character and is read-only.");
            }
        }
        if (resolved.Kind == WorkspaceCollectionKind.Pet
            && resolved.NestedKind is null
            && request.Field is WorkspaceCollectionTextField.Name or WorkspaceCollectionTextField.Metatype
            && !ResolvePetSemantics(resolved.Item).IdentityEditable)
        {
            throw new InvalidOperationException(
                "This pet field is controlled by its linked character and is read-only.");
        }
        string elementName = ResolveTextElementName(resolved, request.Field);
        string value = request.Value ?? string.Empty;
        int maximumLength = request.Field == WorkspaceCollectionTextField.Name
            ? MaximumNameLength
            : MaximumTextLength;
        if (value.Length > maximumLength)
        {
            throw new InvalidOperationException(
                $"Collection field '{request.Field}' exceeds its {maximumLength.ToString(CultureInfo.InvariantCulture)} character limit.");
        }

        if (request.Field == WorkspaceCollectionTextField.Name && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Collection item names cannot be blank.");
        }

        SetElementValue(resolved.Item, elementName, value);
    }

    private static void ApplyRatingMutation(XElement root, WorkspaceSetCollectionRatingRequest request)
    {
        if (request.Value is < 0 or > MaximumRating)
        {
            throw new InvalidOperationException(
                $"Collection ratings must be between 0 and {MaximumRating.ToString(CultureInfo.InvariantCulture)}.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        foreach (string elementName in ResolveRatingElementNames(resolved))
        {
            SetElementValue(resolved.Item, elementName, request.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ApplyQuantityMutation(XElement root, WorkspaceSetCollectionQuantityRequest request)
    {
        if (request.Value <= 0m || request.Value > MaximumQuantity)
        {
            throw new InvalidOperationException(
                $"Collection quantities must be greater than 0 and no greater than {MaximumQuantity.ToString(CultureInfo.InvariantCulture)}.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        string elementName = ResolveQuantityElementName(resolved);
        SetElementValue(resolved.Item, elementName, request.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static void ApplyToggleMutation(XElement root, WorkspaceSetCollectionToggleRequest request)
    {
        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        if (resolved.Kind == WorkspaceCollectionKind.Contact && resolved.NestedKind is null)
        {
            CharacterContactEditSemantics semantics = ResolveContactSemantics(root, resolved.Item);
            bool editable = request.Field switch
            {
                WorkspaceCollectionToggleField.Group => semantics.GroupEditable,
                WorkspaceCollectionToggleField.Free => semantics.FreeEditable,
                WorkspaceCollectionToggleField.Family => semantics.FamilyEditable,
                WorkspaceCollectionToggleField.Blackmail => semantics.BlackmailEditable,
                _ => false
            };
            if (!editable)
            {
                throw new InvalidOperationException($"This contact's {request.Field} value is read-only.");
            }
        }
        string elementName = ResolveToggleElementName(resolved, request.Field);
        SetElementValue(resolved.Item, elementName, request.Value ? "True" : "False");
    }

    private static void ApplyDeleteMutation(XElement root, WorkspaceCollectionItemTarget target)
    {
        ResolvedCollectionItem resolved = ResolveCollectionItem(root, target);
        if (resolved.Kind == WorkspaceCollectionKind.Contact
            && resolved.NestedKind is null
            && !ResolveContactSemantics(root, resolved.Item).CanDelete)
        {
            throw new InvalidOperationException("This read-only contact cannot be deleted.");
        }
        if (resolved.Kind == WorkspaceCollectionKind.Pet
            && resolved.NestedKind is null
            && !ResolvePetSemantics(resolved.Item).CanDelete)
        {
            throw new InvalidOperationException("This pet cannot be deleted.");
        }
        resolved.Item.Remove();
    }

    private static void ApplyLinkedCharacterMutation(
        XElement root,
        WorkspaceSetLinkedCharacterRequest request)
    {
        ResolvedCollectionItem resolved = ResolveLinkedCharacterTarget(root, request.Target);
        ArgumentNullException.ThrowIfNull(request.Identity);

        string fileName = request.FileName?.Trim() ?? string.Empty;
        string relativeFileName = request.RelativeFileName?.Trim().Replace('\\', '/') ?? string.Empty;
        string extension = Path.GetExtension(fileName);
        string relativeExtension = Path.GetExtension(relativeFileName);
        if (fileName.Length is 0 or > 4096
            || !Path.IsPathFullyQualified(fileName)
            || relativeFileName.Length is 0 or > 1024
            || Path.IsPathFullyQualified(relativeFileName)
            || !IsSupportedLinkedCharacterExtension(extension)
            || !string.Equals(extension, relativeExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A linked runner must use a governed .chum5 or .chum5lz private file path.");
        }

        string[] relativeSegments = relativeFileName.Split('/', StringSplitOptions.None);
        if (relativeSegments.Length != 2
            || !string.Equals(relativeSegments[0], "linked-characters", StringComparison.Ordinal)
            || relativeSegments.Any(static segment => string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || segment.Contains(':')))
        {
            throw new InvalidOperationException("The linked runner relative path is not a safe app-private path.");
        }

        string normalizedFullPath;
        try
        {
            normalizedFullPath = Path.GetFullPath(fileName);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException("The linked runner path is invalid.", exception);
        }
        string expectedSuffix = Path.DirectorySeparatorChar
            + relativeFileName.Replace('/', Path.DirectorySeparatorChar);
        if (!normalizedFullPath.EndsWith(expectedSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The linked runner paths do not identify the same private document.");
        }

        string characterName = request.Identity.CharacterName?.Trim() ?? string.Empty;
        string displayName = request.DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(characterName)
            || characterName.Length > MaximumNameLength
            || string.IsNullOrWhiteSpace(displayName)
            || displayName.Length > MaximumNameLength
            || request.Identity.DisplayMetatype.Length > MaximumTextLength
            || request.Identity.Gender.Length > MaximumTextLength
            || request.Identity.Age.Length > MaximumTextLength)
        {
            throw new InvalidOperationException("The linked runner identity is missing or exceeds Chummer's field limits.");
        }

        SetElementValue(resolved.Item, "file", normalizedFullPath);
        SetElementValue(resolved.Item, "relative", relativeFileName);
        XElement extensionRoot = EnsureElement(resolved.Item, "chummercomplete");
        extensionRoot.Element("linkedcharacter")?.Remove();
        extensionRoot.Add(new XElement(
            "linkedcharacter",
            new XElement("displayname", displayName),
            new XElement("name", characterName),
            new XElement("metatype", request.Identity.DisplayMetatype),
            new XElement("gender", request.Identity.Gender),
            new XElement("age", request.Identity.Age)));
    }

    private static void ApplyRemoveLinkedCharacterMutation(
        XElement root,
        WorkspaceCollectionItemTarget target)
    {
        ResolvedCollectionItem resolved = ResolveLinkedCharacterTarget(root, target);
        SetElementValue(resolved.Item, "file", string.Empty);
        SetElementValue(resolved.Item, "relative", string.Empty);
        XElement? extensionRoot = resolved.Item.Element("chummercomplete");
        extensionRoot?.Element("linkedcharacter")?.Remove();
        if (extensionRoot is not null && !extensionRoot.HasElements && string.IsNullOrWhiteSpace(extensionRoot.Value))
        {
            extensionRoot.Remove();
        }
    }

    private static ResolvedCollectionItem ResolveLinkedCharacterTarget(
        XElement root,
        WorkspaceCollectionItemTarget target)
    {
        if (target.Kind is not (WorkspaceCollectionKind.Contact or WorkspaceCollectionKind.Pet)
            || target.NestedKind is not null
            || !string.IsNullOrWhiteSpace(target.NestedItemId))
        {
            throw new InvalidOperationException("Linked runners require a top-level Contact or Pet target.");
        }
        return ResolveCollectionItem(root, target);
    }

    private static bool IsSupportedLinkedCharacterExtension(string extension)
        => string.Equals(extension, ".chum5", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".chum5lz", StringComparison.OrdinalIgnoreCase);

    private static ResolvedCollectionItem ResolveContact(
        XElement root,
        WorkspaceCollectionItemTarget target)
    {
        if (target.Kind != WorkspaceCollectionKind.Contact || target.NestedKind is not null)
        {
            throw new InvalidOperationException("Contact ratings require a top-level Contact target.");
        }
        return ResolveCollectionItem(root, target);
    }

    private static CharacterContactEditSemantics ResolveContactSemantics(
        XElement root,
        XElement contact)
        => CharacterContactEditSemanticsResolver.TryResolve(root, contact, out CharacterContactEditSemantics semantics)
            ? semantics
            : throw new InvalidOperationException(
                "This contact cannot be changed because its exact Chummer5 edit rules are unavailable from the saved runner data.");

    private static CharacterPetEditSemantics ResolvePetSemantics(XElement pet)
        => CharacterPetEditSemanticsResolver.TryResolve(pet, out CharacterPetEditSemantics semantics)
            ? semantics
            : throw new InvalidOperationException(
                "This pet cannot be changed because its exact Chummer5 edit rules are unavailable from the saved runner data.");

    private static void ApplyMoveMutation(XElement root, WorkspaceMoveCollectionItemRequest request)
    {
        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        List<XNode> nodes = resolved.Container.Nodes().ToList();
        List<int> itemSlots = nodes
            .Select(static (node, index) => (node, index))
            .Where(pair => pair.node is XElement element && element.Name.LocalName == resolved.ItemElementName)
            .Select(static pair => pair.index)
            .ToList();
        List<XElement> items = itemSlots.Select(slot => (XElement)nodes[slot]).ToList();

        if (request.TargetIndex < 0 || request.TargetIndex >= items.Count)
        {
            throw new InvalidOperationException(
                $"Collection target index {request.TargetIndex.ToString(CultureInfo.InvariantCulture)} is outside the available item range.");
        }

        int currentIndex = items.IndexOf(resolved.Item);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException("The selected collection item is no longer present in its container.");
        }

        items.RemoveAt(currentIndex);
        items.Insert(request.TargetIndex, resolved.Item);
        for (int index = 0; index < itemSlots.Count; index++)
        {
            nodes[itemSlots[index]] = items[index];
        }

        resolved.Container.ReplaceNodes(nodes);
    }

    private static void ApplyAddNestedMutation(XElement root, WorkspaceAddNestedCollectionItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Item);
        if (request.Target.NestedKind is not null || !string.IsNullOrWhiteSpace(request.Target.NestedItemId))
        {
            throw new InvalidOperationException("Nested additions must target a top-level parent item.");
        }

        ResolvedCollectionItem parent = ResolveCollectionItem(root, request.Target);
        NestedCollectionLocation location = ResolveNestedCollectionLocation(parent.Kind, request.NestedKind);
        ValidateNestedDraft(request.Item);

        XElement child = new(
            location.ItemElementName,
            NewStableIdElement(),
            new XElement("name", request.Item.Name.Trim()));
        SetOptionalElementValue(child, "category", request.Item.Category);
        SetOptionalElementValue(child, "source", request.Item.Source);
        SetOptionalElementValue(child, "notes", request.Item.Notes);
        SetOptionalElementValue(child, "extra", request.Item.CustomName);
        if (location.SupportsRating)
        {
            child.Add(new XElement("rating", request.Item.Rating.ToString(CultureInfo.InvariantCulture)));
        }

        if (location.SupportsQuantity)
        {
            child.Add(new XElement("qty", request.Item.Quantity.ToString(CultureInfo.InvariantCulture)));
        }

        if (location.SupportsEquipped)
        {
            child.Add(new XElement("equipped", request.Item.Equipped ? "True" : "False"));
        }

        if (location.SupportsWireless)
        {
            child.Add(new XElement("wirelesson", request.Item.WirelessEnabled ? "True" : "False"));
        }

        EnsureElement(parent.Item, location.ContainerElementName).Add(child);
    }

    private static void ValidateNestedDraft(WorkspaceNestedItemDraft item)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            throw new InvalidOperationException("Nested collection item names cannot be blank.");
        }

        if (item.Name.Length > MaximumNameLength)
        {
            throw new InvalidOperationException(
                $"Nested collection item names cannot exceed {MaximumNameLength.ToString(CultureInfo.InvariantCulture)} characters.");
        }

        if (item.Rating is < 0 or > MaximumRating)
        {
            throw new InvalidOperationException(
                $"Nested collection ratings must be between 0 and {MaximumRating.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (item.Quantity <= 0m || item.Quantity > MaximumQuantity)
        {
            throw new InvalidOperationException(
                $"Nested collection quantities must be greater than 0 and no greater than {MaximumQuantity.ToString(CultureInfo.InvariantCulture)}.");
        }

        foreach (string? value in new[] { item.Category, item.Source, item.Notes, item.CustomName })
        {
            if (value?.Length > MaximumTextLength)
            {
                throw new InvalidOperationException(
                    $"Nested collection text cannot exceed {MaximumTextLength.ToString(CultureInfo.InvariantCulture)} characters.");
            }
        }
    }

    private static ResolvedCollectionItem ResolveCollectionItem(
        XElement root,
        WorkspaceCollectionItemTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        CollectionLocation topLevelLocation = ResolveCollectionLocation(target.Kind);
        XElement topLevelContainer = ResolveContainer(root, topLevelLocation.ContainerPath);
        bool nestedParentCanBeRecursive = target.NestedKind is WorkspaceNestedCollectionKind.Gear
            or WorkspaceNestedCollectionKind.CyberwarePlugin;
        IEnumerable<XElement> candidates = nestedParentCanBeRecursive
            ? topLevelContainer.Descendants(topLevelLocation.ItemElementName)
            : topLevelContainer.Elements(topLevelLocation.ItemElementName);
        if (target.Kind is WorkspaceCollectionKind.Contact or WorkspaceCollectionKind.Pet)
        {
            candidates = candidates.Where(candidate => IsExpectedContactRecordType(candidate, target.Kind));
        }
        XElement topLevelItem = FindUniqueItemById(
            candidates,
            target.ItemId,
            target.Kind.ToString());

        if (target.NestedKind is null)
        {
            if (!string.IsNullOrWhiteSpace(target.NestedItemId))
            {
                throw new InvalidOperationException("A nested item ID requires a nested collection kind.");
            }

            return new ResolvedCollectionItem(
                target.Kind,
                NestedKind: null,
                topLevelContainer,
                topLevelItem,
                topLevelLocation.ItemElementName,
                SupportsRating: false,
                SupportsQuantity: false,
                SupportsEquipped: false,
                SupportsWireless: false,
                SupportsHomeNode: false);
        }

        if (string.IsNullOrWhiteSpace(target.NestedItemId))
        {
            throw new InvalidOperationException("A nested collection target requires a stable nested item ID.");
        }

        NestedCollectionLocation nestedLocation = ResolveNestedCollectionLocation(target.Kind, target.NestedKind.Value);
        XElement nestedContainer = topLevelItem.Element(nestedLocation.ContainerElementName)
            ?? throw new InvalidOperationException(
                $"Collection item '{target.ItemId}' has no '{target.NestedKind.Value}' child collection.");
        XElement nestedItem = FindUniqueItemById(
            nestedContainer,
            nestedLocation.ItemElementName,
            target.NestedItemId,
            target.NestedKind.Value.ToString());
        return new ResolvedCollectionItem(
            target.Kind,
            target.NestedKind,
            nestedContainer,
            nestedItem,
            nestedLocation.ItemElementName,
            nestedLocation.SupportsRating,
            nestedLocation.SupportsQuantity,
            nestedLocation.SupportsEquipped,
            nestedLocation.SupportsWireless,
            nestedLocation.SupportsHomeNode);
    }

    private static bool IsExpectedContactRecordType(
        XElement candidate,
        WorkspaceCollectionKind kind)
    {
        string type = candidate.Element("type")?.Value.Trim() ?? string.Empty;
        return kind switch
        {
            WorkspaceCollectionKind.Contact => string.IsNullOrEmpty(type)
                || string.Equals(type, "Contact", StringComparison.OrdinalIgnoreCase),
            WorkspaceCollectionKind.Pet => string.Equals(type, "Pet", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static CollectionLocation ResolveCollectionLocation(WorkspaceCollectionKind kind)
        => kind switch
        {
            WorkspaceCollectionKind.Gear => new(["gears"], "gear"),
            WorkspaceCollectionKind.Weapon => new(["weapons"], "weapon"),
            WorkspaceCollectionKind.Armor => new(["armors"], "armor"),
            WorkspaceCollectionKind.Skill => new(["newskills", "skills"], "skill"),
            WorkspaceCollectionKind.Contact => new(["contacts"], "contact"),
            WorkspaceCollectionKind.Pet => new(["contacts"], "contact"),
            WorkspaceCollectionKind.Vehicle => new(["vehicles"], "vehicle"),
            WorkspaceCollectionKind.Quality => new(["qualities"], "quality"),
            WorkspaceCollectionKind.Drug => new(["drugs"], "drug"),
            WorkspaceCollectionKind.Cyberware => new(["cyberwares"], "cyberware"),
            WorkspaceCollectionKind.Spell => new(["spells"], "spell"),
            WorkspaceCollectionKind.Power => new(["powers"], "power"),
            WorkspaceCollectionKind.ComplexForm => new(["complexforms"], "complexform"),
            WorkspaceCollectionKind.MatrixProgram => new(["aiprograms"], "program"),
            WorkspaceCollectionKind.InitiationGrade => new(["initiationgrades"], "initiationgrade"),
            WorkspaceCollectionKind.Spirit => new(["spirits"], "spirit"),
            WorkspaceCollectionKind.CritterPower => new(["critterpowers"], "critterpower"),
            _ => throw new InvalidOperationException($"Unsupported collection kind '{kind}'.")
        };

    private static NestedCollectionLocation ResolveNestedCollectionLocation(
        WorkspaceCollectionKind parentKind,
        WorkspaceNestedCollectionKind nestedKind)
        => (parentKind, nestedKind) switch
        {
            (WorkspaceCollectionKind.Gear, WorkspaceNestedCollectionKind.Gear) =>
                new("children", "gear", true, true, true, true, true),
            (WorkspaceCollectionKind.Cyberware, WorkspaceNestedCollectionKind.CyberwarePlugin) =>
                new("children", "cyberware", true, false, true, true, true),
            (WorkspaceCollectionKind.Weapon, WorkspaceNestedCollectionKind.WeaponAccessory) =>
                new("accessories", "accessory", true, false, true, true, false),
            (WorkspaceCollectionKind.Armor, WorkspaceNestedCollectionKind.ArmorMod) =>
                new("armormods", "armormod", true, false, true, true, false),
            (WorkspaceCollectionKind.Vehicle, WorkspaceNestedCollectionKind.VehicleMod) =>
                new("mods", "mod", true, false, true, true, false),
            _ => throw new InvalidOperationException(
                $"Nested collection '{nestedKind}' is not supported for parent kind '{parentKind}'.")
        };

    private static XElement ResolveContainer(XElement root, IReadOnlyList<string> path)
    {
        XElement current = root;
        foreach (string elementName in path)
        {
            current = current.Element(elementName)
                ?? throw new InvalidOperationException(
                    $"Workspace XML does not contain the required <{elementName}> collection container.");
        }

        return current;
    }

    private static XElement FindUniqueItemById(
        XElement container,
        string itemElementName,
        string? itemId,
        string kindLabel)
        => FindUniqueItemById(container.Elements(itemElementName), itemId, kindLabel);

    private static XElement FindUniqueItemById(
        IEnumerable<XElement> candidates,
        string? itemId,
        string kindLabel)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new InvalidOperationException(
                $"Collection kind '{kindLabel}' requires a stable item ID; display names are not accepted as selectors.");
        }

        XElement[] matches = candidates
            .Where(candidate => string.Equals(
                candidate.Element("guid")?.Value.Trim(),
                itemId.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Collection item '{itemId}' was not found in kind '{kindLabel}' by stable ID."),
            _ => throw new InvalidOperationException(
                $"Collection item ID '{itemId}' is duplicated in kind '{kindLabel}'; mutation was refused.")
        };
    }

    private static string ResolveTextElementName(
        ResolvedCollectionItem resolved,
        WorkspaceCollectionTextField field)
    {
        if (resolved.NestedKind is not null)
        {
            return field switch
            {
                WorkspaceCollectionTextField.Name => "name",
                WorkspaceCollectionTextField.Category => "category",
                WorkspaceCollectionTextField.Source => "source",
                WorkspaceCollectionTextField.Notes => "notes",
                WorkspaceCollectionTextField.CustomName => "extra",
                WorkspaceCollectionTextField.Location => "location",
                _ => throw UnsupportedField(field, resolved)
            };
        }

        if (field == WorkspaceCollectionTextField.Name
            && resolved.Kind != WorkspaceCollectionKind.InitiationGrade)
        {
            return "name";
        }

        if (field == WorkspaceCollectionTextField.Notes)
        {
            return "notes";
        }

        if (field == WorkspaceCollectionTextField.CustomName
            && resolved.Kind is not (WorkspaceCollectionKind.InitiationGrade or WorkspaceCollectionKind.Pet))
        {
            return "extra";
        }

        return (resolved.Kind, field) switch
        {
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Weapon or WorkspaceCollectionKind.Armor
                or WorkspaceCollectionKind.Vehicle or WorkspaceCollectionKind.Drug or WorkspaceCollectionKind.Cyberware
                or WorkspaceCollectionKind.Spell or WorkspaceCollectionKind.CritterPower,
                WorkspaceCollectionTextField.Category) => "category",
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Weapon or WorkspaceCollectionKind.Armor
                or WorkspaceCollectionKind.Vehicle or WorkspaceCollectionKind.Quality or WorkspaceCollectionKind.Drug
                or WorkspaceCollectionKind.Cyberware or WorkspaceCollectionKind.Spell or WorkspaceCollectionKind.Power
                or WorkspaceCollectionKind.ComplexForm or WorkspaceCollectionKind.MatrixProgram
                or WorkspaceCollectionKind.CritterPower,
                WorkspaceCollectionTextField.Source) => "source",
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Contact or WorkspaceCollectionKind.Cyberware,
                WorkspaceCollectionTextField.Location) => "location",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.Role) => "role",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.Metatype) => "metatype",
            (WorkspaceCollectionKind.Pet, WorkspaceCollectionTextField.Metatype) => "metatype",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.Gender) => "gender",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.Age) => "age",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.ContactType) => "contacttype",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.PreferredPayment) => "preferredpayment",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.HobbiesVice) => "hobbiesvice",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.PersonalLife) => "personallife",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionTextField.GroupName) => "groupname",
            (WorkspaceCollectionKind.Cyberware, WorkspaceCollectionTextField.Grade) => "grade",
            (WorkspaceCollectionKind.Cyberware, WorkspaceCollectionTextField.Capacity) => "capacity",
            (WorkspaceCollectionKind.Weapon, WorkspaceCollectionTextField.Damage) => "damage",
            (WorkspaceCollectionKind.Weapon, WorkspaceCollectionTextField.Accuracy) => "accuracy",
            (WorkspaceCollectionKind.Weapon, WorkspaceCollectionTextField.Mode) => "mode",
            (WorkspaceCollectionKind.Weapon, WorkspaceCollectionTextField.ArmorPenetration) => "ap",
            (WorkspaceCollectionKind.Armor, WorkspaceCollectionTextField.ArmorValue) => "armor",
            (WorkspaceCollectionKind.Vehicle, WorkspaceCollectionTextField.Handling) => "handling",
            (WorkspaceCollectionKind.Vehicle, WorkspaceCollectionTextField.Speed) => "speed",
            (WorkspaceCollectionKind.Vehicle, WorkspaceCollectionTextField.Body) => "body",
            (WorkspaceCollectionKind.Vehicle, WorkspaceCollectionTextField.Sensor) => "sensor",
            (WorkspaceCollectionKind.Vehicle, WorkspaceCollectionTextField.Seats) => "seats",
            (WorkspaceCollectionKind.Spell or WorkspaceCollectionKind.CritterPower, WorkspaceCollectionTextField.Type) => "type",
            (WorkspaceCollectionKind.Spell or WorkspaceCollectionKind.CritterPower, WorkspaceCollectionTextField.Range) => "range",
            (WorkspaceCollectionKind.Spell or WorkspaceCollectionKind.ComplexForm or WorkspaceCollectionKind.CritterPower,
                WorkspaceCollectionTextField.Duration) => "duration",
            (WorkspaceCollectionKind.Spell, WorkspaceCollectionTextField.DrainValue) => "dv",
            (WorkspaceCollectionKind.ComplexForm, WorkspaceCollectionTextField.Target) => "target",
            (WorkspaceCollectionKind.ComplexForm, WorkspaceCollectionTextField.FadingValue) => "fv",
            (WorkspaceCollectionKind.MatrixProgram, WorkspaceCollectionTextField.Slot) => "rating",
            (WorkspaceCollectionKind.InitiationGrade, WorkspaceCollectionTextField.Reward) => "reward",
            (WorkspaceCollectionKind.CritterPower, WorkspaceCollectionTextField.Mode) => "action",
            (WorkspaceCollectionKind.Skill, WorkspaceCollectionTextField.Category) => "skillcategory",
            _ => throw UnsupportedField(field, resolved)
        };
    }

    private static IReadOnlyList<string> ResolveRatingElementNames(ResolvedCollectionItem resolved)
    {
        if (resolved.NestedKind is not null)
        {
            if (!resolved.SupportsRating)
            {
                throw UnsupportedOperation("rating", resolved);
            }

            return ["rating"];
        }

        return resolved.Kind switch
        {
            WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Drug or WorkspaceCollectionKind.Cyberware
                or WorkspaceCollectionKind.Power or WorkspaceCollectionKind.CritterPower => ["rating"],
            WorkspaceCollectionKind.Armor => ["rating", "armor"],
            _ => throw UnsupportedOperation("rating", resolved)
        };
    }

    private static string ResolveQuantityElementName(ResolvedCollectionItem resolved)
    {
        if (resolved.NestedKind is not null)
        {
            if (!resolved.SupportsQuantity)
            {
                throw UnsupportedOperation("quantity", resolved);
            }

            return "qty";
        }

        return resolved.Kind switch
        {
            WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Drug => "qty",
            _ => throw UnsupportedOperation("quantity", resolved)
        };
    }

    private static string ResolveToggleElementName(
        ResolvedCollectionItem resolved,
        WorkspaceCollectionToggleField field)
    {
        if (resolved.NestedKind is not null)
        {
            return field switch
            {
                WorkspaceCollectionToggleField.Equipped when resolved.SupportsEquipped => "equipped",
                WorkspaceCollectionToggleField.WirelessEnabled when resolved.SupportsWireless => "wirelesson",
                WorkspaceCollectionToggleField.HomeNode when resolved.SupportsHomeNode => "homenode",
                _ => throw UnsupportedField(field, resolved)
            };
        }

        return (resolved.Kind, field) switch
        {
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Weapon or WorkspaceCollectionKind.Armor
                or WorkspaceCollectionKind.Cyberware,
                WorkspaceCollectionToggleField.Equipped) => "equipped",
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Weapon or WorkspaceCollectionKind.Armor
                or WorkspaceCollectionKind.Cyberware,
                WorkspaceCollectionToggleField.WirelessEnabled) => "wirelesson",
            (WorkspaceCollectionKind.Gear or WorkspaceCollectionKind.Cyberware,
                WorkspaceCollectionToggleField.HomeNode) => "homenode",
            (WorkspaceCollectionKind.Spirit, WorkspaceCollectionToggleField.Bound) => "bound",
            (WorkspaceCollectionKind.InitiationGrade, WorkspaceCollectionToggleField.Resonance) => "res",
            (WorkspaceCollectionKind.InitiationGrade, WorkspaceCollectionToggleField.Group) => "group",
            (WorkspaceCollectionKind.InitiationGrade, WorkspaceCollectionToggleField.Ordeal) => "ordeal",
            (WorkspaceCollectionKind.InitiationGrade, WorkspaceCollectionToggleField.Schooling) => "schooling",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionToggleField.Group) => "group",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionToggleField.Free) => "free",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionToggleField.Family) => "family",
            (WorkspaceCollectionKind.Contact, WorkspaceCollectionToggleField.Blackmail) => "blackmail",
            _ => throw UnsupportedField(field, resolved)
        };
    }

    private static InvalidOperationException UnsupportedField<TField>(
        TField field,
        ResolvedCollectionItem resolved)
        => new(
            $"Collection field '{field}' is not supported for '{DescribeCollectionItem(resolved)}'.");

    private static InvalidOperationException UnsupportedOperation(
        string operation,
        ResolvedCollectionItem resolved)
        => new(
            $"Collection operation '{operation}' is not supported for '{DescribeCollectionItem(resolved)}'.");

    private static string DescribeCollectionItem(ResolvedCollectionItem resolved)
        => resolved.NestedKind is { } nestedKind
            ? $"{resolved.Kind}/{nestedKind}"
            : resolved.Kind.ToString();

    private static void SetOptionalElementValue(XElement parent, string name, string? value)
    {
        if (value is not null)
        {
            SetElementValue(parent, name, value);
        }
    }

    private static XElement NewStableIdElement()
        => new("guid", Guid.NewGuid().ToString("D"));

    private static string Serialize(XDocument document)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private sealed record CollectionLocation(
        IReadOnlyList<string> ContainerPath,
        string ItemElementName);

    private sealed record NestedCollectionLocation(
        string ContainerElementName,
        string ItemElementName,
        bool SupportsRating,
        bool SupportsQuantity,
        bool SupportsEquipped,
        bool SupportsWireless,
        bool SupportsHomeNode);

    private sealed record ResolvedCollectionItem(
        WorkspaceCollectionKind Kind,
        WorkspaceNestedCollectionKind? NestedKind,
        XElement Container,
        XElement Item,
        string ItemElementName,
        bool SupportsRating,
        bool SupportsQuantity,
        bool SupportsEquipped,
        bool SupportsWireless,
        bool SupportsHomeNode);

    private static void AddGear(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "gears").Add(
            new XElement(
                "gear",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Gear")),
                new XElement("rating", request.Rating.ToString(CultureInfo.InvariantCulture)),
                new XElement("qty", Math.Max(1, request.Quantity).ToString(CultureInfo.InvariantCulture)),
                new XElement("cost", FirstNonBlank(request.Cost, "0")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddWeapon(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "weapons").Add(
            new XElement(
                "weapon",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Weapon")),
                new XElement("type", "Weapon"),
                new XElement("damage", FirstNonBlank(request.Damage, "6P")),
                new XElement("ap", FirstNonBlank(request.Ap, "0")),
                new XElement("accuracy", FirstNonBlank(request.Accuracy, "4")),
                new XElement("mode", FirstNonBlank(request.Mode, "SA")),
                new XElement("ammo", "n/a"),
                new XElement("cost", FirstNonBlank(request.Cost, "0")),
                new XElement("equipped", "True")));
    }

    private static void AddArmor(XElement root, WorkspaceQuickAddRequest request)
    {
        string armorValue = FirstNonBlank(request.ArmorValue, request.Rating > 0 ? request.Rating.ToString(CultureInfo.InvariantCulture) : null, "0");
        EnsureElement(root, "armors").Add(
            new XElement(
                "armor",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Armor")),
                new XElement("armor", armorValue),
                new XElement("rating", armorValue),
                new XElement("cost", FirstNonBlank(request.Cost, "0")),
                new XElement("equipped", "True")));
    }

    private static void AddSkill(XElement root, WorkspaceQuickAddRequest request)
    {
        XElement skillsRoot = EnsureElement(EnsureElement(root, "newskills"), "skills");
        skillsRoot.Add(
            new XElement(
                "skill",
                NewStableIdElement(),
                new XElement("suid", NormalizeToken(request.Name)),
                new XElement("name", request.Name),
                new XElement("skillcategory", FirstNonBlank(request.Category, request.IsKnowledge ? "Knowledge" : "Active Skill")),
                new XElement("isknowledge", request.IsKnowledge ? "True" : "False"),
                new XElement("knowledge", request.IsKnowledge ? "True" : "False"),
                new XElement("base", Math.Max(1, request.BaseValue).ToString(CultureInfo.InvariantCulture)),
                new XElement("rating", Math.Max(1, request.BaseValue).ToString(CultureInfo.InvariantCulture)),
                new XElement("karma", "0"),
                new XElement("specs")));
    }

    private static void AddContact(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "contacts").Add(
            new XElement(
                "contact",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("role", FirstNonBlank(request.Role, "Contact")),
                new XElement("location", FirstNonBlank(request.Location, "Seattle")),
                new XElement("connection", Math.Max(0, request.Connection).ToString(CultureInfo.InvariantCulture)),
                new XElement("loyalty", Math.Max(0, request.Loyalty).ToString(CultureInfo.InvariantCulture)),
                new XElement("type", "Contact")));
    }

    private static void AddPet(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "contacts").Add(
            new XElement(
                "contact",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("metatype", string.Empty),
                new XElement("notes", string.Empty),
                new XElement("type", "Pet")));
    }

    private static void AddVehicle(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "vehicles").Add(
            new XElement(
                "vehicle",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Vehicle")),
                new XElement("handling", FirstNonBlank(request.Handling, "3")),
                new XElement("speed", FirstNonBlank(request.Speed, "3")),
                new XElement("body", FirstNonBlank(request.Body, "10")),
                new XElement("armor", FirstNonBlank(request.ArmorValue, "8")),
                new XElement("sensor", FirstNonBlank(request.Sensor, "2")),
                new XElement("seats", FirstNonBlank(request.Seats, "4")),
                new XElement("cost", FirstNonBlank(request.Cost, "0"))));
    }

    private static void AddQuality(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "qualities").Add(
            new XElement(
                "quality",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add")),
                new XElement("bp", request.Karma.ToString(CultureInfo.InvariantCulture))));
    }

    private static void AddDrug(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "drugs").Add(
            new XElement(
                "drug",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Drug")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add")),
                new XElement("rating", Math.Max(0, request.Rating).ToString(CultureInfo.InvariantCulture)),
                new XElement("qty", Math.Max(1, request.Quantity).ToString(CultureInfo.InvariantCulture))));
    }

    private static void AddCyberware(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "cyberwares").Add(
            new XElement(
                "cyberware",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Cyberware")),
                new XElement("ess", FirstNonBlank(request.Essence, "0.00")),
                new XElement("capacity", FirstNonBlank(request.Capacity, "n/a")),
                new XElement("rating", Math.Max(0, request.Rating).ToString(CultureInfo.InvariantCulture)),
                new XElement("cost", FirstNonBlank(request.Cost, "0")),
                new XElement("grade", FirstNonBlank(request.Grade, "Standard")),
                new XElement("location", FirstNonBlank(request.Location, "Body")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddSpell(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "spells").Add(
            new XElement(
                "spell",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Combat")),
                new XElement("type", FirstNonBlank(request.Type, "Mana")),
                new XElement("range", FirstNonBlank(request.Range, "LOS")),
                new XElement("duration", FirstNonBlank(request.Duration, "Instant")),
                new XElement("dv", FirstNonBlank(request.DrainValue, "F-3")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddPower(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "powers").Add(
            new XElement(
                "power",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("rating", Math.Max(0, request.Rating).ToString(CultureInfo.InvariantCulture)),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add")),
                new XElement(
                    "pointsperlevel",
                    (request.PointsPerLevel <= 0m ? 0.5m : request.PointsPerLevel).ToString(CultureInfo.InvariantCulture))));
    }

    private static void AddComplexForm(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "complexforms").Add(
            new XElement(
                "complexform",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("target", FirstNonBlank(request.Target, "Persona")),
                new XElement("duration", FirstNonBlank(request.Duration, "Sustained")),
                new XElement("fv", FirstNonBlank(request.FadingValue, "Level")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddMatrixProgram(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "aiprograms").Add(
            new XElement(
                "program",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("rating", FirstNonBlank(request.Slot, request.Rating > 0 ? request.Rating.ToString(CultureInfo.InvariantCulture) : null, "1")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddInitiationGrade(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "initiationgrades").Add(
            new XElement(
                "initiationgrade",
                NewStableIdElement(),
                new XElement("grade", Math.Max(0, request.Rating).ToString(CultureInfo.InvariantCulture)),
                new XElement("res", request.Res ? "True" : "False"),
                new XElement("group", request.Group ? "True" : "False"),
                new XElement("ordeal", request.Ordeal ? "True" : "False"),
                new XElement("schooling", request.Schooling ? "True" : "False"),
                new XElement("reward", FirstNonBlank(request.Reward, request.Name))));
    }

    private static void AddSpirit(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "spirits").Add(
            new XElement(
                "spirit",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("force", Math.Max(1, request.Force).ToString(CultureInfo.InvariantCulture)),
                new XElement("services", Math.Max(0, request.Services).ToString(CultureInfo.InvariantCulture)),
                new XElement("bound", request.Bound ? "True" : "False")));
    }

    private static void AddCritterPower(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "critterpowers").Add(
            new XElement(
                "critterpower",
                NewStableIdElement(),
                new XElement("name", request.Name),
                new XElement("category", FirstNonBlank(request.Category, "Passive")),
                new XElement("type", FirstNonBlank(request.Type, "Passive")),
                new XElement("action", FirstNonBlank(request.Mode, "Auto")),
                new XElement("range", FirstNonBlank(request.Range, "Self")),
                new XElement("duration", FirstNonBlank(request.Duration, "Always")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add")),
                new XElement("rating", Math.Max(0, request.Rating).ToString(CultureInfo.InvariantCulture))));
    }

    private static XElement EnsureElement(XElement parent, string name)
    {
        XElement? existing = parent.Element(name);
        if (existing is not null)
        {
            return existing;
        }

        XElement created = new(name);
        parent.Add(created);
        return created;
    }

    private static void SetElementValue(XElement parent, string name, string? value)
        => EnsureElement(parent, name).Value = value ?? string.Empty;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static bool AttributeNamesMatch(string? left, string? right)
    {
        string leftName = FirstNonBlank(left);
        string rightName = FirstNonBlank(right);
        if (string.IsNullOrWhiteSpace(leftName) || string.IsNullOrWhiteSpace(rightName))
        {
            return false;
        }

        return string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                AttributeWorkbenchProjector.FormatCompactLabel(leftName),
                AttributeWorkbenchProjector.FormatCompactLabel(rightName),
                StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string? value, int fallback = 0)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    private static bool TryParseRequiredInt(string? value, out int parsed)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);

    private static bool TryParseOptionalInt(string? value, out int parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = 0;
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : 0m;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;

    private static bool TryParseOptionalBool(string? value, out bool parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = false;
            return true;
        }

        return bool.TryParse(value, out parsed);
    }

    private static int ComputeCareerAttributeUpgradeCost(int currentValue, int totalMaximum)
    {
        if (currentValue >= totalMaximum)
        {
            return -1;
        }

        int nextRank = Math.Max(1, currentValue + 1);
        return nextRank * 5;
    }

    private static void AppendKarmaExpense(XElement root, int amount, string reason)
    {
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString()),
                new XElement("date", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", amount.ToString(CultureInfo.InvariantCulture)),
                new XElement("reason", reason),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", "ImproveAttribute"),
                    new XElement("nuyentype", "ManualAdd"),
                    new XElement("objectid"),
                    new XElement("qty", "0"),
                    new XElement("extra"))));
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "entry";
        }

        char[] normalized = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();

        return new string(normalized).Trim('-');
    }
}
