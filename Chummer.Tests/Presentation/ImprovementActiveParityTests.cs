using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ImprovementActiveParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("improvement-active-tests");

    [TestMethod]
    public void Career_projects_direct_improvements_and_legacy_enabled_forms()
    {
        ImprovementActiveEditorState editor = ImprovementActiveEditorProjector.Project(
            CareerXml(), WorkspaceId, 9);

        Assert.AreEqual(3, editor.Improvements.Count);
        Assert.IsTrue(editor.Improvements.Single(item => item.Identity.ImprovedName == "BOD").Enabled);
        Assert.IsFalse(editor.Improvements.Single(item => item.Identity.ImprovedName == "AGI").Enabled);
        Assert.IsTrue(editor.Improvements.Single(item => item.Identity.ImprovedName == "REA").Enabled);
    }

    [TestMethod]
    public void Apply_writes_numeric_enabled_and_preserves_unrelated_xml()
    {
        string source = CareerXml();
        CharacterImprovementActiveState target = ImprovementActiveEditorProjector
            .Project(source, WorkspaceId, 9)
            .Improvements.Single(item => item.Identity.ImprovedName == "AGI");

        string mutated = WorkspaceXmlMutationCatalog.ApplyImprovementActiveEdit(
            source,
            new ImprovementActiveEditRequest(
                WorkspaceId,
                9,
                target.Identity,
                target.Revision,
                Enabled: true));
        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement node = ImprovementActiveEditorProjector.FindNode(document.Root!, target.Identity);
        Assert.AreEqual("1", node.Element("enabled")!.Value);
        Assert.AreEqual("improvement sentinel", node.Element("notes")!.Value);
        Assert.AreEqual("runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Creation_duplicate_identity_invalid_enabled_stale_revision_and_noop_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementActiveEditorProjector.Project(
            CareerXml().Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            WorkspaceId,
            9));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementActiveEditorProjector.Project(
            CareerXml().Replace("<improvedname>AGI</improvedname>", "<improvedname>BOD</improvedname>", StringComparison.Ordinal),
            WorkspaceId,
            9));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementActiveEditorProjector.Project(
            CareerXml().Replace("<enabled>0</enabled>", "<enabled>unknown</enabled>", StringComparison.Ordinal),
            WorkspaceId,
            9));

        CharacterImprovementActiveState target = ImprovementActiveEditorProjector
            .Project(CareerXml(), WorkspaceId, 9)
            .Improvements.First();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementActiveEdit(
                CareerXml(),
                new ImprovementActiveEditRequest(
                    WorkspaceId, 9, target.Identity, new string('0', 64), false)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementActiveEdit(
                CareerXml(),
                new ImprovementActiveEditRequest(
                    WorkspaceId, 9, target.Identity, target.Revision, target.Enabled)));
    }

    private static string CareerXml() => """
<character>
  <created>True</created>
  <improvements>
    <improvement><improvedname>BOD</improvedname><sourcename>a1111111-1111-1111-1111-111111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><customname>Body bonus</customname><enabled>2</enabled><notes>improvement sentinel</notes></improvement>
    <improvement><improvedname>AGI</improvedname><sourcename>a1111111-1111-1111-1111-111111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><customname>Agility bonus</customname><enabled>0</enabled></improvement>
    <improvement><improvedname>REA</improvedname><sourcename>b1111111-1111-1111-1111-111111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><customname>Reaction bonus</customname><enabled>True</enabled></improvement>
  </improvements>
  <customstate>runner sentinel</customstate>
</character>
""";
}
