using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Chummer.Desktop.Runtime;

public sealed record DesktopUpdateStartupResult(
    bool ExitRequested,
    string Reason)
{
    public static DesktopUpdateStartupResult Continue(string reason = "disabled")
        => new(false, reason);

    public static DesktopUpdateStartupResult ExitForApply(string reason = "apply_scheduled")
        => new(true, reason);
}

public sealed record DesktopUpdateClientStatus(
    string HeadId,
    string InstalledVersion,
    string ChannelId,
    string Platform,
    string Arch,
    bool UpdatesEnabled,
    bool AutoApply,
    string ManifestLocation,
    DateTimeOffset? LastCheckedAtUtc,
    string? LastManifestVersion,
    DateTimeOffset? LastManifestPublishedAtUtc,
    string? LastError,
    string Status,
    string RecommendedAction,
    string? RolloutState = null,
    string? RolloutReason = null,
    string? SupportabilityState = null,
    string? SupportabilitySummary = null,
    string? KnownIssueSummary = null,
    string? FixAvailabilitySummary = null,
    string? ProofStatus = null,
    DateTimeOffset? ProofGeneratedAtUtc = null);

public static class DesktopUpdateRuntime
{
    private const string ApplySwitch = "--desktop-update-apply";
    private const string LaunchInstallerSwitch = "--desktop-update-launch-installer";
    private const string UpdateManifestEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MANIFEST";
    private const string UpdateEnabledEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_ENABLED";
    private const string UpdateAutoApplyEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY";
    private const string LegacyManifestEnvironmentVariable = "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL";
    private const string UpdateRootDirectoryName = "desktop-update";
    private const string UpdateProcessPathOverrideEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_PROCESS_PATH_OVERRIDE";
    private const int ManifestLoadRetryCount = 3;
    private const int ArtifactDownloadRetryCount = 3;
    private const int StartupManifestBackoffMinutes = 2;
    private const int StartupDownloadBackoffMinutes = 5;
    private const int StartupApplyBackoffMinutes = 10;

    public static DesktopUpdateClientStatus GetCurrentStatus(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopUpdateConfiguration configuration = DesktopUpdateConfiguration.Load();
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        DesktopReleaseMetadata releaseMetadata = DesktopReleaseMetadata.Load(headId);
        DesktopUpdateState? state = DesktopUpdateStateStore.Load(paths.StateFilePath);

        string installedVersion = string.IsNullOrWhiteSpace(state?.InstalledVersion)
            ? releaseMetadata.Version
            : state!.InstalledVersion;
        string channelId = string.IsNullOrWhiteSpace(state?.ChannelId)
            ? releaseMetadata.ChannelId
            : state!.ChannelId;

        string status;
        string recommendedAction;
        if (!configuration.Enabled)
        {
            status = "disabled";
            recommendedAction = "Configure the desktop update manifest before promising self-update.";
        }
        else if (!string.IsNullOrWhiteSpace(state?.LastError))
        {
            status = "attention_required";
            recommendedAction = "Review the last update error and route support before promotion.";
        }
        else if (state?.LastCheckedAt is null)
        {
            status = "never_checked";
            recommendedAction = "Open the desktop once with update checks enabled so the local install seeds update truth.";
        }
        else if (!string.IsNullOrWhiteSpace(state.LastManifestVersion)
            && !string.Equals(installedVersion, state.LastManifestVersion, StringComparison.OrdinalIgnoreCase))
        {
            status = "update_available";
            recommendedAction = configuration.AutoApply
                ? "Restart to let the desktop head apply the next staged update."
                : "Open Downloads or Account to review the next promoted installer.";
        }
        else if (RequiresReleaseAttention(state))
        {
            status = "attention_required";
            recommendedAction = BuildReleaseAttentionAction(state);
        }
        else
        {
            status = "current";
            recommendedAction = "Continue into the home cockpit or your most recent workspace.";
        }

        return new DesktopUpdateClientStatus(
            HeadId: headId,
            InstalledVersion: installedVersion,
            ChannelId: channelId,
            Platform: identity.Platform,
            Arch: identity.Arch,
            UpdatesEnabled: configuration.Enabled,
            AutoApply: configuration.AutoApply,
            ManifestLocation: configuration.ManifestLocation,
            LastCheckedAtUtc: state?.LastCheckedAt,
            LastManifestVersion: state?.LastManifestVersion,
            LastManifestPublishedAtUtc: state?.LastManifestPublishedAt,
            LastError: state?.LastError,
            Status: status,
            RecommendedAction: recommendedAction,
            RolloutState: state?.LastRolloutState,
            RolloutReason: state?.LastRolloutReason,
            SupportabilityState: state?.LastSupportabilityState,
            SupportabilitySummary: state?.LastSupportabilitySummary,
            KnownIssueSummary: state?.LastKnownIssueSummary,
            FixAvailabilitySummary: state?.LastFixAvailabilitySummary,
            ProofStatus: state?.LastProofStatus,
            ProofGeneratedAtUtc: state?.LastProofGeneratedAt);
    }

