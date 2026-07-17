using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

public sealed record DesktopUpdateStartupResult(
    bool ExitRequested,
    string Reason,
    string? Message = null)
{
    public static DesktopUpdateStartupResult Continue(string reason = "disabled", string? message = null)
        => new(false, reason, message);

    public static DesktopUpdateStartupResult ExitForApply(string reason = "apply_scheduled", string? message = null)
        => new(true, reason, message);
}

public sealed record DesktopUpdateProgressUpdate(
    string Stage,
    string Message,
    int? Completed = null,
    int? Total = null);

public static class DesktopUpdateRuntime
{
    private const string ApplySwitch = "--desktop-update-apply";
    private const string LaunchInstallerSwitch = "--desktop-update-launch-installer";
    private const string UpdateManifestEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MANIFEST";
    private const string UpdateModeEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MODE";
    private const string UpdateEnabledEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_ENABLED";
    private const string UpdateAutoApplyEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY";
    private const string UpdateModeFull = "full";
    private const string UpdateModeNotify = "notify";
    private const string UpdateModeOff = "off";
    private const string LegacyManifestEnvironmentVariable = "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL";
    private const string UpdateRootDirectoryName = "desktop-update";
    private const string UpdateProcessPathOverrideEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_PROCESS_PATH_OVERRIDE";
    private const string DefaultPublicManifestRelativePath = "/downloads/RELEASE_CHANNEL.generated.json";
    private const string WindowsInstallerPayloadPathEnvironmentVariable = "CHUMMER_INSTALLER_PAYLOAD_PATH";
    private const string WindowsInstallerPayloadUrlEnvironmentVariable = "CHUMMER_INSTALLER_PAYLOAD_URL";
    private const string WindowsInstallerPayloadSha256EnvironmentVariable = "CHUMMER_INSTALLER_PAYLOAD_SHA256";
    private const string WindowsInstallerPayloadSizeBytesEnvironmentVariable = "CHUMMER_INSTALLER_PAYLOAD_SIZE_BYTES";
    private const int ManifestLoadRetryCount = 3;
    private const int ArtifactDownloadRetryCount = 3;
    private const int StartupManifestBackoffMinutes = 2;
    private const int StartupDownloadBackoffMinutes = 5;
    private const int StartupApplyBackoffMinutes = 10;
    private const int InstallerCommandTimeoutMinutes = 10;
    private const int RollbackWindowDays = 1;
    private const long MaximumManifestBytes = 4L * 1024L * 1024L;
    private const long MaximumArtifactBytes = 512L * 1024L * 1024L;
    private static readonly Regex RunVersionPattern = new(
        "^run-(?<date>\\d{8})-(?<time>\\d{6})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool ShouldPromptForStartupUpdate(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        DesktopUpdateState? state = DesktopUpdateStateStore.Load(paths.StateFilePath);
        if (state is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(state.LastManifestVersion))
        {
            return false;
        }

        string installedVersion = string.IsNullOrWhiteSpace(state.InstalledVersion)
            ? DesktopReleaseMetadata.Load(headId).Version
            : state.InstalledVersion;
        if (string.IsNullOrWhiteSpace(installedVersion)
            || string.Equals(installedVersion, state.LastManifestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(
            state.LastStartupPromptedManifestVersion,
            state.LastManifestVersion,
            StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkStartupUpdatePromptShown(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        DesktopUpdateState? state = DesktopUpdateStateStore.Load(paths.StateFilePath);
        if (state is null || string.IsNullOrWhiteSpace(state.LastManifestVersion))
        {
            return;
        }

        DesktopUpdateStateStore.Save(paths.StateFilePath, state with
        {
            LastStartupPromptedManifestVersion = state.LastManifestVersion,
            LastStartupPromptedAtUtc = DateTimeOffset.UtcNow
        });
    }

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
        string? pendingInstallerPath = ResolvePendingInstallerPath(state?.PendingInstallerPath);

        string status;
        string recommendedAction;
        if (!configuration.Enabled)
        {
            status = "disabled";
            recommendedAction = configuration.Mode == UpdateModeOff
                ? "Updates are turned off for this install. Choose an update source before relying on in-app updates."
                : "Choose an update source before relying on in-app updates.";
        }
        else if (!string.IsNullOrWhiteSpace(state?.LastError))
        {
            status = "attention_required";
            recommendedAction = BuildFailureAttentionAction(state, pendingInstallerPath);
        }
        else if (!string.IsNullOrWhiteSpace(state?.PendingUpdateVersion))
        {
            status = "update_staged";
            recommendedAction = configuration.AutoApply
                ? "Update is staged. Chummer is installing it in place and should relaunch on the new build."
                : "Update is staged for this install. Finish the update before continuing.";
        }
        else if (state?.LastCheckedAt is null)
        {
            status = "never_checked";
            recommendedAction = "Open Chummer once with update checks enabled so this copy can read current release information.";
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
                ProofGeneratedAtUtc: state?.LastProofGeneratedAt,
                InstallAccessClass: state?.LastInstallAccessClass,
                DesktopChannelRef: state?.LastDesktopChannelRef,
                InstallGuidanceRef: state?.LastInstallGuidanceRef,
                ParticipationReceiptRef: state?.LastParticipationReceiptRef,
                RewardPublicationRef: state?.LastRewardPublicationRef,
                PublicInstallRoute: state?.LastDesktopPublicInstallRoute,
                DesktopSurfaceRationale: state?.LastDesktopSurfaceRationale,
                PendingUpdateVersion: state?.PendingUpdateVersion,
                PendingUpdateChannelId: state?.PendingUpdateChannelId,
                PendingInstallerPath: pendingInstallerPath,
                LastUpdateLaunchAttemptAtUtc: state?.LastUpdateLaunchAttemptAtUtc,
                RollbackWindowStartedAtUtc: state?.RollbackWindowStartedAtUtc,
                RollbackWindowExpiresAtUtc: state?.RollbackWindowExpiresAtUtc,
                LastManifestChannelId: state?.LastManifestChannelId,
                UpdateMode: configuration.Mode);
    }

    public static bool TryOpenPendingInstaller(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        DesktopUpdateState? state = DesktopUpdateStateStore.Load(paths.StateFilePath);
        string? pendingInstallerPath = ResolvePendingInstallerPath(state?.PendingInstallerPath);
        if (string.IsNullOrWhiteSpace(pendingInstallerPath))
        {
            return false;
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryStartCommand("open", pendingInstallerPath);
        }

        return false;
    }

    public static bool TryBuildPendingInstallerManualCommand(string headId, out string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdatePaths paths = DesktopUpdatePaths.Create(headId, identity);
        DesktopUpdateState? state = DesktopUpdateStateStore.Load(paths.StateFilePath);
        string? pendingInstallerPath = ResolvePendingInstallerPath(state?.PendingInstallerPath);
        if (string.IsNullOrWhiteSpace(pendingInstallerPath))
        {
            command = string.Empty;
            return false;
        }

        if (string.Equals(identity.Platform, "linux", StringComparison.OrdinalIgnoreCase)
            && pendingInstallerPath.EndsWith(".deb", StringComparison.OrdinalIgnoreCase))
        {
            command = $"sudo dpkg -i {QuoteShellArgument(pendingInstallerPath)}";
            return true;
        }

        command = string.Empty;
        return false;
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
            return "Open Downloads or Support before relying on this release because the latest release check failed.";
        }

        if (string.Equals(state.LastSupportabilityState, "review_required", StringComparison.OrdinalIgnoreCase))
        {
            return "Review supportability on Downloads or Support before continuing campaign work on this release.";
        }

        if (string.Equals(state.LastRolloutState, "paused", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state.LastRolloutState, "revoked", StringComparison.OrdinalIgnoreCase))
        {
            return "Do not rely on this release until Downloads confirms the current rollout state.";
        }

        return "Open Downloads and Support before relying on this release.";
    }

    private static string BuildFailureAttentionAction(DesktopUpdateState? state, string? pendingInstallerPath)
    {
        if (state is not null
            && string.Equals(state.LastFailureReason, "macos_manual_install_required", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(pendingInstallerPath)
                ? "A macOS installer is waiting. Open Downloads, then install it before continuing."
                : "A macOS installer is downloaded for this copy. Open it from Update Status before continuing.";
        }

        if (state is not null
            && string.Equals(state.LastFailureReason, "installer_launch_failed", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(pendingInstallerPath)
            && pendingInstallerPath.EndsWith(".deb", StringComparison.OrdinalIgnoreCase))
        {
            return "A Linux package is already downloaded for this copy. Copy the install command from Update Status, run it in a terminal, then reopen Chummer.";
        }

        return "Review the last update error or open support before continuing.";
    }

    private static string? ResolvePendingInstallerPath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        try
        {
            string normalizedPath = Path.GetFullPath(rawPath);
            return File.Exists(normalizedPath) ? normalizedPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<DesktopUpdateArtifact> SelectCompatibleArtifacts(
        DesktopUpdateChannelManifest manifest,
        string headId,
        DesktopUpdatePlatformIdentity identity)
    {
        return manifest.Artifacts
            .Where(artifact =>
                string.Equals(artifact.HeadId, headId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(artifact.Platform, identity.Platform, StringComparison.OrdinalIgnoreCase)
                && string.Equals(artifact.Arch, identity.Arch, StringComparison.OrdinalIgnoreCase)
                && (artifact.SupportsInPlaceApply || artifact.SupportsInstallerHandoff))
            .OrderBy(artifact => artifact.SupportsInPlaceApply ? 0 : 1)
            .ThenBy(artifact => ArtifactKindSortKey(artifact.Kind))
            .ThenBy(artifact => artifact.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ArtifactKindSortKey(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "archive" => 0,
            "installer" => 1,
            "dmg" => 2,
            "pkg" => 3,
            "deb" => 4,
            "msix" => 5,
            _ => 6
        };
    }

    private static bool IsRolloutBlocked(string? rolloutState)
    {
        return string.Equals(rolloutState, "paused", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rolloutState, "revoked", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRolloutBlockedMessage(string? rolloutState, string? rolloutReason)
    {
        if (!string.IsNullOrWhiteSpace(rolloutReason))
        {
            return $"Desktop update was skipped because rollout is {rolloutState}. {rolloutReason}";
        }

        if (string.IsNullOrWhiteSpace(rolloutState))
        {
            return "Desktop update was skipped because rollout is paused.";
        }

        return $"Desktop update was skipped because rollout is {rolloutState}.";
    }

    private static void ValidateArtifactIntegrity(string filePath, DesktopUpdateArtifact artifact)
    {
        string expectedSha256 = ValidateArtifactManifestMetadata(artifact);
        long observedSize = GetFileSize(filePath);
        if (observedSize != artifact.SizeBytes!.Value)
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' failed size validation. Expected {artifact.SizeBytes} bytes, observed {observedSize} bytes.");
        }

        string observedSha256 = ComputeSha256(filePath);
        if (!string.Equals(observedSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' failed checksum validation. " +
            $"Expected '{expectedSha256}', observed '{observedSha256}'.");
        }
    }

    private static string ValidateArtifactManifestMetadata(DesktopUpdateArtifact artifact)
    {
        if (artifact.SizeBytes is null or <= 0)
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' is missing required positive sizeBytes metadata.");
        }

        if (artifact.SizeBytes.Value > MaximumArtifactBytes)
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' is too large for automatic updates. Expected at most {MaximumArtifactBytes} bytes, manifest declared {artifact.SizeBytes.Value} bytes.");
        }

        if (string.IsNullOrWhiteSpace(artifact.Sha256))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' is missing a required SHA-256 checksum.");
        }

        string expectedSha256 = NormalizeSha256(artifact.Sha256);
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' has an invalid checksum format.");
        }

        return expectedSha256;
    }

    private static long GetFileSize(string path)
    {
        return new FileInfo(path).Length;
    }

    private static string NormalizeSha256(string rawSha256)
    {
        string normalized = rawSha256.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sha256:".Length..];
        }

        return Regex.IsMatch(normalized, "^[a-fA-F0-9]{64}$")
            ? normalized.ToLowerInvariant()
            : string.Empty;
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
        => await CheckAndScheduleStartupUpdateAsync(headId, relaunchArgs, progress: null, ct).ConfigureAwait(false);

    public static async Task<DesktopUpdateStartupResult> CheckAndScheduleStartupUpdateAsync(
        string headId,
        string[] relaunchArgs,
        IProgress<DesktopUpdateProgressUpdate>? progress,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(relaunchArgs);

        progress?.Report(new DesktopUpdateProgressUpdate("checking", "Checking for the newest Chummer build"));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DesktopUpdateConfiguration configuration = DesktopUpdateConfiguration.Load();
        if (!configuration.Enabled)
        {
            progress?.Report(new DesktopUpdateProgressUpdate("skipped", configuration.Mode == UpdateModeOff
                ? "Updates are turned off"
                : "Updates are not configured"));
            return DesktopUpdateStartupResult.Continue(configuration.Mode == UpdateModeOff
                ? "update_mode_off"
                : "manifest_not_configured");
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
                ChannelId = releaseMetadata.ChannelId,
                LastError = null,
                LastFailureReason = null,
                LastFailureAtUtc = null,
                PendingUpdateVersion = null,
                PendingUpdateChannelId = null,
                PendingUpdatePreparedAtUtc = null,
                PendingInstallerPath = null,
                LastUpdateLaunchAttemptAtUtc = now
            };
            DesktopUpdateStateStore.Save(paths.StateFilePath, state);
            CleanupCompletedUpdateArtifacts(paths.TempRoot);
        }
        else if (string.IsNullOrWhiteSpace(state.InstalledVersion))
        {
            DesktopUpdateStateStore.Save(paths.StateFilePath, state);
        }

        try
        {
            if (state.NextRetryAtUtc is not null && state.NextRetryAtUtc > now && !string.IsNullOrWhiteSpace(state.LastError))
            {
                progress?.Report(new DesktopUpdateProgressUpdate("waiting", "Update retry is delayed after the last failure"));
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
            progress?.Report(new DesktopUpdateProgressUpdate("checking", "Reading update information"));
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
                progress?.Report(new DesktopUpdateProgressUpdate("failed", "Could not read update information"));
                return DesktopUpdateStartupResult.Continue(
                    "manifest_load_failed",
                    "Chummer could not reach the update list. This copy will keep running.");
            }

            bool manifestVersionChanged = !string.Equals(state.LastManifestVersion, manifest.Version, StringComparison.OrdinalIgnoreCase);
            DesktopUpdateDesktopSurfaceRef? desktopSurfaceRef = DesktopUpdateManifestParser.SelectPreferredDesktopSurfaceRef(manifest, headId, identity);
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
                ChannelId = ResolveInstalledChannelId(state, releaseMetadata),
                LastManifestChannelId = string.IsNullOrWhiteSpace(manifest.ChannelId)
                    ? state.LastManifestChannelId
                    : manifest.ChannelId,
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
                LastProofGeneratedAt = manifest.ProofGeneratedAt,
                LastInstallAccessClass = desktopSurfaceRef?.InstallAccessClass,
                LastDesktopChannelRef = desktopSurfaceRef?.DesktopChannelRef,
                LastInstallGuidanceRef = desktopSurfaceRef?.InstallGuidanceRef,
                LastParticipationReceiptRef = desktopSurfaceRef?.ParticipationReceiptRef,
                LastRewardPublicationRef = desktopSurfaceRef?.RewardPublicationRef,
                LastDesktopPublicInstallRoute = desktopSurfaceRef?.PublicInstallRoute,
                LastDesktopSurfaceRationale = desktopSurfaceRef?.Rationale,
                PendingUpdateVersion = null,
                PendingUpdateChannelId = null,
                PendingUpdatePreparedAtUtc = null,
                PendingInstallerPath = null,
                LastUpdateLaunchAttemptAtUtc = state.LastUpdateLaunchAttemptAtUtc
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
                progress?.Report(new DesktopUpdateProgressUpdate("current", "Chummer is already current"));
                return DesktopUpdateStartupResult.Continue("seeded_from_manifest");
            }

            if (TryCompareReleaseVersions(installedVersion, manifest.Version, out int releaseComparison) && releaseComparison >= 0)
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
                progress?.Report(new DesktopUpdateProgressUpdate("current", "Chummer is already current"));
                return DesktopUpdateStartupResult.Continue(releaseComparison == 0 ? "already_current" : "installed_ahead_of_manifest");
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
                progress?.Report(new DesktopUpdateProgressUpdate("available", "A newer build is available"));
                return DesktopUpdateStartupResult.Continue(configuration.Mode == UpdateModeNotify
                    ? "notify_only"
                    : "auto_apply_disabled",
                    "A newer build is available. Open Devices & Access when you want to update.");
            }

            if (IsRolloutBlocked(manifest.RolloutState))
            {
                updatedState = updatedState with
                {
                    LastError = BuildRolloutBlockedMessage(manifest.RolloutState, manifest.RolloutReason),
                    LastFailureReason = "rollout_blocked",
                    LastFailureAtUtc = null,
                    RetryAttempt = 0,
                    NextRetryAtUtc = null
                };
                DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState);
                progress?.Report(new DesktopUpdateProgressUpdate("blocked", "This update is currently paused"));
                return DesktopUpdateStartupResult.Continue(
                    "rollout_blocked",
                    "The newest build is paused. This copy will keep running.");
            }

            IReadOnlyList<DesktopUpdateArtifact> artifacts = SelectCompatibleArtifacts(manifest, headId, identity);
            if (artifacts.Count == 0)
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
                progress?.Report(new DesktopUpdateProgressUpdate("blocked", "No installer is available for this platform"));
                return DesktopUpdateStartupResult.Continue(
                    "no_matching_payload",
                    "No update payload is available for this platform yet. This copy will keep running.");
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
                progress?.Report(new DesktopUpdateProgressUpdate("blocked", "The update helper is not available"));
                return DesktopUpdateStartupResult.Continue(
                    "helper_unavailable",
                    "The update helper is not available on this install. This copy will keep running.");
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
                progress?.Report(new DesktopUpdateProgressUpdate("waiting", "The latest build is not published yet"));
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
                progress?.Report(new DesktopUpdateProgressUpdate("blocked", "The update helper could not resolve the app executable"));
                return DesktopUpdateStartupResult.Continue("helper_invalid_state");
            }

            List<string> artifactFailureSummaries = [];
            for (int artifactAttempt = 0; artifactAttempt < artifacts.Count; artifactAttempt++)
            {
                DesktopUpdateArtifact artifact = artifacts[artifactAttempt];
                string stageRoot = Path.Combine(paths.TempRoot, $"stage-{Guid.NewGuid():N}");
                string downloadedArtifactPath = Path.Combine(stageRoot, artifact.FileName);

                try
                {
                    ValidateArtifactManifestMetadata(artifact);
                    Uri downloadUri = ResolveArtifactUri(manifest.SourceUri, artifact);
                    Directory.CreateDirectory(stageRoot);
                    progress?.Report(new DesktopUpdateProgressUpdate(
                        "downloading",
                        $"Downloading {artifact.FileName}",
                        0,
                        1000));
                    await DownloadArtifactAsync(downloadUri, downloadedArtifactPath, artifact, progress, ct).ConfigureAwait(false);
                    progress?.Report(new DesktopUpdateProgressUpdate(
                        "validating",
                        $"Checking {artifact.FileName}",
                        1000,
                        1000));
                    ValidateArtifactIntegrity(downloadedArtifactPath, artifact);

                    string helperPath = CopyProcessExecutableToHelper(processPath, paths.TempRoot);
                    if (artifact.SupportsInPlaceApply)
                    {
                        progress?.Report(new DesktopUpdateProgressUpdate("staging", "Preparing the in-place update"));
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
                        progress?.Report(new DesktopUpdateProgressUpdate("relaunching", "Installing the update and restarting Chummer"));
                        LaunchApplyHelper(helperPath, requestPath);
                    }
                    else if (artifact.SupportsInstallerHandoff)
                    {
                        if (OperatingSystem.IsMacOS())
                        {
                            DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                            {
                                LastError = "A macOS update is available, but automatic installer handoff is disabled for unsigned or quarantined installer images. Open Update Status to install it manually.",
                                LastFailureReason = "macos_manual_install_required",
                                LastFailureAtUtc = now,
                                RetryAttempt = 0,
                                NextRetryAtUtc = null,
                                PendingUpdateVersion = manifest.Version,
                                PendingUpdateChannelId = manifest.ChannelId,
                                PendingUpdatePreparedAtUtc = DateTimeOffset.UtcNow,
                                PendingInstallerPath = downloadedArtifactPath
                            });
                            progress?.Report(new DesktopUpdateProgressUpdate("manual", "A macOS update is ready. Manual install is required."));
                            return DesktopUpdateStartupResult.Continue(
                                "macos_manual_install_required",
                                "A macOS update is ready. Open Update Status to install it manually; this copy will stay usable.");
                        }

                        progress?.Report(new DesktopUpdateProgressUpdate("staging", "Preparing the installer handoff"));
                        await StageInstallerBootstrapPayloadIfNeededAsync(
                            manifestUri,
                            manifest,
                            artifact,
                            downloadedArtifactPath,
                            progress,
                            ct).ConfigureAwait(false);
                        DesktopUpdateInstallerLaunchRequest request = new(
                            ParentProcessId: Environment.ProcessId,
                            StageRoot: stageRoot,
                            InstallerPath: downloadedArtifactPath,
                            StateFilePath: paths.StateFilePath,
                            Version: manifest.Version,
                            ChannelId: manifest.ChannelId,
                            HeadId: headId,
                            RelaunchArgs: relaunchArgs);
                        string requestPath = Path.Combine(stageRoot, "installer-request.json");
                        WriteInstallerLaunchRequest(requestPath, request);
                        progress?.Report(new DesktopUpdateProgressUpdate("relaunching", "Starting the installer and restarting Chummer"));
                        LaunchInstallerHelper(helperPath, requestPath);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Desktop update payload '{artifact.FileName}' is neither in-place applyable nor installer-launchable.");
                    }

                    DateTimeOffset scheduledAt = DateTimeOffset.UtcNow;
                    DateTimeOffset rollbackWindowExpiresAt = scheduledAt.AddDays(RollbackWindowDays);
                    DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                    {
                        LastError = null,
                        LastFailureReason = null,
                        LastFailureAtUtc = null,
                        RetryAttempt = 0,
                        NextRetryAtUtc = null,
                        PendingUpdateVersion = manifest.Version,
                        PendingUpdateChannelId = manifest.ChannelId,
                        PendingUpdatePreparedAtUtc = scheduledAt,
                        PendingInstallerPath = downloadedArtifactPath,
                        LastUpdateLaunchAttemptAtUtc = null,
                        RollbackWindowStartedAtUtc = scheduledAt,
                        RollbackWindowExpiresAtUtc = rollbackWindowExpiresAt
                    });
                    progress?.Report(new DesktopUpdateProgressUpdate("relaunching", "Update handoff started"));
                    return DesktopUpdateStartupResult.ExitForApply();
                }
                catch (Exception ex)
                {
                    TryDeleteDirectory(stageRoot);
                    TryDeleteFile(downloadedArtifactPath);
                    artifactFailureSummaries.Add($"{artifact.FileName}: {BuildUpdateFailureMessage(ex)}");

                    if (artifactAttempt + 1 < artifacts.Count)
                    {
                        continue;
                    }

                    DesktopUpdateStateStore.Save(paths.StateFilePath, updatedState with
                    {
                        LastError = string.Join(" | ", artifactFailureSummaries),
                        LastFailureReason = "update_apply_failed",
                        LastFailureAtUtc = now,
                        RetryAttempt = updatedState.RetryAttempt + 1,
                        NextRetryAtUtc = CalculateBackoffTime(updatedState.RetryAttempt + 1)
                    });
                    progress?.Report(new DesktopUpdateProgressUpdate("failed", "Update preparation failed"));
                    return DesktopUpdateStartupResult.Continue(
                        "update_schedule_failed",
                        BuildAttentionMessageForUpdateScheduleFailure(string.Join(" | ", artifactFailureSummaries)));
                }
            }

            progress?.Report(new DesktopUpdateProgressUpdate("failed", "Update preparation failed"));
            return DesktopUpdateStartupResult.Continue(
                "update_schedule_failed",
                "The update could not be prepared. This copy will keep running.");
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
            progress?.Report(new DesktopUpdateProgressUpdate("failed", "Update preparation failed"));
            return DesktopUpdateStartupResult.Continue(
                "update_schedule_failed",
                BuildAttentionMessageForUpdateScheduleFailure(BuildUpdateFailureMessage(ex)));
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
            ChannelId: dto.ChannelId ?? string.Empty,
            HeadId: dto.HeadId ?? string.Empty,
            RelaunchArgs: dto.RelaunchArgs ?? []);
    }

    private static async Task<int> ApplyStagedUpdateAsync(DesktopUpdateApplyRequest request, CancellationToken ct)
    {
        try
        {
            await WaitForProcessExitAsync(request.ParentProcessId, ct).ConfigureAwait(false);
            ReplaceInstallDirectory(request.PayloadRoot, request.InstallDirectory);
            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            DateTimeOffset now = DateTimeOffset.UtcNow;
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
                LastError = null,
                LastFailureReason = null,
                LastFailureAtUtc = null,
                PendingUpdateVersion = null,
                PendingUpdateChannelId = null,
                PendingUpdatePreparedAtUtc = null,
                PendingInstallerPath = null,
                LastUpdateLaunchAttemptAtUtc = now,
                RollbackWindowStartedAtUtc = priorState?.RollbackWindowStartedAtUtc,
                RollbackWindowExpiresAtUtc = priorState?.RollbackWindowExpiresAtUtc
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
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    LastError = ex.Message,
                    LastFailureReason = "update_apply_failed",
                    LastFailureAtUtc = now
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
            DesktopBootstrapPayloadHandoff? payloadHandoff = OperatingSystem.IsWindows()
                ? TryLoadWindowsBootstrapPayloadHandoff(request.InstallerPath)
                : null;
            LaunchInstaller(request.InstallerPath, request.HeadId, request.RelaunchArgs, payloadHandoff);

            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            if (priorState is not null)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    ChannelId = string.IsNullOrWhiteSpace(request.ChannelId) ? priorState.ChannelId : request.ChannelId,
                    LastError = null,
                    LastFailureReason = null,
                    LastFailureAtUtc = null,
                    LastUpdateLaunchAttemptAtUtc = now,
                    PendingUpdateVersion = null,
                    PendingUpdateChannelId = null,
                    PendingUpdatePreparedAtUtc = null,
                    PendingInstallerPath = null
                });
            }

            return 0;
        }
        catch (Exception ex)
        {
            DesktopUpdateState? priorState = DesktopUpdateStateStore.Load(request.StateFilePath);
            if (priorState is not null)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DesktopUpdateStateStore.Save(request.StateFilePath, priorState with
                {
                    LastError = ex.Message,
                    LastFailureReason = "installer_launch_failed",
                    LastFailureAtUtc = now
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

            EnsureFileWithinLimit(localPath, MaximumManifestBytes, "Desktop update manifest");
            string json = await File.ReadAllTextAsync(localPath, ct).ConfigureAwait(false);
            return DesktopUpdateManifestParser.Parse(json, manifestUri);
        }

        EnsureTrustedRemoteManifestUri(manifestUri);
        string remoteJson = await ReadRemoteManifestAsync(manifestUri, ct).ConfigureAwait(false);
        return DesktopUpdateManifestParser.Parse(remoteJson, manifestUri);
    }

    private static async Task<string> ReadRemoteManifestAsync(Uri manifestUri, CancellationToken ct)
    {
        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        using HttpResponseMessage response = await client.GetAsync(manifestUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        EnsureContentLengthWithinLimit(response.Content.Headers.ContentLength, MaximumManifestBytes, "Desktop update manifest");
        await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using MemoryStream target = new();
        await CopyStreamWithByteLimitAsync(source, target, MaximumManifestBytes, "Desktop update manifest", ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(target.ToArray());
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
            if (!absoluteUri.IsFile)
            {
                EnsureTrustedRemoteManifestUri(absoluteUri);
            }

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

    private static string ResolveDefaultPublicManifestLocation()
        => DesktopPublicPortalRuntime.BuildPublicPortalAbsoluteUri(DefaultPublicManifestRelativePath);

    private static bool TryCompareReleaseVersions(string installedVersion, string manifestVersion, out int comparison)
    {
        comparison = 0;

        if (string.Equals(installedVersion, manifestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string installed = installedVersion.Trim();
        string manifest = manifestVersion.Trim();
        if (installed.Length == 0 || manifest.Length == 0)
        {
            return false;
        }

        Match installedRunVersion = RunVersionPattern.Match(installed);
        Match manifestRunVersion = RunVersionPattern.Match(manifest);
        if (installedRunVersion.Success && manifestRunVersion.Success)
        {
            string installedStamp = installedRunVersion.Groups["date"].Value + installedRunVersion.Groups["time"].Value;
            string manifestStamp = manifestRunVersion.Groups["date"].Value + manifestRunVersion.Groups["time"].Value;
            comparison = string.CompareOrdinal(installedStamp, manifestStamp);
            return true;
        }

        return false;
    }

    private static Uri ResolveArtifactUri(Uri manifestUri, DesktopUpdateArtifact artifact)
    {
        string rawUrl = !string.IsNullOrWhiteSpace(artifact.DownloadUrl)
            ? artifact.DownloadUrl
            : artifact.UpdateFeedUrl ?? string.Empty;
        if (rawUrl.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' cannot use a protocol-relative URL.");
        }

        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? absoluteUri))
        {
            bool isExplicitFileUri = string.Equals(absoluteUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)
                && (rawUrl.StartsWith("file:", StringComparison.OrdinalIgnoreCase) || manifestUri.IsFile);
            if (isExplicitFileUri)
            {
                return absoluteUri;
            }

            if (string.Equals(absoluteUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return manifestUri.IsFile
                    ? ResolveLocalArtifactUri(manifestUri, rawUrl)
                    : new Uri(manifestUri, rawUrl);
            }

            EnsureTrustedArtifactUri(manifestUri, absoluteUri);
            return absoluteUri;
        }

        if (manifestUri.IsFile)
        {
            return ResolveLocalArtifactUri(manifestUri, rawUrl);
        }

        Uri resolvedUri = new(manifestUri, rawUrl);
        EnsureTrustedArtifactUri(manifestUri, resolvedUri);
        return resolvedUri;
    }

    private static void EnsureTrustedRemoteManifestUri(Uri manifestUri)
    {
        if (manifestUri.IsFile)
        {
            return;
        }

        if (string.Equals(manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(manifestUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && manifestUri.IsLoopback)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Desktop update manifest '{manifestUri}' must use HTTPS or loopback HTTP.");
    }

    private static void EnsureTrustedArtifactUri(Uri manifestUri, Uri artifactUri)
    {
        if (artifactUri.IsFile)
        {
            if (manifestUri.IsFile)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Desktop update artifact '{artifactUri}' cannot use file URLs when the manifest is remote.");
        }

        if (string.Equals(artifactUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            if (artifactUri.IsLoopback && manifestUri.IsLoopback)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Desktop update artifact '{artifactUri}' must use HTTPS unless both manifest and artifact stay on loopback HTTP.");
        }

        if (!string.Equals(artifactUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifactUri}' must use HTTPS or a trusted local file path.");
        }

        if (!manifestUri.IsFile
            && !manifestUri.IsLoopback
            && !IsSameRemoteOrigin(manifestUri, artifactUri))
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{artifactUri}' must stay on the same origin as manifest '{manifestUri}'.");
        }
    }

    private static bool IsSameRemoteOrigin(Uri left, Uri right)
        => string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;

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

    private static async Task DownloadArtifactAsync(
        Uri downloadUri,
        string destinationPath,
        DesktopUpdateArtifact artifact,
        IProgress<DesktopUpdateProgressUpdate>? progress,
        CancellationToken ct)
    {
        string displayName = artifact.FileName;
        long expectedBytes = artifact.SizeBytes
            ?? throw new InvalidOperationException(
                $"Desktop update artifact '{artifact.FileName}' is missing required positive sizeBytes metadata.");
        for (int attempt = 1; attempt <= ArtifactDownloadRetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                if (downloadUri.IsFile)
                {
                    EnsureFileWithinLimit(downloadUri.LocalPath, MaximumArtifactBytes, $"Desktop update artifact '{displayName}'");
                    CopyFileWithProgress(downloadUri.LocalPath, destinationPath, displayName, expectedBytes, progress);
                    return;
                }

                using HttpClient client = new()
                {
                    Timeout = TimeSpan.FromMinutes(2)
                };
                using HttpResponseMessage response = await client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                EnsureContentLengthWithinLimit(response.Content.Headers.ContentLength, MaximumArtifactBytes, $"Desktop update artifact '{displayName}'");
                if (response.Content.Headers.ContentLength is long contentLength && contentLength != expectedBytes)
                {
                    throw new InvalidOperationException(
                        $"Desktop update artifact '{displayName}' failed size validation. Expected {expectedBytes} bytes, response declared {contentLength} bytes.");
                }

                await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using FileStream target = File.Create(destinationPath);
                await CopyStreamWithProgressAsync(
                    source,
                    target,
                    expectedBytes,
                    displayName,
                    progress,
                    ct).ConfigureAwait(false);
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

    private static void CopyFileWithProgress(
        string sourcePath,
        string destinationPath,
        string displayName,
        long expectedBytes,
        IProgress<DesktopUpdateProgressUpdate>? progress)
    {
        long sourceLength = GetFileSize(sourcePath);
        if (sourceLength != expectedBytes)
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{displayName}' failed size validation. Expected {expectedBytes} bytes, source has {sourceLength} bytes.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using FileStream source = File.OpenRead(sourcePath);
        using FileStream target = File.Create(destinationPath);
        byte[] buffer = new byte[1024 * 1024];
        long copied = 0L;
        while (true)
        {
            int read = source.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            target.Write(buffer, 0, read);
            copied += read;
            progress?.Report(new DesktopUpdateProgressUpdate(
                "downloading",
                $"Downloading {displayName}",
                ToProgressUnits(copied, source.Length),
                1000));
        }
    }

    private static async Task CopyStreamWithProgressAsync(
        Stream source,
        Stream target,
        long expectedBytes,
        string displayName,
        IProgress<DesktopUpdateProgressUpdate>? progress,
        CancellationToken ct)
    {
        byte[] buffer = new byte[1024 * 1024];
        long copied = 0L;
        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            copied += read;
            if (copied > expectedBytes)
            {
                throw new InvalidOperationException(
                    $"Desktop update artifact '{displayName}' exceeded the manifest size while downloading. Expected {expectedBytes} bytes.");
            }

            progress?.Report(new DesktopUpdateProgressUpdate(
                "downloading",
                $"Downloading {displayName}",
                ToProgressUnits(copied, expectedBytes),
                1000));
        }

        if (copied != expectedBytes)
        {
            throw new InvalidOperationException(
                $"Desktop update artifact '{displayName}' failed size validation. Expected {expectedBytes} bytes, observed {copied} bytes.");
        }
    }

    private static async Task CopyStreamWithByteLimitAsync(
        Stream source,
        Stream target,
        long maximumBytes,
        string displayName,
        CancellationToken ct)
    {
        byte[] buffer = new byte[64 * 1024];
        long copied = 0L;
        while (true)
        {
            int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            copied += read;
            if (copied > maximumBytes)
            {
                throw new InvalidOperationException(
                    $"{displayName} exceeded the maximum allowed size of {maximumBytes} bytes.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }

    private static void EnsureContentLengthWithinLimit(long? contentLength, long maximumBytes, string displayName)
    {
        if (contentLength is > 0 && contentLength.Value > maximumBytes)
        {
            throw new InvalidOperationException(
                $"{displayName} is too large. Expected at most {maximumBytes} bytes, response declared {contentLength.Value} bytes.");
        }
    }

    private static void EnsureFileWithinLimit(string path, long maximumBytes, string displayName)
    {
        long length = GetFileSize(path);
        if (length > maximumBytes)
        {
            throw new InvalidOperationException(
                $"{displayName} is too large. Expected at most {maximumBytes} bytes, file has {length} bytes.");
        }
    }

    private static int ToProgressUnits(long completed, long? total)
    {
        if (total is null or <= 0)
        {
            return 0;
        }

        decimal ratio = Math.Clamp((decimal)completed / total.Value, 0m, 1m);
        return (int)Math.Round(ratio * 1000m, MidpointRounding.AwayFromZero);
    }

    private static bool IsRetryableDownloadFailure(Exception ex)
        => ex is IOException
            || ex is TimeoutException
            || ex is TaskCanceledException
            || ex is HttpRequestException;

    private static string BuildUpdateFailureMessage(Exception ex)
        => $"Update preparation failed: {ex.GetType().Name}: {ex.Message}";

    private static async Task StageInstallerBootstrapPayloadIfNeededAsync(
        Uri manifestUri,
        DesktopUpdateChannelManifest manifest,
        DesktopUpdateArtifact artifact,
        string installerPath,
        IProgress<DesktopUpdateProgressUpdate>? progress,
        CancellationToken ct)
    {
        if (!string.Equals(artifact.InstallerMode, "bootstrap", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DesktopUpdateArtifact payloadArtifact = BuildInstallerBootstrapPayloadArtifact(artifact);
        Uri payloadUri = ResolveArtifactUri(manifestUri, payloadArtifact);
        string installerDirectory = Path.GetDirectoryName(installerPath)
            ?? throw new InvalidOperationException($"Installer path '{installerPath}' did not have a parent directory.");
        string payloadPath = Path.Combine(installerDirectory, payloadArtifact.FileName);
        await DownloadArtifactAsync(payloadUri, payloadPath, payloadArtifact, progress, ct).ConfigureAwait(false);
        ValidateArtifactIntegrity(payloadPath, payloadArtifact);
        WriteInstallerBootstrapPayloadSidecar(payloadPath, artifact.FileName, payloadArtifact, manifest.Version, payloadUri);
    }

    private static DesktopUpdateArtifact BuildInstallerBootstrapPayloadArtifact(DesktopUpdateArtifact artifact)
    {
        string payloadFileName = string.IsNullOrWhiteSpace(artifact.PayloadFileName)
            ? throw new InvalidOperationException(
                $"Desktop bootstrap installer '{artifact.FileName}' is missing payloadFileName.")
            : artifact.PayloadFileName.Trim();
        string payloadDownloadUrl = !string.IsNullOrWhiteSpace(artifact.PayloadDownloadUrl)
            ? artifact.PayloadDownloadUrl.Trim()
            : payloadFileName;
        if (string.IsNullOrWhiteSpace(artifact.PayloadSha256))
        {
            throw new InvalidOperationException(
                $"Desktop bootstrap installer '{artifact.FileName}' is missing payloadSha256.");
        }

        if (artifact.PayloadSizeBytes is null or <= 0)
        {
            throw new InvalidOperationException(
                $"Desktop bootstrap installer '{artifact.FileName}' is missing payloadSizeBytes.");
        }

        return new DesktopUpdateArtifact(
            ArtifactId: $"{artifact.ArtifactId}-payload",
            HeadId: artifact.HeadId,
            Platform: artifact.Platform,
            Arch: artifact.Arch,
            Kind: "archive",
            FileName: payloadFileName,
            DownloadUrl: payloadDownloadUrl,
            UpdateFeedUrl: null,
            Sha256: artifact.PayloadSha256,
            SizeBytes: artifact.PayloadSizeBytes);
    }

    private static void WriteInstallerBootstrapPayloadSidecar(
        string payloadPath,
        string installerFileName,
        DesktopUpdateArtifact payloadArtifact,
        string manifestVersion,
        Uri payloadUri)
    {
        string sidecarPath = payloadPath + ".json";
        string payloadSha256 = NormalizeSha256(payloadArtifact.Sha256 ?? string.Empty);
        if (string.IsNullOrWhiteSpace(payloadSha256))
        {
            throw new InvalidOperationException(
                $"Desktop bootstrap payload '{payloadArtifact.FileName}' is missing a valid SHA-256 checksum.");
        }

        object sidecarPayload = new
        {
            contractName = "chummer6-ui.windows_bootstrap_payload",
            fileName = payloadArtifact.FileName,
            downloadUrl = payloadUri.IsFile ? payloadUri.LocalPath : payloadUri.AbsoluteUri,
            sha256 = payloadSha256,
            sizeBytes = payloadArtifact.SizeBytes,
            installerFileName,
            releaseVersion = manifestVersion
        };

        File.WriteAllText(
            sidecarPath,
            JsonSerializer.Serialize(sidecarPayload, new JsonSerializerOptions
            {
                WriteIndented = true
            }),
            Encoding.UTF8);
    }

    private static DesktopBootstrapPayloadHandoff? TryLoadWindowsBootstrapPayloadHandoff(string installerPath)
    {
        string installerDirectory = Path.GetDirectoryName(installerPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installerDirectory) || !Directory.Exists(installerDirectory))
        {
            return null;
        }

        string installerFileName = Path.GetFileName(installerPath);
        foreach (string sidecarPath in Directory.GetFiles(installerDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sidecarPath, Encoding.UTF8));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string contractName = GetOptionalJsonString(root, "contractName");
                if (!string.Equals(contractName, "chummer6-ui.windows_bootstrap_payload", StringComparison.Ordinal))
                {
                    continue;
                }

                string sidecarInstallerFileName = GetOptionalJsonString(root, "installerFileName");
                if (!string.Equals(sidecarInstallerFileName, installerFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string payloadFileName = GetOptionalJsonString(root, "fileName");
                if (string.IsNullOrWhiteSpace(payloadFileName))
                {
                    continue;
                }

                string normalizedPayloadFileName = Path.GetFileName(payloadFileName);
                if (!string.Equals(normalizedPayloadFileName, payloadFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                string payloadPath = Path.Combine(installerDirectory, normalizedPayloadFileName);
                if (!File.Exists(payloadPath))
                {
                    continue;
                }

                string payloadSha256 = NormalizeSha256(GetOptionalJsonString(root, "sha256"));
                if (string.IsNullOrWhiteSpace(payloadSha256))
                {
                    continue;
                }

                long? payloadSizeBytes = GetOptionalJsonInt64(root, "sizeBytes");
                if (payloadSizeBytes is null or <= 0)
                {
                    continue;
                }

                string payloadDownloadUrl = GetOptionalJsonString(root, "downloadUrl");
                return new DesktopBootstrapPayloadHandoff(payloadPath, payloadDownloadUrl, payloadSha256, payloadSizeBytes.Value);
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    internal static string BuildAttentionMessageForUpdateScheduleFailure(string? failureDetail)
    {
        const string prefix = "The update could not be prepared. This copy will keep running.";
        string? detail = HumanizeUpdateFailureDetail(failureDetail);
        return string.IsNullOrWhiteSpace(detail)
            ? prefix
            : $"{prefix} {detail}";
    }

    private static string? HumanizeUpdateFailureDetail(string? failureDetail)
    {
        if (string.IsNullOrWhiteSpace(failureDetail))
        {
            return null;
        }

        string detail = failureDetail.Trim();
        detail = Regex.Replace(detail, @"\bUpdate preparation failed:\s*", string.Empty, RegexOptions.IgnoreCase);
        detail = Regex.Replace(detail, @"\b(?:InvalidOperationException|IOException|UnauthorizedAccessException|HttpRequestException|TaskCanceledException|ObjectDisposedException):\s*", string.Empty, RegexOptions.IgnoreCase);

        if (detail.Contains("Bundled desktop payload was not found", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("Installer payload was not found", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("staged desktop payload did not contain", StringComparison.OrdinalIgnoreCase))
        {
            return "The installer payload was missing.";
        }

        if (detail.Contains("checksum", StringComparison.OrdinalIgnoreCase))
        {
            return "The downloaded update did not pass integrity checks.";
        }

        if (detail.Contains("size validation", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("size mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return "The downloaded update size did not match the release manifest.";
        }

        if (detail.Contains("No compatible desktop update payload", StringComparison.OrdinalIgnoreCase))
        {
            return "No update payload is available for this platform yet.";
        }

        if (detail.Contains("Failed to download desktop update artifact", StringComparison.OrdinalIgnoreCase))
        {
            return "The update download failed.";
        }

        if (detail.Contains("FileNotFoundException", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("Could not find file", StringComparison.OrdinalIgnoreCase))
        {
            return "The update download failed.";
        }

        if (detail.Contains("Desktop update helper could not", StringComparison.OrdinalIgnoreCase))
        {
            return "The local update helper could not start.";
        }

        if (detail.Contains("Cannot access a disposed object", StringComparison.OrdinalIgnoreCase)
            || detail.Contains("disposed object", StringComparison.OrdinalIgnoreCase))
        {
            return "The local update helper closed before the handoff finished.";
        }

        detail = detail.Trim().TrimEnd('.', ';', '|');
        return string.IsNullOrWhiteSpace(detail)
            ? null
            : detail.EndsWith('.') ? detail : $"{detail}.";
    }

    private static string ResolveInstalledChannelId(DesktopUpdateState state, DesktopReleaseMetadata releaseMetadata)
    {
        if (!string.IsNullOrWhiteSpace(releaseMetadata.ChannelId)
            && !string.Equals(releaseMetadata.ChannelId, "local", StringComparison.OrdinalIgnoreCase))
        {
            return releaseMetadata.ChannelId;
        }

        return string.IsNullOrWhiteSpace(state.ChannelId)
            ? releaseMetadata.ChannelId
            : state.ChannelId;
    }

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
            request.ChannelId,
            request.HeadId,
            request.RelaunchArgs.ToArray());
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

    private static void LaunchInstaller(
        string installerPath,
        string headId,
        IReadOnlyList<string> relaunchArgs,
        DesktopBootstrapPayloadHandoff? payloadHandoff = null)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Installer payload was not found.", installerPath);
        }

        if (OperatingSystem.IsWindows())
        {
            IReadOnlyList<string> args = BuildWindowsInstallerArguments(
                headId,
                relaunchArgs,
                payloadHandoff?.PayloadPath,
                payloadHandoff?.DownloadUrl,
                payloadHandoff?.Sha256,
                payloadHandoff?.SizeBytes);
            IReadOnlyDictionary<string, string>? environment = BuildWindowsInstallerEnvironment(
                payloadHandoff?.PayloadPath,
                payloadHandoff?.DownloadUrl,
                payloadHandoff?.Sha256,
                payloadHandoff?.SizeBytes);
            StartDetachedProcess(installerPath, args, environment);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            if (installerPath.EndsWith(".deb", StringComparison.OrdinalIgnoreCase))
            {
                InstallLinuxDebianPackage(installerPath);
                TryLaunchLinuxInstalledApplication(headId, relaunchArgs);
                return;
            }

            throw new InvalidOperationException(
                $"Desktop installer launch is not supported on Linux for '{installerPath}'. Expected a Debian package.");
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

    private static IReadOnlyList<string> BuildWindowsInstallerArguments(
        string headId,
        IReadOnlyList<string> relaunchArgs,
        string? payloadPath = null,
        string? payloadDownloadUrl = null,
        string? payloadSha256 = null,
        long? payloadSizeBytes = null)
    {
        List<string> args = ["--auto-update"];
        if (!string.IsNullOrWhiteSpace(headId))
        {
            args.Add("--launch-head");
            args.Add(headId);
        }

        if (!string.IsNullOrWhiteSpace(payloadPath))
        {
            args.Add("--payload-path");
            args.Add(payloadPath);
        }

        if (!string.IsNullOrWhiteSpace(payloadDownloadUrl))
        {
            args.Add("--payload-url");
            args.Add(payloadDownloadUrl);
        }

        string normalizedSha256 = NormalizeSha256(payloadSha256 ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedSha256))
        {
            args.Add("--payload-sha256");
            args.Add(normalizedSha256);
        }

        if (payloadSizeBytes is > 0)
        {
            args.Add("--payload-size-bytes");
            args.Add(payloadSizeBytes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        foreach (string arg in relaunchArgs)
        {
            args.Add("--relaunch-arg");
            args.Add(arg);
        }

        return args;
    }

    private static IReadOnlyDictionary<string, string>? BuildWindowsInstallerEnvironment(
        string? payloadPath = null,
        string? payloadDownloadUrl = null,
        string? payloadSha256 = null,
        long? payloadSizeBytes = null)
    {
        bool hasLocalPayload = !string.IsNullOrWhiteSpace(payloadPath) && File.Exists(payloadPath);
        bool hasPayloadMetadata = !string.IsNullOrWhiteSpace(payloadDownloadUrl)
            || !string.IsNullOrWhiteSpace(payloadSha256)
            || payloadSizeBytes is > 0;
        if (!hasLocalPayload && !hasPayloadMetadata)
        {
            return null;
        }

        Dictionary<string, string> environment = new(StringComparer.Ordinal);

        if (hasLocalPayload)
        {
            environment[WindowsInstallerPayloadPathEnvironmentVariable] = payloadPath!;
        }

        if (!string.IsNullOrWhiteSpace(payloadDownloadUrl))
        {
            environment[WindowsInstallerPayloadUrlEnvironmentVariable] = payloadDownloadUrl;
        }

        string normalizedSha256 = NormalizeSha256(payloadSha256 ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedSha256))
        {
            environment[WindowsInstallerPayloadSha256EnvironmentVariable] = normalizedSha256;
        }

        if (payloadSizeBytes is > 0)
        {
            environment[WindowsInstallerPayloadSizeBytesEnvironmentVariable] = payloadSizeBytes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return environment;
    }

    private static void StartDetachedProcess(
        string path,
        IReadOnlyList<string>? args = null,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
            UseShellExecute = environment is null || environment.Count == 0
        };
        if (args is not null)
        {
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        if (environment is not null)
        {
            foreach ((string key, string value) in environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        Process? process = Process.Start(startInfo);
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

    private static void InstallLinuxDebianPackage(string installerPath)
    {
        bool dpkgAvailable = CommandExists("dpkg");
        bool pkexecAvailable = CommandExists("pkexec");
        bool sudoAvailable = CommandExists("sudo");
        DesktopUpdateCommandSpec? command = ResolveLinuxDebInstallerCommand(
            installerPath,
            IsRunningAsRoot(),
            dpkgAvailable,
            pkexecAvailable,
            sudoAvailable);
        if (command is null)
        {
            throw new InvalidOperationException(
                BuildLinuxDebInstallerUnavailableMessage(installerPath));
        }

        try
        {
            RunCommandToSuccessfulExit(command, TimeSpan.FromMinutes(InstallerCommandTimeoutMinutes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not apply Linux .deb update automatically. The downloaded package remains at '{installerPath}'. You can install it manually with: sudo dpkg -i {QuoteShellArgument(installerPath)}",
                ex);
        }
    }

    private static string BuildLinuxDebInstallerUnavailableMessage(string installerPath)
        => $"Could not apply Linux .deb update automatically. Expected root dpkg, pkexec+dpkg, or passwordless sudo+dpkg to be available. The downloaded package remains at '{installerPath}'. You can install it manually with: sudo dpkg -i {QuoteShellArgument(installerPath)}";

    private static DesktopUpdateCommandSpec? ResolveLinuxDebInstallerCommand(
        string installerPath,
        bool runningAsRoot,
        bool dpkgAvailable,
        bool pkexecAvailable,
        bool sudoAvailable)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !dpkgAvailable)
        {
            return null;
        }

        if (runningAsRoot)
        {
            return new DesktopUpdateCommandSpec("dpkg", ["-i", installerPath]);
        }

        if (pkexecAvailable)
        {
            return new DesktopUpdateCommandSpec("pkexec", ["dpkg", "-i", installerPath]);
        }

        return sudoAvailable
            ? new DesktopUpdateCommandSpec("sudo", ["-n", "dpkg", "-i", installerPath])
            : null;
    }

    private static string QuoteShellArgument(string value)
        => "'" + (value ?? string.Empty).Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";

    private static void TryLaunchLinuxInstalledApplication(string headId, IReadOnlyList<string> relaunchArgs)
    {
        string? launcherPath = ResolveLinuxInstalledLauncherPath(headId);
        if (string.IsNullOrWhiteSpace(launcherPath))
        {
            return;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = launcherPath,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? Path.GetTempPath()
            };
            foreach (string arg in relaunchArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Linux update installed, but relaunch via '{launcherPath}' failed: {ex.Message}");
        }
    }

    private static string? ResolveLinuxInstalledLauncherPath(string headId)
    {
        string launcherName = BuildLinuxInstalledLauncherName(headId);
        string? resolvedFromPath = TryResolveCommandPath(launcherName);
        if (!string.IsNullOrWhiteSpace(resolvedFromPath) && File.Exists(resolvedFromPath))
        {
            return resolvedFromPath;
        }

        foreach (string baseDirectory in new[] { "/usr/local/bin", "/usr/bin", "/bin" })
        {
            string candidate = Path.Combine(baseDirectory, launcherName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildLinuxInstalledLauncherName(string headId)
    {
        string normalizedHead = string.IsNullOrWhiteSpace(headId)
            ? "avalonia"
            : headId.Trim().ToLowerInvariant();
        normalizedHead = Regex.Replace(normalizedHead, "[^a-z0-9-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(normalizedHead))
        {
            normalizedHead = "avalonia";
        }

        return $"chummer6-{normalizedHead}";
    }

    private static string? TryResolveCommandPath(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "which",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(command);

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            if (process.ExitCode != 0)
            {
                return null;
            }

            string resolved = output.Trim();
            return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
        }
        catch
        {
            return null;
        }
    }

    private static bool CommandExists(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "where",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add(command);

                using Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    return false;
                }

                process.WaitForExit(2000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(TryResolveCommandPath(command));
    }

    private static bool IsRunningAsRoot()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (string.Equals(Environment.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("UID"), out int uid) && uid == 0)
        {
            return true;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = "id",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-u");

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            return process.ExitCode == 0
                && int.TryParse(output.Trim(), out int observedUid)
                && observedUid == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void RunCommandToSuccessfulExit(DesktopUpdateCommandSpec command, TimeSpan timeout)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetTempPath()
        };
        foreach (string arg in command.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Could not start '{command.FileName}'.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)Math.Min(timeout.TotalMilliseconds, int.MaxValue)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException($"Timed out waiting for '{command.FileName}' to finish the Linux update install.");
        }

        string errorText = errorTask.GetAwaiter().GetResult().Trim();
        string outputText = outputTask.GetAwaiter().GetResult().Trim();
        if (process.ExitCode == 0)
        {
            return;
        }

        string detail = !string.IsNullOrWhiteSpace(errorText) ? errorText : outputText;
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(detail)
                ? $"'{command.FileName}' exited with code {process.ExitCode}."
                : $"'{command.FileName}' exited with code {process.ExitCode}: {detail}");
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

    private static void CleanupCompletedUpdateArtifacts(string tempRoot)
    {
        if (!Directory.Exists(tempRoot))
        {
            return;
        }

        foreach (string entry in Directory.GetDirectories(tempRoot))
        {
            TryDeleteDirectory(entry);
        }

        foreach (string file in Directory.GetFiles(tempRoot))
        {
            TryDeleteFile(file);
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
        string ManifestLocation,
        string Mode)
    {
        public static DesktopUpdateConfiguration Load()
        {
            string? manifestLocation = Environment.GetEnvironmentVariable(UpdateManifestEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(manifestLocation))
            {
                manifestLocation = Environment.GetEnvironmentVariable(LegacyManifestEnvironmentVariable);
            }

            if (string.IsNullOrWhiteSpace(manifestLocation))
            {
                DesktopReleaseMetadata releaseMetadata = DesktopReleaseMetadata.Load("desktop");
                if (!string.Equals(releaseMetadata.ChannelId, "local", StringComparison.OrdinalIgnoreCase))
                {
                    manifestLocation = ResolveDefaultPublicManifestLocation();
                }
            }

            string? requestedMode = ParseMode(Environment.GetEnvironmentVariable(UpdateModeEnvironmentVariable));
            if (requestedMode is null
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UpdateEnabledEnvironmentVariable))
                && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UpdateAutoApplyEnvironmentVariable))
                && DesktopPreferenceRuntime.TryLoadState("avalonia", out DesktopPreferenceState preferences))
            {
                requestedMode = ParseMode(preferences.UpdateMode);
            }

            bool enabled;
            bool autoApply;
            string mode;
            if (requestedMode is not null)
            {
                mode = requestedMode;
                enabled = mode != UpdateModeOff;
                autoApply = mode == UpdateModeFull;
            }
            else
            {
                enabled = ParseBool(Environment.GetEnvironmentVariable(UpdateEnabledEnvironmentVariable), !string.IsNullOrWhiteSpace(manifestLocation));
                autoApply = ParseBool(Environment.GetEnvironmentVariable(UpdateAutoApplyEnvironmentVariable), defaultValue: true);
                mode = ResolveMode(enabled, autoApply);
            }

            return new DesktopUpdateConfiguration(
                Enabled: enabled && !string.IsNullOrWhiteSpace(manifestLocation),
                AutoApply: autoApply,
                ManifestLocation: manifestLocation ?? string.Empty,
                Mode: mode);
        }

        private static string ResolveMode(bool enabled, bool autoApply)
        {
            if (!enabled)
            {
                return UpdateModeOff;
            }

            return autoApply ? UpdateModeFull : UpdateModeNotify;
        }

        private static string? ParseMode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string normalized = raw.Trim().ToLowerInvariant().Replace("_", "-");
            return normalized switch
            {
                "full" or "auto" or "automatic" or "full-auto" or "full-autoupdate" => UpdateModeFull,
                "notify" or "notification" or "notify-only" or "manual" => UpdateModeNotify,
                "off" or "disabled" or "disable" or "none" => UpdateModeOff,
                _ => null
            };
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
        string? LastInstallAccessClass = null,
        string? LastDesktopChannelRef = null,
        string? LastInstallGuidanceRef = null,
        string? LastParticipationReceiptRef = null,
        string? LastRewardPublicationRef = null,
        string? LastDesktopPublicInstallRoute = null,
        string? LastDesktopSurfaceRationale = null,
        string? LastFailureReason = null,
        DateTimeOffset? LastFailureAtUtc = null,
        int RetryAttempt = 0,
        DateTimeOffset? NextRetryAtUtc = null,
        string? PendingUpdateVersion = null,
        string? PendingUpdateChannelId = null,
        DateTimeOffset? PendingUpdatePreparedAtUtc = null,
        string? PendingInstallerPath = null,
        DateTimeOffset? LastUpdateLaunchAttemptAtUtc = null,
        DateTimeOffset? RollbackWindowStartedAtUtc = null,
        DateTimeOffset? RollbackWindowExpiresAtUtc = null,
        string? LastStartupPromptedManifestVersion = null,
        DateTimeOffset? LastStartupPromptedAtUtc = null,
        string? LastManifestChannelId = null);

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
        string ChannelId,
        string HeadId,
        IReadOnlyList<string> RelaunchArgs);

    private sealed record DesktopBootstrapPayloadHandoff(
        string PayloadPath,
        string DownloadUrl,
        string Sha256,
        long SizeBytes);

    private sealed record DesktopUpdateCommandSpec(
        string FileName,
        IReadOnlyList<string> Arguments);

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
        string? ChannelId,
        string? HeadId,
        string[]? RelaunchArgs);

    private static string GetOptionalJsonString(JsonElement root, string propertyName)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Null ? string.Empty : property.Value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static long? GetOptionalJsonInt64(JsonElement root, string propertyName)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out long numeric))
            {
                return numeric;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && long.TryParse(property.Value.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long parsed))
            {
                return parsed;
            }

            return null;
        }

        return null;
    }
}
