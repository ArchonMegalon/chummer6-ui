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
    private readonly TextBlock _followThroughText;
    private readonly StackPanel _caseActionsRow;
    private readonly StackPanel _releaseActionsRow;
    private readonly StackPanel _followThroughActionsRow;

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
        Width = 840;
        Height = 620;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = S("desktop.support.status.current"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _caseText = new TextBlock
        {
            Text = BuildCaseBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _releaseText = new TextBlock
        {
            Text = BuildReleaseBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _followThroughText = new TextBlock
        {
            Text = BuildFollowThroughBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _caseActionsRow = CreateActionRow(CreateCaseActions());
        _releaseActionsRow = CreateActionRow(CreateReleaseActions());
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
                            Text = S("desktop.support.heading"),
                            FontSize = 24,
                            FontWeight = FontWeight.SemiBold
                        },
                        _introText,
                        _statusText,
                        CreateSection(S("desktop.support.section.case"), _caseText, _caseActionsRow),
                        CreateSection(S("desktop.support.section.release"), _releaseText, _releaseActionsRow),
                        CreateSection(S("desktop.support.section.follow_through"), _followThroughText, _followThroughActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.support.button.refresh"), RefreshSupportStateAsync),
                                CreateButton(S("desktop.home.button.continue"), static () => Task.CompletedTask, closeWindow: true)
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
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _supportProjection.NextSafeAction),
            _supportProjection.Summary
        ];

        foreach (string highlight in _supportProjection.Highlights)
        {
            lines.Add(highlight);
        }

        return string.Join("\n", lines);
    }

    private string BuildReleaseBody()
    {
        List<string> lines =
        [
            F("desktop.support.context.release_status", _updateStatus.Status),
            F("desktop.support.context.recommended_action", _updateStatus.RecommendedAction),
            F("desktop.home.install_summary.install_id", _installState.InstallationId),
            F("desktop.home.install_summary.version", _installState.ApplicationVersion),
            F("desktop.home.install_summary.channel", _installState.ChannelId),
            F("desktop.home.install_summary.platform", _installState.Platform, _installState.Arch)
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.KnownIssueSummary))
        {
            lines.Add(F("desktop.support.context.known_issues", _updateStatus.KnownIssueSummary));
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
                actions.Add(CreateButton(S("desktop.home.button.open_tracked_case"), OpenTrackedSupportCase));
            }

            return actions;
        }

        return
        [
            CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport, isPrimary: true),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        ];
    }

    private IReadOnlyList<Button> CreateReleaseActions()
        =>
        [
            CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        ];

    private IReadOnlyList<Button> CreateFollowThroughActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true)
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync),
            CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
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
        _followThroughText.Text = BuildFollowThroughBody();
        ResetActionRow(_caseActionsRow, CreateCaseActions());
        ResetActionRow(_releaseActionsRow, CreateReleaseActions());
        ResetActionRow(_followThroughActionsRow, CreateFollowThroughActions());
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
