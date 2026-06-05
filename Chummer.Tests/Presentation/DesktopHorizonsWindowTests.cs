using System.IO;
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
        StringAssert.Contains(source, "DesktopHorizonWorkbenchCatalog.ListEntries()");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchCatalog.ListKarmaForgeTargets()");
        StringAssert.Contains(source, "CreateKarmaForgeCard()");
        StringAssert.Contains(source, "CreateHorizonCard(");
        StringAssert.Contains(source, "CreateAliceCard(");
        StringAssert.Contains(source, "CreateRunControlCard(");
        StringAssert.Contains(source, "CreateBlackLedgerCard(");
        StringAssert.Contains(source, "CreateNativeLaunchCard(");
        StringAssert.Contains(source, "DesktopKarmaForgeWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopAliceWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.SupportsNativeWorkbench(entry.Id)");
        StringAssert.Contains(source, "DesktopHorizonWorkbenchLauncher.OpenAsync(this, _headId, entry)");
        StringAssert.Contains(source, "DesktopRunControlWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopBlackLedgerWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/participate/karma-forge#karma-forge-intake\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/packages\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/horizons\")");
    }
}
