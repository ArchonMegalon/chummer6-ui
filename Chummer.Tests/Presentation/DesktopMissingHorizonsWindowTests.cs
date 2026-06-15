using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopMissingHorizonsWindowTests
{
    [TestMethod]
    public void Dedicated_horizon_windows_exist_for_nexus_pan_runbook_press_and_creator_os()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string launcherSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopHorizonWorkbenchLauncher.cs"));
        string nexusPanSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopNexusPanWindow.cs"));
        string runbookSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunbookPressWindow.cs"));
        string creatorOsSource = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorOsWindow.cs"));

        StringAssert.Contains(launcherSource, "\"nexus_pan\" => DesktopNexusPanWindow.ShowAsync(owner, headId)");
        StringAssert.Contains(launcherSource, "\"runbook_press\" => DesktopRunbookPressWindow.ShowAsync(owner, headId)");
        StringAssert.Contains(launcherSource, "\"creator_os\" => DesktopCreatorOsWindow.ShowAsync(owner, headId)");

        StringAssert.Contains(nexusPanSource, "DesktopNexusPanWindow");
        StringAssert.Contains(nexusPanSource, "Open public continuity");
        StringAssert.Contains(nexusPanSource, "/account/access#desktop");

        StringAssert.Contains(runbookSource, "DesktopRunbookPressWindow");
        StringAssert.Contains(runbookSource, "Open public Runbook");
        StringAssert.Contains(runbookSource, "/account/creator");

        StringAssert.Contains(creatorOsSource, "DesktopCreatorOsWindow");
        StringAssert.Contains(creatorOsSource, "Open public Creator OS");
        StringAssert.Contains(creatorOsSource, "Open Runbook Press");
    }
}
