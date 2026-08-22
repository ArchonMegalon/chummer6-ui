using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearStolenParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("gear-stolen-tests");
    private static readonly Guid RootGearId = Guid.Parse("a7111111-7111-7111-7111-711111111111");
    private static readonly Guid ChildGearId = Guid.Parse("b7111111-7111-7111-7111-711111111111");
    private static readonly Guid GrandchildGearId = Guid.Parse("c7111111-7111-7111-7111-711111111111");

    [TestMethod]
    public void Project_requires_exact_create_eligibility_and_covers_recursive_Gear_tree()
    {
        GearStolenEditorState editor = GearStolenEditorProjector.Project(
            CreationXml(), WorkspaceId, 11, RootGearId);

        Assert.AreEqual(3, editor.Nodes.Count);
        Assert.AreEqual(RootGearId, editor.RootGearId);
        Assert.IsTrue(editor.Nodes.Any(node => node.Identity.GearPath.Count == 3));
        Assert.IsTrue(editor.Nodes.All(node =>
            node.Revision.Length == CharacterGearStolenRules.RevisionHexLength));

        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace("<created>False</created>", "<created>True</created>", StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace("<enabled>1</enabled>", "<enabled>0</enabled>", StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace("<condition>create</condition>", "<condition>career</condition>", StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace("<addtorating>0</addtorating>", "<addtorating>1</addtorating>", StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));
    }

    [TestMethod]
    public void Apply_mutates_only_exact_nested_stolen_value_and_preserves_unrelated_xml()
    {
        string source = CreationXml();
        CharacterGearStolenState target = GearStolenEditorProjector
            .Project(source, WorkspaceId, 11, RootGearId)
            .Nodes.Single(node => node.Identity.GearPath.Count == 3);
        string mutated = WorkspaceXmlMutationCatalog.ApplyGearStolenEdit(
            source,
            new GearStolenEditRequest(
                WorkspaceId,
                11,
                target.Identity,
                target.Revision,
                Stolen: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = GearStolenEditorProjector.FindNode(document.Root!, target.Identity);
        Assert.AreEqual("True", selected.Element("stolen")!.Value);
        Assert.AreEqual("Selected nested Gear sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("False", document.Root!.Element("gears")!
            .Elements("gear").Last().Element("stolen")!.Value);
        Assert.AreEqual("Untouched Gear sentinel", document.Root!.Element("gears")!
            .Elements("gear").Last().Element("notes")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Duplicate_identity_invalid_boolean_stale_revision_and_noop_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace(GrandchildGearId.ToString("D"), ChildGearId.ToString("D"), StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearStolenEditorProjector.Project(
            CreationXml().Replace("<stolen>True</stolen>", "<stolen>invalid</stolen>", StringComparison.Ordinal),
            WorkspaceId,
            11,
            RootGearId));

        string source = CreationXml();
        CharacterGearStolenState target = GearStolenEditorProjector
            .Project(source, WorkspaceId, 11, RootGearId)
            .Nodes.Single(node => node.Identity.GearPath.Count == 3);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearStolenEdit(
                source,
                new GearStolenEditRequest(
                    WorkspaceId, 11, target.Identity, new string('0', 64), true)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearStolenEdit(
                source,
                new GearStolenEditRequest(
                    WorkspaceId, 11, target.Identity, target.Revision, target.Stolen)));
    }

    private static string CreationXml() => $$"""
<character>
  <created>False</created>
  <improvements>
    <improvement>
      <improvementttype>Nuyen</improvementttype><improvedname>Stolen</improvedname>
      <condition>create</condition><addtorating>0</addtorating><enabled>1</enabled>
    </improvement>
  </improvements>
  <gears>
    <gear>
      <guid>{{RootGearId:D}}</guid><name>Gear Root</name><stolen>False</stolen>
      <children><gear>
        <guid>{{ChildGearId:D}}</guid><name>Gear Child</name><stolen>True</stolen>
        <children><gear><guid>{{GrandchildGearId:D}}</guid><name>Gear Target</name><stolen>False</stolen><notes>Selected nested Gear sentinel</notes></gear></children>
      </gear></children>
    </gear>
    <gear><guid>d7111111-7111-7111-7111-711111111111</guid><name>Other Gear</name><stolen>False</stolen><notes>Untouched Gear sentinel</notes></gear>
  </gears>
  <customstate>Runner sentinel</customstate>
</character>
""";
}
