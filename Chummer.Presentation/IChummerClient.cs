using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using System.Text.Json.Nodes;

namespace Chummer.Presentation;

public interface IChummerClient
{
    Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct);

    Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct);

    Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct);

    Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct);

    Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct);

    Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct);

    Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct);

    Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct);

    Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct);

    Task<JsonNode> GetSectionAsync(CharacterWorkspaceId id, string sectionId, CancellationToken ct);

    Task<CharacterFileSummary> GetSummaryAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterValidationResult> ValidateAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterProfileSection> GetProfileAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterProgressSection> GetProgressAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterSkillsSection> GetSkillsAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterRulesSection> GetRulesAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterBuildSection> GetBuildAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterMovementSection> GetMovementAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CharacterAwakeningSection> GetAwakeningAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<CharacterProfileSection>> UpdateMetadataAsync(
        CharacterWorkspaceId id,
        UpdateWorkspaceMetadata command,
        CancellationToken ct);

    Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct);
}
