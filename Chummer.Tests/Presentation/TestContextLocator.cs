using System;
using System.IO;
using System.Collections.Generic;

namespace Chummer.Tests.Presentation;

internal static class TestContextLocator
{
    private const string RepoMarkerFileName = "Chummer.sln";

    public static string ResolveChummerPresentationRepoRoot()
    {
        return TryResolveChummerPresentationRepoRoot(AppContext.BaseDirectory, Directory.GetCurrentDirectory())
            ?? Path.GetFullPath(string.IsNullOrWhiteSpace(Directory.GetCurrentDirectory()) ? AppContext.BaseDirectory : Directory.GetCurrentDirectory());
    }

    private static string? TryResolveChummerPresentationRepoRoot(string baseDirectory, string currentDirectory)
    {
        foreach (string candidate in EnumerateSearchRoots(baseDirectory, currentDirectory))
        {
            if (!File.Exists(Path.Combine(candidate, RepoMarkerFileName)))
            {
                continue;
            }

            if (Directory.Exists(Path.Combine(candidate, "Chummer.Blazor"))
                && Directory.Exists(Path.Combine(candidate, "Chummer.Presentation"))
                && Directory.Exists(Path.Combine(candidate, "Chummer.Tests")))
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
