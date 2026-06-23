using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopSupportWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private DesktopHomeSupportProjection _supportProjection;
    private readonly TextBlock _introText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _caseText;
    private readonly TextBlock _releaseText;
    private readonly TextBlock _diagnosticsText;
    private readonly TextBlock _followThroughText;
    private readonly WrapPanel _caseActionsRow;
    private readonly WrapPanel _releaseActionsRow;
    private readonly WrapPanel _followThroughActionsRow;

    private DesktopSupportWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences,
        DesktopHomeSupportProjection supportProjection)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _supportProjection = supportProjection;

        Title = S("desktop.support.title");
        Width = 760;
        Height = 560;
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
            Text = S("desktop.support.status.current"),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _caseText = new TextBlock
        {
            Text = BuildCaseBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _releaseText = new TextBlock
        {
            Text = BuildReleaseBody(),
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

        _caseActionsRow = CreateActionRow(CreateCaseActions());
        _releaseActionsRow = CreateActionRow(CreateReleaseActions());
        _followThroughActionsRow = CreateActionRow(CreateFollowThroughActions());

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
                            Text = S("desktop.support.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _introText,
                        _statusText,
                        CreateSection(S("desktop.support.section.case"), _caseText, _caseActionsRow),
                        CreateSection(S("desktop.support.section.release"), _releaseText, _releaseActionsRow),
                        CreateSection(S("desktop.support.section.diagnostics"), _diagnosticsText, null),
                        CreateSection(S("desktop.support.section.follow_through"), _followThroughText, _followThroughActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.support.button.refresh"), RefreshSupportStateAsync),
                                CreateButton(S("desktop.dialog.action.close"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopSupportWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopSupportWindow> CreateAsync(string headId)
    {
        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(installState.HeadId);
        DesktopHomeSupportProjection supportProjection = await ReadSupportProjectionAsync(installState).ConfigureAwait(true);

        return new DesktopSupportWindow(installState, updateStatus, preferences, supportProjection);
    }

    private static async Task<DesktopHomeSupportProjection> ReadSupportProjectionAsync(DesktopInstallLinkingState installState)
    {
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop support requires an IChummerClient instance."));
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
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            return S("desktop.support.intro.guest");
        }

        if (!_supportProjection.HasTrackedCase)
        {
            return S("desktop.support.intro.quiet");
        }

        return _supportProjection.NeedsAttention
            ? S("desktop.support.intro.action_needed")
            : S("desktop.support.intro.tracked");
    }

    private string BuildCaseBody()
    {
        List<string> lines = [_supportProjection.Summary];
        if (_supportProjection.HasTrackedCase)
        {
            lines.Add("You already have a tracked support case. Open it to pick up where you left off.");
        }

        string? highlight = _supportProjection.Highlights.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(highlight))
        {
            lines.Add(highlight);
        }

        return string.Join("\n", lines);
    }

    private string BuildReleaseBody()
    {
        List<string> lines =
        [
            $"Update status: {_updateStatus.Status}",
            $"You are using version {_installState.ApplicationVersion} on the {_installState.ChannelId} channel."
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(_updateStatus.RecommendedAction);
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"Something still needs attention: {_updateStatus.LastError}");
        }

        return string.Join("\n", lines.Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private string BuildDiagnosticsBody()
    {
        List<string> lines =
        [
            DesktopSupportDiagnosticsText.BuildSupportCenterDiagnostics(_installState, _updateStatus, _supportProjection)
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
            lines.Add("This diagnostics summary stays available while the support task still needs attention.");
        }
    }

    private string BuildFollowThroughBody()
    {
        List<string> lines =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? S("desktop.support.follow_through.claimed")
                : S("desktop.support.follow_through.guest")
        ];

        if (_supportProjection.NeedsAttention)
        {
            lines.Add(S("desktop.support.follow_through.attention"));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateCaseActions()
    {
        if (_supportProjection.HasTrackedCase)
        {
            List<Button> actions =
            [
                CreateButton(_supportProjection.PrimaryActionLabel ?? S("desktop.home.button.open_tracked_case"), OpenPrimarySupportFollowThrough, isPrimary: true)
            ];

            if (!string.IsNullOrWhiteSpace(_supportProjection.DetailHref)
                && !string.Equals(_supportProjection.DetailHref, _supportProjection.PrimaryActionHref, StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(CreateButton("View case timeline", OpenTrackedSupportCase));
            }

            return actions;
        }

        return
        [
            CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport, isPrimary: true)
        ];
    }

    private IReadOnlyList<Button> CreateReleaseActions()
        =>
        [
            CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport)
        ];

    private IReadOnlyList<Button> CreateFollowThroughActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true)
                : CreateButton(DesktopDevicesAccessWindow.BuildInstallLinkEntryButtonLabel(_installState, _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync)
        ];

        return actions;
    }

    private Task OpenUpdateWindowAsync()
        => DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private bool OpenInstallSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState);

    private bool OpenUpdateSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus);

    private Task OpenTrackedSupportCase()
        => _supportProjection.HasTrackedCase
           ? DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection)
           : Task.CompletedTask;

    private Task OpenPrimarySupportFollowThrough()
    {
        if (IsDownloadsRoute(_supportProjection.PrimaryActionHref))
        {
            return DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);
        }

        if (_supportProjection.HasTrackedCase)
        {
            return DesktopSupportCaseWindow.ShowAsync(this, _installState.HeadId, _supportProjection);
        }

        if (!string.IsNullOrWhiteSpace(_supportProjection.PrimaryActionHref))
        {
            DesktopInstallLinkingRuntime.TryOpenRelativePortal(_supportProjection.PrimaryActionHref!);
        }

        return Task.CompletedTask;
    }

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_support");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
        await RefreshSupportStateAsync();
    }

    private static bool IsDownloadsRoute(string? href)
        => string.Equals(href?.Trim(), "/downloads", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshSupportStateAsync()
    {
        try
        {
            _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
            _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
            _supportProjection = await ReadSupportProjectionAsync(_installState).ConfigureAwait(true);
        }
        catch
        {
            _statusText.Text = S("desktop.support.status.refresh_failed");
            return;
        }

        _introText.Text = BuildIntro();
        _statusText.Text = S("desktop.support.status.current");
        _caseText.Text = BuildCaseBody();
        _releaseText.Text = BuildReleaseBody();
        _diagnosticsText.Text = BuildDiagnosticsBody();
        _followThroughText.Text = BuildFollowThroughBody();
        ResetActionRow(_caseActionsRow, CreateCaseActions());
        ResetActionRow(_releaseActionsRow, CreateReleaseActions());
        ResetActionRow(_followThroughActionsRow, CreateFollowThroughActions());
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 10, cornerRadius: 4);

    private static WrapPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateWrapActionRow(actions, new Thickness(0, 0, 6, 6));

    private static void ResetActionRow(WrapPanel actionRow, IReadOnlyList<Button> actions)
        => DesktopShellTheme.ResetActionRow(actionRow, actions, new Thickness(0, 0, 6, 6));

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
