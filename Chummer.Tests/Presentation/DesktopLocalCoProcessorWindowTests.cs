using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopLocalCoProcessorWindowTests
{
    [TestMethod]
    public void DesktopLocalCoProcessorWindow_source_uses_rules_build_context_and_local_coprocessor_routes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopLocalCoProcessorWindow.cs"));

        StringAssert.Contains(source, "TryReadAccountCampaignSummaryAsync");
        StringAssert.Contains(source, "HasCapabilityContext");
        StringAssert.Contains(source, "RulesNavigator");
        StringAssert.Contains(source, "BuildLabHandoffs");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/local-co-processor\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/local-co-processor\")");
        StringAssert.Contains(source, "DesktopInstallLinkingRuntime.TryOpenRelativePortal(\"/account/alice\")");
    }
}
