using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleEquipmentInstalledEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid VehicleId,
    IReadOnlyList<CharacterVehicleEquipmentInstalledState> Nodes);

public sealed record VehicleEquipmentInstalledEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterVehicleEquipmentInstalledIdentity Identity,
    string ExpectedNodeRevision,
    bool Installed);

internal static class VehicleEquipmentInstalledEditorProjector
{
    public static VehicleEquipmentInstalledEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid vehicleId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Vehicle Installed state.");
        }

        return new VehicleEquipmentInstalledEditorState(
            workspaceId,
            contentRevision,
            vehicleId,
            ProjectValue(xml, vehicleId));
    }

    internal static IReadOnlyList<CharacterVehicleEquipmentInstalledState> ProjectValue(
        string xml,
        Guid vehicleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (vehicleId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Vehicle Installed editing requires a stable Vehicle Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBoolean(root, "created", "creation/career state");
        XElement vehicle = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "vehicles").Elements("vehicle"),
            vehicleId,
            "Vehicle");

        var states = new List<CharacterVehicleEquipmentInstalledState>();
        var seenIds = new HashSet<Guid> { vehicleId };
        string vehicleName = DisplayName(vehicle, "Vehicle");
        foreach (XElement mount in ElementsFromOptionalContainer(vehicle, "weaponmounts", "weaponmount"))
        {
            AddWeaponMount(mount, vehicleId, [], vehicleName, created, states, seenIds);
        }
        foreach (XElement mod in ElementsFromOptionalContainer(vehicle, "mods", "mod"))
        {
            AddVehicleMod(mod, vehicleId, [], vehicleName, created, states, seenIds);
        }
        foreach (XElement weapon in ElementsFromOptionalContainer(vehicle, "weapons", "weapon"))
        {
            AddWeapon(weapon, vehicleId, [], vehicleName, created, states, seenIds);
        }
        return states.ToArray();
    }

    internal static XElement FindNode(
        XElement root,
        CharacterVehicleEquipmentInstalledIdentity identity)
    {
        if (!CharacterVehicleEquipmentInstalledRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Vehicle equipment hierarchy is invalid.");
        }

        XElement current = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "vehicles").Elements("vehicle"),
            identity.VehicleId,
            "Vehicle");
        CharacterVehicleEquipmentNodeKind? parentKind = null;
        foreach (CharacterVehicleEquipmentPathSegment segment in identity.Path)
        {
            IEnumerable<XElement> candidates = ResolveChildren(current, parentKind, segment.Kind);
            current = FindUniqueDirectByGuid(candidates, segment.Id, segment.Kind.ToString());
            parentKind = segment.Kind;
        }
        return current;
    }

    private static IEnumerable<XElement> ResolveChildren(
        XElement parent,
        CharacterVehicleEquipmentNodeKind? parentKind,
        CharacterVehicleEquipmentNodeKind childKind)
    {
        if (parentKind is null)
        {
            return childKind switch
            {
                CharacterVehicleEquipmentNodeKind.WeaponMount =>
                    ElementsFromOptionalContainer(parent, "weaponmounts", "weaponmount"),
                CharacterVehicleEquipmentNodeKind.VehicleMod =>
                    ElementsFromOptionalContainer(parent, "mods", "mod"),
                CharacterVehicleEquipmentNodeKind.Weapon =>
                    ElementsFromOptionalContainer(parent, "weapons", "weapon"),
                _ => Array.Empty<XElement>()
            };
        }
        if (parentKind == CharacterVehicleEquipmentNodeKind.WeaponMount)
        {
            return childKind switch
            {
                CharacterVehicleEquipmentNodeKind.VehicleMod =>
                    ElementsFromOptionalContainer(parent, "mods", "mod"),
                CharacterVehicleEquipmentNodeKind.Weapon =>
                    ElementsFromOptionalContainer(parent, "weapons", "weapon"),
                _ => Array.Empty<XElement>()
            };
        }
        if (parentKind == CharacterVehicleEquipmentNodeKind.VehicleMod
            && childKind == CharacterVehicleEquipmentNodeKind.Weapon)
        {
            return ElementsFromOptionalContainer(parent, "weapons", "weapon");
        }
        if (parentKind == CharacterVehicleEquipmentNodeKind.Weapon)
        {
            return childKind switch
            {
                CharacterVehicleEquipmentNodeKind.Weapon => UnderbarrelWeapons(parent),
                CharacterVehicleEquipmentNodeKind.WeaponAccessory =>
                    ElementsFromOptionalContainer(parent, "accessories", "accessory"),
                _ => Array.Empty<XElement>()
            };
        }
        return Array.Empty<XElement>();
    }

    private static void AddWeaponMount(
        XElement mount,
        Guid vehicleId,
        IReadOnlyList<CharacterVehicleEquipmentPathSegment> parentPath,
        string parentDisplayPath,
        bool created,
        ICollection<CharacterVehicleEquipmentInstalledState> states,
        ISet<Guid> seenIds)
    {
        CharacterVehicleEquipmentPathSegment segment = Segment(
            mount, CharacterVehicleEquipmentNodeKind.WeaponMount, seenIds);
        CharacterVehicleEquipmentPathSegment[] path = [.. parentPath, segment];
        string displayPath = JoinDisplayPath(parentDisplayPath, DisplayName(mount, "Weapon Mount"));
        AddState(
            vehicleId,
            path,
            displayPath,
            created,
            mount,
            new CharacterVehicleEquipmentInstalledProvenance(
                ReadRequiredBoolean(mount, "included", "included-in-Vehicle state"),
                string.Empty,
                null,
                true),
            states);

        foreach (XElement mod in ElementsFromOptionalContainer(mount, "mods", "mod"))
        {
            AddVehicleMod(mod, vehicleId, path, displayPath, created, states, seenIds);
        }
        foreach (XElement weapon in ElementsFromOptionalContainer(mount, "weapons", "weapon"))
        {
            AddWeapon(weapon, vehicleId, path, displayPath, created, states, seenIds);
        }
    }

    private static void AddVehicleMod(
        XElement mod,
        Guid vehicleId,
        IReadOnlyList<CharacterVehicleEquipmentPathSegment> parentPath,
        string parentDisplayPath,
        bool created,
        ICollection<CharacterVehicleEquipmentInstalledState> states,
        ISet<Guid> seenIds)
    {
        CharacterVehicleEquipmentPathSegment segment = Segment(
            mod, CharacterVehicleEquipmentNodeKind.VehicleMod, seenIds);
        CharacterVehicleEquipmentPathSegment[] path = [.. parentPath, segment];
        string displayPath = JoinDisplayPath(parentDisplayPath, DisplayName(mod, "Vehicle Mod"));
        bool wirelessOn = ReadRequiredBoolean(mod, "wirelesson", "wireless state");
        bool sensorSideEffect = HasDirectSensor(mod, "bonus")
            || wirelessOn && HasDirectSensor(mod, "wirelessbonus");
        AddState(
            vehicleId,
            path,
            displayPath,
            created,
            mod,
            new CharacterVehicleEquipmentInstalledProvenance(
                ReadRequiredBoolean(mod, "included", "included-in-Vehicle state"),
                string.Empty,
                null,
                !sensorSideEffect),
            states);

        foreach (XElement weapon in ElementsFromOptionalContainer(mod, "weapons", "weapon"))
        {
            AddWeapon(weapon, vehicleId, path, displayPath, created, states, seenIds);
        }
    }

    private static void AddWeapon(
        XElement weapon,
        Guid vehicleId,
        IReadOnlyList<CharacterVehicleEquipmentPathSegment> parentPath,
        string parentDisplayPath,
        bool created,
        ICollection<CharacterVehicleEquipmentInstalledState> states,
        ISet<Guid> seenIds)
    {
        CharacterVehicleEquipmentPathSegment segment = Segment(
            weapon, CharacterVehicleEquipmentNodeKind.Weapon, seenIds);
        CharacterVehicleEquipmentPathSegment[] path = [.. parentPath, segment];
        string displayPath = JoinDisplayPath(parentDisplayPath, DisplayName(weapon, "Weapon"));
        Guid? parentWeaponId = parentPath.Count > 0
            && parentPath[^1].Kind == CharacterVehicleEquipmentNodeKind.Weapon
                ? parentPath[^1].Id
                : null;
        AddState(
            vehicleId,
            path,
            displayPath,
            created,
            weapon,
            new CharacterVehicleEquipmentInstalledProvenance(
                null,
                ReadOptionalSingleText(weapon, "parentid"),
                parentWeaponId,
                true),
            states);

        foreach (XElement accessory in ElementsFromOptionalContainer(weapon, "accessories", "accessory"))
        {
            AddWeaponAccessory(accessory, vehicleId, path, displayPath, created, states, seenIds);
        }
        foreach (XElement underbarrel in UnderbarrelWeapons(weapon))
        {
            AddWeapon(underbarrel, vehicleId, path, displayPath, created, states, seenIds);
        }
    }

    private static void AddWeaponAccessory(
        XElement accessory,
        Guid vehicleId,
        IReadOnlyList<CharacterVehicleEquipmentPathSegment> parentPath,
        string parentDisplayPath,
        bool created,
        ICollection<CharacterVehicleEquipmentInstalledState> states,
        ISet<Guid> seenIds)
    {
        CharacterVehicleEquipmentPathSegment segment = Segment(
            accessory, CharacterVehicleEquipmentNodeKind.WeaponAccessory, seenIds);
        CharacterVehicleEquipmentPathSegment[] path = [.. parentPath, segment];
        AddState(
            vehicleId,
            path,
            JoinDisplayPath(parentDisplayPath, DisplayName(accessory, "Weapon Accessory")),
            created,
            accessory,
            new CharacterVehicleEquipmentInstalledProvenance(null, string.Empty, null, true),
            states);
    }

    private static void AddState(
        Guid vehicleId,
        IReadOnlyList<CharacterVehicleEquipmentPathSegment> path,
        string displayPath,
        bool created,
        XElement element,
        CharacterVehicleEquipmentInstalledProvenance provenance,
        ICollection<CharacterVehicleEquipmentInstalledState> states)
    {
        var identity = new CharacterVehicleEquipmentInstalledIdentity(vehicleId, path);
        if (!CharacterVehicleEquipmentInstalledRules.TryCreateState(
                identity,
                created,
                displayPath,
                ReadRequiredBoolean(element, "equipped", "installed/equipped state"),
                provenance,
                out CharacterVehicleEquipmentInstalledState state))
        {
            throw new InvalidOperationException(
                "Vehicle Installed editing requires exact saved identity, eligibility, and state.");
        }
        states.Add(state);
    }

    private static CharacterVehicleEquipmentPathSegment Segment(
        XElement element,
        CharacterVehicleEquipmentNodeKind kind,
        ISet<Guid> seenIds)
    {
        Guid id = ReadGuid(element, kind.ToString());
        if (!seenIds.Add(id))
        {
            throw new InvalidOperationException(
                "Vehicle Installed editing requires globally unique stable Guids in the selected Vehicle tree.");
        }
        return new CharacterVehicleEquipmentPathSegment(kind, id);
    }

    private static bool HasDirectSensor(XElement parent, string name)
    {
        XElement[] nodes = parent.Elements(name).Take(2).ToArray();
        if (nodes.Length > 1)
        {
            throw new InvalidOperationException(
                $"Vehicle Mod has duplicate <{name}> provenance.");
        }
        return nodes.Length == 1 && nodes[0].Elements("sensor").Any();
    }

    private static IEnumerable<XElement> UnderbarrelWeapons(XElement weapon)
    {
        var result = new List<XElement>();
        foreach (XElement wrapper in weapon.Elements("underbarrel"))
        {
            XElement[] children = wrapper.Elements("weapon").Take(2).ToArray();
            if (children.Length != 1 || wrapper.Elements().Count() != 1)
            {
                throw new InvalidOperationException(
                    "Each saved <underbarrel> must contain exactly one Weapon.");
            }
            result.Add(children[0]);
        }
        return result;
    }

    private static IEnumerable<XElement> ElementsFromOptionalContainer(
        XElement parent,
        string containerName,
        string elementName)
        => ReadOptionalContainer(parent, containerName)?.Elements(elementName)
            ?? Array.Empty<XElement>();

    private static bool ReadRequiredBoolean(XElement parent, string name, string kind)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"Vehicle Installed editing requires one exact saved {kind} Boolean.");
        }
        return value;
    }

    private static string ReadOptionalSingleText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value.Trim(),
            _ => throw new InvalidOperationException(
                $"The selected Vehicle equipment node has duplicate <{name}> values.")
        };
    }

    private static Guid ReadGuid(XElement element, string kind)
    {
        XElement[] values = element.Elements("guid").Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid id)
            || id == Guid.Empty)
        {
            throw new InvalidOperationException($"{kind} requires one stable Guid.");
        }
        return id;
    }

    private static XElement FindUniqueDirectByGuid(
        IEnumerable<XElement> candidates,
        Guid id,
        string kind)
    {
        XElement[] matches = candidates
            .Where(candidate => Guid.TryParseExact(
                    candidate.Elements("guid").SingleOrDefault()?.Value.Trim(),
                    "D",
                    out Guid candidateId)
                && candidateId == id)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"{kind} identity is missing or ambiguous in the saved Vehicle hierarchy.");
    }

    private static XElement ReadRequiredContainer(XElement parent, string name)
        => ReadOptionalContainer(parent, name)
            ?? throw new InvalidOperationException($"The saved hierarchy is missing <{name}>.");

    private static XElement? ReadOptionalContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidOperationException(
                $"The saved hierarchy has duplicate <{name}> collections.")
        };
    }

    private static string DisplayName(XElement element, string fallback)
    {
        XElement[] names = element.Elements("name").Take(2).ToArray();
        return names.Length switch
        {
            0 => fallback,
            1 when names[0].Value.Length != 0 => names[0].Value,
            1 => fallback,
            _ => throw new InvalidOperationException(
                $"A {fallback} node has duplicate <name> values.")
        };
    }

    private static string JoinDisplayPath(string parent, string child)
        => $"{parent} > {child}";
}
