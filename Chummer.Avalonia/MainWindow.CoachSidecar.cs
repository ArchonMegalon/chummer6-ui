using Avalonia.Threading;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Shell;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private AiGatewayStatusProjection? _coachStatus;
    private IReadOnlyList<AiProviderHealthProjection> _coachProviderHealth = [];
    private IReadOnlyList<AiConversationAuditSummary> _coachAudits = [];
    private CoachSidecarPaneState _coachSidecarState = CoachSidecarPaneState.Empty;
    private string? _coachErrorMessage;
    private string? _coachLaunchStatusMessage;
    private string? _lastCoachScopeKey;
    private ShellSurfaceState _lastCoachShellSurface = ShellSurfaceState.Empty;
    private bool _isCoachRefreshPending;

    private void ApplyCoachSidecarState()
    {
        _controls.ApplyCoachSidecar(_coachSidecarState);
    }

    private void QueueCoachSidecarRefreshIfNeeded(ShellSurfaceState shellSurface)
    {
        _lastCoachShellSurface = shellSurface;
        string scopeKey = BuildCoachScopeKey(shellSurface);
        RebuildCoachSidecarState(shellSurface);
        ApplyCoachSidecarState();

        if (_isCoachRefreshPending || string.Equals(_lastCoachScopeKey, scopeKey, StringComparison.Ordinal))
        {
            return;
        }

        _coachAudits = [];
        RebuildCoachSidecarState(shellSurface);
        ApplyCoachSidecarState();
        _isCoachRefreshPending = true;
        _ = RefreshCoachSidecarAsync(scopeKey, shellSurface);
    }

    private async Task RefreshCoachSidecarAsync(string scopeKey, ShellSurfaceState shellSurface)
    {
        try
        {
            AvaloniaCoachSidecarCallResult<AiGatewayStatusProjection> statusResult = await _coachSidecarClient.GetStatusAsync(CancellationToken.None);
            if (!TryCaptureCoachResult(statusResult, payload => _coachStatus = payload))
            {
                return;
            }

            AvaloniaCoachSidecarCallResult<AiProviderHealthProjection[]> providerResult = await _coachSidecarClient.ListProviderHealthAsync(AiRouteTypes.Coach, CancellationToken.None);
            if (!TryCaptureCoachResult(providerResult, payload => _coachProviderHealth = payload))
            {
                return;
            }

            AvaloniaCoachSidecarCallResult<AiConversationAuditCatalogPage> auditResult = await _coachSidecarClient.ListConversationAuditsAsync(
                AiRouteTypes.Coach,
                shellSurface.ActiveRuntime?.RuntimeFingerprint,
                3,
                CancellationToken.None);
            if (TryCaptureCoachResult(auditResult, payload => _coachAudits = payload.Items))
            {
                _lastCoachScopeKey = scopeKey;
            }
        }
        catch (Exception ex)
        {
            _coachErrorMessage = ex.Message;
            ApplyUiActionFailure("coach sidecar refresh", ex);
        }
        finally
        {
            _isCoachRefreshPending = false;
            RebuildCoachSidecarState(shellSurface);
            Dispatcher.UIThread.Post(ApplyCoachSidecarState);
        }
    }

    private bool TryCaptureCoachResult<T>(AvaloniaCoachSidecarCallResult<T> result, Action<T> apply)
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

    private void RebuildCoachSidecarState(ShellSurfaceState shellSurface)
    {
        _coachSidecarState = MainWindowCoachSidecarProjector.Project(
            _coachStatus,
            _coachProviderHealth,
            _coachAudits,
            shellSurface.ActiveWorkspaceId?.Value,
            shellSurface.ActiveRuntime?.RuntimeFingerprint,
            BuildCoachLaunchUri(shellSurface),
            _coachLaunchStatusMessage,
            _coachErrorMessage);
    }

    private string BuildCoachLaunchUri(ShellSurfaceState shellSurface)
        => AiCoachLaunchQuery.BuildRelativeUri(
            "/coach/",
            new AiCoachLaunchContext(
                RouteType: AiRouteTypes.Coach,
                RuntimeFingerprint: shellSurface.ActiveRuntime?.RuntimeFingerprint,
                WorkspaceId: shellSurface.ActiveWorkspaceId?.Value));

    private void CoachSidecar_OnCopyLaunchRequested(object? sender, EventArgs e)
        => _ = CopyCoachLaunchUriAsync();

    private async Task CopyCoachLaunchUriAsync()
    {
        string launchUri = _coachSidecarState.LaunchUri;
        if (string.IsNullOrWhiteSpace(launchUri) || string.Equals(launchUri, "n/a", StringComparison.Ordinal))
        {
            _coachLaunchStatusMessage = "No scoped Coach launch link is available for the current shell state.";
            RebuildCoachSidecarState(_lastCoachShellSurface);
            ApplyCoachSidecarState();
            return;
        }

        try
        {
            if (Clipboard is null)
            {
                _coachLaunchStatusMessage = "Clipboard is unavailable in this environment.";
            }
            else
            {
                await Clipboard.SetTextAsync(launchUri);
                _coachLaunchStatusMessage = "Scoped Coach launch link copied to the clipboard.";
            }
        }
        catch (Exception ex)
        {
            _coachLaunchStatusMessage = $"Could not copy the Coach launch link: {ex.Message}";
        }

        RebuildCoachSidecarState(_lastCoachShellSurface);
        ApplyCoachSidecarState();
    }

    private static string BuildCoachScopeKey(ShellSurfaceState shellSurface)
        => $"{shellSurface.ActiveWorkspaceId?.Value ?? "none"}|{shellSurface.ActiveRuntime?.RuntimeFingerprint ?? "none"}";
}
