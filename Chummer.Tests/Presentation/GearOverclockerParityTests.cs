using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearOverclockerParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("gear-overclocker-tests");
    private static readonly Guid RootId = Guid.Parse("a9151111-1511-4511-8511-151111111111");

    [TestMethod]
    public void Project_requires_career_active_improvement_and_selects_only_cyberdecks()
    {
        GearOverclockerEditorState editor = GearOverclockerEditorProjector.Project(
            CareerXml(), WorkspaceId, 31, RootId);

        Assert.AreEqual(1, editor.Nodes.Count);
        CharacterGearOverclockerState node = editor.Nodes.Single();
        Assert.AreEqual(2, node.Identity.GearPath.Count);
        Assert.AreEqual(CharacterGearOverclockerTarget.Attack, node.Attribute);
        Assert.AreEqual(CharacterGearOverclockerPhase.Career, node.Phase);
        Assert.AreEqual(0m, node.Economics.NuyenDelta);
        Assert.AreEqual(0, node.Economics.KarmaDelta);

        Assert.ThrowsExactly<InvalidOperationException>(() => GearOverclockerEditorProjector.Project(
            CareerXml().Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            WorkspaceId, 31, RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearOverclockerEditorProjector.Project(
            CareerXml().Replace("<enabled>1</enabled>", "<enabled>0</enabled>", StringComparison.Ordinal),
            WorkspaceId, 31, RootId));
    }

    [TestMethod]
    public void Apply_mutates_only_nested_cyberdeck_and_preserves_economics_flags_and_siblings()
    {
        string source = CareerXml();
        CharacterGearOverclockerState target = GearOverclockerEditorProjector
            .Project(source, WorkspaceId, 31, RootId).Nodes.Single();
        string mutated = WorkspaceXmlMutationCatalog.ApplyGearOverclockerEdit(
            source,
            new GearOverclockerEditRequest(
                WorkspaceId,
                31,
                target.Identity,
                target.Revision,
                CharacterGearOverclockerTarget.DataProcessing));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = GearOverclockerEditorProjector.FindNode(document.Root!, target.Identity);
        Assert.AreEqual("Data Processing", selected.Element("overclocked")!.Value);
        Assert.AreEqual("Nested Cyberdeck sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("True", selected.Element("active")!.Value);
        Assert.AreEqual("True", selected.Element("homenode")!.Value);
        Assert.AreEqual("4321", document.Root!.Element("nuyen")!.Value);
        Assert.AreEqual("7", document.Root!.Element("karma")!.Value);
        Assert.AreEqual("Firewall", document.Root!.Element("gears")!
            .Elements("gear").Last().Element("overclocked")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Duplicate_invalid_saved_value_stale_revision_and_noop_fail_closed()
    {
        string source = CareerXml();
        CharacterGearOverclockerState target = GearOverclockerEditorProjector
            .Project(source, WorkspaceId, 31, RootId).Nodes.Single();
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearOverclockerEdit(
            source,
            new GearOverclockerEditRequest(
                WorkspaceId, 31, target.Identity, new string('0', 64),
                CharacterGearOverclockerTarget.Firewall)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearOverclockerEdit(
            source,
            new GearOverclockerEditRequest(
                WorkspaceId, 31, target.Identity, target.Revision,
                CharacterGearOverclockerTarget.Attack)));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearOverclockerEditorProjector.Project(
            source.Replace("<overclocked>Attack</overclocked>", "<overclocked>Device Rating</overclocked>", StringComparison.Ordinal),
            WorkspaceId, 31, RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearOverclockerEditorProjector.Project(
            source.Replace(
                "<guid>b9151111-1511-4511-8511-151111111111</guid>",
                "<guid>a9151111-1511-4511-8511-151111111111</guid>",
                StringComparison.Ordinal),
            WorkspaceId, 31, RootId));
    }

    private static string CareerXml() => """
        <character>
          <created>True</created><nuyen>4321</nuyen><karma>7</karma>
          <improvements><improvement>
            <improvementttype>Overclocker</improvementttype><enabled>1</enabled>
          </improvement></improvements>
          <gears>
            <gear>
              <guid>a9151111-1511-4511-8511-151111111111</guid><name>Gear Root</name>
              <category>Electronics</category><overclocked>None</overclocked><notes>Root sentinel</notes>
              <children><gear>
                <guid>b9151111-1511-4511-8511-151111111111</guid><name>Nested Cyberdeck</name>
                <category>Cyberdecks</category><overclocked>Attack</overclocked>
                <active>True</active><homenode>True</homenode><notes>Nested Cyberdeck sentinel</notes><children />
              </gear></children>
            </gear>
            <gear>
              <guid>c9151111-1511-4511-8511-151111111111</guid><name>Untouched Cyberdeck</name>
              <category>Cyberdecks</category><overclocked>Firewall</overclocked><notes>Untouched sentinel</notes><children />
            </gear>
          </gears>
          <customstate>Runner sentinel</customstate>
        </character>
        """;
}
