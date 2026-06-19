using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopKnowledgeFabricWindowTests
{
    [TestMethod]
    public void DesktopKnowledgeFabricWindow_source_uses_rules_navigator_and_rules_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopKnowledgeFabricWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "RulesNavigator");
        StringAssert.Contains(source, "KnowledgeFabricDetailModeCombo");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/rules\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/rules/receipts\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/edition-studio\")");
        StringAssert.Contains(source, "AreGuidedToolsVisible()");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/local-co-processor\")");
    }
}
