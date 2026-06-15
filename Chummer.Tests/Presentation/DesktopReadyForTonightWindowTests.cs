using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopReadyForTonightWindowTests
{
    [TestMethod]
    public void DesktopReadyForTonightWindow_source_uses_return_lane_and_mobile_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopReadyForTonightWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "ReadyForTonightDetailModeCombo");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/ready\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/runsites/open\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/mobile\")");
    }
}
