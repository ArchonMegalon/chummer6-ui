using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

#nullable enable

[TestClass]
public sealed class DesktopFileCoordinatorTests
{
    [TestMethod]
    public async Task OpenBundledDemoRunnerAsync_prefers_explicit_override_path()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "chummer-demo-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string samplePath = Path.Combine(tempDirectory, "Soma-Career.chum5");
        byte[] expectedPayload = [0x43, 0x48, 0x55, 0x4D];
        string? previousOverride = Environment.GetEnvironmentVariable("CHUMMER_BUNDLED_DEMO_RUNNER_PATH");

        try
        {
            await File.WriteAllBytesAsync(samplePath, expectedPayload);
            Environment.SetEnvironmentVariable("CHUMMER_BUNDLED_DEMO_RUNNER_PATH", samplePath);

            DesktopImportFileResult result = await MainWindowDesktopFileCoordinator.OpenBundledDemoRunnerAsync(CancellationToken.None);

            Assert.AreEqual(DesktopFileOperationOutcome.Completed, result.Outcome);
            CollectionAssert.AreEqual(expectedPayload, result.Payload);
            Assert.AreEqual("Samples/Legacy/Soma-Career.chum5", result.SourceLabel);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHUMMER_BUNDLED_DEMO_RUNNER_PATH", previousOverride);
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Bundled_demo_runner_resolution_does_not_hardcode_the_old_repo_checkout_path()
    {
        string sourcePath = Path.Combine(FindRepoRoot(), "Chummer.Avalonia", "MainWindow.DesktopFileCoordinator.cs");
        string sourceText = File.ReadAllText(sourcePath);

        Assert.IsFalse(
            sourceText.Contains("/docker/chummercomplete/chummer-presentation/Chummer.Avalonia", StringComparison.Ordinal),
            "Bundled demo-runner resolution should not depend on a single repo checkout path.");
        StringAssert.Contains(sourceText, "CHUMMER_BUNDLED_DEMO_RUNNER_PATH", StringComparison.Ordinal);
        StringAssert.Contains(sourceText, "DesktopRepoRootLocator", StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
