using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopAliceLaunchPathTests
{
    [TestMethod]
    public void MainWindow_toolstrip_alice_launch_opens_native_desktop_alice_window()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "MainWindow.EventHandlers.cs"));

        StringAssert.Contains(source, "private async void ToolStrip_OnAutoAliceRequested");
        StringAssert.Contains(source, "DesktopAliceWindow.ShowAsync(this, DesktopHeadId);");
        StringAssert.Contains(source, "open desktop alice");
    }
}
