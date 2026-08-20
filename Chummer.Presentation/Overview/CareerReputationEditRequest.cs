using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerReputationEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    int StreetCred,
    int Notoriety,
    int PublicAwareness,
    bool AstralReputationVisible,
    int AstralReputation,
    bool WildReputationVisible,
    int WildReputation);

public sealed record CareerReputationEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    int StreetCred,
    int Notoriety,
    int PublicAwareness,
    int? AstralReputation,
    int? WildReputation);

internal static class CareerReputationEditorProjector
{
    public static CareerReputationEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing reputation.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ParseBool(root.Element("created")?.Value))
        {
            throw new InvalidOperationException("Reputation can only be changed for a created/career runner.");
        }

        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        bool forbiddenArcana = IsBookEnabled(sourceData, "FA");
        bool streetGrimoire = IsBookEnabled(sourceData, "SG");
        return new CareerReputationEditorState(
            workspaceId,
            contentRevision,
            ParseInt(root.Element("streetcred")?.Value),
            ParseInt(root.Element("notoriety")?.Value),
            ParseInt(root.Element("publicawareness")?.Value),
            AstralReputationVisible: forbiddenArcana || streetGrimoire,
            AstralReputation: ParseInt(root.Element("baseastralreputation")?.Value),
            WildReputationVisible: forbiddenArcana,
            WildReputation: ParseInt(root.Element("basewildreputation")?.Value));
    }

    internal static bool IsBookEnabled(ICharacterSourceDataContext? sourceData, string sourceCode)
        => sourceData is not null
            && sourceData.TryIsBookEnabled(sourceCode, out bool enabled)
            && enabled;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out bool parsed) && parsed;

    private static int ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
}
