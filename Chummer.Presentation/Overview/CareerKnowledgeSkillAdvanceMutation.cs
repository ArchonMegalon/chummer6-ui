using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

internal static class CareerKnowledgeSkillAdvanceMutation
{
    internal const string ReceiptContainerName = "careerknowledgeskilladvancementreceipts";

    internal sealed record ReceiptRecovery(
        IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceReceipt> Receipts,
        int OmittedCount);

    private sealed record ExpenseSortRow(
        XElement Expense,
        DateTime Date,
        int Type,
        string Reason,
        bool Refund,
        decimal Amount,
        bool ForceCareerVisible);

    public static CareerKnowledgeSkillAdvanceMutationResult Apply(
        string xml,
        CareerKnowledgeSkillAdvanceRequest request,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedSkill);
        ValidateRequestAuthority(request);
        ValidateExpectedQuote(request);

        (IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> current, _) =
            CareerKnowledgeSkillAdvanceEditorProjector.ProjectState(
                xml,
                settingsCatalogJson,
                sourceDataResolver);
        ReceiptRecovery existing = RecoverReceipts(xml, current);
        CharacterCareerKnowledgeSkillAdvanceReceipt[] replay = existing.Receipts
            .Where(receipt => receipt.TransactionId == request.ExpenseId)
            .Take(2)
            .ToArray();
        if (replay.Length == 1 && ReceiptMatchesRequest(replay[0], request))
        {
            return new CareerKnowledgeSkillAdvanceMutationResult(xml, replay[0]);
        }
        if (replay.Length != 0)
        {
            throw new InvalidOperationException(
                "The requested knowledge-skill transaction identity is already bound to a different receipt.");
        }

