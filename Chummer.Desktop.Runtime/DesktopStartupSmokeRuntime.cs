using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Chummer.Desktop.Runtime;

public static class DesktopStartupSmokeRuntime
{
    private const string StartupSmokeSwitch = "--startup-smoke";
    private const string StartupSmokeReceiptEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT";
    private const string StartupSmokeHostClassEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS";
    private const string StartupSmokeReadyCheckpointEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT";
    private const string StartupSmokeForceCrashEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int?> TryHandleAsync(string headId, string[] args, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(args);

        if (!args.Any(arg => string.Equals(arg, StartupSmokeSwitch, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        if (ParseBool(Environment.GetEnvironmentVariable(StartupSmokeForceCrashEnvironmentVariable)))
        {
            throw new InvalidOperationException("Startup smoke forced a crash for OODA verification.");
        }

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        string readyCheckpoint = Environment.GetEnvironmentVariable(StartupSmokeReadyCheckpointEnvironmentVariable)
            ?? "pre_ui_event_loop";
        DesktopStartupSmokeReceipt receipt = new(
            HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? headId,
            Version: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
                ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? string.Empty,
            ChannelId: ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseChannel") ?? "local",
            Platform: DetectPlatform(),
            Arch: DetectArchitecture(),
            ReadyCheckpoint: readyCheckpoint,
            HostClass: Environment.GetEnvironmentVariable(StartupSmokeHostClassEnvironmentVariable) ?? Environment.MachineName,
            ProcessPath: Environment.ProcessPath ?? AppContext.BaseDirectory,
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: startedAt,
            CompletedAtUtc: DateTimeOffset.UtcNow);

        string? receiptPath = Environment.GetEnvironmentVariable(StartupSmokeReceiptEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(receiptPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions), Encoding.UTF8);
        }

        Console.Out.WriteLine(
            $"startup smoke ready: head={receipt.HeadId} platform={receipt.Platform} arch={receipt.Arch} checkpoint={receipt.ReadyCheckpoint}");
        return 0;
    }

    private static bool ParseBool(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectPlatform()
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

        return RuntimeInformation.OSDescription;
    }

    private static string DetectArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };
    }

    private static string? ReadAssemblyMetadata(Assembly assembly, string key)
    {
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
            .Value;
    }

    public sealed record DesktopStartupSmokeReceipt(
        string HeadId,
        string Version,
        string ChannelId,
        string Platform,
        string Arch,
        string ReadyCheckpoint,
        string HostClass,
        string ProcessPath,
        string Framework,
        string OperatingSystem,
        DateTimeOffset RecordedAtUtc,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc);
}
