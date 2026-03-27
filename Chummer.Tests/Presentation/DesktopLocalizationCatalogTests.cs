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
}
