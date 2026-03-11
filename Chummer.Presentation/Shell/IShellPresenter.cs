namespace Chummer.Presentation.Shell;

public interface IShellPresenter
{
    ShellState State { get; }

    event EventHandler? StateChanged;

    Task InitializeAsync(CancellationToken ct);

    Task ExecuteCommandAsync(string commandId, CancellationToken ct);

    Task SelectTabAsync(string tabId, CancellationToken ct);

    Task ToggleMenuAsync(string menuId, CancellationToken ct);

    Task SetPreferredRulesetAsync(string rulesetId, CancellationToken ct);

    Task SyncWorkspaceContextAsync(Chummer.Contracts.Workspaces.CharacterWorkspaceId? activeWorkspaceId, CancellationToken ct);

    void SyncOverviewFeedback(ShellOverviewFeedback feedback)
    {
    }
}
