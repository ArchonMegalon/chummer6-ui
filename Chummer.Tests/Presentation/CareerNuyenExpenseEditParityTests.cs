using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerNuyenExpenseEditParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-nuyen-expense-edit-tests");
    private const string ManualId = "65da27db-24a8-4b6e-b42c-30f4bb13a4f8";
    private const string LockedId = "a47497a9-0893-43e1-89cb-fb2dfa803b5d";
    private const string Xml = "<character><created>True</created><nuyen>1000</nuyen><expenses><expense><guid>65da27db-24a8-4b6e-b42c-30f4bb13a4f8</guid><date>2081-05-12T14:30:00</date><amount>-250</amount><reason>Ammo</reason><type>Nuyen</type><refund>True</refund><forcecareervisible>True</forcecareervisible><undo><karmatype>ImproveAttribute</karmatype><nuyentype>ManualSubtract</nuyentype><objectid></objectid><qty>0</qty><extra>keep</extra></undo><custom>keep-manual</custom></expense><expense><guid>a47497a9-0893-43e1-89cb-fb2dfa803b5d</guid><date>2081-05-10T10:00:00</date><amount>-500</amount><reason>Armor</reason><type>Nuyen</type><refund>False</refund><forcecareervisible>False</forcecareervisible><undo><karmatype>ImproveAttribute</karmatype><nuyentype>AddArmor</nuyentype><objectid>armor-id</objectid><qty>0</qty><extra></extra></undo></expense><expense><guid>d1616d91-6848-49bd-a513-9b52d3399787</guid><date>2081-05-01T00:00:00</date><amount>-1</amount><reason>Karma row</reason><type>Karma</type><refund>False</refund></expense></expenses><customstate><value>keep</value></customstate></character>";

    [TestMethod]
    public void Projection_uses_saved_guid_and_exact_manual_amount_authority()
    {
        CareerNuyenExpenseEditorState editor = CareerNuyenExpenseEditorProjector.Project(
            Xml, WorkspaceId, 7);

        Assert.AreEqual(1000m, editor.AvailableNuyen);
        Assert.HasCount(2, editor.Expenses);
        Assert.AreEqual(Guid.Parse(ManualId), editor.Expenses[0].ExpenseId);
        Assert.IsTrue(editor.Expenses[0].AmountEditable);
        Assert.IsFalse(editor.Expenses[1].AmountEditable);
    }

    [TestMethod]
    public void Manual_edit_changes_balance_by_delta_and_preserves_locked_metadata_and_unrelated_xml()
    {
        CareerNuyenExpenseEditorState editor = CareerNuyenExpenseEditorProjector.Project(
            Xml, WorkspaceId, 7);
        CharacterCareerNuyenExpenseEntry selected = editor.Expenses[0];
        XElement root = Apply(new CareerNuyenExpenseEditRequest(
            WorkspaceId,
            7,
            editor.AvailableNuyen,
            selected,
            Amount: -175m,
            Reason: "Less ammo",
            ExpenseDateLocal: new DateTime(2081, 5, 13, 9, 15, 0)));

        Assert.AreEqual("1075", root.Element("nuyen")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").First();
        Assert.AreEqual(ManualId, expense.Element("guid")!.Value);
        Assert.AreEqual("2081-05-13T09:15:00", expense.Element("date")!.Value);
        Assert.AreEqual("-175", expense.Element("amount")!.Value);
        Assert.AreEqual("Less ammo", expense.Element("reason")!.Value);
        Assert.AreEqual("True", expense.Element("refund")!.Value);
        Assert.AreEqual("True", expense.Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualSubtract", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual("keep", expense.Element("undo")!.Element("extra")!.Value);
        Assert.AreEqual("keep-manual", expense.Element("custom")!.Value);
        Assert.AreEqual("keep", root.Element("customstate")!.Element("value")!.Value);
        Assert.AreEqual(LockedId, root.Element("expenses")!.Elements("expense").Skip(1).First().Element("guid")!.Value);
    }

    [TestMethod]
    public void Locked_edit_allows_reason_and_date_but_rejects_amount_or_stale_snapshots()
    {
        CareerNuyenExpenseEditorState editor = CareerNuyenExpenseEditorProjector.Project(
            Xml, WorkspaceId, 7);
        CharacterCareerNuyenExpenseEntry selected = editor.Expenses[1];
        XElement root = Apply(new CareerNuyenExpenseEditRequest(
            WorkspaceId,
            7,
            editor.AvailableNuyen,
            selected,
            selected.Amount,
            "Repaired armor",
            new DateTime(2081, 5, 15, 8, 0, 0)));
        Assert.AreEqual("1000", root.Element("nuyen")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").Skip(1).First();
        Assert.AreEqual("-500", expense.Element("amount")!.Value);
        Assert.AreEqual("Repaired armor", expense.Element("reason")!.Value);

        CareerNuyenExpenseEditRequest request = new(
            WorkspaceId,
            7,
            editor.AvailableNuyen,
            selected,
            -499m,
            selected.Reason,
            selected.ExpenseDateLocal);
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(request));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { Amount = selected.Amount, ExpectedAvailableNuyen = 999m }));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            request with { Amount = selected.Amount, ExpectedExpense = selected with { Reason = "stale" } }));
    }

    [TestMethod]
    public void Duplicate_expense_identity_and_noncareer_runner_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerNuyenExpenseEditorProjector.Project(
            Xml.Replace(LockedId, ManualId), WorkspaceId, 7));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerNuyenExpenseEditorProjector.Project(
            Xml.Replace("<created>True</created>", "<created>False</created>"), WorkspaceId, 7));
    }

    private static XElement Apply(CareerNuyenExpenseEditRequest request)
        => XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerNuyenExpenseEdit(Xml, request)).Root!;
}
