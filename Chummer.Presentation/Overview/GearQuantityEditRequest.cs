using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public enum GearQuantityAction
{
    Increase,
    Reduce,
    Split,
    Merge
}

public sealed record GearQuantityEditRequest(
    CharacterWorkspaceId WorkspaceId,
    long ExpectedContentRevision,
    Guid GearId,
    GearQuantityAction Action,
    decimal Amount,
    Guid? MergeTargetGearId = null,
    bool ReductionConfirmed = false);
