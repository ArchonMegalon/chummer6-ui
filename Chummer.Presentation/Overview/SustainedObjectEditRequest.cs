using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record SustainedObjectsEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterSustainedObjectState> Objects);

public sealed record SustainedObjectEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterSustainedObjectState ExpectedState,
    CharacterSustainedObjectAction Action,
    int Force,
    int NetHits,
    bool SelfSustained,
    bool Confirmed);

internal sealed record SustainedObjectProjection(
    CharacterSustainedObjectState State,
    XElement Element);

internal static class SustainedObjectsEditorProjector
{
    public static SustainedObjectsEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing sustained effects.");
        }

        XDocument document = ParseDocument(xml);
        IReadOnlyList<SustainedObjectProjection> projections = ProjectElements(document.Root!);
        return new SustainedObjectsEditorState(
            workspaceId,
            contentRevision,
            projections.Select(static projection => projection.State).ToArray());
    }

    internal static IReadOnlyList<SustainedObjectProjection> ProjectElements(XElement root)
    {
        XElement[] containers = root.Elements("sustainedobjects").Take(2).ToArray();
        if (containers.Length == 0)
        {
            return [];
        }
        if (containers.Length != 1)
        {
            throw new InvalidOperationException("The saved runner has duplicate <sustainedobjects> containers.");
        }

        var nextOccurrence = new Dictionary<(string Type, Guid Id), int>();
        var basis = new List<CharacterSustainedObjectBasis>();
        var elements = new List<XElement>();
        foreach (XElement element in containers[0].Elements("sustainedobject"))
        {
            string linkedObjectType = ReadRequiredText(element, "linkedobjecttype");
            if (!CharacterSustainedObjectRules.IsSupportedLinkedObjectType(linkedObjectType))
            {
                throw new InvalidOperationException(
                    $"Unsupported sustained-object type '{linkedObjectType}'.");
            }

            string linkedObjectText = ReadRequiredText(element, "linkedobject");
            if (!Guid.TryParseExact(linkedObjectText, "D", out Guid linkedObjectId)
                || linkedObjectId == Guid.Empty)
            {
                throw new InvalidOperationException("A sustained object has an invalid linked-object GUID.");
            }

            var key = (linkedObjectType, linkedObjectId);
            int occurrence = nextOccurrence.GetValueOrDefault(key);
            nextOccurrence[key] = occurrence + 1;
            int force = ReadOptionalInt(element, "force", CharacterSustainedObjectRules.MinimumForce);
            int netHits = ReadOptionalInt(element, "nethits", CharacterSustainedObjectRules.MinimumNetHits);
            bool selfSustained = ReadOptionalBool(element, "self", fallback: true);
            string displayName = ResolveLinkedDisplayName(root, linkedObjectType, linkedObjectId);
            basis.Add(new CharacterSustainedObjectBasis(
                new CharacterSustainedObjectIdentity(linkedObjectType, linkedObjectId, occurrence),
                displayName,
                force,
                netHits,
                selfSustained,
                !string.Equals(linkedObjectType, "CritterPower", StringComparison.Ordinal)));
            elements.Add(element);
        }

        if (!CharacterSustainedObjectRules.TryProjectAll(
                basis,
                out IReadOnlyList<CharacterSustainedObjectState>? states)
            || states is null
            || states.Count != elements.Count)
        {
            throw new InvalidOperationException(
                "The saved sustained effects do not satisfy the exact Chummer5 edit contract.");
        }

        return states.Select((state, index) => new SustainedObjectProjection(state, elements[index])).ToArray();
    }

    internal static XDocument ParseDocument(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (document.Root is not { Name.LocalName: "character" })
        {
            throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        }
        return document;
    }

    private static string ResolveLinkedDisplayName(
        XElement root,
        string linkedObjectType,
        Guid linkedObjectId)
    {
        (string Container, string Item) location = linkedObjectType switch
        {
            "Spell" => ("spells", "spell"),
            "ComplexForm" => ("complexforms", "complexform"),
            "CritterPower" => ("critterpowers", "critterpower"),
            _ => throw new InvalidOperationException($"Unsupported sustained-object type '{linkedObjectType}'.")
        };
        XElement[] containers = root.Elements(location.Container).Take(2).ToArray();
        if (containers.Length != 1)
        {
            throw new InvalidOperationException(
                $"The saved runner must contain exactly one <{location.Container}> collection.");
        }

        XElement[] matches = containers[0]
            .Elements(location.Item)
            .Where(candidate =>
            {
                XElement[] ids = candidate.Elements("guid").Take(2).ToArray();
                return ids.Length == 1
                    && Guid.TryParseExact(ids[0].Value.Trim(), "D", out Guid candidateId)
                    && candidateId == linkedObjectId;
            })
            .Take(2)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "A sustained object does not resolve to exactly one saved linked object.");
        }

        string name = ReadRequiredText(matches[0], "name");
        return name;
    }

    private static string ReadRequiredText(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value))
        {
            throw new InvalidOperationException(
                $"A saved sustained object or linked item has an invalid or duplicate <{name}> value.");
        }
        return values[0].Value.Trim();
    }

    private static int ReadOptionalInt(XElement parent, string name, int fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return fallback;
        }
        if (values.Length != 1
            || !int.TryParse(values[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException(
                $"A saved sustained object has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static bool ReadOptionalBool(XElement parent, string name, bool fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return fallback;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"A saved sustained object has an invalid or duplicate <{name}> value.");
        }
        return value;
    }
}
