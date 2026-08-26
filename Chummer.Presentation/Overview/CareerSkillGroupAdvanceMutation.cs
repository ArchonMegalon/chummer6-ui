using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

/// <summary>
/// Compatibility projection for callers that still own the Chummer5 XML boundary.
/// The owning workspace layer must compare <see cref="CareerSkillGroupAdvanceRequest.ExpectedContentRevision"/>
/// before atomically persisting the returned XML. This helper fail-closes duplicate
/// expense identities, but it is not the Core service boundary and therefore does
/// not mint binding/command/result digests, replay receipts, or a workspace CAS.
/// </summary>
internal static class CareerSkillGroupAdvanceMutation
{
    private sealed record ExpenseSortRow(
        XElement Expense,
        DateTime Date,
        int Type,
        string Reason,
        bool Refund,
        decimal Amount,
        bool ForceCareerVisible);

    public static string Apply(
        string xml,
        CareerSkillGroupAdvanceRequest request,
        string? settingsCatalogJson,
        ICharacterSourceDataResolver? sourceDataResolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ExpectedSkillGroup);
        ValidateRequestAuthority(request);

        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> current, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                xml,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote[] matches = current
            .Where(candidate => candidate.Identity == request.ExpectedSkillGroup.Identity)
            .Take(2)
            .ToArray();
        if (!CharacterCareerSkillGroupAdvanceRules.IsCoherent(request.ExpectedSkillGroup)
            || matches.Length != 1
            || !string.Equals(
                matches[0].LogicalRevision,
                request.ExpectedSkillGroup.LogicalRevision,
                StringComparison.Ordinal)
            || !string.Equals(
                matches[0].SourceRevision,
                request.ExpectedSkillGroup.SourceRevision,
                StringComparison.Ordinal)
            || !string.Equals(
                matches[0].RuleDigest,
                request.ExpectedSkillGroup.RuleDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The selected skill group changed or disappeared while advancement was open.");
        }
        if (!CharacterCareerSkillGroupAdvanceRules.TryPlanAdvance(
                matches[0],
                request.ExpectedSkillGroup.LogicalRevision,
                request.ExpectedSkillGroup.SourceRevision,
                request.ExpectedRuleDigest,
                request.Confirmed,
                // The pure XML compatibility projection cannot atomically claim a
                // service transaction. AddExpense still rejects a duplicate ExpenseId;
                // service replay/receipt authority belongs to the Core workspace API.
                transactionIdAlreadyExists: false,
                transactionId: request.ExpenseId,
                request.ExpenseDateLocal,
                out CharacterCareerSkillGroupAdvancePlan plan))
        {
            throw new InvalidOperationException(
                "Skill-group advancement requires confirmation, unchanged rules, sufficient Karma, an unbroken enabled group, and a valid expense identity/date.");
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = CareerSkillGroupAdvanceEditorProjector.RequireCharacterRoot(document);
        XElement target = ResolveSkillGroupElement(root, plan.Identity);
        SetRequiredValue(
            target,
            "karma",
            plan.SavedGroupKarmaPoints.ToString(CultureInfo.InvariantCulture),
            "The selected skill group");
        SetRequiredValue(
            root,
            "karma",
            plan.SavedCharacterKarma.ToString(CultureInfo.InvariantCulture),
            "The saved runner");
        AddExpense(root, plan);

        string serialized = Serialize(document);
        (IReadOnlyList<CharacterCareerSkillGroupAdvanceQuote> result, _) =
            CareerSkillGroupAdvanceEditorProjector.ProjectState(
                serialized,
                settingsCatalogJson,
                sourceDataResolver);
        CharacterCareerSkillGroupAdvanceQuote[] saved = result
            .Where(candidate => candidate.Identity == plan.Identity)
            .Take(2)
            .ToArray();
        if (saved.Length != 1
            || saved[0].KarmaPoints != plan.SavedGroupKarmaPoints
            || saved[0].AvailableKarma != plan.SavedCharacterKarma
            || saved[0].GroupRating != plan.TargetGroupRating
            || saved[0].CostRating != plan.TargetCostRating
            || saved[0].EnabledMemberCount != plan.EnabledMemberCount)
        {
            throw new InvalidOperationException(
                "Skill-group advancement did not preserve exact saved identity and rating authority.");
        }
        return serialized;
    }

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

        foreach (XElement existing in expenses.Elements("expense"))
        {
            Guid id = CareerSkillGroupAdvanceEditorProjector.ReadRequiredGuid(
                existing,
                "guid",
                "A career expense");
            if (id == plan.ExpenseId)
            {
                throw new InvalidOperationException(
                    "The requested career-expense GUID already exists.");
            }
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
        _ = CareerSkillGroupAdvanceEditorProjector.ReadRequiredGuid(
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

    private static void ValidateRequestAuthority(CareerSkillGroupAdvanceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceId.Value))
        {
            throw new InvalidOperationException(
                "A nonblank dossier identity is required for skill-group advancement.");
        }
        if (request.ExpectedContentRevision <= 0)
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
