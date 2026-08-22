using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class MartialArtDeleteParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("martial-art-delete-tests");
    private static readonly Guid FirstArt = Guid.Parse("a1111111-a111-a111-a111-a11111111111");
    private static readonly Guid FirstTechnique = Guid.Parse("a2222222-a222-a222-a222-a22222222222");
    private static readonly Guid SecondTechnique = Guid.Parse("a3333333-a333-a333-a333-a33333333333");
    private static readonly Guid OtherArt = Guid.Parse("b1111111-b111-b111-b111-b11111111111");
    private static readonly Guid OtherTechnique = Guid.Parse("b2222222-b222-b222-b222-b22222222222");
    private static readonly Guid QualityArt = Guid.Parse("c1111111-c111-c111-c111-c11111111111");
    private static readonly Guid QualityTechnique = Guid.Parse("c2222222-c222-c222-c222-c22222222222");

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Creation_and_career_project_identical_zero_refund_targets(bool created)
    {
        MartialArtDeleteEditorState editor = MartialArtDeleteEditorProjector.Project(
            Xml(created), WorkspaceId, 31);
        Assert.AreEqual(7, editor.Targets.Count);
        CharacterMartialArtDeleteState art = editor.Targets.Single(
            target => target.Identity == new CharacterMartialArtDeleteIdentity(FirstArt, null));
        Assert.IsTrue(art.CanDelete);
        Assert.AreEqual(2, art.CascadeTechniqueCount);
        Assert.AreEqual(0, art.Economics.KarmaDelta);
        Assert.AreEqual(0m, art.Economics.NuyenDelta);
        CharacterMartialArtDeleteState quality = editor.Targets.Single(
            target => target.Identity == new CharacterMartialArtDeleteIdentity(QualityArt, null));
        Assert.IsFalse(quality.CanDelete);
        Assert.IsTrue(editor.Targets.Single(
            target => target.Identity == new CharacterMartialArtDeleteIdentity(QualityArt, QualityTechnique)).CanDelete);
    }

    [TestMethod]
    public void Confirmed_art_delete_cascades_exact_techniques_and_source_bound_improvements_only()
    {
        string source = Xml(false);
        CharacterMartialArtDeleteState target = MartialArtDeleteEditorProjector
            .Project(source, WorkspaceId, 31)
            .Targets.Single(state => state.Identity == new CharacterMartialArtDeleteIdentity(FirstArt, null));
        var unconfirmed = new MartialArtDeleteRequest(
            WorkspaceId, 31, target.Identity, target.Revision, Confirmed: false);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyMartialArtDelete(source, unconfirmed));

        string mutated = WorkspaceXmlMutationCatalog.ApplyMartialArtDelete(
            source,
            unconfirmed with { Confirmed = true });
        XDocument after = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement root = after.Root!;
        Assert.IsFalse(root.Element("martialarts")!.Elements("martialart")
            .Any(art => art.Element("guid")!.Value == FirstArt.ToString("D")));
        Assert.IsTrue(root.Element("martialarts")!.Elements("martialart")
            .Any(art => art.Element("guid")!.Value == OtherArt.ToString("D")));

        var remaining = root.Element("improvements")!.Elements("improvement")
            .Select(improvement => (
                Source: improvement.Element("improvementsource")!.Value,
                SourceName: improvement.Element("sourcename")!.Value,
                Marker: improvement.Element("improvedname")!.Value))
            .ToArray();
        Assert.IsFalse(remaining.Any(item =>
            item.Source == "MartialArt" && item.SourceName == FirstArt.ToString("D")));
        Assert.IsFalse(remaining.Any(item =>
            item.Source == "MartialArtTechnique"
            && item.SourceName is var id
            && (id == FirstTechnique.ToString("D") || id == SecondTechnique.ToString("D"))));
        Assert.IsTrue(remaining.Any(item =>
            item.Source == "Quality" && item.SourceName == FirstArt.ToString("D")));
        Assert.IsTrue(remaining.Any(item =>
            item.Source == "MartialArt" && item.SourceName == OtherArt.ToString("D")));
        Assert.AreEqual("runner sentinel", root.Element("customstate")!.Value);
        Assert.AreEqual("19", root.Element("karma")!.Value);
        Assert.AreEqual("1234.56", root.Element("nuyen")!.Value);
    }

    [TestMethod]
    public void Nested_technique_delete_is_parent_scoped_and_preserves_equal_named_sibling()
    {
        string source = Xml(true);
        CharacterMartialArtDeleteState target = MartialArtDeleteEditorProjector
            .Project(source, WorkspaceId, 31)
            .Targets.Single(state => state.Identity ==
                new CharacterMartialArtDeleteIdentity(FirstArt, FirstTechnique));
        string mutated = WorkspaceXmlMutationCatalog.ApplyMartialArtDelete(
            source,
            new MartialArtDeleteRequest(
                WorkspaceId, 31, target.Identity, target.Revision, Confirmed: true));
        XDocument after = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement[] arts = after.Root!.Element("martialarts")!.Elements("martialart").ToArray();
        XElement first = arts.Single(art => art.Element("guid")!.Value == FirstArt.ToString("D"));
        XElement other = arts.Single(art => art.Element("guid")!.Value == OtherArt.ToString("D"));
        Assert.IsFalse(first.Descendants("martialarttechnique")
            .Any(technique => technique.Element("guid")!.Value == FirstTechnique.ToString("D")));
        Assert.IsTrue(first.Descendants("martialarttechnique")
            .Any(technique => technique.Element("guid")!.Value == SecondTechnique.ToString("D")));
        Assert.IsTrue(other.Descendants("martialarttechnique")
            .Any(technique => technique.Element("guid")!.Value == OtherTechnique.ToString("D")
                && technique.Element("name")!.Value == "Disarm"));
        Assert.IsFalse(after.Root.Element("improvements")!.Elements("improvement").Any(improvement =>
            improvement.Element("improvementsource")!.Value == "MartialArtTechnique"
            && improvement.Element("sourcename")!.Value == FirstTechnique.ToString("D")));
        Assert.IsTrue(after.Root.Element("improvements")!.Elements("improvement").Any(improvement =>
            improvement.Element("improvementsource")!.Value == "MartialArtTechnique"
            && improvement.Element("sourcename")!.Value == OtherTechnique.ToString("D")));
    }

    [TestMethod]
    public void Quality_art_duplicate_identity_stale_revision_and_ambiguous_improvement_fail_closed()
    {
        string source = Xml(true);
        MartialArtDeleteEditorState editor = MartialArtDeleteEditorProjector.Project(source, WorkspaceId, 31);
        CharacterMartialArtDeleteState quality = editor.Targets.Single(
            state => state.Identity == new CharacterMartialArtDeleteIdentity(QualityArt, null));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyMartialArtDelete(
                source,
                new MartialArtDeleteRequest(
                    WorkspaceId, 31, quality.Identity, quality.Revision, Confirmed: true)));

        CharacterMartialArtDeleteState technique = editor.Targets.Single(
            state => state.Identity == new CharacterMartialArtDeleteIdentity(FirstArt, FirstTechnique));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyMartialArtDelete(
                source,
                new MartialArtDeleteRequest(
                    WorkspaceId, 31, technique.Identity, new string('0', 64), Confirmed: true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => MartialArtDeleteEditorProjector.Project(
            source.Replace(OtherTechnique.ToString("D"), FirstTechnique.ToString("D"), StringComparison.Ordinal),
            WorkspaceId,
            31));
        Assert.ThrowsExactly<InvalidOperationException>(() => MartialArtDeleteEditorProjector.Project(
            source.Replace(
                "<improvementsource>MartialArt</improvementsource>",
                "<improvementsource>MartialArt</improvementsource><improvementsource>Quality</improvementsource>",
                StringComparison.Ordinal),
            WorkspaceId,
            31));
    }

    private static string Xml(bool created) => $$"""
<character>
  <created>{{created}}</created><karma>19</karma><nuyen>1234.56</nuyen>
  <martialarts>
    <martialart><guid>{{FirstArt:D}}</guid><name>Aikido</name><isquality>False</isquality><cost>7</cost><source>RG</source><martialarttechniques>
      <martialarttechnique><guid>{{FirstTechnique:D}}</guid><name>Disarm</name><source>RG</source><notes>first</notes></martialarttechnique>
      <martialarttechnique><guid>{{SecondTechnique:D}}</guid><name>Kick Attack</name><source>RG</source><notes>second</notes></martialarttechnique>
    </martialarttechniques><notes>art sentinel</notes></martialart>
    <martialart><guid>{{OtherArt:D}}</guid><name>Krav Maga</name><isquality>False</isquality><cost>7</cost><source>RG</source><martialarttechniques>
      <martialarttechnique><guid>{{OtherTechnique:D}}</guid><name>Disarm</name><source>RG</source><notes>other parent</notes></martialarttechnique>
    </martialarttechniques><notes>other art sentinel</notes></martialart>
    <martialart><guid>{{QualityArt:D}}</guid><name>Quality Art</name><isquality>True</isquality><cost>0</cost><source>RF</source><martialarttechniques>
      <martialarttechnique><guid>{{QualityTechnique:D}}</guid><name>Quality Technique</name><source>RF</source></martialarttechnique>
    </martialarttechniques></martialart>
  </martialarts>
  <improvements>
    {{Improvement("MartialArt", FirstArt, "art improvement")}}
    {{Improvement("MartialArtTechnique", FirstTechnique, "first technique improvement")}}
    {{Improvement("MartialArtTechnique", SecondTechnique, "second technique improvement")}}
    {{Improvement("Quality", FirstArt, "same id unrelated source")}}
    {{Improvement("MartialArt", OtherArt, "other art improvement")}}
    {{Improvement("MartialArtTechnique", OtherTechnique, "other technique improvement")}}
    {{Improvement("MartialArt", QualityArt, "quality art improvement")}}
    {{Improvement("MartialArtTechnique", QualityTechnique, "quality technique improvement")}}
  </improvements>
  <customstate>runner sentinel</customstate>
</character>
""";

    private static string Improvement(string source, Guid sourceName, string marker)
        => $"<improvement><improvedname>{marker}</improvedname><sourcename>{sourceName:D}</sourcename>"
            + $"<improvementsource>{source}</improvementsource><enabled>True</enabled></improvement>";
}
