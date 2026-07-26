using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class PortalReleaseManifestReader
{
    private const string GlobalFlagshipReleaseProfile = "global_flagship";
    private const string MacosEvidenceBindingContract = "chummer.registry.macos-flagship-evidence-binding";
    private const string MacosEvidenceSourceContract = "chummer6-ui.macos-flagship-evidence";
    private const string PublicStableChannel = "public_stable";
    // Canonical sorted-key JSON digest of the complete 13-field coverage
    // projection emitted by Registry 8f02 for the three-platform flagship.
    private const string Registry8f02GlobalCoverageSha256 =
        "eee485e0cade5ccddfe8637eb4b99a5a2a31ee90f99d0703d74479cc2a76f11a";
    private const string UiGlobalPromotionContract =
        "chummer6-ui.global-flagship-channel-promotion-authority.v1";

    public static ReleaseManifestSummary Read(string releasesFile)
    {
        if (!File.Exists(releasesFile))
        {
            return EmptySummary("manifest-missing");
        }

        try
        {
            JsonNode? primaryNode = JsonNode.Parse(File.ReadAllText(releasesFile, Encoding.UTF8));
            if (!TryReadManifestIdentity(primaryNode, out ManifestIdentity identity))
            {
                return EmptySummary("manifest-error");
            }

            bool isPublicStable = IsPublishedPublicStable(identity);
            bool isGlobalFlagship = string.Equals(
                identity.ReleaseProfile,
                GlobalFlagshipReleaseProfile,
                StringComparison.Ordinal);
            GlobalFlagshipAuthority? globalAuthority = null;
            if (isGlobalFlagship
                && !TryReadGlobalFlagshipAuthority(
                    primaryNode,
                    identity,
                    out globalAuthority))
            {
                return EmptySummary("manifest-error");
            }

            JsonNode? fallbackNode = isGlobalFlagship
                ? null
                : LoadSiblingReleaseChannelNode(releasesFile, primaryNode);
            bool siblingAgrees = ManifestIdentityAgrees(identity, fallbackNode);

            List<ReleaseInstallRouteSummary> installRoutes = [];
            CollectInstallRoutes(primaryNode, installRoutes);
            if (siblingAgrees)
            {
                CollectInstallRoutes(fallbackNode, installRoutes);
            }

            List<ReleaseDownloadSummary> downloads = [];
            if (isPublicStable)
            {
                downloads = CollectDownloads(
                    primaryNode,
                    identity.Version,
                    globalAuthority);
                if (downloads.Count == 0 && siblingAgrees && !isGlobalFlagship)
                {
                    downloads = CollectDownloads(
                        fallbackNode,
                        identity.Version,
                        globalAuthority);
                }

                downloads = SelectPrimaryDownloads(downloads);
                if (isGlobalFlagship && downloads.Count != 3)
                {
                    downloads = [];
                }
            }

            return new ReleaseManifestSummary(
                identity.Status,
                identity.Version,
                identity.Channel,
                identity.RolloutState,
                identity.PublishedAt,
                identity.SupportabilityState,
                identity.SupportabilitySummary,
                identity.ReleaseProfile,
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

    private static bool IsPublishedPublicStable(ManifestIdentity identity)
        => string.Equals(identity.Status, "published", StringComparison.Ordinal)
           && string.Equals(identity.Channel, PublicStableChannel, StringComparison.Ordinal)
           && string.Equals(identity.RolloutState, PublicStableChannel, StringComparison.Ordinal);

    private static bool ManifestIdentityAgrees(
        ManifestIdentity primaryIdentity,
        JsonNode? fallbackNode)
    {
        if (!TryReadManifestIdentity(fallbackNode, out ManifestIdentity fallbackIdentity))
        {
            return false;
        }

        return primaryIdentity == fallbackIdentity;
    }

    private static bool TryReadManifestIdentity(
        JsonNode? node,
        out ManifestIdentity identity)
    {
        identity = default!;
        if (node is not JsonObject)
        {
            return false;
        }

        string version = ReadString(node, "version") ?? string.Empty;
        string releaseVersion = ReadString(node, "releaseVersion") ?? string.Empty;
        string channelId = ReadString(node, "channelId") ?? string.Empty;
        string channel = ReadString(node, "channel") ?? string.Empty;
        string contractName = ReadString(node, "contractName") ?? string.Empty;
        string legacyContractName = ReadString(node, "contract_name") ?? string.Empty;
        string registryCommit = ReadString(node, "registryCommit") ?? string.Empty;
        string legacyRegistryCommit = ReadString(node, "registry_commit") ?? string.Empty;
        string status = ReadString(node, "status") ?? string.Empty;
        string rolloutState = ReadString(node, "rolloutState") ?? string.Empty;
        string publishedAt = ReadString(node, "publishedAt") ?? string.Empty;
        string supportabilityState = ReadString(node, "supportabilityState") ?? string.Empty;
        string releaseProfile = ReadString(node, "releaseProfile") ?? string.Empty;
        long? schemaVersion = ReadInteger(node, "schemaVersion");
        long? contractVersion = ReadInteger(node, "contractVersion");

        if (!TryReadGeneratedTimestamp(node, out string generatedAt)
            || string.IsNullOrWhiteSpace(version)
            || !string.Equals(version, releaseVersion, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(status)
            || string.IsNullOrWhiteSpace(rolloutState)
            || string.IsNullOrWhiteSpace(publishedAt)
            || string.IsNullOrWhiteSpace(supportabilityState)
            || (string.IsNullOrWhiteSpace(channelId) && string.IsNullOrWhiteSpace(channel))
            || (!string.IsNullOrWhiteSpace(channelId)
                && !string.IsNullOrWhiteSpace(channel)
                && !string.Equals(channelId, channel, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(contractName)
                && !string.IsNullOrWhiteSpace(legacyContractName)
                && !string.Equals(contractName, legacyContractName, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(registryCommit)
                && !string.IsNullOrWhiteSpace(legacyRegistryCommit)
                && !string.Equals(registryCommit, legacyRegistryCommit, StringComparison.Ordinal)))
        {
            return false;
        }

        bool hasGlobalIntent = string.Equals(
                                   releaseProfile,
                                   GlobalFlagshipReleaseProfile,
                                   StringComparison.Ordinal)
                               || schemaVersion == 2
                               || contractVersion == 2
                               || node["channelPromotionAuthority"] is not null;
        if (hasGlobalIntent
            && (schemaVersion != 2
                || contractVersion != 2
                || !string.Equals(
                    releaseProfile,
                    GlobalFlagshipReleaseProfile,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        identity = new ManifestIdentity(
            status,
            version,
            string.IsNullOrWhiteSpace(channelId) ? channel : channelId,
            rolloutState,
            generatedAt,
            publishedAt,
            supportabilityState,
            ReadString(node, "supportabilitySummary") ?? string.Empty,
            releaseProfile,
            schemaVersion,
            contractVersion,
            string.IsNullOrWhiteSpace(contractName) ? legacyContractName : contractName,
            string.IsNullOrWhiteSpace(registryCommit)
                ? legacyRegistryCommit
                : registryCommit);
        return true;
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

        try
        {
            return JsonNode.Parse(File.ReadAllText(siblingPath, Encoding.UTF8));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryReadGlobalFlagshipAuthority(
        JsonNode? node,
        ManifestIdentity identity,
        out GlobalFlagshipAuthority? authority)
    {
        authority = null;
        bool hasDownloads = node?["downloads"] is JsonArray;
        bool hasArtifacts = node?["artifacts"] is JsonArray;
        if (node is not JsonObject
            || identity.SchemaVersion != 2
            || identity.ContractVersion != 2
            || !string.Equals(
                identity.ReleaseProfile,
                GlobalFlagshipReleaseProfile,
                StringComparison.Ordinal)
            || !string.Equals(identity.Status, "published", StringComparison.Ordinal)
            || !string.Equals(identity.Channel, PublicStableChannel, StringComparison.Ordinal)
            || !string.Equals(identity.RolloutState, PublicStableChannel, StringComparison.Ordinal)
            || !string.Equals(
                identity.SupportabilityState,
                "gold_supported",
                StringComparison.Ordinal)
            || !string.Equals(
                identity.ContractName,
                "Chummer.Hub.Registry.Contracts",
                StringComparison.Ordinal)
            || !IsLowerCommit(identity.RegistryCommit)
            || hasDownloads == hasArtifacts)
        {
            return false;
        }

        JsonArray inventory = (node["downloads"] as JsonArray)
            ?? (node["artifacts"] as JsonArray)
            ?? [];
        if (inventory.Count != 3)
        {
            return false;
        }
        if (!HasCompleteDesktopTupleCoverage(
                node["desktopTupleCoverage"],
                inventory))
        {
            return false;
        }

        JsonNode? promotion = node["channelPromotionAuthority"];
        if (!HasExactKeys(
                promotion,
                "contractName",
                "contractVersion",
                "source",
                "candidateId",
                "releaseVersion",
                "releaseProfile",
                "sourceChannel",
                "targetChannel",
                "artifactInventorySha256",
                "destinationIntent",
                "candidateManifest",
                "finalApprovalReceipt",
                "registryProjectionAuthorized",
                "publicationMutationAuthorized",
                "assembly")
            || !string.Equals(
                ReadString(promotion, "contractName"),
                "chummer.registry.global-flagship-channel-promotion",
                StringComparison.Ordinal)
            || ReadInteger(promotion, "contractVersion") != 1
            || !IsAuthorityToken(ReadString(promotion, "candidateId"))
            || !string.Equals(
                ReadString(promotion, "releaseVersion"),
                identity.Version,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(promotion, "releaseProfile"),
                GlobalFlagshipReleaseProfile,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(promotion, "sourceChannel"),
                "preview",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(promotion, "targetChannel"),
                PublicStableChannel,
                StringComparison.Ordinal)
            || ReadBoolean(promotion, "registryProjectionAuthorized") is not true
            || ReadBoolean(promotion, "publicationMutationAuthorized") is not false)
        {
            return false;
        }

        string inventoryDigest = ComputeGlobalFlagshipInventorySha256(inventory);
        if (string.IsNullOrWhiteSpace(inventoryDigest)
            || !string.Equals(
                ReadString(promotion, "artifactInventorySha256"),
                inventoryDigest,
                StringComparison.Ordinal))
        {
            return false;
        }

        JsonNode? source = promotion?["source"];
        if (!HasExactKeys(
                source,
                "contractName",
                "contractVersion",
                "sha256",
                "sizeBytes")
            || !string.Equals(
                ReadString(source, "contractName"),
                UiGlobalPromotionContract,
                StringComparison.Ordinal)
            || ReadInteger(source, "contractVersion") != 1
            || !IsLowerSha256(ReadString(source, "sha256"))
            || ReadPositiveInteger(source, "sizeBytes") is null
            || !HasPromotionReference(
                promotion?["destinationIntent"],
                "destination-intent.json")
            || !HasPromotionReference(
                promotion?["candidateManifest"],
                "GLOBAL_FLAGSHIP_CANDIDATE.generated.json")
            || !HasPromotionReference(
                promotion?["finalApprovalReceipt"],
                "final-receipt.json"))
        {
            return false;
        }

        JsonNode? assembly = promotion?["assembly"];
        string actor = ReadString(assembly, "actor") ?? string.Empty;
        string triggeringActor = ReadString(assembly, "triggeringActor") ?? string.Empty;
        string sourceCommit = ReadString(assembly, "sha") ?? string.Empty;
        if (!HasExactKeys(
                assembly,
                "repository",
                "workflow",
                "ref",
                "sha",
                "runId",
                "runAttempt",
                "actor",
                "triggeringActor",
                "environment")
            || !string.Equals(
                ReadString(assembly, "repository"),
                "ArchonMegalon/chummer6-ui",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(assembly, "workflow"),
                ".github/workflows/global-flagship-publication-input-assembly.yml",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(assembly, "ref"),
                "refs/heads/main",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(assembly, "environment"),
                "global-flagship-publication-input-assembly",
                StringComparison.Ordinal)
            || !IsLowerCommit(sourceCommit)
            || ReadPositiveInteger(assembly, "runId") is null
            || ReadInteger(assembly, "runAttempt") != 1
            || !IsGithubLogin(actor)
            || !IsGithubLogin(triggeringActor)
            || !string.Equals(actor, triggeringActor, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        authority = new GlobalFlagshipAuthority(
            ReadString(promotion, "candidateId")!,
            sourceCommit,
            identity.Version);
        return true;
    }

    private static bool HasCompleteDesktopTupleCoverage(
        JsonNode? node,
        JsonArray inventory)
    {
        JsonNode? promotedPlatformHeads = node?["promotedPlatformHeads"];
        if (!HasExactKeys(
                node,
                "requiredDesktopPlatforms",
                "requiredDesktopHeads",
                "promotedInstallerTuples",
                "promotedPlatformHeads",
                "requiredDesktopPlatformHeadRidTuples",
                "promotedPlatformHeadRidTuples",
                "missingRequiredPlatforms",
                "missingRequiredHeads",
                "missingRequiredPlatformHeadPairs",
                "missingRequiredPlatformHeadRidTuples",
                "externalProofRequests",
                "desktopRouteTruth",
                "complete")
            || !JsonArrayMatches(
                node?["requiredDesktopPlatforms"],
                "linux",
                "windows",
                "macos")
            || !JsonArrayMatches(node?["requiredDesktopHeads"], "avalonia")
            || !JsonArrayMatches(
                node?["requiredDesktopPlatformHeadRidTuples"],
                "avalonia:linux-x64:linux",
                "avalonia:osx-arm64:macos",
                "avalonia:win-x64:windows")
            || !JsonArrayMatches(
                node?["promotedPlatformHeadRidTuples"],
                "avalonia:linux-x64:linux",
                "avalonia:osx-arm64:macos",
                "avalonia:win-x64:windows")
            || !JsonArrayMatches(node?["missingRequiredPlatforms"])
            || !JsonArrayMatches(node?["missingRequiredHeads"])
            || !JsonArrayMatches(node?["missingRequiredPlatformHeadPairs"])
            || !JsonArrayMatches(node?["missingRequiredPlatformHeadRidTuples"])
            || !JsonArrayMatches(node?["externalProofRequests"])
            || !HasExactKeys(
                promotedPlatformHeads,
                "linux",
                "macos",
                "windows")
            || !JsonArrayMatches(promotedPlatformHeads?["linux"], "avalonia")
            || !JsonArrayMatches(promotedPlatformHeads?["macos"], "avalonia")
            || !JsonArrayMatches(promotedPlatformHeads?["windows"], "avalonia")
            || ReadBoolean(node, "complete") is not true
            || !PromotedInstallerTuplesMatchInventory(
                node?["promotedInstallerTuples"],
                inventory))
        {
            return false;
        }

        return string.Equals(
            ComputeCanonicalJsonSha256(node),
            Registry8f02GlobalCoverageSha256,
            StringComparison.Ordinal);
    }

    private static bool PromotedInstallerTuplesMatchInventory(
        JsonNode? promotedNode,
        JsonArray inventory)
    {
        if (promotedNode is not JsonArray promotedRows
            || promotedRows.Count != 3
            || inventory.Count != 3)
        {
            return false;
        }

        HashSet<string> inventoryBindings = new(StringComparer.Ordinal);
        foreach (JsonNode? row in inventory)
        {
            if (row is not JsonObject)
            {
                return false;
            }

            inventoryBindings.Add(string.Join(
                '\u001f',
                ReadString(row, "artifactId") ?? string.Empty,
                ReadString(row, "head") ?? string.Empty,
                ReadString(row, "platform") ?? string.Empty,
                ReadString(row, "rid") ?? string.Empty,
                ReadString(row, "arch") ?? string.Empty,
                ReadString(row, "kind") ?? string.Empty));
        }

        HashSet<string> promotedBindings = new(StringComparer.Ordinal);
        foreach (JsonNode? row in promotedRows)
        {
            string head = ReadString(row, "head") ?? string.Empty;
            string platform = ReadString(row, "platform") ?? string.Empty;
            string rid = ReadString(row, "rid") ?? string.Empty;
            if (!HasExactKeys(
                    row,
                    "tupleId",
                    "head",
                    "platform",
                    "rid",
                    "arch",
                    "kind",
                    "artifactId")
                || !string.Equals(
                    ReadString(row, "tupleId"),
                    $"{head}:{platform}:{rid}",
                    StringComparison.Ordinal))
            {
                return false;
            }

            promotedBindings.Add(string.Join(
                '\u001f',
                ReadString(row, "artifactId") ?? string.Empty,
                head,
                platform,
                rid,
                ReadString(row, "arch") ?? string.Empty,
                ReadString(row, "kind") ?? string.Empty));
        }

        return inventoryBindings.Count == 3
               && promotedBindings.Count == 3
               && inventoryBindings.SetEquals(promotedBindings);
    }

    private static bool HasPromotionReference(JsonNode? node, string expectedPath)
        => HasExactKeys(node, "path", "sha256", "sizeBytes")
           && string.Equals(
               ReadString(node, "path"),
               expectedPath,
               StringComparison.Ordinal)
           && IsLowerSha256(ReadString(node, "sha256"))
           && ReadPositiveInteger(node, "sizeBytes") is not null;

    private static string ComputeGlobalFlagshipInventorySha256(JsonArray inventory)
    {
        List<SortedDictionary<string, object>> rows = [];
        foreach (JsonNode? item in inventory)
        {
            long? sizeBytes = ReadPositiveInteger(item, "sizeBytes");
            string sha256 = ReadString(item, "sha256") ?? string.Empty;
            if (item is not JsonObject
                || sizeBytes is null
                || !IsLowerSha256(sha256))
            {
                return string.Empty;
            }

            rows.Add(new SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["artifactId"] = ReadString(item, "artifactId", "id") ?? string.Empty,
                ["fileName"] = ReadString(item, "fileName") ?? string.Empty,
                ["platform"] = ReadString(item, "platform") ?? string.Empty,
                ["rid"] = ReadString(item, "rid") ?? string.Empty,
                ["sha256"] = sha256,
                ["sizeBytes"] = sizeBytes.Value
            });
        }

        rows.Sort((left, right) => string.Compare(
            Convert.ToString(left["platform"]),
            Convert.ToString(right["platform"]),
            StringComparison.Ordinal));
        byte[] canonicalBytes = JsonSerializer.SerializeToUtf8Bytes(rows);
        return Convert.ToHexString(SHA256.HashData(canonicalBytes)).ToLowerInvariant();
    }

    private static string ComputeCanonicalJsonSha256(JsonNode? node)
    {
        using MemoryStream canonical = new();
        using (Utf8JsonWriter writer = new(canonical))
        {
            WriteCanonicalJson(writer, node);
        }

        return Convert.ToHexString(
            SHA256.HashData(canonical.ToArray())).ToLowerInvariant();
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case null:
                writer.WriteNullValue();
                return;
            case JsonObject jsonObject:
                writer.WriteStartObject();
                foreach ((string key, JsonNode? value) in jsonObject.OrderBy(
                             pair => pair.Key,
                             StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    WriteCanonicalJson(writer, value);
                }

                writer.WriteEndObject();
                return;
            case JsonArray jsonArray:
                writer.WriteStartArray();
                foreach (JsonNode? value in jsonArray)
                {
                    WriteCanonicalJson(writer, value);
                }

                writer.WriteEndArray();
                return;
            default:
                node.WriteTo(writer);
                return;
        }
    }

    private static List<ReleaseDownloadSummary> CollectDownloads(
        JsonNode? node,
        string releaseVersion,
        GlobalFlagshipAuthority? globalAuthority)
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
                compatibilityProjection: true,
                releaseVersion,
                globalAuthority);
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
                compatibilityProjection: false,
                releaseVersion,
                globalAuthority);
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
        bool compatibilityProjection,
        string releaseVersion,
        GlobalFlagshipAuthority? globalAuthority)
    {
        if (item is null)
        {
            return null;
        }

        string rowChannelId = ReadString(item, "channelId") ?? string.Empty;
        string rowChannel = ReadString(item, "channel") ?? string.Empty;
        string rowVersion = ReadString(item, "version") ?? string.Empty;
        string rowReleaseVersion = ReadString(item, "releaseVersion") ?? string.Empty;
        string compatibilityState = ReadString(item, "compatibilityState") ?? string.Empty;
        bool isGlobalFlagship = globalAuthority is not null;
        if ((!string.IsNullOrWhiteSpace(rowChannelId)
             && !string.Equals(rowChannelId, PublicStableChannel, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(rowChannel)
                && !string.Equals(rowChannel, PublicStableChannel, StringComparison.Ordinal))
            || (!isGlobalFlagship
                && string.IsNullOrWhiteSpace(rowChannelId)
                && string.IsNullOrWhiteSpace(rowChannel))
            || !string.Equals(rowVersion, releaseVersion, StringComparison.Ordinal)
            || !string.Equals(rowReleaseVersion, releaseVersion, StringComparison.Ordinal)
            || !string.Equals(installAccessClass, "open_public", StringComparison.Ordinal)
            || !string.Equals(compatibilityState, "compatible", StringComparison.Ordinal))
        {
            return null;
        }

        string resolvedArtifactId = artifactId ?? string.Empty;
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

        if (isGlobalFlagship
            && !IsCanonicalGlobalArtifact(
                item,
                platformToken,
                resolvedArtifactId,
                resolvedFileName,
                resolvedUrl,
                head,
                ResolveFileFormat(item, resolvedFileName),
                ReadString(item, "arch") ?? string.Empty,
                compatibilityProjection))
        {
            return null;
        }

        string securityState = ResolveSecurityState(
            item,
            platformToken,
            globalAuthority,
            resolvedArtifactId,
            resolvedFileName,
            sha256,
            sizeBytes.Value);
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
            $"/downloads/install/{Uri.EscapeDataString(resolvedArtifactId)}");
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

    private static bool IsCanonicalGlobalArtifact(
        JsonNode item,
        string platform,
        string artifactId,
        string fileName,
        string url,
        string head,
        string format,
        string architecture,
        bool compatibilityProjection)
    {
        (string ArtifactId, string Rid, string Architecture, string FileName, string Format) expected =
            platform switch
            {
                "windows" => (
                    "avalonia-win-x64-installer",
                    "win-x64",
                    "x64",
                    "chummer-avalonia-win-x64-installer.exe",
                    "exe"),
                "linux" => (
                    "avalonia-linux-x64-installer",
                    "linux-x64",
                    "x64",
                    "chummer-avalonia-linux-x64-installer.deb",
                    "deb"),
                "macos" => (
                    "avalonia-osx-arm64-installer",
                    "osx-arm64",
                    "arm64",
                    "chummer-avalonia-osx-arm64-installer.dmg",
                    "dmg"),
                _ => (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
            };
        string expectedUrl = $"https://chummer.run/downloads/files/{expected.FileName}";
        string rowPlatform = ReadString(item, "platform") ?? string.Empty;
        string downloadUrl = ReadString(item, "downloadUrl") ?? string.Empty;
        string compatibilityUrl = ReadString(item, "url") ?? string.Empty;
        string declaredArtifactId = ReadString(item, "artifactId") ?? string.Empty;
        string idAlias = ReadString(item, "id") ?? string.Empty;

        return !string.IsNullOrWhiteSpace(expected.ArtifactId)
               && string.Equals(artifactId, expected.ArtifactId, StringComparison.Ordinal)
               && string.Equals(
                   declaredArtifactId,
                   expected.ArtifactId,
                   StringComparison.Ordinal)
               && (compatibilityProjection
                   ? string.Equals(idAlias, artifactId, StringComparison.Ordinal)
                   : (string.IsNullOrWhiteSpace(idAlias)
                      || string.Equals(idAlias, artifactId, StringComparison.Ordinal)))
               && string.Equals(fileName, expected.FileName, StringComparison.Ordinal)
               && string.Equals(
                   ReadString(item, "rid"),
                   expected.Rid,
                   StringComparison.Ordinal)
               && string.Equals(architecture, expected.Architecture, StringComparison.Ordinal)
               && string.Equals(rowPlatform, platform, StringComparison.Ordinal)
               && string.Equals(head, "avalonia", StringComparison.Ordinal)
               && string.Equals(
                   ReadString(item, "head"),
                   "avalonia",
                   StringComparison.Ordinal)
               && string.Equals(ReadString(item, "kind"), "installer", StringComparison.Ordinal)
               && string.Equals(format, expected.Format, StringComparison.Ordinal)
               && string.Equals(url, expectedUrl, StringComparison.Ordinal)
               && string.Equals(downloadUrl, expectedUrl, StringComparison.Ordinal)
               && (compatibilityProjection
                   ? string.Equals(
                       compatibilityUrl,
                       expectedUrl,
                       StringComparison.Ordinal)
                   : (string.IsNullOrWhiteSpace(compatibilityUrl)
                      || string.Equals(
                          compatibilityUrl,
                          expectedUrl,
                          StringComparison.Ordinal)))
               && (!compatibilityProjection
                   || (string.Equals(
                           ReadString(item, "platformId"),
                           $"{platform}-{expected.Architecture}",
                           StringComparison.Ordinal)
                       && string.Equals(
                           ReadString(item, "flavor"),
                           "installer",
                           StringComparison.Ordinal)
                       && string.Equals(
                           ReadString(item, "format"),
                           expected.Format,
                           StringComparison.Ordinal)))
               && (string.Equals(platform, "macos", StringComparison.Ordinal)
                   || item["macosFlagshipEvidence"] is null);
    }

    private static string ResolveSecurityState(
        JsonNode item,
        string platform,
        GlobalFlagshipAuthority? globalAuthority,
        string artifactId,
        string fileName,
        string sha256,
        long sizeBytes)
    {
        if (string.Equals(platform, "macos", StringComparison.Ordinal)
            && globalAuthority is not null
            && HasBoundMacosEvidence(
                item["macosFlagshipEvidence"],
                globalAuthority,
                artifactId,
                fileName,
                sha256,
                sizeBytes))
        {
            return "signed_notarized";
        }

        if (string.Equals(platform, "linux", StringComparison.Ordinal)
            && globalAuthority is not null)
        {
            return "package_verified";
        }

        return "digest_published";
    }

    private static bool HasBoundMacosEvidence(
        JsonNode? evidence,
        GlobalFlagshipAuthority globalAuthority,
        string artifactId,
        string fileName,
        string sha256,
        long sizeBytes)
    {
        if (!HasExactKeys(
                evidence,
                "contractName",
                "contractVersion",
                "source",
                "candidate",
                "globalCandidateIdentity",
                "github",
                "signingIdentity",
                "notarization",
                "receiptBindings")
            || !string.Equals(
                ReadString(evidence, "contractName"),
                MacosEvidenceBindingContract,
                StringComparison.Ordinal)
            || ReadInteger(evidence, "contractVersion") != 1)
        {
            return false;
        }

        JsonNode? source = evidence?["source"];
        if (!HasExactKeys(
                source,
                "contractName",
                "contractVersion",
                "sha256",
                "sizeBytes")
            || !string.Equals(
                ReadString(source, "contractName"),
                MacosEvidenceSourceContract,
                StringComparison.Ordinal)
            || ReadInteger(source, "contractVersion") != 3
            || !IsLowerSha256(ReadString(source, "sha256"))
            || ReadPositiveInteger(source, "sizeBytes") is null)
        {
            return false;
        }

        JsonNode? candidate = evidence?["candidate"];
        if (!HasExactKeys(candidate, "artifactId", "fileName", "sha256", "sizeBytes")
            || !string.Equals(
                ReadString(candidate, "artifactId"),
                artifactId,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(candidate, "fileName"),
                fileName,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(candidate, "sha256"),
                sha256,
                StringComparison.Ordinal)
            || ReadPositiveInteger(candidate, "sizeBytes") != sizeBytes)
        {
            return false;
        }

        JsonNode? globalIdentity = evidence?["globalCandidateIdentity"];
        if (!HasExactKeys(
                globalIdentity,
                "candidateId",
                "generationId",
                "previousReleaseVersion",
                "releaseVersion",
                "sourceCommit")
            || !IsAuthorityToken(ReadString(globalIdentity, "candidateId"))
            || !IsAuthorityToken(ReadString(globalIdentity, "generationId"))
            || string.IsNullOrWhiteSpace(
                ReadString(globalIdentity, "previousReleaseVersion"))
            || !string.Equals(
                ReadString(globalIdentity, "candidateId"),
                globalAuthority.CandidateId,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(globalIdentity, "releaseVersion"),
                globalAuthority.ReleaseVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(globalIdentity, "sourceCommit"),
                globalAuthority.SourceCommit,
                StringComparison.Ordinal))
        {
            return false;
        }

        JsonNode? github = evidence?["github"];
        string actor = ReadString(github, "actor") ?? string.Empty;
        if (!HasExactKeys(
                github,
                "actor",
                "ref",
                "repository",
                "rerunPolicy",
                "runAttempt",
                "runId",
                "sha",
                "triggeringActor",
                "workflow")
            || !IsGithubLogin(actor)
            || !string.Equals(
                ReadString(github, "triggeringActor"),
                actor,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(github, "repository"),
                "ArchonMegalon/chummer6-ui",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(github, "ref"),
                "refs/heads/main",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(github, "workflow"),
                ".github/workflows/macos-flagship-evidence.yml",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(github, "rerunPolicy"),
                "same-actor-only",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadString(github, "sha"),
                globalAuthority.SourceCommit,
                StringComparison.Ordinal)
            || ReadPositiveInteger(github, "runId") is null
            || ReadPositiveInteger(github, "runAttempt") is null)
        {
            return false;
        }

        JsonNode? signingIdentity = evidence?["signingIdentity"];
        JsonNode? notarization = evidence?["notarization"];
        if (!HasExactKeys(
                signingIdentity,
                "certificateSha256",
                "certificateSpkiSha256",
                "developerIdApplicationIdentity",
                "teamId")
            || !HasExactKeys(notarization, "status", "submissionId"))
        {
            return false;
        }

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
        if (!IsLowerSha256(certificateSha256)
            || !IsLowerSha256(certificateSpkiSha256)
            || !Regex.IsMatch(
                teamId,
                @"^[A-Z0-9]{10}$",
                RegexOptions.CultureInvariant)
            || !developerIdIdentity.StartsWith(
                "Developer ID Application:",
                StringComparison.Ordinal)
            || !developerIdIdentity.EndsWith($"({teamId})", StringComparison.Ordinal)
            || !string.Equals(notarizationStatus, "Accepted", StringComparison.Ordinal)
            || !IsLowerUuid(submissionId))
        {
            return false;
        }

        JsonNode? receiptBindings = evidence?["receiptBindings"];
        if (!HasExactKeys(
                receiptBindings,
                "notaryResult",
                "signingIdentityReceipt",
                "signingReceipt"))
        {
            return false;
        }

        string[] receiptKeys =
        [
            "notaryResult",
            "signingIdentityReceipt",
            "signingReceipt"
        ];
        List<string> receiptPaths = [];
        foreach (string receiptKey in receiptKeys)
        {
            JsonNode? receipt = receiptBindings?[receiptKey];
            string receiptPath = ReadString(receipt, "path") ?? string.Empty;
            if (!HasExactKeys(receipt, "path", "sha256", "sizeBytes")
                || !Regex.IsMatch(
                    receiptPath,
                    @"^receipts/[A-Za-z0-9][A-Za-z0-9._+-]{0,255}$",
                    RegexOptions.CultureInvariant)
                || !IsLowerSha256(ReadString(receipt, "sha256"))
                || ReadPositiveInteger(receipt, "sizeBytes") is null)
            {
                return false;
            }

            receiptPaths.Add(receiptPath);
        }

        return receiptPaths.Distinct(StringComparer.Ordinal).Count() == receiptPaths.Count;
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

    private static bool IsLowerSha256(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(
               value,
               @"^[0-9a-f]{64}$",
               RegexOptions.CultureInvariant);

    private static bool IsLowerCommit(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(
               value,
               @"^[0-9a-f]{40}$",
               RegexOptions.CultureInvariant);

    private static bool IsLowerUuid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(
               value,
               @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
               RegexOptions.CultureInvariant);

    private static bool IsAuthorityToken(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(
               value,
               @"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$",
               RegexOptions.CultureInvariant);

    private static bool IsGithubLogin(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(
               value,
               @"^(?:github-actions\[bot\]|[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$",
               RegexOptions.CultureInvariant);

    private static bool HasExactKeys(JsonNode? node, params string[] expectedKeys)
        => node is JsonObject jsonObject
           && jsonObject.Count == expectedKeys.Length
           && expectedKeys.All(jsonObject.ContainsKey);

    private static bool JsonArrayMatches(JsonNode? node, params string[] expectedValues)
    {
        if (node is not JsonArray jsonArray || jsonArray.Count != expectedValues.Length)
        {
            return false;
        }

        for (int index = 0; index < expectedValues.Length; index++)
        {
            if (!string.Equals(
                    jsonArray[index]?.GetValue<string>(),
                    expectedValues[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static long? ReadInteger(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is not JsonValue value
            || !value.TryGetValue(out long result))
        {
            return null;
        }

        return result;
    }

    private static bool? ReadBoolean(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is not JsonValue value
            || !value.TryGetValue(out bool result))
        {
            return null;
        }

        return result;
    }

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

    private static bool TryReadGeneratedTimestamp(
        JsonNode? node,
        out string generatedAt)
    {
        generatedAt = string.Empty;
        if (node is not JsonObject jsonObject)
        {
            return false;
        }

        bool hasGeneratedAt = jsonObject.ContainsKey("generatedAt");
        bool hasGeneratedAtLegacy = jsonObject.ContainsKey("generated_at");
        if (!hasGeneratedAt && !hasGeneratedAtLegacy)
        {
            return false;
        }

        string? primary = null;
        string? legacy = null;
        if (hasGeneratedAt
            && (jsonObject["generatedAt"] is not JsonValue primaryValue
                || !primaryValue.TryGetValue(out primary)))
        {
            return false;
        }

        if (hasGeneratedAtLegacy
            && (jsonObject["generated_at"] is not JsonValue legacyValue
                || !legacyValue.TryGetValue(out legacy)))
        {
            return false;
        }

        if (hasGeneratedAt
            && hasGeneratedAtLegacy
            && !string.Equals(primary, legacy, StringComparison.Ordinal))
        {
            return false;
        }

        generatedAt = (hasGeneratedAt ? primary : legacy)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(generatedAt)
            || !TryParseRegistryIsoTimestamp(generatedAt))
        {
            generatedAt = string.Empty;
            return false;
        }

        return true;
    }

    private static bool TryParseRegistryIsoTimestamp(string value)
    {
        Match dateMatch = Regex.Match(
            value,
            @"^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})(?<rest>.*)$",
            RegexOptions.CultureInvariant);
        bool isWeekDate = false;
        if (!dateMatch.Success)
        {
            dateMatch = Regex.Match(
                value,
                @"^(?<year>\d{4})-?W(?<week>\d{2})(?:-?(?<weekday>[1-7]))?(?<rest>.*)$",
                RegexOptions.CultureInvariant);
            isWeekDate = dateMatch.Success;
        }

        if (!dateMatch.Success)
        {
            dateMatch = Regex.Match(
                value,
                @"^(?<year>\d{4})(?<month>\d{2})(?<day>\d{2})(?<rest>.*)$",
                RegexOptions.CultureInvariant);
        }

        if (!dateMatch.Success
            || !int.TryParse(
                dateMatch.Groups["year"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int year))
        {
            return false;
        }

        if (isWeekDate)
        {
            int weekday = 1;
            if (!int.TryParse(
                    dateMatch.Groups["week"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int week)
                || (dateMatch.Groups["weekday"].Success
                    && !int.TryParse(
                        dateMatch.Groups["weekday"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out weekday)))
            {
                return false;
            }

            try
            {
                DayOfWeek dayOfWeek = weekday == 7
                    ? DayOfWeek.Sunday
                    : (DayOfWeek)weekday;
                DateTime weekDate = ISOWeek.ToDateTime(year, week, dayOfWeek);
                if (ISOWeek.GetYear(weekDate) != year
                    || ISOWeek.GetWeekOfYear(weekDate) != week)
                {
                    return false;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
        else
        {
            string canonicalDate = string.Create(
                CultureInfo.InvariantCulture,
                $"{year:D4}-{dateMatch.Groups["month"].Value}-{dateMatch.Groups["day"].Value}");
            if (!DateOnly.TryParseExact(
                    canonicalDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                return false;
            }
        }

        string remainder = dateMatch.Groups["rest"].Value;
        if (remainder.Length == 0)
        {
            return true;
        }

        // datetime.fromisoformat accepts one arbitrary separator between the
        // date and time portions. The Registry treats a missing offset as UTC.
        if (!Rune.TryGetRuneAt(remainder, 0, out Rune separator)
            || remainder.Length <= separator.Utf16SequenceLength)
        {
            return false;
        }

        string timeAndOffset = remainder[separator.Utf16SequenceLength..];
        string time = timeAndOffset;
        string offset = string.Empty;
        if (timeAndOffset.EndsWith("Z", StringComparison.Ordinal))
        {
            time = timeAndOffset[..^1];
            offset = "Z";
        }
        else
        {
            int offsetIndex = timeAndOffset.IndexOfAny(['+', '-']);
            if (offsetIndex >= 0)
            {
                time = timeAndOffset[..offsetIndex];
                offset = timeAndOffset[offsetIndex..];
            }
        }

        return TryParseRegistryIsoTime(time)
               && (string.IsNullOrEmpty(offset)
                   || string.Equals(offset, "Z", StringComparison.Ordinal)
                   || TryParseRegistryIsoOffset(offset));
    }

    private static bool TryParseRegistryIsoTime(string value)
    {
        Match match = Regex.Match(
            value,
            value.Contains(':', StringComparison.Ordinal)
                ? @"^(?<hour>\d{2})(?:[.,]\d+|:(?<minute>\d{2})(?:[.,]\d+|:(?<second>\d{2})(?:[.,]\d+)?)?)?$"
                : @"^(?<hour>\d{2})(?:[.,]\d+|(?<minute>\d{2})(?:[.,]\d+|(?<second>\d{2})(?:[.,]\d+)?)?)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !TryReadTimestampPart(match, "hour", out int hour)
            || hour > 23)
        {
            return false;
        }

        return (!match.Groups["minute"].Success
                || (TryReadTimestampPart(match, "minute", out int minute)
                    && minute <= 59))
               && (!match.Groups["second"].Success
                   || (TryReadTimestampPart(match, "second", out int second)
                       && second <= 59));
    }

    private static bool TryParseRegistryIsoOffset(string value)
    {
        Match match = Regex.Match(
            value,
            @"^[+-](?<hour>\d{2})(?:(?::?(?<minute>\d{2}))(?:(?::?(?<second>\d{2})(?:[.,]\d+)?)?)?)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !TryReadTimestampPart(match, "hour", out int hour)
            || hour > 23)
        {
            return false;
        }

        return (!match.Groups["minute"].Success
                || (TryReadTimestampPart(match, "minute", out int minute)
                    && minute <= 59))
               && (!match.Groups["second"].Success
                   || (TryReadTimestampPart(match, "second", out int second)
                       && second <= 59));
    }

    private static bool TryReadTimestampPart(
        Match match,
        string groupName,
        out int value)
        => int.TryParse(
            match.Groups[groupName].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out value);

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

internal sealed record ManifestIdentity(
    string Status,
    string Version,
    string Channel,
    string RolloutState,
    string GeneratedAt,
    string PublishedAt,
    string SupportabilityState,
    string SupportabilitySummary,
    string ReleaseProfile,
    long? SchemaVersion,
    long? ContractVersion,
    string ContractName,
    string RegistryCommit);

internal sealed record GlobalFlagshipAuthority(
    string CandidateId,
    string SourceCommit,
    string ReleaseVersion);

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
