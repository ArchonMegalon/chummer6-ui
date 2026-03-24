using System.Diagnostics;
using System.IO.Compression;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Desktop.Runtime;

public static class DesktopCrashRuntime
{
    private const string ApiBaseUrlEnvironmentVariable = "CHUMMER_API_BASE_URL";
    private const string ApiKeyEnvironmentVariable = "CHUMMER_API_KEY";
    private const string CrashRootDirectoryName = "desktop-crashes";
    private const string PendingCrashFileName = "pending.json";
    private const string CrashReportFileName = "report.json";
    private const string CrashSummaryFileName = "summary.txt";
    private const string CrashBundleFileName = "diagnostics.zip";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static DesktopCrashMonitor InstallUnhandledExceptionMonitor(string headId)
        => new(headId, CaptureCrashReport);

    public static DesktopPendingCrashReport? TryLoadPendingCrashReport()
    {
        string pendingPath = GetPendingCrashMarkerPath();
        DesktopCrashPendingMarker? marker = TryLoadPendingMarker(pendingPath);
        if (marker is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(marker.ReportDirectory))
        {
            return null;
        }

        string reportDirectory = marker.ReportDirectory;
        string reportPath = Path.Combine(reportDirectory, CrashReportFileName);
        if (!File.Exists(reportPath))
        {
            TryDeleteFile(pendingPath);
            return null;
        }

        DesktopCrashReport? report;
        try
        {
            report = JsonSerializer.Deserialize<DesktopCrashReport>(File.ReadAllText(reportPath, Encoding.UTF8), JsonOptions);
        }
        catch
        {
            TryDeleteFile(pendingPath);
            return null;
        }

        if (report is null)
        {
            return null;
        }

        string summaryPath = Path.Combine(reportDirectory, CrashSummaryFileName);
        string summaryText = File.Exists(summaryPath)
            ? File.ReadAllText(summaryPath, Encoding.UTF8)
            : BuildRecoverySummary(report, reportDirectory);

        return new DesktopPendingCrashReport(
            Report: report,
            ReportDirectory: reportDirectory,
            ReportPath: reportPath,
            SummaryPath: summaryPath,
            BundlePath: Path.Combine(reportDirectory, CrashBundleFileName),
            SummaryText: summaryText,
            SubmissionAttempts: marker.SubmissionAttempts,
            LastSubmissionAttemptUtc: marker.LastSubmissionAttemptUtc,
            LastSubmissionError: marker.LastSubmissionError,
            IncidentId: marker.IncidentId,
            SubmittedAtUtc: marker.SubmittedAtUtc);
    }

    [Obsolete("Use TryLoadPendingCrashReport instead.")]
    public static DesktopPendingCrashReport? TryTakePendingCrashReport()
        => TryLoadPendingCrashReport();

