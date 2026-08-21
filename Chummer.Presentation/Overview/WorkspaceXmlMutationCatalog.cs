using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Infrastructure.Xml;

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
    private const int MaximumSelectTextLength = 32_767;
    private const int MaximumTextLength = 65_536;
    private const int MaximumConditionBoxes = 1000;
    private const int MaximumCareerReputation = 100;
    private const int MaximumSituationalModifier = 100;

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

    public static string ApplyCareerReputationEdit(
        string xml,
        CareerReputationEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Reputation can only be changed for a created/career runner.");
        }

        ValidateCareerReputation(request.StreetCred, "Street Cred");
        ValidateCareerReputation(request.Notoriety, "Notoriety");
        ValidateCareerReputation(request.PublicAwareness, "Public Awareness");

        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        bool forbiddenArcana = CareerReputationEditorProjector.IsBookEnabled(sourceData, "FA");
        bool streetGrimoire = CareerReputationEditorProjector.IsBookEnabled(sourceData, "SG");
        if (request.AstralReputation is { } astralReputation)
        {
            ValidateCareerReputation(astralReputation, "Astral Reputation");
            if (!forbiddenArcana && !streetGrimoire)
            {
                throw new InvalidOperationException(
                    "Astral Reputation requires an exact runner settings profile with Street Grimoire or Forbidden Arcana enabled.");
            }
        }
        if (request.WildReputation is { } wildReputation)
        {
            ValidateCareerReputation(wildReputation, "Wild Reputation");
            if (!forbiddenArcana)
            {
                throw new InvalidOperationException(
                    "Wild Reputation requires an exact runner settings profile with Forbidden Arcana enabled.");
            }
        }

        SetElementValue(root, "streetcred", request.StreetCred.ToString(CultureInfo.InvariantCulture));
        SetElementValue(root, "notoriety", request.Notoriety.ToString(CultureInfo.InvariantCulture));
        SetElementValue(root, "publicawareness", request.PublicAwareness.ToString(CultureInfo.InvariantCulture));
        if (request.AstralReputation is { } astral)
        {
            SetElementValue(root, "baseastralreputation", astral.ToString(CultureInfo.InvariantCulture));
        }
        if (request.WildReputation is { } wild)
        {
            SetElementValue(root, "basewildreputation", wild.ToString(CultureInfo.InvariantCulture));
        }
        return Serialize(document);
    }

    public static string ApplyBurnStreetCred(
        string xml,
        BurnStreetCredRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        CareerStreetCredProjection projection = CareerStreetCredRules.Project(root);
        if (!projection.CanBurn)
        {
            throw new InvalidOperationException(
                projection.UnavailableReason
                ?? "At least 2 total Street Cred is required before Street Cred can be burned.");
        }

        int burntStreetCred = checked(projection.BurntStreetCred + 2);
        SetElementValue(root, "burntstreetcred", burntStreetCred.ToString(CultureInfo.InvariantCulture));
        return Serialize(document);
    }

    private static void ValidateCareerReputation(int value, string label)
    {
        if (value < 0 || value > MaximumCareerReputation)
        {
            throw new InvalidOperationException($"{label} must be between 0 and {MaximumCareerReputation.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    public static string ApplySituationalModifiersEdit(
        string xml,
        SituationalModifiersEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        ValidateSituationalModifier(request.CounterspellingDice, "Counterspelling dice");
        ValidateSituationalModifier(request.LiftCarryHits, "Lift/carry hits");
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        SetElementValue(
            root,
            "currentcounterspellingdice",
            request.CounterspellingDice.ToString(CultureInfo.InvariantCulture));
        SetElementValue(
            root,
            "currentliftcarryhits",
            request.LiftCarryHits.ToString(CultureInfo.InvariantCulture));
        return Serialize(document);
    }

    private static void ValidateSituationalModifier(int value, string label)
    {
        if (value < 0 || value > MaximumSituationalModifier)
        {
            throw new InvalidOperationException(
                $"{label} must be between 0 and {MaximumSituationalModifier.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    public static string ApplyPrimaryArmEdit(string xml, PrimaryArmEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (PrimaryArmEditorProjector.IsAmbidextrous(root))
        {
            throw new InvalidOperationException(
                "Primary arm is read-only because this runner is Ambidextrous.");
        }

        SetElementValue(root, "primaryarm", PrimaryArmEditorProjector.NormalizeValue(request.Value));
        return Serialize(document);
    }

    public static string ApplyGearLocationAdd(string xml, GearLocationAddRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        string name = GearLocationAddRequest.ValidateName(request.Name);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement? locations = root.Element("gearlocations");
        if (locations is null)
        {
            locations = new XElement("gearlocations");
            root.Add(locations);
        }
        locations.Add(
            new XElement(
                "location",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("name", name),
                new XElement("notes", string.Empty)));
        return Serialize(document);
    }

    public static string ApplyWeaponLocationAdd(string xml, WeaponLocationAddRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        string name = WeaponLocationAddRequest.ValidateName(request.Name);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement? locations = root.Element("weaponlocations");
        if (locations is null)
        {
            locations = new XElement("weaponlocations");
            root.Add(locations);
        }
        locations.Add(
            new XElement(
                "location",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("name", name),
                new XElement("notes", string.Empty)));
        return Serialize(document);
    }

    public static string ApplyVehicleLocationAdd(string xml, VehicleLocationAddRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        string name = VehicleLocationAddRequest.ValidateName(request.Name);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement owner = root;
        string containerName = "vehiclelocations";
        if (request.VehicleId is { } vehicleId)
        {
            XElement vehicles = root.Element("vehicles")
                ?? throw new InvalidOperationException("Workspace XML does not contain the required <vehicles> container.");
            owner = FindUniqueItemById(vehicles, "vehicle", vehicleId.ToString("D"), "vehicle");
            containerName = "locations";
        }

        XElement[] existingContainers = owner.Elements(containerName).Take(2).ToArray();
        XElement locations = existingContainers.Length switch
        {
            0 => new XElement(containerName),
            1 => existingContainers[0],
            _ => throw new InvalidOperationException(
                $"Workspace XML contains duplicate <{containerName}> location containers; mutation was refused.")
        };
        if (existingContainers.Length == 0)
        {
            owner.Add(locations);
        }
        locations.Add(
            new XElement(
                "location",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("name", name),
                new XElement("notes", string.Empty)));
        return Serialize(document);
    }

    public static string ApplyVehicleHomeNodeEdit(string xml, VehicleHomeNodeEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.VehicleId == Guid.Empty)
        {
            throw new InvalidOperationException("Vehicle home-node editing requires a non-empty stable vehicle identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement vehicles = root.Element("vehicles")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <vehicles> container.");
        XElement vehicle = FindUniqueItemById(
            vehicles,
            "vehicle",
            request.VehicleId.ToString("D"),
            "vehicle");
        XElement[] targetHomeNodes = vehicle.Elements("homenode").Take(2).ToArray();
        if (targetHomeNodes.Length > 1)
        {
            throw new InvalidOperationException("The selected vehicle contains duplicate <homenode> values.");
        }

        XElement[] allHomeNodes = root.Descendants("homenode").ToArray();
        if (allHomeNodes.Any(node => !bool.TryParse(node.Value, out _)))
        {
            throw new InvalidOperationException("Workspace XML contains an invalid home-node Boolean value.");
        }

        if (request.HomeNode)
        {
            foreach (XElement homeNode in allHomeNodes)
            {
                homeNode.Value = "False";
            }

            XElement target = targetHomeNodes.SingleOrDefault() ?? new XElement("homenode");
            target.Value = "True";
            if (target.Parent is null)
            {
                vehicle.Add(target);
            }
        }
        else if (allHomeNodes.Any(node => node != targetHomeNodes.SingleOrDefault()
            && bool.Parse(node.Value)))
        {
            throw new InvalidOperationException(
                "Vehicle home-node removal requires the selected vehicle to be the sole saved home node.");
        }
        else if (targetHomeNodes.SingleOrDefault() is { } target)
        {
            target.Value = "False";
        }

        return Serialize(document);
    }

    public static string ApplyArmorHomeNodeEdit(string xml, ArmorHomeNodeEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArmorId == Guid.Empty)
        {
            throw new InvalidOperationException("Armor home-node editing requires a non-empty stable armor identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement armors = root.Element("armors")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <armors> container.");
        XElement armor = FindUniqueItemById(
            armors,
            "armor",
            request.ArmorId.ToString("D"),
            "armor");
        XElement[] targetHomeNodes = armor.Elements("homenode").Take(2).ToArray();
        if (targetHomeNodes.Length > 1)
        {
            throw new InvalidOperationException("The selected armor contains duplicate <homenode> values.");
        }

        XElement[] allHomeNodes = root.Descendants("homenode").ToArray();
        if (allHomeNodes.Any(node => !bool.TryParse(node.Value, out _)))
        {
            throw new InvalidOperationException("Workspace XML contains an invalid home-node Boolean value.");
        }

        if (request.HomeNode)
        {
            foreach (XElement homeNode in allHomeNodes)
            {
                homeNode.Value = "False";
            }

            XElement target = targetHomeNodes.SingleOrDefault() ?? new XElement("homenode");
            target.Value = "True";
            if (target.Parent is null)
            {
                armor.Add(target);
            }
        }
        else if (allHomeNodes.Any(node => node != targetHomeNodes.SingleOrDefault()
            && bool.Parse(node.Value)))
        {
            throw new InvalidOperationException(
                "Armor home-node removal requires the selected armor to be the sole saved home node.");
        }
        else if (targetHomeNodes.SingleOrDefault() is { } target)
        {
            target.Value = "False";
        }

        return Serialize(document);
    }

    public static string ApplyWeaponHomeNodeEdit(string xml, WeaponHomeNodeEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedSemantics);
        if (request.WeaponId == Guid.Empty
            || request.ExpectedSemantics.WeaponId != request.WeaponId)
        {
            throw new InvalidOperationException("Weapon home-node editing requires one matching stable weapon identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement weapons = root.Element("weapons")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <weapons> container.");
        XElement weapon = FindUniqueItemById(
            weapons,
            "weapon",
            request.WeaponId.ToString("D"),
            "weapon");
        if (!CharacterWeaponHomeNodeRules.TryProject(
                root,
                weapon,
                out CharacterWeaponHomeNodeSemantics current)
            || current != request.ExpectedSemantics)
        {
            throw new InvalidOperationException(
                "The weapon Home Node rule changed or could not be proven from the current runner.");
        }
        if (!current.Visible || !current.Enabled)
        {
            throw new InvalidOperationException(
                "Chummer5 only permits this weapon Home Node change for an eligible AI Matrix owner.");
        }

        XElement[] targetHomeNodes = weapon.Elements("homenode").Take(2).ToArray();
        if (targetHomeNodes.Length > 1)
        {
            throw new InvalidOperationException("The selected weapon contains duplicate <homenode> values.");
        }
        XElement[] allHomeNodes = CharacterWeaponHomeNodeRules.EnumerateSavedHomeNodes(root).ToArray();
        if (allHomeNodes.Any(node => !bool.TryParse(node.Value, out _)))
        {
            throw new InvalidOperationException("Workspace XML contains an invalid home-node Boolean value.");
        }

        if (request.HomeNode)
        {
            foreach (XElement homeNode in allHomeNodes)
            {
                homeNode.Value = "False";
            }
            XElement target = targetHomeNodes.SingleOrDefault() ?? new XElement("homenode");
            target.Value = "True";
            if (target.Parent is null)
            {
                weapon.Add(target);
            }
        }
        else if (allHomeNodes.Any(node => node != targetHomeNodes.SingleOrDefault()
            && bool.Parse(node.Value)))
        {
            throw new InvalidOperationException(
                "Weapon home-node removal requires the selected weapon to be the sole saved home node.");
        }
        else if (targetHomeNodes.SingleOrDefault() is { } target)
        {
            target.Value = "False";
        }

        return Serialize(document);
    }

    public static string ApplyWeaponActiveCommlinkEdit(
        string xml,
        WeaponActiveCommlinkEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedSemantics);
        if (request.WeaponId == Guid.Empty
            || request.ExpectedSemantics.WeaponId != request.WeaponId)
        {
            throw new InvalidOperationException(
                "Weapon active-commlink editing requires one matching stable weapon identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement weapons = root.Element("weapons")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <weapons> container.");
        XElement weapon = FindUniqueItemById(
            weapons,
            "weapon",
            request.WeaponId.ToString("D"),
            "weapon");
        if (!CharacterWeaponActiveCommlinkRules.TryProject(
                root,
                weapon,
                out CharacterWeaponActiveCommlinkSemantics current)
            || current != request.ExpectedSemantics)
        {
            throw new InvalidOperationException(
                "The weapon Active Commlink rule changed or could not be proven from the current runner.");
        }
        if (!current.IsCommlink)
        {
            throw new InvalidOperationException(
                "Chummer5 hides Active Commlink for a weapon whose Matrix owner cannot form a persona.");
        }

        XElement[] targetActiveNodes = weapon.Elements("active").Take(2).ToArray();
        XElement[] allActiveNodes = CharacterWeaponActiveCommlinkRules
            .EnumerateSavedActiveCommlinks(root)
            .ToArray();
        if (request.ActiveCommlink)
        {
            foreach (XElement active in allActiveNodes)
            {
                active.Value = "False";
            }
            XElement target = targetActiveNodes.SingleOrDefault() ?? new XElement("active");
            target.Value = "True";
            if (target.Parent is null)
            {
                weapon.Add(target);
            }
        }
        else if (!current.ActiveCommlink)
        {
            throw new InvalidOperationException(
                "Weapon active-commlink removal requires the selected weapon to be active.");
        }
        else if (targetActiveNodes.SingleOrDefault() is { } target)
        {
            target.Value = "False";
        }

        return Serialize(document);
    }

    public static string ApplyArmorActiveCommlinkEdit(
        string xml,
        ArmorActiveCommlinkEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArmorId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Armor active-commlink editing requires a non-empty stable armor identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement armors = root.Element("armors")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <armors> container.");
        XElement armor = FindUniqueItemById(
            armors,
            "armor",
            request.ArmorId.ToString("D"),
            "armor");
        XElement[] targetActiveNodes = armor.Elements("active").Take(2).ToArray();
        if (targetActiveNodes.Length > 1)
        {
            throw new InvalidOperationException("The selected armor contains duplicate <active> values.");
        }

        XElement[] matrixDevices = root.Descendants()
            .Where(node => node.Name.LocalName is "armor" or "gear" or "weapon" or "cyberware" or "vehicle")
            .ToArray();
        if (matrixDevices.Any(device => device.Elements("active").Take(2).Count() > 1))
        {
            throw new InvalidOperationException("Workspace XML contains duplicate matrix-device <active> values.");
        }

        XElement[] allActiveNodes = matrixDevices.SelectMany(device => device.Elements("active")).ToArray();
        if (allActiveNodes.Any(node => !bool.TryParse(node.Value, out _)))
        {
            throw new InvalidOperationException("Workspace XML contains an invalid active-commlink Boolean value.");
        }

        if (request.ActiveCommlink)
        {
            bool canFormPersona = ReadDirectValue(armor, "canformpersona").Contains("Self", StringComparison.Ordinal)
                || armor.Element("gears")?.Elements("gear").Any(
                    gear => ReadDirectValue(gear, "canformpersona").Contains("Parent", StringComparison.Ordinal)) == true;
            if (!canFormPersona)
            {
                throw new InvalidOperationException(
                    "The selected armor cannot be the active commlink because it cannot form a persona.");
            }

            foreach (XElement active in allActiveNodes)
            {
                active.Value = "False";
            }

            XElement target = targetActiveNodes.SingleOrDefault() ?? new XElement("active");
            target.Value = "True";
            if (target.Parent is null)
            {
                armor.Add(target);
            }
        }
        else if (allActiveNodes.Any(node => node != targetActiveNodes.SingleOrDefault()
            && bool.Parse(node.Value)))
        {
            throw new InvalidOperationException(
                "Armor active-commlink removal requires the selected armor to be the sole saved active commlink.");
        }
        else if (targetActiveNodes.SingleOrDefault() is { } target)
        {
            target.Value = "False";
        }

        return Serialize(document);
    }

    public static string ApplyWeaponAccessoryIncludedEdit(
        string xml,
        WeaponAccessoryIncludedEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.WeaponId == Guid.Empty || request.AccessoryId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Included-in-weapon editing requires stable non-empty weapon and accessory identities.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        ResolvedCollectionItem resolved = ResolveCollectionItem(
            root,
            new WorkspaceCollectionItemTarget(
                WorkspaceCollectionKind.Weapon,
                request.WeaponId.ToString("D"),
                WorkspaceNestedCollectionKind.WeaponAccessory,
                request.AccessoryId.ToString("D")));
        XElement[] includedNodes = resolved.Item.Elements("included").Take(2).ToArray();
        if (includedNodes.Length > 1)
        {
            throw new InvalidOperationException(
                "The selected weapon accessory contains duplicate <included> values.");
        }
        if (includedNodes.SingleOrDefault() is { } saved && !bool.TryParse(saved.Value, out _))
        {
            throw new InvalidOperationException(
                "The selected weapon accessory contains an invalid <included> Boolean value.");
        }

        XElement target = includedNodes.SingleOrDefault() ?? new XElement("included");
        target.Value = request.IncludedInWeapon ? "True" : "False";
        if (target.Parent is null)
        {
            resolved.Item.Add(target);
        }

        return Serialize(document);
    }

    public static string ApplyCritterPowerCountEdit(
        string xml,
        CritterPowerCountEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.CritterPowerId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Critter Power Count editing requires a stable non-empty critter-power identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        ResolvedCollectionItem resolved = ResolveCollectionItem(
            root,
            new WorkspaceCollectionItemTarget(
                WorkspaceCollectionKind.CritterPower,
                request.CritterPowerId.ToString("D")));
        XElement[] countNodes = resolved.Item.Elements("counttowardslimit").Take(2).ToArray();
        if (!CharacterCritterPowerCountRules.TryProject(
                resolved.Item.Elements("guid").Select(element => element.Value).Take(2).ToArray(),
                countNodes.Select(element => element.Value).ToArray(),
                out CharacterCritterPowerCountState? current)
            || current?.CritterPowerId != request.CritterPowerId)
        {
            throw new InvalidOperationException(
                "The selected critter power does not have one exact stable identity and legacy count state.");
        }

        XElement target = countNodes.SingleOrDefault() ?? new XElement("counttowardslimit");
        target.Value = request.CountsTowardsLimit ? "True" : "False";
        if (target.Parent is null)
        {
            resolved.Item.Add(target);
        }

        return Serialize(document);
    }

    public static string ApplySpiritFetteredEdit(
        string xml,
        SpiritFetteredEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        if (request.ExpectedState.SpiritId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Fettered/Pet editing requires a stable non-empty Spirit or Sprite identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        CharacterSpiritFetteringState current = ProjectSpiritFetteringState(
                root,
                request.ExpectedState.SpiritId)
            ?? throw new InvalidOperationException(
                "The saved runner no longer proves the exact Chummer5 Fettered/Pet rules.");
        if (current != request.ExpectedState)
        {
            throw new InvalidOperationException(
                "The selected Spirit or Sprite changed while Fettered/Pet was open.");
        }
        if (!CharacterSpiritFetteringRules.CanSet(current, request.Fettered))
        {
            throw new InvalidOperationException(request.Fettered
                ? "This Spirit or Sprite cannot be Fettered/Pet under the exact saved Chummer5 rules."
                : "Unfettering would exceed Chummer5's serviced unbound Spirit or Sprite limit.");
        }
        if (request.Fettered == current.Fettered)
        {
            return Serialize(document);
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(
            root,
            new WorkspaceCollectionItemTarget(
                WorkspaceCollectionKind.Spirit,
                current.SpiritId.ToString("D")));
        XElement[] savedValues = resolved.Item.Elements("fettered").Take(2).ToArray();
        if (savedValues.Length > 1)
        {
            throw new InvalidOperationException(
                "The selected Spirit or Sprite contains duplicate <fettered> values.");
        }
        XElement fettered = savedValues.SingleOrDefault() ?? new XElement("fettered");
        fettered.Value = request.Fettered ? "True" : "False";
        if (fettered.Parent is null)
        {
            resolved.Item.Add(fettered);
        }

        XElement improvements = root.Elements("improvements").Single();
        if (string.Equals(current.EntityType, "Spirit", StringComparison.Ordinal))
        {
            if (request.Fettered)
            {
                improvements.Add(CreateSpiritFetteringImprovement());
            }
            else
            {
                improvements.Elements("improvement")
                    .Where(improvement => string.Equals(
                        ReadDirectValue(improvement, "improvementsource"),
                        "SpiritFettering",
                        StringComparison.Ordinal))
                    .Remove();
            }
        }

        if (request.Fettered && current.Created)
        {
            int updatedKarma = checked(current.AvailableKarma - current.ActivationKarmaCost);
            EnsureElement(root, "karma").Value = updatedKarma.ToString(CultureInfo.InvariantCulture);
            AppendSpiritFetteringExpense(
                root,
                current,
                FirstNonBlank(ReadDirectValue(resolved.Item, "name"), current.EntityType));
        }

        return Serialize(document);
    }

    public static string ApplyGroupMembershipEdit(
        string xml,
        GroupMembershipEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        CharacterGroupMembershipState current = GroupMembershipEditorProjector.ProjectState(
            xml,
            sourceDataResolver);
        if (current != request.ExpectedState)
        {
            throw new InvalidOperationException(
                "The runner's group membership, mode, Karma, or settings changed while the editor was open.");
        }
        if (!CharacterGroupMembershipRules.CanSet(current, request.GroupMember))
        {
            throw new InvalidOperationException(
                "This group-membership change is unavailable under the exact saved Chummer5 rules.");
        }
        bool changed = request.GroupMember != current.GroupMember;
        if (changed && current.RequiresConfirmation && !request.Confirmed)
        {
            throw new InvalidOperationException(
                "Career magician group-membership changes require explicit Karma confirmation.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement membership = root.Elements("groupmember").SingleOrDefault()
            ?? new XElement("groupmember");
        membership.Value = request.GroupMember ? "True" : "False";
        if (membership.Parent is null)
        {
            root.Add(membership);
        }

        if (changed && current.RequiresConfirmation)
        {
            int updatedKarma = checked(current.AvailableKarma - current.TransitionKarmaCost);
            EnsureElement(root, "karma").Value = updatedKarma.ToString(CultureInfo.InvariantCulture);
            AppendGroupMembershipExpense(root, request.GroupMember, current.TransitionKarmaCost);
        }
        return Serialize(document);
    }

    public static string ApplyGroupNameEdit(string xml, GroupNameEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        string current = GroupNameEditorProjector.ProjectValue(xml);
        if (!string.Equals(current, request.ExpectedGroupName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The runner's group name changed while the editor was open.");
        }
        if (!CharacterGroupNameRules.TryValidate(request.GroupName, out string validated))
        {
            throw new InvalidOperationException(
                "The submitted group name cannot be represented by the Chummer5 single-line control.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement groupName = root.Elements("groupname").SingleOrDefault()
            ?? new XElement("groupname");
        groupName.Value = validated;
        if (groupName.Parent is null)
        {
            root.Add(groupName);
        }
        return Serialize(document);
    }

    public static string ApplyTraditionNameEdit(string xml, TraditionNameEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        TraditionNameProjection current = TraditionNameEditorProjector.ProjectValue(xml);
        if (current.TraditionId != request.TraditionId
            || !string.Equals(
                current.TraditionName,
                request.ExpectedTraditionName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The custom tradition changed while the editor was open.");
        }
        if (!CharacterTraditionNameRules.TryValidate(request.TraditionName, out string validated))
        {
            throw new InvalidOperationException(
                "The submitted tradition name cannot be represented by the Chummer5 single-line control.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement tradition = root.Elements("tradition").Single();
        XElement name = tradition.Elements("name").SingleOrDefault()
            ?? new XElement("name");
        name.Value = validated;
        if (name.Parent is null)
        {
            tradition.Add(name);
        }
        return Serialize(document);
    }

    public static string ApplyTraditionDrainEdit(
        string xml,
        TraditionDrainEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        TraditionDrainProjection current = TraditionDrainEditorProjector.ProjectValue(
            xml,
            sourceDataResolver);
        if (current.TraditionId != request.TraditionId
            || !string.Equals(
                current.DrainExpression,
                request.ExpectedDrainExpression,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The tradition drain state changed while the editor was open.");
        }
        if (!CharacterTraditionDrainRules.TryValidateRequestedExpression(
                request.DrainExpression,
                current.AllowedExpressions,
                out string validated))
        {
            throw new InvalidOperationException(
                "The submitted drain expression is not in the exact traditions.xml catalog.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement tradition = root.Elements("tradition").Single();
        XElement drain = tradition.Elements("drain").Single();
        drain.Value = validated;
        return Serialize(document);
    }

    private static void AppendGroupMembershipExpense(XElement root, bool joining, int cost)
    {
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("date", DateTime.Now.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", (-cost).ToString(CultureInfo.InvariantCulture)),
                new XElement("reason", joining ? "Join Group" : "Leave Group"),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement("forcecareervisible", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", joining ? "JoinGroup" : "LeaveGroup"),
                    new XElement("nuyentype", "AddCyberware"),
                    new XElement("objectid"),
                    new XElement("qty", "0"),
                    new XElement("extra"))));
    }

    public static string ApplyCareerEdgeUseEdit(
        string xml,
        CareerEdgeUseEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        CharacterCareerEdgeUseState current = CareerEdgeUseEditorProjector.ProjectState(xml);
        if (current != request.ExpectedState)
        {
            throw new InvalidOperationException(
                "The runner's used or total Edge changed while the editor was open.");
        }
        int updated = CharacterCareerEdgeUseRules.Apply(current, request.Action);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement edgeUsed = root.Elements("edgeused").SingleOrDefault()
            ?? new XElement("edgeused");
        edgeUsed.Value = updated.ToString(CultureInfo.InvariantCulture);
        if (edgeUsed.Parent is null)
        {
            root.Add(edgeUsed);
        }
        return Serialize(document);
    }

    public static string ApplyCareerManualKarmaEdit(
        string xml,
        CareerManualKarmaEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        if (request.Reason is null
            || request.Reason.Length > CharacterCareerManualKarmaRules.MaximumReasonLength)
        {
            throw new InvalidOperationException(
                $"Manual Karma reason cannot exceed {CharacterCareerManualKarmaRules.MaximumReasonLength} characters.");
        }
        DateTime expenseDate = DateTime.SpecifyKind(request.ExpenseDateLocal, DateTimeKind.Unspecified);
        if (expenseDate < new DateTime(1753, 1, 1)
            || expenseDate > new DateTime(9998, 12, 31, 23, 59, 59))
        {
            throw new InvalidOperationException("Manual Karma expense date is outside Chummer5's supported range.");
        }
        if (!request.KarmaNuyenExchange && request.ForceCareerVisible)
        {
            throw new InvalidOperationException(
                "Force Career visibility is available only for a Karma/Nuyen exchange.");
        }

        CharacterCareerManualKarmaState current = CareerManualKarmaEditorProjector.ProjectState(
            xml,
            sourceDataResolver);
        if (current != request.ExpectedState)
        {
            throw new InvalidOperationException(
                "The runner's Karma, Nuyen, or exchange profile changed while the editor was open.");
        }
        if (!CharacterCareerManualKarmaRules.TryQuote(
                current,
                request.Action,
                request.Amount,
                request.KarmaNuyenExchange,
                out CharacterCareerManualKarmaQuote? quote)
            || quote is null)
        {
            throw new InvalidOperationException(
                request.Action == CharacterCareerManualKarmaAction.Spend
                    ? "The manual Karma spend is invalid or exceeds available Karma."
                    : "The manual Karma gain is invalid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        EnsureElement(root, "karma").Value = quote.UpdatedKarma.ToString(CultureInfo.InvariantCulture);
        if (request.KarmaNuyenExchange)
        {
            EnsureElement(root, "nuyen").Value = quote.UpdatedNuyen.ToString(CultureInfo.InvariantCulture);
        }

        bool karmaForceCareerVisible = request.Action == CharacterCareerManualKarmaAction.Spend
            && request.ForceCareerVisible;
        InsertManualKarmaExpenseSorted(
            root,
            CreateManualExpense(
                expenseDate,
                quote.KarmaExpenseAmount.ToString(CultureInfo.InvariantCulture),
                request.Reason,
                "Karma",
                request.Refund,
                karmaForceCareerVisible,
                karmaType: request.Action == CharacterCareerManualKarmaAction.Gain ? "ManualAdd" : "ManualSubtract",
                nuyenType: "AddCyberware"));

        if (request.KarmaNuyenExchange)
        {
            InsertManualKarmaExpenseSorted(
                root,
                CreateManualExpense(
                    expenseDate,
                    quote.NuyenExpenseAmount.ToString(CultureInfo.InvariantCulture),
                    request.Reason,
                    "Nuyen",
                    refund: false,
                    forceCareerVisible: request.ForceCareerVisible,
                    karmaType: "ImproveAttribute",
                    nuyenType: "ManualSubtract"));
        }
        return Serialize(document);
    }

    public static string ApplySustainedObjectEdit(
        string xml,
        SustainedObjectEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedState);
        if (request.ExpectedContentRevision <= 0)
        {
            throw new InvalidOperationException("A positive dossier revision is required for sustained-effect editing.");
        }

        XDocument document = SustainedObjectsEditorProjector.ParseDocument(xml);
        IReadOnlyList<SustainedObjectProjection> projected =
            SustainedObjectsEditorProjector.ProjectElements(document.Root!);
        SustainedObjectProjection[] matches = projected
            .Where(candidate => candidate.State.Identity == request.ExpectedState.Identity)
            .Take(2)
            .ToArray();
        if (matches.Length != 1 || matches[0].State != request.ExpectedState)
        {
            throw new InvalidOperationException(
                "The selected sustained effect changed or no longer resolves to its saved occurrence.");
        }

        SustainedObjectProjection target = matches[0];
        switch (request.Action)
        {
            case CharacterSustainedObjectAction.Update:
                if (!CharacterSustainedObjectRules.CanUpdate(
                        target.State,
                        request.Force,
                        request.NetHits,
                        request.SelfSustained))
                {
                    throw new InvalidOperationException(
                        "The sustained-effect values are outside Chummer5's editor bounds or the Self-Sustained field is unavailable.");
                }
                SetElementValue(target.Element, "force", request.Force.ToString(CultureInfo.InvariantCulture));
                SetElementValue(target.Element, "nethits", request.NetHits.ToString(CultureInfo.InvariantCulture));
                if (target.State.SelfSustainedEditable)
                {
                    SetElementValue(target.Element, "self", request.SelfSustained ? "True" : "False");
                }
                break;

            case CharacterSustainedObjectAction.Delete:
                if (!CharacterSustainedObjectRules.CanDelete(request.Confirmed))
                {
                    throw new InvalidOperationException("Deleting a sustained effect requires explicit confirmation.");
                }
                target.Element.Remove();
                break;

            default:
                throw new InvalidOperationException($"Unsupported sustained-effect action '{request.Action}'.");
        }

        _ = SustainedObjectsEditorProjector.ProjectElements(document.Root!);
        return Serialize(document);
    }

    private static XElement CreateManualExpense(
        DateTime expenseDate,
        string amount,
        string reason,
        string type,
        bool refund,
        bool forceCareerVisible,
        string karmaType,
        string nuyenType)
        => new(
            "expense",
            new XElement("guid", Guid.NewGuid().ToString("D")),
            new XElement("date", expenseDate.ToString("s", CultureInfo.InvariantCulture)),
            new XElement("amount", amount),
            new XElement("reason", reason),
            new XElement("type", type),
            new XElement("refund", refund ? "True" : "False"),
            new XElement("forcecareervisible", forceCareerVisible ? "True" : "False"),
            new XElement(
                "undo",
                new XElement("karmatype", karmaType),
                new XElement("nuyentype", nuyenType),
                new XElement("objectid"),
                new XElement("qty", "0"),
                new XElement("extra")));

    private static void InsertManualKarmaExpenseSorted(XElement root, XElement expense)
    {
        XElement expenses = EnsureElement(root, "expenses");
        DateTime newDate = ParseManualExpenseDate(expense);
        XElement[] existing = expenses.Elements("expense").ToArray();
        XElement? insertBefore = null;
        foreach (XElement candidate in existing)
        {
            DateTime candidateDate = ParseManualExpenseDate(candidate);
            if (candidateDate > newDate)
            {
                insertBefore = candidate;
                break;
            }
        }
        if (insertBefore is null)
        {
            expenses.Add(expense);
        }
        else
        {
            insertBefore.AddBeforeSelf(expense);
        }
    }

    private static DateTime ParseManualExpenseDate(XElement expense)
    {
        XElement[] dateNodes = expense.Elements("date").Take(2).ToArray();
        if (dateNodes.Length != 1
            || !DateTime.TryParseExact(
                dateNodes[0].Value.Trim(),
                "s",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
        {
            throw new InvalidOperationException(
                "Manual Karma editing requires every saved expense to have one exact sortable date.");
        }
        return date;
    }

    private static CharacterSpiritFetteringState? ProjectSpiritFetteringState(
        XElement root,
        Guid selectedSpiritId)
    {
        XElement[] spiritContainers = root.Elements("spirits").Take(2).ToArray();
        XElement[] improvementContainers = root.Elements("improvements").Take(2).ToArray();
        if (selectedSpiritId == Guid.Empty
            || spiritContainers.Length != 1
            || improvementContainers.Length != 1
            || !TryReadSpiritLegacyInt(root, "karma", 0, out int availableKarma)
            || !TryReadSpiritOptionalNonNegativeInt(root, "karmaspiritfettering", out int? multiplier))
        {
            return null;
        }

        bool allowSpriteFettering = false;
        int spiritFetteringImprovementCount = 0;
        foreach (XElement improvement in improvementContainers[0].Elements("improvement"))
        {
            if (!TryReadSpiritImprovementEnabled(improvement, out bool enabled))
            {
                return null;
            }
            if (enabled && string.Equals(
                    ReadDirectValue(improvement, "improvementttype"),
                    "AllowSpriteFettering",
                    StringComparison.Ordinal))
            {
                allowSpriteFettering = true;
            }
            if (string.Equals(
                    ReadDirectValue(improvement, "improvementsource"),
                    "SpiritFettering",
                    StringComparison.Ordinal))
            {
                spiritFetteringImprovementCount++;
            }
        }

        List<CharacterSpiritFetteringBasis> basis = [];
        foreach (XElement spirit in spiritContainers[0].Elements("spirit"))
        {
            XElement[] ids = spirit.Elements("guid").Take(2).ToArray();
            string entityType = NormalizeSpiritEntityType(ReadDirectValue(spirit, "type"));
            if (ids.Length != 1
                || !Guid.TryParseExact(ids[0].Value.Trim(), "D", out Guid spiritId)
                || spiritId == Guid.Empty
                || string.IsNullOrWhiteSpace(entityType)
                || !TryReadSpiritNonNegativeInt(spirit, "force", 1, out int force)
                || !TryReadSpiritNonNegativeInt(spirit, "services", 0, out int services)
                || !TryReadSpiritLegacyBool(spirit, "bound", true, out bool bound)
                || !TryReadSpiritLegacyBool(spirit, "fettered", false, out bool fettered))
            {
                return null;
            }
            basis.Add(new CharacterSpiritFetteringBasis(
                spiritId,
                entityType,
                force,
                services,
                bound,
                fettered));
        }

        return CharacterSpiritFetteringRules.TryProject(
            selectedSpiritId,
            ParseBool(ReadDirectValue(root, "created")),
            availableKarma,
            multiplier,
            allowSpriteFettering,
            spiritFetteringImprovementCount,
            basis,
            out CharacterSpiritFetteringState? state)
            ? state
            : null;
    }

    private static string NormalizeSpiritEntityType(string value)
        => value.Trim().ToUpperInvariant() switch
        {
            "SPIRIT" => "Spirit",
            "SPRITE" => "Sprite",
            _ => string.Empty
        };

    private static bool TryReadSpiritLegacyBool(
        XElement parent,
        string elementName,
        bool legacyDefault,
        out bool value)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        value = legacyDefault;
        return values.Length == 0
            || values.Length == 1 && bool.TryParse(values[0].Value.Trim(), out value);
    }

    private static bool TryReadSpiritNonNegativeInt(
        XElement parent,
        string elementName,
        int legacyDefault,
        out int value)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        value = legacyDefault;
        return values.Length == 0
            || values.Length == 1
            && int.TryParse(values[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && value >= 0;
    }

    private static bool TryReadSpiritLegacyInt(
        XElement parent,
        string elementName,
        int legacyDefault,
        out int value)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        value = legacyDefault;
        return values.Length == 0
            || values.Length == 1
            && int.TryParse(values[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadSpiritOptionalNonNegativeInt(
        XElement parent,
        string elementName,
        out int? value)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        value = null;
        if (values.Length == 0)
        {
            return true;
        }
        if (values.Length != 1
            || !int.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
            || parsed < 0)
        {
            return false;
        }
        value = parsed;
        return true;
    }

    private static bool TryReadSpiritImprovementEnabled(XElement improvement, out bool enabled)
    {
        XElement[] values = improvement.Elements("enabled").Take(2).ToArray();
        enabled = true;
        if (values.Length == 0)
        {
            return true;
        }
        if (values.Length != 1)
        {
            return false;
        }
        string saved = values[0].Value.Trim();
        if (int.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
        {
            enabled = integer > 0;
            return true;
        }
        return bool.TryParse(saved, out enabled);
    }

    private static XElement CreateSpiritFetteringImprovement()
        => new(
            "improvement",
            new XElement("target"),
            new XElement("improvedname", "MAG"),
            new XElement("sourcename"),
            new XElement("min", "0"),
            new XElement("max", "0"),
            new XElement("aug", "-1"),
            new XElement("augmax", "0"),
            new XElement("val", "0"),
            new XElement("rating", "1"),
            new XElement("exclude"),
            new XElement("condition"),
            new XElement("improvementttype", "Attribute"),
            new XElement("improvementsource", "SpiritFettering"),
            new XElement("custom", "False"),
            new XElement("customname"),
            new XElement("customid"),
            new XElement("customgroup"),
            new XElement("addtorating", "0"),
            new XElement("enabled", "1"),
            new XElement("order", "0"),
            new XElement("notes"));

    private static void AppendSpiritFetteringExpense(
        XElement root,
        CharacterSpiritFetteringState state,
        string name)
    {
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("date", DateTime.Now.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", (-state.ActivationKarmaCost).ToString(CultureInfo.InvariantCulture)),
                new XElement("reason", $"Fettered Spirit {name}"),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement("forcecareervisible", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", "SpiritFettering"),
                    new XElement("nuyentype", "AddCyberware"),
                    new XElement("objectid", state.SpiritId.ToString("D")),
                    new XElement("qty", "0"),
                    new XElement("extra"))));
    }

    public static string ApplyArmorDamageAdjustment(
        string xml,
        ArmorDamageAdjustmentRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArmorId == Guid.Empty)
        {
            throw new InvalidOperationException("Armor damage adjustment requires a stable non-empty armor identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(ReadDirectValue(root, "created")))
        {
            throw new InvalidOperationException("Armor degradation adjustments are available only in Career mode.");
        }

        ResolvedCollectionItem resolved = ResolveCollectionItem(
            root,
            new WorkspaceCollectionItemTarget(
                WorkspaceCollectionKind.Armor,
                request.ArmorId.ToString("D")));
        XElement[] damageNodes = resolved.Item.Elements("damage").Take(2).ToArray();
        if (damageNodes.Length > 1
            || !TryParseOptionalInt(damageNodes.SingleOrDefault()?.Value, out int currentDamage)
            || currentDamage < 0
            || currentDamage != request.ExpectedArmorDamage
            || !TryCalculateArmorDamageMaximum(resolved.Item, out int maximum)
            || maximum != request.ArmorDamageMaximum
            || !CharacterArmorDamageRules.TryApplyAdjustment(
                currentDamage,
                maximum,
                request.Adjustment,
                out int updatedDamage))
        {
            throw new InvalidOperationException(
                "Armor damage changed, its exact bounds are unavailable, or the requested adjustment is disabled.");
        }

        XElement target = damageNodes.SingleOrDefault() ?? new XElement("damage");
        target.Value = updatedDamage.ToString(CultureInfo.InvariantCulture);
        if (target.Parent is null)
        {
            resolved.Item.Add(target);
        }
        return Serialize(document);
    }

    public static string ApplyArmorEquipmentEdit(
        string xml,
        ArmorEquipmentEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ArmorId == Guid.Empty)
        {
            throw new InvalidOperationException("Armor equipment editing requires a stable non-empty armor identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] armorNodes = root.Element("armors")?.Elements("armor").ToArray() ?? [];
        List<(CharacterArmorEquipmentBasis Basis, XElement Equipped)> resolved = [];
        foreach (XElement armor in armorNodes)
        {
            XElement[] equippedNodes = armor.Elements("equipped").Take(2).ToArray();
            if (!Guid.TryParseExact(ReadDirectValue(armor, "guid"), "D", out Guid armorId)
                || armorId == Guid.Empty
                || equippedNodes.Length != 1
                || !bool.TryParse(equippedNodes[0].Value, out bool equipped))
            {
                throw new InvalidOperationException("Armor equipment state requires unique stable identities and exact saved Booleans.");
            }
            resolved.Add((new CharacterArmorEquipmentBasis(armorId, equipped), equippedNodes[0]));
        }

        CharacterArmorEquipmentBasis[] basis = resolved.Select(item => item.Basis).ToArray();
        if (!CharacterArmorEquipmentRules.TryProject(request.ArmorId, basis, out CharacterArmorEquipmentState? state)
            || state is null
            || state.Equipped != request.ExpectedEquipped
            || state.ArmorCount != request.ExpectedArmorCount
            || state.EquippedCount != request.ExpectedEquippedCount
            || !CharacterArmorEquipmentRules.CanApply(
                request.Action,
                state.Equipped,
                state.ArmorCount,
                state.EquippedCount))
        {
            throw new InvalidOperationException(
                "Armor equipment state changed, is ambiguous, or the requested action is already satisfied.");
        }

        foreach ((CharacterArmorEquipmentBasis armor, XElement equipped) in resolved)
        {
            bool updated = CharacterArmorEquipmentRules.ResolveEquipped(
                request.Action,
                request.ArmorId,
                armor.ArmorId,
                armor.Equipped);
            equipped.Value = updated ? "True" : "False";
        }
        return Serialize(document);
    }

    private static bool TryCalculateArmorDamageMaximum(XElement armor, out int maximum)
    {
        maximum = 0;
        if (!TryParseOptionalInt(ReadDirectValue(armor, "rating"), out int rating))
        {
            return false;
        }
        CharacterArmorDamageModifierBasis[] modifiers = armor
            .Element("armormods")?
            .Elements("armormod")
            .Select(modifier =>
            {
                bool armorExact = TryParseOptionalInt(ReadDirectValue(modifier, "armor"), out int armorValue);
                bool equippedExact = TryParseOptionalBool(ReadDirectValue(modifier, "equipped"), out bool equipped);
                return new CharacterArmorDamageModifierBasis(
                    armorValue,
                    equipped,
                    armorExact && equippedExact);
            })
            .ToArray()
            ?? [];
        return CharacterArmorDamageRules.TryCalculateMaximum(
            ReadDirectValue(armor, "armor"),
            ReadDirectValue(armor, "armoroverride"),
            rating,
            modifiers,
            out maximum);
    }

    private static string ReadDirectValue(XElement item, string elementName)
        => item.Element(elementName)?.Value ?? string.Empty;

    public static string ApplyGearQuantityEdit(
        string xml,
        GearQuantityEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.GearId == Guid.Empty)
        {
            throw new InvalidOperationException("Gear quantity editing requires a stable Gear Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(ReadDirectValue(root, "created")))
        {
            throw new InvalidOperationException("Gear quantity lifecycle actions are Career-only.");
        }

        XElement container = root.Element("gears")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <gears> collection.");
        XElement gear = FindUniqueItemById(
            container,
            "gear",
            request.GearId.ToString("D"),
            "Gear quantity");
        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        int? maximumNuyenDecimals = sourceData is not null
            && sourceData.TryResolveMaxNuyenDecimals(out int decimals)
                ? decimals
                : null;
        if (!TryReadExactGearQuantity(
                gear,
                maximumNuyenDecimals,
                out decimal quantity,
                out decimal minimumIncrement))
        {
            throw new InvalidOperationException(
                "Gear quantity precision is unavailable from the exact saved runner settings.");
        }
        if (!CharacterGearQuantityRules.IsValidAmount(request.Amount, minimumIncrement))
        {
            throw new InvalidOperationException(
                $"Gear quantity must use increments of {minimumIncrement.ToString(CultureInfo.InvariantCulture)}.");
        }

        switch (request.Action)
        {
            case GearQuantityAction.Increase:
                ApplyGearQuantityIncrease(root, gear, quantity, request.Amount);
                break;
            case GearQuantityAction.Reduce:
                ApplyGearQuantityReduction(root, gear, quantity, request.Amount, request.ReductionConfirmed);
                break;
            case GearQuantityAction.Split:
                ApplyGearQuantitySplit(root, container, gear, quantity, request.Amount, minimumIncrement);
                break;
            case GearQuantityAction.Merge:
                ApplyGearQuantityMerge(
                    root,
                    container,
                    gear,
                    quantity,
                    request.Amount,
                    request.MergeTargetGearId,
                    maximumNuyenDecimals);
                break;
            default:
                throw new InvalidOperationException($"Unsupported Gear quantity action '{request.Action}'.");
        }

        return Serialize(document);
    }

    public static string ApplyQualityLevelEdit(
        string xml,
        QualityLevelEditRequest request,
        ICharacterSourceDataResolver? sourceDataResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.QualityId == Guid.Empty
            || request.ExpectedLevel < 1
            || request.MaximumLevel < request.ExpectedLevel
            || request.NewLevel < 1
            || request.NewLevel > request.MaximumLevel)
        {
            throw new InvalidOperationException("Quality Level request is outside its exact projected bounds.");
        }

        CharacterQualitySummary[] matches = new CharacterSectionService(sourceDataResolver)
            .ParseQualities(xml)
            .Qualities
            .Where(quality => quality.LevelSemantics is { } semantics
                && semantics.AnchorQualityId == request.QualityId)
            .ToArray();
        if (matches.Length != 1
            || matches[0].LevelSemantics is not { } projected
            || projected.Level != request.ExpectedLevel
            || projected.MaximumLevel != request.MaximumLevel)
        {
            throw new InvalidOperationException(
                "Quality Level identity, current level, or source bound changed; reopen before saving.");
        }
        if (projected.CareerMode
            && request.NewLevel > request.ExpectedLevel
            && !request.IncreaseConfirmed)
        {
            throw new InvalidOperationException("Career Quality Level increases require explicit confirmation.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" } parsedRoot
            ? parsedRoot
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement container = root.Element("qualities")
            ?? throw new InvalidOperationException("Workspace XML does not contain the required <qualities> collection.");
        XElement anchor = FindUniqueItemById(
            container,
            "quality",
            request.QualityId.ToString("D"),
            "Quality Level");
        string sourceId = ReadDirectValue(anchor, "sourceid");
        string extra = ReadDirectValue(anchor, "extra");
        string sourceName = ReadDirectValue(anchor, "sourcename");
        string qualityType = ReadDirectValue(anchor, "qualitytype");
        XElement[] levels = container.Elements("quality")
            .Where(item =>
                string.Equals(ReadDirectValue(item, "sourceid"), sourceId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(ReadDirectValue(item, "extra"), extra, StringComparison.Ordinal)
                && string.Equals(ReadDirectValue(item, "sourcename"), sourceName, StringComparison.Ordinal)
                && string.Equals(ReadDirectValue(item, "qualitytype"), qualityType, StringComparison.Ordinal))
            .ToArray();
        if (levels.Length != request.ExpectedLevel || !ReferenceEquals(levels[0], anchor))
        {
            throw new InvalidOperationException("Quality Level saved identity group changed; reopen before saving.");
        }

        if (request.NewLevel > request.ExpectedLevel)
        {
            for (int level = request.ExpectedLevel; level < request.NewLevel; level++)
            {
                XElement clone = new(anchor);
                Guid cloneId = Guid.NewGuid();
                SetElementValue(clone, "guid", cloneId.ToString("D"));
                container.Add(clone);
                if (projected.CareerMode)
                {
                    AppendFreeCareerQualityExpense(root, clone, projected.QualityType);
                }
            }
        }
        else if (request.NewLevel < request.ExpectedLevel)
        {
            int removeCount = request.ExpectedLevel - request.NewLevel;
            foreach (XElement level in levels.Where(item => !ReferenceEquals(item, anchor)).Take(removeCount))
            {
                if (projected.CareerMode
                    && string.Equals(projected.QualityType, "Negative", StringComparison.Ordinal))
                {
                    AppendFreeCareerNegativeQualityRemovalExpense(root, level);
                }
                level.Remove();
            }
        }

        return Serialize(document);
    }

    private static void AppendFreeCareerQualityExpense(
        XElement root,
        XElement quality,
        string qualityType)
    {
        string qualityId = ReadDirectValue(quality, "guid");
        string qualityName = FirstNonBlank(ReadDirectValue(quality, "name"), "Quality");
        string verb = string.Equals(qualityType, "Negative", StringComparison.Ordinal)
            ? "Add Negative Quality"
            : "Add Positive Quality";
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("date", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", "0"),
                new XElement("reason", $"{verb} {qualityName}"),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", "AddQuality"),
                    new XElement("nuyentype", "ManualAdd"),
                    new XElement("objectid", qualityId),
                    new XElement("qty", "0"),
                    new XElement("extra"))));
    }

    private static void AppendFreeCareerNegativeQualityRemovalExpense(
        XElement root,
        XElement quality)
    {
        string sourceId = ReadDirectValue(quality, "sourceid");
        string qualityName = FirstNonBlank(ReadDirectValue(quality, "name"), "Quality");
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("date", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", "0"),
                new XElement("reason", $"Remove Negative Quality {qualityName}"),
                new XElement("type", "Karma"),
                new XElement("refund", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", "RemoveQuality"),
                    new XElement("nuyentype", "ManualAdd"),
                    new XElement("objectid", sourceId),
                    new XElement("qty", "0"),
                    new XElement("extra", ReadDirectValue(quality, "extra")))));
    }

    private static void ApplyGearQuantityIncrease(
        XElement root,
        XElement gear,
        decimal currentQuantity,
        decimal amount)
    {
        decimal updatedQuantity = checked(currentQuantity + amount);
        if (updatedQuantity > CharacterGearQuantityRules.MaximumQuantity
            || !TryBuildGearCostSnapshot(gear, out CharacterGearCostSnapshot? costSnapshot)
            || !CharacterGearQuantityRules.TryCalculatePurchaseUnitCost(costSnapshot!, out decimal unitCost))
        {
            throw new InvalidOperationException(
                "This Gear's exact saved purchase cost is unavailable; quantity increase was refused.");
        }

        decimal purchaseCost = checked(unitCost * amount);
        XElement nuyen = root.Element("nuyen")
            ?? throw new InvalidOperationException("Career Gear purchase requires an exact saved Nuyen balance.");
        if (!decimal.TryParse(
                nuyen.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal availableNuyen))
        {
            throw new InvalidOperationException("Career Gear purchase requires an exact saved Nuyen balance.");
        }
        if (availableNuyen < purchaseCost)
        {
            throw new InvalidOperationException(
                $"Gear quantity increase costs {purchaseCost.ToString(CultureInfo.InvariantCulture)} Nuyen but only {availableNuyen.ToString(CultureInfo.InvariantCulture)} is available.");
        }

        SetElementValue(gear, "qty", updatedQuantity.ToString(CultureInfo.InvariantCulture));
        nuyen.Value = (availableNuyen - purchaseCost).ToString(CultureInfo.InvariantCulture);
        AppendGearPurchaseExpense(
            root,
            gear,
            purchaseCost,
            amount);
    }

    private static void ApplyGearQuantityReduction(
        XElement root,
        XElement gear,
        decimal currentQuantity,
        decimal amount,
        bool confirmed)
    {
        if (!confirmed)
        {
            throw new InvalidOperationException("Reducing Gear quantity requires explicit deletion confirmation.");
        }
        if (amount > currentQuantity)
        {
            throw new InvalidOperationException("Gear reduction cannot exceed the selected stack quantity.");
        }

        decimal remaining = currentQuantity - amount;
        if (remaining == 0m)
        {
            EnsureGearCloneOrRemovalIsIsolated(root, gear);
            gear.Remove();
        }
        else
        {
            SetElementValue(gear, "qty", remaining.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ApplyGearQuantitySplit(
        XElement root,
        XElement container,
        XElement gear,
        decimal currentQuantity,
        decimal amount,
        decimal minimumIncrement)
    {
        if (currentQuantity <= minimumIncrement
            || amount > currentQuantity - minimumIncrement)
        {
            throw new InvalidOperationException(
                "A Gear split must leave at least one exact minimum increment in the original stack.");
        }

        EnsureGearCloneOrRemovalIsIsolated(root, gear);
        XElement clone = new(gear);
        foreach (XElement clonedGear in clone.DescendantsAndSelf().Where(node => node.Name.LocalName == "gear"))
        {
            SetElementValue(clonedGear, "guid", Guid.NewGuid().ToString("D"));
        }
        SetElementValue(clone, "qty", amount.ToString(CultureInfo.InvariantCulture));
        SetElementValue(gear, "qty", (currentQuantity - amount).ToString(CultureInfo.InvariantCulture));
        container.Add(clone);
    }

    private static void ApplyGearQuantityMerge(
        XElement root,
        XElement container,
        XElement source,
        decimal sourceQuantity,
        decimal amount,
        Guid? targetGearId,
        int? maximumNuyenDecimals)
    {
        if (targetGearId is not { } targetId || targetId == Guid.Empty || targetId == Guid.Parse(ReadDirectValue(source, "guid")))
        {
            throw new InvalidOperationException("Gear merge requires a different stable target Gear Guid.");
        }
        if (amount > sourceQuantity)
        {
            throw new InvalidOperationException("Gear merge cannot exceed the selected source stack quantity.");
        }

        XElement target = FindUniqueItemById(container, "gear", targetId.ToString("D"), "Gear merge target");
        if (!TryReadExactGearQuantity(target, maximumNuyenDecimals, out decimal targetQuantity, out decimal targetIncrement)
            || !CharacterGearQuantityRules.IsValidAmount(amount, targetIncrement)
            || !TryBuildGearMergeIdentity(source, out CharacterGearMergeIdentity? sourceIdentity)
            || !TryBuildGearMergeIdentity(target, out CharacterGearMergeIdentity? targetIdentity)
            || !CharacterGearQuantityRules.AreIdenticalForMerge(sourceIdentity, targetIdentity))
        {
            throw new InvalidOperationException(
                "The selected Gear stacks are not exact Chummer5 IsIdenticalToOtherGear merge matches.");
        }

        decimal updatedTargetQuantity = checked(targetQuantity + amount);
        if (updatedTargetQuantity > CharacterGearQuantityRules.MaximumQuantity)
        {
            throw new InvalidOperationException("Merged Gear quantity exceeds the supported saved-data limit.");
        }

        decimal remaining = sourceQuantity - amount;
        if (remaining == 0m)
        {
            EnsureGearCloneOrRemovalIsIsolated(root, source);
        }
        SetElementValue(target, "qty", updatedTargetQuantity.ToString(CultureInfo.InvariantCulture));
        if (remaining == 0m)
        {
            source.Remove();
        }
        else
        {
            SetElementValue(source, "qty", remaining.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void EnsureGearCloneOrRemovalIsIsolated(XElement root, XElement gear)
    {
        XElement[] gearSubtree = gear.DescendantsAndSelf()
            .Where(node => node.Name.LocalName == "gear")
            .ToArray();
        HashSet<string> gearIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement item in gearSubtree)
        {
            string id = ReadDirectValue(item, "guid");
            if (!Guid.TryParseExact(id, "D", out Guid parsed) || parsed == Guid.Empty || !gearIds.Add(id))
            {
                throw new InvalidOperationException(
                    "Gear clone/removal requires unique stable recursive Gear GUIDs.");
            }
            if (Guid.TryParse(ReadDirectValue(item, "weaponid"), out Guid weaponId) && weaponId != Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Gear with a generated Weapon cannot be cloned or removed through the bounded quantity path.");
            }
        }

        HashSet<XElement> subtree = gear.DescendantsAndSelf().ToHashSet();
        bool externallyReferenced = root.Descendants()
            .Where(node => !subtree.Contains(node) && !node.HasElements)
            .Any(node => gearIds.Contains(node.Value.Trim()));
        if (externallyReferenced)
        {
            throw new InvalidOperationException(
                "Gear with external saved-data references cannot be cloned or removed through the bounded quantity path.");
        }
    }

    private static bool TryReadExactGearQuantity(
        XElement gear,
        int? maximumNuyenDecimals,
        out decimal quantity,
        out decimal minimumIncrement)
    {
        quantity = 0m;
        minimumIncrement = 0m;
        return decimal.TryParse(
                ReadDirectValue(gear, "qty"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out quantity)
            && CharacterGearQuantityRules.TryResolvePrecision(
                ReadDirectValue(gear, "name"),
                ReadDirectValue(gear, "category"),
                maximumNuyenDecimals,
                out _,
                out minimumIncrement)
            && CharacterGearQuantityRules.IsValidAmount(quantity, minimumIncrement);
    }

    private static bool TryBuildGearMergeIdentity(
        XElement gear,
        out CharacterGearMergeIdentity? identity)
    {
        identity = null;
        if (!int.TryParse(
                ReadDirectValue(gear, "rating"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int rating))
        {
            return false;
        }

        List<CharacterGearMergeChildIdentity> children = [];
        foreach (XElement child in gear.Element("children")?.Elements("gear") ?? [])
        {
            if (!decimal.TryParse(
                    ReadDirectValue(child, "qty"),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal quantity)
                || quantity <= 0m
                || !TryBuildGearMergeIdentity(child, out CharacterGearMergeIdentity? childIdentity))
            {
                return false;
            }
            children.Add(new CharacterGearMergeChildIdentity(quantity, childIdentity!));
        }

        identity = new CharacterGearMergeIdentity(
            ReadDirectValue(gear, "name"),
            ReadDirectValue(gear, "category"),
            rating,
            ReadDirectValue(gear, "extra"),
            ReadDirectValue(gear, "gearname"),
            ReadDirectValue(gear, "notes"),
            children);
        return true;
    }

    private static bool TryBuildGearCostSnapshot(
        XElement gear,
        out CharacterGearCostSnapshot? snapshot)
    {
        snapshot = null;
        if (!int.TryParse(ReadDirectValue(gear, "rating"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rating)
            || !TryReadPositiveDecimal(gear, "qty", 1m, out decimal quantity)
            || !TryReadPositiveDecimal(gear, "costfor", 1m, out decimal costFor)
            || !TryParseOptionalBool(ReadDirectValue(gear, "discountedcost"), out bool discounted))
        {
            return false;
        }

        int childMultiplier = 1;
        string childMultiplierText = ReadDirectValue(gear, "childcostmultiplier");
        if (!string.IsNullOrWhiteSpace(childMultiplierText)
            && (!int.TryParse(childMultiplierText, NumberStyles.Integer, CultureInfo.InvariantCulture, out childMultiplier)
                || childMultiplier <= 0))
        {
            return false;
        }

        List<CharacterGearCostSnapshot> children = [];
        foreach (XElement child in gear.Element("children")?.Elements("gear") ?? [])
        {
            if (!TryBuildGearCostSnapshot(child, out CharacterGearCostSnapshot? childSnapshot))
            {
                return false;
            }
            children.Add(childSnapshot!);
        }

        snapshot = new CharacterGearCostSnapshot(
            rating,
            quantity,
            ReadDirectValue(gear, "cost"),
            costFor,
            discounted,
            childMultiplier,
            children);
        return true;
    }

    private static bool TryReadPositiveDecimal(
        XElement gear,
        string elementName,
        decimal fallback,
        out decimal value)
    {
        string raw = ReadDirectValue(gear, elementName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = fallback;
            return true;
        }
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value > 0m;
    }

    private static void AppendGearPurchaseExpense(
        XElement root,
        XElement gear,
        decimal purchaseCost,
        decimal quantity)
    {
        string gearId = ReadDirectValue(gear, "guid");
        string displayName = FirstNonBlank(ReadDirectValue(gear, "gearname"), ReadDirectValue(gear, "name"), "Gear");
        EnsureElement(root, "expenses").Add(
            new XElement(
                "expense",
                new XElement("guid", Guid.NewGuid().ToString("D")),
                new XElement("date", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
                new XElement("amount", (-purchaseCost).ToString(CultureInfo.InvariantCulture)),
                new XElement("reason", $"Purchased Gear {displayName}"),
                new XElement("type", "Nuyen"),
                new XElement("refund", "False"),
                new XElement(
                    "undo",
                    new XElement("karmatype", "ImproveAttribute"),
                    new XElement("nuyentype", "AddGear"),
                    new XElement("objectid", gearId),
                    new XElement("qty", quantity.ToString(CultureInfo.InvariantCulture)),
                    new XElement("extra"))));
    }

    public static string ApplyCyberwareCommerceEdit(
        string xml,
        CyberwareCommerceRequest request,
        ICharacterSourceDataResolver? sourceDataResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        if (request.CyberwareId == Guid.Empty)
        {
            throw new InvalidOperationException("Cyberware commerce requires a stable Cyberware Guid.");
        }
        if (!request.Confirmed)
        {
            throw new InvalidOperationException("Cyberware commerce requires explicit confirmation.");
        }
        if (string.IsNullOrEmpty(request.QuoteDigest)
            || request.QuoteDigest.Length != 64
            || request.QuoteDigest.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("Cyberware commerce requires an exact lowercase SHA-256 quote digest.");
        }

        CharacterCyberwareSummary[] summaries = new CharacterSectionService(sourceDataResolver)
            .ParseCyberwares(xml)
            .Cyberwares
            .Where(candidate => Guid.TryParseExact(candidate.Guid, "D", out Guid parsed)
                && parsed == request.CyberwareId)
            .ToArray();
        if (summaries.Length != 1 || summaries[0].CommerceSemantics is not { } semantics)
        {
            throw new InvalidOperationException("The selected Cyberware commerce state is unavailable.");
        }

        CharacterCyberwareCommerceQuote quote = request.Action switch
        {
            CharacterCyberwareCommerceAction.Upgrade => CharacterCyberwareCommerceRules.QuoteUpgrade(
                semantics,
                request.GradeId,
                request.Rating,
                request.RefundPercentage,
                request.FreeCost),
            CharacterCyberwareCommerceAction.Sell => CharacterCyberwareCommerceRules.QuoteSale(
                semantics,
                request.RefundPercentage),
            _ => throw new InvalidOperationException($"Unsupported Cyberware commerce action '{request.Action}'.")
        };
        if (!quote.Exact)
        {
            throw new InvalidOperationException(quote.BlockReason);
        }
        if (!string.Equals(quote.QuoteDigest, request.QuoteDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Cyberware quote changed. Reopen commerce before saving.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(ReadDirectValue(root, "created")))
        {
            throw new InvalidOperationException("Cyberware commerce is Career-only.");
        }
        XElement cyberware = FindUniqueCyberware(root, request.CyberwareId);
        bool hasParent = cyberware.Ancestors("cyberware").Any();
        if (hasParent && string.Equals(ReadDirectValue(cyberware, "capacity"), "[*]", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Linked Capacity=[*] child Cyberware cannot be upgraded or sold.");
        }

        XElement nuyen = root.Element("nuyen")
            ?? throw new InvalidOperationException("Cyberware commerce requires an exact saved Nuyen balance.");
        if (!decimal.TryParse(nuyen.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal availableNuyen)
            || semantics.Snapshot is null
            || availableNuyen != semantics.Snapshot.AvailableNuyen)
        {
            throw new InvalidOperationException("The saved Nuyen balance changed before Cyberware commerce committed.");
        }

        decimal updatedNuyen = checked(availableNuyen + quote.NuyenDelta);
        switch (request.Action)
        {
            case CharacterCyberwareCommerceAction.Upgrade:
                ApplyEssenceBookkeeping(
                    root,
                    semantics.Snapshot,
                    quote.NewEssenceHoleRating,
                    quote.NewEssenceAntiHoleRating);
                SetElementValue(cyberware, "rating", quote.Rating.ToString(CultureInfo.InvariantCulture));
                SetElementValue(cyberware, "grade", quote.GradeName);
                AppendCyberwareExpense(
                    root,
                    cyberware,
                    quote.NuyenDelta,
                    $"Upgraded Cyberware {summaries[0].Name}",
                    addGearUndo: true);
                break;
            case CharacterCyberwareCommerceAction.Sell:
                ApplyEssenceBookkeeping(
                    root,
                    semantics.Snapshot,
                    quote.NewEssenceHoleRating,
                    quote.NewEssenceAntiHoleRating);
                AppendCyberwareExpense(
                    root,
                    cyberware,
                    quote.NuyenDelta,
                    $"Sold Cyberware {summaries[0].Name}",
                    addGearUndo: false);
                cyberware.Remove();
                break;
            default:
                throw new InvalidOperationException($"Unsupported Cyberware commerce action '{request.Action}'.");
        }
        nuyen.Value = updatedNuyen.ToString(CultureInfo.InvariantCulture);
        return Serialize(document);
    }

    private static XElement FindUniqueCyberware(XElement root, Guid cyberwareId)
    {
        XElement[] matches = root.Element("cyberwares")?
            .Descendants("cyberware")
            .Where(candidate => Guid.TryParseExact(ReadDirectValue(candidate, "guid"), "D", out Guid parsed)
                && parsed == cyberwareId)
            .ToArray()
            ?? [];
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("Cyberware commerce requires one exact stable Cyberware identity.");
    }

    private static void ApplyEssenceBookkeeping(
        XElement root,
        CharacterCyberwareCommerceSnapshot snapshot,
        int? newHoleRating,
        int? newAntiHoleRating)
    {
        ApplyEssenceBookkeepingItem(
            root,
            "b57eadaa-7c3b-4b80-8d79-cbbd922c1196",
            snapshot.EssenceHoleRating,
            newHoleRating);
        ApplyEssenceBookkeepingItem(
            root,
            "961eac53-0c43-4b19-8741-2872177a3a4c",
            snapshot.EssenceAntiHoleRating,
            newAntiHoleRating);
    }

    private static void ApplyEssenceBookkeepingItem(
        XElement root,
        string sourceId,
        int? expectedRating,
        int? newRating)
    {
        XElement[] matches = root.Element("cyberwares")?
            .Elements("cyberware")
            .Where(candidate => string.Equals(
                ReadDirectValue(candidate, "sourceid"),
                sourceId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? [];
        if (expectedRating is null)
        {
            if (matches.Length != 0 || newRating is not null)
            {
                throw new InvalidOperationException("Essence Hole bookkeeping changed before Cyberware commerce committed.");
            }
            return;
        }
        if (matches.Length != 1
            || !int.TryParse(ReadDirectValue(matches[0], "rating"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int current)
            || current != expectedRating.Value
            || newRating is null)
        {
            throw new InvalidOperationException("Essence Hole bookkeeping changed before Cyberware commerce committed.");
        }
        EnsureSimpleBookkeepingItem(root, matches[0]);
        if (newRating.Value == 0)
        {
            matches[0].Remove();
        }
        else
        {
            SetElementValue(matches[0], "rating", newRating.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void EnsureSimpleBookkeepingItem(XElement root, XElement item)
    {
        if (item.Descendants("cyberware").Any()
            || item.Descendants("gear").Any()
            || item.DescendantsAndSelf().Any(element => !element.HasElements
                && element.Name.LocalName is ("weaponid" or "vehicleid")
                && Guid.TryParse(element.Value.Trim(), out Guid generatedId)
                && generatedId != Guid.Empty))
        {
            throw new InvalidOperationException("Complex Essence Hole bookkeeping cannot be mutated through the bounded commerce path.");
        }
        string itemGuid = ReadDirectValue(item, "guid");
        HashSet<XElement> subtree = item.DescendantsAndSelf().ToHashSet();
        if (root.Descendants().Where(element => !subtree.Contains(element) && !element.HasElements)
            .Any(element => string.Equals(element.Value.Trim(), itemGuid, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Referenced Essence Hole bookkeeping cannot be mutated through the bounded commerce path.");
        }
    }

    private static void AppendCyberwareExpense(
        XElement root,
        XElement cyberware,
        decimal amount,
        string reason,
        bool addGearUndo)
    {
        var expense = new XElement(
            "expense",
            new XElement("guid", Guid.NewGuid().ToString("D")),
            new XElement("date", DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture)),
            new XElement("amount", amount.ToString(CultureInfo.InvariantCulture)),
            new XElement("reason", reason),
            new XElement("type", "Nuyen"),
            new XElement("refund", "False"));
        if (addGearUndo)
        {
            // Preserve the exact Chummer5 Cyberware.Upgrade legacy quirk.
            expense.Add(new XElement(
                "undo",
                new XElement("karmatype", "ImproveAttribute"),
                new XElement("nuyentype", "AddGear"),
                new XElement("objectid", ReadDirectValue(cyberware, "guid")),
                new XElement("qty", "0"),
                new XElement("extra")));
        }
        EnsureElement(root, "expenses").Add(expense);
    }

    public static string ApplyLocationRename(string xml, LocationRenameRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);

        string name = LocationRenameRequest.ValidateName(request.Name);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        string containerName = WorkspaceLocationEditorProjector.SectionId(request.Kind);
        XElement container = root.Element(containerName)
            ?? throw new InvalidOperationException(
                $"Workspace XML does not contain the required <{containerName}> location container.");
        XElement location = FindUniqueItemById(
            container,
            "location",
            request.LocationId.ToString("D"),
            $"{request.Kind} location");
        SetElementValue(location, "name", name);
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
            case WorkspaceSetCollectionIntegerRequest integerRequest:
                ApplyIntegerMutation(root, integerRequest);
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
            || request.IntegerValues is { Count: > 0 }
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

        IEnumerable<KeyValuePair<WorkspaceCollectionIntegerField, int>> integerValues = request.IntegerValues is null
            ? Enumerable.Empty<KeyValuePair<WorkspaceCollectionIntegerField, int>>()
            : request.IntegerValues.OrderBy(static pair => pair.Key);
        foreach ((WorkspaceCollectionIntegerField field, int value) in integerValues)
        {
            ApplyIntegerMutation(root, new WorkspaceSetCollectionIntegerRequest(request.Target, field, value));
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
        if (resolved.Kind == WorkspaceCollectionKind.Spirit
            && resolved.NestedKind is null
            && request.Field == WorkspaceCollectionTextField.CritterName
            && (!string.IsNullOrWhiteSpace(resolved.Item.Element("file")?.Value)
                || !string.IsNullOrWhiteSpace(resolved.Item.Element("relative")?.Value)))
        {
            throw new InvalidOperationException(
                "Spirit Critter Name is read-only until the saved linked-character path is cleared.");
        }
        string elementName = ResolveTextElementName(resolved, request.Field);
        string value = request.Value ?? string.Empty;
        int maximumLength = request.Field switch
        {
            WorkspaceCollectionTextField.Name => MaximumNameLength,
            WorkspaceCollectionTextField.GearName => MaximumSelectTextLength,
            _ => MaximumTextLength
        };
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
        if (resolved.Kind == WorkspaceCollectionKind.Spirit
            && resolved.NestedKind is null
            && request.Field == WorkspaceCollectionToggleField.Bound
            && !ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Spirit Bound/Registered can only be changed for a created/career runner.");
        }
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

    private static void ApplyIntegerMutation(XElement root, WorkspaceSetCollectionIntegerRequest request)
    {
        ResolvedCollectionItem resolved = ResolveCollectionItem(root, request.Target);
        if (resolved.Kind != WorkspaceCollectionKind.Spirit
            || resolved.NestedKind is not null)
        {
            throw new InvalidOperationException(
                $"Collection integer field '{request.Field}' is not supported for this item.");
        }

        if (request.Field == WorkspaceCollectionIntegerField.Services)
        {
            if (request.Value < 0)
            {
                throw new InvalidOperationException("Spirit Services/Tasks Owed cannot be negative.");
            }

            SetElementValue(resolved.Item, "services", request.Value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (request.Field != WorkspaceCollectionIntegerField.Force)
        {
            throw new InvalidOperationException(
                $"Collection integer field '{request.Field}' is not supported for this item.");
        }
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Spirit Force/Rating can only be changed for a created/career runner.");
        }
        if (!TryCalculateSpiritForceMaximum(root, resolved.Item, created: true, out int maximum))
        {
            throw new InvalidOperationException(
                "Spirit Force/Rating is read-only because the saved runner cannot determine the exact Chummer5 maximum.");
        }
        if (request.Value < 0 || request.Value > maximum)
        {
            throw new InvalidOperationException(
                $"Spirit Force/Rating must be between 0 and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        SetElementValue(resolved.Item, "force", request.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryCalculateSpiritForceMaximum(
        XElement character,
        XElement spirit,
        bool created,
        out int maximum)
    {
        maximum = 0;
        string entityType = (spirit.Element("type")?.Value ?? string.Empty).Trim().ToUpperInvariant();
        int basis;
        switch (entityType)
        {
            case "SPRITE":
                if (!ParseBool(character.Element("resenabled")?.Value)
                    || !TryReadCharacterAttributeValue(character, "RES", "totalvalue", out basis))
                {
                    return false;
                }
                break;
            case "SPIRIT":
                if (!ParseBool(character.Element("magenabled")?.Value)
                    || !TryReadCharacterAttributeValue(character, "MAG", "value", out int magicValue)
                    || !TryReadCharacterAttributeValue(character, "MAG", "totalvalue", out int magicTotalValue))
                {
                    return false;
                }

                string savedSetting = character.Element("spiritforcebasedontotalmag")?.Value ?? string.Empty;
                if (bool.TryParse(savedSetting, out bool useTotalMagic))
                {
                    basis = useTotalMagic ? magicTotalValue : magicValue;
                }
                else if (magicValue == magicTotalValue)
                {
                    basis = magicValue;
                }
                else
                {
                    return false;
                }
                break;
            default:
                return false;
        }

        if (basis <= 0)
        {
            return true;
        }
        if (created)
        {
            if (basis > int.MaxValue / 2)
            {
                return false;
            }
            basis *= 2;
        }

        maximum = basis;
        return true;
    }

    private static bool TryReadCharacterAttributeValue(
        XElement character,
        string attributeName,
        string propertyName,
        out int value)
    {
        value = 0;
        XElement? attribute = character.Element("attributes")?
            .Elements("attribute")
            .FirstOrDefault(candidate => string.Equals(
                candidate.Element("name")?.Value ?? string.Empty,
                attributeName,
                StringComparison.OrdinalIgnoreCase));
        return attribute is not null
            && int.TryParse(
                attribute.Element(propertyName)?.Value ?? string.Empty,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
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
                WorkspaceCollectionTextField.GearName
                    when resolved.Kind == WorkspaceCollectionKind.Gear
                        && resolved.NestedKind == WorkspaceNestedCollectionKind.Gear => "gearname",
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

        if (field == WorkspaceCollectionTextField.GearName
            && resolved.Kind == WorkspaceCollectionKind.Gear)
        {
            return "gearname";
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
            (WorkspaceCollectionKind.Spirit, WorkspaceCollectionTextField.CritterName) => "crittername",
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
