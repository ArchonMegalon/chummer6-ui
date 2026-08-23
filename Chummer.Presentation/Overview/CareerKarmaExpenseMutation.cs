using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

internal static class CareerKarmaExpenseMutation
{
    public static string Apply(string xml, CareerKarmaExpenseEditRequest request)
        => Apply(
            xml,
            request,
            Chummer5CareerExpenseReasonNormalizationAuthority.ForLanguage(
                DesktopLocalizationCatalog.GetCurrentLanguage()));

    internal static string Apply(
        string xml,
        CareerKarmaExpenseEditRequest request,
        ICareerExpenseReasonNormalizationAuthority reasonNormalizationAuthority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedExpense);
        ArgumentNullException.ThrowIfNull(reasonNormalizationAuthority);
        if (string.IsNullOrWhiteSpace(request.WorkspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for Karma-expense editing.");
        }
        if (request.ExpectedContentRevision <= 0)
        {
            throw new InvalidOperationException(
                "A positive dossier revision is required for Karma-expense editing.");
        }
        if (!string.Equals(
                request.ExpectedReasonNormalizationLanguage,
                reasonNormalizationAuthority.LanguageCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Karma-expense reason normalization language is unavailable or changed.");
        }

        (int currentKarma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> expenses)
            = CareerKarmaExpenseEditorProjector.ProjectState(xml, reasonNormalizationAuthority);
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
        Dictionary<Guid, XElement> nonTargetExpenseSnapshots = (root.Element("expenses")?.Elements("expense") ?? [])
            .Select(expense => (Id: ReadExpenseId(expense), Snapshot: new XElement(expense)))
            .Where(candidate => candidate.Id != edit.Expense.ExpenseId)
            .ToDictionary(candidate => candidate.Id, candidate => candidate.Snapshot);
        int expectedKarma = checked(currentKarma + edit.KarmaDelta);
        target.Elements("date").Single().Value = edit.Expense.ExpenseDateLocal
            .ToString("s", CultureInfo.InvariantCulture);
        XElement reason = target.Elements("reason").SingleOrDefault() ?? new XElement("reason");
        reason.Value = edit.Expense.Reason;
        if (reason.Parent is null)
        {
            target.Elements("amount").Single().AddAfterSelf(reason);
        }
        if (edit.KarmaDelta != 0)
        {
            target.Elements("amount").Single().Value = edit.Expense.Amount
                .ToString(CultureInfo.InvariantCulture);
            EnsureElement(root, "karma").Value = expectedKarma
                .ToString(CultureInfo.InvariantCulture);
        }

        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        string serialized = writer.ToString();
        (int serializedKarma, IReadOnlyList<CharacterCareerKarmaExpenseEntry> serializedExpenses)
            = CareerKarmaExpenseEditorProjector.ProjectState(serialized, reasonNormalizationAuthority);
        CharacterCareerKarmaExpenseEntry expectedProjectedExpense = edit.Expense with
        {
            Reason = reasonNormalizationAuthority.NormalizeLoadedReason(edit.Expense.Reason)
        };
        CharacterCareerKarmaExpenseEntry[] expectedExpenses = expenses
            .Select(expense => expense.ExpenseId == edit.Expense.ExpenseId
                ? expectedProjectedExpense
                : expense)
            .ToArray();
        if (serializedKarma != expectedKarma
            || !serializedExpenses.SequenceEqual(expectedExpenses))
        {
            throw new InvalidOperationException(
                "The serialized Karma expense edit did not preserve its projected balance and full-record authority.");
        }

        VerifyNonTargetExpenseStructure(
            serialized,
            edit.Expense.ExpenseId,
            nonTargetExpenseSnapshots);
        return serialized;
    }

    private static Guid ReadExpenseId(XElement expense)
    {
        XElement[] values = expense.Elements("guid").Take(2).ToArray();
        if (values.Length != 1
            || !Guid.TryParse(values[0].Value.Trim(), out Guid id)
            || id == Guid.Empty)
        {
            throw new InvalidOperationException("An expense has an invalid or duplicate <guid> value.");
        }

        return id;
    }

    private static void VerifyNonTargetExpenseStructure(
        string serialized,
        Guid targetExpenseId,
        IReadOnlyDictionary<Guid, XElement> expectedSnapshots)
    {
        XDocument result = XDocument.Parse(serialized, LoadOptions.PreserveWhitespace);
        XElement root = result.Root!;
        Dictionary<Guid, XElement> actualSnapshots = (root.Element("expenses")?.Elements("expense") ?? [])
            .Select(expense => (Id: ReadExpenseId(expense), Snapshot: expense))
            .Where(candidate => candidate.Id != targetExpenseId)
            .ToDictionary(candidate => candidate.Id, candidate => candidate.Snapshot);
        if (actualSnapshots.Count != expectedSnapshots.Count
            || expectedSnapshots.Any(expected =>
                !actualSnapshots.TryGetValue(expected.Key, out XElement? actual)
                || !XNode.DeepEquals(expected.Value, actual)))
        {
            throw new InvalidOperationException(
                "The serialized Karma expense edit changed a non-target expense record.");
        }
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
