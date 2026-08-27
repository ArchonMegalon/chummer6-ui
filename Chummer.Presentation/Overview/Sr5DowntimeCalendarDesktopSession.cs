using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public static class Sr5DowntimeCalendarSchemas
{
    public const string CheckpointV1 = "chummer.sr5-downtime-calendar.checkpoint.v1";
    public const string PreviewV1 = "chummer.sr5-downtime-calendar.preview.v1";
    public const string RuntimeV1 = "chummer.sr5-downtime-calendar.runtime.v1";
}

public static class Sr5DowntimeCalendarCheckpointInvalidationReasons
{
    public const string Invalid = "downtime-calendar-checkpoint-invalid";
    public const string WorkspaceChanged = "downtime-calendar-checkpoint-workspace-changed";
    public const string WorkspaceRevisionChanged = "downtime-calendar-checkpoint-revision-changed";
    public const string SnapshotChanged = "downtime-calendar-checkpoint-snapshot-changed";
    public const string PreviewUnavailable = "downtime-calendar-checkpoint-preview-unavailable";
}

public enum Sr5DowntimeCalendarOperation
{
    Add,
    Edit,
    Delete
}

public sealed record Sr5DowntimeCalendarPreview(
    string Schema,
    Sr5DowntimeCalendarOperation Operation,
    Guid WeekId,
    int Year,
    int Week,
    string Notes,
    string NotesColor,
    string ExpectedCalendarRevision,
    string ExpectedLogicalRevision,
    string ExpectedSourceRevision,
    string Summary,
    string PreviewDigest);

/// <summary>
/// Restart state contains one deterministic review preview. Confirmation is deliberately absent:
/// a restored preview must be reviewed and confirmed again before a request can be created.
/// </summary>
public sealed record Sr5DowntimeCalendarCheckpoint(
    string Schema,
    string WorkspaceId,
    long WorkspaceRevision,
    string SnapshotDigest,
    Sr5DowntimeCalendarPreview Preview);

public sealed record Sr5DowntimeCalendarResume(bool Restored, string? InvalidationReason);

public sealed record Sr5DowntimeCalendarDesktopState(
    Sr5CareerWizardBinding Binding,
    CareerCalendarEditorState Editor,
    string SnapshotDigest,
    Sr5DowntimeCalendarPreview? Preview,
    bool Confirmed,
    Sr5DowntimeCalendarResume Resume)
{
    public bool CanApply => Preview is not null && Confirmed;
}

/// <summary>
/// Renderer-neutral SR5 Downtime Calendar review session. Core owns calendar legality;
/// Presentation owns deterministic preview, explicit confirmation, and restart invalidation.
/// </summary>
public sealed class Sr5DowntimeCalendarDesktopSession
{
    private const int MaximumCheckpointBytes = 16 * 1024;
    private Sr5DowntimeCalendarDesktopState? _state;

    public static string RuntimeFingerprint { get; } = ComputeDigest(string.Join(
        '\0',
        Sr5DowntimeCalendarSchemas.RuntimeV1,
        nameof(Sr5DowntimeCalendarOperation.Add),
        nameof(Sr5DowntimeCalendarOperation.Edit),
        nameof(Sr5DowntimeCalendarOperation.Delete),
        Sr5DowntimeCalendarSchemas.PreviewV1,
        Sr5DowntimeCalendarSchemas.CheckpointV1));

    public Sr5DowntimeCalendarDesktopState State
        => _state ?? throw new InvalidOperationException(
            "Bind an exact Downtime Calendar projection before reading state.");

