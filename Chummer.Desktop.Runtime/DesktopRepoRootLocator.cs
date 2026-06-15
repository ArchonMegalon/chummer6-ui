using System.Collections.Generic;
using System.IO;

namespace Chummer.Desktop.Runtime;

public static class DesktopRepoRootLocator
{
    private const string RepoMarkerFileName = "Chummer.sln";

    public static string ResolveChummerPresentationRepoRootOrFallback(string baseDirectory, string currentDirectory)
        => TryResolveChummerPresentationRepoRoot(baseDirectory, currentDirectory)
            ?? Path.GetFullPath(string.IsNullOrWhiteSpace(currentDirectory) ? baseDirectory : currentDirectory);

    public static string? TryResolveChummerPresentationRepoRoot(string baseDirectory, string currentDirectory)
        => TryResolveRepoRoot(
            baseDirectory,
            currentDirectory,
            requiredDirectories: ["Chummer.Avalonia", "Chummer.Tests", "Chummer.Desktop.Runtime"]);

    private static string? TryResolveRepoRoot(
        string baseDirectory,
        string currentDirectory,
        IReadOnlyList<string> requiredDirectories)
    {
        foreach (string candidate in EnumerateSearchRoots(baseDirectory, currentDirectory))
        {
            if (!File.Exists(Path.Combine(candidate, RepoMarkerFileName)))
            {
                continue;
            }

            bool allDirectoriesPresent = true;
            foreach (string requiredDirectory in requiredDirectories)
            {
                if (!Directory.Exists(Path.Combine(candidate, requiredDirectory)))
                {
                    allDirectoriesPresent = false;
                    break;
                }
            }

            if (allDirectoriesPresent)
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots(string baseDirectory, string currentDirectory)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string root in EnumerateAncestorDirectories(currentDirectory))
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }

        foreach (string root in EnumerateAncestorDirectories(baseDirectory))
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            yield break;
        }

        DirectoryInfo? current = new(Path.GetFullPath(startPath));
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }
}
