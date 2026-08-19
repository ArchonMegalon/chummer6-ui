using Chummer.Campaign.Contracts;
using Chummer.Contracts.Api;
using Chummer.Contracts.Characters;
using Chummer.Contracts.Content;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;
using Chummer.Presentation.Overview;
using System.Text.Json.Nodes;
using Chummer.Run.Contracts.Billing;

namespace Chummer.Presentation;

public sealed record BuildGhostAnalysisClientContext(
    string Locale,
    IReadOnlyList<string> SupportedLocales,
    string DeterministicFallbackText);

public interface IChummerClient
{
    Task<ShellPreferences> GetShellPreferencesAsync(CancellationToken ct);

    Task SaveShellPreferencesAsync(ShellPreferences preferences, CancellationToken ct);

    Task<ShellSessionState> GetShellSessionAsync(CancellationToken ct);

    Task SaveShellSessionAsync(ShellSessionState session, CancellationToken ct);

    Task<WorkspaceImportResult> ImportAsync(WorkspaceImportDocument document, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(CancellationToken ct);

    Task<CommandResult<WorkspaceDocumentSnapshot>> GetWorkspaceAsync(
        CharacterWorkspaceId id,
        CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceDocumentSnapshot>(
            false,
            null,
            "Revision-aware workspace reads are unavailable on this compatibility client.",
            WorkspaceOperationOutcome.Unavailable));

    Task<AccountCampaignSummary?> GetAccountCampaignSummaryAsync(CancellationToken ct);

    Task<MyFirstBookQuotaSnapshotDto?> GetMyFirstBookQuotaAsync(CancellationToken ct);

    Task<MyFirstBookQuotaConsumeResultDto> ConsumeMyFirstBookQuotaAsync(CancellationToken ct);

    Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> GetCampaignWorkspaceDigestsAsync(CancellationToken ct);

    Task<IReadOnlyList<DesktopHomeSupportDigest>> GetDesktopHomeSupportDigestsAsync(CancellationToken ct);

    Task<DesktopSupportCaseDetails?> GetDesktopSupportCaseDetailsAsync(string caseId, CancellationToken ct);

    Task<DesktopInstallLinkingSummaryProjection> GetDesktopInstallLinkingSummaryAsync(CancellationToken ct);

    Task<bool> CloseWorkspaceAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspaceRevisionReceipt>> CloseWorkspaceAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
            false,
            null,
            "Revision-aware close is unavailable on this compatibility client.",
            WorkspaceOperationOutcome.Unavailable));

    Task<IReadOnlyList<AppCommandDefinition>> GetCommandsAsync(string? rulesetId, CancellationToken ct);

    Task<IReadOnlyList<NavigationTabDefinition>> GetNavigationTabsAsync(string? rulesetId, CancellationToken ct);

    Task<ShellBootstrapSnapshot> GetShellBootstrapAsync(string? rulesetId, CancellationToken ct);

    Task<RuntimeInspectorProjection?> GetRuntimeInspectorProfileAsync(string profileId, string? rulesetId, CancellationToken ct);

    Task<MasterIndexResponse> GetMasterIndexAsync(CancellationToken ct);

    Task<TranslatorLanguagesResponse> GetTranslatorLanguagesAsync(CancellationToken ct);

    Task<IReadOnlyList<DesktopBuildPathSuggestion>> GetBuildPathSuggestionsAsync(string? rulesetId, CancellationToken ct);

    Task<DesktopBuildPathPreview?> GetBuildPathPreviewAsync(string buildKitId, CharacterWorkspaceId workspaceId, string? rulesetId, CancellationToken ct);

    Task<string?> GetBuildGhostAnalysisPacketAsync(
        CharacterWorkspaceId workspaceId,
        BuildGhostAnalysisClientContext context,
        CancellationToken ct)
        => Task.FromResult<string?>(null);

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

    Task<CommandResult<WorkspaceMetadataResult>> UpdateMetadataAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        UpdateWorkspaceMetadata command,
        CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceMetadataResult>(
            false,
            null,
            "Revision-aware metadata update is unavailable on this compatibility client.",
            WorkspaceOperationOutcome.Unavailable));

    Task<CommandResult<WorkspaceRevisionReceipt>> ReplaceWorkspaceDocumentAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        WorkspaceDocument document,
        CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceRevisionReceipt>(
            false,
            null,
            "Revision-aware replacement is unavailable on this compatibility client.",
            WorkspaceOperationOutcome.Unavailable));

    Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspaceSaveReceipt>> SaveAsync(
        CharacterWorkspaceId id,
        long expectedContentRevision,
        CancellationToken ct)
        => Task.FromResult(new CommandResult<WorkspaceSaveReceipt>(
            false,
            null,
            "Revision-aware save is unavailable on this compatibility client.",
            WorkspaceOperationOutcome.Unavailable));

    Task<CommandResult<WorkspaceDownloadReceipt>> DownloadAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspaceExportReceipt>> ExportAsync(CharacterWorkspaceId id, CancellationToken ct);

    Task<CommandResult<WorkspacePrintReceipt>> PrintAsync(CharacterWorkspaceId id, CancellationToken ct);
}
