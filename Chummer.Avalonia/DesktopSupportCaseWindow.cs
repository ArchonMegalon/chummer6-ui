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
        Width = 780;
        Height = 580;
        MinWidth = 680;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = BuildStatus(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveMutedForegroundBrush()
        };

        _summaryText = new TextBlock
        {
            Text = BuildSummaryBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _timelineText = new TextBlock
        {
            Text = BuildTimelineBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _diagnosticsText = new TextBlock
        {
            Text = BuildDiagnosticsBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _followThroughText = new TextBlock
        {
            Text = BuildFollowThroughBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _summaryActionsRow = CreateActionRow(CreateSummaryActions());
        _timelineActionsRow = CreateActionRow(CreateTimelineActions());
        _followThroughActionsRow = CreateActionRow(CreateFollowThroughActions());

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.support_case.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _introText,
                        _statusText,
                        CreateSection(S("desktop.support_case.section.summary"), _summaryText, _summaryActionsRow),
                        CreateSection(S("desktop.support_case.section.timeline"), _timelineText, _timelineActionsRow),
                        CreateSection(S("desktop.support_case.section.diagnostics"), _diagnosticsText, null),
                        CreateSection(S("desktop.support_case.section.follow_through"), _followThroughText, _followThroughActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.support_case.button.refresh"), RefreshSupportCaseAsync),
                                CreateButton(S("desktop.dialog.action.close"), static () => Task.CompletedTask, closeWindow: true)
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
            Summary: "Tracked case preview.",
            NextSafeAction: "Review update status on this desktop, then return here if you still need help.",
            PrimaryActionLabel: DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_update_status", language),
            PrimaryActionHref: "/downloads",
            DetailHref: "/account/support/preview-support-case",
            InstallReadinessSummary: "This preview copy already includes the fix shown here.",
            StatusLabel: "Released",
            StageLabel: "Released",
            UpdatedLabel: FormatDisplayTime(now),
            FixedReleaseLabel: "preview smoke",
            AffectedInstallSummary: "This preview stays attached to this desktop copy.",
            FollowUpLaneSummary: "Final confirmation returns here when account support is available again.",
            ReleaseProgressSummary: "The preview fix is already included in this desktop release.",
            VerificationSummary: "Use account support to record final confirmation once the account page is available again.",
            HasTrackedCase: true,
            NeedsAttention: true,
            FixReadyOnLinkedInstall: true,
            NeedsInstallUpdate: false,
            NeedsLinkedInstall: false,
            Highlights:
            [
                "Stage: Released (Released)",
                "Closure: The preview fix is already included in this desktop release.",
                "Release progress: The preview fix is already included in this desktop release.",
                "Fix availability: preview smoke is the tracked fix target for this desktop support path.",
                "Confirmation: Use account support to record final fix confirmation once the account page is available again.",
                $"Updated: {FormatDisplayTime(now)}"
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
            Title: "Support case preview",
            Summary: "This preview shows how a tracked support case appears in the desktop app.",
            Detail: "A live account case can replace this preview whenever one is available.",
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
                    Summary: "The preview fix reached this desktop.",
                    OccurredAtUtc: now.AddHours(-6),
                    Actor: "release automation"),
                new DesktopSupportCaseTimelineEntry(
                    EventId: "preview-routed",
                    Status: "routed",
                    Summary: "The tracked case moved into desktop support.",
                    OccurredAtUtc: now.AddDays(-1),
                    Actor: "support"),
                new DesktopSupportCaseTimelineEntry(
                    EventId: "preview-received",
                    Status: "new",
                    Summary: "Local support case preview created for this desktop.",
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
        List<string> lines = [_supportProjection.Summary];

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
                .Take(4))
            {
                lines.Add(F(
                    "desktop.support_case.context.timeline_entry",
                    FormatDisplayTime(entry.OccurredAtUtc),
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
            foreach (DesktopSupportCaseAttachment attachment in attachments.Take(1))
            {
                lines.Add(F(
                    "desktop.support_case.context.attachment",
                    attachment.FileName,
                    FormatBytes(attachment.SizeBytes),
                    FormatDisplayTime(attachment.UploadedAtUtc)));
            }
        }

        return string.Join("\n", TrimTrailingBlankLines(lines));
    }

    private string BuildDiagnosticsBody()
    {
        List<string> lines =
        [
            DesktopSupportDiagnosticsText.BuildTrackedCaseDiagnostics(_installState, _updateStatus, _supportProjection, _supportCase)
        ];
        AppendDiagnosticsDiffLines(
            lines,
            DesktopTrustReceiptText.BuildDiagnosticsDiff(_installState, _updateStatus),
            _supportProjection);
        return string.Join("\n", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private static void AppendDiagnosticsDiffLines(
        List<string> lines,
        IReadOnlyList<string> diagnosticsDiff,
        DesktopHomeSupportProjection supportProjection)
    {
        foreach (string line in diagnosticsDiff)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (supportProjection.NeedsAttention)
        {
            lines.Add("System details stay visible while this case still needs attention.");
        }
    }

    private string BuildFollowThroughBody()
    {
        List<string> lines = [];

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

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(F("desktop.support.context.recommended_action", _updateStatus.RecommendedAction));
        }

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
            CreatePrimaryFollowThroughButton(isPrimary: true)
        ];

    private IReadOnlyList<Button> CreateTimelineActions()
    {
        List<Button> actions =
        [
            HasOpenableAttachment
                ? CreateButton(S("desktop.support_case.button.open_attachment"), OpenFirstAttachment, isPrimary: true)
                : CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        return actions;
    }

    private IReadOnlyList<Button> CreateFollowThroughActions()
    {
        List<Button> actions =
        [
            CreatePreferredDesktopActionButton(),
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

    private static string FormatDisplayTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static Border CreateSection(string title, Control body, Control? actionContent)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 10, cornerRadius: 4, includeHeading: false, spacing: 0);

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 6);

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
        => DesktopShellTheme.ResetActionRow(actionRow, actions);

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
