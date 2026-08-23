using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerSkillSpecializationParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-specialization-tests");
    private static readonly TestResolver Resolver = new();
    private const string ActiveSkillId = "11111111-1111-1111-1111-111111111111";
    private const string ActiveSourceId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string PeerSkillId = "22222222-2222-2222-2222-222222222222";
    private const string PeerSourceId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string KnowledgeSkillId = "33333333-3333-3333-3333-333333333333";
    private const string KnowledgeSourceId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    private const string CustomKnowledgeSkillId = "44444444-4444-4444-4444-444444444444";
    private const string ExistingSpecId = "55555555-5555-5555-5555-555555555555";
    private const string NewSpecId = "66666666-6666-6666-6666-666666666666";
    private const string ExpenseId = "77777777-7777-7777-7777-777777777777";
    private const string ActiveOptionId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string KnowledgeOptionId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SettingsId = "223a11ff-80e0-428b-89a9-6ef1c243b8b6";
    private const string Xml = """
        <character>
          <created>True</created>
          <settings>223a11ff-80e0-428b-89a9-6ef1c243b8b6</settings>
          <karma>30</karma>
          <newskills>
            <skills>
              <skill><guid>11111111-1111-1111-1111-111111111111</guid><suid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</suid><isknowledge>False</isknowledge><skillcategory>Physical Active</skillcategory><karma>1</karma><base>2</base><name>Sneaking</name><specs><spec><guid>55555555-5555-5555-5555-555555555555</guid><name>Old specialization</name><free>False</free><expertise>False</expertise></spec></specs><notes>keep active</notes></skill>
              <skill><guid>22222222-2222-2222-2222-222222222222</guid><suid>bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb</suid><isknowledge>False</isknowledge><skillcategory>Physical Active</skillcategory><karma>0</karma><base>1</base><name>Palming</name><notes>keep peer</notes></skill>
            </skills>
            <knoskills>
              <skill><guid>33333333-3333-3333-3333-333333333333</guid><suid>cccccccc-cccc-cccc-cccc-cccccccccccc</suid><isknowledge>True</isknowledge><skillcategory>Academic</skillcategory><karma>1</karma><base>2</base><name>Matrix Theory</name><type>Academic</type><isnativelanguage>False</isnativelanguage><notes>keep sourced knowledge</notes></skill>
              <skill><guid>44444444-4444-4444-4444-444444444444</guid><suid>00000000-0000-0000-0000-000000000000</suid><isknowledge>True</isknowledge><skillcategory>Street</skillcategory><karma>0</karma><base>2</base><name>Seattle Gangs</name><type>Street</type><isnativelanguage>False</isnativelanguage><notes>keep custom knowledge</notes></skill>
            </knoskills>
            <groups><group><id>88888888-8888-8888-8888-888888888888</id><name>Stealth</name><karma>0</karma><base>1</base><isbroken>False</isbroken></group></groups>
          </newskills>
          <improvements>
            <improvement><improvedname /><min>0</min><val>-1</val><condition>career</condition><improvementttype>SkillCategorySpecializationKarmaCost</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
            <improvement><improvedname>Physical Active</improvedname><min>0</min><val>50</val><condition>career</condition><improvementttype>SkillCategorySpecializationKarmaCostMultiplier</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
            <improvement><improvedname>Physical Active</improvedname><min>0</min><val>99</val><condition>career</condition><improvementttype>SkillCategorySpecializationKarmaCost</improvementttype><addtorating>True</addtorating><enabled>True</enabled></improvement>
            <improvement><improvedname>Sneaking</improvedname><unique>Urban infiltration</unique><condition>career</condition><improvementttype>SkillSpecializationOption</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
          </improvements>
          <expenses>
            <expense><guid>99999999-9999-9999-9999-999999999999</guid><date>2081-05-01T08:00:00</date><amount>-1</amount><reason>Older</reason><type>Karma</type><refund>False</refund><forcecareervisible>False</forcecareervisible></expense>
          </expenses>
          <customstate>preserve me</customstate>
        </character>
        """;

    [TestMethod]
    public void Editor_and_quote_bind_typed_active_knowledge_custom_options_group_and_exact_cost()
    {
        CareerSkillSpecializationEditorState editor = Project(Xml, Resolver);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(9, editor.ContentRevision);
        Assert.AreEqual(0, editor.OmittedSkillCount);
        Assert.HasCount(4, editor.Skills);
        CareerSkillSpecializationCandidate active = editor.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(ActiveSkillId));
        Assert.AreEqual(CharacterCareerSkillKind.Active, active.Identity.Kind);
        Assert.AreEqual(Guid.Parse(ActiveSourceId), active.Identity.SourceSkillId);
        Assert.AreEqual(2, active.TotalBaseRating, "A nonbroken group's base supersedes individual base points under the saved setting.");
        Assert.AreEqual(1, active.ExistingSpecializationCount);
        Assert.IsTrue(active.AvailableOptions.Any(option =>
            option.Kind == CharacterCareerSkillSpecializationOptionKind.Improvement
            && option.Name == "Urban infiltration"));

        CharacterCareerSkillSpecializationQuote quote = Quote(
            Xml,
            active.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Urban",
                CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                ActiveOptionId),
            Resolver);
        Assert.AreEqual(3, quote.KarmaCost, "(7 * 50%) - 1 uses Chummer5 away-from-zero rounding.");
        Assert.IsTrue(quote.CanAdd);
        Assert.IsTrue(quote.WillBreakSkillGroup);
        Assert.AreEqual(2, quote.EnabledSkillGroupMemberCount);
        Assert.AreEqual(64, quote.CharacterRevision.Length);
        Assert.AreEqual(64, quote.SourceRevision.Length);
        Assert.AreEqual(64, quote.RuleDigest.Length);
        Assert.AreEqual(64, quote.LogicalRevision.Length);

        CareerSkillSpecializationCandidate customKnowledge = editor.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(CustomKnowledgeSkillId));
        Assert.AreEqual(CharacterCareerSkillKind.Knowledge, customKnowledge.Identity.Kind);
        Assert.IsNull(customKnowledge.Identity.SourceSkillId);
        CharacterCareerSkillSpecializationQuote customQuote = Quote(
            Xml,
            customKnowledge.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Halloweeners",
                CharacterCareerSkillSpecializationOptionKind.Custom,
                OptionIdentity: null),
            Resolver);
        Assert.AreEqual(2, customQuote.KarmaCost, "Knowledge specialization uses its separate 3 Karma setting, then global -1.");
        Assert.IsFalse(customQuote.WillBreakSkillGroup);
    }

    [TestMethod]
    public void Apply_adds_exact_spec_spends_karma_and_writes_chummer5_expense_undo_then_reopens()
    {
        CareerSkillSpecializationCandidate active = Project(Xml, Resolver).Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(ActiveSkillId));
        CharacterCareerSkillSpecializationQuote quote = Quote(
            Xml,
            active.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Urban",
                CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                ActiveOptionId),
            Resolver);
        CareerSkillSpecializationRequest request = Request(quote, Confirmed: true);

        string result = WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
            Xml,
            request,
            settingsCatalogJson: null,
            Resolver);
        XElement root = XDocument.Parse(result).Root!;
        XElement activeSkill = root.Element("newskills")!.Element("skills")!.Elements("skill")
            .Single(skill => skill.Element("guid")!.Value == ActiveSkillId);
        XElement[] specs = activeSkill.Element("specs")!.Elements("spec").ToArray();
        Assert.HasCount(2, specs);
        XElement added = specs.Single(spec => spec.Element("guid")!.Value == NewSpecId);
        Assert.AreEqual("Urban", added.Element("name")!.Value);
        Assert.AreEqual("False", added.Element("free")!.Value);
        Assert.AreEqual("False", added.Element("expertise")!.Value);
        Assert.AreEqual("keep active", activeSkill.Element("notes")!.Value);
        Assert.AreEqual("keep peer", root.Element("newskills")!.Element("skills")!.Elements("skill")
            .Single(skill => skill.Element("guid")!.Value == PeerSkillId).Element("notes")!.Value);
        Assert.AreEqual("27", root.Element("karma")!.Value);
        Assert.AreEqual("preserve me", root.Element("customstate")!.Value);

        XElement expense = root.Element("expenses")!.Elements("expense").First();
        Assert.AreEqual(ExpenseId, expense.Element("guid")!.Value);
        Assert.AreEqual("-3", expense.Element("amount")!.Value);
        Assert.AreEqual("Learned Specialization Sneaking (Urban)", expense.Element("reason")!.Value);
        XElement undo = expense.Element("undo")!;
        Assert.AreEqual("AddSpecialization", undo.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", undo.Element("nuyentype")!.Value);
        Assert.AreEqual(NewSpecId, undo.Element("objectid")!.Value);
        Assert.AreEqual("0", undo.Element("qty")!.Value);
        Assert.AreEqual(string.Empty, undo.Element("extra")!.Value);

        CareerSkillSpecializationEditorState reopenedOnce = Project(result, Resolver);
        CareerSkillSpecializationEditorState reopenedAfterFirstRestart = Project(result, Resolver);
        CareerSkillSpecializationEditorState reopenedAfterSecondRestart = Project(result, Resolver);
        CollectionAssert.AreEqual(
            reopenedOnce.Skills.Select(CandidateSnapshot).ToArray(),
            reopenedAfterFirstRestart.Skills.Select(CandidateSnapshot).ToArray());
        CollectionAssert.AreEqual(
            reopenedOnce.Skills.Select(CandidateSnapshot).ToArray(),
            reopenedAfterSecondRestart.Skills.Select(CandidateSnapshot).ToArray());
        Assert.AreEqual(2, reopenedOnce.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(ActiveSkillId)).ExistingSpecializationCount);
    }

    [TestMethod]
    public void Confirmation_four_revision_staleness_and_duplicate_ids_fail_closed()
    {
        CareerSkillSpecializationCandidate active = Project(Xml, Resolver).Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(ActiveSkillId));
        CharacterCareerSkillSpecializationQuote quote = Quote(
            Xml,
            active.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Urban",
                CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                ActiveOptionId),
            Resolver);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml,
                Request(quote, Confirmed: false),
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml.Replace("<karma>30</karma>", "<karma>29</karma>", StringComparison.Ordinal),
                Request(quote, Confirmed: true),
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml,
                Request(quote, Confirmed: true) with { ExpectedRuleDigest = new string('0', 64) },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml,
                Request(quote, Confirmed: true),
                null,
                new TestResolver(activeCost: 8)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml,
                Request(quote, Confirmed: true),
                null,
                new TestResolver(sourceSuffix: "changed")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml,
                Request(quote, Confirmed: true) with { SpecializationId = Guid.Parse(ExistingSpecId) },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillSpecialization(
                Xml.Replace(
                    "99999999-9999-9999-9999-999999999999",
                    ExpenseId,
                    StringComparison.Ordinal),
                Request(quote, Confirmed: true),
                null,
                Resolver));
    }

    [TestMethod]
    public void Native_disabled_and_blocked_skills_expose_exact_blockers_while_bad_identity_fails_closed()
    {
        CareerSkillSpecializationCandidate knowledge = Project(Xml, Resolver).Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(KnowledgeSkillId));
        string nativeXml = Xml.Replace(
            "<name>Matrix Theory</name><type>Academic</type><isnativelanguage>False</isnativelanguage>",
            "<name>Matrix Theory</name><type>Academic</type><isnativelanguage>True</isnativelanguage>",
            StringComparison.Ordinal);
        CharacterCareerSkillSpecializationQuote native = Quote(
            nativeXml,
            knowledge.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Hosts",
                CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                KnowledgeOptionId),
            Resolver);
        Assert.AreEqual(CharacterCareerSkillSpecializationBlocker.NativeLanguage, native.Blocker);
        Assert.IsFalse(native.CanAdd);

        string blockedImprovement = "<improvement><improvedname>Sneaking</improvedname><condition>career</condition><improvementttype>BlockSkillSpecializations</improvementttype><enabled>True</enabled></improvement>";
        string blockedXml = Xml.Replace("</improvements>", blockedImprovement + "</improvements>", StringComparison.Ordinal);
        CareerSkillSpecializationCandidate active = Project(blockedXml, Resolver).Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(ActiveSkillId));
        CharacterCareerSkillSpecializationQuote blocked = Quote(
            blockedXml,
            active.Identity,
            new CharacterCareerSkillSpecializationSelection(
                "Urban",
                CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                ActiveOptionId),
            Resolver);
        Assert.AreEqual(CharacterCareerSkillSpecializationBlocker.SkillSpecializationsBlocked, blocked.Blocker);

        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(PeerSkillId, ActiveSkillId, StringComparison.Ordinal),
            Resolver));
        CareerSkillSpecializationEditorState unsupported = Project(
            Xml.Replace(PeerSourceId, "dddddddd-dddd-dddd-dddd-dddddddddddd", StringComparison.Ordinal),
            Resolver);
        Assert.IsGreaterThanOrEqualTo(1, unsupported.OmittedSkillCount);
    }

    private static CareerSkillSpecializationEditorState Project(string xml, TestResolver resolver)
        => CareerSkillSpecializationEditorProjector.Project(
            xml,
            WorkspaceId,
            9,
            settingsCatalogJson: null,
            resolver);

    private static CharacterCareerSkillSpecializationQuote Quote(
        string xml,
        CharacterCareerSkillIdentity identity,
        CharacterCareerSkillSpecializationSelection selection,
        TestResolver resolver)
        => CareerSkillSpecializationEditorProjector.ProjectQuote(
            xml,
            WorkspaceId,
            9,
            identity,
            selection,
            settingsCatalogJson: null,
            resolver);

    private static CareerSkillSpecializationRequest Request(
        CharacterCareerSkillSpecializationQuote quote,
        bool Confirmed)
        => new(
            WorkspaceId,
            ExpectedContentRevision: 9,
            quote,
            quote.CharacterRevision,
            quote.SourceRevision,
            quote.RuleDigest,
            quote.LogicalRevision,
            Confirmed,
            Guid.Parse(NewSpecId),
            Guid.Parse(ExpenseId),
            new DateTime(2081, 5, 12, 14, 30, 0));

    private static string CandidateSnapshot(CareerSkillSpecializationCandidate candidate)
        => string.Join(
            "|",
            candidate.Identity.SkillId.ToString("D"),
            candidate.Identity.SourceSkillId?.ToString("D") ?? "custom",
            candidate.Identity.Kind,
            candidate.SkillName,
            candidate.SkillCategory,
            candidate.SkillGroup,
            candidate.TotalBaseRating,
            candidate.ExistingSpecializationCount,
            string.Join(",", candidate.AvailableOptions.Select(option =>
                $"{option.OptionIdentity}:{option.Kind}:{option.Name}:{option.SourceAnchor}")));

    private sealed class TestResolver(int activeCost = 7, string sourceSuffix = "")
        : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml)
            => new TestContext(activeCost, sourceSuffix);
    }

    private sealed class TestContext(int activeCost, string sourceSuffix)
        : ICharacterSourceDataContext
    {
        public bool TryResolveCareerSkillSpecializationSettings(
            out CharacterCareerSkillSpecializationSettings settings,
            out string rawRuleState)
        {
            settings = new CharacterCareerSkillSpecializationSettings(
                activeCost,
                KarmaKnowledgeSpecialization: 3,
                SpecializationsBreakSkillGroups: true);
            rawRuleState = $"<settings><active>{activeCost}</active><knowledge>3</knowledge><break>True</break></settings>";
            return true;
        }

        public bool TryResolveCareerSkillSpecializationSource(
            string sourceSkillId,
            CharacterCareerSkillKind kind,
            out CharacterCareerSkillSpecializationSource source)
        {
            source = (sourceSkillId, kind) switch
            {
                (ActiveSourceId, CharacterCareerSkillKind.Active) => SpecializationSource(
                    ActiveSourceId,
                    kind,
                    "Sneaking",
                    "Physical Active",
                    ActiveOptionId,
                    "Urban"),
                (PeerSourceId, CharacterCareerSkillKind.Active) => SpecializationSource(
                    PeerSourceId,
                    kind,
                    "Palming",
                    "Physical Active",
                    new string('c', 64),
                    "Legerdemain"),
                (KnowledgeSourceId, CharacterCareerSkillKind.Knowledge) => SpecializationSource(
                    KnowledgeSourceId,
                    kind,
                    "Matrix Theory",
                    "Academic",
                    KnowledgeOptionId,
                    "Hosts"),
                _ => CharacterCareerSkillSpecializationSource.Unavailable
            };
            return source != CharacterCareerSkillSpecializationSource.Unavailable;
        }

        public bool TryResolveActiveSkillSource(
            string sourceSkillId,
            out CharacterActiveSkillSource source)
        {
            source = sourceSkillId switch
            {
                ActiveSourceId => ActiveSource(ActiveSourceId, "Sneaking"),
                PeerSourceId => ActiveSource(PeerSourceId, "Palming"),
                _ => CharacterActiveSkillSource.Unavailable
            };
            return source != CharacterActiveSkillSource.Unavailable;
        }

        private CharacterCareerSkillSpecializationSource SpecializationSource(
            string id,
            CharacterCareerSkillKind kind,
            string name,
            string category,
            string optionId,
            string optionName)
            => new(
                id,
                kind,
                name,
                category,
                [new CharacterCareerSkillSpecializationOption(
                    optionId,
                    optionName,
                    CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                    $"skills.xml:{id}:{sourceSuffix}")],
                $"<skill><id>{id}</id><name>{name}</name><suffix>{sourceSuffix}</suffix></skill>");

        private CharacterActiveSkillSource ActiveSource(string id, string name)
            => new(
                id,
                name,
                "Physical Active",
                "Stealth",
                "AGI",
                false,
                false,
                false,
                false,
                $"<skill><id>{id}</id><name>{name}</name><suffix>{sourceSuffix}</suffix></skill>");

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
