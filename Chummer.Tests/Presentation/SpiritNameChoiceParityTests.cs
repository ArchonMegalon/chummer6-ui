using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class SpiritNameChoiceParityTests
{
    private static readonly Guid SpiritId = Guid.Parse("84111111-8411-8411-8411-841111111111");
    private static readonly CharacterWorkspaceId WorkspaceId = new("spirit-name-choice-tests");

    [TestMethod]
    public void ProjectorRequiresTypedIdentityAndStrictAllowedNames()
    {
        JsonObject semantics = new()
        {
            ["spiritId"] = SpiritId.ToString("D"),
            ["entityType"] = "Spirit",
            ["currentName"] = "Spirit of Fire",
            ["allowedNames"] = new JsonArray("Spirit of Fire", "Spirit of Water")
        };
        JsonObject section = new()
        {
            ["spirits"] = new JsonArray(new JsonObject
            {
                ["guid"] = SpiritId.ToString("D"),
                ["name"] = "Spirit of Fire",
                ["nameChoiceSemantics"] = semantics
            })
        };

        CharacterSpiritNameChoiceState? state = WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single()
            .SpiritNameChoice;
        Assert.IsNotNull(state);
        CollectionAssert.AreEqual(
            new[] { "Spirit of Fire", "Spirit of Water" },
            state.AllowedNames.ToArray());

        semantics["spiritId"] = Guid.NewGuid().ToString("D");
        Assert.IsNull(WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single()
            .SpiritNameChoice);
    }

    [TestMethod]
    public void MutationChangesOnlyTheDirectSpiritName()
    {
        const string xml = """
<character>
  <created>True</created><magenabled>True</magenabled><resenabled>False</resenabled>
  <tradition><guid>84222222-8422-8422-8422-842222222222</guid><traditiontype>MAG</traditiontype><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><name>Custom</name><spiritcombat>Spirit of Fire</spiritcombat><spiritdetection>Spirit of Water</spiritdetection><spirithealth>Spirit of Earth</spirithealth><spiritillusion>Spirit of Man</spiritillusion><spiritmanipulation>Spirit of Air</spiritmanipulation><spirits /></tradition>
  <improvements />
  <spirits><spirit><guid>84111111-8411-8411-8411-841111111111</guid><name>Spirit of Fire</name><type>Spirit</type><crittername>Ash</crittername><force>4</force><services>2</services><bound>True</bound><fettered>False</fettered><notes>preserve me</notes></spirit></spirits>
</character>
""";
        CharacterSpiritNameChoiceState expected = new(
            SpiritId,
            "Spirit",
            "Spirit of Fire",
            ["Spirit of Fire", "Spirit of Water", "Spirit of Earth", "Spirit of Man", "Spirit of Air"]);

        XElement spirit = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplySpiritNameChoiceEdit(
                xml,
                new SpiritNameChoiceEditRequest(
                    WorkspaceId,
                    12,
                    expected,
                    "Spirit of Water")))
            .Root!
            .Element("spirits")!
            .Element("spirit")!;

        Assert.AreEqual("Spirit of Water", spirit.Element("name")!.Value);
        Assert.AreEqual("Ash", spirit.Element("crittername")!.Value);
        Assert.AreEqual("4", spirit.Element("force")!.Value);
        Assert.AreEqual("2", spirit.Element("services")!.Value);
        Assert.AreEqual("True", spirit.Element("bound")!.Value);
        Assert.AreEqual("False", spirit.Element("fettered")!.Value);
        Assert.AreEqual("preserve me", spirit.Element("notes")!.Value);
    }

    [TestMethod]
    public void MutationRejectsStaleChoiceSetBeforeTouchingXml()
    {
        const string xml = """
<character><magenabled>True</magenabled><resenabled>False</resenabled><tradition><guid>84222222-8422-8422-8422-842222222222</guid><traditiontype>MAG</traditiontype><sourceid>616ba093-306c-45fc-8f41-0b98c8cccb46</sourceid><spiritcombat>Spirit of Fire</spiritcombat><spiritdetection>Spirit of Water</spiritdetection><spirits /></tradition><improvements /><spirits><spirit><guid>84111111-8411-8411-8411-841111111111</guid><name>Spirit of Fire</name><type>Spirit</type></spirit></spirits></character>
""";
        CharacterSpiritNameChoiceState stale = new(
            SpiritId,
            "Spirit",
            "Spirit of Fire",
            ["Spirit of Fire"]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySpiritNameChoiceEdit(
                xml,
                new SpiritNameChoiceEditRequest(
                    WorkspaceId,
                    13,
                    stale,
                    "Spirit of Fire")));
    }
}
