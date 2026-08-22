using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearOverclockerEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearOverclockerState> Nodes);

public sealed record GearOverclockerEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearOverclockerIdentity Identity,
    string ExpectedNodeRevision,
    CharacterGearOverclockerAttribute Attribute);

internal static class GearOverclockerEditorProjector
{
    public static GearOverclockerEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Gear Overclocker.");
        }

        return new GearOverclockerEditorState(
            workspaceId,
            contentRevision,
            rootGearId,
            ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearOverclockerState> ProjectValue(
        string xml,
        Guid rootGearId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (rootGearId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Gear Overclocker editing requires a stable root Gear Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredBoolean(root, "created", "creation/career state"))
        {
            throw new InvalidOperationException(
                "Gear Overclocker is exposed by CharacterCareer only.");
        }
        if (!HasActiveOverclockerImprovement(root))
        {
            throw new InvalidOperationException(
                "Gear Overclocker requires an active Overclocker improvement.");
        }

        XElement selectedRoot = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "gears"),
            rootGearId,
            "root Gear");
        var states = new List<CharacterGearOverclockerState>();
        var seenIds = new HashSet<Guid>();
        AddStateRecursive(
            selectedRoot,
            Array.Empty<Guid>(),
            parentDisplayPath: null,
            states,
            seenIds);
        if (states.Count == 0)
        {
            throw new InvalidOperationException(
                "The selected Gear tree has no Cyberdeck eligible for Overclocker.");
        }
        return states.ToArray();
    }

    internal static XElement FindNode(
        XElement root,
        CharacterGearOverclockerIdentity identity)
    {
        if (!CharacterGearOverclockerRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Gear hierarchy is invalid.");
        }

        XElement current = FindUniqueDirectByGuid(
            ReadRequiredContainer(root, "gears"),
            identity.GearPath[0],
            "root Gear");
        for (int index = 1; index < identity.GearPath.Count; index++)
        {
            current = FindUniqueDirectByGuid(
                ReadRequiredContainer(current, "children"),
                identity.GearPath[index],
                "nested Gear");
        }
        return current;
    }

    private static void AddStateRecursive(
        XElement gear,
        IReadOnlyList<Guid> parentPath,
        string? parentDisplayPath,
        ICollection<CharacterGearOverclockerState> states,
        ISet<Guid> seenIds)
    {
        Guid gearId = ReadGuid(gear);
        if (!seenIds.Add(gearId))
        {
            throw new InvalidOperationException(
                "Gear Overclocker requires unique stable Guid identity throughout the selected tree.");
        }

        Guid[] path = [.. parentPath, gearId];
        string name = DisplayName(gear);
        string displayPath = parentDisplayPath is null ? name : $"{parentDisplayPath} > {name}";
        string category = ReadRequiredSingleText(gear, "category", "Gear category");
        if (string.Equals(category, "Cyberdecks", StringComparison.Ordinal))
        {
            var identity = new CharacterGearOverclockerIdentity(path);
            if (!CharacterGearOverclockerRules.TryCreateState(
                    identity,
                    created: true,
                    hasActiveOverclocker: true,
                    category,
                    displayPath,
                    ReadOptionalSingleText(gear, "overclocked"),
                    out CharacterGearOverclockerState state))
            {
                throw new InvalidOperationException(
                    "Gear Overclocker requires exact saved Career Cyberdeck state.");
            }
            states.Add(state);
        }

        XElement? children = ReadOptionalContainer(gear, "children");
        if (children is null)
        {
            return;
        }
        foreach (XElement child in children.Elements("gear"))
        {
            AddStateRecursive(child, path, displayPath, states, seenIds);
        }
    }

    private static bool HasActiveOverclockerImprovement(XElement root)
    {
        XElement? improvements = ReadOptionalContainer(root, "improvements");
        if (improvements is null)
        {
            return false;
        }
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            if (string.Equals(
                    ReadOptionalSingleText(improvement, "improvementttype"),
                    "Overclocker",
                    StringComparison.Ordinal)
                && ReadOptionalIntegerBoolean(improvement, "enabled", 1) > 0)
            {
                return true;
            }
        }
        return false;
    }

    private static int ReadOptionalIntegerBoolean(
        XElement parent,
        string name,
        int defaultValue)
    {
        string value = ReadOptionalSingleText(parent, name);
        if (value.Length == 0)
        {
            return defaultValue;
        }
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        if (bool.TryParse(value, out bool boolean))
        {
            return boolean ? 1 : 0;
        }
        throw new InvalidOperationException(
            $"A saved Improvement has an invalid <{name}> value.");
    }

    private static bool ReadRequiredBoolean(XElement parent, string name, string kind)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"Gear Overclocker editing requires one exact saved {kind} Boolean.");
        }
        return value;
    }

    private static string ReadRequiredSingleText(XElement parent, string name, string kind)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || values[0].Value.Length == 0)
        {
            throw new InvalidOperationException(
                $"Gear Overclocker editing requires one exact saved {kind}.");
        }
        return values[0].Value;
    }

    private static string ReadOptionalSingleText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value.Trim(),
            _ => throw new InvalidOperationException(
                $"The saved node has duplicate <{name}> values.")
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
        Guid id,
        string kind)
    {
        XElement[] matches = container.Elements("gear")
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
