using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Chummer.Desktop.Installer;

internal static class Program
{
    private const string PreferredPayloadResourceName = "ChummerInstaller.Payload.zip";
    private const string PreferredPayloadMetadataKey = "ChummerInstallerPayloadResourceName";
    private const string AppendedPayloadMagic = "CHUMMER6PAYLOAD1";
    private const string ClaimCodeEnvironmentVariable = "CHUMMER_INSTALL_CLAIM_CODE";
    private const string ClaimCodeSwitch = "--install-claim-code";
    private const string InstallLinkCallbackSwitch = "--install-link-callback";
    private const string AutoUpdateSwitch = "--auto-update";
    private const string LaunchHeadSwitch = "--launch-head";
    private const string RelaunchArgSwitch = "--relaunch-arg";
    private const string ExplicitStateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";
    private const string PendingClaimCodeFileName = "pending-claim-code.txt";
    private const string ChummerIconFileName = "chummer.ico";
    private const string ChummerProtocolScheme = "chummer";
    private const string InstallerTraceFileName = "chummer-desktop-installer-progress.log";

    [STAThread]
    private static int Main(string[] args)
    {
        bool smokeInstall = args.Length > 1
            && string.Equals(args[0], "--smoke-install", StringComparison.OrdinalIgnoreCase);

        if (!smokeInstall)
        {
            ApplicationConfiguration.Initialize();
        }

        if (!OperatingSystem.IsWindows())
        {
            if (smokeInstall)
            {
                Console.Error.WriteLine("This installer only runs on Windows.");
            }
            else
            {
                MessageBox.Show(
                    "This installer only runs on Windows.",
                    "Chummer Installer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return 1;
        }

        try
        {
            ResetInstallerTrace();
            TraceInstaller($"start args={FormatTraceArguments(args)}");
            InstallerMetadata metadata = InstallerMetadata.Load();
            string? payloadPathOverride = ResolvePayloadPathOverride(args);
            string? claimCode = ResolveClaimCode(args);
            bool autoUpdate = args.Any(arg => string.Equals(arg, AutoUpdateSwitch, StringComparison.OrdinalIgnoreCase));
            string? requestedLaunchHeadId = ResolveLaunchHeadId(args);
            string[] relaunchArgs = ResolveRelaunchArgs(args);

            if (args.Length > 0 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                return Uninstall(metadata);
            }

            if (args.Length > 1 && string.Equals(args[0], "--smoke-install", StringComparison.OrdinalIgnoreCase))
            {
                return SmokeInstall(metadata, args[1], payloadPathOverride);
            }

            return Install(metadata, payloadPathOverride, claimCode, autoUpdate, requestedLaunchHeadId, relaunchArgs);
        }
        catch (Exception ex)
        {
            TraceInstaller("failed " + ex);
            if (smokeInstall)
            {
                Console.Error.WriteLine(ex.ToString());
            }
            else
            {
                MessageBox.Show(
                    ex.Message,
                    "Chummer Installer Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return 1;
        }
    }

    private static int Install(
        InstallerMetadata metadata,
        string? payloadPathOverride,
        string? claimCode,
        bool autoUpdate,
        string? requestedLaunchHeadId,
        IReadOnlyList<string> relaunchArgs)
    {
        using InstallSplashForm splash = new(metadata.DisplayName);
        splash.Show();
        Application.DoEvents();

        IProgress<InstallProgressUpdate> progress = new Progress<InstallProgressUpdate>(update =>
        {
            TraceProgress(update);
            splash.ApplyProgress(update);
        });
        progress.Report(new InstallProgressUpdate("Preparing installer"));

        Stopwatch installStopwatch = Stopwatch.StartNew();
        TimeSpan lastPulse = TimeSpan.Zero;
        Task<string> installTask = Task.Run(() => CompleteInstall(metadata, payloadPathOverride, claimCode, progress));
        try
        {
            while (!installTask.Wait(50))
            {
                if (installStopwatch.Elapsed - lastPulse >= TimeSpan.FromSeconds(1))
                {
                    splash.ApplyElapsed(installStopwatch.Elapsed);
                    lastPulse = installStopwatch.Elapsed;
                }

                Application.DoEvents();
                Thread.Sleep(15);
            }

            splash.ApplyElapsed(installStopwatch.Elapsed);
            TimeSpan minimumProgressDisplay = TimeSpan.FromMilliseconds(1200);
            while (installStopwatch.Elapsed < minimumProgressDisplay)
            {
                splash.ApplyElapsed(installStopwatch.Elapsed);
                Application.DoEvents();
                Thread.Sleep(15);
            }

            Application.DoEvents();
        }
        finally
        {
            splash.Close();
        }

        string targetDir = installTask.GetAwaiter().GetResult();

        if (autoUpdate)
        {
            splash.Show();
            splash.ApplyProgress(new InstallProgressUpdate("Starting updated Chummer"));
            Application.DoEvents();
            LaunchInstalledApp(metadata, claimCode, requestedLaunchHeadId, relaunchArgs, null);
            PumpLaunchSplash(splash, TimeSpan.FromSeconds(2));
            return 0;
        }

        DialogResult launch = PromptForInstalledHeadLaunch(metadata, targetDir);
        if (launch is DialogResult.Yes or DialogResult.No)
        {
            LaunchInstalledApp(metadata, claimCode, requestedLaunchHeadId: null, relaunchArgs: Array.Empty<string>(), launchChoice: launch);
        }

        return 0;
    }

    private static string CompleteInstall(
        InstallerMetadata metadata,
        string? payloadPathOverride,
        string? claimCode,
        IProgress<InstallProgressUpdate>? progress)
    {
        string targetDir = InstallPayload(metadata, metadata.InstallDirectory, payloadPathOverride, progress);
        progress?.Report(new InstallProgressUpdate("Cleaning previous install layout"));
        TryDeleteLegacyInstallDirectories(metadata);

        string installedInstallerPath = Path.Combine(targetDir, metadata.InstallerOutputName + ".exe");
        progress?.Report(new InstallProgressUpdate("Caching installer for uninstall"));
        File.Copy(Environment.ProcessPath!, installedInstallerPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(claimCode))
        {
            progress?.Report(new InstallProgressUpdate("Preparing copy claim"));
            StagePendingClaimCode(metadata, claimCode);
        }

        int totalShortcuts = metadata.InstalledHeads.Count * 2;
        int createdShortcuts = 0;
        foreach (InstalledHeadMetadata head in metadata.InstalledHeads)
        {
            string headLaunchPath = head.ResolveLaunchPath(targetDir);

            progress?.Report(new InstallProgressUpdate("Creating shortcuts", createdShortcuts, totalShortcuts));
            CreateShortcut(head.StartMenuShortcutPath, headLaunchPath, head.DisplayName);
            createdShortcuts++;

            progress?.Report(new InstallProgressUpdate("Creating shortcuts", createdShortcuts, totalShortcuts));
            CreateShortcut(head.DesktopShortcutPath, headLaunchPath, head.DisplayName);
            createdShortcuts++;
        }

        progress?.Report(new InstallProgressUpdate("Registering uninstall entry"));
        RegisterUninstall(metadata, installedInstallerPath);
        progress?.Report(new InstallProgressUpdate("Registering Chummer link handler"));
        RegisterUrlProtocol(metadata);
        progress?.Report(new InstallProgressUpdate("Install complete"));
        TraceInstaller("install complete target=" + targetDir);
        return targetDir;
    }

    private static string InstallerTracePath
        => Path.Combine(Path.GetTempPath(), InstallerTraceFileName);

    private static void ResetInstallerTrace()
    {
        try
        {
            File.WriteAllText(
                InstallerTracePath,
                $"# Chummer installer trace {DateTimeOffset.UtcNow:O}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chummer installer could not reset trace: {ex.Message}");
        }
    }

    private static void TraceInstaller(string message)
    {
        try
        {
            File.AppendAllText(
                InstallerTracePath,
                $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chummer installer trace failed: {ex.Message}");
        }
    }

    private static string FormatTraceArguments(IReadOnlyList<string> args)
    {
        string[] redacted = new string[args.Count];
        bool redactNext = false;
        for (int i = 0; i < args.Count; i++)
        {
            string value = args[i];
            if (redactNext)
            {
                redacted[i] = "<redacted>";
                redactNext = false;
                continue;
            }

            if (string.Equals(value, ClaimCodeSwitch, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, InstallLinkCallbackSwitch, StringComparison.OrdinalIgnoreCase))
            {
                redacted[i] = value;
                redactNext = true;
                continue;
            }

            redacted[i] = value.StartsWith("chummer://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase)
                    ? "<redacted>"
                    : value;
        }

        return string.Join(' ', redacted);
    }

    private static void TraceProgress(InstallProgressUpdate update)
    {
        string completed = update.Completed?.ToString() ?? "";
        string total = update.Total?.ToString() ?? "";
        TraceInstaller($"progress stage=\"{update.Stage}\" completed={completed} total={total}");
    }

    private static int Uninstall(InstallerMetadata metadata)
    {
        foreach (InstalledHeadMetadata head in metadata.InstalledHeads)
        {
            RemoveShortcut(head.StartMenuShortcutPath);
            RemoveShortcut(head.DesktopShortcutPath);
        }

        UnregisterUninstall(metadata);
        UnregisterUrlProtocol();
        ScheduleDirectoryRemoval(metadata.InstallDirectory);
        MessageBox.Show(
            $"{metadata.DisplayName} is being removed from:\n{metadata.InstallDirectory}",
            "Uninstall Scheduled",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return 0;
    }

    private static void TryDeleteLegacyInstallDirectories(InstallerMetadata metadata)
    {
        foreach (string legacyDirectory in metadata.GetLegacyInstallDirectories())
        {
            if (!Directory.Exists(legacyDirectory))
            {
                continue;
            }

            try
            {
                Directory.Delete(legacyDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Chummer installer could not prune legacy install directory '{legacyDirectory}': {ex.Message}");
            }
        }
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

    private static string InstallPayload(
        InstallerMetadata metadata,
        string targetDirectory,
        string? payloadPathOverride,
        IProgress<InstallProgressUpdate>? progress = null)
    {
        string targetDir = Path.GetFullPath(targetDirectory);
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"chummer-installer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);
        try
        {
            ExtractPayload(tempExtractDir, payloadPathOverride, progress);

            if (metadata.UsesBundledLayout)
            {
                EnsureBundledLaunchExecutables(tempExtractDir, metadata.InstalledHeads);

                if (Directory.Exists(targetDir))
                {
                    progress?.Report(new InstallProgressUpdate("Replacing existing install"));
                    TryDeleteDirectory(targetDir, progress, "Removing previous install");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
                MoveOrCopyDirectory(tempExtractDir, targetDir, progress);
                return targetDir;
            }

            string payloadRoot = FindPayloadRoot(tempExtractDir, metadata.PrimaryHead.LaunchExecutable);
            EnsureLaunchExecutableInRoot(payloadRoot, metadata.PrimaryHead.LaunchExecutable);

            if (Directory.Exists(targetDir))
            {
                progress?.Report(new InstallProgressUpdate("Replacing existing install"));
                TryDeleteDirectory(targetDir, progress, "Removing previous install");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
            MoveOrCopyDirectory(payloadRoot, targetDir, progress);
            return targetDir;
        }
        finally
        {
            TryDeleteDirectory(tempExtractDir);
        }
    }

    private static void ExtractPayload(
        string tempExtractDir,
        string? payloadPathOverride,
        IProgress<InstallProgressUpdate>? progress)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        progress?.Report(new InstallProgressUpdate("Opening bundled desktop payload"));
        using Stream payload = OpenPayloadStream(assembly, payloadPathOverride);
        Stream archivePayload = PreparePayloadArchiveStream(payload, tempExtractDir, progress);
        bool leaveArchivePayloadOpen = ReferenceEquals(archivePayload, payload);

        using (ZipArchive archive = new(archivePayload, ZipArchiveMode.Read, leaveArchivePayloadOpen))
        {
            TraceInstaller($"extract archive entries={archive.Entries.Count}");
            string extractRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(tempExtractDir));
            ZipArchiveEntry[] fileEntries = archive.Entries
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToArray();
            int extractedFiles = 0;
            long totalBytes = Math.Max(1L, fileEntries.Sum(static entry => Math.Max(0L, entry.Length)));
            long extractedBytes = 0L;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string destinationPath = Path.GetFullPath(Path.Combine(tempExtractDir, entry.FullName));
                if (!destinationPath.StartsWith(extractRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Installer payload entry '{entry.FullName}' would extract outside '{tempExtractDir}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                using (Stream entryStream = entry.Open())
                using (FileStream destinationStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    CopyStreamWithProgress(
                        entryStream,
                        destinationStream,
                        entry.Length,
                        bytesCopied =>
                        {
                            long totalExtracted = Interlocked.Read(ref extractedBytes) + bytesCopied;
                            progress?.Report(new InstallProgressUpdate(
                                $"Extracting {Path.GetFileName(entry.FullName)} ({FormatBytes(totalExtracted)} of {FormatBytes(totalBytes)})",
                                ToProgressUnits(totalExtracted, totalBytes),
                                ProgressUnitScale));
                        });
                }

                Interlocked.Add(ref extractedBytes, Math.Max(0L, entry.Length));
                extractedFiles++;
                progress?.Report(new InstallProgressUpdate("Extracting application files", extractedFiles, fileEntries.Length));
            }
        }

        TraceInstaller("extract payload complete");
    }

    private static Stream PreparePayloadArchiveStream(
        Stream payload,
        string tempExtractDir,
        IProgress<InstallProgressUpdate>? progress)
    {
        long? payloadLength = TryGetStreamLength(payload);
        TraceInstaller($"payload stream canSeek={payload.CanSeek} length={payloadLength?.ToString() ?? "unknown"}");
        if (payload.CanSeek)
        {
            payload.Position = 0;
            progress?.Report(new InstallProgressUpdate("Reading packaged files"));
            return payload;
        }

        string payloadZipPath = Path.Combine(tempExtractDir, "payload.zip");
        progress?.Report(new InstallProgressUpdate("Caching packaged files"));
        FileStream zipFile = File.Create(payloadZipPath);
        try
        {
            CopyStreamWithProgress(
                payload,
                zipFile,
                payloadLength,
                bytesCopied => progress?.Report(new InstallProgressUpdate(
                    $"Caching packaged files ({FormatBytes(bytesCopied)} copied)",
                    ToProgressUnits(bytesCopied, payloadLength),
                    ProgressUnitScale)));
            zipFile.Position = 0;
            return zipFile;
        }
        catch
        {
            zipFile.Dispose();
            throw;
        }
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
            TraceInstaller($"opened appended payload length={payloadLength}");
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

    private static string? ResolveLaunchHeadId(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], LaunchHeadSwitch, StringComparison.OrdinalIgnoreCase))
            {
                string candidate = args[i + 1]?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string[] ResolveRelaunchArgs(string[] args)
    {
        List<string> values = [];
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], RelaunchArgSwitch, StringComparison.OrdinalIgnoreCase))
            {
                string candidate = args[i + 1] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    values.Add(candidate);
                }
            }
        }

        return values.ToArray();
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

    private static void EnsureBundledLaunchExecutables(string payloadRoot, IReadOnlyList<InstalledHeadMetadata> heads)
    {
        foreach (InstalledHeadMetadata head in heads)
        {
            string launchPath = head.ResolveLaunchPath(payloadRoot);
            if (File.Exists(launchPath))
            {
                continue;
            }

            string rootHint = string.IsNullOrWhiteSpace(head.RelativeRoot)
                ? payloadRoot
                : Path.Combine(payloadRoot, head.RelativeRoot);
            string topEntries = SummarizePayloadEntries(rootHint);
            throw new InvalidOperationException(
                $"The bundled desktop payload did not contain '{head.LaunchExecutable}' for head '{head.HeadId}'. " +
                $"Expected '{launchPath}'. Payload sample: {topEntries}");
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

    private static void CopyDirectory(
        string sourceDir,
        string targetDir,
        IProgress<InstallProgressUpdate>? progress = null)
    {
        Directory.CreateDirectory(targetDir);
        string[] directories = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
        foreach (string directory in directories)
        {
            string relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        int copiedFiles = 0;
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (FileStream sourceStream = File.OpenRead(file))
            using (FileStream destinationStream = new(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                CopyStreamWithProgress(
                    sourceStream,
                    destinationStream,
                    sourceStream.Length,
                    bytesCopied => progress?.Report(new InstallProgressUpdate(
                        $"Copying {Path.GetFileName(file)}",
                        copiedFiles * ProgressUnitScale + ToProgressUnits(bytesCopied, Math.Max(1L, sourceStream.Length)),
                        Math.Max(1, files.Length) * ProgressUnitScale)));
            }

            copiedFiles++;
            progress?.Report(new InstallProgressUpdate("Copying application files", copiedFiles, files.Length));
        }
    }

    private static void MoveOrCopyDirectory(
        string sourceDir,
        string targetDir,
        IProgress<InstallProgressUpdate>? progress = null)
    {
        progress?.Report(new InstallProgressUpdate("Installing application files", 0, 1));
        try
        {
            Directory.Move(sourceDir, targetDir);
            progress?.Report(new InstallProgressUpdate("Installing application files", 1, 1));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Directory.CreateDirectory(targetDir);
            CopyDirectory(sourceDir, targetDir, progress);
        }
    }

    private static string EnsureTrailingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private const int ProgressUnitScale = 1000;

    private static int ToProgressUnits(long completed, long? total)
    {
        if (total is null or <= 0)
        {
            return 0;
        }

        decimal ratio = Math.Clamp((decimal)completed / total.Value, 0m, 1m);
        return (int)Math.Round(ratio * ProgressUnitScale, MidpointRounding.AwayFromZero);
    }

    private static long? TryGetStreamLength(Stream stream)
    {
        try
        {
            return stream.CanSeek ? stream.Length : null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = Math.Max(0L, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {units[unit]}"
            : $"{value:0.0} {units[unit]}";
    }

    private static void CopyStreamWithProgress(
        Stream source,
        Stream destination,
        long? totalBytes,
        Action<long>? reportCopiedBytes)
    {
        byte[] buffer = new byte[1024 * 1024];
        long copied = 0L;
        int lastReportedUnit = -1;

        while (true)
        {
            int bytesRead = source.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                break;
            }

            destination.Write(buffer, 0, bytesRead);
            copied += bytesRead;

            int currentUnit = ToProgressUnits(copied, totalBytes);
            if (currentUnit == lastReportedUnit && copied != totalBytes)
            {
                continue;
            }

            lastReportedUnit = currentUnit;
            reportCopiedBytes?.Invoke(copied);
        }

        reportCopiedBytes?.Invoke(copied);
        TraceInstaller($"copy stream complete bytes={copied} total={totalBytes?.ToString() ?? "unknown"}");
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

    private static void TryDeleteDirectory(
        string path,
        IProgress<InstallProgressUpdate>? progress = null,
        string stage = "Removing temporary files")
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            string[] directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                .OrderByDescending(static directory => directory.Length)
                .ToArray();
            int total = Math.Max(1, files.Length + directories.Length + 1);
            int completed = 0;

            progress?.Report(new InstallProgressUpdate(stage, completed, total));
            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                completed++;
                progress?.Report(new InstallProgressUpdate(stage, completed, total));
            }

            foreach (string directory in directories)
            {
                Directory.Delete(directory, recursive: false);
                completed++;
                progress?.Report(new InstallProgressUpdate(stage, completed, total));
            }

            Directory.Delete(path, recursive: false);
            progress?.Report(new InstallProgressUpdate(stage, total, total));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not replace the existing installation at '{path}'. Close Chummer and try again.\n\n{ex.Message}");
        }
    }

    private static DialogResult PromptForInstalledHeadLaunch(InstallerMetadata metadata, string targetDir)
    {
        if (metadata.InstalledHeads.Count > 1)
        {
            return PromptForInstalledHeadLaunchWithButtons(
                metadata.DisplayName,
                "Chummer is ready.",
                BuildInstalledPathText(targetDir),
                (
                    "Open Chummer",
                    "Open Blazor Desktop",
                    "Close",
                    "Recommended desktop app.",
                    "Use only when support asks.",
                    "Close installer"));
        }

        return PromptForInstalledHeadLaunchWithButtons(
            metadata.DisplayName,
            "Chummer is ready.",
            BuildInstalledPathText(targetDir),
            (
                "Open Chummer",
                "Close",
                null,
                "Ready to use.",
                "Close installer.",
                null));
    }

    private static string BuildInstalledPathText(string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            return "Installed.";
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetDir.Trim());
        }
        catch
        {
            fullPath = targetDir.Trim();
        }

        string[] parts = fullPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 3)
        {
            return $"Installed to {fullPath}";
        }

        string compactTail = string.Join(Path.DirectorySeparatorChar, parts.TakeLast(3));
        return $"Installed to ...{Path.DirectorySeparatorChar}{compactTail}";
    }

    private static DialogResult PromptForInstalledHeadLaunchWithButtons(
        string displayName,
        string headline,
        string pathText,
        (
            string PrimaryButtonText,
            string SecondaryButtonText,
            string? CancelButtonText,
            string PrimaryFootnote,
            string SecondaryFootnote,
            string? _CancelFootnote
        ) options)
    {
        using Form prompt = new()
        {
            Text = $"{displayName} Installer - Install Complete",
            Name = "ChummerInstallerCompletionDialog",
            AccessibleName = $"{displayName} install complete",
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            ClientSize = new Size(860, 420),
            MinimumSize = new Size(860, 420),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = true,
            TopMost = true,
            ShowIcon = false,
            BackColor = Color.FromArgb(9, 13, 20),
            AutoScaleMode = AutoScaleMode.Dpi
        };

        void CompletePrompt(DialogResult result)
        {
            prompt.DialogResult = result;
            prompt.Close();
        }

        Panel accentBar = new()
        {
            Dock = DockStyle.Top,
            Height = 3,
            BackColor = Color.FromArgb(57, 196, 156)
        };

        Label stateLabel = new()
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 6.75F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(116, 223, 193),
            Text = "INSTALLED",
            Dock = DockStyle.Fill,
            Height = 18,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        Label titleLabel = new()
        {
            AutoSize = false,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.White,
            Text = headline,
            Dock = DockStyle.Fill,
            Height = 42,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 0),
            AutoEllipsis = true,
            UseMnemonic = false
        };

        Label pathLabel = new()
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(205, 213, 226),
            Text = pathText,
            Dock = DockStyle.Fill,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 0),
            AutoEllipsis = true,
            MaximumSize = new Size(0, 0),
            UseMnemonic = false
        };

        string noteText = options.CancelButtonText is null
            ? options.PrimaryFootnote
            : $"{options.PrimaryFootnote} {options.SecondaryFootnote}".Trim();

        Label noteLabel = new()
        {
            AutoSize = false,
            Font = new Font("Segoe UI", 7.25F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(156, 169, 190),
            Text = noteText,
            Dock = DockStyle.Fill,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 0, 0),
            AutoEllipsis = true,
            UseMnemonic = false
        };

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 18, 0, 0),
            Height = 62,
            WrapContents = false
        };

        Button primaryButton = new()
        {
            Text = options.PrimaryButtonText,
            AutoSize = false,
            Size = new Size(164, 38),
            MinimumSize = new Size(164, 38),
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point),
            DialogResult = DialogResult.Yes,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(14, 0, 14, 2),
            BackColor = Color.FromArgb(57, 196, 156),
            ForeColor = Color.FromArgb(8, 13, 20),
            FlatStyle = FlatStyle.Flat,
            AutoEllipsis = true,
            UseMnemonic = false
        };
        primaryButton.FlatAppearance.BorderSize = 0;
        primaryButton.Click += (_, _) =>
        {
            CompletePrompt(DialogResult.Yes);
        };

