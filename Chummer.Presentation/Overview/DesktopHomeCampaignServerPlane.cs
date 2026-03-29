namespace Chummer.Presentation.Overview;

public sealed record DesktopHomeCampaignServerPlane(
    string WorkspaceId,
    string SessionReadinessSummary,
    string RestoreSummary,
    string PublicationSummary,
    string RosterSummary,
    string? RunboardSummary,
    string? TravelModeSummary,
    string? TravelPrefetchInventorySummary,
    string? CampaignMemorySummary,
    string? CampaignMemoryReturnSummary,
    string NextSafeAction,
    IReadOnlyList<string> ReadinessHighlights,
    IReadOnlyList<string> Watchouts,
    IReadOnlyList<string> SupportHighlights,
    IReadOnlyList<string> DecisionNotices,
    DateTimeOffset GeneratedAtUtc);

public sealed record DesktopHomeCampaignServerPlaneDto(
    DesktopHomeWorkspaceSummaryDto Workspace,
    DesktopHomeCampaignSummaryDto CampaignSummary,
    DesktopHomeRosterReadinessDto RosterReadiness,
    IReadOnlyList<DesktopHomeCampaignReadinessCueDto> ReadinessCues,
    IReadOnlyList<DesktopHomeWorkspaceChangePacketDto> ChangePackets,
    IReadOnlyList<DesktopHomeRosterTransferDto> RosterTransfers,
    IReadOnlyList<DesktopHomeDossierFreshnessCueDto> DossierFreshness,
    IReadOnlyList<DesktopHomeRuleEnvironmentHealthCueDto> RuleEnvironmentHealth,
    DesktopHomeRunboardSummaryDto? Runboard,
    IReadOnlyList<DesktopHomeContinuityConflictCueDto> ContinuityConflicts,
    IReadOnlyList<DesktopHomeSupportClosureCueDto> SupportClosures,
    IReadOnlyList<DesktopHomeKnownIssueCueDto> KnownIssues,
    IReadOnlyList<DesktopHomeDecisionNoticeDto> DecisionNotices,
    DesktopHomeTravelModeDto? TravelMode,
    DesktopHomeCampaignMemoryDto? CampaignMemory,
    DesktopHomeNextSafeActionCueDto NextSafeAction,
    DateTimeOffset GeneratedAtUtc)
{
    public DesktopHomeCampaignServerPlane ToProjection()
    {
        List<string> readinessHighlights =
        [
            CampaignSummary.SessionReadinessSummary,
            $"Roster: {RosterReadiness.Summary}",
            $"Publication lane: {CampaignSummary.PublicationSummary}"
        ];

        if (!string.IsNullOrWhiteSpace(Runboard?.ActiveSceneSummary))
        {
            readinessHighlights.Add($"Runboard: {Runboard.ActiveSceneSummary}");
        }

        if (!string.IsNullOrWhiteSpace(Runboard?.ObjectiveSummary))
        {
            readinessHighlights.Add($"Objectives: {Runboard.ObjectiveSummary}");
        }

        if (!string.IsNullOrWhiteSpace(TravelMode?.Summary))
        {
            readinessHighlights.Add($"Travel mode: {TravelMode.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(TravelMode?.PrefetchInventorySummary))
        {
            readinessHighlights.Add($"Travel inventory: {TravelMode.PrefetchInventorySummary}");
        }

        if (!string.IsNullOrWhiteSpace(CampaignMemory?.Summary))
        {
            readinessHighlights.Add($"Campaign memory: {CampaignMemory.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(CampaignMemory?.ReturnSummary))
        {
            readinessHighlights.Add($"Campaign memory return: {CampaignMemory.ReturnSummary}");
        }

        if (CampaignMemory?.EvidenceLines.Count > 0)
        {
            readinessHighlights.Add($"Campaign memory evidence: {CampaignMemory.EvidenceLines[0]}");
        }

        readinessHighlights.AddRange(ReadinessCues
            .Take(3)
            .Select(static cue => $"{cue.Title} — {cue.Summary}"));
        readinessHighlights.AddRange(ChangePackets
            .Take(2)
            .Select(static packet => $"{packet.Label} — {packet.Summary}"));
        readinessHighlights.AddRange(RosterTransfers
            .Take(2)
            .Select(static transfer => $"Roster transfer: {transfer.RunnerHandle} — {transfer.Summary}"));

        List<string> watchouts = [];
        watchouts.AddRange(DossierFreshness
            .Where(static cue => NeedsAttention(cue.Severity))
            .Select(static cue => $"{cue.RunnerHandle}: {cue.Summary}"));
        watchouts.AddRange(RuleEnvironmentHealth
            .Where(static cue => NeedsAttention(cue.Severity))
            .Select(static cue => $"{cue.Title}: {cue.Summary}"));
        watchouts.AddRange(ContinuityConflicts.Select(static cue => cue.Summary));
        watchouts.AddRange(KnownIssues.Select(static cue => cue.Summary));
        if (NeedsAttention(TravelMode?.Status))
        {
            watchouts.Add($"Travel mode: {TravelMode!.Summary}");
        }

        IReadOnlyList<string> supportHighlights = SupportClosures
            .Take(3)
            .Select(static cue => $"{cue.StageLabel}: {cue.Summary}")
            .ToArray();
        IReadOnlyList<string> decisionNotices = DecisionNotices
            .Take(3)
            .Select(static notice => $"{notice.Kind}: {notice.Summary}")
            .ToArray();

        string? runboardSummary = Runboard is null
            ? null
            : string.Join(
                " ",
                new[]
                {
                    Runboard.ActiveSceneSummary,
                    Runboard.ObjectiveSummary,
                    Runboard.ReturnSummary
                }.Where(static item => !string.IsNullOrWhiteSpace(item)));

        return new DesktopHomeCampaignServerPlane(
            WorkspaceId: Workspace.WorkspaceId,
            SessionReadinessSummary: CampaignSummary.SessionReadinessSummary,
            RestoreSummary: CampaignSummary.RestoreSummary,
            PublicationSummary: CampaignSummary.PublicationSummary,
            RosterSummary: RosterReadiness.Summary,
            RunboardSummary: string.IsNullOrWhiteSpace(runboardSummary) ? null : runboardSummary,
            TravelModeSummary: NormalizeOptional(TravelMode?.Summary),
            TravelPrefetchInventorySummary: NormalizeOptional(TravelMode?.PrefetchInventorySummary),
            CampaignMemorySummary: NormalizeOptional(CampaignMemory?.Summary),
            CampaignMemoryReturnSummary: NormalizeOptional(CampaignMemory?.ReturnSummary),
            NextSafeAction: NextSafeAction.Summary,
            ReadinessHighlights: FinalizeLines(readinessHighlights),
            Watchouts: FinalizeLines(watchouts),
            SupportHighlights: supportHighlights,
            DecisionNotices: decisionNotices,
            GeneratedAtUtc: GeneratedAtUtc);
    }

    private static bool NeedsAttention(string? severity)
        => !string.IsNullOrWhiteSpace(severity)
           && !severity.Equals("healthy", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("info", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("ok", StringComparison.OrdinalIgnoreCase)
           && !severity.Equals("ready", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> FinalizeLines(IEnumerable<string> lines)
        => lines
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record DesktopHomeWorkspaceSummaryDto(string WorkspaceId);

public sealed record DesktopHomeCampaignSummaryDto(
    string SessionReadinessSummary,
    string RestoreSummary,
    string PublicationSummary);

public sealed record DesktopHomeRosterReadinessDto(string Summary);

public sealed record DesktopHomeCampaignReadinessCueDto(
    string Title,
    string Summary);

public sealed record DesktopHomeWorkspaceChangePacketDto(
    string Label,
    string Summary);

public sealed record DesktopHomeRosterTransferDto(
    string RunnerHandle,
    string Summary);

public sealed record DesktopHomeDossierFreshnessCueDto(
    string RunnerHandle,
    string Severity,
    string Summary);

public sealed record DesktopHomeRuleEnvironmentHealthCueDto(
    string Title,
    string Severity,
    string Summary);

public sealed record DesktopHomeRunboardSummaryDto(
    string? ActiveSceneSummary,
    string ObjectiveSummary,
    string ReturnSummary);

public sealed record DesktopHomeContinuityConflictCueDto(string Summary);

public sealed record DesktopHomeSupportClosureCueDto(
    string StageLabel,
    string Summary);

public sealed record DesktopHomeKnownIssueCueDto(string Summary);

public sealed record DesktopHomeDecisionNoticeDto(
    string Kind,
    string Summary);

public sealed record DesktopHomeTravelModeDto(
    string Status,
    string Summary,
    string PrefetchInventorySummary);

public sealed record DesktopHomeCampaignMemoryDto(
    string Label,
    string Summary,
    string ReturnSummary,
    string NextSafeAction,
    IReadOnlyList<string> EvidenceLines);

public sealed record DesktopHomeNextSafeActionCueDto(string Summary);
