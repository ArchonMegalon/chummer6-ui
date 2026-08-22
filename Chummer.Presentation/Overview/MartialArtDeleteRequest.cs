using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record MartialArtDeleteEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    IReadOnlyList<CharacterMartialArtDeleteState> Targets);

public sealed record MartialArtDeleteRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterMartialArtDeleteIdentity Identity,
    string ExpectedTargetRevision,
    bool Confirmed);

internal sealed record MartialArtDeleteProjection(
    CharacterMartialArtDeleteState State,
    XElement Target,
    IReadOnlyList<XElement> Improvements);

internal static class MartialArtDeleteEditorProjector
{
    private const string MartialArtSource = "MartialArt";
    private const string TechniqueSource = "MartialArtTechnique";

    public static MartialArtDeleteEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before deleting a Martial Art or Technique.");
        }
        XDocument document = ParseDocument(xml);
        return new MartialArtDeleteEditorState(
            workspaceId,
            contentRevision,
            ProjectElements(document.Root!).Select(static projection => projection.State).ToArray());
    }

    internal static IReadOnlyList<MartialArtDeleteProjection> ProjectElements(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        bool created = ReadRequiredBool(root, "created");
        XElement arts = FindSingleContainer(root, "martialarts");
        XElement improvements = FindSingleContainer(root, "improvements");
        var identities = new HashSet<Guid>();
        var projections = new List<MartialArtDeleteProjection>();

        foreach (XElement art in arts.Elements("martialart"))
        {
            Guid artId = ReadIdentity(art, "Martial Art");
            if (!identities.Add(artId))
            {
                throw new InvalidOperationException("Martial Art GUIDs must be globally unique.");
            }
            string artName = ReadRequiredText(art, "name", "Martial Art");
            bool isQuality = ReadRequiredBool(art, "isquality");
            XElement techniqueContainer = FindSingleContainer(art, "martialarttechniques");
            var techniques = new List<(Guid Id, XElement Element, string Name)>();
            foreach (XElement technique in techniqueContainer.Elements("martialarttechnique"))
            {
                Guid techniqueId = ReadIdentity(technique, "Martial Art Technique");
                if (!identities.Add(techniqueId))
                {
                    throw new InvalidOperationException(
                        "Martial Art and Technique GUIDs must be globally unique.");
                }
                techniques.Add((
                    techniqueId,
                    technique,
                    ReadRequiredText(technique, "name", "Martial Art Technique")));
            }

            IReadOnlyList<XElement> artImprovements = FindImprovements(
                improvements,
                new HashSet<(string Source, Guid Id)>(
                    new[] { (MartialArtSource, artId) }
                        .Concat(techniques.Select(static technique => (TechniqueSource, technique.Id)))));
            projections.Add(CreateProjection(
                new CharacterMartialArtDeleteIdentity(artId, null),
                created,
                artName,
                artName,
                isQuality,
                techniques.Count,
                art,
                artImprovements));

            foreach ((Guid techniqueId, XElement technique, string techniqueName) in techniques)
            {
                IReadOnlyList<XElement> techniqueImprovements = FindImprovements(
                    improvements,
                    new HashSet<(string Source, Guid Id)> { (TechniqueSource, techniqueId) });
                projections.Add(CreateProjection(
                    new CharacterMartialArtDeleteIdentity(artId, techniqueId),
                    created,
                    artName,
                    techniqueName,
                    isQuality,
                    0,
                    technique,
                    techniqueImprovements));
            }
        }
        return projections;
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

    private static MartialArtDeleteProjection CreateProjection(
        CharacterMartialArtDeleteIdentity identity,
        bool created,
        string martialArtName,
        string targetName,
        bool isQuality,
        int cascadeTechniqueCount,
        XElement target,
        IReadOnlyList<XElement> improvements)
    {
        string improvementState = string.Join(
            "\n",
            improvements.Select(element => element.ToString(SaveOptions.DisableFormatting)));
        if (!CharacterMartialArtDeleteRules.TryCreateState(
                identity,
                created,
                martialArtName,
                targetName,
                isQuality,
                cascadeTechniqueCount,
                target.ToString(SaveOptions.DisableFormatting),
                improvementState,
                out CharacterMartialArtDeleteState state))
        {
            throw new InvalidOperationException(
                "The Martial Art delete target does not satisfy the exact Chummer5 identity contract.");
        }
        return new MartialArtDeleteProjection(state, target, improvements);
    }

    private static IReadOnlyList<XElement> FindImprovements(
        XElement improvements,
        ISet<(string Source, Guid Id)> targets)
    {
        var matches = new List<XElement>();
        foreach (XElement improvement in improvements.Elements("improvement"))
        {
            XElement[] sources = improvement.Elements("improvementsource").Take(2).ToArray();
            if (sources.Length != 1)
            {
                throw new InvalidOperationException(
                    "Every saved Improvement requires one exact <improvementsource> value.");
            }
            string source = sources[0].Value;
            if (source is not (MartialArtSource or TechniqueSource))
            {
                continue;
            }
            XElement[] sourceNames = improvement.Elements("sourcename").Take(2).ToArray();
            if (sourceNames.Length != 1
                || !Guid.TryParseExact(sourceNames[0].Value.Trim(), "D", out Guid sourceId)
                || sourceId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    "Martial Art Improvements require one exact stable source GUID.");
            }
            if (targets.Contains((source, sourceId)))
            {
                matches.Add(improvement);
            }
        }
        return matches;
    }

    private static XElement FindSingleContainer(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0],
            0 => throw new InvalidOperationException($"The saved runner requires one <{name}> collection."),
            _ => throw new InvalidOperationException($"The saved runner has duplicate <{name}> collections.")
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

    private static string ReadRequiredText(XElement target, string name, string label)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0].Value
            : throw new InvalidOperationException($"{label} requires one <{name}> value.");
    }

    private static bool ReadRequiredBool(XElement target, string name)
    {
        XElement[] values = target.Elements(name).Take(2).ToArray();
        return values.Length == 1 && bool.TryParse(values[0].Value.Trim(), out bool parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Martial Art deletion requires one exact saved <{name}> Boolean.");
    }
}
