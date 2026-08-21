using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ImprovementNotesParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("improvement-notes-tests");

    [TestMethod]
    public void Career_projects_direct_improvement_notes_and_legacy_default_color()
    {
        ImprovementNotesEditorState editor = ImprovementNotesEditorProjector.Project(
            CareerXml(), WorkspaceId, 11);

        Assert.AreEqual(2, editor.Improvements.Count);
        Assert.AreEqual(
            "#112233",
            editor.Improvements.Single(item => item.Identity.ImprovedName == "BOD").NotesColor);
        Assert.AreEqual(
            "Chocolate",
            editor.Improvements.Single(item => item.Identity.ImprovedName == "AGI").NotesColor);
    }

    [TestMethod]
    public void Apply_writes_notes_and_color_together_and_preserves_unrelated_xml()
    {
        string source = CareerXml();
        CharacterImprovementNotesState target = ImprovementNotesEditorProjector
            .Project(source, WorkspaceId, 11)
            .Improvements.Single(item => item.Identity.ImprovedName == "AGI");

        string mutated = WorkspaceXmlMutationCatalog.ApplyImprovementNotesEdit(
            source,
            new ImprovementNotesEditRequest(
                WorkspaceId,
                11,
                target.Identity,
                target.Revision,
                "Updated line one\nUpdated line two",
                "#445566"));
        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement node = ImprovementNotesEditorProjector.FindNode(document.Root!, target.Identity);
        Assert.AreEqual("Updated line one\nUpdated line two", node.Element("notes")!.Value);
        Assert.AreEqual("#445566", node.Element("notesColor")!.Value);
        Assert.AreEqual("0", node.Element("enabled")!.Value);
        Assert.AreEqual("runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Creation_duplicate_color_stale_revision_and_noop_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementNotesEditorProjector.Project(
            CareerXml().Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            WorkspaceId,
            11));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementNotesEditorProjector.Project(
            CareerXml().Replace("<notesColor>#112233</notesColor>", "<notesColor>#112233</notesColor><notesColor>#445566</notesColor>", StringComparison.Ordinal),
            WorkspaceId,
            11));

        CharacterImprovementNotesState target = ImprovementNotesEditorProjector
            .Project(CareerXml(), WorkspaceId, 11)
            .Improvements.First();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementNotesEdit(
                CareerXml(),
                new ImprovementNotesEditRequest(
                    WorkspaceId, 11, target.Identity, new string('0', 64), "new", "#445566")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementNotesEdit(
                CareerXml(),
                new ImprovementNotesEditRequest(
                    WorkspaceId, 11, target.Identity, target.Revision, target.Notes, target.NotesColor)));
    }

    private static string CareerXml() => """
<character>
  <created>True</created>
  <improvements>
    <improvement><improvedname>BOD</improvedname><sourcename>a1111111-1111-1111-1111-111111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><customname>Body bonus</customname><enabled>1</enabled><notes>body note</notes><notesColor>#112233</notesColor></improvement>
    <improvement><improvedname>AGI</improvedname><sourcename>a1111111-1111-1111-1111-111111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><customname>Agility bonus</customname><enabled>0</enabled><notes>agility note</notes></improvement>
  </improvements>
  <customstate>runner sentinel</customstate>
</character>
""";
}
