using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ArmorTreeFlagParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("armor-tree-flag-tests");
    private static readonly Guid ArmorId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid ModId = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    private static readonly Guid ArmorGearId = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid ArmorChildId = Guid.Parse("d1111111-1111-1111-1111-111111111111");
    private static readonly Guid ModGearId = Guid.Parse("e1111111-1111-1111-1111-111111111111");
    private static readonly Guid ModChildId = Guid.Parse("f1111111-1111-1111-1111-111111111111");

    [TestMethod]
    public void Project_covers_Armor_ArmorMod_and_recursive_Gear_under_both_parents()
    {
        ArmorTreeFlagEditorState editor = ArmorTreeFlagEditorProjector.Project(
            CreationXml(), WorkspaceId, 7, ArmorId);

        Assert.AreEqual(6, editor.Nodes.Count);
        Assert.AreEqual(1, editor.Nodes.Count(node => node.Identity.Kind == CharacterArmorTreeNodeKind.Armor));
        Assert.AreEqual(1, editor.Nodes.Count(node => node.Identity.Kind == CharacterArmorTreeNodeKind.ArmorMod));
        Assert.AreEqual(4, editor.Nodes.Count(node => node.Identity.Kind == CharacterArmorTreeNodeKind.Gear));
        Assert.IsTrue(editor.Nodes.Any(node =>
            node.Identity.Kind == CharacterArmorTreeNodeKind.Gear
            && node.Identity.ArmorModId is null
            && node.Identity.GearPath.Count == 2));
        Assert.IsTrue(editor.Nodes.Any(node =>
            node.Identity.Kind == CharacterArmorTreeNodeKind.Gear
            && node.Identity.ArmorModId is not null
            && node.Identity.GearPath.Count == 2));
    }

    [TestMethod]
    public void Apply_mutates_each_exact_node_kind_and_preserves_unrelated_nested_xml()
    {
        string source = CreationXml();
        ArmorTreeFlagEditorState editor = ArmorTreeFlagEditorProjector.Project(
            source, WorkspaceId, 7, ArmorId);
        CharacterArmorTreeFlagState[] targets =
        [
            editor.Nodes.Single(node => node.Identity.Kind == CharacterArmorTreeNodeKind.Armor),
            editor.Nodes.Single(node => node.Identity.Kind == CharacterArmorTreeNodeKind.ArmorMod),
            editor.Nodes.Single(node =>
                node.Identity.Kind == CharacterArmorTreeNodeKind.Gear
                && node.Identity.ArmorModId is null
                && node.Identity.GearPath.Count == 2),
            editor.Nodes.Single(node =>
                node.Identity.Kind == CharacterArmorTreeNodeKind.Gear
                && node.Identity.ArmorModId is not null
                && node.Identity.GearPath.Count == 2)
        ];

        foreach (CharacterArmorTreeFlagState target in targets)
        {
            string mutated = WorkspaceXmlMutationCatalog.ApplyArmorTreeFlagEdit(
                source,
                new ArmorTreeFlagEditRequest(
                    WorkspaceId,
                    7,
                    target.Identity,
                    target.Revision,
                    !target.Stolen,
                    !target.DiscountedCost));
            XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
            XElement node = ArmorTreeFlagEditorProjector.FindNode(document.Root!, target.Identity);
            Assert.AreEqual((!target.Stolen).ToString(), node.Element("stolen")!.Value);
            Assert.AreEqual((!target.DiscountedCost).ToString(), node.Element("discountedcost")!.Value);
            Assert.AreEqual("Selected armor sentinel", document.Root!.Element("armors")!
                .Element("armor")!.Element("notes")!.Value);
            Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
            Assert.AreEqual("Untouched sibling", document.Root!.Element("armors")!
                .Elements("armor").Last().Element("notes")!.Value);
        }
    }

    [TestMethod]
    public void Career_duplicate_identity_stale_revision_and_noop_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ArmorTreeFlagEditorProjector.Project(
            CreationXml().Replace("<created>False</created>", "<created>True</created>", StringComparison.Ordinal),
            WorkspaceId,
            7,
            ArmorId));
        Assert.ThrowsExactly<InvalidOperationException>(() => ArmorTreeFlagEditorProjector.Project(
            CreationXml().Replace(ModChildId.ToString("D"), ModGearId.ToString("D"), StringComparison.Ordinal),
            WorkspaceId,
            7,
            ArmorId));

        ArmorTreeFlagEditorState editor = ArmorTreeFlagEditorProjector.Project(
            CreationXml(), WorkspaceId, 7, ArmorId);
        CharacterArmorTreeFlagState target = editor.Nodes.Single(node =>
            node.Identity.Kind == CharacterArmorTreeNodeKind.Armor);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyArmorTreeFlagEdit(
                CreationXml(),
                new ArmorTreeFlagEditRequest(
                    WorkspaceId, 7, target.Identity, new string('0', 64), true, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyArmorTreeFlagEdit(
                CreationXml(),
                new ArmorTreeFlagEditRequest(
                    WorkspaceId,
                    7,
                    target.Identity,
                    target.Revision,
                    target.Stolen,
                    target.DiscountedCost)));
    }

    private static string CreationXml() => $$"""
<character>
  <created>False</created>
  <armors>
    <armor>
      <guid>{{ArmorId:D}}</guid><name>Root Armor</name><stolen>False</stolen><discountedcost>False</discountedcost>
      <notes>Selected armor sentinel</notes>
      <armormods>
        <armormod>
          <guid>{{ModId:D}}</guid><name>Armor Mod</name><stolen>True</stolen><discountedcost>False</discountedcost>
          <gears><gear>
            <guid>{{ModGearId:D}}</guid><name>Mod Gear</name><stolen>False</stolen><discountedcost>True</discountedcost>
            <children><gear><guid>{{ModChildId:D}}</guid><name>Nested Mod Gear</name><stolen>True</stolen><discountedcost>True</discountedcost><marker>mod child sentinel</marker></gear></children>
          </gear></gears>
        </armormod>
      </armormods>
      <gears><gear>
        <guid>{{ArmorGearId:D}}</guid><name>Armor Gear</name><stolen>True</stolen><discountedcost>False</discountedcost>
        <children><gear><guid>{{ArmorChildId:D}}</guid><name>Nested Armor Gear</name><stolen>False</stolen><discountedcost>True</discountedcost><marker>armor child sentinel</marker></gear></children>
      </gear></gears>
    </armor>
    <armor><guid>a2222222-2222-2222-2222-222222222222</guid><name>Other Armor</name><notes>Untouched sibling</notes></armor>
  </armors>
  <customstate>Runner sentinel</customstate>
</character>
""";
}
