using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerManualKarmaParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-manual-karma-tests");
    private static readonly ICharacterSourceDataResolver Resolver = new TestResolver();
    private const string Xml = "<character><created>True</created><settings>test</settings><karma>5</karma><nuyen>10000</nuyen><expenses><expense><guid>00000000-0000-0000-0000-000000000001</guid><date>2024-01-01T12:00:00</date><amount>1</amount><reason>Existing</reason><type>Karma</type><refund>False</refund></expense></expenses><customstate><karma>keep</karma></customstate></character>";

    [TestMethod]
    public void Gain_exchange_preserves_people_man_rate_asymmetry_and_sorting()
    {
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Xml, WorkspaceId, 9, Resolver);
        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
            Xml,
            new CareerManualKarmaEditRequest(
                WorkspaceId,
                9,
                editor.Karma,
                CharacterCareerManualKarmaAction.Gain,
                2,
                "Working for the People",
                new DateTime(2023, 12, 1, 8, 30, 0),
                Refund: true,
                KarmaNuyenExchange: true,
                ForceCareerVisible: true),
            Resolver)).Root!;

        Assert.AreEqual("7", root.Element("karma")!.Value);
        Assert.AreEqual("6000", root.Element("nuyen")!.Value);
        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.AreEqual(3, expenses.Length);
        Assert.AreEqual("2", expenses[0].Element("amount")!.Value);
        Assert.AreEqual("Karma", expenses[0].Element("type")!.Value);
        Assert.AreEqual("True", expenses[0].Element("refund")!.Value);
        Assert.AreEqual("False", expenses[0].Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualAdd", expenses[0].Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("-3000", expenses[1].Element("amount")!.Value);
        Assert.AreEqual("Nuyen", expenses[1].Element("type")!.Value);
        Assert.AreEqual("True", expenses[1].Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualSubtract", expenses[1].Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual("Existing", expenses[2].Element("reason")!.Value);
        Assert.AreEqual("keep", root.Element("customstate")!.Element("karma")!.Value);
    }

    [TestMethod]
    public void Spend_exchange_checks_available_karma_and_appends_exact_expenses()
    {
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Xml, WorkspaceId, 9, Resolver);
        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
            Xml,
            new CareerManualKarmaEditRequest(
                WorkspaceId,
                9,
                editor.Karma,
                CharacterCareerManualKarmaAction.Spend,
                3,
                "Working for the Man",
                new DateTime(2025, 1, 1, 12, 0, 0),
                Refund: true,
                KarmaNuyenExchange: true,
                ForceCareerVisible: true),
            Resolver)).Root!;

        Assert.AreEqual("2", root.Element("karma")!.Value);
        Assert.AreEqual("16000", root.Element("nuyen")!.Value);
        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.AreEqual("Existing", expenses[0].Element("reason")!.Value);
        Assert.AreEqual("-3", expenses[1].Element("amount")!.Value);
        Assert.AreEqual("ManualSubtract", expenses[1].Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("True", expenses[1].Element("forcecareervisible")!.Value);
        Assert.AreEqual("6000", expenses[2].Element("amount")!.Value);
        Assert.AreEqual("False", expenses[2].Element("refund")!.Value);
        Assert.AreEqual("True", expenses[2].Element("forcecareervisible")!.Value);
    }

    [TestMethod]
    public void Invalid_force_visibility_stale_state_and_unaffordable_spend_fail_closed()
    {
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Xml, WorkspaceId, 9, Resolver);
        CareerManualKarmaEditRequest request = new(
            WorkspaceId,
            9,
            editor.Karma,
            CharacterCareerManualKarmaAction.Spend,
            6,
            string.Empty,
            new DateTime(2025, 1, 1),
            Refund: false,
            KarmaNuyenExchange: false,
            ForceCareerVisible: false);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(Xml, request, Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
                Xml,
                request with { Amount = 1, ForceCareerVisible = true },
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
                Xml.Replace("<karma>5</karma>", "<karma>4</karma>"),
                request with { Amount = 1 },
                Resolver));
    }

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
