using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerKarmaExpenseEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    int AvailableKarma,
    IReadOnlyList<CharacterCareerKarmaExpenseEntry> Expenses,
    string ReasonNormalizationLanguage = DesktopLocalizationCatalog.DefaultLanguage);

public sealed record CareerKarmaExpenseEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    int ExpectedAvailableKarma,
    CharacterCareerKarmaExpenseEntry ExpectedExpense,
    decimal Amount,
    string Reason,
    DateTime ExpenseDateLocal,
    string ExpectedReasonNormalizationLanguage = DesktopLocalizationCatalog.DefaultLanguage);

internal interface ICareerExpenseReasonNormalizationAuthority
{
    string LanguageCode { get; }

    string NormalizeLoadedReason(string savedReason);
}

internal sealed class Chummer5CareerExpenseReasonNormalizationAuthority :
    ICareerExpenseReasonNormalizationAuthority
{
    private readonly string _localizedRefundLabel;

    private Chummer5CareerExpenseReasonNormalizationAuthority(
        string languageCode,
        string localizedRefundLabel)
    {
        LanguageCode = languageCode;
        _localizedRefundLabel = localizedRefundLabel;
    }

    public string LanguageCode { get; }

    public static Chummer5CareerExpenseReasonNormalizationAuthority ForLanguage(
        string? languageCode)
    {
        string normalizedLanguage = DesktopLocalizationCatalog.NormalizeOrDefault(languageCode);
        return new Chummer5CareerExpenseReasonNormalizationAuthority(
            normalizedLanguage,
            DesktopLocalizationCatalog.GetChummer5ExpenseRefundLabel(normalizedLanguage));
    }

    public string NormalizeLoadedReason(string savedReason)
    {
        ArgumentNullException.ThrowIfNull(savedReason);
        string refundSuffix = string.Concat(" (", _localizedRefundLabel, ")");
        string normalized = savedReason.EndsWith(refundSuffix, StringComparison.Ordinal)
            ? savedReason[..^refundSuffix.Length]
            : savedReason;
        return normalized.Replace("🡒", "->", StringComparison.Ordinal);
    }
}

internal static class CareerKarmaExpenseEditorProjector
{
    public static CareerKarmaExpenseEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
        => Project(
            xml,
            workspaceId,
            contentRevision,
            Chummer5CareerExpenseReasonNormalizationAuthority.ForLanguage(
                DesktopLocalizationCatalog.GetCurrentLanguage()));

