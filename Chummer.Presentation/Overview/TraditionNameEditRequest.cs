using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record TraditionNameEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid TraditionId,
    string TraditionName);

public sealed record TraditionNameEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid TraditionId,
    string ExpectedTraditionName,
    string TraditionName);

internal sealed record TraditionNameProjection(Guid TraditionId, string TraditionName);

internal static class TraditionNameEditorProjector
{
    public static TraditionNameEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing the tradition name.");
        }

        TraditionNameProjection projection = ProjectValue(xml);
        return new TraditionNameEditorState(
            workspaceId,
            contentRevision,
            projection.TraditionId,
            projection.TraditionName);
    }

    internal static TraditionNameProjection ProjectValue(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] traditions = root.Elements("tradition").Take(2).ToArray();
        if (traditions.Length != 1)
        {
            throw new InvalidOperationException(
                "Exactly one saved tradition is required for tradition-name editing.");
        }

        XElement tradition = traditions[0];
        if (!Guid.TryParse(ReadRequiredSingleValue(tradition, "guid"), out Guid traditionId)
            || traditionId == Guid.Empty)
        {
            throw new InvalidOperationException("The saved tradition has no stable GUID identity.");
        }
        if (!Guid.TryParse(ReadRequiredSingleValue(tradition, "sourceid"), out Guid sourceId)
            || sourceId != CharacterTraditionNameRules.CustomMagicalTraditionSourceId)
        {
            throw new InvalidOperationException(
                "Chummer5 exposes tradition-name editing only for the exact Custom magical tradition.");
        }
        if (!string.Equals(
                ReadRequiredSingleValue(tradition, "traditiontype"),
                "MAG",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Custom tradition-name editing requires a magical tradition.");
        }

        XElement[] names = tradition.Elements("name").Take(2).ToArray();
        string name = names.Length switch
        {
            0 => string.Empty,
            1 => names[0].Value,
            _ => throw new InvalidOperationException("The saved tradition has duplicate <name> values.")
        };
        if (!CharacterTraditionNameRules.TryValidate(name, out string validated))
        {
            throw new InvalidOperationException(
                "The saved tradition name cannot be represented by the Chummer5 single-line control.");
        }
        return new TraditionNameProjection(traditionId, validated);
    }

    private static string ReadRequiredSingleValue(XElement parent, string elementName)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0].Value,
            0 => throw new InvalidOperationException(
                $"The saved tradition is missing <{elementName}>."),
            _ => throw new InvalidOperationException(
                $"The saved tradition has duplicate <{elementName}> values.")
        };
    }
}
