using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record WeaponStolenEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootWeaponId,
    IReadOnlyList<CharacterWeaponStolenState> Nodes);

public sealed record WeaponStolenEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterWeaponStolenIdentity Identity,
    string ExpectedNodeRevision,
    bool Stolen);

internal static class WeaponStolenEditorProjector
{
    public static WeaponStolenEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootWeaponId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Weapon Stolen.");
        }
        return new WeaponStolenEditorState(
            workspaceId,
            contentRevision,
            rootWeaponId,
            ProjectValue(xml, rootWeaponId));
    }

    internal static IReadOnlyList<CharacterWeaponStolenState> ProjectValue(
        string xml,
        Guid rootWeaponId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (rootWeaponId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Weapon Stolen editing requires a stable root Weapon Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "Weapon Stolen is exposed by CharacterCreate only.");
        }
        if (!HasActiveStolenNuyenImprovement(root))
        {
            throw new InvalidOperationException(
                "Weapon Stolen requires an active creation-mode Nuyen/Stolen improvement.");
        }

        XElement weapon = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "weapons"),
            "weapon",
            rootWeaponId,
            "root Weapon");
        var states = new List<CharacterWeaponStolenState>();
        var seenIds = new HashSet<Guid>();
        AddWeaponTree(
            weapon,
            Array.Empty<CharacterWeaponStolenHop>(),
            parentDisplayPath: null,
            states,
            seenIds);
        return states.ToArray();
    }

    internal static XElement FindNode(
        XElement root,
        CharacterWeaponStolenIdentity identity)
    {
        if (!CharacterWeaponStolenRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Weapon-tree hierarchy is invalid.");
        }

        CharacterWeaponStolenHop first = identity.Path[0];
        XElement current = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "weapons"),
            "weapon",
            first.Id,
            "root Weapon");
        for (int index = 1; index < identity.Path.Count; index++)
        {
            CharacterWeaponStolenHop hop = identity.Path[index];
            current = (current.Name.LocalName, hop.Kind) switch
            {
                ("weapon", CharacterWeaponStolenNodeKind.Weapon) =>
                    FindUniqueUnderbarrelByGuid(current, hop.Id),
                ("weapon", CharacterWeaponStolenNodeKind.WeaponAccessory) =>
                    FindUniqueDirectByGuid(
                        ReadRequiredContainer(current, "accessories"),
                        "accessory",
                        hop.Id,
                        "WeaponAccessory"),
                ("accessory", CharacterWeaponStolenNodeKind.Gear) =>
                    FindUniqueDirectByGuid(
                        ReadRequiredContainer(current, "gears"),
                        "gear",
                        hop.Id,
                        "accessory Gear"),
                ("gear", CharacterWeaponStolenNodeKind.Gear) =>
                    FindUniqueDirectByGuid(
                        ReadRequiredContainer(current, "children"),
                        "gear",
                        hop.Id,
                        "nested Gear"),
                _ => throw new InvalidOperationException(
                    "The selected Weapon-tree path has an invalid typed transition.")
            };
        }
        return current;
    }

    private static void AddWeaponTree(
        XElement weapon,
        IReadOnlyList<CharacterWeaponStolenHop> parentPath,
        string? parentDisplayPath,
        ICollection<CharacterWeaponStolenState> states,
        ISet<Guid> seenIds)
    {
        Guid weaponId = ReadGuid(weapon, "Weapon");
        CharacterWeaponStolenHop[] path = [
            .. parentPath,
            new(CharacterWeaponStolenNodeKind.Weapon, weaponId)
        ];
        string name = DisplayName(weapon, "Unnamed Weapon");
        string displayPath = parentDisplayPath is null ? name : $"{parentDisplayPath} > {name}";
        AddState(weapon, new CharacterWeaponStolenIdentity(path), displayPath, states, seenIds);

        XElement? accessories = ReadOptionalContainer(weapon, "accessories");
        if (accessories is not null)
        {
            foreach (XElement accessory in accessories.Elements("accessory"))
            {
                AddAccessoryTree(accessory, path, displayPath, states, seenIds);
            }
        }

        foreach (XElement wrapper in weapon.Elements("underbarrel"))
        {
            XElement underbarrel = ReadSingleWrappedElement(wrapper, "weapon", "underbarrel Weapon");
            AddWeaponTree(underbarrel, path, displayPath, states, seenIds);
        }
    }

    private static void AddAccessoryTree(
        XElement accessory,
        IReadOnlyList<CharacterWeaponStolenHop> parentPath,
        string parentDisplayPath,
        ICollection<CharacterWeaponStolenState> states,
        ISet<Guid> seenIds)
    {
        Guid accessoryId = ReadGuid(accessory, "WeaponAccessory");
        CharacterWeaponStolenHop[] path = [
            .. parentPath,
            new(CharacterWeaponStolenNodeKind.WeaponAccessory, accessoryId)
        ];
        string displayPath = $"{parentDisplayPath} > {DisplayName(accessory, "Unnamed Accessory")}";
        AddState(accessory, new CharacterWeaponStolenIdentity(path), displayPath, states, seenIds);
        AddGearTree(accessory, path, displayPath, rootContainer: "gears", states, seenIds);
    }

    private static void AddGearTree(
        XElement parent,
        IReadOnlyList<CharacterWeaponStolenHop> parentPath,
        string parentDisplayPath,
        string rootContainer,
        ICollection<CharacterWeaponStolenState> states,
        ISet<Guid> seenIds)
    {
        XElement? container = ReadOptionalContainer(parent, rootContainer);
        if (container is null)
        {
            return;
        }
        foreach (XElement gear in container.Elements("gear"))
        {
            Guid gearId = ReadGuid(gear, "Gear");
            CharacterWeaponStolenHop[] path = [
                .. parentPath,
                new(CharacterWeaponStolenNodeKind.Gear, gearId)
            ];
            string displayPath = $"{parentDisplayPath} > {DisplayName(gear, "Unnamed Gear")}";
            AddState(gear, new CharacterWeaponStolenIdentity(path), displayPath, states, seenIds);
            AddGearTree(gear, path, displayPath, "children", states, seenIds);
        }
    }

    private static void AddState(
        XElement element,
        CharacterWeaponStolenIdentity identity,
        string displayPath,
        ICollection<CharacterWeaponStolenState> states,
        ISet<Guid> seenIds)
    {
        Guid nodeId = identity.Path[^1].Id;
        if (!seenIds.Add(nodeId)
            || !CharacterWeaponStolenRules.TryCreateState(
                identity,
                created: false,
                hasStolenNuyenImprovement: true,
                displayPath,
                ReadOptionalBoolean(element, "stolen"),
                out CharacterWeaponStolenState state))
        {
            throw new InvalidOperationException(
                "Weapon-tree nodes require unique stable typed identity and exact saved state.");
        }
        states.Add(state);
    }

    private static bool HasActiveStolenNuyenImprovement(XElement root)
    {
        XElement? improvements = ReadOptionalContainer(root, "improvements");
        if (improvements is null)
        {
            return false;
        }
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            if (!string.Equals(ReadOptionalText(improvement, "improvementttype"), "Nuyen", StringComparison.Ordinal)
                || !string.Equals(ReadOptionalText(improvement, "improvedname"), "Stolen", StringComparison.Ordinal))
            {
                continue;
            }
            int enabled = ReadOptionalIntegerBoolean(improvement, "enabled", 1);
            int addToRating = ReadOptionalIntegerBoolean(improvement, "addtorating", 0);
            string condition = ReadOptionalText(improvement, "condition");
            if (enabled > 0
                && addToRating <= 0
                && (condition.Length == 0 || string.Equals(condition, "create", StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    private static int ReadOptionalIntegerBoolean(XElement parent, string name, int defaultValue)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return defaultValue;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException($"A saved Improvement has duplicate <{name}> values.");
        }
        string value = values[0].Value.Trim();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        if (bool.TryParse(value, out bool boolean))
        {
            return boolean ? 1 : 0;
        }
        throw new InvalidOperationException($"A saved Improvement has an invalid <{name}> value.");
    }

    private static string ReadOptionalText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value,
            _ => throw new InvalidOperationException(
                $"A saved Improvement has duplicate <{name}> values.")
        };
    }

    private static bool ReadRequiredCreated(XElement root)
    {
        XElement[] values = root.Elements("created").Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool created))
        {
            throw new InvalidOperationException(
                "Weapon Stolen editing requires an exact saved creation/career state.");
        }
        return created;
    }

    private static bool ReadOptionalBoolean(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return false;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException($"The selected node has duplicate <{name}> values.");
        }
        string value = values[0].Value.Trim();
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }
        return value switch
        {
            "1" => true,
            "0" => false,
            _ => throw new InvalidOperationException($"The selected node has an invalid <{name}> value.")
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

    private static XElement FindUniqueUnderbarrelByGuid(XElement weapon, Guid id)
    {
        XElement[] matches = weapon.Elements("underbarrel")
            .Select(wrapper => ReadSingleWrappedElement(wrapper, "weapon", "underbarrel Weapon"))
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
                "underbarrel Weapon identity is missing or ambiguous in the saved hierarchy.");
    }

    private static XElement FindUniqueDirectByGuid(
        XElement container,
        string elementName,
        Guid id,
        string kind)
    {
        XElement[] matches = container.Elements(elementName)
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
                $"{kind} identity is missing or ambiguous in the saved hierarchy.");
    }

    private static XElement ReadSingleWrappedElement(XElement wrapper, string name, string kind)
    {
        XElement[] values = wrapper.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0]
            : throw new InvalidOperationException($"A saved {kind} wrapper must contain exactly one <{name}>.");
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
            _ => throw new InvalidOperationException("A Weapon-tree node has duplicate <name> values.")
        };
    }
}
