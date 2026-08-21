using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class GearQuantityMutationTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("gear-quantity-workspace");
    private static readonly Guid SourceId = Guid.Parse("71111111-1111-1111-1111-111111111111");
    private static readonly Guid TargetId = Guid.Parse("72222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void Increase_uses_exact_saved_cost_spends_nuyen_and_appends_undoable_expense()
    {
        XElement root = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Increase,
            Amount: 2m));
        XElement source = Gear(root, SourceId);
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("7", source.Element("qty")!.Value);
        Assert.AreEqual("560", root.Element("nuyen")!.Value);
        Assert.AreEqual("-440", expense.Element("amount")!.Value);
        Assert.AreEqual("Purchased Gear Source label", expense.Element("reason")!.Value);
        Assert.AreEqual("Nuyen", expense.Element("type")!.Value);
        Assert.AreEqual("AddGear", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual(SourceId.ToString("D"), expense.Element("undo")!.Element("objectid")!.Value);
        Assert.AreEqual("2", expense.Element("undo")!.Element("qty")!.Value);
        Assert.AreEqual("unrelated", root.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Reduce_requires_confirmation_and_deletes_only_at_the_exact_stack_boundary()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Reduce,
            Amount: 2m)));

        XElement reduced = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Reduce,
            Amount: 2m,
            ReductionConfirmed: true));
        Assert.AreEqual("3", Gear(reduced, SourceId).Element("qty")!.Value);

        XElement deleted = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Reduce,
            Amount: 5m,
            ReductionConfirmed: true));
        Assert.IsFalse(deleted.Element("gears")!.Elements("gear")
            .Any(item => item.Element("guid")!.Value == SourceId.ToString("D")));
        Assert.AreEqual("unrelated", deleted.Element("customstate")!.Value);
    }

    [TestMethod]
    public void Split_regenerates_recursive_ids_and_preserves_saved_clone_fields()
    {
        XElement root = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Split,
            Amount: 2m));
        XElement[] gear = root.Element("gears")!.Elements("gear").ToArray();
        XElement original = gear.Single(item => item.Element("guid")!.Value == SourceId.ToString("D"));
        XElement clone = gear.Single(item => item.Element("guid")!.Value != SourceId.ToString("D")
            && item.Element("name")!.Value == "Medkit");

        Assert.AreEqual("3", original.Element("qty")!.Value);
        Assert.AreEqual("2", clone.Element("qty")!.Value);
        Assert.AreEqual(original.Element("equipped")!.Value, clone.Element("equipped")!.Value);
        Assert.AreEqual(original.Element("location")!.Value, clone.Element("location")!.Value);
        Assert.AreEqual(original.Element("notes")!.Value, clone.Element("notes")!.Value);
        Assert.AreNotEqual(
            original.Element("children")!.Element("gear")!.Element("guid")!.Value,
            clone.Element("children")!.Element("gear")!.Element("guid")!.Value);
    }

    [TestMethod]
    public void Merge_uses_recursive_legacy_identity_ignores_superficials_and_moves_requested_amount()
    {
        XElement partial = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Merge,
            Amount: 2m,
            MergeTargetGearId: TargetId));
        Assert.AreEqual("3", Gear(partial, SourceId).Element("qty")!.Value);
        Assert.AreEqual("5", Gear(partial, TargetId).Element("qty")!.Value);
        Assert.AreEqual("Target label", Gear(partial, TargetId).Element("gearname")!.Value);
        Assert.AreEqual("Target notes", Gear(partial, TargetId).Element("notes")!.Value);

        XElement complete = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Merge,
            Amount: 5m,
            MergeTargetGearId: TargetId));
        Assert.IsFalse(complete.Element("gears")!.Elements("gear")
            .Any(item => item.Element("guid")!.Value == SourceId.ToString("D")));
        Assert.AreEqual("8", Gear(complete, TargetId).Element("qty")!.Value);
    }

    [TestMethod]
    public void Clone_and_full_removal_fail_closed_when_saved_data_references_the_gear()
    {
        string referencedXml = Xml.Replace(
            "</character>",
            $"<weapons><weapon><parentid>{SourceId:D}</parentid></weapon></weapons></character>",
            StringComparison.Ordinal);

        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            new GearQuantityEditRequest(
                WorkspaceId,
                7,
                SourceId,
                GearQuantityAction.Split,
                Amount: 2m),
            referencedXml));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            new GearQuantityEditRequest(
                WorkspaceId,
                7,
                SourceId,
                GearQuantityAction.Reduce,
                Amount: 5m,
                ReductionConfirmed: true),
            referencedXml));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            new GearQuantityEditRequest(
                WorkspaceId,
                7,
                SourceId,
                GearQuantityAction.Merge,
                Amount: 5m,
                MergeTargetGearId: TargetId),
            referencedXml));

        XElement partial = Apply(new GearQuantityEditRequest(
            WorkspaceId,
            7,
            SourceId,
            GearQuantityAction.Reduce,
            Amount: 2m,
            ReductionConfirmed: true), referencedXml);
        Assert.AreEqual("3", Gear(partial, SourceId).Element("qty")!.Value);
    }

    [TestMethod]
    public void Projector_exposes_only_core_proven_career_quantity_semantics()
    {
        JsonNode section = JsonNode.Parse($$"""
        {
          "gear": [
            {
              "guid": "{{SourceId:D}}",
              "name": "Medkit",
              "quantity": "5",
              "careerEditable": true,
              "quantitySemantics": {
                "quantity": 5,
                "decimalPlaces": 0,
                "minimumIncrement": 1,
                "purchaseUnitCost": 220,
                "purchaseUnitCostExact": true,
                "mergeCandidateGuids": ["{{TargetId:D}}"]
              }
            },
            {
              "guid": "{{TargetId:D}}",
              "name": "Other Medkit",
              "quantity": "3",
              "careerEditable": true,
              "quantitySemantics": {
                "quantity": 3,
                "decimalPlaces": 0,
                "minimumIncrement": 1,
                "purchaseUnitCost": 220,
                "purchaseUnitCostExact": true,
                "mergeCandidateGuids": ["{{SourceId:D}}"]
              }
            }
          ]
        }
        """)!;

        WorkspaceCollectionEditorState state = WorkspaceCollectionEditorProjector.TryProject("gear", section)!;
        WorkspaceGearQuantityLifecycleState lifecycle = state.Items.Single(item => item.Target.ItemId == SourceId.ToString("D")).GearQuantityLifecycle!;
        Assert.IsNotNull(lifecycle);
        Assert.AreEqual(5m, lifecycle.Quantity);
        Assert.AreEqual(220m, lifecycle.PurchaseUnitCost);
        Assert.AreEqual(TargetId, lifecycle.MergeCandidates.Single().GearId);

        ((JsonObject)((JsonArray)section["gear"]!)[0]!).Remove("quantitySemantics");
        Assert.IsNull(WorkspaceCollectionEditorProjector.TryProject("gear", section)!.Items[0].GearQuantityLifecycle);
    }

    private static XElement Apply(GearQuantityEditRequest request, string? xml = null)
        => XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyGearQuantityEdit(xml ?? Xml, request)).Root!;

    private static XElement Gear(XElement root, Guid id)
        => root.Element("gears")!.Elements("gear")
            .Single(item => item.Element("guid")!.Value == id.ToString("D"));

    private static string Xml => $$"""
        <character>
          <created>True</created><nuyen>1000</nuyen><customstate>unrelated</customstate>
          <gears>
            <gear>
              <guid>{{SourceId:D}}</guid><name>Medkit</name><category>Medical</category><rating>2</rating><qty>5</qty>
              <cost>Rating * 100</cost><costfor>1</costfor><discountedcost>False</discountedcost>
              <extra>Trauma</extra><gearname>Source label</gearname><notes>Source notes</notes><equipped>True</equipped><location>locker-a</location>
              <children><gear><guid>73333333-3333-3333-3333-333333333333</guid><name>Refill</name><category>Medical</category><rating>0</rating><qty>2</qty><cost>10</cost><extra /><gearname /><notes /><children /></gear></children>
            </gear>
            <gear>
              <guid>{{TargetId:D}}</guid><name>Medkit</name><category>Medical</category><rating>2</rating><qty>3</qty>
              <cost>Rating * 100</cost><costfor>1</costfor><discountedcost>False</discountedcost>
              <extra>Trauma</extra><gearname>Target label</gearname><notes>Target notes</notes><equipped>False</equipped><location>locker-b</location>
              <children><gear><guid>74444444-4444-4444-4444-444444444444</guid><name>Refill</name><category>Medical</category><rating>0</rating><qty>2</qty><cost>10</cost><extra /><gearname /><notes /><children /></gear></children>
            </gear>
          </gears>
        </character>
        """;
}
