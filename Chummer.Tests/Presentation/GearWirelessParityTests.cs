using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearWirelessParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("gear-wireless-tests");
    private static readonly Guid RootId = Guid.Parse("d9121111-9111-4111-8111-911111111111");
    private static readonly Guid ChildId = Guid.Parse("e9121111-9111-4111-8111-911111111111");

    [TestMethod]
    public void Creation_projects_read_only_while_career_is_zero_economic_and_editable()
    {
        GearWirelessEditorState creation = GearWirelessEditorProjector.Project(
            Xml(created: false), WorkspaceId, 17, RootId);
        GearWirelessEditorState career = GearWirelessEditorProjector.Project(
            Xml(created: true), WorkspaceId, 18, RootId);

        Assert.AreEqual(2, creation.Nodes.Count);
        Assert.IsTrue(creation.Nodes.All(node => !node.CanChangeWireless));
        Assert.IsTrue(career.Nodes.All(node => node.CanChangeWireless));
        Assert.IsTrue(career.Nodes.All(node => node.Economics is { NuyenDelta: 0m, KarmaDelta: 0 }));
        Assert.AreNotEqual(creation.Nodes[0].Revision, career.Nodes[0].Revision);
    }

    [TestMethod]
    public void Career_mutation_changes_only_exact_nested_wirelesson_value()
    {
        string source = Xml(created: true);
        CharacterGearWirelessState child = GearWirelessEditorProjector
            .Project(source, WorkspaceId, 17, RootId)
            .Nodes.Single(node => node.Identity.GearPath.Count == 2);
        string mutated = WorkspaceXmlMutationCatalog.ApplyGearWirelessEdit(
            source,
            new GearWirelessEditRequest(
                WorkspaceId, 17, child.Identity, child.Revision, WirelessOn: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = GearEquipmentEditorProjector.FindNode(document.Root!, child.Identity);
        Assert.AreEqual("True", selected.Element("wirelesson")!.Value);
        Assert.AreEqual("False", selected.Element("equipped")!.Value);
        Assert.AreEqual("Nested sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
        Assert.AreEqual("1234", document.Root!.Element("nuyen")!.Value);
        Assert.AreEqual("7", document.Root!.Element("karma")!.Value);
    }

    [TestMethod]
    public void Creation_stale_noop_and_malformed_wireless_fail_closed()
    {
        string careerXml = Xml(created: true);
        CharacterGearWirelessState root = GearWirelessEditorProjector
            .Project(careerXml, WorkspaceId, 17, RootId).Nodes[0];
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearWirelessEdit(
                careerXml,
                new GearWirelessEditRequest(
                    WorkspaceId, 17, root.Identity, new string('0', 64), false)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearWirelessEdit(
                careerXml,
                new GearWirelessEditRequest(
                    WorkspaceId, 17, root.Identity, root.Revision, true)));

        string creationXml = Xml(created: false);
        CharacterGearWirelessState creation = GearWirelessEditorProjector
            .Project(creationXml, WorkspaceId, 17, RootId).Nodes[0];
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearWirelessEdit(
                creationXml,
                new GearWirelessEditRequest(
                    WorkspaceId, 17, creation.Identity, creation.Revision, false)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            GearWirelessEditorProjector.Project(
                careerXml.Replace("<wirelesson>False</wirelesson>", "<wirelesson>maybe</wirelesson>", StringComparison.Ordinal),
                WorkspaceId,
                17,
                RootId));
    }

    private static string Xml(bool created) => $"""
        <character>
          <created>{created}</created>
          <gears>
            <gear>
              <guid>{RootId:D}</guid><name>Root Gear</name><parentid></parentid>
              <equipped>True</equipped><wirelesson>True</wirelesson><notes>Root sentinel</notes>
              <children><gear>
                <guid>{ChildId:D}</guid><name>Nested Gear</name>
                <equipped>False</equipped><wirelesson>False</wirelesson><notes>Nested sentinel</notes>
              </gear></children>
            </gear>
          </gears>
          <nuyen>1234</nuyen><karma>7</karma><customstate>Runner sentinel</customstate>
        </character>
        """;
}
