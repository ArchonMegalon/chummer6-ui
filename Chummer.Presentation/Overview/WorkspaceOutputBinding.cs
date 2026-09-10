using Chummer.Application.Owners;
using Chummer.Contracts.Workspaces;
using System.Text.Json.Serialization;

namespace Chummer.Presentation.Overview;

/// <summary>In-memory provenance of one actual prepared output, not a credential
/// or a durable receipt. Replacing its payload or view invalidates delivery.</summary>
public sealed class WorkspaceOutputBinding
{
    private readonly object _receipt;
    private readonly CharacterWorkspaceId _workspaceId;
    private readonly long _revision;

    internal WorkspaceOutputBinding(OwnerContextStamp? owner, CharacterWorkspaceId id,
        long revision, object receipt, CancellationToken cancellationToken)
    {
        OriginalOwner = owner;
        _workspaceId = id;
        _revision = revision;
        _receipt = receipt;
        CancellationToken = cancellationToken;
    }

    [JsonIgnore]
    public OwnerContextStamp? OriginalOwner { get; }

    [JsonIgnore]
    public CancellationToken CancellationToken { get; }

    public bool Matches(CharacterOverviewState state, object receipt)
        => !CancellationToken.IsCancellationRequested
           && ReferenceEquals(state.PendingOutputBinding, this)
           && ReferenceEquals(_receipt, receipt)
           && (state.WorkspaceId ?? state.Session.ActiveWorkspaceId) == _workspaceId
           && state.ContentRevision == _revision
           && state.DisplayOwnerContext == OriginalOwner;
}
