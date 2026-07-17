using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSessionPresenter : IWorkspaceSessionPresenter
{
    private const int MaxRecentWorkspaceCount = 24;
    private readonly IWorkspaceSessionManager _manager;
    private readonly Dictionary<string, OpenWorkspaceState> _closedWorkspaceCache = new(StringComparer.Ordinal);

    public WorkspaceSessionPresenter(IWorkspaceSessionManager? manager = null)
    {
        _manager = manager ?? new WorkspaceSessionManager();
    }

    public WorkspaceSessionState State { get; private set; } = WorkspaceSessionState.Empty;

    public WorkspaceSessionState Restore(IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null)
    {
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = _manager.Restore(workspaces)
            .Select(MergeRetainedState)
            .ToArray();
        foreach (OpenWorkspaceState workspace in openWorkspaces)
            _closedWorkspaceCache.Remove(workspace.Id.Value);

        CharacterWorkspaceId? activeWorkspace = ResolveActiveWorkspaceId(activeWorkspaceId, openWorkspaces);

        State = new WorkspaceSessionState(
            ActiveWorkspaceId: activeWorkspace,
            OpenWorkspaces: openWorkspaces,
            RecentWorkspaceIds: BuildRecentList(openWorkspaces.Select(workspace => workspace.Id), State.RecentWorkspaceIds));
        return State;
    }

    public WorkspaceSessionState Open(CharacterWorkspaceId id, CharacterProfileSection? profile)
    {
        return Open(id, profile, rulesetId: null);
    }

    public WorkspaceSessionState Open(CharacterWorkspaceId id, CharacterProfileSection? profile, string? rulesetId)
    {
        IReadOnlyList<OpenWorkspaceState> activationSource = State.OpenWorkspaces;
        if (!Contains(activationSource, id) && _closedWorkspaceCache.TryGetValue(id.Value, out OpenWorkspaceState? retainedWorkspace))
            activationSource = activationSource.Append(retainedWorkspace).ToArray();

        IReadOnlyList<OpenWorkspaceState> openWorkspaces = _manager.Activate(activationSource, id, profile, rulesetId);
        _closedWorkspaceCache.Remove(id.Value);
        State = State with
        {
            ActiveWorkspaceId = id,
            OpenWorkspaces = openWorkspaces,
            RecentWorkspaceIds = TouchRecent(State.RecentWorkspaceIds, id)
        };
        return State;
    }

    public WorkspaceSessionState Switch(CharacterWorkspaceId id)
    {
        if (!Contains(id))
            return State;

        State = State with
        {
            ActiveWorkspaceId = id,
            RecentWorkspaceIds = TouchRecent(State.RecentWorkspaceIds, id)
        };
        return State;
    }

    public WorkspaceSessionState ClearActive()
    {
        State = State with
        {
            ActiveWorkspaceId = null
        };
        return State;
    }

    public WorkspaceSessionState Close(CharacterWorkspaceId id)
    {
        OpenWorkspaceState? closingWorkspace = State.FindWorkspace(id);
        if (closingWorkspace is not null)
            CacheClosedWorkspace(closingWorkspace);

        return RemoveOpenWorkspace(id);
    }

    public WorkspaceSessionState Forget(CharacterWorkspaceId id)
    {
        _closedWorkspaceCache.Remove(id.Value);
        RemoveOpenWorkspace(id);
        State = State with
        {
            RecentWorkspaceIds = State.RecentWorkspaceIds
                .Where(workspaceId => !WorkspaceIdsEqual(workspaceId, id))
                .ToArray()
        };
        return State;
    }

    private WorkspaceSessionState RemoveOpenWorkspace(CharacterWorkspaceId id)
    {
        bool closedActiveWorkspace = State.ActiveWorkspaceId is { } activeWorkspace
            && WorkspaceIdsEqual(activeWorkspace, id);
        IReadOnlyList<OpenWorkspaceState> remaining = _manager.Close(State.OpenWorkspaces, id);
        CharacterWorkspaceId? nextActiveWorkspace = State.ActiveWorkspaceId;

        if (closedActiveWorkspace)
        {
            nextActiveWorkspace = SelectMostRecentOpenWorkspace(remaining, State.RecentWorkspaceIds, id)
                ?? _manager.SelectNext(remaining);
        }
        else if (nextActiveWorkspace is { } existingActive && !Contains(remaining, existingActive))
        {
            nextActiveWorkspace = SelectMostRecentOpenWorkspace(remaining, State.RecentWorkspaceIds, id)
                ?? _manager.SelectNext(remaining);
        }

        State = State with
        {
            ActiveWorkspaceId = nextActiveWorkspace,
            OpenWorkspaces = remaining,
            RecentWorkspaceIds = TrimRecent(State.RecentWorkspaceIds)
        };
        return State;
    }

    public WorkspaceSessionState CloseAll()
    {
        foreach (OpenWorkspaceState workspace in State.OpenWorkspaces)
            CacheClosedWorkspace(workspace);

        State = State with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = []
        };
        return State;
    }

    public WorkspaceSessionState SetRevisions(
        CharacterWorkspaceId id,
        long contentRevision,
        long savedRevision,
        bool clearConflict = true)
    {
        ValidateRevisions(contentRevision, savedRevision);
        return UpdateWorkspace(
            id,
            workspace => workspace with
            {
                ContentRevision = contentRevision,
                SavedRevision = savedRevision,
                ConflictState = clearConflict ? null : workspace.ConflictState
            });
    }

    public WorkspaceSessionState SetConflictState(CharacterWorkspaceId id, WorkspaceConflictState? conflictState)
    {
        return UpdateWorkspace(id, workspace => workspace with { ConflictState = conflictState });
    }

    [Obsolete("Use SetRevisions. HasSavedWorkspace is derived from SavedRevision.")]
    public WorkspaceSessionState SetSavedStatus(CharacterWorkspaceId id, bool hasSavedWorkspace)
    {
        return UpdateWorkspace(
            id,
            workspace =>
            {
                long contentRevision = Math.Max(workspace.ContentRevision, 1);
                return workspace with
                {
                    ContentRevision = contentRevision,
                    SavedRevision = hasSavedWorkspace ? contentRevision : 0,
                    ConflictState = null
                };
            });
    }

    public bool Contains(CharacterWorkspaceId id)
    {
        return Contains(State.OpenWorkspaces, id);
    }

    private OpenWorkspaceState MergeRetainedState(OpenWorkspaceState restoredWorkspace)
    {
        OpenWorkspaceState? retainedWorkspace = State.FindWorkspace(restoredWorkspace.Id);
        if (retainedWorkspace is null)
            _closedWorkspaceCache.TryGetValue(restoredWorkspace.Id.Value, out retainedWorkspace);
        if (retainedWorkspace is null)
            return restoredWorkspace;

        bool restoredRevisionIsKnown = restoredWorkspace.ContentRevision > 0 || restoredWorkspace.SavedRevision > 0;
        return restoredWorkspace with
        {
            ContentRevision = restoredRevisionIsKnown
                ? restoredWorkspace.ContentRevision
                : retainedWorkspace.ContentRevision,
            SavedRevision = restoredRevisionIsKnown
                ? restoredWorkspace.SavedRevision
                : retainedWorkspace.SavedRevision,
            ConflictState = retainedWorkspace.ConflictState
        };
    }

    private WorkspaceSessionState UpdateWorkspace(
        CharacterWorkspaceId id,
        Func<OpenWorkspaceState, OpenWorkspaceState> update)
    {
        OpenWorkspaceState[] updated = State.OpenWorkspaces
            .Select(workspace => WorkspaceIdsEqual(workspace.Id, id) ? update(workspace) : workspace)
            .ToArray();

        if (_closedWorkspaceCache.TryGetValue(id.Value, out OpenWorkspaceState? retainedWorkspace))
            _closedWorkspaceCache[id.Value] = update(retainedWorkspace);

        State = State with { OpenWorkspaces = updated };
        return State;
    }

    private void CacheClosedWorkspace(OpenWorkspaceState workspace)
    {
        _closedWorkspaceCache[workspace.Id.Value] = workspace;
        if (_closedWorkspaceCache.Count <= MaxRecentWorkspaceCount)
            return;

        OpenWorkspaceState oldestWorkspace = _closedWorkspaceCache.Values
            .OrderBy(candidate => candidate.LastOpenedUtc)
            .First();
        _closedWorkspaceCache.Remove(oldestWorkspace.Id.Value);
    }

    private static void ValidateRevisions(long contentRevision, long savedRevision)
    {
        if (contentRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(contentRevision), "Content revision cannot be negative.");
        if (savedRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(savedRevision), "Saved revision cannot be negative.");
        if (savedRevision > contentRevision)
            throw new ArgumentException("Saved revision cannot be newer than content revision.", nameof(savedRevision));
    }

    private static IReadOnlyList<CharacterWorkspaceId> TouchRecent(
        IReadOnlyList<CharacterWorkspaceId> existing,
        CharacterWorkspaceId activeWorkspaceId)
    {
        List<CharacterWorkspaceId> recentWorkspaces = new(capacity: Math.Min(existing.Count + 1, MaxRecentWorkspaceCount))
        {
            activeWorkspaceId
        };

        foreach (CharacterWorkspaceId workspaceId in existing)
        {
            if (WorkspaceIdsEqual(workspaceId, activeWorkspaceId))
                continue;

            recentWorkspaces.Add(workspaceId);
            if (recentWorkspaces.Count >= MaxRecentWorkspaceCount)
                break;
        }

        return recentWorkspaces;
    }

    private static IReadOnlyList<CharacterWorkspaceId> BuildRecentList(
        IEnumerable<CharacterWorkspaceId> openWorkspaceIds,
        IReadOnlyList<CharacterWorkspaceId> existingRecent)
    {
        List<CharacterWorkspaceId> recentWorkspaces = new();

        foreach (CharacterWorkspaceId workspaceId in openWorkspaceIds)
        {
            if (recentWorkspaces.Any(existing => WorkspaceIdsEqual(existing, workspaceId)))
                continue;

            recentWorkspaces.Add(workspaceId);
            if (recentWorkspaces.Count >= MaxRecentWorkspaceCount)
                return recentWorkspaces;
        }

        foreach (CharacterWorkspaceId workspaceId in existingRecent)
        {
            if (recentWorkspaces.Any(existing => WorkspaceIdsEqual(existing, workspaceId)))
                continue;

            recentWorkspaces.Add(workspaceId);
            if (recentWorkspaces.Count >= MaxRecentWorkspaceCount)
                break;
        }

        return recentWorkspaces;
    }

    private static IReadOnlyList<CharacterWorkspaceId> TrimRecent(IReadOnlyList<CharacterWorkspaceId> recentWorkspaceIds)
    {
        if (recentWorkspaceIds.Count <= MaxRecentWorkspaceCount)
            return recentWorkspaceIds;

        return recentWorkspaceIds.Take(MaxRecentWorkspaceCount).ToArray();
    }

    private static CharacterWorkspaceId? SelectMostRecentOpenWorkspace(
        IReadOnlyList<OpenWorkspaceState> openWorkspaces,
        IReadOnlyList<CharacterWorkspaceId> recentWorkspaceIds,
        CharacterWorkspaceId closedWorkspaceId)
    {
        foreach (CharacterWorkspaceId recentWorkspace in recentWorkspaceIds)
        {
            if (WorkspaceIdsEqual(recentWorkspace, closedWorkspaceId))
                continue;

            if (Contains(openWorkspaces, recentWorkspace))
                return recentWorkspace;
        }

        return null;
    }

    private static CharacterWorkspaceId? ResolveActiveWorkspaceId(
        CharacterWorkspaceId? requestedActiveWorkspaceId,
        IReadOnlyList<OpenWorkspaceState> openWorkspaces)
    {
        if (requestedActiveWorkspaceId is not null && Contains(openWorkspaces, requestedActiveWorkspaceId.Value))
        {
            return requestedActiveWorkspaceId;
        }

        return openWorkspaces.Count == 0
            ? null
            : openWorkspaces[0].Id;
    }

    private static bool Contains(IReadOnlyList<OpenWorkspaceState> openWorkspaces, CharacterWorkspaceId id)
    {
        return openWorkspaces.Any(workspace => WorkspaceIdsEqual(workspace.Id, id));
    }

    private static bool WorkspaceIdsEqual(CharacterWorkspaceId left, CharacterWorkspaceId right)
    {
        return string.Equals(left.Value, right.Value, StringComparison.Ordinal);
    }
}
