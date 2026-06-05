using Microsoft.VisualStudio.TestTools.UnitTesting;
using Chummer.Avalonia;
using Chummer.Presentation.Overview;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopHorizonWorkbenchLauncherTests
{
    [TestMethod]
    public void DesktopHorizonWorkbenchLauncher_supports_every_horizon_catalog_entry()
    {
        foreach (DesktopHorizonWorkbenchEntry entry in DesktopHorizonWorkbenchCatalog.ListEntries())
        {
            Assert.IsTrue(
                DesktopHorizonWorkbenchLauncher.SupportsNativeWorkbench(entry.Id),
                $"Expected native workbench coverage for horizon '{entry.Id}'.");
        }
    }
}
