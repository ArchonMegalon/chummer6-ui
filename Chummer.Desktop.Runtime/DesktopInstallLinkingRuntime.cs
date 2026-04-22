using System.Net.Mime;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chummer.Hub.Registry.Contracts.InstallLinking;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

public sealed record DesktopInstallLinkingStartupContext(
    DesktopInstallLinkingState State,
    DesktopInstallClaimResult? ClaimResult,
    string? StartupClaimCode,
    bool ShouldPrompt,
    string PromptReason);

public sealed record DesktopInstallClaimResult(
    bool Succeeded,
    bool AlreadyClaimed,
    string Message,
    DesktopInstallLinkingState State);

public sealed record DesktopInstallLinkingState(
    string InstallationId,
    string HeadId,
    string ApplicationVersion,
    string ChannelId,
    string Platform,
    string Arch,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int LaunchCount,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LastPromptDismissedAtUtc,
    string PublicKey,
    string PrivateKey,
    string? ClaimTicketId = null,
    string? LastClaimCode = null,
    string? LastClaimMessage = null,
    string? LastClaimError = null,
    DateTimeOffset? LastClaimAttemptUtc = null,
    string? GrantId = null,
    string? GrantToken = null,
    DateTimeOffset? GrantIssuedAtUtc = null,
    DateTimeOffset? GrantExpiresAtUtc = null,
    string? UserId = null,
    string? SubjectId = null);