        CharacterCareerKnowledgeSkillAdvanceQuote reviewed = ResolveQuote(
            current,
            request.ExpectedSkill.Identity,
            "The selected knowledge or language skill changed or disappeared while advancement was open.");
        if (!QuoteMatches(reviewed, request.ExpectedSkill))
        {
            throw new InvalidOperationException(
                "The selected knowledge or language skill changed while advancement was open.");
        }
        if (!CharacterCareerKnowledgeSkillAdvanceRules.TryPlanAdvance(
                reviewed,
                request.ExpectedCharacterRevision,
                request.ExpectedLogicalRevision,
                request.ExpectedSourceRevision,
                request.ExpectedRuleDigest,
                request.Confirmed,
                request.ExpenseId,
                request.ExpenseDateLocal,
                out CharacterCareerKnowledgeSkillAdvancePlan plan))
        {
            throw new InvalidOperationException(
                "Knowledge-skill advancement requires explicit confirmation, unchanged character/source/rule digests, sufficient Karma, and a fresh expense identity.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(document);
        XElement target = ResolveSkillElement(root, plan.Identity);
        SetRequiredValue(
            target,
            "karma",
            plan.SavedSkillKarmaPoints.ToString(CultureInfo.InvariantCulture),
            "The selected knowledge skill");
        SetRequiredValue(
            root,
            "karma",
            plan.SavedCharacterKarma.ToString(CultureInfo.InvariantCulture),
            "The saved runner");
        AddExpense(root, plan);

        string staged = Serialize(document);
        (IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> afterQuotes, _) =
            CareerKnowledgeSkillAdvanceEditorProjector.ProjectState(
                staged,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerKnowledgeSkillAdvanceQuote after = ResolveQuote(
            afterQuotes,
            plan.Identity,
            "The advanced knowledge or language skill no longer resolves from exact saved authority.");
        XDocument verifiedDocument = XDocument.Parse(staged, LoadOptions.PreserveWhitespace);
        XElement verifiedRoot = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(
            verifiedDocument);
        XElement[] expenses = FindExpenses(verifiedRoot, plan.ExpenseId);
        bool expenseExactlyOnce = expenses.Length == 1 && ExpenseMatches(expenses[0], plan);
        if (after.BasePoints != reviewed.BasePoints
            || after.KarmaPoints != plan.SavedSkillKarmaPoints
            || after.TotalBaseRating != reviewed.TotalBaseRating + 1
            || after.AvailableKarma != plan.SavedCharacterKarma
            || after.Identity != reviewed.Identity
            || !string.Equals(after.Name, reviewed.Name, StringComparison.Ordinal)
            || !string.Equals(after.SkillType, reviewed.SkillType, StringComparison.Ordinal)
            || !string.Equals(after.SourceRevision, reviewed.SourceRevision, StringComparison.Ordinal)
            || !expenseExactlyOnce)
        {
            throw new InvalidOperationException(
                "The serialized knowledge-skill advancement did not preserve its reviewed identity, rating, Karma, source, and expense authority.");
        }
        if (!CharacterCareerKnowledgeSkillAdvanceRules.TryCreateReceipt(
                plan.ExpenseId,
                reviewed,
                plan,
                after.KarmaPoints,
                after.AvailableKarma,
                expenseExactlyOnce,
                out CharacterCareerKnowledgeSkillAdvanceReceipt receipt))
        {
            throw new InvalidOperationException(
                "The persisted knowledge-skill advancement could not be bound to an exact receipt.");
        }

        AddReceiptAudit(verifiedRoot, receipt, after);
        string serialized = Serialize(verifiedDocument);
        (IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> finalQuotes, _) =
            CareerKnowledgeSkillAdvanceEditorProjector.ProjectState(
                serialized,
                settingsCatalogJson,
                sourceDataResolver);
        ReceiptRecovery recovered = RecoverReceipts(serialized, finalQuotes);
        CharacterCareerKnowledgeSkillAdvanceReceipt[] persisted = recovered.Receipts
            .Where(candidate => candidate.TransactionId == receipt.TransactionId)
            .Take(2)
            .ToArray();
        if (persisted.Length != 1 || persisted[0] != receipt)
        {
            throw new InvalidOperationException(
                "The knowledge-skill advancement receipt was not persisted exactly once.");
        }
        return new CareerKnowledgeSkillAdvanceMutationResult(serialized, receipt);
    }

    internal static ReceiptRecovery RecoverReceipts(
        string xml,
        IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(current);
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerActiveSkillAdvanceEditorProjector.RequireCharacterRoot(document);
        XElement[] containers = root.Elements(ReceiptContainerName).Take(2).ToArray();
        if (containers.Length == 0)
        {
            return new ReceiptRecovery([], 0);
        }
        if (containers.Length != 1)
        {
            return new ReceiptRecovery([], containers.Sum(static value => value.Elements("receipt").Count()));
        }

        XElement[] audits = containers[0].Elements("receipt").ToArray();
        Dictionary<Guid, int> counts = [];
        foreach (XElement audit in audits)
        {
            if (Guid.TryParse(ReadAttribute(audit, "transactionId"), out Guid id))
            {
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }

        List<CharacterCareerKnowledgeSkillAdvanceReceipt> receipts = [];
        int omitted = 0;
        foreach (XElement audit in audits)
        {
            try
            {
                CharacterCareerKnowledgeSkillAdvanceReceipt receipt = ParseReceiptAudit(audit);
                if (counts.GetValueOrDefault(receipt.TransactionId) != 1
                    || !CharacterCareerKnowledgeSkillAdvanceRules.IsCoherent(receipt))
                {
                    omitted++;
                    continue;
                }
                CharacterCareerKnowledgeSkillAdvanceQuote observed = ResolveQuote(
                    current,
                    receipt.Identity,
                    "The receipt target no longer resolves from exact saved authority.");
                XElement[] expenses = FindExpenses(root, receipt.ExpenseId);
                if (expenses.Length != 1
                    || !ExpenseMatchesReceipt(expenses[0], receipt)
                    || !ReceiptAuditMatches(audit, receipt, observed))
                {
                    omitted++;
                    continue;
                }
                receipts.Add(receipt);
            }
            catch (InvalidOperationException)
            {
                omitted++;
            }
        }
        return new ReceiptRecovery(receipts, omitted);
    }

    private static CharacterCareerKnowledgeSkillAdvanceQuote ResolveQuote(
        IReadOnlyList<CharacterCareerKnowledgeSkillAdvanceQuote> quotes,
        CharacterCareerKnowledgeSkillIdentity identity,
        string error)
    {
        CharacterCareerKnowledgeSkillAdvanceQuote[] matches = quotes
            .Where(candidate => candidate.Identity == identity)
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(error);
    }

    private static bool QuoteMatches(
        CharacterCareerKnowledgeSkillAdvanceQuote current,
        CharacterCareerKnowledgeSkillAdvanceQuote expected)
        => current.Identity == expected.Identity
            && string.Equals(current.Name, expected.Name, StringComparison.Ordinal)
            && string.Equals(current.SkillType, expected.SkillType, StringComparison.Ordinal)
            && string.Equals(current.SkillCategory, expected.SkillCategory, StringComparison.Ordinal)
            && current.AllowUpgrade == expected.AllowUpgrade
            && current.IsNativeLanguage == expected.IsNativeLanguage
            && current.BasePoints == expected.BasePoints
            && current.KarmaPoints == expected.KarmaPoints
            && current.TotalBaseRating == expected.TotalBaseRating
            && current.RatingMaximum == expected.RatingMaximum
            && current.AvailableKarma == expected.AvailableKarma
            && current.KarmaCost == expected.KarmaCost
            && current.ApplicationDuration == expected.ApplicationDuration
            && current.TimeAuthority == expected.TimeAuthority
            && current.Prerequisites.SequenceEqual(expected.Prerequisites)
            && current.CanAdvance == expected.CanAdvance
            && current.Blocker == expected.Blocker
            && string.Equals(current.CharacterRevision, expected.CharacterRevision, StringComparison.Ordinal)
            && string.Equals(current.LogicalRevision, expected.LogicalRevision, StringComparison.Ordinal)
            && string.Equals(current.SourceRevision, expected.SourceRevision, StringComparison.Ordinal)
            && string.Equals(current.RuleDigest, expected.RuleDigest, StringComparison.Ordinal);

    private static bool ReceiptMatchesRequest(
        CharacterCareerKnowledgeSkillAdvanceReceipt receipt,
        CareerKnowledgeSkillAdvanceRequest request)
        => receipt.Identity == request.ExpectedSkill.Identity
            && string.Equals(receipt.Name, request.ExpectedSkill.Name, StringComparison.Ordinal)
            && string.Equals(receipt.SkillType, request.ExpectedSkill.SkillType, StringComparison.Ordinal)
            && string.Equals(receipt.CharacterRevision, request.ExpectedCharacterRevision, StringComparison.Ordinal)
            && string.Equals(receipt.LogicalRevision, request.ExpectedLogicalRevision, StringComparison.Ordinal)
            && string.Equals(receipt.SourceRevision, request.ExpectedSourceRevision, StringComparison.Ordinal)
            && string.Equals(receipt.RuleDigest, request.ExpectedRuleDigest, StringComparison.Ordinal);

    private static XElement ResolveSkillElement(
        XElement root,
        CharacterCareerKnowledgeSkillIdentity identity)
    {
        XElement newSkills = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            root,
            "newskills",
            "The saved runner must have one <newskills> container.");
        XElement skills = CareerActiveSkillAdvanceEditorProjector.RequireSingle(
            newSkills,
            "knoskills",
            "The saved runner must have one knowledge <knoskills> container.");
        XElement[] matches = skills.Elements("skill")
            .Where(candidate => MatchesIdentity(candidate, identity))
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "The selected knowledge-skill identity is ambiguous.");
    }

    private static bool MatchesIdentity(
        XElement candidate,
        CharacterCareerKnowledgeSkillIdentity identity)
    {
        XElement[] ids = candidate.Elements("guid").Take(2).ToArray();
        XElement[] sourceIds = candidate.Elements("suid").Take(2).ToArray();
        if (ids.Length != 1
            || sourceIds.Length != 1
            || !Guid.TryParse(ids[0].Value.Trim(), out Guid id)
            || !Guid.TryParse(sourceIds[0].Value.Trim(), out Guid sourceId)
            || id != identity.SkillId)
        {
            return false;
        }
        return identity.SourceSkillId is { } expected
            ? sourceId == expected
            : sourceId == Guid.Empty;
    }

    private static void AddExpense(
        XElement root,
        CharacterCareerKnowledgeSkillAdvancePlan plan)
    {
        XElement[] containers = root.Elements("expenses").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <expenses> containers.");
        }
        XElement expenses = containers.SingleOrDefault() ?? new XElement("expenses");
        if (expenses.Parent is null)
        {
            root.Add(expenses);
        }
        if (FindExpenses(root, plan.ExpenseId).Length != 0)
        {
            throw new InvalidOperationException(
                "The requested career-expense GUID already exists.");
        }

        expenses.Add(new XElement(
            "expense",
            new XElement("guid", plan.ExpenseId.ToString("D")),
            new XElement("date", plan.ExpenseDateLocal.ToString("s", CultureInfo.InvariantCulture)),
            new XElement("amount", plan.ExpenseAmount.ToString(CultureInfo.InvariantCulture)),
            new XElement("reason", plan.ExpenseReason),
            new XElement("type", "Karma"),
            new XElement("refund", "False"),
            new XElement("forcecareervisible", "False"),
            new XElement(
                "undo",
                new XElement("karmatype", plan.KarmaUndoType),
                new XElement("nuyentype", plan.NuyenUndoType),
                new XElement("objectid", plan.UndoObjectId),
                new XElement("qty", plan.UndoQuantity.ToString(CultureInfo.InvariantCulture)),
                new XElement("extra", plan.UndoExtra))));

        XElement[] ordered = expenses.Elements("expense")
            .Select(ProjectExpenseSortRow)
            .OrderByDescending(static candidate => candidate.Date)
            .ThenByDescending(static candidate => candidate.Type)
            .ThenByDescending(static candidate => candidate.Reason, StringComparer.Ordinal)
            .ThenByDescending(static candidate => candidate.Refund)
            .ThenByDescending(static candidate => candidate.Amount)
            .ThenByDescending(static candidate => candidate.ForceCareerVisible)
            .Select(static candidate => candidate.Expense)
            .ToArray();
        foreach (XElement expense in ordered)
        {
            expense.Remove();
        }
        expenses.Add(ordered);
    }

