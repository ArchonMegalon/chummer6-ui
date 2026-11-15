using System.Globalization;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerManualKarmaEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    CharacterCareerManualKarmaState Karma);

public sealed record CareerManualKarmaEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    CharacterCareerManualKarmaState ExpectedState,
    CharacterCareerManualKarmaAction Action,
    int Amount,
    string Reason,
    DateTime ExpenseDateLocal,
    bool Refund,
    bool KarmaNuyenExchange,
    bool ForceCareerVisible)
{
    public Guid OperationId { get; init; } = Guid.NewGuid();
}

/// <summary>
/// Resolves the Chummer5 Working-for-the-People / Working-for-the-Man
/// exchange rates behind the career manual Karma and Nuyen editors.
/// SR6 workspaces bind virtual creation profiles
/// (sr6.creation.profiles#profile:...) that never select an SR5
/// settings.xml row, so the exchange keys nuyenperbpwftp/nuyenperbpwftm
/// cannot resolve there. The ordinary (KarmaNuyenExchange=false) reward
/// route then reuses Core's neutral-rate precedent from
/// CharacterAfterRunRewardProjector: neutral values that do not claim a
/// saved settings profile resolved either exchange rate. Exchange-capable
/// requests stay refused for SR6 in WorkspaceXmlMutationCatalog; SR5
/// keeps the exact fail-closed contract. Ambiguous edition declarations
/// (duplicate or nested gameedition elements) refuse every manual
/// Karma/Nuyen edit before any rate source is accepted.
/// </summary>
internal static class CareerManualExchangeRates
{
    internal const decimal NeutralRate = 1m;

    internal static (decimal? WorkingForPeopleRate, decimal? WorkingForManRate) Resolve(
        XElement characterRoot,
        string xml,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        // Fail closed BEFORE accepting resolver-provided rates: a duplicate
        // or nested edition element makes the edition ambiguous, so neither
        // the SR6 neutral fallback nor a resolver row may be trusted.
        if (!TryGetCanonicalEdition(characterRoot, out string? canonicalEdition))
        {
            throw new InvalidOperationException(
                "The workspace declares an ambiguous game edition (duplicate or nested gameedition elements); manual Karma and Nuyen edits are refused.");
        }

        ICharacterSourceDataContext? sourceData = sourceDataResolver?.TryCreateContext(xml);
        if (sourceData is not null
            && sourceData.TryResolveKarmaNuyenExchangeRates(
                out decimal resolvedWorkingForPeopleRate,
                out decimal resolvedWorkingForManRate))
        {
            return (resolvedWorkingForPeopleRate, resolvedWorkingForManRate);
        }

        if (AttributeWorkbenchProjector.IsSr6Ruleset(canonicalEdition))
        {
            return (NeutralRate, NeutralRate);
        }

        return (null, null);
    }

    internal static bool IsSr6Document(XElement characterRoot)
        => TryGetCanonicalEdition(characterRoot, out string? edition)
            && AttributeWorkbenchProjector.IsSr6Ruleset(edition);

    /// <summary>
    /// Reads the single canonical direct-child gameedition element.
    /// Returns false when the edition is ambiguous: more than one direct
    /// child gameedition element (in any order) or an additional nested
    /// gameedition element elsewhere in the document. A missing edition
    /// keeps the legacy no-edition behavior (edition = null); supported
    /// single canonical SR6 aliases stay normalized by the ruleset check.
    /// </summary>
    internal static bool TryGetCanonicalEdition(XElement characterRoot, out string? edition)
    {
        edition = null;
        XElement? direct = null;
        int directCount = 0;
        foreach (XElement element in characterRoot.Elements("gameedition"))
        {
            directCount++;
            direct = element;
        }

        if (directCount > 1)
        {
            return false;
        }

        if (directCount == 1)
        {
            foreach (XElement nested in characterRoot.Descendants("gameedition"))
            {
                if (!ReferenceEquals(nested, direct))
                {
                    return false;
                }
            }

            edition = direct!.Value.Trim();
            return true;
        }

        if (characterRoot.Descendants("gameedition").Any())
        {
            return false;
        }

        return true;
    }
}

internal static class CareerManualKarmaEditorProjector
{
    public static CareerManualKarmaEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing manual Karma.");
        }
        return new CareerManualKarmaEditorState(
            workspaceId,
            contentRevision,
            ProjectState(xml, sourceDataResolver));
    }

    internal static CharacterCareerManualKarmaState ProjectState(
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

        (decimal? workingForPeopleRate, decimal? workingForManRate)
            = CareerManualExchangeRates.Resolve(root, xml, sourceDataResolver);

        if (!CharacterCareerManualKarmaRules.TryProject(
                created,
                availableKarma,
                availableNuyen,
                workingForPeopleRate,
                workingForManRate,
                out CharacterCareerManualKarmaState? state)
            || state is null)
        {
            throw new InvalidOperationException(
                "The saved runner and settings profile do not prove the exact Chummer5 manual-Karma exchange rules.");
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
