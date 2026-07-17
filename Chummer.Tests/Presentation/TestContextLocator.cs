using System.IO;
using System.Runtime.CompilerServices;
using Chummer.Desktop.Runtime;

namespace Chummer.Tests.Presentation;

internal static class TestContextLocator
{
    public static string ResolveChummerPresentationRepoRoot([CallerFilePath] string callerFilePath = "")
    {
        foreach (string candidate in EnumerateCallerRootCandidates(callerFilePath))
        {
            if (LooksLikeChummerPresentationRepoRoot(candidate))
            {
                return candidate;
            }
        }

        return DesktopRepoRootLocator.ResolveChummerPresentationRepoRootOrFallback(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory());
    }

    private static IEnumerable<string> EnumerateCallerRootCandidates(string callerFilePath)
    {
        if (string.IsNullOrWhiteSpace(callerFilePath))
        {
            yield break;
        }

        DirectoryInfo? directory = new(Path.GetDirectoryName(callerFilePath)!);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static bool LooksLikeChummerPresentationRepoRoot(string candidate)
    {
        return Directory.Exists(Path.Combine(candidate, "Chummer.Presentation"))
            && Directory.Exists(Path.Combine(candidate, "Chummer.Tests"));
    }
}

internal static class AvaloniaHeadlessSessionGate
{
    // Serializes headless session lifecycles across test classes.
    internal static object SyncRoot { get; } = new();
}
