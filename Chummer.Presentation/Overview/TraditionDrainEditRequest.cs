using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record TraditionDrainEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    Guid TraditionId,
    string DrainExpression,
    IReadOnlyList<string> AllowedExpressions);

public sealed record TraditionDrainEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid TraditionId,
    string ExpectedDrainExpression,
    string DrainExpression);

internal sealed record TraditionDrainProjection(
    Guid TraditionId,
    string DrainExpression,
    IReadOnlyList<string> AllowedExpressions);

internal static class TraditionDrainEditorProjector
{
    public static TraditionDrainEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing tradition drain attributes.");
        }

        TraditionDrainProjection projection = ProjectValue(xml, sourceDataResolver);
        return new TraditionDrainEditorState(
            workspaceId,
            contentRevision,
            projection.TraditionId,
            projection.DrainExpression,
            projection.AllowedExpressions);
    }

    internal static TraditionDrainProjection ProjectValue(
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ICharacterSourceDataContext? context = sourceDataResolver?.TryCreateContext(xml);
        if (context is null
            || !context.TryResolveTraditionDrainExpressions(out IReadOnlyList<string> sourceExpressions))
        {
            throw new InvalidOperationException(
                "The exact traditions.xml drain-attribute catalog is unavailable for this runner profile.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] traditions = root.Elements("tradition").Take(2).ToArray();
        if (traditions.Length != 1)
        {
            throw new InvalidOperationException(
                "Exactly one saved tradition is required for drain-attribute editing.");
        }

        XElement tradition = traditions[0];
        if (!Guid.TryParse(ReadRequiredSingleValue(tradition, "guid"), out Guid traditionId)
            || traditionId == Guid.Empty
            || !Guid.TryParse(ReadRequiredSingleValue(tradition, "sourceid"), out Guid sourceId)
            || sourceId == Guid.Empty)
        {
            throw new InvalidOperationException("The saved tradition has no stable source and instance identity.");
        }

        string traditionType = ReadRequiredSingleValue(tradition, "traditiontype");
        string drain = ReadRequiredSingleValue(tradition, "drain");
        bool adept = ReadOptionalBoolean(root, "adept");
        bool magician = ReadOptionalBoolean(root, "magician");
        if (!CharacterTraditionDrainRules.TryCreateSemantics(
                traditionId,
                sourceId,
                traditionType,
                adept,
                magician,
                drain,
                sourceExpressions,
                out CharacterTraditionDrainSemantics semantics))
        {
            throw new InvalidOperationException(
                "Chummer5 does not expose an exact drain-attribute selection for this saved tradition state.");
        }

        return new TraditionDrainProjection(
            semantics.TraditionId,
            semantics.CurrentExpression,
            semantics.AllowedExpressions);
    }

    private static bool ReadOptionalBoolean(XElement root, string elementName)
    {
        XElement[] values = root.Elements(elementName).Take(2).ToArray();
        if (values.Length == 0)
        {
            return false;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value, out bool parsed))
        {
            throw new InvalidOperationException(
                $"The saved character has an invalid or duplicate <{elementName}> value.");
        }
        return parsed;
    }

    private static string ReadRequiredSingleValue(XElement parent, string elementName)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0].Value,
            0 => throw new InvalidOperationException($"The saved tradition is missing <{elementName}>."),
            _ => throw new InvalidOperationException($"The saved tradition has duplicate <{elementName}> values.")
        };
    }
}
