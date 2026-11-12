using System.Diagnostics;

namespace Chummer.Presentation.Content;

public static class CoreContentRootDiscovery
{
    public const string BundleDirectoryName = "core-content";
    public const string EnvironmentVariable = "CHUMMER_CORE_CONTENT_ROOT";
    public static readonly string[] PinnedCommits =
    ["2057d3160122608339884cddc129bb6a4abdb6bb", "0751145891d84e124825c59a968c499eb22d2875"];

    public static bool TryResolve(string repoRoot, out string coreContentRoot,
        Func<string, string?>? resolveHeadCommit = null, string? appBaseDirectory = null,
        Func<string, string?>? readEnvironmentVariable = null, string? expectedBundleManifestSha256 = null)
    {
        coreContentRoot = string.Empty;
        string bundle = GetBundleCandidate(appBaseDirectory ?? AppContext.BaseDirectory);
        // A published application is bound to its delivered content, even if a
        // developer checkout or environment variable happens to be present.
        if (!string.IsNullOrWhiteSpace(expectedBundleManifestSha256) || Directory.Exists(bundle))
        {
            string? error = CoreContentBundle.Validate(bundle, PinnedCommits, out _, expectedBundleManifestSha256);
            if (error is not null) throw new InvalidOperationException(error);
            coreContentRoot = bundle;
            return true;
        }
        resolveHeadCommit ??= RealResolveHeadCommit;
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        string? configured = readEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string path = Path.GetFullPath(configured);
            string? error = ValidateAny(path, resolveHeadCommit, out _);
            if (error is not null) throw new InvalidOperationException("The development Core-content override was rejected: " + error);
            coreContentRoot = path;
            return true;
        }
        foreach (string path in new[] { Path.GetFullPath(repoRoot), Path.GetFullPath(Path.Combine(repoRoot, "..", "chummer6-core")) })
        {
            if (ValidateAny(path, resolveHeadCommit, out _) is not null) continue;
            coreContentRoot = path;
            return true;
        }
        return false;
    }

    public static string GetBundleCandidate(string appBaseDirectory)
        => Path.GetFullPath(Path.Combine(appBaseDirectory, BundleDirectoryName));

    public static string? ValidateAny(string candidate, Func<string, string?> resolveHeadCommit,
        out string? failureDetail, string? expectedManifestSha256 = null)
    {
        failureDetail = null;
        if (!Directory.Exists(candidate)) return failureDetail = "Content directory is missing.";
        if (File.Exists(Path.Combine(candidate, CoreContentBundle.ManifestFileName)))
            return failureDetail = CoreContentBundle.Validate(candidate, PinnedCommits, out _, expectedManifestSha256);
        string? structure = CoreContentBundle.ValidateStructure(candidate);
        if (structure is not null) return failureDetail = structure;
        return PinnedCommits.Contains(resolveHeadCommit(candidate), StringComparer.Ordinal)
            ? null : failureDetail = "Development content is not at an approved Core commit.";
    }

    private static string? RealResolveHeadCommit(string root)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "git", Arguments = "--no-replace-objects rev-parse HEAD", WorkingDirectory = root,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            })!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000)) { process.Kill(entireProcessTree: true); return null; }
            string value = output.GetAwaiter().GetResult().Trim();
            error.GetAwaiter().GetResult();
            return process.ExitCode == 0 && value.Length == 40 ? value : null;
        }
        catch (Exception) { return null; }
    }
}