    private static bool RequiresReleaseAttention(DesktopUpdateState? state)
    {
        if (state is null)
        {
            return false;
        }

        if (string.Equals(state.LastProofStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(state.LastSupportabilityState, "review_required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(state.LastRolloutState, "paused", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.LastRolloutState, "revoked", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReleaseAttentionAction(DesktopUpdateState? state)
    {
        if (state is null)
        {
            return "Open Downloads and Support before relying on this release.";
        }

        if (string.Equals(state.LastProofStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Open Downloads and Support before relying on this release because the latest local release proof failed.";
        }

        if (string.Equals(state.LastSupportabilityState, "review_required", StringComparison.OrdinalIgnoreCase))
        {
            return "Review supportability on Downloads or Support before continuing campaign work on this release.";
        }

        if (string.Equals(state.LastRolloutState, "paused", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.LastRolloutState, "revoked", StringComparison.OrdinalIgnoreCase))
        {
            return "Do not rely on this release until Downloads confirms the current rollout posture.";
        }

        return "Open Downloads and Support before relying on this release.";
    }

    public static async Task<int?> TryHandleSpecialModeAsync(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 2)
        {
            return null;
        }

        if (string.Equals(args[0], ApplySwitch, StringComparison.Ordinal))
        {
            try
            {
                DesktopUpdateApplyRequest request = LoadApplyRequest(args[1]);
                return await ApplyStagedUpdateAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to apply staged update request '{args[1]}': {ex.Message}");
                return 1;
            }
        }

        if (string.Equals(args[0], LaunchInstallerSwitch, StringComparison.Ordinal))
        {
            try
            {
                DesktopUpdateInstallerLaunchRequest request = LoadInstallerLaunchRequest(args[1]);
                return await LaunchInstallerAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to launch staged installer request '{args[1]}': {ex.Message}");
                return 1;
            }
        }

        return null;
    }

    public static async Task<DesktopUpdateStartupResult> CheckAndScheduleStartupUpdateAsync(
        string headId,
        string[] relaunchArgs,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(relaunchArgs);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DesktopUpdateConfiguration configuration = DesktopUpdateConfiguration.Load();
        if (!configuration.Enabled)
        {
            return DesktopUpdateStartupResult.Continue("manifest_not_configured");
        }

        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        CleanupExpiredTempArtifacts(paths.TempRoot);

        DesktopReleaseMetadata releaseMetadata = DesktopReleaseMetadata.Load(headId);
        DesktopUpdateState state = DesktopUpdateStateStore.Load(paths.StateFilePath)
            ?? new DesktopUpdateState(
                HeadId: headId,
                Platform: identity.Platform,
                Arch: identity.Arch,
                InstalledVersion: releaseMetadata.Version,
                ChannelId: releaseMetadata.ChannelId,
                LastCheckedAt: null,
                LastManifestVersion: null,
                LastManifestPublishedAt: null,
                LastError: null);

        if (!string.IsNullOrWhiteSpace(releaseMetadata.Version)
            && !string.Equals(state.InstalledVersion, releaseMetadata.Version, StringComparison.OrdinalIgnoreCase))
        {
            state = state with
            {
                InstalledVersion = releaseMetadata.Version,
                ChannelId = releaseMetadata.ChannelId
            };
            DesktopUpdateStateStore.Save(paths.StateFilePath, state);
        }
        else if (string.IsNullOrWhiteSpace(state.InstalledVersion))
        {
            DesktopUpdateStateStore.Save(paths.StateFilePath, state);
        }

        try
        {
            if (state.NextRetryAtUtc is not null && state.NextRetryAtUtc > now && !string.IsNullOrWhiteSpace(state.LastError))
            {
                return DesktopUpdateStartupResult.Continue("retry_backoff");
            }

            if (state.NextRetryAtUtc is not null && state.NextRetryAtUtc <= now)
            {
                state = state with
                {
                    LastError = null,
                    NextRetryAtUtc = null,
                    LastFailureReason = null,
                    RetryAttempt = 0,
                    LastFailureAtUtc = null
                };
            }

            Uri manifestUri = ResolveManifestUri(configuration.ManifestLocation);
            DesktopUpdateChannelManifest? manifest = await TryLoadManifestAsync(manifestUri, ct).ConfigureAwait(false);
            if (manifest is null)
            {
                DesktopUpdateStateStore.Save(paths.StateFilePath, state with
                {
                    LastCheckedAt = now,
                    LastError = $"Could not load manifest '{manifestUri}'.",
                    LastFailureReason = "manifest_load_failed",
                    LastFailureAtUtc = now,
                    RetryAttempt = state.RetryAttempt + 1,
                    NextRetryAtUtc = now.AddMinutes(StartupManifestBackoffMinutes)
                });
                return DesktopUpdateStartupResult.Continue("manifest_load_failed");
            }

            bool manifestVersionChanged = !string.Equals(state.LastManifestVersion, manifest.Version, StringComparison.OrdinalIgnoreCase);
            DesktopUpdateState updatedState = (manifestVersionChanged ? state with
            {
                LastError = null,
                LastFailureReason = null,
                LastFailureAtUtc = null,
                RetryAttempt = 0,
                NextRetryAtUtc = null
            } : state) with
            {
                HeadId = headId,
                Platform = identity.Platform,
                Arch = identity.Arch,
                ChannelId = string.IsNullOrWhiteSpace(state.ChannelId) ? manifest.ChannelId : state.ChannelId,
                LastCheckedAt = now,
                LastManifestVersion = manifest.Version,
                LastManifestPublishedAt = manifest.PublishedAt,
                LastRolloutState = manifest.RolloutState,
                LastRolloutReason = manifest.RolloutReason,
                LastSupportabilityState = manifest.SupportabilityState,
                LastSupportabilitySummary = manifest.SupportabilitySummary,
                LastKnownIssueSummary = manifest.KnownIssueSummary,
                LastFixAvailabilitySummary = manifest.FixAvailabilitySummary,
                LastProofStatus = manifest.ProofStatus,
                LastProofGeneratedAt = manifest.ProofGeneratedAt
            };

            string installedVersion = string.IsNullOrWhiteSpace(updatedState.InstalledVersion)
                ? releaseMetadata.Version
                : updatedState.InstalledVersion;

            if (string.IsNullOrWhiteSpace(installedVersion))
            {
                updatedState = updatedState with
                {
                    InstalledVersion = manifest.Version,
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = null,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                };
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState);
                return DesktopUpdateStartupResult.Continue("seeded_from_manifest");
            }

            if (string.Equals(installedVersion, manifest.Version, StringComparison.OrdinalIgnoreCase))
            {
                updatedState = updatedState with
                {
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = now,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                };
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState);
                return DesktopUpdateStartupResult.Continue("already_current");
            }

            if (!configuration.AutoApply)
            {
                updatedState = updatedState with
                {
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = null,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                };
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState);
                return DesktopUpdateStartupResult.Continue("auto_apply_disabled");
            }

            DesktopUpdateArtifact? artifact = DesktopUpdateManifestParser.SelectPreferredArtifact(manifest, headId, identity);
            if (artifact is null)
            {
                updatedState = updatedState with
                {
                    LastError = $"No compatible desktop update payload was available for {headId} {identity.Platform}/{identity.Arch}.",
                    LastFailureReason = "no_matching_payload",
                    LastFailureAtUtc = now,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                };
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState);
                return DesktopUpdateStartupResult.Continue("no_matching_payload");
            }

            string processPath = ResolveProcessPath();
            if (!CanRunCopiedHelper(processPath, AppContext.BaseDirectory))
            {
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                {
                    LastError = "Desktop update helper could not run copied process from app directory.",
                    LastFailureReason = "helper_unavailable",
                    LastFailureAtUtc = now,
                    RetryAttempt = updatedState.RetryAttempt + 1,
                    NextRetryAtUtc = CalculateBackoffTime(updatedState.RetryAttempt + 1)
                });
                return DesktopUpdateStartupResult.Continue("helper_unavailable");
            }

            if (!IsPublishedManifest(manifest))
            {
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                {
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = null,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                });
                return DesktopUpdateStartupResult.Continue("manifest_not_published");
            }

