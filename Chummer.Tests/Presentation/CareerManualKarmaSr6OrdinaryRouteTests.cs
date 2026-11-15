using System.Xml;
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

    [TestMethod]
    public void Ambiguous_duplicate_editions_refuse_karma_paths_in_both_orders()
    {
        // SR6-first duplicate previously gained the neutral fallback; SR5-first
        // previously evaded the exchange refusal. Both orders must now refuse.
        string sr6First = Sr6Xml.Replace(
            "<gameedition>SR6</gameedition>",
            "<gameedition>SR6</gameedition><gameedition>SR5</gameedition>");
        string sr5First = Sr6Xml.Replace(
            "<gameedition>SR6</gameedition>",
            "<gameedition>SR5</gameedition><gameedition>SR6</gameedition>");

        InvalidOperationException firstOrdinary = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(sr6First, WorkspaceId, 9, NonResolvingResolver));
        StringAssert.Contains(firstOrdinary.Message, "ambiguous game edition");
        InvalidOperationException secondOrdinary = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(sr5First, WorkspaceId, 9, NonResolvingResolver));
        StringAssert.Contains(secondOrdinary.Message, "ambiguous game edition");

        // Exchange requests refuse with the same ambiguity contract, not the
        // ordinary SR6 exchange refusal, in both duplicate orders.
        CareerManualKarmaEditorState validEditor = CareerManualKarmaEditorProjector.Project(
            Sr6Xml, WorkspaceId, 9, NonResolvingResolver);
        foreach (string ambiguousXml in new[] { sr6First, sr5First })
        {
            InvalidOperationException exchange = Assert.ThrowsExactly<InvalidOperationException>(() =>
                WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(
                    ambiguousXml,
                    new CareerManualKarmaEditRequest(
                        WorkspaceId,
                        9,
                        validEditor.Karma,
                        CharacterCareerManualKarmaAction.Gain,
                        2,
                        "Working for the People",
                        new DateTime(2023, 12, 1, 8, 30, 0),
                        Refund: false,
                        KarmaNuyenExchange: true,
                        ForceCareerVisible: false),
                    NonResolvingResolver));
            StringAssert.Contains(exchange.Message, "ambiguous game edition");
        }
    }

    [TestMethod]
    public void Ambiguous_duplicate_editions_refuse_nuyen_paths_in_both_orders()
    {
        string sr6First = Sr6Xml.Replace(
            "<gameedition>SR6</gameedition>",
            "<gameedition>SR6</gameedition><gameedition>SR5</gameedition>");
        string sr5First = Sr6Xml.Replace(
            "<gameedition>SR6</gameedition>",
            "<gameedition>SR5</gameedition><gameedition>SR6</gameedition>");

        foreach (string ambiguousXml in new[] { sr6First, sr5First })
        {
            InvalidOperationException ordinary = Assert.ThrowsExactly<InvalidOperationException>(() =>
                CareerManualNuyenEditorProjector.Project(ambiguousXml, WorkspaceId, 9, NonResolvingResolver));
            StringAssert.Contains(ordinary.Message, "ambiguous game edition");

            InvalidOperationException exchange = Assert.ThrowsExactly<InvalidOperationException>(() =>
                WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(
                    ambiguousXml,
                    new CareerManualNuyenEditRequest(
                        WorkspaceId,
                        9,
                        new CharacterCareerManualNuyenState(
                            AvailableKarma: 5,
                            AvailableNuyen: 10000m,
                            NuyenPerKarmaWorkingForPeople: 1m,
                            NuyenPerKarmaWorkingForMan: 1m),
                        CharacterCareerManualNuyenAction.Gain,
                        10000,
                        50m,
                        "Salary",
                        new DateTime(2023, 12, 1, 8, 30, 0),
                        Refund: false,
                        KarmaNuyenExchange: true,
                        ForceCareerVisible: false),
                    NonResolvingResolver));
            StringAssert.Contains(exchange.Message, "ambiguous game edition");
        }
    }

    [TestMethod]
    public void Ambiguous_nested_edition_refuses_before_accepting_resolver_rates()
    {
        // One direct SR5 child plus a nested SR6 element is ambiguous even when
        // a resolver would happily provide rates: validation precedes rate
        // acceptance (Root review 8063 ordering requirement).
        string nested = Sr5Xml.Replace(
            "<customstate><karma>keep</karma></customstate>",
            "<customstate><karma>keep</karma><gameedition>SR6</gameedition></customstate>");

        InvalidOperationException karmaProjection = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(nested, WorkspaceId, 9, ResolvingResolver));
        StringAssert.Contains(karmaProjection.Message, "ambiguous game edition");

        InvalidOperationException nuyenProjection = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualNuyenEditorProjector.Project(nested, WorkspaceId, 9, ResolvingResolver));
        StringAssert.Contains(nuyenProjection.Message, "ambiguous game edition");
    }

    [TestMethod]
    public void Single_canonical_edition_and_legacy_no_edition_behavior_unchanged()
    {
        // A single direct SR5 child plus a nested element carrying the SAME
        // value is still ambiguous by construction (two gameedition elements
        // in the document): the duplication is what refuses, not the value.
        string sr5NestedSame = Sr5Xml.Replace(
            "<customstate><karma>keep</karma></customstate>",
            "<customstate><karma>keep</karma><gameedition>SR5</gameedition></customstate>");
        InvalidOperationException nestedSame = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(sr5NestedSame, WorkspaceId, 9, NonResolvingResolver));
        StringAssert.Contains(nestedSame.Message, "ambiguous game edition");

        // Legacy no-edition documents keep the exact previous behavior: no
        // edition default is invented; non-resolving rates fail closed.
        string noEdition = Sr5Xml.Replace("<gameedition>SR5</gameedition>", string.Empty);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualKarmaEditorProjector.Project(noEdition, WorkspaceId, 9, NonResolvingResolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerManualNuyenEditorProjector.Project(noEdition, WorkspaceId, 9, NonResolvingResolver));

        // A supported single canonical SR6 alias keeps the neutral fallback and
        // projects to the exact same state as the plain SR6 document. The alias
        // set is whatever the pre-existing IsSr6Ruleset normalizer accepted
        // (proven for lowercase sr6 by the passing ordinary-route tests); no
        // new alias is invented here. The "Shadowrun 6" display alias is mapped
        // by CharacterOverviewPresenter.Workspace before reaching this gate,
        // matching the pre-correction behavior.
        string sr6Alias = Sr6Xml.Replace("<gameedition>SR6</gameedition>", "<gameedition>sr6</gameedition>");
        CareerManualKarmaEditorState baselineEditor = CareerManualKarmaEditorProjector.Project(
            Sr6Xml, WorkspaceId, 9, NonResolvingResolver);
        CareerManualKarmaEditorState aliasEditor = CareerManualKarmaEditorProjector.Project(
            sr6Alias, WorkspaceId, 9, NonResolvingResolver);
        Assert.AreEqual(baselineEditor.Karma, aliasEditor.Karma);
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

