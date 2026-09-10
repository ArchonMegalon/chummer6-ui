using Chummer.Application.Characters;
using Chummer.Application.Owners;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private CreationDialogAuthority CaptureCreationDialogAuthority()
        => new((_client as IOwnerBoundWorkspaceProjectionClient)?.CaptureOwnerContext(),
            _client is IOwnerBoundWorkspaceMutationClient || _ownerBoundCharacterCreationBootstrapService is not null);

    private bool IsCreationOwnerCurrent(CreationDialogAuthority authority)
        => authority.Owner is { IsValid: true } owner
            ? _client is IOwnerBoundWorkspaceProjectionClient bound && bound.CaptureOwnerContext() == owner
            : !authority.RequiresOwner;

    private bool IsCreationDialogCurrent(CreationDialogAuthority? authority)
        => authority is not null && ReferenceEquals(State.ActiveDialog?.CreationAuthority, authority)
            && IsCreationOwnerCurrent(authority);

    private bool AdmitCreation(CreationDialogAuthority? authority)
        => authority is null
            ? _client is not IOwnerBoundWorkspaceMutationClient && _ownerBoundCharacterCreationBootstrapService is null
            : IsCreationDialogCurrent(authority)
                && (authority.Owner is null || _ownerBoundCharacterCreationBootstrapService is not null)
                && authority.TryStart();

    private static void RetainCreationReceipt(CreationDialogAuthority? authority, CharacterCreationBootstrapReceipt? receipt)
    {
        if (authority is null) return;
        if (receipt is not null && CharacterCreationBootstrapReceiptDigest.IsValid(receipt)) authority.Receipt = receipt;
        else if (receipt is null) authority.RejectedBeforeCommit();
    }

    private async Task<CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>>
        CreateCharacterBootstrapAsync(
            CharacterCreationBootstrapRequest request,
            CancellationToken ct,
            CreationDialogAuthority? authority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ICharacterCreationBootstrapService? service = _characterCreationBootstrapService;
        if ((service is null && _ownerBoundCharacterCreationBootstrapService is null) || !AdmitCreation(authority))
        {
            return
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    [CharacterCreationBootstrapBlockers.AtomicCreateUnavailable]);
        }

        try
        {
            var result = await RunCreationBootstrapWorkAsync(
                    () => authority?.Owner is { } owner
                        ? _ownerBoundCharacterCreationBootstrapService!.Create(owner, request)
                        : service!.Create(request),
                    ct)
                .ConfigureAwait(false);
            RetainCreationReceipt(authority, result.Value);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or FormatException
                                           or InvalidDataException
                                           or InvalidOperationException)
        {
            return
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    [CharacterCreationBootstrapBlockers.WorkspaceCreateFailed]);
        }
    }

    private async Task<CharacterCreationBootstrapActivationAttempt>
        CreateCharacterBootstrapActivationAsync(
            CharacterCreationBootstrapRequest request,
            CancellationToken ct,
            CreationDialogAuthority? authority = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ICharacterCreationBootstrapActivationService? service =
            _characterCreationBootstrapActivationService;
        if ((service is null && _ownerBoundCharacterCreationBootstrapService is null) || !AdmitCreation(authority))
        {
            return
                new CharacterCreationBootstrapActivationAttempt(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    null,
                    [CharacterCreationBootstrapBlockers.ActivationProjectionUnavailable]);
        }

        try
        {
            var result = await RunCreationBootstrapWorkAsync(
                    () => authority?.Owner is { } owner
                        ? _ownerBoundCharacterCreationBootstrapService!.CreateActivation(owner, request)
                        : service!.CreateActivation(request),
                    ct)
                .ConfigureAwait(false);
            RetainCreationReceipt(authority, result.Receipt);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or FormatException
                                           or InvalidDataException
                                           or InvalidOperationException)
        {
            return
                new CharacterCreationBootstrapActivationAttempt(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    null,
                    [CharacterCreationBootstrapBlockers.WorkspaceCreateFailed]);
        }
    }

    internal static Task<T> RunCreationBootstrapWorkAsync<T>(
        Func<T> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ct.ThrowIfCancellationRequested();
        return Task.Run(operation, ct);
    }

    private async Task ActivateCharacterBootstrapAsync(
        CharacterCreationBootstrapActivationBundle activation,
        CancellationToken ct,
        CreationDialogAuthority? authority = null)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (authority is not null && !IsCreationDialogCurrent(authority)) return;
        long displayGeneration = BeginDisplayTransition();
        if ((_characterCreationBootstrapActivationService is null && _ownerBoundCharacterCreationBootstrapService is null)
            || _workspaceOverviewLifecycleCoordinator
                is not IWorkspaceOverviewCreationActivationCoordinator activationCoordinator)
        {
            await LoadCreatedWorkspaceAsync(activation.Receipt.WorkspaceId, authority, ct).ConfigureAwait(false);
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOverviewLifecycleResult result = authority?.Owner is { } owner
                ? await activationCoordinator.ActivateCreatedAsync(State, owner, activation,
                    _ownerBoundCharacterCreationBootstrapService!, ct).ConfigureAwait(false)
                : await activationCoordinator.ActivateCreatedAsync(
                    State,
                    activation,
                    _characterCreationBootstrapActivationService!,
                    ct)
                .ConfigureAwait(false);
            if (!result.CanPublish)
            {
                return;
            }

            if (!IsDisplayGenerationCurrent(displayGeneration)
                || (authority is not null && !IsCreationDialogCurrent(authority))) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(displayGeneration, result.State)) return;
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct).ConfigureAwait(false);
            await EnsureDefaultWorkspaceSurfaceAsync(ct).ConfigureAwait(false);
            await SyncShellWorkspaceContextAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (authority is not null && !IsCreationDialogCurrent(authority)) return;
            TryPublishDisplayTransition(displayGeneration, State with
            {
                IsBusy = false,
                Error = ex.Message
            });
        }
    }

    private async Task LoadCreatedWorkspaceAsync(CharacterWorkspaceId id, CreationDialogAuthority? authority, CancellationToken ct)
    {
        if (authority?.Owner is not { } owner)
        {
            if (authority?.RequiresOwner != true && _client is not IOwnerBoundWorkspaceMutationClient)
                await LoadAsync(id, ct).ConfigureAwait(false);
            return;
        }
        if (!IsCreationDialogCurrent(authority)
            || _workspaceOverviewLifecycleCoordinator is not IWorkspaceOverviewCreationActivationCoordinator lifecycle) return;
        long generation = BeginDisplayTransition();
        try
        {
            var result = await lifecycle.LoadCreatedAsync(State, owner, id, ct).ConfigureAwait(false);
            if (!result.CanPublish || result.State.DisplayOwnerContext != owner
                || !IsCreationDialogCurrent(authority) || !IsDisplayGenerationCurrent(generation)) return;
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(generation, result.State)) return;
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct).ConfigureAwait(false);
            await EnsureDefaultWorkspaceSurfaceAsync(ct).ConfigureAwait(false);
            await SyncShellWorkspaceContextAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (IsCreationDialogCurrent(authority))
                TryPublishDisplayTransition(generation, State with { IsBusy = false, Error = ex.Message });
        }
    }
}
