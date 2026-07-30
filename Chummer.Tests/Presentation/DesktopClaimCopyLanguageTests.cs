#nullable enable

using System.IO;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopClaimCopyLanguageTests
{
    [TestMethod]
    public void Desktop_claim_copy_localization_avoids_install_handoff_language()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string catalog = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Presentation", "Overview", "DesktopLocalizationCatalog.cs"));

        StringAssert.Contains(catalog, "Claim your copy");
        StringAssert.Contains(catalog, "Browser claim attempt: {0} UTC.");
        StringAssert.Contains(catalog, "Browser claim error: {0}");
        StringAssert.Contains(catalog, "Last claim attempt: {0} UTC.");
        StringAssert.Contains(catalog, "Copy claim reviewed.");

        AssertVisibleCopyDoesNotContain(catalog, "Browser handoff");
        AssertVisibleCopyDoesNotContain(catalog, "Recent handoff");
        AssertVisibleCopyDoesNotContain(catalog, "install linking reviewed");
        AssertVisibleCopyDoesNotContain(catalog, "Finish it online");
        AssertVisibleCopyDoesNotContain(catalog, "Link this install to keep");
    }

    [TestMethod]
    public void Headless_claim_copy_output_uses_user_claim_language()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string runtime = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Desktop.Runtime", "DesktopInstallLinkingRuntime.cs"));

        StringAssert.Contains(runtime, "Chummer claim-your-copy headless mode");
        StringAssert.Contains(runtime, "Open this URL to sign in and claim this Linux copy:");
        StringAssert.Contains(runtime, "copy the claim URL from the browser page");
        StringAssert.Contains(runtime, "Browser claim requested; waiting for this copy to finish.");
        StringAssert.Contains(runtime, "Browser claim could not be opened automatically");

        AssertVisibleCopyDoesNotContain(runtime, "Chummer install-link headless mode");
        AssertVisibleCopyDoesNotContain(runtime, "finish linking this Linux install");
        AssertVisibleCopyDoesNotContain(runtime, "copy the callback URL from the browser page");
        AssertVisibleCopyDoesNotContain(runtime, "Browser handoff requested");
        AssertVisibleCopyDoesNotContain(runtime, "Browser handoff could not be opened automatically");
    }

    private static void AssertVisibleCopyDoesNotContain(string source, string value)
    {
        Assert.IsFalse(source.Contains(value, StringComparison.Ordinal), $"Visible desktop copy should not contain '{value}'.");
    }
}
