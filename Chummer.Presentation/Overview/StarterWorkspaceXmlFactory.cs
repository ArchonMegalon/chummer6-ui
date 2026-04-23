using System.IO;
using System.Linq;
using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Rulesets;

namespace Chummer.Presentation.Overview;

internal static class StarterWorkspaceXmlFactory
{
    public static string CreateCharacterXml(
        string rulesetId,
        string name,
        string alias,
        string buildMethod,
        bool isCritter = false)
    {
        string normalizedRulesetId = RulesetDefaults.NormalizeOptional(rulesetId) ?? RulesetDefaults.Sr5;
        string edition = normalizedRulesetId switch
        {
            var id when string.Equals(id, RulesetDefaults.Sr6, StringComparison.Ordinal) => "SR6",
            var id when string.Equals(id, RulesetDefaults.Sr4, StringComparison.Ordinal) => "SR4",
            _ => "SR5"
        };
        string resolvedBuildMethod = ResolveBuildMethod(normalizedRulesetId, buildMethod);
        string concept = isCritter
            ? $"Starter {edition} critter dossier"
            : $"{edition} street operator starter dossier";
        string metatype = isCritter ? "Critter" : "Human";
        string priorityMetatype = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "D" : string.Empty;
        string priorityAttributes = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "B" : string.Empty;
        string prioritySpecial = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "E" : string.Empty;
        string prioritySkills = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "C" : string.Empty;
        string priorityResources = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "A" : string.Empty;
        string priorityTalent = string.Equals(resolvedBuildMethod, "Priority", StringComparison.OrdinalIgnoreCase) ? "D" : string.Empty;

        XDocument document = new(
            new XElement(
                "character",
                new XElement("name", string.IsNullOrWhiteSpace(name) ? "New Character" : name.Trim()),
                new XElement("alias", string.IsNullOrWhiteSpace(alias) ? "Runner" : alias.Trim()),
                new XElement("playername", "Desktop User"),
                new XElement("metatype", metatype),
                new XElement("metavariant", isCritter ? "Starter Critter" : string.Empty),
                new XElement("sex", isCritter ? string.Empty : "Unspecified"),
                new XElement("age", isCritter ? "2" : "28"),
                new XElement("height", isCritter ? string.Empty : "178 cm"),
                new XElement("weight", isCritter ? string.Empty : "78 kg"),
                new XElement("hair", isCritter ? string.Empty : "Black"),
                new XElement("eyes", isCritter ? string.Empty : "Brown"),
                new XElement("skin", isCritter ? string.Empty : "Light"),
                new XElement("concept", concept),
                new XElement("description", isCritter
                    ? "Starter critter profile seeded with enough detail to exercise the desktop sections."
                    : "Starter runner profile seeded with practical data for section-level parity checks."),
                new XElement("background", isCritter
                    ? "A deterministic critter starter payload for the Avalonia workspace."
                    : "A deterministic starter payload for the Avalonia workspace and parity tests."),
                new XElement("notes", "Starter workspace seeded by the desktop parity flow."),
                new XElement("buildmethod", resolvedBuildMethod),
                new XElement("createdversion", "5.225.0"),
                new XElement("appversion", "5.225.0"),
                new XElement("karma", "35"),
                new XElement("nuyen", "8500"),
                new XElement("startingnuyen", "8500"),
                new XElement("streetcred", "0"),
                new XElement("notoriety", "0"),
                new XElement("publicawareness", "0"),
                new XElement("burntstreetcred", "0"),
                new XElement("buildkarma", "35"),
                new XElement("created", "True"),
                new XElement("gameedition", edition),
                new XElement("gameplayoption", "Standard"),
                new XElement("settings", "Core Rulebook"),
                new XElement("gameplayoptionqualitylimit", "25"),
                new XElement("maxnuyen", "50000"),
                new XElement("maxkarma", "50"),
                new XElement("contactmultiplier", "3"),
                new XElement("prioritymetatype", priorityMetatype),
                new XElement("priorityattributes", priorityAttributes),
                new XElement("priorityspecial", prioritySpecial),
                new XElement("priorityskills", prioritySkills),
                new XElement("priorityresources", priorityResources),
                new XElement("prioritytalent", priorityTalent),
                new XElement("sumtoten", string.Equals(resolvedBuildMethod, "Sum-to-Ten", StringComparison.OrdinalIgnoreCase) ? "10" : "0"),
                new XElement("special", "2"),
                new XElement("totalspecial", "2"),
                new XElement("totalattributes", "28"),
                new XElement("contactpoints", "12"),
                new XElement("contactpointsused", "8"),
                new XElement("walk", "10"),
                new XElement("run", "15"),
                new XElement("sprint", "20"),
                new XElement("walkalt", "10"),
                new XElement("runalt", "15"),
                new XElement("sprintalt", "20"),
                new XElement("physicalcmfilled", "0"),
                new XElement("stuncmfilled", "0"),
                new XElement("totaless", "5.85"),
                new XElement("initiategrade", "0"),
                new XElement("submersiongrade", "0"),
                new XElement("magenabled", "False"),
                new XElement("resenabled", "False"),
                new XElement("depenabled", "False"),
                new XElement("adept", "False"),
                new XElement("magician", "False"),
                new XElement("technomancer", "False"),
                new XElement("ai", "False"),
                BuildAttributes(),
                BuildSkills(),
                BuildQualities(),
                BuildContacts(),
                BuildGear(),
                BuildWeapons(),
                BuildArmors(),
                BuildCyberware(),
                BuildVehicles()));

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private static string ResolveBuildMethod(string rulesetId, string buildMethod)
    {
        string normalized = string.IsNullOrWhiteSpace(buildMethod) ? string.Empty : buildMethod.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Equals(rulesetId, RulesetDefaults.Sr4, StringComparison.Ordinal) ? "BP" : "Priority";
        }

        return normalized;
    }

