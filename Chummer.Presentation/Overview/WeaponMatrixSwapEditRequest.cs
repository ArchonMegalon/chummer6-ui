using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponMatrixSwapEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterWeaponMatrixSwapState Weapon);

public sealed record WeaponMatrixSwapEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterWeaponMatrixSwapIdentity Identity,
    string ExpectedNodeRevision,
    CharacterWeaponMatrixStat ChangedAttribute,
    CharacterWeaponMatrixStat TargetAttribute);

internal static class WeaponMatrixSwapEditorProjector
{
    public static WeaponMatrixSwapEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid weaponId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before swapping Weapon Matrix values.");
        }

        return new(workspaceId, contentRevision, ProjectValue(xml, weaponId));
    }

    internal static CharacterWeaponMatrixSwapState ProjectValue(string xml, Guid weaponId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (weaponId == Guid.Empty)
        {
            throw new InvalidOperationException("Weapon Matrix swapping requires a stable Weapon Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement weapon = FindWeaponRoot(root, weaponId);
        if (!CharacterWeaponMatrixSwapRules.TryCreateState(
                new CharacterWeaponMatrixSwapIdentity(weaponId),
                ReadBoolean(root, "created"),
                ReadDisplayName(weapon),
                ReadSingle(weapon, "attack"),
                ReadSingle(weapon, "sleaze"),
                ReadSingle(weapon, "dataprocessing"),
                ReadSingle(weapon, "firewall"),
                ReadSingle(weapon, "attributearray"),
                ReadBoolean(weapon, "canswapattributes"),
                out CharacterWeaponMatrixSwapState state))
        {
            throw new InvalidOperationException(
                "The selected Weapon is not a Career-only, direct, enabled CanSwapAttributes Matrix target.");
        }

        return state;
    }

    internal static XElement FindWeaponRoot(XElement root, Guid weaponId)
    {
        XElement[] containers = root.Elements("weapons").Take(2).ToArray();
        if (containers.Length != 1)
        {
            throw new InvalidOperationException("Weapon Matrix swapping requires exactly one <weapons> container.");
        }

        XElement[] directMatches = containers[0].Elements("weapon")
            .Where(candidate => TryReadGuid(candidate, out Guid id) && id == weaponId)
            .Take(2)
            .ToArray();
        int globalMatches = root.Descendants("weapon")
            .Count(candidate => TryReadGuid(candidate, out Guid id) && id == weaponId);
        return directMatches.Length == 1 && globalMatches == 1
            ? directMatches[0]
            : throw new InvalidOperationException(
                "Weapon Guid identity is missing, ambiguous, or belongs to a descendant or other owner.");
    }

    private static bool TryReadGuid(XElement weapon, out Guid id)
    {
        id = Guid.Empty;
        XElement[] values = weapon.Elements("guid").Take(2).ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0].Value, "D", out id)
            && id != Guid.Empty;
    }

    private static string ReadDisplayName(XElement weapon)
    {
        XElement[] customNames = weapon.Elements("customname").Take(2).ToArray();
        if (customNames.Length > 1)
        {
            throw new InvalidOperationException("Weapon requires at most one <customname> element.");
        }
        if (customNames.Length == 1 && !string.IsNullOrWhiteSpace(customNames[0].Value))
        {
            return customNames[0].Value;
        }

        string name = ReadSingle(weapon, "name");
        return string.IsNullOrWhiteSpace(name) ? "Weapon" : name;
    }

    private static string ReadSingle(XElement parent, string name)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0].Value
            : throw new InvalidOperationException($"Weapon requires exactly one <{name}> element.");
    }

    private static bool ReadBoolean(XElement parent, string name)
        => bool.TryParse(ReadSingle(parent, name), out bool value)
            ? value
            : throw new InvalidOperationException($"Weapon <{name}> must be a saved Boolean.");
}
