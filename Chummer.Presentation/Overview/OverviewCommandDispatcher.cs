using Chummer.Contracts.Presentation;
using Chummer.Contracts.Rulesets;
using Chummer.Contracts.Workspaces;
using Chummer.Contracts.Content;

namespace Chummer.Presentation.Overview;

public sealed class OverviewCommandDispatcher : IOverviewCommandDispatcher
{
    public async Task DispatchAsync(string commandId, OverviewCommandExecutionContext context, CancellationToken ct)
    {
        if (OverviewCommandPolicy.IsRuntimeInspectorCommand(commandId))
        {
            await OpenRuntimeInspectorDialogAsync(context, ct);
            return;
        }

        if (OverviewCommandPolicy.IsMenuCommand(commandId))
        {
            context.Publish(context.State with
            {
                Error = null,
                Notice = $"Menu '{commandId}' is handled by the active UI shell."
            });
            return;
        }

        if (OverviewCommandPolicy.IsImportHintCommand(commandId))
        {
            DesktopDialogState dialog = BuildCommandDialog(commandId, context);
            context.Publish(context.State with
            {
                Error = null,
                ActiveDialog = dialog,
                Notice = $"Import flow ready for '{commandId}'."
            });
            return;
        }

        if (OverviewCommandPolicy.IsDialogCommand(commandId))
        {
            DesktopDialogState dialog = BuildCommandDialog(commandId, context);
            context.Publish(context.State with
            {
                Error = null,
                ActiveDialog = dialog
            });
            return;
        }

        if (OverviewCommandPolicy.IsEditorRelayCommand(commandId))
        {
            context.Publish(context.State with
            {
                Error = null,
                Notice = $"Command '{commandId}' dispatched to the active section editor."
            });
            return;
        }

        switch (commandId)
        {
            case "save_character":
                await context.SaveAsync(ct);
                return;
            case "save_character_as":
                await context.DownloadAsync(ct);
                return;
            case "print_character":
                await context.PrintAsync(ct);
                return;
            case "refresh_character":
                if (context.CurrentWorkspace is null)
                {
                    context.Publish(context.State with { Error = "No workspace loaded." });
                    return;
                }

                await context.LoadAsync(context.CurrentWorkspace.Value, ct);
                return;
            case "new_character":
                context.Publish(context.CreateResetState(commandId, "New character workspace initialized."));
                return;
            case "new_critter":
                context.Publish(context.CreateResetState(commandId, "New critter workspace initialized."));
                return;
            case "close_all":
            case "restart":
                await context.CloseAllAsync(ct, "Workspace reset complete.");
                return;
            case "close_window":
                if (context.CurrentWorkspace is null)
                {
                    context.Publish(context.State with
                    {
                        Error = null,
                        Notice = "No open workspace to close."
                    });
                    return;
                }

                await context.CloseWorkspaceAsync(context.CurrentWorkspace.Value, ct);
                return;
            default:
                context.Publish(context.State with
                {
                    Error = $"Command '{commandId}' is not implemented in shared presenter yet."
                });
                return;
        }
    }

    private static DesktopDialogState BuildCommandDialog(string commandId, OverviewCommandExecutionContext context)
    {
        return context.DialogFactory.CreateCommandDialog(
            commandId,
            context.State.Profile,
            context.State.Preferences,
            context.State.ActiveSectionJson,
            context.CurrentWorkspace,
            ResolveDialogRulesetId(context),
            runtimeInspector: null);
    }

    private static async Task OpenRuntimeInspectorDialogAsync(OverviewCommandExecutionContext context, CancellationToken ct)
    {
        string? rulesetId = ResolveDialogRulesetId(context);
        ShellBootstrapSnapshot bootstrap = await context.GetShellBootstrapAsync(rulesetId, ct);
        ActiveRuntimeStatusProjection? activeRuntime = bootstrap.ActiveRuntime;
        if (activeRuntime is null)
        {
            context.Publish(context.State with
            {
                ActiveDialog = null,
                Error = "No active runtime profile is available for inspection.",
                Notice = null
            });
            return;
        }

        RuntimeInspectorProjection? runtimeInspector = await context.GetRuntimeInspectorProfileAsync(
            activeRuntime.ProfileId,
            activeRuntime.RulesetId,
            ct);
        if (runtimeInspector is null)
        {
            context.Publish(context.State with
            {
                ActiveDialog = null,
                Error = $"Runtime profile '{activeRuntime.ProfileId}' could not be resolved.",
                Notice = null
            });
            return;
        }

        DesktopDialogState dialog = context.DialogFactory.CreateCommandDialog(
            OverviewCommandPolicy.RuntimeInspectorCommandId,
            context.State.Profile,
            context.State.Preferences,
            context.State.ActiveSectionJson,
            context.CurrentWorkspace,
            activeRuntime.RulesetId,
            runtimeInspector);
        context.Publish(context.State with
        {
            ActiveDialog = dialog,
            Error = null,
            Notice = $"Runtime inspector opened for '{activeRuntime.ProfileId}'."
        });
    }

    private static string? ResolveDialogRulesetId(OverviewCommandExecutionContext context)
    {
        CharacterWorkspaceId? activeWorkspace = context.CurrentWorkspace;
        if (activeWorkspace is not null)
        {
            OpenWorkspaceState? workspace = context.State.OpenWorkspaces.FirstOrDefault(
                candidate => string.Equals(candidate.Id.Value, activeWorkspace.Value.Value, StringComparison.Ordinal));
            if (workspace is not null)
                return RulesetDefaults.NormalizeOptional(workspace.RulesetId);
        }

        string? commandRulesetId = context.State.Commands.FirstOrDefault()?.RulesetId;
        if (!string.IsNullOrWhiteSpace(commandRulesetId))
            return RulesetDefaults.NormalizeRequired(commandRulesetId);

        string? tabRulesetId = context.State.NavigationTabs.FirstOrDefault()?.RulesetId;
        return string.IsNullOrWhiteSpace(tabRulesetId)
            ? null
            : RulesetDefaults.NormalizeRequired(tabRulesetId);
    }
}
