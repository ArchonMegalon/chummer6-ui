using System.Xml.Linq;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CareerCalendarParityTests
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("career-calendar-tests");
    private const string LatestId = "11111111-1111-1111-1111-111111111111";
    private const string EarlierId = "22222222-2222-2222-2222-222222222222";
    private const string NewId = "33333333-3333-3333-3333-333333333333";
    private const string Xml = "<character><created>True</created><name>Calendar Runner</name><calendar><week><guid>11111111-1111-1111-1111-111111111111</guid><year>2081</year><week>12</week><notes>Run night</notes><notesColor>#A52A2A</notesColor><custom>keep-latest</custom></week><week><guid>22222222-2222-2222-2222-222222222222</guid><year>2081</year><week>11</week><notes>Legwork</notes><custom>keep-earlier</custom></week></calendar><customstate><calendar><week><guid>33333333-3333-3333-3333-333333333333</guid><sentinel>nested-keep</sentinel></week></calendar></customstate></character>";

    [TestMethod]
    public void Projection_uses_stable_week_identity_descending_order_and_fail_closed_start_shift()
    {
        CareerCalendarEditorState editor = Project(Xml);

        Assert.AreEqual(WorkspaceId, editor.WorkspaceId);
        Assert.AreEqual(7, editor.ContentRevision);
        Assert.HasCount(2, editor.Weeks);
        Assert.AreEqual(Guid.Parse(LatestId), editor.Weeks[0].Identity.WeekId);
        Assert.AreEqual(2081, editor.Weeks[0].Year);
        Assert.AreEqual(12, editor.Weeks[0].Week);
        Assert.AreEqual("Run night", editor.Weeks[0].Notes);
        Assert.AreEqual("#A52A2A", editor.Weeks[0].NotesColor);
        Assert.AreEqual(
            CharacterCareerCalendarRules.DefaultNotesColor,
            editor.Weeks[1].NotesColor);
        Assert.IsFalse(editor.CanChangeStartingDate);
        StringAssert.Contains(editor.ChangeStartingDateBlocker, "ignores its requested offset");
    }

    [TestMethod]
    public void Add_uses_week_after_latest_and_preserves_every_existing_record_and_nested_sibling()
    {
        CareerCalendarEditorState editor = Project(Xml);
        string result = WorkspaceXmlMutationCatalog.ApplyCareerCalendarAdd(
            Xml,
            new CareerCalendarAddRequest(
                WorkspaceId,
                editor.ContentRevision,
                new CharacterCareerCalendarWeekIdentity(Guid.Parse(NewId)),
                RequestedFirstYear: 2000,
                RequestedFirstWeek: 1));

        XElement root = XDocument.Parse(result).Root!;
        XElement added = root.Element("calendar")!.Elements("week").Single(
            candidate => candidate.Element("guid")!.Value == NewId);
        Assert.AreEqual("2081", added.Element("year")!.Value);
        Assert.AreEqual("13", added.Element("week")!.Value);
        Assert.AreEqual(string.Empty, added.Element("notes")!.Value);
        Assert.AreEqual("Chocolate", added.Element("notesColor")!.Value);
        Assert.AreEqual(
            "keep-latest",
            root.Element("calendar")!.Elements("week").First().Element("custom")!.Value);
        Assert.AreEqual(
            "nested-keep",
            root.Element("customstate")!.Element("calendar")!.Element("week")!.Element("sentinel")!.Value);
    }

    [TestMethod]
    public void Empty_calendar_add_honors_first_week_selector_bounds_and_long_year()
    {
        const string empty = "<character><created>True</created><name>Empty</name></character>";
        string result = WorkspaceXmlMutationCatalog.ApplyCareerCalendarAdd(
            empty,
            new CareerCalendarAddRequest(
                WorkspaceId,
                4,
                new CharacterCareerCalendarWeekIdentity(Guid.Parse(NewId)),
                RequestedFirstYear: 2026,
                RequestedFirstWeek: 53));

        XElement added = XDocument.Parse(result).Root!.Element("calendar")!.Element("week")!;
        Assert.AreEqual("2026", added.Element("year")!.Value);
        Assert.AreEqual("53", added.Element("week")!.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerCalendarAdd(
                empty,
                new CareerCalendarAddRequest(
                    WorkspaceId,
                    4,
                    new CharacterCareerCalendarWeekIdentity(Guid.Parse(NewId)),
                    RequestedFirstYear: 1999,
                    RequestedFirstWeek: 1)));
    }

    [TestMethod]
    public void Edit_changes_only_notes_and_color_for_exact_source_revision()
    {
        CareerCalendarEditorState editor = Project(Xml);
        CharacterCareerCalendarWeekState selected = editor.Weeks[0];
        string result = WorkspaceXmlMutationCatalog.ApplyCareerCalendarEdit(
            Xml,
            new CareerCalendarEditRequest(
                WorkspaceId,
                editor.ContentRevision,
                selected,
                selected.SourceRevision,
                "After-run complete",
                "Chocolate"));

        XElement root = XDocument.Parse(result).Root!;
        XElement changed = root.Element("calendar")!.Elements("week").Single(
            candidate => candidate.Element("guid")!.Value == LatestId);
        Assert.AreEqual("2081", changed.Element("year")!.Value);
        Assert.AreEqual("12", changed.Element("week")!.Value);
        Assert.AreEqual("After-run complete", changed.Element("notes")!.Value);
        Assert.AreEqual("Chocolate", changed.Element("notesColor")!.Value);
        Assert.AreEqual("keep-latest", changed.Element("custom")!.Value);
        Assert.AreEqual(
            "keep-earlier",
            root.Element("calendar")!.Elements("week").Single(
                candidate => candidate.Element("guid")!.Value == EarlierId).Element("custom")!.Value);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerCalendarEdit(
                Xml,
                new CareerCalendarEditRequest(
                    WorkspaceId,
                    editor.ContentRevision,
                    selected,
                    new string('0', 64),
                    "stale",
                    "Chocolate")));
    }

    [TestMethod]
    public void Delete_requires_confirmation_and_removes_only_the_exact_guid()
    {
        CareerCalendarEditorState editor = Project(Xml);
        CharacterCareerCalendarWeekState selected = editor.Weeks[1];
        CareerCalendarDeleteRequest request = new(
            WorkspaceId,
            editor.ContentRevision,
            selected,
            selected.SourceRevision,
            Confirmed: false);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WorkspaceXmlMutationCatalog.ApplyCareerCalendarDelete(Xml, request));

        string result = WorkspaceXmlMutationCatalog.ApplyCareerCalendarDelete(
            Xml,
            request with { Confirmed = true });
        XElement root = XDocument.Parse(result).Root!;
        XElement[] weeks = root.Element("calendar")!.Elements("week").ToArray();
        Assert.HasCount(1, weeks);
        Assert.AreEqual(LatestId, weeks[0].Element("guid")!.Value);
        Assert.AreEqual("keep-latest", weeks[0].Element("custom")!.Value);
        Assert.AreEqual(
            "nested-keep",
            root.Element("customstate")!.Element("calendar")!.Element("week")!.Element("sentinel")!.Value);
    }

    [TestMethod]
    public void Malformed_duplicate_identity_coordinate_and_creation_dossier_fail_closed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace(EarlierId, LatestId, StringComparison.Ordinal)));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<week>11</week>", "<week>12</week>", StringComparison.Ordinal)));
        Assert.ThrowsExactly<InvalidOperationException>(() => Project(
            Xml.Replace("<created>True</created>", "<created>False</created>", StringComparison.Ordinal)));
    }

    private static CareerCalendarEditorState Project(string xml)
        => CareerCalendarEditorProjector.Project(xml, WorkspaceId, 7);
}
