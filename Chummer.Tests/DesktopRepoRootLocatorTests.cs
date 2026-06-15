using System;
using System.IO;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests;

[TestClass]
public sealed class DesktopRepoRootLocatorTests
{
    [TestMethod]
    public void ResolveChummerPresentationRepoRoot_finds_repo_from_base_directory_ancestors()
    {
        string root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Chummer.Avalonia"));
            Directory.CreateDirectory(Path.Combine(root, "Chummer.Tests"));
            Directory.CreateDirectory(Path.Combine(root, "Chummer.Desktop.Runtime"));
            Directory.CreateDirectory(Path.Combine(root, "artifacts", "bin", "Debug", "net10.0"));
            File.WriteAllText(Path.Combine(root, "Chummer.sln"), string.Empty);

            string baseDirectory = Path.Combine(root, "artifacts", "bin", "Debug", "net10.0");
            string currentDirectory = Path.Combine(root, "elsewhere");
            Directory.CreateDirectory(currentDirectory);

            string? resolved = DesktopRepoRootLocator.TryResolveChummerPresentationRepoRoot(baseDirectory, currentDirectory);

            Assert.AreEqual(root, resolved);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [TestMethod]
    public void ResolveChummerPresentationRepoRootOrFallback_uses_current_directory_when_repo_markers_are_missing()
    {
        string root = CreateTempDirectory();
        try
        {
            string baseDirectory = Path.Combine(root, "bin");
            string currentDirectory = Path.Combine(root, "cwd");
            Directory.CreateDirectory(baseDirectory);
            Directory.CreateDirectory(currentDirectory);

            string resolved = DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(baseDirectory, currentDirectory);

            Assert.AreEqual(Path.GetFullPath(currentDirectory), resolved);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "chummer-desktop-root-locator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
