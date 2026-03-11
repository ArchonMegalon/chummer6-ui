using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;

namespace Chummer.Avalonia;

public static class MainWindowCoachSidecarProjector
{
    public static CoachSidecarPaneState Project(
        AiGatewayStatusProjection? status,
        IReadOnlyList<AiProviderHealthProjection> providerHealth,
        IReadOnlyList<AiConversationAuditSummary> audits,
        string? workspaceId,
        string? runtimeFingerprint,
        string? launchUri,
        string? launchStatusMessage,
        string? errorMessage)
    {
        AiRouteBudgetStatusProjection? coachBudget = status?.RouteBudgetStatuses?.FirstOrDefault(entry =>
            string.Equals(entry.RouteType, AiRouteTypes.Coach, StringComparison.Ordinal));

        CoachProviderDisplayItem[] providers = providerHealth
            .Where(provider => provider.AllowedRouteTypes.Contains(AiRouteTypes.Coach))
            .Select(provider => new CoachProviderDisplayItem(
                DisplayName: provider.DisplayName,
                ProviderId: provider.ProviderId,
                AdapterKind: provider.AdapterKind,
                CircuitState: provider.CircuitState,
                TransportSummary: DescribeTransport(provider),
                CredentialSummary: DescribeCredentialCounts(provider),
                BindingSummary: DescribeBinding(provider),
                LastSuccess: FormatTimestamp(provider.LastSuccessAtUtc),
                LastFailure: string.IsNullOrWhiteSpace(provider.LastFailureMessage) ? "none" : provider.LastFailureMessage))
            .ToArray();

        CoachAuditDisplayItem[] auditItems = audits
            .Select(audit => new CoachAuditDisplayItem(
                ConversationId: audit.ConversationId,
                RuntimeFingerprint: string.IsNullOrWhiteSpace(audit.RuntimeFingerprint) ? "no runtime pin" : audit.RuntimeFingerprint,
                LaunchUri: BuildLaunchUri(audit, workspaceId, runtimeFingerprint),
                Summary: audit.LastAssistantAnswer ?? "No stored assistant answer yet.",
                FlavorLine: audit.FlavorLine,
                BudgetSummary: FormatBudgetSnapshot(audit.Budget),
                StructuredSummary: audit.StructuredAnswer?.Summary ?? "none",
                RecommendationSummary: DescribeRecommendations(audit.StructuredAnswer),
                EvidenceSummary: DescribeEvidence(audit.StructuredAnswer),
                RiskSummary: DescribeRisks(audit.StructuredAnswer),
                SourceSummary: DescribeSources(audit.StructuredAnswer),
                CacheStatus: FormatCacheStatus(audit.Cache),
                RouteDecision: DescribeRouteDecision(audit.RouteDecision, audit.LastProviderId ?? "n/a"),
                Coverage: DescribeCoverage(audit.GroundingCoverage),
                Updated: FormatTimestamp(audit.LastUpdatedAtUtc)))
            .ToArray();

        return new CoachSidecarPaneState(
            Status: status?.Status ?? "unloaded",
            PromptPolicy: status?.PromptPolicy ?? "n/a",
            BudgetSummary: coachBudget is null ? "n/a" : $"{coachBudget.MonthlyRemaining} left / {coachBudget.BurstRemaining} burst",
            WorkspaceId: string.IsNullOrWhiteSpace(workspaceId) ? "n/a" : workspaceId,
            RuntimeFingerprint: string.IsNullOrWhiteSpace(runtimeFingerprint) ? "n/a" : runtimeFingerprint,
            LaunchUri: string.IsNullOrWhiteSpace(launchUri) ? "n/a" : launchUri,
            LaunchStatusMessage: launchStatusMessage,
            ErrorMessage: errorMessage,
            Providers: providers,
            Audits: auditItems);
    }

    private static string FormatCacheStatus(AiCacheMetadata? cache)
        => cache?.Status switch
        {
            AiCacheStatuses.Hit => "cache hit",
            AiCacheStatuses.Miss => "cache miss",
            _ => "cache none"
        };

    private static string DescribeCoverage(AiGroundingCoverage? coverage)
        => coverage is null
            ? "none"
            : $"{coverage.ScorePercent}% · {coverage.Summary}";

