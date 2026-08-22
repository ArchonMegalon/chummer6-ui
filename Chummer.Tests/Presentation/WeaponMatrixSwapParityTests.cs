using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class WeaponMatrixSwapParityTests
{
    private static readonly Guid WeaponId = Guid.Parse("d7111111-1711-4711-8711-171111111111");
    private static readonly Guid DescendantId = Guid.Parse("d7222222-1722-4722-8722-172222222222");

    [TestMethod]
    public void Career_four_handlers_exchange_only_the_two_raw_direct_weapon_values()
    {
        AssertSwap(CharacterWeaponMatrixStat.Attack, CharacterWeaponMatrixStat.Sleaze,
            expectedAttack: "7", expectedSleaze: "8", expectedDataProcessing: "6", expectedFirewall: "5");
        AssertSwap(CharacterWeaponMatrixStat.Sleaze, CharacterWeaponMatrixStat.DataProcessing,
            expectedAttack: "8", expectedSleaze: "6", expectedDataProcessing: "7", expectedFirewall: "5");
        AssertSwap(CharacterWeaponMatrixStat.DataProcessing, CharacterWeaponMatrixStat.Attack,
            expectedAttack: "6", expectedSleaze: "7", expectedDataProcessing: "8", expectedFirewall: "5");
        AssertSwap(CharacterWeaponMatrixStat.Firewall, CharacterWeaponMatrixStat.Sleaze,
            expectedAttack: "8", expectedSleaze: "5", expectedDataProcessing: "6", expectedFirewall: "7");
    }

    [TestMethod]
    public void Creation_descendant_duplicate_disabled_and_stale_targets_fail_closed()
    {
        string xml = Fixture(created: true);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WeaponMatrixSwapEditorProjector.ProjectValue(Fixture(created: false), WeaponId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WeaponMatrixSwapEditorProjector.ProjectValue(xml, DescendantId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WeaponMatrixSwapEditorProjector.ProjectValue(
                xml.Replace(
                    $"<guid>{DescendantId:D}</guid>",
                    $"<guid>{WeaponId:D}</guid>",
                    StringComparison.Ordinal),
                WeaponId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WeaponMatrixSwapEditorProjector.ProjectValue(
                xml.Replace("<canswapattributes>True", "<canswapattributes>False", StringComparison.Ordinal),
                WeaponId));

        CharacterWeaponMatrixSwapState state = WeaponMatrixSwapEditorProjector.ProjectValue(xml, WeaponId);
        var stale = new WeaponMatrixSwapEditRequest(
            new CharacterWorkspaceId("runner"),
            9,
            state.Identity,
            new string('0', 64),
            CharacterWeaponMatrixStat.Attack,
            CharacterWeaponMatrixStat.Firewall);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyWeaponMatrixSwapEdit(xml, stale));
    }

    private static void AssertSwap(
        CharacterWeaponMatrixStat changed,
        CharacterWeaponMatrixStat target,
        string expectedAttack,
        string expectedSleaze,
        string expectedDataProcessing,
        string expectedFirewall)
    {
        string xml = Fixture(created: true);
        CharacterWeaponMatrixSwapState state = WeaponMatrixSwapEditorProjector.ProjectValue(xml, WeaponId);
        string changedXml = WorkspaceXmlMutationCatalog.ApplyWeaponMatrixSwapEdit(
            xml,
            new WeaponMatrixSwapEditRequest(
                new CharacterWorkspaceId("runner"),
                9,
                state.Identity,
                state.Revision,
                changed,
                target));
        XElement before = Root(xml);
        XElement after = Root(changedXml);

        Assert.AreEqual(expectedAttack, after.Element("attack")!.Value);
        Assert.AreEqual(expectedSleaze, after.Element("sleaze")!.Value);
        Assert.AreEqual(expectedDataProcessing, after.Element("dataprocessing")!.Value);
        Assert.AreEqual(expectedFirewall, after.Element("firewall")!.Value);
        foreach (string name in new[]
                 {
                     "attributearray", "canswapattributes", "modattack", "modsleaze",
                     "moddataprocessing", "modfirewall", "rating", "category", "cost",
                     "active", "homenode", "notes", "underbarrel", "accessories"
                 })
        {
            Assert.IsTrue(XNode.DeepEquals(before.Element(name), after.Element(name)), name);
        }

        XElement changedRoot = XDocument.Parse(changedXml).Root!;
        Assert.AreEqual("8765", changedRoot.Element("nuyen")!.Value);
        Assert.AreEqual("19", changedRoot.Element("karma")!.Value);
    }

    private static string Fixture(bool created)
        => $$"""<character><created>{{created}}</created><nuyen>8765</nuyen><karma>19</karma><weapons><weapon><guid>{{WeaponId:D}}</guid><name>Career Matrix Weapon</name><category>Cyberweapons</category><rating>4</rating><attack>8</attack><sleaze>7</sleaze><dataprocessing>6</dataprocessing><firewall>5</firewall><attributearray>8,7,6,5</attributearray><canswapattributes>True</canswapattributes><modattack>2</modattack><modsleaze>3</modsleaze><moddataprocessing>4</moddataprocessing><modfirewall>1</modfirewall><cost>54321</cost><active>True</active><homenode>False</homenode><notes>root sentinel</notes><underbarrel><weapon><guid>{{DescendantId:D}}</guid><name>Underbarrel Matrix sentinel</name><attack>4</attack><sleaze>3</sleaze><dataprocessing>2</dataprocessing><firewall>1</firewall><attributearray>4,3,2,1</attributearray><canswapattributes>True</canswapattributes></weapon></underbarrel><accessories><accessory><guid>d7333333-1733-4733-8733-173333333333</guid><name>Accessory sentinel</name><gears><gear><guid>d7444444-1744-4744-8744-174444444444</guid><name>Child Gear Matrix sentinel</name><attack>4</attack><sleaze>3</sleaze><dataprocessing>2</dataprocessing><firewall>1</firewall><attributearray>4,3,2,1</attributearray><canswapattributes>True</canswapattributes></gear></gears></accessory></accessories></weapon></weapons></character>""";

    private static XElement Root(string xml)
        => XDocument.Parse(xml).Root!.Element("weapons")!.Element("weapon")!;
}
