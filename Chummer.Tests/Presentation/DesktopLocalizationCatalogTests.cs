using Chummer.Presentation.Overview;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public class DesktopLocalizationCatalogTests
{
    [TestMethod]
    public void NormalizeOrDefault_uses_shipping_language_and_falls_back_to_english()
    {
        Assert.AreEqual("de-de", DesktopLocalizationCatalog.NormalizeOrDefault("de-DE"));
        Assert.AreEqual(DesktopLocalizationCatalog.DefaultLanguage, DesktopLocalizationCatalog.NormalizeOrDefault("es-es"));
        Assert.AreEqual(DesktopLocalizationCatalog.DefaultLanguage, DesktopLocalizationCatalog.NormalizeOrDefault(null));
    }

    [TestMethod]
    public void ShippingLanguages_match_locked_desktop_wave()
    {
        CollectionAssert.AreEqual(
            new[] { "en-us", "de-de", "fr-fr", "ja-jp", "pt-br", "zh-cn" },
            DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code).ToArray());
    }

    [TestMethod]
    public void RequiredTrustSurfaceKeys_resolve_for_every_shipping_language()
    {
        foreach (string key in DesktopLocalizationCatalog.RequiredTrustSurfaceKeys())
        {
            foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages.Select(language => language.Code))
            {
                string value = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"Expected trust-surface localization for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Non_default_locales_never_return_unmarked_english_values_on_seeded_keys()
    {
        string[] seededKeys =
        [
            "desktop.shell.menu.file",
            "desktop.shell.tool.desktop_home",
            "desktop.home.section.install_support",
            "desktop.home.title",
            "desktop.support.title"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in seededKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                string enValue = DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage);
                Assert.AreNotEqual(enValue, localizedValue, $"Expected locale-distinct value for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Non_default_locales_cover_remaining_trust_surface_seed_keys_without_fallback_markers()
    {
        string[] seedKeys =
        [
            "desktop.install_link.summary",
            "desktop.update.heading",
            "desktop.report.bug.intro",
            "desktop.report.heading",
            "desktop.crash.heading"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in seedKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                string enValue = DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage);
                Assert.IsFalse(localizedValue.Contains("[en-US fallback]", StringComparison.Ordinal), $"Expected localized value without fallback marker for {key} / {languageCode}.");
                Assert.AreNotEqual(enValue, localizedValue, $"Expected locale-distinct localized value for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Release_critical_localized_seed_keys_cover_menu_support_update_and_home_surfaces_without_fallback()
    {
        string[] releaseCriticalSeedKeys =
        [
            "desktop.shell.menu.file",
            "desktop.shell.tool.update_status",
            "desktop.shell.tool.open_support",
            "desktop.shell.tool.report_issue",
            "desktop.home.title",
            "desktop.home.section.install_support",
            "desktop.home.section.update_posture",
            "desktop.support.title"
        ];

        foreach (string languageCode in DesktopLocalizationCatalog.ShippingLanguages
                     .Select(language => language.Code)
                     .Where(language => !string.Equals(language, DesktopLocalizationCatalog.DefaultLanguage, StringComparison.Ordinal)))
        {
            foreach (string key in releaseCriticalSeedKeys)
            {
                string localizedValue = DesktopLocalizationCatalog.GetRequiredString(key, languageCode);
                Assert.IsFalse(localizedValue.Contains("[en-US fallback]", StringComparison.Ordinal), $"Expected fully localized seeded key for {key} / {languageCode}.");
                Assert.AreNotEqual(
                    DesktopLocalizationCatalog.GetRequiredString(key, DesktopLocalizationCatalog.DefaultLanguage),
                    localizedValue,
                    $"Expected locale-distinct seeded key for {key} / {languageCode}.");
            }
        }
    }

    [TestMethod]
    public void Required_trust_surface_keys_cover_flagship_localization_domains()
    {
        string[] requiredPrefixes =
        [
            "desktop.shell.menu.",
            "desktop.shell.tool.",
            "desktop.home.",
            "desktop.install_link.",
            "desktop.update.",
            "desktop.support.",
            "desktop.support_case.",
            "desktop.crash.",
            "desktop.report.",
            "desktop.dialog.global_settings.",
            "desktop.dialog.translator.",
            "desktop.shell.notice.export_"
        ];

        IReadOnlyList<string> keys = DesktopLocalizationCatalog.RequiredTrustSurfaceKeys();
        foreach (string prefix in requiredPrefixes)
        {
            Assert.IsTrue(
                keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)),
                $"Expected flagship localization trust-surface coverage for prefix '{prefix}'.");
        }
    }
}
