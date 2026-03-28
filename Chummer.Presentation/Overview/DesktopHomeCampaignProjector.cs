using Chummer.Campaign.Contracts;

namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeCampaignProjection(
    string Summary,
    string NextSafeAction,
    string RestoreSummary,
    string DeviceRoleSummary,
    string SupportClosureSummary,
    string? LeadWorkspaceId,
    IReadOnlyList<string> ReadinessHighlights,
    IReadOnlyList<string> Watchouts);

public static class DesktopHomeCampaignProjector
{
    public static DesktopHomeCampaignProjection Create(
        AccountCampaignSummary? summary,
        IReadOnlyList<CampaignWorkspaceDigestProjection>? workspaceDigests = null)
    {
        CampaignWorkspaceDigestProjection? leadDigest = workspaceDigests?
            .OrderByDescending(static digest => digest.UpdatedAtUtc)
            .FirstOrDefault();

        if (summary is null && leadDigest is null)
        {
            return new DesktopHomeCampaignProjection(
                "No signed-in campaign spine is loaded yet. Link this copy and open the account-aware route before you trust desktop restore, continuity, or install-specific closure.",
                "Link this copy, open the account-aware route, and refresh the home cockpit before you rely on campaign return or claimed-device restore.",
                "Restore packet: no account-backed dossiers, campaigns, or reconnectable artifacts are loaded yet for this desktop head.",
                "Claimed device posture: this copy is still local-only until the account restore packet can attach a device role and channel-aware continuity target.",
                "Support closure stays strongest after this copy is linked, because fixes, notices, and restore follow-through can then target the exact install and campaign lane.",
                LeadWorkspaceId: null,
                ReadinessHighlights:
                [
                    "Campaign highlight: the flagship desktop home can reopen grounded campaign continuity as soon as account-backed restore truth is available."
                ],
                Watchouts:
                [
                    "Campaign return remains guest-only until an account-backed restore packet lands on this install.",
                    "Support and fix notices stay generic until the claimed install, channel, and current campaign workspace line up."
                ]);
        }

        if (summary is null && leadDigest is not null)
        {
            return new DesktopHomeCampaignProjection(
                $"Campaign posture: {leadDigest.ReturnSummary} {leadDigest.ActiveSceneSummary ?? string.Empty}".Trim(),
                leadDigest.NextSafeAction,
                "Restore packet: the calmer workspace digest is present, but the full account-backed restore packet still needs a refresh.",
                $"Claimed device posture: {leadDigest.DeviceRoleSummary}",
                $"Support closure: {leadDigest.SupportClosureSummary}",
                LeadWorkspaceId: leadDigest.WorkspaceId,
                ReadinessHighlights: FinalizeLines(leadDigest.ReadinessHighlights),
                Watchouts: FinalizeLines(leadDigest.Watchouts));
        }

        AccountCampaignSummary groundedSummary = summary!;

        CampaignWorkspaceProjection? leadWorkspace = groundedSummary.Workspaces
            .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        leadDigest ??= workspaceDigests?
            .FirstOrDefault(digest => string.Equals(digest.WorkspaceId, leadWorkspace?.WorkspaceId, StringComparison.OrdinalIgnoreCase))
            ?? workspaceDigests?.OrderByDescending(static digest => digest.UpdatedAtUtc).FirstOrDefault();
        BuildLabHandoffProjection? leadHandoff = groundedSummary.BuildLabHandoffs
            .OrderByDescending(static handoff => handoff.UpdatedAtUtc)
            .FirstOrDefault();
        RulesNavigatorAnswerProjection? leadRulesAnswer = groundedSummary.RulesNavigator.FirstOrDefault();
        LegacyMigrationReceiptProjection? leadMigration = groundedSummary.MigrationReceipts
            .OrderByDescending(static receipt => receipt.ImportedAtUtc)
            .FirstOrDefault();
        CreatorPublicationProjection? leadPublication = groundedSummary.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();
        WorkspaceRestoreProjection restore = groundedSummary.Restore;
        ClaimedDeviceRestoreProjection[] claimedDevices = restore.ClaimedDevices
            .Where(static device => !string.IsNullOrWhiteSpace(device.InstallationId))
            .Take(2)
            .ToArray();

        string summaryLine = leadWorkspace is null
            ? $"Campaign posture: {groundedSummary.Dossiers.Count} dossier(s), {groundedSummary.Campaigns.Count} campaign(s), and {groundedSummary.Runs.Count} runboard lane(s) are attached to this account, but no current campaign workspace return target is pinned yet."
            : $"Campaign posture: {(leadDigest?.ReturnSummary ?? leadWorkspace.ReturnSummary)} {(leadDigest?.ActiveSceneSummary ?? leadWorkspace.ActiveSceneSummary) ?? string.Empty} {groundedSummary.Dossiers.Count} dossier(s), {groundedSummary.Campaigns.Count} campaign(s), and {groundedSummary.Runs.Count} runboard lane(s) stay attached to the same account-backed continuity packet.";

        string nextSafeAction = !string.IsNullOrWhiteSpace(leadDigest?.NextSafeAction)
            ? leadDigest.NextSafeAction
            : ResolveNextSafeAction(groundedSummary, leadWorkspace, leadHandoff, restore);
        string restoreSummary = $"Restore packet: {restore.RecentDossiers.Count} recent dossier(s), {restore.RecentCampaigns.Count} recent campaign(s), {restore.RecentArtifacts.Count} reconnectable artifact(s), and {restore.ClaimedDevices.Count} claimed device(s) were generated at {restore.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";
        string deviceRoleSummary = leadDigest is null
            ? BuildClaimedDeviceSummary(claimedDevices, restore.ClaimedDevices.Count)
            : $"Claimed device posture: {leadDigest.DeviceRoleSummary}";
        string supportClosureSummary = leadDigest is null
            ? ResolveSupportClosureSummary(leadHandoff, leadRulesAnswer)
            : $"Support closure: {leadDigest.SupportClosureSummary}";

        List<string> readinessHighlights = [];
        if (leadDigest is not null)
        {
            readinessHighlights.AddRange(leadDigest.ReadinessHighlights);
        }

        if (leadWorkspace is not null)
        {
            readinessHighlights.Add($"Campaign return: {leadWorkspace.ReturnSummary}");
            if (!string.IsNullOrWhiteSpace(leadWorkspace.ActiveSceneSummary))
            {
                readinessHighlights.Add($"Current scene: {leadWorkspace.ActiveSceneSummary}");
            }
        }

        if (!string.IsNullOrWhiteSpace(leadHandoff?.CampaignReturnSummary))
        {
            readinessHighlights.Add($"Build handoff: {leadHandoff.CampaignReturnSummary}");
        }

        if (!string.IsNullOrWhiteSpace(leadRulesAnswer?.AfterSummary))
        {
            readinessHighlights.Add($"Rules follow-through: {leadRulesAnswer.AfterSummary}");
        }

        if (!string.IsNullOrWhiteSpace(leadMigration?.Summary))
        {
            readinessHighlights.Add($"Migration continuity: {leadMigration.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(leadPublication?.ProvenanceSummary))
        {
            readinessHighlights.Add($"Publication trust: {leadPublication.Title} — {leadPublication.ProvenanceSummary}");
        }

        readinessHighlights.AddRange(
            leadWorkspace?.ReadinessCues
                .Take(3)
                .Select(static cue => $"Readiness cue: {cue.Title} — {cue.Summary}")
            ?? []);
        readinessHighlights.AddRange(
            leadWorkspace?.ChangePackets?
                .Take(2)
                .Select(static packet => $"Change packet: {packet.Label} — {packet.Summary}")
            ?? []);

        List<string> watchouts = [];
        if (leadDigest is not null)
        {
            watchouts.AddRange(leadDigest.Watchouts);
        }

        watchouts.AddRange(restore.ConflictSummaries);
        watchouts.AddRange(restore.LocalOnlyNotes);
        if (leadHandoff?.Watchouts is not null)
        {
            watchouts.AddRange(leadHandoff.Watchouts);
        }

        if (leadWorkspace is not null)
        {
            watchouts.AddRange(
                leadWorkspace.ReadinessCues
                    .Where(static cue => NeedsAttention(cue.Severity))
                    .Select(static cue => $"{cue.Title}: {cue.Summary}"));
        }

        LegacyMigrationFieldProjection? migrationAttentionField = leadMigration?.Fields.FirstOrDefault(static field => NeedsAttentionStatus(field.Status));
        if (migrationAttentionField is not null)
        {
            watchouts.Add($"Migration watchout: {migrationAttentionField.Label} is {migrationAttentionField.Status} — {migrationAttentionField.Summary}");
        }

        if (leadPublication is not null && NeedsAttentionStatus(leadPublication.PublicationStatus))
        {
            watchouts.Add(
                $"Publication watchout: {leadPublication.Title} is {leadPublication.PublicationStatus} — {leadPublication.DiscoverySummary}");
        }

        return new DesktopHomeCampaignProjection(
            summaryLine,
            nextSafeAction,
            restoreSummary,
            deviceRoleSummary,
            supportClosureSummary,
            LeadWorkspaceId: leadDigest?.WorkspaceId ?? leadWorkspace?.WorkspaceId,
            ReadinessHighlights: FinalizeLines(readinessHighlights),
            Watchouts: FinalizeLines(watchouts));
    }

    private static string ResolveNextSafeAction(
        AccountCampaignSummary summary,
        CampaignWorkspaceProjection? leadWorkspace,
        BuildLabHandoffProjection? leadHandoff,
        WorkspaceRestoreProjection restore)
    {
        string? firstConflict = restore.ConflictSummaries.FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item));
        if (!string.IsNullOrWhiteSpace(firstConflict))
        {
            return $"Resolve the restore conflict before you trust this copy for campaign return: {firstConflict}";
        }

        if (!string.IsNullOrWhiteSpace(leadWorkspace?.NextSafeAction))
        {
            return leadWorkspace.NextSafeAction!;
        }

        CampaignReadinessCue? attentionCue = leadWorkspace?.ReadinessCues.FirstOrDefault(static cue => NeedsAttention(cue.Severity));
        if (attentionCue is not null)
        {
            return $"Review {attentionCue.Title} before you reopen {leadWorkspace!.CampaignName}: {attentionCue.Summary}";
        }

        if (!string.IsNullOrWhiteSpace(leadHandoff?.NextSafeAction))
        {
            return leadHandoff.NextSafeAction!;
        }

        if (leadWorkspace is not null)
        {
            return $"Reopen {leadWorkspace.CampaignName} and continue the grounded campaign workspace from the latest continuity snapshot.";
        }

        if (summary.Restore.RecentCampaigns.Count > 0)
        {
            return "Open the account-aware work surface and restore the current campaign lane before you create another local-only workspace.";
        }

        return "Link this copy and seed one campaign-facing workspace before you trust desktop return and restore continuity.";
    }

