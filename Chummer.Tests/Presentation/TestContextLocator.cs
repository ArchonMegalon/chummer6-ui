using System.IO;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests.Presentation;

internal static class TestContextLocator
{
    public static string ResolveChummerPresentationRepoRoot()
    {
        return DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
    }
}

internal static class AvaloniaHeadlessSessionGate
{
    // Serializes headless session lifecycles across test classes.
    internal static object SyncRoot { get; } = new();
}
