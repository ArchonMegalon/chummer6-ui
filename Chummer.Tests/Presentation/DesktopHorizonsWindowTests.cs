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
        StringAssert.Contains(dialogSource, "Desktop Tools");
        StringAssert.Contains(dialogSource, "main Chummer tools");
        StringAssert.Contains(dialogSource, "CreateLegacyFieldGroup(\"Tools\", content)");
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
        StringAssert.Contains(localizationSource, "[\"desktop.shell.tool.horizons\"] = \"Tools\"");
        StringAssert.Contains(localizationSource, "[\"desktop.horizons.title\"] = \"Tools\"");
        StringAssert.Contains(localizationSource, "[\"desktop.horizons.heading\"] = \"Tools\"");
        StringAssert.Contains(localizationSource, "[\"desktop.horizons.button.open_public_index\"] = \"Open roadmap\"");
        StringAssert.Contains(localizationSource, "[\"desktop.home.button.open_horizons_public\"] = \"Open roadmap\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.shell.tool.horizons\"] = \"Werkzeuge\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.horizons.title\"] = \"Werkzeuge\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.horizons.button.open_public_index\"] = \"Roadmap öffnen\"");
        StringAssert.Contains(localizationSource, "localized[\"desktop.home.button.open_horizons_public\"] = \"Roadmap öffnen\"");
        Assert.IsFalse(localizationSource.Contains("Open product areas", StringComparison.Ordinal));
        Assert.IsFalse(localizationSource.Contains("Open public index", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Desktop_tools_surfaces_do_not_expose_workbench_or_horizon_maintenance_copy()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string[] sourceFiles =
        [
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicToolStrip.axaml"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "CharacterCreateClassicPort.axaml.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonsWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopBlackLedgerWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopKarmaForgeWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopJackpointWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCommunityHubWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopLocalCoProcessorWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunnerPassportWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunsiteWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopSupportCaseWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopTablePulseWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopGhostwireWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorOsWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunControlWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopQuicksilverWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopReadyForTonightWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopNexusPanWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOnrampWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunbookPressWindow.cs"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "WorkspaceStripControl.axaml"),
            Path.Combine(repoRoot, "Chummer.Avalonia", "Controls", "ClassicFormPorts", "SettingsClassicPort.axaml.cs"),
            Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DialogCoordinator.cs"),
            Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopDialogFactory.cs")
        ];

        string[] forbiddenVisiblePhrases =
        [
            "Open workbench",
            "Open Workbench",
            "Open campaign workbench",
            "Open ALICE workbench",
            "Workbenches",
            "workbenches",
            "desktop workbench",
            "roster workbench",
            "current workbench",
            "runner workbench",
            "creator workbench",
            "Native workbenches",
            "session lane",
            "notification lane",
            "signed-in work rail",
            "save posture",
            "plugin posture",
            "Publishing and creator lanes",
            "posture summaries",
            "Build handoffs",
            "Starter lane",
            "Groups and operators",
            "governed package lanes",
            "Mission-space posture",
            "Open return lane",
            "Open starter lane",
            "native rails",
            "publication lane",
            "network posture",
            "account rail",
            "Jackpoint lane",
            "world-state return lane",
            "bounded projection",
            "support closure lane",
            "operator posture",
            "network posture",
            "public Community lane",
            "Build handoffs",
            "identity-network posture",
            "native rails",
            "passport lane",
            "replay lane",
            "replay posture",
            "browser-only artifacts",
            "runbook lane",
            "publication posture",
            "dossier lane",
            "distribution posture",
            "assembly lane",
            "creator lane",
            "adjacent lanes",
            "Publishing posture",
            "Network posture",
            "network-facing posture",
            "governed run",
            "governed runs",
            "Current session posture",
            "current governed run lane",
            "active scene posture",
            "session posture",
            "ALICE handoffs",
            "blocker posture",
            "mobile-safe handoff",
            "return rail",
            "current run posture",
            "workspace return lane",
            "Open signed-in return lane",
            "Role kits and handoff",
            "mobile rail",
            "access posture",
            "continuity lane",
            "Continuity posture",
            "account posture",
            "recovery posture"
        ];

        foreach (string path in sourceFiles)
        {
            string source = File.ReadAllText(path);
            foreach (string phrase in forbiddenVisiblePhrases)
            {
                Assert.IsFalse(
                    source.Contains(phrase, StringComparison.Ordinal),
                    $"{Path.GetRelativePath(repoRoot, path)} must not expose '{phrase}' in user-facing copy.");
            }
        }
    }
}
