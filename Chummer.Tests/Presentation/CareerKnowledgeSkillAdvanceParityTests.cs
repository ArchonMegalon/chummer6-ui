using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerKnowledgeSkillAdvanceParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-knowledge-tests");
    private static readonly TestResolver Resolver = new();
    private static readonly Guid SourceBackedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SourceId = Guid.Parse("9f348c99-27e8-47ac-a098-a8a6a54c446a");
    private static readonly Guid CustomId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid NativeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ExpenseId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string Xml = """
        <character>
          <created>True</created>
          <settings>223a11ff-80e0-428b-89a9-6ef1c243b8b6</settings>
          <karma>20</karma>
          <newskills>
            <skills />
            <knoskills>
              <skill><guid>11111111-1111-1111-1111-111111111111</guid><suid>9f348c99-27e8-47ac-a098-a8a6a54c446a</suid><isknowledge>True</isknowledge><skillcategory>Professional</skillcategory><karma>1</karma><base>2</base><name>Administration</name><type>Professional</type><isnativelanguage>False</isnativelanguage><notes>keep source</notes></skill>
              <skill><guid>22222222-2222-2222-2222-222222222222</guid><suid>00000000-0000-0000-0000-000000000000</suid><isknowledge>True</isknowledge><skillcategory>Interests</skillcategory><karma>0</karma><base>1</base><name>Urban Brawl Trivia</name><type>Interests</type><isnativelanguage>False</isnativelanguage><notes>keep custom</notes></skill>
              <skill><guid>33333333-3333-3333-3333-333333333333</guid><suid>00000000-0000-0000-0000-000000000000</suid><isknowledge>True</isknowledge><skillcategory>Language</skillcategory><karma>0</karma><base>0</base><name>English</name><type>Language</type><isnativelanguage>True</isnativelanguage><notes>keep native</notes></skill>
            </knoskills>
            <groups />
          </newskills>
          <improvements>
            <improvement><improvedname>Administration</improvedname><min>4</min><max>4</max><val>50</val><condition>career</condition><improvementttype>KnowledgeSkillKarmaCostMultiplier</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
            <improvement><improvedname>Professional</improvedname><min>4</min><max>4</max><val>0.2</val><condition>career</condition><improvementttype>SkillCategoryKarmaCost</improvementttype><addtorating>False</addtorating><enabled>True</enabled></improvement>
          </improvements>
          <expenses>
            <expense><guid>55555555-5555-5555-5555-555555555555</guid><date>2081-05-01T08:00:00</date><amount>-1</amount><reason>Older</reason><type>Karma</type><refund>False</refund><forcecareervisible>False</forcecareervisible></expense>
          </expenses>
          <customstate>preserve me</customstate>
        </character>
        """;

    [TestMethod]
    public void Projection_preserves_nullable_source_language_gate_and_knowledge_cost_authority()
    {
        CareerKnowledgeSkillAdvanceEditorState editor = Project(Xml);
        Assert.AreEqual(0, editor.OmittedSkillCount);
        Assert.HasCount(3, editor.Skills);

        CharacterCareerKnowledgeSkillAdvanceQuote source = Find(editor, SourceBackedId);
        Assert.AreEqual(SourceId, source.Identity.SourceSkillId);
        Assert.AreEqual(3, source.KarmaCost,
            "Knowledge rating 4 costs 4, then the exact knowledge/category modifiers round 2.2 away from zero to 3.");
        Assert.IsTrue(source.CanAdvance);
        Assert.AreEqual(64, source.CharacterRevision.Length);
        Assert.AreEqual(64, source.SourceRevision.Length);
        Assert.AreEqual(64, source.RuleDigest.Length);

        CharacterCareerKnowledgeSkillAdvanceQuote custom = Find(editor, CustomId);
        Assert.IsNull(custom.Identity.SourceSkillId);
        Assert.AreEqual("Urban Brawl Trivia", custom.Name);

        CharacterCareerKnowledgeSkillAdvanceQuote native = Find(editor, NativeId);
        Assert.AreEqual("Language", native.SkillType);
        Assert.AreEqual(CharacterCareerKnowledgeSkillAdvanceBlocker.NativeLanguage, native.Blocker);
        Assert.IsFalse(native.CanAdvance);
    }

    [TestMethod]
    public void Apply_is_atomic_receipted_recoverable_and_idempotent()
    {
        CareerKnowledgeSkillAdvanceEditorState editor = Project(Xml);
        CharacterCareerKnowledgeSkillAdvanceQuote selected = Find(editor, SourceBackedId);
        CareerKnowledgeSkillAdvanceRequest request = Request(editor, selected, ExpenseId);

        CareerKnowledgeSkillAdvanceMutationResult applied =
            CareerKnowledgeSkillAdvanceMutation.Apply(Xml, request, null, Resolver);
        Assert.IsTrue(CharacterCareerKnowledgeSkillAdvanceRules.IsCoherent(applied.Receipt));
        Assert.AreEqual(ExpenseId, applied.Receipt.TransactionId);
        XElement root = XDocument.Parse(applied.Xml).Root!;
        XElement target = root.Element("newskills")!.Element("knoskills")!.Elements("skill")
            .Single(skill => skill.Element("guid")!.Value == SourceBackedId.ToString("D"));
        Assert.AreEqual("2", target.Element("karma")!.Value);
        Assert.AreEqual("keep source", target.Element("notes")!.Value);
        Assert.AreEqual("17", root.Element("karma")!.Value);
        Assert.AreEqual("preserve me", root.Element("customstate")!.Value);
        Assert.HasCount(1, root.Element(CareerKnowledgeSkillAdvanceMutation.ReceiptContainerName)!
            .Elements("receipt").ToArray());

        CareerKnowledgeSkillAdvanceEditorState reopened = Project(applied.Xml);
        Assert.HasCount(1, reopened.RecoverableReceipts);
        Assert.AreEqual(applied.Receipt, reopened.RecoverableReceipts[0]);
        Assert.AreEqual(2, Find(reopened, SourceBackedId).KarmaPoints);

        CareerKnowledgeSkillAdvanceMutationResult replay =
            CareerKnowledgeSkillAdvanceMutation.Apply(applied.Xml, request, null, Resolver);
        Assert.AreEqual(applied.Xml, replay.Xml);
        Assert.AreEqual(applied.Receipt, replay.Receipt);
    }

    [TestMethod]
    public void Confirmation_and_every_cas_dimension_fail_closed()
    {
        CareerKnowledgeSkillAdvanceEditorState editor = Project(Xml);
        CharacterCareerKnowledgeSkillAdvanceQuote selected = Find(editor, SourceBackedId);
        CareerKnowledgeSkillAdvanceRequest request = Request(editor, selected, ExpenseId);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerKnowledgeSkillAdvanceMutation.Apply(
                Xml,
                request with { Confirmed = false },
                null,
                Resolver));
        foreach (CareerKnowledgeSkillAdvanceRequest stale in new[]
                 {
                     request with { ExpectedCharacterRevision = new string('0', 64) },
                     request with { ExpectedLogicalRevision = new string('0', 64) },
                     request with { ExpectedSourceRevision = new string('0', 64) },
                     request with { ExpectedRuleDigest = new string('0', 64) }
                 })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                CareerKnowledgeSkillAdvanceMutation.Apply(Xml, stale, null, Resolver));
        }
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerKnowledgeSkillAdvanceMutation.Apply(
                Xml.Replace("<karma>20</karma>", "<karma>19</karma>", StringComparison.Ordinal),
                request,
                null,
                Resolver));
    }

    [TestMethod]
    public void Ambiguous_identity_missing_source_and_rating_authority_fail_closed_or_omit()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(CustomId.ToString("D"), SourceBackedId.ToString("D"), StringComparison.Ordinal)));

        CareerKnowledgeSkillAdvanceEditorState unresolved = Project(
            Xml.Replace(SourceId.ToString("D"), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", StringComparison.Ordinal));
        Assert.AreEqual(1, unresolved.OmittedSkillCount);
        Assert.HasCount(2, unresolved.Skills);

        string ratingAuthority = "<improvement><improvedname>Administration</improvedname><val>1</val><condition>career</condition><improvementttype>KnowledgeSkillLevel</improvementttype><addtorating>True</addtorating><enabled>True</enabled></improvement>";
        CareerKnowledgeSkillAdvanceEditorState omitted = Project(
            Xml.Replace("</improvements>", ratingAuthority + "</improvements>", StringComparison.Ordinal));
        Assert.IsFalse(omitted.Skills.Any(skill => skill.Identity.SkillId == SourceBackedId));
    }

    private static CareerKnowledgeSkillAdvanceEditorState Project(string xml)
        => CareerKnowledgeSkillAdvanceEditorProjector.Project(
            xml,
            WorkspaceId,
            7,
            settingsCatalogJson: null,
            Resolver);

    private static CharacterCareerKnowledgeSkillAdvanceQuote Find(
        CareerKnowledgeSkillAdvanceEditorState editor,
        Guid id)
        => editor.Skills.Single(candidate => candidate.Identity.SkillId == id);

    private static CareerKnowledgeSkillAdvanceRequest Request(
        CareerKnowledgeSkillAdvanceEditorState editor,
        CharacterCareerKnowledgeSkillAdvanceQuote quote,
        Guid expenseId)
        => new(
            editor.WorkspaceId,
            editor.ContentRevision,
            quote,
            quote.CharacterRevision,
            quote.LogicalRevision,
            quote.SourceRevision,
            quote.RuleDigest,
            Confirmed: true,
            expenseId,
            new DateTime(2081, 5, 12, 14, 30, 0));

    private sealed class TestResolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new TestContext();
    }

    private sealed class TestContext : ICharacterSourceDataContext
    {
        public bool TryResolveKnowledgeSkillSource(
            string sourceSkillId,
            out CharacterKnowledgeSkillSource source)
        {
            source = string.Equals(sourceSkillId, SourceId.ToString("D"), StringComparison.Ordinal)
                ? new CharacterKnowledgeSkillSource(
                    SourceId.ToString("D"),
                    "Administration",
                    "Professional",
                    "LOG",
                    $"<skill><id>{SourceId:D}</id><name>Administration</name><category>Professional</category><attribute>LOG</attribute></skill>")
                : CharacterKnowledgeSkillSource.Unavailable;
            return source != CharacterKnowledgeSkillSource.Unavailable;
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
