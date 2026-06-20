using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopNexusPanWindowTests
{
    [TestMethod]
    public void DesktopNexusPanWindow_source_uses_continuity_and_native_follow_through_desks()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopNexusPanWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "LatestContinuity?.Summary");
        StringAssert.Contains(source, "NexusPanDetailModeCombo");
        StringAssert.Contains(source, "NexusPanWorkspaceList");
        StringAssert.Contains(source, "NexusPanDetailText");
        StringAssert.Contains(source, "NexusPanSelectedWorkspaceFollowUpText");
        StringAssert.Contains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopRunControlWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopDevicesAccessWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "\"Open Your Copy\"");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/play/continuity\")");
        Assert.IsFalse(source.Contains("Open devices & access", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Devices and access", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("devices-and-access", System.StringComparison.Ordinal));
    }
}
