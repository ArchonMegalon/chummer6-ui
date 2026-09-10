using Chummer.Application.Owners;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Rulesets;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter : IOwnerBoundWorkspaceContinuationPresenter
{
    public async Task<bool> ActivateContinuationAsync(OwnerContextStamp originalOwner,
        WorkspaceContinuationExport expected, WorkspaceContinuationRestoreReceipt? restoreReceipt,
        CancellationToken ct)
    {
        if (expected?.Snapshot?.Workspace?.Document is null
            || string.IsNullOrWhiteSpace(expected.Snapshot.Workspace.Id.Value)
            || string.IsNullOrWhiteSpace(expected.SnapshotDigest)
            || !IsContinuationOwnerCurrent(originalOwner)
            || _client is not IOwnerBoundWorkspaceContinuationClient continuation
            || _workspaceOverviewLifecycleCoordinator is not IWorkspaceOverviewContinuationActivationCoordinator lifecycle
            || expected.Snapshot.OwnerId != originalOwner.Owner.NormalizedValue
            || State.IsDirty || State.ActiveWorkspace?.ConflictState is not null)
            return false;

        using PresenterOperationLease operation = EnterPresenterOperation(ct);
        ct = operation.Token;
        long generation = BeginDisplayTransition();
        CharacterWorkspaceId id = expected.Snapshot.Workspace.Id;
        try
        {
            // A supplied receipt is not authority until the actual Core store
            // recovers it. Recovery is lookup-only, never a repeated write.
            if (restoreReceipt is not null)
            {
                var recovered = await continuation.RecoverContinuationAsync(originalOwner, id,
                    restoreReceipt.OperationId, restoreReceipt.AdmissionDigest, ct).ConfigureAwait(false);
                if (recovered.Outcome != WorkspaceContinuationRestoreOutcome.Recovered
                    || recovered.Receipt != restoreReceipt
                    || recovered.Target?.SnapshotDigest != expected.SnapshotDigest
                    || recovered.Target.IncarnationId != restoreReceipt.IncarnationId)
                    return false;
            }

            var current = await continuation.ExportContinuationAsync(originalOwner, id, ct).ConfigureAwait(false);
            if (!current.Success || current.Value?.SnapshotDigest != expected.SnapshotDigest
                || !IsContinuationOwnerCurrent(originalOwner) || !IsDisplayGenerationCurrent(generation)
                || State.IsDirty || State.ActiveWorkspace?.ConflictState is not null)
                return false;

            // Use the freshly captured Core snapshot, not caller-owned mutable
            // collections inside the transport projection.
            CharacterOverviewState beforeLoad = State;
            var result = await lifecycle.LoadContinuationAsync(beforeLoad, originalOwner,
                current.Value.Snapshot.Workspace, ct).ConfigureAwait(false);
            if (!result.CanPublish || result.State.DisplayOwnerContext != originalOwner
                || !IsContinuationOwnerCurrent(originalOwner) || !IsDisplayGenerationCurrent(generation)
                || State.IsDirty || State.ActiveWorkspace?.ConflictState is not null)
                return false;
            ct.ThrowIfCancellationRequested();
            CaptureRecoveryPayload(result);
            if (!TryPublishDisplayTransition(generation, result.State, beforeLoad)) return false;
            await RefreshNavigationContextForCurrentWorkspaceAsync(ct).ConfigureAwait(false);
            if (!IsContinuationDisplayCurrent(originalOwner, current.Value.Snapshot.Workspace, generation)) return false;
            if (!await RenderContinuationDefaultSectionAsync(originalOwner,
                    current.Value.Snapshot.Workspace, generation, ct).ConfigureAwait(false)) return false;
            await SyncShellWorkspaceContextAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return IsContinuationDisplayCurrent(originalOwner, current.Value.Snapshot.Workspace, generation);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // The restore, if any, already committed. Failed activation cannot
            // become permission to import again or invent a successful display.
            return false;
        }
    }

    private bool IsContinuationOwnerCurrent(OwnerContextStamp originalOwner)
        => originalOwner.IsValid && _client is IOwnerBoundWorkspaceProjectionClient bound
            && bound.CaptureOwnerContext() == originalOwner;

    private bool IsContinuationDisplayCurrent(OwnerContextStamp owner, WorkspaceDocumentSnapshot expected, long generation)
        => IsContinuationOwnerCurrent(owner) && IsDisplayGenerationCurrent(generation)
            && State.DisplayOwnerContext == owner && State.Session.OwnerContext == owner
            && State.WorkspaceId == expected.Id && State.Session.ActiveWorkspaceId == expected.Id
            && State.ContentRevision == expected.ContentRevision && State.SavedRevision == expected.SavedRevision;

    private async Task<bool> RenderContinuationDefaultSectionAsync(OwnerContextStamp owner,
        WorkspaceDocumentSnapshot expected, long generation, CancellationToken ct)
    {
        if (!IsContinuationDisplayCurrent(owner, expected, generation)) return false;
        if (!string.IsNullOrWhiteSpace(State.ActiveSectionId)) return true;
        if (_client is not IOwnerBoundWorkspaceProjectionClient client
            || _workspaceSectionRenderer is not IOwnerBoundWorkspaceSectionRenderer renderer) return false;

        CharacterOverviewState display = State;
        string? tabId = !string.IsNullOrWhiteSpace(display.ActiveTabId)
            ? display.ActiveTabId : ResolveDefaultWorkspaceTabId(display.NavigationTabs, display.LastCommandId);
        if (string.IsNullOrWhiteSpace(tabId)) return false;
        string ruleset = expected.Document.RulesetId;
        NavigationTabDefinition? tab = display.NavigationTabs.FirstOrDefault(item => item.Id == tabId)
            ?? _shellCatalogResolver.ResolveNavigationTabs(ruleset).FirstOrDefault(item => item.Id == tabId);
        if (tab is null || !RulesetUiDirectiveCatalog.IsLoadedRunnerVisibleNavigationTab(tab.Id)) return false;
        WorkspaceSurfaceActionDefinition? action = _shellCatalogResolver.ResolveWorkspaceActionsForTab(tab.Id, ruleset)
            .FirstOrDefault(item => item.Kind == WorkspaceSurfaceActionKind.Section && item.TargetId == tab.SectionId);

        // This render belongs to the activation already in progress. Public
        // SelectTabAsync starts a new display generation; using it here would
        // make our own successful section look like a superseding activation.
        // Reuse the real owner-bound renderer but retain the original generation
        // so an independent tab/workspace/owner transition still wins.
        var execution = await _workspaceOperationCoordinator.RunCurrentAsync(expected.Id,
            token => renderer.RenderSectionAsync(client, display, tab.SectionId, tab.Id,
                action?.Id ?? $"{tab.Id}.{tab.SectionId}", token), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (!execution.CanPublish || !IsContinuationDisplayCurrent(owner, expected, generation)
            || execution.Value.DisplayOwnerContext != owner) return false;
        WorkspaceSectionRenderResult section = execution.Value;
        if (!TryPublishDisplayTransition(generation, State with
            {
                IsBusy = false,
                Error = null,
                ActiveTabId = section.ActiveTabId,
                ActiveActionId = section.ActiveActionId,
                ActiveSectionId = section.ActiveSectionId,
                ActiveSectionJson = section.ActiveSectionJson,
                ActiveSectionRows = section.ActiveSectionRows,
                ActiveBuildLab = section.ActiveBuildLab,
                ActiveBrowseWorkspace = section.ActiveBrowseWorkspace,
                ActiveNpcPersonaStudio = section.ActiveNpcPersonaStudio,
                ActiveCollectionEditor = section.ActiveCollectionEditor,
                ActiveConditionMonitor = section.ActiveConditionMonitor,
                ActiveLocationEditor = section.ActiveLocationEditor
            }, display)) return false;
        _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(State);
        return true;
    }
}
