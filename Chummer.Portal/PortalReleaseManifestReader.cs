using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class PortalReleaseManifestReader
{
    private const string GlobalFlagshipReleaseProfile = "global_flagship";
    private const string PublicStableChannel = "public_stable";

    public static ReleaseManifestSummary Read(string releasesFile)
    {
        if (!File.Exists(releasesFile))
        {
            return EmptySummary("manifest-missing");
        }

        try
        {
            JsonNode? primaryNode = JsonNode.Parse(File.ReadAllText(releasesFile, Encoding.UTF8));
            JsonNode? fallbackNode = LoadSiblingReleaseChannelNode(releasesFile, primaryNode);

            string status = ReadString(primaryNode, "status")
                ?? ReadString(fallbackNode, "status")
                ?? "manifest-error";
            string version = ReadString(primaryNode, "version")
                ?? ReadString(primaryNode, "releaseVersion")
                ?? ReadString(fallbackNode, "version")
                ?? ReadString(fallbackNode, "releaseVersion")
                ?? "unpublished";
            string channel = ReadString(primaryNode, "channelId", "channel")
                ?? ReadString(fallbackNode, "channelId", "channel")
                ?? "unpublished";
            string rolloutState = ReadString(primaryNode, "rolloutState")
                ?? ReadString(fallbackNode, "rolloutState")
                ?? "unpublished";
            string publishedAt = ReadString(primaryNode, "publishedAt", "generatedAt", "generated_at")
                ?? ReadString(fallbackNode, "publishedAt", "generatedAt", "generated_at")
                ?? string.Empty;
            string supportabilityState = ReadString(primaryNode, "supportabilityState")
                ?? ReadString(fallbackNode, "supportabilityState")
                ?? string.Empty;
            string supportabilitySummary = ReadString(primaryNode, "supportabilitySummary")
                ?? ReadString(fallbackNode, "supportabilitySummary")
                ?? string.Empty;
            string releaseProfile = ReadString(primaryNode, "releaseProfile")
                ?? ReadString(fallbackNode, "releaseProfile")
                ?? string.Empty;
            bool isPublicStable = IsPublishedPublicStable(primaryNode);
            bool siblingAgrees = ManifestIdentityAgrees(primaryNode, fallbackNode);

            List<ReleaseInstallRouteSummary> installRoutes = [];
            CollectInstallRoutes(primaryNode, installRoutes);
            if (siblingAgrees)
            {
                CollectInstallRoutes(fallbackNode, installRoutes);
            }

            Dictionary<string, string> installRoutesByArtifactId = installRoutes
                .Where(route => !string.IsNullOrWhiteSpace(route.ArtifactId) && !string.IsNullOrWhiteSpace(route.PublicInstallRoute))
                .GroupBy(route => route.ArtifactId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().PublicInstallRoute, StringComparer.OrdinalIgnoreCase);

            List<ReleaseDownloadSummary> downloads = [];
            if (isPublicStable)
            {
                bool isGlobalFlagship = string.Equals(
                    releaseProfile,
                    GlobalFlagshipReleaseProfile,
                    StringComparison.Ordinal);
                downloads = CollectDownloads(
                    primaryNode,
                    installRoutesByArtifactId,
                    version,
                    isGlobalFlagship);
                if (downloads.Count == 0 && siblingAgrees)
                {
                    downloads = CollectDownloads(
                        fallbackNode,
                        installRoutesByArtifactId,
                        version,
                        isGlobalFlagship);
                }

                downloads = SelectPrimaryDownloads(downloads);
            }

            return new ReleaseManifestSummary(
                status,
                version,
                channel,
                rolloutState,
                publishedAt,
                supportabilityState,
                supportabilitySummary,
                releaseProfile,
                isPublicStable,
                downloads,
                installRoutes);
        }
        catch (JsonException)
        {
            return EmptySummary("manifest-error");
        }
        catch (InvalidOperationException)
        {
            return EmptySummary("manifest-error");
        }
        catch (FormatException)
        {
            return EmptySummary("manifest-error");
        }
        catch (IOException)
        {
            return EmptySummary("manifest-error");
        }
        catch (UnauthorizedAccessException)
        {
            return EmptySummary("manifest-error");
        }
    }

    private static ReleaseManifestSummary EmptySummary(string status)
        => new(
            status,
            "unpublished",
            "unpublished",
            "unpublished",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            [],
            []);

    private static bool IsPublishedPublicStable(JsonNode? node)
        => string.Equals(ReadString(node, "status"), "published", StringComparison.OrdinalIgnoreCase)
           && string.Equals(ReadString(node, "channelId", "channel"), PublicStableChannel, StringComparison.OrdinalIgnoreCase)
           && string.Equals(ReadString(node, "rolloutState"), PublicStableChannel, StringComparison.OrdinalIgnoreCase);

    private static bool ManifestIdentityAgrees(JsonNode? primaryNode, JsonNode? fallbackNode)
    {
        if (primaryNode is null || fallbackNode is null)
        {
            return false;
        }

        if (ReferenceEquals(primaryNode, fallbackNode))
        {
            return true;
        }

        return string.Equals(
                   ReadString(primaryNode, "version", "releaseVersion"),
                   ReadString(fallbackNode, "version", "releaseVersion"),
                   StringComparison.Ordinal)
               && string.Equals(
                   ReadString(primaryNode, "channelId", "channel"),
                   ReadString(fallbackNode, "channelId", "channel"),
                   StringComparison.OrdinalIgnoreCase)
               && string.Equals(
                   ReadString(primaryNode, "status"),
                   ReadString(fallbackNode, "status"),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static JsonNode? LoadSiblingReleaseChannelNode(string releasesFile, JsonNode? primaryNode)
    {
        string fileName = Path.GetFileName(releasesFile);
        if (string.Equals(fileName, "RELEASE_CHANNEL.generated.json", StringComparison.OrdinalIgnoreCase))
        {
            return primaryNode;
        }

        string siblingPath = Path.Combine(Path.GetDirectoryName(releasesFile) ?? string.Empty, "RELEASE_CHANNEL.generated.json");
        if (!File.Exists(siblingPath))
        {
            return null;
        }

        return JsonNode.Parse(File.ReadAllText(siblingPath, Encoding.UTF8));
    }

    private static List<ReleaseDownloadSummary> CollectDownloads(
        JsonNode? node,
        IReadOnlyDictionary<string, string> installRoutesByArtifactId,
        string releaseVersion,
        bool isGlobalFlagship)
    {
        List<ReleaseDownloadSummary> downloads = [];
        if (node is null)
        {
            return downloads;
        }

        foreach (JsonNode? item in node["downloads"]?.AsArray() ?? [])
        {
            ReleaseDownloadSummary? download = BuildDownloadSummary(
                item,
                label: ReadString(item, "label", "fileName"),
                platform: ReadString(item, "platform"),
                url: ReadString(item, "url", "downloadUrl"),
                artifactId: ReadString(item, "artifactId", "id"),
                fileName: ReadString(item, "fileName"),
                installAccessClass: ReadString(item, "installAccessClass"),
                publicInstallRoute: ReadString(item, "publicInstallRoute"),
                installRoutesByArtifactId,
                releaseVersion,
                isGlobalFlagship);
            if (download is not null)
            {
                downloads.Add(download);
            }
        }

        if (downloads.Count > 0)
        {
            return downloads;
        }

        foreach (JsonNode? item in node["artifacts"]?.AsArray() ?? [])
        {
            ReleaseDownloadSummary? download = BuildDownloadSummary(
                item,
                label: ReadString(item, "platformLabel", "label", "fileName", "artifactId", "id"),
                platform: ReadString(item, "platform"),
                url: ReadString(item, "downloadUrl", "url"),
                artifactId: ReadString(item, "artifactId", "id"),
                fileName: ReadString(item, "fileName"),
                installAccessClass: ReadString(item, "installAccessClass"),
                publicInstallRoute: ReadString(item, "publicInstallRoute"),
                installRoutesByArtifactId,
                releaseVersion,
                isGlobalFlagship);
            if (download is not null)
            {
                downloads.Add(download);
            }
        }

        return downloads;
    }

    private static ReleaseDownloadSummary? BuildDownloadSummary(
        JsonNode? item,
        string? label,
        string? platform,
        string? url,
        string? artifactId,
        string? fileName,
        string? installAccessClass,
        string? publicInstallRoute,
        IReadOnlyDictionary<string, string> installRoutesByArtifactId,
        string releaseVersion,
        bool isGlobalFlagship)
    {
        if (item is null)
        {
            return null;
        }

        string rowChannel = ReadString(item, "channelId", "channel") ?? string.Empty;
        string rowVersion = ReadString(item, "version", "releaseVersion") ?? string.Empty;
        string compatibilityState = ReadString(item, "compatibilityState") ?? string.Empty;
        if (!string.Equals(rowChannel, PublicStableChannel, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rowVersion, releaseVersion, StringComparison.Ordinal)
            || !string.Equals(installAccessClass, "open_public", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(compatibilityState, "compatible", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string resolvedArtifactId = artifactId ?? string.Empty;
        string resolvedPublicInstallRoute = publicInstallRoute ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedPublicInstallRoute)
            && !string.IsNullOrWhiteSpace(resolvedArtifactId)
            && installRoutesByArtifactId.TryGetValue(resolvedArtifactId, out string? mappedRoute))
        {
            resolvedPublicInstallRoute = mappedRoute;
        }

        string resolvedFileName = fileName ?? string.Empty;
        string resolvedUrl = url ?? string.Empty;
        string platformToken = ResolvePlatformToken(item, platform, resolvedArtifactId);
        string head = NormalizeToken(ReadString(item, "head"));
        string sha256 = NormalizeToken(ReadString(item, "sha256"));
        long? sizeBytes = ReadPositiveInteger(item, "sizeBytes");
        if (string.IsNullOrWhiteSpace(resolvedArtifactId)
            || string.IsNullOrWhiteSpace(resolvedFileName)
            || string.IsNullOrWhiteSpace(resolvedUrl)
            || !IsSupportedPlatform(platformToken)
            || string.IsNullOrWhiteSpace(head)
            || !IsSha256(sha256)
            || sizeBytes is null)
        {
            return null;
        }

        string securityState = ResolveSecurityState(item, platformToken, isGlobalFlagship);
        if (isGlobalFlagship
            && string.Equals(platformToken, "macos", StringComparison.Ordinal)
            && !string.Equals(securityState, "signed_notarized", StringComparison.Ordinal))
        {
            return null;
        }

        return new ReleaseDownloadSummary(
            label ?? "artifact",
            platformToken,
            PlatformDisplayName(platformToken),
            ReadString(item, "arch") ?? ArchitectureFromRid(ReadString(item, "rid")),
            head,
            resolvedUrl,
            resolvedArtifactId,
            resolvedFileName,
            sha256,
            sizeBytes.Value,
            ResolveFileFormat(item, resolvedFileName),
            securityState,
            "open_public",
            resolvedPublicInstallRoute);
    }

    private static List<ReleaseDownloadSummary> SelectPrimaryDownloads(
        IEnumerable<ReleaseDownloadSummary> downloads)
    {
        List<ReleaseDownloadSummary> primaryDownloads = [];
        foreach (string platform in new[] { "windows", "linux", "macos" })
        {
            ReleaseDownloadSummary? primary = downloads.FirstOrDefault(download =>
                string.Equals(download.Platform, platform, StringComparison.Ordinal)
                && string.Equals(download.Head, "avalonia", StringComparison.Ordinal));
            if (primary is not null)
            {
                primaryDownloads.Add(primary);
            }
        }

        return primaryDownloads;
    }

    private static string ResolvePlatformToken(JsonNode item, string? platform, string artifactId)
    {
        foreach (string? candidate in new[]
                 {
                     ReadString(item, "platformId"),
                     platform,
                     ReadString(item, "rid"),
                     artifactId
                 })
        {
            string normalized = NormalizeToken(candidate);
            if (normalized.Contains("windows", StringComparison.Ordinal)
                || normalized.StartsWith("win-", StringComparison.Ordinal)
                || normalized.Contains("-win-", StringComparison.Ordinal))
            {
                return "windows";
            }

            if (normalized.Contains("linux", StringComparison.Ordinal))
            {
                return "linux";
            }

            if (normalized.Contains("macos", StringComparison.Ordinal)
                || normalized.Contains("osx", StringComparison.Ordinal))
            {
                return "macos";
            }
        }

        return string.Empty;
    }

    private static string ResolveSecurityState(
        JsonNode item,
        string platform,
        bool isGlobalFlagship)
    {
        string signingStatus = NormalizeToken(ReadString(item, "signingStatus"));
        string notarizationStatus = NormalizeToken(ReadString(item, "notarizationStatus"));
        JsonNode? macosEvidence = item["macosFlagshipEvidence"];
        bool hasBoundMacosEvidence = HasBoundMacosEvidence(macosEvidence);

        if (string.Equals(platform, "macos", StringComparison.Ordinal))
        {
            if (isGlobalFlagship)
            {
                return hasBoundMacosEvidence ? "signed_notarized" : "digest_published";
            }

            if ((IsPassing(signingStatus) && IsAcceptedNotarization(notarizationStatus))
                || hasBoundMacosEvidence)
            {
                return "signed_notarized";
            }
        }

        if (string.Equals(platform, "windows", StringComparison.Ordinal)
            && (IsPassing(signingStatus) || isGlobalFlagship))
        {
            return "signed";
        }

        if (string.Equals(platform, "linux", StringComparison.Ordinal)
            && isGlobalFlagship)
        {
            return "package_verified";
        }

        return "digest_published";
    }

    private static bool IsPassing(string value)
        => value is "pass" or "passed" or "valid" or "verified";

    private static bool IsAcceptedNotarization(string value)
        => value is "accepted" or "pass" or "passed";

    private static bool HasBoundMacosEvidence(JsonNode? evidence)
    {
        JsonNode? signingIdentity = evidence?["signingIdentity"];
        JsonNode? notarization = evidence?["notarization"];
        string developerIdIdentity = ReadString(
            signingIdentity,
            "developerIdApplicationIdentity") ?? string.Empty;
        string teamId = ReadString(signingIdentity, "teamId") ?? string.Empty;
        string certificateSha256 = ReadString(
            signingIdentity,
            "certificateSha256") ?? string.Empty;
        string certificateSpkiSha256 = ReadString(
            signingIdentity,
            "certificateSpkiSha256") ?? string.Empty;
        string notarizationStatus = ReadString(notarization, "status") ?? string.Empty;
        string submissionId = ReadString(notarization, "submissionId") ?? string.Empty;

        Match identityMatch = Regex.Match(
            developerIdIdentity,
            @"^Developer ID Application:.+\(([A-Z0-9]{10})\)$",
            RegexOptions.CultureInvariant);

        return identityMatch.Success
               && Regex.IsMatch(teamId, @"^[A-Z0-9]{10}$", RegexOptions.CultureInvariant)
               && string.Equals(identityMatch.Groups[1].Value, teamId, StringComparison.Ordinal)
               && Regex.IsMatch(certificateSha256, @"^[0-9a-f]{64}$", RegexOptions.CultureInvariant)
               && Regex.IsMatch(certificateSpkiSha256, @"^[0-9a-f]{64}$", RegexOptions.CultureInvariant)
               && string.Equals(notarizationStatus, "Accepted", StringComparison.Ordinal)
               && Regex.IsMatch(
                   submissionId,
                   @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
                   RegexOptions.CultureInvariant);
    }

    private static string ResolveFileFormat(JsonNode item, string fileName)
    {
        string format = NormalizeToken(ReadString(item, "format"));
        if (!string.IsNullOrWhiteSpace(format))
        {
            return format;
        }

        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return "tar.gz";
        }

        return Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
    }

    private static string ArchitectureFromRid(string? rid)
    {
        string normalized = NormalizeToken(rid);
        int separatorIndex = normalized.LastIndexOf('-');
        return separatorIndex >= 0 && separatorIndex < normalized.Length - 1
            ? normalized[(separatorIndex + 1)..]
            : normalized;
    }

    private static string PlatformDisplayName(string platform)
        => platform switch
        {
            "windows" => "Windows",
            "linux" => "Linux",
            "macos" => "macOS",
            _ => "Desktop"
        };

    private static bool IsSupportedPlatform(string platform)
        => platform is "windows" or "linux" or "macos";

    private static string NormalizeToken(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static long? ReadPositiveInteger(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is not JsonValue value
            || !value.TryGetValue(out long result)
            || result <= 0)
        {
            return null;
        }

        return result;
    }

    private static string? ReadString(JsonNode? node, params string[] propertyNames)
    {
        if (node is null)
        {
            return null;
        }

        foreach (string propertyName in propertyNames)
        {
            string? value = node[propertyName]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static void CollectInstallRoutes(JsonNode? node, List<ReleaseInstallRouteSummary> installRoutes)
    {
        if (node is null)
        {
            return;
        }

        if (node is JsonObject jsonObject)
        {
            string publicInstallRoute = ReadString(jsonObject, "publicInstallRoute") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(publicInstallRoute)
                && !installRoutes.Any(route => string.Equals(route.PublicInstallRoute, publicInstallRoute, StringComparison.OrdinalIgnoreCase)))
            {
                installRoutes.Add(new ReleaseInstallRouteSummary(
                    publicInstallRoute,
                    ReadString(jsonObject, "artifactId") ?? string.Empty,
                    ReadString(jsonObject, "promotionState") ?? string.Empty,
                    ReadString(jsonObject, "installPosture") ?? "proof_required"));
            }

            foreach ((_, JsonNode? child) in jsonObject)
            {
                CollectInstallRoutes(child, installRoutes);
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? child in jsonArray)
            {
                CollectInstallRoutes(child, installRoutes);
            }
        }
    }
}

internal sealed record ReleaseManifestSummary(
    string Status,
    string Version,
    string Channel,
    string RolloutState,
    string PublishedAt,
    string SupportabilityState,
    string SupportabilitySummary,
    string ReleaseProfile,
    bool IsPublicStable,
    IReadOnlyList<ReleaseDownloadSummary> Downloads,
    IReadOnlyList<ReleaseInstallRouteSummary> InstallRoutes);

internal sealed record ReleaseDownloadSummary(
    string Label,
    string Platform,
    string PlatformLabel,
    string Architecture,
    string Head,
    string Url,
    string ArtifactId,
    string FileName,
    string Sha256,
    long SizeBytes,
    string Format,
    string SecurityState,
    string InstallAccessClass,
    string PublicInstallRoute);

internal sealed record ReleaseInstallRouteSummary(
    string PublicInstallRoute,
    string ArtifactId,
    string PromotionState,
    string InstallPosture);
