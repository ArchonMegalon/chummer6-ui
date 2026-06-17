using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Contracts.Workspaces;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal enum DesktopOrganizerOperationsSurface
{
    Overview,
    Roles
}

internal sealed class DesktopOrganizerOperationsWindow : Window
{
    private readonly DesktopInstallLinkingState _installState;
    private readonly DesktopPreferenceState _preferences;
    private readonly IReadOnlyList<WorkspaceListItem> _recentWorkspaces;
    private readonly DesktopHomeCampaignProjection _campaignProjection;
    private readonly DesktopHomeCampaignServerPlane? _campaignServerPlane;
    private readonly DesktopHomePortableExchangePreview? _portableExchangePreview;
    private readonly DesktopHomeSupportProjection _supportProjection;
    private readonly CreatorPublicationProjection? _leadPublication;
    private readonly WorkspacePortabilityActivity? _portabilityActivity;

    private DesktopOrganizerOperationsWindow(
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomePortableExchangePreview? portableExchangePreview,
        DesktopHomeSupportProjection supportProjection,
        CreatorPublicationProjection? leadPublication,
        DesktopOrganizerOperationsSurface initialSurface,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        _installState = installState;
        _preferences = preferences;
        _recentWorkspaces = recentWorkspaces;
        _campaignProjection = campaignProjection;
        _campaignServerPlane = campaignServerPlane;
        _portableExchangePreview = portableExchangePreview;
        _supportProjection = supportProjection;
        _leadPublication = leadPublication;
        _portabilityActivity = portabilityActivity;

        Title = "Organizer Operations";
        Width = 920;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Border operationsSection = CreateSection(
            "Organizer operations",
            CreateWrappedText(BuildOperationsBody()),
            CreateActionRow(CreateOperationsActions()));
        Border rolesSection = CreateSection(
            "Role boundaries",
            CreateWrappedText(BuildRoleBoundariesBody()),
            CreateActionRow(CreateRoleActions()));
        Border escalationSection = CreateSection(
            "Publication and escalation",
            CreateWrappedText(BuildEscalationBody()),
            CreateActionRow(CreateEscalationActions()));
        Border? focusSection = initialSurface switch
        {
            DesktopOrganizerOperationsSurface.Roles => rolesSection,
            _ => operationsSection
        };

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Organizer Operations",
                            IsVisible = false,
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
                        },
                        CreateWrappedText(BuildIntro(), visible: false),
                        CreateWrappedText(BuildStatus(), visible: false),
                        operationsSection,
                        rolesSection,
                        escalationSection,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.continue"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };

