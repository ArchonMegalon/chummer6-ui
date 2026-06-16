using System;
using System.IO;
using System.Linq;
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

    [TestMethod]
    public void Horizon_catalog_keeps_native_adjunct_actions_for_deeper_native_horizon_lanes()
    {
        DesktopHorizonWorkbenchEntry nexusPan = DesktopHorizonWorkbenchCatalog.ListEntries().Single(static entry => entry.Id == "nexus_pan");
        DesktopHorizonWorkbenchEntry runbookPress = DesktopHorizonWorkbenchCatalog.ListEntries().Single(static entry => entry.Id == "runbook_press");
        DesktopHorizonWorkbenchEntry creatorOs = DesktopHorizonWorkbenchCatalog.ListEntries().Single(static entry => entry.Id == "creator_os");

        CollectionAssert.AreEquivalent(
            new[] { "workspace", "devices_access" },
            nexusPan.NativeActions?.Select(static action => action.Id).ToArray() ?? Array.Empty<string>());
        CollectionAssert.AreEquivalent(
            new[] { "publication", "workspace" },
            runbookPress.NativeActions?.Select(static action => action.Id).ToArray() ?? Array.Empty<string>());
        CollectionAssert.AreEquivalent(
            new[] { "publication", "workspace" },
            creatorOs.NativeActions?.Select(static action => action.Id).ToArray() ?? Array.Empty<string>());
    }

    [TestMethod]
    public void Desktop_horizon_workbench_catalog_delegates_to_shared_product_spine()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopHorizonWorkbenchCatalog.cs"));

        StringAssert.Contains(source, "ProductSpineCatalog.ListDesktopHorizons()");
        StringAssert.Contains(source, "ProductSpineCatalog.ListKarmaForgeTargets()");
    }
}
