using System.Collections.Immutable;
using Avalonia.Controls;
using Chummer.Application.Characters;
using Avalonia.Platform.Storage;
using Chummer.Avalonia.Creation;
using Chummer.Avalonia.Controls;
using Chummer.Contracts.AI;
using Chummer.Desktop.Runtime;
using Chummer.Contracts.Characters;
using Chummer.Contracts.LifeModules;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private readonly CharacterCreationWizardDesktopSession _creationWizardSession = new();
    private readonly CancellationTokenSource _creationWizardLifetime = new();
    private AvaloniaCreationWizardCheckpointStore? _creationWizardCheckpointStore;
    private CreationAttributesAllocationWindow? _creationAttributesWindow;
    private CharacterCreationWizardSnapshot? _boundCreationWizardSnapshot;
    private CharacterCreationContactPreparedPreview? _creationContactPreparedPreview;
    private CharacterCreationFoundationPreparedPreview? _creationFoundationPreparedPreview;
    private CharacterCreationResourcesPreparedPreview? _creationResourcesPreparedPreview;
    private string? _creationFoundationSelectedMetatype;
    private (string ModuleId, string? versionId)? _creationFoundationSelectedNationality;
    private bool _creationWizardBuildGhostPreferenceEnabled;
    private bool _creationWizardAiFinishFeatureEnabled;
    private bool _creationAiFinishBusy;
    private bool _creationQualitiesBusy;
    private bool _creationAttributesBusy;
    private bool _creationSkillsBusy;
    private CreationSkillsAllocationWindow? _creationSkillsWindow;
    private readonly Dictionary<Chummer.Contracts.Workspaces.CharacterWorkspaceId, CharacterCreationQualitiesInteraction> _pendingQualitySelections = new();

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

        // Finish-with-AI ships dark (WINDOWS_HANDOFF 2026-11-02 item 2): the
        // affordance and its handler unlock only for an explicit session opt-in.
        _creationWizardAiFinishFeatureEnabled = CharacterCreationAiFinishGatePolicy.IsSessionEnabled();
        CharacterCreationWizardControl.SetFinishWithAiFeatureEnabled(_creationWizardAiFinishFeatureEnabled);

        CharacterCreationWizardControl.StepRequested += CreationWizard_OnStepRequested;
        CharacterCreationWizardControl.ContinueRequested += CreationWizard_OnContinueRequested;
        CharacterCreationWizardControl.BuildGhostQuestionSubmitted += CreationWizard_OnBuildGhostQuestionSubmitted;
        CharacterCreationWizardControl.RecoverCheckpointRequested += CreationWizard_OnRecoverCheckpointRequested;
        CharacterCreationWizardControl.ExportCheckpointRequested += CreationWizard_OnExportCheckpointRequested;
        CharacterCreationWizardControl.ContactPreviewRequested += CreationWizard_OnContactPreviewRequested;
        CharacterCreationWizardControl.ContactConfirmRequested += CreationWizard_OnContactConfirmRequested;
        CharacterCreationWizardControl.FoundationOptionClickRequested += CreationWizard_OnFoundationOptionClickRequested;
        CharacterCreationWizardControl.FoundationConfirmRequested += CreationWizard_OnFoundationConfirmRequested;
        CharacterCreationWizardControl.ResourcesOptionClickRequested += CreationWizard_OnResourcesOptionClickRequested;
        CharacterCreationWizardControl.ResourcesConfirmRequested += CreationWizard_OnResourcesConfirmRequested;
        CharacterCreationWizardControl.QualitiesOptionClickRequested += CreationWizard_OnQualitiesOptionClickRequested;
        CharacterCreationWizardControl.QualitiesEditRequested += CreationWizard_OnQualitiesEditRequested;
        CharacterCreationWizardControl.AttributesEditRequested += CreationWizard_OnAttributesEditRequested;
        CharacterCreationWizardControl.SkillsEditRequested += CreationWizard_OnSkillsEditRequested;
        CharacterCreationWizardControl.MagicResonanceOptionClickRequested += CreationWizard_OnMagicResonanceOptionClickRequested;
        CharacterCreationWizardControl.FinalizeRequested += CreationWizard_OnFinalizeRequested;
        CharacterCreationWizardControl.PriorityConfirmRequested += CreationWizard_OnPriorityConfirmRequested;
        CharacterCreationWizardControl.FinishWithAiRequested += CreationWizard_OnFinishWithAiRequested;
    }

    private void DetachCreationWizard()
    {
        _creationWizardLifetime.Cancel();
        _creationAttributesWindow?.Close();
        _creationSkillsWindow?.Close();
        _creationAttributesWindow = null;
        CharacterCreationWizardControl.StepRequested -= CreationWizard_OnStepRequested;
        CharacterCreationWizardControl.ContinueRequested -= CreationWizard_OnContinueRequested;
        CharacterCreationWizardControl.BuildGhostQuestionSubmitted -= CreationWizard_OnBuildGhostQuestionSubmitted;
        CharacterCreationWizardControl.RecoverCheckpointRequested -= CreationWizard_OnRecoverCheckpointRequested;
        CharacterCreationWizardControl.ExportCheckpointRequested -= CreationWizard_OnExportCheckpointRequested;
        CharacterCreationWizardControl.ContactPreviewRequested -= CreationWizard_OnContactPreviewRequested;
        CharacterCreationWizardControl.ContactConfirmRequested -= CreationWizard_OnContactConfirmRequested;
        CharacterCreationWizardControl.FoundationOptionClickRequested -= CreationWizard_OnFoundationOptionClickRequested;
        CharacterCreationWizardControl.FoundationConfirmRequested -= CreationWizard_OnFoundationConfirmRequested;
        CharacterCreationWizardControl.ResourcesOptionClickRequested -= CreationWizard_OnResourcesOptionClickRequested;
        CharacterCreationWizardControl.ResourcesConfirmRequested -= CreationWizard_OnResourcesConfirmRequested;
        CharacterCreationWizardControl.QualitiesOptionClickRequested -= CreationWizard_OnQualitiesOptionClickRequested;
        CharacterCreationWizardControl.QualitiesEditRequested -= CreationWizard_OnQualitiesEditRequested;
        CharacterCreationWizardControl.AttributesEditRequested -= CreationWizard_OnAttributesEditRequested;
        CharacterCreationWizardControl.SkillsEditRequested -= CreationWizard_OnSkillsEditRequested;
        CharacterCreationWizardControl.MagicResonanceOptionClickRequested -= CreationWizard_OnMagicResonanceOptionClickRequested;
        CharacterCreationWizardControl.FinalizeRequested -= CreationWizard_OnFinalizeRequested;
        CharacterCreationWizardControl.PriorityConfirmRequested -= CreationWizard_OnPriorityConfirmRequested;
        CharacterCreationWizardControl.FinishWithAiRequested -= CreationWizard_OnFinishWithAiRequested;
    }

    private void ApplyCreationWizardState(CharacterOverviewState overview)
    {
        _creationAttributesWindow?.InvalidateIfContextChanged();
        _creationSkillsWindow?.InvalidateIfContextChanged();
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
        CharacterCreationWizardControl.SetQualitiesSelection(overview.CreationQualities is { } qualities
            ? CharacterCreationQualitiesInteraction.SelectedIds(qualities) : []);
        CharacterCreationWizardControl.SetState(state);
        CharacterCreationWizardControl.SetStartingCashState(overview.CreationFinalization?.StartingCashSource);
        CharacterCreationWizardControl.SetFinalizeAvailability(
            overview.CreationFinalization is { } finalization && finalization.CanReview,
            overview.CreationFinalization?.Blockers ?? []);
        _creationWizardBuildGhostPreferenceEnabled = !overview.Preferences.DisableAiFeatures;
        CharacterCreationWizardControl.SetBuildGhostEnabled(_creationWizardBuildGhostPreferenceEnabled);
        CharacterCreationWizardControl.SetFinishWithAiAvailability(
            // Qualification gate remains separate; a live linked owner is also required.
        overview.CreationWizard is not null
            && _creationWizardBuildGhostPreferenceEnabled
            && CreationWizard_HasLinkedAiOwner(overview));
        TryPersistWizardCheckpoint();
    
        CharacterCreationWizardControl.SetPrerequisiteState(CreationWizard_LoadPrerequisiteState());
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

    private CharacterCreationFoundationInteractionLoadResult? CreationWizard_LoadFoundationState()
    {
        if (_creationFoundationInteractionPresenter is null)
            return null;
        return _creationFoundationInteractionPresenter.Load(_adapter.State);
    }

    private void CreationWizard_OnFoundationOptionClickRequested(
        object? sender,
        CharacterCreationFoundationOptionClickRequested request)
    {
        CharacterCreationFoundationInteractionLoadResult? load = CreationWizard_LoadFoundationState();
        if (load?.State is not { } foundation)
            return;

        CharacterCreationWizardDesktopOption option = request.Option;
        if (!option.IsEnabled)
            return;

        // Classify the clicked card against the projected authority lists.
        string? metatypeId = foundation.MetatypeOptions
            .FirstOrDefault(candidate => string.Equals(candidate.OptionId, option.OptionId, StringComparison.Ordinal))
            ?.OptionId;
        LifeModuleLegalOptionDto? nationality = foundation.NationalityOptions
            .FirstOrDefault(candidate => string.Equals(candidate.ModuleId, option.OptionId, StringComparison.Ordinal));
        if (metatypeId is null && nationality is null)
            return;

        if (metatypeId is not null)
            _creationFoundationSelectedMetatype = metatypeId;
        if (nationality is not null)
            _creationFoundationSelectedNationality = (nationality.ModuleId, option.VersionId);

        // Auto-prepare once both halves of the foundation selection are present.
        if (_creationFoundationSelectedMetatype is null || _creationFoundationSelectedNationality is null)
            return;

        CharacterCreationWizardControl.SetFoundationMutationBusy(true);
        try
        {
            CharacterCreationFoundationInteractionPrepareResult result =
                _creationFoundationInteractionPresenter!.Prepare(
                    _adapter.State,
                    new CharacterCreationFoundationSelectionInput(
                        _creationFoundationSelectedMetatype,
                        _creationFoundationSelectedNationality.Value.ModuleId,
                        _creationFoundationSelectedNationality.Value.versionId));
            if (result.PreparedPreview is { } prepared)
                _creationFoundationPreparedPreview = prepared;
            CharacterCreationWizardControl.SetFoundationPrepareResult(result);
        }
        finally
        {
            CharacterCreationWizardControl.SetFoundationMutationBusy(false);
        }
    }

    private async void CreationWizard_OnQualitiesOptionClickRequested(
        object? sender, CharacterCreationQualitiesOptionClickRequested request)
        => await CreationWizard_EditQualitiesAsync(request.Option.OptionId, request.Remove);

    private async void CreationWizard_OnQualitiesEditRequested(object? sender, EventArgs args)
        => await CreationWizard_EditQualitiesAsync(null, false);

    private bool IsCreationWizardWindowUsable() => IsVisible && !_creationWizardLifetime.IsCancellationRequested;

    private async Task CreationWizard_EditQualitiesAsync(string? optionId, bool remove)
    {
        if (_creationQualitiesBusy || _creationWizardLifetime.IsCancellationRequested)
            return;
        _creationQualitiesBusy = true;
        CharacterCreationWizardControl.QualitiesMutationBusy(true);
        CharacterCreationWizardControl.QualitiesStatus("Loading your selected qualities…");
        try
        {
            CharacterOverviewState overview = _adapter.State;
            if (overview.WorkspaceId is not { } workspaceId)
                return;
            bool pending = _pendingQualitySelections.TryGetValue(workspaceId, out var interaction);
            interaction ??= new CharacterCreationQualitiesInteraction(
                ResolveService<ICharacterCreationQualitiesService>(),
                App.Services?.GetService(typeof(IOwnerBoundCharacterCreationQualitiesService)) as IOwnerBoundCharacterCreationQualitiesService,
                overview);
            interaction.ValidateContext(overview);
            CharacterCreationQualitiesState qualities = pending
                ? interaction.PendingReview!.Before
                : await Task.Run(interaction.Load);
            // Revalidation gap guard: the await above yields the UI thread, so the captured
            // overview can go stale (character switch, account transition, revision bump).
            // Discard the loaded state instead of presenting it (scope item 2, 3 Nov handoff).
            if (_creationWizardLifetime.IsCancellationRequested || !IsCreationWizardWindowUsable())
            {
                CharacterCreationWizardControl.QualitiesStatus(
                    "Qualities loading was cancelled. The loaded view was discarded.");
                return;
            }
            interaction.ValidateContext(_adapter.State);
            if (pending)
                interaction.RequireReconciliationDisplayContext(_adapter.State);
            else
                interaction.RequireDisplayContext(_adapter.State);
            var dialog = new CreationQualitiesReviewWindow(interaction, qualities, () => _adapter.State,
                pending ? null : optionId, remove);
            await dialog.ShowDialog<bool>(this);
            if (interaction.OutcomeUncertain)
                _pendingQualitySelections[workspaceId] = interaction;
            else
                _pendingQualitySelections.Remove(workspaceId);
            CharacterCreationWizardControl.QualitiesStatus(interaction.Status);
            if (interaction.Confirmed && _adapter.State.WorkspaceId == workspaceId
                && _adapter.State.DisplayOwnerContext == overview.DisplayOwnerContext)
            {
                await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token);
                ApplyCreationWizardState(_adapter.State);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            CharacterCreationWizardControl.QualitiesStatus(
                "Qualities could not be loaded. Reload the character and check its prerequisites. Any pending confirmation must be checked before another change.");
        }
        finally
        {
            _creationQualitiesBusy = false;
            CharacterCreationWizardControl.QualitiesMutationBusy(false);
        }
    }

    private async void CreationWizard_OnAttributesEditRequested(object? sender, EventArgs args)
        => await CreationWizard_EditAttributesAsync();

    /// <summary>
    /// Opens the modal attributes-allocation editor. Load/Prepare/Confirm run through
    /// the Core-bound presenter; the fresh overview is re-captured after every await
    /// and the refreshed state is applied only when the workspace still matches
    /// (Chain-B display-revalidation discipline).
    /// </summary>
    private async Task CreationWizard_EditAttributesAsync()
    {
        if (_creationAttributesBusy || _creationWizardLifetime.IsCancellationRequested)
            return;
        _creationAttributesBusy = true;
        CharacterCreationWizardControl.AttributesMutationBusy(true);
        CharacterCreationWizardControl.AttributesStatus("Loading authoritative attributes…");
        try
        {
            CharacterOverviewState overview = _adapter.State;
            if (overview.WorkspaceId is not { } workspaceId)
                return;

            var presenter = new CharacterCreationAttributesInteractionPresenter(
                App.Services?.GetService(typeof(IOwnerBoundCharacterCreationAttributesService))
                    as IOwnerBoundCharacterCreationAttributesService
                ?? throw new InvalidOperationException(
                    "attributes service unavailable — cannot allocate attributes honestly"));
            CharacterCreationAttributesInteractionLoadResult loaded =
                await Task.Run(() => presenter.Load(overview));
            if (_creationWizardLifetime.IsCancellationRequested || !IsCreationWizardWindowUsable())
            {
                CharacterCreationWizardControl.AttributesStatus(
                    "Attributes loading was cancelled. The loaded view was discarded.");
                return;
            }
            if (loaded is not { Outcome: "ok", State: { } state })
            {
                CharacterCreationWizardControl.AttributesStatus(
                    "Attributes could not be loaded: " + (loaded.Error ?? loaded.Outcome));
                return;
            }
            if (!presenter.IsCurrentContext(_adapter.State))
            {
                CharacterCreationWizardControl.AttributesStatus(
                    "The workspace changed while attributes were loading. The loaded view was discarded.");
                return;
            }

            var dialog = new CreationAttributesAllocationWindow(presenter, state, () => _adapter.State);
            _creationAttributesWindow = dialog;
            await dialog.ShowDialog<bool>(this);
            _creationAttributesWindow = null;
            CharacterCreationWizardControl.AttributesStatus(dialog.Status);
            if (dialog.Confirmed
                && _adapter.State.WorkspaceId == workspaceId
                && _adapter.State.DisplayOwnerContext == overview.DisplayOwnerContext)
            {
                await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token);
                if (!_creationWizardLifetime.IsCancellationRequested && IsCreationWizardWindowUsable()
                    && _adapter.State.WorkspaceId == workspaceId
                    && _adapter.State.DisplayOwnerContext == overview.DisplayOwnerContext)
                    ApplyCreationWizardState(_adapter.State);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            CharacterCreationWizardControl.AttributesStatus(
                "Attributes could not be loaded. Reload the character and check its prerequisites.");
        }
        finally
        {
            _creationAttributesBusy = false;
            _creationAttributesWindow?.Close();
            _creationAttributesWindow = null;
            CharacterCreationWizardControl.AttributesMutationBusy(false);
        }
    }

    private async void CreationWizard_OnSkillsEditRequested(object? sender, EventArgs args)
        => await CreationWizard_EditSkillsAsync();

    /// <summary>
    /// Opens the modal skills-allocation editor. Load/Prepare/Confirm run through
    /// the Core-bound presenter; the fresh overview is re-captured after every await
    /// and the refreshed state is applied only when the workspace still matches
    /// (Chain-B display-revalidation discipline).
    /// </summary>
    private async Task CreationWizard_EditSkillsAsync()
    {
        if (_creationSkillsBusy || _creationWizardLifetime.IsCancellationRequested)
            return;
        _creationSkillsBusy = true;
        CharacterCreationWizardControl.SkillsMutationBusy(true);
        CharacterCreationWizardControl.SkillsStatus("Loading authoritative skills…");
        try
        {
            CharacterOverviewState overview = _adapter.State;
            if (overview.WorkspaceId is not { } workspaceId)
                return;

            var presenter = new CharacterCreationSkillsInteractionPresenter(
                App.Services?.GetService(typeof(IOwnerBoundCharacterCreationSkillsService))
                    as IOwnerBoundCharacterCreationSkillsService
                ?? throw new InvalidOperationException(
                    "skills service unavailable — cannot allocate skills honestly"));
            CharacterCreationSkillsInteractionLoadResult loaded =
                await Task.Run(() => presenter.Load(overview));
            if (_creationWizardLifetime.IsCancellationRequested || !IsCreationWizardWindowUsable())
            {
                CharacterCreationWizardControl.SkillsStatus(
                    "Skills loading was cancelled. The loaded view was discarded.");
                return;
            }
            if (loaded is not { Outcome: "ok", State: { } state })
            {
                CharacterCreationWizardControl.SkillsStatus(
                    "Skills could not be loaded: " + (loaded.Error ?? loaded.Outcome));
                return;
            }
            if (!presenter.IsCurrentContext(_adapter.State))
            {
                CharacterCreationWizardControl.SkillsStatus(
                    "The workspace changed while skills were loading. The loaded view was discarded.");
                return;
            }

            var dialog = new CreationSkillsAllocationWindow(presenter, state, () => _adapter.State);
            _creationSkillsWindow = dialog;
            await dialog.ShowDialog<bool>(this);
            _creationSkillsWindow = null;
            CharacterCreationWizardControl.SkillsStatus(dialog.Status);
            if (dialog.Confirmed
                && _adapter.State.WorkspaceId == workspaceId
                && _adapter.State.DisplayOwnerContext == overview.DisplayOwnerContext)
            {
                await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token);
                if (!_creationWizardLifetime.IsCancellationRequested && IsCreationWizardWindowUsable()
                    && _adapter.State.WorkspaceId == workspaceId
                    && _adapter.State.DisplayOwnerContext == overview.DisplayOwnerContext)
                    ApplyCreationWizardState(_adapter.State);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            CharacterCreationWizardControl.SkillsStatus(
                "Skills could not be loaded. Reload the character and check its prerequisites.");
        }
        finally
        {
            _creationSkillsBusy = false;
            _creationSkillsWindow?.Close();
            _creationSkillsWindow = null;
            CharacterCreationWizardControl.SkillsMutationBusy(false);
        }
    }

    private void CreationWizard_OnMagicResonanceOptionClickRequested(
        object? sender, CharacterCreationMagicResonanceOptionClickRequested request)
        => CharacterCreationWizardControl.QualitiesStatus(
            "Magic/Resonance editing is not available in this Windows build yet.");

    private async void CreationWizard_OnFoundationConfirmRequested(
        object? sender,
        CharacterCreationFoundationConfirmRequested request)
    {
        if (_creationFoundationInteractionPresenter is null
            || _creationFoundationPreparedPreview is not { } prepared
            || !string.Equals(request.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal))
        {
            return;
        }

        CharacterCreationWizardControl.SetFoundationMutationBusy(true);
        try
        {
            CharacterCreationFoundationInteractionConfirmResult result =
                _creationFoundationInteractionPresenter.Confirm(
                    _adapter.State,
                    new CharacterCreationFoundationConfirmation(
                        prepared,
                        prepared.PreviewDigest,
                        ExplicitlyConfirmed: true));
            CharacterCreationWizardControl.SetFoundationConfirmResult(result);
            if (result.Receipt is not null)
            {
                _creationFoundationPreparedPreview = null;
                _creationFoundationSelectedMetatype = null;
                _creationFoundationSelectedNationality = null;
                if (_adapter.State.WorkspaceId is { } workspaceId)
                {
                    await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token).ConfigureAwait(true);
                    ApplyCreationWizardState(_adapter.State);
                }
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Authority refresh on the next state application will reconcile.
        }
        finally
        {
            CharacterCreationWizardControl.SetFoundationMutationBusy(false);
        }
    }

    private void CreationWizard_OnResourcesOptionClickRequested(
        object? sender,
        CharacterCreationResourcesOptionClickRequested request)
    {
        if (_creationResourcesInteractionPresenter is null)
            return;
        CharacterCreationWizardDesktopOption option = request.Option;
        if (!option.IsEnabled)
            return;

        CharacterCreationWizardControl.SetResourcesMutationBusy(true);
        try
        {
            CharacterCreationResourcesInteractionPrepareResult result =
                _creationResourcesInteractionPresenter.Prepare(
                    _adapter.State,
                    option.OptionId);
            if (result.PreparedPreview is { } prepared)
                _creationResourcesPreparedPreview = prepared;
            CharacterCreationWizardControl.SetResourcesPrepareResult(result);
        }
        finally
        {
            CharacterCreationWizardControl.SetResourcesMutationBusy(false);
        }
    }

    private async void CreationWizard_OnResourcesConfirmRequested(
        object? sender,
        CharacterCreationResourcesConfirmRequested request)
    {
        if (_creationResourcesInteractionPresenter is null
            || _creationResourcesPreparedPreview is not { } prepared
            || !string.Equals(request.PreviewDigest, prepared.PreviewDigest, StringComparison.Ordinal))
        {
            return;
        }

        CharacterCreationWizardControl.SetResourcesMutationBusy(true);
        try
        {
            CharacterCreationResourcesInteractionConfirmResult result =
                _creationResourcesInteractionPresenter.Confirm(
                    _adapter.State,
                    new CharacterCreationResourcesConfirmation(
                        prepared,
                        prepared.PreviewDigest,
                        prepared.IdempotencyKey,
                        ExplicitlyConfirmed: true));
            CharacterCreationWizardControl.SetResourcesConfirmResult(result);
            if (result.Receipt is not null)
            {
                _creationResourcesPreparedPreview = null;
                if (_adapter.State.WorkspaceId is { } workspaceId)
                {
                    await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token).ConfigureAwait(true);
                    ApplyCreationWizardState(_adapter.State);
                }
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Authority refresh on the next state application will reconcile.
        }
        finally
        {
            CharacterCreationWizardControl.SetResourcesMutationBusy(false);
        }
    }

    private async void CreationWizard_OnFinalizeRequested(object? sender, EventArgs e)
    {
        if (_creationFinalizationService is null
            || _adapter.State.WorkspaceId is not { } workspaceId
            || _adapter.State.CreationFinalization is not { } finalizationState
            || !finalizationState.CanReview)
        {
            return;
        }

        CharacterCreationWizardControl.SetFinalizeMutationBusy(true);
        try
        {
            CharacterCreationFinalizationResult<CharacterCreationFinalizationReview> review =
                _creationFinalizationService.Review(
                    new CharacterCreationFinalizationReviewRequest(finalizationState.Binding)
                {
                    StartingCash = CharacterCreationWizardControl.GetStartingCashChoice(),
                });
            if (!review.Success || review.Value is not { } reviewValue)
            {
                CharacterCreationWizardControl.SetFinalizeResult(review.Outcome, review.Blockers);
                return;
            }

            if (!reviewValue.CanConfirm)
            {
                CharacterCreationWizardControl.SetFinalizeResult("blocked", reviewValue.Blockers);
                return;
            }

            string idempotencyKey = $"win002-finalize:{workspaceId:D}:{reviewValue.PreviewDigest}";
            CharacterCreationFinalizationResult<CharacterCreationFinalizationReceipt> confirm =
                _creationFinalizationService.Confirm(
                    new CharacterCreationFinalizationConfirmRequest(
                        reviewValue.Binding,
                        reviewValue.PreviewDigest,
                        reviewValue.Plan?.PlanDigest
                            ?? throw new InvalidOperationException(
                                "Whole-build review returned no plan digest for a confirmable finalization."),
                        idempotencyKey,
                        ExplicitlyConfirmed: true)
                    {
                        StartingCash = CharacterCreationWizardControl.GetStartingCashChoice()
                    });
            if (confirm.Success)
            {
                CharacterCreationWizardControl.SetFinalizeResult(confirm.Outcome, confirm.Blockers);
                await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token).ConfigureAwait(true);
                ApplyCreationWizardState(_adapter.State);
            }
            else
            {
                CharacterCreationWizardControl.SetFinalizeResult(confirm.Outcome, confirm.Blockers);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            CharacterCreationWizardControl.SetFinalizeResult("invalid", [ex.Message]);
        }
        finally
        {
            CharacterCreationWizardControl.SetFinalizeMutationBusy(false);
        }
    }
    private bool CreationWizard_HasLinkedAiOwner(CharacterOverviewState state) =>
        DesktopInstallLinkingRuntime.IsClaimed(_installLinkingState)
        && state.DisplayOwnerContext is { } owner
        && owner.Owner == DesktopInstallLinkingRuntime.ResolveOwnerScope(_installLinkingState)
        && _aiFinishService?.IsCurrentOwner(owner) == true;

    private async void CreationWizard_OnFinishWithAiRequested(object? sender, EventArgs e)
    {
        CharacterOverviewState original = _adapter.State;
        if (_aiFinishService is null || _creationAiFinishBusy
            || !_creationWizardAiFinishFeatureEnabled || !_creationWizardBuildGhostPreferenceEnabled
            || !CreationWizard_HasLinkedAiOwner(original)
            || original.DisplayOwnerContext is not { } owner
            || original.WorkspaceId is not { } workspaceId) return;

        bool Current(bool revision = true) => !_creationWizardLifetime.IsCancellationRequested
            && IsCreationWizardWindowUsable() && CreationWizard_HasLinkedAiOwner(_adapter.State)
            && _adapter.State.DisplayOwnerContext == owner
            && _adapter.State.WorkspaceId == workspaceId
            && (!revision || (_adapter.State.ContentRevision == original.ContentRevision
                && _adapter.State.SavedRevision == original.SavedRevision));
        void ShowResults(IReadOnlyList<CreationAiFinishService.AiFinishDomainResult> results, bool recovered)
        {
            if (!Current(false)) return;
            bool success = results.Count > 0 && results.All(row => row.Success);
            CharacterCreationWizardControl.SetFinishWithAiResult(success,
                (recovered ? "Saved AI operation (read-only inspection): " : "AI completion: ")
                + string.Join("; ", results.Select(row => row.Domain + ": "
                    + (row.Success ? "confirmed" : string.Join(" ", row.Blockers)))));
        }

        _creationAiFinishBusy = true;
        CharacterCreationWizardControl.SetFinishWithAiBusy(true);
        try
        {
            var saved = await Task.Run(() => _aiFinishService.InspectSavedResult(workspaceId.Value, owner),
                _creationWizardLifetime.Token);
            if (!Current()) return;
            if (saved is not null)
            {
                ShowResults(saved, true);
                // Read-only recovery surface (re-audit 2026-11-05): the pinned
                // Core has no draft-completion receipt/replay contract, so an
                // unobserved stage's outcome is genuinely unknown and is NEVER
                // re-dispatched. Typed classification, no message matching:
                // OutcomeUnknown names the uncertain stage so the player can
                // inspect it manually; Completed/Failed/Unrecoverable stay
                // silent because InspectSavedResult already surfaced them.
                var (recoveryState, uncertainDomain) = await Task.Run(
                    () => _aiFinishService.ClassifyRecoveryOutcome(workspaceId.Value, owner),
                    _creationWizardLifetime.Token);
                if (Current() && recoveryState == CreationAiFinishRunStore.RecoveryState.OutcomeUnknown
                    && uncertainDomain is not null)
                {
                    var explain = new CreationAiFinishResumeWindow(uncertainDomain);
                    await explain.ShowDialog<bool>(this);
                    if (!Current()) return;
                }
                return;
            }
            var proposal = await Task.Run(() => _aiFinishService.BuildProposal(workspaceId.Value),
                _creationWizardLifetime.Token);
            if (!Current() || proposal.ExpectedOwner != owner) return;
            if (!proposal.NeedsChanges)
            {
                CharacterCreationWizardControl.SetFinishWithAiResult(true,
                    "No additional supported recommendations. Review any remaining manual steps.");
                return;
            }
            var dialog = new CreationAiFinishReviewWindow(proposal);
            void ContextChanged(object? sender, EventArgs args) { if (!Current()) dialog.Close(false); }
            _adapter.Updated += ContextChanged;
            bool approved;
            try { approved = await dialog.ShowDialog<bool>(this); }
            finally { _adapter.Updated -= ContextChanged; }
            if (!approved || !Current()) return;
            var results = await Task.Run(() => _aiFinishService.Apply(workspaceId.Value, proposal),
                _creationWizardLifetime.Token);
            ShowResults(results, false);
            if (Current(false))
            {
                await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token);
                if (Current(false)) ApplyCreationWizardState(_adapter.State);
            }
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested) { }
        catch (CreationAiFinishService.CreationAiFinishException ex)
        {
            if (Current(false)) CharacterCreationWizardControl.SetFinishWithAiResult(false, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            if (Current(false)) CharacterCreationWizardControl.SetFinishWithAiResult(false,
                "AI completion could not finish. Reload the character to inspect the saved outcome. No automatic retry was sent.");
        }
        finally
        {
            _creationAiFinishBusy = false;
            CharacterCreationWizardControl.SetFinishWithAiBusy(false);
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
    private CharacterCreationPrerequisiteState? CreationWizard_LoadPrerequisiteState()
    {
        if (_creationPrerequisiteService is null
            || _adapter.State.WorkspaceId is not { } workspaceId)
        {
            return null;
        }

        try
        {
            CharacterCreationFoundationResult<CharacterCreationPrerequisiteState> result =
                _creationPrerequisiteService.Load(new CharacterCreationPrerequisiteLoadRequest(workspaceId));
            return result.Value;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            return null;
        }
    }

    private bool _priorityMutationBusy;

    private async void CreationWizard_OnPriorityConfirmRequested(object? sender, EventArgs e)
    {
        if (_priorityMutationBusy
            || sender is not CharacterCreationWizardControl wizard
            || _creationPrerequisiteService is null
            || _adapter.State.WorkspaceId is not { } workspaceId)
        {
            return;
        }

        IReadOnlyDictionary<string, string> selections = wizard.GetPrioritySelections();
        _priorityMutationBusy = true;
        wizard.ReportPriorityMutationStatus("guard passed; selections=" + string.Join(",", selections.Select(kv => kv.Key + "=" + kv.Value).OrderBy(kv => kv, StringComparer.Ordinal)));
        wizard.SetPriorityMutationBusy(true);
        try
        {
            CharacterCreationPrerequisiteState? state = CreationWizard_LoadPrerequisiteState();
            if (state is null)
            {
                wizard.ReportPriorityMutationStatus("exited: prerequisite load returned no state");
                return;
            }

            var binding = state.Binding;
            CharacterCreationFoundationResult<CharacterCreationPrerequisitePreview> preview =
                _creationPrerequisiteService.Preview(
                    new CharacterCreationPrerequisitePreviewRequest(binding, selections)
                    {
                        HeritageSelectionId = wizard.GetPriorityHeritageSelectionId(),
                        TalentSelectionId = wizard.GetPriorityTalentSelectionId(),
                    });
            if (preview.Value is not { CanConfirm: true } previewState)
            {
                wizard.ReportPriorityMutationStatus(
                    "exited: preview cannot confirm; resultBlockers=["
                    + string.Join(", ", preview.Blockers) + "]");
                return;
            }

            CharacterCreationFoundationResult<CharacterCreationPrerequisiteReceipt> confirm =
                _creationPrerequisiteService.Confirm(
                    new CharacterCreationPrerequisiteConfirmRequest(
                        binding,
                        selections,
                        previewState.PreviewDigest,
                        ExplicitlyConfirmed: true)
                    {
                        HeritageSelectionId = wizard.GetPriorityHeritageSelectionId(),
                        TalentSelectionId = wizard.GetPriorityTalentSelectionId(),
                    });
            // Rebind the authoritative prerequisite state (PendingDraft) into the
            // control: SetState alone does not refresh the Priority surface.
            wizard.ReportPriorityMutationStatus(
                "confirm accepted; resultBlockers=[" + string.Join(", ", confirm.Blockers) + "]");
            await _adapter.LoadAsync(workspaceId, _creationWizardLifetime.Token).ConfigureAwait(true);
            ApplyCreationWizardState(_adapter.State);
            wizard.ReportPriorityMutationStatus("rebound authoritative state after confirm");
        }
        catch (OperationCanceledException) when (_creationWizardLifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            wizard.ReportPriorityMutationStatus("exited: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _priorityMutationBusy = false;
            CharacterCreationWizardControl.SetPriorityMutationBusy(false);
        }
    }

}
