using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using Chummer.Application.Workspaces;
using Chummer.Contracts.Characters;
using System.Text.Json.Nodes;

namespace Chummer.Presentation;

/// <summary>
/// Local capability for carrying the original live owner authority across UI and
/// queue waits. A captured stamp is not a lease, credential or commit receipt.
/// Implementations must acquire the matching live lease after queue admission.
/// </summary>
public interface IOwnerBoundWorkspaceMutationClient
{
    OwnerContextStamp CaptureOwnerContext();

    Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
        OwnerContextStamp expectedOwner,
        CharacterWorkspaceId id,
        CancellationToken ct);

    /// <param name="onDispatch">
    /// Trusted synchronous local notification, invoked under the exact live lease
    /// immediately before the Core call. It may only record dispatch state; it
    /// must not reenter owner transitions, perform I/O, await or acquire UI locks.
    /// Once entered, even an exception is conservatively a dispatched/unknown
    /// outcome. The notification proves neither success nor historical commit.
    /// </param>
    Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(
        OwnerContextStamp expectedOwner,
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document,
        Action onDispatch,
        CancellationToken ct);
}

/// <summary>
/// Local read capability. Each projection is materialized under the original
/// live owner lease; no lease is held across an await. Callers also bind the
/// before/after canonical snapshot and revisions before publishing display data.
/// </summary>
public interface IOwnerBoundWorkspaceProjectionClient : IOwnerBoundWorkspaceMutationClient
{
    Task<CommandResult<WorkspaceOverviewProjection>> GetWorkspaceOverviewAsync(
        OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct);

    Task<JsonNode> GetSectionAsync(
        OwnerContextStamp expectedOwner, CharacterWorkspaceId id, string sectionId, CancellationToken ct);

    Task<CharacterFileSummary> GetSummaryAsync(
        OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterValidationResult> ValidateAsync(
        OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct);
}

/// <summary>Joined local dispatch observation, never a persisted operation receipt.</summary>
public enum OwnerBoundWorkspaceMutationDispatch
{
    NotDispatched,
    Dispatched
}

/// <summary>
/// Optional local presenter capability. Callers capture authority before their
/// own picker, confirmation or durable-intent waits and pass that original stamp.
/// Remote clients must not fabricate this local authority capability.
/// </summary>
public interface IOwnerBoundWorkspaceMutationPresenter
{
    Task<CommandResult<WorkspaceRevisionReceipt>> ApplyCareerReputationEditAsync(
        CareerReputationEditRequest request, OwnerContextStamp expectedOwner, CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(false, null,
            "Original-account reputation mutation is unavailable.", WorkspaceOperationOutcome.Unavailable));

    Task<CommandResult<WorkspaceRevisionReceipt>> ApplyBurnStreetCredAsync(
        BurnStreetCredRequest request, OwnerContextStamp expectedOwner, CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(false, null,
            "Original-account Street Cred mutation is unavailable.", WorkspaceOperationOutcome.Unavailable));

    Task<OwnerBoundWorkspaceMutationDispatch> ApplyCollectionMutationAsync(
        WorkspaceCollectionMutationRequest request,
        OwnerContextStamp expectedOwner,
        CancellationToken ct);
}

/// <summary>Reload an existing gesture's workspace without recapturing another account.</summary>
public interface IOwnerBoundWorkspaceRefreshPresenter
{
    Task LoadAsync(OwnerContextStamp expectedOwner, CharacterWorkspaceId id, CancellationToken ct);
}
