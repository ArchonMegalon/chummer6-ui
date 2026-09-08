using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class CreationWizardCoreProjectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RealCorePriorityCompletionAndActivationPreserveAuthority()
    {
        // No discovery and no skip: governed callers verify the content bytes
        // against the same Core authority as the packages before and after execution.
        TestContext.Properties.TryGetValue("ChummerCoreContentRoot", out object? suppliedRoot);
        string? contentRoot = suppliedRoot as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(contentRoot),
            "Supply the explicit ChummerCoreContentRoot test parameter; missing Core data cannot skip this test.");
        int checks = await CoreCreationProjectionScenario.RunAsync(contentRoot!, TestContext.WriteLine);
        Assert.IsGreaterThanOrEqualTo(39, checks);
    }
}
