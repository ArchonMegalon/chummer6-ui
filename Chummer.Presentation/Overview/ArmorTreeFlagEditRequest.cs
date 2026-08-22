using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ArmorTreeFlagEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootArmorId,
    IReadOnlyList<CharacterArmorTreeFlagState> Nodes);

public sealed record ArmorTreeFlagEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterArmorTreeNodeIdentity Identity,
    string ExpectedNodeRevision,
    bool Stolen,
    bool DiscountedCost);

internal static class ArmorTreeFlagEditorProjector
{
    public static ArmorTreeFlagEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid armorId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing armor-tree flags.");
        }

        return new ArmorTreeFlagEditorState(
            workspaceId,
            contentRevision,
            armorId,
            ProjectValue(xml, armorId));
    }

    internal static IReadOnlyList<CharacterArmorTreeFlagState> ProjectValue(
        string xml,
        Guid armorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (armorId == Guid.Empty)
        {
            throw new InvalidOperationException("Armor-tree flag editing requires a stable root Armor Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "The selected armor-tree flags are exposed by CharacterCreate only.");
        }

        XElement armor = FindNode(root, new CharacterArmorTreeNodeIdentity(
            CharacterArmorTreeNodeKind.Armor,
            armorId,
            null,
            Array.Empty<Guid>()));
        var states = new List<CharacterArmorTreeFlagState>();
        var seenIds = new HashSet<Guid>();
        string armorName = DisplayName(armor, "Unnamed Armor");
        AddState(
            armor,
            new CharacterArmorTreeNodeIdentity(
                CharacterArmorTreeNodeKind.Armor,
                armorId,
                null,
                Array.Empty<Guid>()),
            armorName,
            states,
            seenIds);

        XElement? mods = ReadOptionalContainer(armor, "armormods");
        if (mods is not null)
        {
            foreach (XElement mod in mods.Elements("armormod"))
            {
                Guid modId = ReadGuid(mod, "ArmorMod");
                string modPath = $"{armorName} > {DisplayName(mod, "Unnamed ArmorMod")}";
                AddState(
                    mod,
                    new CharacterArmorTreeNodeIdentity(
                        CharacterArmorTreeNodeKind.ArmorMod,
                        armorId,
                        modId,
                        Array.Empty<Guid>()),
                    modPath,
                    states,
                    seenIds);
                AddGearStates(
                    mod,
                    armorId,
                    modId,
                    modPath,
                    Array.Empty<Guid>(),
                    states,
                    seenIds);
            }
        }

        AddGearStates(
            armor,
            armorId,
            armorModId: null,
            armorName,
            Array.Empty<Guid>(),
            states,
            seenIds);
        return states.ToArray();
    }

    internal static XElement FindNode(XElement root, CharacterArmorTreeNodeIdentity identity)
    {
        if (!CharacterArmorTreeFlagRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected armor-tree hierarchy is invalid.");
        }

        XElement armors = ReadRequiredContainer(root, "armors");
        XElement current = FindUniqueDirectByGuid(
            armors,
            "armor",
            identity.ArmorId,
            "Armor");
        if (identity.Kind == CharacterArmorTreeNodeKind.Armor)
        {
            return current;
        }

        if (identity.ArmorModId is Guid modId)
        {
            XElement mods = ReadRequiredContainer(current, "armormods");
            current = FindUniqueDirectByGuid(mods, "armormod", modId, "ArmorMod");
            if (identity.Kind == CharacterArmorTreeNodeKind.ArmorMod)
            {
                return current;
            }
        }

        XElement gears = ReadRequiredContainer(current, "gears");
        current = FindUniqueDirectByGuid(gears, "gear", identity.GearPath[0], "Gear");
        for (int index = 1; index < identity.GearPath.Count; index++)
        {
            XElement children = ReadRequiredContainer(current, "children");
            current = FindUniqueDirectByGuid(
                children,
                "gear",
                identity.GearPath[index],
                "nested Gear");
        }
        return current;
    }

    private static void AddGearStates(
        XElement parent,
        Guid armorId,
        Guid? armorModId,
        string parentPath,
        IReadOnlyList<Guid> parentGearPath,
        ICollection<CharacterArmorTreeFlagState> states,
        ISet<Guid> seenIds)
    {
        string containerName = parentGearPath.Count == 0 ? "gears" : "children";
        XElement? container = ReadOptionalContainer(parent, containerName);
        if (container is null)
        {
            return;
        }

        foreach (XElement gear in container.Elements("gear"))
        {
            Guid gearId = ReadGuid(gear, "Gear");
            Guid[] path = [.. parentGearPath, gearId];
            string displayPath = $"{parentPath} > {DisplayName(gear, "Unnamed Gear")}";
            AddState(
                gear,
                new CharacterArmorTreeNodeIdentity(
                    CharacterArmorTreeNodeKind.Gear,
                    armorId,
                    armorModId,
                    path),
                displayPath,
                states,
                seenIds);
            AddGearStates(
                gear,
                armorId,
                armorModId,
                displayPath,
                path,
                states,
                seenIds);
        }
    }

    private static void AddState(
        XElement element,
        CharacterArmorTreeNodeIdentity identity,
        string displayPath,
        ICollection<CharacterArmorTreeFlagState> states,
        ISet<Guid> seenIds)
    {
        Guid nodeId = identity.Kind switch
        {
            CharacterArmorTreeNodeKind.Armor => identity.ArmorId,
            CharacterArmorTreeNodeKind.ArmorMod => identity.ArmorModId!.Value,
            CharacterArmorTreeNodeKind.Gear => identity.GearPath[^1],
            _ => Guid.Empty
        };
        if (!seenIds.Add(nodeId)
            || !CharacterArmorTreeFlagRules.TryCreateState(
                identity,
                created: false,
                displayPath: displayPath,
                stolen: ReadOptionalBoolean(element, "stolen"),
                discountedCost: ReadOptionalBoolean(element, "discountedcost"),
                out CharacterArmorTreeFlagState state))
        {
            throw new InvalidOperationException(
                "Armor-tree nodes require unique stable hierarchical identity and exact saved flags.");
        }
        states.Add(state);
    }

    private static bool ReadRequiredCreated(XElement root)
    {
        XElement[] values = root.Elements("created").Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool created))
        {
            throw new InvalidOperationException(
                "Armor-tree flag editing requires an exact saved creation/career state.");
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
            _ => throw new InvalidOperationException(
                $"The selected node has an invalid <{name}> value.")
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
            _ => throw new InvalidOperationException("An armor-tree node has duplicate <name> values.")
        };
    }
}
