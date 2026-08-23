using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;
using Chummer.Contracts.Characters;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private readonly CharacterCreationWizardDesktopSession _creationWizardSession = new();
    private readonly CancellationTokenSource _creationWizardLifetime = new();
    private AvaloniaCreationWizardCheckpointStore? _creationWizardCheckpointStore;
    private CharacterCreationWizardSnapshot? _boundCreationWizardSnapshot;
    private CharacterCreationContactPreparedPreview? _creationContactPreparedPreview;
    private bool _creationWizardBuildGhostPreferenceEnabled;

    private void InitializeCreationWizard()
    {
        try
        {
            _creationWizardCheckpointStore = AvaloniaCreationWizardCheckpointStore.CreateDefault();
        }
        catch (InvalidOperationException)
        {
            _creationWizardCheckpointStore = null;
        }

        CharacterCreationWizardControl.StepRequested += CreationWizard_OnStepRequested;
        CharacterCreationWizardControl.ContinueRequested += CreationWizard_OnContinueRequested;
        CharacterCreationWizardControl.BuildGhostQuestionSubmitted += CreationWizard_OnBuildGhostQuestionSubmitted;
        CharacterCreationWizardControl.RecoverCheckpointRequested += CreationWizard_OnRecoverCheckpointRequested;
        CharacterCreationWizardControl.ExportCheckpointRequested += CreationWizard_OnExportCheckpointRequested;
        CharacterCreationWizardControl.ContactPreviewRequested += CreationWizard_OnContactPreviewRequested;
        CharacterCreationWizardControl.ContactConfirmRequested += CreationWizard_OnContactConfirmRequested;
    }

    private void DetachCreationWizard()
    {
        _creationWizardLifetime.Cancel();
        CharacterCreationWizardControl.StepRequested -= CreationWizard_OnStepRequested;
        CharacterCreationWizardControl.ContinueRequested -= CreationWizard_OnContinueRequested;
        CharacterCreationWizardControl.BuildGhostQuestionSubmitted -= CreationWizard_OnBuildGhostQuestionSubmitted;
        CharacterCreationWizardControl.RecoverCheckpointRequested -= CreationWizard_OnRecoverCheckpointRequested;
        CharacterCreationWizardControl.ExportCheckpointRequested -= CreationWizard_OnExportCheckpointRequested;
        CharacterCreationWizardControl.ContactPreviewRequested -= CreationWizard_OnContactPreviewRequested;
        CharacterCreationWizardControl.ContactConfirmRequested -= CreationWizard_OnContactConfirmRequested;
    }

    private void ApplyCreationWizardState(CharacterOverviewState overview)
    {
        CharacterCreationWizardSnapshot? snapshot = overview.CreationWizard;
        CharacterCreationWizardControl.IsVisible = snapshot is not null;
        CharacterCreationWizardControl.IsHitTestVisible = snapshot is not null;
        if (snapshot is null)
        {
            if (_boundCreationWizardSnapshot is not null && overview.Profile?.Created == true)
            {
                TryDeleteWizardCheckpoint(_boundCreationWizardSnapshot.WorkspaceId);
            }

            _boundCreationWizardSnapshot = null;
            _creationContactPreparedPreview = null;
            return;
        }

        // An unfinished character owns the center surface. The unrestricted editor remains
        // unreachable until canonical finalization makes CreationWizard null.
        ClassicFormPortHostControl.IsVisible = false;
        ClassicFormPortHostControl.IsHitTestVisible = false;
        SectionHostControl.IsVisible = false;
        SectionHostControl.IsHitTestVisible = false;
        RightShellRegion.IsVisible = false;
        RightShellRegion.IsHitTestVisible = false;
        RightShellRegion.Opacity = 0d;
        ApplyPaneWidth(RightShellRegion, false, RightShellWidth, 0d, RightShellWidth);
        if (ContentRegion.ColumnDefinitions.Count >= 3)
            ContentRegion.ColumnDefinitions[2].Width = new GridLength(0);

        if (_boundCreationWizardSnapshot is null
            || !string.Equals(
                _boundCreationWizardSnapshot.SnapshotDigest,
                snapshot.SnapshotDigest,
                StringComparison.Ordinal))
        {
            _creationContactPreparedPreview = null;
        }

        CharacterCreationWizardDesktopCheckpoint? checkpoint = ResolveCheckpoint(snapshot);
        CharacterCreationWizardDesktopState state = _creationWizardSession.Bind(
            snapshot,
            checkpoint,
            overview.CreationContacts);
        _boundCreationWizardSnapshot = snapshot;
        CharacterCreationWizardControl.SetState(state);
        _creationWizardBuildGhostPreferenceEnabled = !overview.Preferences.DisableAiFeatures;
        CharacterCreationWizardControl.SetBuildGhostEnabled(_creationWizardBuildGhostPreferenceEnabled);
        TryPersistWizardCheckpoint();
    }

    private void CreationWizard_OnContactPreviewRequested(
        object? sender,
        CharacterCreationContactPreviewRequested request)
    {
        if (_creationContactsInteractionPresenter is null)
            return;

        CharacterCreationContactsInteractionPrepareResult result =
            _creationContactsInteractionPresenter.Prepare(_adapter.State, request.Input);
        if (result.PreparedPreview is { } prepared)
        {
            if (_creationContactPreparedPreview is { } previous
                && string.Equals(
                    previous.PreviewDigest,
                    prepared.PreviewDigest,
                    StringComparison.Ordinal))
            {
                prepared = prepared with { IdempotencyKey = previous.IdempotencyKey };
                result = result with { PreparedPreview = prepared };
            }
            _creationContactPreparedPreview = prepared;
        }
        else
        {
            _creationContactPreparedPreview = null;
        }

        CharacterCreationWizardControl.SetContactPrepareResult(result);
    }

    private async void CreationWizard_OnContactConfirmRequested(
        object? sender,
        CharacterCreationContactConfirmRequested request)
    {
        if (_creationContactsInteractionPresenter is null
            || _creationContactPreparedPreview is not { } prepared
            || !string.Equals(request.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal))
        {
            return;
        }

        CharacterCreationWizardControl.SetContactMutationBusy(true);
        try
        {
            CharacterCreationContactsInteractionConfirmResult result =
                _creationContactsInteractionPresenter.Confirm(
                    _adapter.State,
                    new CharacterCreationContactConfirmation(
                        prepared,
                        prepared.PreviewDigest,
                        prepared.IdempotencyKey,
                        ExplicitlyConfirmed: true));
            CharacterCreationWizardControl.SetContactConfirmResult(result);
            if (result.Outcome is CharacterCreationContactOutcomes.Applied
                or CharacterCreationContactOutcomes.Replayed
                || result.Receipt is not null)
            {
                await ReloadCreationContactsAuthorityAsync(_creationWizardLifetime.Token)
                    .ConfigureAwait(true);
                return;
            }

            if (string.Equals(
                    result.Outcome,
                    CharacterCreationContactOutcomes.Unavailable,
                    StringComparison.Ordinal))
            {
                await RecoverAmbiguousCreationContactResultAsync(
                        prepared.IdempotencyKey,
                        _creationWizardLifetime.Token)
                    .ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            await RecoverAmbiguousCreationContactResultAsync(
                    prepared.IdempotencyKey,
                    _creationWizardLifetime.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            CharacterCreationWizardControl.SetContactMutationBusy(false);
        }
    }

    private async Task RecoverAmbiguousCreationContactResultAsync(
        string idempotencyKey,
        CancellationToken ct)
    {
        if (_creationContactsInteractionPresenter is null)
            return;
        CharacterCreationContactsInteractionReceiptLookupResult lookup;
        try
        {
            lookup = _creationContactsInteractionPresenter.LookupReceipt(
                _adapter.State,
                idempotencyKey);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            lookup = new CharacterCreationContactsInteractionReceiptLookupResult(
                CharacterCreationContactOutcomes.Unavailable,
                null,
                null,
                [CharacterCreationContactsBlockers.AuthorityUnavailable]);
        }
        CharacterCreationWizardControl.SetContactReceiptLookupResult(lookup);
        if (lookup.Receipt is not null)
        {
            await ReloadCreationContactsAuthorityAsync(ct).ConfigureAwait(true);
        }
    }

    private async Task ReloadCreationContactsAuthorityAsync(CancellationToken ct)
    {
        if (_adapter.State.WorkspaceId is not { } workspaceId)
            return;
        await _adapter.LoadAsync(workspaceId, ct).ConfigureAwait(true);
        ApplyCreationWizardState(_adapter.State);
    }

    private CharacterCreationWizardDesktopCheckpoint? ResolveCheckpoint(
        CharacterCreationWizardSnapshot snapshot)
    {
        if (_boundCreationWizardSnapshot is not null
            && string.Equals(
                _boundCreationWizardSnapshot.WorkspaceId,
                snapshot.WorkspaceId,
                StringComparison.Ordinal))
        {
            return _creationWizardSession.CreateCheckpoint();
        }

        AvaloniaCreationWizardCheckpointLoad load = _creationWizardCheckpointStore?.Load(snapshot.WorkspaceId)
            ?? new AvaloniaCreationWizardCheckpointLoad(null, null);
        if (load.Checkpoint is not null)
            return load.Checkpoint;
        return load.RecoveryReason is null
            ? null
            : new CharacterCreationWizardDesktopCheckpoint(
                Schema: "invalid",
                WorkspaceId: snapshot.WorkspaceId,
                WorkspaceRevision: snapshot.WorkspaceRevision,
                SnapshotDigest: snapshot.SnapshotDigest,
                SelectedStepId: snapshot.ActiveStepId);
    }

    private void CreationWizard_OnStepRequested(
        object? sender,
        CharacterCreationWizardStepRequest request)
    {
        if (_creationWizardSession.TrySelectStep(request.StepId))
            RefreshCreationWizardControlAndCheckpoint();
    }

    private void CreationWizard_OnContinueRequested(object? sender, EventArgs e)
    {
        if (_creationWizardSession.TryContinue())
            RefreshCreationWizardControlAndCheckpoint();
    }

    private void RefreshCreationWizardControlAndCheckpoint()
    {
        CharacterCreationWizardControl.SetState(_creationWizardSession.State);
        TryPersistWizardCheckpoint();
    }

    private void TryPersistWizardCheckpoint()
    {
        try
        {
            _creationWizardCheckpointStore?.Save(_creationWizardSession.CreateCheckpoint());
        }
        catch (IOException)
        {
            // Navigation remains usable. Recovery/export stays fail-closed until storage returns.
        }
        catch (UnauthorizedAccessException)
        {
            // Navigation remains usable. Recovery/export stays fail-closed until storage returns.
        }
    }

    private void TryDeleteWizardCheckpoint(string workspaceId)
    {
        try
        {
            _creationWizardCheckpointStore?.Delete(workspaceId);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void CreationWizard_OnBuildGhostQuestionSubmitted(
        object? sender,
        CharacterCreationWizardBuildGhostQuestion question)
    {
        if (!CharacterCreationWizardBuildGhostPolicy.CanSend(
                _creationWizardSession.State,
                _creationWizardBuildGhostPreferenceEnabled))
        {
            CharacterCreationWizardControl.AppendBuildGhostAnswer(
                "Rook",
                "Build Ghost is waiting for authoritative runtime and wizard context.");
            return;
        }

        _ = AskCreationWizardBuildGhostAsync(question, _creationWizardLifetime.Token);
    }

    private async Task AskCreationWizardBuildGhostAsync(
        CharacterCreationWizardBuildGhostQuestion turn,
        CancellationToken ct)
    {
        CharacterCreationWizardControl.SetBuildGhostBusy(true);
        try
        {
            string contextJson = CharacterCreationWizardDesktopSession.SerializeBuildGhostContext(turn.Context);
            string groundedMessage =
                "You are the Build Ghost inside Chummer's character-creation wizard. "
                + "Answer the user's question from the revision-bound context below. "
                + "Advice and reviewable suggestions only: do not claim to apply, confirm, finalize, or mutate anything."
                + Environment.NewLine
                + contextJson
                + Environment.NewLine
                + $"User question: {turn.Question}";
            AiConversationTurnRequest request = new(
                Message: groundedMessage,
                ConversationId: $"creation-wizard:{turn.Context.WorkspaceId}",
                RuntimeFingerprint: turn.Context.RuntimeFingerprint,
                CharacterId: turn.Context.WorkspaceId,
                WorkspaceId: turn.Context.WorkspaceId);
            AvaloniaCoachSidecarCallResult<AiConversationTurnResponse> result =
                await _coachSidecarClient.SendBuildTurnAsync(request, ct).ConfigureAwait(true);

            if (!MatchesCurrentBuildGhostContext(turn.Context))
            {
                CharacterCreationWizardControl.AppendBuildGhostAnswer(
                    "Rook",
                    "The character changed while I was answering. Ask again so the advice can bind to the current draft.");
                return;
            }

            string answer = result.IsSuccess && result.Payload is { } response
                ? response.Answer
                : result.ErrorMessage ?? "Build Ghost is unavailable for this revision.";
            CharacterCreationWizardControl.AppendBuildGhostAnswer("Rook", answer);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            CharacterCreationWizardControl.AppendBuildGhostAnswer(
                "Rook",
                "Build Ghost is unavailable for this revision.");
        }
        finally
        {
            CharacterCreationWizardControl.SetBuildGhostBusy(false);
        }
    }

    private bool MatchesCurrentBuildGhostContext(CharacterCreationWizardBuildGhostContext expected)
    {
        if (_boundCreationWizardSnapshot is null)
            return false;
        CharacterCreationWizardBuildGhostContext current = _creationWizardSession.State.BuildGhostContext;
        return string.Equals(current.WorkspaceId, expected.WorkspaceId, StringComparison.Ordinal)
               && current.WorkspaceRevision == expected.WorkspaceRevision
               && string.Equals(current.WizardSnapshotDigest, expected.WizardSnapshotDigest, StringComparison.Ordinal)
               && string.Equals(current.ActiveStepId, expected.ActiveStepId, StringComparison.Ordinal);
    }

    private void CreationWizard_OnRecoverCheckpointRequested(object? sender, EventArgs e)
        => _ = RunCreationWizardFileActionAsync(
            () => RecoverCreationWizardCheckpointAsync(_creationWizardLifetime.Token),
            _creationWizardLifetime.Token);

    private async Task RecoverCreationWizardCheckpointAsync(CancellationToken ct)
    {
        if (_boundCreationWizardSnapshot is null || !StorageProvider.CanOpen)
            return;

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Recover Character Creation Navigation",
            AllowMultiple = false,
            FileTypeFilter = [WizardCheckpointFileType()]
        });
        IStorageFile? file = files.FirstOrDefault();
        if (file is null)
            return;

        byte[] payload;
        try
        {
            await using Stream input = await file.OpenReadAsync();
            payload = await ReadBoundedCheckpointAsync(input, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            CharacterCreationWizardControl.AppendBuildGhostAnswer(
                "System",
                "That checkpoint could not be read. Character data was not changed.");
            return;
        }

        if (payload.Length > AvaloniaCreationWizardCheckpointStore.MaximumCheckpointBytes
            || !CharacterCreationWizardDesktopSession.TryDeserializeCheckpoint(
                payload,
                out CharacterCreationWizardDesktopCheckpoint? checkpoint)
            || checkpoint is null)
        {
            CharacterCreationWizardControl.AppendBuildGhostAnswer(
                "System",
                "That checkpoint is invalid. Character data was not changed.");
            return;
        }

        CharacterCreationWizardDesktopState recovered = _creationWizardSession.Bind(
            _boundCreationWizardSnapshot,
            checkpoint,
            _adapter.State.CreationContacts);
        CharacterCreationWizardControl.SetState(recovered);
        TryPersistWizardCheckpoint();
    }

    private void CreationWizard_OnExportCheckpointRequested(object? sender, EventArgs e)
        => _ = RunCreationWizardFileActionAsync(
            () => ExportCreationWizardCheckpointAsync(_creationWizardLifetime.Token),
            _creationWizardLifetime.Token);

    private async Task ExportCreationWizardCheckpointAsync(CancellationToken ct)
    {
        if (_boundCreationWizardSnapshot is null || !StorageProvider.CanSave)
            return;

        IStorageFile? target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Character Creation Navigation",
            SuggestedFileName = "chummer-creation-navigation.json",
            FileTypeChoices = [WizardCheckpointFileType()],
            ShowOverwritePrompt = true
        });
        if (target is null)
            return;

        byte[] payload = CharacterCreationWizardDesktopSession.SerializeCheckpoint(
            _creationWizardSession.CreateCheckpoint());
        await using Stream output = await target.OpenWriteAsync();
        if (output.CanSeek)
            output.SetLength(0);
        await output.WriteAsync(payload, ct);
        await output.FlushAsync(ct);
    }

    private async Task RunCreationWizardFileActionAsync(Func<Task> action, CancellationToken ct)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            CharacterCreationWizardControl.AppendBuildGhostAnswer(
                "System",
                "The checkpoint operation failed. Character data was not changed.");
        }
    }

    private static async Task<byte[]> ReadBoundedCheckpointAsync(Stream input, CancellationToken ct)
    {
        byte[] payload = new byte[AvaloniaCreationWizardCheckpointStore.MaximumCheckpointBytes + 1];
        int count = 0;
        while (count < payload.Length)
        {
            int read = await input.ReadAsync(payload.AsMemory(count, payload.Length - count), ct);
            if (read == 0)
                break;
            count += read;
        }

        return payload[..count];
    }

    private static FilePickerFileType WizardCheckpointFileType()
        => new("Chummer Creation Navigation")
        {
            Patterns = ["*.json"],
            MimeTypes = ["application/json"]
        };
}