            string? launchExecutableName = Path.GetFileName(processPath);
            if (string.IsNullOrWhiteSpace(launchExecutableName))
            {
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                {
                    LastError = "Desktop update helper could not resolve the launch executable.",
                    LastFailureReason = "helper_launch_executable_missing",
                    LastFailureAtUtc = now,
                    RetryAttempt = updatedState.RetryAttempt + 1,
                    NextRetryAtUtc = now.AddMinutes(StartupApplyBackoffMinutes)
                });
                return DesktopUpdateStartupResult.Continue("helper_invalid_state");
            }

            string stageRoot = Path.Combine(paths.TempRoot, $"stage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stageRoot);
            string downloadedArtifactPath = Path.Combine(stageRoot, artifact.FileName);

            try
            {
                Uri downloadUri = ResolveArtifactUri(manifest.SourceUri, artifact);
                await DownloadArtifactAsync(downloadUri, downloadedArtifactPath, ct).ConfigureAwait(false);

                string helperPath = CopyProcessExecutableToHelper(processPath, paths.TempRoot);
                if (artifact.SupportsInPlaceApply)
                {
                    string payloadRoot = Path.Combine(stageRoot, "payload");
                    Directory.CreateDirectory(payloadRoot);
                    ExtractArchive(downloadedArtifactPath, payloadRoot);

                    string payloadInstallRoot = NormalizePayloadRoot(payloadRoot, launchExecutableName);
                    if (!File.Exists(Path.Combine(payloadInstallRoot, launchExecutableName)))
                    {
                        throw new InvalidOperationException(
                            $"The staged desktop payload did not contain '{launchExecutableName}'.");
                    }

                    DesktopUpdateApplyRequest request = new(
                        ParentProcessId: Environment.ProcessId,
                        StageRoot: stageRoot,
                        PayloadRoot: payloadInstallRoot,
                        InstallDirectory: AppContext.BaseDirectory,
                        LaunchExecutableName: launchExecutableName,
                        StateFilePath: paths.StateFilePath,
                        Version: manifest.Version,
                        ChannelId: manifest.ChannelId,
                        RelaunchArgs: relaunchArgs);
                    string requestPath = Path.Combine(stageRoot, "apply-request.json");
                    WriteApplyRequest(requestPath, request);
                    LaunchApplyHelper(helperPath, requestPath);
                }
                else if (artifact.SupportsInstallerHandoff)
                {
                    DesktopUpdateInstallerLaunchRequest request = new(
                        ParentProcessId: Environment.ProcessId,
                        StageRoot: stageRoot,
                        InstallerPath: downloadedArtifactPath,
                        StateFilePath: paths.StateFilePath,
                        Version: manifest.Version,
                        ChannelId: manifest.ChannelId);
                    string requestPath = Path.Combine(stageRoot, "installer-request.json");
                    WriteInstallerLaunchRequest(requestPath, request);
                    LaunchInstallerHelper(helperPath, requestPath);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Desktop update payload '{artifact.FileName}' is neither in-place applyable nor installer-launchable.");
                }

                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                {
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = now,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                });
                return DesktopUpdateStartupResult.ExitForApply();
            }
            catch (Exception ex)
            {
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                {
                    LastError = BuildUpdateFailureMessage(ex),
                    LastFailureReason = "update_apply_failed",
                    LastFailureAtUtc = now,
                    RetryAttempt = updatedState.RetryAttempt + 1,
                    NextRetryAtUtc = CalculateBackoffTime(updatedState.RetryAttempt + 1)
                });
                TryDeleteDirectory(stageRoot);
                if (File.Exists(downloadedArtifactPath))
                {
                    TryDeleteFile(downloadedArtifactPath);
                }

                return DesktopUpdateStartupResult.Continue("update_schedule_failed");
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                throw;
            }

            DesktopUpdateStateStore.Save(paths.StateFilePath, state with
            {
                LastCheckedAt = now,
                LastError = BuildUpdateFailureMessage(ex),
                LastFailureReason = "update_schedule_failed",
                LastFailureAtUtc = now,
                RetryAttempt = state.RetryAttempt + 1,
                NextRetryAtUtc = CalculateBackoffTime(state.RetryAttempt + 1)
            });
            return DesktopUpdateStartupResult.Continue("update_schedule_failed");
        }
    }

    private static DesktopUpdateApplyRequest LoadApplyRequest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ApplyRequestDto? dto = JsonSerializer.Deserialize<ApplyRequestDto>(File.ReadAllText(path, Encoding.UTF8));
        if (dto is null
            || string.IsNullOrWhiteSpace(dto.StageRoot)
            || string.IsNullOrWhiteSpace(dto.PayloadRoot)
            || string.IsNullOrWhiteSpace(dto.InstallDirectory)
            || string.IsNullOrWhiteSpace(dto.LaunchExecutableName)
            || string.IsNullOrWhiteSpace(dto.StateFilePath))
        {
            throw new InvalidOperationException($"Desktop update apply request '{path}' was invalid.");
        }

        return new DesktopUpdateApplyRequest(
            ParentProcessId: dto.ParentProcessId,
            StageRoot: dto.StageRoot,
            PayloadRoot: dto.PayloadRoot,
            InstallDirectory: dto.InstallDirectory,
            LaunchExecutableName: dto.LaunchExecutableName,
            StateFilePath: dto.StateFilePath,
            Version: dto.Version ?? string.Empty,
            ChannelId: dto.ChannelId ?? string.Empty,
            RelaunchArgs: dto.RelaunchArgs ?? []);
    }

    private static DesktopUpdateInstallerLaunchRequest LoadInstallerLaunchRequest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        InstallerLaunchRequestDto? dto = JsonSerializer.Deserialize<InstallerLaunchRequestDto>(File.ReadAllText(path, Encoding.UTF8));
        if (dto is null
            || string.IsNullOrWhiteSpace(dto.StageRoot)
            || string.IsNullOrWhiteSpace(dto.InstallerPath)
            || string.IsNullOrWhiteSpace(dto.StateFilePath))
        {
            throw new InvalidOperationException($"Desktop installer launch request '{path}' was invalid.");
        }

        return new DesktopUpdateInstallerLaunchRequest(
            ParentProcessId: dto.ParentProcessId,
            StageRoot: dto.StageRoot,
            InstallerPath: dto.InstallerPath,
            StateFilePath: dto.StateFilePath,
            Version: dto.Version ?? string.Empty,
            ChannelId: dto.ChannelId ?? string.Empty);
    }

    private static async Task<int> ApplyStagedUpdateAsync(DesktopUpdateApplyRequest request, CancellationToken ct)
    {
        try
        {
            await WaitForProcessExitAsync(request.ParentProcessId, ct).ConfigureAwait(false);
            ReplaceInstallDirectory(request.PayloadRoot, request.InstallDirectory);
            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            DesktopUpdateState nextState = (priorState ?? new DesktopUpdateState(
                HeadId: string.Empty,
                Platform: string.Empty,
                Arch: string.Empty,
                InstalledVersion: string.Empty,
                ChannelId: string.Empty,
                LastCheckedAt: null,
                LastManifestVersion: null,
                LastManifestPublishedAt: null,
                LastError: null)) with
            {
                InstalledVersion = request.Version,
                ChannelId = request.ChannelId,
                LastError = null
            };
            DesktopUpdateStateStore.Save(request.StateFilePath, nextState);
            LaunchInstalledApplication(request.InstallDirectory, request.LaunchExecutableName, request.RelaunchArgs);
            TryDeleteDirectory(request.StageRoot);
            return 0;
        }
        catch (Exception ex)
        {
            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            if (priorState is not null)
            {
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    LastError = ex.Message
                });
            }

            return 1;
        }
    }

    private static async Task<int> LaunchInstallerAsync(DesktopUpdateInstallerLaunchRequest request, CancellationToken ct)
    {
        try
        {
            await WaitForProcessExitAsync(request.ParentProcessId, ct).ConfigureAwait(false);
            LaunchInstaller(request.InstallerPath);

            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            if (priorState is not null)
            {
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    ChannelId = string.IsNullOrWhiteSpace(request.ChannelId) ? priorState.ChannelId : request.ChannelId,
                    LastError = null
                });
            }

            return 0;
        }
        catch (Exception ex)
        {
            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            if (priorState is not null)
            {
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    LastError = ex.Message
                });
            }

            return 1;
        }
    }

    private static bool IsPublishedManifest(DesktopUpdateChannelManifest manifest)
        => string.Equals(manifest.Status, "published", StringComparison.OrdinalIgnoreCase);

    private static async Task<DesktopUpdateChannelManifest?> TryLoadManifestAsync(Uri manifestUri, CancellationToken ct)
    {
        DesktopUpdateChannelManifest? manifest = await TryLoadManifestCoreWithRetryAsync(manifestUri, ct).ConfigureAwait(false);
        if (manifest is not null || !manifestUri.AbsolutePath.EndsWith("RELEASE_CHANNEL.generated.json", StringComparison.OrdinalIgnoreCase))
        {
            return manifest;
        }

        Uri fallbackUri = new(manifestUri, "releases.json");
        return await TryLoadManifestCoreWithRetryAsync(fallbackUri, ct).ConfigureAwait(false);
    }

    private static async Task<DesktopUpdateChannelManifest?> TryLoadManifestCoreWithRetryAsync(Uri manifestUri, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= ManifestLoadRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await TryLoadManifestCoreAsync(manifestUri, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt < ManifestLoadRetryCount && IsRetryableManifestFailure(ex))
                {
                    await Task.Delay(CalculateBackoffDelay(attempt), ct).ConfigureAwait(false);
                    continue;
                }

                return null;
            }
        }

        return null;
    }

    private static async Task<DesktopUpdateChannelManifest?> TryLoadManifestCoreAsync(Uri manifestUri, CancellationToken ct)
    {
        if (manifestUri.IsFile)
        {
            string localPath = manifestUri.LocalPath;
            if (!File.Exists(localPath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(localPath, ct).ConfigureAwait(false);
            return DesktopUpdateManifestParser.Parse(json, manifestUri);
        }

        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        string remoteJson = await client.GetStringAsync(manifestUri, ct).ConfigureAwait(false);
        return DesktopUpdateManifestParser.Parse(remoteJson, manifestUri);
    }

    private static bool IsRetryableManifestFailure(Exception ex)
        => ex is IOException
            || ex is TimeoutException
            || ex is TaskCanceledException
            || ex is HttpRequestException;

    private static TimeSpan CalculateBackoffDelay(int attempt)
    {
        if (attempt <= 1)
        {
            return TimeSpan.FromMilliseconds(200);
        }

        if (attempt == 2)
        {
            return TimeSpan.FromMilliseconds(800);
        }

        return TimeSpan.FromSeconds(2);
    }

    private static Uri ResolveManifestUri(string manifestLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestLocation);

        if (Uri.TryCreate(manifestLocation, UriKind.Absolute, out Uri? absoluteUri)
            && (absoluteUri.IsFile
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            if (absoluteUri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return absoluteUri;
            }

            string path = absoluteUri.AbsoluteUri.TrimEnd('/');
            return new Uri($"{path}/RELEASE_CHANNEL.generated.json");
        }

        string expandedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(manifestLocation));
        if (Directory.Exists(expandedPath) || !Path.HasExtension(expandedPath))
        {
            return new Uri(Path.Combine(expandedPath, "RELEASE_CHANNEL.generated.json"));
        }

        return new Uri(expandedPath);
    }

    private static Uri ResolveArtifactUri(Uri manifestUri, DesktopUpdateArtifact artifact)
    {
        string rawUrl = !string.IsNullOrWhiteSpace(artifact.DownloadUrl)
            ? artifact.DownloadUrl
            : artifact.UpdateFeedUrl ?? string.Empty;
        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? absoluteUri)
            && (!manifestUri.IsFile
                || absoluteUri.IsFile
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absoluteUri;
        }

        if (manifestUri.IsFile)
        {
            return ResolveLocalArtifactUri(manifestUri, rawUrl);
        }

        return new Uri(manifestUri, rawUrl);
    }

    private static Uri ResolveLocalArtifactUri(Uri manifestUri, string rawUrl)
    {
        string baseDirectory = Path.GetDirectoryName(manifestUri.LocalPath)
            ?? throw new InvalidOperationException($"Manifest URI '{manifestUri}' did not have a parent directory.");
        string trimmed = rawUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"Desktop update artifact URL was missing for manifest '{manifestUri}'.");
        }

        if (Path.IsPathRooted(trimmed)
            && !trimmed.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("\\downloads\\", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed);
        }

        string relative = trimmed.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith("downloads/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative["downloads/".Length..];
        }

        return new Uri(Path.Combine(baseDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static async Task DownloadArtifactAsync(Uri downloadUri, string destinationPath, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= ArtifactDownloadRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (downloadUri.IsFile)
                {
                    File.Copy(downloadUri.LocalPath, destinationPath, overwrite: true);
                    return;
                }

                using HttpClient client = new()
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                using HttpResponseMessage response = await client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using FileStream target = File.Create(destinationPath);
                await source.CopyToAsync(target, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < ArtifactDownloadRetryCount && IsRetryableDownloadFailure(ex))
            {
                await Task.Delay(CalculateBackoffDelay(attempt), ct).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            $"Failed to download desktop update artifact from '{downloadUri}' after {ArtifactDownloadRetryCount} attempts.");
    }

    private static bool IsRetryableDownloadFailure(Exception ex)
        => ex is IOException
            || ex is TimeoutException
            || ex is TaskCanceledException
            || ex is HttpRequestException;

    private static string BuildUpdateFailureMessage(Exception ex)
        => $"Update preparation failed: {ex.GetType().Name}: {ex.Message}";

    private static DateTimeOffset CalculateBackoffTime(int attempt)
        => DateTimeOffset.UtcNow.Add(CalculateFailureBackoff(attempt));

    private static TimeSpan CalculateFailureBackoff(int attempt)
        => attempt switch
        {
            <= 1 => TimeSpan.FromMinutes(StartupDownloadBackoffMinutes),
            <= 4 => TimeSpan.FromMinutes(StartupApplyBackoffMinutes),
            _ => TimeSpan.FromHours(1)
        };

    private static void ExtractArchive(string archivePath, string destinationDirectory)
    {
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true);
            return;
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            using FileStream stream = File.OpenRead(archivePath);
            using var gzip = new System.IO.Compression.GZipStream(stream, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
            return;
        }

        throw new InvalidOperationException($"Unsupported desktop update archive '{archivePath}'.");
    }

    private static string NormalizePayloadRoot(string payloadRoot, string launchExecutableName)
    {
        if (File.Exists(Path.Combine(payloadRoot, launchExecutableName)))
        {
            return payloadRoot;
        }

        string[] directories = Directory.GetDirectories(payloadRoot);
        if (directories.Length == 1 && File.Exists(Path.Combine(directories[0], launchExecutableName)))
        {
            return directories[0];
        }

        return payloadRoot;
    }

    private static string CopyProcessExecutableToHelper(string processPath, string tempRoot)
    {
        Directory.CreateDirectory(tempRoot);
        string helperPath = Path.Combine(
            tempRoot,
            $"{Path.GetFileNameWithoutExtension(processPath)}-update-helper-{Guid.NewGuid():N}{Path.GetExtension(processPath)}");
        File.Copy(processPath, helperPath, overwrite: true);
        CopyUnixModeIfNeeded(processPath, helperPath);
        return helperPath;
    }

    private static void LaunchApplyHelper(string helperPath, string requestPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = helperPath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(helperPath) ?? Path.GetTempPath()
        };
        startInfo.ArgumentList.Add(ApplySwitch);
        startInfo.ArgumentList.Add(requestPath);

        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to launch the desktop update helper.");
        }
    }

    private static void LaunchInstallerHelper(string helperPath, string requestPath)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = helperPath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(helperPath) ?? Path.GetTempPath()
        };
        startInfo.ArgumentList.Add(LaunchInstallerSwitch);
        startInfo.ArgumentList.Add(requestPath);

        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to launch the desktop installer helper.");
        }
    }

    private static void WriteApplyRequest(string path, DesktopUpdateApplyRequest request)
    {
        ApplyRequestDto dto = new(
            request.ParentProcessId,
            request.StageRoot,
            request.PayloadRoot,
            request.InstallDirectory,
            request.LaunchExecutableName,
            request.StateFilePath,
            request.Version,
            request.ChannelId,
            request.RelaunchArgs.ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            WriteIndented = true
        }), Encoding.UTF8);
    }

    private static void WriteInstallerLaunchRequest(string path, DesktopUpdateInstallerLaunchRequest request)
    {
        InstallerLaunchRequestDto dto = new(
            request.ParentProcessId,
            request.StageRoot,
            request.InstallerPath,
            request.StateFilePath,
            request.Version,
            request.ChannelId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            WriteIndented = true
        }), Encoding.UTF8);
    }

    private static async Task WaitForProcessExitAsync(int pid, CancellationToken ct)
    {
        if (pid <= 0)
        {
            return;
        }

        for (int attempt = 0; attempt < 240; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using Process process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for process {pid} to exit before applying the desktop update.");
    }

    private static void ReplaceInstallDirectory(string sourceDirectory, string installDirectory)
    {
        Directory.CreateDirectory(installDirectory);

        foreach (string file in Directory.GetFiles(installDirectory))
        {
            File.Delete(file);
        }

        foreach (string directory in Directory.GetDirectories(installDirectory))
        {
            Directory.Delete(directory, recursive: true);
        }

        CopyDirectory(sourceDirectory, installDirectory);
    }

    private static void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer payload was not found.", installerPath);
        }

        if (OperatingSystem.IsWindows())
        {
            StartDetachedProcess(installerPath);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            if (TryStartCommand("xdg-open", installerPath)
                || TryStartCommand("gio", "open", installerPath)
                || TryStartCommand("pkexec", "dpkg", "-i", installerPath))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Could not launch Linux installer '{installerPath}'. Expected xdg-open, gio, or pkexec+dpkg to be available.");
        }

        if (OperatingSystem.IsMacOS())
        {
            if (TryStartCommand("open", installerPath))
            {
                return;
            }

            throw new InvalidOperationException($"Could not launch macOS installer '{installerPath}' via 'open'.");
        }

        throw new InvalidOperationException($"Desktop installer launch is not supported on this platform for '{installerPath}'.");
    }

    private static void StartDetachedProcess(string path)
    {
        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
            UseShellExecute = true
        });
        if (process is null)
        {
            throw new InvalidOperationException($"Failed to launch process '{path}'.");
        }
    }

    private static bool TryStartCommand(string command, params string[] args)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = command,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process? process = Process.Start(startInfo);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            string destinationPath = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
            CopyUnixModeIfNeeded(file, destinationPath);
        }
    }

    private static void LaunchInstalledApplication(string installDirectory, string launchExecutableName, IReadOnlyList<string> args)
    {
        string executablePath = Path.Combine(installDirectory, launchExecutableName);
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            WorkingDirectory = installDirectory,
            UseShellExecute = false
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
    }

    private static void CopyUnixModeIfNeeded(string sourcePath, string destinationPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(sourcePath);
        File.SetUnixFileMode(destinationPath, mode);
    }

    private static bool CanRunCopiedHelper(string? processPath, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return false;
        }

        string fileName = Path.GetFileName(processPath);
        if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
            Path.GetDirectoryName(processPath)?.TrimEnd(Path.DirectorySeparatorChar),
            baseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveProcessPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(UpdateProcessPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        return Environment.ProcessPath
            ?? throw new InvalidOperationException("Desktop update process path was not available.");
    }

    private static void CleanupExpiredTempArtifacts(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
        {
            return;
        }

        foreach (string entry in Directory.GetDirectories(tempRoot))
        {
            try
            {
                DateTime created = Directory.GetCreationTimeUtc(entry);
                if (created < DateTime.UtcNow.AddDays(-2))
                {
                    Directory.Delete(entry, recursive: true);
                }
            }
            catch
            {
            }
        }

        foreach (string file in Directory.GetFiles(tempRoot))
        {
            try
            {
                DateTime created = File.GetCreationTimeUtc(file);
                if (created < DateTime.UtcNow.AddDays(-2))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private sealed record DesktopUpdateConfiguration(
        bool Enabled,
        bool AutoApply,
        string ManifestLocation)
    {
        public static DesktopUpdateConfiguration Load()
        {
            string? manifestLocation = Environment.GetEnvironmentVariable(UpdateManifestEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(manifestLocation))
            {
                manifestLocation = Environment.GetEnvironmentVariable(LegacyManifestEnvironmentVariable);
            }

            bool enabled = ParseBool(Environment.GetEnvironmentVariable(UpdateEnabledEnvironmentVariable), !string.IsNullOrWhiteSpace(manifestLocation));
            bool autoApply = ParseBool(Environment.GetEnvironmentVariable(UpdateAutoApplyEnvironmentVariable), defaultValue: true);
            return new DesktopUpdateConfiguration(
                Enabled: enabled && !string.IsNullOrWhiteSpace(manifestLocation),
                AutoApply: autoApply,
                ManifestLocation: manifestLocation ?? string.Empty);
        }

        private static bool ParseBool(string? raw, bool defaultValue)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? defaultValue
                : string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "on", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record DesktopReleaseMetadata(
        string HeadId,
        string Version,
        string ChannelId)
    {
        public static DesktopReleaseMetadata Load(string fallbackHeadId)
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return new DesktopReleaseMetadata(
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

    private sealed record DesktopUpdatePaths(
        string StateFilePath,
        string TempRoot)
    {
        public static DesktopUpdatePaths Create(string headId, DesktopUpdatePlatformIdentity identity)
        {
            string root = Path.Combine(
                DesktopStateRootResolver.Resolve("Chummer6", "Chummer6"),
                UpdateRootDirectoryName,
                headId,
                identity.Platform,
                identity.Arch);
            return new DesktopUpdatePaths(
                StateFilePath: Path.Combine(root, "state.json"),
                TempRoot: Path.Combine(root, "tmp"));
        }
    }

    private sealed record DesktopUpdateState(
        string HeadId,
        string Platform,
        string Arch,
        string InstalledVersion,
        string ChannelId,
        DateTimeOffset? LastCheckedAt,
        string? LastManifestVersion,
        DateTimeOffset? LastManifestPublishedAt,
        string? LastError,
        string? LastRolloutState = null,
        string? LastRolloutReason = null,
        string? LastSupportabilityState = null,
        string? LastSupportabilitySummary = null,
        string? LastKnownIssueSummary = null,
        string? LastFixAvailabilitySummary = null,
        string? LastProofStatus = null,
        DateTimeOffset? LastProofGeneratedAt = null,
        string? LastFailureReason = null,
        DateTimeOffset? LastFailureAtUtc = null,
        int RetryAttempt = 0,
        DateTimeOffset? NextRetryAtUtc = null);

    private static class DesktopUpdateStateStore
    {
        public static DesktopUpdateState? Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<DesktopUpdateState>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        public static void Save(string path, DesktopUpdateState state)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(state, new JsonSerializerOptions
                {
                    WriteIndented = true
                }), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private sealed record DesktopUpdateApplyRequest(
        int ParentProcessId,
        string StageRoot,
        string PayloadRoot,
        string InstallDirectory,
        string LaunchExecutableName,
        string StateFilePath,
        string Version,
        string ChannelId,
        IReadOnlyList<string> RelaunchArgs);

    private sealed record DesktopUpdateInstallerLaunchRequest(
        int ParentProcessId,
        string StageRoot,
        string InstallerPath,
        string StateFilePath,
        string Version,
        string ChannelId);

    private sealed record ApplyRequestDto(
        int ParentProcessId,
        string StageRoot,
        string PayloadRoot,
        string InstallDirectory,
        string LaunchExecutableName,
        string StateFilePath,
        string? Version,
        string? ChannelId,
        string[]? RelaunchArgs);

    private sealed record InstallerLaunchRequestDto(
        int ParentProcessId,
        string StageRoot,
        string InstallerPath,
        string StateFilePath,
        string? Version,
        string? ChannelId);
}
