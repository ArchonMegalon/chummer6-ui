using Chummer.Contracts.Workspaces;
using Chummer.Application.Owners;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    public Task UpdateMetadataAsync(UpdateWorkspaceMetadata command, CancellationToken ct)
        => UpdateMetadataCoreAsync(State, command, ct);

    private async Task UpdateMetadataCoreAsync(CharacterOverviewState originalState,
        UpdateWorkspaceMetadata command, CancellationToken ct,
        Action<CommandResult<WorkspaceMetadataResult>?>? observeCanonical = null)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        OwnerContextStamp? originalOwner = originalState.DisplayOwnerContext;
        long displayGeneration = CaptureDisplayGeneration();
        CharacterWorkspaceId? currentWorkspace = originalState.WorkspaceId;
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
            });
            return;
        }

        if (!IsOriginalPersistenceOwnerCurrent(originalOwner))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
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
            long expectedContentRevision = originalState.ContentRevision;
            if (expectedContentRevision <= 0)
            {
                Publish(State with { IsBusy = false, Error = "Dossier revision is unavailable. Reload before editing." });
                return;
            }

            if (HasAuthoritativeRecoveryLoader)
            {
                long anticipatedContentRevision = checked(expectedContentRevision + 1);
                RecoveryPayloads(originalOwner).TryBeginCaptureIntent(
                    currentWorkspace.Value,
                    anticipatedContentRevision,
                    out postCommitCaptureIntent);
            }

            WorkspaceOperationExecution<WorkspaceMetadataUpdateResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    async token =>
                    {
                        WorkspaceMetadataUpdateResult persisted = originalOwner is { } original
                        ? await _workspacePersistenceService.UpdateMetadataAsync(
                            _client, original, currentWorkspace.Value, expectedContentRevision,
                            command, originalState.Preferences, token)
                        : await _workspacePersistenceService.UpdateMetadataAsync(
                        _client,
                        currentWorkspace.Value,
                        expectedContentRevision,
                        command,
                        originalState.Preferences,
                        token);
                        observeCanonical?.Invoke(persisted.CanonicalResult);
                        return persisted;
                    },
                    ct)
                .ConfigureAwait(false);
            if (!IsOriginalPersistenceOwnerCurrent(originalOwner))
            {
                postCommitCaptureIntent?.Dispose();
                AbandonOriginalPersistenceView(displayGeneration, originalState,
                    execution.HasValue && execution.Value.Success);
                return;
            }
            if (!execution.CanPublish || !IsDisplayGenerationCurrent(displayGeneration))
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
                        postCommitCaptureIntent, originalOwner: originalOwner).ConfigureAwait(false);
                    if (!staleRecoveryCaptured)
                    {
                        GateStalePostCommitRecovery(
                            originalOwner,
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
                    ? SetOriginalSessionConflict(
                        originalOwner,
                        currentWorkspace.Value,
                        new WorkspaceConflictState(
                            "metadata update",
                            expectedContentRevision,
                            result.ContentRevision > 0 ? result.ContentRevision : null,
                            result.Error ?? "The dossier changed before metadata could be updated."))
                    : _workspaceSessionPresenter.State;
                if (result.Outcome == WorkspaceOperationOutcome.Conflict)
                {
                    RecoveryPayloads(originalOwner).SetProtected(
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
                    postCommitCaptureIntent, originalOwner: originalOwner).ConfigureAwait(false);
                postCommitCaptureIntent = null;
            }
            if (!IsOriginalPersistenceOwnerCurrent(originalOwner) || !IsDisplayGenerationCurrent(displayGeneration))
            {
                AbandonOriginalPersistenceView(displayGeneration, originalState, committed: true);
                return;
            }
            WorkspaceSessionState session = SetOriginalSessionRevisions(
                originalOwner,
                currentWorkspace.Value,
                contentRevision,
                savedRevision);
            string? notice = State.Notice;
            if (!recoveryCaptured)
            {
                session = SetOriginalSessionConflict(
                    originalOwner,
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "postcommit metadata recovery",
                        contentRevision,
                        contentRevision,
                        "The metadata committed, but exact postcommit recovery could not be secured within its bounded verification window."));
                RecoveryPayloads(originalOwner).SetProtected(
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
            }, displayGeneration);
            TryCapturePostCommitWorkspaceView(
                "Metadata committed, but the local workspace view could not be retained; it will refresh on the next interaction.");
        }
        catch (Exception ex)
        {
            postCommitCaptureIntent?.Dispose();
            TryPublishDisplayTransition(displayGeneration, State with
            {
                IsBusy = false,
                Error = ex.Message
            }, originalState);
        }
    }

    public Task SaveAsync(CancellationToken ct)
        => SaveCoreAsync(State, ct);

    private async Task SaveCoreAsync(CharacterOverviewState originalState, CancellationToken ct,
        Action<CommandResult<WorkspaceSaveReceipt>?>? observeCanonical = null)
    {
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        OwnerContextStamp? originalOwner = originalState.DisplayOwnerContext;
        long displayGeneration = CaptureDisplayGeneration();
        CharacterWorkspaceId? currentWorkspace = originalState.WorkspaceId;
        if (currentWorkspace is null)
        {
            Publish(State with
            {
                Error = "No dossier loaded."
            });
            return;
        }

        if (!IsOriginalPersistenceOwnerCurrent(originalOwner))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
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
            long expectedContentRevision = originalState.ContentRevision;
            if (expectedContentRevision <= 0)
            {
                Publish(State with { IsBusy = false, Error = "Dossier revision is unavailable. Reload before saving." });
                return;
            }

            if (HasAuthoritativeRecoveryLoader)
            {
                RecoveryPayloads(originalOwner).TryBeginCaptureIntent(
                    currentWorkspace.Value,
                    expectedContentRevision,
                    out postCommitCaptureIntent);
            }

            WorkspaceOperationExecution<WorkspaceSaveResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    async token =>
                    {
                        WorkspaceSaveResult persisted = originalOwner is { } original
                        ? await _workspacePersistenceService.SaveAsync(
                            _client, original, currentWorkspace.Value, expectedContentRevision, token)
                        : await _workspacePersistenceService.SaveAsync(
                        _client,
                        currentWorkspace.Value,
                        expectedContentRevision,
                        token);
                        observeCanonical?.Invoke(persisted.CanonicalResult);
                        return persisted;
                    },
                    ct)
                .ConfigureAwait(false);
            if (!IsOriginalPersistenceOwnerCurrent(originalOwner))
            {
                postCommitCaptureIntent?.Dispose();
                AbandonOriginalPersistenceView(displayGeneration, originalState,
                    execution.HasValue && execution.Value.Success);
                return;
            }
            if (!execution.CanPublish || !IsDisplayGenerationCurrent(displayGeneration))
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
                        postCommitCaptureIntent, originalOwner: originalOwner).ConfigureAwait(false);
                    if (!staleRecoveryCaptured)
                    {
                        GateStalePostCommitRecovery(
                            originalOwner,
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
                    ? SetOriginalSessionConflict(
                        originalOwner,
                        currentWorkspace.Value,
                        new WorkspaceConflictState(
                            "save",
                            expectedContentRevision,
                            result.Receipt?.ContentRevision > 0 ? result.Receipt.ContentRevision : null,
                            result.Error ?? "The dossier changed before it could be saved."))
                    : _workspaceSessionPresenter.State;
                if (result.Outcome == WorkspaceOperationOutcome.Conflict)
                {
                    RecoveryPayloads(originalOwner).SetProtected(
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
                    postCommitCaptureIntent, originalOwner: originalOwner).ConfigureAwait(false);
                postCommitCaptureIntent = null;
            }
            if (!IsOriginalPersistenceOwnerCurrent(originalOwner) || !IsDisplayGenerationCurrent(displayGeneration))
            {
                AbandonOriginalPersistenceView(displayGeneration, originalState, committed: true);
                return;
            }
            WorkspaceSessionState session = SetOriginalSessionRevisions(
                originalOwner,
                currentWorkspace.Value,
                contentRevision,
                savedRevision);
            string notice;
            if (recoveryCaptured)
            {
                RecoveryPayloads(originalOwner).SetProtected(
                    currentWorkspace.Value,
                    contentRevision,
                    protectedFromEviction: false);
                notice = "Dossier saved.";
            }
            else
            {
                session = SetOriginalSessionConflict(
                    originalOwner,
                    currentWorkspace.Value,
                    new WorkspaceConflictState(
                        "postcommit save recovery",
                        contentRevision,
                        contentRevision,
                        "The save committed, but exact postcommit recovery could not be secured within its bounded verification window."));
                RecoveryPayloads(originalOwner).SetProtected(
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
            }, displayGeneration);
            TryCapturePostCommitWorkspaceView(
                "Save committed, but the local workspace view could not be retained; it will refresh on the next interaction.");
        }
        catch (Exception ex)
        {
            postCommitCaptureIntent?.Dispose();
            TryPublishDisplayTransition(displayGeneration, State with
            {
                IsBusy = false,
                Error = ex.Message
            }, originalState);
        }
    }

    public Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId, long expectedContentRevision, CancellationToken ct)
        => RunOriginalPersistenceGestureAsync<WorkspaceSaveReceipt>(originalOwner, workspaceId,
            expectedContentRevision, (state, observe) => SaveCoreAsync(state, ct, observe));

    public Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId, long expectedContentRevision, UpdateWorkspaceMetadata command, CancellationToken ct)
        => RunOriginalPersistenceGestureAsync<WorkspaceMetadataResult>(originalOwner, workspaceId,
            expectedContentRevision, (state, observe) => UpdateMetadataCoreAsync(state, command, ct, observe));

    private async Task<CommandResult<T>> RunOriginalPersistenceGestureAsync<T>(OwnerContextStamp originalOwner,
        CharacterWorkspaceId workspaceId, long expectedContentRevision,
        Func<CharacterOverviewState, Action<CommandResult<T>?>, Task> operation) where T : class
    {
        CharacterOverviewState originalState = State;
        if (!originalOwner.IsValid || originalState.DisplayOwnerContext != originalOwner
            || originalState.WorkspaceId != workspaceId || expectedContentRevision <= 0
            || originalState.ContentRevision != expectedContentRevision
            || !IsOriginalPersistenceOwnerCurrent(originalOwner))
            return new(false, null, "The original account or runner changed. Reopen before saving.", WorkspaceOperationOutcome.Conflict);

        CommandResult<T>? observed = null;
        try { await operation(originalState, result => observed = result).ConfigureAwait(false); }
        catch (Exception error) when (observed is not null && error is not OutOfMemoryException)
        {
            // A joined canonical result survives optional view/recovery follow-up failures.
        }
        return observed ?? new(false, null,
            "No canonical persistence result is available. Reload to review the runner.", WorkspaceOperationOutcome.Unavailable);
    }

    private bool IsOriginalPersistenceOwnerCurrent(OwnerContextStamp? originalOwner)
    {
        if (_client is not IOwnerBoundWorkspaceMutationClient bound)
            return originalOwner is null;
        try
        {
            return originalOwner is { IsValid: true } original
                && State.DisplayOwnerContext == original
                && bound.CaptureOwnerContext() == original;
        }
        catch (Exception error) when (error is InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void AbandonOriginalPersistenceView(long generation, CharacterOverviewState originalState, bool committed)
        => TryPublishDisplayTransition(generation, CharacterOverviewState.Empty with
        {
            Preferences = originalState.Preferences,
            Commands = originalState.Commands,
            NavigationTabs = originalState.NavigationTabs,
            Notice = committed
                ? "The change was committed for the original account. Reopen that account to review it."
                : "The account or runner view changed. Reload before saving or editing."
        }, originalState);

    public async Task DownloadAsync(CancellationToken ct)
    {
        CancellationToken outputCancellation = ct;
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterOverviewState originalState = State;
        long displayGeneration = BeginDisplayTransition();
        if (!IsOriginalPersistenceOwnerCurrent(originalState.DisplayOwnerContext))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
            return;
        }
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
            {
                Error = "No dossier loaded."
            });
            return;
        }

        PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceDownloadResult result = originalState.DisplayOwnerContext is { } original
                ? await _workspacePersistenceService.DownloadAsync(_client, original, currentWorkspace.Value, originalState.ContentRevision, ct)
                : await _workspacePersistenceService.DownloadAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
                {
                    IsBusy = false,
                    Error = result.Error,
                    PendingDownload = null,
                    PendingExport = null,
                    PendingPrint = null
                });
                return;
            }

            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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
        CancellationToken outputCancellation = ct;
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterOverviewState originalState = State;
        long displayGeneration = BeginDisplayTransition();
        if (!IsOriginalPersistenceOwnerCurrent(originalState.DisplayOwnerContext))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
            return;
        }
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
            {
                Error = "No dossier loaded."
            });
            return;
        }

        PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspaceExportResult result = originalState.DisplayOwnerContext is { } original
                ? await _workspacePersistenceService.ExportAsync(_client, original, currentWorkspace.Value, originalState.ContentRevision, ct)
                : await _workspacePersistenceService.ExportAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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

            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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
        CancellationToken outputCancellation = ct;
        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        CharacterOverviewState originalState = State;
        long displayGeneration = BeginDisplayTransition();
        if (!IsOriginalPersistenceOwnerCurrent(originalState.DisplayOwnerContext))
        {
            AbandonOriginalPersistenceView(displayGeneration, originalState, committed: false);
            return;
        }
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
            {
                Error = "No dossier loaded."
            });
            return;
        }

        PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
        {
            IsBusy = true,
            Error = null,
            PendingDownload = null,
            PendingExport = null,
            PendingPrint = null
        });

        try
        {
            WorkspacePrintResult result = originalState.DisplayOwnerContext is { } original
                ? await _workspacePersistenceService.PrintAsync(_client, original, currentWorkspace.Value, originalState.ContentRevision, ct)
                : await _workspacePersistenceService.PrintAsync(_client, currentWorkspace.Value, ct);
            if (!result.Success || result.Receipt is null)
            {
                PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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

            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
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
            PublishOutputTransition(displayGeneration, originalState, outputCancellation, State with
            {
                IsBusy = false,
                Error = ex.Message,
                PendingDownload = null,
                PendingExport = null,
                PendingPrint = null
            });
        }
    }

    private bool PublishOutputTransition(long generation, CharacterOverviewState original, CancellationToken outputCancellation, CharacterOverviewState next)
    {
        if (!IsDisplayGenerationCurrent(generation)) return false;
        if (!IsOriginalPersistenceOwnerCurrent(original.DisplayOwnerContext))
        {
            AbandonOriginalPersistenceView(generation, original, committed: false);
            return false;
        }
        object? receipt = (object?)next.PendingDownload ?? (object?)next.PendingExport ?? next.PendingPrint;
        CharacterWorkspaceId? id = original.WorkspaceId ?? original.Session.ActiveWorkspaceId;
        CharacterWorkspaceId? receiptId = receipt switch
        {
            WorkspaceDownloadReceipt download => download.Id,
            WorkspaceExportReceipt export => export.Id,
            WorkspacePrintReceipt print => print.Id,
            _ => null
        };
        if (receipt is not null && (id is null || receiptId != id))
            next = next with
            {
                IsBusy = false, Error = "The prepared output does not match the original runner.",
                PendingDownload = null, PendingExport = null, PendingPrint = null, PendingOutputBinding = null
            };
        else next = next with
        {
            PendingOutputBinding = receipt is null || id is null ? null
                : new WorkspaceOutputBinding(original.DisplayOwnerContext, id.Value, original.ContentRevision, receipt, outputCancellation)
        };
        return TryPublishDisplayTransition(generation, next, original);
    }
}