    private static XElement[] FindExpenses(XElement root, Guid id)
    {
        XElement[] containers = root.Elements("expenses").Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate <expenses> containers.");
        }
        return containers.SingleOrDefault()?.Elements("expense")
            .Where(expense => Guid.TryParse(
                    CareerActiveSkillAdvanceEditorProjector.ReadRequiredText(
                        expense,
                        "guid",
                        "A career expense"),
                    out Guid expenseId)
                && expenseId == id)
            .Take(2)
            .ToArray() ?? [];
    }

    private static bool ExpenseMatches(
        XElement expense,
        CharacterCareerKnowledgeSkillAdvancePlan plan)
        => string.Equals(ReadRequiredExpenseText(expense, "type"), "Karma", StringComparison.Ordinal)
            && int.TryParse(
                ReadRequiredExpenseText(expense, "amount"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int amount)
            && amount == plan.ExpenseAmount
            && string.Equals(
                ReadRequiredExpenseText(expense, "reason"),
                plan.ExpenseReason,
                StringComparison.Ordinal)
            && string.Equals(ReadRequiredExpenseText(expense, "refund"), "False", StringComparison.Ordinal);

    private static bool ExpenseMatchesReceipt(
        XElement expense,
        CharacterCareerKnowledgeSkillAdvanceReceipt receipt)
        => string.Equals(ReadRequiredExpenseText(expense, "type"), "Karma", StringComparison.Ordinal)
            && int.TryParse(
                ReadRequiredExpenseText(expense, "amount"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int amount)
            && amount == receipt.ExpenseAmount
            && string.Equals(ReadRequiredExpenseText(expense, "refund"), "False", StringComparison.Ordinal);

    private static ExpenseSortRow ProjectExpenseSortRow(XElement expense)
    {
        _ = CareerActiveSkillAdvanceEditorProjector.ReadRequiredGuid(
            expense,
            "guid",
            "A career expense");
        string type = ReadRequiredExpenseText(expense, "type");
        int typeOrder = type switch
        {
            "Karma" => 0,
            "Nuyen" => 1,
            _ => throw new InvalidOperationException(
                "A career expense has an invalid <type> value.")
        };
        if (!bool.TryParse(ReadRequiredExpenseText(expense, "refund"), out bool refund)
            || !decimal.TryParse(
                ReadRequiredExpenseText(expense, "amount"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal amount)
            || !bool.TryParse(
                ReadRequiredExpenseText(expense, "forcecareervisible"),
                out bool forceCareerVisible))
        {
            throw new InvalidOperationException(
                "A career expense has invalid sorting values.");
        }
        return new ExpenseSortRow(
            expense,
            ReadExpenseDate(expense),
            typeOrder,
            ReadRequiredExpenseText(expense, "reason"),
            refund,
            amount,
            forceCareerVisible);
    }

    private static DateTime ReadExpenseDate(XElement expense)
    {
        XElement[] values = expense.Elements("date").Take(2).ToArray();
        if (values.Length != 1
            || !DateTime.TryParse(
                values[0].Value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime date))
        {
            throw new InvalidOperationException(
                "A career expense has an invalid or duplicate <date> value.");
        }
        return date;
    }

    private static string ReadRequiredExpenseText(XElement expense, string name)
    {
        XElement[] values = expense.Elements(name).Take(2).ToArray();
        return values.Length == 1
            ? values[0].Value
            : throw new InvalidOperationException(
                $"A career expense has a missing or duplicate <{name}> value.");
    }

    private static void AddReceiptAudit(
        XElement root,
        CharacterCareerKnowledgeSkillAdvanceReceipt receipt,
        CharacterCareerKnowledgeSkillAdvanceQuote observed)
    {
        XElement[] containers = root.Elements(ReceiptContainerName).Take(2).ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate knowledge-skill receipt containers.");
        }
        XElement container = containers.SingleOrDefault() ?? new XElement(ReceiptContainerName);
        if (container.Parent is null)
        {
            root.Add(container);
        }
        if (container.Elements("receipt").Any(candidate =>
                string.Equals(
                    ReadAttribute(candidate, "transactionId"),
                    receipt.TransactionId.ToString("D"),
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The knowledge-skill transaction already has a persisted receipt.");
        }

        container.Add(new XElement(
            "receipt",
            new XAttribute("transactionId", receipt.TransactionId.ToString("D")),
            new XAttribute("skillId", receipt.Identity.SkillId.ToString("D")),
            new XAttribute("sourceSkillId", receipt.Identity.SourceSkillId?.ToString("D") ?? "custom"),
            new XAttribute("name", receipt.Name),
            new XAttribute("skillType", receipt.SkillType),
            new XAttribute("skillKarmaBefore", receipt.SkillKarmaBefore),
            new XAttribute("skillKarmaAfter", receipt.SkillKarmaAfter),
            new XAttribute("characterKarmaBefore", receipt.CharacterKarmaBefore),
            new XAttribute("characterKarmaAfter", receipt.CharacterKarmaAfter),
            new XAttribute("expenseId", receipt.ExpenseId.ToString("D")),
            new XAttribute("expenseAmount", receipt.ExpenseAmount),
            new XAttribute("characterRevision", receipt.CharacterRevision),
            new XAttribute("logicalRevision", receipt.LogicalRevision),
            new XAttribute("sourceRevision", receipt.SourceRevision),
            new XAttribute("ruleDigest", receipt.RuleDigest),
            new XAttribute("receiptDigest", receipt.ReceiptDigest),
            new XAttribute("postCharacterRevision", observed.CharacterRevision),
            new XAttribute("postLogicalRevision", observed.LogicalRevision),
            new XAttribute("postSourceRevision", observed.SourceRevision),
            new XAttribute("postRuleDigest", observed.RuleDigest)));
    }

    private static CharacterCareerKnowledgeSkillAdvanceReceipt ParseReceiptAudit(XElement audit)
    {
        string rawSourceId = ReadAttribute(audit, "sourceSkillId");
        Guid? sourceId = string.Equals(rawSourceId, "custom", StringComparison.Ordinal)
            ? null
            : ParseGuid(rawSourceId, "sourceSkillId");
        return new CharacterCareerKnowledgeSkillAdvanceReceipt(
            ParseGuid(ReadAttribute(audit, "transactionId"), "transactionId"),
            new CharacterCareerKnowledgeSkillIdentity(
                ParseGuid(ReadAttribute(audit, "skillId"), "skillId"),
                sourceId),
            ReadAttribute(audit, "name"),
            ReadAttribute(audit, "skillType"),
            ParseInt(ReadAttribute(audit, "skillKarmaBefore"), "skillKarmaBefore"),
            ParseInt(ReadAttribute(audit, "skillKarmaAfter"), "skillKarmaAfter"),
            ParseInt(ReadAttribute(audit, "characterKarmaBefore"), "characterKarmaBefore"),
            ParseInt(ReadAttribute(audit, "characterKarmaAfter"), "characterKarmaAfter"),
            ParseGuid(ReadAttribute(audit, "expenseId"), "expenseId"),
            ParseInt(ReadAttribute(audit, "expenseAmount"), "expenseAmount"),
            ReadAttribute(audit, "characterRevision"),
            ReadAttribute(audit, "logicalRevision"),
            ReadAttribute(audit, "sourceRevision"),
            ReadAttribute(audit, "ruleDigest"),
            ReadAttribute(audit, "receiptDigest"));
    }

    private static bool ReceiptAuditMatches(
        XElement audit,
        CharacterCareerKnowledgeSkillAdvanceReceipt receipt,
        CharacterCareerKnowledgeSkillAdvanceQuote observed)
        => receipt.Identity == observed.Identity
            && receipt.SkillKarmaAfter == observed.KarmaPoints
            && receipt.CharacterKarmaAfter == observed.AvailableKarma
            && string.Equals(receipt.Name, observed.Name, StringComparison.Ordinal)
            && string.Equals(receipt.SkillType, observed.SkillType, StringComparison.Ordinal)
            && string.Equals(ReadAttribute(audit, "postCharacterRevision"), observed.CharacterRevision, StringComparison.Ordinal)
            && string.Equals(ReadAttribute(audit, "postLogicalRevision"), observed.LogicalRevision, StringComparison.Ordinal)
            && string.Equals(ReadAttribute(audit, "postSourceRevision"), observed.SourceRevision, StringComparison.Ordinal)
            && string.Equals(ReadAttribute(audit, "postRuleDigest"), observed.RuleDigest, StringComparison.Ordinal);

    private static string ReadAttribute(XElement element, string name)
    {
        XAttribute? value = element.Attribute(name);
        return value is not null
            ? value.Value
            : throw new InvalidOperationException(
                $"A knowledge-skill receipt has no {name} authority.");
    }

    private static Guid ParseGuid(string raw, string name)
        => Guid.TryParse(raw, out Guid value) && value != Guid.Empty
            ? value
            : throw new InvalidOperationException(
                $"A knowledge-skill receipt has invalid {name} authority.");

    private static int ParseInt(string raw, string name)
        => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new InvalidOperationException(
                $"A knowledge-skill receipt has invalid {name} authority.");

    private static void SetRequiredValue(
        XElement parent,
        string name,
        string value,
        string owner)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"{owner} has a missing or duplicate <{name}> value.");
        }
        matches[0].Value = value;
    }

    private static void ValidateRequestAuthority(CareerKnowledgeSkillAdvanceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for knowledge-skill advancement.");
        }
        if (request.ExpectedContentRevision <= 0)
        {
            throw new InvalidOperationException(
                "A positive dossier revision is required for knowledge-skill advancement.");
        }
    }

    private static void ValidateExpectedQuote(CareerKnowledgeSkillAdvanceRequest request)
    {
        if (!string.Equals(request.ExpectedCharacterRevision, request.ExpectedSkill.CharacterRevision, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedLogicalRevision, request.ExpectedSkill.LogicalRevision, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedSourceRevision, request.ExpectedSkill.SourceRevision, StringComparison.Ordinal)
            || !string.Equals(request.ExpectedRuleDigest, request.ExpectedSkill.RuleDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The requested knowledge-skill digests do not match the reviewed quote.");
        }
    }

    private static string Serialize(XDocument document)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }
}
