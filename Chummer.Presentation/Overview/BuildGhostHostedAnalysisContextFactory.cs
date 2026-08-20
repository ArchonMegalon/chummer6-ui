using System.Globalization;

namespace Chummer.Presentation.Overview;

public static class BuildGhostHostedAnalysisContextFactory
{
    public static bool TryCreate(
        BuildGhostAnalysisClientContext? requested,
        out BuildGhostAnalysisClientContext normalized)
    {
        normalized = new BuildGhostAnalysisClientContext(string.Empty, [], string.Empty);
        if (requested is null || string.IsNullOrWhiteSpace(requested.Locale))
        {
            return false;
        }

        string? localeCode = DesktopLocalizationCatalog.ShippingLanguages
            .Select(static language => language.Code)
            .FirstOrDefault(code => string.Equals(code, requested.Locale.Trim(), StringComparison.OrdinalIgnoreCase));
        if (localeCode is null)
        {
            return false;
        }

        string contractLocale = CultureInfo.GetCultureInfo(localeCode).Name;
        string[] supportedLocales = DesktopLocalizationCatalog.ShippingLanguages
            .Select(static language => CultureInfo.GetCultureInfo(language.Code).Name)
            .OrderBy(static locale => locale, StringComparer.Ordinal)
            .ToArray();
        normalized = new BuildGhostAnalysisClientContext(
            Locale: contractLocale,
            SupportedLocales: supportedLocales,
            DeterministicFallbackText: BuildGhostAlicePresentation.GetDeterministicFallbackText(localeCode));
        return true;
    }
}
