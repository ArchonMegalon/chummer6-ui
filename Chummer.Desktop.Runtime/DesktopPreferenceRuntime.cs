using System.Text;
using System.Text.Json;
using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

public static class DesktopPreferenceRuntime
{
    private const string PreferencesRootDirectoryName = "preferences";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static DesktopPreferenceState LoadOrCreateState(string headId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        string stateFilePath = GetStateFilePath(headId);
        DesktopPreferenceState normalized = DesktopPreferenceStateRuntime.Normalize(
            LoadState(stateFilePath) ?? DesktopPreferenceState.Default);
        SaveState(headId, normalized);
        return normalized;
    }

    public static void SaveState(string headId, DesktopPreferenceState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(state);

        string stateFilePath = GetStateFilePath(headId);
        DesktopPreferenceState normalized = DesktopPreferenceStateRuntime.Normalize(state);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
            File.WriteAllText(stateFilePath, JsonSerializer.Serialize(normalized, JsonOptions), Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static DesktopPreferenceState? LoadState(string stateFilePath)
    {
        if (!File.Exists(stateFilePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DesktopPreferenceState>(File.ReadAllText(stateFilePath, Encoding.UTF8), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string GetStateFilePath(string headId)
        => Path.Combine(
            DesktopStateRootResolver.Resolve("Chummer6", "Chummer6"),
            PreferencesRootDirectoryName,
            headId.Trim(),
            "state.json");
}
