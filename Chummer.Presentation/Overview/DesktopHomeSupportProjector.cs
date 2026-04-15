namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeSupportDigest(
    string CaseId,
    string Title,
    string Summary,
    string StatusLabel,
    string StageLabel,
    string NextSafeAction,
    string ClosureSummary,
    string VerificationSummary,
    string DetailHref,
    string PrimaryActionLabel,
    string PrimaryActionHref,
    string UpdatedLabel,
    string? FixedReleaseLabel,
    string? AffectedInstallSummary,
    string FollowUpLaneSummary,
    string ReleaseProgressSummary,
    bool ReporterActionNeeded,
    bool CanVerifyFix,
    string InstallReadinessSummary = "",
    bool FixReadyOnLinkedInstall = false,
    bool NeedsInstallUpdate = false,
    bool NeedsLinkedInstall = false);

public sealed record DesktopHomeSupportProjection(
    string? CaseId,
    string Summary,
    string NextSafeAction,
    string? PrimaryActionLabel,
    string? PrimaryActionHref,
    string? DetailHref,
    string? InstallReadinessSummary,
    string? StatusLabel,
    string? StageLabel,
    string? UpdatedLabel,
    string? FixedReleaseLabel,
    string? AffectedInstallSummary,
    string? FollowUpLaneSummary,
    string? ReleaseProgressSummary,
    string? VerificationSummary,
    bool HasTrackedCase,
    bool NeedsAttention,
    bool FixReadyOnLinkedInstall,
    bool NeedsInstallUpdate,
    bool NeedsLinkedInstall,
    IReadOnlyList<string> Highlights);

public static class DesktopHomeSupportProjector
{
    public static DesktopHomeSupportProjection Create(
        IReadOnlyList<DesktopHomeSupportDigest>? digests,
        bool installClaimed)
    {
        if (digests is null || digests.Count == 0)
        {
            return new DesktopHomeSupportProjection(
                CaseId: null,
                Summary: installClaimed
                    ? "No tracked support cases are attached to this linked install right now, so closure and fix notices stay quiet until a real install, update, or campaign issue needs one case."
                    : "No tracked support cases are attached yet, and install-aware closure will stay generic until this desktop copy is linked to an account-backed install.",
                NextSafeAction: installClaimed
                    ? "Keep the install-aware support lane quiet until a real install, update, or campaign issue needs one tracked case."
                    : "Link this copy before you rely on install-specific support closure or fix notices.",
                PrimaryActionLabel: null,
                PrimaryActionHref: null,
                DetailHref: null,
                InstallReadinessSummary: null,
                StatusLabel: null,
                StageLabel: null,
                UpdatedLabel: null,
                FixedReleaseLabel: null,
                AffectedInstallSummary: null,
                FollowUpLaneSummary: null,
                ReleaseProgressSummary: null,
                VerificationSummary: null,
                HasTrackedCase: false,
                NeedsAttention: false,
                FixReadyOnLinkedInstall: false,
                NeedsInstallUpdate: false,
                NeedsLinkedInstall: false,
                Highlights:
                [
                    installClaimed
                        ? "Support posture: the linked install can open tracked cases directly when a real issue appears."
                        : "Support posture: the support lane becomes install-aware only after claim and restore attach this copy to an account."
                ]);
        }

        DesktopHomeSupportDigest lead = digests[0];
        List<string> highlights =
        [
            $"Stage: {lead.StageLabel} ({lead.StatusLabel})",
            $"Closure: {lead.ClosureSummary}",
            $"Release progress: {lead.ReleaseProgressSummary}",
            $"Fix availability: {BuildFixAvailabilitySummary(lead)}",
            $"Current caution: {BuildCurrentCaution(lead)}",
            $"Verification: {lead.VerificationSummary}",
            $"Updated: {lead.UpdatedLabel}"
        ];

        if (!string.IsNullOrWhiteSpace(lead.FixedReleaseLabel))
        {
            highlights.Add($"Fixed release: {lead.FixedReleaseLabel}");
        }

        if (!string.IsNullOrWhiteSpace(lead.AffectedInstallSummary))
        {
            highlights.Add($"Affected install: {lead.AffectedInstallSummary}");
        }

        if (!string.IsNullOrWhiteSpace(lead.InstallReadinessSummary))
        {
            highlights.Add($"Linked install state: {lead.InstallReadinessSummary}");
        }

        if (!string.IsNullOrWhiteSpace(lead.FollowUpLaneSummary))
        {
            highlights.Add($"Follow-up: {lead.FollowUpLaneSummary}");
        }

        return new DesktopHomeSupportProjection(
            CaseId: lead.CaseId,
            Summary: $"Tracked case: {lead.Title}. {lead.Summary}",
            NextSafeAction: lead.NextSafeAction,
            PrimaryActionLabel: lead.PrimaryActionLabel,
            PrimaryActionHref: lead.PrimaryActionHref,
            DetailHref: lead.DetailHref,
            InstallReadinessSummary: string.IsNullOrWhiteSpace(lead.InstallReadinessSummary) ? null : lead.InstallReadinessSummary,
            StatusLabel: lead.StatusLabel,
            StageLabel: lead.StageLabel,
            UpdatedLabel: lead.UpdatedLabel,
            FixedReleaseLabel: string.IsNullOrWhiteSpace(lead.FixedReleaseLabel) ? null : lead.FixedReleaseLabel,
            AffectedInstallSummary: string.IsNullOrWhiteSpace(lead.AffectedInstallSummary) ? null : lead.AffectedInstallSummary,
            FollowUpLaneSummary: string.IsNullOrWhiteSpace(lead.FollowUpLaneSummary) ? null : lead.FollowUpLaneSummary,
            ReleaseProgressSummary: string.IsNullOrWhiteSpace(lead.ReleaseProgressSummary) ? null : lead.ReleaseProgressSummary,
            VerificationSummary: string.IsNullOrWhiteSpace(lead.VerificationSummary) ? null : lead.VerificationSummary,
            HasTrackedCase: true,
            NeedsAttention: lead.ReporterActionNeeded || lead.CanVerifyFix || lead.NeedsInstallUpdate || lead.NeedsLinkedInstall,
            FixReadyOnLinkedInstall: lead.FixReadyOnLinkedInstall,
            NeedsInstallUpdate: lead.NeedsInstallUpdate,
            NeedsLinkedInstall: lead.NeedsLinkedInstall,
            Highlights: highlights);
    }

    private static string BuildFixAvailabilitySummary(DesktopHomeSupportDigest lead)
        => !string.IsNullOrWhiteSpace(lead.FixedReleaseLabel)
            ? $"{lead.FixedReleaseLabel} is the tracked fix target for this desktop support lane."
            : lead.ReleaseProgressSummary;

    private static string BuildCurrentCaution(DesktopHomeSupportDigest lead)
        => !string.IsNullOrWhiteSpace(lead.NextSafeAction)
            ? lead.NextSafeAction
            : lead.ReleaseProgressSummary;
}
