using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopOnrampWindowTests
{
    [TestMethod]
    public void DesktopOnrampWindow_source_uses_starter_and_mobile_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopOnrampWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "OnrampDetailModeCombo");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/onramp\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/runsites/open\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/mobile\")");
    }
}
