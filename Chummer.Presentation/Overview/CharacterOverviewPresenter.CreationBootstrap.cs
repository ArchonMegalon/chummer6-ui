using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private Task<CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>>
        CreateCharacterBootstrapAsync(
            CharacterCreationBootstrapRequest request,
            CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        if (_characterCreationBootstrapService is null)
        {
            return Task.FromResult(
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    [CharacterCreationBootstrapBlockers.AtomicCreateUnavailable]));
        }

        try
        {
            return Task.FromResult(_characterCreationBootstrapService.Create(request));
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
            return Task.FromResult(
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    [CharacterCreationBootstrapBlockers.WorkspaceCreateFailed]));
        }
    }

    private Task<CharacterCreationBootstrapActivationAttempt>
        CreateCharacterBootstrapActivationAsync(
            CharacterCreationBootstrapRequest request,
            CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        if (_characterCreationBootstrapActivationService is null)
        {
            return Task.FromResult(
                new CharacterCreationBootstrapActivationAttempt(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    null,
                    [CharacterCreationBootstrapBlockers.ActivationProjectionUnavailable]));
        }

        try
        {
            return Task.FromResult(
                _characterCreationBootstrapActivationService.CreateActivation(request));
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
            return Task.FromResult(
                new CharacterCreationBootstrapActivationAttempt(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    null,
                    [CharacterCreationBootstrapBlockers.WorkspaceCreateFailed]));
        }
    }

    private async Task ActivateCharacterBootstrapAsync(
        CharacterCreationBootstrapActivationBundle activation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (_characterCreationBootstrapActivationService is null
            || _workspaceOverviewLifecycleCoordinator
                is not IWorkspaceOverviewCreationActivationCoordinator activationCoordinator)
        {
            await LoadAsync(activation.Receipt.WorkspaceId, ct).ConfigureAwait(false);
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOverviewLifecycleResult result = await activationCoordinator
                .ActivateCreatedAsync(
                    State,
                    activation,
                    _characterCreationBootstrapActivationService,
                    ct)
                .ConfigureAwait(false);
            if (!result.CanPublish)
            {
                return;
            }

            CaptureRecoveryPayload(result);
            Publish(result.State);
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct).ConfigureAwait(false);
            await EnsureDefaultWorkspaceSurfaceAsync(ct).ConfigureAwait(false);
            await SyncShellWorkspaceContextAsync(ct).ConfigureAwait(false);
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
}