        focusSection?.BringIntoView();
    }

    public static Task ShowAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomePortableExchangePreview? portableExchangePreview,
        DesktopHomeSupportProjection supportProjection,
        CreatorPublicationProjection? leadPublication,
        WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(
            owner,
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            portableExchangePreview,
            supportProjection,
            leadPublication,
            DesktopOrganizerOperationsSurface.Overview,
            portabilityActivity);

    public static Task ShowRolesAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomePortableExchangePreview? portableExchangePreview,
        DesktopHomeSupportProjection supportProjection,
        CreatorPublicationProjection? leadPublication,
        WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(
            owner,
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            portableExchangePreview,
            supportProjection,
            leadPublication,
            DesktopOrganizerOperationsSurface.Roles,
            portabilityActivity);

    public static Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId, DesktopOrganizerOperationsSurface.Overview, portabilityActivity);

    public static Task ShowRolesAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId, DesktopOrganizerOperationsSurface.Roles, portabilityActivity);

    private static async Task ShowAsync(
        Window owner,
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomePortableExchangePreview? portableExchangePreview,
        DesktopHomeSupportProjection supportProjection,
        CreatorPublicationProjection? leadPublication,
        DesktopOrganizerOperationsSurface initialSurface,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(installState);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(recentWorkspaces);
        ArgumentNullException.ThrowIfNull(campaignProjection);
        ArgumentNullException.ThrowIfNull(supportProjection);

        DesktopOrganizerOperationsWindow dialog = new(
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            portableExchangePreview,
            supportProjection,
            leadPublication,
            initialSurface,
            portabilityActivity);
        await dialog.ShowDialog(owner);
    }

    private static async Task ShowAsync(
        Window owner,
        string headId,
        DesktopOrganizerOperationsSurface initialSurface,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopOrganizerOperationsContext context = await CreateContextAsync(headId).ConfigureAwait(true);
        await ShowAsync(
            owner,
            context.InstallState,
            context.Preferences,
            context.RecentWorkspaces,
            context.CampaignProjection,
            context.CampaignServerPlane,
            context.PortableExchangePreview,
            context.SupportProjection,
            context.LeadPublication,
            initialSurface,
            portabilityActivity).ConfigureAwait(true);
    }

    private static async Task<DesktopOrganizerOperationsContext> CreateContextAsync(string headId)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop organizer operations surface requires an IChummerClient instance."));

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(headId);
        IReadOnlyList<WorkspaceListItem> recentWorkspaces = await ReadWorkspacesAsync(client).ConfigureAwait(true);
        AccountCampaignSummary? campaignSummary = await ReadCampaignSummaryAsync(client).ConfigureAwait(true);
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests = await ReadCampaignWorkspaceDigestsAsync(client).ConfigureAwait(true);
        string? leadWorkspaceId = ResolveLeadWorkspaceId(campaignSummary, campaignWorkspaceDigests);
        string? leadCampaignId = ResolveLeadCampaignId(campaignSummary, campaignWorkspaceDigests);
        DesktopHomeCampaignServerPlane? campaignServerPlane = await ReadCampaignWorkspaceServerPlaneAsync(client, leadWorkspaceId).ConfigureAwait(true);
        DesktopHomePortableExchangePreview? portableExchangePreview = await ReadPortableExchangePreviewAsync(client, leadCampaignId).ConfigureAwait(true);
        DesktopHomeCampaignProjection campaignProjection = DesktopHomeCampaignProjector.Create(campaignSummary, campaignWorkspaceDigests, campaignServerPlane, portableExchangePreview);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(client, installState).ConfigureAwait(true);
        CreatorPublicationProjection? leadPublication = campaignSummary?.CreatorPublications
            .OrderByDescending(static publication => publication.UpdatedAtUtc)
            .FirstOrDefault();

        return new DesktopOrganizerOperationsContext(
            installState,
            preferences,
            recentWorkspaces,
            campaignProjection,
            campaignServerPlane,
            portableExchangePreview,
            supportProjection,
            leadPublication);
    }

    private static async Task<IReadOnlyList<WorkspaceListItem>> ReadWorkspacesAsync(IChummerClient client)
    {
        try
        {
            IReadOnlyList<WorkspaceListItem> workspaces = await client.ListWorkspacesAsync(CancellationToken.None).ConfigureAwait(false);
            return workspaces
                .OrderByDescending(workspace => workspace.LastUpdatedUtc)
                .Take(5)
                .ToArray();
        }
        catch
        {
            return Array.Empty<WorkspaceListItem>();
        }
    }

    private static async Task<AccountCampaignSummary?> ReadCampaignSummaryAsync(IChummerClient client)
    {
        try
        {
            return await client.GetAccountCampaignSummaryAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<CampaignWorkspaceDigestProjection>> ReadCampaignWorkspaceDigestsAsync(IChummerClient client)
    {
        try
        {
            return await client.GetCampaignWorkspaceDigestsAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<CampaignWorkspaceDigestProjection>();
        }
    }

    private static string? ResolveLeadWorkspaceId(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests)
        => campaignSummary?.Workspaces
               .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
               .Select(static workspace => workspace.WorkspaceId)
               .FirstOrDefault()
           ?? campaignWorkspaceDigests
               .OrderByDescending(static digest => digest.UpdatedAtUtc)
               .Select(static digest => digest.WorkspaceId)
               .FirstOrDefault();

    private static string? ResolveLeadCampaignId(
        AccountCampaignSummary? campaignSummary,
        IReadOnlyList<CampaignWorkspaceDigestProjection> campaignWorkspaceDigests)
        => campaignSummary?.Workspaces
               .OrderByDescending(static workspace => workspace.LatestContinuity?.CapturedAtUtc ?? DateTimeOffset.MinValue)
               .Select(static workspace => workspace.CampaignId)
               .FirstOrDefault()
           ?? campaignWorkspaceDigests
               .OrderByDescending(static digest => digest.UpdatedAtUtc)
               .Select(static digest => digest.CampaignId)
               .FirstOrDefault();

    private static async Task<DesktopHomeCampaignServerPlane?> ReadCampaignWorkspaceServerPlaneAsync(IChummerClient client, string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || client is not HttpChummerClient httpClient)
        {
            return null;
        }

        try
        {
            return await httpClient.GetCampaignWorkspaceServerPlaneAsync(workspaceId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<DesktopHomePortableExchangePreview?> ReadPortableExchangePreviewAsync(IChummerClient client, string? campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || client is not HttpChummerClient httpClient)
        {
            return null;
        }

        try
        {
            return await httpClient.GetPortableExchangePreviewAsync(campaignId, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<DesktopHomeSupportProjection> ReadSupportProjectionAsync(
        IChummerClient client,
        DesktopInstallLinkingState installState)
    {
        try
        {
            return DesktopHomeSupportProjector.Create(
                await client.GetDesktopHomeSupportDigestsAsync(CancellationToken.None).ConfigureAwait(false),
                DesktopInstallLinkingRuntime.IsClaimed(installState));
        }
        catch
        {
            return DesktopHomeSupportProjector.Create(Array.Empty<DesktopHomeSupportDigest>(), DesktopInstallLinkingRuntime.IsClaimed(installState));
        }
    }

    private string BuildIntro()
        => "Desktop organizer operations keep organizer, GM, player, creator, moderator, and operator follow-through visible on one governed lane without collapsing them into one catch-all admin role.";

    private string BuildStatus()
        => _campaignServerPlane is null
            ? "Desktop status: organizer review is using local workspace and support projections until governed community packets refresh."
            : $"Desktop status: organizer-facing packet refreshed at {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";

    private string BuildOperationsBody()
    {
        List<string> lines =
        [
            "Organizer lane:" + " " + ResolveOrganizerLaneSummary(),
            "Event lifecycle receipt:" + " " + ResolveEventLifecycleSummary(),
            "Roster decision receipt:" + " " + ResolveRosterDecisionSummary(),
            "Season cadence:" + " " + ResolveSeasonCadenceSummary(),
            "Audit packet:" + " " + ResolveAuditPacketSummary(),
            "Calendar mirrors:" + " " + ResolveCalendarMirrorSummary()
        ];

        if (_portableExchangePreview is not null)
        {
            lines.Add("Portable exchange follow-through:" + " " + _portableExchangePreview.NextSafeAction);
        }

        return string.Join("\n", lines);
    }

    private string BuildRoleBoundariesBody()
    {
        List<string> lines =
        [
            "Organizer lane:" + " " + ResolveOrganizerBoundarySummary(),
            "GM lane:" + " " + ResolveGmBoundarySummary(),
            "Player lane:" + " " + ResolvePlayerBoundarySummary(),
            "Creator lane:" + " " + ResolveCreatorBoundarySummary(),
            "Support lane:" + " " + ResolveSupportBoundarySummary(),
            "Operator packet lane:" + " " + ResolveOperatorPacketBoundarySummary()
        ];

        if (_campaignProjection.Watchouts.Count > 0)
        {
            lines.Add("Boundary watchout:" + " " + _campaignProjection.Watchouts[0]);
        }

        return string.Join("\n", lines);
    }

    private string BuildEscalationBody()
    {
        List<string> lines =
        [
            $"Publication boundary: {ResolvePublicationBoundarySummary()}",
            $"Support escalation: {ResolveSupportEscalationSummary()}",
            $"Moderation packet: {ResolveModerationPacketSummary()}",
            $"Audience and retention: {ResolveAudienceRetentionSummary()}",
            $"Next safe action: {ResolveNextSafeAction()}",
            $"Proof shelf: {ResolveProofShelfSummary()}"
        ];

        if (_supportProjection.NeedsAttention)
        {
            lines.Add("Support watchout: a tracked support lane is already active, so organizer escalation must attach evidence instead of self-closing the case.");
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateOperationsActions()
    {
        bool canOpenPublicProofShelf = DesktopInstallLinkingRuntime.IsClaimed(_installState) && HasPublishedCreatorPublication();
        List<Button> actions =
        [
            CreateButton("Review Organizer Roles", OpenRolesSurfaceAsync, isPrimary: true),
            CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync),
            CreateButton("Open GM Runboard", OpenGmRunboardAsync),
            CreateButton("Open GM Prep Packets", OpenGmPrepPacketsAsync),
            CreateButton("Open Roster Movement", OpenRosterMovementAsync)
        ];

        if (canOpenPublicProofShelf)
        {
            actions.Add(CreateButton("Open Public Proof Shelf", () => Task.FromResult(OpenArtifactShelfView("public"))));
        }
        else
        {
            actions.Add(CreateButton(
                DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language),
                OpenInstallLinkingAsync));
        }

        if (HasPortableExchangePreview())
        {
            actions.Add(CreateButton("Review Portable Exchange", OpenPortableExchangeAsync));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateRoleActions()
    {
        List<Button> actions =
        [
            CreateButton("Open Organizer Operations", OpenOrganizerOperationsSurfaceAsync, isPrimary: true, closeWindow: true),
            CreateButton("Open Creator Publication", OpenCreatorPublicationAsync),
            CreateButton("Open Rule Environment Studio", OpenRuleEnvironmentStudioAsync),
            CreateButton(S("desktop.home.button.open_campaign_primer"), OpenCampaignPrimerArtifact),
            CreateButton(S("desktop.home.button.open_mission_briefing"), OpenMissionBriefingArtifact)
        ];

        if (HasModerationContext())
        {
            actions.Insert(2, CreateButton("Review Moderation Flow", OpenCreatorModerationAsync));
        }

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState) && HasPublishedCreatorPublication())
        {
            actions.Add(CreateButton("Open Creator Artifact Shelf", () => Task.FromResult(OpenArtifactShelfView("creator"))));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateEscalationActions()
    {
        List<Button> actions =
        [
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        if (_supportProjection.HasTrackedCase)
        {
            actions.Insert(1, CreateButton(S("desktop.home.button.open_tracked_case"), OpenTrackedSupportCase));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupportAsync));
        return actions;
    }

    private Task OpenRolesSurfaceAsync()
        => ShowRolesAsync(
            this,
            _installState,
            _preferences,
            _recentWorkspaces,
            _campaignProjection,
            _campaignServerPlane,
            _portableExchangePreview,
            _supportProjection,
            _leadPublication,
            _portabilityActivity);

    private Task OpenOrganizerOperationsSurfaceAsync()
        => ShowAsync(
            this,
            _installState,
            _preferences,
            _recentWorkspaces,
            _campaignProjection,
            _campaignServerPlane,
            _portableExchangePreview,
            _supportProjection,
            _leadPublication,
            _portabilityActivity);

    private Task OpenCampaignWorkspaceAsync()
        => DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenGmRunboardAsync()
        => DesktopCampaignWorkspaceWindow.ShowGmRunboardAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenGmPrepPacketsAsync()
        => DesktopCampaignWorkspaceWindow.ShowGmPrepAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenRosterMovementAsync()
        => DesktopCampaignWorkspaceWindow.ShowRosterMovementAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenCreatorPublicationAsync()
        => DesktopCreatorPublicationWindow.ShowAsync(
            this,
            _installState,
            _preferences,
            _recentWorkspaces,
            _campaignProjection,
            _campaignServerPlane,
            _portableExchangePreview,
            _supportProjection,
            _leadPublication,
            _portabilityActivity);

    private Task OpenCreatorModerationAsync()
        => DesktopCreatorPublicationWindow.ShowModerationAsync(
            this,
            _installState,
            _preferences,
            _recentWorkspaces,
            _campaignProjection,
            _campaignServerPlane,
            _portableExchangePreview,
            _supportProjection,
            _leadPublication,
            _portabilityActivity);

    private Task OpenRuleEnvironmentStudioAsync()
        => DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenCampaignPrimerArtifact()
        => DesktopCampaignArtifactWindow.ShowPrimerAsync(this, _installState.HeadId);

    private Task OpenMissionBriefingArtifact()
        => DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(this, _installState.HeadId);

    private Task OpenPortableExchangeAsync()
        => Task.FromResult(OpenPortableExchangeRoute());

    private Task OpenTrackedSupportCase()
        => _supportProjection.HasTrackedCase
           ? DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)
           : Task.CompletedTask;

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_organizer_operations");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
    }

    private Task OpenWorkspaceSupport()
    {
        if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(_installState, ResolveSupportWorkspace()))
        {
            return Task.CompletedTask;
        }

        return DesktopSupportWindow.ShowAsync(this, _installState.HeadId);
    }

    private Task OpenInstallSupportAsync()
        => Task.FromResult(DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState));

    private bool OpenArtifactShelfView(string view)
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
           && DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/artifacts?view={Uri.EscapeDataString(view)}");

    private bool OpenPortableExchangeRoute()
    {
        string? workspaceId = _campaignProjection.LeadWorkspaceId ?? ResolvePrimaryWorkspace()?.Id.Value;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return DesktopInstallLinkingRuntime.TryOpenRelativePortal("/artifacts?view=campaign");
        }

        return DesktopInstallLinkingRuntime.TryOpenRelativePortal(
            $"/account/work/workspaces/{Uri.EscapeDataString(workspaceId)}#portable-exchange");
    }

    private WorkspaceListItem? ResolvePrimaryWorkspace()
    {
        if (!string.IsNullOrWhiteSpace(_campaignProjection.LeadWorkspaceId))
        {
            WorkspaceListItem? leadWorkspace = _recentWorkspaces.FirstOrDefault(workspace =>
                string.Equals(workspace.Id.Value, _campaignProjection.LeadWorkspaceId, StringComparison.Ordinal));
            if (leadWorkspace is not null)
            {
                return leadWorkspace;
            }
        }

        return _recentWorkspaces.FirstOrDefault();
    }

    private WorkspaceListItem? ResolveSupportWorkspace()
        => ResolvePrimaryWorkspace();

    private bool HasPortableExchangePreview()
        => _portableExchangePreview is not null;

    private bool HasPublishedCreatorPublication()
        => _leadPublication is not null
           && (string.Equals(_leadPublication.PublicationStatus, "published", StringComparison.OrdinalIgnoreCase)
               || string.Equals(_leadPublication.PublicationStatus, "ready", StringComparison.OrdinalIgnoreCase));

    private bool HasModerationContext()
        => _leadPublication is not null
           && (!string.IsNullOrWhiteSpace(_leadPublication.ModerationSummary)
               || !string.IsNullOrWhiteSpace(_leadPublication.TrustSummary)
               || !string.IsNullOrWhiteSpace(_leadPublication.TrustBand));

    private string ResolveOrganizerLaneSummary()
        => FirstNonBlank(
            _campaignProjection.NextSafeAction,
            "Create or review group, event, and season shells from one governed desktop lane before external mirrors widen distribution.");

    private string ResolveEventLifecycleSummary()
        => FirstNonBlank(
            _campaignServerPlane?.PublicationSummary,
            "Event creation and scheduling need a receipt-backed lifecycle before calendar mirrors or staff packets are trusted.");

    private string ResolveRosterDecisionSummary()
        => _campaignProjection.SupportClosureSummary;

    private string ResolveSeasonCadenceSummary()
        => "Season operator timing can publish standings windows and cross-event cadence, but it cannot rewrite campaign continuity or hidden score math.";

    private string ResolveAuditPacketSummary()
        => "Every organizer-visible group, event, roster, publication, moderation, and escalation action needs one audit packet before downstream packets or mirrors become trustworthy.";

    private string ResolveCalendarMirrorSummary()
        => "Calendar, spreadsheet, and chat mirrors may project dates or staffing, but they do not own role grants, roster acceptance, consent posture, or publication state.";

    private string ResolveOrganizerBoundarySummary()
        => "Organizer lane owns community structure, event visibility, and season posture, but it does not silently seize GM truth, support closure, or registry publication receipts.";

    private string ResolveGmBoundarySummary()
        => "GM lane owns one run's live-table truth, roster-fit reasons, and session-close consequences even when organizer policy frames the surrounding event.";

    private string ResolvePlayerBoundarySummary()
        => "Player lane receives briefings, roster outcomes, and published artifacts, but it does not grant roles, close incidents, or rewrite organizer policy.";

    private string ResolveCreatorBoundarySummary()
        => FirstNonBlank(
            _leadPublication?.TrustSummary,
            "Creator lane stays on registry-backed publication, lineage, and moderation rails instead of being folded into organizer administration.");

    private string ResolveSupportBoundarySummary()
        => _supportProjection.HasTrackedCase
            ? "Support lane still owns case state and closure; organizer escalation may attach evidence and freeze publication, but it cannot mark the incident closed."
            : "Support lane remains separate; organizers can request escalation, but only support state can close the case.";

    private string ResolveOperatorPacketBoundarySummary()
        => "Fleet and EA packets may summarize organizer health, publication readiness, and support risk, but they remain projections linked back to audit packet ids.";

    private string ResolvePublicationBoundarySummary()
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? "Registry-backed publication truth still owns audience, retention, locale, and availability once a community artifact leaves draft state."
            : "Link this install before you trust organizer publication state, artifact availability, or registry-facing follow-through.";

    private string ResolveSupportEscalationSummary()
        => _supportProjection.Summary;

    private string ResolveModerationPacketSummary()
        => "Safety and moderation actions require a packet-backed case; temporary organizer action is not final support closure or release-health truth.";

    private string ResolveAudienceRetentionSummary()
        => "Publication, notices, and honors need explicit audience, retention, and locale posture so organizer copy cannot blur into player-safe, creator-only, or public truth.";

    private string ResolveNextSafeAction()
        => FirstNonBlank(
            _portableExchangePreview?.NextSafeAction,
            _campaignProjection.NextSafeAction,
            "Review organizer roles before you publish, escalate support, or widen discovery.");

    private string ResolveProofShelfSummary()
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? "Use the public proof shelf and creator publication lane to verify what the community can actually see."
            : "Organizer proof stays blocked until this install is linked and can open the governed desktop shelves.";

    private static string FirstNonBlank(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private TextBlock CreateWrappedText(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };

    private TextBlock CreateWrappedText(string text, bool visible)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = visible
        };

    private static Border CreateSection(string heading, Control body, Control? actionContent = null)
        => DesktopShellTheme.CreateSection(heading, body, actionContent, padding: 8, cornerRadius: 4, includeHeading: false, spacing: 0);

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 6);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);
}

internal sealed record DesktopOrganizerOperationsContext(
    DesktopInstallLinkingState InstallState,
    DesktopPreferenceState Preferences,
    IReadOnlyList<WorkspaceListItem> RecentWorkspaces,
    DesktopHomeCampaignProjection CampaignProjection,
    DesktopHomeCampaignServerPlane? CampaignServerPlane,
    DesktopHomePortableExchangePreview? PortableExchangePreview,
    DesktopHomeSupportProjection SupportProjection,
    CreatorPublicationProjection? LeadPublication);