    private static string BuildLaunchUri(AiConversationAuditSummary audit, string? workspaceId, string? runtimeFingerprint)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: audit.RouteType,
                ConversationId: audit.ConversationId,
                RuntimeFingerprint: audit.RuntimeFingerprint ?? runtimeFingerprint,
                CharacterId: audit.CharacterId,
                WorkspaceId: audit.WorkspaceId ?? workspaceId));

    private static string FormatBudgetSnapshot(AiBudgetSnapshot? budget)
        => budget is null
            ? "n/a"
            : $"{budget.MonthlyConsumed} / {budget.MonthlyAllowance} {budget.BudgetUnit} · burst {budget.CurrentBurstConsumed} / {budget.BurstLimitPerMinute}";

    private static string DescribeTransport(AiProviderHealthProjection provider)
    {
        string readiness = provider.TransportMetadataConfigured
            ? "ready"
            : provider.TransportBaseUrlConfigured || provider.TransportModelConfigured
                ? "partial"
                : "missing";
        return $"{readiness} · base {(provider.TransportBaseUrlConfigured ? "yes" : "no")} · model {(provider.TransportModelConfigured ? "yes" : "no")}";
    }

    private static string DescribeCredentialCounts(AiProviderHealthProjection provider)
        => $"primary {provider.PrimaryCredentialCount} / fallback {provider.FallbackCredentialCount}";

    private static string DescribeBinding(AiProviderHealthProjection provider)
    {
        string route = string.IsNullOrWhiteSpace(provider.LastRouteType)
            ? "route n/a"
            : $"route {provider.LastRouteType}";
        string binding = string.IsNullOrWhiteSpace(provider.LastCredentialTier)
            ? "binding none"
            : provider.LastCredentialSlotIndex is int slotIndex
                ? $"binding {provider.LastCredentialTier} / slot {slotIndex}"
                : $"binding {provider.LastCredentialTier}";
        return $"{route} · {binding}";
    }

    private static string DescribeRecommendations(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Recommendations.Count == 0)
        {
            return "none";
        }

        AiRecommendation top = structuredAnswer.Recommendations[0];
        return $"{structuredAnswer.Recommendations.Count} · {top.Title}";
    }

    private static string DescribeEvidence(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Evidence.Count == 0)
        {
            return "none";
        }

        AiEvidenceEntry lead = structuredAnswer.Evidence[0];
        return $"{structuredAnswer.Evidence.Count} · {lead.Title}";
    }

    private static string DescribeRisks(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Risks.Count == 0)
        {
            return "none";
        }

        AiRiskEntry lead = structuredAnswer.Risks[0];
        return $"{structuredAnswer.Risks.Count} · {lead.Title}";
    }

    private static string DescribeSources(AiStructuredAnswer? structuredAnswer)
        => structuredAnswer is null
            ? "0 sources / 0 action drafts"
            : $"{structuredAnswer.Sources.Count} sources / {structuredAnswer.ActionDrafts.Count} action drafts";

    private static string DescribeRouteDecision(AiProviderRouteDecision? routeDecision, string providerId)
    {
        if (routeDecision is null)
        {
            return providerId;
        }

        string binding = DescribeCredentialBinding(routeDecision);
        return string.Equals(binding, "none", StringComparison.Ordinal)
            ? $"{routeDecision.ProviderId} · {routeDecision.Reason}"
            : $"{routeDecision.ProviderId} · {routeDecision.Reason} · {binding}";
    }

    private static string DescribeCredentialBinding(AiProviderRouteDecision routeDecision)
    {
        if (string.IsNullOrWhiteSpace(routeDecision.CredentialTier)
            || string.Equals(routeDecision.CredentialTier, AiProviderCredentialTiers.None, StringComparison.Ordinal))
        {
            return "none";
        }

        return routeDecision.CredentialSlotIndex is int slotIndex
            ? $"{routeDecision.CredentialTier} / slot {slotIndex}"
            : routeDecision.CredentialTier;
    }

    private static string FormatTimestamp(DateTimeOffset? value)
        => value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "n/a";
}
