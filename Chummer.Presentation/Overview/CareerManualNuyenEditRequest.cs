using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerManualNuyenEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterCareerManualNuyenState Nuyen);

public sealed record CareerManualNuyenEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerManualNuyenState ExpectedState,
    CharacterCareerManualNuyenAction Action,
    int Amount,
    decimal Percent,
    string Reason,
    DateTime ExpenseDateLocal,
    bool Refund,
    bool KarmaNuyenExchange,
    bool ForceCareerVisible);

internal static class CareerManualNuyenEditorProjector
{
    public static CareerManualNuyenEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing manual Nuyen.");
        }
        return new CareerManualNuyenEditorState(
            workspaceId,
            contentRevision,
            ProjectState(xml, sourceDataResolver));
    }

    internal static CharacterCareerManualNuyenState ProjectState(
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { Name.LocalName: "character" }
            ? document.Root
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        bool created = ReadRequiredBool(root, "created");
        int availableKarma = ReadOptionalInt(root, "karma");
        decimal availableNuyen = ReadOptionalDecimal(root, "nuyen");

        decimal? workingForPeopleRate = null;
        decimal? workingForManRate = null;
        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        if (sourceData is not null
            && sourceData.TryResolveKarmaNuyenExchangeRates(
                out decimal resolvedWorkingForPeopleRate,
                out decimal resolvedWorkingForManRate))
        {
            workingForPeopleRate = resolvedWorkingForPeopleRate;
            workingForManRate = resolvedWorkingForManRate;
        }

        if (!CharacterCareerManualNuyenRules.TryProject(
                created,
                availableKarma,
                availableNuyen,
                workingForPeopleRate,
                workingForManRate,
                out CharacterCareerManualNuyenState? state)
            || state is null)
        {
            throw new InvalidOperationException(
                "The saved runner and settings profile do not prove the exact Chummer5 manual-Nuyen exchange rules.");
        }
        return state;
    }

    private static bool ReadRequiredBool(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static int ReadOptionalInt(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return 0;
        }
        if (values.Length != 1
            || !int.TryParse(values[0].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static decimal ReadOptionalDecimal(XElement root, string name)
    {
        XElement[] values = root.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return 0m;
        }
        if (values.Length != 1
            || !decimal.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }
}
