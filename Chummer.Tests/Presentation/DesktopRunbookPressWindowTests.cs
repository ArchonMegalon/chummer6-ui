using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopRunbookPressWindowTests
{
    [TestMethod]
    public void DesktopRunbookPressWindow_source_uses_publication_campaign_and_native_creator_follow_through()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunbookPressWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "CreatorPublications");
        StringAssert.Contains(source, "Campaigns");
        StringAssert.Contains(source, "RunbookPressDetailModeCombo");
        StringAssert.Contains(source, "RunbookPressDetailList");
        StringAssert.Contains(source, "RunbookPressDetailText");
        StringAssert.Contains(source, "RunbookPressSelectedEntryFollowUpText");
        StringAssert.Contains(source, "DesktopCreatorPublicationWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCampaignWorkspaceWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCreatorOsWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopJackpointWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCommunityHubWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/runbook\")");
    }
}
