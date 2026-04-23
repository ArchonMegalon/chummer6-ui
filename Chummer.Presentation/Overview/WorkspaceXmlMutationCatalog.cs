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
