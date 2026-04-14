using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chummer.Desktop.Runtime;

public static class DesktopStartupSmokeRuntime
{
    private const string StartupSmokeSwitch = "--startup-smoke";
    private const string StartupSmokeReceiptEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT";
    private const string StartupSmokeFailurePacketEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET";
    private const string StartupSmokeArtifactDigestEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST";
    private const string StartupSmokeHostClassEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS";
    private const string StartupSmokeReadyCheckpointEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT";
    private const string StartupSmokeReleaseVersionEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION";
    private const string StartupSmokeRidEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_RID";
    private const string StartupSmokeForceCrashEnvironmentVariable = "CHUMMER_DESKTOP_STARTUP_SMOKE_FORCE_CRASH";
    private const string StartupSmokeReleaseChannelEnvironmentVariable = "CHUMMER_DESKTOP_RELEASE_CHANNEL";

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

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DesktopStartupSmokeContext context = BuildContext(headId, startedAt);

        try
        {
            if (ParseBool(Environment.GetEnvironmentVariable(StartupSmokeForceCrashEnvironmentVariable)))
            {
                throw new InvalidOperationException("Startup smoke forced a crash for OODA verification.");
            }

            DesktopStartupSmokeReceipt receipt = new(
                HeadId: context.HeadId,
                Version: context.Version,
                ReleaseVersion: context.ReleaseVersion,
                ChannelId: context.ChannelId,
                Platform: context.Platform,
                Arch: context.Arch,
                Rid: context.Rid,
                ReadyCheckpoint: context.ReadyCheckpoint,
                HostClass: context.HostClass,
                ProcessPath: context.ProcessPath,
                ArtifactDigest: context.ArtifactDigest,
                ArtifactDigestSource: context.ArtifactDigestSource,
                Framework: context.Framework,
                OperatingSystem: context.OperatingSystem,
                RecordedAtUtc: DateTimeOffset.UtcNow,
                StartedAtUtc: context.StartedAtUtc,
                CompletedAtUtc: DateTimeOffset.UtcNow);

            string? receiptPath = Environment.GetEnvironmentVariable(StartupSmokeReceiptEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(receiptPath))
            {
                WriteReceipt(receiptPath, receipt);
            }

            Console.Out.WriteLine(
                $"startup smoke ready: head={receipt.HeadId} platform={receipt.Platform} arch={receipt.Arch} checkpoint={receipt.ReadyCheckpoint}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Startup smoke failed: {ex.Message}");
            string? packetPath = TryWriteFailurePacket(context, ex, exitCode: 1);
            if (!string.IsNullOrWhiteSpace(packetPath))
            {
                Console.Error.WriteLine($"Startup smoke failure packet: {packetPath}");
            }

            return 1;
        }
    }

    private static bool ParseBool(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static DesktopStartupSmokeContext BuildContext(string headId, DateTimeOffset startedAtUtc)
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        string processPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        (string? artifactDigest, string artifactDigestSource) = ResolveArtifactDigest(processPath);
        string resolvedVersion = ResolveStartupSmokeVersion(assembly);
        return new DesktopStartupSmokeContext(
            HeadId: ReadAssemblyMetadata(assembly, "ChummerDesktopHeadId") ?? headId,
            Version: resolvedVersion,
            ReleaseVersion: ResolveReleaseVersion(assembly, resolvedVersion),
            ChannelId: ResolveStartupSmokeChannelId(assembly),
            Platform: DetectPlatform(),
            Arch: DetectArchitecture(),
            Rid: ResolveStartupSmokeRid(),
            ReadyCheckpoint: Environment.GetEnvironmentVariable(StartupSmokeReadyCheckpointEnvironmentVariable) ?? "pre_ui_event_loop",
            HostClass: Environment.GetEnvironmentVariable(StartupSmokeHostClassEnvironmentVariable) ?? Environment.MachineName,
            ProcessPath: processPath,
            ArtifactDigest: artifactDigest,
            ArtifactDigestSource: artifactDigestSource,
            Framework: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            StartedAtUtc: startedAtUtc);
    }

    private static (string? ArtifactDigest, string ArtifactDigestSource) ResolveArtifactDigest(string processPath)
    {
        string? configuredDigest = Environment.GetEnvironmentVariable(StartupSmokeArtifactDigestEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredDigest))
        {
            return (NormalizeSha256Digest(configuredDigest), "environment");
        }

        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return (null, "unavailable");
        }

        try
        {
            using FileStream stream = File.OpenRead(processPath);
            using SHA256 sha256 = SHA256.Create();
            return ($"sha256:{Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant()}", "process_path");
        }
        catch
        {
            return (null, "unavailable");
        }
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

    private static string ResolveStartupSmokeChannelId(Assembly assembly)
    {
        string? overrideChannel = Environment.GetEnvironmentVariable(StartupSmokeReleaseChannelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideChannel))
        {
            return overrideChannel;
        }

        return ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseChannel") ?? "local";
    }

    private static string ResolveStartupSmokeVersion(Assembly assembly)
    {
        string? overrideVersion = Environment.GetEnvironmentVariable(StartupSmokeReleaseVersionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideVersion))
        {
            return overrideVersion.Trim();
        }

        return ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? string.Empty;
    }

    private static string ResolveReleaseVersion(Assembly assembly, string fallbackVersion)
    {
        string? overrideVersion = Environment.GetEnvironmentVariable(StartupSmokeReleaseVersionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideVersion))
        {
            return overrideVersion.Trim();
        }

        string? metadataVersion = ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion");
        if (!string.IsNullOrWhiteSpace(metadataVersion))
        {
            return metadataVersion;
        }

        if (!string.IsNullOrWhiteSpace(fallbackVersion))
        {
            return fallbackVersion;
        }

        return string.Empty;
    }

    private static string ResolveStartupSmokeRid()
    {
        string? overrideRid = Environment.GetEnvironmentVariable(StartupSmokeRidEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideRid))
        {
            return overrideRid.Trim().ToLowerInvariant();
        }

        return string.Empty;
    }

    private static void WriteReceipt(string receiptPath, DesktopStartupSmokeReceipt receipt)
    {
        string? receiptDirectory = Path.GetDirectoryName(receiptPath);
        if (string.IsNullOrWhiteSpace(receiptDirectory))
        {
            throw new InvalidOperationException($"Startup smoke receipt path was invalid: '{receiptPath}'.");
        }

        Directory.CreateDirectory(receiptDirectory);
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions), Encoding.UTF8);
    }

    private static string? TryWriteFailurePacket(DesktopStartupSmokeContext context, Exception ex, int exitCode)
    {
        string? failurePacketPath = ResolveFailurePacketPath();
        if (string.IsNullOrWhiteSpace(failurePacketPath))
        {
            return null;
        }

        try
        {
            WriteFailurePacket(failurePacketPath, BuildFailurePacket(context, ex, exitCode));
            return failurePacketPath;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveFailurePacketPath()
    {
        string? explicitPath = Environment.GetEnvironmentVariable(StartupSmokeFailurePacketEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        string? receiptPath = Environment.GetEnvironmentVariable(StartupSmokeReceiptEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(receiptPath))
        {
            return null;
        }

        string? receiptDirectory = Path.GetDirectoryName(receiptPath);
        string? receiptFileName = Path.GetFileNameWithoutExtension(receiptPath);
        string? receiptExtension = Path.GetExtension(receiptPath);
        if (string.IsNullOrWhiteSpace(receiptDirectory) || string.IsNullOrWhiteSpace(receiptFileName))
        {
            return null;
        }

        string extension = string.IsNullOrWhiteSpace(receiptExtension) ? ".json" : receiptExtension;
        return Path.Combine(receiptDirectory, $"{receiptFileName}.failure{extension}");
    }

    private static DesktopStartupSmokeFailurePacket BuildFailurePacket(
        DesktopStartupSmokeContext context,
        Exception ex,
        int exitCode)
    {
        string[] logTail = BuildLogTail(ex);
        string fingerprintSource = string.Join(
            "|",
            context.HeadId,
            context.Platform,
            context.Arch,
            context.ReadyCheckpoint,
            ex.GetType().FullName ?? ex.GetType().Name,
            ex.Message,
            string.Join("\n", logTail));
        using SHA256 sha256 = SHA256.Create();
        string fingerprint = Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(fingerprintSource)))
            .ToLowerInvariant()[..16];

        return new DesktopStartupSmokeFailurePacket(
            SignalClass: "release_smoke_start_failure",
            HeadId: context.HeadId,
            Version: context.Version,
            ChannelId: context.ChannelId,
            Platform: context.Platform,
            Arch: context.Arch,
            ReadyCheckpoint: context.ReadyCheckpoint,
            HostClass: context.HostClass,
            ProcessPath: context.ProcessPath,
            ArtifactDigest: context.ArtifactDigest,
            ArtifactDigestSource: context.ArtifactDigestSource,
            Framework: context.Framework,
            OperatingSystem: context.OperatingSystem,
            ExitCode: exitCode,
            ErrorType: ex.GetType().FullName ?? ex.GetType().Name,
            ErrorMessage: ex.Message,
            CrashFingerprint: fingerprint,
            LogTail: logTail,
            RecordedAtUtc: DateTimeOffset.UtcNow,
            StartedAtUtc: context.StartedAtUtc,
            OodaRecommendation: "freeze_or_fix_before_promotion");
    }

    private static string[] BuildLogTail(Exception ex)
        => ex.ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0)
            .TakeLast(20)
            .Select(static line => line.Length <= 240 ? line : $"{line[..237]}...")
            .ToArray();

    private static void WriteFailurePacket(string packetPath, DesktopStartupSmokeFailurePacket packet)
    {
        string? packetDirectory = Path.GetDirectoryName(packetPath);
        if (string.IsNullOrWhiteSpace(packetDirectory))
        {
            throw new InvalidOperationException($"Startup smoke failure packet path was invalid: '{packetPath}'.");
        }

        Directory.CreateDirectory(packetDirectory);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(packet, JsonOptions), Encoding.UTF8);
    }

    private static string NormalizeSha256Digest(string digest)
    {
        string trimmed = digest.Trim();
        if (trimmed.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return $"sha256:{trimmed["sha256:".Length..].Trim()}";
        }

        return $"sha256:{trimmed}";
    }

    public sealed record DesktopStartupSmokeReceipt(
        string HeadId,
        string Version,
        string ReleaseVersion,
        string ChannelId,
        string Platform,
        string Arch,
        string Rid,
        string ReadyCheckpoint,
        string HostClass,
        string ProcessPath,
        string? ArtifactDigest,
        string ArtifactDigestSource,
        string Framework,
        string OperatingSystem,
        DateTimeOffset RecordedAtUtc,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset CompletedAtUtc);

    private sealed record DesktopStartupSmokeContext(
        string HeadId,
        string Version,
        string ReleaseVersion,
        string ChannelId,
        string Platform,
        string Arch,
        string Rid,
        string ReadyCheckpoint,
        string HostClass,
        string ProcessPath,
        string? ArtifactDigest,
        string ArtifactDigestSource,
        string Framework,
        string OperatingSystem,
        DateTimeOffset StartedAtUtc);

    public sealed record DesktopStartupSmokeFailurePacket(
        string SignalClass,
        string HeadId,
        string Version,
        string ChannelId,
        string Platform,
        string Arch,
        string ReadyCheckpoint,
        string HostClass,
        string ProcessPath,
        string? ArtifactDigest,
        string ArtifactDigestSource,
        string Framework,
        string OperatingSystem,
        int ExitCode,
        string ErrorType,
        string ErrorMessage,
        IReadOnlyList<string> LogTail,
        string CrashFingerprint,
        DateTimeOffset RecordedAtUtc,
        DateTimeOffset StartedAtUtc,
        string OodaRecommendation);
}
