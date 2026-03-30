namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportCaseDetails(
    string CaseId,
    string Kind,
    string Status,
    string Title,
    string Summary,
    string Detail,
    string CandidateOwnerRepo,
    bool DesignImpactSuspected,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Source,
    string? ReporterEmail = null,
    string? InstallationId = null,
    string? ApplicationVersion = null,
    string? ReleaseChannel = null,
    string? HeadId = null,
    string? Platform = null,
    string? Arch = null,
    string? FixedVersion = null,
    string? FixedChannel = null,
    DateTimeOffset? ReleasedToReporterChannelAtUtc = null,
    DateTimeOffset? UserNotifiedAtUtc = null,
    string? ReporterVerificationState = null,
    string? ReporterVerificationNote = null,
    DateTimeOffset? ReporterVerifiedAtUtc = null,
    IReadOnlyList<DesktopSupportCaseTimelineEntry>? Timeline = null,
    IReadOnlyList<DesktopSupportCaseAttachment>? Attachments = null);

public sealed record DesktopSupportCaseTimelineEntry(
    string EventId,
    string Status,
    string Summary,
    DateTimeOffset OccurredAtUtc,
    string? Actor = null);

public sealed record DesktopSupportCaseAttachment(
    string AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    string? DownloadHref = null);
