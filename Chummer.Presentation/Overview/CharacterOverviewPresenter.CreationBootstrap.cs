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
}