    public static bool TryAcknowledgePendingCrashReport(string crashId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(crashId);

        string pendingPath = GetPendingCrashMarkerPath();
        DesktopCrashPendingMarker? marker = TryLoadPendingMarker(pendingPath);
        if (marker is null || !string.Equals(marker.CrashId, crashId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        TryDeleteFile(pendingPath);
        return true;
    }

    public static async Task<DesktopCrashSubmissionResult> SubmitPendingCrashReportAsync(
        DesktopPendingCrashReport pending,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        string pendingPath = GetPendingCrashMarkerPath();
        DesktopCrashPendingMarker? marker = TryLoadPendingMarker(pendingPath);
        if (marker is null || !string.Equals(marker.CrashId, pending.Report.CrashId, StringComparison.OrdinalIgnoreCase))
        {
            return new DesktopCrashSubmissionResult(
                Succeeded: false,
                AlreadySubmitted: false,
                IncidentId: null,
                ClusterId: null,
                WorkItemId: null,
                SubmittedAtUtc: null,
                Message: "No matching pending crash report is available anymore.");
        }

        if (marker.SubmittedAtUtc is not null && !string.IsNullOrWhiteSpace(marker.IncidentId))
        {
            return new DesktopCrashSubmissionResult(
                Succeeded: true,
                AlreadySubmitted: true,
                IncidentId: marker.IncidentId,
                ClusterId: marker.ClusterId,
                WorkItemId: marker.WorkItemId,
                SubmittedAtUtc: marker.SubmittedAtUtc,
                Message: $"Crash report already reached Hub as {marker.IncidentId}.");
        }

        DateTimeOffset attemptAtUtc = DateTimeOffset.UtcNow;
        try
        {
            DesktopCrashEnvelope envelope = BuildEnvelope(pending.Report, pending.SummaryText);
            using HttpClient client = CreateApiHttpClient(TimeSpan.FromSeconds(20));
            using StringContent content = new(
                JsonSerializer.Serialize(envelope, JsonOptions),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
            using HttpResponseMessage response = await client.PostAsync("api/v1/support/crashes", content, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string error = $"Hub intake returned {(int)response.StatusCode} {response.ReasonPhrase}.";
                PersistPendingMarker(marker with
                {
                    SubmissionAttempts = marker.SubmissionAttempts + 1,
                    LastSubmissionAttemptUtc = attemptAtUtc,
                    LastSubmissionError = error
                });
                return new DesktopCrashSubmissionResult(
                    Succeeded: false,
                    AlreadySubmitted: false,
                    IncidentId: null,
                    ClusterId: null,
                    WorkItemId: null,
                    SubmittedAtUtc: null,
                    Message: error);
            }

            DesktopCrashIntakeAcceptedResponse? accepted = JsonSerializer.Deserialize<DesktopCrashIntakeAcceptedResponse>(responseText, JsonOptions);
            string? incidentId = accepted?.Incident?.IncidentId;
            string? clusterId = accepted?.Cluster?.ClusterId;
            string? workItemId = accepted?.WorkItem?.WorkItemId;
            PersistPendingMarker(marker with
            {
                SubmissionAttempts = marker.SubmissionAttempts + 1,
                LastSubmissionAttemptUtc = attemptAtUtc,
                LastSubmissionError = null,
                IncidentId = incidentId,
                ClusterId = clusterId,
                WorkItemId = workItemId,
                SubmittedAtUtc = attemptAtUtc
            });

            string message = string.IsNullOrWhiteSpace(incidentId)
                ? "Crash report reached Hub."
                : $"Crash report reached Hub as {incidentId}.";
            return new DesktopCrashSubmissionResult(
                Succeeded: true,
                AlreadySubmitted: false,
                IncidentId: incidentId,
                ClusterId: clusterId,
                WorkItemId: workItemId,
                SubmittedAtUtc: attemptAtUtc,
                Message: message);
        }
        catch (Exception ex)
        {
            PersistPendingMarker(marker with
            {
                SubmissionAttempts = marker.SubmissionAttempts + 1,
                LastSubmissionAttemptUtc = attemptAtUtc,
                LastSubmissionError = ex.Message
            });
            return new DesktopCrashSubmissionResult(
                Succeeded: false,
                AlreadySubmitted: false,
                IncidentId: null,
                ClusterId: null,
                WorkItemId: null,
                SubmittedAtUtc: null,
                Message: $"Automatic crash upload failed: {ex.Message}");
        }
    }

    public static bool TryOpenPathInShell(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe", $"\"{path}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
                : OperatingSystem.IsMacOS()
                    ? new ProcessStartInfo("open", $"\"{path}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                    : new ProcessStartInfo("xdg-open", $"\"{path}\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static string BuildRecoverySummary(DesktopCrashReport report, string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportDirectory);

        StringBuilder builder = new();
        builder.AppendLine("Chummer closed unexpectedly.");
        builder.AppendLine();
        builder.AppendLine($"Report id: {report.CrashId}");
        builder.AppendLine($"Head: {report.HeadId}");
        builder.AppendLine($"Captured (UTC): {report.CapturedAtUtc:u}");
        builder.AppendLine($"Version: {report.ApplicationVersion}");
        builder.AppendLine($"OS: {report.OperatingSystem}");
        builder.AppendLine($"Architecture: {report.ProcessArchitecture}");
        builder.AppendLine($"Exception: {report.ExceptionType}");
        builder.AppendLine($"Message: {report.ExceptionMessage}");
        builder.AppendLine();
        builder.AppendLine("Artifacts:");
        builder.AppendLine($"- Report: {Path.Combine(reportDirectory, CrashReportFileName)}");
        builder.AppendLine($"- Summary: {Path.Combine(reportDirectory, CrashSummaryFileName)}");
        builder.AppendLine($"- Bundle: {Path.Combine(reportDirectory, CrashBundleFileName)}");

        if (!string.IsNullOrWhiteSpace(report.ExceptionDetail))
        {
            builder.AppendLine();
            builder.AppendLine("Details:");
            builder.AppendLine(report.ExceptionDetail);
        }

        return builder.ToString().TrimEnd();
    }

    private static void CaptureCrashReport(string headId, Exception exception, bool isTerminating)
    {
        try
        {
            DesktopCrashContext context = DesktopCrashContext.Create(headId);
            string crashRoot = EnsureCrashRoot();
            string reportDirectory = Path.Combine(
                crashRoot,
                $"{context.CapturedAtUtc:yyyyMMdd-HHmmss}-{context.CrashId[..8]}");
            Directory.CreateDirectory(reportDirectory);

            DesktopCrashReport report = new(
                CrashId: context.CrashId,
                HeadId: context.HeadId,
                CapturedAtUtc: context.CapturedAtUtc,
                IsTerminating: isTerminating,
                ApplicationVersion: context.ApplicationVersion,
                RuntimeVersion: context.RuntimeVersion,
                OperatingSystem: context.OperatingSystem,
                ProcessArchitecture: context.ProcessArchitecture,
                ProcessName: context.ProcessName,
                BaseDirectoryLabel: context.BaseDirectoryLabel,
                CurrentDirectoryLabel: context.CurrentDirectoryLabel,
                ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
                ExceptionMessage: SanitizeText(exception.Message, context),
                ExceptionDetail: SanitizeText(exception.ToString(), context));

            string reportPath = Path.Combine(reportDirectory, CrashReportFileName);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);

            string summaryText = BuildRecoverySummary(report, reportDirectory);
            string summaryPath = Path.Combine(reportDirectory, CrashSummaryFileName);
            File.WriteAllText(summaryPath, summaryText, Encoding.UTF8);

            string bundlePath = Path.Combine(reportDirectory, CrashBundleFileName);
            CreateCrashBundle(bundlePath, reportPath, summaryPath);

            if (isTerminating)
            {
                PersistPendingMarker(new DesktopCrashPendingMarker(report.CrashId, reportDirectory));
            }

            Console.Error.WriteLine(summaryText);
        }
        catch (Exception captureFailure)
        {
            Console.Error.WriteLine($"Failed to persist desktop crash report for '{headId}': {captureFailure}");
        }
    }

    private static string EnsureCrashRoot()
    {
        string root = Path.Combine(GetStateRoot(), CrashRootDirectoryName);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string GetPendingCrashMarkerPath()
        => Path.Combine(EnsureCrashRoot(), PendingCrashFileName);

    private static DesktopCrashPendingMarker? TryLoadPendingMarker(string pendingPath)
    {
        if (!File.Exists(pendingPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DesktopCrashPendingMarker>(File.ReadAllText(pendingPath, Encoding.UTF8), JsonOptions);
        }
        catch
        {
            TryDeleteFile(pendingPath);
            return null;
        }
    }

    private static void PersistPendingMarker(DesktopCrashPendingMarker marker)
    {
        string pendingPath = GetPendingCrashMarkerPath();
        Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
        File.WriteAllText(pendingPath, JsonSerializer.Serialize(marker, JsonOptions), Encoding.UTF8);
    }

    private static string GetStateRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "Chummer");
        }

        return Path.Combine(Path.GetTempPath(), "Chummer");
    }

    private static void CreateCrashBundle(string bundlePath, string reportPath, string summaryPath)
    {
        if (File.Exists(bundlePath))
        {
            File.Delete(bundlePath);
        }

        using ZipArchive archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(reportPath, Path.GetFileName(reportPath));
        archive.CreateEntryFromFile(summaryPath, Path.GetFileName(summaryPath));
    }

    private static string SanitizeText(string? value, DesktopCrashContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string sanitized = value;
        foreach ((string needle, string replacement) in context.Redactions)
        {
            if (!string.IsNullOrWhiteSpace(needle))
            {
                sanitized = sanitized.Replace(needle, replacement, StringComparison.OrdinalIgnoreCase);
            }
        }

        return sanitized.Trim();
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
            // Ignore cleanup failures; the next launch can retry.
        }
    }

    private static DesktopCrashEnvelope BuildEnvelope(DesktopCrashReport report, string summaryText)
    {
        string platform = ResolvePlatformFromOs(report.OperatingSystem);
        return new DesktopCrashEnvelope(
            CrashId: report.CrashId,
            HeadId: report.HeadId,
            ApplicationVersion: report.ApplicationVersion,
            RuntimeVersion: report.RuntimeVersion,
            OperatingSystem: report.OperatingSystem,
            ProcessArchitecture: report.ProcessArchitecture,
            CrashFingerprint: ComputeFingerprint(report),
            ExceptionType: report.ExceptionType,
            ExceptionMessage: report.ExceptionMessage,
            ExceptionDetail: report.ExceptionDetail,
            CapturedAtUtc: report.CapturedAtUtc,
            IsTerminating: report.IsTerminating,
            ReleaseChannel: ResolveReleaseChannel(),
            Platform: platform,
            DesktopHead: report.HeadId,
            RuntimeHead: "desktop-runtime",
            LastActionCategory: null,
            LogTail: BuildLogTail(summaryText, report.ExceptionDetail),
            FullDiagnosticsOptIn: false);
    }

    private static IReadOnlyList<string> BuildLogTail(string summaryText, string exceptionDetail)
    {
        IEnumerable<string> lines = summaryText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(exceptionDetail.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Take(12);
        return lines.ToArray();
    }

    private static string ComputeFingerprint(DesktopCrashReport report)
    {
        List<string> frames = report.ExceptionDetail
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => line.StartsWith("at ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("---", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();
        string seed = $"{report.HeadId}|{report.ExceptionType}|{string.Join('|', frames)}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static string ResolveReleaseChannel()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "ChummerDesktopReleaseChannel", StringComparison.Ordinal))?
            .Value
            ?? "local";
    }

    private static string ResolvePlatformFromOs(string operatingSystem)
    {
        if (operatingSystem.Contains("windows", StringComparison.OrdinalIgnoreCase))
        {
            return "windows";
        }

        if (operatingSystem.Contains("darwin", StringComparison.OrdinalIgnoreCase)
            || operatingSystem.Contains("mac", StringComparison.OrdinalIgnoreCase))
        {
            return "macos";
        }

        if (operatingSystem.Contains("linux", StringComparison.OrdinalIgnoreCase))
        {
            return "linux";
        }

        return "unknown";
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

    private sealed record DesktopCrashPendingMarker(
        string CrashId,
        string ReportDirectory,
        int SubmissionAttempts = 0,
        DateTimeOffset? LastSubmissionAttemptUtc = null,
        string? LastSubmissionError = null,
        string? IncidentId = null,
        string? ClusterId = null,
        string? WorkItemId = null,
        DateTimeOffset? SubmittedAtUtc = null);

    private sealed record DesktopCrashEnvelope(
        string CrashId,
        string HeadId,
        string ApplicationVersion,
        string RuntimeVersion,
        string OperatingSystem,
        string ProcessArchitecture,
        string CrashFingerprint,
        string ExceptionType,
        string ExceptionMessage,
        string ExceptionDetail,
        DateTimeOffset CapturedAtUtc,
        bool IsTerminating,
        string? ReleaseChannel,
        string? Platform,
        string? DesktopHead,
        string? RuntimeHead,
        string? LastActionCategory,
        IReadOnlyList<string> LogTail,
        bool FullDiagnosticsOptIn);

    private sealed record DesktopCrashIncidentResponse(string IncidentId);

    private sealed record DesktopCrashClusterResponse(string ClusterId);

    private sealed record DesktopCrashWorkItemResponse(string WorkItemId);

    private sealed record DesktopCrashIntakeAcceptedResponse(
        DesktopCrashIncidentResponse? Incident,
        DesktopCrashClusterResponse? Cluster,
        DesktopCrashWorkItemResponse? WorkItem,
        bool ForwardedForAutomation);

    private sealed record DesktopCrashContext(
        string CrashId,
        string HeadId,
        DateTimeOffset CapturedAtUtc,
        string ApplicationVersion,
        string RuntimeVersion,
        string OperatingSystem,
        string ProcessArchitecture,
        string ProcessName,
        string BaseDirectoryLabel,
        string CurrentDirectoryLabel,
        IReadOnlyList<(string needle, string replacement)> Redactions)
    {
        public static DesktopCrashContext Create(string headId)
        {
            string baseDirectory = NormalizePath(AppContext.BaseDirectory);
            string currentDirectory = NormalizePath(Directory.GetCurrentDirectory());
            string tempDirectory = NormalizePath(Path.GetTempPath());
            string? userProfile = NormalizePath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            string? userName = string.IsNullOrWhiteSpace(Environment.UserName) ? null : Environment.UserName;

            List<(string needle, string replacement)> redactions =
            [
                (baseDirectory, "<app-base>"),
                (currentDirectory, "<cwd>"),
                (tempDirectory, "<temp>")
            ];
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                redactions.Add((userProfile, "<user-home>"));
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                redactions.Add((userName, "<user>"));
            }

            return new DesktopCrashContext(
                CrashId: Guid.NewGuid().ToString("N"),
                HeadId: headId,
                CapturedAtUtc: DateTimeOffset.UtcNow,
                ApplicationVersion: ResolveApplicationVersion(),
                RuntimeVersion: RuntimeInformation.FrameworkDescription,
                OperatingSystem: RuntimeInformation.OSDescription,
                ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
                ProcessName: ResolveProcessName(),
                BaseDirectoryLabel: LabelPath(baseDirectory),
                CurrentDirectoryLabel: LabelPath(currentDirectory),
                Redactions: redactions);
        }

        private static string ResolveApplicationVersion()
        {
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly is null)
            {
                return "unknown";
            }

            string? informationalVersion = entryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            return entryAssembly.GetName().Version?.ToString() ?? "unknown";
        }

        private static string ResolveProcessName()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return Path.GetFileName(processPath);
            }

            using Process process = Process.GetCurrentProcess();
            return process.ProcessName;
        }

        private static string NormalizePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string LabelPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "(none)";
            }

            string? name = Path.GetFileName(value);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return value;
        }
    }
}

public sealed class DesktopCrashMonitor : IDisposable
{
    private readonly string _headId;
    private readonly Action<string, Exception, bool> _capture;
    private int _captureState;
    private bool _disposed;

    internal DesktopCrashMonitor(string headId, Action<string, Exception, bool> capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(capture);

        _headId = headId;
        _capture = capture;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _disposed = true;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            CaptureOnce(exception, args.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        CaptureOnce(args.Exception, isTerminating: false);
        args.SetObserved();
    }

    private void CaptureOnce(Exception exception, bool isTerminating)
    {
        if (isTerminating)
        {
            if (Interlocked.Exchange(ref _captureState, 2) == 2)
            {
                return;
            }
        }
        else if (Interlocked.CompareExchange(ref _captureState, 1, 0) != 0)
        {
            return;
        }

        _capture(_headId, exception, isTerminating);
    }
}
