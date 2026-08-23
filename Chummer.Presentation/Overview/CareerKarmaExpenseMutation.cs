using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

internal static class CareerKarmaExpenseMutation
{
    public static string Apply(string xml, CareerKarmaExpenseEditRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedExpense);
        (int currentKarma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> expenses)
            = CareerKarmaExpenseEditorProjector.ProjectState(xml);
        if (currentKarma != request.ExpectedAvailableKarma)
        {
            throw new InvalidOperationException(
                "The runner's Karma balance changed while the expense editor was open.");
        }

        CharacterCareerKarmaExpenseEntry[] matches = expenses
            .Where(entry => entry.ExpenseId == request.ExpectedExpense.ExpenseId)
            .Take(2)
            .ToArray();
        if (matches.Length != 1 || matches[0] != request.ExpectedExpense)
        {
            throw new InvalidOperationException(
                "The selected Karma expense changed or disappeared while the editor was open.");
        }
        if (!CharacterCareerKarmaExpenseEditRules.TryEdit(
                matches[0],
                request.Amount,
                request.Reason,
                request.ExpenseDateLocal,
                out CharacterCareerKarmaExpenseEditResult? edit)
            || edit is null)
        {
            throw new InvalidOperationException(
                "The submitted Karma expense edit violates Chummer5's amount, date, reason, or undo-type rules.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root is { } candidate
            && candidate.Name == XName.Get("character")
            ? candidate
            : throw new InvalidOperationException("Workspace XML must use <character> as the root node.");
        XElement[] targets = (root.Element("expenses")?.Elements("expense") ?? [])
            .Where(expense =>
            {
                XElement[] guidElements = expense.Elements("guid").Take(2).ToArray();
                return guidElements.Length == 1
                    && Guid.TryParse(guidElements[0].Value.Trim(), out Guid id)
                    && id == edit.Expense.ExpenseId;
            })
            .Take(2)
            .ToArray();
        if (targets.Length != 1)
        {
            throw new InvalidOperationException("The selected Karma expense identity is ambiguous.");
        }

        XElement target = targets[0];
        target.Elements("date").Single().Value = edit.Expense.ExpenseDateLocal
            .ToString("s", CultureInfo.InvariantCulture);
        XElement reason = target.Elements("reason").SingleOrDefault() ?? new XElement("reason");
        reason.Value = edit.Expense.Reason;
        if (reason.Parent is null)
        {
            target.Add(reason);
        }
        if (edit.KarmaDelta != 0)
        {
            target.Elements("amount").Single().Value = edit.Expense.Amount
                .ToString(CultureInfo.InvariantCulture);
            EnsureElement(root, "karma").Value = checked(currentKarma + edit.KarmaDelta)
                .ToString(CultureInfo.InvariantCulture);
        }

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private static XElement EnsureElement(XElement parent, string name)
    {
        XElement[] elements = parent.Elements(name).Take(2).ToArray();
        if (elements.Length > 1)
        {
            throw new InvalidOperationException($"The saved runner has duplicate <{name}> values.");
        }
        if (elements.Length == 1)
        {
            return elements[0];
        }

        XElement created = new(name);
        parent.Add(created);
        return created;
    }
}
