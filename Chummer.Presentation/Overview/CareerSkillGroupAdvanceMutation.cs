using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

internal static class CareerSkillGroupAdvanceMutation
{
    internal sealed record ReceiptRecovery(
        IReadOnlyList<CharacterCareerSkillGroupAdvanceReceipt> Receipts,
        int OmittedCount);

    private sealed record ExpenseSortRow(
        XElement Expense,
        DateTime Date,
        int Type,
        string Reason,
        bool Refund,
        decimal Amount,
        bool ForceCareerVisible);

    public static CareerSkillGroupAdvanceMutationResult Apply(
        string xml,
        CareerSkillGroupAdvanceRequest request,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedSkillGroup);
        ValidateRequestAuthority(request.WorkspaceId.Value, request.ExpectedContentRevision);
        ValidateExpectedQuote(request);

        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> current, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                xml,
                request.ExpectedRulesetId,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote reviewed = ResolveQuote(
            current,
            request.ExpectedSkillGroup.Identity,
            "The selected skill group changed or disappeared while advancement was open.");
        if (!QuoteMatches(reviewed, request.ExpectedSkillGroup))
        {
            throw new InvalidOperationException(
                "The selected skill group changed while advancement was open.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(document);
        bool transactionIdAlreadyExists = TransactionIdExists(root, request.ExpenseId);
        if (!CharacterCareerSkillGroupAdvanceRules.TryPlanAdvance(
                reviewed,
                request.ExpectedLogicalRevision,
                request.ExpectedSourceRevision,
                request.ExpectedRuleDigest,
                request.Confirmed,
                transactionIdAlreadyExists,
                request.ExpenseId,
                request.ExpenseDateLocal,
                out CharacterCareerSkillGroupAdvancePlan plan))
        {
            throw new InvalidOperationException(
                "Skill-group advancement requires explicit confirmation, an unchanged SR5 career quote, exact member authority, sufficient Karma, and a fresh transaction identity.");
        }

        XElement target = ResolveSkillGroupElement(root, plan.Identity);
        SetRequiredValue(
            target,
            "karma",
            plan.SavedGroupKarmaPoints,
            "The selected skill group");
        SetRequiredValue(
            root,
            "karma",
            plan.SavedCharacterKarma,
            "The saved runner");
        AddExpense(root, plan);

        string stagedXml = Serialize(document);
        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> afterQuotes, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                stagedXml,
                request.ExpectedRulesetId,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote after = ResolveQuote(
            afterQuotes,
            plan.Identity,
            "The advanced skill group no longer resolves from exact saved authority.");
        XDocument verifiedDocument = XDocument.Parse(
            stagedXml,
            LoadOptions.PreserveWhitespace);
        XElement verifiedRoot = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(
            verifiedDocument);
        CharacterCareerSkillGroupExpenseObservation observedExpense =
            ObserveExpense(verifiedRoot, plan.ExpenseId);
        if (after.BasePoints != reviewed.BasePoints
            || after.KarmaPoints != plan.SavedGroupKarmaPoints
            || after.GroupRating != plan.TargetGroupRating
            || after.CostRating != plan.TargetCostRating
            || after.EnabledMemberCount != plan.EnabledMemberCount
            || after.AvailableKarma != plan.SavedCharacterKarma)
        {
            throw new InvalidOperationException(
                "The serialized skill-group advancement did not preserve its reviewed rating, member, and Karma authority.");
        }
        if (!CharacterCareerSkillGroupAdvanceRules.TryCreateReceipt(
                plan.TransactionId,
                reviewed,
                plan,
                after,
                observedExpense,
                out CharacterCareerSkillGroupAdvanceReceipt receipt))
        {
            throw new InvalidOperationException(
                "The persisted skill-group advancement could not be bound to an exact Core receipt.");
        }

        AddReceiptAudit(verifiedRoot, receipt, after);
        string serialized = Serialize(verifiedDocument);
        ReceiptRecovery recovery = RecoverReceipts(serialized, afterQuotes);
        CharacterCareerSkillGroupAdvanceReceipt[] matches = recovery.Receipts
            .Where(candidate => candidate.TransactionId == receipt.TransactionId)
            .Take(2)
            .ToArray();
        if (recovery.OmittedCount != 0
            || matches.Length != 1
            || matches[0] != receipt)
        {
            throw new InvalidOperationException(
                "The skill-group advancement receipt was not durably recoverable exactly once.");
        }

        return new CareerSkillGroupAdvanceMutationResult(serialized, receipt);
    }

    public static CareerSkillGroupCorrectionMutationResult Correct(
        string xml,
        CareerSkillGroupCorrectionRequest request,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.OriginalReceipt);
        ValidateRequestAuthority(request.WorkspaceId.Value, request.ExpectedContentRevision);
        ValidateExpectedRuleset(request.ExpectedRulesetId);
        if (!request.Confirmed)
        {
            throw new InvalidOperationException(
                "A compensating skill-group correction requires explicit confirmation.");
        }

        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> current, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                xml,
                request.ExpectedRulesetId,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote observed = ResolveQuote(
            current,
            request.OriginalReceipt.Identity,
            "The corrected skill group no longer resolves from exact saved authority.");
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(document);
        bool originalTransactionAlreadyCorrected = ReadCorrectedTransactionIds(
                root,
                current)
            .Contains(request.OriginalReceipt.TransactionId);
        CharacterCareerSkillGroupExpenseObservation observedExpense = ObserveExpense(
            root,
            request.OriginalReceipt.ExpenseId);
        XElement[] receiptAudits = FindReceiptAudits(
            root,
            request.OriginalReceipt.TransactionId);
        bool persistedReceiptMatches = receiptAudits.Length == 1
            && ReceiptAuditMatches(receiptAudits[0], request.OriginalReceipt, observed);
        bool correctionIdAlreadyExists = TransactionIdExists(root, request.CorrectionId);

        if (!persistedReceiptMatches
            || !CharacterCareerSkillGroupAdvanceRules.TryPlanCorrection(
                request.OriginalReceipt,
                observed,
                observedExpense,
                request.CorrectionId,
                request.Reason,
                correctionIdAlreadyExists,
                originalTransactionAlreadyCorrected,
                request.ExpectedReceiptDigest,
                out CharacterCareerSkillGroupCorrectionPlan correction))
        {
            throw new InvalidOperationException(
                "The skill-group correction is stale, foreign, already applied, or no longer matches its exact receipt and expense.");
        }

        XElement target = ResolveSkillGroupElement(root, correction.Identity);
        SetRequiredValue(
            target,
            "karma",
            correction.SavedGroupKarmaPoints,
            "The selected skill group");
        SetRequiredValue(
            root,
            "karma",
            correction.SavedCharacterKarma,
            "The saved runner");
        XElement[] expenses = FindExpenses(root, correction.ExpenseIdToRemove);
        if (expenses.Length != 1)
        {
            throw new InvalidOperationException(
                "The skill-group correction requires exactly one bound expense.");
        }
        expenses[0].Remove();
        AddCorrectionAudit(root, correction);

        string serialized = Serialize(document);
        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> revertedQuotes, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                serialized,
                request.ExpectedRulesetId,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote reverted = ResolveQuote(
            revertedQuotes,
            correction.Identity,
            "The corrected skill group no longer resolves from exact saved authority.");
        XDocument verifiedDocument = XDocument.Parse(
            serialized,
            LoadOptions.PreserveWhitespace);
        XElement verifiedRoot = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(
            verifiedDocument);
        if (reverted.KarmaPoints != correction.SavedGroupKarmaPoints
            || reverted.AvailableKarma != correction.SavedCharacterKarma
            || reverted.GroupRating != correction.RestoredGroupRating
            || reverted.CostRating != correction.RestoredCostRating
            || FindExpenses(verifiedRoot, correction.ExpenseIdToRemove).Length != 0
            || CountCorrections(verifiedRoot, correction.CorrectionId) != 1
            || !ReadCorrectedTransactionIds(verifiedRoot, revertedQuotes)
                .Contains(correction.OriginalTransactionId))
        {
            throw new InvalidOperationException(
                "The serialized skill-group correction did not exactly restore saved Karma and correction authority.");
        }

        return new CareerSkillGroupCorrectionMutationResult(serialized, correction);
    }