    public Sr5DowntimeCalendarDesktopState Bind(
        Sr5CareerWizardBinding binding,
        CareerCalendarEditorState editor,
        Sr5DowntimeCalendarCheckpoint? checkpoint = null)
    {
        ValidateBinding(binding, editor);
        string snapshotDigest = ComputeSnapshotDigest(binding, editor);
        _state = new Sr5DowntimeCalendarDesktopState(
            binding,
            editor,
            snapshotDigest,
            Preview: null,
            Confirmed: false,
            new Sr5DowntimeCalendarResume(false, null));

        if (checkpoint is null)
            return _state;

        string? invalidation = ValidateCheckpointShape(checkpoint) switch
        {
            false => Sr5DowntimeCalendarCheckpointInvalidationReasons.Invalid,
            true when !string.Equals(
                checkpoint.WorkspaceId,
                binding.WorkspaceId,
                StringComparison.Ordinal) =>
                Sr5DowntimeCalendarCheckpointInvalidationReasons.WorkspaceChanged,
            true when checkpoint.WorkspaceRevision != binding.WorkspaceRevision =>
                Sr5DowntimeCalendarCheckpointInvalidationReasons.WorkspaceRevisionChanged,
            true when !string.Equals(
                checkpoint.SnapshotDigest,
                snapshotDigest,
                StringComparison.Ordinal) =>
                Sr5DowntimeCalendarCheckpointInvalidationReasons.SnapshotChanged,
            _ => null
        };
        if (invalidation is not null)
        {
            return _state = _state with
            {
                Resume = new Sr5DowntimeCalendarResume(false, invalidation)
            };
        }

        bool restored = checkpoint.Preview.Operation switch
        {
            Sr5DowntimeCalendarOperation.Add => TryPreviewAdd(
                checkpoint.Preview.WeekId,
                checkpoint.Preview.Year,
                checkpoint.Preview.Week,
                out _),
            Sr5DowntimeCalendarOperation.Edit => TryPreviewEdit(
                checkpoint.Preview.WeekId,
                checkpoint.Preview.Notes,
                checkpoint.Preview.NotesColor,
                out _),
            Sr5DowntimeCalendarOperation.Delete => TryPreviewDelete(
                checkpoint.Preview.WeekId,
                out _),
            _ => false
        };
        if (!restored
            || _state.Preview is null
            || !string.Equals(
                _state.Preview.PreviewDigest,
                checkpoint.Preview.PreviewDigest,
                StringComparison.Ordinal))
        {
            return _state = _state with
            {
                Preview = null,
                Confirmed = false,
                Resume = new Sr5DowntimeCalendarResume(
                    false,
                    Sr5DowntimeCalendarCheckpointInvalidationReasons.PreviewUnavailable)
            };
        }

        return _state = _state with
        {
            Confirmed = false,
            Resume = new Sr5DowntimeCalendarResume(true, null)
        };
    }

    public bool TryPreviewAdd(Guid weekId, int requestedYear, int requestedWeek, out string blocker)
    {
        blocker = string.Empty;
        Sr5DowntimeCalendarDesktopState state = State;
        if (weekId == Guid.Empty
            || !CharacterCareerCalendarRules.TryPlanAdd(
                state.Editor.Calendar,
                CharacterCareerCalendarRules.PinnedSourceAuthority,
                state.Editor.CalendarRevision,
                new CharacterCareerCalendarWeekIdentity(weekId),
                requestedYear,
                requestedWeek,
                out CharacterCareerCalendarWeekDraft draft))
        {
            blocker = "Core rejected the new calendar week for this exact calendar revision.";
            return ClearPreview();
        }

        Sr5DowntimeCalendarPreview preview = CreatePreview(
            state,
            Sr5DowntimeCalendarOperation.Add,
            draft.Identity.WeekId,
            draft.Year,
            draft.Week,
            draft.Notes,
            draft.NotesColor,
            expectedLogicalRevision: string.Empty,
            expectedSourceRevision: string.Empty,
            $"Add {draft.Year.ToString(CultureInfo.InvariantCulture)} W{draft.Week.ToString("00", CultureInfo.InvariantCulture)}.");
        _state = state with { Preview = preview, Confirmed = false, Resume = new(false, null) };
        return true;
    }

    public bool TryPreviewEdit(Guid weekId, string? notes, string? notesColor, out string blocker)
    {
        blocker = string.Empty;
        Sr5DowntimeCalendarDesktopState state = State;
        CharacterCareerCalendarWeekState? selected = FindWeek(state.Editor, weekId);
        if (selected is null
            || !CharacterCareerCalendarRules.TryPlanEdit(
                state.Editor.Calendar,
                CharacterCareerCalendarRules.PinnedSourceAuthority,
                state.Editor.CalendarRevision,
                selected.Identity,
                selected.LogicalRevision,
                selected.SourceRevision,
                notes,
                notesColor,
                out CharacterCareerCalendarWeekDraft draft))
        {
            blocker = "Core rejected the notes, color, identity, or exact calendar revision.";
            return ClearPreview();
        }

        Sr5DowntimeCalendarPreview preview = CreatePreview(
            state,
            Sr5DowntimeCalendarOperation.Edit,
            draft.Identity.WeekId,
            draft.Year,
            draft.Week,
            draft.Notes,
            draft.NotesColor,
            selected.LogicalRevision,
            selected.SourceRevision,
            $"Edit notes for {draft.Year.ToString(CultureInfo.InvariantCulture)} W{draft.Week.ToString("00", CultureInfo.InvariantCulture)}.");
        _state = state with { Preview = preview, Confirmed = false, Resume = new(false, null) };
        return true;
    }

