using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearEquipmentEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearEquipmentState> Nodes);

public sealed record GearEquipmentEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearEquipmentIdentity Identity,
    string ExpectedNodeRevision,
    bool Equipped);

internal static class GearEquipmentEditorProjector
{
    public static GearEquipmentEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Gear Equipped.");
        }

        return new GearEquipmentEditorState(
            workspaceId,
            contentRevision,
            rootGearId,
            ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearEquipmentState> ProjectValue(
        string xml,
        Guid rootGearId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (rootGearId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Gear Equipped editing requires a stable root Gear Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBoolean(root, "created", "creation/career state");
        XElement gears = ReadRequiredContainer(root, "gears");
        XElement selectedRoot = FindUniqueDirectByGuid(
            gears, "gear", rootGearId, "root Gear");

        var states = new List<CharacterGearEquipmentState>();
        var seenIds = new HashSet<Guid>();
        AddStateRecursive(
            selectedRoot,
            Array.Empty<Guid>(),
            parentDisplayPath: null,
            created,
            states,
            seenIds);
        return states.ToArray();
    }

    internal static XElement FindNode(
        XElement root,
        CharacterGearEquipmentIdentity identity)
    {
        if (!CharacterGearEquipmentRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Gear hierarchy is invalid.");
        }

        XElement current = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "gears"),
            "gear",
            identity.GearPath[0],
            "root Gear");
        for (int index = 1; index < identity.GearPath.Count; index++)
        {
            current = FindUniqueDirectByGuid(
                ReadRequiredContainer(current, "children"),
                "gear",
                identity.GearPath[index],
                "nested Gear");
        }
        return current;
    }

    private static void AddStateRecursive(
        XElement gear,
        IReadOnlyList<Guid> parentPath,
        string? parentDisplayPath,
        bool created,
        ICollection<CharacterGearEquipmentState> states,
        ISet<Guid> seenIds)
    {
        Guid gearId = ReadGuid(gear);
        if (!seenIds.Add(gearId))
        {
            throw new InvalidOperationException(
                "Gear Equipped requires unique stable Guid identity throughout the selected tree.");
        }

        Guid[] path = [.. parentPath, gearId];
        string name = DisplayName(gear);
        string displayPath = parentDisplayPath is null ? name : $"{parentDisplayPath} > {name}";
        string parentId = ReadOptionalSingleText(gear, "parentid");
        var identity = new CharacterGearEquipmentIdentity(path);
        if (!CharacterGearEquipmentRules.TryCreateState(
                identity,
                created,
                includedInParent: parentId.Length != 0,
                loadedIntoClip: false,
                displayPath,
                ReadRequiredBoolean(gear, "equipped", "equipped state"),
                out CharacterGearEquipmentState state))
        {
            throw new InvalidOperationException(
                "Gear Equipped requires exact saved hierarchical identity and state.");
        }
        states.Add(state);

        XElement? children = ReadOptionalContainer(gear, "children");
        if (children is null)
        {
            return;
        }
        foreach (XElement child in children.Elements("gear"))
        {
            AddStateRecursive(child, path, displayPath, created, states, seenIds);
        }
    }

    private static bool ReadRequiredBoolean(XElement parent, string name, string kind)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"Gear Equipped editing requires one exact saved {kind} Boolean.");
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
                $"The selected Gear has duplicate <{name}> values.")
        };
    }

    private static Guid ReadGuid(XElement element)
    {
        XElement[] values = element.Elements("guid").Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid id)
            || id == Guid.Empty)
        {
            throw new InvalidOperationException("Gear requires one stable Guid.");
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

    private static string DisplayName(XElement element)
    {
        XElement[] names = element.Elements("name").Take(2).ToArray();
        return names.Length switch
        {
            0 => "Unnamed Gear",
            1 when names[0].Value.Length != 0 => names[0].Value,
            1 => "Unnamed Gear",
            _ => throw new InvalidOperationException("A Gear node has duplicate <name> values.")
        };
    }
}
