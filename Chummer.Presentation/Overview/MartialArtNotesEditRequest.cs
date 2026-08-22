using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record MartialArtNotesEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterMartialArtNotesState> Targets);

public sealed record MartialArtNotesEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterMartialArtNotesIdentity Identity,
    string ExpectedTargetRevision,
    string Notes,
    string NotesColor);

internal static class MartialArtNotesEditorProjector
{
    private const string LegacyDefaultNotesColor = "Chocolate";

    public static MartialArtNotesEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Martial Arts notes.");
        }
        return new MartialArtNotesEditorState(workspaceId, contentRevision, ProjectValue(xml));
    }

    internal static IReadOnlyList<CharacterMartialArtNotesState> ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBool(root, "created");
        XElement arts = FindContainer(root);
        var identities = new HashSet<Guid>();
        var states = new List<CharacterMartialArtNotesState>();
        foreach (XElement art in arts.Elements("martialart"))
        {
            Guid artId = ReadIdentity(art, "Martial Art");
            if (!identities.Add(artId))
            {
                throw new InvalidOperationException("Martial Art GUIDs must be globally unique.");
            }
            string artName = ReadRequiredValue(art, "name", "Martial Art");
            AddState(
                states,
                new CharacterMartialArtNotesIdentity(artId, null),
                created,
                artName,
                artName,
                art);

            XElement techniques = FindTechniqueContainer(art);
            foreach (XElement technique in techniques.Elements("martialarttechnique"))
            {
                Guid techniqueId = ReadIdentity(technique, "Martial Art Technique");
                if (!identities.Add(techniqueId))
                {
                    throw new InvalidOperationException(
                        "Martial Art and Technique GUIDs must be globally unique.");
                }
                AddState(
                    states,
                    new CharacterMartialArtNotesIdentity(artId, techniqueId),
                    created,
                    artName,
                    ReadRequiredValue(technique, "name", "Martial Art Technique"),
                    technique);
            }
        }
        return states;
    }

    internal static XElement FindContainer(XElement root)
    {
        XElement[] values = root.Elements("martialarts").Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException("Martial Arts notes require one <martialarts> collection."),
            _ => throw new InvalidOperationException("The saved runner has duplicate <martialarts> collections.")
        };
    }

    internal static XElement FindNode(XElement root, CharacterMartialArtNotesIdentity identity)
    {
        if (!CharacterMartialArtNotesRules.IsValidIdentity(identity))
        {
            throw new InvalidOperationException("Martial Arts notes require one stable typed identity.");
        }
        XElement[] arts = FindContainer(root).Elements("martialart")
            .Where(art => ReadIdentity(art, "Martial Art") == identity.MartialArtId)
            .Take(2)
            .ToArray();
        if (arts.Length != 1)
        {
            throw new InvalidOperationException("The selected Martial Art identity is missing or ambiguous.");
        }
        if (!identity.TechniqueId.HasValue)
        {
            return arts[0];
        }
        XElement[] techniques = FindTechniqueContainer(arts[0])
            .Elements("martialarttechnique")
            .Where(technique => ReadIdentity(technique, "Martial Art Technique") == identity.TechniqueId.Value)
            .Take(2)
            .ToArray();
        return techniques.Length == 1
            ? techniques[0]
            : throw new InvalidOperationException(
                "The selected Technique identity is missing, ambiguous, or outside its parent Martial Art.");
    }

    private static void AddState(
        ICollection<CharacterMartialArtNotesState> states,
        CharacterMartialArtNotesIdentity identity,
        bool created,
        string artName,
        string targetName,
        XElement target)
    {
        string notes = ReadOptionalValue(target, "notes", string.Empty);
        string notesColor = ReadOptionalValue(target, "notesColor", LegacyDefaultNotesColor);
        if (!CharacterMartialArtNotesRules.TryCreateState(
                identity, created, artName, targetName, notes, notesColor,
                out CharacterMartialArtNotesState state))
        {
            throw new InvalidOperationException(
                "Martial Arts notes require exact identity, note text, and legacy HTML color.");
        }
        states.Add(state);
    }

    private static XElement FindTechniqueContainer(XElement art)
    {
        XElement[] values = art.Elements("martialarttechniques").Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException(
                "Every Martial Art requires one <martialarttechniques> collection."),
            _ => throw new InvalidOperationException(
                "A Martial Art has duplicate <martialarttechniques> collections.")
        };
    }

    private static Guid ReadIdentity(XElement target, string label)
    {
        XElement[] values = target.Elements("guid").Take(2).ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0].Value.Trim(), "D", out Guid parsed)
            && parsed != Guid.Empty
                ? parsed
                : throw new InvalidOperationException($"{label} requires one stable non-empty GUID.");
    }

    private static string ReadRequiredValue(XElement target, string name, string label)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0].Value
            : throw new InvalidOperationException($"{label} requires one <{name}> value.");
    }

    private static string ReadOptionalValue(XElement target, string name, string fallback)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => fallback,
            1 => values[0].Value,
            _ => throw new InvalidOperationException($"A Martial Arts note target has duplicate <{name}> values.")
        };
    }

    private static bool ReadRequiredBool(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        return values.Length == 1 && bool.TryParse(values[0].Value.Trim(), out bool parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Martial Arts notes require one exact saved <{name}> Boolean.");
    }
}
