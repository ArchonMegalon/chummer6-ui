using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopUpdateWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private readonly TextBlock _introText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _currentText;
    private readonly TextBlock _followThroughText;
    private readonly TextBlock _installText;
    private readonly WrapPanel _currentActionsRow;
    private readonly WrapPanel _followThroughActionsRow;
    private readonly WrapPanel _installActionsRow;
    private readonly Border _statusBanner;
    private readonly TextBlock _statusBannerTitleText;
    private readonly TextBlock _statusBannerBodyText;
    private bool _isChecking;

    private DesktopUpdateWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;

        Title = S("desktop.update.title");
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
            Text = BuildStatusText(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _statusBannerTitleText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        _statusBannerBodyText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap
        };

        _statusBanner = DesktopShellTheme.CreateUtilityPanel(
            new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    _statusBannerTitleText,
                    _statusBannerBodyText
                }
            },
            padding: 10,
            cornerRadius: 6);

        _currentText = new TextBlock
        {
            Text = BuildCurrentBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _followThroughText = new TextBlock
        {
            Text = BuildFollowThroughBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _installText = new TextBlock
        {
            Text = BuildInstallBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _currentActionsRow = CreateActionRow(CreateCurrentActions());
        _followThroughActionsRow = CreateActionRow(CreateFollowThroughActions());
        _installActionsRow = CreateActionRow(CreateInstallActions());

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
                            Text = S("desktop.update.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _statusBanner,
                        _introText,
                        _statusText,
                        CreateSection(S("desktop.update.section.current"), _currentText, _currentActionsRow),
                        CreateSection(S("desktop.update.section.follow_through"), _followThroughText, _followThroughActionsRow),
                        CreateSection(S("desktop.update.section.install"), _installText, _installActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.update.button.refresh"), RefreshUpdateStateAsync),
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

        DesktopUpdateWindow dialog = Create(headId);
        await dialog.ShowDialog(owner);
    }

    private static DesktopUpdateWindow Create(string headId)
    {
        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(installState.HeadId);

        return new DesktopUpdateWindow(installState, updateStatus, preferences);
    }

    private string BuildIntro()
    {
        return _updateStatus.Status switch
        {
            "disabled" => S("desktop.update.intro.disabled"),
            "update_staged" => "The next build is already staged for this install. Chummer should finish the in-place update and relaunch on the newer build.",
            "update_available" => S("desktop.update.intro.available"),
            "attention_required" => S("desktop.update.intro.attention"),
            "never_checked" => S("desktop.update.intro.never_checked"),
            _ => S("desktop.update.intro.current")
        };
    }

    private string BuildStatusText()
    {
        List<string> lines =
        [
            F("desktop.update.mode", FormatUpdateMode(_updateStatus.UpdateMode)),
            F("desktop.update.updates_enabled", _updateStatus.UpdatesEnabled),
            F("desktop.update.manifest_location", _updateStatus.ManifestLocation)
        ];

        if (_updateStatus.LastCheckedAtUtc is not null)
        {
            lines.Add(F(
                "desktop.update.last_checked",
                _updateStatus.LastCheckedAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        return string.Join("\n", lines);
    }

    private string BuildCurrentBody()
    {
        List<string> lines =
        [
            $"Installed now: {_updateStatus.InstalledVersion}",
            $"Latest available: {(_updateStatus.LastManifestVersion ?? S("desktop.home.value.unknown"))}"
        ];

        if (string.Equals(_updateStatus.Status, "update_staged", StringComparison.Ordinal))
        {
            lines.Add("Update state: staged for in-place install and relaunch.");
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(_updateStatus.RecommendedAction);
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"Something still needs attention: {_updateStatus.LastError}");
        }

        return string.Join("\n", lines);
    }

    private string BuildFollowThroughBody()
    {
        List<string> lines = [];

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(F("desktop.home.next_safe_action", _updateStatus.RecommendedAction));
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.PendingUpdateVersion))
        {
            lines.Add(F(
                "desktop.update.pending_update",
                _updateStatus.PendingUpdateVersion,
                string.IsNullOrWhiteSpace(_updateStatus.PendingUpdateChannelId) ? _updateStatus.ChannelId : _updateStatus.PendingUpdateChannelId));

            if (_updateStatus.AutoApply)
            {
                lines.Add("Full auto-update is on. Chummer should install the staged build in place and relaunch without another prompt.");
            }
            else if (string.Equals(_updateStatus.UpdateMode, "notify", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("Notify-only mode is on. Chummer will tell you about newer builds without installing them automatically.");
            }
        }
        else
        {
            lines.Add(S("desktop.update.no_pending_update"));
        }

        if (_updateStatus.LastUpdateLaunchAttemptAtUtc is not null)
        {
            lines.Add(F(
                "desktop.update.last_launch_attempt",
                _updateStatus.LastUpdateLaunchAttemptAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        if (_updateStatus.RollbackWindowStartedAtUtc is not null
            && _updateStatus.RollbackWindowExpiresAtUtc is not null)
        {
            lines.Add(F(
                "desktop.update.rollback_window",
                _updateStatus.RollbackWindowStartedAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm"),
                _updateStatus.RollbackWindowExpiresAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        return string.Join("\n", lines);
    }

    private string BuildInstallBody()
    {
        List<string> lines =
        [
            $"This desktop copy: {_installState.Platform}/{_installState.Arch}",
            $"Installed version: {_installState.ApplicationVersion} on {_installState.ChannelId}"
        ];

        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? $"Account link stays active until {_installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown")} UTC."
                : "This copy is not linked to an account yet.");

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add($"Linking still needs attention: {_installState.LastClaimError}");
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateCurrentActions()
        =>
        [
            CreateButton(S("desktop.update.button.check_now"), CheckForUpdatesAsync, isPrimary: true)
        ];

    private IReadOnlyList<Button> CreateFollowThroughActions()
        =>
        [
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync),
            CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport)
        ];

    private IReadOnlyList<Button> CreateInstallActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true)
                : CreateButton(DesktopDevicesAccessWindow.BuildInstallLinkEntryButtonLabel(_installState, _preferences.Language), OpenInstallLinkingAsync, isPrimary: true)
        ];
        actions.Add(CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language),
            static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal()));

        return actions;
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_isChecking)
        {
            return;
        }

        _isChecking = true;
        _statusText.Text = S("desktop.update.checking");
        try
        {
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                _installState.HeadId,
                [],
                CancellationToken.None).ConfigureAwait(true);

            _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
            _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
            RefreshTextAndActions();

            if (result.ExitRequested)
            {
                _statusText.Text = S("desktop.update.apply_scheduled");
                _statusBannerTitleText.Text = "Installing update";
                _statusBannerBodyText.Text = "Chummer is closing this build, applying the staged update in place, and relaunching the newer build.";
                _statusBanner.Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellChromeAccentBrush", "#DEE8F6");
                _statusBanner.BorderBrush = DesktopShellTheme.ResolveThemeBrush("ChummerShellActiveMenuBorderBrush", "#60A5FA");
                await Task.Delay(1200).ConfigureAwait(true);
                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                else
                {
                    Close();
                }

                return;
            }

            _statusText.Text = F("desktop.update.checked", result.Reason);
        }
        finally
        {
            _isChecking = false;
        }
    }

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_update");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this);
        await RefreshUpdateStateAsync();
    }

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenDevicesAccessWindowAsync()
        => DesktopDevicesAccessWindow.ShowAsync(this, _installState.HeadId);

    private bool OpenUpdateSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForUpdate(_installState, _updateStatus);

    private bool OpenInstallSupport()
        => DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_installState);

    private Task RefreshUpdateStateAsync()
    {
        _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
        _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
        RefreshTextAndActions();
        return Task.CompletedTask;
    }

    private void RefreshTextAndActions()
    {
        _introText.Text = BuildIntro();
        _statusText.Text = BuildStatusText();
        _currentText.Text = BuildCurrentBody();
        _followThroughText.Text = BuildFollowThroughBody();
        _installText.Text = BuildInstallBody();
        ApplyStatusBanner();
        ResetActionRow(_currentActionsRow, CreateCurrentActions());
        ResetActionRow(_followThroughActionsRow, CreateFollowThroughActions());
        ResetActionRow(_installActionsRow, CreateInstallActions());
    }

    private void ApplyStatusBanner()
    {
        (string title, string body, string brushKey, string fallback) = _updateStatus.Status switch
        {
            "update_staged" => (
                "Update staged",
                "A newer build is already staged for this install. Chummer should install it in place and relaunch on the next step.",
                "ChummerShellChromeAccentBrush",
                "#DEE8F6"),
            "update_available" => (
                "Update available",
                "A newer published build is available for this desktop. Check now or let the release path move this install forward.",
                "ChummerShellSelectionPanelBrush",
                "#F8FAFC"),
            "attention_required" => (
                "Needs attention",
                "Update, release, or rollout status needs review before this install is treated as current.",
                "ChummerShellSelectionPanelBrush",
                "#F8FAFC"),
            "disabled" => (
                "Updater disabled",
                "This install is not currently attached to a working update source.",
                "ChummerShellSelectionPanelBrush",
                "#F8FAFC"),
            _ => (
                "Current build",
                "This install currently matches the latest known release for its configured update path.",
                "ChummerShellSurfaceAltBrush",
                "#F2F5FA")
        };

        _statusBannerTitleText.Text = title;
        _statusBannerBodyText.Text = body;
        _statusBanner.Background = DesktopShellTheme.ResolveThemeBrush(brushKey, fallback);
        _statusBanner.BorderBrush = string.Equals(_updateStatus.Status, "update_staged", StringComparison.Ordinal)
            ? DesktopShellTheme.ResolveThemeBrush("ChummerShellActiveMenuBorderBrush", "#60A5FA")
            : DesktopShellTheme.ResolveThemeBrush("ChummerShellBorderBrush", "#B5C0CF");
    }

    private static string FormatUpdateMode(string? updateMode)
        => updateMode?.Trim().ToLowerInvariant() switch
        {
            "full" => "full auto-update",
            "notify" => "notify only",
            "off" => "off",
            _ => "unknown"
        };

    private static Border CreateSection(string title, Control body, Control? actionContent)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 10, cornerRadius: 4);

    private static WrapPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateWrapActionRow(actions, new Thickness(0, 0, 6, 6));

    private static void ResetActionRow(WrapPanel actionRow, IReadOnlyList<Button> actions)
        => DesktopShellTheme.ResetActionRow(actionRow, actions, new Thickness(0, 0, 6, 6));

    private static Button CreateButton(string label, Func<bool> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 92);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
