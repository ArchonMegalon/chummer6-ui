using System.IO;
using System.Linq;
using Chummer.Contracts.Product;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopHorizonsWindowTests
{
    [TestMethod]
    public void DesktopHorizonsWindow_source_builds_native_horizon_workbench_cards_and_karma_forge_actions()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonsWindow.cs"));

        StringAssert.Contains(source, "DesktopHorizonsWindow");
        StringAssert.Contains(source, "CreateKarmaForgeCard()");
        StringAssert.Contains(source, "CreateHorizonCard(");
        StringAssert.Contains(source, "CreateAliceCard(");
        StringAssert.Contains(source, "CreateRunControlCard(");
        StringAssert.Contains(source, "CreateBlackLedgerCard(");
        StringAssert.Contains(source, "CreateNativeLaunchCard(");
        StringAssert.Contains(source, "CreateNativeAdjunctActionButton(");
        StringAssert.Contains(source, "ShouldShowWorkbenchEntry");
        StringAssert.Contains(source, ".Where(ShouldShowWorkbenchEntry)");
        StringAssert.Contains(source, "OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(entry.Id, _preferences)");
        StringAssert.Contains(source, "DesktopKarmaForgeWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopAliceWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.SupportsNativeWorkbench(entry.Id)");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.OpenAsync(this, _headId, entry)");
        StringAssert.Contains(source, "DesktopRunControlWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopBlackLedgerWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopReadyForTonightWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopKnowledgeFabricWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopTablePulseWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopGhostwireWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCreatorPublicationWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/participate/karma-forge#karma-forge-intake\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/packages\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/roadmap\")");
        Assert.IsFalse(source.Contains("DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/horizons\")", StringComparison.Ordinal));
        Assert.IsTrue(ProductSpineCatalog.ListDesktopHorizons().Any(entry => string.Equals(entry.Id, "alice", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(
            new[] { "/packages", "/account/packages", "/participate/karma-forge#karma-forge-intake" },
            ProductSpineCatalog.ListKarmaForgeTargets().Select(static target => target.RelativeHref).ToArray());

        string launcherSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonWorkbenchLauncher.cs"));
        StringAssert.Contains(launcherSource, "OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(");
        StringAssert.Contains(launcherSource, "DesktopPreferenceRuntime.LoadOrCreateState(headId)");

        string homeSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHomeWindow.cs"));
        StringAssert.Contains(homeSource, "OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(item.Id, _preferences)");
        StringAssert.Contains(homeSource, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/roadmap\")");
        Assert.IsFalse(homeSource.Contains("DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/horizons\")", StringComparison.Ordinal));

        string dialogSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopDialogWindow.axaml.cs"));
        StringAssert.Contains(dialogSource, "DesktopPreferenceRuntime.LoadOrCreateState(\"avalonia\")");
        StringAssert.Contains(dialogSource, "OverviewCommandPolicy.IsBlockedByAiFeaturePreferenceForHorizon(item.Id, preferences)");
        StringAssert.Contains(dialogSource, "Desktop Workbench Integrations");
        StringAssert.Contains(dialogSource, "first-party workbenches");
        StringAssert.Contains(dialogSource, "CreateLegacyFieldGroup(\"Workbenches\", content)");
        Assert.IsFalse(dialogSource.Contains("Desktop Horizon Integrations", StringComparison.Ordinal));
        Assert.IsFalse(dialogSource.Contains("horizon lanes", StringComparison.Ordinal));
        Assert.IsFalse(dialogSource.Contains("CreateLegacyFieldGroup(\"Horizons\"", StringComparison.Ordinal));

        string karmaForgeSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopKarmaForgeWindow.cs"));
        StringAssert.Contains(karmaForgeSource, "CreateButton(\"Open roadmap\", static () => DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/roadmap\")");
        Assert.IsFalse(karmaForgeSource.Contains("Open Horizons index", StringComparison.Ordinal));
        Assert.IsFalse(karmaForgeSource.Contains("TryOpenRelativePortal(\"/horizons\")", StringComparison.Ordinal));

        string toolStripSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicToolStrip.axaml.cs"));
        StringAssert.Contains(toolStripSource, "SetButtonLabel(\"HorizonsButton\", DesktopLocalizationCatalog.GetRequiredString(\"desktop.shell.tool.horizons\", DesktopLocalizationCatalog.GetCurrentLanguage()), \"Tools\")");
        Assert.IsFalse(toolStripSource.Contains("SetButtonLabel(\"HorizonsButton\", DesktopLocalizationCatalog.GetRequiredString(\"desktop.shell.tool.horizons\", DesktopLocalizationCatalog.GetCurrentLanguage()), \"Horizons\")", StringComparison.Ordinal));

        string localizationSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));
        StringAssert.Contains(localizationSource, "[\"desktop.horizons.button.open_public_index\"] = \"Open roadmap\"");
        StringAssert.Contains(localizationSource, "[\"desktop.home.button.open_horizons_public\"] = \"Open roadmap\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.horizons.button.open_public_index\"] = \"Roadmap öffnen\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.home.button.open_horizons_public\"] = \"Roadmap öffnen\"");
        Assert.IsFalse(localizationSource.Contains("Open product areas", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Open public index", StringComparison.Ordinal));
    }
}
