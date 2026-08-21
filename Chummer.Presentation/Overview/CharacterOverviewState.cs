using Chummer.Contracts.Characters;
using Chummer.Contracts.Presentation;
using Chummer.Contracts.Workspaces;

namespace Chummer.Presentation.Overview;

public sealed record CharacterOverviewState(
    bool IsBusy,
    string? Error,
    WorkspaceSessionState Session,
    CharacterWorkspaceId? WorkspaceId,
    IReadOnlyList<OpenWorkspaceState> OpenWorkspaces,
    CharacterProfileSection? Profile,
    CharacterProgressSection? Progress,
    CharacterSkillsSection? Skills,
    CharacterRulesSection? Rules,
    CharacterBuildSection? Build,
    CharacterMovementSection? Movement,
    CharacterAwakeningSection? Awakening,
    string? ActiveTabId,
    string? ActiveActionId,
    string? ActiveSectionId,
    string? ActiveSectionJson,
    IReadOnlyList<SectionRowState> ActiveSectionRows,
    BuildLabConceptIntakeState? ActiveBuildLab,
    BrowseWorkspaceState? ActiveBrowseWorkspace,
    string? LastCommandId,
    WorkspacePortabilityActivity? LatestPortabilityActivity,
    string? Notice,
    DesktopDialogState? ActiveDialog,
    DesktopPreferenceState Preferences,
    IReadOnlyList<AppCommandDefinition> Commands,
    IReadOnlyList<NavigationTabDefinition> NavigationTabs,
    WorkspaceDownloadReceipt? PendingDownload = null,
    long PendingDownloadVersion = 0,
    WorkspaceExportReceipt? PendingExport = null,
    long PendingExportVersion = 0,
    WorkspacePrintReceipt? PendingPrint = null,
    long PendingPrintVersion = 0,
    NpcPersonaStudioState? ActiveNpcPersonaStudio = null,
    WorkspaceRecoveryExportRequest? PendingRecoveryExport = null,
    long PendingRecoveryExportVersion = 0,
    WorkspaceCollectionEditorState? ActiveCollectionEditor = null,
    ConditionMonitorEditorState? ActiveConditionMonitor = null,
    WorkspaceLocationEditorState? ActiveLocationEditor = null)
{
    public static CharacterOverviewState Empty { get; } = new(
        IsBusy: false,
        Error: null,
        Session: WorkspaceSessionState.Empty,
        WorkspaceId: null,
        OpenWorkspaces: [],
        Profile: null,
        Progress: null,
        Skills: null,
        Rules: null,
        Build: null,
        Movement: null,
        Awakening: null,
        ActiveTabId: null,
        ActiveActionId: null,
        ActiveSectionId: null,
        ActiveSectionJson: null,
        ActiveSectionRows: [],
        ActiveBuildLab: null,
        ActiveBrowseWorkspace: null,
        LastCommandId: null,
        LatestPortabilityActivity: null,
        Notice: null,
        ActiveDialog: null,
        Preferences: DesktopPreferenceState.Default,
        Commands: [],
        NavigationTabs: [],
        PendingDownload: null,
        PendingDownloadVersion: 0,
        PendingExport: null,
        PendingExportVersion: 0,
        PendingPrint: null,
        PendingPrintVersion: 0,
        ActiveNpcPersonaStudio: null,
        PendingRecoveryExport: null,
        PendingRecoveryExportVersion: 0,
        ActiveCollectionEditor: null,
        ActiveConditionMonitor: null,
        ActiveLocationEditor: null);

    public OpenWorkspaceState? ActiveWorkspace => Session.ActiveWorkspace;

    public long ContentRevision => ActiveWorkspace?.ContentRevision ?? 0;

    public long SavedRevision => ActiveWorkspace?.SavedRevision ?? 0;

    public bool IsDirty => ActiveWorkspace?.IsDirty ?? false;

    public WorkspaceConflictState? ConflictState => ActiveWorkspace?.ConflictState;

    public bool HasSavedWorkspace => ActiveWorkspace?.HasSavedWorkspace ?? false;
}

/// <summary>
/// Character roster tabs matching Chummer5a oracle:
/// Description, Concept, Background, Character Notes, Game Notes
/// </summary>
public static class CharacterRosterTabs
{
    public const string Description = "Description";
    public const string Concept = "Concept";
    public const string Background = "Background";
    public const string CharacterNotes = "Character Notes";
    public const string GameNotes = "Game Notes";
    
    public static readonly string[] All = [Description, Concept, Background, CharacterNotes, GameNotes];
}