    private static XElement BuildAttributes()
    {
        return new XElement(
            "attributes",
            Attribute("Body", 3, 3, "Physical"),
            Attribute("Agility", 4, 4, "Physical"),
            Attribute("Reaction", 4, 4, "Physical"),
            Attribute("Strength", 2, 2, "Physical"),
            Attribute("Charisma", 3, 3, "Mental"),
            Attribute("Intuition", 4, 4, "Mental"),
            Attribute("Logic", 3, 3, "Mental"),
            Attribute("Willpower", 3, 3, "Mental"),
            Attribute("Edge", 2, 2, "Special"));
    }

    private static XElement BuildSkills()
    {
        return new XElement(
            "newskills",
            new XElement(
                "skills",
                Skill("perception", "Perception", "Active Skill", 4, false, ["Visual"]),
                Skill("sneaking", "Sneaking", "Active Skill", 3, false, []),
                Skill("etiquette", "Etiquette", "Active Skill", 3, false, []),
                Skill("firearms", "Firearms", "Active Skill", 4, false, []),
                Skill("shadow-community", "Shadow Community", "Knowledge", 2, true, [])));
    }

    private static XElement BuildQualities()
    {
        return new XElement(
            "qualities",
            new XElement(
                "quality",
                new XElement("name", "First Impression"),
                new XElement("source", "Core Rulebook p. 73"),
                new XElement("bp", "11")),
            new XElement(
                "quality",
                new XElement("name", "Toughness"),
                new XElement("source", "Core Rulebook p. 79"),
                new XElement("bp", "9")));
    }

    private static XElement BuildContacts()
    {
        return new XElement(
            "contacts",
            new XElement(
                "contact",
                new XElement("name", "Nyx"),
                new XElement("role", "Fixer"),
                new XElement("location", "Seattle"),
                new XElement("connection", "5"),
                new XElement("loyalty", "4")),
            new XElement(
                "contact",
                new XElement("name", "Dr. Mercy"),
                new XElement("role", "Street Doc"),
                new XElement("location", "Tacoma"),
                new XElement("connection", "3"),
                new XElement("loyalty", "2")));
    }

