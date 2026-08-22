using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record ImprovementGroupActiveEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterImprovementGroupActiveState> Groups);

public sealed record ImprovementGroupActiveEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterImprovementGroupIdentity Identity,
    string ExpectedGroupRevision,
    bool Enabled);

internal static class ImprovementGroupActiveEditorProjector
{
    public static ImprovementGroupActiveEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Improvement groups.");
        }

        return new ImprovementGroupActiveEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml));
    }

    internal static IReadOnlyList<CharacterImprovementGroupActiveState> ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredCreated(root))
        {
            throw new InvalidOperationException(
                "Improvement group Enable All and Disable All are exposed by CharacterCareer only.");
        }

        XElement improvements = ReadRequiredContainer(root, "improvements");
        var identities = new List<CharacterImprovementGroupIdentity>
        {
            new(CharacterImprovementGroupKind.Ungrouped, string.Empty)
        };
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        XElement? groups = ReadOptionalContainer(root, "improvementgroups");
        if (groups is not null)
        {
            foreach (XElement group in groups.Elements("improvementgroup"))
            {
                string name = group.Value;
                var identity = new CharacterImprovementGroupIdentity(
                    CharacterImprovementGroupKind.Named,
                    name);
                if (!CharacterImprovementGroupActiveRules.IsValidIdentity(identity)
                    || !seenNames.Add(name))
                {
                    throw new InvalidOperationException(
                        "Named Improvement groups require unique, non-reserved saved identity.");
                }
                identities.Add(identity);
            }
        }

        var members = identities.ToDictionary(
            identity => identity,
            _ => new List<CharacterImprovementGroupMemberState>());
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            bool custom = ReadOptionalBoolean(improvement, "custom", defaultValue: false);
            if (!custom)
            {
                continue;
            }

            string customGroup = ReadOptionalValue(improvement, "customgroup");
            CharacterImprovementGroupIdentity groupIdentity = customGroup.Length == 0
                ? identities[0]
                : identities.SingleOrDefault(identity =>
                    identity.Kind == CharacterImprovementGroupKind.Named
                    && string.Equals(identity.Name, customGroup, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "A Custom Improvement references an unavailable saved group.");
            var member = new CharacterImprovementGroupMemberState(
                ReadIdentity(improvement),
                ReadEnabled(improvement));
            if (members[groupIdentity].Any(existing => existing.Identity == member.Identity))
            {
                throw new InvalidOperationException(
                    "A Custom Improvement has duplicate stable identity within its saved group.");
            }
            members[groupIdentity].Add(member);
        }

        return identities.Select(identity =>
        {
            if (!CharacterImprovementGroupActiveRules.TryCreateState(
                    identity,
                    created: true,
                    displayName: identity.Kind == CharacterImprovementGroupKind.Ungrouped
                        ? "Ungrouped custom improvements"
                        : identity.Name,
                    members: members[identity],
                    out CharacterImprovementGroupActiveState state))
            {
                throw new InvalidOperationException(
                    "Improvement group state requires exact stable member identity.");
            }
            return state;
        }).ToArray();
    }

    internal static IReadOnlyList<XElement> FindMatchingNodes(
        XElement root,
        CharacterImprovementGroupIdentity identity)
    {
        if (!CharacterImprovementGroupActiveRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("The selected Improvement group identity is invalid.");
        }
        XElement improvements = ReadRequiredContainer(root, "improvements");
        return improvements.Elements("improvement")
            .Where(improvement => CharacterImprovementGroupActiveRules.Includes(
                identity,
                ReadOptionalBoolean(improvement, "custom", defaultValue: false),
                ReadOptionalValue(improvement, "customgroup")))
            .ToArray();
    }

    internal static bool ReadEnabled(XElement improvement)
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
                "Improvement group editing requires an exact saved creation/career state.");
        }
        return created;
    }

    private static bool ReadOptionalBoolean(
        XElement parent,
        string name,
        bool defaultValue)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return defaultValue;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException($"An Improvement has duplicate <{name}> values.");
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
                $"An Improvement has an invalid <{name}> value.")
        };
    }

    private static XElement ReadRequiredContainer(XElement parent, string name)
        => ReadOptionalContainer(parent, name)
            ?? throw new InvalidOperationException(
                $"The saved runner requires one <{name}> collection.");

    private static XElement? ReadOptionalContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidOperationException(
                $"The saved runner has duplicate <{name}> collections.")
        };
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
