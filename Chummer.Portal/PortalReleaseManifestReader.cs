using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PortalReleaseManifestReader
{
    public static ReleaseManifestSummary Read(string releasesFile)
    {
        if (!File.Exists(releasesFile))
        {
            return new ReleaseManifestSummary("manifest-missing", "unpublished", [], []);
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

            List<ReleaseInstallRouteSummary> installRoutes = [];
            CollectInstallRoutes(primaryNode, installRoutes);
            CollectInstallRoutes(fallbackNode, installRoutes);
            Dictionary<string, string> installRoutesByArtifactId = installRoutes
                .Where(route => !string.IsNullOrWhiteSpace(route.ArtifactId) && !string.IsNullOrWhiteSpace(route.PublicInstallRoute))
                .GroupBy(route => route.ArtifactId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().PublicInstallRoute, StringComparer.OrdinalIgnoreCase);

            List<ReleaseDownloadSummary> downloads = CollectDownloads(primaryNode, installRoutesByArtifactId);
            if (downloads.Count == 0)
            {
                downloads = CollectDownloads(fallbackNode, installRoutesByArtifactId);
            }

            return new ReleaseManifestSummary(status, version, downloads, installRoutes);
        }
        catch (JsonException)
        {
            return new ReleaseManifestSummary("manifest-error", "unpublished", [], []);
        }
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
        IReadOnlyDictionary<string, string> installRoutesByArtifactId)
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
                installRoutesByArtifactId);
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
                installRoutesByArtifactId);
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
        IReadOnlyDictionary<string, string> installRoutesByArtifactId)
    {
        if (item is null)
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

        return new ReleaseDownloadSummary(
            label ?? "artifact",
            platform ?? "unknown",
            url ?? "#",
            resolvedArtifactId,
            fileName ?? string.Empty,
            installAccessClass ?? string.Empty,
            resolvedPublicInstallRoute);
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
    IReadOnlyList<ReleaseDownloadSummary> Downloads,
    IReadOnlyList<ReleaseInstallRouteSummary> InstallRoutes);

internal sealed record ReleaseDownloadSummary(
    string Label,
    string Platform,
    string Url,
    string ArtifactId,
    string FileName,
    string InstallAccessClass,
    string PublicInstallRoute);

internal sealed record ReleaseInstallRouteSummary(
    string PublicInstallRoute,
    string ArtifactId,
    string PromotionState,
    string InstallPosture);
