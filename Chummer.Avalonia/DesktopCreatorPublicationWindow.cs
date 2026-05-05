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

internal enum DesktopCreatorPublicationSurface
{
    Overview,
    Moderation
}

internal sealed class DesktopCreatorPublicationWindow : Window
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

    private DesktopCreatorPublicationWindow(
        DesktopInstallLinkingState installState,
        DesktopPreferenceState preferences,
        IReadOnlyList<WorkspaceListItem> recentWorkspaces,
        DesktopHomeCampaignProjection campaignProjection,
        DesktopHomeCampaignServerPlane? campaignServerPlane,
        DesktopHomePortableExchangePreview? portableExchangePreview,
        DesktopHomeSupportProjection supportProjection,
        CreatorPublicationProjection? leadPublication,
        DesktopCreatorPublicationSurface initialSurface,
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

        Title = "Creator Publication";
        Width = 900;
        Height = 700;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Border publicationSection = CreateSection(
            "Creator publication",
            CreateWrappedText(BuildPublicationBody()),
            CreateActionRow(CreatePublicationActions()));
        Border trustSection = CreateSection(
            "Trust ranking and lineage",
            CreateWrappedText(BuildTrustBody()),
            CreateActionRow(CreateTrustActions()));
        Border moderationSection = CreateSection(
            "Moderation flow",
            CreateWrappedText(BuildModerationBody()),
            CreateActionRow(CreateModerationActions()));
        Border? focusSection = initialSurface switch
        {
            DesktopCreatorPublicationSurface.Moderation => moderationSection,
            _ => publicationSection
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
                            Text = "Creator Publication",
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
                        },
                        CreateWrappedText(BuildIntro()),
                        CreateWrappedText(BuildStatus()),
                        publicationSection,
                        trustSection,
                        moderationSection,
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
            DesktopCreatorPublicationSurface.Overview,
            portabilityActivity);

    public static Task ShowModerationAsync(
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
            DesktopCreatorPublicationSurface.Moderation,
            portabilityActivity);

    public static Task ShowAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId, DesktopCreatorPublicationSurface.Overview, portabilityActivity);

    public static Task ShowModerationAsync(Window owner, string headId, WorkspacePortabilityActivity? portabilityActivity = null)
        => ShowAsync(owner, headId, DesktopCreatorPublicationSurface.Moderation, portabilityActivity);

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
        DesktopCreatorPublicationSurface initialSurface,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(installState);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(recentWorkspaces);
        ArgumentNullException.ThrowIfNull(campaignProjection);
        ArgumentNullException.ThrowIfNull(supportProjection);

        DesktopCreatorPublicationWindow dialog = new(
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
        DesktopCreatorPublicationSurface initialSurface,
        WorkspacePortabilityActivity? portabilityActivity)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopCreatorPublicationContext context = await CreateContextAsync(headId).ConfigureAwait(true);
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

    private static async Task<DesktopCreatorPublicationContext> CreateContextAsync(string headId)
    {
        IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
            ?? throw new InvalidOperationException("Desktop creator publication surface requires an IChummerClient instance."));

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

        return new DesktopCreatorPublicationContext(
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
        => "Desktop-native creator publication route: review discovery posture, trust ranking, lineage, and moderation flow without bypassing registry truth.";

    private string BuildStatus()
        => _campaignServerPlane is null
            ? "Desktop status: local publication fallback is active, so creator publication stays grounded on the current workspace digest."
            : $"Desktop status: publication packet refreshed at {_campaignServerPlane.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";

    private string BuildPublicationBody()
    {
        List<string> lines =
        [
            $"Next safe action: {ResolvePublicationNextSafeAction()}",
            $"Creator publication summary: {ResolvePublicationSummary()}",
            $"Discovery posture: {ResolveDiscoverySummary()}",
            $"Publication status: {ResolvePublicationStatusSummary()}",
            $"Artifact shelf view: {ResolveArtifactShelfSummary()}"
        ];

        if (_portableExchangePreview is not null)
        {
            lines.Add($"Portable exchange follow-through: {_portableExchangePreview.NextSafeAction}");
        }

        return string.Join("\n", lines);
    }

    private string BuildTrustBody()
    {
        List<string> lines =
        [
            $"Trust ranking: {ResolveTrustSummary()}",
            $"Lineage: {ResolveLineageSummary()}",
            $"Comparison rail: {ResolveComparisonSummary()}",
            $"Compatibility posture: {_campaignProjection.SupportClosureSummary}"
        ];

        foreach (string highlight in _campaignProjection.ReadinessHighlights.Where(IsPublicationHighlight).Take(5))
        {
            lines.Add(highlight);
        }

        return string.Join("\n", lines);
    }

    private string BuildModerationBody()
    {
        List<string> lines =
        [
            $"Moderation flow: {ResolveModerationSummary()}",
            $"Review lane: {ResolveModerationReviewSummary()}",
            $"Correction path: {ResolveCorrectionSummary()}",
            $"Support follow-through: {_supportProjection.Summary}"
        ];

        foreach (string watchout in _campaignProjection.Watchouts.Where(IsPublicationWatchout).Take(4))
        {
            lines.Add($"Moderation watchout: {watchout}");
        }

        if (_supportProjection.NeedsAttention)
        {
            lines.Add("Moderation follow-through: a tracked support lane is active on this install before discovery widens further.");
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreatePublicationActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton("Open Creator Publication", () => Task.FromResult(OpenArtifactShelfView("creator")), isPrimary: true)
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton("Review Moderation Flow", OpenModerationSurfaceAsync),
            CreateButton("Open Campaign Workspace", OpenCampaignWorkspaceAsync)
        ];

        if (HasPortableExchangePreview())
        {
            actions.Add(CreateButton("Review Portable Exchange", OpenPortableExchangeAsync));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateTrustActions()
    {
        List<Button> actions =
        [
            CreateButton("Open Rule Environment Studio", OpenRuleEnvironmentStudioAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_campaign_primer"), OpenCampaignPrimerArtifact),
            CreateButton(S("desktop.home.button.open_mission_briefing"), OpenMissionBriefingArtifact)
        ];

        if (ResolvePrimaryWorkspace() is not null)
        {
            actions.Add(CreateButton("Open Portable Export", OpenPortableExportAsync));
        }

        return actions;
    }

    private IReadOnlyList<Button> CreateModerationActions()
    {
        List<Button> actions =
        [
            CreateButton(S("desktop.home.button.open_work_support"), OpenWorkspaceSupport, isPrimary: true),
            CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        if (_supportProjection.HasTrackedCase)
        {
            actions.Insert(1, CreateButton(S("desktop.home.button.open_tracked_case"), OpenTrackedSupportCase));
        }

        return actions;
    }

    private Task OpenModerationSurfaceAsync()
        => ShowModerationAsync(
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

    private Task OpenRuleEnvironmentStudioAsync()
        => DesktopRuleEnvironmentStudioWindow.ShowAsync(this, _installState.HeadId, _portabilityActivity);

    private Task OpenCampaignPrimerArtifact()
        => DesktopCampaignArtifactWindow.ShowPrimerAsync(this, _installState.HeadId);

    private Task OpenMissionBriefingArtifact()
        => DesktopCampaignArtifactWindow.ShowMissionBriefingAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenTrackedSupportCase()
        => _supportProjection.HasTrackedCase
           ? DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)
           : Task.CompletedTask;

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_creator_publication");

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

    private Task OpenPortableExchangeAsync()
        => Task.FromResult(OpenPortableExchangeRoute());

    private Task OpenPortableExportAsync()
    {
        if (ResolvePrimaryWorkspace() is not { } workspace)
        {
            return Task.CompletedTask;
        }

        if (Owner is MainWindow mainWindow)
        {
            return mainWindow.OpenWorkspaceCommandFromDesktopSurfaceAsync(
                workspace.Id.Value,
                "export_character",
                "open portable dossier export");
        }

        DesktopInstallLinkingRuntime.TryOpenWorkspacePortal(workspace.Id.Value, fragment: "portable-exchange");
        return Task.CompletedTask;
    }

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

    private bool OpenArtifactShelfView(string view)
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
           && DesktopInstallLinkingRuntime.TryOpenRelativePortal($"/artifacts?view={Uri.EscapeDataString(view)}");

    private WorkspaceListItem? ResolveSupportWorkspace()
        => ResolvePrimaryWorkspace();

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

    private bool HasPortableExchangePreview()
        => _portableExchangePreview is not null;

    private string ResolvePublicationSummary()
        => FirstNonBlank(
            _campaignServerPlane?.PublicationSummary,
            _leadPublication?.Summary,
            _campaignProjection.Summary);

    private string ResolveDiscoverySummary()
        => FirstNonBlank(
            _leadPublication?.DiscoverySummary,
            "Discovery stays bounded until governed publication posture and compatibility review are explicit.");

    private string ResolvePublicationStatusSummary()
        => _leadPublication is null
            ? "No creator publication record is loaded yet for this desktop route."
            : $"{HumanizeValue(_leadPublication.PublicationStatus, "Ready")} with {HumanizeValue(_leadPublication.Visibility, "Private")} visibility.";

    private string ResolveArtifactShelfSummary()
        => DesktopInstallLinkingRuntime.IsClaimed(_installState)
            ? "Open the creator artifact shelf from this desktop route before widening audience or discovery."
            : "Link this install before you trust creator publication or moderation follow-through.";

    private string ResolvePublicationNextSafeAction()
        => FirstNonBlank(
            _leadPublication?.NextSafeAction,
            _campaignProjection.ReadinessHighlights.FirstOrDefault(static line => line.StartsWith("Publication next:", StringComparison.OrdinalIgnoreCase)),
            _campaignProjection.NextSafeAction);

    private string ResolveTrustSummary()
        => FirstNonBlank(
            _leadPublication?.TrustSummary,
            "Trust ranking stays anchored to governed provenance, compatibility posture, and campaign-return fit.");

    private string ResolveLineageSummary()
        => FirstNonBlank(
            _leadPublication?.LineageSummary,
            _campaignProjection.ReadinessHighlights.FirstOrDefault(static line => line.StartsWith("Publication lineage:", StringComparison.OrdinalIgnoreCase)),
            "Lineage remains bounded to the current governed campaign lane until a successor replaces it.");

    private string ResolveComparisonSummary()
        => FirstNonBlank(
            _leadPublication?.ComparisonSummary,
            "Compare creator publication candidates by provenance, lineage, moderation posture, and trust ranking instead of popularity fog.");

    private string ResolveModerationSummary()
        => FirstNonBlank(
            _leadPublication?.ModerationSummary,
            "Moderation stays review-first until lineage, compatibility, and trust ranking are explicit on the same governed desktop lane.");

    private string ResolveModerationReviewSummary()
        => FirstNonBlank(
            _leadPublication?.DiscoverySummary,
            _campaignServerPlane?.PublicationSummary,
            _campaignProjection.SupportClosureSummary);

    private string ResolveCorrectionSummary()
        => _campaignProjection.Watchouts.FirstOrDefault(IsPublicationWatchout)
            ?? "Use workspace support, tracked support closure, or rule-environment review before a creator publication correction widens discovery.";

    private static bool IsPublicationHighlight(string line)
        => line.Contains("Publication ", StringComparison.OrdinalIgnoreCase)
           || line.Contains("Artifact publication:", StringComparison.OrdinalIgnoreCase)
           || line.Contains("Artifact trust:", StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicationWatchout(string line)
        => line.Contains("publication", StringComparison.OrdinalIgnoreCase)
           || line.Contains("moderation", StringComparison.OrdinalIgnoreCase)
           || line.Contains("trust", StringComparison.OrdinalIgnoreCase);

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

    private static string HumanizeValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string normalized = value.Replace('_', ' ').Replace('-', ' ').Trim();
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private TextBlock CreateWrappedText(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };

    private static Border CreateSection(string heading, Control body, Control? actionContent = null)
    {
        StackPanel content = new()
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 15
                },
                body
            }
        };

        if (actionContent is not null)
        {
            content.Children.Add(actionContent);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F4F6FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D4DCE7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }

        return actionRow;
    }

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 104
        };

        if (isPrimary)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        button.Click += async (_, _) =>
        {
            await action().ConfigureAwait(true);
            if (closeWindow && TopLevel.GetTopLevel(button) is Window window)
            {
                window.Close();
            }
        };

        return button;
    }

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);
}

internal sealed record DesktopCreatorPublicationContext(
    DesktopInstallLinkingState InstallState,
    DesktopPreferenceState Preferences,
    IReadOnlyList<WorkspaceListItem> RecentWorkspaces,
    DesktopHomeCampaignProjection CampaignProjection,
    DesktopHomeCampaignServerPlane? CampaignServerPlane,
    DesktopHomePortableExchangePreview? PortableExchangePreview,
    DesktopHomeSupportProjection SupportProjection,
    CreatorPublicationProjection? LeadPublication);
