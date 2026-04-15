namespace Chummer.Presentation;

public sealed record DesktopBuildPathSuggestion(
    string BuildKitId,
    string Title,
    IReadOnlyList<string> Targets,
    string TrustTier,
    string Visibility);

public sealed record DesktopBuildPathPreview(
    string State,
    string? RuntimeFingerprint,
    IReadOnlyList<string> ChangeSummaries,
    IReadOnlyList<string> DiagnosticMessages,
    bool RequiresConfirmation,
    string? RuntimeCompatibilitySummary = null,
    string? CampaignReturnSummary = null,
    string? SupportClosureSummary = null);

public sealed record DesktopBuildPathCandidate(
    DesktopBuildPathSuggestion Suggestion,
    DesktopBuildPathPreview? Preview);