    private static string BuildClaimedDeviceSummary(
        IReadOnlyList<ClaimedDeviceRestoreProjection> claimedDevices,
        int totalDeviceCount)
    {
        if (claimedDevices.Count == 0)
        {
            return "Claimed device posture: no account-backed device roles are attached to the restore packet yet.";
        }

        string summary = string.Join(
            "; ",
            claimedDevices.Select(static device =>
                $"{device.DeviceRole} on {device.Platform}/{device.HeadId} ({device.Channel})"));

        if (totalDeviceCount > claimedDevices.Count)
        {
            summary += $"; plus {totalDeviceCount - claimedDevices.Count} more claimed device(s).";
        }

        return $"Claimed device posture: {summary}";
    }

    private static string ResolveSupportClosureSummary(
        BuildLabHandoffProjection? leadHandoff,
        RulesNavigatorAnswerProjection? leadRulesAnswer)
    {
        if (!string.IsNullOrWhiteSpace(leadHandoff?.SupportClosureSummary))
        {
            return $"Support closure: {leadHandoff.SupportClosureSummary}";
        }

        string? supportReuseHint = leadRulesAnswer?.SupportReuseHints.FirstOrDefault(static hint => !string.IsNullOrWhiteSpace(hint));
        if (!string.IsNullOrWhiteSpace(supportReuseHint))
        {
            return $"Support closure: {supportReuseHint}";
        }

        return "Support closure: fixes, notices, and verification stay attached to the claimed install, current channel, and the campaign workspace you reopen from this home cockpit.";
    }

    private static bool NeedsAttention(string? severity)
        => !string.IsNullOrWhiteSpace(severity)
           && !severity.Equals("healthy", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("info", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("ok", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("ready", StringComparison.OrdinalIgnoreCase);

    private static bool NeedsAttentionStatus(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && !status.Equals("healthy", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("info", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("ok", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("ready", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("published", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("safe", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("mapped", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("approved", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("active", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> FinalizeLines(IEnumerable<string> lines)
        => lines
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(static line => line.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(9)
            .ToArray();
}
