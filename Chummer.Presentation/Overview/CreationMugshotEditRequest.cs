using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CreationMugshotEditorItem(
    CharacterMugshotIdentity Identity,
    string ImageBase64);

public sealed record CreationMugshotEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterCreationMugshotState MugshotState,
    IReadOnlyList<CreationMugshotEditorItem> Items);

public sealed record CreationMugshotMainEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterMugshotIdentity SelectedIdentity,
    string ExpectedMugshotRevision,
    bool IsMain);

internal static class CreationMugshotEditorProjector
{
    public static CreationMugshotEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing Creation mugshots.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = RequiredBoolean(root, "created");
        int mainMugshotIndex = RequiredInteger(root, "mainmugshotindex");
        XElement[] containers = root.Elements("mugshots").Take(2).ToArray();
        if (containers.Length != 1)
        {
            throw new InvalidOperationException(
                "Creation mugshot editing requires one exact saved <mugshots> collection.");
        }

        XElement[] imageElements = containers[0].Elements("mugshot").ToArray();
        var identities = new List<CharacterMugshotIdentity>(imageElements.Length);
        var items = new List<CreationMugshotEditorItem>(imageElements.Length);
        for (int index = 0; index < imageElements.Length; index++)
        {
            byte[] imageBytes;
            try
            {
                imageBytes = Convert.FromBase64String(imageElements[index].Value);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "Creation mugshot editing requires every saved mugshot to contain exact Base64 image bytes.",
                    exception);
            }
            if (!CharacterCreationMugshotRules.TryCreateIdentity(
                    index,
                    imageBytes,
                    out CharacterMugshotIdentity identity))
            {
                throw new InvalidOperationException(
                    "Creation mugshot editing does not target empty or unidentifiable mugshot entries.");
            }
            identities.Add(identity);
            items.Add(new CreationMugshotEditorItem(identity, Convert.ToBase64String(imageBytes)));
        }

        if (!CharacterCreationMugshotRules.TryCreateState(
                created,
                identities,
                mainMugshotIndex,
                out CharacterCreationMugshotState state))
        {
            throw new InvalidOperationException(
                "Creation mugshot editing requires Creation phase and an exact saved collection/main-index state.");
        }

        return new CreationMugshotEditorState(
            workspaceId,
            contentRevision,
            state,
            items.AsReadOnly());
    }

    private static bool RequiredBoolean(XElement root, string elementName)
    {
        XElement[] matches = root.Elements(elementName).Take(2).ToArray();
        if (matches.Length != 1 || !bool.TryParse(matches[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException(
                $"Creation mugshot editing requires one exact saved <{elementName}> Boolean.");
        }
        return value;
    }

    private static int RequiredInteger(XElement root, string elementName)
    {
        XElement[] matches = root.Elements(elementName).Take(2).ToArray();
        if (matches.Length != 1
            || !int.TryParse(
                matches[0].Value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value))
        {
            throw new InvalidOperationException(
                $"Creation mugshot editing requires one exact saved <{elementName}> integer.");
        }
        return value;
    }
}
