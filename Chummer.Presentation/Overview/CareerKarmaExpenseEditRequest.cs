using System.Globalization;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CareerKarmaExpenseEditorState(
    CharacterWorkspaceId WorkspaceId,
    long ContentRevision,
    int AvailableKarma,
    IReadOnlyList<CharacterCareerKarmaExpenseEntry> Expenses);

public sealed record CareerKarmaExpenseEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    int ExpectedAvailableKarma,
    CharacterCareerKarmaExpenseEntry ExpectedExpense,
    decimal Amount,
    string Reason,
    DateTime ExpenseDateLocal);

internal static class CareerKarmaExpenseEditorProjector
{
    public static CareerKarmaExpenseEditorState Project(
        string xml,
        CharacterWorkspaceId workspaceId,
        long contentRevision)
    {
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException("Dossier revision is unavailable. Reload before editing Karma expenses.");
        }

        (int karma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> expenses) = ProjectState(xml);
        if (expenses.Count == 0)
        {
            throw new InvalidOperationException("The saved career runner has no Karma expense to edit.");
        }

        return new CareerKarmaExpenseEditorState(workspaceId, contentRevision, karma, expenses);
    }

    internal static (int Karma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> Expenses) ProjectState(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
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
            string type = ReadOptionalText(expense, "type", "Karma");
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
            string reason = ReadOptionalText(expense, "reason", string.Empty);
            bool refund = ReadOptionalBool(expense, "refund");
            bool forceCareerVisible = ReadOptionalBool(expense, "forcecareervisible");
            (bool karmaTypePresent, string? rawKarmaType) = ReadKarmaUndoType(expense);
            if (!CharacterCareerKarmaExpenseEditRules.TryCreateEntry(
                    id,
                    date,
                    amount,
                    reason,
                    refund,
                    forceCareerVisible,
                    karmaTypePresent,
                    rawKarmaType,
                    out CharacterCareerKarmaExpenseEntry? entry)
                || entry is null)
            {
                throw new InvalidOperationException($"Karma expense {id:D} is outside Chummer5's editable bounds.");
            }
            entries.Add(entry);
        }

        return (karma, entries);
    }

    private static (bool Present, string? Raw) ReadKarmaUndoType(XElement expense)
    {
        XElement[] undoNodes = expense.Elements("undo").Take(2).ToArray();
        if (undoNodes.Length == 0)
        {
            return (false, null);
        }
        if (undoNodes.Length != 1)
        {
            throw new InvalidOperationException("A Karma expense has duplicate <undo> values.");
        }

        XElement[] karmaTypeNodes = undoNodes[0].Elements("karmatype").Take(2).ToArray();
        return karmaTypeNodes.Length switch
        {
            0 => (false, null),
            1 => (true, karmaTypeNodes[0].Value),
            _ => throw new InvalidOperationException("A Karma expense has duplicate <karmatype> values.")
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
    {
        XElement[] values = parent.Elements(name).Take(2).ToArray();
        if (values.Length == 0)
        {
            return false;
        }
        if (values.Length != 1 || !bool.TryParse(values[0].Value.Trim(), out bool value))
        {
            throw new InvalidOperationException($"A Karma expense has an invalid or duplicate <{name}> value.");
        }
        return value;
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
