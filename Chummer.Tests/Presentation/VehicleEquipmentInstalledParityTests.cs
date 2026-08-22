using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class VehicleEquipmentInstalledParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("vehicle-installed-parity");
    private static readonly Guid VehicleId = Guid.Parse("61111111-6111-4111-8111-611111111111");
    private static readonly Guid MountId = Guid.Parse("62222222-6222-4222-8222-622222222222");
    private static readonly Guid MountModId = Guid.Parse("63333333-6333-4333-8333-633333333333");
    private static readonly Guid MountWeaponId = Guid.Parse("64444444-6444-4444-8444-644444444444");
    private static readonly Guid AccessoryId = Guid.Parse("65555555-6555-4555-8555-655555555555");
    private static readonly Guid SensorModId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid RootWeaponId = Guid.Parse("67777777-6777-4777-8777-677777777777");
    private static readonly Guid UnderbarrelId = Guid.Parse("68888888-6888-4888-8888-688888888888");
    private static readonly Guid IncludedWeaponId = Guid.Parse("69999999-6999-4999-8999-699999999999");

    [TestMethod]
    public void Projector_proves_typed_union_phase_enable_rules_and_zero_economics()
    {
        VehicleEquipmentInstalledEditorState creation = VehicleEquipmentInstalledEditorProjector
            .Project(Xml(created: false), WorkspaceId, 17, VehicleId);
        VehicleEquipmentInstalledEditorState career = VehicleEquipmentInstalledEditorProjector
            .Project(Xml(created: true), WorkspaceId, 18, VehicleId);

        Assert.AreEqual(8, creation.Nodes.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                CharacterVehicleEquipmentNodeKind.WeaponMount,
                CharacterVehicleEquipmentNodeKind.VehicleMod,
                CharacterVehicleEquipmentNodeKind.Weapon,
                CharacterVehicleEquipmentNodeKind.WeaponAccessory
            },
            creation.Nodes.Select(node => node.Identity.Path[^1].Kind).Distinct().ToArray());
        Assert.IsTrue(creation.Nodes.All(node =>
            node.Phase == CharacterVehicleEquipmentInstalledPhase.Creation
            && node.Economics is { NuyenDelta: 0m, KarmaDelta: 0 }));
        Assert.IsTrue(career.Nodes.All(node =>
            node.Phase == CharacterVehicleEquipmentInstalledPhase.Career));

        CharacterVehicleEquipmentInstalledState mount = Node(creation, MountId);
        CharacterVehicleEquipmentInstalledState mountMod = Node(creation, MountModId);
        CharacterVehicleEquipmentInstalledState mountWeapon = Node(creation, MountWeaponId);
        CharacterVehicleEquipmentInstalledState accessory = Node(creation, AccessoryId);
        CharacterVehicleEquipmentInstalledState sensorMod = Node(creation, SensorModId);
        CharacterVehicleEquipmentInstalledState rootWeapon = Node(creation, RootWeaponId);
        CharacterVehicleEquipmentInstalledState underbarrel = Node(creation, UnderbarrelId);
        CharacterVehicleEquipmentInstalledState includedWeapon = Node(creation, IncludedWeaponId);
        Assert.IsTrue(mount.CanChangeInstalled);
        Assert.IsTrue(mountMod.CanChangeInstalled);
        Assert.IsTrue(mountWeapon.CanChangeInstalled);
        Assert.IsTrue(accessory.CanChangeInstalled);
        Assert.IsTrue(rootWeapon.CanChangeInstalled);
        Assert.IsTrue(sensorMod.LegacyEnabled);
        Assert.IsFalse(sensorMod.CanChangeInstalled);
        Assert.IsFalse(underbarrel.LegacyEnabled);
        Assert.IsFalse(includedWeapon.LegacyEnabled);
    }

    [TestMethod]
    public void Apply_changes_only_the_exact_deep_accessory_equipped_value()
    {
        string source = Xml(created: true);
        CharacterVehicleEquipmentInstalledState accessory = Node(
            VehicleEquipmentInstalledEditorProjector.Project(source, WorkspaceId, 17, VehicleId),
            AccessoryId);
        string mutated = WorkspaceXmlMutationCatalog.ApplyVehicleEquipmentInstalledEdit(
            source,
            new VehicleEquipmentInstalledEditRequest(
                WorkspaceId,
                17,
                accessory.Identity,
                accessory.Revision,
                Installed: false));

        XDocument document = XDocument.Parse(mutated, LoadOptions.PreserveWhitespace);
        XElement selected = VehicleEquipmentInstalledEditorProjector.FindNode(
            document.Root!, accessory.Identity);
        Assert.AreEqual("False", selected.Element("equipped")!.Value);
        Assert.AreEqual("Accessory sentinel", selected.Element("notes")!.Value);
        Assert.AreEqual("False", document.Root!.Element("vehicles")!
            .Elements("vehicle").Last().Element("homenode")!.Value);
        Assert.AreEqual("7654", document.Root!.Element("nuyen")!.Value);
        Assert.AreEqual("23", document.Root!.Element("karma")!.Value);
        Assert.AreEqual("Runner sentinel", document.Root!.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Stale_disabled_sensor_ambiguous_and_invalid_saved_data_fail_closed()
    {
        string source = Xml(created: false);
        VehicleEquipmentInstalledEditorState editor = VehicleEquipmentInstalledEditorProjector
            .Project(source, WorkspaceId, 17, VehicleId);
        CharacterVehicleEquipmentInstalledState accessory = Node(editor, AccessoryId);
        CharacterVehicleEquipmentInstalledState sensorMod = Node(editor, SensorModId);
        CharacterVehicleEquipmentInstalledState underbarrel = Node(editor, UnderbarrelId);

        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog
            .ApplyVehicleEquipmentInstalledEdit(source, new(
                WorkspaceId, 17, accessory.Identity, new string('0', 64), false)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog
            .ApplyVehicleEquipmentInstalledEdit(source, new(
                WorkspaceId, 17, sensorMod.Identity, sensorMod.Revision, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkspaceXmlMutationCatalog
            .ApplyVehicleEquipmentInstalledEdit(source, new(
                WorkspaceId, 17, underbarrel.Identity, underbarrel.Revision, true)));
        Assert.ThrowsExactly<InvalidOperationException>(() => VehicleEquipmentInstalledEditorProjector.Project(
            source.Replace("<equipped>True</equipped>", "<equipped>maybe</equipped>", StringComparison.Ordinal),
            WorkspaceId, 17, VehicleId));
        Assert.ThrowsExactly<InvalidOperationException>(() => VehicleEquipmentInstalledEditorProjector.Project(
            source.Replace(
                "</vehicles>",
                $"<vehicle><guid>{VehicleId:D}</guid><name>Duplicate</name></vehicle></vehicles>",
                StringComparison.Ordinal),
            WorkspaceId, 17, VehicleId));
    }

    private static CharacterVehicleEquipmentInstalledState Node(
        VehicleEquipmentInstalledEditorState editor,
        Guid id)
        => editor.Nodes.Single(node => node.Identity.Path[^1].Id == id);

    private static string Xml(bool created) => $"""
        <character>
          <created>{created}</created>
          <vehicles>
            <vehicle>
              <guid>{VehicleId:D}</guid><name>Roadmaster</name><homenode>True</homenode>
              <weaponmounts><weaponmount>
                <guid>{MountId:D}</guid><name>External Mount</name>
                <included>False</included><equipped>False</equipped><notes>Mount sentinel</notes>
                <mods><mod>
                  <guid>{MountModId:D}</guid><name>Mount Mod</name>
                  <included>False</included><equipped>True</equipped><wirelesson>True</wirelesson>
                  <weapons />
                </mod></mods>
                <weapons><weapon>
                  <guid>{MountWeaponId:D}</guid><name>Mount Weapon</name>
                  <parentid>{MountId:D}</parentid><equipped>False</equipped>
                  <accessories><accessory>
                    <guid>{AccessoryId:D}</guid><name>Smartgun</name>
                    <equipped>True</equipped><notes>Accessory sentinel</notes>
                  </accessory></accessories>
                </weapon></weapons>
              </weaponmount></weaponmounts>
              <mods><mod>
                <guid>{SensorModId:D}</guid><name>Sensor Side Effect</name>
                <included>False</included><equipped>False</equipped><wirelesson>True</wirelesson>
                <bonus><sensor>2</sensor></bonus><weapons />
              </mod></mods>
              <weapons>
                <weapon>
                  <guid>{RootWeaponId:D}</guid><name>Loose Weapon</name>
                  <parentid></parentid><equipped>True</equipped><accessories />
                  <underbarrel><weapon>
                    <guid>{UnderbarrelId:D}</guid><name>Included Underbarrel</name>
                    <parentid>{RootWeaponId:D}</parentid><equipped>False</equipped><accessories />
                  </weapon></underbarrel>
                </weapon>
                <weapon>
                  <guid>{IncludedWeaponId:D}</guid><name>Vehicle Included Weapon</name>
                  <parentid>{VehicleId:D}</parentid><equipped>True</equipped><accessories />
                </weapon>
              </weapons>
            </vehicle>
            <vehicle>
              <guid>6aaaaaaa-6aaa-4aaa-8aaa-6aaaaaaaaaaa</guid><name>Untouched Vehicle</name>
              <homenode>False</homenode><weaponmounts /><mods /><weapons />
            </vehicle>
          </vehicles>
          <nuyen>7654</nuyen><karma>23</karma><customstate>Runner sentinel</customstate>
        </character>
        """;
}
