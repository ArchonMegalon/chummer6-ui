namespace Chummer.Desktop.Runtime;

public sealed record DesktopCrashReport(
    string CrashId,
    string HeadId,
    DateTimeOffset CapturedAtUtc,
    bool IsTerminating,
    string ApplicationVersion,
    string RuntimeVersion,
    string OperatingSystem,
    string ProcessArchitecture,
    string ProcessName,
    string BaseDirectoryLabel,
    string CurrentDirectoryLabel,
    string ExceptionType,
    string ExceptionMessage,
    string ExceptionDetail);

public sealed record DesktopCrashClaimSnapshot(
    string? InstallationId,
    string? ClaimedUserId,
    string? ClaimedSubjectId,
    string? ClaimGrantId);

public sealed record DesktopPendingCrashReport(
    DesktopCrashReport Report,
    string ReportDirectory,
    string ReportPath,
    string SummaryPath,
    string BundlePath,
    string SummaryText,
    DesktopCrashClaimSnapshot? ClaimSnapshot,
    int SubmissionAttempts,
    DateTimeOffset? LastSubmissionAttemptUtc,
    string? LastSubmissionError,
    string? IncidentId,
    DateTimeOffset? SubmittedAtUtc);

public sealed record DesktopCrashSubmissionResult(
    bool Succeeded,
    bool AlreadySubmitted,
    string? IncidentId,
    string? ClusterId,
    string? WorkItemId,
    DateTimeOffset? SubmittedAtUtc,
    string Message);
