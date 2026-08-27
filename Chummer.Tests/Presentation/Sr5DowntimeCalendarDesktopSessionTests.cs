using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;

internal static class Program
{
    private static readonly CharacterWorkspaceId WorkspaceId = new("downtime-calendar-tests");
    private const string FirstId = "11111111-1111-1111-1111-111111111111";
    private const string SecondId = "22222222-2222-2222-2222-222222222222";
    private const string NewId = "33333333-3333-3333-3333-333333333333";
    private const string Xml = "<character><created>True</created><calendar><week><guid>11111111-1111-1111-1111-111111111111</guid><year>2081</year><week>12</week><notes>Run night</notes><notesColor>#A52A2A</notesColor><custom>preserve</custom></week><week><guid>22222222-2222-2222-2222-222222222222</guid><year>2081</year><week>11</week><notes>Legwork</notes></week></calendar></character>";

    private static int Main()
    {
        ExactPreviewConfirmCheckpointAndAdd();
        EditAndDeleteUseExactCoreAuthority();
        RestartAndBindingDriftFailClosed();
        PostconditionRejectsUnexpectedMutation();
        Console.WriteLine("SR5 Downtime Calendar Presentation tests passed: 4");
        return 0;
    }

    private static void ExactPreviewConfirmCheckpointAndAdd()
    {
        CareerCalendarEditorState editor = Project(Xml, 7);
        var session = new Sr5DowntimeCalendarDesktopSession();
        Sr5CareerWizardBinding binding = Binding(editor, savedRevision: 7);
        session.Bind(binding, editor);
        Require(session.TryPreviewAdd(Guid.Parse(NewId), 2000, 1, out _), "add preview");
        Sr5DowntimeCalendarPreview preview = session.State.Preview!;
        Require(preview.Year == 2081 && preview.Week == 13, "next week comes from Core");

        byte[] serialized = Sr5DowntimeCalendarDesktopSession.SerializeCheckpoint(
            session.CreateCheckpoint());
        Require(Sr5DowntimeCalendarDesktopSession.TryDeserializeCheckpoint(
            serialized,
            out Sr5DowntimeCalendarCheckpoint? checkpoint), "checkpoint decode");
        Sr5DowntimeCalendarDesktopState restored = new Sr5DowntimeCalendarDesktopSession()
            .Bind(binding, editor, checkpoint);
        Require(restored.Resume.Restored && !restored.Confirmed && !restored.CanApply,
            "restart restores preview but never confirmation");
        RequireThrows<InvalidOperationException>(
            () =>
            {
                var unconfirmed = new Sr5DowntimeCalendarDesktopSession();
                unconfirmed.Bind(binding, editor);
                _ = unconfirmed.CreateAddRequest();
            },
            "request without confirmation");

        Require(session.TryConfirm(preview.PreviewDigest), "explicit confirmation");
        CareerCalendarAddRequest request = session.CreateAddRequest();
        Require(request.ExpectedCalendarRevision == editor.CalendarRevision, "calendar CAS");
        Require(request.ExpectedSourceAuthorityDigest == editor.SourceAuthorityDigest, "source CAS");
        string mutated = CareerCalendarMutation.Add(Xml, request);
        CareerCalendarEditorState after = Project(mutated, 8);
        Require(Sr5DowntimeCalendarDesktopSession.PostconditionMatches(preview, editor, after),
            "typed add postcondition");
        Require(mutated.Contains("<custom>preserve</custom>", StringComparison.Ordinal),
            "unknown sibling preserved");
    }

