using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class GearAttackSwapParityTests
{
    private static readonly Guid RootId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ChildId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [TestMethod]
    public void SwapsRawValuesAndPreservesBonusesProvenanceCostAndState()
    {
        string xml = Fixture(created: false);
        CharacterGearAttackSwapState target = GearAttackSwapEditorProjector.ProjectValue(xml, RootId).Single();
        string mutated = WorkspaceXmlMutationCatalog.ApplyGearAttackSwapEdit(xml, new GearAttackSwapEditRequest(
            new CharacterWorkspaceId("runner"), 7, target.Identity, target.Revision,
            CharacterGearAttackSwapTarget.DataProcessing));

        XDocument before = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XDocument after = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement beforeGear = GearAttackSwapEditorProjector.FindNode(before.Root!, target.Identity);
        XElement afterGear = GearAttackSwapEditorProjector.FindNode(after.Root!, target.Identity);
        Assert.AreEqual("5", afterGear.Element("attack")!.Value);
        Assert.AreEqual("7", afterGear.Element("dataprocessing")!.Value);
        foreach (string name in new[] { "sleaze", "firewall", "attributearray", "canswapattributes", "modattack",
                     "modsleaze", "moddataprocessing", "modfirewall", "cost", "active", "homenode", "equipped" })
            Assert.AreEqual(beforeGear.Element(name)!.ToString(SaveOptions.DisableFormatting),
                afterGear.Element(name)!.ToString(SaveOptions.DisableFormatting), name);
        Assert.AreEqual(CharacterGearAttackSwapPhase.Creation, target.Phase);
        Assert.AreEqual(0m, target.Economics.NuyenDelta);
        Assert.AreEqual(0, target.Economics.KarmaDelta);
    }

    [TestMethod]
    public void RejectsStaleIneligibleDuplicateAndAmbiguousIdentity()
    {
        string xml = Fixture(created: true);
        CharacterGearAttackSwapState target = GearAttackSwapEditorProjector.ProjectValue(xml, RootId).Single();
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog.ApplyGearAttackSwapEdit(
            xml, new GearAttackSwapEditRequest(new CharacterWorkspaceId("runner"), 2, target.Identity,
                new string('0', 64), CharacterGearAttackSwapTarget.Sleaze)));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearAttackSwapEditorProjector.ProjectValue(
            xml.Replace("<canswapattributes>True</canswapattributes>", "<canswapattributes>False</canswapattributes>"), RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearAttackSwapEditorProjector.ProjectValue(
            xml.Replace("<attack>7</attack>", "<attack>7</attack><attack>8</attack>"), RootId));
        Assert.ThrowsExactly<InvalidOperationException>(() => GearAttackSwapEditorProjector.ProjectValue(
            xml.Replace($"</gears>", $"<gear><guid>{RootId:D}</guid></gear></gears>"), RootId));
    }

    private static string Fixture(bool created) => $"""
        <character><created>{created}</created><gears><gear>
          <guid>{RootId:D}</guid><name>Carrier</name><canswapattributes>False</canswapattributes>
          <children><gear><guid>{ChildId:D}</guid><name>Raw Deck</name><category>Cyberdecks</category>
            <attack>7</attack><sleaze>{{Rating}}</sleaze><dataprocessing>5</dataprocessing><firewall>4</firewall>
            <attributearray>7,6,5,4</attributearray><canswapattributes>True</canswapattributes>
            <modattack>2</modattack><modsleaze>3</modsleaze><moddataprocessing>4</moddataprocessing><modfirewall>5</modfirewall>
            <cost>12345</cost><active>True</active><homenode>False</homenode><equipped>True</equipped>
          </gear></children>
        </gear></gears></character>
        """;
}
