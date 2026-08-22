using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CyberwareMatrixSwapParityTests
{
    private static readonly Guid CyberwareId = Guid.Parse("a6111111-1611-4611-8611-161111111111");
    private static readonly Guid ChildId = Guid.Parse("b6111111-1611-4611-8611-161111111111");

    [TestMethod]
    public void Creation_attack_and_sleaze_handlers_exchange_only_the_two_raw_root_values()
    {
        AssertSwap(false, CharacterCyberwareMatrixStat.Attack, CharacterCyberwareMatrixStat.Sleaze,
            expectedAttack: "{Rating}", expectedSleaze: "7", expectedDataProcessing: "5", expectedFirewall: "4");
        AssertSwap(false, CharacterCyberwareMatrixStat.Sleaze, CharacterCyberwareMatrixStat.DataProcessing,
            expectedAttack: "7", expectedSleaze: "5", expectedDataProcessing: "{Rating}", expectedFirewall: "4");
    }

    [TestMethod]
    public void Career_data_processing_and_firewall_handlers_use_the_same_revision_bound_permutation()
    {
        AssertSwap(true, CharacterCyberwareMatrixStat.DataProcessing, CharacterCyberwareMatrixStat.Attack,
            expectedAttack: "{Rating}", expectedSleaze: "7", expectedDataProcessing: "8", expectedFirewall: "5");
        AssertSwap(true, CharacterCyberwareMatrixStat.Firewall, CharacterCyberwareMatrixStat.Sleaze,
            expectedAttack: "8", expectedSleaze: "5", expectedDataProcessing: "{Rating}", expectedFirewall: "7");
    }

    [TestMethod]
    public void Descendant_duplicate_disabled_and_stale_targets_fail_closed()
    {
        string xml = Fixture(false);
        Assert.ThrowsException<InvalidOperationException>(() =>
            CyberwareMatrixSwapEditorProjector.ProjectValue(xml, ChildId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            CyberwareMatrixSwapEditorProjector.ProjectValue(
                xml.Replace("<cyberwares>", $"<cyberwares>{Root(xml)}", StringComparison.Ordinal), CyberwareId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            CyberwareMatrixSwapEditorProjector.ProjectValue(
                xml.Replace("<canswapattributes>True", "<canswapattributes>False", StringComparison.Ordinal),
                CyberwareId));

        CharacterCyberwareMatrixSwapState state = CyberwareMatrixSwapEditorProjector.ProjectValue(xml, CyberwareId);
        var request = new CyberwareMatrixSwapEditRequest(
            new CharacterWorkspaceId("runner"), 5, state.Identity, new string('0', 64),
            CharacterCyberwareMatrixStat.Attack, CharacterCyberwareMatrixStat.Firewall);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCyberwareMatrixSwapEdit(xml, request));
    }

    private static void AssertSwap(
        bool created,
        CharacterCyberwareMatrixStat changed,
        CharacterCyberwareMatrixStat target,
        string expectedAttack,
        string expectedSleaze,
        string expectedDataProcessing,
        string expectedFirewall)
    {
        string xml = Fixture(created);
        CharacterCyberwareMatrixSwapState state = CyberwareMatrixSwapEditorProjector.ProjectValue(xml, CyberwareId);
        string changedXml = WorkspaceXmlMutationCatalog.ApplyCyberwareMatrixSwapEdit(xml,
            new(new CharacterWorkspaceId("runner"), created ? 9 : 5, state.Identity, state.Revision,
                changed, target));
        XElement before = Root(xml);
        XElement after = Root(changedXml);

        Assert.AreEqual(expectedAttack, after.Element("attack")!.Value);
        Assert.AreEqual(expectedSleaze, after.Element("sleaze")!.Value);
        Assert.AreEqual(expectedDataProcessing, after.Element("dataprocessing")!.Value);
        Assert.AreEqual(expectedFirewall, after.Element("firewall")!.Value);
        foreach (string name in new[] { "attributearray", "canswapattributes", "modattack", "modsleaze",
                     "moddataprocessing", "modfirewall", "rating", "grade", "cost", "active", "homenode",
                     "notes", "children", "gears" })
        {
            Assert.IsTrue(XNode.DeepEquals(before.Element(name), after.Element(name)), name);
        }
        XElement changedRoot = XDocument.Parse(changedXml).Root!;
        Assert.AreEqual(created ? "8765" : "4321", changedRoot.Element("nuyen")!.Value);
        Assert.AreEqual(created ? "19" : "7", changedRoot.Element("karma")!.Value);
    }

    private static string Fixture(bool created)
    {
        string attack = created ? "8" : "7";
        string sleaze = created ? "7" : "{Rating}";
        string dataProcessing = created ? "{Rating}" : "5";
        string firewall = created ? "5" : "4";
        string array = created ? "8,7,6,5" : "7,6,5,4";
        string nuyen = created ? "8765" : "4321";
        string karma = created ? "19" : "7";
        return $$"""<character><created>{{created}}</created><nuyen>{{nuyen}}</nuyen><karma>{{karma}}</karma><cyberwares><cyberware><guid>{{CyberwareId:D}}</guid><name>Matrix Cyberware</name><rating>3</rating><grade>Standard</grade><attack>{{attack}}</attack><sleaze>{{sleaze}}</sleaze><dataprocessing>{{dataProcessing}}</dataprocessing><firewall>{{firewall}}</firewall><attributearray>{{array}}</attributearray><canswapattributes>True</canswapattributes><modattack>2</modattack><modsleaze>3</modsleaze><moddataprocessing>9</moddataprocessing><modfirewall>5</modfirewall><cost>12345</cost><active>True</active><homenode>False</homenode><notes>root sentinel</notes><children><cyberware><guid>{{ChildId:D}}</guid><name>Child Matrix target</name><attack>3</attack><sleaze>2</sleaze><dataprocessing>1</dataprocessing><firewall>4</firewall><attributearray>4,3,2,1</attributearray><canswapattributes>True</canswapattributes></cyberware></children><gears><gear><guid>c6111111-1611-4611-8611-161111111111</guid><name>Child Gear target</name><attack>3</attack><sleaze>2</sleaze><dataprocessing>1</dataprocessing><firewall>4</firewall><attributearray>4,3,2,1</attributearray><canswapattributes>True</canswapattributes></gear></gears></cyberware></cyberwares></character>""";
    }

    private static XElement Root(string xml)
        => XDocument.Parse(xml).Root!.Element("cyberwares")!.Element("cyberware")!;
}