    internal static ReceiptRecovery RecoverReceipts(
        string xml,
        IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> current)
    {
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(document);
        XElement[] ledgers = root.Elements("careerskillgroupadvancementreceipts")
            .Take(2)
            .ToArray();
        if (ledgers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group receipt ledgers.");
        }
        XElement[] elements = ledgers.SingleOrDefault()?.Elements("receipt").ToArray() ?? [];
        HashSet<Guid> correctedTransactions = ReadCorrectedTransactionIds(
            root,
            current);
        Dictionary<Guid, int> counts = [];
        foreach (XElement element in elements)
        {
            try
            {
                Guid id = ReadRequiredGuidAttribute(element, "transactionId");
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
            catch (InvalidOperationException)
            {
                // Malformed ledger rows never become recovery authority.
            }
        }

        List<CharacterCareerSkillGroupAdvanceReceipt> recovered = [];
        int omitted = 0;
        foreach (XElement element in elements)
        {
            try
            {
                CharacterCareerSkillGroupAdvanceReceipt persisted = ParseReceiptAudit(element);
                if (correctedTransactions.Contains(persisted.TransactionId))
                {
                    continue;
                }
                if (counts.GetValueOrDefault(persisted.TransactionId) != 1)
                {
                    omitted++;
                    continue;
                }
                CharacterCareerSkillGroupAdvanceQuote observed = ResolveQuote(
                    current,
                    persisted.Identity,
                    "The skill-group receipt target no longer resolves from exact saved authority.");
                CharacterCareerSkillGroupExpenseObservation expense = ObserveExpense(
                    root,
                    persisted.ExpenseId);
                if (!ReceiptAuditMatches(element, persisted, observed)
                    || !CharacterCareerSkillGroupAdvanceRules.TryRecoverReceipt(
                        persisted,
                        persisted.TransactionId,
                        observed,
                        expense,
                        persisted.ReceiptDigest,
                        out CharacterCareerSkillGroupAdvanceReceipt receipt))
                {
                    omitted++;
                    continue;
                }
                recovered.Add(receipt);
            }
            catch (InvalidOperationException)
            {
                omitted++;
            }
        }
        return new ReceiptRecovery(recovered, omitted);
    }

    private static void ValidateExpectedQuote(CareerSkillGroupAdvanceRequest request)
    {
        ValidateExpectedRuleset(request.ExpectedRulesetId);
        if (!string.Equals(
                request.ExpectedLogicalRevision,
                request.ExpectedSkillGroup.LogicalRevision,
                StringComparison.Ordinal)
            || !string.Equals(
                request.ExpectedSourceRevision,
                request.ExpectedSkillGroup.SourceRevision,
                StringComparison.Ordinal)
            || !string.Equals(
                request.ExpectedRuleDigest,
                request.ExpectedSkillGroup.RuleDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The requested skill-group digests do not match the reviewed quote.");
        }
    }

    private static void ValidateExpectedRuleset(string? rulesetId)
    {
        if (!string.Equals(
                rulesetId,
                CharacterCareerSkillGroupAdvanceRules.RulesetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Skill-group advancement is bound to the exact SR5 workspace ruleset.");
        }
    }

    private static CharacterCareerSkillGroupAdvanceQuote ResolveQuote(
        IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> quotes,
        CharacterCareerSkillGroupIdentity identity,
        string error)
    {
        CharacterCareerSkillGroupAdvanceQuote[] matches = quotes
            .Where(candidate => candidate.Identity == identity)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(error);
    }

    private static bool QuoteMatches(
        CharacterCareerSkillGroupAdvanceQuote current,
        CharacterCareerSkillGroupAdvanceQuote expected)
        => current.Identity == expected.Identity
            && string.Equals(current.Name, expected.Name, StringComparison.Ordinal)
            && current.BasePoints == expected.BasePoints
            && current.KarmaPoints == expected.KarmaPoints
            && current.GroupRating == expected.GroupRating
            && current.CostRating == expected.CostRating
            && current.TargetGroupRating == expected.TargetGroupRating
            && current.TargetCostRating == expected.TargetCostRating
            && current.EnabledMemberCount == expected.EnabledMemberCount
            && current.RatingMaximum == expected.RatingMaximum
            && current.AvailableKarma == expected.AvailableKarma
            && current.Disabled == expected.Disabled
            && current.Broken == expected.Broken
            && current.KarmaCost == expected.KarmaCost
            && current.ApplicationDuration == expected.ApplicationDuration
            && current.TimeAuthority == expected.TimeAuthority
            && current.Prerequisites.SequenceEqual(expected.Prerequisites)
            && current.CanAdvance == expected.CanAdvance
            && current.Blocker == expected.Blocker
            && string.Equals(current.LogicalRevision, expected.LogicalRevision, StringComparison.Ordinal)
            && string.Equals(current.SourceRevision, expected.SourceRevision, StringComparison.Ordinal)
            && string.Equals(current.RuleDigest, expected.RuleDigest, StringComparison.Ordinal);

    private static XElement ResolveSkillGroupElement(
        XElement root,
        CharacterCareerSkillGroupIdentity identity)
    {
        XElement newSkills = CareerSkillGroupAdvanceEditorProjector.RequireSingle(
            root,
            "newskills",
            "The saved runner must have one <newskills> container.");
        XElement groups = CareerSkillGroupAdvanceEditorProjector.RequireSingle(
            newSkills,
            "groups",
            "The saved runner must have one <groups> container.");
        XElement[] matches = groups.Elements("group")
            .Where(candidate => MatchesIdentity(candidate, identity))
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "The selected skill-group identity is ambiguous.");
    }

    private static bool MatchesIdentity(
        XElement candidate,
        CharacterCareerSkillGroupIdentity identity)
    {
        XElement[] ids = candidate.Elements("id").Take(2).ToArray();
        return ids.Length == 1
            && Guid.TryParse(ids[0].Value.Trim(), out Guid id)
            && id == identity.InternalId;
    }

    private static void AddExpense(
        XElement root,
        CharacterCareerSkillGroupAdvancePlan plan)
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
        if (TransactionIdExists(root, plan.TransactionId))
        {
            throw new InvalidOperationException(
                "The requested skill-group transaction identity already exists.");
        }

        expenses.Add(new XElement(
            "expense",
            new XElement("guid", plan.ExpenseId.ToString("D")),
            new XElement("date", plan.ExpenseDateLocal.ToString("s", CultureInfo.InvariantCulture)),
            new XElement("amount", plan.ExpenseAmount.ToString(CultureInfo.InvariantCulture)),
            new XElement("reason", plan.ExpenseReason),
            new XElement("type", "Karma"),
            new XElement("refund", "False"),
            new XElement("forcecareervisible", "True"),
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

    private static ExpenseSortRow ProjectExpenseSortRow(XElement expense)
    {
        _ = ReadExpenseId(expense);
        int typeOrder = ReadExactText(expense, "type") switch
        {
            "Karma" => 0,
            "Nuyen" => 1,
            _ => throw new InvalidOperationException(
                "A career expense has an invalid <type> value.")
        };
        if (!DateTime.TryParse(
                ReadExactText(expense, "date"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime date)
            || !bool.TryParse(ReadExactText(expense, "refund"), out bool refund)
            || !decimal.TryParse(
                ReadExactText(expense, "amount"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal amount)
            || !bool.TryParse(
                ReadExactText(expense, "forcecareervisible"),
                out bool forceCareerVisible))
        {
            throw new InvalidOperationException(
                "A career expense has invalid sorting authority.");
        }

        return new ExpenseSortRow(
            expense,
            date,
            typeOrder,
            ReadExactText(expense, "reason"),
            refund,
            amount,
            forceCareerVisible);
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
            .Where(candidate => ReadExpenseId(candidate) == id)
            .Take(2)
            .ToArray() ?? [];
    }

    private static Guid ReadExpenseId(XElement expense)
        => CareerSkillGroupAdvanceEditorProjector.ReadRequiredGuid(
            expense,
            "guid",
            "A career expense");

    private static CharacterCareerSkillGroupExpenseObservation ObserveExpense(
        XElement root,
        Guid expenseId)
    {
        XElement[] matches = FindExpenses(root, expenseId);
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Skill-group authority requires exactly one bound career expense.");
        }

        XElement expense = matches[0];
        XElement[] undo = expense.Elements("undo").Take(2).ToArray();
        if (undo.Length != 1
            || !DateTime.TryParse(
                ReadExactText(expense, "date"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime date)
            || !int.TryParse(
                ReadExactText(expense, "amount"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int amount)
            || !bool.TryParse(ReadExactText(expense, "refund"), out bool refund)
            || !bool.TryParse(
                ReadExactText(expense, "forcecareervisible"),
                out bool forceCareerVisible))
        {
            throw new InvalidOperationException(
                "The bound skill-group expense is malformed.");
        }

        return new CharacterCareerSkillGroupExpenseObservation(
            matches.Length,
            expenseId,
            DateTime.SpecifyKind(date, DateTimeKind.Unspecified),
            amount,
            ReadExactText(expense, "reason"),
            ReadExactText(expense, "type"),
            refund,
            forceCareerVisible,
            ReadExactText(undo[0], "karmatype"),
            ReadExactText(undo[0], "nuyentype"),
            ReadExactText(undo[0], "objectid"),
            ReadRequiredDecimal(undo[0], "qty"),
            ReadExactText(undo[0], "extra"));
    }

    private static string ReadExactText(XElement parent, string name)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0].Value
            : throw new InvalidOperationException(
                $"Saved authority has a missing or duplicate <{name}> value.");
    }

    private static decimal ReadRequiredDecimal(XElement parent, string name)
        => decimal.TryParse(
                ReadExactText(parent, name),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal value)
            ? value
            : throw new InvalidOperationException(
                $"Saved authority has an invalid <{name}> value.");

    private static bool TransactionIdExists(XElement root, Guid transactionId)
        => transactionId == Guid.Empty
            || FindExpenses(root, transactionId).Length != 0
            || FindReceiptAudits(root, transactionId).Length != 0
            || CountCorrections(root, transactionId) != 0;

    private static XElement[] FindReceiptAudits(XElement root, Guid transactionId)
    {
        XElement[] containers = root.Elements("careerskillgroupadvancementreceipts")
            .Take(2)
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group receipt ledgers.");
        }
        return containers.SingleOrDefault()?.Elements("receipt")
            .Where(candidate => ReadRequiredGuidAttribute(candidate, "transactionId")
                == transactionId)
            .Take(2)
            .ToArray() ?? [];
    }

    private static CharacterCareerSkillGroupAdvanceReceipt ParseReceiptAudit(
        XElement element)
    {
        CharacterCareerSkillGroupAdvanceReceipt receipt = new(
            ReadRequiredGuidAttribute(element, "transactionId"),
            new CharacterCareerSkillGroupIdentity(
                ReadRequiredGuidAttribute(element, "targetInternalId")),
            ReadRequiredIntAttribute(element, "groupKarmaBefore"),
            ReadRequiredIntAttribute(element, "groupKarmaAfter"),
            ReadRequiredIntAttribute(element, "characterKarmaBefore"),
            ReadRequiredIntAttribute(element, "characterKarmaAfter"),
            ReadRequiredIntAttribute(element, "groupRatingBefore"),
            ReadRequiredIntAttribute(element, "groupRatingAfter"),
            ReadRequiredIntAttribute(element, "costRatingBefore"),
            ReadRequiredIntAttribute(element, "costRatingAfter"),
            ReadRequiredIntAttribute(element, "enabledMemberCount"),
            ReadRequiredGuidAttribute(element, "expenseId"),
            ReadRequiredDateAttribute(element, "expenseDateLocal"),
            ReadRequiredIntAttribute(element, "expenseAmount"),
            ReadAttribute(element, "expenseReason"),
            ReadAttribute(element, "expenseAuthorityDigest"),
            ReadAttribute(element, "logicalRevisionBefore"),
            ReadAttribute(element, "sourceRevisionBefore"),
            ReadAttribute(element, "ruleDigestBefore"),
            ReadAttribute(element, "logicalRevisionAfter"),
            ReadAttribute(element, "sourceRevisionAfter"),
            ReadAttribute(element, "ruleDigestAfter"),
            ReadAttribute(element, "receiptDigest"));
        return CharacterCareerSkillGroupAdvanceRules.IsCoherent(receipt)
            ? receipt
            : throw new InvalidOperationException(
                "Skill-group receipt authority is not coherent with the Core contract.");
    }

    private static HashSet<Guid> ReadCorrectedTransactionIds(
        XElement root,
        IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> current)
    {
        XElement[] ledgers = root.Elements("careerskillgroupadvancementcorrections")
            .Take(2)
            .ToArray();
        if (ledgers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group correction ledgers.");
        }

        HashSet<Guid> corrected = [];
        HashSet<Guid> correctionIds = [];
        foreach (XElement element in ledgers.SingleOrDefault()?.Elements("correction") ?? [])
        {
            CharacterCareerSkillGroupCorrectionPlan correction = ParseCorrectionAudit(element);
            XElement[] receipts = FindReceiptAudits(root, correction.OriginalTransactionId);
            if (element.Attributes().Count() != 13
                || element.Elements().Count() != 1
                || !correctionIds.Add(correction.CorrectionId)
                || receipts.Length != 1)
            {
                throw new InvalidOperationException(
                    "Skill-group correction authority is duplicate, malformed, or missing its original receipt.");
            }
            CharacterCareerSkillGroupAdvanceReceipt receipt = ParseReceiptAudit(receipts[0]);
            CharacterCareerSkillGroupAdvanceQuote restored = ResolveQuote(
                current,
                correction.Identity,
                "The corrected skill-group target no longer resolves from exact saved authority.");
            if (receipt.Identity != correction.Identity
                || receipt.ExpenseId != correction.ExpenseIdToRemove
                || receipt.ReceiptDigest != correction.OriginalReceiptDigest
                || receipt.GroupKarmaBefore != correction.SavedGroupKarmaPoints
                || receipt.CharacterKarmaBefore != correction.SavedCharacterKarma
                || receipt.GroupRatingBefore != correction.RestoredGroupRating
                || receipt.CostRatingBefore != correction.RestoredCostRating
                || receipt.LogicalRevisionAfter
                    != correction.ExpectedPostLogicalRevision
                || receipt.SourceRevisionAfter
                    != correction.ExpectedPostSourceRevision
                || receipt.RuleDigestAfter != correction.ExpectedPostRuleDigest
                || FindExpenses(root, correction.ExpenseIdToRemove).Length != 0
                || FindExpenses(root, correction.CorrectionId).Length != 0
                || FindReceiptAudits(root, correction.CorrectionId).Length != 0
                || restored.KarmaPoints != correction.SavedGroupKarmaPoints
                || restored.AvailableKarma != correction.SavedCharacterKarma
                || restored.GroupRating != correction.RestoredGroupRating
                || restored.CostRating != correction.RestoredCostRating
                || !corrected.Add(correction.OriginalTransactionId))
            {
                throw new InvalidOperationException(
                    "Skill-group correction authority is foreign or duplicated for one transaction.");
            }
        }
        return corrected;
    }

    private static CharacterCareerSkillGroupCorrectionPlan ParseCorrectionAudit(
        XElement element)
    {
        CharacterCareerSkillGroupCorrectionPlan correction = new(
            ReadRequiredGuidAttribute(element, "id"),
            ReadRequiredGuidAttribute(element, "originalTransactionId"),
            ReadRequiredGuidAttribute(element, "expenseIdToRemove"),
            new CharacterCareerSkillGroupIdentity(
                ReadRequiredGuidAttribute(element, "targetInternalId")),
            ReadRequiredIntAttribute(element, "savedGroupKarmaPoints"),
            ReadRequiredIntAttribute(element, "savedCharacterKarma"),
            ReadRequiredIntAttribute(element, "restoredGroupRating"),
            ReadRequiredIntAttribute(element, "restoredCostRating"),
            ReadExactText(element, "reason"),
            ReadAttribute(element, "expectedPostLogicalRevision"),
            ReadAttribute(element, "expectedPostSourceRevision"),
            ReadAttribute(element, "expectedPostRuleDigest"),
            ReadAttribute(element, "originalReceiptDigest"),
            ReadAttribute(element, "correctionDigest"));
        return CharacterCareerSkillGroupAdvanceRules.IsCoherent(correction)
            ? correction
            : throw new InvalidOperationException(
                "Skill-group correction authority is not coherent with the Core contract.");
    }

    private static void AddReceiptAudit(
        XElement root,
        CharacterCareerSkillGroupAdvanceReceipt receipt,
        CharacterCareerSkillGroupAdvanceQuote postState)
    {
        XElement[] containers = root.Elements("careerskillgroupadvancementreceipts")
            .Take(2)
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group receipt ledgers.");
        }
        XElement ledger = containers.SingleOrDefault()
            ?? new XElement("careerskillgroupadvancementreceipts");
        if (ledger.Parent is null)
        {
            root.Add(ledger);
        }
        if (FindReceiptAudits(root, receipt.TransactionId).Length != 0)
        {
            throw new InvalidOperationException(
                "The skill-group transaction already has a persisted receipt.");
        }

        ledger.Add(new XElement(
            "receipt",
            new XAttribute("transactionId", receipt.TransactionId.ToString("D")),
            new XAttribute("targetInternalId", receipt.Identity.InternalId.ToString("D")),
            new XAttribute("groupKarmaBefore", receipt.GroupKarmaBefore),
            new XAttribute("groupKarmaAfter", receipt.GroupKarmaAfter),
            new XAttribute("characterKarmaBefore", receipt.CharacterKarmaBefore),
            new XAttribute("characterKarmaAfter", receipt.CharacterKarmaAfter),
            new XAttribute("groupRatingBefore", receipt.GroupRatingBefore),
            new XAttribute("groupRatingAfter", receipt.GroupRatingAfter),
            new XAttribute("costRatingBefore", receipt.CostRatingBefore),
            new XAttribute("costRatingAfter", receipt.CostRatingAfter),
            new XAttribute("enabledMemberCount", receipt.EnabledMemberCount),
            new XAttribute("expenseId", receipt.ExpenseId.ToString("D")),
            new XAttribute(
                "expenseDateLocal",
                receipt.ExpenseDateLocal.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("expenseAmount", receipt.ExpenseAmount),
            new XAttribute("expenseReason", receipt.ExpenseReason),
            new XAttribute("expenseAuthorityDigest", receipt.ExpenseAuthorityDigest),
            new XAttribute("logicalRevisionBefore", receipt.LogicalRevisionBefore),
            new XAttribute("sourceRevisionBefore", receipt.SourceRevisionBefore),
            new XAttribute("ruleDigestBefore", receipt.RuleDigestBefore),
            new XAttribute("logicalRevisionAfter", receipt.LogicalRevisionAfter),
            new XAttribute("sourceRevisionAfter", receipt.SourceRevisionAfter),
            new XAttribute("ruleDigestAfter", receipt.RuleDigestAfter),
            new XAttribute("receiptDigest", receipt.ReceiptDigest),
            new XAttribute(
                "projectionDigest",
                CalculateProjectionDigest(receipt, postState))));
    }

    private static bool ReceiptAuditMatches(
        XElement element,
        CharacterCareerSkillGroupAdvanceReceipt receipt,
        CharacterCareerSkillGroupAdvanceQuote postState)
        => element.Attributes().Count() == 24
            && ReadAttribute(element, "transactionId")
                == receipt.TransactionId.ToString("D")
            && ReadAttribute(element, "targetInternalId")
                == receipt.Identity.InternalId.ToString("D")
            && ReadAttribute(element, "groupKarmaBefore")
                == receipt.GroupKarmaBefore.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "groupKarmaAfter")
                == receipt.GroupKarmaAfter.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "characterKarmaBefore")
                == receipt.CharacterKarmaBefore.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "characterKarmaAfter")
                == receipt.CharacterKarmaAfter.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "groupRatingBefore")
                == receipt.GroupRatingBefore.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "groupRatingAfter")
                == receipt.GroupRatingAfter.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "costRatingBefore")
                == receipt.CostRatingBefore.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "costRatingAfter")
                == receipt.CostRatingAfter.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "enabledMemberCount")
                == receipt.EnabledMemberCount.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "expenseId") == receipt.ExpenseId.ToString("D")
            && ReadAttribute(element, "expenseDateLocal")
                == receipt.ExpenseDateLocal.ToString("O", CultureInfo.InvariantCulture)
            && ReadAttribute(element, "expenseAmount")
                == receipt.ExpenseAmount.ToString(CultureInfo.InvariantCulture)
            && ReadAttribute(element, "expenseReason") == receipt.ExpenseReason
            && ReadAttribute(element, "expenseAuthorityDigest")
                == receipt.ExpenseAuthorityDigest
            && ReadAttribute(element, "logicalRevisionBefore")
                == receipt.LogicalRevisionBefore
            && ReadAttribute(element, "sourceRevisionBefore")
                == receipt.SourceRevisionBefore
            && ReadAttribute(element, "ruleDigestBefore") == receipt.RuleDigestBefore
            && ReadAttribute(element, "logicalRevisionAfter")
                == receipt.LogicalRevisionAfter
            && ReadAttribute(element, "sourceRevisionAfter")
                == receipt.SourceRevisionAfter
            && ReadAttribute(element, "ruleDigestAfter") == receipt.RuleDigestAfter
            && ReadAttribute(element, "receiptDigest") == receipt.ReceiptDigest
            && ReadAttribute(element, "projectionDigest")
                == CalculateProjectionDigest(receipt, postState);

    private static string CalculateProjectionDigest(
        CharacterCareerSkillGroupAdvanceReceipt receipt,
        CharacterCareerSkillGroupAdvanceQuote postState)
    {
        string canonical = string.Join(
            '\0',
            "chummer6.presentation.career-skill-group-receipt/v2",
            receipt.ReceiptDigest,
            postState.LogicalRevision,
            postState.SourceRevision,
            postState.RuleDigest);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static int CountCorrections(XElement root, Guid correctionId)
    {
        XElement[] containers = root.Elements("careerskillgroupadvancementcorrections")
            .Take(2)
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group correction ledgers.");
        }
        return containers.SingleOrDefault()?.Elements("correction")
            .Count(candidate => ReadRequiredGuidAttribute(candidate, "id") == correctionId)
            ?? 0;
    }

    private static void AddCorrectionAudit(
        XElement root,
        CharacterCareerSkillGroupCorrectionPlan correction)
    {
        XElement[] containers = root.Elements("careerskillgroupadvancementcorrections")
            .Take(2)
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidOperationException(
                "The saved runner has duplicate skill-group correction ledgers.");
        }
        XElement ledger = containers.SingleOrDefault()
            ?? new XElement("careerskillgroupadvancementcorrections");
        if (ledger.Parent is null)
        {
            root.Add(ledger);
        }
        if (CountCorrections(root, correction.CorrectionId) != 0)
        {
            throw new InvalidOperationException(
                "The skill-group correction identity already exists.");
        }
        ledger.Add(new XElement(
            "correction",
            new XAttribute("id", correction.CorrectionId.ToString("D")),
            new XAttribute(
                "originalTransactionId",
                correction.OriginalTransactionId.ToString("D")),
            new XAttribute(
                "expenseIdToRemove",
                correction.ExpenseIdToRemove.ToString("D")),
            new XAttribute(
                "targetInternalId",
                correction.Identity.InternalId.ToString("D")),
            new XAttribute("savedGroupKarmaPoints", correction.SavedGroupKarmaPoints),
            new XAttribute("savedCharacterKarma", correction.SavedCharacterKarma),
            new XAttribute("restoredGroupRating", correction.RestoredGroupRating),
            new XAttribute("restoredCostRating", correction.RestoredCostRating),
            new XAttribute(
                "expectedPostLogicalRevision",
                correction.ExpectedPostLogicalRevision),
            new XAttribute(
                "expectedPostSourceRevision",
                correction.ExpectedPostSourceRevision),
            new XAttribute(
                "expectedPostRuleDigest",
                correction.ExpectedPostRuleDigest),
            new XAttribute("originalReceiptDigest", correction.OriginalReceiptDigest),
            new XAttribute("correctionDigest", correction.CorrectionDigest),
            new XElement("reason", correction.Reason)));
    }

    private static Guid ReadRequiredGuidAttribute(XElement element, string name)
    {
        XAttribute[] matches = element.Attributes(name).Take(2).ToArray();
        return matches.Length == 1
            && Guid.TryParse(matches[0].Value, out Guid value)
            && value != Guid.Empty
            ? value
            : throw new InvalidOperationException(
                $"Skill-group ledger authority has an invalid or duplicate '{name}' value.");
    }

    private static int ReadRequiredIntAttribute(XElement element, string name)
        => int.TryParse(
                ReadAttribute(element, name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
            ? value
            : throw new InvalidOperationException(
                $"Skill-group ledger authority has an invalid '{name}' value.");

    private static DateTime ReadRequiredDateAttribute(XElement element, string name)
        => DateTime.TryParse(
                ReadAttribute(element, name),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime value)
            ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            : throw new InvalidOperationException(
                $"Skill-group ledger authority has an invalid '{name}' value.");

    private static string ReadAttribute(XElement element, string name)
    {
        XAttribute[] matches = element.Attributes(name).Take(2).ToArray();
        return matches.Length == 1
            ? matches[0].Value
            : throw new InvalidOperationException(
                $"Skill-group receipt authority has a missing or duplicate '{name}' value.");
    }

    private static void SetRequiredValue(
        XElement parent,
        string name,
        int value,
        string owner)
    {
        XElement[] matches = parent.Elements(name).Take(2).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"{owner} has a missing or duplicate <{name}> value.");
        }
        matches[0].Value = value.ToString(CultureInfo.InvariantCulture);
    }

    private static void ValidateRequestAuthority(
        string workspaceId,
        long contentRevision)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for skill-group advancement.");
        }
        if (contentRevision <= 0)
        {
            throw new InvalidOperationException(
                "A positive dossier revision is required for skill-group advancement.");
        }
    }

    private static string Serialize(XDocument document)
    {
        using StringWriter writer = new(CultureInfo.InvariantCulture);
        document.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }
}
