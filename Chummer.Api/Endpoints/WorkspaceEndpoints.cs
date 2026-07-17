using Chummer.Contracts.Workspaces;
using Chummer.Presentation;
using System.Text;
using Chummer.Contracts.Characters;

namespace Chummer.Api.Endpoints;

public static class WorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/workspaces/import", async (HttpContext http, WorkspaceImportRequest request, IChummerClient client, CancellationToken ct) =>
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
            try
            {
                WorkspaceImportResult imported = await client.ImportAsync(
                    new WorkspaceImportDocument(content, request.RulesetId ?? string.Empty, format),
                    ct).ConfigureAwait(false);

                SetEtag(http, imported.ContentRevision);

                return Results.Ok(new WorkspaceImportResponse(
                    Id: imported.Id.Value,
                    Summary: imported.Summary,
                    RulesetId: imported.RulesetId,
                    ImportReceiptId: imported.ImportReceiptId,
                    ImportedAtUtc: imported.ImportedAtUtc,
                    Portability: imported.Portability,
                    WorkflowDeterministicReceipt: imported.WorkflowDeterministicReceipt,
                    ContentRevision: imported.ContentRevision,
                    SavedRevision: imported.SavedRevision));
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException)
            {
                return Results.BadRequest(new { error = "invalid_workspace_import" });
            }
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
                    workspace.HasSavedWorkspace,
                    workspace.ContentRevision,
                    workspace.SavedRevision)).ToArray()));
        });

        app.MapGet("/api/workspaces/{id}", async (HttpContext http, string id, IChummerClient client, CancellationToken ct) =>
        {
            CommandResult<WorkspaceDocumentSnapshot> result = await client
                .GetWorkspaceAsync(new CharacterWorkspaceId(id), ct)
                .ConfigureAwait(false);
            if (!result.Success || result.Value is null)
            {
                return MapWorkspaceFailure(result);
            }

            WorkspaceDocumentSnapshot snapshot = result.Value;
            SetEtag(http, snapshot.ContentRevision);
            return Results.Ok(new WorkspaceDocumentResponse(
                Id: snapshot.Id.Value,
                Format: snapshot.Document.Format.ToString(),
                ContentBase64: Convert.ToBase64String(Encoding.UTF8.GetBytes(snapshot.Document.Content)),
                RulesetId: snapshot.Document.RulesetId,
                SchemaVersion: snapshot.Document.SchemaVersion,
                PayloadKind: snapshot.Document.PayloadKind,
                LastUpdatedUtc: snapshot.LastUpdatedUtc,
                ContentRevision: snapshot.ContentRevision,
                SavedRevision: snapshot.SavedRevision));
        });

        app.MapPut("/api/workspaces/{id}", async (HttpContext http, string id, WorkspaceDocumentReplaceRequest request, IChummerClient client, CancellationToken ct) =>
        {
            if (!TryReadExpectedRevision(http.Request, out long expectedContentRevision, out IResult? preconditionFailure))
            {
                return preconditionFailure!;
            }

            if (!TryCreateWorkspaceDocument(request, out WorkspaceDocument? document))
            {
                return Results.BadRequest(new { error = "invalid_workspace_document" });
            }

            CommandResult<WorkspaceRevisionReceipt> result = await client.ReplaceWorkspaceDocumentAsync(
                new CharacterWorkspaceId(id),
                expectedContentRevision,
                document!,
                ct).ConfigureAwait(false);
            return RevisionResult(http, result);
        });

        app.MapDelete("/api/workspaces/{id}", async (HttpContext http, string id, IChummerClient client, CancellationToken ct) =>
        {
            if (!TryReadExpectedRevision(http.Request, out long expectedContentRevision, out IResult? preconditionFailure))
            {
                return preconditionFailure!;
            }

            CommandResult<WorkspaceRevisionReceipt> result = await client.CloseWorkspaceAsync(
                new CharacterWorkspaceId(id),
                expectedContentRevision,
                ct).ConfigureAwait(false);
            return RevisionResult(http, result);
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

        app.MapPatch("/api/workspaces/{id}/metadata", async (HttpContext http, string id, UpdateWorkspaceMetadata command, IChummerClient client, CancellationToken ct) =>
        {
            if (!TryReadExpectedRevision(http.Request, out long expectedContentRevision, out IResult? preconditionFailure))
            {
                return preconditionFailure!;
            }

            CommandResult<WorkspaceMetadataResult> result = await client.UpdateMetadataAsync(
                new CharacterWorkspaceId(id),
                expectedContentRevision,
                command,
                ct).ConfigureAwait(false);
            if (!result.Success || result.Value is null)
            {
                return MapWorkspaceFailure(result);
            }

            SetEtag(http, result.Value.ContentRevision);
            return Results.Ok(new WorkspaceMetadataResponse(
                result.Value.Profile,
                result.Value.ContentRevision,
                result.Value.SavedRevision));
        });

        app.MapPost("/api/workspaces/{id}/save", async (HttpContext http, string id, IChummerClient client, CancellationToken ct) =>
        {
            if (!TryReadExpectedRevision(http.Request, out long expectedContentRevision, out IResult? preconditionFailure))
            {
                return preconditionFailure!;
            }

            CommandResult<WorkspaceSaveReceipt> result = await client.SaveAsync(
                new CharacterWorkspaceId(id),
                expectedContentRevision,
                ct).ConfigureAwait(false);
            if (!result.Success || result.Value is null)
            {
                return MapWorkspaceFailure(result);
            }

            SetEtag(http, result.Value.ContentRevision);
            return Results.Ok(new WorkspaceSaveResponse(
                result.Value.Id.Value,
                result.Value.DocumentLength,
                result.Value.RulesetId,
                result.Value.ReceiptId,
                result.Value.WorkflowDeterministicReceipt,
                result.Value.ContentRevision,
                result.Value.SavedRevision));
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
                : MapWorkspaceFailure(result);
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
                : MapWorkspaceFailure(result);
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
                : MapWorkspaceFailure(result);
        });

        return app;
    }

    private static WorkspaceDocumentFormat ParseFormat(string? rawFormat)
        => Enum.TryParse(rawFormat, ignoreCase: true, out WorkspaceDocumentFormat parsedFormat)
           && Enum.IsDefined(parsedFormat)
            ? parsedFormat
            : WorkspaceDocumentFormat.NativeXml;

    private static bool TryReadExpectedRevision(
        HttpRequest request,
        out long expectedContentRevision,
        out IResult? failure)
    {
        string ifMatch = request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            expectedContentRevision = 0;
            failure = Results.Json(
                new { error = "if_match_required" },
                statusCode: StatusCodes.Status428PreconditionRequired);
            return false;
        }

        if (!WorkspaceRevisionEtag.TryParseStrong(ifMatch, out expectedContentRevision))
        {
            failure = Results.BadRequest(new { error = "invalid_if_match" });
            return false;
        }

        failure = null;
        return true;
    }

    private static bool TryCreateWorkspaceDocument(
        WorkspaceDocumentReplaceRequest request,
        out WorkspaceDocument? document)
    {
        document = null;
        if (request.ContentBase64 is null
            || string.IsNullOrWhiteSpace(request.RulesetId)
            || string.IsNullOrWhiteSpace(request.PayloadKind)
            || request.SchemaVersion is not > 0
            || !Enum.TryParse(request.Format, ignoreCase: true, out WorkspaceDocumentFormat format)
            || !Enum.IsDefined(format))
        {
            return false;
        }

        try
        {
            string content = Encoding.UTF8.GetString(Convert.FromBase64String(request.ContentBase64));
            document = new WorkspaceDocument(
                new WorkspaceDocumentState(
                    request.RulesetId,
                    request.SchemaVersion.Value,
                    request.PayloadKind,
                    content),
                format);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IResult RevisionResult(
        HttpContext http,
        CommandResult<WorkspaceRevisionReceipt> result)
    {
        if (!result.Success || result.Value is null)
        {
            return MapWorkspaceFailure(result);
        }

        SetEtag(http, result.Value.ContentRevision);
        return Results.Ok(new WorkspaceRevisionResponse(
            result.Value.Id.Value,
            result.Value.ContentRevision,
            result.Value.SavedRevision));
    }

    private static IResult MapWorkspaceFailure<T>(CommandResult<T> result)
        where T : class
    {
        return result.Outcome switch
        {
            WorkspaceOperationOutcome.Missing => Results.Json(
                new { error = "workspace_not_found" },
                statusCode: StatusCodes.Status404NotFound),
            WorkspaceOperationOutcome.Conflict => Results.Json(
                new { error = "workspace_revision_conflict" },
                statusCode: StatusCodes.Status409Conflict),
            WorkspaceOperationOutcome.Corrupt => Results.Json(
                new { error = "workspace_corrupt" },
                statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Json(
                new { error = "workspace_unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static void SetEtag(HttpContext http, long contentRevision)
    {
        http.Response.Headers.ETag = WorkspaceRevisionEtag.Format(contentRevision);
    }
}
