#nullable enable annotations

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopCrashRuntimeTests
{
    [TestMethod]
    public void BuildEnvelope_keeps_claim_identity_when_install_state_matches_crash_snapshot()
    {
        using TestStateRootScope scope = new();
        DesktopCrashReport report = new(
            CrashId: "crash-1",
            HeadId: "avalonia",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            IsTerminating: true,
            ApplicationVersion: "1.0.0",
            RuntimeVersion: ".NET 10",
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessName: "chummer",
            BaseDirectoryLabel: "<base>",
            CurrentDirectoryLabel: "<cwd>",
            ExceptionType: "System.Exception",
            ExceptionMessage: "boom",
            ExceptionDetail: "System.Exception: boom");
        DesktopCrashClaimSnapshot snapshot = new("install-1", "user-1", "subject-1", "grant-1");
        scope.WriteInstallState(CreateState("install-1", "user-1", "subject-1", "grant-1", "token-1"));

        object envelope = BuildEnvelope(report, "summary", snapshot);

        Assert.AreEqual("install-1", GetEnvelopeProperty(envelope, "InstallationId"));
        Assert.AreEqual("user-1", GetEnvelopeProperty(envelope, "UserId"));
        Assert.AreEqual("subject-1", GetEnvelopeProperty(envelope, "SubjectId"));
        Assert.AreEqual("token-1", GetEnvelopeProperty(envelope, "InstallationGrantToken"));
    }

    [TestMethod]
    public void BuildEnvelope_drops_claim_identity_when_install_state_changes_after_crash()
    {
        using TestStateRootScope scope = new();
        DesktopCrashReport report = new(
            CrashId: "crash-2",
            HeadId: "avalonia",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            IsTerminating: true,
            ApplicationVersion: "1.0.0",
            RuntimeVersion: ".NET 10",
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessName: "chummer",
            BaseDirectoryLabel: "<base>",
            CurrentDirectoryLabel: "<cwd>",
            ExceptionType: "System.Exception",
            ExceptionMessage: "boom",
            ExceptionDetail: "System.Exception: boom");
        DesktopCrashClaimSnapshot snapshot = new("install-1", "user-1", "subject-1", "grant-1");
        scope.WriteInstallState(CreateState("install-1", "user-2", "subject-2", "grant-2", "token-2"));

        object envelope = BuildEnvelope(report, "summary", snapshot);

        Assert.IsNull(GetEnvelopeProperty(envelope, "InstallationId"));
        Assert.IsNull(GetEnvelopeProperty(envelope, "UserId"));
        Assert.IsNull(GetEnvelopeProperty(envelope, "SubjectId"));
        Assert.IsNull(GetEnvelopeProperty(envelope, "InstallationGrantToken"));
    }

    [TestMethod]
    public void CrashReport_serialization_does_not_persist_claim_identity()
    {
        DesktopCrashReport report = new(
            CrashId: "crash-3",
            HeadId: "avalonia",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            IsTerminating: true,
            ApplicationVersion: "1.0.0",
            RuntimeVersion: ".NET 10",
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessName: "chummer",
            BaseDirectoryLabel: "<base>",
            CurrentDirectoryLabel: "<cwd>",
            ExceptionType: "System.Exception",
            ExceptionMessage: "boom",
            ExceptionDetail: "System.Exception: boom");

        string json = JsonSerializer.Serialize(report);

        Assert.IsFalse(json.Contains("installationId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("claimedUserId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("claimedSubjectId", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("claimGrantId", StringComparison.OrdinalIgnoreCase));
    }

    private static object BuildEnvelope(DesktopCrashReport report, string summary, DesktopCrashClaimSnapshot? snapshot)
    {
        MethodInfo method = typeof(DesktopCrashRuntime).GetMethod("BuildEnvelope", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildEnvelope method was not found.");
        return method.Invoke(null, [report, summary, snapshot])
            ?? throw new InvalidOperationException("BuildEnvelope returned null.");
    }

    private static string? GetEnvelopeProperty(object envelope, string propertyName)
    {
        PropertyInfo property = envelope.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Envelope property '{propertyName}' was not found.");
        return property.GetValue(envelope) as string;
    }

    private static DesktopInstallLinkingState CreateState(
        string installationId,
        string userId,
        string subjectId,
        string grantId,
        string grantToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopInstallLinkingState(
            InstallationId: installationId,
            HeadId: "avalonia",
            ApplicationVersion: "1.0.0",
            ChannelId: "preview",
            Platform: ResolvePlatform(),
            Arch: ResolveArch(),
            Status: "claimed",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LaunchCount: 1,
            LastStartedAtUtc: now,
            ClaimedAtUtc: now,
            LastPromptDismissedAtUtc: null,
            PublicKey: "public-key",
            PrivateKey: "private-key",
            ClaimTicketId: "ticket-1",
            LastClaimCode: "CLAIM1",
            LastClaimMessage: "linked",
            LastClaimError: null,
            LastClaimAttemptUtc: now,
            GrantId: grantId,
            GrantToken: grantToken,
            GrantIssuedAtUtc: now,
            GrantExpiresAtUtc: now.AddDays(1),
            UserId: userId,
            SubjectId: subjectId);
    }

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

    private static string ResolveArch()
        => RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        };

    private sealed class TestStateRootScope : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string? _priorRoot;

        public TestStateRootScope()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), $"desktop-crash-runtime-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempRoot);
            _priorRoot = Environment.GetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT");
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _tempRoot);
        }

        public void WriteInstallState(DesktopInstallLinkingState state)
        {
            string path = Path.Combine(
                _tempRoot,
                "Chummer6",
                "install-linking",
                state.HeadId,
                state.Platform,
                state.Arch,
                "state.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(state));
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("CHUMMER_DESKTOP_STATE_ROOT", _priorRoot);
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
    }
}
