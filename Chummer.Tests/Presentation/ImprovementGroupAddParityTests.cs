using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class ImprovementGroupAddParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("improvement-group-add-tests");

    [TestMethod]
    public void Career_projects_exact_order_duplicates_and_zero_economics()
    {
        ImprovementGroupAddEditorState editor = ImprovementGroupAddEditorProjector.Project(
            CareerXml(), WorkspaceId, 17);

        CollectionAssert.AreEqual(
            new[] { "Alpha", "Alpha", " Beta " },
            editor.Collection.Groups.ToArray());
        Assert.AreEqual(0, editor.Collection.Economics.KarmaDelta);
        Assert.AreEqual(0m, editor.Collection.Economics.NuyenDelta);
    }

    [TestMethod]
    public void Exact_untrimmed_duplicate_name_appends_only_one_group()
    {
        string source = CareerXml();
        ImprovementGroupAddEditorState editor = ImprovementGroupAddEditorProjector.Project(
            source, WorkspaceId, 17);
        Assert.IsTrue(CharacterImprovementGroupAddRules.TryCreateIdentity(
            editor.Collection,
            "Alpha",
            out CharacterImprovementGroupInsertionIdentity identity));

        string mutated = WorkspaceXmlMutationCatalog.ApplyImprovementGroupAdd(
            source,
            new ImprovementGroupAddRequest(
                WorkspaceId,
                17,
                identity,
                editor.Collection.Revision));
        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        string[] groups = document.Root!.Element("improvementgroups")!
            .Elements("improvementgroup")
            .Select(group => group.Value)
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { "Alpha", "Alpha", " Beta ", "Alpha" },
            groups);
        Assert.AreEqual(
            source.Substring(source.IndexOf("<improvements>", StringComparison.Ordinal)),
            mutated.Substring(mutated.IndexOf("<improvements>", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Creation_empty_name_stale_identity_revision_and_duplicate_container_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupAddEditorProjector.Project(
            CareerXml().Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal),
            WorkspaceId,
            17));
        Assert.ThrowsExactly<InvalidOperationException>(() => ImprovementGroupAddEditorProjector.Project(
            CareerXml().Replace("<improvements>", "<improvementgroups/><improvements>", StringComparison.Ordinal),
            WorkspaceId,
            17));

        ImprovementGroupAddEditorState editor = ImprovementGroupAddEditorProjector.Project(
            CareerXml(), WorkspaceId, 17);
        Assert.IsFalse(CharacterImprovementGroupAddRules.TryCreateIdentity(
            editor.Collection,
            string.Empty,
            out _));
        Assert.IsTrue(CharacterImprovementGroupAddRules.TryCreateIdentity(
            editor.Collection,
            "Gamma",
            out CharacterImprovementGroupInsertionIdentity identity));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementGroupAdd(
                CareerXml(),
                new ImprovementGroupAddRequest(
                    WorkspaceId,
                    17,
                    identity with { ExpectedAppendIndex = identity.ExpectedAppendIndex - 1 },
                    editor.Collection.Revision)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyImprovementGroupAdd(
                CareerXml(),
                new ImprovementGroupAddRequest(
                    WorkspaceId,
                    17,
                    identity,
                    new string('0', 64))));
    }

    private static string CareerXml() => """
<character>
  <created>True</created>
  <improvementgroups><improvementgroup>Alpha</improvementgroup><improvementgroup>Alpha</improvementgroup><improvementgroup> Beta </improvementgroup></improvementgroups>
  <improvements><improvement><customgroup>Alpha</customgroup><enabled>0</enabled><notes>untouched</notes></improvement></improvements>
  <customstate>Runner sentinel</customstate>
</character>
""";
}
