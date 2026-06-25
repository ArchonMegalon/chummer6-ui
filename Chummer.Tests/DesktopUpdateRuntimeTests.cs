#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Desktop.Runtime;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopUpdateRuntimeTests
{
    private const string ManifestEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MANIFEST";
    private const string UpdateModeEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MODE";
    private const string UpdateEnabledEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_ENABLED";
    private const string UpdateAutoApplyEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY";
    private const string StateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string UpdateProcessPathOverrideEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_PROCESS_PATH_OVERRIDE";

    [TestMethod]
    public void DesktopSurfacePostureText_uses_plain_user_language()
    {
        DesktopUpdateClientStatus status = new(
            HeadId: "avalonia",
            InstalledVersion: "run-20260621-054902",
            ChannelId: "stable",
            Platform: "linux",
            Arch: "x64",
            UpdatesEnabled: true,
            AutoApply: true,
            ManifestLocation: "https://chummer.run/downloads/RELEASE_CHANNEL.generated.json",
            LastCheckedAtUtc: DateTimeOffset.UtcNow,
            LastManifestVersion: "run-20260621-054902",
            LastManifestPublishedAtUtc: DateTimeOffset.UtcNow,
            LastError: null,
            Status: "current",
            RecommendedAction: "Continue.",
            InstallAccessClass: "open_public",
            DesktopChannelRef: "desktop-channel:stable",
            InstallGuidanceRef: "install-guidance:stable",
            ParticipationReceiptRef: "participation-receipt:stable",
            RewardPublicationRef: "reward-publication:stable",
            PublicInstallRoute: "/downloads",
            DesktopSurfaceRationale: "Registry proof posture is open_public on the install rail.");

        string copy = string.Join("\n", DesktopSurfacePostureText.BuildLines(status));

        StringAssert.Contains(copy, "Account link: optional.");
        StringAssert.Contains(copy, "Devices & Access keeps this copy, downloads, updates, and recovery in one place.");
        StringAssert.Contains(copy, "Download channel: available.");
        StringAssert.Contains(copy, "Install help: available.");
        StringAssert.Contains(copy, "Account activity: available.");
        StringAssert.Contains(copy, "Recovery page: /downloads");
        foreach (string forbidden in new[]
                 {
                     "Entitlement posture",
                     "Desktop follow-through",
                     "Desktop channel ref",
                     "Install guidance ref",
                     "Participation receipt",
                     "Reward publication ref",
                     "Registry rationale",
                     "proof",
                     "receipt",
                     "posture",
                     "rail"
                 })
        {
            Assert.IsFalse(copy.Contains(forbidden, StringComparison.OrdinalIgnoreCase), copy);
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_manifest_load_failed_records_retry_backoff()
    {
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-missing-{Guid.NewGuid():N}.json");
        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
            "avalonia",
            [],
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("manifest_load_failed", result.Reason);

        string statePath = stateRootScope.StatePathForHead("avalonia");
        Assert.IsTrue(File.Exists(statePath));
        using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));

        string? lastFailureReason = GetStringProperty(state.RootElement, "lastFailureReason");
        Assert.AreEqual("manifest_load_failed", lastFailureReason);

        Assert.IsNotNull(GetStringProperty(state.RootElement, "lastError"));
        Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "nextRetryAtUtc"));
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_no_matching_payload_surfaces_failure_reason()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-no-match-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "other-head-linux-x64",
                  "head": "other",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-other-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "/tmp/does-not-matter/other.zip"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("no_matching_payload", result.Reason);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("no_matching_payload", GetStringProperty(state.RootElement, "lastFailureReason"));
            Assert.IsNotNull(GetStringProperty(state.RootElement, "lastError"));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_download_failure_records_apply_failure()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string missingPayloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-missing-artifact-{Guid.NewGuid():N}.zip");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-download-failed-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{missingPayloadPath.Replace("\\", "/")}}",
                  "sizeBytes": 12,
                  "sha256": "{{new string('a', 64)}}"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);
            StringAssert.Contains(result.Message ?? string.Empty, "update download failed", StringComparison.OrdinalIgnoreCase);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("update_apply_failed", GetStringProperty(state.RootElement, "lastFailureReason"));
            string? lastError = GetStringProperty(state.RootElement, "lastError");
            Assert.IsNotNull(lastError);
            Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "nextRetryAtUtc"));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_rollout_blocked_manifests_reason_and_stops_scheduling()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-blocked-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "rolloutState": "revoked",
              "rolloutReason": "Emergency revoke from Registry.",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "/tmp/does-not-matter/blocked.zip"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("rollout_blocked", result.Reason);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("rollout_blocked", GetStringProperty(state.RootElement, "lastFailureReason"));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "revoked");
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_notify_mode_records_available_update_without_staging_payload()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"run-29991231-235959";
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-notify-{Guid.NewGuid():N}.json");
        string payloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-notify-should-not-download-{Guid.NewGuid():N}.zip");
        string manifestJson = $$"""
            {
              "channelId": "stable",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{payloadPath.Replace("\\", "/")}}"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateModeEnvironmentVariable] = "notify",
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("notify_only", result.Reason);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual(version, GetStringProperty(state.RootElement, "lastManifestVersion"));
            Assert.IsNull(GetStringProperty(state.RootElement, "pendingUpdateVersion"));
            Assert.IsNull(GetStringProperty(state.RootElement, "lastFailureReason"));
            Assert.IsFalse(Directory.Exists(stateRootScope.TempRootForHead("avalonia")));

            DesktopUpdateClientStatus status = DesktopUpdateRuntime.GetCurrentStatus("avalonia");
            Assert.AreEqual("notify", status.UpdateMode);
            Assert.IsFalse(status.AutoApply);
            Assert.AreEqual("update_available", status.Status);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_off_mode_skips_without_reading_manifest()
    {
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-off-{Guid.NewGuid():N}.json");
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateModeEnvironmentVariable] = "off",
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
            "avalonia",
            [],
            CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual("update_mode_off", result.Reason);
        Assert.IsFalse(File.Exists(stateRootScope.StatePathForHead("avalonia")));

        DesktopUpdateClientStatus status = DesktopUpdateRuntime.GetCurrentStatus("avalonia");
        Assert.AreEqual("off", status.UpdateMode);
        Assert.IsFalse(status.UpdatesEnabled);
        Assert.IsFalse(status.AutoApply);
        Assert.AreEqual("disabled", status.Status);
        StringAssert.Contains(status.RecommendedAction, "turned off", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void TryCompareReleaseVersions_orders_run_timestamps_and_blocks_downgrades()
    {
        System.Reflection.MethodInfo? method = typeof(DesktopUpdateRuntime).GetMethod(
            "TryCompareReleaseVersions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, "Expected DesktopUpdateRuntime.TryCompareReleaseVersions to remain available for coverage.");

        object?[] newerInstalledArgs = ["run-20260617-064329", "run-20260617-061500", 0];
        bool comparable = (bool)method.Invoke(null, newerInstalledArgs)!;
        Assert.IsTrue(comparable);
        Assert.IsTrue((int)newerInstalledArgs[2]! > 0);

        object?[] equalArgs = ["run-20260617-064329", "run-20260617-064329", 0];
        comparable = (bool)method.Invoke(null, equalArgs)!;
        Assert.IsTrue(comparable);
        Assert.AreEqual(0, (int)equalArgs[2]!);

        object?[] olderInstalledArgs = ["run-20260617-061500", "run-20260617-064329", 0];
        comparable = (bool)method.Invoke(null, olderInstalledArgs)!;
        Assert.IsTrue(comparable);
        Assert.IsTrue((int)olderInstalledArgs[2]! < 0);
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_rejects_artifact_that_fails_checksum_validation()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string payloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-size-{Guid.NewGuid():N}.zip");
        File.WriteAllText(payloadPath, "payload-data");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-checksum-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
            {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{payloadPath.Replace("\\", "/")}}",
                  "sizeBytes": 12,
                  "sha256": "sha256:badbadsum"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);
            StringAssert.Contains(result.Message ?? string.Empty, "integrity", StringComparison.OrdinalIgnoreCase);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("update_apply_failed", GetStringProperty(state.RootElement, "lastFailureReason"));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "checksum");
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_rejects_payload_without_checksum()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string payloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-no-sha-{Guid.NewGuid():N}.zip");
        File.WriteAllText(payloadPath, "payload-data");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-no-sha-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{payloadPath.Replace("\\", "/")}}",
                  "sizeBytes": 12
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);
            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "missing a required SHA-256 checksum", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_rejects_payload_without_size_metadata()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string payloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-no-size-{Guid.NewGuid():N}.zip");
        File.WriteAllText(payloadPath, "payload-data");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-no-size-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{payloadPath.Replace("\\", "/")}}",
                  "sha256": "{{new string('a', 64)}}"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);
            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "sizeBytes", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_rejects_payload_archive_that_does_not_contain_launch_executable()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string payloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-missing-launch-{Guid.NewGuid():N}.zip");
        using (ZipArchive archive = ZipFile.Open(payloadPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry notesEntry = archive.CreateEntry("notes/readme.txt");
            using StreamWriter writer = new(notesEntry.Open());
            writer.Write("payload exists but launch executable is missing");
        }

        byte[] payloadBytes = File.ReadAllBytes(payloadPath);
        string payloadSha = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-missing-launch-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "{{payloadPath.Replace("\\", "/")}}",
                  "sizeBytes": {{payloadBytes.Length}},
                  "sha256": "sha256:{{payloadSha}}"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);
            StringAssert.Contains(result.Message ?? string.Empty, "installer payload was missing", StringComparison.OrdinalIgnoreCase);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("update_apply_failed", GetStringProperty(state.RootElement, "lastFailureReason"));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "did not contain", StringComparison.OrdinalIgnoreCase);
            Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "nextRetryAtUtc"));
        }
        finally
        {
            if (File.Exists(payloadPath))
            {
                File.Delete(payloadPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public void BuildAttentionMessageForUpdateScheduleFailure_humanizes_disposed_object_failures()
    {
        string message = InvokePrivateStatic<string>(
            "BuildAttentionMessageForUpdateScheduleFailure",
            "Update preparation failed: ObjectDisposedException: Cannot access a disposed object.");

        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The local update helper closed before the handoff finished.",
            message);
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_falls_back_between_matching_artifacts_on_failures()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string firstPayloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-fallback-1-{Guid.NewGuid():N}.zip");
        string secondPayloadPath = Path.Combine(Path.GetTempPath(), $"desktop-update-artifact-fallback-2-{Guid.NewGuid():N}.zip");
        File.WriteAllText(firstPayloadPath, "primary-payload");
        File.WriteAllText(secondPayloadPath, "secondary-payload");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-fallback-{Guid.NewGuid():N}.json");
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}-primary.zip",
                  "downloadUrl": "{{firstPayloadPath.Replace("\\", "/")}}",
                  "sizeBytes": 8,
                  "sha256": "sha256:wrong"
                },
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}-secondary.zip",
                  "downloadUrl": "{{secondPayloadPath.Replace("\\", "/")}}",
                  "sizeBytes": 15,
                  "sha256": "sha256:wrong"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("update_schedule_failed", result.Reason);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            string? lastError = GetStringProperty(state.RootElement, "lastError");
            Assert.IsTrue((lastError ?? string.Empty).Contains("primary", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue((lastError ?? string.Empty).Contains("secondary", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual("update_apply_failed", GetStringProperty(state.RootElement, "lastFailureReason"));
        }
        finally
        {
            if (File.Exists(firstPayloadPath))
            {
                File.Delete(firstPayloadPath);
            }
            if (File.Exists(secondPayloadPath))
            {
                File.Delete(secondPayloadPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"run-20991231-{Guid.NewGuid():N}".Substring(0, 23);
        string tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-update-bootstrap-handoff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        string installerSourcePath = Path.Combine(tempRoot, "chummer-avalonia-linux-x64-installer.deb");
        string payloadSourcePath = Path.Combine(tempRoot, "chummer-avalonia-win-x64-payload.zip");
        string manifestPath = Path.Combine(tempRoot, "RELEASE_CHANNEL.generated.json");
        string helperPath = Path.Combine(AppContext.BaseDirectory, $"desktop-update-helper-script-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(installerSourcePath, "installer-bytes");
            using (ZipArchive archive = ZipFile.Open(payloadSourcePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry launcher = archive.CreateEntry("Chummer.Avalonia.exe");
                using StreamWriter writer = new(launcher.Open());
                writer.Write("launcher");
            }

            byte[] installerBytes = File.ReadAllBytes(installerSourcePath);
            string installerSha = Convert.ToHexString(SHA256.HashData(installerBytes)).ToLowerInvariant();
            byte[] payloadBytes = File.ReadAllBytes(payloadSourcePath);
            string payloadSha = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

            File.WriteAllText(
                manifestPath,
                $$"""
                {
                  "channelId": "stable",
                  "version": "{{version}}",
                  "status": "published",
                  "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
                  "artifacts": [
                    {
                      "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}-installer",
                      "head": "avalonia",
                      "platform": "{{identity.Platform}}",
                      "arch": "{{identity.Arch}}",
                      "kind": "installer",
                      "fileName": "{{Path.GetFileName(installerSourcePath)}}",
                      "downloadUrl": "{{installerSourcePath.Replace("\\", "/")}}",
                      "sha256": "{{installerSha}}",
                      "sizeBytes": {{installerBytes.LongLength}},
                      "installerMode": "bootstrap",
                      "payloadFileName": "{{Path.GetFileName(payloadSourcePath)}}",
                      "payloadDownloadUrl": "{{payloadSourcePath.Replace("\\", "/")}}",
                      "payloadSha256": "{{payloadSha}}",
                      "payloadSizeBytes": {{payloadBytes.LongLength}}
                    }
                  ]
                }
                """);

            File.WriteAllText(
                helperPath,
                "#!/usr/bin/env bash\nexit 0\n");
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(
                    helperPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            using TestStateRootScope stateRootScope = new();
            using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
            {
                [ManifestEnvironmentVariable] = manifestPath,
                [UpdateEnabledEnvironmentVariable] = "true",
                [UpdateAutoApplyEnvironmentVariable] = "true",
                [StateRootEnvironmentVariable] = stateRootScope.Root,
                [UpdateProcessPathOverrideEnvironmentVariable] = helperPath
            });

            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(result.ExitRequested, $"{result.Reason}: {result.Message}");
            Assert.AreEqual("apply_scheduled", result.Reason);

            string statePath = stateRootScope.StatePathForHead("avalonia");
            Assert.IsTrue(File.Exists(statePath));
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual(version, GetStringProperty(state.RootElement, "pendingUpdateVersion"));
            Assert.AreEqual("stable", GetStringProperty(state.RootElement, "pendingUpdateChannelId"));
            Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "pendingUpdatePreparedAtUtc"));

            string runtimeTempRoot = stateRootScope.TempRootForHead("avalonia");
            string[] stageDirectories = Directory.GetDirectories(runtimeTempRoot, "stage-*", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, stageDirectories.Length);

            string stageDirectory = stageDirectories[0];
            string stagedInstallerPath = Path.Combine(stageDirectory, Path.GetFileName(installerSourcePath));
            string stagedPayloadPath = Path.Combine(stageDirectory, Path.GetFileName(payloadSourcePath));
            string stagedPayloadSidecarPath = stagedPayloadPath + ".json";
            string requestPath = Path.Combine(stageDirectory, "installer-request.json");

            Assert.IsTrue(File.Exists(stagedInstallerPath));
            Assert.IsTrue(File.Exists(stagedPayloadPath));
            Assert.IsTrue(File.Exists(stagedPayloadSidecarPath));
            Assert.IsTrue(File.Exists(requestPath));
            CollectionAssert.AreEqual(payloadBytes, File.ReadAllBytes(stagedPayloadPath));

            using JsonDocument request = JsonDocument.Parse(File.ReadAllText(requestPath));
            Assert.AreEqual(stagedInstallerPath, GetStringProperty(request.RootElement, "installerPath"));

            using JsonDocument sidecar = JsonDocument.Parse(File.ReadAllText(stagedPayloadSidecarPath));
            Assert.AreEqual("chummer6-ui.windows_bootstrap_payload", GetStringProperty(sidecar.RootElement, "contractName"));
            Assert.AreEqual(Path.GetFileName(payloadSourcePath), GetStringProperty(sidecar.RootElement, "fileName"));
            Assert.AreEqual(Path.GetFileName(installerSourcePath), GetStringProperty(sidecar.RootElement, "installerFileName"));
            Assert.AreEqual(payloadSha, GetStringProperty(sidecar.RootElement, "sha256"));
            Assert.AreEqual(version, GetStringProperty(sidecar.RootElement, "releaseVersion"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }

            if (File.Exists(helperPath))
            {
                File.Delete(helperPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_returns_helper_unavailable_when_override_is_not_packaged_like()
    {
        string helperPath = Path.Combine(Path.GetTempPath(), $"desktop-update-helper-outside-base-{Guid.NewGuid():N}.exe");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-helper-unavailable-{Guid.NewGuid():N}.json");
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        string version = $"0.0.0-test-{Guid.NewGuid():N}";
        string manifestJson = $$"""
            {
              "channelId": "preview",
              "version": "{{version}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": [
                {
                  "artifactId": "avalonia-{{identity.Platform}}-{{identity.Arch}}",
                  "head": "avalonia",
                  "platform": "{{identity.Platform}}",
                  "arch": "{{identity.Arch}}",
                  "kind": "archive",
                  "fileName": "chummer-avalonia-{{identity.Platform}}-{{identity.Arch}}.zip",
                  "downloadUrl": "/tmp/does-not-matter/avalonia.zip"
                }
              ]
            }
            """;
        File.WriteAllText(manifestPath, manifestJson);
        File.WriteAllText(helperPath, "// helper outside AppContext base directory");
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root,
            [UpdateProcessPathOverrideEnvironmentVariable] = helperPath
        });
        string statePath = stateRootScope.StatePathForHead("avalonia");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            $$"""
            {
              "HeadId": "avalonia",
              "Platform": "{{identity.Platform}}",
              "Arch": "{{identity.Arch}}",
              "InstalledVersion": "0.0.0",
              "ChannelId": "preview",
              "LastCheckedAt": null,
              "LastManifestVersion": "0.0.0",
              "LastManifestPublishedAt": null,
              "LastError": null
            }
            """);

        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("helper_unavailable", result.Reason);
        }
        finally
        {
            if (File.Exists(helperPath))
            {
                File.Delete(helperPath);
            }
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public void GetCurrentStatus_reports_disabled_when_manifest_is_missing()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = null,
            [UpdateEnabledEnvironmentVariable] = null,
            [UpdateAutoApplyEnvironmentVariable] = null,
            [StateRootEnvironmentVariable] = null
        });

        DesktopUpdateClientStatus status = DesktopUpdateRuntime.GetCurrentStatus("avalonia");

        Assert.AreEqual("disabled", status.Status);
        StringAssert.Contains(status.RecommendedAction, "Choose an update source", StringComparison.Ordinal);
    }

    [TestMethod]
    public void GetCurrentStatus_reports_release_attention_for_failed_proof_and_paused_rollout()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/manifest.json",
            [UpdateEnabledEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        string statePath = stateRootScope.StatePathForHead("avalonia");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            """
            {
              "HeadId": "avalonia",
              "Platform": "linux",
              "Arch": "x64",
              "InstalledVersion": "6.0.1-preview",
              "ChannelId": "preview",
              "LastCheckedAt": "2026-05-20T09:00:00Z",
              "LastManifestVersion": "6.0.1-preview",
              "LastManifestPublishedAt": "2026-05-20T08:55:00Z",
              "LastError": null,
              "LastProofStatus": "failed",
              "LastRolloutState": "paused",
              "LastRolloutReason": "registry hold"
            }
            """);

        DesktopUpdateClientStatus status = DesktopUpdateRuntime.GetCurrentStatus("avalonia");

        Assert.AreEqual("attention_required", status.Status);
        StringAssert.Contains(status.RecommendedAction, "latest release check failed", StringComparison.OrdinalIgnoreCase);
        Assert.IsFalse(status.RecommendedAction.Contains("proof", StringComparison.OrdinalIgnoreCase), status.RecommendedAction);
    }

    [TestMethod]
    public void GetCurrentStatus_reports_update_staged_when_pending_update_is_already_prepared()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/manifest.json",
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        string statePath = stateRootScope.StatePathForHead("avalonia");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            """
            {
              "HeadId": "avalonia",
              "Platform": "linux",
              "Arch": "x64",
              "InstalledVersion": "run-20260617-110751",
              "ChannelId": "preview",
              "LastCheckedAt": "2026-06-18T06:00:00Z",
              "LastManifestVersion": "run-20260618-061500",
              "LastManifestPublishedAt": "2026-06-18T06:15:00Z",
              "LastError": null,
              "PendingUpdateVersion": "run-20260618-061500",
              "PendingUpdateChannelId": "preview",
              "PendingUpdatePreparedAtUtc": "2026-06-18T06:16:00Z"
            }
            """);

        DesktopUpdateClientStatus status = DesktopUpdateRuntime.GetCurrentStatus("avalonia");

        Assert.AreEqual("update_staged", status.Status);
        Assert.AreEqual("run-20260618-061500", status.PendingUpdateVersion);
        StringAssert.Contains(status.RecommendedAction, "installing it in place", StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_already_current_preserves_installed_channel_and_records_manifest_channel()
    {
        string releaseVersion = ResolveCurrentRuntimeReleaseProperty("Version");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-current-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            manifestPath,
            $$"""
            {
              "channelId": "stable",
              "version": "{{releaseVersion}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": []
            }
            """);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            string statePath = stateRootScope.StatePathForHead("avalonia");
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(
                statePath,
                $$"""
                {
                  "HeadId": "avalonia",
                  "Platform": "{{DesktopUpdatePlatformIdentity.Current().Platform}}",
                  "Arch": "{{DesktopUpdatePlatformIdentity.Current().Arch}}",
                  "InstalledVersion": "{{releaseVersion}}",
                  "ChannelId": "preview",
                  "LastCheckedAt": "2026-06-18T06:00:00Z",
                  "LastManifestVersion": "{{releaseVersion}}",
                  "LastManifestPublishedAt": "2026-06-18T06:15:00Z",
                  "LastError": null,
                  "LastFailureReason": null,
                  "LastFailureAtUtc": "2026-06-18T06:16:00Z"
                }
                """);

            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("already_current", result.Reason);
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("preview", GetStringProperty(state.RootElement, "channelId"));
            Assert.AreEqual("stable", GetStringProperty(state.RootElement, "lastManifestChannelId"));
            Assert.IsNull(GetStringProperty(state.RootElement, "lastFailureReason"));
            Assert.IsNull(GetDateTimeProperty(state.RootElement, "lastFailureAtUtc"));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_missing_manifest_channel_preserves_installed_channel()
    {
        string releaseVersion = ResolveCurrentRuntimeReleaseProperty("Version");
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-no-channel-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            manifestPath,
            $$"""
            {
              "version": "{{releaseVersion}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": []
            }
            """);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            string statePath = stateRootScope.StatePathForHead("avalonia");
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(
                statePath,
                $$"""
                {
                  "HeadId": "avalonia",
                  "Platform": "{{DesktopUpdatePlatformIdentity.Current().Platform}}",
                  "Arch": "{{DesktopUpdatePlatformIdentity.Current().Arch}}",
                  "InstalledVersion": "{{releaseVersion}}",
                  "ChannelId": "stable",
                  "LastCheckedAt": "2026-06-18T06:00:00Z",
                  "LastManifestVersion": "{{releaseVersion}}",
                  "LastManifestPublishedAt": "2026-06-18T06:15:00Z",
                  "LastError": null
                }
                """);

            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("already_current", result.Reason);
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("stable", GetStringProperty(state.RootElement, "channelId"));
            Assert.IsNull(GetStringProperty(state.RootElement, "lastManifestChannelId"));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public async Task CheckAndScheduleStartupUpdateAsync_completed_pending_update_clears_stage_artifacts()
    {
        string releaseVersion = ResolveCurrentRuntimeReleaseProperty("Version");
        string releaseChannel = ResolveCurrentRuntimeReleaseProperty("ChannelId");
        string oldVersion = string.Equals(releaseVersion, "run-20000101-000000", StringComparison.OrdinalIgnoreCase)
            ? "run-19990101-000000"
            : "run-20000101-000000";
        string manifestPath = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-completed-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            manifestPath,
            $$"""
            {
              "channelId": "{{releaseChannel}}",
              "version": "{{releaseVersion}}",
              "status": "published",
              "publishedAt": "{{DateTimeOffset.UtcNow:O}}",
              "artifacts": []
            }
            """);

        using TestStateRootScope stateRootScope = new();
        using TestProcessPathOverrideScope processPathScope = TestProcessPathOverrideScope.CreatePackagedLike();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = manifestPath,
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        try
        {
            string statePath = stateRootScope.StatePathForHead("avalonia");
            string tempRoot = stateRootScope.TempRootForHead("avalonia");
            string stageDirectory = Path.Combine(tempRoot, "stage-completed");
            string helperPath = Path.Combine(tempRoot, "Chummer-update-helper-test.Avalonia");
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            Directory.CreateDirectory(stageDirectory);
            File.WriteAllText(Path.Combine(stageDirectory, "installer-request.json"), "{}");
            File.WriteAllText(helperPath, "helper");
            File.WriteAllText(
                statePath,
                $$"""
                {
                  "HeadId": "avalonia",
                  "Platform": "{{DesktopUpdatePlatformIdentity.Current().Platform}}",
                  "Arch": "{{DesktopUpdatePlatformIdentity.Current().Arch}}",
                  "InstalledVersion": "{{oldVersion}}",
                  "ChannelId": "preview",
                  "LastCheckedAt": "2026-06-18T06:00:00Z",
                  "LastManifestVersion": "{{releaseVersion}}",
                  "LastManifestPublishedAt": "2026-06-18T06:15:00Z",
                  "LastError": "previous error",
                  "LastFailureReason": "update_apply_failed",
                  "LastFailureAtUtc": "2026-06-18T06:16:00Z",
                  "PendingUpdateVersion": "{{releaseVersion}}",
                  "PendingUpdateChannelId": "{{releaseChannel}}",
                  "PendingUpdatePreparedAtUtc": "2026-06-18T06:16:00Z"
                }
                """);

            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                "avalonia",
                [],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual("already_current", result.Reason);
            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual(releaseVersion, GetStringProperty(state.RootElement, "installedVersion"));
            Assert.AreEqual(releaseChannel, GetStringProperty(state.RootElement, "channelId"));
            Assert.IsNull(GetStringProperty(state.RootElement, "lastError"));
            Assert.IsNull(GetStringProperty(state.RootElement, "lastFailureReason"));
            Assert.IsNull(GetDateTimeProperty(state.RootElement, "lastFailureAtUtc"));
            Assert.IsNull(GetStringProperty(state.RootElement, "pendingUpdateVersion"));
            Assert.IsNull(GetStringProperty(state.RootElement, "pendingUpdateChannelId"));
            Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "lastUpdateLaunchAttemptAtUtc"));
            Assert.IsFalse(Directory.Exists(stageDirectory));
            Assert.IsFalse(File.Exists(helperPath));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [TestMethod]
    public void ShouldPromptForStartupUpdate_returns_true_for_unseen_update_and_false_after_marking_prompt_shown()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/manifest.json",
            [UpdateEnabledEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        string statePath = stateRootScope.StatePathForHead("avalonia");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            """
            {
              "HeadId": "avalonia",
              "Platform": "linux",
              "Arch": "x64",
              "InstalledVersion": "run-20260616-110751",
              "ChannelId": "preview",
              "LastCheckedAt": "2026-06-17T05:00:00Z",
              "LastManifestVersion": "run-20260617-055252",
              "LastManifestPublishedAt": "2026-06-17T05:52:52Z",
              "LastError": null
            }
            """);

        Assert.IsTrue(DesktopUpdateRuntime.ShouldPromptForStartupUpdate("avalonia"));

        DesktopUpdateRuntime.MarkStartupUpdatePromptShown("avalonia");

        Assert.IsFalse(DesktopUpdateRuntime.ShouldPromptForStartupUpdate("avalonia"));
    }

    [TestMethod]
    public void ShouldPromptForStartupUpdate_returns_false_when_status_is_not_update_available()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/manifest.json",
            [UpdateEnabledEnvironmentVariable] = "true",
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });

        string statePath = stateRootScope.StatePathForHead("avalonia");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            """
            {
              "HeadId": "avalonia",
              "Platform": "linux",
              "Arch": "x64",
              "InstalledVersion": "run-20260617-055252",
              "ChannelId": "preview",
              "LastCheckedAt": "2026-06-17T05:00:00Z",
              "LastManifestVersion": "run-20260617-055252",
              "LastManifestPublishedAt": "2026-06-17T05:52:52Z",
              "LastError": null
            }
            """);

        Assert.IsFalse(DesktopUpdateRuntime.ShouldPromptForStartupUpdate("avalonia"));
    }

    [TestMethod]
    public void SelectCompatibleArtifacts_prefers_in_place_apply_and_filters_head_platform_matches()
    {
        DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
        DesktopUpdateChannelManifest manifest = new(
            ChannelId: "preview",
            Version: "6.0.2-preview",
            Status: "published",
            PublishedAt: DateTimeOffset.Parse("2026-05-20T09:00:00Z"),
            Artifacts:
            [
                new DesktopUpdateArtifact("other", "other", identity.Platform, identity.Arch, "archive", "other.zip", "/tmp/other.zip", null, null, null),
                new DesktopUpdateArtifact("installer", "avalonia", identity.Platform, identity.Arch, "installer", "avalonia-installer.exe", "/tmp/installer.exe", null, null, null),
                new DesktopUpdateArtifact("archive", "avalonia", identity.Platform, identity.Arch, "archive", "avalonia.zip", "/tmp/archive.zip", null, null, null)
            ],
            DesktopSurfaceRefs: [],
            RolloutState: null,
            RolloutReason: null,
            SupportabilityState: null,
            SupportabilitySummary: null,
            KnownIssueSummary: null,
            FixAvailabilitySummary: null,
            ProofStatus: null,
            ProofGeneratedAt: null,
            SourceUri: new Uri("file:///tmp/RELEASE_CHANNEL.generated.json"));

        IReadOnlyList<DesktopUpdateArtifact> artifacts = InvokePrivateStatic<IReadOnlyList<DesktopUpdateArtifact>>(
            "SelectCompatibleArtifacts",
            manifest,
            "avalonia",
            identity);

        Assert.AreEqual(2, artifacts.Count);
        Assert.AreEqual("archive", artifacts[0].ArtifactId);
        Assert.AreEqual("installer", artifacts[1].ArtifactId);
    }

    [TestMethod]
    public void Compatibility_manifest_parser_preserves_windows_bootstrap_installer_metadata()
    {
        string json =
            """
            {
              "channel": "public_stable",
              "version": "run-20260623-102621",
              "status": "published",
              "publishedAt": "2026-06-23T10:26:21Z",
              "downloads": [
                {
                  "id": "avalonia-win-x64-installer",
                  "platform": "Avalonia Desktop Windows X64 Installer",
                  "url": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
                  "sha256": "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
                  "sizeBytes": 51856809,
                  "format": "exe",
                  "flavor": "installer",
                  "kind": "installer",
                  "head": "avalonia",
                  "platformId": "windows-x64",
                  "arch": "x64",
                  "fileName": "chummer-avalonia-win-x64-installer.exe",
                  "channelId": "public_stable",
                  "releaseVersion": "run-20260623-102621",
                  "installerMode": "bootstrap",
                  "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
                  "payloadDownloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip",
                  "payloadSha256": "00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7",
                  "payloadSizeBytes": 47152146,
                  "installAccessClass": "open_public"
                }
              ]
            }
            """;

        DesktopUpdateChannelManifest manifest = DesktopUpdateManifestParser.Parse(
            json,
            new Uri("https://chummer.run/downloads/releases.json"));

        Assert.AreEqual("public_stable", manifest.ChannelId);
        Assert.AreEqual("run-20260623-102621", manifest.Version);
        Assert.AreEqual(1, manifest.Artifacts.Count);

        DesktopUpdateArtifact artifact = manifest.Artifacts[0];
        Assert.AreEqual("avalonia-win-x64-installer", artifact.ArtifactId);
        Assert.AreEqual("avalonia", artifact.HeadId);
        Assert.AreEqual("windows", artifact.Platform);
        Assert.AreEqual("x64", artifact.Arch);
        Assert.AreEqual("installer", artifact.Kind);
        Assert.IsTrue(artifact.SupportsInstallerHandoff);
        Assert.AreEqual("bootstrap", artifact.InstallerMode);
        Assert.AreEqual("chummer-avalonia-win-x64-payload.zip", artifact.PayloadFileName);
        Assert.AreEqual("https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip", artifact.PayloadDownloadUrl);
        Assert.AreEqual("00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7", artifact.PayloadSha256);
        Assert.AreEqual(47152146L, artifact.PayloadSizeBytes);
    }

    [TestMethod]
    public void Canonical_manifest_parser_preserves_windows_bootstrap_installer_metadata()
    {
        string json =
            """
            {
              "channelId": "public_stable",
              "version": "run-20260623-102621",
              "status": "published",
              "publishedAt": "2026-06-23T10:26:21Z",
              "artifacts": [
                {
                  "artifactId": "avalonia-win-x64-installer",
                  "head": "avalonia",
                  "platform": "windows",
                  "arch": "x64",
                  "kind": "installer",
                  "fileName": "chummer-avalonia-win-x64-installer.exe",
                  "downloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
                  "sha256": "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
                  "sizeBytes": 51856809,
                  "installerMode": "bootstrap",
                  "payloadFileName": "chummer-avalonia-win-x64-payload.zip",
                  "payloadDownloadUrl": "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip",
                  "payloadSha256": "sha256:00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7",
                  "payloadSizeBytes": 47152146
                }
              ]
            }
            """;

        DesktopUpdateChannelManifest manifest = DesktopUpdateManifestParser.Parse(
            json,
            new Uri("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json"));

        Assert.AreEqual(1, manifest.Artifacts.Count);

        DesktopUpdateArtifact artifact = manifest.Artifacts[0];
        Assert.AreEqual("bootstrap", artifact.InstallerMode);
        Assert.AreEqual("chummer-avalonia-win-x64-payload.zip", artifact.PayloadFileName);
        Assert.AreEqual("https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip", artifact.PayloadDownloadUrl);
        Assert.AreEqual("00d34c7514b9e44bd315c3d9914547d0c750865ddf5bffaf3e17f861648fe4b7", artifact.PayloadSha256);
        Assert.AreEqual(47152146L, artifact.PayloadSizeBytes);
    }

    [TestMethod]
    public void BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata()
    {
        DesktopUpdateArtifact installerArtifact = new(
            ArtifactId: "avalonia-win-x64-installer",
            HeadId: "avalonia",
            Platform: "windows",
            Arch: "x64",
            Kind: "installer",
            FileName: "chummer-avalonia-win-x64-installer.exe",
            DownloadUrl: "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
            UpdateFeedUrl: null,
            Sha256: "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
            SizeBytes: 51856809,
            InstallerMode: "bootstrap",
            PayloadFileName: "chummer-avalonia-win-x64-payload.zip",
            PayloadDownloadUrl: "https://chummer.run/downloads/files/chummer-avalonia-win-x64-payload.zip",
            PayloadSha256: null,
            PayloadSizeBytes: 47152146);

        try
        {
            _ = InvokePrivateStatic<DesktopUpdateArtifact>("BuildInstallerBootstrapPayloadArtifact", installerArtifact);
            Assert.Fail("Expected bootstrap payload artifact construction to reject missing payloadSha256.");
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException inner)
        {
            StringAssert.Contains(inner.Message, "missing payloadSha256", StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    public async Task StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-update-bootstrap-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        string payloadSourcePath = Path.Combine(tempRoot, "source-payload.zip");
        string installerPath = Path.Combine(tempRoot, "chummer-avalonia-win-x64-installer.exe");
        File.WriteAllText(installerPath, "installer");
        using (ZipArchive archive = ZipFile.Open(payloadSourcePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry launcher = archive.CreateEntry("Chummer.Avalonia.exe");
            using StreamWriter writer = new(launcher.Open());
            writer.Write("launcher");
        }

        byte[] payloadBytes = File.ReadAllBytes(payloadSourcePath);
        string payloadSha256 = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        DesktopUpdateArtifact installerArtifact = new(
            ArtifactId: "avalonia-win-x64-installer",
            HeadId: "avalonia",
            Platform: "windows",
            Arch: "x64",
            Kind: "installer",
            FileName: Path.GetFileName(installerPath),
            DownloadUrl: "https://chummer.run/downloads/files/chummer-avalonia-win-x64-installer.exe",
            UpdateFeedUrl: null,
            Sha256: "2f4ad755491b86e3a4ae0fb3251b0c863552ec4f0ae29049cedb7973bc372a4f",
            SizeBytes: 51856809,
            InstallerMode: "bootstrap",
            PayloadFileName: "chummer-avalonia-win-x64-payload.zip",
            PayloadDownloadUrl: payloadSourcePath.Replace("\\", "/"),
            PayloadSha256: payloadSha256,
            PayloadSizeBytes: payloadBytes.LongLength);
        DesktopUpdateChannelManifest manifest = new(
            ChannelId: "stable",
            Version: "run-20260624-090000",
            Status: "published",
            PublishedAt: DateTimeOffset.UtcNow,
            Artifacts: [installerArtifact],
            DesktopSurfaceRefs: [],
            RolloutState: null,
            RolloutReason: null,
            SupportabilityState: null,
            SupportabilitySummary: null,
            KnownIssueSummary: null,
            FixAvailabilitySummary: null,
            ProofStatus: null,
            ProofGeneratedAt: null,
            SourceUri: new Uri(Path.Combine(tempRoot, "RELEASE_CHANNEL.generated.json")));

        try
        {
            await InvokePrivateStaticTask(
                "StageInstallerBootstrapPayloadIfNeededAsync",
                manifest.SourceUri,
                manifest,
                installerArtifact,
                installerPath,
                null,
                CancellationToken.None).ConfigureAwait(false);

            string stagedPayloadPath = Path.Combine(tempRoot, installerArtifact.PayloadFileName!);
            string stagedSidecarPath = stagedPayloadPath + ".json";
            Assert.IsTrue(File.Exists(stagedPayloadPath));
            Assert.IsTrue(File.Exists(stagedSidecarPath));
            CollectionAssert.AreEqual(payloadBytes, File.ReadAllBytes(stagedPayloadPath));

            using JsonDocument sidecar = JsonDocument.Parse(File.ReadAllText(stagedSidecarPath));
            Assert.AreEqual("chummer6-ui.windows_bootstrap_payload", GetStringProperty(sidecar.RootElement, "contractName"));
            Assert.AreEqual(installerArtifact.PayloadFileName, GetStringProperty(sidecar.RootElement, "fileName"));
            Assert.AreEqual(installerArtifact.FileName, GetStringProperty(sidecar.RootElement, "installerFileName"));
            Assert.AreEqual(installerArtifact.PayloadDownloadUrl, GetStringProperty(sidecar.RootElement, "downloadUrl"));
            Assert.AreEqual(payloadSha256, GetStringProperty(sidecar.RootElement, "sha256"));
            Assert.AreEqual(manifest.Version, GetStringProperty(sidecar.RootElement, "releaseVersion"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Resolve_manifest_and_artifact_uris_support_directory_and_download_routes()
    {
        string manifestDirectory = Path.Combine(Path.GetTempPath(), $"desktop-update-manifest-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(manifestDirectory);
        try
        {
            Uri manifestUri = InvokePrivateStatic<Uri>("ResolveManifestUri", manifestDirectory);
            DesktopUpdateArtifact artifact = new(
                "archive",
                "avalonia",
                "linux",
                "x64",
                "archive",
                "avalonia.zip",
                "/downloads/promoted/avalonia.zip",
                null,
                null,
                null);

            Uri artifactUri = InvokePrivateStatic<Uri>("ResolveArtifactUri", manifestUri, artifact);

            Assert.IsTrue(manifestUri.LocalPath.EndsWith(Path.Combine("RELEASE_CHANNEL.generated.json"), StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(artifactUri.LocalPath.EndsWith(Path.Combine("promoted", "avalonia.zip"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Resolve_manifest_uri_rejects_non_loopback_http_manifest()
    {
        System.Reflection.TargetInvocationException ex;
        try
        {
            _ = InvokePrivateStatic<Uri>("ResolveManifestUri", "http://updates.example.invalid/channel");
            Assert.Fail("Expected non-loopback HTTP manifest locations to be rejected.");
            return;
        }
        catch (System.Reflection.TargetInvocationException caught)
        {
            ex = caught;
        }

        StringAssert.Contains(ex.InnerException?.Message ?? ex.Message, "must use HTTPS or loopback HTTP", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_artifact_uri_keeps_root_relative_web_downloads_on_manifest_host()
    {
        Uri manifestUri = new("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json");
        DesktopUpdateArtifact artifact = new(
            "installer",
            "avalonia",
            "linux",
            "x64",
            "installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "/downloads/files/chummer-avalonia-linux-x64-installer.deb",
            null,
            null,
            null);

        Uri artifactUri = InvokePrivateStatic<Uri>("ResolveArtifactUri", manifestUri, artifact);

        Assert.AreEqual("https", artifactUri.Scheme);
        Assert.AreEqual("chummer.run", artifactUri.Host);
        Assert.AreEqual("/downloads/files/chummer-avalonia-linux-x64-installer.deb", artifactUri.AbsolutePath);
    }

    [TestMethod]
    public void Resolve_artifact_uri_rejects_cross_origin_remote_artifact()
    {
        Uri manifestUri = new("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json");
        DesktopUpdateArtifact artifact = new(
            "installer",
            "avalonia",
            "linux",
            "x64",
            "installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "https://evil.example.invalid/chummer-avalonia-linux-x64-installer.deb",
            null,
            new string('a', 64),
            42);

        System.Reflection.TargetInvocationException ex;
        try
        {
            _ = InvokePrivateStatic<Uri>("ResolveArtifactUri", manifestUri, artifact);
            Assert.Fail("Expected cross-origin remote artifacts to be rejected.");
            return;
        }
        catch (System.Reflection.TargetInvocationException caught)
        {
            ex = caught;
        }

        StringAssert.Contains(ex.InnerException?.Message ?? ex.Message, "same origin as manifest", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_artifact_uri_rejects_same_host_different_port_remote_artifact()
    {
        Uri manifestUri = new("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json");
        DesktopUpdateArtifact artifact = new(
            "installer",
            "avalonia",
            "linux",
            "x64",
            "installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "https://chummer.run:444/downloads/files/chummer-avalonia-linux-x64-installer.deb",
            null,
            new string('a', 64),
            42);

        System.Reflection.TargetInvocationException ex;
        try
        {
            _ = InvokePrivateStatic<Uri>("ResolveArtifactUri", manifestUri, artifact);
            Assert.Fail("Expected same-host, different-port remote artifacts to be rejected.");
            return;
        }
        catch (System.Reflection.TargetInvocationException caught)
        {
            ex = caught;
        }

        StringAssert.Contains(ex.InnerException?.Message ?? ex.Message, "same origin as manifest", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Resolve_artifact_uri_rejects_protocol_relative_artifact()
    {
        Uri manifestUri = new("https://chummer.run/downloads/RELEASE_CHANNEL.generated.json");
        DesktopUpdateArtifact artifact = new(
            "installer",
            "avalonia",
            "linux",
            "x64",
            "installer",
            "chummer-avalonia-linux-x64-installer.deb",
            "//evil.example.invalid/chummer-avalonia-linux-x64-installer.deb",
            null,
            new string('a', 64),
            42);

        System.Reflection.TargetInvocationException ex;
        try
        {
            _ = InvokePrivateStatic<Uri>("ResolveArtifactUri", manifestUri, artifact);
            Assert.Fail("Expected protocol-relative artifacts to be rejected.");
            return;
        }
        catch (System.Reflection.TargetInvocationException caught)
        {
            ex = caught;
        }

        StringAssert.Contains(ex.InnerException?.Message ?? ex.Message, "protocol-relative URL", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Normalize_payload_and_helper_checks_follow_packaged_runtime_rules()
    {
        string payloadRoot = Path.Combine(Path.GetTempPath(), $"desktop-update-payload-{Guid.NewGuid():N}");
        string nestedRoot = Path.Combine(payloadRoot, "bundle");
        string helperDirectory = Path.Combine(Path.GetTempPath(), $"desktop-update-helper-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(nestedRoot);
        Directory.CreateDirectory(helperDirectory);
        File.WriteAllText(Path.Combine(nestedRoot, "Chummer.Desktop"), "// app");
        string helperPath = Path.Combine(helperDirectory, "Chummer.Desktop");
        File.WriteAllText(helperPath, "// helper");
        try
        {
            string normalized = InvokePrivateStatic<string>("NormalizePayloadRoot", payloadRoot, "Chummer.Desktop");
            bool canRunHelper = InvokePrivateStatic<bool>("CanRunCopiedHelper", helperPath, helperDirectory);
            bool dotnetBlocked = InvokePrivateStatic<bool>("CanRunCopiedHelper", "/usr/bin/dotnet", helperDirectory);

            Assert.AreEqual(nestedRoot, normalized);
            Assert.IsTrue(canRunHelper);
            Assert.IsFalse(dotnetBlocked);
        }
        finally
        {
            if (Directory.Exists(payloadRoot))
            {
                Directory.Delete(payloadRoot, recursive: true);
            }
            if (Directory.Exists(helperDirectory))
            {
                Directory.Delete(helperDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Linux_deb_installer_command_uses_dpkg_direct_for_root_and_pkexec_for_desktop_users()
    {
        const string installerPath = "/tmp/chummer-avalonia-linux-x64-installer.deb";

        object? rootCommand = InvokePrivateStatic<object?>(
            "ResolveLinuxDebInstallerCommand",
            installerPath,
            true,
            true,
            true,
            true);
        object? desktopCommand = InvokePrivateStatic<object?>(
            "ResolveLinuxDebInstallerCommand",
            installerPath,
            false,
            true,
            true,
            true);
        object? sudoFallbackCommand = InvokePrivateStatic<object?>(
            "ResolveLinuxDebInstallerCommand",
            installerPath,
            false,
            true,
            false,
            true);
        object? missingPrivilegeCommand = InvokePrivateStatic<object?>(
            "ResolveLinuxDebInstallerCommand",
            installerPath,
            false,
            true,
            false,
            false);
        object? missingDpkgCommand = InvokePrivateStatic<object?>(
            "ResolveLinuxDebInstallerCommand",
            installerPath,
            true,
            false,
            true,
            true);

        Assert.IsNotNull(rootCommand);
        Assert.AreEqual("dpkg", GetPrivateProperty<string>(rootCommand, "FileName"));
        CollectionAssert.AreEqual(
            new[] { "-i", installerPath },
            GetPrivateProperty<IReadOnlyList<string>>(rootCommand, "Arguments").ToArray());

        Assert.IsNotNull(desktopCommand);
        Assert.AreEqual("pkexec", GetPrivateProperty<string>(desktopCommand, "FileName"));
        CollectionAssert.AreEqual(
            new[] { "dpkg", "-i", installerPath },
            GetPrivateProperty<IReadOnlyList<string>>(desktopCommand, "Arguments").ToArray());

        Assert.IsNotNull(sudoFallbackCommand);
        Assert.AreEqual("sudo", GetPrivateProperty<string>(sudoFallbackCommand, "FileName"));
        CollectionAssert.AreEqual(
            new[] { "-n", "dpkg", "-i", installerPath },
            GetPrivateProperty<IReadOnlyList<string>>(sudoFallbackCommand, "Arguments").ToArray());

        Assert.IsNull(missingPrivilegeCommand);
        Assert.IsNull(missingDpkgCommand);
    }

    [TestMethod]
    public void Linux_deb_installer_path_does_not_use_desktop_mime_handoff()
    {
        string repoRoot = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
        string runtime = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopUpdateRuntime.cs"));

        Assert.Contains("InstallLinuxDebianPackage(installerPath)", runtime, StringComparison.Ordinal);
        Assert.Contains("ResolveLinuxDebInstallerCommand", runtime, StringComparison.Ordinal);
        Assert.Contains("dpkg", runtime, StringComparison.Ordinal);
        Assert.Contains("pkexec", runtime, StringComparison.Ordinal);
        Assert.Contains("sudo", runtime, StringComparison.Ordinal);
        Assert.Contains("\"-n\"", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("gio", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xdg-open", runtime, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("application/vnd.debian.binary-package", runtime, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Linux_deb_installer_unavailable_message_keeps_manual_recovery_command()
    {
        const string installerPath = "/tmp/chummer update/runner's build.deb";

        string message = InvokePrivateStatic<string>("BuildLinuxDebInstallerUnavailableMessage", installerPath);

        StringAssert.Contains(message, "Could not apply Linux .deb update automatically.");
        StringAssert.Contains(message, "The downloaded package remains at '/tmp/chummer update/runner's build.deb'.");
        StringAssert.Contains(message, "sudo dpkg -i '/tmp/chummer update/runner'\"'\"'s build.deb'");
    }

    [TestMethod]
    public async Task TryHandleSpecialModeAsync_installer_launch_failure_records_structured_failure_reason()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-update-installer-failure-{Guid.NewGuid():N}");
        string stageRoot = Path.Combine(tempRoot, "stage");
        string statePath = Path.Combine(tempRoot, "state.json");
        string requestPath = Path.Combine(stageRoot, "installer-request.json");
        string missingInstallerPath = Path.Combine(stageRoot, "missing-installer.deb");
        Directory.CreateDirectory(stageRoot);
        File.WriteAllText(
            statePath,
            """
            {
              "HeadId": "avalonia",
              "Platform": "linux",
              "Arch": "x64",
              "InstalledVersion": "run-20260618-024810",
              "ChannelId": "stable",
              "LastCheckedAt": "2026-06-18T06:00:00Z",
              "LastManifestVersion": "run-20260618-051119",
              "LastManifestPublishedAt": "2026-06-18T06:15:00Z",
              "LastError": null,
              "PendingUpdateVersion": "run-20260618-051119",
              "PendingUpdateChannelId": "stable",
              "PendingUpdatePreparedAtUtc": "2026-06-18T06:16:00Z"
            }
            """);
        File.WriteAllText(
            requestPath,
            $$"""
            {
              "ParentProcessId": 0,
              "StageRoot": "{{stageRoot.Replace("\\", "\\\\")}}",
              "InstallerPath": "{{missingInstallerPath.Replace("\\", "\\\\")}}",
              "StateFilePath": "{{statePath.Replace("\\", "\\\\")}}",
              "Version": "run-20260618-051119",
              "ChannelId": "stable",
              "HeadId": "avalonia",
              "RelaunchArgs": []
            }
            """);

        try
        {
            int? exitCode = await DesktopUpdateRuntime.TryHandleSpecialModeAsync(
                ["--desktop-update-launch-installer", requestPath],
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual(1, exitCode);

            using JsonDocument state = JsonDocument.Parse(File.ReadAllText(statePath));
            Assert.AreEqual("installer_launch_failed", GetStringProperty(state.RootElement, "lastFailureReason"));
            StringAssert.Contains(GetStringProperty(state.RootElement, "lastError") ?? string.Empty, "Installer payload was not found");
            Assert.IsNotNull(GetDateTimeProperty(state.RootElement, "lastFailureAtUtc"));
            Assert.AreEqual("run-20260618-051119", GetStringProperty(state.RootElement, "pendingUpdateVersion"));
            Assert.AreEqual("stable", GetStringProperty(state.RootElement, "pendingUpdateChannelId"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Update_configuration_load_honors_legacy_manifest_and_boolean_aliases()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = null,
            ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"] = "/tmp/promoted",
            [UpdateModeEnvironmentVariable] = null,
            [UpdateEnabledEnvironmentVariable] = "yes",
            [UpdateAutoApplyEnvironmentVariable] = "off"
        });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(true, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
        Assert.AreEqual("notify", configurationType.GetProperty("Mode")!.GetValue(configuration));
    }

    [TestMethod]
    public void Update_configuration_load_honors_explicit_notify_mode()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/promoted",
            [UpdateModeEnvironmentVariable] = "notify",
            [UpdateEnabledEnvironmentVariable] = "false",
            [UpdateAutoApplyEnvironmentVariable] = "true"
        });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(true, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
        Assert.AreEqual("notify", configurationType.GetProperty("Mode")!.GetValue(configuration));
    }

    [TestMethod]
    public void Update_configuration_load_honors_explicit_off_mode()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/promoted",
            [UpdateModeEnvironmentVariable] = "off",
            [UpdateEnabledEnvironmentVariable] = "true",
            [UpdateAutoApplyEnvironmentVariable] = "true"
        });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(false, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
        Assert.AreEqual("off", configurationType.GetProperty("Mode")!.GetValue(configuration));
    }

    [TestMethod]
    public void ResolveLinuxInstalledLauncherPath_prefers_path_command_for_linux_relaunch()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), $"chummer-linux-launcher-{Guid.NewGuid():N}");
        string binRoot = Path.Combine(tempRoot, "bin");
        Directory.CreateDirectory(binRoot);
        string launcherPath = Path.Combine(binRoot, "chummer6-avalonia");
        File.WriteAllText(launcherPath, "#!/usr/bin/env bash\nexit 0\n");
        File.SetUnixFileMode(
            launcherPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        string? priorPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", $"{binRoot}{Path.PathSeparator}{priorPath}");
            string? resolved = InvokePrivateStatic<string?>("ResolveLinuxInstalledLauncherPath", "avalonia");
            Assert.AreEqual(launcherPath, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", priorPath);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TryLaunchLinuxInstalledApplication_skips_noisy_stderr_when_launcher_is_unavailable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        StringWriter errorWriter = new();
        TextWriter priorError = Console.Error;
        string? priorPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Console.SetError(errorWriter);
            Environment.SetEnvironmentVariable("PATH", "/nonexistent");
            InvokePrivateStatic<object?>("TryLaunchLinuxInstalledApplication", "avalonia", Array.Empty<string>());
            Assert.AreEqual(string.Empty, errorWriter.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", priorPath);
            Console.SetError(priorError);
            errorWriter.Dispose();
        }
    }

    [TestMethod]
    public void Update_configuration_load_uses_persisted_preference_mode_when_no_env_override_exists()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/promoted",
            [UpdateModeEnvironmentVariable] = null,
            [UpdateEnabledEnvironmentVariable] = null,
            [UpdateAutoApplyEnvironmentVariable] = null,
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });
        DesktopPreferenceRuntime.SaveState(
            "avalonia",
            Chummer.Presentation.Overview.DesktopPreferenceState.Default with { UpdateMode = "notify" });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(true, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
        Assert.AreEqual("notify", configurationType.GetProperty("Mode")!.GetValue(configuration));
    }

    [TestMethod]
    public void Update_configuration_load_prefers_explicit_env_mode_over_persisted_preference_mode()
    {
        using TestStateRootScope stateRootScope = new();
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = "/tmp/promoted",
            [UpdateModeEnvironmentVariable] = "notify",
            [UpdateEnabledEnvironmentVariable] = null,
            [UpdateAutoApplyEnvironmentVariable] = null,
            [StateRootEnvironmentVariable] = stateRootScope.Root
        });
        DesktopPreferenceRuntime.SaveState(
            "avalonia",
            Chummer.Presentation.Overview.DesktopPreferenceState.Default with { UpdateMode = "full" });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(true, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
        Assert.AreEqual("notify", configurationType.GetProperty("Mode")!.GetValue(configuration));
    }

    [TestMethod]
    public void Default_public_manifest_location_uses_public_portal_base_override()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            ["CHUMMER_PUBLIC_BASE_URL"] = "https://updates.example.test/chummer/",
            ["CHUMMER_PUBLIC_WEB_BASE_URL"] = "https://public-web.example.test/chummer/",
            ["CHUMMER_WEB_BASE_URL"] = "https://web.example.test/chummer/"
        });

        string manifestLocation = InvokePrivateStatic<string>("ResolveDefaultPublicManifestLocation");

        Assert.AreEqual(
            "https://updates.example.test/downloads/RELEASE_CHANNEL.generated.json",
            manifestLocation);
    }

    [TestMethod]
    public void State_store_load_returns_null_for_invalid_json_and_cleanup_helpers_remove_stale_artifacts()
    {
        string statePath = Path.Combine(Path.GetTempPath(), $"desktop-update-state-invalid-{Guid.NewGuid():N}.json");
        string tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-update-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        string oldDirectory = Path.Combine(tempRoot, "old-dir");
        string oldFile = Path.Combine(tempRoot, "old-file.txt");
        Directory.CreateDirectory(oldDirectory);
        File.WriteAllText(oldFile, "stale");
        File.WriteAllText(statePath, "{ this is not valid json");
        Directory.SetCreationTimeUtc(oldDirectory, DateTime.UtcNow.AddDays(-3));
        File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddDays(-3));

        try
        {
            object? loaded = InvokeNestedStatic("DesktopUpdateStateStore", "Load", statePath);
            InvokePrivateStatic<object?>("CleanupExpiredTempArtifacts", tempRoot);

            Assert.IsNull(loaded);
            Assert.IsFalse(Directory.Exists(oldDirectory));
            Assert.IsFalse(File.Exists(oldFile));
        }
        finally
        {
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string? GetStringProperty(JsonElement root, string propertyName)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
            }
        }

        return null;
    }

    private static DateTimeOffset? GetDateTimeProperty(JsonElement root, string propertyName)
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetDateTimeOffset();
            }
        }

        return null;
    }

    private static T InvokePrivateStatic<T>(string methodName, params object?[] args)
    {
        System.Reflection.MethodInfo? method = typeof(DesktopUpdateRuntime).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, $"Expected DesktopUpdateRuntime.{methodName} to remain available for coverage.");
        return (T)method.Invoke(null, args)!;
    }

    private static async Task InvokePrivateStaticTask(string methodName, params object?[] args)
    {
        System.Reflection.MethodInfo? method = typeof(DesktopUpdateRuntime).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, $"Expected DesktopUpdateRuntime.{methodName} to remain available for coverage.");
        object? result = method.Invoke(null, args);
        if (result is not Task task)
        {
            Assert.Fail($"Expected DesktopUpdateRuntime.{methodName} to return Task for coverage.");
            return;
        }

        await task.ConfigureAwait(false);
    }

    private static T GetPrivateProperty<T>(object target, string propertyName)
    {
        System.Reflection.PropertyInfo? property = target.GetType().GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(property, $"Expected {target.GetType().Name}.{propertyName} to remain available for coverage.");
        return (T)property.GetValue(target)!;
    }

    private static object InvokeNestedStatic(string nestedTypeName, string methodName, params object?[] args)
    {
        Type? nestedType = typeof(DesktopUpdateRuntime).GetNestedType(
            nestedTypeName,
            System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(nestedType, $"Expected nested type {nestedTypeName} to remain available for coverage.");
        System.Reflection.MethodInfo? method = nestedType.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, $"Expected {nestedTypeName}.{methodName} to remain available for coverage.");
        return method.Invoke(null, args)!;
    }

    private static string ResolveCurrentRuntimeReleaseProperty(string propertyName)
    {
        object release = InvokeNestedStatic("DesktopReleaseMetadata", "Load", "avalonia");
        object? value = release.GetType().GetProperty(propertyName)!.GetValue(release);
        return Convert.ToString(value) ?? string.Empty;
    }

    private sealed class TestEnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _priorValues = [];

        public TestEnvironmentScope(IReadOnlyDictionary<string, string?> values)
        {
            foreach (KeyValuePair<string, string?> value in values)
            {
                _priorValues[value.Key] = Environment.GetEnvironmentVariable(value.Key);
                Environment.SetEnvironmentVariable(value.Key, value.Value);
            }
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, string?> prior in _priorValues)
            {
                Environment.SetEnvironmentVariable(prior.Key, prior.Value);
            }
        }
    }

    private sealed class TestStateRootScope : IDisposable
    {
        private readonly string? _priorStateRoot;
        public string Root { get; }

        public TestStateRootScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"chummer-update-runtime-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            _priorStateRoot = Environment.GetEnvironmentVariable(StateRootEnvironmentVariable);
        }

        public string StatePathForHead(string headId)
        {
            DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
            return Path.Combine(
                Root,
                "Chummer6",
                "desktop-update",
                headId,
                identity.Platform,
                identity.Arch,
                "state.json");
        }

        public string TempRootForHead(string headId)
        {
            DesktopUpdatePlatformIdentity identity = DesktopUpdatePlatformIdentity.Current();
            return Path.Combine(
                Root,
                "Chummer6",
                "desktop-update",
                headId,
                identity.Platform,
                identity.Arch,
                "tmp");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(StateRootEnvironmentVariable, _priorStateRoot);
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class TestProcessPathOverrideScope : IDisposable
    {
        private readonly string? _priorProcessPathOverride;
        private readonly string? _helperPath;

        private TestProcessPathOverrideScope(string? helperPath)
        {
            _priorProcessPathOverride = Environment.GetEnvironmentVariable(UpdateProcessPathOverrideEnvironmentVariable);
            _helperPath = helperPath;
            Environment.SetEnvironmentVariable(UpdateProcessPathOverrideEnvironmentVariable, helperPath);
        }

        public static TestProcessPathOverrideScope CreatePackagedLike()
        {
            string helperPath = Path.Combine(AppContext.BaseDirectory, $"desktop-update-helper-{Guid.NewGuid():N}");
            File.WriteAllText(helperPath, "// packaged-like helper stub");
            return new TestProcessPathOverrideScope(helperPath);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(UpdateProcessPathOverrideEnvironmentVariable, _priorProcessPathOverride);
            if (!string.IsNullOrWhiteSpace(_helperPath) && File.Exists(_helperPath))
            {
                File.Delete(_helperPath);
            }
        }
    }
}
