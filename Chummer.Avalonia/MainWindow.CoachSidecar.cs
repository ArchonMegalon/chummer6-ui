using Avalonia.Threading;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;
using Chummer.Contracts.Presentation;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Shell;
using System.Diagnostics;

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

    private void ResetCoachSidecarForDisabledAi()
    {
        _coachStatus = null;
        _coachProviderHealth = [];
        _coachAudits = [];
        _coachErrorMessage = null;
        _coachLaunchStatusMessage = null;
        _lastCoachScopeKey = null;
        _coachSidecarState = CoachSidecarPaneState.Empty;
        ApplyCoachSidecarState();
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
            _coachErrorMessage = result.NotImplemented?.Message
                ?? "Coach is unavailable for the current shell state right now. Open the browser route to continue there.";
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
        => BuildCoachAbsoluteLaunchUri(
            AiCoachLaunchQuery.BuildRelativeUri(
                "/coach/",
                new AiCoachLaunchContext(
                    RouteType: AiRouteTypes.Coach,
                    RuntimeFingerprint: shellSurface.ActiveRuntime?.RuntimeFingerprint,
                    WorkspaceId: shellSurface.ActiveWorkspaceId?.Value)));

    private void CoachSidecar_OnOpenLaunchRequested(object? sender, EventArgs e)
        => _ = OpenCoachLaunchUriAsync();

    private void CoachSidecar_OnCopyLaunchRequested(object? sender, EventArgs e)
        => _ = CopyCoachLaunchUriAsync();

    private async Task OpenCoachLaunchUriAsync()
    {
        string launchUri = _coachSidecarState.LaunchUri;
        if (string.IsNullOrWhiteSpace(launchUri))
        {
            _coachLaunchStatusMessage = "No Coach destination is available for the current shell state.";
            RebuildCoachSidecarState(_lastCoachShellSurface);
            ApplyCoachSidecarState();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = launchUri,
                UseShellExecute = true
            });
            _coachLaunchStatusMessage = "Coach opened in your browser.";
        }
        catch (Exception ex)
        {
            _coachLaunchStatusMessage = $"Could not open Coach in the browser: {ex.Message}";
        }

        RebuildCoachSidecarState(_lastCoachShellSurface);
        ApplyCoachSidecarState();
        await Task.CompletedTask;
    }

    private async Task CopyCoachLaunchUriAsync()
    {
        string launchUri = _coachSidecarState.LaunchUri;
        if (string.IsNullOrWhiteSpace(launchUri))
        {
            _coachLaunchStatusMessage = "No Coach destination is available for the current shell state.";
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
                _coachLaunchStatusMessage = "Coach browser link copied to the clipboard.";
            }
        }
        catch (Exception ex)
        {
            _coachLaunchStatusMessage = $"Could not copy the Coach browser link: {ex.Message}";
        }

        RebuildCoachSidecarState(_lastCoachShellSurface);
        ApplyCoachSidecarState();
    }

    private static string BuildCoachScopeKey(ShellSurfaceState shellSurface)
        => $"{shellSurface.ActiveWorkspaceId?.Value ?? "none"}|{shellSurface.ActiveRuntime?.RuntimeFingerprint ?? "none"}";

    private static string BuildCoachAbsoluteLaunchUri(string relativeLaunchUri)
    {
        if (string.IsNullOrWhiteSpace(relativeLaunchUri))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(relativeLaunchUri, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri.ToString();
        }

        string baseUrl = ResolveCoachPublicBaseUrl();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? baseUri)
            || !Uri.TryCreate(baseUri, relativeLaunchUri, out Uri? combinedUri))
        {
            return relativeLaunchUri;
        }

        return combinedUri.ToString();
    }

    private static string ResolveCoachPublicBaseUrl()
        => DesktopPublicPortalRuntime.ResolvePublicPortalBaseAddress().ToString();
}
