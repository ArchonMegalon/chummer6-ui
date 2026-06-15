using System.IO;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests.Presentation;

internal static class TestContextLocator
{
    public static string ResolveChummerPresentationRepoRoot()
    {
        return DesktopRepoRootLocator.TryResolveChummerPresentationRepoRoot(
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory())
            ?? "/docker/chummercomplete/chummer-presentation";
    }
}
