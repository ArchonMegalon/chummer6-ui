using System.Diagnostics;
using System.IO;

namespace Chummer.Avalonia;

/// <summary>
/// Resolves the pinned Core content checkout that authoritative character
/// creation requires. The headless core refuses creation bootstrap with
/// <c>creation-bootstrap-source-context-unavailable</c> unless its content root
/// is a verified Core checkout containing <c>Chummer/data/settings-all-sources.xml</c>;
/// the UI repository's partial <c>Chummer/data</c> mirror is not sufficient.
///
/// Discovery order:
/// 1. <c>CHUMMER_CORE_CONTENT_ROOT</c> (explicit; invalid values fail fast).
/// 2. The resolved UI repository root itself, when it already is a Core checkout.
/// 3. The <c>chummer6-core</c> sibling of the repository root.
///
/// Every candidate must pass structural validation (required content files and
/// directories) and identity validation (git HEAD equals one of the pinned Core
/// commits mirrored from <c>scripts/ai/verify_fresh_checkout_package_plane.py</c>,
/// the release authority for those values). Candidates that fail structural or
/// identity validation are never accepted; an explicitly configured root that
/// fails validation throws so misconfiguration cannot silently fall back.
/// </summary>
public static class DesktopCoreContentRootResolver
{
    private const string CoreContentRootEnvironmentVariable = "CHUMMER_CORE_CONTENT_ROOT";

    /// <summary>Mirrored from CORE_RUNTIME_SOURCE_COMMIT in scripts/ai/verify_fresh_checkout_package_plane.py.</summary>
    public const string RuntimeSourceCommit = "8706f38a7b5c29308fe007baa101038f06f61906";

    /// <summary>Mirrored from CORE_RUNTIME_RECIPE_COMMIT in scripts/ai/verify_fresh_checkout_package_plane.py.</summary>
    public const string PackageRecipeCommit = "c29b27f3f6791189cade6c37621773ddd7339ecd";

    /// <summary>
    /// Attempts to resolve a validated Core content checkout. Returns false when
    /// none is configured or discovered; throws when the explicitly configured
    /// root exists but fails validation.
    /// </summary>
    public static bool TryResolve(string repoRoot, out string coreContentRoot, Func<string, string?>? resolveHeadCommit = null)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        resolveHeadCommit ??= ResolveHeadCommit;

        string? explicitRoot = Environment.GetEnvironmentVariable(CoreContentRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            string normalized = Path.GetFullPath(explicitRoot);
            string? failure = Validate(normalized, resolveHeadCommit);
            if (failure is not null)
            {
                throw new InvalidOperationException(
                    $"{CoreContentRootEnvironmentVariable}='{explicitRoot}' was rejected: {failure}. " +
                    "Point it at a checkout of https://github.com/ArchonMegalon/chummer6-core at the pinned Core content commit.");
            }

            coreContentRoot = normalized;
            return true;
        }

        foreach (string candidate in DiscoverCandidates(repoRoot))
        {
            if (Validate(candidate, resolveHeadCommit) is null)
            {
                coreContentRoot = candidate;
                return true;
            }
        }

        coreContentRoot = string.Empty;
        return false;
    }

    private static IEnumerable<string> DiscoverCandidates(string repoRoot)
    {
        yield return Path.GetFullPath(repoRoot);
        yield return Path.GetFullPath(Path.Combine(repoRoot, "..", "chummer6-core"));
    }

    /// <summary>
    /// Returns null when the candidate is a structurally complete Core content
    /// checkout at a pinned commit; otherwise returns the rejection reason.
    /// </summary>
    private static string? Validate(string candidate, Func<string, string?> resolveHeadCommit)
    {
        if (!Directory.Exists(candidate)) return "directory does not exist";
        if (!File.Exists(Path.Combine(candidate, "Chummer", "data", "settings-all-sources.xml")))
        {
            return "Chummer/data/settings-all-sources.xml is missing";
        }

        if (!File.Exists(Path.Combine(candidate, "Chummer", "data", "settings.xml")))
        {
            return "Chummer/data/settings.xml is missing";
        }

        if (!Directory.Exists(Path.Combine(candidate, "Chummer", "lang"))) return "Chummer/lang is missing";
        if (!Directory.Exists(Path.Combine(candidate, "Chummer", "customdata"))) return "Chummer/customdata is missing";

        string? head = resolveHeadCommit(candidate);
        if (string.IsNullOrWhiteSpace(head)) return "git HEAD could not be read";
        if (!head.Equals(RuntimeSourceCommit, StringComparison.Ordinal)
            && !head.Equals(PackageRecipeCommit, StringComparison.Ordinal))
        {
            return $"git HEAD {head} is not a pinned Core content commit";
        }

        return null;
    }

    private static string? ResolveHeadCommit(string repositoryRoot)
    {
        try
        {
            using Process process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"--no-replace-objects rev-parse HEAD",
                    WorkingDirectory = repositoryRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                })!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(10_000);
            return process.ExitCode == 0 && output.Length == 40 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
