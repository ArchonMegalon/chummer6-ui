using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopBlackLedgerWindowTests
{
    [TestMethod]
    public void DesktopBlackLedgerWindow_source_uses_campaign_workspace_context_and_ledger_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopBlackLedgerWindow.cs"));

        StringAssert.Contains(source, "GetAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "_campaignSummary?.Campaigns");
        StringAssert.Contains(source, "_campaignSummary?.Workspaces");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/ledger\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/ledger/map#ledger-map\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/ledger/worldtick/validation\")");
        Assert.IsFalse(source.Contains("follow-through", System.StringComparison.OrdinalIgnoreCase));
    }
}
