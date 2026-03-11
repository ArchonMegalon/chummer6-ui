using Chummer.Blazor;
using Chummer.Contracts.AI;
using Microsoft.AspNetCore.Components;

namespace Chummer.Blazor.Components.Layout;

public partial class DesktopShell
{
    private const int CoachAuditCount = 3;

    private AiGatewayStatusProjection? _coachStatus;
    private IReadOnlyList<AiProviderHealthProjection> _coachProviderHealth = [];
    private IReadOnlyList<AiConversationAuditSummary> _coachAudits = [];
    private string? _coachErrorMessage;
    private string? _lastCoachScopeKey;
    private bool _isCoachLoading;

    [Inject]
    public IWorkbenchCoachApiClient WorkbenchCoachApi { get; set; } = default!;

    private Task LoadCoachSidecarAsync()
        => RefreshCoachSidecarIfNeededAsync(force: true);

    private async Task RefreshCoachSidecarIfNeededAsync(bool force = false)
    {
        if (_isDisposed || _isCoachLoading)
        {
            return;
        }

        string scopeKey = BuildCoachScopeKey();
        if (!force && string.Equals(_lastCoachScopeKey, scopeKey, StringComparison.Ordinal))
        {
            return;
        }

        _isCoachLoading = true;
        try
        {
            if (await LoadCoachSidecarCoreAsync())
            {
                _lastCoachScopeKey = scopeKey;
            }
        }
        finally
        {
            _isCoachLoading = false;
            if (!_isDisposed)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private async Task<bool> LoadCoachSidecarCoreAsync()
    {
        WorkbenchCoachApiCallResult<AiGatewayStatusProjection> statusResult = await WorkbenchCoachApi.GetStatusAsync(CancellationToken.None);
        if (!TryCaptureCoachResult(statusResult, payload => _coachStatus = payload))
        {
            return false;
        }

        WorkbenchCoachApiCallResult<AiProviderHealthProjection[]> providerResult = await WorkbenchCoachApi.ListProviderHealthAsync(AiRouteTypes.Coach, CancellationToken.None);
        if (!TryCaptureCoachResult(providerResult, payload => _coachProviderHealth = payload))
        {
            return false;
        }

        WorkbenchCoachApiCallResult<AiConversationAuditCatalogPage> auditResult = await WorkbenchCoachApi.ListConversationAuditsAsync(
            AiRouteTypes.Coach,
            GetCoachRuntimeFingerprint(),
            CoachAuditCount,
            CancellationToken.None);
        TryCaptureCoachResult(auditResult, payload => _coachAudits = payload.Items);
        return true;
    }

    private bool TryCaptureCoachResult<T>(WorkbenchCoachApiCallResult<T> result, Action<T> apply)
    {
        _coachErrorMessage = null;

        if (!result.IsImplemented)
        {
            _coachErrorMessage = result.NotImplemented?.Message ?? "Coach sidecar route is not implemented yet.";
            return false;
        }

        if (result.QuotaExceeded is not null)
        {
            _coachErrorMessage = result.QuotaExceeded.Message;
            return false;
        }

        if (!result.IsSuccess)
        {
            _coachErrorMessage = result.ErrorMessage ?? $"Coach request failed with HTTP {result.StatusCode}.";
            return false;
        }

        if (result.Payload is null)
        {
            _coachErrorMessage = $"Coach request returned HTTP {result.StatusCode} without a payload.";
            return false;
        }

        apply(result.Payload);
        return true;
    }

    private string BuildCoachScopeKey()
        => $"{_shellSurfaceState.ActiveWorkspaceId?.Value ?? "none"}|{GetCoachRuntimeFingerprint() ?? "none"}";

    private string BuildCoachLaunchUri()
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: AiRouteTypes.Coach,
                RuntimeFingerprint: GetCoachRuntimeFingerprint(),
                WorkspaceId: _shellSurfaceState.ActiveWorkspaceId?.Value));

