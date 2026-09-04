using Chummer.Application.Characters;
using Chummer.Contracts.Characters;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private async Task<CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>>
        CreateCharacterBootstrapAsync(
            CharacterCreationBootstrapRequest request,
            CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ICharacterCreationBootstrapService? service = _characterCreationBootstrapService;
        if (service is null)
        {
            return
                new CharacterCreationBootstrapResult<CharacterCreationBootstrapReceipt>(
                    CharacterCreationBootstrapOutcomes.Unavailable,
                    null,
                    [CharacterCreationBootstrapBlockers.AtomicCreateUnavailable]);
        }

        try
        {
            return await RunCreationBootstrapWorkAsync(
                    () => service.Create(request),
                    ct)
                .ConfigureAwait(false);
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
            CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        ICharacterCreationBootstrapActivationService? service =
            _characterCreationBootstrapActivationService;
        if (service is null)
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
            return await RunCreationBootstrapWorkAsync(
                    () => service.CreateActivation(request),
                    ct)
                .ConfigureAwait(false);
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
