#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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

    [TestMethod]
    public void BuildCrashDiagnosticsReceiptLines_includes_grounded_before_after_receipts()
    {
        DesktopCrashReport report = CreateReport("crash-4");

        IReadOnlyList<string> lines = DesktopCrashRuntime.BuildCrashDiagnosticsReceiptLines(report);

        Assert.IsTrue(lines.Count > 0);
        Assert.IsTrue(lines.Any(line => line.Contains("crash diagnostics receipt", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(lines.Any(line => line.Contains("crash support handoff receipt", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual("local files, support posture, and install state remain unchanged", lines[^1]);
    }

    [TestMethod]
    public void BuildRecoverySummary_lists_artifacts_and_details()
    {
        DesktopCrashReport report = CreateReport("crash-5");
        string reportDirectory = Path.Combine(Path.GetTempPath(), $"desktop-crash-summary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);
        try
        {
            string summary = DesktopCrashRuntime.BuildRecoverySummary(report, reportDirectory);

            StringAssert.Contains(summary, "Report id: crash-5", StringComparison.Ordinal);
            StringAssert.Contains(summary, "- Report:", StringComparison.Ordinal);
            StringAssert.Contains(summary, "- Summary:", StringComparison.Ordinal);
            StringAssert.Contains(summary, "- Bundle:", StringComparison.Ordinal);
            StringAssert.Contains(summary, "Details:", StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(reportDirectory))
            {
                Directory.Delete(reportDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ResolveApiBaseAddress_falls_back_to_public_web_host_when_api_host_is_unset()
    {
        string? previousApiBase = Environment.GetEnvironmentVariable("CHUMMER_API_BASE_URL");
        string? previousWebBase = Environment.GetEnvironmentVariable("CHUMMER_WEB_BASE_URL");
        string? previousPublicBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL");
        string? previousPublicWebBase = Environment.GetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", null);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", null);

            MethodInfo method = typeof(DesktopCrashRuntime).GetMethod(
                "ResolveApiBaseAddress",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ResolveApiBaseAddress was not found.");

            Uri uri = (Uri)(method.Invoke(null, null)
                ?? throw new InvalidOperationException("ResolveApiBaseAddress returned null."));

            Assert.AreEqual("https://chummer.run/", uri.AbsoluteUri);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_API_BASE_URL", previousApiBase);
            Environment.SetEnvironmentVariable("CHUMMER_WEB_BASE_URL", previousWebBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_BASE_URL", previousPublicBase);
            Environment.SetEnvironmentVariable("CHUMMER_PUBLIC_WEB_BASE_URL", previousPublicWebBase);
        }
    }

    [TestMethod]
    public void TryLoadPendingCrashReport_reads_marker_report_and_summary_and_acknowledges_match()
    {
        using TestStateRootScope scope = new();
        DesktopCrashReport report = CreateReport("crash-pending-1");
        string reportDirectory = Path.Combine(scope.CrashRoot, "20260520-090000-crash");
        Directory.CreateDirectory(reportDirectory);
        string reportPath = Path.Combine(reportDirectory, "report.json");
        string summaryPath = Path.Combine(reportDirectory, "summary.txt");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }),
            Encoding.UTF8);
        File.WriteAllText(summaryPath, "desktop crash summary", Encoding.UTF8);
        scope.WritePendingMarker(
            """
            {
              "crashId": "crash-pending-1",
              "reportDirectory": "__REPORT_DIRECTORY__",
              "submissionAttempts": 2,
              "lastSubmissionError": "network timeout"
            }
            """.Replace("__REPORT_DIRECTORY__", reportDirectory.Replace("\\", "\\\\"), StringComparison.Ordinal));

        DesktopPendingCrashReport? pending = DesktopCrashRuntime.TryLoadPendingCrashReport();

        Assert.IsNotNull(pending);
        Assert.AreEqual("crash-pending-1", pending.Report.CrashId);
        Assert.AreEqual(reportDirectory, pending.ReportDirectory);
        Assert.AreEqual(summaryPath, pending.SummaryPath);
        Assert.AreEqual("desktop crash summary", pending.SummaryText);
        Assert.AreEqual(2, pending.SubmissionAttempts);
        Assert.AreEqual("network timeout", pending.LastSubmissionError);
        Assert.IsTrue(DesktopCrashRuntime.TryAcknowledgePendingCrashReport("crash-pending-1"));
        Assert.IsNull(DesktopCrashRuntime.TryLoadPendingCrashReport());
    }

    [TestMethod]
    public void TryLoadPendingCrashReport_clears_marker_when_report_payload_is_invalid()
    {
        using TestStateRootScope scope = new();
        string reportDirectory = Path.Combine(scope.CrashRoot, "20260520-091500-invalid");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, "report.json"), "{ invalid", Encoding.UTF8);
        scope.WritePendingMarker(
            """
            {
              "crashId": "crash-invalid",
              "reportDirectory": "__REPORT_DIRECTORY__"
            }
            """.Replace("__REPORT_DIRECTORY__", reportDirectory.Replace("\\", "\\\\"), StringComparison.Ordinal));

        DesktopPendingCrashReport? pending = DesktopCrashRuntime.TryLoadPendingCrashReport();

        Assert.IsNull(pending);
        Assert.IsFalse(File.Exists(scope.PendingMarkerPath));
    }

    [TestMethod]
    public void IgnorableBackgroundException_treats_missing_linux_appmenu_registrar_as_non_crash_noise()
    {
        Type monitorType = typeof(DesktopCrashRuntime).Assembly.GetType("Chummer.Desktop.Runtime.DesktopCrashMonitor", throwOnError: true)
            ?? throw new InvalidOperationException("DesktopCrashMonitor type was not found.");
        MethodInfo method = monitorType.GetMethod("IsIgnorableBackgroundException", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsIgnorableBackgroundException method was not found.");

        Exception exception = new AggregateException(
            new InvalidOperationException(
                "org.freedesktop.DBus.Error.ServiceUnknown: The name com.canonical.AppMenu.Registrar was not provided by any .service files"));
        typeof(InvalidOperationException).GetField("_className", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
            exception.InnerException,
            "Tmds.DBus.Protocol.DBusException");

        bool ignored = (bool)(method.Invoke(null, [exception])
            ?? throw new InvalidOperationException("IsIgnorableBackgroundException returned null."));

        Assert.IsTrue(ignored);
    }

    [TestMethod]
    public void TryOpenPathInShell_treats_fast_linux_browser_launcher_failure_as_failed_handoff()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Linux browser launcher failure handling is only exercised on Linux.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"desktop-shell-open-{Guid.NewGuid():N}");
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            WriteExecutable(Path.Combine(tempDirectory, "xdg-open"), "#!/usr/bin/env bash\necho 'Operation not supported' >&2\nexit 1\n");
            WriteExecutable(Path.Combine(tempDirectory, "gio"), "#!/usr/bin/env bash\nif [[ \"$1\" == \"open\" ]]; then echo 'Operation not supported' >&2; exit 1; fi\necho 'Operation not supported' >&2\nexit 1\n");
            Environment.SetEnvironmentVariable("PATH", $"{tempDirectory}:{previousPath}");

            bool opened = DesktopCrashRuntime.TryOpenPathInShell("/tmp/test-path", out string? failureReason);

            Assert.IsFalse(opened, "A launcher that exits immediately with a non-zero code must not be treated as a successful browser handoff.");
            StringAssert.Contains(failureReason ?? string.Empty, "Operation not supported", StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TryOpenPathInShell_linux_url_uses_python_webbrowser_fallback_when_default_launchers_fail()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("URL launcher fallback behavior is only exercised on Linux.");
        }

        string tempDirectory = Path.Combine(Path.GetTempPath(), $"desktop-shell-open-fallback-{Guid.NewGuid():N}");
        string? previousPath = Environment.GetEnvironmentVariable("PATH");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            WriteExecutable(Path.Combine(tempDirectory, "xdg-open"), "#!/usr/bin/env bash\necho 'Operation not supported' >&2\nexit 1\n");
            WriteExecutable(Path.Combine(tempDirectory, "gio"), "#!/usr/bin/env bash\nif [[ \"$1\" == \"open\" ]]; then echo 'Operation not supported' >&2; exit 1; fi\necho 'Operation not supported' >&2\nexit 1\n");
            WriteExecutable(
                Path.Combine(tempDirectory, "python3"),
                "#!/usr/bin/env bash\n# Python compatibility shim; return success for webbrowser fallback path.\nexit 0\n");
            Environment.SetEnvironmentVariable("PATH", $"{tempDirectory}:{previousPath}");

            bool opened = DesktopCrashRuntime.TryOpenPathInShell("https://chummer.run/login?next=test", out string? failureReason);

            Assert.IsTrue(opened, "Python webbrowser fallback should be used when standard launcher commands fail.");
            Assert.IsTrue(string.IsNullOrWhiteSpace(failureReason), failureReason);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static object BuildEnvelope(DesktopCrashReport report, string summary, DesktopCrashClaimSnapshot? snapshot)
    {
        MethodInfo method = typeof(DesktopCrashRuntime).GetMethod("BuildEnvelope", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildEnvelope method was not found.");
        return method.Invoke(null, [report, summary, snapshot])
            ?? throw new InvalidOperationException("BuildEnvelope returned null.");
    }

    private static void WriteExecutable(string path, string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);
        }
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

    private static DesktopCrashReport CreateReport(string crashId)
        => new(
            CrashId: crashId,
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

        public string CrashRoot => Path.Combine(_tempRoot, "Chummer", "desktop-crashes");

        public string PendingMarkerPath => Path.Combine(CrashRoot, "pending.json");

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

        public void WritePendingMarker(string json)
        {
            Directory.CreateDirectory(CrashRoot);
            File.WriteAllText(PendingMarkerPath, json, Encoding.UTF8);
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
