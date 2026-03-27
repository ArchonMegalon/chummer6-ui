namespace Chummer.Presentation.Overview;

public sealed record DesktopSupportedLanguage(
    string Code,
    string Label);

public static class DesktopLocalizationCatalog
{
    public const string DefaultLanguage = "en-us";

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
}
