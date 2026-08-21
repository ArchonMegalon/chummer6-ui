using System.Text.Json.Nodes;
using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class LifestyleIncrementParityTests
{
    private static readonly Guid LifestyleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly CharacterWorkspaceId WorkspaceId = new("lifestyle-increment-tests");

    [TestMethod]
    public void Projector_ExposesExactTypedLifestyleIncrementState()
    {
        JsonObject section = SectionJson(careerMode: true, increments: 4, nuyen: 8_000m, cost: 2_500m);

        CharacterLifestyleIncrementState state = WorkspaceCollectionEditorProjector
            .TryProject("lifestyles", section)!
            .Items.Single()
            .LifestyleIncrement!;

        Assert.AreEqual(LifestyleId, state.LifestyleId);
        Assert.AreEqual(4, state.Increments);
        Assert.AreEqual(CharacterLifestyleIncrementUnit.Month, state.Unit);
        Assert.IsTrue(state.CareerMode);
        Assert.AreEqual(8_000m, state.Nuyen);
        Assert.AreEqual(2_500m, state.TotalIncrementCost);
    }

    [TestMethod]
    public void CreationSetUpdatesMonthsDerivedTotalAndPurchasedWithoutExpense()
    {
        CharacterLifestyleIncrementState expected = State(careerMode: false, increments: 2, nuyenExact: false);
        string mutated = WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
            Xml(careerMode: false, increments: 2, nuyen: null),
            Request(expected, CharacterLifestyleIncrementAction.SetCreation, requested: 100));
        XElement root = XDocument.Parse(mutated).Root!;
        XElement lifestyle = root.Element("lifestyles")!.Element("lifestyle")!;

        Assert.AreEqual("100", lifestyle.Element("months")!.Value);
        Assert.AreEqual("250000", lifestyle.Element("totalcost")!.Value);
        Assert.AreEqual("True", lifestyle.Element("purchased")!.Value);
        Assert.IsNull(root.Element("expenses"));
        Assert.IsNull(root.Element("nuyen"));
        Assert.AreEqual("Untouched", root.Element("customstate")!.Value);
    }

    [TestMethod]
    public void CareerIncreaseWritesExactNuyenExpenseUndoAndDerivedValues()
    {
        CharacterLifestyleIncrementState expected = State(careerMode: true, increments: 4);
        string mutated = WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
            Xml(careerMode: true, increments: 4, nuyen: 8_000m),
            Request(expected, CharacterLifestyleIncrementAction.IncreaseCareer));
        XElement root = XDocument.Parse(mutated).Root!;
        XElement lifestyle = root.Element("lifestyles")!.Element("lifestyle")!;
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("5", lifestyle.Element("months")!.Value);
        Assert.AreEqual("12500", lifestyle.Element("totalcost")!.Value);
        Assert.AreEqual("False", lifestyle.Element("purchased")!.Value);
        Assert.AreEqual("5500", root.Element("nuyen")!.Value);
        Assert.AreEqual("-2500", expense.Element("amount")!.Value);
        Assert.AreEqual("Purchased Lifestyle Low", expense.Element("reason")!.Value);
        Assert.AreEqual("Nuyen", expense.Element("type")!.Value);
        Assert.AreEqual("IncreaseLifestyle", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual(LifestyleId.ToString("D"), expense.Element("undo")!.Element("objectid")!.Value);
    }

    [TestMethod]
    public void CareerDecreaseAllowsNegativeAndWritesZeroExpenseWithoutUndo()
    {
        CharacterLifestyleIncrementState expected = State(careerMode: true, increments: 0);
        string mutated = WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
            Xml(careerMode: true, increments: 0, nuyen: 8_000m),
            Request(expected, CharacterLifestyleIncrementAction.DecreaseCareer));
        XElement root = XDocument.Parse(mutated).Root!;
        XElement lifestyle = root.Element("lifestyles")!.Element("lifestyle")!;
        XElement expense = root.Element("expenses")!.Elements("expense").Single();

        Assert.AreEqual("-1", lifestyle.Element("months")!.Value);
        Assert.AreEqual("-2500", lifestyle.Element("totalcost")!.Value);
        Assert.AreEqual("8000", root.Element("nuyen")!.Value);
        Assert.AreEqual("0", expense.Element("amount")!.Value);
        Assert.AreEqual("Decremented Lifestyle Low", expense.Element("reason")!.Value);
        Assert.IsNull(expense.Element("undo"));
    }

    [TestMethod]
    public void MutationFailsClosedOnInsufficientFundsStaleStateAndDuplicateIdentity()
    {
        CharacterLifestyleIncrementState current = State(careerMode: true, increments: 4, nuyen: 2_000m);
        string xml = Xml(careerMode: true, increments: 4, nuyen: 2_000m);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
                xml,
                Request(current, CharacterLifestyleIncrementAction.IncreaseCareer)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
                xml,
                Request(current with { Increments = 3 }, CharacterLifestyleIncrementAction.DecreaseCareer)));
        string duplicate = xml.Replace(
            "</lifestyles>",
            $"<lifestyle><guid>{LifestyleId:D}</guid><months>4</months></lifestyle></lifestyles>",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyLifestyleIncrementEdit(
                duplicate,
                Request(current, CharacterLifestyleIncrementAction.DecreaseCareer)));
    }

    private static LifestyleIncrementEditRequest Request(
        CharacterLifestyleIncrementState expected,
        CharacterLifestyleIncrementAction action,
        int? requested = null)
        => new(WorkspaceId, 7, LifestyleId, action, requested, expected);

    private static CharacterLifestyleIncrementState State(
        bool careerMode,
        int increments,
        decimal nuyen = 8_000m,
        bool nuyenExact = true)
        => new(
            LifestyleId,
            increments,
            CharacterLifestyleIncrementUnit.Month,
            careerMode,
            nuyenExact ? nuyen : 0m,
            nuyenExact,
            2_500m,
            TotalIncrementCostExact: true,
            "Low");

    private static string Xml(bool careerMode, int increments, decimal? nuyen)
        => $"""
            <character><created>{careerMode}</created>{(nuyen.HasValue ? $"<nuyen>{nuyen.Value}</nuyen>" : string.Empty)}
              <lifestyles><lifestyle>
                <guid>{LifestyleId:D}</guid><name>Low</name><baselifestyle>Low</baselifestyle>
                <months>{increments}</months><increment>Month</increment>
                <totalmonthlycost>2500</totalmonthlycost><totalcost>{increments * 2500}</totalcost><purchased>False</purchased>
              </lifestyle></lifestyles><customstate>Untouched</customstate>
            </character>
            """;

    private static JsonObject SectionJson(bool careerMode, int increments, decimal nuyen, decimal cost)
        => new()
        {
            ["lifestyles"] = new JsonArray
            {
                new JsonObject
                {
                    ["guid"] = LifestyleId.ToString("D"),
                    ["name"] = "Low",
                    ["incrementState"] = new JsonObject
                    {
                        ["lifestyleId"] = LifestyleId.ToString("D"),
                        ["increments"] = increments,
                        ["unit"] = (int)CharacterLifestyleIncrementUnit.Month,
                        ["careerMode"] = careerMode,
                        ["nuyen"] = nuyen,
                        ["nuyenExact"] = true,
                        ["totalIncrementCost"] = cost,
                        ["totalIncrementCostExact"] = true,
                        ["displayName"] = "Low"
                    }
                }
            }
        };
}
