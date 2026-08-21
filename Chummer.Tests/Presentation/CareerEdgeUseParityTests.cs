using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerEdgeUseParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-edge-use-tests");
    private const string Xml = "<character><created>True</created><edgeused>1</edgeused><attributes><attribute><name>EDG</name><totalvalue>4</totalvalue></attribute></attributes><customstate><edgeused>keep</edgeused></customstate></character>";

    [TestMethod]
    public void Spend_and_regain_mutate_only_root_edgeused()
    {
        CareerEdgeUseEditorState editor = CareerEdgeUseEditorProjector.Project(Xml, WorkspaceId, 9);
        XElement spent = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerEdgeUseEdit(
            Xml,
            new CareerEdgeUseEditRequest(
                WorkspaceId,
                9,
                editor.Edge,
                CharacterCareerEdgeUseAction.Spend))).Root!;
        Assert.AreEqual("2", spent.Element("edgeused")!.Value);
        Assert.AreEqual("keep", spent.Element("customstate")!.Element("edgeused")!.Value);

        const string usedTwo = "<character><created>True</created><edgeused>2</edgeused><attributes><attribute><name>EDG</name><totalvalue>4</totalvalue></attribute></attributes></character>";
        CareerEdgeUseEditorState regain = CareerEdgeUseEditorProjector.Project(usedTwo, WorkspaceId, 10);
        XElement regained = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerEdgeUseEdit(
            usedTwo,
            new CareerEdgeUseEditRequest(
                WorkspaceId,
                10,
                regain.Edge,
                CharacterCareerEdgeUseAction.Regain))).Root!;
        Assert.AreEqual("1", regained.Element("edgeused")!.Value);
    }

    [TestMethod]
    public void Projector_and_mutation_fail_closed_on_creation_duplicate_and_stale_state()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerEdgeUseEditorProjector.Project(
            Xml.Replace("<created>True</created>", "<created>False</created>"),
            WorkspaceId,
            1));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerEdgeUseEditorProjector.Project(
            Xml.Replace("<edgeused>1</edgeused>", "<edgeused>1</edgeused><edgeused>2</edgeused>"),
            WorkspaceId,
            1));
        CareerEdgeUseEditorState editor = CareerEdgeUseEditorProjector.Project(Xml, WorkspaceId, 9);
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyCareerEdgeUseEdit(
            Xml.Replace("<edgeused>1</edgeused>", "<edgeused>2</edgeused>"),
            new CareerEdgeUseEditRequest(
                WorkspaceId,
                9,
                editor.Edge,
                CharacterCareerEdgeUseAction.Spend)));
    }
}
