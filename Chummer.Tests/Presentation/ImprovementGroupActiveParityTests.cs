using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ImprovementGroupActiveParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("improvement-group-active-tests");

    [TestMethod]
    public void Career_projects_ungrouped_and_exact_named_custom_groups_only()
    {
        ImprovementGroupActiveEditorState editor = ImprovementGroupActiveEditorProjector.Project(
            CareerXml(), WorkspaceId, 11);

        Assert.AreEqual(3, editor.Groups.Count);
        CharacterImprovementGroupActiveState ungrouped = editor.Groups.Single(group =>
            group.Identity.Kind == CharacterImprovementGroupKind.Ungrouped);
        CharacterImprovementGroupActiveState alpha = editor.Groups.Single(group =>
            group.Identity.Name == "Alpha");
        CharacterImprovementGroupActiveState beta = editor.Groups.Single(group =>
            group.Identity.Name == "Beta");
        Assert.AreEqual(2, ungrouped.Members.Count);
        Assert.AreEqual(2, alpha.Members.Count);
        Assert.AreEqual(1, beta.Members.Count);
        Assert.AreEqual(1, alpha.EnabledCount);
        Assert.AreEqual(1, alpha.DisabledCount);
    }

    [TestMethod]
    public void Enable_then_disable_named_group_changes_only_opposite_custom_members()
    {
        string source = CareerXml();
        CharacterImprovementGroupActiveState alpha = ImprovementGroupActiveEditorProjector
            .Project(source, WorkspaceId, 11)
            .Groups.Single(group => group.Identity.Name == "Alpha");
        string enabled = WorkspaceXmlMutationCatalog.ApplyImprovementGroupActiveEdit(
            source,
            new ImprovementGroupActiveEditRequest(
                WorkspaceId, 11, alpha.Identity, alpha.Revision, Enabled: true));
        XDocument enabledDocument = XDocument.Parse(enabled, LoadOptions.PreserveWhitespace);
        XElement[] alphaNodes = enabledDocument.Root!.Element("improvements")!.Elements("improvement")
            .Where(node => node.Element("customgroup")?.Value == "Alpha")
            .ToArray();
        Assert.AreEqual("1", alphaNodes[0].Element("enabled")!.Value);
        Assert.AreEqual("1", alphaNodes[1].Element("enabled")!.Value);
        Assert.AreEqual("True", alphaNodes[2].Element("enabled")!.Value);

        CharacterImprovementGroupActiveState refreshed = ImprovementGroupActiveEditorProjector
            .Project(enabled, WorkspaceId, 12)
            .Groups.Single(group => group.Identity.Name == "Alpha");
        string disabled = WorkspaceXmlMutationCatalog.ApplyImprovementGroupActiveEdit(
            enabled,
            new ImprovementGroupActiveEditRequest(
                WorkspaceId, 12, refreshed.Identity, refreshed.Revision, Enabled: false));
        XDocument disabledDocument = XDocument.Parse(disabled, LoadOptions.PreserveWhitespace);
        XElement[] finalAlpha = disabledDocument.Root!.Element("improvements")!.Elements("improvement")
            .Where(node => node.Element("customgroup")?.Value == "Alpha")
            .ToArray();
        Assert.AreEqual("0", finalAlpha[0].Element("enabled")!.Value);
        Assert.AreEqual("0", finalAlpha[1].Element("enabled")!.Value);
        Assert.AreEqual("True", finalAlpha[2].Element("enabled")!.Value);
        Assert.AreEqual("Beta sentinel", disabledDocument.Root!.Element("improvements")!
            .Elements("improvement").Single(node => node.Element("customgroup")?.Value == "Beta")
            .Element("notes")!.Value);
        Assert.AreEqual("Runner sentinel", disabledDocument.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Ungrouped_legacy_root_changes_only_custom_members_with_empty_group()
    {
        CharacterImprovementGroupActiveState ungrouped = ImprovementGroupActiveEditorProjector
            .Project(CareerXml(), WorkspaceId, 11)
            .Groups.Single(group => group.Identity.Kind == CharacterImprovementGroupKind.Ungrouped);
        string enabled = WorkspaceXmlMutationCatalog.ApplyImprovementGroupActiveEdit(
            CareerXml(),
            new ImprovementGroupActiveEditRequest(
                WorkspaceId, 11, ungrouped.Identity, ungrouped.Revision, Enabled: true));
        XDocument document = XDocument.Parse(enabled, LoadOptions.PreserveWhitespace);
        XElement[] improvements = document.Root!.Element("improvements")!.Elements("improvement").ToArray();
        Assert.AreEqual("1", improvements.Single(node => node.Element("improvedname")?.Value == "WIL").Element("enabled")!.Value);
        Assert.AreEqual("1", improvements.Single(node => node.Element("improvedname")?.Value == "CHA").Element("enabled")!.Value);
        Assert.AreEqual("0", improvements.Single(node => node.Element("improvedname")?.Value == "AGI").Element("enabled")!.Value);
        Assert.AreEqual("Beta sentinel", improvements.Single(node => node.Element("improvedname")?.Value == "REA").Element("notes")!.Value);
    }

    [TestMethod]
    public void Creation_duplicate_reserved_or_orphan_group_and_stale_or_noop_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupActiveEditorProjector.Project(
            CareerXml().Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            WorkspaceId,
            11));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupActiveEditorProjector.Project(
            CareerXml().Replace("<improvementgroup>Beta</improvementgroup>", "<improvementgroup>Alpha</improvementgroup>", StringComparison.Ordinal),
            WorkspaceId,
            11));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupActiveEditorProjector.Project(
            CareerXml().Replace("<customgroup>Beta</customgroup>", "<customgroup>Missing</customgroup>", StringComparison.Ordinal),
            WorkspaceId,
            11));
        const string duplicateMember = "<improvement><improvedname>BOD</improvedname><sourcename>a6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup>Alpha</customgroup><enabled>0</enabled></improvement>";
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupActiveEditorProjector.Project(
            CareerXml().Replace("</improvements>", duplicateMember + "</improvements>", StringComparison.Ordinal),
            WorkspaceId,
            11));

        CharacterImprovementGroupActiveState alpha = ImprovementGroupActiveEditorProjector
            .Project(CareerXml(), WorkspaceId, 11)
            .Groups.Single(group => group.Identity.Name == "Alpha");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementGroupActiveEdit(
                CareerXml(),
                new ImprovementGroupActiveEditRequest(
                    WorkspaceId, 11, alpha.Identity, new string('0', 64), true)));
        CharacterImprovementGroupActiveState beta = ImprovementGroupActiveEditorProjector
            .Project(CareerXml(), WorkspaceId, 11)
            .Groups.Single(group => group.Identity.Name == "Beta");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementGroupActiveEdit(
                CareerXml(),
                new ImprovementGroupActiveEditRequest(
                    WorkspaceId, 11, beta.Identity, beta.Revision, true)));
    }

    private static string CareerXml() => """
<character>
  <created>True</created>
  <improvementgroups><improvementgroup>Alpha</improvementgroup><improvementgroup>Beta</improvementgroup></improvementgroups>
  <improvements>
    <improvement><improvedname>BOD</improvedname><sourcename>a6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup>Alpha</customgroup><enabled>1</enabled></improvement>
    <improvement><improvedname>AGI</improvedname><sourcename>a6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup>Alpha</customgroup><enabled>0</enabled></improvement>
    <improvement><improvedname>LOG</improvedname><sourcename>n6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Quality</improvementsource><custom>False</custom><customgroup>Alpha</customgroup><enabled>True</enabled><notes>Non-custom sentinel</notes></improvement>
    <improvement><improvedname>REA</improvedname><sourcename>b6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup>Beta</customgroup><enabled>1</enabled><notes>Beta sentinel</notes></improvement>
    <improvement><improvedname>WIL</improvedname><sourcename>c6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup></customgroup><enabled>1</enabled></improvement>
    <improvement><improvedname>CHA</improvedname><sourcename>d6111111-6111-6111-6111-611111111111</sourcename><improvementttype>Attribute</improvementttype><improvementsource>Custom</improvementsource><custom>True</custom><customgroup></customgroup><enabled>0</enabled></improvement>
  </improvements>
  <customstate>Runner sentinel</customstate>
</character>
""";
}