public static class DesktopInstallLinkingRuntime
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string WebBaseUrlEnvironmentVariable = "CHUMMER_WEB_BASE_URL";
    private const string ClaimCodeEnvironmentVariable = "CHUMMER_INSTALL_CLAIM_CODE";
    private const string InstallLinkCallbackEnvironmentVariable = "CHUMMER_INSTALL_LINK_CALLBACK_URI";
    private const string ClaimCodeSwitch = "--install-claim-code";
    private const string InstallLinkCallbackSwitch = "--install-link-callback";
    private const string StateRootDirectoryName = "install-linking";
    private const string PendingClaimCodeFileName = "pending-claim-code.txt";
    private const string PendingInstallLinkCallbackFileName = "pending-install-link-callback.txt";
    private const string ProtectedPrivateKeyFileName = "private-key.protected";
    private const string GuestStatus = "guest";
    private const string ClaimedStatus = "claimed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<DesktopInstallLinkingStartupContext> InitializeForStartupAsync(
        string headId,
        string[] args,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(args);

        DesktopInstallLinkingState state = LoadOrCreateState(headId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        state = RefreshRuntimeMetadata(state, now) with
        {
            LaunchCount = state.LaunchCount + 1,
            LastStartedAtUtc = now,
            UpdatedAtUtc = now
        };
        SaveState(state);

        DesktopInstallLinkingState preClaimState = state;
        string? startupBrowserCallbackCode = ExtractStartupBrowserCallbackCode(args, state);
        string? startupClaimCode = ExtractStartupClaimCode(args, state);
        DesktopInstallClaimResult? claimResult = null;
        if (!string.IsNullOrWhiteSpace(startupBrowserCallbackCode))
        {
            claimResult = await ExchangeBrowserCallbackCodeAsync(headId, startupBrowserCallbackCode, state, cancellationToken).ConfigureAwait(false);
            state = claimResult.State;
            if (claimResult.Succeeded)
            {
                TryDeletePendingClaimCode(preClaimState);
                TryDeletePendingClaimCode(state);
                TryDeletePendingInstallLinkCallback(preClaimState);
                TryDeletePendingInstallLinkCallback(state);
            }
        }
        else if (!string.IsNullOrWhiteSpace(startupClaimCode))
        {
            claimResult = await RedeemClaimCodeAsync(headId, startupClaimCode, state, cancellationToken).ConfigureAwait(false);
            state = claimResult.State;
            if (claimResult.Succeeded)
            {
                TryDeletePendingClaimCode(preClaimState);
                TryDeletePendingClaimCode(state);
                TryDeletePendingInstallLinkCallback(preClaimState);
                TryDeletePendingInstallLinkCallback(state);
            }
        }

        bool shouldPrompt = !IsClaimed(state)
            && (!string.IsNullOrWhiteSpace(startupBrowserCallbackCode)
                || !string.IsNullOrWhiteSpace(startupClaimCode));
        if (claimResult?.Succeeded == true)
        {
            shouldPrompt = false;
        }

        string promptReason = !string.IsNullOrWhiteSpace(startupBrowserCallbackCode)
            ? claimResult?.Succeeded == true ? "browser_callback_applied" : "browser_callback_present"
            : !string.IsNullOrWhiteSpace(startupClaimCode)
            ? claimResult?.Succeeded == true ? "claim_applied" : "claim_code_present"
            : "none";

        return new DesktopInstallLinkingStartupContext(
            State: state,
            ClaimResult: claimResult,
            StartupClaimCode: startupClaimCode,
            ShouldPrompt: shouldPrompt,
            PromptReason: promptReason);
    }

    public static DesktopInstallLinkingState LoadOrCreateState(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopRuntimeReleaseMetadata release = DesktopRuntimeReleaseMetadata.Load(headId);
        DesktopRuntimePlatformIdentity identity = DesktopRuntimePlatformIdentity.Current();
        DesktopInstallLinkingPaths paths = DesktopInstallLinkingPaths.Create(headId, identity);
        DesktopInstallLinkingState? state = DesktopInstallLinkingStateStore.Load(paths);
        if (state is null)
        {
            state = CreateInitialState(release, identity, DateTimeOffset.UtcNow);
            SaveState(state);
            return state;
        }

        if (ShouldProtectPrivateKeyAtRest()
            && !string.IsNullOrWhiteSpace(state.PrivateKey)
            && DesktopInstallLinkingStateStore.ShouldMigratePlaintextPrivateKey(paths))
        {
            DesktopInstallLinkingStateStore.Save(paths, state);
        }

        if (string.IsNullOrWhiteSpace(state.PublicKey) || string.IsNullOrWhiteSpace(state.PrivateKey))
        {
            (string publicKey, string privateKey) = CreateInstallationKeyPair();
            state = state with
            {
                PublicKey = publicKey,
                PrivateKey = privateKey,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            SaveState(state);
        }

        return RefreshRuntimeMetadata(state, DateTimeOffset.UtcNow);
    }

    public static async Task<DesktopInstallClaimResult> RedeemClaimCodeAsync(
        string headId,
        string claimCode,
        CancellationToken cancellationToken)
        => await RedeemClaimCodeAsync(headId, claimCode, LoadOrCreateState(headId), cancellationToken).ConfigureAwait(false);

    public static void MarkPromptDismissed(string headId)
    {
        DesktopInstallLinkingState state = LoadOrCreateState(headId);
        SaveState(state with
        {
            LastPromptDismissedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    public static bool TryOpenAccountPortal()
    {
        return TryOpenPublicPortal("/account#desktop");
    }

    public static bool TryOpenAccountPortalForInstall(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TryOpenPublicPortal(BuildAccountPortalRelativePathForInstall(state));
    }

    public static bool TryOpenRelativePortal(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        return TryOpenPublicPortal(relativePath.Trim());
    }

    public static bool TryOpenSupportPortal()
    {
        return TryOpenPublicPortal("/account/support");
    }

    public static bool TryOpenSupportPortalForInstall(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TryOpenPublicPortal(BuildSupportPortalRelativePathForInstall(state));
    }

    public static bool TryOpenSupportPortalForUpdate(DesktopInstallLinkingState state, DesktopUpdateClientStatus updateStatus)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(updateStatus);
        return TryOpenPublicPortal(BuildSupportPortalRelativePathForUpdate(state, updateStatus));
    }

    public static bool TryOpenSupportPortalForWorkspace(DesktopInstallLinkingState state, WorkspaceListItem? workspace)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TryOpenPublicPortal(BuildSupportPortalRelativePathForWorkspace(state, workspace));
    }

    public static bool TryOpenSupportPortalForBugReport(
        DesktopInstallLinkingState state,
        DesktopUpdateClientStatus? updateStatus,
        string title,
        string expectedBehavior,
        string actualBehavior,
        string reproSteps,
        string? evidenceNote = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TryOpenPublicPortal(BuildSupportPortalRelativePathForBugReport(
            state,
            updateStatus,
            title,
            expectedBehavior,
            actualBehavior,
            reproSteps,
            evidenceNote));
    }

    public static bool TryOpenSupportPortalForFeedback(
        DesktopInstallLinkingState state,
        DesktopUpdateClientStatus? updateStatus,
        string summary,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TryOpenPublicPortal(BuildSupportPortalRelativePathForFeedback(state, updateStatus, summary, detail));
    }

    public static bool TryOpenDownloadsPortal()
    {
        return TryOpenPublicPortal("/downloads");
    }

    public static bool TryOpenWorkPortal()
    {
        return TryOpenPublicPortal("/account/work");
    }

    public static bool TryOpenWorkspacePortal(string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return TryOpenWorkPortal();
        }

        return TryOpenPublicPortal($"/account/work/workspaces/{Uri.EscapeDataString(workspaceId.Trim())}");
    }

    public static string BuildSupportPortalRelativePathForInstall(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return BuildSupportPortalRelativePath(
            new SupportPortalPrefill(
                Kind: "install_help",
                Title: $"Desktop install handoff needs support for {state.HeadId}",
                Summary: $"This desktop install is {state.Status} on {state.ChannelId} {state.ApplicationVersion}.",
                Detail: string.Join(
                    "\n",
                    new[]
                    {
                        $"Install ID: {state.InstallationId}",
                        $"Head: {state.HeadId}",
                        $"Version: {state.ApplicationVersion}",
                        $"Channel: {state.ChannelId}",
                        $"Platform: {state.Platform}/{state.Arch}",
                        "Restore posture: review claimed-install entitlement, stale-state visibility, and conflict choices before restoring workspace continuity.",
                        "Stale-state visibility: keep the local workspace visible until support confirms the current continuity packet.",
                        "Conflict choices: keep local work, save local work, or review Campaign Workspace before accepting restore replacement.",
                        string.IsNullOrWhiteSpace(state.LastClaimMessage) ? null : $"Hub message: {state.LastClaimMessage}",
                        string.IsNullOrWhiteSpace(state.LastClaimError) ? null : $"Claim error: {state.LastClaimError}"
                    }.Where(static item => !string.IsNullOrWhiteSpace(item))),
                InstallationId: state.InstallationId,
                ApplicationVersion: state.ApplicationVersion,
                ReleaseChannel: state.ChannelId,
                HeadId: state.HeadId,
                Platform: state.Platform,
                Arch: state.Arch));
    }

    public static string BuildAccountPortalRelativePathForInstall(DesktopInstallLinkingState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (IsClaimed(state))
        {
            return "/account/access#desktop";
        }

        List<string> query = [];
        AppendQueryParameter(query, "installationId", state.InstallationId);
        AppendQueryParameter(query, "headId", state.HeadId);
        AppendQueryParameter(query, "applicationVersion", state.ApplicationVersion);
        AppendQueryParameter(query, "releaseChannel", state.ChannelId);
        AppendQueryParameter(query, "platform", state.Platform);
        AppendQueryParameter(query, "arch", state.Arch);
        AppendQueryParameter(query, "installLinkMode", "browser_callback");
        AppendQueryParameter(query, "installLinkTransport", "grant_callback");
        AppendQueryParameter(query, "installLinkCallbackUri", BuildInstallLinkCallbackUri());

        return query.Count == 0
            ? "/account/access/install-link"
            : $"/account/access/install-link?{string.Join("&", query)}";
    }

    public static string BuildSupportPortalRelativePathForUpdate(DesktopInstallLinkingState state, DesktopUpdateClientStatus updateStatus)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(updateStatus);
        return BuildSupportPortalRelativePath(
            new SupportPortalPrefill(
                Kind: "install_help",
                Title: $"Desktop update posture needs review for {updateStatus.HeadId}",
                Summary: $"This desktop install is {updateStatus.Status} on {updateStatus.ChannelId} {updateStatus.InstalledVersion}.",
                Detail: string.Join(
                    "\n",
                    new[]
                    {
                        $"Install ID: {state.InstallationId}",
                        $"Manifest: {updateStatus.LastManifestVersion ?? "unknown"}",
                        updateStatus.LastManifestPublishedAtUtc is null
                            ? null
                            : $"Manifest published: {updateStatus.LastManifestPublishedAtUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
                        string.IsNullOrWhiteSpace(updateStatus.RolloutState) ? null : $"Release posture: {updateStatus.RolloutState}",
                        string.IsNullOrWhiteSpace(updateStatus.SupportabilityState) ? null : $"Supportability: {updateStatus.SupportabilityState}",
                        string.IsNullOrWhiteSpace(updateStatus.SupportabilitySummary) ? null : $"Supportability summary: {updateStatus.SupportabilitySummary}",
                        string.IsNullOrWhiteSpace(updateStatus.KnownIssueSummary) ? null : $"Known issues: {updateStatus.KnownIssueSummary}",
                        string.IsNullOrWhiteSpace(updateStatus.FixAvailabilitySummary) ? null : $"Fix availability: {updateStatus.FixAvailabilitySummary}",
                        string.IsNullOrWhiteSpace(updateStatus.ProofStatus) ? null : $"Local release proof: {updateStatus.ProofStatus}",
                        updateStatus.ProofGeneratedAtUtc is null
                            ? null
                            : $"Proof generated: {updateStatus.ProofGeneratedAtUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
                        $"Recommended action: {updateStatus.RecommendedAction}",
                        string.IsNullOrWhiteSpace(updateStatus.LastError) ? null : $"Last error: {updateStatus.LastError}"
                    }.Where(static item => !string.IsNullOrWhiteSpace(item))),
                InstallationId: state.InstallationId,
                ApplicationVersion: updateStatus.InstalledVersion,
                ReleaseChannel: updateStatus.ChannelId,
                HeadId: updateStatus.HeadId,
                Platform: updateStatus.Platform,
                Arch: updateStatus.Arch));
    }

    public static string BuildSupportPortalRelativePathForWorkspace(DesktopInstallLinkingState state, WorkspaceListItem? workspace)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (workspace is null)
        {
            return BuildSupportPortalRelativePath(
                new SupportPortalPrefill(
                    Kind: "bug_report",
                    Title: $"Workspace restore review needs help for {state.HeadId}",
                    Summary: "No local workspace is selected, but restore continuation, stale-state visibility, and conflict choices need support review before replacement.",
                    Detail: string.Join(
                        "\n",
                        new[]
                        {
                            "Workspace: none selected",
                            "Workspace ID: none",
                            "Ruleset: unknown",
                            "Local save state: no local workspace is selected.",
                            "Restore posture: review workspace continuation, stale-state visibility, and conflict choices before replacing local work.",
                            "Stale-state visibility: keep the local workspace list visible until support confirms the current continuity packet.",
                            "Conflict choices: keep local work visible, save local work when available, review Campaign Workspace, or open Workspace Support before accepting restore replacement.",
                            $"Install ID: {state.InstallationId}",
                            $"Head: {state.HeadId}",
                            $"Version: {state.ApplicationVersion}"
                        }.Where(static item => !string.IsNullOrWhiteSpace(item))),
                    InstallationId: state.InstallationId,
                    ApplicationVersion: state.ApplicationVersion,
                    ReleaseChannel: state.ChannelId,
                    HeadId: state.HeadId,
                    Platform: state.Platform,
                    Arch: state.Arch));
        }

        string workspaceName = string.IsNullOrWhiteSpace(workspace.Summary.Name)
            ? workspace.Id.Value
            : workspace.Summary.Name;
        return BuildSupportPortalRelativePath(
            new SupportPortalPrefill(
                Kind: "bug_report",
                Title: $"Workspace follow-through needs help for {workspaceName}",
                Summary: $"Current workspace {workspaceName} on {workspace.RulesetId} needs support from the desktop home cockpit.",
                Detail: string.Join(
                    "\n",
                    new[]
                    {
                        $"Workspace: {workspaceName}",
                        $"Workspace ID: {workspace.Id.Value}",
                        $"Ruleset: {workspace.RulesetId}",
                        $"Build method: {workspace.Summary.BuildMethod}",
                        $"Last local update: {workspace.LastUpdatedUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
                        $"Local save state: {(workspace.HasSavedWorkspace ? "saved workspace is available" : "unsaved or missing local workspace file")}.",
                        "Restore posture: review workspace continuation, stale-state visibility, and conflict choices before replacing local work.",
                        "Stale-state visibility: keep the local workspace visible until support confirms the current continuity packet.",
                        "Conflict choices: keep local work, save local work, or review Campaign Workspace before accepting restore replacement.",
                        $"Install ID: {state.InstallationId}",
                        $"Head: {state.HeadId}",
                        $"Version: {state.ApplicationVersion}"
                    }.Where(static item => !string.IsNullOrWhiteSpace(item))),
                InstallationId: state.InstallationId,
                ApplicationVersion: state.ApplicationVersion,
                ReleaseChannel: state.ChannelId,
                HeadId: state.HeadId,
                Platform: state.Platform,
                Arch: state.Arch));
    }

    public static string BuildSupportPortalRelativePathForBugReport(
        DesktopInstallLinkingState state,
        DesktopUpdateClientStatus? updateStatus,
        string title,
        string expectedBehavior,
        string actualBehavior,
        string reproSteps,
        string? evidenceNote = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        string normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? $"Desktop bug report for {state.HeadId}"
            : title.Trim();
        string normalizedActualBehavior = NormalizeSupportDraftField(actualBehavior);
        return BuildSupportPortalRelativePath(
            new SupportPortalPrefill(
                Kind: "bug_report",
                Title: normalizedTitle,
                Summary: $"Bug report from {state.HeadId}: {normalizedActualBehavior}",
                Detail: string.Join(
                    "\n",
                    BuildSupportDraftContextLines(
                        state,
                        updateStatus,
                        $"Expected: {NormalizeSupportDraftField(expectedBehavior)}",
                        $"Actual: {normalizedActualBehavior}",
                        $"Repro: {NormalizeSupportDraftField(reproSteps)}",
                        string.IsNullOrWhiteSpace(evidenceNote) ? null : $"Evidence: {evidenceNote.Trim()}")),
                InstallationId: state.InstallationId,
                ApplicationVersion: state.ApplicationVersion,
                ReleaseChannel: state.ChannelId,
                HeadId: state.HeadId,
                Platform: state.Platform,
                Arch: state.Arch));
    }

    public static string BuildSupportPortalRelativePathForFeedback(
        DesktopInstallLinkingState state,
        DesktopUpdateClientStatus? updateStatus,
        string summary,
        string detail)
    {
        ArgumentNullException.ThrowIfNull(state);

        string normalizedSummary = NormalizeSupportDraftField(summary);
        return BuildSupportPortalRelativePath(
            new SupportPortalPrefill(
                Kind: "feedback",
                Title: string.IsNullOrWhiteSpace(summary)
                    ? $"Desktop feedback for {state.HeadId}"
                    : $"Desktop feedback: {summary.Trim()}",
                Summary: normalizedSummary,
                Detail: string.Join(
                    "\n",
                    BuildSupportDraftContextLines(
                        state,
                        updateStatus,
                        $"Feedback: {normalizedSummary}",
                        $"Detail: {NormalizeSupportDraftField(detail)}")),
                InstallationId: state.InstallationId,
                ApplicationVersion: state.ApplicationVersion,
                ReleaseChannel: state.ChannelId,
                HeadId: state.HeadId,
                Platform: state.Platform,
                Arch: state.Arch));
    }

    public static bool IsClaimed(DesktopInstallLinkingState state)
        => string.Equals(state.Status, ClaimedStatus, StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(state.GrantToken);

    private static async Task<DesktopInstallClaimResult> RedeemClaimCodeAsync(
        string headId,
        string claimCode,
        DesktopInstallLinkingState state,
        CancellationToken cancellationToken)
    {
        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (normalizedClaimCode is null)
        {
            DesktopInstallLinkingState invalidState = state with
            {
                LastClaimAttemptUtc = DateTimeOffset.UtcNow,
                LastClaimCode = null,
                LastClaimError = "Claim code is required.",
                LastClaimMessage = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            SaveState(invalidState);
            return new DesktopInstallClaimResult(false, false, "Claim code is required.", invalidState);
        }

        DesktopInstallLinkingState currentState = RefreshRuntimeMetadata(state, DateTimeOffset.UtcNow);
        DateTimeOffset attemptAtUtc = DateTimeOffset.UtcNow;
        try
        {
            RedeemInstallClaimRequestDto request = new(
                ClaimCode: normalizedClaimCode,
                InstallationId: currentState.InstallationId,
                HeadId: currentState.HeadId,
                ApplicationVersion: currentState.ApplicationVersion,
                ChannelId: currentState.ChannelId,
                Platform: currentState.Platform,
                Arch: currentState.Arch,
                PublicKey: currentState.PublicKey,
                HostLabel: null);

            using HttpClient client = CreateApiHttpClient(TimeSpan.FromSeconds(20));
            using StringContent content = new(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            using HttpResponseMessage response = await client.PostAsync("api/v1/install-linking/redeem", content, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string error = BuildErrorMessage(response, responseText);
                DesktopInstallLinkingState failedState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = normalizedClaimCode,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(failedState);
                return new DesktopInstallClaimResult(false, false, error, failedState);
            }

            RedeemInstallClaimResponseDto? accepted = JsonSerializer.Deserialize<RedeemInstallClaimResponseDto>(responseText, JsonOptions);
            if (accepted?.Installation is null || accepted.Grant is null || accepted.Ticket is null)
            {
                string error = "Hub accepted install claim redemption but returned an unreadable payload.";
                DesktopInstallLinkingState invalidState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = normalizedClaimCode,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(invalidState);
                return new DesktopInstallClaimResult(false, false, error, invalidState);
            }

            DesktopInstallLinkingState claimedState = currentState with
            {
                Status = ClaimedStatus,
                ClaimedAtUtc = currentState.ClaimedAtUtc ?? attemptAtUtc,
                ClaimTicketId = accepted.Ticket.TicketId,
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = normalizedClaimCode,
                LastClaimError = null,
                LastClaimMessage = accepted.AlreadyClaimed
                    ? "This copy was already linked. Hub refreshed the installation grant."
                    : "This copy is now linked to your Hub account.",
                UpdatedAtUtc = attemptAtUtc,
                HeadId = accepted.Installation.HeadId ?? currentState.HeadId,
                ApplicationVersion = accepted.Installation.Version,
                ChannelId = accepted.Installation.Channel,
                Platform = accepted.Installation.Platform ?? currentState.Platform,
                Arch = accepted.Installation.Arch ?? currentState.Arch,
                GrantId = accepted.Grant.GrantId,
                GrantToken = accepted.Grant.AccessToken,
                GrantIssuedAtUtc = accepted.Grant.IssuedAtUtc,
                GrantExpiresAtUtc = accepted.Grant.ExpiresAtUtc,
                UserId = accepted.Installation.UserId,
                SubjectId = accepted.Installation.SubjectId
            };
            SaveState(claimedState);
            return new DesktopInstallClaimResult(
                true,
                accepted.AlreadyClaimed,
                claimedState.LastClaimMessage ?? "This copy is linked.",
                claimedState);
        }
        catch (Exception ex)
        {
            DesktopInstallLinkingState failedState = currentState with
            {
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = normalizedClaimCode,
                LastClaimError = ex.Message,
                LastClaimMessage = null,
                UpdatedAtUtc = attemptAtUtc
            };
            SaveState(failedState);
            return new DesktopInstallClaimResult(false, false, $"Install linking failed: {ex.Message}", failedState);
        }
    }

    private static async Task<DesktopInstallClaimResult> ExchangeBrowserCallbackCodeAsync(
        string headId,
        string callbackCode,
        DesktopInstallLinkingState state,
        CancellationToken cancellationToken)
    {
        string? normalizedCallbackCode = NormalizeBrowserCallbackCode(callbackCode);
        if (normalizedCallbackCode is null)
        {
            DesktopInstallLinkingState invalidState = state with
            {
                LastClaimAttemptUtc = DateTimeOffset.UtcNow,
                LastClaimCode = null,
                LastClaimError = "Browser callback code is required.",
                LastClaimMessage = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            SaveState(invalidState);
            return new DesktopInstallClaimResult(false, false, "Browser callback code is required.", invalidState);
        }

        DesktopInstallLinkingState currentState = RefreshRuntimeMetadata(state, DateTimeOffset.UtcNow);
        DateTimeOffset attemptAtUtc = DateTimeOffset.UtcNow;
        try
        {
            ExchangeInstallBrowserCallbackRequestDto request = new(
                CallbackCode: normalizedCallbackCode,
                InstallationId: currentState.InstallationId,
                HeadId: currentState.HeadId,
                ApplicationVersion: currentState.ApplicationVersion,
                ChannelId: currentState.ChannelId,
                Platform: currentState.Platform,
                Arch: currentState.Arch,
                PublicKey: currentState.PublicKey,
                HostLabel: null);

            using HttpClient client = CreateApiHttpClient(TimeSpan.FromSeconds(20));
            using StringContent content = new(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            using HttpResponseMessage response = await client.PostAsync("api/v1/install-linking/callbacks/exchange", content, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string error = BuildErrorMessage(response, responseText);
                DesktopInstallLinkingState failedState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = null,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(failedState);
                return new DesktopInstallClaimResult(false, false, error, failedState);
            }

            ExchangeInstallBrowserCallbackResponseDto? accepted = JsonSerializer.Deserialize<ExchangeInstallBrowserCallbackResponseDto>(responseText, JsonOptions);
            if (accepted?.Installation is null || accepted.Grant is null || accepted.Callback is null)
            {
                string error = "Hub accepted the browser install callback but returned an unreadable payload.";
                DesktopInstallLinkingState invalidState = currentState with
                {
                    LastClaimAttemptUtc = attemptAtUtc,
                    LastClaimCode = null,
                    LastClaimError = error,
                    LastClaimMessage = null,
                    UpdatedAtUtc = attemptAtUtc
                };
                SaveState(invalidState);
                return new DesktopInstallClaimResult(false, false, error, invalidState);
            }

            DesktopInstallLinkingState claimedState = currentState with
            {
                Status = ClaimedStatus,
                ClaimedAtUtc = currentState.ClaimedAtUtc ?? attemptAtUtc,
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = null,
                LastClaimError = null,
                LastClaimMessage = accepted.AlreadyClaimed
                    ? "This copy was already linked. Hub refreshed the installation grant from the browser callback."
                    : "This copy is now linked to your Hub account.",
                UpdatedAtUtc = attemptAtUtc,
                HeadId = accepted.Installation.HeadId ?? currentState.HeadId,
                ApplicationVersion = accepted.Installation.Version,
                ChannelId = accepted.Installation.Channel,
                Platform = accepted.Installation.Platform ?? currentState.Platform,
                Arch = accepted.Installation.Arch ?? currentState.Arch,
                GrantId = accepted.Grant.GrantId,
                GrantToken = accepted.Grant.AccessToken,
                GrantIssuedAtUtc = accepted.Grant.IssuedAtUtc,
                GrantExpiresAtUtc = accepted.Grant.ExpiresAtUtc,
                UserId = accepted.Installation.UserId,
                SubjectId = accepted.Installation.SubjectId
            };
            SaveState(claimedState);
            return new DesktopInstallClaimResult(
                true,
                accepted.AlreadyClaimed,
                claimedState.LastClaimMessage ?? "This copy is linked.",
                claimedState);
        }
        catch (Exception ex)
        {
            DesktopInstallLinkingState failedState = currentState with
            {
                LastClaimAttemptUtc = attemptAtUtc,
                LastClaimCode = null,
                LastClaimError = ex.Message,
                LastClaimMessage = null,
                UpdatedAtUtc = attemptAtUtc
            };
            SaveState(failedState);
            return new DesktopInstallClaimResult(false, false, $"Install linking failed: {ex.Message}", failedState);
        }
    }

    private static DesktopInstallLinkingState CreateInitialState(
        DesktopRuntimeReleaseMetadata release,
        DesktopRuntimePlatformIdentity identity,
        DateTimeOffset now)
    {
        (string publicKey, string privateKey) = CreateInstallationKeyPair();
        return new DesktopInstallLinkingState(
            InstallationId: $"ins-{Guid.NewGuid():N}",
            HeadId: release.HeadId,
            ApplicationVersion: release.Version,
            ChannelId: release.ChannelId,
            Platform: identity.Platform,
            Arch: identity.Arch,
            Status: GuestStatus,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 0,
            LastStartedAtUtc: null,
            ClaimedAtUtc: null,
            LastPromptDismissedAtUtc: null,
            PublicKey: publicKey,
            PrivateKey: privateKey);
    }

    private static DesktopInstallLinkingState RefreshRuntimeMetadata(DesktopInstallLinkingState state, DateTimeOffset now)
    {
        DesktopRuntimeReleaseMetadata release = DesktopRuntimeReleaseMetadata.Load(state.HeadId);
        DesktopRuntimePlatformIdentity identity = DesktopRuntimePlatformIdentity.Current();
        return state with
        {
            HeadId = release.HeadId,
            ApplicationVersion = string.IsNullOrWhiteSpace(release.Version) ? state.ApplicationVersion : release.Version,
            ChannelId = string.IsNullOrWhiteSpace(release.ChannelId) ? state.ChannelId : release.ChannelId,
            Platform = identity.Platform,
            Arch = identity.Arch,
            UpdatedAtUtc = now
        };
    }

    private static void SaveState(DesktopInstallLinkingState state)
    {
        DesktopInstallLinkingPaths paths = DesktopInstallLinkingPaths.Create(
            state.HeadId,
            new DesktopRuntimePlatformIdentity(state.Platform, state.Arch));
        DesktopInstallLinkingStateStore.Save(paths, state);
    }

    private static string? ExtractStartupBrowserCallbackCode(IReadOnlyList<string> args, DesktopInstallLinkingState state)
    {
        if (TryExtractBrowserCallbackCodeFromCallbackUri(Environment.GetEnvironmentVariable(InstallLinkCallbackEnvironmentVariable), out string? callbackCode))
        {
            return callbackCode;
        }

        for (int i = 0; i < args.Count; i++)
        {
            if (TryReadBrowserCallbackCodeFromCallbackArgument(args, i, out string? callbackArgumentCode))
            {
                return callbackArgumentCode;
            }
        }

        foreach (string pendingPath in GetPendingInstallLinkCallbackPaths(state))
        {
            if (!File.Exists(pendingPath))
            {
                continue;
            }

            try
            {
                string pendingValue = File.ReadAllText(pendingPath, Encoding.UTF8);
                if (TryExtractBrowserCallbackCodeFromCallbackUri(pendingValue, out string? pendingCallbackCode))
                {
                    return pendingCallbackCode;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static string? ExtractStartupClaimCode(IReadOnlyList<string> args, DesktopInstallLinkingState state)
    {
        string? fromEnvironment = NormalizeClaimCode(Environment.GetEnvironmentVariable(ClaimCodeEnvironmentVariable));
        if (fromEnvironment is not null)
        {
            return fromEnvironment;
        }

        if (TryExtractClaimCodeFromCallbackUri(Environment.GetEnvironmentVariable(InstallLinkCallbackEnvironmentVariable), out string? callbackClaimCode))
        {
            return callbackClaimCode;
        }

        for (int i = 0; i < args.Count; i++)
        {
            if (TryReadValueAfterSwitch(args, i, out string? claimCode))
            {
                return claimCode;
            }

            if (TryReadClaimCodeFromCallbackArgument(args, i, out string? callbackArgumentClaimCode))
            {
                return callbackArgumentClaimCode;
            }
        }

        foreach (string pendingPath in GetPendingClaimCodePaths(state))
        {
            if (!File.Exists(pendingPath))
            {
                continue;
            }

            try
            {
                string pendingValue = File.ReadAllText(pendingPath, Encoding.UTF8);
                string? normalizedPendingValue = NormalizeClaimCode(pendingValue);
                if (normalizedPendingValue is not null)
                {
                    return normalizedPendingValue;
                }
            }
            catch (IOException)
            {
                // Fall back to other candidate claim paths.
            }
            catch (UnauthorizedAccessException)
            {
                // Fall back to other candidate claim paths.
            }
        }

        foreach (string pendingPath in GetPendingInstallLinkCallbackPaths(state))
        {
            if (!File.Exists(pendingPath))
            {
                continue;
            }

            try
            {
                string pendingValue = File.ReadAllText(pendingPath, Encoding.UTF8);
                if (TryExtractClaimCodeFromCallbackUri(pendingValue, out string? pendingCallbackClaimCode))
                {
                    return pendingCallbackClaimCode;
                }
            }
            catch (IOException)
            {
                // Fall back to other candidate callback paths.
            }
            catch (UnauthorizedAccessException)
            {
                // Fall back to other candidate callback paths.
            }
        }

        return null;
    }

    private static bool TryReadValueAfterSwitch(IReadOnlyList<string> args, int index, out string? claimCode)
    {
        claimCode = null;
        string arg = args[index];
        ReadOnlySpan<char> argSpan = arg.AsSpan().Trim();
        if (argSpan.Length == 0)
        {
            return false;
        }

        if (string.Equals(argSpan.ToString(), ClaimCodeSwitch, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Count)
            {
                claimCode = NormalizeClaimCode(args[index + 1]);
                return claimCode is not null;
            }

            return false;
        }

        if (argSpan[0] == '/')
        {
            argSpan = argSpan[1..];
        }
        else if (argSpan[0] == '-')
        {
            argSpan = argSpan[1..];
            if (argSpan.Length > 0 && argSpan[0] == '-')
            {
                argSpan = argSpan[1..];
            }
        }

        string normalizedSwitch = ClaimCodeSwitch.AsSpan(2).ToString();
        string normalizedArg = argSpan.ToString();
        if (string.Equals(normalizedArg, normalizedSwitch, StringComparison.OrdinalIgnoreCase)
            && index + 1 < args.Count)
        {
            claimCode = NormalizeClaimCode(args[index + 1]);
            return claimCode is not null;
        }

        string legacyEqualsPrefix = $"{normalizedSwitch}=";
        if (normalizedArg.StartsWith(legacyEqualsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            claimCode = NormalizeClaimCode(normalizedArg[legacyEqualsPrefix.Length..]);
            return claimCode is not null;
        }

        string legacyColonPrefix = $"{normalizedSwitch}:";
        if (normalizedArg.StartsWith(legacyColonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            claimCode = NormalizeClaimCode(normalizedArg[legacyColonPrefix.Length..]);
            return claimCode is not null;
        }

        return false;
    }

    private static bool TryReadClaimCodeFromCallbackArgument(IReadOnlyList<string> args, int index, out string? claimCode)
    {
        claimCode = null;

        if (index < 0 || index >= args.Count)
        {
            return false;
        }

        string arg = args[index];
        if (TryExtractClaimCodeFromCallbackUri(arg, out claimCode))
        {
            return true;
        }

        ReadOnlySpan<char> argSpan = arg.AsSpan().Trim();
        if (argSpan.Length == 0)
        {
            return false;
        }

        if (string.Equals(argSpan.ToString(), InstallLinkCallbackSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Count && TryExtractClaimCodeFromCallbackUri(args[index + 1], out claimCode);
        }

        if (argSpan[0] == '/')
        {
            argSpan = argSpan[1..];
        }
        else if (argSpan[0] == '-')
        {
            argSpan = argSpan[1..];
            if (argSpan.Length > 0 && argSpan[0] == '-')
            {
                argSpan = argSpan[1..];
            }
        }

        string normalizedSwitch = InstallLinkCallbackSwitch.AsSpan(2).ToString();
        string normalizedArg = argSpan.ToString();
        if (string.Equals(normalizedArg, normalizedSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Count && TryExtractClaimCodeFromCallbackUri(args[index + 1], out claimCode);
        }

        string equalsPrefix = $"{normalizedSwitch}=";
        if (normalizedArg.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryExtractClaimCodeFromCallbackUri(normalizedArg[equalsPrefix.Length..], out claimCode);
        }

        string colonPrefix = $"{normalizedSwitch}:";
        if (normalizedArg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryExtractClaimCodeFromCallbackUri(normalizedArg[colonPrefix.Length..], out claimCode);
        }

        return false;
    }

    private static bool TryReadBrowserCallbackCodeFromCallbackArgument(IReadOnlyList<string> args, int index, out string? callbackCode)
    {
        callbackCode = null;

        if (index < 0 || index >= args.Count)
        {
            return false;
        }

        string arg = args[index];
        if (TryExtractBrowserCallbackCodeFromCallbackUri(arg, out callbackCode))
        {
            return true;
        }

        ReadOnlySpan<char> argSpan = arg.AsSpan().Trim();
        if (argSpan.Length == 0)
        {
            return false;
        }

        if (string.Equals(argSpan.ToString(), InstallLinkCallbackSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Count && TryExtractBrowserCallbackCodeFromCallbackUri(args[index + 1], out callbackCode);
        }

        if (argSpan[0] == '/')
        {
            argSpan = argSpan[1..];
        }
        else if (argSpan[0] == '-')
        {
            argSpan = argSpan[1..];
            if (argSpan.Length > 0 && argSpan[0] == '-')
            {
                argSpan = argSpan[1..];
            }
        }

        string normalizedSwitch = InstallLinkCallbackSwitch.AsSpan(2).ToString();
        string normalizedArg = argSpan.ToString();
        if (string.Equals(normalizedArg, normalizedSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return index + 1 < args.Count && TryExtractBrowserCallbackCodeFromCallbackUri(args[index + 1], out callbackCode);
        }

        string equalsPrefix = $"{normalizedSwitch}=";
        if (normalizedArg.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryExtractBrowserCallbackCodeFromCallbackUri(normalizedArg[equalsPrefix.Length..], out callbackCode);
        }

        string colonPrefix = $"{normalizedSwitch}:";
        if (normalizedArg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TryExtractBrowserCallbackCodeFromCallbackUri(normalizedArg[colonPrefix.Length..], out callbackCode);
        }

        return false;
    }

    private static IReadOnlyList<string> GetPendingClaimCodePaths(DesktopInstallLinkingState state)
    {
        HashSet<string> pendingPaths = [];
        string stateRoot = GetStateRoot();
        foreach (string platform in NormalizePlatformAliases(state.Platform))
        {
            foreach (string architecture in NormalizeArchitectureAliases(state.Arch))
            {
                pendingPaths.Add(Path.Combine(stateRoot, StateRootDirectoryName, state.HeadId, platform, architecture, PendingClaimCodeFileName));
                if (!string.IsNullOrWhiteSpace(platform) && !string.IsNullOrWhiteSpace(architecture))
                {
                    pendingPaths.Add(Path.Combine(stateRoot, StateRootDirectoryName, state.HeadId, $"{platform}-{architecture}", PendingClaimCodeFileName));
                }
            }
        }

        pendingPaths.Add(Path.Combine(GetStateDirectory(stateRoot, state), PendingClaimCodeFileName));
        return pendingPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).ToList();
    }

    private static IReadOnlyList<string> GetPendingInstallLinkCallbackPaths(DesktopInstallLinkingState state)
    {
        HashSet<string> pendingPaths = [];
        string stateRoot = GetStateRoot();
        foreach (string platform in NormalizePlatformAliases(state.Platform))
        {
            foreach (string architecture in NormalizeArchitectureAliases(state.Arch))
            {
                pendingPaths.Add(Path.Combine(stateRoot, StateRootDirectoryName, state.HeadId, platform, architecture, PendingInstallLinkCallbackFileName));
                if (!string.IsNullOrWhiteSpace(platform) && !string.IsNullOrWhiteSpace(architecture))
                {
                    pendingPaths.Add(Path.Combine(stateRoot, StateRootDirectoryName, state.HeadId, $"{platform}-{architecture}", PendingInstallLinkCallbackFileName));
                }
            }
        }

        pendingPaths.Add(Path.Combine(GetStateDirectory(stateRoot, state), PendingInstallLinkCallbackFileName));
        return pendingPaths.Where(static path => !string.IsNullOrWhiteSpace(path)).ToList();
    }

    private static string GetStateDirectory(DesktopInstallLinkingState state)
    {
        DesktopInstallLinkingPaths paths = DesktopInstallLinkingPaths.Create(
            state.HeadId,
            new DesktopRuntimePlatformIdentity(state.Platform, state.Arch));
        string stateDirectory = Path.GetDirectoryName(paths.StateFilePath)
            ?? throw new InvalidOperationException("Desktop install-linking state path is invalid.");
        return stateDirectory;
    }

    private static string GetStateDirectory(string stateRoot, DesktopInstallLinkingState state)
    {
        return Path.Combine(stateRoot, StateRootDirectoryName, state.HeadId, state.Platform, state.Arch);
    }

    private static IEnumerable<string> NormalizeArchitectureAliases(string? architecture)
    {
        string normalized = string.IsNullOrWhiteSpace(architecture)
            ? string.Empty
            : architecture.Trim().ToLowerInvariant();

        yield return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;

        if (string.Equals(normalized, "x64", StringComparison.OrdinalIgnoreCase))
        {
            yield return "amd64";
        }

        if (string.Equals(normalized, "amd64", StringComparison.OrdinalIgnoreCase))
        {
            yield return "x64";
        }

        if (string.Equals(normalized, "x86", StringComparison.OrdinalIgnoreCase))
        {
            yield return "i386";
        }
    }

    private static IEnumerable<string> NormalizePlatformAliases(string? platform)
    {
        string normalized = string.IsNullOrWhiteSpace(platform)
            ? string.Empty
            : platform.Trim().ToLowerInvariant();

        HashSet<string> aliases =
        [
            normalized
        ];

        if (string.Equals(normalized, "win", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "windows", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("windows");
            aliases.Add("win");
        }

        foreach (string alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                yield return alias;
            }
        }
    }

    private static string GetPendingClaimCodePath(DesktopInstallLinkingState state)
    {
        return Path.Combine(GetStateDirectory(state), PendingClaimCodeFileName);
    }

    private static void TryDeletePendingClaimCode(DesktopInstallLinkingState state)
    {
        foreach (string pendingPath in GetPendingClaimCodePaths(state))
        {
            try
            {
                if (File.Exists(pendingPath))
                {
                    File.Delete(pendingPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void TryDeletePendingInstallLinkCallback(DesktopInstallLinkingState state)
    {
        foreach (string pendingPath in GetPendingInstallLinkCallbackPaths(state))
        {
            try
            {
                if (File.Exists(pendingPath))
                {
                    File.Delete(pendingPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static HttpClient CreateApiHttpClient(TimeSpan timeout)
    {
        HttpClient client = new()
        {
            BaseAddress = ResolveApiBaseAddress(),
            Timeout = timeout
        };

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Remove("X-Api-Key");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    private static Uri ResolveApiBaseAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return new Uri("http://chummer-api:8080", UriKind.Absolute);
    }

    private static Uri ResolvePublicWebAddress()
    {
        string? configured = Environment.GetEnvironmentVariable(WebBaseUrlEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured) && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return ResolveApiBaseAddress();
    }

    private static bool TryOpenPublicPortal(string relativePath)
    {
        Uri uri = ResolvePublicWebAddress();
        return DesktopCrashRuntime.TryOpenPathInShell(new Uri(uri, relativePath).ToString());
    }

    private static string BuildSupportPortalRelativePath(SupportPortalPrefill prefill)
    {
        List<string> query = [];
        AppendQueryParameter(query, "kind", prefill.Kind);
        AppendQueryParameter(query, "title", prefill.Title);
        AppendQueryParameter(query, "summary", prefill.Summary);
        AppendQueryParameter(query, "detail", prefill.Detail);
        AppendQueryParameter(query, "installationId", prefill.InstallationId);
        AppendQueryParameter(query, "applicationVersion", prefill.ApplicationVersion);
        AppendQueryParameter(query, "releaseChannel", prefill.ReleaseChannel);
        AppendQueryParameter(query, "headId", prefill.HeadId);
        AppendQueryParameter(query, "platform", prefill.Platform);
        AppendQueryParameter(query, "arch", prefill.Arch);
        return query.Count == 0
            ? "/contact"
            : $"/contact?{string.Join("&", query)}";
    }

    private static void AppendQueryParameter(List<string> query, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
    }

    private static string BuildInstallLinkCallbackUri()
    {
        return "chummer://install-link";
    }

    private static string BuildErrorMessage(HttpResponseMessage response, string responseText)
    {
        try
        {
            ProblemEnvelope? problem = JsonSerializer.Deserialize<ProblemEnvelope>(responseText, JsonOptions);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail.Trim();
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem.Title.Trim();
            }
        }
        catch
        {
            // Fall back to the HTTP status line below.
        }

        return $"Hub install linking returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    private static IReadOnlyList<string> BuildSupportDraftContextLines(
        DesktopInstallLinkingState state,
        DesktopUpdateClientStatus? updateStatus,
        params string?[] primaryLines)
    {
        List<string> lines = [];
        foreach (string? line in primaryLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line.Trim());
            }
        }

        lines.Add($"Install ID: {state.InstallationId}");
        lines.Add($"Head: {state.HeadId}");
        lines.Add($"Version: {state.ApplicationVersion}");
        lines.Add($"Channel: {state.ChannelId}");
        lines.Add($"Platform: {state.Platform}/{state.Arch}");

        if (!string.IsNullOrWhiteSpace(state.LastClaimMessage))
        {
            lines.Add($"Hub message: {state.LastClaimMessage}");
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimError))
        {
            lines.Add($"Claim error: {state.LastClaimError}");
        }

        if (updateStatus is not null)
        {
            lines.Add($"Release status: {updateStatus.Status}");
            lines.Add($"Recommended action: {updateStatus.RecommendedAction}");

            if (!string.IsNullOrWhiteSpace(updateStatus.LastManifestVersion))
            {
                lines.Add($"Manifest: {updateStatus.LastManifestVersion}");
            }

            if (!string.IsNullOrWhiteSpace(updateStatus.SupportabilityState))
            {
                lines.Add($"Supportability: {updateStatus.SupportabilityState}");
            }

            if (!string.IsNullOrWhiteSpace(updateStatus.KnownIssueSummary))
            {
                lines.Add($"Known issues: {updateStatus.KnownIssueSummary}");
            }

            if (!string.IsNullOrWhiteSpace(updateStatus.LastError))
            {
                lines.Add($"Last error: {updateStatus.LastError}");
            }
        }

        return lines;
    }

    private static string NormalizeSupportDraftField(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "Not provided."
            : value.Trim();

    private static bool TryExtractBrowserCallbackCodeFromCallbackUri(string? rawValue, out string? callbackCode)
    {
        callbackCode = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        string candidate = rawValue.Trim().Trim('"');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || !IsInstallLinkCallbackUri(uri))
        {
            return false;
        }

        callbackCode = TryReadBrowserCallbackCodeFromQueryString(uri.Query);
        if (callbackCode is not null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(uri.Fragment))
        {
            string fragment = uri.Fragment[0] == '#' ? uri.Fragment[1..] : uri.Fragment;
            if (fragment.Length > 0 && fragment[0] == '?')
            {
                fragment = fragment[1..];
            }

            callbackCode = TryReadBrowserCallbackCodeFromQueryString(fragment);
        }

        return callbackCode is not null;
    }

    private static bool TryExtractClaimCodeFromCallbackUri(string? rawValue, out string? claimCode)
    {
        claimCode = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        string candidate = rawValue.Trim().Trim('"');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || !IsInstallLinkCallbackUri(uri))
        {
            return false;
        }

        claimCode = TryReadClaimCodeFromQueryString(uri.Query);
        if (claimCode is not null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(uri.Fragment))
        {
            string fragment = uri.Fragment[0] == '#' ? uri.Fragment[1..] : uri.Fragment;
            if (fragment.Length > 0 && fragment[0] == '?')
            {
                fragment = fragment[1..];
            }

            claimCode = TryReadClaimCodeFromQueryString(fragment);
        }

        return claimCode is not null;
    }

    private static bool IsInstallLinkCallbackUri(Uri uri)
    {
        if (string.Equals(uri.Scheme, "chummer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string absolutePath = uri.AbsolutePath.Trim('/');
        return absolutePath.Contains("install-link", StringComparison.OrdinalIgnoreCase)
               || absolutePath.Contains("downloads/install", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadBrowserCallbackCodeFromQueryString(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return null;
        }

        ReadOnlySpan<char> querySpan = rawQuery.AsSpan().Trim();
        if (querySpan.Length > 0 && querySpan[0] == '?')
        {
            querySpan = querySpan[1..];
        }

        foreach (string segment in querySpan.ToString().Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = segment.IndexOf('=');
            string key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            string? value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : null;
            if (!string.Equals(key, "code", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "callbackCode", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "installLinkCode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string decodedValue = Uri.UnescapeDataString((value ?? string.Empty).Replace('+', ' '));
            string? normalized = NormalizeBrowserCallbackCode(decodedValue);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? TryReadClaimCodeFromQueryString(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return null;
        }

        ReadOnlySpan<char> querySpan = rawQuery.AsSpan().Trim();
        if (querySpan.Length > 0 && querySpan[0] == '?')
        {
            querySpan = querySpan[1..];
        }

        foreach (string segment in querySpan.ToString().Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separatorIndex = segment.IndexOf('=');
            string key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            string? value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : null;
            if (!string.Equals(key, "claimCode", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "claim", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "installClaimCode", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "claim_code", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string decodedValue = Uri.UnescapeDataString((value ?? string.Empty).Replace('+', ' '));
            string? normalized = NormalizeClaimCode(decodedValue);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizeClaimCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Concat(value.Trim().Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    private static string? NormalizeBrowserCallbackCode(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static (string PublicKey, string PrivateKey) CreateInstallationKeyPair()
    {
        using RSA rsa = RSA.Create(2048);
        return (rsa.ExportRSAPublicKeyPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private sealed record SupportPortalPrefill(
        string? Kind,
        string? Title,
        string? Summary,
        string? Detail,
        string? InstallationId,
        string? ApplicationVersion,
        string? ReleaseChannel,
        string? HeadId,
        string? Platform,
        string? Arch);

    private sealed record ProblemEnvelope(
        string? Title,
        string? Detail,
        int? Status);

    private sealed record DesktopRuntimeReleaseMetadata(
        string HeadId,
        string Version,
        string ChannelId)
    {
        public static DesktopRuntimeReleaseMetadata Load(string fallbackHeadId)
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return new DesktopRuntimeReleaseMetadata(
                HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? fallbackHeadId,
                Version: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
                    ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? assembly.GetName().Version?.ToString()
                    ?? string.Empty,
                ChannelId: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseChannel") ?? "local");
        }

        private static string? ReadAssemblyMetadata(Assembly assembly, string key)
        {
            return assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
                .Value;
        }
    }

    private sealed record DesktopRuntimePlatformIdentity(
        string Platform,
        string Arch)
    {
        public static DesktopRuntimePlatformIdentity Current()
            => new(
                Platform: ResolvePlatform(),
                Arch: NormalizeArchitecture(RuntimeInformation.OSArchitecture));

        private static string ResolvePlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return "windows";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "macos";
            }

            if (OperatingSystem.IsLinux())
            {
                return "linux";
            }

            return "unknown";
        }

        private static string NormalizeArchitecture(Architecture architecture)
            => architecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => architecture.ToString().ToLowerInvariant()
            };
    }

    private sealed record DesktopInstallLinkingPaths(
        string StateFilePath,
        string ProtectedPrivateKeyFilePath)
    {
        public static DesktopInstallLinkingPaths Create(string headId, DesktopRuntimePlatformIdentity identity)
        {
            string root = Path.Combine(
                GetStateRoot(),
                StateRootDirectoryName,
                headId,
                identity.Platform,
                identity.Arch);
            return new DesktopInstallLinkingPaths(
                StateFilePath: Path.Combine(root, "state.json"),
                ProtectedPrivateKeyFilePath: Path.Combine(root, ProtectedPrivateKeyFileName));
        }
    }

    private static string GetStateRoot()
    {
        return DesktopStateRootResolver.Resolve("Chummer6", "Chummer6");
    }

    private static class DesktopInstallLinkingStateStore
    {
        public static DesktopInstallLinkingState? Load(DesktopInstallLinkingPaths paths)
        {
            if (!File.Exists(paths.StateFilePath))
            {
                return null;
            }

            DesktopInstallLinkingState? state = JsonSerializer.Deserialize<DesktopInstallLinkingState>(
                File.ReadAllText(paths.StateFilePath, Encoding.UTF8),
                JsonOptions);
            if (state is null)
            {
                return null;
            }

            if (!ShouldProtectPrivateKeyAtRest() || !string.IsNullOrWhiteSpace(state.PrivateKey))
            {
                return state;
            }

            string? privateKey = LoadProtectedPrivateKey(paths, state.InstallationId);
            return string.IsNullOrWhiteSpace(privateKey)
                ? state
                : state with { PrivateKey = privateKey };
        }

        public static void Save(DesktopInstallLinkingPaths paths, DesktopInstallLinkingState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.StateFilePath)!);
            if (!ShouldProtectPrivateKeyAtRest())
            {
                TryDeleteProtectedPrivateKey(paths);
                File.WriteAllText(paths.StateFilePath, JsonSerializer.Serialize(state, JsonOptions), Encoding.UTF8);
                return;
            }

            SaveProtectedPrivateKey(paths, state);
            DesktopInstallLinkingState persisted = state with { PrivateKey = string.Empty };
            File.WriteAllText(paths.StateFilePath, JsonSerializer.Serialize(persisted, JsonOptions), Encoding.UTF8);
        }

        public static bool ShouldMigratePlaintextPrivateKey(DesktopInstallLinkingPaths paths)
        {
            if (!File.Exists(paths.StateFilePath))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(paths.StateFilePath, Encoding.UTF8));
                if (!document.RootElement.TryGetProperty("privateKey", out JsonElement privateKeyElement))
                {
                    return !File.Exists(paths.ProtectedPrivateKeyFilePath);
                }

                string? persistedPrivateKey = privateKeyElement.ValueKind == JsonValueKind.String
                    ? privateKeyElement.GetString()
                    : null;
                return !string.IsNullOrWhiteSpace(persistedPrivateKey)
                    || !File.Exists(paths.ProtectedPrivateKeyFilePath);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        [SupportedOSPlatform("windows")]
        private static string? LoadProtectedPrivateKey(DesktopInstallLinkingPaths paths, string installationId)
        {
            if (!File.Exists(paths.ProtectedPrivateKeyFilePath))
            {
                return null;
            }

            try
            {
                byte[] protectedBytes = File.ReadAllBytes(paths.ProtectedPrivateKeyFilePath);
                byte[] privateKeyBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    BuildProtectionEntropy(installationId),
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(privateKeyBytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        [SupportedOSPlatform("windows")]
        private static void SaveProtectedPrivateKey(DesktopInstallLinkingPaths paths, DesktopInstallLinkingState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.ProtectedPrivateKeyFilePath)!);
            byte[] privateKeyBytes = Encoding.UTF8.GetBytes(state.PrivateKey);
            byte[] protectedBytes = ProtectedData.Protect(
                privateKeyBytes,
                BuildProtectionEntropy(state.InstallationId),
                DataProtectionScope.CurrentUser);
            File.WriteAllBytes(paths.ProtectedPrivateKeyFilePath, protectedBytes);
        }

        private static void TryDeleteProtectedPrivateKey(DesktopInstallLinkingPaths paths)
        {
            try
            {
                if (File.Exists(paths.ProtectedPrivateKeyFilePath))
                {
                    File.Delete(paths.ProtectedPrivateKeyFilePath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static byte[] BuildProtectionEntropy(string installationId)
            => Encoding.UTF8.GetBytes($"chummer6.install-linking.private-key:{installationId}");
    }

    [SupportedOSPlatformGuard("windows")]
    private static bool ShouldProtectPrivateKeyAtRest()
        => OperatingSystem.IsWindows();
}
