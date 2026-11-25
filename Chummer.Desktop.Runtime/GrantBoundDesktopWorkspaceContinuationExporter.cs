using System.Text.Json;
using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Desktop.Runtime;

/// <summary>
/// Bridges the Core-plane <see cref="WorkspaceContinuationExportService"/> into the
/// roaming sync's exporter seam. The seam contract requires the export to run
/// OUTSIDE the sync's own owner lease (the exporter coordinates its own lease via
/// <c>OwnerContextAdmission.TryAcquire</c>); at the sync's call site no lease is
/// held, so the export runs directly. An unavailable or failed export degrades to
/// a payload-only push rather than failing the whole sync.
/// </summary>
public sealed class GrantBoundDesktopWorkspaceContinuationExporter
{
    private readonly WorkspaceContinuationExportService _exportService;

    public GrantBoundDesktopWorkspaceContinuationExporter(
        WorkspaceContinuationExportService exportService)
    {
        _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
    }

    /// <summary>
    /// Seam delegate: exports the complete continuation for the original owner
    /// authority, or returns null when the export is unavailable or failed so the
    /// caller degrades to a payload-only push. Never throws (except cancellation).
    /// </summary>
    public async Task<WorkspaceContinuationExport?> ExportAsync(
        OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct)
    {
        await Task.Yield();

        CommandResult<WorkspaceContinuationExport> result;
        try
        {
            result = _exportService.Export(originalOwner, workspaceId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Export failure degrades to a payload-only push; the roaming sync
            // treats a null export as "no rich fields this round".
            return null;
        }

        if (!result.Success || result.Value is null)
            return null;

        return result.Value;
    }
}
