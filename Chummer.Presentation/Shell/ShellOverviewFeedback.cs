namespace Chummer.Presentation.Shell;

public sealed record ShellOverviewFeedback(
    IReadOnlyList<ShellWorkspaceState> OpenWorkspaces,
    string? Notice,
    string? Error,
    string? LastCommandId);
