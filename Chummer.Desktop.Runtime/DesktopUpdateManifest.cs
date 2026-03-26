using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chummer.Desktop.Runtime;

public sealed record DesktopUpdatePlatformIdentity(
    string Platform,
    string Arch)
{
    public static DesktopUpdatePlatformIdentity Current()
    {
        string platform =
            OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "macos"
            : OperatingSystem.IsLinux() ? "linux"
            : "unknown";

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        return new DesktopUpdatePlatformIdentity(platform, arch);
    }
}

public sealed record DesktopUpdateArtifact(
    string ArtifactId,
    string HeadId,
    string Platform,
    string Arch,
    string Kind,
    string FileName,
    string DownloadUrl,
    string? UpdateFeedUrl)
{
    public string Extension
    {
        get
        {
            if (FileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                return ".tar.gz";
            }

            return Path.GetExtension(FileName);
        }
    }

    public bool SupportsInPlaceApply
        => string.Equals(Extension, ".zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Extension, ".tar.gz", StringComparison.OrdinalIgnoreCase);

    public bool SupportsInstallerHandoff
        => !SupportsInPlaceApply
            && (string.Equals(Kind, "installer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Kind, "dmg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Kind, "pkg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Kind, "msix", StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrWhiteSpace(Kind)
                    && string.Equals(Extension, ".exe", StringComparison.OrdinalIgnoreCase))
                || string.Equals(Extension, ".deb", StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrWhiteSpace(Kind) && string.Equals(Extension, ".pkg", StringComparison.OrdinalIgnoreCase))
                || (string.IsNullOrWhiteSpace(Kind) && string.Equals(Extension, ".dmg", StringComparison.OrdinalIgnoreCase))
                || (string.IsNullOrWhiteSpace(Kind) && string.Equals(Extension, ".msix", StringComparison.OrdinalIgnoreCase)));
}

public sealed record DesktopUpdateChannelManifest(
    string ChannelId,
    string Version,
    string Status,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<DesktopUpdateArtifact> Artifacts,
    Uri SourceUri);

public static class DesktopUpdateManifestParser
{
    private static readonly Regex CompatibilityArtifactPattern = new(
        "^chummer-(?<head>avalonia|blazor-desktop)-(?<rid>[^.]+?)(?<installer>-installer)?\\.(?<ext>exe|zip|tar\\.gz|deb|dmg|pkg|msix)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static DesktopUpdateChannelManifest Parse(string json, Uri sourceUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(sourceUri);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("artifacts", out JsonElement artifactsElement) && artifactsElement.ValueKind == JsonValueKind.Array)
        {
            return ParseCanonicalManifest(root, artifactsElement, sourceUri);
        }

        if (root.TryGetProperty("downloads", out JsonElement downloadsElement) && downloadsElement.ValueKind == JsonValueKind.Array)
        {
            return ParseCompatibilityManifest(root, downloadsElement, sourceUri);
        }

        throw new InvalidOperationException("Desktop update manifest did not contain either 'artifacts' or 'downloads'.");
    }

    public static DesktopUpdateArtifact? SelectPreferredArtifact(
        DesktopUpdateChannelManifest manifest,
        string headId,
        DesktopUpdatePlatformIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);
        ArgumentNullException.ThrowIfNull(identity);

        return manifest.Artifacts
            .Where(artifact =>
                string.Equals(artifact.HeadId, headId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(artifact.Platform, identity.Platform, StringComparison.OrdinalIgnoreCase)
                && string.Equals(artifact.Arch, identity.Arch, StringComparison.OrdinalIgnoreCase)
                && (artifact.SupportsInPlaceApply || artifact.SupportsInstallerHandoff))
            .OrderBy(artifact => artifact.SupportsInPlaceApply ? 0 : 1)
            .ThenBy(artifact => KindSortKey(artifact.Kind))
            .ThenBy(artifact => artifact.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static DesktopUpdateChannelManifest ParseCanonicalManifest(
        JsonElement root,
        JsonElement artifactsElement,
        Uri sourceUri)
    {
        List<DesktopUpdateArtifact> artifacts = [];
        foreach (JsonElement element in artifactsElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string artifactId = GetOptionalString(element, "artifactId") ?? GetOptionalString(element, "id") ?? string.Empty;
            string headId = GetOptionalString(element, "head") ?? "unknown";
            string platform = GetOptionalString(element, "platform") ?? "unknown";
            string arch = GetOptionalString(element, "arch") ?? "unknown";
            string kind = GetOptionalString(element, "kind") ?? "artifact";
            string fileName = GetOptionalString(element, "fileName") ?? Path.GetFileName(GetOptionalString(element, "downloadUrl") ?? string.Empty);
            string downloadUrl = GetOptionalString(element, "downloadUrl") ?? string.Empty;
            string? updateFeedUrl = GetOptionalString(element, "updateFeedUrl");
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            artifacts.Add(new DesktopUpdateArtifact(
                ArtifactId: string.IsNullOrWhiteSpace(artifactId) ? fileName : artifactId,
                HeadId: headId,
                Platform: platform,
                Arch: arch,
                Kind: kind,
                FileName: fileName,
                DownloadUrl: downloadUrl,
                UpdateFeedUrl: updateFeedUrl));
        }

        return new DesktopUpdateChannelManifest(
            ChannelId: GetOptionalString(root, "channelId") ?? GetOptionalString(root, "channel") ?? "preview",
            Version: GetOptionalString(root, "version") ?? string.Empty,
            Status: GetOptionalString(root, "status") ?? "published",
            PublishedAt: GetOptionalDateTimeOffset(root, "publishedAt"),
            Artifacts: artifacts,
            SourceUri: sourceUri);
    }

    private static DesktopUpdateChannelManifest ParseCompatibilityManifest(
        JsonElement root,
        JsonElement downloadsElement,
        Uri sourceUri)
    {
        List<DesktopUpdateArtifact> artifacts = [];
        foreach (JsonElement element in downloadsElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string rawUrl = GetOptionalString(element, "url") ?? GetOptionalString(element, "downloadUrl") ?? string.Empty;
            string fileName = GetOptionalString(element, "fileName") ?? Path.GetFileName(rawUrl);
            if (string.IsNullOrWhiteSpace(rawUrl) || string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            Match match = CompatibilityArtifactPattern.Match(fileName);
            string headId = match.Success ? match.Groups["head"].Value.ToLowerInvariant() : "unknown";
            string rid = match.Success ? match.Groups["rid"].Value.ToLowerInvariant() : string.Empty;
            (string platform, string arch) = RidToPlatformAndArch(rid);
            string kind = GetOptionalString(element, "kind") ?? GetOptionalString(element, "flavor") ?? (match.Success
                ? ArtifactKind(match.Groups["ext"].Value.ToLowerInvariant(), match.Groups["installer"].Success)
                : "artifact");

            artifacts.Add(new DesktopUpdateArtifact(
                ArtifactId: GetOptionalString(element, "id") ?? fileName,
                HeadId: headId,
                Platform: platform,
                Arch: arch,
                Kind: kind,
                FileName: fileName,
                DownloadUrl: rawUrl,
                UpdateFeedUrl: null));
        }

        return new DesktopUpdateChannelManifest(
            ChannelId: GetOptionalString(root, "channel") ?? GetOptionalString(root, "channelId") ?? "preview",
            Version: GetOptionalString(root, "version") ?? string.Empty,
            Status: GetOptionalString(root, "status") ?? "published",
            PublishedAt: GetOptionalDateTimeOffset(root, "publishedAt"),
            Artifacts: artifacts,
            SourceUri: sourceUri);
    }

    private static int KindSortKey(string kind)
    {
        return kind.ToLowerInvariant() switch
        {
            "archive" => 0,
            "portable" => 1,
            "artifact" => 2,
            "installer" => 3,
            _ => 4
        };
    }

    private static (string Platform, string Arch) RidToPlatformAndArch(string rid)
    {
        return rid switch
        {
            "win-x64" => ("windows", "x64"),
            "win-arm64" => ("windows", "arm64"),
            "linux-x64" => ("linux", "x64"),
            "linux-arm64" => ("linux", "arm64"),
            "osx-x64" => ("macos", "x64"),
            "osx-arm64" => ("macos", "arm64"),
            _ => ("unknown", "unknown")
        };
    }

    private static string ArtifactKind(string extension, bool installerSuffix)
    {
        if (installerSuffix)
        {
            return "installer";
        }
        if (string.Equals(extension, "exe", StringComparison.OrdinalIgnoreCase))
        {
            return "portable";
        }

        return extension switch
        {
            "zip" => "archive",
            "tar.gz" => "archive",
            "deb" => "installer",
            "dmg" => "dmg",
            "pkg" => "pkg",
            "msix" => "msix",
            _ => "artifact"
        };
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.GetString(),
            _ => property.ToString()
        };
    }

    private static DateTimeOffset? GetOptionalDateTimeOffset(JsonElement element, string propertyName)
    {
        string? raw = GetOptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed
            : null;
    }
}
