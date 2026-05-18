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
    private readonly StackPanel _currentActionsRow;
    private readonly StackPanel _followThroughActionsRow;
    private readonly StackPanel _installActionsRow;
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
            Foreground = Brushes.DarkSlateGray
        };

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
            $"State: {_updateStatus.Status} · Installed {_updateStatus.InstalledVersion}",
            $"Latest: {(_updateStatus.LastManifestVersion ?? S("desktop.home.value.unknown"))}"
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.RecommendedAction))
        {
            lines.Add(_updateStatus.RecommendedAction);
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"Issue: {_updateStatus.LastError}");
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
            $"{_installState.HeadId} · {_installState.Platform}/{_installState.Arch}",
            $"Version {_installState.ApplicationVersion} · {_installState.ChannelId}"
        ];

        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? $"Linked until {_installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown")} UTC."
                : "This copy is not linked yet.");

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add($"Claim issue: {_installState.LastClaimError}");
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
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true)
        ];

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
        ResetActionRow(_currentActionsRow, CreateCurrentActions());
        ResetActionRow(_followThroughActionsRow, CreateFollowThroughActions());
        ResetActionRow(_installActionsRow, CreateInstallActions());
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        ToolTip.SetTip(body, title);
        StackPanel content = new() { Spacing = 0 };

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
            MinWidth = 92
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
