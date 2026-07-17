#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopReleaseMatrixRuntimeTests
{
    private const string ManifestEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_MANIFEST";
    private const string UpdateEnabledEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_ENABLED";
    private const string UpdateAutoApplyEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_AUTO_APPLY";
    private const string StateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string UpdateProcessPathOverrideEnvironmentVariable = "CHUMMER_DESKTOP_UPDATE_PROCESS_PATH_OVERRIDE";

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

            File.WriteAllText(helperPath, "#!/usr/bin/env bash\nexit 0\n");
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

            Assert.AreEqual(stagedInstallerPath, GetStringProperty(state.RootElement, "pendingInstallerPath"));
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

        public TestStateRootScope()
        {
            Root = Path.Combine(Path.GetTempPath(), $"chummer-update-runtime-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            _priorStateRoot = Environment.GetEnvironmentVariable(StateRootEnvironmentVariable);
        }

        public string Root { get; }

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
}
