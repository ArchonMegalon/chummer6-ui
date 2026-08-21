using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class SpiritFetteredParityTests
{
    private static readonly Guid SpiritId = Guid.Parse("73111111-7311-7311-7311-731111111111");
    private static readonly CharacterWorkspaceId WorkspaceId = new("spirit-fettered-tests");

    [TestMethod]
    public void Projector_requires_and_preserves_typed_stable_fettering_identity()
    {
        JsonObject semantics = new()
        {
            ["spiritId"] = SpiritId.ToString("D"),
            ["entityType"] = "Spirit",
            ["created"] = false,
            ["fettered"] = false,
            ["force"] = 4,
            ["services"] = 2,
            ["bound"] = true,
            ["spriteFetteringAllowed"] = true,
            ["activationCostExact"] = true,
            ["activationKarmaCost"] = 0,
            ["availableKarma"] = 0,
            ["canFetter"] = true,
            ["canUnfetter"] = false
        };
        JsonObject section = new()
        {
            ["spirits"] = new JsonArray(new JsonObject
            {
                ["guid"] = SpiritId.ToString("D"),
                ["name"] = "Fire Spirit",
                ["fetteringSemantics"] = semantics
            })
        };

        CharacterSpiritFetteringState? state = WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single()
            .SpiritFettering;

        Assert.IsNotNull(state);
        Assert.AreEqual(SpiritId, state.SpiritId);
        Assert.IsTrue(state.CanFetter);

        semantics["spiritId"] = Guid.NewGuid().ToString("D");
        Assert.IsNull(WorkspaceCollectionEditorProjector
            .TryProject("spirits", section)!
            .Items.Single()
            .SpiritFettering);
    }

    [TestMethod]
    public void Creation_fetter_adds_exact_saved_flag_and_magic_improvement_atomically()
    {
        const string xml = """
<character><created>False</created><karma>0</karma><improvements />
  <spirits><spirit><guid>73111111-7311-7311-7311-731111111111</guid><name>Fire Spirit</name><type>Spirit</type><force>4</force><services>2</services><bound>True</bound><fettered>False</fettered><notes>preserve me</notes></spirit></spirits>
</character>
""";
        CharacterSpiritFetteringState expected = new(
            SpiritId, "Spirit", false, false, 4, 2, true, true, true, 0, 0, true, false);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplySpiritFetteredEdit(
            xml,
            new SpiritFetteredEditRequest(WorkspaceId, 5, expected, true))).Root!;

        Assert.AreEqual("True", root.Element("spirits")!.Element("spirit")!.Element("fettered")!.Value);
        Assert.AreEqual("preserve me", root.Element("spirits")!.Element("spirit")!.Element("notes")!.Value);
        XElement improvement = root.Element("improvements")!.Elements("improvement").Single();
        Assert.AreEqual("MAG", improvement.Element("improvedname")!.Value);
        Assert.AreEqual("Attribute", improvement.Element("improvementttype")!.Value);
        Assert.AreEqual("SpiritFettering", improvement.Element("improvementsource")!.Value);
        Assert.AreEqual("-1", improvement.Element("aug")!.Value);
        Assert.AreEqual("0", improvement.Element("augmax")!.Value);
        Assert.IsNull(root.Element("expenses"));
    }

    [TestMethod]
    public void Career_fetter_spends_exact_profile_cost_and_records_legacy_undo_identity()
    {
        const string xml = """
<character><created>True</created><karma>20</karma><karmaspiritfettering>3</karmaspiritfettering><improvements />
  <spirits><spirit><guid>73111111-7311-7311-7311-731111111111</guid><name>Fire Spirit</name><type>Spirit</type><force>4</force><services>2</services><bound>True</bound><fettered>False</fettered></spirit></spirits>
</character>
""";
        CharacterSpiritFetteringState expected = new(
            SpiritId, "Spirit", true, false, 4, 2, true, true, true, 12, 20, true, false);

        XElement root = XDocument.Parse(WorkspaceXmlMutationCatalog.ApplySpiritFetteredEdit(
            xml,
            new SpiritFetteredEditRequest(WorkspaceId, 8, expected, true))).Root!;

        Assert.AreEqual("8", root.Element("karma")!.Value);
        XElement expense = root.Element("expenses")!.Element("expense")!;
        Assert.AreEqual("-12", expense.Element("amount")!.Value);
        Assert.AreEqual("SpiritFettering", expense.Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("AddCyberware", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual(SpiritId.ToString("D"), expense.Element("undo")!.Element("objectid")!.Value);
    }

    [TestMethod]
    public void Mutation_rejects_stale_typed_state_before_touching_xml()
    {
        const string xml = """
<character><created>False</created><karma>0</karma><improvements />
  <spirits><spirit><guid>73111111-7311-7311-7311-731111111111</guid><name>Fire Spirit</name><type>Spirit</type><force>4</force><services>2</services><bound>True</bound><fettered>False</fettered></spirit></spirits>
</character>
""";
        CharacterSpiritFetteringState stale = new(
            SpiritId, "Spirit", false, false, 5, 2, true, true, true, 0, 0, true, false);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplySpiritFetteredEdit(
                xml,
                new SpiritFetteredEditRequest(WorkspaceId, 9, stale, true)));
    }
}
