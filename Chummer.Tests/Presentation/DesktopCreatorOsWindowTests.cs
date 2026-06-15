using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopCreatorOsWindowTests
{
    [TestMethod]
    public void DesktopCreatorOsWindow_source_uses_creator_campaign_and_native_publishing_stack_follow_through()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopCreatorOsWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "CreatorPublications");
        StringAssert.Contains(source, "Dossiers");
        StringAssert.Contains(source, "CreatorOsDetailModeCombo");
        StringAssert.Contains(source, "CreatorOsDetailList");
        StringAssert.Contains(source, "CreatorOsDetailText");
        StringAssert.Contains(source, "CreatorOsSelectedEntryFollowUpText");
        StringAssert.Contains(source, "DesktopJackpointWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopRunbookPressWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopCommunityHubWindow.ShowAsync(this, _headId)");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/creator\")");
    }
}
