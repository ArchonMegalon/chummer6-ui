using Chummer.Contracts.Characters;
using Chummer.Contracts.Api;
using Chummer.Contracts.Workspaces;
using Chummer.Application.Workspaces;

namespace Chummer.Presentation.Overview;

public interface IWorkspaceOverviewLoader
{
    /// <summary>
    /// Loads display projections from a caller-selected client. This public
    /// compatibility surface deliberately never issues recovery authority.
    /// </summary>
    Task<WorkspaceOverviewLoadResult> LoadAsync(
        IChummerClient client,
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

/// <summary>
/// Optional client capability for projecting every overview section from one
/// immutable workspace snapshot. The loader still verifies the returned
/// snapshot against independent before/after reads.
/// </summary>
public interface IWorkspaceOverviewProjectionClient
{
    Task<CommandResult<WorkspaceOverviewProjection>> GetWorkspaceOverviewAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

internal interface IAuthoritativeWorkspaceOverviewLoader
{
    bool IsCompositionBound { get; }

    Task<WorkspaceOverviewLoadResult> LoadAuthoritativeAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);

    Task<WorkspaceRecoveryAuthoritySnapshot> LoadRecoverySnapshotAsync(
        CharacterWorkspaceId workspaceId,
        CancellationToken ct);
}

internal sealed record WorkspaceRecoveryAuthoritySnapshot(
    WorkspaceDocument Document,
    long ContentRevision,
    WorkspaceOverviewLoader.CanonicalValidationCapability Validation);

public sealed record WorkspaceOverviewLoadResult(
    CharacterProfileSection Profile,
    CharacterProgressSection Progress,
    CharacterSkillsSection Skills,
    CharacterRulesSection Rules,
    CharacterBuildSection Build,
    CharacterMovementSection Movement,
    CharacterAwakeningSection Awakening,
    long ContentRevision = 0,
    long SavedRevision = 0,
    WorkspaceDocument? Document = null)
{
    internal WorkspaceOverviewLoader.CanonicalValidationCapability? CanonicalValidation { get; init; }
}
