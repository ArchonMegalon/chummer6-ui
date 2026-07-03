using System;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace Chummer.Presentation.Overview;

internal static class WorkspaceXmlMutationCatalog
{
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

    private static void AddGear(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "gears").Add(
            new XElement(
                "gear",
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
                new XElement("guid", $"desktop-{NormalizeToken(request.Name)}"),
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
                new XElement("name", request.Name),
                new XElement("role", FirstNonBlank(request.Role, "Contact")),
                new XElement("location", FirstNonBlank(request.Location, "Seattle")),
                new XElement("connection", Math.Max(0, request.Connection).ToString(CultureInfo.InvariantCulture)),
                new XElement("loyalty", Math.Max(0, request.Loyalty).ToString(CultureInfo.InvariantCulture))));
    }

    private static void AddVehicle(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "vehicles").Add(
            new XElement(
                "vehicle",
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
                new XElement("name", request.Name),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add")),
                new XElement("bp", request.Karma.ToString(CultureInfo.InvariantCulture))));
    }

    private static void AddDrug(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "drugs").Add(
            new XElement(
                "drug",
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
                new XElement("guid", $"desktop-{NormalizeToken(request.Name)}"),
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
                new XElement("name", request.Name),
                new XElement("rating", FirstNonBlank(request.Slot, request.Rating > 0 ? request.Rating.ToString(CultureInfo.InvariantCulture) : null, "1")),
                new XElement("source", FirstNonBlank(request.Source, "Desktop Quick Add"))));
    }

    private static void AddInitiationGrade(XElement root, WorkspaceQuickAddRequest request)
    {
        EnsureElement(root, "initiationgrades").Add(
            new XElement(
                "initiationgrade",
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

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : 0m;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;

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
