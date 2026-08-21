using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ImprovementActiveEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterImprovementActiveState> Improvements);

public sealed record ImprovementActiveEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterImprovementIdentity Identity,
    string ExpectedImprovementRevision,
    bool Enabled);

internal static class ImprovementActiveEditorProjector
{
    public static ImprovementActiveEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing improvements.");
        }

        return new ImprovementActiveEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml));
    }

    internal static IReadOnlyList<CharacterImprovementActiveState> ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "Improvement Active is exposed by CharacterCareer only.");
        }

        XElement improvements = ReadRequiredContainer(root, "improvements");
        var states = new List<CharacterImprovementActiveState>();
        var seen = new HashSet<CharacterImprovementIdentity>();
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            CharacterImprovementIdentity identity = ReadIdentity(improvement);
            if (!seen.Add(identity)
                || !CharacterImprovementActiveRules.TryCreateState(
                    identity,
                    created: true,
                    displayName: ReadDisplayName(improvement, identity),
                    enabled: ReadEnabled(improvement),
                    out CharacterImprovementActiveState state))
            {
                throw new InvalidOperationException(
                    "Improvements require unique stable saved identity and exact active state.");
            }
            states.Add(state);
        }

        return states.ToArray();
    }

    internal static XElement FindNode(XElement root, CharacterImprovementIdentity identity)
    {
        if (!CharacterImprovementActiveRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Improvement identity is invalid.");
        }

        XElement improvements = ReadRequiredContainer(root, "improvements");
        XElement[] matches = improvements.Elements("improvement")
            .Where(candidate => CharacterImprovementActiveRules.IdentityEquals(
                ReadIdentity(candidate),
                identity))
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "The selected Improvement identity is missing or ambiguous.");
    }

    private static CharacterImprovementIdentity ReadIdentity(XElement improvement)
        => new(
            ReadRequiredValue(improvement, "sourcename", "Improvement source name"),
            ReadRequiredValue(improvement, "improvementttype", "Improvement type"),
            ReadRequiredValue(improvement, "improvementsource", "Improvement source"),
            ReadOptionalValue(improvement, "improvedname"),
            ReadOptionalValue(improvement, "unique"),
            ReadOptionalValue(improvement, "target"),
            ReadOptionalValue(improvement, "customid"),
            ReadOptionalValue(improvement, "customgroup"));

    private static bool ReadRequiredCreated(XElement root)
    {
        XElement[] values = root.Elements("created").Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool created))
        {
            throw new InvalidOperationException(
                "Improvement Active requires an exact saved creation/career state.");
        }
        return created;
    }

    private static bool ReadEnabled(XElement improvement)
    {
        XElement[] values = improvement.Elements("enabled").Take(2).ToArray();
        if (values.Length == 0)
        {
            return true;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException("An Improvement has duplicate <enabled> values.");
        }

        string value = values[0].Value.Trim();
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
        {
            return count > 0;
        }
        if (bool.TryParse(value, out bool enabled))
        {
            return enabled;
        }
        throw new InvalidOperationException("An Improvement has an invalid <enabled> value.");
    }

    private static string ReadDisplayName(
        XElement improvement,
        CharacterImprovementIdentity identity)
    {
        string customName = ReadOptionalValue(improvement, "customname");
        if (!string.IsNullOrWhiteSpace(customName))
        {
            return customName;
        }
        if (!string.IsNullOrWhiteSpace(identity.ImprovedName))
        {
            return $"{identity.ImprovementType} · {identity.ImprovedName}";
        }
        return $"{identity.ImprovementType} · {identity.SourceName}";
    }

    private static XElement ReadRequiredContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0]
            : throw new InvalidOperationException(
                $"The saved runner requires one <{name}> collection.");
    }

    private static string ReadRequiredValue(XElement parent, string name, string description)
    {
        string value = ReadOptionalValue(parent, name);
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{description} is missing.");
    }

    private static string ReadOptionalValue(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => string.Empty,
            1 => values[0].Value,
            _ => throw new InvalidOperationException(
                $"An Improvement has duplicate <{name}> values.")
        };
    }
}
