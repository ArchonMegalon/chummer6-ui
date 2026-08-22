using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record GearStolenEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid RootGearId,
    IReadOnlyList<CharacterGearStolenState> Nodes);

public sealed record GearStolenEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterGearStolenIdentity Identity,
    string ExpectedNodeRevision,
    bool Stolen);

internal static class GearStolenEditorProjector
{
    public static GearStolenEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        Guid rootGearId)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Gear Stolen.");
        }

        return new GearStolenEditorState(
            workspaceId,
            contentRevision,
            rootGearId,
            ProjectValue(xml, rootGearId));
    }

    internal static IReadOnlyList<CharacterGearStolenState> ProjectValue(
        string xml,
        Guid rootGearId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (rootGearId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Gear Stolen editing requires a stable root Gear Guid.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "Gear Stolen is exposed by CharacterCreate only.");
        }
        if (!HasActiveStolenNuyenImprovement(root))
        {
            throw new InvalidOperationException(
                "Gear Stolen requires an active creation-mode Nuyen/Stolen improvement.");
        }

        XElement gears = ReadRequiredContainer(root, "gears");
        XElement selectedRoot = FindUniqueDirectByGuid(
            gears,
            "gear",
            rootGearId,
            "root Gear");
        var states = new List<CharacterGearStolenState>();
        var seenIds = new HashSet<Guid>();
        AddStateRecursive(
            selectedRoot,
            Array.Empty<Guid>(),
            parentDisplayPath: null,
            states,
            seenIds);
        return states.ToArray();
    }

    internal static XElement FindNode(
        XElement root,
        CharacterGearStolenIdentity identity)
    {
        if (!CharacterGearStolenRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Gear hierarchy is invalid.");
        }

        XElement gears = ReadRequiredContainer(root, "gears");
        XElement current = FindUniqueDirectByGuid(
            gears,
            "gear",
            identity.GearPath[0],
            "root Gear");
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

    private static void AddStateRecursive(
        XElement gear,
        IReadOnlyList<Guid> parentPath,
        string? parentDisplayPath,
        ICollection<CharacterGearStolenState> states,
        ISet<Guid> seenIds)
    {
        Guid gearId = ReadGuid(gear);
        if (!seenIds.Add(gearId))
        {
            throw new InvalidOperationException(
                "Gear Stolen requires unique stable Guid identity throughout the selected tree.");
        }

        Guid[] path = [.. parentPath, gearId];
        string name = DisplayName(gear);
        string displayPath = parentDisplayPath is null ? name : $"{parentDisplayPath} > {name}";
        var identity = new CharacterGearStolenIdentity(path);
        if (!CharacterGearStolenRules.TryCreateState(
                identity,
                created: false,
                hasStolenNuyenImprovement: true,
                displayPath,
                ReadOptionalBoolean(gear, "stolen"),
                out CharacterGearStolenState state))
        {
            throw new InvalidOperationException(
                "Gear Stolen requires exact saved hierarchical identity and state.");
        }
        states.Add(state);

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

    private static bool HasActiveStolenNuyenImprovement(XElement root)
    {
        XElement? improvements = ReadOptionalContainer(root, "improvements");
        if (improvements is null)
        {
            return false;
        }

        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            string type = ReadOptionalText(improvement, "improvementttype");
            string improvedName = ReadOptionalText(improvement, "improvedname");
            if (!string.Equals(type, "Nuyen", StringComparison.Ordinal)
                || !string.Equals(improvedName, "Stolen", StringComparison.Ordinal))
            {
                continue;
            }

            int enabled = ReadOptionalIntegerBoolean(improvement, "enabled", defaultValue: 1);
            int addToRating = ReadOptionalIntegerBoolean(
                improvement,
                "addtorating",
                defaultValue: 0);
            string condition = ReadOptionalText(improvement, "condition");
            if (enabled > 0
                && addToRating <= 0
                && (condition.Length == 0
                    || string.Equals(condition, "create", StringComparison.Ordinal)))
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
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return defaultValue;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException(
                $"A saved Improvement has duplicate <{name}> values.");
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
        throw new InvalidOperationException(
            $"A saved Improvement has an invalid <{name}> value.");
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
                "Gear Stolen editing requires an exact saved creation/career state.");
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
            throw new InvalidOperationException(
                $"The selected Gear has duplicate <{name}> values.");
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
                $"The selected Gear has an invalid <{name}> value.")
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
