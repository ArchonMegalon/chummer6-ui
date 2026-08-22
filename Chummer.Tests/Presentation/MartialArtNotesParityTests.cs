using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class MartialArtNotesParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("martial-art-notes-tests");
    private static readonly Guid FirstArt = Guid.Parse("91111111-9111-9111-9111-911111111111");
    private static readonly Guid FirstTechnique = Guid.Parse("92222222-9222-9222-9222-922222222222");
    private static readonly Guid SecondArt = Guid.Parse("93333333-9333-9333-9333-933333333333");

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Creation_and_career_project_art_and_parent_scoped_technique(bool created)
    {
        MartialArtNotesEditorState editor = MartialArtNotesEditorProjector.Project(
            Xml(created), WorkspaceId, 23);
        Assert.AreEqual(3, editor.Targets.Count);
        CharacterMartialArtNotesState technique = editor.Targets.Single(state => state.Identity.TechniqueId == FirstTechnique);
        Assert.AreEqual(FirstArt, technique.Identity.MartialArtId);
        Assert.AreEqual("Aikido", technique.MartialArtName);
        Assert.AreEqual("Disarm", technique.TargetName);
        Assert.AreEqual("Chocolate", technique.NotesColor);
        Assert.AreEqual(0, technique.Economics.KarmaDelta);
        Assert.AreEqual(0m, technique.Economics.NuyenDelta);
    }

    [TestMethod]
    public void Apply_changes_only_target_notes_and_color_and_preserves_all_other_semantics()
    {
        string source = Xml(true);
        CharacterMartialArtNotesState target = MartialArtNotesEditorProjector
            .Project(source, WorkspaceId, 23)
            .Targets.Single(state => state.Identity.TechniqueId == FirstTechnique);
        XDocument before = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
        XElement beforeTarget = MartialArtNotesEditorProjector.FindNode(before.Root!, target.Identity);
        string[] beforeNonNotes = beforeTarget.Elements()
            .Where(element => element.Name.LocalName is not ("notes" or "notesColor"))
            .Select(element => element.ToString(SaveOptions.DisableFormatting))
            .ToArray();

        string mutated = WorkspaceXmlMutationCatalog.ApplyMartialArtNotesEdit(
            source,
            new MartialArtNotesEditRequest(
                WorkspaceId, 23, target.Identity, target.Revision,
                "Updated technique notes", "#445566"));
        XDocument after = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement afterTarget = MartialArtNotesEditorProjector.FindNode(after.Root!, target.Identity);
        CollectionAssert.AreEqual(
            beforeNonNotes,
            afterTarget.Elements()
                .Where(element => element.Name.LocalName is not ("notes" or "notesColor"))
                .Select(element => element.ToString(SaveOptions.DisableFormatting))
                .ToArray());
        Assert.AreEqual("Updated technique notes", afterTarget.Element("notes")!.Value);
        Assert.AreEqual("#445566", afterTarget.Element("notesColor")!.Value);
        Assert.AreEqual(
            "Second art sentinel",
            after.Root!.Element("martialarts")!.Elements("martialart").Last().Element("notes")!.Value);
        Assert.AreEqual("runner sentinel", after.Root.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Duplicate_or_ambiguous_guids_wrong_parent_and_duplicate_fields_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => MartialArtNotesEditorProjector.Project(
            Xml(false).Replace(SecondArt.ToString("D"), FirstTechnique.ToString("D"), StringComparison.Ordinal),
            WorkspaceId, 23));
        Assert.ThrowsExactly<InvalidOperationException>(() => MartialArtNotesEditorProjector.Project(
            Xml(false).Replace("<notes>Technique sentinel</notes>", "<notes>Technique sentinel</notes><notes>duplicate</notes>", StringComparison.Ordinal),
            WorkspaceId, 23));

        CharacterMartialArtNotesState target = MartialArtNotesEditorProjector
            .Project(Xml(true), WorkspaceId, 23)
            .Targets.Single(state => state.Identity.TechniqueId == FirstTechnique);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyMartialArtNotesEdit(
                Xml(true),
                new MartialArtNotesEditRequest(
                    WorkspaceId, 23,
                    target.Identity with { MartialArtId = SecondArt },
                    target.Revision, "wrong parent", "#445566")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyMartialArtNotesEdit(
                Xml(true),
                new MartialArtNotesEditRequest(
                    WorkspaceId, 23, target.Identity, new string('0', 64), "stale", "#445566")));
    }

    private static string Xml(bool created) => $$"""
<character>
  <created>{{created}}</created>
  <martialarts>
    <martialart>
      <name>Aikido</name><sourceid>a1111111-1111-1111-1111-111111111111</sourceid><guid>{{FirstArt:D}}</guid><source>RG</source><page>128</page><cost>7</cost><isquality>False</isquality>
      <martialarttechniques><martialarttechnique><sourceid>a2222222-2222-2222-2222-222222222222</sourceid><guid>{{FirstTechnique:D}}</guid><name>Disarm</name><notes>Technique sentinel</notes><source>RG</source><page>129</page></martialarttechnique></martialarttechniques>
      <notes>First art sentinel</notes><notesColor>#112233</notesColor>
    </martialart>
    <martialart>
      <name>Krav Maga</name><sourceid>a3333333-3333-3333-3333-333333333333</sourceid><guid>{{SecondArt:D}}</guid><source>RG</source><page>130</page><cost>7</cost><isquality>True</isquality><martialarttechniques/><notes>Second art sentinel</notes><notesColor>#223344</notesColor>
    </martialart>
  </martialarts>
  <karma>19</karma><nuyen>1234.56</nuyen><customstate>runner sentinel</customstate>
</character>
""";
}
