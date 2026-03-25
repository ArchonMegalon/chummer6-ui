using System.Runtime.InteropServices;

namespace Chummer.Desktop.Runtime;

internal static class DesktopStateRootResolver
{
    private const string ExplicitStateRootEnvironmentVariable = "CHUMMER_DESKTOP_STATE_ROOT";

    public static string Resolve(string productDirectoryName, string fallbackProductDirectoryName)
    {
        if (TryResolveExplicitStateRoot(out string? explicitRoot))
        {
            ArgumentNullException.ThrowIfNull(explicitRoot);
            return Path.Combine(explicitRoot, productDirectoryName);
        }

        if (TryResolveXdgDataRoot(out string? xdgRoot))
        {
            ArgumentNullException.ThrowIfNull(xdgRoot);
            return Path.Combine(xdgRoot, productDirectoryName);
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, productDirectoryName);
        }

        return Path.Combine(Path.GetTempPath(), fallbackProductDirectoryName);
    }

    private static bool TryResolveExplicitStateRoot(out string? stateRoot)
    {
        string? configured = Environment.GetEnvironmentVariable(ExplicitStateRootEnvironmentVariable);
        return TryNormalizeDirectory(configured, out stateRoot);
    }

    private static bool TryResolveXdgDataRoot(out string? dataRoot)
    {
        dataRoot = null;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (TryNormalizeDirectory(xdgDataHome, out dataRoot))
        {
            return true;
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (!TryNormalizeDirectory(home, out string? normalizedHome))
        {
            return false;
        }

        ArgumentNullException.ThrowIfNull(normalizedHome);
        dataRoot = Path.Combine(normalizedHome, ".local", "share");
        return true;
    }

    private static bool TryNormalizeDirectory(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
        return true;
    }
}
