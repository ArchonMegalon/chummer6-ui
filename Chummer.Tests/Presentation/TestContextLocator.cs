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
