using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerSkillGroupAdvanceParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-skill-group-tests");
    private static readonly TestResolver Resolver = new();
    private const string GroupId = "11111111-1111-1111-1111-111111111111";
    private const string SkillId = "22222222-2222-2222-2222-222222222222";
    private const string SkillSourceId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OtherSkillId = "33333333-3333-3333-3333-333333333333";
    private const string OtherSourceId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string ExpenseId = "44444444-4444-4444-4444-444444444444";
    private const string SettingsId = "223a11ff-80e0-428b-89a9-6ef1c243b8b6";
    private const string Xml = """
        <character>
          <created>True</created>
          <settings>223a11ff-80e0-428b-89a9-6ef1c243b8b6</settings>
          <karma>40</karma>
          <newskills>
            <skills>
              <skill><guid>22222222-2222-2222-2222-222222222222</guid><suid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</suid><isknowledge>False</isknowledge><skillcategory>Physical Active</skillcategory><karma>0</karma><base>0</base><notes>keep first</notes></skill>
              <skill><guid>33333333-3333-3333-3333-333333333333</guid><suid>bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb</suid><isknowledge>False</isknowledge><skillcategory>Physical Active</skillcategory><karma>0</karma><base>0</base><notes>keep second</notes></skill>
            </skills>
            <groups>
              <group><id>11111111-1111-1111-1111-111111111111</id><name>Stealth</name><karma>1</karma><base>2</base><isbroken>False</isbroken><notes>keep group</notes></group>
            </groups>
          </newskills>
          <improvements />
          <expenses>
            <expense><guid>55555555-5555-5555-5555-555555555555</guid><date>2081-05-01T08:00:00</date><amount>-1</amount><reason>Older</reason><type>Karma</type><refund>False</refund><forcecareervisible>False</forcecareervisible></expense>
          </expenses>
          <customstate>preserve me</customstate>
        </character>
        """;

    [TestMethod]
    public void Projection_binds_exact_group_guid_members_settings_and_chummer5_cost()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.AreEqual(0, editor.OmittedSkillGroupCount);
        Assert.HasCount(1, editor.SkillGroups);
        CharacterCareerSkillGroupAdvanceQuote group = editor.SkillGroups.Single();
        Assert.AreEqual(Guid.Parse(GroupId), group.Identity.InternalId);
        Assert.AreEqual("Stealth", group.Name);
        Assert.AreEqual(3, group.GroupRating);
        Assert.AreEqual(4, group.TargetGroupRating);
        Assert.AreEqual(3, group.CostRating);
        Assert.AreEqual(4, group.TargetCostRating);
        Assert.AreEqual(2, group.EnabledMemberCount);
        Assert.AreEqual(20, group.KarmaCost);
        Assert.IsTrue(group.CanAdvance);
        Assert.AreEqual(64, group.SourceRevision.Length);
        Assert.AreEqual(64, group.RuleDigest.Length);
    }

    [TestMethod]
    public void Apply_updates_only_exact_group_karma_and_sorted_expense_with_undo()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CharacterCareerSkillGroupAdvanceQuote selected = editor.SkillGroups.Single();
        CareerSkillGroupAdvanceRequest request = new(
            WorkspaceId,
            editor.ContentRevision,
            selected,
            selected.RuleDigest,
            Confirmed: true,
            Guid.Parse(ExpenseId),
            new DateTime(2081, 5, 12, 14, 30, 0));

        string result = WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
            Xml,
            request,
            settingsCatalogJson: null,
            Resolver);
        XElement root = XDocument.Parse(result).Root!;
        XElement group = root.Element("newskills")!.Element("groups")!.Element("group")!;
        Assert.AreEqual(GroupId, group.Element("id")!.Value);
        Assert.AreEqual("2", group.Element("karma")!.Value);
        Assert.AreEqual("2", group.Element("base")!.Value);
        Assert.AreEqual("keep group", group.Element("notes")!.Value);
        Assert.AreEqual("20", root.Element("karma")!.Value);
        Assert.AreEqual("preserve me", root.Element("customstate")!.Value);
        Assert.IsTrue(root.Element("newskills")!.Element("skills")!.Elements("skill")
            .All(skill => skill.Element("karma")!.Value == "0"
                && skill.Element("base")!.Value == "0"));

        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.HasCount(2, expenses);
        XElement added = expenses[0];
        Assert.AreEqual(ExpenseId, added.Element("guid")!.Value);
        Assert.AreEqual("2081-05-12T14:30:00", added.Element("date")!.Value);
        Assert.AreEqual("-20", added.Element("amount")!.Value);
        Assert.AreEqual("Skill Group Stealth 3 -> 4", added.Element("reason")!.Value);
        Assert.AreEqual("True", added.Element("forcecareervisible")!.Value);
        XElement undo = added.Element("undo")!;
        Assert.AreEqual("ImproveSkillGroup", undo.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", undo.Element("nuyentype")!.Value);
        Assert.AreEqual(GroupId, undo.Element("objectid")!.Value);
        Assert.AreEqual("0", undo.Element("qty")!.Value);
        Assert.AreEqual(string.Empty, undo.Element("extra")!.Value);

        CareerSkillGroupAdvanceEditorState reopenedOnce = Project(result);
        CareerSkillGroupAdvanceEditorState reopenedAfterFirstRestart = Project(result);
        CareerSkillGroupAdvanceEditorState reopenedAfterSecondRestart = Project(result);
        AssertQuoteEquivalent(
            reopenedOnce.SkillGroups.Single(),
            reopenedAfterFirstRestart.SkillGroups.Single());
        AssertQuoteEquivalent(
            reopenedOnce.SkillGroups.Single(),
            reopenedAfterSecondRestart.SkillGroups.Single());
        CharacterCareerSkillGroupAdvanceQuote reopened = reopenedOnce.SkillGroups.Single();
        Assert.AreEqual(2, reopened.KarmaPoints);
        Assert.AreEqual(4, reopened.GroupRating);
        Assert.AreEqual(4, reopened.CostRating);
        Assert.AreEqual(2, reopened.EnabledMemberCount);
        Assert.AreEqual(20, reopened.AvailableKarma);
    }

    [TestMethod]
    public void Confirmation_stale_rules_duplicate_expense_and_broken_group_fail_closed()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CharacterCareerSkillGroupAdvanceQuote selected = editor.SkillGroups.Single();
        CareerSkillGroupAdvanceRequest request = new(
            WorkspaceId,
            editor.ContentRevision,
            selected,
            selected.RuleDigest,
            Confirmed: false,
            Guid.Parse(ExpenseId),
            new DateTime(2081, 5, 12));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                request,
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml.Replace("<karma>40</karma>", "<karma>39</karma>", StringComparison.Ordinal),
                request with { Confirmed = true },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml.Replace(
                    "55555555-5555-5555-5555-555555555555",
                    ExpenseId,
                    StringComparison.Ordinal),
                request with { Confirmed = true },
                null,
                Resolver));

        CareerSkillGroupAdvanceEditorState broken = Project(
            Xml.Replace("<isbroken>False</isbroken>", "<isbroken>True</isbroken>", StringComparison.Ordinal));
        Assert.AreEqual(CharacterCareerSkillGroupAdvanceBlocker.Broken, broken.SkillGroups.Single().Blocker);
        Assert.IsFalse(broken.SkillGroups.Single().CanAdvance);
    }

    [TestMethod]
    public void Duplicate_identity_unresolved_member_and_rating_improvement_fail_closed_or_omit()
    {
        string duplicateGroup = "<group><id>" + GroupId + "</id><name>Other</name><karma>0</karma><base>0</base><isbroken>False</isbroken></group>";
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("</groups>", duplicateGroup + "</groups>", StringComparison.Ordinal)));

        CareerSkillGroupAdvanceEditorState unresolved = Project(
            Xml.Replace(OtherSourceId, "cccccccc-cccc-cccc-cccc-cccccccccccc", StringComparison.Ordinal));
        Assert.AreEqual(1, unresolved.OmittedSkillGroupCount);
        Assert.HasCount(0, unresolved.SkillGroups);

        string ratingImprovement = "<improvement><improvedname>Stealth</improvedname><val>1</val><condition>career</condition><improvementttype>SkillGroupLevel</improvementttype><addtorating>True</addtorating><enabled>True</enabled></improvement>";
        CareerSkillGroupAdvanceEditorState unsupported = Project(
            Xml.Replace("<improvements />", "<improvements>" + ratingImprovement + "</improvements>", StringComparison.Ordinal));
        Assert.AreEqual(1, unsupported.OmittedSkillGroupCount);
        Assert.HasCount(0, unsupported.SkillGroups);
    }

    private static CareerSkillGroupAdvanceEditorState Project(string xml)
        => CareerSkillGroupAdvanceEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            settingsCatalogJson: null,
            Resolver);

    private static void AssertQuoteEquivalent(
        CharacterCareerSkillGroupAdvanceQuote expected,
        CharacterCareerSkillGroupAdvanceQuote actual)
    {
        Assert.AreEqual(expected.Identity, actual.Identity);
        Assert.AreEqual(expected.Name, actual.Name);
        Assert.AreEqual(expected.BasePoints, actual.BasePoints);
        Assert.AreEqual(expected.KarmaPoints, actual.KarmaPoints);
        Assert.AreEqual(expected.GroupRating, actual.GroupRating);
        Assert.AreEqual(expected.CostRating, actual.CostRating);
        Assert.AreEqual(expected.TargetGroupRating, actual.TargetGroupRating);
        Assert.AreEqual(expected.TargetCostRating, actual.TargetCostRating);
        Assert.AreEqual(expected.EnabledMemberCount, actual.EnabledMemberCount);
        Assert.AreEqual(expected.RatingMaximum, actual.RatingMaximum);
        Assert.AreEqual(expected.AvailableKarma, actual.AvailableKarma);
        Assert.AreEqual(expected.Disabled, actual.Disabled);
        Assert.AreEqual(expected.Broken, actual.Broken);
        Assert.AreEqual(expected.KarmaCost, actual.KarmaCost);
        Assert.AreEqual(expected.ApplicationDuration, actual.ApplicationDuration);
        Assert.AreEqual(expected.TimeAuthority, actual.TimeAuthority);
        CollectionAssert.AreEqual(
            expected.Prerequisites.ToArray(),
            actual.Prerequisites.ToArray());
        Assert.AreEqual(expected.CanAdvance, actual.CanAdvance);
        Assert.AreEqual(expected.Blocker, actual.Blocker);
        Assert.AreEqual(expected.LogicalRevision, actual.LogicalRevision);
        Assert.AreEqual(expected.SourceRevision, actual.SourceRevision);
        Assert.AreEqual(expected.RuleDigest, actual.RuleDigest);
    }

    private sealed class TestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new TestContext();
    }

    private sealed class TestContext : ICharacterSourceDataContext
    {
        public bool TryResolveActiveSkillSource(
            string sourceSkillId,
            out CharacterActiveSkillSource source)
        {
            source = sourceSkillId switch
            {
                SkillSourceId => Source(SkillSourceId, "Sneaking"),
                OtherSourceId => Source(OtherSourceId, "Palming"),
                _ => CharacterActiveSkillSource.Unavailable
            };
            return source != CharacterActiveSkillSource.Unavailable;
        }

        private static CharacterActiveSkillSource Source(string id, string name)
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
                $"<skill><id>{id}</id><name>{name}</name><skillgroup>Stealth</skillgroup></skill>");

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
