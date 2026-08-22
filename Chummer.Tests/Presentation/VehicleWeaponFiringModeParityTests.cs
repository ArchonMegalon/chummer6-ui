using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class VehicleWeaponFiringModeParityTests
{
    private static readonly Guid VehicleId = Guid.Parse("93333333-3333-4333-8333-333333333333");
    private static readonly Guid WeaponId = Guid.Parse("94444444-4444-4444-8444-444444444444");
    private static readonly Guid HiddenWeaponId = Guid.Parse("95555555-5555-4555-8555-555555555555");
    private static readonly Guid DescendantWeaponId = Guid.Parse("96666666-6666-4666-8666-666666666666");

    [TestMethod]
    public void Creation_projects_only_visible_direct_weapons_and_changes_only_firingmode()
    {
        string xml = Fixture(created: false, "DogBrain");
        VehicleWeaponFiringModeEditorState editor = VehicleWeaponFiringModeEditorProjector.Project(
            xml, new CharacterWorkspaceId("runner"), 4, VehicleId);
        Assert.AreEqual(1, editor.Weapons.Count);
        Assert.AreEqual(WeaponId, editor.Weapons[0].Identity.WeaponId);
        Assert.AreEqual(CharacterVehicleWeaponFiringModePhase.Creation, editor.Weapons[0].Phase);

        string changed = WorkspaceXmlMutationCatalog.ApplyVehicleWeaponFiringModeEdit(
            xml,
            new(new CharacterWorkspaceId("runner"), 4, editor.Weapons[0].Identity,
                editor.Weapons[0].Revision, CharacterVehicleWeaponFiringMode.RemoteOperated));
        XDocument expected = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        DirectWeapon(expected).Element("firingmode")!.Value = "RemoteOperated";
        Assert.IsTrue(XNode.DeepEquals(expected, XDocument.Parse(changed, LoadOptions.PreserveWhitespace)));
    }

    [TestMethod]
    public void Career_uses_the_same_typed_revision_bound_zero_economics_mutation()
    {
        string xml = Fixture(created: true, "ManualOperation");
        CharacterVehicleWeaponFiringModeState state = VehicleWeaponFiringModeEditorProjector.ProjectValue(
            xml, new(VehicleId, WeaponId));
        Assert.AreEqual(CharacterVehicleWeaponFiringModePhase.Career, state.Phase);
        Assert.AreEqual(0m, state.Economics.NuyenDelta);
        Assert.AreEqual(0, state.Economics.KarmaDelta);

        var request = new VehicleWeaponFiringModeEditRequest(
            new CharacterWorkspaceId("runner"), 8, state.Identity, state.Revision,
            CharacterVehicleWeaponFiringMode.GunneryCommandDevice);
        string changed = WorkspaceXmlMutationCatalog.ApplyVehicleWeaponFiringModeEdit(xml, request);
        Assert.AreEqual("GunneryCommandDevice", DirectWeapon(XDocument.Parse(changed)).Element("firingmode")!.Value);
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyVehicleWeaponFiringModeEdit(changed, request));
    }

    [TestMethod]
    public void Descendant_hidden_duplicate_and_unsupported_targets_fail_closed()
    {
        string xml = Fixture(created: false, "DogBrain");
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleWeaponFiringModeEditorProjector.ProjectValue(xml, new(VehicleId, DescendantWeaponId)));
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleWeaponFiringModeEditorProjector.ProjectValue(xml, new(VehicleId, HiddenWeaponId)));
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleWeaponFiringModeEditorProjector.ProjectValue(
                xml.Replace(
                    $"<guid>{DescendantWeaponId:D}</guid>",
                    $"<guid>{WeaponId:D}</guid>",
                    StringComparison.Ordinal),
                new(VehicleId, WeaponId)));
        Assert.ThrowsException<InvalidOperationException>(() =>
            VehicleWeaponFiringModeEditorProjector.ProjectValue(
                xml.Replace("<firingmode>DogBrain</firingmode>", "<firingmode>Burst</firingmode>",
                    StringComparison.Ordinal),
                new(VehicleId, WeaponId)));
    }

    private static string Fixture(bool created, string firingMode) => $$"""
        <character><created>{{created}}</created><nuyen>4321</nuyen><karma>7</karma><vehicles><vehicle>
        <guid>{{VehicleId:D}}</guid><name>Roadmaster</name><cost>50000</cost><notes>vehicle sentinel</notes><weapons>
        <weapon><guid>{{WeaponId:D}}</guid><name>Vehicle LMG</name><customname>Turret Alpha</customname>
        <firingmode>{{firingMode}}</firingmode><type>Ranged</type><ammo>100(belt)</ammo>
        <cost>12345</cost><notes>root weapon sentinel</notes><underbarrel><weapon>
        <guid>{{DescendantWeaponId:D}}</guid><name>Descendant</name><firingmode>Skill</firingmode>
        <type>Ranged</type><ammo>1</ammo><notes>descendant sentinel</notes>
        </weapon></underbarrel></weapon><weapon><guid>{{HiddenWeaponId:D}}</guid><name>Hidden Blade</name>
        <firingmode>Skill</firingmode><type>Melee</type><ammo>0</ammo><notes>hidden sentinel</notes></weapon>
        </weapons></vehicle></vehicles></character>
        """;

    private static XElement DirectWeapon(XDocument document)
        => document.Root!.Element("vehicles")!.Element("vehicle")!.Element("weapons")!
            .Elements("weapon").Single(node => node.Element("guid")!.Value == WeaponId.ToString("D"));
}
