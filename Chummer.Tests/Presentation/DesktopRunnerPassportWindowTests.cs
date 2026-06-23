using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopRunnerPassportWindowTests
{
    [TestMethod]
    public void DesktopRunnerPassportWindow_source_uses_dossiers_and_passport_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunnerPassportWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "HasIdentityContext");
        StringAssert.Contains(source, "Dossiers");
        StringAssert.Contains(source, "Crews");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/passport\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/passport\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/access#desktop\")");
        StringAssert.Contains(source, "\"Open Your Copy\"");
        Assert.IsFalse(source.Contains("Open devices & access", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Device and access", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("device/access", System.StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("follow-through", System.StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("account-bound", System.StringComparison.OrdinalIgnoreCase));
    }
}
