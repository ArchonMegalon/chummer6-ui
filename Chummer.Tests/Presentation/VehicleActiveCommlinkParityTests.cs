using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class VehicleActiveCommlinkParityTests
{
    private static readonly Guid VehicleId = Guid.Parse("a1222222-1222-4222-8222-222222222222");

    [TestMethod]
    public void Projector_requires_exact_top_level_identity_phase_enabled_state_and_zero_economics()
    {
        JsonObject semantics = Semantics();
        JsonObject section = Section(semantics);

        WorkspaceCollectionItemEditorState item = WorkspaceCollectionEditorProjector
            .TryProject("vehicles", section)!.Items.Single();

        Assert.IsNotNull(item.VehicleActiveCommlink);
        CharacterVehicleActiveCommlinkSemantics projected = item.VehicleActiveCommlink!;
        Assert.AreEqual(VehicleId, projected.VehicleId);
        Assert.AreEqual(CharacterVehicleActiveCommlinkPhase.Creation, projected.Phase);
        Assert.IsTrue(projected.IsCommlink);
        Assert.IsTrue(projected.Visible);
        Assert.IsTrue(projected.Enabled);
        Assert.AreEqual(0m, projected.Economics.NuyenDelta);
        Assert.AreEqual(0, projected.Economics.KarmaDelta);

        ((JsonObject)semantics["economics"]!)["nuyenDelta"] = 1m;
        item = WorkspaceCollectionEditorProjector.TryProject("vehicles", section)!.Items.Single();
        Assert.IsNull(item.VehicleActiveCommlink);

        ((JsonObject)semantics["economics"]!)["nuyenDelta"] = 0m;
        semantics["enabled"] = false;
        item = WorkspaceCollectionEditorProjector.TryProject("vehicles", section)!.Items.Single();
        Assert.IsNull(item.VehicleActiveCommlink);
    }

    [TestMethod]
    public void Mutation_enables_exclusively_with_zero_economics_and_preserves_unrelated_data()
    {
        string xml = CharacterXml(active: false);
        XElement initialRoot = XDocument.Parse(xml).Root!;
        XElement initialVehicle = initialRoot.Element("vehicles")!.Elements("vehicle").First();
        Assert.IsTrue(CharacterVehicleActiveCommlinkRules.TryProject(
            initialRoot,
            initialVehicle,
            created: false,
            out CharacterVehicleActiveCommlinkSemantics expected));

        string mutated = WorkspaceXmlMutationCatalog.ApplyVehicleActiveCommlinkEdit(
            xml,
            new VehicleActiveCommlinkEditRequest(
                new CharacterWorkspaceId("vehicle-active"),
                7,
                VehicleId,
                ActiveCommlink: true,
                expected));
        XElement root = XDocument.Parse(mutated).Root!;
        XElement vehicle = root.Element("vehicles")!.Elements("vehicle").First();
        XElement priorGear = root.Element("gears")!.Element("gear")!;

        Assert.AreEqual("True", vehicle.Element("active")!.Value);
        Assert.AreEqual("False", priorGear.Element("active")!.Value);
        Assert.AreEqual("4321", root.Element("nuyen")!.Value);
        Assert.AreEqual("7", root.Element("karma")!.Value);
        Assert.AreEqual("Unrelated active text", root.Element("customstate")!.Element("active")!.Value);
        Assert.AreEqual("Persona child sentinel", vehicle.Element("gears")!.Element("gear")!.Element("notes")!.Value);
    }

    [TestMethod]
    public void Mutation_rejects_stale_semantics_nonzero_economics_and_nonactive_removal()
    {
        string xml = CharacterXml(active: false);
        XElement root = XDocument.Parse(xml).Root!;
        XElement vehicle = root.Element("vehicles")!.Elements("vehicle").First();
        Assert.IsTrue(CharacterVehicleActiveCommlinkRules.TryProject(
            root, vehicle, created: false, out CharacterVehicleActiveCommlinkSemantics expected));
        var request = new VehicleActiveCommlinkEditRequest(
            new CharacterWorkspaceId("vehicle-active"), 7, VehicleId, false, expected);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyVehicleActiveCommlinkEdit(xml, request));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyVehicleActiveCommlinkEdit(
                xml,
                request with
                {
                    ExpectedSemantics = expected with
                    {
                        Economics = new CharacterVehicleActiveCommlinkEconomics(1m, 0)
                    }
                }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyVehicleActiveCommlinkEdit(
                xml,
                request with { ExpectedSemantics = expected with { Enabled = false } }));
    }

    private static JsonObject Section(JsonObject semantics) => new()
    {
        ["vehicles"] = new JsonArray
        {
            new JsonObject
            {
                ["guid"] = VehicleId.ToString("D"),
                ["name"] = "Roadmaster",
                ["activeCommlinkSemantics"] = semantics
            }
        }
    };

    private static JsonObject Semantics() => new()
    {
        ["vehicleId"] = VehicleId.ToString("D"),
        ["phase"] = "Creation",
        ["activeCommlink"] = false,
        ["isCommlink"] = true,
        ["visible"] = true,
        ["enabled"] = true,
        ["economics"] = new JsonObject
        {
            ["nuyenDelta"] = 0m,
            ["karmaDelta"] = 0
        }
    };

    private static string CharacterXml(bool active) => $$"""
        <character>
          <created>False</created>
          <nuyen>4321</nuyen>
          <karma>7</karma>
          <customstate><active>Unrelated active text</active></customstate>
          <gears>
            <gear>
              <guid>a1111111-1111-4111-8111-111111111111</guid>
              <active>True</active>
              <canformpersona>Self</canformpersona>
            </gear>
          </gears>
          <vehicles>
            <vehicle>
              <guid>{{VehicleId:D}}</guid>
              <name>Roadmaster</name>
              <pilot>3</pilot>
              <active>{{active}}</active>
              <gears>
                <gear>
                  <guid>a1444444-1444-4444-8444-444444444444</guid>
                  <canformpersona>Parent</canformpersona>
                  <equipped>True</equipped>
                  <notes>Persona child sentinel</notes>
                </gear>
              </gears>
              <mods />
            </vehicle>
          </vehicles>
        </character>
        """;
}
