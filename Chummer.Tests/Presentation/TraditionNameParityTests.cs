using System.Xml.Linq;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class TraditionNameParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("tradition-name-tests");
    private static readonly Guid TraditionId = Guid.Parse("8b3e871a-b308-4280-8672-11d7d4ea40a3");

    [TestMethod]
    public void ProjectAndApply_PreserveExactTextAndUnrelatedNodes()
    {
        string xml = $"""
            <character><created>False</created><tradition><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><guid>{TraditionId:D}</guid><traditiontype>MAG</traditiontype><name>Old Path</name><extra><name>Nested</name></extra></tradition></character>
            """;
        TraditionNameEditorState editor = TraditionNameEditorProjector.Project(xml, WorkspaceId, 4);

        XElement tradition = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyTraditionNameEdit(
            xml,
            new TraditionNameEditRequest(
                WorkspaceId,
                4,
                editor.TraditionId,
                editor.TraditionName,
                "  Vienna Hermetic  "))).Root!.Element("tradition")!;

        Assert.AreEqual("  Vienna Hermetic  ", tradition.Element("name")!.Value);
        Assert.AreEqual("Nested", tradition.Element("extra")!.Element("name")!.Value);
    }

    [TestMethod]
    public void Project_FailsClosedForNonCustomMissingIdentityAndDuplicateTraditions()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionNameEditorProjector.Project(
            "<character><tradition><sourceid>19320625-bc1a-492f-8904-da6a847e5700</sourceid><guid>8b3e871a-b308-4280-8672-11d7d4ea40a3</guid><traditiontype>MAG</traditiontype><name>Hermetic</name></tradition></character>",
            WorkspaceId,
            3));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionNameEditorProjector.Project(
            "<character><tradition><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><traditiontype>MAG</traditiontype><name>Custom</name></tradition></character>",
            WorkspaceId,
            3));
        Assert.ThrowsExactly<InvalidOperationException>(() => TraditionNameEditorProjector.Project(
            "<character><tradition /><tradition /></character>",
            WorkspaceId,
            3));
    }

    [TestMethod]
    public void Apply_FailsClosedForIdentityNameDriftAndMultilineValues()
    {
        string xml = $"""
            <character><tradition><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><guid>{TraditionId:D}</guid><traditiontype>MAG</traditiontype><name>Current</name></tradition></character>
            """;
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyTraditionNameEdit(
            xml,
            new TraditionNameEditRequest(WorkspaceId, 3, Guid.NewGuid(), "Current", "Next")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyTraditionNameEdit(
            xml,
            new TraditionNameEditRequest(WorkspaceId, 3, TraditionId, "Stale", "Next")));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyTraditionNameEdit(
            xml,
            new TraditionNameEditRequest(WorkspaceId, 3, TraditionId, "Current", "one\ntwo")));
    }
}
