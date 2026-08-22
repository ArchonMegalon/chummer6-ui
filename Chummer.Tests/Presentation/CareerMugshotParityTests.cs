using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerMugshotParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-mugshot-tests");

    [TestMethod]
    public void Career_projects_exact_order_identity_default_and_main_state()
    {
        CareerMugshotEditorState editor = CareerMugshotEditorProjector.Project(
            Xml(created: true, mainIndex: 1), WorkspaceId, 17);

        Assert.AreEqual(2, editor.Items.Count);
        Assert.AreEqual(2, editor.MugshotState.DefaultSelectedOneBasedIndex);
        Assert.AreEqual(1, editor.MugshotState.MainMugshotIndex);
        Assert.AreEqual(0, editor.Items[0].Identity.ZeroBasedIndex);
        Assert.AreEqual(1, editor.Items[1].Identity.ZeroBasedIndex);
        Assert.AreNotEqual(
            editor.Items[0].Identity.ImageSha256,
            editor.Items[1].Identity.ImageSha256);
    }

    [TestMethod]
    public void Main_mutation_changes_only_main_index_and_preserves_exact_images_and_sentinels()
    {
        string source = Xml(created: true, mainIndex: 0);
        CareerMugshotEditorState editor = CareerMugshotEditorProjector.Project(
            source, WorkspaceId, 17);
        string mutated = WorkspaceXmlMutationCatalog.ApplyCareerMugshotMainEdit(
            source,
            new CareerMugshotMainEditRequest(
                WorkspaceId,
                17,
                editor.Items[1].Identity,
                editor.MugshotState.Revision,
                IsMain: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        Assert.AreEqual("1", document.Root!.Element("mainmugshotindex")!.Value);
        CollectionAssert.AreEqual(
            XDocument.Parse(source).Root!.Element("mugshots")!.Elements("mugshot").Select(x => x.Value).ToArray(),
            document.Root.Element("mugshots")!.Elements("mugshot").Select(x => x.Value).ToArray());
        Assert.AreEqual("Mugshot sentinel", document.Root.Element("customstate")!.Value);
        Assert.AreEqual("3141", document.Root.Element("nuyen")!.Value);
        Assert.AreEqual("27", document.Root.Element("karma")!.Value);
    }

    [TestMethod]
    public void Creation_malformed_stale_noop_and_invalid_main_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerMugshotEditorProjector.Project(Xml(created: false, mainIndex: 0), WorkspaceId, 17));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerMugshotEditorProjector.Project(Xml(created: true, mainIndex: 2), WorkspaceId, 17));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerMugshotEditorProjector.Project(
                Xml(created: true, mainIndex: 0).Replace("AQIDBA==", "not-base64", StringComparison.Ordinal),
                WorkspaceId,
                17));

        string source = Xml(created: true, mainIndex: 0);
        CareerMugshotEditorState editor = CareerMugshotEditorProjector.Project(source, WorkspaceId, 17);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerMugshotMainEdit(
                source,
                new CareerMugshotMainEditRequest(
                    WorkspaceId,
                    17,
                    editor.Items[1].Identity,
                    new string('0', 64),
                    IsMain: true)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerMugshotMainEdit(
                source,
                new CareerMugshotMainEditRequest(
                    WorkspaceId,
                    17,
                    editor.Items[0].Identity,
                    editor.MugshotState.Revision,
                    IsMain: true)));
    }

    private static string Xml(bool created, int mainIndex) => $"""
        <character>
          <created>{created}</created>
          <mainmugshotindex>{mainIndex}</mainmugshotindex>
          <mugshots>
            <mugshot>AQIDBA==</mugshot>
            <mugshot>BQYHCA==</mugshot>
          </mugshots>
          <nuyen>3141</nuyen><karma>27</karma><customstate>Mugshot sentinel</customstate>
        </character>
        """;
}
