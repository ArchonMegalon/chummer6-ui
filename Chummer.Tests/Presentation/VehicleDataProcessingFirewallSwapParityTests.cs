using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class VehicleDataProcessingFirewallSwapParityTests
{
    private static readonly Guid VehicleId = Guid.Parse("91111111-1111-4111-8111-111111111111");

    [TestMethod]
    public void Creation_data_processing_swap_changes_only_two_raw_vehicle_elements()
    {
        string xml = Fixture(false);
        CharacterVehicleMatrixSwapState state = VehicleDataProcessingFirewallSwapEditorProjector.ProjectValue(xml, VehicleId);
        string changed = WorkspaceXmlMutationCatalog.ApplyVehicleDataProcessingFirewallSwapEdit(xml,
            new(new CharacterWorkspaceId("runner"), 5, state.Identity, state.Revision,
                CharacterVehicleMatrixStat.DataProcessing, CharacterVehicleMatrixStat.Attack));
        XElement before = Vehicle(xml); XElement after = Vehicle(changed);
        Assert.AreEqual("7", after.Element("dataprocessing")!.Value);
        Assert.AreEqual("5", after.Element("attack")!.Value);
        AssertPreserved(before, after, "sleaze", "firewall", "attributearray", "canswapattributes",
            "modattack", "modsleaze", "moddataprocessing", "modfirewall", "active", "homenode",
            "sensor", "cost", "notes");
        Assert.AreEqual("4321", XDocument.Parse(changed).Root!.Element("nuyen")!.Value);
    }

    [TestMethod]
    public void Career_firewall_swap_is_revision_bound_and_preserves_parent_state()
    {
        string xml = Fixture(true);
        CharacterVehicleMatrixSwapState state = VehicleDataProcessingFirewallSwapEditorProjector.ProjectValue(xml, VehicleId);
        var request = new VehicleDataProcessingFirewallSwapEditRequest(
            new CharacterWorkspaceId("runner"), 9, state.Identity, state.Revision,
            CharacterVehicleMatrixStat.Firewall, CharacterVehicleMatrixStat.DataProcessing);
        string changed = WorkspaceXmlMutationCatalog.ApplyVehicleDataProcessingFirewallSwapEdit(xml, request);
        Assert.AreEqual("5", Vehicle(changed).Element("firewall")!.Value);
        Assert.AreEqual("4", Vehicle(changed).Element("dataprocessing")!.Value);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyVehicleDataProcessingFirewallSwapEdit(
                xml, request with { ExpectedNodeRevision = new string('0', 64) }));
    }

    [TestMethod]
    public void Duplicate_vehicle_or_disabled_combo_fails_closed()
    {
        string xml = Fixture(false);
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleDataProcessingFirewallSwapEditorProjector.ProjectValue(
                xml.Replace("<vehicles>", $"<vehicles>{Vehicle(xml)}", StringComparison.Ordinal), VehicleId));
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleDataProcessingFirewallSwapEditorProjector.ProjectValue(
                xml.Replace("<canswapattributes>True", "<canswapattributes>False", StringComparison.Ordinal), VehicleId));
    }

    private static string Fixture(bool created) =>
        $$"""<character><created>{{created}}</created><nuyen>4321</nuyen><karma>7</karma><vehicles><vehicle><guid>{{VehicleId:D}}</guid><name>Roadmaster</name><attack>7</attack><sleaze>{Pilot}</sleaze><dataprocessing>5</dataprocessing><firewall>4</firewall><attributearray>7,6,5,4</attributearray><canswapattributes>True</canswapattributes><modattack>2</modattack><modsleaze>3</modsleaze><moddataprocessing>9</moddataprocessing><modfirewall>5</modfirewall><active>True</active><homenode>False</homenode><sensor>6</sensor><cost>50000</cost><notes>sentinel</notes></vehicle></vehicles></character>""";

    private static XElement Vehicle(string xml) => XDocument.Parse(xml).Root!.Element("vehicles")!.Element("vehicle")!;
    private static void AssertPreserved(XElement before, XElement after, params string[] names)
    {
        foreach (string name in names) Assert.AreEqual(before.Element(name)!.Value, after.Element(name)!.Value, name);
    }
}
