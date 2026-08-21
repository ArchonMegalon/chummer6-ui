using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record TraditionSpiritCategoryEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterTraditionSpiritCategorySemantics Semantics);

public sealed record TraditionSpiritCategoryFieldEdit(
    CharacterTraditionSpiritCategory Category,
    string ExpectedFieldRevision,
    string SpiritName);

public sealed record TraditionSpiritCategoryEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid TraditionId,
    Guid SourceId,
    IReadOnlyList<TraditionSpiritCategoryFieldEdit> Fields);

internal static class TraditionSpiritCategoryEditorProjector
{
    public static TraditionSpiritCategoryEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "Dossier revision is unavailable. Reload before editing tradition spirit categories.");
        }

        return new TraditionSpiritCategoryEditorState(
            workspaceId,
            contentRevision,
            ProjectValue(xml, sourceDataResolver));
    }

    internal static CharacterTraditionSpiritCategorySemantics ProjectValue(
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ICharacterSourceDataContext? context = sourceDataResolver?.TryCreateContext(xml);
        if (context is null
            || !context.TryResolveSpiritCatalogNames("Spirit", out IReadOnlyList<string> sourceCatalog))
        {
            throw new InvalidOperationException(
                "The exact active traditions.xml Spirit catalog and custom-data overlays are unavailable.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] traditions = root.Elements("tradition").Take(2).ToArray();
        if (traditions.Length != 1)
        {
            throw new InvalidOperationException(
                "Exactly one saved magical tradition is required for spirit-category editing.");
        }

        XElement tradition = traditions[0];
        if (!Guid.TryParseExact(ReadRequiredSingleValue(tradition, "guid"), "D", out Guid traditionId)
            || traditionId == Guid.Empty
            || !Guid.TryParseExact(ReadRequiredSingleValue(tradition, "sourceid"), "D", out Guid sourceId)
            || sourceId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The saved tradition has no exact stable source and instance identity.");
        }

        var fields = new List<CharacterTraditionSpiritCategoryValue>(5);
        foreach (CharacterTraditionSpiritCategory category in CharacterTraditionSpiritCategoryRules.Categories)
        {
            string elementName = CharacterTraditionSpiritCategoryRules.ElementName(category);
            XElement[] values = tradition.Elements(elementName).Take(2).ToArray();
            if (values.Length > 1)
            {
                throw new InvalidOperationException(
                    $"The saved tradition has duplicate <{elementName}> values.");
            }
            fields.Add(new CharacterTraditionSpiritCategoryValue(
                category,
                values.SingleOrDefault()?.Value ?? string.Empty));
        }

        if (!CharacterTraditionSpiritCategoryRules.TryCreateSemantics(
                traditionId,
                sourceId,
                ReadRequiredSingleValue(tradition, "traditiontype"),
                ReadRequiredBoolean(root, "magenabled"),
                ReadRequiredBoolean(root, "resenabled"),
                fields,
                sourceCatalog,
                ReadLimitCategories(root),
                out CharacterTraditionSpiritCategorySemantics semantics))
        {
            throw new InvalidOperationException(
                "Chummer5 exposes spirit-category editing only for an exact Custom MAG tradition and active catalog.");
        }
        return semantics;
    }

    private static IReadOnlyList<string> ReadLimitCategories(XElement root)
    {
        XElement[] containers = root.Elements("improvements").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException("The saved runner has duplicate <improvements> collections.");
        }
        if (containers.Length == 0)
        {
            return Array.Empty<string>();
        }

        var limits = new List<string>();
        foreach (XElement improvement in containers[0].Elements("improvement"))
        {
            XElement[] types = improvement.Elements("improvementttype").Take(2).ToArray();
            if (types.Length > 1)
            {
                throw new InvalidOperationException("A saved improvement has duplicate type values.");
            }
            if (types.Length != 1
                || !string.Equals(
                    types[0].Value.Trim(),
                    "LimitSpiritCategory",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!ReadImprovementEnabled(improvement))
            {
                continue;
            }

            XElement[] names = improvement.Elements("improvedname").Take(2).ToArray();
            if (names.Length != 1 || string.IsNullOrWhiteSpace(names[0].Value))
            {
                throw new InvalidOperationException(
                    "An enabled LimitSpiritCategory improvement has no exact category name.");
            }
            limits.Add(names[0].Value);
        }
        return limits;
    }

    private static bool ReadImprovementEnabled(XElement improvement)
    {
        XElement[] values = improvement.Elements("enabled").Take(2).ToArray();
        if (values.Length > 1)
        {
            throw new InvalidOperationException("A saved improvement has duplicate enabled values.");
        }
        if (values.Length == 0)
        {
            return true;
        }
        string saved = values[0].Value.Trim();
        if (int.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            return numeric > 0;
        }
        if (bool.TryParse(saved, out bool enabled))
        {
            return enabled;
        }
        throw new InvalidOperationException("A saved improvement has an invalid enabled value.");
    }

    private static bool ReadRequiredBoolean(XElement root, string elementName)
    {
        string value = ReadRequiredSingleValue(root, elementName);
        return bool.TryParse(value, out bool parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"The saved runner has an invalid <{elementName}> value.");
    }

    private static string ReadRequiredSingleValue(XElement parent, string elementName)
    {
        XElement[] values = parent.Elements(elementName).Take(2).ToArray();
        return values.Length switch
        {
            1 => values[0].Value,
            0 => throw new InvalidOperationException(
                $"The saved data is missing <{elementName}>."),
            _ => throw new InvalidOperationException(
                $"The saved data has duplicate <{elementName}> values.")
        };
    }
}