    private string BuildCoachLaunchUri(AiConversationAuditSummary audit)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: audit.RouteType,
                ConversationId: audit.ConversationId,
                RuntimeFingerprint: audit.RuntimeFingerprint ?? GetCoachRuntimeFingerprint(),
                CharacterId: audit.CharacterId,
                WorkspaceId: audit.WorkspaceId ?? _shellSurfaceState.ActiveWorkspaceId?.Value));

    private string? GetCoachRuntimeFingerprint()
        => _shellSurfaceState.ActiveRuntime?.RuntimeFingerprint;

    private AiRouteBudgetStatusProjection? GetCoachRouteBudgetStatus()
        => _coachStatus?.RouteBudgetStatuses?.FirstOrDefault(status =>
            string.Equals(status.RouteType, AiRouteTypes.Coach, StringComparison.Ordinal));

    private string GetCoachBudgetSummary()
    {
        AiRouteBudgetStatusProjection? budget = GetCoachRouteBudgetStatus();
        return budget is null
            ? "n/a"
            : $"{budget.MonthlyRemaining} left / {budget.BurstRemaining} burst";
    }

    private IReadOnlyList<AiProviderHealthProjection> GetCoachProviders()
        => _coachProviderHealth
            .Where(provider => provider.AllowedRouteTypes.Contains(AiRouteTypes.Coach))
            .ToArray();

    private string DescribeCoachProviderTransport(AiProviderHealthProjection provider)
    {
        string readiness = provider.TransportMetadataConfigured
            ? "ready"
            : provider.TransportBaseUrlConfigured || provider.TransportModelConfigured
                ? "partial"
                : "missing";
        return $"{readiness} · base {(provider.TransportBaseUrlConfigured ? "yes" : "no")} · model {(provider.TransportModelConfigured ? "yes" : "no")}";
    }

    private string DescribeCoachProviderKeys(AiProviderHealthProjection provider)
        => $"primary {provider.PrimaryCredentialCount} / fallback {provider.FallbackCredentialCount}";

    private static string DescribeCoachProviderBinding(AiProviderHealthProjection provider)
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

    private static string GetCoachProviderHealthBadgeClass(AiProviderHealthProjection provider)
        => provider.CircuitState switch
        {
            AiProviderCircuitStates.Open => "coach-badge error",
            AiProviderCircuitStates.Degraded => "coach-badge warn",
            AiProviderCircuitStates.Closed => "coach-badge good",
            _ => "coach-badge"
        };

    private static string GetCoachCacheBadgeClass(AiCacheMetadata? cache)
        => cache?.Status switch
        {
            AiCacheStatuses.Hit => "coach-badge good",
            AiCacheStatuses.Miss => "coach-badge warn",
            _ => "coach-badge"
        };

    private static string FormatCoachCacheStatus(AiCacheMetadata? cache)
        => cache?.Status switch
        {
            AiCacheStatuses.Hit => "cache hit",
            AiCacheStatuses.Miss => "cache miss",
            _ => "cache none"
        };

    private static string DescribeCoachCoverage(AiGroundingCoverage? coverage)
        => coverage is null
            ? "none"
            : $"{coverage.ScorePercent}% · {coverage.Summary}";

    private static string DescribeCoachBudgetSnapshot(AiBudgetSnapshot? budget)
        => budget is null
            ? "n/a"
            : $"{budget.MonthlyConsumed} / {budget.MonthlyAllowance} {budget.BudgetUnit} · burst {budget.CurrentBurstConsumed} / {budget.BurstLimitPerMinute}";

    private static string DescribeCoachRecommendationSummary(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Recommendations.Count == 0)
        {
            return "none";
        }

        AiRecommendation top = structuredAnswer.Recommendations[0];
        return $"{structuredAnswer.Recommendations.Count} · {top.Title}";
    }

    private static string DescribeCoachEvidenceSummary(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Evidence.Count == 0)
        {
            return "none";
        }

        AiEvidenceEntry lead = structuredAnswer.Evidence[0];
        return $"{structuredAnswer.Evidence.Count} · {lead.Title}";
    }

    private static string DescribeCoachRiskSummary(AiStructuredAnswer? structuredAnswer)
    {
        if (structuredAnswer is null || structuredAnswer.Risks.Count == 0)
        {
            return "none";
        }

        AiRiskEntry lead = structuredAnswer.Risks[0];
        return $"{structuredAnswer.Risks.Count} · {lead.Title}";
    }

    private static string DescribeCoachSourceSummary(AiStructuredAnswer? structuredAnswer)
        => structuredAnswer is null
            ? "0 sources / 0 action drafts"
            : $"{structuredAnswer.Sources.Count} sources / {structuredAnswer.ActionDrafts.Count} action drafts";

    private static string DescribeCoachRouteDecision(AiProviderRouteDecision? routeDecision, string providerId)
    {
        if (routeDecision is null)
        {
            return providerId;
        }

        string binding = DescribeCoachCredentialBinding(routeDecision);
        return string.Equals(binding, "none", StringComparison.Ordinal)
            ? $"{routeDecision.ProviderId} · {routeDecision.Reason}"
            : $"{routeDecision.ProviderId} · {routeDecision.Reason} · {binding}";
    }

    private static string DescribeCoachCredentialBinding(AiProviderRouteDecision routeDecision)
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

    private static string FormatCoachTimestamp(DateTimeOffset? value)
        => value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "n/a";

    private static string FormatCoachString(string? value)
        => string.IsNullOrWhiteSpace(value) ? "n/a" : value;
}
