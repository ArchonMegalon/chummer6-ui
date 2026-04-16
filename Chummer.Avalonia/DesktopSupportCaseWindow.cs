using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopSupportCaseWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private DesktopHomeSupportProjection _supportProjection;
    private DesktopSupportCaseDetails? _supportCase;
    private readonly bool _isPreview;
    private readonly TextBlock _introText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _timelineText;
    private readonly TextBlock _diagnosticsText;
    private readonly TextBlock _followThroughText;
    private readonly StackPanel _summaryActionsRow;
    private readonly StackPanel _timelineActionsRow;
    private readonly StackPanel _followThroughActionsRow;

    private DesktopSupportCaseWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences,
        DesktopHomeSupportProjection supportProjection,
        DesktopSupportCaseDetails? supportCase,
        bool isPreview)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _supportProjection = supportProjection;
        _supportCase = supportCase;
        _isPreview = isPreview;

        Title = S("desktop.support_case.title");
        Width = 920;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = BuildStatus(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _summaryText = new TextBlock
        {
            Text = BuildSummaryBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _timelineText = new TextBlock
        {
            Text = BuildTimelineBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _diagnosticsText = new TextBlock
        {
            Text = BuildDiagnosticsBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _followThroughText = new TextBlock
        {
            Text = BuildFollowThroughBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _summaryActionsRow = CreateActionRow(CreateSummaryActions());
        _timelineActionsRow = CreateActionRow(CreateTimelineActions());
        _followThroughActionsRow = CreateActionRow(CreateFollowThroughActions());

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(22),
                Child = new StackPanel
                {
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.support_case.heading"),
                            FontSize = 24,
                            FontWeight = FontWeight.SemiBold
                        },
                        _introText,
                        _statusText,
                        CreateSection(S("desktop.support_case.section.summary"), _summaryText, _summaryActionsRow),
                        CreateSection(S("desktop.support_case.section.timeline"), _timelineText, _timelineActionsRow),
                        CreateSection("Diagnostics environment diff", _diagnosticsText, null),
                        CreateSection(S("desktop.support_case.section.follow_through"), _followThroughText, _followThroughActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.support_case.button.refresh"), RefreshSupportCaseAsync),
                                CreateButton(S("desktop.home.button.continue"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId, DesktopHomeSupportProjection supportProjection)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopSupportCaseWindow dialog = await CreateAsync(headId, supportProjection).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowPreviewAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopPreferenceState preferences = CreatePreferences(installState.HeadId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopHomeSupportProjection supportProjection = CreatePreviewSupportProjection(preferences.Language);
        DesktopSupportCaseDetails supportCase = CreatePreviewSupportCaseDetails(installState, updateStatus, supportProjection);
        DesktopSupportCaseWindow dialog = new(installState, updateStatus, preferences, supportProjection, supportCase, isPreview: true);
        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopSupportCaseWindow> CreateAsync(string headId, DesktopHomeSupportProjection supportProjection)
    {
        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = CreatePreferences(headId);
        (DesktopHomeSupportProjection refreshedProjection, DesktopSupportCaseDetails? supportCase) = await ReadSupportCaseStateAsync(
            installState,
            supportProjection).ConfigureAwait(true);

        return new DesktopSupportCaseWindow(
            installState,
            updateStatus,
            preferences,
            refreshedProjection,
            supportCase,
            isPreview: false);
    }

    private static DesktopPreferenceState CreatePreferences(string headId)
        => DesktopPreferenceRuntime.LoadOrCreateState(headId);

    private static async Task<(DesktopHomeSupportProjection Projection, DesktopSupportCaseDetails? SupportCase)> ReadSupportCaseStateAsync(
        DesktopInstallLinkingState installState,
        DesktopHomeSupportProjection fallbackProjection)
    {
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop tracked support case requires an IChummerClient instance."));

            bool installClaimed = DesktopInstallLinkingRuntime.IsClaimed(installState);
            Task<IReadOnlyList<DesktopHomeSupportDigest>> digestsTask = client.GetDesktopHomeSupportDigestsAsync(CancellationToken.None);
            Task<DesktopSupportCaseDetails?> detailsTask = string.IsNullOrWhiteSpace(fallbackProjection.CaseId)
                ? Task.FromResult<DesktopSupportCaseDetails?>(null)
                : client.GetDesktopSupportCaseDetailsAsync(fallbackProjection.CaseId, CancellationToken.None);
            await Task.WhenAll(digestsTask, detailsTask).ConfigureAwait(false);

            DesktopHomeSupportProjection projection = fallbackProjection;
            IReadOnlyList<DesktopHomeSupportDigest> digests = digestsTask.Result;
            if (!string.IsNullOrWhiteSpace(fallbackProjection.CaseId))
            {
                DesktopHomeSupportDigest? matchedDigest = digests.FirstOrDefault(digest =>
                    string.Equals(digest.CaseId, fallbackProjection.CaseId, StringComparison.OrdinalIgnoreCase));
                if (matchedDigest is not null)
                {
                    projection = DesktopHomeSupportProjector.Create([matchedDigest], installClaimed);
                }
                else if (digests.Count > 0)
                {
                    projection = DesktopHomeSupportProjector.Create(digests, installClaimed);
                }
            }
            else if (digests.Count > 0)
            {
                projection = DesktopHomeSupportProjector.Create(digests, installClaimed);
            }

            return (projection, detailsTask.Result);
        }
        catch
        {
            return (fallbackProjection, null);
        }
    }

    private static DesktopHomeSupportProjection CreatePreviewSupportProjection(string language)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DesktopHomeSupportProjection(
            CaseId: "preview-support-case",
            Summary: "Tracked case: synthetic support closure preview for flagship desktop smoke verification.",
            NextSafeAction: "Review the native release and install posture on this desktop, then return to the tracked case if more follow-through is needed.",
            PrimaryActionLabel: DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_update_status", language),
            PrimaryActionHref: "/downloads",
            DetailHref: "/account/support/preview-support-case",
            InstallReadinessSummary: "This preview install is already carrying the reporter-ready fix posture, so the last visible closure step stays grounded to this exact desktop copy.",
            StatusLabel: "Released",
            StageLabel: "Released",
            UpdatedLabel: $"{now.ToUniversalTime():yyyy-MM-dd HH:mm} UTC",
            FixedReleaseLabel: "preview smoke",
            AffectedInstallSummary: "This preview case stays attached to the linked avalonia desktop copy.",
            FollowUpLaneSummary: "Follow-up stays grounded to the signed-in support lane when the live account surface is reachable again.",
            ReleaseProgressSummary: "The preview fix already reached the reporter-ready release lane for this desktop path.",
            VerificationSummary: "Use the signed-in support lane to record final fix confirmation once the live account surface is available again.",
            HasTrackedCase: true,
            NeedsAttention: true,
            FixReadyOnLinkedInstall: true,
            NeedsInstallUpdate: false,
            NeedsLinkedInstall: false,
            Highlights:
            [
                "Stage: Released (Released)",
                "Closure: The preview fix already reached the reporter-ready release lane for this desktop path.",
                "Release progress: The preview fix already reached the reporter-ready release lane for this desktop path.",
                "Fix availability: preview smoke is the tracked fix target for this desktop support lane.",
                "Verification: Use the signed-in support lane to record final fix confirmation once the live account surface is available again.",
                $"Updated: {now.ToUniversalTime():yyyy-MM-dd HH:mm} UTC"
            ]);
    }

    private static DesktopSupportCaseDetails CreatePreviewSupportCaseDetails(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopHomeSupportProjection supportProjection)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string? fixedVersion = updateStatus.LastManifestVersion;
        if (string.IsNullOrWhiteSpace(fixedVersion))
        {
            fixedVersion = installState.ApplicationVersion;
        }

        return new DesktopSupportCaseDetails(
            CaseId: supportProjection.CaseId ?? "preview-support-case",
            Kind: "bug_report",
            Status: "released_to_reporter_channel",
            Title: "Synthetic support closure preview",
            Summary: "This preview simulates the tracked support-case posture that the flagship desktop should expose natively.",
            Detail: "Synthetic support case preview for flagship desktop smoke verification. The live account surface can replace this bounded projection whenever a real tracked case is available.",
            CandidateOwnerRepo: "chummer-presentation",
            DesignImpactSuspected: false,
            CreatedAtUtc: now.AddDays(-2),
            UpdatedAtUtc: now,
            Source: "desktop_feedback",
            InstallationId: installState.InstallationId,
            ApplicationVersion: installState.ApplicationVersion,
            ReleaseChannel: installState.ChannelId,
            HeadId: installState.HeadId,
            Platform: installState.Platform,
            Arch: installState.Arch,
            FixedVersion: fixedVersion,
            FixedChannel: installState.ChannelId,
            ReleasedToReporterChannelAtUtc: now.AddHours(-6),
            UserNotifiedAtUtc: now.AddHours(-2),
            ReporterVerificationState: null,
            ReporterVerificationNote: null,
            ReporterVerifiedAtUtc: null,
            Timeline:
            [
                new DesktopSupportCaseTimelineEntry(
                    EventId: "preview-released",
                    Status: "released_to_reporter_channel",
                    Summary: "The preview fix reached the reporter-ready lane for this desktop.",
                    OccurredAtUtc: now.AddHours(-6),
                    Actor: "release automation"),
                new DesktopSupportCaseTimelineEntry(
                    EventId: "preview-routed",
                    Status: "routed",
                    Summary: "The tracked case was routed into the desktop support closure lane.",
                    OccurredAtUtc: now.AddDays(-1),
                    Actor: "support"),
                new DesktopSupportCaseTimelineEntry(
                    EventId: "preview-received",
                    Status: "new",
                    Summary: "Synthetic support case preview created for flagship desktop smoke verification.",
                    OccurredAtUtc: now.AddDays(-2),
                    Actor: "desktop")
            ],
            Attachments: []);
    }

    private string BuildIntro()
    {
        if (_isPreview)
        {
            return S("desktop.support_case.intro.preview");
        }

        if (_supportCase is null)
        {
            return S("desktop.support_case.intro.fallback");
        }

        return _supportProjection.NeedsAttention
            ? S("desktop.support_case.intro.action_needed")
            : S("desktop.support_case.intro.current");
    }

    private string BuildStatus()
    {
        if (_isPreview)
        {
            return S("desktop.support_case.status.preview");
        }

        return _supportCase is null
            ? S("desktop.support_case.status.case_unavailable")
            : S("desktop.support_case.status.current");
    }

    private string BuildSummaryBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _supportProjection.NextSafeAction),
            _supportProjection.Summary
        ];

        if (!string.IsNullOrWhiteSpace(_supportProjection.CaseId))
        {
            lines.Add(F("desktop.support_case.context.case_id", _supportProjection.CaseId));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.StageLabel))
        {
            lines.Add(F(
                "desktop.support_case.context.stage",
                _supportProjection.StageLabel,
                _supportProjection.StatusLabel ?? HumanizeToken(_supportCase?.Status)));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.UpdatedLabel))
        {
            lines.Add(F("desktop.support_case.context.updated", _supportProjection.UpdatedLabel));
        }

        if (_supportCase is not null)
        {
            lines.Add(F("desktop.support_case.context.kind", HumanizeToken(_supportCase.Kind)));
            lines.Add(F("desktop.support_case.context.source", HumanizeToken(_supportCase.Source)));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.InstallReadinessSummary))
        {
            lines.Add(F("desktop.support_case.context.install_readiness", _supportProjection.InstallReadinessSummary));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.FixedReleaseLabel))
        {
            lines.Add(F("desktop.support_case.context.fixed_release", _supportProjection.FixedReleaseLabel));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.AffectedInstallSummary))
        {
            lines.Add(F("desktop.support_case.context.affected_install", _supportProjection.AffectedInstallSummary));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.ReleaseProgressSummary))
        {
            lines.Add(F("desktop.support_case.context.release_progress", _supportProjection.ReleaseProgressSummary));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.VerificationSummary))
        {
            lines.Add(F("desktop.support_case.context.verification", _supportProjection.VerificationSummary));
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.FollowUpLaneSummary))
        {
            lines.Add(F("desktop.support_case.context.follow_up", _supportProjection.FollowUpLaneSummary));
        }

        if (_supportCase is not null && !string.IsNullOrWhiteSpace(_supportCase.Detail))
        {
            lines.Add(string.Empty);
            lines.Add(F("desktop.support_case.context.detail", _supportCase.Detail));
        }

        return string.Join("\n", TrimTrailingBlankLines(lines));
    }

    private string BuildTimelineBody()
    {
        List<string> lines = [];

        if (_supportCase?.Timeline is { Count: > 0 } timeline)
        {
            foreach (DesktopSupportCaseTimelineEntry entry in timeline
                .OrderByDescending(static item => item.OccurredAtUtc)
                .Take(8))
            {
                lines.Add(F(
                    "desktop.support_case.context.timeline_entry",
                    entry.OccurredAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"),
                    HumanizeToken(entry.Status),
                    entry.Summary));

                if (!string.IsNullOrWhiteSpace(entry.Actor))
                {
                    lines.Add(F("desktop.support_case.context.timeline_actor", entry.Actor));
                }
            }
        }
        else
        {
            lines.Add(_supportCase is null
                ? S("desktop.support_case.context.timeline_fallback")
                : S("desktop.support_case.context.timeline_none"));
        }

        if (_supportCase?.Attachments is { Count: > 0 } attachments)
        {
            lines.Add(string.Empty);
            foreach (DesktopSupportCaseAttachment attachment in attachments.Take(3))
            {
                lines.Add(F(
                    "desktop.support_case.context.attachment",
                    attachment.FileName,
                    FormatBytes(attachment.SizeBytes),
                    attachment.UploadedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
            }
        }

        return string.Join("\n", TrimTrailingBlankLines(lines));
    }

    private string BuildDiagnosticsBody()
        => DesktopSupportDiagnosticsText.BuildTrackedCaseDiagnostics(_installState, _updateStatus, _supportProjection, _supportCase);

    private string BuildFollowThroughBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _supportProjection.NextSafeAction)
        ];

        if (_supportProjection.NeedsLinkedInstall)
        {
            lines.Add(S("desktop.support_case.follow_through.link_install"));
        }
        else if (_supportProjection.NeedsInstallUpdate)
        {
            lines.Add(S("desktop.support_case.follow_through.update_install"));
        }
        else if (_supportProjection.FixReadyOnLinkedInstall)
        {
            lines.Add(S("desktop.support_case.follow_through.verify"));
        }
        else if (_supportProjection.NeedsAttention)
        {
            lines.Add(S("desktop.support_case.follow_through.attention"));
        }
        else
        {
            lines.Add(S("desktop.support_case.follow_through.current"));
        }

        lines.Add(F("desktop.support.context.recommended_action", _updateStatus.RecommendedAction));
        lines.Add(F("desktop.home.install_summary.install_id", _installState.InstallationId));

        if (!string.IsNullOrWhiteSpace(_updateStatus.FixAvailabilitySummary))
        {
            lines.Add(F("desktop.support.context.fix_availability", _updateStatus.FixAvailabilitySummary));
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add(F("desktop.support.context.last_error", _updateStatus.LastError));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateSummaryActions()
        =>
        [
            CreatePrimaryFollowThroughButton(isPrimary: true),
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync)
        ];

    private IReadOnlyList<Button> CreateTimelineActions()
    {
        List<Button> actions =
        [
            HasOpenableAttachment
                ? CreateButton(S("desktop.support_case.button.open_attachment"), OpenFirstAttachment, isPrimary: true)
                : CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        return actions;
    }

    private IReadOnlyList<Button> CreateFollowThroughActions()
    {
        List<Button> actions =
        [
            CreatePreferredDesktopActionButton(),
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        return actions;
    }

    private Button CreatePreferredDesktopActionButton()
    {
        if (_supportProjection.NeedsLinkedInstall)
        {
            return CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true);
        }

        if (ShouldUseUpdateAction())
        {
            return CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync, isPrimary: true);
        }

        return CreatePrimaryFollowThroughButton(isPrimary: true);
    }

    private Button CreatePrimaryFollowThroughButton(bool isPrimary = false)
    {
        string label = ResolvePrimaryFollowThroughLabel();
        return CreateButton(label, OpenPrimarySupportFollowThroughAsync, isPrimary: isPrimary);
    }

    private string ResolvePrimaryFollowThroughLabel()
    {
        if (_supportProjection.NeedsLinkedInstall)
        {
            return S("desktop.home.button.open_devices_access");
        }

        if (ShouldUseUpdateAction())
        {
            return S("desktop.home.button.open_update_status");
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionLabel))
        {
            return _supportProjection.PrimaryActionLabel;
        }

        return S("desktop.home.button.open_support_center");
    }

    private bool HasOpenableAttachment
        => _supportCase?.Attachments?.Any(attachment => !string.IsNullOrWhiteSpace(attachment.DownloadHref)) == true;

    private bool ShouldUseUpdateAction()
        => _supportProjection.NeedsInstallUpdate
           || _supportProjection.FixReadyOnLinkedInstall
           || IsDownloadsRoute(_supportProjection.PrimaryActionHref);

    private static bool IsDownloadsRoute(string? href)
        => string.Equals(href?.Trim(), "/downloads", StringComparison.OrdinalIgnoreCase);

    private async Task OpenPrimarySupportFollowThroughAsync()
    {
        if (_supportProjection.NeedsLinkedInstall)
        {
            await OpenDevicesAccessWindowAsync().ConfigureAwait(true);
            return;
        }

        if (ShouldUseUpdateAction())
        {
            await OpenUpdateWindowAsync().ConfigureAwait(true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionHref)
            && !string.Equals(_supportProjection.PrimaryActionHref, _supportProjection.DetailHref, StringComparison.OrdinalIgnoreCase))
        {
            DesktopInstallLinkingRuntime.TryOpenRelativePortal(_supportProjection.PrimaryActionHref!);
            return;
        }

        await OpenSupportWindowAsync().ConfigureAwait(true);
    }

    private bool OpenFirstAttachment()
    {
        string? href = _supportCase?.Attachments?
            .FirstOrDefault(attachment => !string.IsNullOrWhiteSpace(attachment.DownloadHref))
            ?.DownloadHref;
        return !string.IsNullOrWhiteSpace(href)
               && DesktopInstallLinkingRuntime.TryOpenRelativePortal(href);
    }

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenUpdateWindowAsync()
        => DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private async Task RefreshSupportCaseAsync()
    {
        if (_isPreview)
        {
            _statusText.Text = S("desktop.support_case.status.preview");
            return;
        }

        try
        {
            _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
            _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
            (_supportProjection, _supportCase) = await ReadSupportCaseStateAsync(_installState, _supportProjection).ConfigureAwait(true);
        }
        catch
        {
            _statusText.Text = S("desktop.support_case.status.refresh_failed");
            return;
        }

        _introText.Text = BuildIntro();
        _statusText.Text = BuildStatus();
        _summaryText.Text = BuildSummaryBody();
        _timelineText.Text = BuildTimelineBody();
        _diagnosticsText.Text = BuildDiagnosticsBody();
        _followThroughText.Text = BuildFollowThroughBody();
        ResetActionRow(_summaryActionsRow, CreateSummaryActions());
        ResetActionRow(_timelineActionsRow, CreateTimelineActions());
        ResetActionRow(_followThroughActionsRow, CreateFollowThroughActions());
    }

    private static IEnumerable<string> TrimTrailingBlankLines(List<string> lines)
    {
        int lastNonBlankIndex = lines.FindLastIndex(static line => !string.IsNullOrWhiteSpace(line));
        if (lastNonBlankIndex < 0)
        {
            return Array.Empty<string>();
        }

        return lines.Take(lastNonBlankIndex + 1);
    }

    private static string HumanizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        string normalized = value.Trim().Replace('_', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
    }

    private static string FormatBytes(long sizeBytes)
    {
        if (sizeBytes < 1024)
        {
            return $"{sizeBytes} B";
        }

        if (sizeBytes < 1024 * 1024)
        {
            return $"{sizeBytes / 1024d:0.#} KB";
        }

        return $"{sizeBytes / (1024d * 1024d):0.#} MB";
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        StackPanel content = new()
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 18
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
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = content
        };
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
    {
        StackPanel actionRow = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }

        return actionRow;
    }

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
    {
        actionRow.Children.Clear();
        foreach (Button action in actions)
        {
            actionRow.Children.Add(action);
        }
    }

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => CreateButton(
            label,
            () =>
            {
                action();
                return Task.CompletedTask;
            },
            closeWindow,
            isPrimary);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 120
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

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
