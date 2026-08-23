using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerActiveSkillAdvanceParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-active-skill-tests");
    private static readonly TestResolver Resolver = new();
    private const string SkillId = "11111111-1111-1111-1111-111111111111";
    private const string SkillSourceId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OtherSkillId = "22222222-2222-2222-2222-222222222222";
    private const string OtherSourceId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string ExpenseId = "33333333-3333-3333-3333-333333333333";
    private const string SettingsId = "223a11ff-80e0-428b-89a9-6ef1c243b8b6";
    private const string Xml = """
        <character>
          <created>True</created>
          <settings>223a11ff-80e0-428b-89a9-6ef1c243b8b6</settings>
          <karma>20</karma>
          <newskills>
            <skills>
              <skill><guid>11111111-1111-1111-1111-111111111111</guid><suid>aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa</suid><isknowledge>False</isknowledge><skillcategory>Physical Active</skillcategory><karma>1</karma><base>2</base><notes>keep target</notes></skill>
              <skill><guid>22222222-2222-2222-2222-222222222222</guid><suid>bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb</suid><isknowledge>False</isknowledge><skillcategory>Vehicle Active</skillcategory><karma>0</karma><base>1</base><notes>keep other</notes></skill>
            </skills>
            <groups />
          </newskills>
          <improvements>
            <improvement><improvedname /><sourcename>quality-one</sourcename><min>0</min><max>0</max><val>-1</val><condition>career</condition><improvementttype>ActiveSkillKarmaCost</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
            <improvement><improvedname>Physical Active</improvedname><sourcename>quality-two</sourcename><min>0</min><max>0</max><val>50</val><condition>career</condition><improvementttype>SkillCategoryKarmaCostMultiplier</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
          </improvements>
          <expenses>
            <expense><guid>44444444-4444-4444-4444-444444444444</guid><date>2081-05-01T08:00:00</date><amount>-1</amount><reason>Older</reason><type>Karma</type><refund>False</refund><forcecareervisible>False</forcecareervisible></expense>
          </expenses>
          <customstate>preserve me</customstate>
        </character>
        """;

    [TestMethod]
    public void Projection_binds_exact_instance_source_rules_and_chummer5_cost()
    {
        CareerActiveSkillAdvanceEditorState editor = Project(Xml);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.AreEqual(0, editor.OmittedSkillCount);
        Assert.HasCount(2, editor.Skills);
        CharacterCareerActiveSkillAdvanceQuote sneaking = editor.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(SkillId));
        Assert.AreEqual(Guid.Parse(SkillSourceId), sneaking.Identity.SourceSkillId);
        Assert.AreEqual("Sneaking", sneaking.Name);
        Assert.AreEqual(3, sneaking.TotalBaseRating);
        Assert.AreEqual(3, sneaking.KarmaCost, "(4 * 2 * 50%) - 1 uses Chummer5 rounding.");
        Assert.IsTrue(sneaking.CanAdvance);
        Assert.AreEqual(64, sneaking.SourceRevision.Length);
        Assert.AreEqual(64, sneaking.RuleDigest.Length);
    }

    [TestMethod]
    public void Apply_updates_only_exact_guid_karma_and_sorted_expense_with_undo()
    {
        CareerActiveSkillAdvanceEditorState editor = Project(Xml);
        CharacterCareerActiveSkillAdvanceQuote selected = editor.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(SkillId));
        CareerActiveSkillAdvanceRequest request = new(
            WorkspaceId,
            editor.ContentRevision,
            selected,
            selected.RuleDigest,
            Confirmed: true,
            Guid.Parse(ExpenseId),
            new DateTime(2081, 5, 12, 14, 30, 0));

        string result = WorkspaceXmlMutationCatalog.ApplyCareerActiveSkillAdvance(
            Xml,
            request,
            settingsCatalogJson: null,
            Resolver);
        XElement root = XDocument.Parse(result).Root!;
        XElement[] skills = root.Element("newskills")!.Element("skills")!.Elements("skill").ToArray();
        Assert.AreEqual("2", skills.Single(skill => skill.Element("guid")!.Value == SkillId).Element("karma")!.Value);
        Assert.AreEqual("keep target", skills.Single(skill => skill.Element("guid")!.Value == SkillId).Element("notes")!.Value);
        Assert.AreEqual("0", skills.Single(skill => skill.Element("guid")!.Value == OtherSkillId).Element("karma")!.Value);
        Assert.AreEqual("keep other", skills.Single(skill => skill.Element("guid")!.Value == OtherSkillId).Element("notes")!.Value);
        Assert.AreEqual("17", root.Element("karma")!.Value);
        Assert.AreEqual("preserve me", root.Element("customstate")!.Value);

        XElement[] expenses = root.Element("expenses")!.Elements("expense").ToArray();
        Assert.HasCount(2, expenses);
        XElement added = expenses[0];
        Assert.AreEqual(ExpenseId, added.Element("guid")!.Value);
        Assert.AreEqual("2081-05-12T14:30:00", added.Element("date")!.Value);
        Assert.AreEqual("-3", added.Element("amount")!.Value);
        Assert.AreEqual("Active Skill Sneaking 3 -> 4", added.Element("reason")!.Value);
        Assert.AreEqual("Karma", added.Element("type")!.Value);
        Assert.AreEqual("False", added.Element("refund")!.Value);
        Assert.AreEqual("False", added.Element("forcecareervisible")!.Value);
        XElement undo = added.Element("undo")!;
        Assert.AreEqual("ImproveSkill", undo.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", undo.Element("nuyentype")!.Value);
        Assert.AreEqual(SkillId, undo.Element("objectid")!.Value);
        Assert.AreEqual("0", undo.Element("qty")!.Value);
        Assert.AreEqual(string.Empty, undo.Element("extra")!.Value);

        CareerActiveSkillAdvanceEditorState reopenedOnce = Project(result);
        CareerActiveSkillAdvanceEditorState reopenedAfterSecondRestart = Project(result);
        Assert.AreEqual(reopenedOnce.WorkspaceId, reopenedAfterSecondRestart.WorkspaceId);
        Assert.AreEqual(reopenedOnce.ContentRevision, reopenedAfterSecondRestart.ContentRevision);
        Assert.AreEqual(reopenedOnce.OmittedSkillCount, reopenedAfterSecondRestart.OmittedSkillCount);
        CollectionAssert.AreEqual(
            reopenedOnce.Skills.ToArray(),
            reopenedAfterSecondRestart.Skills.ToArray());
        CharacterCareerActiveSkillAdvanceQuote reopened = reopenedOnce.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(SkillId));
        Assert.AreEqual(2, reopened.KarmaPoints);
        Assert.AreEqual(4, reopened.TotalBaseRating);
        Assert.AreEqual(17, reopened.AvailableKarma);
    }

    [TestMethod]
    public void Confirmation_stale_quote_rule_change_and_duplicate_expense_fail_closed()
    {
        CareerActiveSkillAdvanceEditorState editor = Project(Xml);
        CharacterCareerActiveSkillAdvanceQuote selected = editor.Skills.Single(
            candidate => candidate.Identity.SkillId == Guid.Parse(SkillId));
        CareerActiveSkillAdvanceRequest request = new(
            WorkspaceId,
            editor.ContentRevision,
            selected,
            selected.RuleDigest,
            Confirmed: false,
            Guid.Parse(ExpenseId),
            new DateTime(2081, 5, 12));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerActiveSkillAdvance(Xml, request, null, Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerActiveSkillAdvance(
                Xml.Replace("<val>50</val>", "<val>75</val>", StringComparison.Ordinal),
                request with { Confirmed = true },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerActiveSkillAdvance(
                Xml.Replace(
                    "44444444-4444-4444-4444-444444444444",
                    ExpenseId,
                    StringComparison.Ordinal),
                request with { Confirmed = true },
                null,
                Resolver));
    }

    [TestMethod]
    public void Duplicate_identity_missing_source_and_rating_improvement_fail_closed_or_omit()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(OtherSkillId, SkillId, StringComparison.Ordinal)));

        CareerActiveSkillAdvanceEditorState unresolved = Project(
            Xml.Replace(OtherSourceId, "cccccccc-cccc-cccc-cccc-cccccccccccc", StringComparison.Ordinal));
        Assert.AreEqual(1, unresolved.OmittedSkillCount);
        Assert.HasCount(1, unresolved.Skills);

        string ratingImprovement = "<improvement><improvedname>Sneaking</improvedname><val>1</val><condition>career</condition><improvementttype>Skill</improvementttype><addtorating>True</addtorating><enabled>True</enabled></improvement>";
        CareerActiveSkillAdvanceEditorState unsupported = Project(
            Xml.Replace("</improvements>", ratingImprovement + "</improvements>", StringComparison.Ordinal));
        Assert.AreEqual(1, unsupported.OmittedSkillCount);
        Assert.IsFalse(unsupported.Skills.Any(candidate => candidate.Name == "Sneaking"));
    }

    private static CareerActiveSkillAdvanceEditorState Project(string xml)
        => CareerActiveSkillAdvanceEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            settingsCatalogJson: null,
            Resolver);

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
                SkillSourceId => new CharacterActiveSkillSource(
                    SkillSourceId,
                    "Sneaking",
                    "Physical Active",
                    string.Empty,
                    "AGI",
                    false,
                    false,
                    false,
                    false,
                    $"<skill><id>{SkillSourceId}</id><name>Sneaking</name></skill>"),
                OtherSourceId => new CharacterActiveSkillSource(
                    OtherSourceId,
                    "Pilot Ground Craft",
                    "Vehicle Active",
                    string.Empty,
                    "REA",
                    false,
                    false,
                    false,
                    false,
                    $"<skill><id>{OtherSourceId}</id><name>Pilot Ground Craft</name></skill>"),
                _ => CharacterActiveSkillSource.Unavailable
            };
            return source != CharacterActiveSkillSource.Unavailable;
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