[TestClass]
public sealed class CareerManualExpenseDateTests
{
    private static readonly CharacterWorkspaceId Workspace = new("manual-expense-dates");
    private static readonly DateTime Second = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Unspecified);

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(10)]
    [DataRow(100)]
    [DataRow(1000)]
    [DataRow(10000)]
    [DataRow(100000)]
    [DataRow(1000000)]
    public void Core_expense_timestamp_allows_later_manual_rewards_without_losing_precision(int ticks)
    {
        // Use the actual Core writer's format, including its omitted trailing zeros.
        string date = XmlConvert.ToString(Second.AddTicks(ticks), XmlDateTimeSerializationMode.Unspecified);
        string original = Character(new XElement("date", date));
        foreach (bool nuyen in new[] { false, true })
        {
            XElement result = XDocument.Parse(Apply(original, nuyen)).Root!;
            Assert.AreEqual(nuyen ? "10050" : "10000", result.Element("nuyen")!.Value);
            Assert.AreEqual(nuyen ? "5" : "7", result.Element("karma")!.Value);
            XElement[] expenses = result.Element("expenses")!.Elements("expense").ToArray();
            Assert.AreEqual(2, expenses.Length);
            XElement existing = expenses.Single(e => e.Element("reason")!.Value == "Core advancement");
            Assert.AreEqual(date, existing.Element("date")!.Value);
            Assert.AreEqual("-3", existing.Element("amount")!.Value);
            Assert.AreSame(existing, expenses[ticks == 0 ? 0 : 1]);
            Assert.AreEqual("keep", result.Element("customstate")!.Value);
        }
    }

    [DataTestMethod]
    [DataRow("2026-11-15T12:00:00Z")]
    [DataRow("2026-11-15T12:00:00.1234567Z")]
    [DataRow("2026-11-15T12:00:00.123+01:00")]
    [DataRow("2026-11-15T12:00:00.12345678")]
    [DataRow("2026-11-15")]
    [DataRow("not-a-date")]
    public void Malformed_or_timezone_bearing_existing_dates_still_refuse_manual_rewards(string date)
    {
        string original = Character(new XElement("date", date));
        foreach (bool nuyen in new[] { false, true })
            Assert.ThrowsExactly<InvalidOperationException>(() => Apply(original, nuyen));
    }

    [TestMethod]
    public void Missing_or_duplicate_existing_dates_still_refuse_manual_rewards()
    {
        foreach (XElement[] dates in new[]
        {
            Array.Empty<XElement>(),
            new[] { new XElement("date", "2026-11-15T12:00:00"), new XElement("date", "2026-11-15T12:00:00") }
        })
        {
            string original = Character(dates);
            foreach (bool nuyen in new[] { false, true })
                Assert.ThrowsExactly<InvalidOperationException>(() => Apply(original, nuyen));
        }
    }

    private static string Character(params XElement[] dates) => new XElement("character",
        new XElement("gameedition", "SR6"), new XElement("created", "True"),
        new XElement("karma", "5"), new XElement("nuyen", "10000"),
        new XElement("expenses", new XElement("expense", dates,
            new XElement("guid", "00000000-0000-0000-0000-000000000001"),
            new XElement("amount", "-3"), new XElement("type", "Karma"),
            new XElement("reason", "Core advancement"), new XElement("refund", "False"))),
        new XElement("customstate", "keep")).ToString(SaveOptions.DisableFormatting);

    private static string Apply(string xml, bool nuyen) => nuyen
        ? WorkspaceXmlMutationCatalog.ApplyCareerManualNuyenEdit(xml,
            new CareerManualNuyenEditRequest(Workspace, 9,
                CareerManualNuyenEditorProjector.Project(xml, Workspace, 9, null).Nuyen,
                CharacterCareerManualNuyenAction.Gain, 50, 100m, "Manual reward", Second,
                Refund: false, KarmaNuyenExchange: false, ForceCareerVisible: false), null)
        : WorkspaceXmlMutationCatalog.ApplyCareerManualKarmaEdit(xml,
            new CareerManualKarmaEditRequest(Workspace, 9,
                CareerManualKarmaEditorProjector.Project(xml, Workspace, 9, null).Karma,
                CharacterCareerManualKarmaAction.Gain, 2, "Manual reward", Second,
                Refund: false, KarmaNuyenExchange: false, ForceCareerVisible: false), null);
}
