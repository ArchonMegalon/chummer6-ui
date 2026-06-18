#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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
    private const string UpdateEnabledEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_ENABLED";
    private const string UpdateAutoApplyEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY";
    private const string StateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string UpdateProcessPathOverrideEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_PROCESS_PATH_OVERRIDE";

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
                  "downloadUrl": "{{missingPayloadPath.Replace("\\", "/")}}"
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
        StringAssert.Contains(status.RecommendedAction, "Configure the desktop update manifest", StringComparison.Ordinal);
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
        StringAssert.Contains(status.RecommendedAction, "latest local release proof failed", StringComparison.OrdinalIgnoreCase);
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
    public void Update_configuration_load_honors_legacy_manifest_and_boolean_aliases()
    {
        using TestEnvironmentScope envScope = new(new Dictionary<string, string?>()
        {
            [ManifestEnvironmentVariable] = null,
            ["CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"] = "/tmp/promoted",
            [UpdateEnabledEnvironmentVariable] = "yes",
            [UpdateAutoApplyEnvironmentVariable] = "off"
        });

        object configuration = InvokeNestedStatic("DesktopUpdateConfiguration", "Load");
        Type configurationType = configuration.GetType();

        Assert.AreEqual(true, configurationType.GetProperty("Enabled")!.GetValue(configuration));
        Assert.AreEqual(false, configurationType.GetProperty("AutoApply")!.GetValue(configuration));
        Assert.AreEqual("/tmp/promoted", configurationType.GetProperty("ManifestLocation")!.GetValue(configuration));
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
