using Chummer.Desktop.Runtime;
using System.Text.Json;

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
