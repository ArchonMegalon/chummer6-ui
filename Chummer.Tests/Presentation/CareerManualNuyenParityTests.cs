using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerManualNuyenParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-manual-nuyen-tests");
    private static readonly ICharacterSourceDataResolver Resolver = new TestResolver();
    private const string Xml = "<character><created>True</created><settings>test</settings><karma>5</karma><nuyen>10000</nuyen><expenses><expense><guid>00000000-0000-0000-0000-000000000001</guid><date>2024-01-01T12:00:00</date><amount>1</amount><reason>Existing</reason><type>Karma</type><refund>False</refund></expense></expenses><customstate><nuyen>keep</nuyen></customstate></character>";

    [TestMethod]
    public void Gain_applies_percent_refund_sorted_expense_and_manual_add_undo()
    {
        CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
            Xml, WorkspaceId, 11, Resolver);
        XElement root = Apply(new CareerManualNuyenEditRequest(
            WorkspaceId,
            11,
            editor.Nuyen,
            CharacterCareerManualNuyenAction.Gain,
            Amount: 100,
            Percent: 150m,
            Reason: "Run payment",
            ExpenseDateLocal: new DateTime(2023, 12, 1, 8, 30, 0),
            Refund: true,
            KarmaNuyenExchange: false,
            ForceCareerVisible: false));

        Assert.AreEqual("10150", root.Element("nuyen")!.Value);
        Assert.AreEqual("5", root.Element("karma")!.Value);
        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.AreEqual("150", expenses[0].Element("amount")!.Value);
        Assert.AreEqual("Nuyen", expenses[0].Element("type")!.Value);
        Assert.AreEqual("True", expenses[0].Element("refund")!.Value);
        Assert.AreEqual("False", expenses[0].Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualAdd", expenses[0].Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual("Existing", expenses[1].Element("reason")!.Value);
        Assert.AreEqual("keep", root.Element("customstate")!.Element("nuyen")!.Value);
    }

    [TestMethod]
    public void Spend_applies_percent_affordability_and_ignores_refund_for_nuyen_expense()
    {
        CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
            Xml, WorkspaceId, 11, Resolver);
        XElement root = Apply(new CareerManualNuyenEditRequest(
            WorkspaceId,
            11,
            editor.Nuyen,
            CharacterCareerManualNuyenAction.Spend,
            Amount: 100,
            Percent: 50m,
            Reason: "Lifestyle incidentals",
            ExpenseDateLocal: new DateTime(2025, 1, 1, 12, 0, 0),
            Refund: true,
            KarmaNuyenExchange: false,
            ForceCareerVisible: false));

        Assert.AreEqual("9950", root.Element("nuyen")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").Last();
        Assert.AreEqual("-50", expense.Element("amount")!.Value);
        Assert.AreEqual("False", expense.Element("refund")!.Value);
        Assert.AreEqual("ManualSubtract", expense.Element("undo")!.Element("nuyentype")!.Value);
    }

    [TestMethod]
    public void Exchanges_preserve_people_validation_man_gain_conversion_and_legacy_undo()
    {
        CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
            Xml, WorkspaceId, 11, Resolver);
        XElement gained = Apply(new CareerManualNuyenEditRequest(
            WorkspaceId,
            11,
            editor.Nuyen,
            CharacterCareerManualNuyenAction.Gain,
            Amount: 3_000,
            Percent: 725m,
            Reason: "Working for the Man",
            ExpenseDateLocal: new DateTime(2025, 1, 1),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true));
        Assert.AreEqual("13000", gained.Element("nuyen")!.Value);
        Assert.AreEqual("4", gained.Element("karma")!.Value);
        XElement[] gainedExpenses = gained.Element("expenses")!.Elements("expense").Skip(1).ToArray();
        Assert.AreEqual("3000", gainedExpenses[0].Element("amount")!.Value);
        Assert.AreEqual("False", gainedExpenses[0].Element("forcecareervisible")!.Value);
        Assert.AreEqual("-1", gainedExpenses[1].Element("amount")!.Value);
        Assert.AreEqual("True", gainedExpenses[1].Element("refund")!.Value);
        Assert.AreEqual("True", gainedExpenses[1].Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualSubtract", gainedExpenses[1].Element("undo")!.Element("karmatype")!.Value);

        XElement spent = Apply(new CareerManualNuyenEditRequest(
            WorkspaceId,
            11,
            editor.Nuyen,
            CharacterCareerManualNuyenAction.Spend,
            Amount: 3_000,
            Percent: 25m,
            Reason: "Working for the People",
            ExpenseDateLocal: new DateTime(2025, 1, 1),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true));
        Assert.AreEqual("7000", spent.Element("nuyen")!.Value);
        Assert.AreEqual("7", spent.Element("karma")!.Value);
        XElement[] spentExpenses = spent.Element("expenses")!.Elements("expense").Skip(1).ToArray();
        Assert.AreEqual("-3000", spentExpenses[0].Element("amount")!.Value);
        Assert.AreEqual("False", spentExpenses[0].Element("refund")!.Value);
        Assert.AreEqual("2", spentExpenses[1].Element("amount")!.Value);
        Assert.AreEqual("True", spentExpenses[1].Element("forcecareervisible")!.Value);
    }

    [TestMethod]
    public void Invalid_exchange_multiple_stale_state_force_visibility_and_overspend_fail_closed()
    {
        CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
            Xml, WorkspaceId, 11, Resolver);
        CareerManualNuyenEditRequest request = new(
            WorkspaceId,
            11,
            editor.Nuyen,
            CharacterCareerManualNuyenAction.Spend,
            Amount: 2_000,
            Percent: 100m,
            Reason: string.Empty,
            ExpenseDateLocal: new DateTime(2025, 1, 1),
            Refund: false,
            KarmaNuyenExchange: true,
            ForceCareerVisible: false);
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(request));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { KarmaNuyenExchange = false, ForceCareerVisible = true, Amount = 1 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { KarmaNuyenExchange = false, ForceCareerVisible = false, Amount = 9_999_999 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
            Xml.Replace("<nuyen>10000</nuyen>", "<nuyen>9999</nuyen>"),
            request with { Amount = 1_500 },
            Resolver));
    }

    private static XElement Apply(CareerManualNuyenEditRequest request)
        => XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
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
