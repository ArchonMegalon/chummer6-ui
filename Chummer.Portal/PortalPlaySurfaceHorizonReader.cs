using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class PortalPlaySurfaceHorizonReader
{
    public const string PublicRelativePath = "release-evidence/browser-lane/BLAZOR_PLAY_SURFACE_HORIZON.generated.json";

    public static PlaySurfaceHorizonSummary Read(string downloadsDirectory)
    {
        string receiptPath = ResolveReceiptPath(downloadsDirectory);
        if (!File.Exists(receiptPath))
        {
            return new PlaySurfaceHorizonSummary(
                Status: "missing",
                ContractName: "unpublished",
                ReceiptRelativePath: PublicRelativePath,
                GeneratedAt: string.Empty,
                CurrentExecutionScope: "unknown",
                PublicEntryRoute: string.Empty,
                PublicRosterEntryRoute: string.Empty,
                PublicBlazorRootRoute: string.Empty,
                HostedAppRoute: string.Empty,
                CompatibilityRouteBase: string.Empty,
                ExecutionRouteBase: string.Empty,
                Horizons: [],
                Summary: "The public play-surface horizon receipt has not been synced into the downloads shelf.");
        }

        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(receiptPath, Encoding.UTF8));
            List<PlaySurfaceHorizonItem> horizons = [];
            foreach (JsonNode? node in root?["horizons"]?.AsArray() ?? [])
            {
                if (node is null)
                {
                    continue;
                }

                List<PlaySurfaceEvidenceReference> runtimeProvenReceipts = ReadReferences(node["runtime_proven_receipts"]);
                List<PlaySurfaceEvidenceReference> sourceStagedReceipts = ReadReferences(node["source_staged_receipts"]);
                List<PlaySurfaceEvidenceReference> documentationSources = ReadReferences(node["documentation_sources"]);
                List<string> unprovenClaims = ReadStringList(node["unproven_claims"]);
                List<string> serverBoundBoundaries = ReadStringList(node["server_bound_boundaries"]);

                horizons.Add(new PlaySurfaceHorizonItem(
                    Id: ReadString(node, "id") ?? "unknown",
                    Title: ReadString(node, "title") ?? "Unnamed horizon",
                    Status: ReadString(node, "status") ?? "unknown",
                    EvidenceTier: ReadString(node, "evidence_tier") ?? "unknown",
                    Headline: ReadString(node, "headline") ?? string.Empty,
                    Summary: ReadString(node, "summary") ?? string.Empty,
                    RuntimeProvenReceiptCount: runtimeProvenReceipts.Count,
                    SourceStagedReceiptCount: sourceStagedReceipts.Count,
                    DocumentationSourceCount: documentationSources.Count,
                    RuntimeProvenReceipts: runtimeProvenReceipts,
                    SourceStagedReceipts: sourceStagedReceipts,
                    DocumentationSources: documentationSources,
                    UnprovenClaims: unprovenClaims,
                    ServerBoundBoundaries: serverBoundBoundaries));
            }

            JsonNode? currentReleaseTruth = root?["current_release_truth"];
            return new PlaySurfaceHorizonSummary(
                Status: ReadString(root, "status") ?? "unknown",
                ContractName: ReadString(root, "contract_name") ?? "missing",
                ReceiptRelativePath: PublicRelativePath,
                GeneratedAt: ReadString(root, "generated_at", "generatedAt") ?? string.Empty,
                CurrentExecutionScope: ReadString(currentReleaseTruth, "current_execution_scope", "smoke_execution_scope") ?? "unknown",
                PublicEntryRoute: ReadString(currentReleaseTruth, "public_entry_route") ?? string.Empty,
                PublicRosterEntryRoute: ReadString(currentReleaseTruth, "public_roster_entry_route") ?? string.Empty,
                PublicBlazorRootRoute: ReadString(currentReleaseTruth, "public_blazor_root_route") ?? string.Empty,
                HostedAppRoute: ReadString(currentReleaseTruth, "hosted_app_route") ?? string.Empty,
                CompatibilityRouteBase: ReadString(currentReleaseTruth, "compatibility_route_base", "promoted_route_base") ?? string.Empty,
                ExecutionRouteBase: ReadString(currentReleaseTruth, "execution_route_base", "promoted_route_base") ?? string.Empty,
                Horizons: horizons,
                Summary: ReadString(root, "scope") ?? "play-surface horizon receipt");
        }
        catch (JsonException)
        {
            return new PlaySurfaceHorizonSummary(
                Status: "error",
                ContractName: "invalid",
                ReceiptRelativePath: PublicRelativePath,
                GeneratedAt: string.Empty,
                CurrentExecutionScope: "unknown",
                PublicEntryRoute: string.Empty,
                PublicRosterEntryRoute: string.Empty,
                PublicBlazorRootRoute: string.Empty,
                HostedAppRoute: string.Empty,
                CompatibilityRouteBase: string.Empty,
                ExecutionRouteBase: string.Empty,
                Horizons: [],
                Summary: "The public play-surface horizon receipt could not be parsed.");
        }
    }

    private static string ResolveReceiptPath(string downloadsDirectory)
        => Path.Combine(downloadsDirectory, PublicRelativePath.Replace('/', Path.DirectorySeparatorChar));

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

    private static List<PlaySurfaceEvidenceReference> ReadReferences(JsonNode? node)
    {
        List<PlaySurfaceEvidenceReference> references = [];
        foreach (JsonNode? item in node?.AsArray() ?? [])
        {
            if (item is null)
            {
                continue;
            }

            references.Add(new PlaySurfaceEvidenceReference(
                Id: ReadString(item, "id") ?? "unknown",
                Label: ReadString(item, "label") ?? "Unnamed reference",
                Status: ReadString(item, "status") ?? "unknown",
                PublicRelativePath: ReadString(item, "public_download_relative_path") ?? string.Empty,
                LocalPath: ReadString(item, "path") ?? string.Empty));
        }

        return references;
    }

    private static List<string> ReadStringList(JsonNode? node)
    {
        List<string> values = [];
        foreach (JsonNode? item in node?.AsArray() ?? [])
        {
            string? value = item?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        return values;
    }
}

internal sealed record PlaySurfaceHorizonSummary(
    string Status,
    string ContractName,
    string ReceiptRelativePath,
    string GeneratedAt,
    string CurrentExecutionScope,
    string PublicEntryRoute,
    string PublicRosterEntryRoute,
    string PublicBlazorRootRoute,
    string HostedAppRoute,
    string CompatibilityRouteBase,
    string ExecutionRouteBase,
    IReadOnlyList<PlaySurfaceHorizonItem> Horizons,
    string Summary);

internal sealed record PlaySurfaceHorizonItem(
    string Id,
    string Title,
    string Status,
    string EvidenceTier,
    string Headline,
    string Summary,
    int RuntimeProvenReceiptCount,
    int SourceStagedReceiptCount,
    int DocumentationSourceCount,
    IReadOnlyList<PlaySurfaceEvidenceReference> RuntimeProvenReceipts,
    IReadOnlyList<PlaySurfaceEvidenceReference> SourceStagedReceipts,
    IReadOnlyList<PlaySurfaceEvidenceReference> DocumentationSources,
    IReadOnlyList<string> UnprovenClaims,
    IReadOnlyList<string> ServerBoundBoundaries);

internal sealed record PlaySurfaceEvidenceReference(
    string Id,
    string Label,
    string Status,
    string PublicRelativePath,
    string LocalPath);
