using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed partial class CharacterOverviewPresenter
{
    private async Task LoadSectionAsync(string sectionId, string? tabId, string? actionId, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            Publish(State with { Error = "Section id is required." });
            return;
        }

        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No dossier loaded." });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null,
            ActiveTabId = tabId ?? State.ActiveTabId,
            ActiveActionId = actionId ?? State.ActiveActionId,
            ActiveSectionId = sectionId,
            ActiveSectionJson = null,
            ActiveSectionRows = [],
            ActiveBuildLab = null,
            ActiveBrowseWorkspace = null,
            ActiveNpcPersonaStudio = null,
            ActiveCollectionEditor = null,
            ActiveConditionMonitor = null,
            ActiveLocationEditor = null
        });

        try
        {
            WorkspaceOperationExecution<WorkspaceSectionRenderResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    token => _workspaceSectionRenderer.RenderSectionAsync(
                        _client,
                        currentWorkspace.Value,
                        sectionId,
                        tabId,
                        actionId,
                        State.ActiveTabId,
                        State.ActiveActionId,
                        token),
                    ct)
                .ConfigureAwait(false);
            if (!execution.CanPublish)
            {
                return;
            }

            WorkspaceSectionRenderResult section = execution.Value;
            Publish(State with
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
            });
            _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(State);
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

    private async Task RenderSummaryAction(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No dossier loaded." });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOperationExecution<WorkspaceSectionRenderResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    token => _workspaceSectionRenderer.RenderSummaryAsync(
                        _client,
                        currentWorkspace.Value,
                        action,
                        token),
                    ct)
                .ConfigureAwait(false);
            if (!execution.CanPublish)
            {
                return;
            }

            WorkspaceSectionRenderResult summary = execution.Value;
            Publish(State with
            {
                IsBusy = false,
                Error = null,
                ActiveTabId = summary.ActiveTabId,
                ActiveActionId = summary.ActiveActionId,
                ActiveSectionId = summary.ActiveSectionId,
                ActiveSectionJson = summary.ActiveSectionJson,
                ActiveSectionRows = summary.ActiveSectionRows,
                ActiveBuildLab = summary.ActiveBuildLab,
                ActiveBrowseWorkspace = summary.ActiveBrowseWorkspace,
                ActiveNpcPersonaStudio = summary.ActiveNpcPersonaStudio,
                ActiveCollectionEditor = summary.ActiveCollectionEditor,
                ActiveConditionMonitor = summary.ActiveConditionMonitor,
                ActiveLocationEditor = summary.ActiveLocationEditor
            });
            _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(State);
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

    private async Task RenderValidateAction(WorkspaceSurfaceActionDefinition action, CancellationToken ct)
    {
        CharacterWorkspaceId? currentWorkspace = ResolveCurrentWorkspaceId();
        if (currentWorkspace is null)
        {
            Publish(State with { Error = "No dossier loaded." });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceOperationExecution<WorkspaceSectionRenderResult> execution = await _workspaceOperationCoordinator
                .RunCurrentAsync(
                    currentWorkspace.Value,
                    token => _workspaceSectionRenderer.RenderValidationAsync(
                        _client,
                        currentWorkspace.Value,
                        action,
                        token),
                    ct)
                .ConfigureAwait(false);
            if (!execution.CanPublish)
            {
                return;
            }

            WorkspaceSectionRenderResult validation = execution.Value;
            Publish(State with
            {
                IsBusy = false,
                Error = null,
                ActiveTabId = validation.ActiveTabId,
                ActiveActionId = validation.ActiveActionId,
                ActiveSectionId = validation.ActiveSectionId,
                ActiveSectionJson = validation.ActiveSectionJson,
                ActiveSectionRows = validation.ActiveSectionRows,
                ActiveBuildLab = validation.ActiveBuildLab,
                ActiveBrowseWorkspace = validation.ActiveBrowseWorkspace,
                ActiveNpcPersonaStudio = validation.ActiveNpcPersonaStudio,
                ActiveCollectionEditor = validation.ActiveCollectionEditor,
                ActiveConditionMonitor = validation.ActiveConditionMonitor,
                ActiveLocationEditor = validation.ActiveLocationEditor
            });
            _workspaceOverviewLifecycleCoordinator.CaptureCurrentWorkspaceView(State);
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
