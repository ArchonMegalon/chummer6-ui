using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Chummer.Desktop.Installer;

internal static class Program
{
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
            EnsureLaunchExecutableExists(tempExtractDir, metadata);

            if (Directory.Exists(targetDir))
            {
                TryDeleteDirectory(targetDir);
            }

            Directory.CreateDirectory(targetDir);
            CopyDirectory(tempExtractDir, targetDir);
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
        using Stream? payload = assembly.GetManifestResourceStream("ChummerInstaller.Payload.zip");
        if (payload is null)
        {
            throw new InvalidOperationException("Embedded desktop payload was not found.");
        }

        string zipPath = Path.Combine(tempExtractDir, "payload.zip");
        using (FileStream zipFile = File.Create(zipPath))
        {
            payload.CopyTo(zipFile);
        }

        ZipFile.ExtractToDirectory(zipPath, tempExtractDir, overwriteFiles: true);
        File.Delete(zipPath);
    }

    private static void EnsureLaunchExecutableExists(string tempExtractDir, InstallerMetadata metadata)
    {
        string launchPath = Path.Combine(tempExtractDir, metadata.LaunchExecutable);
        if (!File.Exists(launchPath))
        {
            throw new InvalidOperationException(
                $"The bundled desktop payload did not contain '{metadata.LaunchExecutable}'.");
        }
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
