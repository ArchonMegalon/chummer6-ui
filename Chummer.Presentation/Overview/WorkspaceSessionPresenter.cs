using Chummer.Contracts.Characters;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed class WorkspaceSessionPresenter : IWorkspaceSessionPresenter
{
    private const int MaxRecentWorkspaceCount = 24;
    private readonly IWorkspaceSessionManager _manager;

    public WorkspaceSessionPresenter(IWorkspaceSessionManager? manager = null)
    {
        _manager = manager ?? new WorkspaceSessionManager();
    }

    public WorkspaceSessionState State { get; private set; } = WorkspaceSessionState.Empty;

    public WorkspaceSessionState Restore(IReadOnlyList<WorkspaceListItem> workspaces, CharacterWorkspaceId? activeWorkspaceId = null)
    {
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = _manager.Restore(workspaces);
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
        IReadOnlyList<OpenWorkspaceState> openWorkspaces = _manager.Activate(State.OpenWorkspaces, id, profile, rulesetId);
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
        State = State with
        {
            ActiveWorkspaceId = null,
            OpenWorkspaces = []
        };
        return State;
    }

    public WorkspaceSessionState SetSavedStatus(CharacterWorkspaceId id, bool hasSavedWorkspace)
    {
        OpenWorkspaceState[] updated = State.OpenWorkspaces
            .Select(workspace => WorkspaceIdsEqual(workspace.Id, id)
                ? workspace with { HasSavedWorkspace = hasSavedWorkspace }
                : workspace)
            .ToArray();

        State = State with
        {
            OpenWorkspaces = updated
        };
        return State;
    }

    public bool Contains(CharacterWorkspaceId id)
    {
        return Contains(State.OpenWorkspaces, id);
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
