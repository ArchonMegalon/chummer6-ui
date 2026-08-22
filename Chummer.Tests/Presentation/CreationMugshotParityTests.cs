using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CreationMugshotParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("creation-mugshot-tests");

    [TestMethod]
    public void Creation_projects_exact_order_identity_default_and_main_state()
    {
        CreationMugshotEditorState editor = CreationMugshotEditorProjector.Project(
            Xml(created: false, mainIndex: 1), WorkspaceId, 19);

        Assert.AreEqual(2, editor.Items.Count);
        Assert.AreEqual(2, editor.MugshotState.DefaultSelectedOneBasedIndex);
        Assert.AreEqual(1, editor.MugshotState.MainMugshotIndex);
        Assert.AreEqual(0, editor.Items[0].Identity.ZeroBasedIndex);
        Assert.AreEqual(1, editor.Items[1].Identity.ZeroBasedIndex);
        Assert.AreNotEqual(
            editor.Items[0].Identity.ImageSha256,
            editor.Items[1].Identity.ImageSha256);
        Assert.AreEqual(2, CharacterCreationMugshotRules.WrapSelection(editor.MugshotState, 0));
        Assert.AreEqual(1, CharacterCreationMugshotRules.WrapSelection(editor.MugshotState, 3));
        Assert.IsTrue(CharacterCreationMugshotRules.IsSelectedMain(editor.MugshotState, 2));
        Assert.IsFalse(CharacterCreationMugshotRules.IsSelectedMain(editor.MugshotState, 1));
    }

    [TestMethod]
    public void Main_mutation_changes_only_main_index_and_preserves_exact_images_and_sentinels()
    {
        string source = Xml(created: false, mainIndex: 0);
        CreationMugshotEditorState editor = CreationMugshotEditorProjector.Project(
            source, WorkspaceId, 19);
        string mutated = WorkspaceXmlMutationCatalog.ApplyCreationMugshotMainEdit(
            source,
            new CreationMugshotMainEditRequest(
                WorkspaceId,
                19,
                editor.Items[1].Identity,
                editor.MugshotState.Revision,
                IsMain: true));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        Assert.AreEqual("1", document.Root!.Element("mainmugshotindex")!.Value);
        CollectionAssert.AreEqual(
            XDocument.Parse(source).Root!.Element("mugshots")!.Elements("mugshot").Select(x => x.Value).ToArray(),
            document.Root.Element("mugshots")!.Elements("mugshot").Select(x => x.Value).ToArray());
        Assert.AreEqual("Creation mugshot sentinel", document.Root.Element("customstate")!.Value);
        Assert.AreEqual("3141", document.Root.Element("nuyen")!.Value);
        Assert.AreEqual("27", document.Root.Element("karma")!.Value);
    }

    [TestMethod]
    public void Career_malformed_stale_noop_and_invalid_main_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreationMugshotEditorProjector.Project(Xml(created: true, mainIndex: 0), WorkspaceId, 19));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreationMugshotEditorProjector.Project(Xml(created: false, mainIndex: 2), WorkspaceId, 19));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CreationMugshotEditorProjector.Project(
                Xml(created: false, mainIndex: 0).Replace("AQIDBA==", "not-base64", StringComparison.Ordinal),
                WorkspaceId,
                19));

        string source = Xml(created: false, mainIndex: 0);
        CreationMugshotEditorState editor = CreationMugshotEditorProjector.Project(source, WorkspaceId, 19);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCreationMugshotMainEdit(
                source,
                new CreationMugshotMainEditRequest(
                    WorkspaceId,
                    19,
                    editor.Items[1].Identity,
                    new string('0', 64),
                    IsMain: true)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCreationMugshotMainEdit(
                source,
                new CreationMugshotMainEditRequest(
                    WorkspaceId,
                    19,
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
          <nuyen>3141</nuyen><karma>27</karma><customstate>Creation mugshot sentinel</customstate>
        </character>
        """;
}
