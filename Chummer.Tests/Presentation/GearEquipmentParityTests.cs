using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearEquipmentParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("gear-equipment-tests");
    private static readonly Guid RootId = Guid.Parse("a9121111-9111-4111-8111-911111111111");
    private static readonly Guid ChildId = Guid.Parse("b9121111-9111-4111-8111-911111111111");

    [TestMethod]
    public void Create_and_career_project_recursive_stable_identity_and_zero_economics()
    {
        GearEquipmentEditorState creation = GearEquipmentEditorProjector.Project(
            Xml(created: false), WorkspaceId, 17, RootId);
        GearEquipmentEditorState career = GearEquipmentEditorProjector.Project(
            Xml(created: true), WorkspaceId, 18, RootId);

        Assert.AreEqual(2, creation.Nodes.Count);
        Assert.AreEqual(CharacterGearEquipmentPhase.Creation, creation.Nodes[0].Phase);
        Assert.AreEqual(CharacterGearEquipmentPhase.Career, career.Nodes[0].Phase);
        Assert.IsTrue(creation.Nodes.Any(node => node.Identity.GearPath.Count == 2));
        Assert.IsTrue(creation.Nodes.All(node => node.Economics is { NuyenDelta: 0m, KarmaDelta: 0 }));
    }

    [TestMethod]
    public void Apply_changes_only_exact_nested_equipped_value()
    {
        string source = Xml(created: true);
        CharacterGearEquipmentState child = GearEquipmentEditorProjector
            .Project(source, WorkspaceId, 17, RootId)
            .Nodes.Single(node => node.Identity.GearPath.Count == 2);
        string mutated = WorkspaceXmlMutationCatalog.ApplyGearEquipmentEdit(
            source,
            new GearEquipmentEditRequest(
                WorkspaceId, 17, child.Identity, child.Revision, Equipped: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = GearEquipmentEditorProjector.FindNode(document.Root!, child.Identity);
        Assert.AreEqual("True", selected.Element("equipped")!.Value);
        Assert.AreEqual("Nested sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("False", document.Root!.Element("gears")!
            .Elements("gear").Last().Element("equipped")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Included_duplicate_stale_noop_and_invalid_boolean_fail_closed()
    {
        string source = Xml(created: false);
        CharacterGearEquipmentState root = GearEquipmentEditorProjector
            .Project(source, WorkspaceId, 17, RootId).Nodes[0];
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearEquipmentEdit(
            source,
            new GearEquipmentEditRequest(WorkspaceId, 17, root.Identity, new string('0', 64), false)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearEquipmentEdit(
            source,
            new GearEquipmentEditRequest(WorkspaceId, 17, root.Identity, root.Revision, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearEquipmentEditorProjector.Project(
            source.Replace("<equipped>False</equipped>", "<equipped>maybe</equipped>", StringComparison.Ordinal),
            WorkspaceId, 17, RootId));

        string includedXml = source.Replace(
            "<parentid></parentid>",
            "<parentid>included-source</parentid>",
            StringComparison.Ordinal);
        CharacterGearEquipmentState included = GearEquipmentEditorProjector
            .Project(includedXml, WorkspaceId, 17, RootId).Nodes[0];
        Assert.IsFalse(included.CanChangeEquip);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearEquipmentEdit(
            includedXml,
            new GearEquipmentEditRequest(WorkspaceId, 17, included.Identity, included.Revision, false)));

        string duplicate = source.Replace(
            "</gears>",
            $"<gear><guid>{RootId:D}</guid><name>Duplicate</name><equipped>True</equipped></gear></gears>",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() => GearEquipmentEditorProjector.Project(
            duplicate, WorkspaceId, 17, RootId));
    }

    private static string Xml(bool created) => $"""
        <character>
          <created>{created}</created>
          <gears>
            <gear>
              <guid>{RootId:D}</guid><name>Root Gear</name><parentid></parentid>
              <equipped>True</equipped><notes>Root sentinel</notes>
              <children><gear>
                <guid>{ChildId:D}</guid><name>Nested Gear</name>
                <equipped>False</equipped><notes>Nested sentinel</notes>
              </gear></children>
            </gear>
            <gear>
              <guid>c9121111-9111-4111-8111-911111111111</guid><name>Untouched</name>
              <equipped>False</equipped><notes>Untouched sentinel</notes>
            </gear>
          </gears>
          <nuyen>1234</nuyen><karma>7</karma><customstate>Runner sentinel</customstate>
        </character>
        """;
}
