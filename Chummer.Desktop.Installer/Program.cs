using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Chummer.Desktop.Installer;

internal static class Program
{
    private const string PreferredPayloadResourceName = "ChummerInstaller.Payload.zip";
    private const string PreferredPayloadMetadataKey = "ChummerInstallerPayloadResourceName";
    private const string AppendedPayloadMagic = "CHUMMER6PAYLOAD1";
    private const string ClaimCodeEnvironmentVariable = "CHUMMER_INSTALL_CLAIM_CODE";
    private const string ClaimCodeSwitch = "--install-claim-code";
    private const string ExplicitStateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string PendingClaimCodeFileName = "pending-claim-code.txt";
    private const string ChummerIconFileName = "chummer.ico";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (!OperatingSystem.IsWindows())
        {
            MessageBox.Show(
                "This installer only runs on Windows.",
                "Chummer Installer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        try
        {
            InstallerMetadata metadata = InstallerMetadata.Load();
            string? payloadPathOverride = ResolvePayloadPathOverride(args);
            string? claimCode = ResolveClaimCode(args);

            if (args.Length > 0 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                return Uninstall(metadata);
            }

            if (args.Length > 1 && string.Equals(args[0], "--smoke-install", StringComparison.OrdinalIgnoreCase))
            {
                return SmokeInstall(metadata, args[1], payloadPathOverride);
            }

            return Install(metadata, payloadPathOverride, claimCode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Chummer Installer Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int Install(InstallerMetadata metadata, string? payloadPathOverride, string? claimCode)
    {
        string targetDir = InstallPayload(metadata, metadata.InstallDirectory, payloadPathOverride);
        string installedInstallerPath = Path.Combine(targetDir, metadata.InstallerOutputName + ".exe");
        string launchPath = Path.Combine(targetDir, metadata.LaunchExecutable);
        File.Copy(Environment.ProcessPath!, installedInstallerPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(claimCode))
        {
            StagePendingClaimCode(metadata, claimCode);
        }

        CreateShortcut(metadata.ShortcutPath, launchPath, metadata.DisplayName);
        CreateShortcut(metadata.DesktopShortcutPath, launchPath, metadata.DisplayName);
        RegisterUninstall(metadata, installedInstallerPath);

        DialogResult launch = MessageBox.Show(
            $"{metadata.DisplayName} installed to:\n{targetDir}\n\nYes: launch now\nNo: finish without launching",
            "Install Complete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (launch == DialogResult.Yes)
        {
            LaunchInstalledApp(metadata, claimCode);
        }

        return 0;
    }

    private static int Uninstall(InstallerMetadata metadata)
    {
        RemoveShortcut(metadata.ShortcutPath);
        RemoveShortcut(metadata.DesktopShortcutPath);
        UnregisterUninstall(metadata);
        ScheduleDirectoryRemoval(metadata.InstallDirectory);
        MessageBox.Show(
            $"{metadata.DisplayName} is being removed from:\n{metadata.InstallDirectory}",
            "Uninstall Scheduled",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return 0;
    }

    private static int SmokeInstall(InstallerMetadata metadata, string targetDirectory, string? payloadPathOverride)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Smoke install requires a target directory.");
        }

        InstallPayload(metadata, targetDirectory, payloadPathOverride);
        return 0;
    }

    private static string InstallPayload(InstallerMetadata metadata, string targetDirectory, string? payloadPathOverride)
    {
        string targetDir = Path.GetFullPath(targetDirectory);
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"chummer-installer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);
        try
        {
            ExtractPayload(tempExtractDir, payloadPathOverride);
            string payloadRoot = FindPayloadRoot(tempExtractDir, metadata.LaunchExecutable);
            EnsureLaunchExecutableInRoot(payloadRoot, metadata.LaunchExecutable);

            if (Directory.Exists(targetDir))
            {
                TryDeleteDirectory(targetDir);
            }

            Directory.CreateDirectory(targetDir);
            CopyDirectory(payloadRoot, targetDir);
            return targetDir;
        }
        finally
        {
            TryDeleteDirectory(tempExtractDir);
        }
    }

    private static void ExtractPayload(string tempExtractDir, string? payloadPathOverride)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string payloadZipPath = Path.Combine(tempExtractDir, "payload.zip");

        using Stream payload = OpenPayloadStream(assembly, payloadPathOverride);

        using (FileStream zipFile = File.Create(payloadZipPath))
        {
            payload.CopyTo(zipFile);
        }

        ZipFile.ExtractToDirectory(payloadZipPath, tempExtractDir, overwriteFiles: true);
        File.Delete(payloadZipPath);
    }

    private static Stream OpenPayloadStream(Assembly assembly, string? payloadPathOverride = null)
    {
        List<string> failureReports = new();
        string baseDirectory = AppContext.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(payloadPathOverride))
        {
            if (TryOpenPayloadFile(payloadPathOverride, "command line/environment override", out Stream? overrideStream, out string? overrideFailure))
            {
                return overrideStream!;
            }

            failureReports.Add($"1) {overrideFailure}");
        }

        string? embeddedPayloadResource = FindPayloadResource(assembly);
        if (embeddedPayloadResource is not null)
        {
            if (TryOpenPayloadResource(assembly, embeddedPayloadResource, out Stream? resourceStream, out string? resourceFailure))
            {
                RecordPayloadResolution("embedded-resource", failureReports);
                return resourceStream!;
            }

            string resourceTrace = $"embedded resource '{embeddedPayloadResource}'";
            failureReports.Add($"2) {resourceTrace} failed: {resourceFailure}");
        }

        if (TryOpenAppendedPayload(Environment.ProcessPath, out Stream? appendedStream, out string? appendedFailure))
        {
            RecordPayloadResolution("appended-installer-payload", failureReports);
            return appendedStream!;
        }

        if (!string.IsNullOrWhiteSpace(appendedFailure))
        {
            failureReports.Add($"3) {appendedFailure}");
        }

        string[] sidecarPayloads = FindPayloadSidecars(baseDirectory).ToArray();
        for (int i = 0; i < sidecarPayloads.Length; i++)
        {
            string candidate = sidecarPayloads[i];
            if (TryOpenPayloadFile(candidate, $"sidecar payload candidate #{i + 1}", out Stream? sidecarStream, out string? sidecarFailure))
            {
                RecordPayloadResolution($"sidecar:{candidate}", failureReports);
                return sidecarStream!;
            }

            failureReports.Add($"4.{i + 1}) {sidecarFailure}");
        }

        string resourceNames = FormatResourceNames(assembly);
        string failureSummary = failureReports.Count > 0
            ? string.Join("; ", failureReports)
            : "<none>";
        string sidecarSummary = sidecarPayloads.Length > 0
            ? string.Join(", ", sidecarPayloads)
            : "<none>";
        throw new InvalidOperationException(
            $"Bundled desktop payload was not found. Expected '{PreferredPayloadResourceName}'. " +
            $"Appended payload marker: '{AppendedPayloadMagic}'. " +
            $"Embedded resources: {resourceNames}. " +
            $"Checked {baseDirectory} for sidecar payloads: {sidecarSummary}. " +
            $"Discovery trace: {failureSummary}");
    }

    private static bool TryOpenPayloadFile(string payloadPath, string context, out Stream? payloadStream, out string? failure)
    {
        payloadStream = null;
        failure = null;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(payloadPath);
        }
        catch (ArgumentException ex)
        {
            failure = $"{context} payload path was malformed: '{payloadPath}'. {ex.Message}";
            return false;
        }

        try
        {
            payloadStream = File.OpenRead(candidate);
            return true;
        }
        catch (Exception ex)
        {
            failure = $"{context} payload path could not be opened: '{candidate}'. {ex.Message}";
            return false;
        }
    }

    private static bool TryOpenPayloadResource(Assembly assembly, string payloadResourceName, out Stream? payloadStream, out string? failure)
    {
        payloadStream = null;
        failure = null;

        try
        {
            payloadStream = assembly.GetManifestResourceStream(payloadResourceName);
            if (payloadStream is null)
            {
                failure = $"Embedded resource '{payloadResourceName}' was not found in this assembly.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            failure = $"Embedded resource '{payloadResourceName}' could not be opened. {ex.Message}";
            return false;
        }
    }

    private static bool TryOpenAppendedPayload(string? executablePath, out Stream? payloadStream, out string? failure)
    {
        payloadStream = null;
        failure = null;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            failure = "current process path is unavailable, so the appended payload could not be checked.";
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(executablePath);
        }
        catch (ArgumentException ex)
        {
            failure = $"installer path was malformed: '{executablePath}'. {ex.Message}";
            return false;
        }

        byte[] magicBytes = Encoding.ASCII.GetBytes(AppendedPayloadMagic);
        int footerLength = sizeof(long) + magicBytes.Length;

        try
        {
            using FileStream executable = File.Open(candidate, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (executable.Length <= footerLength)
            {
                failure = $"installer '{candidate}' was too small to contain an appended payload footer.";
                return false;
            }

            executable.Seek(-footerLength, SeekOrigin.End);
            using BinaryReader footerReader = new(executable, Encoding.ASCII, leaveOpen: true);
            long payloadLength = footerReader.ReadInt64();
            byte[] marker = footerReader.ReadBytes(magicBytes.Length);

            if (marker.Length != magicBytes.Length || !marker.SequenceEqual(magicBytes))
            {
                failure = $"installer '{candidate}' did not contain the appended payload marker '{AppendedPayloadMagic}'.";
                return false;
            }

            long payloadOffset = executable.Length - footerLength - payloadLength;
            if (payloadLength <= 0 || payloadOffset < 0)
            {
                failure = $"installer '{candidate}' contained an invalid appended payload footer.";
                return false;
            }

            executable.Position = payloadOffset;
            string tempPayloadPath = Path.Combine(Path.GetTempPath(), $"chummer-installer-appended-payload-{Guid.NewGuid():N}.zip");
            FileStream extractedPayload = new(
                tempPayloadPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.DeleteOnClose);
            CopyExactBytes(executable, extractedPayload, payloadLength);
            extractedPayload.Position = 0;
            payloadStream = extractedPayload;
            return true;
        }
        catch (Exception ex)
        {
            failure = $"installer '{candidate}' appended payload could not be opened. {ex.Message}";
            return false;
        }
    }

    private static string? ResolvePayloadPathOverride(string[] args)
    {
        string? overridePath = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--payload-path", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args[i], "--payload", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            overridePath = args[i + 1];
        }

        if (string.IsNullOrWhiteSpace(overridePath))
        {
            overridePath = Environment.GetEnvironmentVariable("CHUMMER_INSTALLER_PAYLOAD_PATH");
        }

        return string.IsNullOrWhiteSpace(overridePath) ? null : overridePath;
    }

    private static string? ResolveClaimCode(string[] args)
    {
        string? fromEnvironment = NormalizeClaimCode(Environment.GetEnvironmentVariable(ClaimCodeEnvironmentVariable));
        if (fromEnvironment is not null)
        {
            return fromEnvironment;
        }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (TryReadValueAfterSwitch(args, i, out string? claimCode))
            {
                return claimCode;
            }
        }

        return null;
    }

