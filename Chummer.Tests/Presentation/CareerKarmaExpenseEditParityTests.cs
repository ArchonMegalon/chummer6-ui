using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerKarmaExpenseEditParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-karma-expense-edit-tests");
    private const string ManualId = "65da27db-24a8-4b6e-b42c-30f4bb13a4f8";
    private const string LockedId = "a47497a9-0893-43e1-89cb-fb2dfa803b5d";
    private const string NuyenId = "d1616d91-6848-49bd-a513-9b52d3399787";
    private const string Xml = "<character><created>True</created><karma>10</karma><expenses><expense><guid>65da27db-24a8-4b6e-b42c-30f4bb13a4f8</guid><date>2081-05-12T14:30:00</date><amount>1.9</amount><reason>Run reward</reason><type>karma</type><refund>True</refund><forcecareervisible>True</forcecareervisible><undo><karmatype>ManualAdd</karmatype><nuyentype>ManualSubtract</nuyentype><objectid></objectid><qty>0</qty><extra>keep</extra></undo><custom>keep-manual</custom></expense><expense><guid>a47497a9-0893-43e1-89cb-fb2dfa803b5d</guid><date>2081-05-10T10:00:00</date><amount>-5</amount><reason>Attribute</reason><refund>False</refund><forcecareervisible>False</forcecareervisible><undo><karmatype>ImproveAttribute</karmatype><objectid>attribute-id</objectid><extra>keep-locked</extra></undo></expense><expense><guid>d1616d91-6848-49bd-a513-9b52d3399787</guid><date>2081-05-01T00:00:00</date><amount>-250</amount><reason>Nuyen row</reason><type>nUyEn</type><refund>False</refund><undo><karmatype>ManualAdd</karmatype><nuyentype>AddGear</nuyentype><extra>keep-nuyen</extra></undo><custom>keep-nuyen-row</custom></expense></expenses><customstate><expenses><expense><guid>65da27db-24a8-4b6e-b42c-30f4bb13a4f8</guid><sentinel>nested-keep</sentinel></expense></expenses></customstate></character>";

    [TestMethod]
    public void Projection_classifies_every_non_Nuyen_row_and_captures_full_CAS_authority()
    {
        CareerKarmaExpenseEditorState editor = CareerKarmaExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            7);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.AreEqual(10, editor.AvailableKarma);
        Assert.HasCount(2, editor.Expenses);
        Assert.AreEqual(Guid.Parse(ManualId), editor.Expenses[0].ExpenseId);
        Assert.AreEqual(1.9m, editor.Expenses[0].Amount);
        Assert.AreEqual("Run reward", editor.Expenses[0].Reason);
        Assert.IsTrue(editor.Expenses[0].Refund);
        Assert.IsTrue(editor.Expenses[0].ForceCareerVisible);
        Assert.IsTrue(editor.Expenses[0].KarmaUndoTypeElementPresent);
        Assert.AreEqual("ManualAdd", editor.Expenses[0].RawKarmaUndoType);
        Assert.IsTrue(editor.Expenses[0].AmountEditable);
        Assert.AreEqual(Guid.Parse(LockedId), editor.Expenses[1].ExpenseId);
        Assert.IsFalse(editor.Expenses[1].AmountEditable);
    }

    [TestMethod]
    public void Fractional_edit_uses_Core_integer_delta_and_preserves_unrelated_XML()
    {
        CareerKarmaExpenseEditorState editor = Project(Xml);
        CharacterCareerKarmaExpenseEntry selected = editor.Expenses[0];
        XElement root = Apply(
            Xml,
            new CareerKarmaExpenseEditRequest(
                WorkspaceId,
                7,
                editor.AvailableKarma,
                selected,
                Amount: 2.1m,
                Reason: "Corrected reward",
                ExpenseDateLocal: new DateTime(2081, 5, 13, 9, 15, 0)));

        Assert.AreEqual("11", root.Element("karma")!.Value);
        XElement expense = root.Element("expenses")!.Elements("expense").First();
        Assert.AreEqual("2", expense.Element("amount")!.Value);
        Assert.AreEqual("2081-05-13T09:15:00", expense.Element("date")!.Value);
        Assert.AreEqual("Corrected reward", expense.Element("reason")!.Value);
        Assert.AreEqual("True", expense.Element("refund")!.Value);
        Assert.AreEqual("True", expense.Element("forcecareervisible")!.Value);
        Assert.AreEqual("ManualAdd", expense.Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("ManualSubtract", expense.Element("undo")!.Element("nuyentype")!.Value);
        Assert.AreEqual("keep", expense.Element("undo")!.Element("extra")!.Value);
        Assert.AreEqual("keep-manual", expense.Element("custom")!.Value);

        XElement nuyen = root.Element("expenses")!.Elements("expense").Single(
            value => value.Element("guid")!.Value == NuyenId);
        Assert.AreEqual("-250", nuyen.Element("amount")!.Value);
        Assert.AreEqual("keep-nuyen", nuyen.Element("undo")!.Element("extra")!.Value);
        Assert.AreEqual("keep-nuyen-row", nuyen.Element("custom")!.Value);
        Assert.AreEqual(
            "nested-keep",
            root.Element("customstate")!.Element("expenses")!.Element("expense")!.Element("sentinel")!.Value);
    }

    [TestMethod]
    public void Locked_ImproveAttribute_allows_date_and_reason_only()
    {
        CareerKarmaExpenseEditorState editor = Project(Xml);
        CharacterCareerKarmaExpenseEntry selected = editor.Expenses[1];
        XElement root = Apply(
            Xml,
            Request(editor, selected) with
            {
                Reason = "Corrected attribute label",
                ExpenseDateLocal = new DateTime(2081, 5, 15, 8, 0, 0)
            });

        XElement expense = root.Element("expenses")!.Elements("expense").Skip(1).First();
        Assert.AreEqual("10", root.Element("karma")!.Value);
        Assert.AreEqual("-5", expense.Element("amount")!.Value);
        Assert.AreEqual("Corrected attribute label", expense.Element("reason")!.Value);
        Assert.AreEqual("2081-05-15T08:00:00", expense.Element("date")!.Value);
        Assert.AreEqual("ImproveAttribute", expense.Element("undo")!.Element("karmatype")!.Value);
        Assert.AreEqual("keep-locked", expense.Element("undo")!.Element("extra")!.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            Xml,
            Request(editor, selected) with { Amount = -4m }));
    }

    [TestMethod]
    public void Raw_karmatype_presence_and_text_survive_projection_and_edit()
    {
        (string UndoXml, bool Present, string? Raw, bool Editable)[] cases =
        [
            ("<undo><extra>absent</extra></undo>", false, null, false),
            ("<undo><karmatype></karmatype><extra>blank</extra></undo>", true, string.Empty, true),
            ("<undo><karmatype>manualadd</karmatype><extra>case</extra></undo>", true, "manualadd", true),
            ("<undo><karmatype>12</karmatype><extra>numeric</extra></undo>", true, "12", true)
        ];

        foreach ((string undoXml, bool present, string? raw, bool editable) in cases)
        {
            string xml = SingleExpenseXml(undoXml);
            CareerKarmaExpenseEditorState editor = Project(xml);
            CharacterCareerKarmaExpenseEntry selected = editor.Expenses.Single();
            Assert.AreEqual(present, selected.KarmaUndoTypeElementPresent);
            Assert.AreEqual(raw, selected.RawKarmaUndoType);
            Assert.AreEqual(editable, selected.AmountEditable);

            decimal requestedAmount = editable ? 6m : selected.Amount;
            XElement root = Apply(
                xml,
                Request(editor, selected) with
                {
                    Amount = requestedAmount,
                    Reason = "Edited",
                    ExpenseDateLocal = new DateTime(2081, 6, 1)
                });
            XElement undo = root.Element("expenses")!.Element("expense")!.Element("undo")!;
            XElement[] karmaTypes = undo.Elements("karmatype").ToArray();
            Assert.HasCount(present ? 1 : 0, karmaTypes);
            if (present)
            {
                Assert.AreEqual(raw, karmaTypes[0].Value);
            }
            Assert.IsNotNull(undo.Element("extra"));
        }
    }

    [TestMethod]
    public void Duplicate_malformed_stale_and_noncareer_authority_fail_closed()
    {
        CareerKarmaExpenseEditorState editor = Project(Xml);
        CharacterCareerKarmaExpenseEntry selected = editor.Expenses[0];
        CareerKarmaExpenseEditRequest request = Request(editor, selected);

        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(NuyenId, ManualId)));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(ManualId, "not-a-guid")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("2081-05-12T14:30:00", "not-a-date")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<amount>1.9</amount>", "<amount>not-an-amount</amount>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<created>True</created>", "<created>False</created>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerKarmaExpenseEditorProjector.Project(
            Xml,
            WorkspaceId,
            0));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerKarmaExpenseEditorProjector.Project(
            Xml.Replace("<character>", "<runner>").Replace("</character>", "</runner>"),
            WorkspaceId,
            7));
        Assert.ThrowsExactly<InvalidOperationException>(() => CareerKarmaExpenseEditorProjector.Project(
            Xml.Replace("<character>", "<character xmlns=\"urn:not-chummer\">"),
            WorkspaceId,
            7));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            Xml,
            request with { ExpectedAvailableKarma = 9 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
            Xml,
            request with { ExpectedExpense = selected with { Reason = "stale" } }));
    }

    [TestMethod]
    public void Every_expected_expense_field_participates_in_compare_and_swap()
    {
        CareerKarmaExpenseEditorState editor = Project(Xml);
        CharacterCareerKarmaExpenseEntry selected = editor.Expenses[0];
        CharacterCareerKarmaExpenseEntry[] staleSnapshots =
        [
            selected with { ExpenseId = Guid.Parse(LockedId) },
            selected with { ExpenseDateLocal = selected.ExpenseDateLocal.AddSeconds(1) },
            selected with { Amount = selected.Amount + 1m },
            selected with { Reason = selected.Reason + " stale" },
            selected with { Refund = !selected.Refund },
            selected with { ForceCareerVisible = !selected.ForceCareerVisible },
            selected with { KarmaUndoTypeElementPresent = false },
            selected with { RawKarmaUndoType = "ManualSubtract" },
            selected with { AmountEditable = false }
        ];

        foreach (CharacterCareerKarmaExpenseEntry stale in staleSnapshots)
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => Apply(
                Xml,
                Request(editor, selected) with { ExpectedExpense = stale }));
        }
    }

    [TestMethod]
    public void Duplicate_undo_karmatype_type_and_root_authority_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            SingleExpenseXml("<undo></undo><undo></undo>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            SingleExpenseXml("<undo><karmatype>ManualAdd</karmatype><karmatype>ManualAdd</karmatype></undo>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            SingleExpenseXml("<undo><karmatype>ManualAdd</karmatype></undo>")
                .Replace("<type>Karma</type>", "<type>Karma</type><type>Karma</type>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<karma>10</karma>", "<karma>10</karma><karma>10</karma>")));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<expenses>", "<expenses></expenses><expenses>", StringComparison.Ordinal)));
    }

    private static CareerKarmaExpenseEditorState Project(string xml)
        => CareerKarmaExpenseEditorProjector.Project(xml, WorkspaceId, 7);

    private static CareerKarmaExpenseEditRequest Request(
        CareerKarmaExpenseEditorState editor,
        CharacterCareerKarmaExpenseEntry expense)
        => new(
            WorkspaceId,
            editor.ContentRevision,
            editor.AvailableKarma,
            expense,
            expense.Amount,
            expense.Reason,
            expense.ExpenseDateLocal);

    private static XElement Apply(string xml, CareerKarmaExpenseEditRequest request)
        => XDocument.Parse(WorkspaceXmlMutationCatalog.ApplyCareerKarmaExpenseEdit(xml, request)).Root!;

    private static string SingleExpenseXml(string undoXml)
        => $"<character><created>True</created><karma>10</karma><expenses><expense><guid>{ManualId}</guid><date>2081-05-12T14:30:00</date><amount>5</amount><reason>Raw</reason><type>Karma</type><refund>False</refund><forcecareervisible>False</forcecareervisible>{undoXml}<custom>keep</custom></expense></expenses></character>";
}
