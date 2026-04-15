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
            Publish(State with { Error = "No workspace loaded." });
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
            ActiveNpcPersonaStudio = null
        });

        try
        {
            WorkspaceSectionRenderResult section = await _workspaceSectionRenderer.RenderSectionAsync(
                _client,
                currentWorkspace.Value,
                sectionId,
                tabId,
                actionId,
                State.ActiveTabId,
                State.ActiveActionId,
                ct);
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
                ActiveNpcPersonaStudio = section.ActiveNpcPersonaStudio
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
            Publish(State with { Error = "No workspace loaded." });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceSectionRenderResult summary = await _workspaceSectionRenderer.RenderSummaryAsync(
                _client,
                currentWorkspace.Value,
                action,
                ct);
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
                ActiveNpcPersonaStudio = summary.ActiveNpcPersonaStudio
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
            Publish(State with { Error = "No workspace loaded." });
            return;
        }

        Publish(State with
        {
            IsBusy = true,
            Error = null
        });

        try
        {
            WorkspaceSectionRenderResult validation = await _workspaceSectionRenderer.RenderValidationAsync(
                _client,
                currentWorkspace.Value,
                action,
                ct);
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
                ActiveNpcPersonaStudio = validation.ActiveNpcPersonaStudio
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
