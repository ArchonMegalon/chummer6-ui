using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopKarmaForgeWindowTests
{
    [TestMethod]
    public void DesktopKarmaForgeWindow_source_uses_account_campaign_context_and_native_package_actions()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopKarmaForgeWindow.cs"));

        StringAssert.Contains(source, "IChummerClient");
        StringAssert.Contains(source, "GetAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "BuildLabHandoffs.Count");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/packages\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/participate/karma-forge#karma-forge-intake\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/alice\")");
        StringAssert.Contains(source, "AreGuidedToolsVisible()");
        StringAssert.Contains(source, "if (showGuidedTools)");

        IReadOnlyList<DesktopHorizonRouteOption> targets = ProductSpineCatalog.ListKarmaForgeTargets();
        CollectionAssert.AreEqual(
            new[] { "/packages", "/account/packages", "/participate/karma-forge#karma-forge-intake" },
            targets.Select(static target => target.RelativeHref).ToArray());
    }
}
