using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CreationLifestyleDeleteParityTests
{
    private static readonly Guid WorkspaceGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TargetId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetQualityId = Guid.Parse("21111111-1111-1111-1111-111111111111");
    private static readonly Guid KeepId = Guid.Parse("12222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void ConfirmedCreationDeleteRemovesExactLifestyleAndQualityImprovementsOnly()
    {
        string xml = Fixture(created: false);
        CreationLifestyleDeleteEditorState editor = CreationLifestyleDeleteEditorProjector.Project(
            xml,
            new CharacterWorkspaceId(WorkspaceGuid.ToString("D")),
            contentRevision: 7);
        CharacterCreationLifestyleDeleteState selected = editor.Lifestyles.Single(
            lifestyle => lifestyle.Identity.LifestyleId == TargetId);

        string updated = WorkspaceXmlMutationCatalog.ApplyCreationLifestyleDelete(
            xml,
            new CreationLifestyleDeleteRequest(
                editor.WorkspaceId,
                editor.ContentRevision,
                selected.Identity,
                selected.Revision,
                Confirmed: true));

        XDocument document = XDocument.Parse(updated);
        XElement root = document.Root!;
        Assert.AreEqual(
            new[] { KeepId.ToString("D") },
            root.Element("lifestyles")!.Elements("lifestyle")
                .Select(item => item.Element("guid")!.Value)
                .ToArray());
        Assert.AreEqual(
            new[] { "keep-quality", "keep-custom" },
            root.Element("improvements")!.Elements("improvement")
                .Select(item => item.Element("marker")!.Value)
                .ToArray());
        Assert.AreEqual("9000", root.Element("nuyen")!.Value);
        Assert.AreEqual("keep-expense", root.Element("expenses")!.Element("expense")!.Element("reason")!.Value);
        Assert.AreEqual("unrelated sentinel", root.Element("customstate")!.Value);
        Assert.IsFalse(updated.Contains("target raw cost sentinel", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CancelCareerAndTargetRevisionDriftAreZeroMutationFailures()
    {
        string creationXml = Fixture(created: false);
        CreationLifestyleDeleteEditorState editor = CreationLifestyleDeleteEditorProjector.Project(
            creationXml,
            new CharacterWorkspaceId(WorkspaceGuid.ToString("D")),
            8);
        CharacterCreationLifestyleDeleteState selected = editor.Lifestyles[0];

        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCreationLifestyleDelete(
                creationXml,
                new CreationLifestyleDeleteRequest(
                    editor.WorkspaceId,
                    editor.ContentRevision,
                    selected.Identity,
                    selected.Revision,
                    Confirmed: false)));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCreationLifestyleDelete(
                creationXml,
                new CreationLifestyleDeleteRequest(
                    editor.WorkspaceId,
                    editor.ContentRevision,
                    selected.Identity,
                    new string('0', CharacterCreationLifestyleDeleteRules.RevisionHexLength),
                    Confirmed: true)));

        string careerXml = Fixture(created: true);
        CreationLifestyleDeleteEditorState career = CreationLifestyleDeleteEditorProjector.Project(
            careerXml,
            editor.WorkspaceId,
            9);
        Assert.IsFalse(career.Lifestyles[0].CanDelete);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCreationLifestyleDelete(
                careerXml,
                new CreationLifestyleDeleteRequest(
                    career.WorkspaceId,
                    career.ContentRevision,
                    career.Lifestyles[0].Identity,
                    career.Lifestyles[0].Revision,
                    Confirmed: true)));
    }

    [TestMethod]
    public void DuplicateIdentityOrMalformedCascadeAuthorityFailsClosed()
    {
        string duplicate = Fixture(created: false).Replace(
            KeepId.ToString("D"),
            TargetId.ToString("D"),
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() =>
            CreationLifestyleDeleteEditorProjector.Project(
                duplicate,
                new CharacterWorkspaceId(WorkspaceGuid.ToString("D")),
                10));

        string malformed = Fixture(created: false).Replace(
            "<improvementsource>Quality</improvementsource><sourcename>",
            "<improvementsource>Quality</improvementsource><improvementsource>Quality</improvementsource><sourcename>",
            StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() =>
            CreationLifestyleDeleteEditorProjector.Project(
                malformed,
                new CharacterWorkspaceId(WorkspaceGuid.ToString("D")),
                11));

        string persistedCascade = Fixture(created: false).Replace(
            "<improvementttype>Attribute</improvementttype><improvedname>BOD</improvedname>",
            "<improvementttype>Gear</improvementttype><improvedname>granted-gear</improvedname>",
            StringComparison.Ordinal);
        InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(() =>
            CreationLifestyleDeleteEditorProjector.Project(
                persistedCascade,
                new CharacterWorkspaceId(WorkspaceGuid.ToString("D")),
                12));
        StringAssert.Contains(error.Message, "ImprovementManager persisted-object cascade");
    }

    private static string Fixture(bool created)
        => $"""
            <character>
              <created>{created}</created><nuyen>9000</nuyen>
              <expenses><expense><reason>keep-expense</reason></expense></expenses>
              <lifestyles>
                <lifestyle>
                  <guid>{TargetId:D}</guid><name>Low</name><extra>Target Home</extra>
                  <cost>2000</cost><percentage>100</percentage><notes>target raw cost sentinel</notes>
                  <lifestylequalities><lifestylequality><guid>{TargetQualityId:D}</guid><name>Grid Subscription</name></lifestylequality></lifestylequalities>
                </lifestyle>
                <lifestyle>
                  <guid>{KeepId:D}</guid><name>Medium</name><extra>Keep Home</extra><cost>5000</cost>
                  <lifestylequalities />
                </lifestyle>
              </lifestyles>
              <improvements>
                <improvement><improvementsource>Quality</improvementsource><sourcename>{TargetQualityId:D}</sourcename><marker>remove-exact</marker></improvement>
                <improvement><improvementsource>Quality</improvementsource><sourcename>{TargetQualityId:D} selected value</sourcename><marker>remove-legacy-prefix</marker></improvement>
                <improvement><improvementsource>Quality</improvementsource><sourcename>29999999-9999-9999-9999-999999999999</sourcename><marker>keep-quality</marker></improvement>
                <improvement><improvementsource>Custom</improvementsource><sourcename>{TargetQualityId:D}</sourcename><marker>keep-custom</marker></improvement>
              </improvements>
              <customstate>unrelated sentinel</customstate>
            </character>
            """;
}
