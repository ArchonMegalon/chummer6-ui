using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public async Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No workspace loaded."
            });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceMetadataUpdateResult result = await _workspacePersistenceService.UpdateMetadataAsync(
                _client,
                currentWorkspace.Value,
                command,
                State.Preferences,
                ct);
            if (!result.Success || result.Profile is null)
            {
                Publish(State with
                {
                    IsBusy = false,
                    Error = result.Error
                });
                return;
            }

            WorkspaceSessionState session = _workspaceSessionPresenter.SetSavedStatus(currentWorkspace.Value, hasSavedWorkspace: false);
            Publish(State with
            {
                IsBusy = false,
                Error = null,
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                WorkspaceId = currentWorkspace,
                Profile = result.Profile,
                Preferences = result.Preferences,
                HasSavedWorkspace = false
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No workspace loaded."
            });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceSaveResult result = await _workspacePersistenceService.SaveAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success)
            {
                Publish(State with
                {
                    IsBusy = false,
                    Error = result.Error
                });
                return;
            }

            WorkspaceSessionState session = _workspaceSessionPresenter.SetSavedStatus(currentWorkspace.Value, hasSavedWorkspace: true);
            Publish(State with
            {
                IsBusy = false,
                Error = null,
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                WorkspaceId = currentWorkspace,
                HasSavedWorkspace = true,
                Notice = "Workspace saved.",
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task DownloadAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No workspace loaded."
            });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceDownloadResult result = await _workspacePersistenceService.DownloadAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                Publish(State with
                {
                    IsBusy = false,
                    Error = result.Error,
                    PendingDownload = null,
                    PendingExport = null,
                    PendingPrint = null
                });
                return;
            }

            Publish(State with
            {
                IsBusy = false,
                Error = null,
                Notice = $"Download prepared: {result.Receipt.FileName} ({result.Receipt.DocumentLength} bytes).",
                PendingDownload = result.Receipt,
                PendingDownloadVersion = State.PendingDownloadVersion + 1,
                PendingExport = null,
                PendingPrint = null
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message,
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
        }
    }

    public async Task ExportAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No workspace loaded."
            });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceExportResult result = await _workspacePersistenceService.ExportAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                Publish(State with
                {
                    ActiveDialog = null,
                    IsBusy = false,
                    Error = result.Error,
                    PendingDownload = null,
                    PendingExport = null,
                    PendingPrint = null
                });
                return;
            }

            Publish(State with
            {
                ActiveDialog = null,
                IsBusy = false,
                Error = null,
                LatestPortabilityActivity = result.Receipt.Portability is null
                    ? null
                    : new WorkspacePortabilityActivity("Last portable export", result.Receipt.Portability),
                Notice = BuildExportNotice(result.Receipt),
                PendingDownload = null,
                PendingExport = result.Receipt,
                PendingExportVersion = State.PendingExportVersion + 1,
                PendingPrint = null
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message,
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
        }
    }

    private static string BuildExportNotice(WorkspaceExportReceipt receipt)
    {
        if (receipt.Portability is { } portability)
        {
            return $"Portable export prepared: {receipt.FileName} ({receipt.DocumentLength} bytes). {portability.ReceiptSummary}";
        }

        return $"Export prepared: {receipt.FileName} ({receipt.DocumentLength} bytes).";
    }

    public async Task PrintAsync(CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No workspace loaded."
            });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspacePrintResult result = await _workspacePersistenceService.PrintAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                Publish(State with
                {
                    ActiveDialog = null,
                    IsBusy = false,
                    Error = result.Error,
                    PendingDownload = null,
                    PendingExport = null,
                    PendingPrint = null
                });
                return;
            }

            Publish(State with
            {
                ActiveDialog = null,
                IsBusy = false,
                Error = null,
                Notice = $"Print preview prepared: {result.Receipt.Title}.",
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = result.Receipt,
                PendingPrintVersion = State.PendingPrintVersion + 1
            });
        }
        catch (Exception ex)
        {
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message,
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
        }
    }
}
