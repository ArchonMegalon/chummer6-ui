using System.Xml.Linq;
using Chummer.Application.Characters;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerFreeSkillRatingTests
{
    private const string SkillId = "11111111-1111-1111-1111-111111111111";
    private const string SecondId = "22222222-2222-2222-2222-222222222222";
    private const string FirstSource = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string SecondSource = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    private const string ProfileId = "66666666-6666-6666-6666-666666666666";
    private static readonly CharacterWorkspaceId Workspace = new("free-skill-career");
    private static readonly Resolver Sources = new();

    [TestMethod]
    [DataRow("SkillGroupBase")]
    [DataRow("SkillGroupLevel")]
    public void Specialization_purchase_preserves_free_grants_and_paid_allocations_after_reopen(string groupGrant)
    {
        string xml = Fixture().Replace("SkillGroupLevel", groupGrant, StringComparison.Ordinal);
        var candidate = Specializations(xml).Skills.Single(s => s.Identity.SkillId == Guid.Parse(SkillId));
        Assert.AreEqual(3, candidate.TotalBaseRating);
        var selection = new CharacterCareerSkillSpecializationSelection("Urban",
            CharacterCareerSkillSpecializationOptionKind.SourceCatalog, new string('a', 64));
        var quote = CareerSkillSpecializationEditorProjector.ProjectQuote(xml, Workspace, 1,
            candidate.Identity, selection, null, Sources);
        Assert.IsTrue(quote.CanAdd);
        Assert.AreEqual(7, quote.KarmaCost);
        var request = new CareerSkillSpecializationRequest(Workspace, 1, quote, quote.CharacterRevision,
            quote.SourceRevision, quote.RuleDigest, quote.LogicalRevision, true,
            Guid.NewGuid(), Guid.NewGuid(), new DateTime(2081, 1, 1));
        string changed = CareerSkillSpecializationMutation.Apply(xml, request, null, Sources);
        AssertPreserved(xml, changed, "groups");
        var reopened = Specializations(changed).Skills.Single(s => s.Identity == candidate.Identity);
        Assert.AreEqual(3, reopened.TotalBaseRating);
        Assert.AreEqual(1, reopened.ExistingSpecializationCount);
        XElement saved = XElement.Parse(changed);
        Assert.AreEqual("93", saved.Element("karma")!.Value);
        Assert.IsTrue(saved.Descendants("skill").All(s => s.Element("base")!.Value == "0"
            && s.Element("karma")!.Value == "0"));
        Assert.AreEqual(1, saved.Descendants("spec").Count());
        Assert.AreEqual(1, saved.Element("expenses")!.Elements().Count());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerSkillSpecializationMutation.Apply(changed, request, null, Sources));
        XElement stale = XElement.Parse(xml);
        stale.Element("improvements")!.Elements().Last().SetElementValue("val", "3");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerSkillSpecializationMutation.Apply(stale.ToString(), request, null, Sources));
    }

    [TestMethod]
    public void Active_purchase_retains_module_grants_and_reopens_exact_paid_allocation()
    {
        string xml = Fixture();
        var quote = Active(xml).Skills.Single(s => s.Identity.SkillId == Guid.Parse(SkillId));
        Assert.AreEqual(3, quote.TotalBaseRating);
        Assert.AreEqual(0, quote.KarmaPoints);
        Assert.AreEqual(8, quote.KarmaCost);
        CareerActiveSkillAdvanceRequest request = new(Workspace, 1, quote, quote.RuleDigest,
            true, Guid.NewGuid(), new DateTime(2081, 1, 1));
        string changed = CareerActiveSkillAdvanceMutation.Apply(xml, request, null, Sources);
        var reopened = Active(changed).Skills.Single(s => s.Identity == quote.Identity);
        Assert.AreEqual(4, reopened.TotalBaseRating);
        Assert.AreEqual(1, reopened.KarmaPoints);
        Assert.AreEqual(92, reopened.AvailableKarma);
        AssertPreserved(xml, changed, "groups");
        XElement target = XDocument.Parse(changed).Descendants("skill").Single(s => s.Element("guid")!.Value == SkillId);
        Assert.AreEqual("0", target.Element("base")!.Value);
        Assert.AreEqual("1", target.Element("karma")!.Value);
        Assert.IsTrue(XNode.DeepEquals(XDocument.Parse(xml).Descendants("skill").Last(),
            XDocument.Parse(changed).Descendants("skill").Last()));
        Assert.AreEqual(reopened, Active(changed).Skills.Single(s => s.Identity == quote.Identity));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerActiveSkillAdvanceMutation.Apply(changed, request, null, Sources));
    }

    [TestMethod]
    public void Group_purchase_prices_all_member_grants_and_changes_only_paid_group_karma()
    {
        string xml = Fixture();
        var quote = Groups(xml).SkillGroups.Single();
        Assert.AreEqual(2, quote.GroupRating);
        Assert.AreEqual(3, quote.CostRating);
        Assert.AreEqual(20, quote.KarmaCost);
        Assert.AreEqual(0, quote.KarmaPoints);
        CareerSkillGroupAdvanceRequest request = new(Workspace, 1, quote, quote.RuleDigest,
            true, Guid.NewGuid(), new DateTime(2081, 1, 1));
        string changed = CareerSkillGroupAdvanceMutation.Apply(xml, request, null, Sources);
        var reopened = Groups(changed).SkillGroups.Single();
        Assert.AreEqual(3, reopened.GroupRating);
        Assert.AreEqual(4, reopened.CostRating);
        Assert.AreEqual(1, reopened.KarmaPoints);
        Assert.AreEqual(80, reopened.AvailableKarma);
        Assert.AreEqual(reopened.LogicalRevision, Groups(changed).SkillGroups.Single().LogicalRevision);
        AssertPreserved(xml, changed, "skills");
        Assert.IsTrue(Active(changed).Skills.All(s => s.TotalBaseRating == 4 && s.KarmaPoints == 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerSkillGroupAdvanceMutation.Apply(changed, request, null, Sources));
    }

    [TestMethod]
    public void Changed_free_grant_rejects_an_already_reviewed_purchase()
    {
        string xml = Fixture();
        var quote = Active(xml).Skills.First();
        CareerActiveSkillAdvanceRequest request = new(Workspace, 1, quote, quote.RuleDigest,
            true, Guid.NewGuid(), new DateTime(2081, 1, 1));
        XElement changed = XElement.Parse(xml);
        changed.Element("improvements")!.Elements().Last().SetElementValue("val", "3");
        string changedXml = changed.ToString(SaveOptions.DisableFormatting);
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerActiveSkillAdvanceMutation.Apply(changedXml, request, null, Sources));
        Assert.AreEqual("100", changed.Element("karma")!.Value);
    }

    [TestMethod]
    [DataRow("unique", "priority")]
    [DataRow("val", "0.5")]
    public void Unprojectable_grants_are_not_silently_ignored(string field, string value)
    {
        XElement xml = XElement.Parse(Fixture());
        xml.Element("improvements")!.Elements().Last().SetElementValue(field, value);
        string text = xml.ToString(SaveOptions.DisableFormatting);
        Assert.IsEmpty(Active(text).Skills);
        Assert.IsEmpty(Groups(text).SkillGroups);
        Assert.IsEmpty(Specializations(text).Skills);
    }

    [TestMethod]
    public void Malformed_grant_enable_flag_rejects_the_source_instead_of_offering_a_purchase()
    {
        XElement xml = XElement.Parse(Fixture());
        xml.Element("improvements")!.Elements().Last().SetElementValue("enabled", "maybe");
        string text = xml.ToString(SaveOptions.DisableFormatting);
        Assert.ThrowsExactly<InvalidOperationException>(() => Active(text));
        Assert.IsEmpty(Groups(text).SkillGroups);
    }

    [TestMethod]
    public void Rating_category_effect_is_not_mistaken_for_a_pool_only_bonus()
    {
        XElement xml = XElement.Parse(Fixture());
        xml.Element("improvements")!.Add(new XElement("improvement",
            new XElement("improvementttype", "SkillCategory"), new XElement("improvedname", "Physical Active"),
            new XElement("enabled", "1"), new XElement("addtorating", "1"), new XElement("val", "1")));
        Assert.IsEmpty(Active(xml.ToString()).Skills);
        Assert.IsEmpty(Groups(xml.ToString()).SkillGroups);
        Assert.IsEmpty(Specializations(xml.ToString()).Skills);
    }

    [TestMethod]
    public void Suppressed_group_karma_cannot_be_reactivated_as_a_one_point_purchase()
    {
        XElement xml = XElement.Parse(Fixture());
        xml.Descendants("skill").First().SetElementValue("base", "3");
        xml.Descendants("group").Single().SetElementValue("karma", "4");
        Assert.IsEmpty(Active(xml.ToString()).Skills);
        Assert.IsEmpty(Groups(xml.ToString()).SkillGroups);
    }

    private static void AssertPreserved(string before, string after, string container)
    {
        XElement first = XElement.Parse(before);
        XElement second = XElement.Parse(after);
        Assert.IsTrue(XNode.DeepEquals(first.Element("improvements"), second.Element("improvements")));
        Assert.IsTrue(XNode.DeepEquals(first.Element("newskills")!.Element(container), second.Element("newskills")!.Element(container)));
        Assert.AreEqual(first.Element("customstate")!.Value, second.Element("customstate")!.Value);
    }

    private static CareerActiveSkillAdvanceEditorState Active(string xml)
        => CareerActiveSkillAdvanceEditorProjector.Project(xml, Workspace, 1, null, Sources);
    private static CareerSkillGroupAdvanceEditorState Groups(string xml)
        => CareerSkillGroupAdvanceEditorProjector.Project(xml, Workspace, 1, null, Sources);
    private static CareerSkillSpecializationEditorState Specializations(string xml)
        => CareerSkillSpecializationEditorProjector.Project(xml, Workspace, 1, null, Sources);

    private static string Fixture() => $"""
        <character><created>1</created><settings>{ProfileId}</settings><karma>100</karma>
        <newskills><skills>
        <skill><guid>{SkillId}</guid><suid>{FirstSource}</suid><isknowledge>0</isknowledge><skillcategory>Physical Active</skillcategory><base>0</base><karma>0</karma><notes>retain first</notes></skill>
        <skill><guid>{SecondId}</guid><suid>{SecondSource}</suid><isknowledge>0</isknowledge><skillcategory>Physical Active</skillcategory><base>0</base><karma>0</karma><notes>retain second</notes></skill>
        </skills><knoskills/><groups><group><id>33333333-3333-3333-3333-333333333333</id><name>Stealth</name><base>0</base><karma>0</karma><isbroken>0</isbroken></group></groups></newskills>
        <improvements>
        <improvement><improvementttype>SkillLevel</improvementttype><improvedname>Sneaking</improvedname><enabled>1</enabled><val>1</val><condition>career</condition></improvement>
        <improvement><improvementttype>SkillLevel</improvementttype><improvedname>Palming</improvedname><enabled>1</enabled><val>1</val></improvement>
        <improvement><improvementttype>SkillGroupLevel</improvementttype><improvedname>Stealth</improvedname><enabled>1</enabled><val>2</val></improvement>
        </improvements><expenses/><customstate>retain history</customstate></character>
        """;

    private sealed class Resolver : ICharacterSourceDataResolver
    {
        public ICharacterSourceDataContext? TryCreateContext(string characterXml) => new Context();
    }
    private sealed class Context : ICharacterSourceDataContext
    {
        public bool TryResolveCareerSkillSpecializationSettings(
            out CharacterCareerSkillSpecializationSettings settings, out string rawRuleState)
        {
            settings = new(7, 3, true);
            rawRuleState = "<specializations><active>7</active><knowledge>3</knowledge><break>1</break></specializations>";
            return true;
        }
        public bool TryResolveCareerSkillSpecializationSource(string sourceSkillId, CharacterCareerSkillKind kind,
            out CharacterCareerSkillSpecializationSource source)
        {
            source = CharacterCareerSkillSpecializationSource.Unavailable;
            if (kind != CharacterCareerSkillKind.Active || !TryResolveActiveSkillSource(sourceSkillId, out var active))
                return false;
            source = new(sourceSkillId, kind, active.Name, active.SkillCategory,
                [new(new string('a', 64), "Urban", CharacterCareerSkillSpecializationOptionKind.SourceCatalog,
                    "skills.xml:" + sourceSkillId)], active.RawSourceXml);
            return true;
        }
        public bool TryResolveCareerSettingsProfile(out string xml, out string authority)
        {
            xml = $"<settings><id>{ProfileId}</id><maxskillrating>12</maxskillrating><karmacost><karmanewactiveskill>2</karmanewactiveskill><karmaimproveactiveskill>2</karmaimproveactiveskill><karmanewskillgroup>5</karmanewskillgroup><karmaimproveskillgroup>5</karmaimproveskillgroup></karmacost></settings>";
            authority = "test-profile-authority";
            return true;
        }
        public bool TryResolveActiveSkillSource(string sourceSkillId, out CharacterActiveSkillSource source)
        {
            source = CharacterActiveSkillSource.Unavailable;
            string name = sourceSkillId switch { FirstSource => "Sneaking", SecondSource => "Palming", _ => "" };
            if (name.Length == 0) return false;
            source = new(sourceSkillId, name, "Physical Active", "Stealth", "AGI", false, false, false, false,
                $"<skill><id>{sourceSkillId}</id><name>{name}</name></skill>");
            return true;
        }
        public bool TryResolveCyberwareGradeDeviceRating(string gradeName, string improvementSource, out int deviceRating)
        { deviceRating = 0; return false; }
        public bool TryResolveVehicleModBonuses(string sourceId, string name, out CharacterVehicleModSourceBonuses bonuses)
        { bonuses = CharacterVehicleModSourceBonuses.Empty; return false; }
    }
}
