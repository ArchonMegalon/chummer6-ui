using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerCreateExpenseParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-create-expense-tests");
    private static readonly ICharacterSourceDataResolver Resolver = new TestResolver();
    private const string Xml = "<character><created>True</created><settings>test</settings><karma>5</karma><nuyen>10000</nuyen><expenses><expense><guid>00000000-0000-0000-0000-000000000001</guid><date>2024-01-01T12:00:00</date><amount>1</amount><reason>Existing</reason><type>Karma</type><refund>False</refund></expense></expenses><customstate><expense>keep</expense></customstate></character>";

    [TestMethod]
    public void Nuyen_gain_commits_exact_percentage_refund_date_reason_and_sign()
    {
        CareerCreateExpenseEditorState editor = CareerCreateExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            17,
            CharacterCareerCreateExpenseOperation.NuyenGained,
            Resolver);
        XElement root = Apply(new CareerCreateExpenseEditRequest(
            WorkspaceId,
            17,
            editor.Expense,
            editor.Operation,
            Amount: 100,
            Percent: 150m,
            Reason: "Run payment",
            ExpenseDateLocal: new DateTime(2023, 12, 1, 8, 30, 0),
            Refund: true,
            KarmaNuyenExchange: false,
            ForceCareerVisible: false));

        Assert.AreEqual("10150", root.Element("nuyen")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").First();
        Assert.AreEqual("150", expense.Element("amount")!.Value);
        Assert.AreEqual("2023-12-01T08:30:00", expense.Element("date")!.Value);
        Assert.AreEqual("Run payment", expense.Element("reason")!.Value);
        Assert.AreEqual("True", expense.Element("refund")!.Value);
        Assert.AreEqual("ManualAdd", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual("keep", root.Element("customstate")!.Element("expense")!.Value);
    }

    [TestMethod]
    public void Karma_spend_exchange_commits_exact_primary_secondary_and_visibility_semantics()
    {
        CareerCreateExpenseEditorState editor = CareerCreateExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            17,
            CharacterCareerCreateExpenseOperation.KarmaSpent,
            Resolver);
        XElement root = Apply(new CareerCreateExpenseEditRequest(
            WorkspaceId,
            17,
            editor.Expense,
            editor.Operation,
            Amount: 3,
            Percent: 100m,
            Reason: "Working for the Man",
            ExpenseDateLocal: new DateTime(2025, 1, 1),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true));

        Assert.AreEqual("2", root.Element("karma")!.Value);
        Assert.AreEqual("16000", root.Element("nuyen")!.Value);
        XElement[] added = root.Element("expenses")!.Elements("expense").Skip(1).ToArray();
        Assert.AreEqual("-3", added[0].Element("amount")!.Value);
        Assert.AreEqual("True", added[0].Element("refund")!.Value);
        Assert.AreEqual("True", added[0].Element("forcecareervisible")!.Value);
        Assert.AreEqual("6000", added[1].Element("amount")!.Value);
        Assert.AreEqual("False", added[1].Element("refund")!.Value);
        Assert.AreEqual("ManualSubtract", added[1].Element("undo")!.Element("nuyentype")!.Value);
    }

    [TestMethod]
    public void Nuyen_exchange_rejection_and_integral_canonical_no_op_fail_before_xml_mutation()
    {
        CareerCreateExpenseEditorState editor = CareerCreateExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            17,
            CharacterCareerCreateExpenseOperation.NuyenGained,
            Resolver);
        CareerCreateExpenseEditRequest request = new(
            WorkspaceId,
            17,
            editor.Expense,
            editor.Operation,
            Amount: 2_000,
            Percent: 100m,
            Reason: "Working for the Man",
            ExpenseDateLocal: new DateTime(2025, 1, 1),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true);

        InvalidOperationException rejected = Assert.ThrowsExactly<InvalidOperationException>(() => Apply(request));
        StringAssert.Contains(rejected.Message, "not an exact Working for the People multiple");
        InvalidOperationException noOp = Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { Amount = 3_000 }));
        StringAssert.Contains(noOp.Message, "keeps an integral Nuyen exchange editor open");
    }

    [TestMethod]
    public void Stale_state_force_visibility_and_invalid_date_fail_closed()
    {
        CareerCreateExpenseEditorState editor = CareerCreateExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            17,
            CharacterCareerCreateExpenseOperation.KarmaGained,
            Resolver);
        CareerCreateExpenseEditRequest request = new(
            WorkspaceId,
            17,
            editor.Expense,
            editor.Operation,
            1,
            100m,
            CharacterCareerCreateExpenseRules.DefaultReason,
            new DateTime(2025, 1, 1),
            false,
            false,
            false);

        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { ForceCareerVisible = true }));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { ExpenseDateLocal = new DateTime(1700, 1, 1) }));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerCreateExpenseEdit(
            Xml.Replace("<karma>5</karma>", "<karma>4</karma>"),
            request,
            Resolver));
    }

    private static XElement Apply(CareerCreateExpenseEditRequest request)
        => XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerCreateExpenseEdit(
            Xml,
            request,
            Resolver)).Root!;

    private sealed class TestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new TestContext();
    }

    private sealed class TestContext : ICharacterSourceDataContext
    {
        public bool TryResolveKarmaNuyenExchangeRates(
            out decimal workingForPeopleRate,
            out decimal workingForManRate)
        {
            workingForPeopleRate = 1_500m;
            workingForManRate = 2_000m;
            return true;
        }

        public bool TryResolveCyberwareGradeDeviceRating(
            string gradeName,
            string improvementSource,
            out int deviceRating)
        {
            deviceRating = 0;
            return false;
        }

        public bool TryResolveVehicleModBonuses(
            string sourceId,
            string name,
            out CharacterVehicleModSourceBonuses bonuses)
        {
            bonuses = CharacterVehicleModSourceBonuses.Empty;
            return false;
        }
    }
}
