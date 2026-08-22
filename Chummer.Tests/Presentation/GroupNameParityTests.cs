using System.Xml.Linq;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GroupNameParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("group-name-tests");

    [TestMethod]
    public void ProjectAndApply_PreserveExactTextAndUnrelatedNodes()
    {
        const string xml = "<character><created>False</created><groupname>Old Circle</groupname><customstate><groupname>Nested</groupname></customstate></character>";
        GroupNameEditorState editor = GroupNameEditorProjector.Project(xml, WorkspaceId, 4);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyGroupNameEdit(
            xml,
            new GroupNameEditRequest(WorkspaceId, 4, editor.GroupName, "  New Circle  "))).Root!;

        Assert.AreEqual("  New Circle  ", root.Element("groupname")!.Value);
        Assert.AreEqual("Nested", root.Element("customstate")!.Element("groupname")!.Value);
    }

    [TestMethod]
    public void Apply_FailsClosedForDriftDuplicateAndMultilineValues()
    {
        const string xml = "<character><groupname>Current</groupname></character>";
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGroupNameEdit(
            xml,
            new GroupNameEditRequest(WorkspaceId, 3, "Stale", "Next")));
        Assert.ThrowsExactly<InvalidOperationException>(() => GroupNameEditorProjector.Project(
            "<character><groupname>A</groupname><groupname>B</groupname></character>", WorkspaceId, 3));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGroupNameEdit(
            xml,
            new GroupNameEditRequest(WorkspaceId, 3, "Current", "one\ntwo")));
    }
}
