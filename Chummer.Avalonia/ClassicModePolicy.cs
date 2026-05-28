using System.Reflection;

namespace Chummer.Avalonia;

public enum DesktopUiMode
{
    Classic,
    Modern,
    SupportRecovery,
    Developer
}

internal static class ClassicModePolicy
{
    private const string DesktopModeEnvironmentVariable = "CHUMMER_DESKTOP_MODE";
    private const string ReleaseChannelEnvironmentVariable = "CHUMMER_DESKTOP_RELEASE_CHANNEL";

    public static DesktopUiMode ResolveCurrentMode()
    {
        string? configured = Environment.GetEnvironmentVariable(DesktopModeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (TryParse(configured, out DesktopUiMode mode))
            {
                return mode;
            }
        }

        return DesktopUiMode.Classic;
    }

    public static bool IsClassicDefault()
        => ResolveCurrentMode() == DesktopUiMode.Classic;

    public static bool ShouldShowSampleControls()
        => ResolveCurrentMode() is DesktopUiMode.Developer or DesktopUiMode.SupportRecovery
            && !string.Equals(ResolveReleaseChannel(), "public_stable", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldUseClassicFormPort(string? sectionId, string? selectedCommandId = null)
    {
        if (ResolveCurrentMode() != DesktopUiMode.Classic)
        {
            return false;
        }

        return TryResolveClassicPortId(sectionId, selectedCommandId) is not null;
    }

    public static string? TryResolveClassicPortId(string? sectionId, string? selectedCommandId = null)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            if (string.Equals(selectedCommandId, "global_settings", StringComparison.Ordinal)
                || string.Equals(selectedCommandId, "character_settings", StringComparison.Ordinal))
            {
                return "settings";
            }

            return null;
        }

        string normalized = sectionId.Trim().ToLowerInvariant();
        if (normalized.Contains("gear", StringComparison.Ordinal))
        {
            return "gear";
        }

        if (normalized.Contains("career", StringComparison.Ordinal)
            || normalized.Contains("karma", StringComparison.Ordinal)
            || normalized.Contains("advancement", StringComparison.Ordinal))
        {
            return "character_career";
        }

        if (normalized.Contains("create", StringComparison.Ordinal)
            || normalized.Contains("metatype", StringComparison.Ordinal)
            || normalized.Contains("priority", StringComparison.Ordinal))
        {
            return "character_create";
        }

        if (normalized.Contains("settings", StringComparison.Ordinal)
            || normalized.Contains("global", StringComparison.Ordinal))
        {
            return "settings";
        }

        if (normalized.Contains("index", StringComparison.Ordinal))
        {
            return "master_index";
        }

        return null;
    }

    public static bool ShouldHideFromClassicMode(string surfaceId)
    {
        if (ResolveCurrentMode() != DesktopUiMode.Classic)
        {
            return false;
        }

        return surfaceId is
            "black_ledger"
            or "signal_deck"
            or "living_world"
            or "runner_passport"
            or "table_pulse"
            or "newsroom"
            or "proof_shelf"
            or "raw_xml";
    }

    public static string ResolveReleaseChannel()
    {
        string? overrideChannel = Environment.GetEnvironmentVariable(ReleaseChannelEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overrideChannel))
        {
            return overrideChannel.Trim();
        }

        Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "ChummerDesktopReleaseChannel", StringComparison.Ordinal))?
            .Value?
            .Trim()
            ?? "local";
    }

    private static bool TryParse(string raw, out DesktopUiMode mode)
    {
        mode = DesktopUiMode.Classic;
        string normalized = raw.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return normalized switch
        {
            "classic" => Assign(DesktopUiMode.Classic, out mode),
            "modern" => Assign(DesktopUiMode.Modern, out mode),
            "supportrecovery" => Assign(DesktopUiMode.SupportRecovery, out mode),
            "developer" => Assign(DesktopUiMode.Developer, out mode),
            _ => false
        };
    }

    private static bool Assign(DesktopUiMode next, out DesktopUiMode mode)
    {
        mode = next;
        return true;
    }
}