    internal static CareerKarmaExpenseEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision,
        ICareerExpenseReasonNormalizationAuthority reasonNormalizationAuthority)
    {
        ArgumentNullException.ThrowIfNull(reasonNormalizationAuthority);
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for Karma-expense editing.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing Karma expenses.");
        }

        (int karma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> expenses) = ProjectState(
            xml,
            reasonNormalizationAuthority);
        if (expenses.Count == 0)
        {
            throw new InvalidOperationException("The saved career runner has no Karma expense to edit.");
        }

        return new CareerKarmaExpenseEditorState(
            workspaceId,
            contentRevision,
            karma,
            expenses,
            reasonNormalizationAuthority.LanguageCode);
    }

    internal static (int Karma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> Expenses) ProjectState(string xml)
        => ProjectState(
            xml,
            Chummer5CareerExpenseReasonNormalizationAuthority.ForLanguage(
                DesktopLocalizationCatalog.GetCurrentLanguage()));

    internal static (int Karma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> Expenses) ProjectState(
        string xml,
        ICareerExpenseReasonNormalizationAuthority reasonNormalizationAuthority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(reasonNormalizationAuthority);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { } candidate
            && candidate.Name == XName.Get("character")
            ? candidate
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        if (!ReadRequiredBool(root, "created"))
        {
            throw new InvalidOperationException("Karma expense editing is available only for career runners.");
        }

        int karma = ReadOptionalInt(root, "karma");
        XElement[] expenseContainers = root.Elements("expenses").Take(2).ToArray();
        if (expenseContainers.Length > 1)
        {
            throw new InvalidOperationException("The saved runner has duplicate <expenses> containers.");
        }

        List<CharacterCareerKarmaExpenseEntry> entries = [];
        HashSet<Guid> identities = [];
        foreach (XElement expense in expenseContainers.SingleOrDefault()?.Elements("expense") ?? [])
        {
            (bool expenseTypePresent, string? rawExpenseType) = ReadOptionalRawText(
                expense,
                "type",
                "An expense");
            string type = expenseTypePresent ? rawExpenseType! : "Karma";
            Guid id = ReadRequiredGuid(expense, "guid");
            if (!identities.Add(id))
            {
                throw new InvalidOperationException("The saved runner has duplicate expense GUIDs.");
            }
            if (string.Equals(type, "Nuyen", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DateTime date = ReadRequiredDate(expense, "date");
            decimal amount = ReadRequiredDecimal(expense, "amount");
            string reason = reasonNormalizationAuthority.NormalizeLoadedReason(
                ReadOptionalText(expense, "reason", string.Empty));
            (bool refundPresent, bool refund) = ReadOptionalBoolWithPresence(
                expense,
                "refund");
            (bool forceCareerVisiblePresent, bool forceCareerVisible) =
                ReadOptionalBoolWithPresence(expense, "forcecareervisible");
            (
                bool karmaTypePresent,
                string? rawKarmaType,
                CharacterCareerKarmaExpenseSourceAuthority sourceAuthority
            ) = ReadExpenseSourceAuthority(
                expense,
                expenseTypePresent,
                rawExpenseType,
                refundPresent,
                forceCareerVisiblePresent);
            if (!CharacterCareerKarmaExpenseEditRules.TryCreateEntry(
                    id,
                    date,
                    amount,
                    reason,
                    refund,
                    forceCareerVisible,
                    karmaTypePresent,
                    rawKarmaType,
                    sourceAuthority,
                    out CharacterCareerKarmaExpenseEntry? entry)
                || entry is null)
            {
                throw new InvalidOperationException($"Karma expense {id:D} is outside Chummer5's editable bounds.");
            }
            entries.Add(entry);
        }

        return (karma, entries);
    }

    private static (
        bool KarmaTypePresent,
        string? RawKarmaType,
        CharacterCareerKarmaExpenseSourceAuthority SourceAuthority
    ) ReadExpenseSourceAuthority(
        XElement expense,
        bool expenseTypePresent,
        string? rawExpenseType,
        bool refundPresent,
        bool forceCareerVisiblePresent)
    {
        XElement[] undoNodes = expense.Elements("undo").Take(2).ToArray();
        if (undoNodes.Length == 0)
        {
            return (
                false,
                null,
                new CharacterCareerKarmaExpenseSourceAuthority(
                    expenseTypePresent,
                    rawExpenseType,
                    refundPresent,
                    forceCareerVisiblePresent,
                    NuyenUndoTypeElementPresent: false,
                    RawNuyenUndoType: null,
                    UndoObjectIdElementPresent: false,
                    RawUndoObjectId: null,
                    UndoQuantityElementPresent: false,
                    UndoQuantity: null,
                    UndoExtraElementPresent: false,
                    RawUndoExtra: null));
        }
        if (undoNodes.Length != 1)
        {
            throw new InvalidOperationException("A Karma expense has duplicate <undo> values.");
        }

        XElement undo = undoNodes[0];
        (bool karmaTypePresent, string? rawKarmaType) = ReadOptionalRawText(
            undo,
            "karmatype",
            "A Karma expense undo value");
        (bool nuyenTypePresent, string? rawNuyenType) = ReadOptionalRawText(
            undo,
            "nuyentype",
            "A Karma expense undo value");
        (bool objectIdPresent, string? rawObjectId) = ReadOptionalRawText(
            undo,
            "objectid",
            "A Karma expense undo value");
        (bool quantityPresent, string? rawQuantity) = ReadOptionalRawText(
            undo,
            "qty",
            "A Karma expense undo value");
        decimal? quantity = null;
        if (quantityPresent)
        {
            if (!decimal.TryParse(
                    rawQuantity,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out decimal parsedQuantity))
            {
                throw new InvalidOperationException(
                    "A Karma expense has an invalid <qty> undo value.");
            }
            quantity = parsedQuantity;
        }
        (bool extraPresent, string? rawExtra) = ReadOptionalRawText(
            undo,
            "extra",
            "A Karma expense undo value");
        return (
            karmaTypePresent,
            rawKarmaType,
            new CharacterCareerKarmaExpenseSourceAuthority(
                expenseTypePresent,
                rawExpenseType,
                refundPresent,
                forceCareerVisiblePresent,
                nuyenTypePresent,
                rawNuyenType,
                objectIdPresent,
                rawObjectId,
                quantityPresent,
                quantity,
                extraPresent,
                rawExtra));
    }

    private static (bool Present, string? Raw) ReadOptionalRawText(
        XElement parent,
        string name,
        string subject)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        return values.Length switch
        {
            0 => (false, null),
            1 => (true, values[0].Value),
            _ => throw new InvalidOperationException(
                $"{subject} has duplicate <{name}> values.")
        };
    }

    private static Guid ReadRequiredGuid(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !Guid.TryParse(values[0].Value.Trim(), out Guid value) || value == Guid.Empty)
        {
            throw new InvalidOperationException($"An expense has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static DateTime ReadRequiredDate(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !DateTime.TryParse(
                values[0].Value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime value))
        {
            throw new InvalidOperationException($"A Karma expense has an invalid or duplicate <{name}> value.");
        }
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static decimal ReadRequiredDecimal(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1
            || !decimal.TryParse(
                values[0].Value.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            throw new InvalidOperationException($"A Karma expense has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static int ReadOptionalInt(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
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

    private static bool ReadRequiredBool(XElement parent, string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"The saved runner has an invalid or duplicate <{name}> value.");
        }
        return value;
    }

    private static bool ReadOptionalBool(XElement parent, string name)
        => ReadOptionalBoolWithPresence(parent, name).Value;

    private static (bool Present, bool Value) ReadOptionalBoolWithPresence(
        XElement parent,
        string name)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return (false, false);
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"A Karma expense has an invalid or duplicate <{name}> value.");
        }
        return (true, value);
    }

    private static string ReadOptionalText(XElement parent, string name, string fallback)
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return fallback;
        }
        if (values.Length != 1)
        {
            throw new InvalidOperationException($"An expense has duplicate <{name}> values.");
        }
        return values[0].Value;
    }
}
