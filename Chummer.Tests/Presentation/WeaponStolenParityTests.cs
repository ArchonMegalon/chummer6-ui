using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WeaponStolenParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("weapon-stolen-tests");
    private static readonly Guid RootId = Guid.Parse("aa131111-1311-4311-8311-131111111111");

    [TestMethod]
    public void Project_requires_creation_eligibility_and_covers_exact_typed_tree()
    {
        WeaponStolenEditorState editor = WeaponStolenEditorProjector.Project(
            CreationXml(), WorkspaceId, 23, RootId);

        Assert.AreEqual(5, editor.Nodes.Count);
        Assert.IsTrue(editor.Nodes.Any(node => node.Identity.Path.Select(hop => hop.Kind).SequenceEqual([
            CharacterWeaponStolenNodeKind.Weapon,
            CharacterWeaponStolenNodeKind.WeaponAccessory,
            CharacterWeaponStolenNodeKind.Gear,
            CharacterWeaponStolenNodeKind.Gear
        ])));
        Assert.IsTrue(editor.Nodes.Any(node => node.Identity.Path.Count == 2
            && node.Identity.Path[^1].Kind == CharacterWeaponStolenNodeKind.Weapon));
        Assert.IsTrue(editor.Nodes.All(node =>
            node.Phase == CharacterWeaponStolenPhase.Creation
            && node.Economics is { NuyenDelta: 0m, KarmaDelta: 0 }));

        Assert.ThrowsExactly<InvalidOperationException>(() => WeaponStolenEditorProjector.Project(
            CreationXml().Replace("<created>False</created>", "<created>True</created>", StringComparison.Ordinal),
            WorkspaceId, 23, RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => WeaponStolenEditorProjector.Project(
            CreationXml().Replace("<enabled>1</enabled>", "<enabled>0</enabled>", StringComparison.Ordinal),
            WorkspaceId, 23, RootId));
    }

    [TestMethod]
    public void Apply_mutates_only_exact_nested_gear_and_preserves_economics_and_unrelated_xml()
    {
        string source = CreationXml();
        CharacterWeaponStolenState target = WeaponStolenEditorProjector
            .Project(source, WorkspaceId, 23, RootId)
            .Nodes.Single(node => node.Identity.Path.Count == 4);
        string mutated = WorkspaceXmlMutationCatalog.ApplyWeaponStolenEdit(
            source,
            new WeaponStolenEditRequest(
                WorkspaceId, 23, target.Identity, target.Revision, Stolen: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = WeaponStolenEditorProjector.FindNode(document.Root!, target.Identity);
        Assert.AreEqual("True", selected.Element("stolen")!.Value);
        Assert.AreEqual("Nested Gear sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("4321", document.Root!.Element("nuyen")!.Value);
        Assert.AreEqual("7", document.Root!.Element("karma")!.Value);
        Assert.AreEqual("False", document.Root!.Element("weapons")!
            .Elements("weapon").Last().Element("stolen")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Duplicate_identity_invalid_topology_stale_revision_and_noop_fail_closed()
    {
        string source = CreationXml();
        CharacterWeaponStolenState root = WeaponStolenEditorProjector
            .Project(source, WorkspaceId, 23, RootId).Nodes[0];
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyWeaponStolenEdit(
            source,
            new WeaponStolenEditRequest(WorkspaceId, 23, root.Identity, new string('0', 64), true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyWeaponStolenEdit(
            source,
            new WeaponStolenEditRequest(WorkspaceId, 23, root.Identity, root.Revision, false)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WeaponStolenEditorProjector.Project(
            source.Replace("<stolen>False</stolen>", "<stolen>maybe</stolen>", StringComparison.Ordinal),
            WorkspaceId, 23, RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => WeaponStolenEditorProjector.Project(
            source.Replace(
                "<guid>dd131111-1311-4311-8311-131111111111</guid>",
                "<guid>bb131111-1311-4311-8311-131111111111</guid>",
                StringComparison.Ordinal),
            WorkspaceId, 23, RootId));
    }

    private static string CreationXml() => """
        <character>
          <created>False</created><nuyen>4321</nuyen><karma>7</karma>
          <improvements><improvement>
            <improvementttype>Nuyen</improvementttype><improvedname>Stolen</improvedname>
            <condition>create</condition><addtorating>0</addtorating><enabled>1</enabled>
          </improvement></improvements>
          <weapons>
            <weapon>
              <guid>aa131111-1311-4311-8311-131111111111</guid><name>Root Weapon</name>
              <stolen>False</stolen><notes>Root sentinel</notes>
              <accessories><accessory>
                <guid>bb131111-1311-4311-8311-131111111111</guid><name>Accessory</name>
                <stolen>True</stolen><notes>Accessory sentinel</notes>
                <gears><gear>
                  <guid>cc131111-1311-4311-8311-131111111111</guid><name>Accessory Gear</name>
                  <stolen>False</stolen><notes>Gear sentinel</notes>
                  <children><gear>
                    <guid>dd131111-1311-4311-8311-131111111111</guid><name>Nested Gear</name>
                    <stolen>False</stolen><notes>Nested Gear sentinel</notes><children />
                  </gear></children>
                </gear></gears>
              </accessory></accessories>
              <underbarrel><weapon>
                <guid>ee131111-1311-4311-8311-131111111111</guid><name>Underbarrel</name>
                <stolen>True</stolen><notes>Underbarrel sentinel</notes>
              </weapon></underbarrel>
            </weapon>
            <weapon>
              <guid>ff131111-1311-4311-8311-131111111111</guid><name>Untouched</name>
              <stolen>False</stolen><notes>Untouched sentinel</notes>
            </weapon>
          </weapons>
          <customstate>Runner sentinel</customstate>
        </character>
        """;
}
