using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record VehicleWeaponFiringModeEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid VehicleId,
    string VehicleDisplayName,
    IReadOnlyList<CharacterVehicleWeaponFiringModeState> Weapons);

public sealed record VehicleWeaponFiringModeEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterVehicleWeaponFiringModeIdentity Identity,
    string ExpectedNodeRevision,
    CharacterVehicleWeaponFiringMode FiringMode);

internal static class VehicleWeaponFiringModeEditorProjector
{
    public static VehicleWeaponFiringModeEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid vehicleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Vehicle Weapon firing modes.");
        if (vehicleId == Guid.Empty)
            throw new InvalidOperationException(
                "Vehicle Weapon firing-mode editing requires a stable Vehicle Guid.");

        XDocument document = Parse(xml);
        XElement root = document.Root!;
        bool created = ReadBoolean(root, "created", "Character");
        XElement vehicle = FindVehicleRoot(root, vehicleId);
        XElement weapons = ReadDirectWeaponsContainer(vehicle);
        var states = new List<CharacterVehicleWeaponFiringModeState>();
        foreach (XElement weapon in weapons.Elements("weapon"))
        {
            Guid weaponId = ReadGuid(weapon, "Weapon");
            EnsureGloballyUniqueDirectWeapon(vehicle, weapon, weaponId);
            string rangeType = ReadSingle(weapon, "type", "Weapon");
            string ammo = ReadSingle(weapon, "ammo", "Weapon");
            if (!CharacterVehicleWeaponFiringModeRules.IsLegacyEditorVisible(rangeType, ammo))
                continue;

            if (!CharacterVehicleWeaponFiringModeRules.TryCreateState(
                    new CharacterVehicleWeaponFiringModeIdentity(vehicleId, weaponId),
                    created,
                    ReadDisplayName(weapon, "Weapon"),
                    ReadSingle(weapon, "firingmode", "Weapon"),
                    rangeType,
                    ammo,
                    out CharacterVehicleWeaponFiringModeState state))
            {
                throw new InvalidOperationException(
                    "A direct Vehicle Weapon has an unsupported saved firing mode or incomplete exact state.");
            }
            states.Add(state);
        }

        return new VehicleWeaponFiringModeEditorState(
            workspaceId,
            contentRevision,
            vehicleId,
            ReadDisplayName(vehicle, "Vehicle"),
            states);
    }

    internal static CharacterVehicleWeaponFiringModeState ProjectValue(
        string xml,
        CharacterVehicleWeaponFiringModeIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (!CharacterVehicleWeaponFiringModeRules.IsValidIdentity(identity))
            throw new InvalidOperationException(
                "Vehicle Weapon firing-mode editing requires exact Vehicle and Weapon Guids.");

        XDocument document = Parse(xml);
        XElement root = document.Root!;
        XElement weapon = FindWeaponRoot(root, identity);
        if (!CharacterVehicleWeaponFiringModeRules.TryCreateState(
                identity,
                ReadBoolean(root, "created", "Character"),
                ReadDisplayName(weapon, "Weapon"),
                ReadSingle(weapon, "firingmode", "Weapon"),
                ReadSingle(weapon, "type", "Weapon"),
                ReadSingle(weapon, "ammo", "Weapon"),
                out CharacterVehicleWeaponFiringModeState state))
        {
            throw new InvalidOperationException(
                "The selected direct Vehicle Weapon is hidden or has unsupported saved firing-mode state.");
        }
        return state;
    }

    internal static XElement FindWeaponRoot(
        XElement root,
        CharacterVehicleWeaponFiringModeIdentity identity)
    {
        if (!CharacterVehicleWeaponFiringModeRules.IsValidIdentity(identity))
            throw new InvalidOperationException(
                "Vehicle Weapon firing-mode editing requires exact Vehicle and Weapon Guids.");
        XElement vehicle = FindVehicleRoot(root, identity.VehicleId);
        XElement weapons = ReadDirectWeaponsContainer(vehicle);
        XElement[] directMatches = weapons.Elements("weapon")
            .Where(candidate => ReadGuid(candidate, "Weapon") == identity.WeaponId)
            .Take(2)
            .ToArray();
        if (directMatches.Length != 1)
            throw new InvalidOperationException(
                "Weapon Guid identity is missing, ambiguous, or belongs to a Vehicle descendant.");
        EnsureGloballyUniqueDirectWeapon(vehicle, directMatches[0], identity.WeaponId);
        return directMatches[0];
    }

    private static XDocument Parse(string xml)
    {
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return document.Root is { Name.LocalName: "character" }
            ? document
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
    }

    private static XElement FindVehicleRoot(XElement root, Guid vehicleId)
    {
        XElement[] containers = root.Elements("vehicles").Take(2).ToArray();
        if (containers.Length != 1)
            throw new InvalidOperationException("Character requires exactly one <vehicles> container.");
        XElement[] matches = containers[0].Elements("vehicle")
            .Where(candidate => ReadGuid(candidate, "Vehicle") == vehicleId)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("Vehicle Guid identity is missing or ambiguous.");
    }

    private static XElement ReadDirectWeaponsContainer(XElement vehicle)
    {
        XElement[] matches = vehicle.Elements("weapons").Take(2).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "Vehicle requires exactly one direct <weapons> container for firing-mode editing.");
    }

    private static void EnsureGloballyUniqueDirectWeapon(
        XElement vehicle,
        XElement directWeapon,
        Guid weaponId)
    {
        XElement[] allMatches = vehicle.Descendants("weapon")
            .Where(candidate => ReadGuid(candidate, "Weapon") == weaponId)
            .Take(2)
            .ToArray();
        if (allMatches.Length != 1 || !ReferenceEquals(allMatches[0], directWeapon))
            throw new InvalidOperationException(
                "Weapon Guid identity is duplicated or resolves through a Vehicle descendant.");
    }

    private static Guid ReadGuid(XElement element, string label)
        => Guid.TryParse(ReadSingle(element, "guid", label), out Guid value) && value != Guid.Empty
            ? value
            : throw new InvalidOperationException($"{label} requires one non-empty saved Guid.");

    private static string ReadDisplayName(XElement element, string label)
    {
        XElement[] customNames = element.Elements("customname").Take(2).ToArray();
        if (customNames.Length > 1)
            throw new InvalidOperationException($"{label} requires at most one <customname> element.");
        if (customNames.Length == 1 && !string.IsNullOrWhiteSpace(customNames[0].Value))
            return customNames[0].Value;
        string name = ReadSingle(element, "name", label);
        return string.IsNullOrWhiteSpace(name) ? label : name;
    }

    private static string ReadSingle(XElement parent, string name, string label)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0].Value
            : throw new InvalidOperationException($"{label} requires exactly one <{name}> element.");
    }

    private static bool ReadBoolean(XElement parent, string name, string label)
        => bool.TryParse(ReadSingle(parent, name, label), out bool value)
            ? value
            : throw new InvalidOperationException($"{label} <{name}> must be a saved Boolean.");
}
