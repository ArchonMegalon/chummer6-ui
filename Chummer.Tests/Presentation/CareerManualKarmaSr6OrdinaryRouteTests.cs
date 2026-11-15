using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

/// <summary>
/// Work order 8055 s1 focused regression: the ordinary (KarmaNuyenExchange=false)
/// SR6 career manual-Karma/Nuyen reward route must project without the Chummer5
/// settings exchange keys (SR6 binds virtual creation profiles that never select
/// an SR5 settings.xml row), while exchange-capable SR6 requests stay refused and
/// SR5 keeps the exact fail-closed contract.
/// </summary>
[TestClass]
public sealed class CareerManualKarmaSr6OrdinaryRouteTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-manual-karma-sr6-ordinary-tests");

    /// <summary>SR5-style resolver that resolves rates like the bundled settings rows do.</summary>
    private static readonly ICharacterSourceDataResolver ResolvingResolver = new ResolvingTestResolver();

    /// <summary>Resolver modeling an SR6 virtual creation profile: no exchange keys resolvable.</summary>
    private static readonly ICharacterSourceDataResolver NonResolvingResolver = new NonResolvingTestResolver();

    private const string Sr5Xml =
        "<character><gameedition>SR5</gameedition><created>True</created><settings>test</settings>"
        + "<karma>5</karma><nuyen>10000</nuyen>"
        + "<expenses><expense><guid>00000000-0000-0000-0000-000000000001</guid><date>2024-01-01T12:00:00</date>"
        + "<amount>1</amount><reason>Existing</reason><type>Karma</type><refund>False</refund></expense></expenses>"
        + "<customstate><karma>keep</karma></customstate></character>";

    private const string Sr6Xml =
        "<character><gameedition>SR6</gameedition><created>True</created>"
        + "<karma>5</karma><nuyen>10000</nuyen>"
        + "<expenses><expense><guid>00000000-0000-0000-0000-000000000001</guid><date>2024-01-01T12:00:00</date>"
        + "<amount>1</amount><reason>Existing</reason><type>Karma</type><refund>False</refund></expense></expenses>"
        + "<customstate><karma>keep</karma></customstate></character>";

    [TestMethod]
    public void Sr6_ordinary_karma_gain_projects_and_applies_without_exchange_keys()
    {
        // Pre-fix this threw: "... do not prove the exact Chummer5 manual-Karma exchange rules".
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Sr6Xml, WorkspaceId, 9, NonResolvingResolver);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
            Sr6Xml,
            new CareerManualKarmaEditRequest(
                WorkspaceId,
                9,
                editor.Karma,
                CharacterCareerManualKarmaAction.Gain,
                2,
                "Working for the People",
                new DateTime(2023, 12, 1, 8, 30, 0),
                Refund: false,
                KarmaNuyenExchange: false,
                ForceCareerVisible: false),
            NonResolvingResolver)).Root!;

        Assert.AreEqual("7", root.Element("karma")!.Value);
        Assert.AreEqual("10000", root.Element("nuyen")!.Value);
        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.AreEqual(2, expenses.Length);
        Assert.AreEqual("2", expenses[0].Element("amount")!.Value);
        Assert.AreEqual("Karma", expenses[0].Element("type")!.Value);
        Assert.AreEqual("Working for the People", expenses[0].Element("reason")!.Value);
        Assert.AreEqual("keep", root.Element("customstate")!.Element("karma")!.Value);
    }

    [TestMethod]
    public void Sr6_ordinary_nuyen_route_projects_and_applies_without_exchange_keys()
    {
        CareerManualNuyenEditorState editor = CareerManualNuyenEditorProjector.Project(
            Sr6Xml, WorkspaceId, 9, NonResolvingResolver);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
            Sr6Xml,
            new CareerManualNuyenEditRequest(
                WorkspaceId,
                9,
                editor.Nuyen,
                CharacterCareerManualNuyenAction.Gain,
                10000,
                50m,
                "Salary",
                new DateTime(2023, 12, 1, 8, 30, 0),
                Refund: false,
                KarmaNuyenExchange: false,
                ForceCareerVisible: false),
            NonResolvingResolver)).Root!;

        Assert.AreEqual("15000", root.Element("nuyen")!.Value);
        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.AreEqual(2, expenses.Length);
        Assert.AreEqual("5000", expenses[0].Element("amount")!.Value);
        Assert.AreEqual("Nuyen", expenses[0].Element("type")!.Value);
    }

    [TestMethod]
    public void Sr6_exchange_request_refused_fail_closed()
    {
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Sr6Xml, WorkspaceId, 9, NonResolvingResolver);

        CareerManualKarmaEditRequest request = new(
            WorkspaceId,
            9,
            editor.Karma,
            CharacterCareerManualKarmaAction.Gain,
            2,
            "Working for the People",
            new DateTime(2023, 12, 1, 8, 30, 0),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(Sr6Xml, request, NonResolvingResolver));
        StringAssert.Contains(exception.Message, "SR6");

        CareerManualNuyenEditorState nuyenEditor = CareerManualNuyenEditorProjector.Project(
            Sr6Xml, WorkspaceId, 3, NonResolvingResolver);
        CareerManualNuyenEditRequest nuyenRequest = new(
            WorkspaceId,
            3,
            nuyenEditor.Nuyen,
            CharacterCareerManualNuyenAction.Gain,
            5000,
            0m,
            string.Empty,
            new DateTime(2023, 12, 1),
            Refund: true,
            KarmaNuyenExchange: true,
            ForceCareerVisible: true);
        InvalidOperationException nuyenException = Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(Sr6Xml, nuyenRequest, NonResolvingResolver));
        StringAssert.Contains(nuyenException.Message, "SR6");
    }

    [TestMethod]
    public void Sr5_fail_closed_contract_unchanged()
    {
        // SR5 without a resolvable settings context must still throw (fail-closed).
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(Sr5Xml, WorkspaceId, 9, NonResolvingResolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualNuyenEditorProjector.Project(Sr5Xml, WorkspaceId, 9, NonResolvingResolver));

        // SR5 with resolving settings keeps the exact exchange behavior.
        CareerManualKarmaEditorState editor = CareerManualKarmaEditorProjector.Project(
            Sr5Xml, WorkspaceId, 9, ResolvingResolver);
        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
            Sr5Xml,
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
            ResolvingResolver)).Root!;

        Assert.AreEqual("7", root.Element("karma")!.Value);
        Assert.AreEqual("6000", root.Element("nuyen")!.Value);
    }

    private sealed class ResolvingTestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new ResolvingTestContext();
    }

    private sealed class NonResolvingTestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new NonResolvingTestContext();
    }

    private sealed class ResolvingTestContext : ICharacterSourceDataContext
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

    private sealed class NonResolvingTestContext : ICharacterSourceDataContext
    {
        public bool TryResolveKarmaNuyenExchangeRates(
            out decimal workingForPeopleRate,
            out decimal workingForManRate)
        {
            workingForPeopleRate = 0m;
            workingForManRate = 0m;
            return false;
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