    private static void EditAndDeleteUseExactCoreAuthority()
    {
        CareerCalendarEditorState editor = Project(Xml, 7);
        var edit = new Sr5DowntimeCalendarDesktopSession();
        edit.Bind(Binding(editor, 7), editor);
        Require(edit.TryPreviewEdit(Guid.Parse(FirstId), "Downtime healed", "Chocolate", out _),
            "edit preview");
        Sr5DowntimeCalendarPreview editPreview = edit.State.Preview!;
        Require(edit.TryConfirm(editPreview.PreviewDigest), "edit confirm");
        string editedXml = CareerCalendarMutation.Edit(Xml, edit.CreateEditRequest());
        CareerCalendarEditorState edited = Project(editedXml, 8);
        Require(Sr5DowntimeCalendarDesktopSession.PostconditionMatches(
            editPreview,
            editor,
            edited), "typed edit postcondition");

        var delete = new Sr5DowntimeCalendarDesktopSession();
        delete.Bind(Binding(editor, 7), editor);
        Require(delete.TryPreviewDelete(Guid.Parse(SecondId), out _), "delete preview");
        Sr5DowntimeCalendarPreview deletePreview = delete.State.Preview!;
        Require(delete.TryConfirm(deletePreview.PreviewDigest), "delete confirm");
        string deletedXml = CareerCalendarMutation.Delete(Xml, delete.CreateDeleteRequest());
        CareerCalendarEditorState deleted = Project(deletedXml, 8);
        Require(Sr5DowntimeCalendarDesktopSession.PostconditionMatches(
            deletePreview,
            editor,
            deleted), "typed delete postcondition");

        CareerCalendarEditRequest stale = edit.CreateEditRequest() with
        {
            ExpectedLogicalRevision = new string('0', 64)
        };
        RequireThrows<InvalidOperationException>(
            () => CareerCalendarMutation.Edit(Xml, stale),
            "stale logical revision");
    }

    private static void RestartAndBindingDriftFailClosed()
    {
        CareerCalendarEditorState editor = Project(Xml, 7);
        var session = new Sr5DowntimeCalendarDesktopSession();
        Sr5CareerWizardBinding binding = Binding(editor, 7);
        session.Bind(binding, editor);
        Require(session.TryPreviewDelete(Guid.Parse(FirstId), out _), "preview for drift");
        Sr5DowntimeCalendarCheckpoint checkpoint = session.CreateCheckpoint();

        Sr5DowntimeCalendarDesktopState stale = new Sr5DowntimeCalendarDesktopSession().Bind(
            binding with { ContentDigest = H('c') },
            editor,
            checkpoint);
        Require(stale.Resume.InvalidationReason
                == Sr5DowntimeCalendarCheckpointInvalidationReasons.SnapshotChanged,
            "content drift invalidates restart");
        RequireThrows<InvalidOperationException>(
            () => new Sr5DowntimeCalendarDesktopSession().Bind(
                binding with { SourceDigest = H('d') },
                editor),
            "foreign source authority");
        RequireThrows<InvalidOperationException>(
            () => new Sr5DowntimeCalendarDesktopSession().Bind(
                binding with { SavedRevision = 6 },
                editor),
            "dirty/unsaved binding");
    }

    private static void PostconditionRejectsUnexpectedMutation()
    {
        CareerCalendarEditorState editor = Project(Xml, 7);
        var session = new Sr5DowntimeCalendarDesktopSession();
        session.Bind(Binding(editor, 7), editor);
        Require(session.TryPreviewEdit(Guid.Parse(FirstId), "Expected", "Chocolate", out _),
            "postcondition preview");
        Sr5DowntimeCalendarPreview preview = session.State.Preview!;

        var other = new Sr5DowntimeCalendarDesktopSession();
        other.Bind(Binding(editor, 7), editor);
        Require(other.TryPreviewEdit(Guid.Parse(FirstId), "Different", "Chocolate", out _),
            "different preview");
        Require(other.TryConfirm(other.State.Preview!.PreviewDigest), "different confirm");
        CareerCalendarEditorState unexpected = Project(
            CareerCalendarMutation.Edit(Xml, other.CreateEditRequest()),
            8);
        Require(!Sr5DowntimeCalendarDesktopSession.PostconditionMatches(
            preview,
            editor,
            unexpected), "unexpected state must not verify");
    }

    private static CareerCalendarEditorState Project(string xml, long revision)
        => CareerCalendarEditorProjector.Project(xml, WorkspaceId, revision);

    private static Sr5CareerWizardBinding Binding(
        CareerCalendarEditorState editor,
        long savedRevision)
        => new(
            editor.WorkspaceId.Value,
            editor.ContentRevision,
            savedRevision,
            "sr5",
            Sr5DowntimeCalendarDesktopSession.RuntimeFingerprint,
            "sha256:" + editor.SourceAuthorityDigest,
            H('a'));

    private static string H(char value) => "sha256:" + new string(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