    public bool TryPreviewDelete(Guid weekId, out string blocker)
    {
        blocker = string.Empty;
        Sr5DowntimeCalendarDesktopState state = State;
        CharacterCareerCalendarWeekState? selected = FindWeek(state.Editor, weekId);
        if (selected is null
            || !CharacterCareerCalendarRules.CanDelete(
                state.Editor.Calendar,
                CharacterCareerCalendarRules.PinnedSourceAuthority,
                state.Editor.CalendarRevision,
                selected.Identity,
                selected.LogicalRevision,
                selected.SourceRevision,
                confirmed: true))
        {
            blocker = "Core rejected deletion for this exact week and calendar revision.";
            return ClearPreview();
        }

        Sr5DowntimeCalendarPreview preview = CreatePreview(
            state,
            Sr5DowntimeCalendarOperation.Delete,
            selected.Identity.WeekId,
            selected.Year,
            selected.Week,
            selected.Notes,
            selected.NotesColor,
            selected.LogicalRevision,
            selected.SourceRevision,
            $"Delete {selected.Year.ToString(CultureInfo.InvariantCulture)} W{selected.Week.ToString("00", CultureInfo.InvariantCulture)}.");
        _state = state with { Preview = preview, Confirmed = false, Resume = new(false, null) };
        return true;
    }

    public bool TryConfirm(string previewDigest)
    {
        Sr5DowntimeCalendarDesktopState state = State;
        if (state.Preview is null
            || !IsDigest(previewDigest)
            || !FixedEquals(state.Preview.PreviewDigest, previewDigest))
        {
            _state = state with { Confirmed = false };
            return false;
        }
        _state = state with { Confirmed = true, Resume = new(false, null) };
        return true;
    }

    public CareerCalendarAddRequest CreateAddRequest()
    {
        (Sr5DowntimeCalendarDesktopState state, Sr5DowntimeCalendarPreview preview) =
            RequireConfirmed(Sr5DowntimeCalendarOperation.Add);
        return new CareerCalendarAddRequest(
            state.Editor.WorkspaceId,
            state.Editor.ContentRevision,
            state.Editor.CalendarRevision,
            state.Editor.SourceAuthorityDigest,
            new CharacterCareerCalendarWeekIdentity(preview.WeekId),
            preview.Year,
            preview.Week);
    }

    public CareerCalendarEditRequest CreateEditRequest()
    {
        (Sr5DowntimeCalendarDesktopState state, Sr5DowntimeCalendarPreview preview) =
            RequireConfirmed(Sr5DowntimeCalendarOperation.Edit);
        CharacterCareerCalendarWeekState selected = FindWeek(state.Editor, preview.WeekId)
            ?? throw new InvalidOperationException("The reviewed calendar week is no longer available.");
        return new CareerCalendarEditRequest(
            state.Editor.WorkspaceId,
            state.Editor.ContentRevision,
            state.Editor.CalendarRevision,
            state.Editor.SourceAuthorityDigest,
            selected,
            preview.ExpectedLogicalRevision,
            preview.ExpectedSourceRevision,
            preview.Notes,
            preview.NotesColor);
    }

    public CareerCalendarDeleteRequest CreateDeleteRequest()
    {
        (Sr5DowntimeCalendarDesktopState state, Sr5DowntimeCalendarPreview preview) =
            RequireConfirmed(Sr5DowntimeCalendarOperation.Delete);
        CharacterCareerCalendarWeekState selected = FindWeek(state.Editor, preview.WeekId)
            ?? throw new InvalidOperationException("The reviewed calendar week is no longer available.");
        return new CareerCalendarDeleteRequest(
            state.Editor.WorkspaceId,
            state.Editor.ContentRevision,
            state.Editor.CalendarRevision,
            state.Editor.SourceAuthorityDigest,
            selected,
            preview.ExpectedLogicalRevision,
            preview.ExpectedSourceRevision,
            Confirmed: true);
    }

