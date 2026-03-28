namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportedLanguage(
    string Code,
    string Label);

public static class DesktopLocalizationCatalog
{
    public const string DefaultLanguage = "en-us";
    private static readonly IReadOnlyDictionary<string, string> DefaultTrustSurfaceStrings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["desktop.home.title"] = "Desktop home cockpit",
        ["desktop.home.section.install_support"] = "Install and support",
        ["desktop.home.section.update_posture"] = "Update posture",
        ["desktop.home.section.campaign_return"] = "Campaign return and restore",
        ["desktop.home.section.support_closure"] = "Support closure and fix notices",
        ["desktop.home.section.build_explain"] = "Build and explain next",
        ["desktop.home.section.language_trust"] = "Language and trust surfaces",
        ["desktop.home.section.recent_workspaces"] = "Recent workspaces",
        ["desktop.install_link.title"] = "Link this copy",
        ["desktop.install_link.heading"] = "Link this desktop copy to your account",
        ["desktop.install_link.summary"] = "Chummer keeps the binary canonical. Linking happens through an install claim code and a Hub-issued installation grant instead of mutating the installer per user.",
        ["desktop.install_link.shipping_locales"] = "Shipping locales: {0}. Install, update, and support trust flows should stay aligned across this desktop wave.",
        ["desktop.install_link.claim_code_label"] = "Install claim code",
        ["desktop.install_link.button.copy_install_id"] = "Copy Install ID",
        ["desktop.install_link.button.open_downloads"] = "Open Downloads",
        ["desktop.install_link.button.open_support"] = "Open Support",
        ["desktop.install_link.button.open_work"] = "Open Work",
        ["desktop.install_link.button.open_account"] = "Open Account",
        ["desktop.install_link.button.link_copy"] = "Link This Copy",
        ["desktop.install_link.button.continue_guest"] = "Continue as Guest"
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TrustSurfaceStrings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en-us"] = DefaultTrustSurfaceStrings,
            ["de-de"] = DefaultTrustSurfaceStrings,
            ["fr-fr"] = DefaultTrustSurfaceStrings,
            ["ja-jp"] = DefaultTrustSurfaceStrings,
            ["pt-br"] = DefaultTrustSurfaceStrings,
            ["zh-cn"] = DefaultTrustSurfaceStrings
        };

    public static IReadOnlyList<DesktopSupportedLanguage> ShippingLanguages { get; } =
    [
        new("en-us", "English (en-US)"),
        new("de-de", "Deutsch (de-DE)"),
        new("fr-fr", "Francais (fr-FR)"),
        new("ja-jp", "Japanese (ja-JP)"),
        new("pt-br", "Portugues (pt-BR)"),
        new("zh-cn", "Chinese Simplified (zh-CN)")
    ];

    public static string NormalizeOrDefault(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return DefaultLanguage;
        }

        string normalized = languageCode.Trim().ToLowerInvariant();
        return ShippingLanguages.Any(language => string.Equals(language.Code, normalized, StringComparison.Ordinal))
            ? normalized
            : DefaultLanguage;
    }

    public static string GetDisplayLabel(string? languageCode)
    {
        string code = NormalizeOrDefault(languageCode);
        return ShippingLanguages
            .First(language => string.Equals(language.Code, code, StringComparison.Ordinal))
            .Label;
    }

    public static string BuildSupportedLanguageSummary()
        => string.Join(", ", ShippingLanguages.Select(language => language.Label));

    public static string GetRequiredString(string key, string? languageCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string normalizedLanguage = NormalizeOrDefault(languageCode);
        if (TrustSurfaceStrings.TryGetValue(normalizedLanguage, out IReadOnlyDictionary<string, string>? localized)
            && localized.TryGetValue(key, out string? value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (TrustSurfaceStrings.TryGetValue(DefaultLanguage, out IReadOnlyDictionary<string, string>? fallback)
            && fallback.TryGetValue(key, out value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new KeyNotFoundException($"desktop_localization_key_missing:{key}");
    }

    public static string GetRequiredFormattedString(string key, string? languageCode, params object[] values)
        => string.Format(GetRequiredString(key, languageCode), values);

    public static IReadOnlyList<string> RequiredTrustSurfaceKeys()
        => DefaultTrustSurfaceStrings.Keys.OrderBy(static key => key, StringComparer.Ordinal).ToArray();
}