        Button secondaryButton = new()
        {
            Text = options.SecondaryButtonText,
            AutoSize = false,
            Size = new Size(164, 38),
            MinimumSize = new Size(164, 38),
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point),
            DialogResult = DialogResult.No,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(14, 0, 14, 2),
            BackColor = Color.FromArgb(24, 31, 44),
            ForeColor = Color.FromArgb(224, 231, 242),
            FlatStyle = FlatStyle.Flat,
            AutoEllipsis = true,
            UseMnemonic = false
        };
        secondaryButton.FlatAppearance.BorderColor = Color.FromArgb(55, 66, 84);
        secondaryButton.Click += (_, _) =>
        {
            CompletePrompt(DialogResult.No);
        };

        Button cancelButton = new()
        {
            Text = options.CancelButtonText ?? "Cancel",
            AutoSize = false,
            Size = new Size(164, 38),
            MinimumSize = new Size(164, 38),
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point),
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(8, 0, 0, 0),
            Padding = new Padding(14, 0, 14, 2),
            BackColor = Color.FromArgb(24, 31, 44),
            ForeColor = Color.FromArgb(224, 231, 242),
            FlatStyle = FlatStyle.Flat,
            Visible = options.CancelButtonText is not null,
            AutoEllipsis = true,
            UseMnemonic = false
        };
        cancelButton.FlatAppearance.BorderColor = Color.FromArgb(55, 66, 84);
        cancelButton.Click += (_, _) =>
        {
            CompletePrompt(DialogResult.Cancel);
        };

        actions.Controls.Add(cancelButton);
        actions.Controls.Add(secondaryButton);
        actions.Controls.Add(primaryButton);

        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(14, 19, 28),
            Padding = new Padding(32, 24, 32, 24),
            ColumnCount = 1,
            RowCount = 5
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        content.Controls.Add(stateLabel, 0, 0);
        content.Controls.Add(titleLabel, 0, 1);
        content.Controls.Add(pathLabel, 0, 2);
        content.Controls.Add(noteLabel, 0, 3);
        content.Controls.Add(actions, 0, 4);

        prompt.Controls.Add(content);
        prompt.Controls.Add(accentBar);
        prompt.AcceptButton = primaryButton;
        prompt.CancelButton = options.CancelButtonText is null ? secondaryButton : cancelButton;
        prompt.StartPosition = FormStartPosition.CenterScreen;
        prompt.FormClosing += (_, _) =>
        {
            if (prompt.DialogResult == DialogResult.None)
            {
                prompt.DialogResult = DialogResult.Cancel;
            }
        };

        if (!string.IsNullOrWhiteSpace(options.PrimaryFootnote))
        {
            noteLabel.Visible = true;
        }

        prompt.Shown += (_, _) =>
        {
            prompt.Activate();
            prompt.BringToFront();
        };

        TraceInstaller("showing completion prompt title=" + prompt.Text);
        DialogResult result = prompt.ShowDialog();
        TraceInstaller("completion prompt result=" + result);
        return result == DialogResult.None ? DialogResult.Cancel : result;
    }

    private static void LaunchInstalledApp(
        InstallerMetadata metadata,
        string? claimCode,
        string? requestedLaunchHeadId,
        IReadOnlyList<string> relaunchArgs,
        DialogResult? launchChoice)
    {
        InstalledHeadMetadata head = ResolveLaunchHead(metadata, requestedLaunchHeadId, launchChoice);
        string target = head.ResolveLaunchPath(metadata.InstallDirectory);
        ProcessStartInfo startInfo = new()
        {
            FileName = target,
            WorkingDirectory = Path.GetDirectoryName(target) ?? metadata.InstallDirectory,
            UseShellExecute = true,
        };

        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (!string.IsNullOrWhiteSpace(normalizedClaimCode))
        {
            startInfo.ArgumentList.Add(ClaimCodeSwitch);
            startInfo.ArgumentList.Add(normalizedClaimCode);
        }

        foreach (string arg in relaunchArgs)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        using InstallSplashForm launchSplash = new(metadata.DisplayName);
        launchSplash.Show();
        launchSplash.ApplyProgress(new InstallProgressUpdate($"Starting {head.DisplayName}"));
        Application.DoEvents();

        Process? process = Process.Start(startInfo);
        if (process is null)
        {
            launchSplash.ApplyProgress(new InstallProgressUpdate("Launch requested"));
            PumpLaunchSplash(launchSplash, TimeSpan.FromSeconds(2));
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!process.HasExited && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            try
            {
                if (process.WaitForInputIdle(250))
                {
                    launchSplash.ApplyProgress(new InstallProgressUpdate("Chummer is starting"));
                    PumpLaunchSplash(launchSplash, TimeSpan.FromMilliseconds(600));
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                break;
            }

            launchSplash.ApplyProgress(new InstallProgressUpdate($"Starting {head.DisplayName} ({Math.Max(1, (int)stopwatch.Elapsed.TotalSeconds)}s)"));
            Application.DoEvents();
        }

        launchSplash.ApplyProgress(new InstallProgressUpdate("Chummer is starting"));
        PumpLaunchSplash(launchSplash, TimeSpan.FromSeconds(2));
    }

    private static InstalledHeadMetadata ResolveLaunchHead(
        InstallerMetadata metadata,
        string? requestedLaunchHeadId,
        DialogResult? launchChoice)
    {
        if (!string.IsNullOrWhiteSpace(requestedLaunchHeadId))
        {
            InstalledHeadMetadata? requested = metadata.InstalledHeads
                .FirstOrDefault(head => string.Equals(head.HeadId, requestedLaunchHeadId, StringComparison.OrdinalIgnoreCase));
            if (requested is not null)
            {
                return requested;
            }
        }

        return launchChoice == DialogResult.No && metadata.InstalledHeads.Count > 1
            ? metadata.InstalledHeads[1]
            : metadata.PrimaryHead;
    }

    private static void PumpLaunchSplash(Form form, TimeSpan duration)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            Application.DoEvents();
            Thread.Sleep(50);
        }

        form.Close();
    }

    private static void StagePendingClaimCode(InstallerMetadata metadata, string claimCode)
    {
        string? normalizedClaimCode = NormalizeClaimCode(claimCode);
        if (string.IsNullOrWhiteSpace(normalizedClaimCode))
        {
            return;
        }

        foreach (InstalledHeadMetadata head in metadata.InstalledHeads)
        {
            string pendingPath = GetPendingClaimCodePath(head.HeadId);
            Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);
            File.WriteAllText(pendingPath, normalizedClaimCode, Encoding.UTF8);
        }
    }

    private static string GetPendingClaimCodePath(string headId)
        => Path.Combine(
            ResolveDesktopStateRoot(),
            "install-linking",
            headId,
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
        string launchPath = metadata.PrimaryHead.ResolveLaunchPath(metadata.InstallDirectory);
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

    private static void RegisterUrlProtocol(InstallerMetadata metadata)
    {
        string launchPath = metadata.PrimaryHead.ResolveLaunchPath(metadata.InstallDirectory);
        using RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ChummerProtocolScheme}", writable: true)
            ?? throw new InvalidOperationException("Could not create Chummer protocol registry entry.");
        protocolKey.SetValue(string.Empty, "URL: Chummer Protocol");
        protocolKey.SetValue("URL Protocol", string.Empty);

        using RegistryKey defaultIconKey = protocolKey.CreateSubKey("DefaultIcon", writable: true)
            ?? throw new InvalidOperationException("Could not create Chummer protocol icon registry entry.");
        defaultIconKey.SetValue(string.Empty, ResolveShortcutIconPath(launchPath));

        using RegistryKey commandKey = protocolKey
            .CreateSubKey(@"shell\open\command", writable: true)
            ?? throw new InvalidOperationException("Could not create Chummer protocol command registry entry.");
        commandKey.SetValue(string.Empty, $"\"{launchPath}\" {InstallLinkCallbackSwitch} \"%1\"");
    }

    private static void UnregisterUninstall(InstallerMetadata metadata)
    {
        Registry.CurrentUser.DeleteSubKeyTree(metadata.UninstallRegistryKeyPath, throwOnMissingSubKey: false);
    }

    private static void UnregisterUrlProtocol()
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ChummerProtocolScheme}", throwOnMissingSubKey: false);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string description)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            CreateShortcutCore(shortcutPath, targetPath, description);
            return;
        }

        Exception? shortcutError = null;
        Thread shortcutThread = new(() =>
        {
            try
            {
                CreateShortcutCore(shortcutPath, targetPath, description);
            }
            catch (Exception ex)
            {
                shortcutError = ex;
            }
        })
        {
            IsBackground = true,
            Name = "Chummer shortcut creation"
        };
        shortcutThread.SetApartmentState(ApartmentState.STA);
        shortcutThread.Start();
        if (!shortcutThread.Join(TimeSpan.FromSeconds(15)))
        {
            throw new TimeoutException($"Timed out while creating shortcut '{shortcutPath}'.");
        }
        if (shortcutError is not null)
        {
            throw new InvalidOperationException($"Could not create shortcut '{shortcutPath}'.", shortcutError);
        }
    }

    private static void CreateShortcutCore(string shortcutPath, string targetPath, string description)
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
        string InstallerOutputName,
        IReadOnlyList<InstalledHeadMetadata> InstalledHeads)
    {
        public InstalledHeadMetadata PrimaryHead => InstalledHeads[0];
        public bool UsesBundledLayout => InstalledHeads.Any(head => !string.IsNullOrWhiteSpace(head.RelativeRoot));

        public string InstallRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Chummer6");

        public string InstallDirectory =>
            Path.Combine(InstallRoot, InstallDirName);

        public string UninstallRegistryKeyPath => $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Chummer6.{AppId}";

        public IReadOnlyList<string> GetLegacyInstallDirectories()
        {
            if (!UsesBundledLayout
                || !InstallDirName.StartsWith("Desktop-", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            string ridSuffix = InstallDirName["Desktop-".Length..];
            if (string.IsNullOrWhiteSpace(ridSuffix))
            {
                return [];
            }

            string[] candidates =
            [
                Path.Combine(InstallRoot, $"AvaloniaDesktop-{ridSuffix}"),
                Path.Combine(InstallRoot, $"BlazorDesktop-{ridSuffix}")
            ];

            return candidates
                .Where(candidate => !PathEquals(candidate, InstallDirectory))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static InstallerMetadata Load()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? ReadOptional(string key)
            {
                return assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(attr => string.Equals(attr.Key, key, StringComparison.Ordinal))?
                    .Value;
            }

            string Read(string key, string fallback)
                => ReadOptional(key) ?? fallback;

            string appId = Read("ChummerAppId", "avalonia");
            string headId = Read("ChummerHeadId", ResolveHeadId(appId));
            string displayName = Read("ChummerDisplayName", "Chummer6");
            string launchExecutable = Read("ChummerLaunchExecutable", "Chummer.Avalonia.exe");
            string shortcutName = Read("ChummerShortcutName", "Chummer6");
            return new InstallerMetadata(
                AppId: appId,
                HeadId: headId,
                DisplayName: displayName,
                InstallDirName: Read("ChummerInstallDirName", "Chummer6"),
                LaunchExecutable: launchExecutable,
                Version: Read("ChummerVersion", "unpublished"),
                Publisher: Read("ChummerPublisher", "ArchonMegalon"),
                ShortcutName: shortcutName,
                InstallerOutputName: Read("ChummerInstallerOutputName", "Chummer6Installer"),
                InstalledHeads: ReadInstalledHeads(
                    ReadOptional("ChummerInstallerHeadsJsonBase64"),
                    headId,
                    displayName,
                    launchExecutable,
                    shortcutName));
        }

        private static IReadOnlyList<InstalledHeadMetadata> ReadInstalledHeads(
            string? encodedHeads,
            string fallbackHeadId,
            string fallbackDisplayName,
            string fallbackLaunchExecutable,
            string fallbackShortcutName)
        {
            if (!string.IsNullOrWhiteSpace(encodedHeads))
            {
                try
                {
                    byte[] payloadBytes = Convert.FromBase64String(encodedHeads);
                    InstalledHeadDescriptor[]? descriptors = JsonSerializer.Deserialize<InstalledHeadDescriptor[]>(payloadBytes);
                    if (descriptors is { Length: > 0 })
                    {
                        InstalledHeadMetadata[] heads = descriptors
                            .Where(static descriptor =>
                                !string.IsNullOrWhiteSpace(descriptor.HeadId)
                                && !string.IsNullOrWhiteSpace(descriptor.LaunchExecutable)
                                && !string.IsNullOrWhiteSpace(descriptor.ShortcutName))
                            .Select(descriptor => new InstalledHeadMetadata(
                                HeadId: descriptor.HeadId!.Trim(),
                                DisplayName: string.IsNullOrWhiteSpace(descriptor.DisplayName) ? descriptor.HeadId!.Trim() : descriptor.DisplayName.Trim(),
                                LaunchExecutable: descriptor.LaunchExecutable!.Trim(),
                                ShortcutName: descriptor.ShortcutName!.Trim(),
                                RelativeRoot: (descriptor.RelativeRoot ?? string.Empty).Trim()))
                            .ToArray();

                        if (heads.Length > 0)
                        {
                            return heads;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Installer head metadata was malformed.", ex);
                }
            }

            return
            [
                new InstalledHeadMetadata(
                    HeadId: fallbackHeadId,
                    DisplayName: fallbackDisplayName,
                    LaunchExecutable: fallbackLaunchExecutable,
                    ShortcutName: fallbackShortcutName,
                    RelativeRoot: string.Empty)
            ];
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

        private static bool PathEquals(string left, string right)
        {
            static string Normalize(string path)
                => Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record InstalledHeadMetadata(
        string HeadId,
        string DisplayName,
        string LaunchExecutable,
        string ShortcutName,
        string RelativeRoot)
    {
        public string StartMenuShortcutPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                $"{ShortcutName}.lnk");

        public string DesktopShortcutPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{ShortcutName}.lnk");

        public string ResolveLaunchPath(string installDirectory)
            => string.IsNullOrWhiteSpace(RelativeRoot)
                ? Path.Combine(installDirectory, LaunchExecutable)
                : Path.Combine(installDirectory, RelativeRoot, LaunchExecutable);
    }

    private sealed record InstalledHeadDescriptor(
        string? HeadId,
        string? DisplayName,
        string? LaunchExecutable,
        string? ShortcutName,
        string? RelativeRoot);

    private readonly record struct InstallProgressUpdate(
        string Stage,
        int? Completed = null,
        int? Total = null);

    private sealed class InstallSplashForm : Form
    {
        private readonly Label _statusLabel;
        private readonly Label _elapsedLabel;
        private readonly Panel _progressTrack;
        private readonly Panel _progressFill;
        private readonly Label _progressValueLabel;

        public InstallSplashForm(string displayName)
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(860, 420);
            MinimumSize = new Size(860, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = $"{displayName} Installer - Installing";
            Name = "ChummerInstallerProgressDialog";
            AccessibleName = $"{displayName} installer progress";
            BackColor = Color.FromArgb(9, 13, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            Panel accentBar = new()
            {
                Dock = DockStyle.Top,
                Height = 3,
                BackColor = Color.FromArgb(57, 196, 156)
            };

            Panel surface = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(32, 24, 32, 24),
                BackColor = Color.FromArgb(14, 19, 28)
            };

            Panel glyphTile = new()
            {
                Size = new Size(56, 56),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(22, 28, 40)
            };

            PictureBox appGlyph = new()
            {
                Size = new Size(42, 42),
                Location = new Point(7, 7),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent
            };
            try
            {
                appGlyph.Image = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)?.ToBitmap();
            }
            catch
            {
                appGlyph.Visible = false;
            }
            glyphTile.Controls.Add(appGlyph);

            Label eyebrowLabel = new()
            {
                AutoSize = false,
                Text = "INSTALLER",
                Font = new Font("Segoe UI Semibold", 6.75F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(116, 223, 193),
                Dock = DockStyle.Top,
                Height = 12,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 0, 2)
            };

            Label titleLabel = new()
            {
                AutoSize = false,
                Text = displayName,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 0, 0, 2),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            Label copyLabel = new()
            {
                AutoSize = false,
                Text = "Shortcuts and first launch are prepared automatically.",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(207, 216, 230),
                Dock = DockStyle.Top,
                Height = 34,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 10),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Text = "Preparing installer",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 38,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 8),
                AutoEllipsis = true,
                UseMnemonic = false
            };

            _progressTrack = new Panel
            {
                Dock = DockStyle.Top,
                Height = 12,
                BackColor = Color.FromArgb(33, 42, 58),
                Margin = new Padding(0, 0, 0, 8)
            };

            _progressFill = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(57, 196, 156)
            };
            _progressTrack.Controls.Add(_progressFill);

            Panel progressMetaRow = new()
            {
                Dock = DockStyle.Top,
                Height = 24,
                Margin = new Padding(0, 0, 0, 8)
            };

            _elapsedLabel = new Label
            {
                AutoSize = false,
                Text = "Elapsed: 0s",
                Font = new Font("Segoe UI", 7.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(178, 190, 208),
                Dock = DockStyle.Left,
                Width = 160,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _progressValueLabel = new Label
            {
                AutoSize = false,
                Text = "Preparing…",
                Font = new Font("Segoe UI", 7.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(178, 190, 208),
                Dock = DockStyle.Right,
                Width = 96,
                Height = 22,
                TextAlign = ContentAlignment.MiddleRight
            };

            progressMetaRow.Controls.Add(_progressValueLabel);
            progressMetaRow.Controls.Add(_elapsedLabel);

            Label hintLabel = new()
            {
                AutoSize = false,
                Text = "This may take a few minutes on slower systems.",
                Font = new Font("Segoe UI", 7.25F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(146, 160, 180),
                Dock = DockStyle.Top,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            Panel textColumn = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 0, 0, 0)
            };
            textColumn.Controls.Add(copyLabel);
            textColumn.Controls.Add(titleLabel);
            textColumn.Controls.Add(eyebrowLabel);

            Panel heroRow = new()
            {
                Dock = DockStyle.Top,
                Height = 104
            };
            heroRow.Controls.Add(textColumn);
            heroRow.Controls.Add(glyphTile);

            Panel body = new()
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0)
            };

            body.Controls.Add(hintLabel);
            body.Controls.Add(_progressTrack);
            body.Controls.Add(progressMetaRow);
            body.Controls.Add(_statusLabel);
            body.Controls.Add(heroRow);
            surface.Controls.Add(body);
            Controls.Add(surface);
            Controls.Add(accentBar);
        }

        public void ApplyElapsed(TimeSpan elapsed)
        {
            string elapsedText = elapsed.TotalMinutes >= 1
                ? $"Elapsed: {(int)elapsed.TotalMinutes}m {elapsed.Seconds:00}s"
                : $"Elapsed: {Math.Max(0, (int)elapsed.TotalSeconds)}s";
            _elapsedLabel.Text = elapsedText;
        }

        public void ApplyProgress(InstallProgressUpdate update)
        {
            int? total = update.Total is > 0 ? update.Total : null;
            int? completed = update.Completed;
            string statusText = BuildProgressDisplayStage(update.Stage);
            if (total.HasValue && completed.HasValue && ShouldShowInlineCount(total.Value))
            {
                statusText = $"{statusText} ({Math.Min(completed.Value, total.Value)}/{total.Value})";
            }

            _statusLabel.Text = statusText;

            if (total.HasValue)
            {
                int boundedTotal = Math.Max(1, total.Value);
                int boundedCompleted = Math.Max(0, Math.Min(completed ?? 0, boundedTotal));
                int percent = (int)Math.Round((double)boundedCompleted / boundedTotal * 100d, MidpointRounding.AwayFromZero);
                int trackWidth = Math.Max(1, _progressTrack.ClientSize.Width);
                _progressFill.Width = Math.Max(8, (int)Math.Round(trackWidth * percent / 100d, MidpointRounding.AwayFromZero));
                _progressValueLabel.Text = $"{percent}%";
                return;
            }

            int pulse = 96 + Math.Abs((int)(Environment.TickCount64 % 180) - 90) * 2;
            int pulsingTrackWidth = Math.Max(1, _progressTrack.ClientSize.Width);
            _progressFill.Width = Math.Min(pulsingTrackWidth, pulse);
            _progressValueLabel.Text = "Preparing…";
        }

        private static string BuildProgressDisplayStage(string stage)
        {
            if (stage.StartsWith("Extracting ", StringComparison.OrdinalIgnoreCase)
                && stage.Contains(" MB of ", StringComparison.OrdinalIgnoreCase))
            {
                return "Extracting application files";
            }

            if (stage.StartsWith("Copying ", StringComparison.OrdinalIgnoreCase)
                && stage.Contains(" MB of ", StringComparison.OrdinalIgnoreCase))
            {
                return "Copying application files";
            }

            return stage;
        }

        private static bool ShouldShowInlineCount(int total)
        {
            if (total <= 0)
            {
                return false;
            }

            return total < ProgressUnitScale || total % ProgressUnitScale != 0;
        }
    }
}