    private static bool TryReadValueAfterSwitch(string[] args, int index, out string? claimCode)
    {
        claimCode = null;

        string arg = args[index];
        ReadOnlySpan<char> argSpan = arg.AsSpan().Trim();
        if (argSpan.Length == 0)
        {
            return false;
        }

        if (string.Equals(argSpan.ToString(), ClaimCodeSwitch, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < args.Length)
            {
                claimCode = NormalizeClaimCode(args[index + 1]);
                return claimCode is not null;
            }

            return false;
        }

        if (argSpan[0] == '/')
        {
            argSpan = argSpan[1..];
        }
        else if (argSpan[0] == '-')
        {
            argSpan = argSpan[1..];
            if (argSpan.Length > 0 && argSpan[0] == '-')
            {
                argSpan = argSpan[1..];
            }
        }

        string normalizedSwitch = ClaimCodeSwitch.AsSpan(2).ToString();
        if (argSpan.Equals(normalizedSwitch, StringComparison.OrdinalIgnoreCase)
            && index + 1 < args.Length)
        {
            claimCode = NormalizeClaimCode(args[index + 1]);
            return claimCode is not null;
        }

        string legacyEqualsPrefix = $"{normalizedSwitch}=";
        if (argSpan.StartsWith(legacyEqualsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            claimCode = NormalizeClaimCode(argSpan[legacyEqualsPrefix.Length..].ToString());
            return claimCode is not null;
        }

        string legacyColonPrefix = $"{normalizedSwitch}:";
        if (argSpan.StartsWith(legacyColonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            claimCode = NormalizeClaimCode(argSpan[legacyColonPrefix.Length..].ToString());
            return claimCode is not null;
        }

        return false;
    }

    private static string? NormalizeClaimCode(string? claimCode)
    {
        if (string.IsNullOrWhiteSpace(claimCode))
        {
            return null;
        }

        return string.Concat(claimCode.Trim().Where(static ch => char.IsLetterOrDigit(ch)).ToArray()).ToUpperInvariant();
    }

    private static void RecordPayloadResolution(string chosenSource, IReadOnlyCollection<string> attempts)
    {
        if (attempts.Count == 0)
        {
            return;
        }

        string tracePath = Path.Combine(Path.GetTempPath(), "chummer-desktop-installer-payload-trace.log");
        string traceLine = $"[{DateTime.UtcNow:O}] selected={chosenSource}; recovery={string.Join("; ", attempts)}";
        try
        {
            File.AppendAllText(tracePath, traceLine + Environment.NewLine);
        }
        catch
        {
            // Do not fail installer resolution if trace persistence is unavailable.
        }
    }

    private static IEnumerable<string> FindPayloadSidecars(string baseDirectory)
    {
        if (!Directory.Exists(baseDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory
                .EnumerateFiles(baseDirectory, "*.zip", SearchOption.AllDirectories)
                .Where(name => IsPayloadZipName(Path.GetFileName(name)))
                .Select(name => new { Name = name, Score = ScorePayloadCandidate(name) })
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => Path.GetFileName(entry.Name), StringComparer.OrdinalIgnoreCase)
                .Select(entry => entry.Name)
                .ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    private static int ScorePayloadCandidate(string path)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "Payload.zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "payload.zip", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (fileName.EndsWith("-payload.zip", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (fileName.IndexOf("installer", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 6;
        }

        if (fileName.IndexOf("payload", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 4;
        }

        return 1;
    }

    private static string FindPayloadRoot(string tempExtractDir, string launchExecutable)
    {
        string? launchPath = FindLaunchExecutablePath(tempExtractDir, launchExecutable);
        if (launchPath is null)
        {
            string topEntries = SummarizePayloadEntries(tempExtractDir);
            string target = Path.Combine(tempExtractDir, launchExecutable);
            throw new InvalidOperationException(
                $"The bundled desktop payload did not contain '{launchExecutable}'. " +
                $"Searched from '{tempExtractDir}'. " +
                $"Expected '{target}' was not found. " +
                $"Payload sample: {topEntries}");
        }

        return Path.GetDirectoryName(launchPath)!;
    }

    private static string? FindLaunchExecutablePath(string payloadRoot, string launchExecutable)
    {
        string directPath = Path.Combine(payloadRoot, launchExecutable);
        if (File.Exists(directPath))
        {
            return directPath;
        }

        string? match = null;
        foreach (string file in Directory.EnumerateFiles(payloadRoot, "*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (!string.Equals(fileName, launchExecutable, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match is null || file.Length < match.Length)
            {
                match = file;
            }
        }

        return match;
    }

    private static void EnsureLaunchExecutableInRoot(string payloadRoot, string launchExecutable)
    {
        string launchPath = Path.Combine(payloadRoot, launchExecutable);
        if (!File.Exists(launchPath))
        {
            throw new InvalidOperationException(
                $"The bundled desktop payload did not contain '{launchExecutable}'.");
        }
    }

    private static string? FindPayloadResource(Assembly assembly)
    {
        string[] resourceNames = assembly.GetManifestResourceNames();
        string? preferredResourceFromMetadata = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, PreferredPayloadMetadataKey, StringComparison.Ordinal))
            ?.Value;
        if (!string.IsNullOrWhiteSpace(preferredResourceFromMetadata))
        {
            string? exactMetadataMatch = Array.Find(resourceNames, resourceName =>
                string.Equals(resourceName, preferredResourceFromMetadata, StringComparison.OrdinalIgnoreCase));
            if (exactMetadataMatch is not null)
            {
                return exactMetadataMatch;
            }
        }

        string? exactMatch = resourceNames.FirstOrDefault(
            name => string.Equals(name, PreferredPayloadResourceName, StringComparison.Ordinal));
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        string? suffixMatch = resourceNames.FirstOrDefault(
            static name => name.EndsWith(".Payload.zip", StringComparison.OrdinalIgnoreCase));
        if (suffixMatch is not null)
        {
            return suffixMatch;
        }

        string? candidateMatch = resourceNames.FirstOrDefault(
            static name => IsPayloadZipName(name));
        if (candidateMatch is not null)
        {
            return candidateMatch;
        }

        return null;
    }

    private static string FormatResourceNames(Assembly assembly)
    {
        string[] resourceNames = assembly.GetManifestResourceNames();
        if (resourceNames.Length == 0)
        {
            return "<none>";
        }

        Array.Sort(resourceNames, StringComparer.OrdinalIgnoreCase);
        return string.Join(", ", resourceNames);
    }

    private static bool IsPayloadZipName(string name)
    {
        return string.Equals(Path.GetFileName(name), "Payload.zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "payload.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".Payload.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".payload.zip", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("-payload.zip", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Payload", StringComparison.OrdinalIgnoreCase)
            && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string SummarizePayloadEntries(string payloadRoot)
    {
        if (!Directory.Exists(payloadRoot))
        {
            return "<missing>";
        }

        StringBuilder summary = new StringBuilder();
        summary.Append("root=[");
        IEnumerable<string> topEntries = Directory.EnumerateFileSystemEntries(payloadRoot, "*", SearchOption.TopDirectoryOnly)
            .Take(40)
            .Select(path => Path.GetFileName(path) ?? string.Empty);
        summary.AppendJoin(", ", topEntries);
        summary.Append(']');
        return summary.ToString();
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void CopyExactBytes(Stream source, Stream destination, long bytesToCopy)
    {
        byte[] buffer = new byte[81920];
        long remaining = bytesToCopy;
        while (remaining > 0)
        {
            int bytesRead = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (bytesRead <= 0)
            {
                throw new EndOfStreamException($"Expected {bytesToCopy} bytes from appended payload, but only copied {bytesToCopy - remaining}.");
            }

            destination.Write(buffer, 0, bytesRead);
            remaining -= bytesRead;
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
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not replace the existing installation at '{path}'. Close Chummer and try again.\n\n{ex.Message}");
        }
    }

    private static void LaunchInstalledApp(InstallerMetadata metadata, string? claimCode)
    {
        string target = Path.Combine(metadata.InstallDirectory, metadata.LaunchExecutable);
        ProcessStartInfo startInfo = new()
        {
            FileName = target,
            WorkingDirectory = metadata.InstallDirectory,
            UseShellExecute = true,
        };

        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (!string.IsNullOrWhiteSpace(normalizedClaimCode))
        {
            startInfo.Arguments = $"{ClaimCodeSwitch} {QuoteArgument(normalizedClaimCode)}";
        }

        Process.Start(startInfo);
    }

    private static void StagePendingClaimCode(InstallerMetadata metadata, string claimCode)
    {
        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (string.IsNullOrWhiteSpace(normalizedClaimCode))
        {
            return;
        }

        string pendingPath = GetPendingClaimCodePath(metadata);
        Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
        File.WriteAllText(pendingPath, normalizedClaimCode, Encoding.UTF8);
    }

    private static string GetPendingClaimCodePath(InstallerMetadata metadata)
        => Path.Combine(
            ResolveDesktopStateRoot(),
            "install-linking",
            metadata.HeadId,
            "windows",
            NormalizeArchitecture(RuntimeInformation.OSArchitecture),
            PendingClaimCodeFileName);

    private static string ResolveDesktopStateRoot()
    {
        string? configured = Environment.GetEnvironmentVariable(ExplicitStateRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured.Trim())), "Chummer6");
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "Chummer6");
        }

        return Path.Combine(Path.GetTempPath(), "Chummer6");
    }

