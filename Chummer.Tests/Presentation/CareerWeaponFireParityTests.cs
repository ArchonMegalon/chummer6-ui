using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerWeaponFireParityTests
{
    private static readonly Guid WeaponId = Guid.Parse("f1111111-1111-4111-8111-111111111111");
    private static readonly Guid AmmoId = Guid.Parse("f2222222-2222-4222-8222-222222222222");
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-fire-runner");

    [TestMethod]
    public void Projector_preserves_direct_typed_identity_effective_counts_modes_and_default()
    {
        CareerWeaponFireProjection projection = CareerWeaponFireEditorProjector.ProjectValue(
            Fixture(ammo: 30, quantity: 30),
            WeaponId);

        Assert.AreEqual(new CharacterWeaponFireIdentity(WeaponId, 1, AmmoId), projection.State.Identity);
        Assert.AreEqual("Ares Alpha", projection.State.DisplayName);
        Assert.AreEqual(30, projection.State.AmmoRemaining);
        Assert.AreEqual(30m, projection.State.AmmoGearQuantity);
        Assert.AreEqual(CharacterWeaponFireMode.SingleShot, projection.State.DefaultMode);
        CollectionAssert.AreEqual(
            new[] { 2, 4, 7, 11, 21 },
            projection.State.Modes.Select(mode => mode.Rounds).ToArray());
    }

    [TestMethod]
    public void Fire_mutation_decrements_saved_clip_and_linked_ammo_atomically()
    {
        string xml = Fixture(ammo: 30, quantity: 30);
        CharacterWeaponFireState state = CareerWeaponFireEditorProjector.ProjectValue(xml, WeaponId).State;

        string updated = Apply(xml, state, CharacterWeaponFireMode.SingleShot);
        XElement root = XDocument.Parse(updated).Root!;
        Assert.AreEqual("28", root.Descendants("clip").Single().Element("count")!.Value);
        Assert.AreEqual("28", root.Descendants("gear").Single().Element("qty")!.Value);
        Assert.AreEqual("777", root.Element("karma")!.Value);
        Assert.AreEqual("sentinel", root.Element("notes")!.Value);
    }

    [TestMethod]
    public void Short_burst_partial_requires_confirmation_and_then_consumes_remaining_ammo()
    {
        string xml = Fixture(ammo: 2, quantity: 2);
        CharacterWeaponFireState state = CareerWeaponFireEditorProjector.ProjectValue(xml, WeaponId).State;
        var unconfirmed = Request(state, CharacterWeaponFireMode.ShortBurst, confirmedPartial: false);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerWeaponFire(xml, unconfirmed));

        string updated = WorkspaceXmlMutationCatalog.ApplyCareerWeaponFire(
            xml,
            unconfirmed with { ConfirmedPartial = true });
        XElement root = XDocument.Parse(updated).Root!;
        Assert.IsFalse(root.Descendants("gear").Any());
        Assert.IsFalse(root.Descendants("clip").Any());
        Assert.IsNull(root.Descendants("weapon").Single().Element("clips"));
    }

    [TestMethod]
    public void Full_and_suppressive_fire_do_not_apply_partial_mutations()
    {
        string xml = Fixture(ammo: 9, quantity: 9);
        CharacterWeaponFireState state = CareerWeaponFireEditorProjector.ProjectValue(xml, WeaponId).State;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Apply(xml, state, CharacterWeaponFireMode.FullBurst));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Apply(xml, state, CharacterWeaponFireMode.SuppressiveFire));
    }

    [TestMethod]
    public void Exhausted_ammo_stack_and_unsaved_empty_clip_are_removed_like_Chummer5_Save()
    {
        string xml = Fixture(ammo: 4, quantity: 4);
        CharacterWeaponFireState state = CareerWeaponFireEditorProjector.ProjectValue(xml, WeaponId).State;

        string updated = Apply(xml, state, CharacterWeaponFireMode.ShortBurst);
        XElement root = XDocument.Parse(updated).Root!;
        Assert.IsFalse(root.Descendants("gear").Any());
        Assert.IsFalse(root.Descendants("clip").Any());
        Assert.IsNull(root.Descendants("weapon").Single().Element("clips"));
    }

    [TestMethod]
    public void Linked_clip_and_ammo_quantity_mismatches_fail_closed()
    {
        foreach (decimal quantity in new[] { 29m, 30.5m, 40m })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                CareerWeaponFireEditorProjector.ProjectValue(
                    Fixture(ammo: 30, quantity: quantity),
                    WeaponId));
        }
    }

    [TestMethod]
    public void Creation_descendant_stale_bonus_and_unsafe_delete_states_fail_closed()
    {
        string xml = Fixture(ammo: 30, quantity: 30);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerWeaponFireEditorProjector.ProjectValue(
                xml.Replace("<created>True", "<created>False", StringComparison.Ordinal),
                WeaponId));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerWeaponFireEditorProjector.ProjectValue(
                xml.Replace("<weapon><guid>", "<weapon><underbarrels><weapon><guid>", StringComparison.Ordinal)
                    .Replace("</weapon></weapons>", "</weapon></underbarrels></weapon></weapons>", StringComparison.Ordinal),
                WeaponId));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerWeaponFireEditorProjector.ProjectValue(
                xml.Replace(
                    "<wirelessweaponbonus />",
                    "<wirelessweaponbonus><firemode>Special</firemode></wirelessweaponbonus>",
                    StringComparison.Ordinal),
                WeaponId));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            CareerWeaponFireEditorProjector.ProjectValue(
                xml.Replace("<children />", "<children><gear><guid>f3333333-3333-4333-8333-333333333333</guid></gear></children>", StringComparison.Ordinal),
                WeaponId));

        CharacterWeaponFireState state = CareerWeaponFireEditorProjector.ProjectValue(xml, WeaponId).State;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerWeaponFire(
                xml,
                Request(state, CharacterWeaponFireMode.SingleShot) with
                {
                    ExpectedNodeRevision = new string('0', CharacterWeaponFireRules.RevisionHexLength)
                }));
    }

    private static string Apply(
        string xml,
        CharacterWeaponFireState state,
        CharacterWeaponFireMode mode)
        => WorkspaceXmlMutationCatalog.ApplyCareerWeaponFire(xml, Request(state, mode));

    private static CareerWeaponFireRequest Request(
        CharacterWeaponFireState state,
        CharacterWeaponFireMode mode,
        bool confirmedPartial = false)
        => new(WorkspaceId, 17, state.Identity, state.Revision, mode, confirmedPartial);

    private static string Fixture(int ammo, decimal quantity)
        => $$"""
             <character><created>True</created><karma>777</karma><notes>sentinel</notes><weapons><weapon><guid>{{WeaponId:D}}</guid><name>Ares Alpha</name><customname></customname><type>Ranged</type><ammo>42(c)</ammo><ammoslots>1</ammoslots><activeammoslot>1</activeammoslot><mode>SA/BF/FA</mode><singleshot>1</singleshot><shortburst>3</shortburst><longburst>6</longburst><fullburst>10</fullburst><suppressive>20</suppressive><allowsingleshot>True</allowsingleshot><allowshortburst>True</allowshortburst><allowlongburst>True</allowlongburst><allowfullburst>True</allowfullburst><allowsuppressive>True</allowsuppressive><wirelesson>True</wirelesson><wirelessweaponbonus /><clips><clip><count>{{ammo}}</count><location>loaded</location><id>{{AmmoId:D}}</id></clip></clips><accessories><accessory><equipped>True</equipped><firemode></firemode><firemodereplace></firemodereplace><singleshot>2</singleshot><shortburst>4</shortburst><longburst>7</longburst><fullburst>11</fullburst><suppressive>21</suppressive><wirelesson>False</wirelesson><wirelessweaponbonus /></accessory></accessories></weapon></weapons><gears><gear><guid>{{AmmoId:D}}</guid><name>Regular Ammo</name><category>Ammunition</category><qty>{{quantity}}</qty><weaponid>00000000-0000-0000-0000-000000000000</weaponid><children /><weaponbonus /><flechetteweaponbonus /></gear></gears><improvements /></character>
             """;
}
