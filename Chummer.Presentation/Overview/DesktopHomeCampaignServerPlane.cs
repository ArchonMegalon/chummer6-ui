using Chummer.Campaign.Contracts;
using Chummer.Presentation;

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
    string? AdoptionSummary,
    string? AdoptionConfidenceSummary,
    string? AdoptionEvidenceSummary,
    string? GoalPinSummary,
    string? ResolutionReportSummary,
    string? BlackLedgerSummary,
    string? BlackLedgerProofSummary,
    FirstPlayableSessionProjection? FirstPlayableSession,
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
    IReadOnlyList<DesktopHomeRecapShelfEntryDto> RecapShelf,
    IReadOnlyList<DesktopHomeSupportClosureCueDto> SupportClosures,
    IReadOnlyList<DesktopHomeKnownIssueCueDto> KnownIssues,
    IReadOnlyList<DesktopHomeDecisionNoticeDto> DecisionNotices,
    DesktopHomeTravelModeDto? TravelMode,
    FirstPlayableSessionProjection? FirstPlayableSession,
    DesktopHomeCampaignMemoryDto? CampaignMemory,
    DesktopHomeCampaignAdoptionDto? Adoption,
    IReadOnlyList<DesktopHomeRunnerGoalPinDto> GoalPins,
    DesktopHomeResolutionReportCloseoutDto? ResolutionReport,
    DesktopHomeBlackLedgerConsequenceDto? BlackLedger,
    DesktopHomeNextSafeActionCueDto NextSafeAction,
    DateTimeOffset GeneratedAtUtc)
{
    public DesktopHomeCampaignServerPlane ToProjection()
    {
        DesktopHomeRecapShelfEntryDto? leadRecapShelfEntry = RecapShelf.FirstOrDefault();
        List<string> readinessHighlights =
        [
            CampaignSummary.SessionReadinessSummary,
            $"Roster: {RosterReadiness.Summary}",
            $"Publication: {BuildPublicationSummary(CampaignSummary.PublicationSummary, leadRecapShelfEntry)}"
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

        if (!string.IsNullOrWhiteSpace(Adoption?.Summary))
        {
            readinessHighlights.Add($"Campaign adoption: {UndetectableHumanizerCopyAdapter.Humanize(Adoption.Summary)}");
        }

        if (!string.IsNullOrWhiteSpace(Adoption?.ConfidenceSummary))
        {
            readinessHighlights.Add($"Adoption confidence: {UndetectableHumanizerCopyAdapter.Humanize(Adoption.ConfidenceSummary)}");
        }

        if (Adoption?.EvidenceLines.Count > 0)
        {
            readinessHighlights.Add($"Adoption details: {UndetectableHumanizerCopyAdapter.Humanize(Adoption.EvidenceLines[0])}");
        }

        if (GoalPins.Count > 0)
        {
            readinessHighlights.Add($"Goal pins: {BuildGoalPinSummary(GoalPins)}");
        }

        if (!string.IsNullOrWhiteSpace(ResolutionReport?.Summary))
        {
            readinessHighlights.Add($"ResolutionReport closeout: {UndetectableHumanizerCopyAdapter.Humanize(ResolutionReport.Summary)}");
        }

        if (!string.IsNullOrWhiteSpace(BlackLedger?.Summary))
        {
            readinessHighlights.Add($"BLACK LEDGER consequence: {UndetectableHumanizerCopyAdapter.Humanize(BlackLedger.Summary)}");
        }

        if (!string.IsNullOrWhiteSpace(BlackLedger?.ProofSummary))
        {
            readinessHighlights.Add($"BLACK LEDGER details: {UndetectableHumanizerCopyAdapter.Humanize(BlackLedger.ProofSummary)}");
        }

        if (FirstPlayableSession is not null)
        {
            readinessHighlights.Add($"First session: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.CampaignStartSummary)}");
            readinessHighlights.Add($"Legal runner: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.RuleReadySummary)}");
            readinessHighlights.Add($"Understandable return: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.ReturnLaneSummary)}");
            readinessHighlights.Add($"Campaign-ready path: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.CampaignReadySummary)}");

            if (!string.IsNullOrWhiteSpace(FirstPlayableSession.NextSafeAction))
            {
                readinessHighlights.Add($"Starter path next: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.NextSafeAction)}");
            }

            if (FirstPlayableSession.EvidenceLines.Count > 0)
            {
                readinessHighlights.Add($"First-session details: {UndetectableHumanizerCopyAdapter.Humanize(FirstPlayableSession.EvidenceLines[0])}");
            }
        }

        if (CampaignMemory?.EvidenceLines.Count > 0)
        {
            readinessHighlights.Add($"Campaign memory details: {UndetectableHumanizerCopyAdapter.Humanize(CampaignMemory.EvidenceLines[0])}");
        }

        if (leadRecapShelfEntry is not null)
        {
            readinessHighlights.Add($"Item audience: {HumanizeAudience(leadRecapShelfEntry.Audience)}");
            readinessHighlights.Add($"Item views: {HumanizeAudience(leadRecapShelfEntry.Audience)} stay browseable from the same page.");

            if (!string.IsNullOrWhiteSpace(leadRecapShelfEntry.OwnershipSummary))
            {
                readinessHighlights.Add($"Item ownership: {UndetectableHumanizerCopyAdapter.Humanize(leadRecapShelfEntry.OwnershipSummary)}");
            }

            if (!string.IsNullOrWhiteSpace(leadRecapShelfEntry.PublicationSummary))
            {
                readinessHighlights.Add(
                    $"Item publication: {HumanizeState(leadRecapShelfEntry.PublicationState, "Ready")} — {UndetectableHumanizerCopyAdapter.Humanize(leadRecapShelfEntry.PublicationSummary)}");
            }

            if (!string.IsNullOrWhiteSpace(leadRecapShelfEntry.TrustBand))
            {
                readinessHighlights.Add(
                    $"Item trust: {HumanizeState(leadRecapShelfEntry.TrustBand, "Draft")} — {(leadRecapShelfEntry.Discoverable ? "Eligible now" : "Still limited")}");
            }

            if (!string.IsNullOrWhiteSpace(leadRecapShelfEntry.NextSafeAction))
            {
                readinessHighlights.Add($"Item next: {UndetectableHumanizerCopyAdapter.Humanize(leadRecapShelfEntry.NextSafeAction)}");
            }
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
            SessionReadinessSummary: UndetectableHumanizerCopyAdapter.Humanize(CampaignSummary.SessionReadinessSummary),
            RestoreSummary: UndetectableHumanizerCopyAdapter.Humanize(CampaignSummary.RestoreSummary),
            PublicationSummary: BuildPublicationSummary(CampaignSummary.PublicationSummary, leadRecapShelfEntry),
            RosterSummary: UndetectableHumanizerCopyAdapter.Humanize(RosterReadiness.Summary),
            RunboardSummary: string.IsNullOrWhiteSpace(runboardSummary) ? null : UndetectableHumanizerCopyAdapter.Humanize(runboardSummary),
            TravelModeSummary: NormalizeOptional(TravelMode?.Summary),
            TravelPrefetchInventorySummary: NormalizeOptional(TravelMode?.PrefetchInventorySummary),
            CampaignMemorySummary: NormalizeOptional(CampaignMemory?.Summary),
            CampaignMemoryReturnSummary: NormalizeOptional(CampaignMemory?.ReturnSummary),
            AdoptionSummary: NormalizeOptional(Adoption?.Summary),
            AdoptionConfidenceSummary: NormalizeOptional(Adoption?.ConfidenceSummary),
            AdoptionEvidenceSummary: Adoption?.EvidenceLines.Count > 0 ? NormalizeOptional(Adoption.EvidenceLines[0]) : null,
            GoalPinSummary: GoalPins.Count > 0 ? BuildGoalPinSummary(GoalPins) : null,
            ResolutionReportSummary: NormalizeOptional(ResolutionReport?.Summary),
            BlackLedgerSummary: NormalizeOptional(BlackLedger?.Summary),
            BlackLedgerProofSummary: NormalizeOptional(BlackLedger?.ProofSummary),
            FirstPlayableSession: FirstPlayableSession,
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

    private static string BuildPublicationSummary(
        string publicationSummary,
        DesktopHomeRecapShelfEntryDto? leadRecapShelfEntry)
    {
        if (leadRecapShelfEntry is null || string.IsNullOrWhiteSpace(leadRecapShelfEntry.PublicationSummary))
        {
            return UndetectableHumanizerCopyAdapter.Humanize(publicationSummary);
        }

        return UndetectableHumanizerCopyAdapter.Humanize($"{publicationSummary} Published item: {leadRecapShelfEntry.PublicationSummary}");
    }

    private static string HumanizeAudience(string? audience)
    {
        if (string.IsNullOrWhiteSpace(audience))
        {
            return "Campaign stuff";
        }

        var labels = audience
            .Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.ToLowerInvariant() switch
            {
                "personal" => "My stuff",
                "campaign" => "Campaign stuff",
                "creator" => "Published stuff",
                _ => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('_', ' ').Replace('-', ' '))
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return labels.Length == 0 ? "Campaign stuff" : string.Join(", ", labels);
    }

    private static string HumanizeState(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            value.Replace('_', ' ').Replace('-', ' '));
    }

    private static IReadOnlyList<string> FinalizeLines(IEnumerable<string> lines)
        => lines
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => UndetectableHumanizerCopyAdapter.Humanize(item))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();

    private static string BuildGoalPinSummary(IReadOnlyList<DesktopHomeRunnerGoalPinDto> goalPins)
        => string.Join(
            "; ",
            goalPins
                .Take(2)
                .Select(static goalPin =>
                    string.IsNullOrWhiteSpace(goalPin.ProgressSummary)
                        ? goalPin.Label
                        : $"{goalPin.Label} ({goalPin.ProgressSummary})"));

    private static string? NormalizeOptional(string? value)
    {
        string cleaned = UndetectableHumanizerCopyAdapter.Humanize(value);
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
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

public sealed record DesktopHomeRecapShelfEntryDto(
    string Label,
    string Summary,
    string? Audience,
    string? OwnershipSummary,
    string? PublicationState,
    string? TrustBand,
    bool Discoverable,
    string? PublicationSummary,
    string? NextSafeAction);

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

public sealed record DesktopHomeCampaignAdoptionDto(
    string Summary,
    string ConfidenceSummary,
    string? NextSafeAction,
    IReadOnlyList<string> EvidenceLines);

public sealed record DesktopHomeRunnerGoalPinDto(
    string RunnerHandle,
    string Label,
    string? ProgressSummary,
    string? NextSafeAction);

public sealed record DesktopHomeResolutionReportCloseoutDto(
    string Summary,
    string? NextSafeAction,
    IReadOnlyList<string> EvidenceLines);

public sealed record DesktopHomeBlackLedgerConsequenceDto(
    string Summary,
    string? ProofSummary,
    string? SpoilerClass,
    string? NextSafeAction,
    IReadOnlyList<string> EvidenceLines);

public sealed record DesktopHomeNextSafeActionCueDto(string Summary);
