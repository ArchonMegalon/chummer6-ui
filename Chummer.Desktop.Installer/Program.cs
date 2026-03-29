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
            if (args.Length > 0 && string.Equals(args[0], "--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                return Uninstall(metadata);
            }

            if (args.Length > 1 && string.Equals(args[0], "--smoke-install", StringComparison.OrdinalIgnoreCase))
            {
                return SmokeInstall(metadata, args[1]);
            }

            return Install(metadata);
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

    private static int Install(InstallerMetadata metadata)
    {
        string targetDir = InstallPayload(metadata, metadata.InstallDirectory);
        string installedInstallerPath = Path.Combine(targetDir, metadata.InstallerOutputName + ".exe");
        File.Copy(Environment.ProcessPath!, installedInstallerPath, overwrite: true);

        CreateShortcut(metadata.ShortcutPath, Path.Combine(targetDir, metadata.LaunchExecutable), metadata.DisplayName);
        CreateShortcut(metadata.DesktopShortcutPath, Path.Combine(targetDir, metadata.LaunchExecutable), metadata.DisplayName);
        RegisterUninstall(metadata, installedInstallerPath);

        DialogResult launch = MessageBox.Show(
            $"{metadata.DisplayName} installed to:\n{targetDir}\n\nYes: launch now\nNo: finish without launching",
            "Install Complete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (launch == DialogResult.Yes)
        {
            LaunchInstalledApp(metadata);
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

    private static int SmokeInstall(InstallerMetadata metadata, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Smoke install requires a target directory.");
        }

        InstallPayload(metadata, targetDirectory);
        return 0;
    }

    private static string InstallPayload(InstallerMetadata metadata, string targetDirectory)
    {
        string targetDir = Path.GetFullPath(targetDirectory);
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"chummer-installer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);
        try
        {
            ExtractPayload(tempExtractDir);
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

    private static void ExtractPayload(string tempExtractDir)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string payloadZipPath = Path.Combine(tempExtractDir, "payload.zip");

        using Stream payload = OpenPayloadStream(assembly);

        using (FileStream zipFile = File.Create(payloadZipPath))
        {
            payload.CopyTo(zipFile);
        }

        ZipFile.ExtractToDirectory(payloadZipPath, tempExtractDir, overwriteFiles: true);
        File.Delete(payloadZipPath);
    }

    private static Stream OpenPayloadStream(Assembly assembly)
    {
        string? embeddedPayloadResource = FindPayloadResource(assembly);
        if (embeddedPayloadResource is not null)
        {
            Stream? stream = assembly.GetManifestResourceStream(embeddedPayloadResource);
            if (stream is null)
            {
                string? names = FormatResourceNames(assembly);
                throw new InvalidOperationException(
                    $"Embedded desktop payload '{embeddedPayloadResource}' could not be opened. Embedded resources: {names}.");
            }

            return stream;
        }

        string baseDirectory = AppContext.BaseDirectory;
        string[] sidecarPayloads = Directory
            .EnumerateFiles(baseDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Where(name => IsPayloadZipName(Path.GetFileName(name)))
            .OrderBy(name => Path.GetFileName(name), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sidecarPayloads.Length > 0)
        {
            return File.OpenRead(sidecarPayloads[0]);
        }

        string resourceNames = FormatResourceNames(assembly);
        string? sidecarSummary = sidecarPayloads.Length > 0
            ? string.Join(", ", sidecarPayloads.Select(Path.GetFileName))
            : "<none>";
        throw new InvalidOperationException(
            $"Embedded desktop payload was not found. Expected '{PreferredPayloadResourceName}'. " +
            $"Embedded resources: {resourceNames}. " +
            $"Checked {baseDirectory} for sidecar payloads: {sidecarSummary}.");
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

    private static void LaunchInstalledApp(InstallerMetadata metadata)
    {
        string target = Path.Combine(metadata.InstallDirectory, metadata.LaunchExecutable);
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = metadata.InstallDirectory,
            UseShellExecute = true,
        });
    }

    private static void RegisterUninstall(InstallerMetadata metadata, string installerPath)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(metadata.UninstallRegistryKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not create uninstall registry entry.");
        key.SetValue("DisplayName", metadata.DisplayName);
        key.SetValue("DisplayVersion", metadata.Version);
        key.SetValue("Publisher", metadata.Publisher);
        key.SetValue("InstallLocation", metadata.InstallDirectory);
        key.SetValue("DisplayIcon", Path.Combine(metadata.InstallDirectory, metadata.LaunchExecutable));
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
            shortcut.IconLocation = targetPath;
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

            return new InstallerMetadata(
                AppId: Read("ChummerAppId", "avalonia"),
                DisplayName: Read("ChummerDisplayName", "Chummer6"),
                InstallDirName: Read("ChummerInstallDirName", "Chummer6"),
                LaunchExecutable: Read("ChummerLaunchExecutable", "Chummer.Avalonia.exe"),
                Version: Read("ChummerVersion", "unpublished"),
                Publisher: Read("ChummerPublisher", "ArchonMegalon"),
                ShortcutName: Read("ChummerShortcutName", "Chummer6"),
                InstallerOutputName: Read("ChummerInstallerOutputName", "Chummer6Installer"));
        }
    }
}