    private static XElement BuildGear()
    {
        return new XElement(
            "gears",
            Gear("Fake SIN", "General", "0", "1", "2500", "Core Rulebook p. 367"),
            Gear("Medkit Rating 6", "Medical", "6", "1", "1500", "Core Rulebook p. 449"));
    }

    private static XElement BuildWeapons()
    {
        return new XElement(
            "weapons",
            new XElement(
                "weapon",
                new XElement("name", "Colt M23"),
                new XElement("category", "Heavy Pistols"),
                new XElement("type", "Firearm"),
                new XElement("damage", "7P"),
                new XElement("ap", "-1"),
                new XElement("accuracy", "5"),
                new XElement("mode", "SA"),
                new XElement("ammo", "15(c)"),
                new XElement("cost", "750"),
                new XElement("equipped", "True")));
    }

    private static XElement BuildArmors()
    {
        return new XElement(
            "armors",
            new XElement(
                "armor",
                new XElement("name", "Armor Jacket"),
                new XElement("category", "Armor"),
                new XElement("armor", "12"),
                new XElement("rating", "12"),
                new XElement("cost", "1000"),
                new XElement("equipped", "True")));
    }

    private static XElement BuildCyberware()
    {
        return new XElement(
            "cyberwares",
            new XElement(
                "cyberware",
                new XElement("name", "Datajack"),
                new XElement("category", "Headware"),
                new XElement("ess", "0.1"),
                new XElement("capacity", "0"),
                new XElement("rating", "1"),
                new XElement("cost", "1000"),
                new XElement("grade", "Standard"),
                new XElement("location", "Head")));
    }

    private static XElement BuildVehicles()
    {
        return new XElement(
            "vehicles",
            new XElement(
                "vehicle",
                new XElement("name", "Hyundai Shin-Hyung"),
                new XElement("category", "Cars"),
                new XElement("handling", "4"),
                new XElement("speed", "4"),
                new XElement("body", "10"),
                new XElement("armor", "8"),
                new XElement("sensor", "2"),
                new XElement("seats", "4"),
                new XElement("cost", "16000")));
    }

    private static XElement Attribute(string name, int baseValue, int totalValue, string category)
    {
        return new XElement(
            "attribute",
            new XElement("name", name),
            new XElement("base", baseValue.ToString(CultureInfo.InvariantCulture)),
            new XElement("value", baseValue.ToString(CultureInfo.InvariantCulture)),
            new XElement("totalvalue", totalValue.ToString(CultureInfo.InvariantCulture)),
            new XElement("karma", "0"),
            new XElement("metatypemin", "1"),
            new XElement("metatypemax", category == "Special" ? "6" : "6"),
            new XElement("metatypeaugmax", category == "Special" ? "7" : "9"),
            new XElement("metatypecategory", category));
    }

    private static XElement Skill(
        string suid,
        string name,
        string category,
        int baseValue,
        bool isKnowledge,
        IReadOnlyList<string> specializations)
    {
        return new XElement(
            "skill",
            new XElement("guid", $"starter-{suid}"),
            new XElement("suid", suid),
            new XElement("name", name),
            new XElement("skillcategory", category),
            new XElement("isknowledge", isKnowledge ? "True" : "False"),
            new XElement("knowledge", isKnowledge ? "True" : "False"),
            new XElement("base", baseValue.ToString(CultureInfo.InvariantCulture)),
            new XElement("rating", baseValue.ToString(CultureInfo.InvariantCulture)),
            new XElement("karma", "0"),
            new XElement(
                "specs",
                specializations.Select(spec => new XElement("spec", new XElement("name", spec)))));
    }

    private static XElement Gear(
        string name,
        string category,
        string rating,
        string quantity,
        string cost,
        string source)
    {
        return new XElement(
            "gear",
            new XElement("name", name),
            new XElement("category", category),
            new XElement("rating", rating),
            new XElement("qty", quantity),
            new XElement("cost", cost),
            new XElement("source", source));
    }
}
