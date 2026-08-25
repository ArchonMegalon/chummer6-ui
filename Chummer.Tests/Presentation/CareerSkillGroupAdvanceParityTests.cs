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
    private static readonly Guid ExpenseId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CorrectionId =
        Guid.Parse("66666666-6666-6666-6666-666666666666");
    private const string GroupId = "11111111-1111-1111-1111-111111111111";
    private const string SkillSourceId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string OtherSourceId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
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
    public void Projection_binds_exact_identity_members_sr5_prerequisites_and_cost_authority()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.AreEqual(CharacterCareerSkillGroupAdvanceRules.RulesetId, editor.RulesetId);
        Assert.AreEqual(0, editor.OmittedSkillGroupCount);
        Assert.AreEqual(0, editor.OmittedReceiptCount);
        Assert.HasCount(0, editor.RecoverableReceipts);
        Assert.HasCount(1, editor.SkillGroups);

        CharacterCareerSkillGroupAdvanceQuote group = editor.SkillGroups.Single();
        Assert.AreEqual(Guid.Parse(GroupId), group.Identity.InternalId);
        Assert.AreEqual("Stealth", group.Name);
        Assert.AreEqual(3, group.GroupRating);
        Assert.AreEqual(3, group.CostRating);
        Assert.AreEqual(4, group.TargetGroupRating);
        Assert.AreEqual(4, group.TargetCostRating);
        Assert.AreEqual(2, group.EnabledMemberCount);
        Assert.AreEqual(20, group.KarmaCost);
        Assert.AreEqual(TimeSpan.Zero, group.ApplicationDuration);
        Assert.AreEqual(
            CharacterCareerSkillGroupTimeAuthority.ImmediateChummerPersistence,
            group.TimeAuthority);
        Assert.IsTrue(group.Prerequisites.All(static prerequisite => prerequisite.Satisfied));
        Assert.IsTrue(group.CanAdvance);
        Assert.AreEqual(CharacterCareerSkillGroupAdvanceBlocker.None, group.Blocker);
        Assert.AreEqual(64, group.LogicalRevision.Length);
        Assert.AreEqual(64, group.SourceRevision.Length);
        Assert.AreEqual(64, group.RuleDigest.Length);
    }

    [TestMethod]
    public void Apply_returns_exact_receipt_and_restart_recovers_it_without_replay()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CharacterCareerSkillGroupAdvanceQuote selected = editor.SkillGroups.Single();
        CareerSkillGroupAdvanceMutationResult result =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                Request(editor, selected),
                settingsCatalogJson: null,
                Resolver);

        XElement root = XDocument.Parse(result.Xml).Root!;
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

        XElement added = root.Element("expenses")!.Elements("expense")
            .Single(expense => expense.Element("guid")!.Value == ExpenseId.ToString("D"));
        Assert.AreEqual("-20", added.Element("amount")!.Value);
        Assert.AreEqual("Skill Group Stealth 3 -> 4", added.Element("reason")!.Value);
        Assert.AreEqual("True", added.Element("forcecareervisible")!.Value);
        XElement undo = added.Element("undo")!;
        Assert.AreEqual("ImproveSkillGroup", undo.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", undo.Element("nuyentype")!.Value);
        Assert.AreEqual(GroupId, undo.Element("objectid")!.Value);
        Assert.AreEqual("0", undo.Element("qty")!.Value);
        Assert.AreEqual(string.Empty, undo.Element("extra")!.Value);

        Assert.IsTrue(CharacterCareerSkillGroupAdvanceRules.IsCoherent(result.Receipt));
        Assert.AreEqual(ExpenseId, result.Receipt.TransactionId);
        Assert.AreEqual(3, result.Receipt.GroupRatingBefore);
        Assert.AreEqual(4, result.Receipt.GroupRatingAfter);
        Assert.AreEqual(3, result.Receipt.CostRatingBefore);
        Assert.AreEqual(4, result.Receipt.CostRatingAfter);

        CareerSkillGroupAdvanceEditorState reopened = Project(result.Xml);
        CareerSkillGroupAdvanceEditorState restarted = Project(result.Xml);
        Assert.AreEqual(0, reopened.OmittedReceiptCount);
        Assert.HasCount(1, reopened.RecoverableReceipts);
        Assert.AreEqual(result.Receipt, reopened.RecoverableReceipts.Single());
        CollectionAssert.AreEqual(
            reopened.RecoverableReceipts.ToArray(),
            restarted.RecoverableReceipts.ToArray());
        Assert.AreEqual(4, reopened.SkillGroups.Single().GroupRating);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                result.Xml,
                Request(reopened, reopened.SkillGroups.Single(), ExpenseId),
                null,
                Resolver));
    }

    [TestMethod]
    public void Correction_is_compensating_exact_and_cannot_replay()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CareerSkillGroupAdvanceMutationResult applied =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                Request(editor, editor.SkillGroups.Single()),
                null,
                Resolver);
        CareerSkillGroupAdvanceEditorState reopened = Project(applied.Xml);
        CareerSkillGroupCorrectionRequest correctionRequest = new(
            WorkspaceId,
            reopened.ContentRevision,
            reopened.RulesetId,
            reopened.RecoverableReceipts.Single(),
            reopened.RecoverableReceipts.Single().ReceiptDigest,
            Confirmed: true,
            CorrectionId,
            "Undo mistaken advancement");

        CareerSkillGroupCorrectionMutationResult corrected =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupCorrection(
                applied.Xml,
                correctionRequest,
                null,
                Resolver);
        XElement root = XDocument.Parse(corrected.Xml).Root!;
        XElement group = root.Element("newskills")!.Element("groups")!.Element("group")!;
        Assert.AreEqual("1", group.Element("karma")!.Value);
        Assert.AreEqual("40", root.Element("karma")!.Value);
        Assert.IsFalse(root.Element("expenses")!.Elements("expense")
            .Any(expense => expense.Element("guid")!.Value == ExpenseId.ToString("D")));
        Assert.IsTrue(CharacterCareerSkillGroupAdvanceRules.IsCoherent(
            corrected.Correction));

        CareerSkillGroupAdvanceEditorState after = Project(corrected.Xml);
        Assert.HasCount(0, after.RecoverableReceipts);
        Assert.AreEqual(0, after.OmittedReceiptCount);
        Assert.AreEqual(3, after.SkillGroups.Single().GroupRating);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupCorrection(
                corrected.Xml,
                correctionRequest,
                null,
                Resolver));
    }

    [TestMethod]
    public void Restart_rejects_a_correction_marker_whose_restored_state_drifted()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CareerSkillGroupAdvanceMutationResult applied =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                Request(editor, editor.SkillGroups.Single()),
                null,
                Resolver);
        CareerSkillGroupAdvanceEditorState reopened = Project(applied.Xml);
        CharacterCareerSkillGroupAdvanceReceipt receipt =
            reopened.RecoverableReceipts.Single();
        CareerSkillGroupCorrectionMutationResult corrected =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupCorrection(
                applied.Xml,
                new CareerSkillGroupCorrectionRequest(
                    WorkspaceId,
                    reopened.ContentRevision,
                    reopened.RulesetId,
                    receipt,
                    receipt.ReceiptDigest,
                    Confirmed: true,
                    CorrectionId,
                    "Undo mistaken advancement"),
                null,
                Resolver);

        string drifted = corrected.Xml.Replace(
            "<karma>1</karma><base>2</base><isbroken>False</isbroken>",
            "<karma>2</karma><base>2</base><isbroken>False</isbroken>",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(drifted));
    }

    [TestMethod]
    public void Confirmation_and_all_three_review_digests_fail_closed()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CharacterCareerSkillGroupAdvanceQuote selected = editor.SkillGroups.Single();
        CareerSkillGroupAdvanceRequest request = Request(editor, selected);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                request with { Confirmed = false },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                request with { ExpectedLogicalRevision = new string('0', 64) },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                request with { ExpectedSourceRevision = new string('0', 64) },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                request with { ExpectedRuleDigest = new string('0', 64) },
                null,
                Resolver));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml.Replace("<karma>40</karma>", "<karma>39</karma>", StringComparison.Ordinal),
                request,
                null,
                Resolver));
    }

    [TestMethod]
    public void Career_and_broken_prerequisites_surface_as_typed_blockers()
    {
        CareerSkillGroupAdvanceEditorState otherRuleset = Project(
            Xml,
            "sr6");
        Assert.AreEqual(
            CharacterCareerSkillGroupAdvanceBlocker.UnsupportedRuleset,
            otherRuleset.SkillGroups.Single().Blocker);

        CareerSkillGroupAdvanceEditorState creation = Project(
            Xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal));
        CharacterCareerSkillGroupAdvanceQuote creationQuote = creation.SkillGroups.Single();
        Assert.AreEqual(
            CharacterCareerSkillGroupAdvanceBlocker.NotCareerCharacter,
            creationQuote.Blocker);
        Assert.IsFalse(creationQuote.Prerequisites.Single(value =>
            value.Prerequisite == CharacterCareerSkillGroupPrerequisite.CareerCharacter).Satisfied);

        CareerSkillGroupAdvanceEditorState broken = Project(
            Xml.Replace("<isbroken>False</isbroken>", "<isbroken>True</isbroken>", StringComparison.Ordinal));
        Assert.AreEqual(
            CharacterCareerSkillGroupAdvanceBlocker.Broken,
            broken.SkillGroups.Single().Blocker);
        Assert.IsFalse(broken.SkillGroups.Single().CanAdvance);
    }

    [TestMethod]
    public void Exact_modifier_projection_changes_cost_and_is_digest_bound()
    {
        string modifier = """
            <improvement><improvedname>Stealth</improvedname><val>-3</val><min>0</min><max>0</max><condition>career</condition><improvementttype>SkillGroupKarmaCost</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
            """;
        CareerSkillGroupAdvanceEditorState editor = Project(
            Xml.Replace("<improvements />", "<improvements>" + modifier + "</improvements>", StringComparison.Ordinal));
        Assert.AreEqual(17, editor.SkillGroups.Single().KarmaCost);
        Assert.AreNotEqual(
            Project(Xml).SkillGroups.Single().RuleDigest,
            editor.SkillGroups.Single().RuleDigest);
    }

    [TestMethod]
    public void Tampered_receipt_or_expense_is_not_recoverable()
    {
        CareerSkillGroupAdvanceEditorState editor = Project(Xml);
        CareerSkillGroupAdvanceMutationResult applied =
            WorkspaceXmlMutationCatalog.ApplyCareerSkillGroupAdvance(
                Xml,
                Request(editor, editor.SkillGroups.Single()),
                null,
                Resolver);

        string tamperedReceipt = applied.Xml.Replace(
            "expenseAmount=\"-20\"",
            "expenseAmount=\"-19\"",
            StringComparison.Ordinal);
        CareerSkillGroupAdvanceEditorState receiptState = Project(tamperedReceipt);
        Assert.HasCount(0, receiptState.RecoverableReceipts);
        Assert.AreEqual(1, receiptState.OmittedReceiptCount);

        string tamperedExpense = applied.Xml.Replace(
            "<forcecareervisible>True</forcecareervisible>",
            "<forcecareervisible>False</forcecareervisible>",
            StringComparison.Ordinal);
        CareerSkillGroupAdvanceEditorState expenseState = Project(tamperedExpense);
        Assert.HasCount(0, expenseState.RecoverableReceipts);
        Assert.AreEqual(1, expenseState.OmittedReceiptCount);
    }

    [TestMethod]
    public void Duplicate_identity_unresolved_member_and_rating_improvement_fail_closed_or_omit()
    {
        string duplicateGroup = "<group><id>" + GroupId
            + "</id><name>Other</name><karma>0</karma><base>0</base><isbroken>False</isbroken></group>";
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

    private static CareerSkillGroupAdvanceRequest Request(
        CareerSkillGroupAdvanceEditorState editor,
        CharacterCareerSkillGroupAdvanceQuote selected,
        Guid? transactionId = null)
        => new(
            WorkspaceId,
            editor.ContentRevision,
            editor.RulesetId,
            selected,
            selected.LogicalRevision,
            selected.SourceRevision,
            selected.RuleDigest,
            Confirmed: true,
            transactionId ?? ExpenseId,
            new DateTime(2081, 5, 12, 14, 30, 0));

    private static CareerSkillGroupAdvanceEditorState Project(string xml)
        => Project(xml, CharacterCareerSkillGroupAdvanceRules.RulesetId);

    private static CareerSkillGroupAdvanceEditorState Project(
        string xml,
        string rulesetId)
        => CareerSkillGroupAdvanceEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            rulesetId,
            settingsCatalogJson: null,
            Resolver);

    private sealed class TestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml)
            => new TestContext();
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
