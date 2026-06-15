using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Reflection;
using Chummer.Contracts.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public sealed class DesktopAnalyticsClient
{
    private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly() ?? typeof(DesktopAnalyticsClient).Assembly;
    private static readonly string ReleaseVersion = EntryAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(static attribute => string.Equals(attribute.Key, "ChummerDesktopReleaseVersion", StringComparison.Ordinal))
        ?.Value
        ?? EntryAssembly.GetName().Version?.ToString()
        ?? "0.0.0";
    private static readonly string ReleaseChannel = EntryAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(static attribute => string.Equals(attribute.Key, "ChummerDesktopReleaseChannel", StringComparison.Ordinal))
        ?.Value
        ?? "local";
    private readonly HttpClient _httpClient;

    public DesktopAnalyticsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task TrackShellEventAsync(
        string headId,
        string eventName,
        string surface,
        IReadOnlyDictionary<string, string?>? properties = null,
        CancellationToken ct = default)
    {
        DesktopPreferenceState preferences = DesktopPreferenceStateRuntime.Current;
        if (!preferences.AnalyticsOptIn
            || string.IsNullOrWhiteSpace(headId)
            || string.IsNullOrWhiteSpace(eventName)
            || string.IsNullOrWhiteSpace(surface))
        {
            return;
        }

        Dictionary<string, string> normalizedProperties = new(StringComparer.Ordinal);
        if (properties is not null)
        {
            foreach ((string key, string? value) in properties)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                normalizedProperties[key.Trim()] = value.Trim();
            }
        }

        DesktopAnalyticsTrackRequest payload = new(
            HeadId: headId.Trim(),
            EventName: eventName.Trim(),
            Surface: surface.Trim(),
            ReleaseVersion: ReleaseVersion,
            ReleaseChannel: ReleaseChannel,
            OptIn: true,
            UiMode: ClassicModePolicy.ResolveCurrentMode().ToString(),
            Language: preferences.Language,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Properties: new ReadOnlyDictionary<string, string>(normalizedProperties));

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/desktop-analytics/track",
                payload,
                ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Desktop analytics track failed with HTTP {(int)response.StatusCode} for '{payload.EventName}'.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Desktop analytics track failed for '{payload.EventName}': {ex.Message}");
        }
    }
}
