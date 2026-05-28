using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using System.Text;
using Chummer.Contracts.Characters;

namespace Chummer.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/import", async (WorkspaceImportRequest request, IChummerClient client, CancellationToken ct) =>
        {
            string content = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.ContentBase64))
            {
                content = Encoding.UTF8.GetString(Convert.FromBase64String(request.ContentBase64));
            }
            else if (!string.IsNullOrWhiteSpace(request.Xml))
            {
                content = request.Xml;
            }

            WorkspaceDocumentFormat format = ParseFormat(request.Format);
            WorkspaceImportResult imported = await client.ImportAsync(
                new WorkspaceImportDocument(content, request.RulesetId ?? string.Empty, format),
                ct).ConfigureAwait(false);

            return Results.Ok(new WorkspaceImportResponse(
                Id: imported.Id.Value,
                Summary: imported.Summary,
                RulesetId: imported.RulesetId,
                ImportReceiptId: imported.ImportReceiptId,
                ImportedAtUtc: imported.ImportedAtUtc,
                Portability: imported.Portability));
        });

        app.MapGet("/api/workspaces", async (int? maxCount, IChummerClient client, CancellationToken ct) =>
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(ct).ConfigureAwait(false);
            if (maxCount is > 0)
            {
                workspaces = workspaces.Take(maxCount.Value).ToArray();
            }

            return Results.Ok(new WorkspaceListResponse(
                workspaces.Count,
                workspaces.Select(static workspace => new WorkspaceListItemResponse(
                    workspace.Id.Value,
                    workspace.Summary,
                    workspace.LastUpdatedUtc,
                    workspace.RulesetId,
                    workspace.HasSavedWorkspace)).ToArray()));
        });

        app.MapDelete("/api/workspaces/{id}", async (string id, IChummerClient client, CancellationToken ct) =>
        {
            bool closed = await client.CloseWorkspaceAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false);
            return closed ? Results.Ok() : Results.NotFound();
        });

        app.MapGet("/api/workspaces/{id}/sections/{sectionId}", async (string id, string sectionId, IChummerClient client, CancellationToken ct) =>
            Results.Json(await client.GetSectionAsync(new CharacterWorkspaceId(id), sectionId, ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/summary", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetSummaryAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/validate", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.ValidateAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/profile", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetProfileAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/progress", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetProgressAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/skills", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetSkillsAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/rules", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetRulesAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/build", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetBuildAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/movement", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetMovementAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));
        app.MapGet("/api/workspaces/{id}/awakening", async (string id, IChummerClient client, CancellationToken ct) =>
            Results.Ok(await client.GetAwakeningAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false)));

        app.MapPatch("/api/workspaces/{id}/metadata", async (string id, UpdateWorkspaceMetadata command, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<CharacterProfileSection> result = await client.UpdateMetadataAsync(new CharacterWorkspaceId(id), command, ct).ConfigureAwait(false);
            return result.Success && result.Value is not null
                ? Results.Ok(new WorkspaceMetadataResponse(result.Value))
                : Results.BadRequest(new { error = result.Error ?? "metadata_update_failed" });
        });

        app.MapPost("/api/workspaces/{id}/save", async (string id, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<WorkspaceSaveReceipt> result = await client.SaveAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false);
            return result.Success && result.Value is not null
                ? Results.Ok(new WorkspaceSaveResponse(result.Value.Id.Value, result.Value.DocumentLength, result.Value.RulesetId))
                : Results.BadRequest(new { error = result.Error ?? "workspace_save_failed" });
        });

        app.MapPost("/api/workspaces/{id}/download", async (string id, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<WorkspaceDownloadReceipt> result = await client.DownloadAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false);
            return result.Success && result.Value is not null
                ? Results.Ok(new WorkspaceDownloadResponse(
                    result.Value.Id.Value,
                    result.Value.Format.ToString(),
                    result.Value.ContentBase64,
                    result.Value.FileName,
                    result.Value.DocumentLength,
                    result.Value.RulesetId))
                : Results.BadRequest(new { error = result.Error ?? "workspace_download_failed" });
        });

        app.MapGet("/api/workspaces/{id}/export", async (string id, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<WorkspaceExportReceipt> result = await client.ExportAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false);
            return result.Success && result.Value is not null
                ? Results.Ok(new WorkspaceExportResponse(
                    result.Value.Id.Value,
                    result.Value.Format.ToString(),
                    result.Value.ContentBase64,
                    result.Value.FileName,
                    result.Value.DocumentLength,
                    result.Value.RulesetId,
                    result.Value.PackageId,
                    result.Value.ExportedAtUtc,
                    result.Value.Portability))
                : Results.BadRequest(new { error = result.Error ?? "workspace_export_failed" });
        });

        app.MapGet("/api/workspaces/{id}/print", async (string id, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<WorkspacePrintReceipt> result = await client.PrintAsync(new CharacterWorkspaceId(id), ct).ConfigureAwait(false);
            return result.Success && result.Value is not null
                ? Results.Ok(new WorkspacePrintResponse(
                    result.Value.Id.Value,
                    result.Value.ContentBase64,
                    result.Value.FileName,
                    result.Value.MimeType,
                    result.Value.DocumentLength,
                    result.Value.Title,
                    result.Value.RulesetId))
                : Results.BadRequest(new { error = result.Error ?? "workspace_print_failed" });
        });

        return app;
    }

    private static WorkspaceDocumentFormat ParseFormat(string? rawFormat)
        => Enum.TryParse(rawFormat, ignoreCase: true, out WorkspaceDocumentFormat parsedFormat)
            ? parsedFormat
            : WorkspaceDocumentFormat.NativeXml;
}
