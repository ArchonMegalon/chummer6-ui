using System.Text.Json.Nodes;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class PrototypeTranshumanParityTests
{
    private static readonly Guid ParentId = Guid.Parse("81111111-8111-8111-8111-811111111111");
    private static readonly Guid ChildId = Guid.Parse("82222222-8222-8222-8222-822222222222");

    [TestMethod]
    public void Projector_accepts_only_matching_top_level_cyberware_semantics()
    {
        JsonNode section = JsonNode.Parse($$"""
        {"count":1,"cyberwares":[{"guid":"{{ParentId:D}}","name":"Nephritic Screen","prototypeTranshumanSemantics":{"cyberwareId":"{{ParentId:D}}","prototypeTranshuman":false,"essenceAllowance":1.25,"hierarchy":[{"cyberwareId":"{{ParentId:D}}","prototypeTranshuman":false},{"cyberwareId":"{{ChildId:D}}","prototypeTranshuman":true}]}}]}
        """)!;

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("cyberwares", section)!
            .Items
            .Single();

        Assert.IsNotNull(item.PrototypeTranshuman);
        Assert.AreEqual(ParentId, item.PrototypeTranshuman.CyberwareId);
        Assert.AreEqual(1.25m, item.PrototypeTranshuman.EssenceAllowance);
        Assert.AreEqual(2, item.PrototypeTranshuman.Hierarchy.Count);
    }

    [TestMethod]
    public void Mutation_recursively_sets_root_and_descendants_only()
    {
        string xml = CharacterXml(created: false);
        CharacterPrototypeTranshumanSemantics expected = Project(xml);
        string mutated = WorkspaceXmlMutationCatalog.ApplyPrototypeTranshumanEdit(
            xml,
            new PrototypeTranshumanEditRequest(
                new CharacterWorkspaceId("prototype-projector-test"),
                7,
                ParentId,
                true,
                expected));

        System.Xml.Linq.XElement root = System.Xml.Linq.XElement.Parse(mutated);
        System.Xml.Linq.XElement parent = root.Element("cyberwares")!.Element("cyberware")!;
        System.Xml.Linq.XElement child = parent.Element("children")!.Element("cyberware")!;
        Assert.AreEqual("True", parent.Element("prototypetranshuman")!.Value);
        Assert.AreEqual("True", child.Element("prototypetranshuman")!.Value);
        Assert.AreEqual("keep parent", parent.Element("notes")!.Value);
        Assert.AreEqual("keep child", child.Element("notes")!.Value);
        Assert.AreEqual("untouched", root.Element("customstate")!.Value);

        CharacterPrototypeTranshumanSemantics enabled = Project(mutated);
        string disabled = WorkspaceXmlMutationCatalog.ApplyPrototypeTranshumanEdit(
            mutated,
            new PrototypeTranshumanEditRequest(
                new CharacterWorkspaceId("prototype-disable-test"),
                8,
                ParentId,
                false,
                enabled));
        System.Xml.Linq.XElement disabledRoot = System.Xml.Linq.XElement.Parse(disabled);
        System.Xml.Linq.XElement disabledParent = disabledRoot.Element("cyberwares")!.Element("cyberware")!;
        Assert.AreEqual("False", disabledParent.Element("prototypetranshuman")!.Value);
        Assert.AreEqual(
            "False",
            disabledParent.Element("children")!.Element("cyberware")!.Element("prototypetranshuman")!.Value);
    }

    [TestMethod]
    public void Mutation_rejects_career_stale_hierarchy_and_no_op()
    {
        string creation = CharacterXml(created: false);
        CharacterPrototypeTranshumanSemantics expected = Project(creation);
        PrototypeTranshumanEditRequest request = new(
            new CharacterWorkspaceId("prototype-mutation-test"),
            9,
            ParentId,
            true,
            expected);

        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyPrototypeTranshumanEdit(CharacterXml(created: true), request));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyPrototypeTranshumanEdit(
                creation.Replace(ChildId.ToString("D"), "83333333-8333-8333-8333-833333333333", StringComparison.Ordinal),
                request));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyPrototypeTranshumanEdit(
                creation,
                request with { PrototypeTranshuman = false }));
    }

    private static CharacterPrototypeTranshumanSemantics Project(string xml)
    {
        System.Xml.Linq.XElement root = System.Xml.Linq.XElement.Parse(xml);
        System.Xml.Linq.XElement selected = root.Element("cyberwares")!.Element("cyberware")!;
        Assert.IsTrue(CharacterPrototypeTranshumanRules.TryProject(root, selected, out CharacterPrototypeTranshumanSemantics state));
        return state;
    }

    private static string CharacterXml(bool created) => $$"""
<character>
  <created>{{created}}</created>
  <improvements><improvement><improvementttype>PrototypeTranshuman</improvementttype><val>1.25</val><enabled>1</enabled></improvement></improvements>
  <cyberwares><cyberware><guid>{{ParentId:D}}</guid><name>Nephritic Screen</name><improvementsource>Bioware</improvementsource><prototypetranshuman>False</prototypetranshuman><notes>keep parent</notes><children><cyberware><guid>{{ChildId:D}}</guid><name>Child Option</name><improvementsource>Bioware</improvementsource><notes>keep child</notes></cyberware></children></cyberware></cyberwares>
  <customstate>untouched</customstate>
</character>
""";
}
