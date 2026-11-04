using Chummer.Presentation.Content;
using System.Reflection;

namespace Chummer.Avalonia;

/// <summary>
/// Avalonia host facade over <see cref="CoreContentRootDiscovery"/>: forwards
/// the application base directory so the bundle candidate next to the
/// executable participates in discovery. All validation logic lives in
/// Chummer.Presentation.Content and is unit-tested there.
/// </summary>
public static class DesktopCoreContentRootResolver
{
    public const string BundleDirectoryName = CoreContentRootDiscovery.BundleDirectoryName;
    public const string RuntimeSourceCommit = "8706f38a7b5c29308fe007baa101038f06f61906";
    public const string PackageRecipeCommit = "c29b27f3f6791189cade6c37621773ddd7339ecd";

    public static bool TryResolve(
        string repoRoot,
        out string coreContentRoot,
        Func<string, string?>? resolveHeadCommit = null,
        string? appBaseDirectory = null)
    {
        return CoreContentRootDiscovery.TryResolve(
            repoRoot,
            out coreContentRoot,
            resolveHeadCommit,
            appBaseDirectory,
            readEnvironmentVariable: null,
            expectedBundleManifestSha256: typeof(DesktopCoreContentRootResolver).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(value => value.Key == "ChummerCoreContentManifestSha256")?.Value);
    }

    public static string GetBundleCandidate(string appBaseDirectory)
    {
        return CoreContentRootDiscovery.GetBundleCandidate(appBaseDirectory);
    }
}
