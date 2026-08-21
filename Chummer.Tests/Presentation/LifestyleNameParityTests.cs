using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class LifestyleNameParityTests
{
    private const string LifestyleId = "11111111-1111-1111-1111-111111111111";

    [TestMethod]
    public void Projector_ExposesExactOptionalLifestyleCustomNameAlongsideCoupledNotes()
    {
        JsonObject section = new()
        {
            ["lifestyles"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = LifestyleId,
                    ["name"] = "Low",
                    ["customName"] = "Safehouse",
                    ["notes"] = "Preserved notes",
                    ["notesColor"] = "#123456"
                }
            }
        };

        WorkspaceCollectionEditorState editor = WorkspaceCollectionEditorProjector.TryProject("lifestyles", section)!;
        WorkspaceCollectionItemEditorState item = editor.Items.Single();
        WorkspaceCollectionTextValueState value = item.TextValues.Single(candidate =>
            candidate.Field == WorkspaceCollectionTextField.CustomName);

        Assert.AreEqual(WorkspaceCollectionKind.Lifestyle, editor.Kind);
        Assert.AreEqual(WorkspaceCollectionTextField.CustomName, value.Field);
        Assert.AreEqual("Safehouse", value.Value);
        Assert.AreEqual("Safehouse", item.Label);
        Assert.AreEqual(32_767, value.MaximumLength);
        Assert.IsFalse(value.IsRequired);
        Assert.IsFalse(item.CanDelete);
        Assert.IsFalse(item.CanMove);
    }

    [TestMethod]
    public void Projector_ExposesCoupledLifestyleNotesAndColorWithExactBounds()
    {
        JsonObject section = new()
        {
            ["lifestyles"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = LifestyleId,
                    ["name"] = "Low",
                    ["customName"] = "Safehouse",
                    ["notes"] = "Preserved notes",
                    ["notesColor"] = "#123456"
                }
            }
        };

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("lifestyles", section)!
            .Items.Single();
        WorkspaceCollectionTextValueState notes = item.TextValues.Single(candidate =>
            candidate.Field == WorkspaceCollectionTextField.Notes);
        WorkspaceCollectionTextValueState color = item.TextValues.Single(candidate =>
            candidate.Field == WorkspaceCollectionTextField.NotesColor);

        Assert.HasCount(3, item.TextValues);
        Assert.AreEqual("Preserved notes", notes.Value);
        Assert.AreEqual(int.MaxValue, notes.MaximumLength);
        Assert.AreEqual("#123456", color.Value);
        Assert.AreEqual(32, color.MaximumLength);
    }

    [TestMethod]
    public void Mutation_ChangesOnlyStableLifestyleExtraAndAllowsBlank()
    {
        const string xml = """
            <character><lifestyles><lifestyle>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Low</name><extra>Safehouse</extra><notes>Preserved notes</notes>
              <cost>2000</cost>
            </lifestyle></lifestyles><alias>Untouched</alias></character>
            """;
        var target = new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Lifestyle, LifestyleId);

        string renamed = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspaceSetCollectionTextRequest(target, WorkspaceCollectionTextField.CustomName, "Bolt-hole"));
        string cleared = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            renamed,
            new WorkspaceSetCollectionTextRequest(target, WorkspaceCollectionTextField.CustomName, string.Empty));
        XElement root = XDocument.Parse(cleared).Root!;
        XElement lifestyle = root.Element("lifestyles")!.Element("lifestyle")!;

        Assert.AreEqual(string.Empty, lifestyle.Element("extra")!.Value);
        Assert.AreEqual("Low", lifestyle.Element("name")!.Value);
        Assert.AreEqual("Preserved notes", lifestyle.Element("notes")!.Value);
        Assert.AreEqual("2000", lifestyle.Element("cost")!.Value);
        Assert.AreEqual("Untouched", root.Element("alias")!.Value);
    }

    [TestMethod]
    public void Mutation_RejectsDuplicateIdentityAndLegacyLengthOverflow()
    {
        string duplicate = $"""
            <character><lifestyles>
              <lifestyle><guid>{LifestyleId}</guid><name>Low</name><extra /></lifestyle>
              <lifestyle><guid>{LifestyleId}</guid><name>High</name><extra /></lifestyle>
            </lifestyles></character>
            """;
        var target = new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Lifestyle, LifestyleId);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                duplicate,
                new WorkspaceSetCollectionTextRequest(target, WorkspaceCollectionTextField.CustomName, "Ambiguous")));

        string unique = $"""
            <character><lifestyles><lifestyle><guid>{LifestyleId}</guid><name>Low</name><extra /></lifestyle></lifestyles></character>
            """;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                unique,
                new WorkspaceSetCollectionTextRequest(
                    target,
                    WorkspaceCollectionTextField.CustomName,
                    new string('x', 32_768))));
    }

    [TestMethod]
    public void Mutation_AtomicallyChangesNotesAndColorAndPreservesLifestyleName()
    {
        const string xml = """
            <character><lifestyles><lifestyle>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Low</name><extra>Safehouse</extra>
              <notes>Old notes</notes><notesColor>#123456</notesColor><cost>2000</cost>
            </lifestyle></lifestyles><alias>Untouched</alias></character>
            """;
        var target = new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Lifestyle, LifestyleId);

        string mutated = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            xml,
            new WorkspacePatchCollectionItemRequest(
                target,
                TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                {
                    [WorkspaceCollectionTextField.Notes] = "New notes\nSecond line",
                    [WorkspaceCollectionTextField.NotesColor] = "#abcdef"
                }));
        XElement root = XDocument.Parse(mutated).Root!;
        XElement lifestyle = root.Element("lifestyles")!.Element("lifestyle")!;

        Assert.AreEqual("New notes\nSecond line", lifestyle.Element("notes")!.Value);
        Assert.AreEqual("#ABCDEF", lifestyle.Element("notesColor")!.Value);
        Assert.AreEqual("Safehouse", lifestyle.Element("extra")!.Value);
        Assert.AreEqual("Low", lifestyle.Element("name")!.Value);
        Assert.AreEqual("2000", lifestyle.Element("cost")!.Value);
        Assert.AreEqual("Untouched", root.Element("alias")!.Value);

        string namedColor = WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
            mutated,
            new WorkspaceSetCollectionTextRequest(
                target,
                WorkspaceCollectionTextField.NotesColor,
                "red"));
        Assert.AreEqual(
            "Red",
            XDocument.Parse(namedColor).Descendants("lifestyle").Single().Element("notesColor")!.Value);
    }

    [TestMethod]
    public void Mutation_RejectsInvalidLifestyleNotesColorBeforeReturningAnyPatch()
    {
        const string xml = """
            <character><lifestyles><lifestyle>
              <guid>11111111-1111-1111-1111-111111111111</guid>
              <name>Low</name><extra>Safehouse</extra>
              <notes>Old notes</notes><notesColor>#123456</notesColor>
            </lifestyle></lifestyles></character>
            """;
        var target = new WorkspaceCollectionItemTarget(WorkspaceCollectionKind.Lifestyle, LifestyleId);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspacePatchCollectionItemRequest(
                    target,
                    TextValues: new Dictionary<WorkspaceCollectionTextField, string?>
                    {
                        [WorkspaceCollectionTextField.Notes] = "Must not partially escape",
                        [WorkspaceCollectionTextField.NotesColor] = "not-a-real-color"
                    })));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCollectionMutation(
                xml,
                new WorkspaceSetCollectionTextRequest(
                    target,
                    WorkspaceCollectionTextField.NotesColor,
                    "1")));
    }
}
