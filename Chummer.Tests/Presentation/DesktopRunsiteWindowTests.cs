using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopRunsiteWindowTests
{
    [TestMethod]
    public void DesktopRunsiteWindow_source_uses_workspace_digests_and_runsite_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopRunsiteWindow.cs"));

        StringAssert.Contains(source, "GetAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "GetCampaignWorkspaceDigestsAsync");
        StringAssert.Contains(source, "RunsiteDetailModeCombo");
        StringAssert.Contains(source, "RunsiteSelectedWorkspaceDetailText");
        StringAssert.Contains(source, "RunsiteSelectedWorkspaceFollowUpText");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/runsites\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/runsites\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/runsites/open\")");
    }
}
