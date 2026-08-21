using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearNameParityTests
{
    private const string ParentId = "11111111-1111-1111-1111-111111111111";
    private const string ChildId = "22222222-2222-2222-2222-222222222222";

    [TestMethod]
    public void Projector_ExposesExactOptionalGearNameForTopLevelAndNestedGear()
    {
        JsonObject section = new()
        {
            ["gear"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = ParentId,
                    ["name"] = "Commlink",
                    ["gearName"] = "Primary link",
                    ["parentGuid"] = ""
                },
                new JsonObject
                {
                    ["guid"] = ChildId,
                    ["name"] = "Module",
                    ["gearName"] = "Hidden module",
                    ["parentGuid"] = ParentId,
                    ["depth"] = 1
                }
            }
        };

        WorkspaceCollectionEditorState editor = WorkspaceCollectionEditorProjector.TryProject("gear", section)!;
        WorkspaceCollectionTextValueState parent = editor.Items
            .Single(item => item.Target.NestedKind is null)
            .TextValues.Single(value => value.Field == WorkspaceCollectionTextField.GearName);
        WorkspaceCollectionTextValueState child = editor.Items
            .Single(item => item.Target.NestedKind == WorkspaceNestedCollectionKind.Gear)
            .TextValues.Single(value => value.Field == WorkspaceCollectionTextField.GearName);

        Assert.AreEqual("Primary link", parent.Value);
        Assert.AreEqual("Hidden module", child.Value);
        Assert.AreEqual(32_767, parent.MaximumLength);
        Assert.IsFalse(parent.IsRequired);
    }

    [TestMethod]
    public void Mutation_ChangesOnlyStableGearNameAndAllowsLegacyBlankValue()
    {
        const string xml = """
            <character><gears><gear>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Commlink</name><gearname>Primary link</gearname><extra>Preserve parent</extra>
              <children><gear>
                <guid>22222222-2222-2222-2222-222222222222</guid>
                <name>Module</name><gearname>Hidden module</gearname><extra>Preserve child</extra>
              </gear></children>
            </gear></gears><alias>Untouched</alias></character>
            """;
        var parent = new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, ParentId);
        var child = new WorkspaceCollectionItemTarget(
            WorkspaceCollectionKind.Gear,
            ParentId,
            WorkspaceNestedCollectionKind.Gear,
            ChildId);

        string parentPatched = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(parent, WorkspaceCollectionTextField.GearName, "Street deck"));
        string childPatched = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            parentPatched,
            new WorkspaceSetCollectionTextRequest(child, WorkspaceCollectionTextField.GearName, string.Empty));
        XDocument document = XDocument.Parse(childPatched);
        XElement parentGear = document.Root!.Element("gears")!.Element("gear")!;
        XElement childGear = parentGear.Element("children")!.Element("gear")!;

        Assert.AreEqual("Street deck", parentGear.Element("gearname")!.Value);
        Assert.AreEqual(string.Empty, childGear.Element("gearname")!.Value);
        Assert.AreEqual("Preserve parent", parentGear.Element("extra")!.Value);
        Assert.AreEqual("Preserve child", childGear.Element("extra")!.Value);
        Assert.AreEqual("Untouched", document.Root.Element("alias")!.Value);
    }

    [TestMethod]
    public void Mutation_RejectsValueBeyondLegacySelectTextLimit()
    {
        const string xml = """
            <character><gears><gear>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Commlink</name><gearname />
            </gear></gears></character>
            """;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Gear, ParentId),
                    WorkspaceCollectionTextField.GearName,
                    new string('x', 32_768))));
    }
}
