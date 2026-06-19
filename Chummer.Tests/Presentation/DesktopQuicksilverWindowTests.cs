using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopQuicksilverWindowTests
{
    [TestMethod]
    public void DesktopQuicksilverWindow_source_uses_account_jump_targets_and_quicksilver_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopQuicksilverWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "BuildLabHandoffs");
        StringAssert.Contains(source, "RulesNavigator");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/quicksilver\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/quicksilver\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/alice\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/runsites\")");
        StringAssert.Contains(source, "AreGuidedToolsVisible()");
        StringAssert.Contains(source, "if (showGuidedTools)");
    }
}