    public Sr5DowntimeCalendarCheckpoint CreateCheckpoint()
    {
        Sr5DowntimeCalendarDesktopState state = State;
        Sr5DowntimeCalendarPreview preview = state.Preview
            ?? throw new InvalidOperationException("Preview a Downtime Calendar change before checkpointing it.");
        return new Sr5DowntimeCalendarCheckpoint(
            Sr5DowntimeCalendarSchemas.CheckpointV1,
            state.Binding.WorkspaceId,
            state.Binding.WorkspaceRevision,
            state.SnapshotDigest,
            preview);
    }

    public static byte[] SerializeCheckpoint(Sr5DowntimeCalendarCheckpoint checkpoint)
    {
        if (!ValidateCheckpointShape(checkpoint))
            throw new InvalidOperationException("The Downtime Calendar checkpoint is invalid.");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(checkpoint);
        if (payload.Length is 0 or > MaximumCheckpointBytes)
        {
            CryptographicOperations.ZeroMemory(payload);
            throw new InvalidOperationException("The Downtime Calendar checkpoint exceeds its bound.");
        }
        return payload;
    }

    public static bool TryDeserializeCheckpoint(
        ReadOnlySpan<byte> payload,
        out Sr5DowntimeCalendarCheckpoint? checkpoint)
    {
        checkpoint = null;
        if (payload.Length is 0 or > MaximumCheckpointBytes)
            return false;
        try
        {
            Sr5DowntimeCalendarCheckpoint? parsed =
                JsonSerializer.Deserialize<Sr5DowntimeCalendarCheckpoint>(payload);
            if (!ValidateCheckpointShape(parsed))
                return false;
            checkpoint = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool PostconditionMatches(
        Sr5DowntimeCalendarPreview preview,
        CareerCalendarEditorState before,
        CareerCalendarEditorState after)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (!ValidatePreviewShape(preview)
            || before.WorkspaceId != after.WorkspaceId
            || before.ContentRevision >= long.MaxValue
            || after.ContentRevision != before.ContentRevision + 1
            || !CharacterCareerCalendarRules.IsCoherent(before.Calendar)
            || !CharacterCareerCalendarRules.IsCoherent(after.Calendar)
            || !string.Equals(
                before.SourceAuthorityDigest,
                after.SourceAuthorityDigest,
                StringComparison.Ordinal))
        {
            return false;
        }

        return FixedEquals(
            ComputeExpectedPostconditionDigest(preview, before),
            ComputeObservedCalendarDigest(after));
    }

    public static string ComputeExpectedPostconditionDigest(
        Sr5DowntimeCalendarPreview preview,
        CareerCalendarEditorState before)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(before);
        if (!ValidatePreviewShape(preview)
            || !CharacterCareerCalendarRules.IsCoherent(before.Calendar)
            || !string.Equals(
                preview.ExpectedCalendarRevision,
                before.CalendarRevision,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Downtime Calendar preview does not match its exact pre-mutation state.");
        }

        List<CalendarSemanticState> expected = before.Calendar.Weeks
            .Select(static week => new CalendarSemanticState(
                week.Identity.WeekId,
                week.Year,
                week.Week,
                week.Notes,
                week.NotesColor))
            .ToList();
        int target = expected.FindIndex(candidate => candidate.WeekId == preview.WeekId);
        CalendarSemanticState replacement = new(
            preview.WeekId,
            preview.Year,
            preview.Week,
            preview.Notes,
            preview.NotesColor);
        switch (preview.Operation)
        {
            case Sr5DowntimeCalendarOperation.Add when target < 0:
                expected.Add(replacement);
                break;
            case Sr5DowntimeCalendarOperation.Edit when target >= 0:
                expected[target] = replacement;
                break;
            case Sr5DowntimeCalendarOperation.Delete when target >= 0:
                expected.RemoveAt(target);
                break;
            default:
                throw new InvalidOperationException(
                    "The Downtime Calendar preview cannot produce one unambiguous postcondition.");
        }
        return ComputeSemanticDigest(before.SourceAuthorityDigest, expected);
    }

    public static string ComputeObservedCalendarDigest(CareerCalendarEditorState observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        if (!CharacterCareerCalendarRules.IsCoherent(observed.Calendar))
            throw new InvalidOperationException("The observed calendar is not coherent.");
        return ComputeSemanticDigest(
            observed.SourceAuthorityDigest,
            observed.Calendar.Weeks.Select(static week => new CalendarSemanticState(
                week.Identity.WeekId,
                week.Year,
                week.Week,
                week.Notes,
                week.NotesColor)));
    }

    private bool ClearPreview()
    {
        _state = State with { Preview = null, Confirmed = false, Resume = new(false, null) };
        return false;
    }

    private static Sr5DowntimeCalendarPreview CreatePreview(
        Sr5DowntimeCalendarDesktopState state,
        Sr5DowntimeCalendarOperation operation,
        Guid weekId,
        int year,
        int week,
        string notes,
        string notesColor,
        string expectedLogicalRevision,
        string expectedSourceRevision,
        string summary)
    {
        var unsigned = new Sr5DowntimeCalendarPreview(
            Sr5DowntimeCalendarSchemas.PreviewV1,
            operation,
            weekId,
            year,
            week,
            notes,
            notesColor,
            state.Editor.CalendarRevision,
            expectedLogicalRevision,
            expectedSourceRevision,
            summary,
            string.Empty);
        return unsigned with
        {
            PreviewDigest = ComputePreviewDigest(state.SnapshotDigest, unsigned)
        };
    }

    private (Sr5DowntimeCalendarDesktopState, Sr5DowntimeCalendarPreview) RequireConfirmed(
        Sr5DowntimeCalendarOperation operation)
    {
        Sr5DowntimeCalendarDesktopState state = State;
        if (!state.Confirmed
            || state.Preview is not { } preview
            || preview.Operation != operation
            || !FixedEquals(
                preview.PreviewDigest,
                ComputePreviewDigest(state.SnapshotDigest, preview)))
        {
            throw new InvalidOperationException(
                "Review and explicitly confirm the exact Downtime Calendar preview first.");
        }
        return (state, preview);
    }

    private static void ValidateBinding(
        Sr5CareerWizardBinding binding,
        CareerCalendarEditorState editor)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(editor);
        if (string.IsNullOrWhiteSpace(binding.WorkspaceId)
            || binding.WorkspaceRevision <= 0
            || binding.SavedRevision != binding.WorkspaceRevision
            || !string.Equals(binding.RulesetId, "sr5", StringComparison.Ordinal)
            || !string.Equals(binding.RuntimeFingerprint, RuntimeFingerprint, StringComparison.Ordinal)
            || !string.Equals(
                binding.SourceDigest,
                "sha256:" + editor.SourceAuthorityDigest,
                StringComparison.Ordinal)
            || !IsDigest(binding.ContentDigest)
            || !IsDigest(binding.SourceDigest)
            || editor.WorkspaceId.Value != binding.WorkspaceId
            || editor.ContentRevision != binding.WorkspaceRevision
            || !CharacterCareerCalendarRules.IsCoherent(editor.Calendar)
            || !string.Equals(
                editor.SourceAuthorityDigest,
                CharacterCareerCalendarRules.PinnedSourceAuthorityDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Downtime Calendar requires one exact saved SR5 workspace, runtime, source, and content binding.");
        }
    }

    private static string ComputeSnapshotDigest(
        Sr5CareerWizardBinding binding,
        CareerCalendarEditorState editor)
    {
        var material = new StringBuilder("chummer.sr5-downtime-calendar.snapshot.v1");
        AppendBinding(material, binding);
        Append(material, editor.CalendarRevision);
        Append(material, editor.SourceAuthorityDigest);
        foreach (CharacterCareerCalendarWeekState week in editor.Calendar.Weeks)
        {
            Append(material, week.Identity.WeekId.ToString("D"));
            Append(material, week.Year.ToString(CultureInfo.InvariantCulture));
            Append(material, week.Week.ToString(CultureInfo.InvariantCulture));
            Append(material, week.Notes);
            Append(material, week.NotesColor);
            Append(material, week.LogicalRevision);
            Append(material, week.SourceRevision);
            Append(material, week.SourceAuthorityDigest);
        }
        return ComputeDigest(material.ToString());
    }

    private static string ComputePreviewDigest(
        string snapshotDigest,
        Sr5DowntimeCalendarPreview preview)
    {
        var material = new StringBuilder(Sr5DowntimeCalendarSchemas.PreviewV1);
        Append(material, snapshotDigest);
        Append(material, preview.Operation.ToString());
        Append(material, preview.WeekId.ToString("D"));
        Append(material, preview.Year.ToString(CultureInfo.InvariantCulture));
        Append(material, preview.Week.ToString(CultureInfo.InvariantCulture));
        Append(material, preview.Notes);
        Append(material, preview.NotesColor);
        Append(material, preview.ExpectedCalendarRevision);
        Append(material, preview.ExpectedLogicalRevision);
        Append(material, preview.ExpectedSourceRevision);
        Append(material, preview.Summary);
        return ComputeDigest(material.ToString());
    }

    private static void AppendBinding(StringBuilder material, Sr5CareerWizardBinding binding)
    {
        Append(material, binding.WorkspaceId);
        Append(material, binding.WorkspaceRevision.ToString(CultureInfo.InvariantCulture));
        Append(material, binding.SavedRevision.ToString(CultureInfo.InvariantCulture));
        Append(material, binding.RulesetId);
        Append(material, binding.RuntimeFingerprint);
        Append(material, binding.SourceDigest);
        Append(material, binding.ContentDigest);
    }

    private static void Append(StringBuilder builder, string value)
        => builder.Append(value.Length).Append(':').Append(value).Append(';');

    private static string ComputeDigest(string value)
        => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool ValidateCheckpointShape(Sr5DowntimeCalendarCheckpoint? checkpoint)
        => checkpoint is not null
            && string.Equals(
                checkpoint.Schema,
                Sr5DowntimeCalendarSchemas.CheckpointV1,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(checkpoint.WorkspaceId)
            && checkpoint.WorkspaceRevision > 0
            && IsDigest(checkpoint.SnapshotDigest)
            && ValidatePreviewShape(checkpoint.Preview);

    private static bool ValidatePreviewShape(Sr5DowntimeCalendarPreview? preview)
        => preview is not null
            && string.Equals(
                preview.Schema,
                Sr5DowntimeCalendarSchemas.PreviewV1,
                StringComparison.Ordinal)
            && Enum.IsDefined(preview.Operation)
            && preview.WeekId != Guid.Empty
            && CharacterCareerCalendarRules.IsSupportedCalendarCoordinate(preview.Year, preview.Week)
            && preview.Notes is not null
            && CharacterCareerCalendarRules.TryNormalizeNotesColor(
                preview.NotesColor,
                out string normalized)
            && string.Equals(preview.NotesColor, normalized, StringComparison.Ordinal)
            && IsRawDigest(preview.ExpectedCalendarRevision)
            && (preview.Operation == Sr5DowntimeCalendarOperation.Add
                ? preview.ExpectedLogicalRevision.Length == 0
                    && preview.ExpectedSourceRevision.Length == 0
                : IsRawDigest(preview.ExpectedLogicalRevision)
                    && IsRawDigest(preview.ExpectedSourceRevision))
            && !string.IsNullOrWhiteSpace(preview.Summary)
            && IsDigest(preview.PreviewDigest);

    private static CharacterCareerCalendarWeekState? FindWeek(
        CareerCalendarEditorState editor,
        Guid weekId)
        => editor.Calendar.Weeks.SingleOrDefault(candidate => candidate.Identity.WeekId == weekId);

    private static string ComputeSemanticDigest(
        string sourceAuthorityDigest,
        IEnumerable<CalendarSemanticState> calendar)
    {
        var material = new StringBuilder("chummer.sr5-downtime-calendar.semantic-postcondition.v1");
        Append(material, sourceAuthorityDigest);
        foreach (CalendarSemanticState week in calendar.OrderBy(static candidate => candidate.WeekId))
        {
            Append(material, week.WeekId.ToString("D"));
            Append(material, week.Year.ToString(CultureInfo.InvariantCulture));
            Append(material, week.Week.ToString(CultureInfo.InvariantCulture));
            Append(material, week.Notes);
            Append(material, week.NotesColor);
        }
        return ComputeDigest(material.ToString());
    }

    private static bool FixedEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsDigest(string? value)
        => value is { Length: 71 }
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && IsRawDigest(value["sha256:".Length..]);

    private static bool IsRawDigest(string? value)
        => value is { Length: 64 }
            && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record CalendarSemanticState(
        Guid WeekId,
        int Year,
        int Week,
        string Notes,
        string NotesColor);
}
