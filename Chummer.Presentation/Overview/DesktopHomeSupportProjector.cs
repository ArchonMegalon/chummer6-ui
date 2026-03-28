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
    bool CanVerifyFix);

public sealed record DesktopHomeSupportProjection(
    string Summary,
    string NextSafeAction,
    string? PrimaryActionLabel,
    string? PrimaryActionHref,
    string? DetailHref,
    bool HasTrackedCase,
    bool NeedsAttention,
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
                Summary: installClaimed
                    ? "No tracked support cases are attached to this linked install right now, so closure and fix notices stay quiet until a real install, update, or campaign issue needs one case."
                    : "No tracked support cases are attached yet, and install-aware closure will stay generic until this desktop copy is linked to an account-backed install.",
                NextSafeAction: installClaimed
                    ? "Keep the install-aware support lane quiet until a real install, update, or campaign issue needs one tracked case."
                    : "Link this copy before you rely on install-specific support closure or fix notices.",
                PrimaryActionLabel: null,
                PrimaryActionHref: null,
                DetailHref: null,
                HasTrackedCase: false,
                NeedsAttention: false,
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

        if (!string.IsNullOrWhiteSpace(lead.FollowUpLaneSummary))
        {
            highlights.Add($"Follow-up: {lead.FollowUpLaneSummary}");
        }

        return new DesktopHomeSupportProjection(
            Summary: $"Tracked case: {lead.Title}. {lead.Summary}",
            NextSafeAction: lead.NextSafeAction,
            PrimaryActionLabel: lead.PrimaryActionLabel,
            PrimaryActionHref: lead.PrimaryActionHref,
            DetailHref: lead.DetailHref,
            HasTrackedCase: true,
            NeedsAttention: lead.ReporterActionNeeded || lead.CanVerifyFix,
            Highlights: highlights);
    }
}