    private static string ResolveShortcutIconPath(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return targetPath;
        }

        string? launchDirectory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(launchDirectory) || !Directory.Exists(launchDirectory))
        {
            return targetPath;
        }

        string preferredIconPath = Path.Combine(launchDirectory, ChummerIconFileName);
        if (File.Exists(preferredIconPath))
        {
            return preferredIconPath;
        }

        try
        {
            string? discoveredIconPath = Directory
                .EnumerateFiles(launchDirectory, "*.ico", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path => string.Equals(
                    Path.GetFileName(path),
                    ChummerIconFileName,
                    StringComparison.OrdinalIgnoreCase));

            if (discoveredIconPath is not null)
            {
                return discoveredIconPath;
            }
        }
        catch (IOException)
        {
            // Fallback to launcher executable icon if directory enumeration fails.
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback to launcher executable icon if directory enumeration fails.
        }

        return targetPath;
    }

    private static string NormalizeArchitecture(Architecture architecture)
        => architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant()
        };

    private static string QuoteArgument(string value)
        => $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static void RegisterUninstall(InstallerMetadata metadata, string installerPath)
    {
        string launchPath = Path.Combine(metadata.InstallDirectory, metadata.LaunchExecutable);
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(metadata.UninstallRegistryKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not create uninstall registry entry.");
        key.SetValue("DisplayName", metadata.DisplayName);
        key.SetValue("DisplayVersion", metadata.Version);
        key.SetValue("Publisher", metadata.Publisher);
        key.SetValue("InstallLocation", metadata.InstallDirectory);
        key.SetValue("DisplayIcon", ResolveShortcutIconPath(launchPath));
        key.SetValue("UninstallString", $"\"{installerPath}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{installerPath}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static void UnregisterUninstall(InstallerMetadata metadata)
    {
        Registry.CurrentUser.DeleteSubKeyTree(metadata.UninstallRegistryKeyPath, throwOnMissingSubKey: false);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is unavailable on this Windows installation.");
        object shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Could not create WScript.Shell.");
        try
        {
            object shortcutObject = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: new object[] { shortcutPath }) ?? throw new InvalidOperationException("Could not create shortcut.");
            dynamic shortcut = shortcutObject;
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = description;
            shortcut.IconLocation = ResolveShortcutIconPath(targetPath);
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcutObject);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void RemoveShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static void ScheduleDirectoryRemoval(string installDirectory)
    {
        string tempScript = Path.Combine(Path.GetTempPath(), $"chummer-uninstall-{Guid.NewGuid():N}.cmd");
        string script = string.Join(
            Environment.NewLine,
            "@echo off",
            "setlocal",
            "ping 127.0.0.1 -n 3 > nul",
            $"rmdir /s /q \"{installDirectory}\"",
            $"del /f /q \"{tempScript}\"");
        File.WriteAllText(tempScript, script, Encoding.ASCII);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{tempScript}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    private sealed record InstallerMetadata(
        string AppId,
        string HeadId,
        string DisplayName,
        string InstallDirName,
        string LaunchExecutable,
        string Version,
        string Publisher,
        string ShortcutName,
        string InstallerOutputName)
    {
        public string InstallDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Chummer6",
                InstallDirName);

        public string ShortcutPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                $"{ShortcutName}.lnk");

        public string DesktopShortcutPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{ShortcutName}.lnk");

        public string UninstallRegistryKeyPath => $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.{AppId}";

        public static InstallerMetadata Load()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string Read(string key, string fallback)
            {
                return assembly
                           .GetCustomAttributes<AssemblyMetadataAttribute>()
                           .FirstOrDefault(attr => string.Equals(attr.Key, key, StringComparison.Ordinal))?
                           .Value
                       ?? fallback;
            }

            string appId = Read("ChummerAppId", "avalonia");
            return new InstallerMetadata(
                AppId: appId,
                HeadId: Read("ChummerHeadId", ResolveHeadId(appId)),
                DisplayName: Read("ChummerDisplayName", "Chummer6"),
                InstallDirName: Read("ChummerInstallDirName", "Chummer6"),
                LaunchExecutable: Read("ChummerLaunchExecutable", "Chummer.Avalonia.exe"),
                Version: Read("ChummerVersion", "unpublished"),
                Publisher: Read("ChummerPublisher", "ArchonMegalon"),
                ShortcutName: Read("ChummerShortcutName", "Chummer6"),
                InstallerOutputName: Read("ChummerInstallerOutputName", "Chummer6Installer"));
        }

        private static string ResolveHeadId(string appId)
        {
            if (appId.StartsWith("blazor-desktop", StringComparison.OrdinalIgnoreCase))
            {
                return "blazor-desktop";
            }

            if (appId.StartsWith("avalonia", StringComparison.OrdinalIgnoreCase))
            {
                return "avalonia";
            }

            return string.IsNullOrWhiteSpace(appId) ? "avalonia" : appId.Trim();
        }
    }
}
