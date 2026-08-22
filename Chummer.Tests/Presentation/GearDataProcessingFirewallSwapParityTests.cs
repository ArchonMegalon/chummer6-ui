using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearDataProcessingFirewallSwapParityTests
{
    private static readonly Guid RootId = Guid.Parse("a1111111-2222-4333-8444-555555555555");
    private static readonly Guid TargetId = Guid.Parse("b1111111-2222-4333-8444-555555555555");

    [TestMethod]
    public void DataProcessingSwapPreservesRawProvenanceEconomicsAndNotificationConsumers()
    {
        string xml = Fixture(created: false);
        CharacterGearMatrixSwapState state = GearDataProcessingFirewallSwapEditorProjector
            .ProjectValue(xml, RootId).Single();
        Assert.AreEqual(CharacterGearMatrixSwapPhase.Creation, state.Phase);
        CollectionAssert.AreEqual(new[] { RootId, TargetId }, state.Identity.GearPath.ToArray());
        Assert.IsTrue(CharacterGearMatrixSwapRules.RequiresMatrixInitiativeNotification(
            CharacterGearMatrixStat.DataProcessing, CharacterGearMatrixStat.Attack));

        string changed = WorkspaceXmlMutationCatalog.ApplyGearDataProcessingFirewallSwapEdit(
            xml,
            new GearDataProcessingFirewallSwapEditRequest(
                new CharacterWorkspaceId("runner"),
                7,
                state.Identity,
                state.Revision,
                CharacterGearMatrixStat.DataProcessing,
                CharacterGearMatrixStat.Attack));

        XElement before = Target(xml);
        XElement after = Target(changed);
        Assert.AreEqual("7", after.Element("dataprocessing")!.Value);
        Assert.AreEqual("5", after.Element("attack")!.Value);
        AssertPreserved(before, after, "sleaze", "firewall", "attributearray", "canswapattributes",
            "modattack", "modsleaze", "moddataprocessing", "modfirewall", "active", "homenode",
            "equipped", "stolen", "cost", "notes");
        Assert.AreEqual("4321", XDocument.Parse(changed).Root!.Element("nuyen")!.Value);
        Assert.AreEqual("7", XDocument.Parse(changed).Root!.Element("karma")!.Value);
    }

    [TestMethod]
    public void FirewallSwapWithDataProcessingIsCareerTypedAndRejectsStaleOrWrongChangedStat()
    {
        string xml = Fixture(created: true);
        CharacterGearMatrixSwapState state = GearDataProcessingFirewallSwapEditorProjector
            .ProjectValue(xml, RootId).Single();
        Assert.AreEqual(CharacterGearMatrixSwapPhase.Career, state.Phase);

        var request = new GearDataProcessingFirewallSwapEditRequest(
            new CharacterWorkspaceId("runner"),
            9,
            state.Identity,
            state.Revision,
            CharacterGearMatrixStat.Firewall,
            CharacterGearMatrixStat.DataProcessing);
        string changed = WorkspaceXmlMutationCatalog.ApplyGearDataProcessingFirewallSwapEdit(xml, request);
        Assert.AreEqual("5", Target(changed).Element("firewall")!.Value);
        Assert.AreEqual("4", Target(changed).Element("dataprocessing")!.Value);
        Assert.AreEqual("True", Target(changed).Element("active")!.Value);
        Assert.AreEqual("True", Target(changed).Element("homenode")!.Value);

        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearDataProcessingFirewallSwapEdit(
                xml, request with { ExpectedNodeRevision = new string('0', 64) }));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyGearDataProcessingFirewallSwapEdit(
                xml, request with { ChangedAttribute = CharacterGearMatrixStat.Attack }));
    }

    [TestMethod]
    public void DuplicateRecursiveGearIdentityFailsClosed()
    {
        string xml = Fixture(created: false);
        string duplicate = Target(xml).ToString(SaveOptions.DisableFormatting);
        string malformed = xml.Replace("</children>", $"{duplicate}</children>", StringComparison.Ordinal);
        Assert.ThrowsException<InvalidOperationException>(() =>
            GearDataProcessingFirewallSwapEditorProjector.ProjectValue(malformed, RootId));
    }

    private static string Fixture(bool created) =>
        $$"""<character><created>{{created}}</created><nuyen>4321</nuyen><karma>7</karma><gears><gear><guid>{{RootId:D}}</guid><name>Root</name><category>Commlinks</category><attack>0</attack><sleaze>0</sleaze><dataprocessing>3</dataprocessing><firewall>3</firewall><canswapattributes>False</canswapattributes><children><gear><guid>{{TargetId:D}}</guid><name>Deck</name><category>Cyberdecks</category><attack>7</attack><sleaze>{Rating}</sleaze><dataprocessing>5</dataprocessing><firewall>4</firewall><attributearray>7,6,5,4</attributearray><canswapattributes>True</canswapattributes><modattack>2</modattack><modsleaze>3</modsleaze><moddataprocessing>9</moddataprocessing><modfirewall>5</modfirewall><active>True</active><homenode>True</homenode><equipped>True</equipped><stolen>False</stolen><cost>12345</cost><notes>sentinel</notes></gear></children></gear></gears></character>""";

    private static XElement Target(string xml)
        => XDocument.Parse(xml).Root!.Element("gears")!.Element("gear")!
            .Element("children")!.Element("gear")!;

    private static void AssertPreserved(XElement before, XElement after, params string[] names)
    {
        foreach (string name in names)
        {
            Assert.AreEqual(before.Element(name)!.Value, after.Element(name)!.Value, name);
        }
    }
}
