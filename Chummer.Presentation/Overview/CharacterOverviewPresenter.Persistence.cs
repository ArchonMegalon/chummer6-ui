using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public async Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
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

        IWorkspaceRecoveryCaptureIntent? postCommitCaptureIntent = null;
        try
        {
            long expectedContentRevision = State.ContentRevision;
            if (expectedContentRevision <= 0)
            {
                Publish(State with { IsBusy = false, Error = "Dossier revision is unavailable. Reload before editing." });
                return;
            }

            if (HasAuthoritativeRecoveryLoader)
            {
                long anticipatedContentRevision = checked(expectedContentRevision + 1);
                _workspaceRecoveryPayloadStore.TryBeginCaptureIntent(
                    currentWorkspace.Value,
                    anticipatedContentRevision,
                    out postCommitCaptureIntent);
            }

            WorkspaceOperationExecution<WorkspaceMetadataUpdateResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    token => _workspacePersistenceService.UpdateMetadataAsync(
                        _client,
                        currentWorkspace.Value,
                        expectedContentRevision,
                        command,
                        State.Preferences,
                        token),
                    ct)
                .ConfigureAwait(false);
            if (!execution.CanPublish)
            {
                if (execution.HasValue
                    && execution.Value is { Success: true, Profile: not null } staleResult
                    && HasAuthoritativeRecoveryLoader)
                {
                    long staleContentRevision = staleResult.ContentRevision > 0
                        ? staleResult.ContentRevision
                        : expectedContentRevision + 1;
                    using var postCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
                    bool staleRecoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                        currentWorkspace.Value,
                        staleContentRevision,
                        postCommitBudget.Token,
                        postCommitCaptureIntent).ConfigureAwait(false);
                    if (!staleRecoveryCaptured)
                    {
                        GateStalePostCommitRecovery(
                            currentWorkspace.Value,
                            staleContentRevision,
                            "stale postcommit metadata recovery",
                            "The metadata committed after its view was superseded, but exact recovery validation failed. Review this runner before closing it.");
                    }
                    postCommitCaptureIntent = null;
                }

                postCommitCaptureIntent?.Dispose();
                postCommitCaptureIntent = null;
                return;
            }

            WorkspaceMetadataUpdateResult result = execution.Value;
            if (!result.Success || result.Profile is null)
            {
                postCommitCaptureIntent?.Dispose();
                postCommitCaptureIntent = null;
                WorkspaceSessionState failedSession = result.Outcome == WorkspaceOperationOutcome.Conflict
                    ? _workspaceSessionPresenter.SetConflictState(
                        currentWorkspace.Value,
                        new WorkspaceConflictState(
                            "metadata update",
                            expectedContentRevision,
                            result.ContentRevision > 0 ? result.ContentRevision : null,
                            result.Error ?? "The dossier changed before metadata could be updated."))
                    : _workspaceSessionPresenter.State;
                if (result.Outcome == WorkspaceOperationOutcome.Conflict)
                {
                    _workspaceRecoveryPayloadStore.SetProtected(
                        currentWorkspace.Value,
                        expectedContentRevision,
                        protectedFromEviction: true);
                }
                Publish(State with
                {
                    IsBusy = false,
                    Error = result.Error,
                    Notice = result.Outcome == WorkspaceOperationOutcome.Conflict
                        ? "Metadata update stopped because a newer dossier revision won. No overwrite was attempted."
                        : State.Notice,
                    Session = failedSession,
                    OpenWorkspaces = failedSession.OpenWorkspaces
                });
                return;
            }

            long contentRevision = result.ContentRevision > 0
                ? result.ContentRevision
                : expectedContentRevision + 1;
            long savedRevision = result.SavedRevision > 0 || State.SavedRevision == 0
                ? result.SavedRevision
                : State.SavedRevision;
            bool recoveryCaptured = !HasAuthoritativeRecoveryLoader;
            if (!recoveryCaptured)
            {
                using var postCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
                recoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                    currentWorkspace.Value,
                    contentRevision,
                    postCommitBudget.Token,
                    postCommitCaptureIntent).ConfigureAwait(false);
                postCommitCaptureIntent = null;
            }
            WorkspaceSessionState session = _workspaceSessionPresenter.SetRevisions(
                currentWorkspace.Value,
                contentRevision,
                savedRevision);
            string? notice = State.Notice;
            if (!recoveryCaptured)
            {
                session = _workspaceSessionPresenter.SetConflictState(
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "postcommit metadata recovery",
                        contentRevision,
                        contentRevision,
                        "The metadata committed, but exact postcommit recovery could not be secured within its bounded verification window."));
                _workspaceRecoveryPayloadStore.SetProtected(
                    currentWorkspace.Value,
                    contentRevision,
                    protectedFromEviction: true);
                notice = "Metadata committed, but exact postcommit recovery is review-gated. Keep this runner open.";
            }
            PublishPostCommitState(State with
            {
                IsBusy = false,
                Error = null,
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                WorkspaceId = currentWorkspace,
                Profile = result.Profile,
                Preferences = result.Preferences,
                Notice = notice
            });
            TryCapturePostCommitWorkspaceView(
                "Metadata committed, but the local workspace view could not be retained; it will refresh on the next interaction.");
        }
        catch (Exception ex)
        {
            postCommitCaptureIntent?.Dispose();
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
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

        IWorkspaceRecoveryCaptureIntent? postCommitCaptureIntent = null;
        try
        {
            long expectedContentRevision = State.ContentRevision;
            if (expectedContentRevision <= 0)
            {
                Publish(State with { IsBusy = false, Error = "Dossier revision is unavailable. Reload before saving." });
                return;
            }

            if (HasAuthoritativeRecoveryLoader)
            {
                _workspaceRecoveryPayloadStore.TryBeginCaptureIntent(
                    currentWorkspace.Value,
                    expectedContentRevision,
                    out postCommitCaptureIntent);
            }

            WorkspaceOperationExecution<WorkspaceSaveResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    token => _workspacePersistenceService.SaveAsync(
                        _client,
                        currentWorkspace.Value,
                        expectedContentRevision,
                        token),
                    ct)
                .ConfigureAwait(false);
            if (!execution.CanPublish)
            {
                if (execution.HasValue
                    && execution.Value is { Success: true } staleResult
                    && HasAuthoritativeRecoveryLoader)
                {
                    long staleContentRevision = staleResult.Receipt?.ContentRevision > 0
                        ? staleResult.Receipt.ContentRevision
                        : expectedContentRevision;
                    using var postCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
                    bool staleRecoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                        currentWorkspace.Value,
                        staleContentRevision,
                        postCommitBudget.Token,
                        postCommitCaptureIntent).ConfigureAwait(false);
                    if (!staleRecoveryCaptured)
                    {
                        GateStalePostCommitRecovery(
                            currentWorkspace.Value,
                            staleContentRevision,
                            "stale postcommit save recovery",
                            "The save committed after its view was superseded, but exact recovery validation failed. Review this runner before closing it.");
                    }
                    postCommitCaptureIntent = null;
                }

                postCommitCaptureIntent?.Dispose();
                postCommitCaptureIntent = null;
                return;
            }

            WorkspaceSaveResult result = execution.Value;
            if (!result.Success)
            {
                postCommitCaptureIntent?.Dispose();
                postCommitCaptureIntent = null;
                WorkspaceSessionState failedSession = result.Outcome == WorkspaceOperationOutcome.Conflict
                    ? _workspaceSessionPresenter.SetConflictState(
                        currentWorkspace.Value,
                        new WorkspaceConflictState(
                            "save",
                            expectedContentRevision,
                            result.Receipt?.ContentRevision > 0 ? result.Receipt.ContentRevision : null,
                            result.Error ?? "The dossier changed before it could be saved."))
                    : _workspaceSessionPresenter.State;
                if (result.Outcome == WorkspaceOperationOutcome.Conflict)
                {
                    _workspaceRecoveryPayloadStore.SetProtected(
                        currentWorkspace.Value,
                        expectedContentRevision,
                        protectedFromEviction: true);
                }
                Publish(State with
                {
                    IsBusy = false,
                    Error = result.Error,
                    Notice = result.Outcome == WorkspaceOperationOutcome.Conflict
                        ? "Save stopped because a newer dossier revision won. Reload or resolve the conflict; no overwrite was attempted."
                        : State.Notice,
                    Session = failedSession,
                    OpenWorkspaces = failedSession.OpenWorkspaces
                });
                return;
            }

            long contentRevision = result.Receipt?.ContentRevision > 0
                ? result.Receipt.ContentRevision
                : expectedContentRevision;
            long savedRevision = result.Receipt?.SavedRevision > 0
                ? result.Receipt.SavedRevision
                : contentRevision;
            bool recoveryCaptured = !HasAuthoritativeRecoveryLoader;
            if (!recoveryCaptured)
            {
                using var postCommitBudget = new CancellationTokenSource(PostCommitRecoveryBudget);
                recoveryCaptured = await TryCaptureRecoveryPayloadAsync(
                    currentWorkspace.Value,
                    contentRevision,
                    postCommitBudget.Token,
                    postCommitCaptureIntent).ConfigureAwait(false);
                postCommitCaptureIntent = null;
            }
            WorkspaceSessionState session = _workspaceSessionPresenter.SetRevisions(
                currentWorkspace.Value,
                contentRevision,
                savedRevision);
            string notice;
            if (recoveryCaptured)
            {
                _workspaceRecoveryPayloadStore.SetProtected(
                    currentWorkspace.Value,
                    contentRevision,
                    protectedFromEviction: false);
                notice = "Dossier saved.";
            }
            else
            {
                session = _workspaceSessionPresenter.SetConflictState(
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "postcommit save recovery",
                        contentRevision,
                        contentRevision,
                        "The save committed, but exact postcommit recovery could not be secured within its bounded verification window."));
                _workspaceRecoveryPayloadStore.SetProtected(
                    currentWorkspace.Value,
                    contentRevision,
                    protectedFromEviction: true);
                notice = "Save committed, but exact postcommit recovery is review-gated. Keep this runner open.";
            }
            PublishPostCommitState(State with
            {
                IsBusy = false,
                Error = null,
                Session = session,
                OpenWorkspaces = session.OpenWorkspaces,
                WorkspaceId = currentWorkspace,
                Notice = notice,
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
            TryCapturePostCommitWorkspaceView(
                "Save committed, but the local workspace view could not be retained; it will refresh on the next interaction.");
        }
        catch (Exception ex)
        {
            postCommitCaptureIntent?.Dispose();
            Publish(State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    public async Task DownloadAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
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
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
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
            return $"Portable export ready: {receipt.FileName} ({receipt.DocumentLength} bytes). {portability.ReceiptSummary}";
        }

        return $"Export prepared: {receipt.FileName} ({receipt.DocumentLength} bytes).";
    }

    public async Task PrintAsync(CancellationToken ct)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
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
