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
            Text = BuildStatusText(),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _currentText = new TextBlock
        {
            Text = BuildCurrentBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _followThroughText = new TextBlock
        {
            Text = BuildFollowThroughBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _installText = new TextBlock
        {
            Text = BuildInstallBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _currentActionsRow = CreateActionRow(CreateCurrentActions());
        _followThroughActionsRow = CreateActionRow(CreateFollowThroughActions());
        _installActionsRow = CreateActionRow(CreateInstallActions());

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
                            Text = S("desktop.update.heading"),
                            FontSize = 24,
                            FontWeight = FontWeight.SemiBold
                        },
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
        => DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.home.update_summary",
            _preferences.Language,
            _updateStatus.Status,
            _updateStatus.InstalledVersion,
            string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion) ? S("desktop.home.value.unknown") : _updateStatus.LastManifestVersion!,
            _updateStatus.LastManifestPublishedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown"),
            _updateStatus.ChannelId,
            _updateStatus.LastCheckedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.never"),
            _updateStatus.AutoApply,
            string.IsNullOrWhiteSpace(_updateStatus.RolloutState) ? S("desktop.home.value.unknown") : _updateStatus.RolloutState!,
            string.IsNullOrWhiteSpace(_updateStatus.RolloutReason) ? S("desktop.home.value.none") : _updateStatus.RolloutReason!,
            string.IsNullOrWhiteSpace(_updateStatus.SupportabilityState) ? S("desktop.home.value.unknown") : _updateStatus.SupportabilityState!,
            string.IsNullOrWhiteSpace(_updateStatus.SupportabilitySummary) ? S("desktop.home.value.no_supportability_summary") : _updateStatus.SupportabilitySummary!,
            string.IsNullOrWhiteSpace(_updateStatus.ProofStatus) ? S("desktop.home.value.unknown") : _updateStatus.ProofStatus!,
            _updateStatus.ProofGeneratedAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown"),
            string.IsNullOrWhiteSpace(_updateStatus.KnownIssueSummary) ? S("desktop.home.value.none_published") : _updateStatus.KnownIssueSummary!,
            string.IsNullOrWhiteSpace(_updateStatus.FixAvailabilitySummary) ? S("desktop.home.value.no_fix_guidance") : _updateStatus.FixAvailabilitySummary!,
            _updateStatus.RecommendedAction,
            string.IsNullOrWhiteSpace(_updateStatus.LastError) ? S("desktop.home.value.none") : _updateStatus.LastError!);

    private string BuildFollowThroughBody()
    {
        List<string> lines =
        [
            F("desktop.home.next_safe_action", _updateStatus.RecommendedAction)
        ];

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
            F("desktop.home.install_summary.install_id", _installState.InstallationId),
            F("desktop.home.install_summary.head", _installState.HeadId),
            F("desktop.home.install_summary.version", _installState.ApplicationVersion),
            F("desktop.home.install_summary.channel", _installState.ChannelId),
            F("desktop.home.install_summary.platform", _installState.Platform, _installState.Arch)
        ];

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            lines.Add(F(
                "desktop.home.install_summary.linked_status",
                _installState.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? S("desktop.home.value.unknown")));
        }
        else
        {
            lines.Add(S("desktop.home.install_summary.unlinked_status"));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimMessage))
        {
            lines.Add(F("desktop.home.install_summary.hub_message", _installState.LastClaimMessage));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add(F("desktop.home.install_summary.claim_error", _installState.LastClaimError));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateCurrentActions()
        =>
        [
            CreateButton(S("desktop.update.button.check_now"), CheckForUpdatesAsync, isPrimary: true),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        ];

    private IReadOnlyList<Button> CreateFollowThroughActions()
        =>
        [
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync),
            CreateButton(S("desktop.home.button.open_update_support"), OpenUpdateSupport),
            CreateButton(S("desktop.home.button.open_install_support"), OpenInstallSupport)
        ];

    private IReadOnlyList<Button> CreateInstallActions()
    {
        List<Button> actions =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_devices_access"), OpenDevicesAccessWindowAsync, isPrimary: true)
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language), static () => DesktopInstallLinkingRuntime.TryOpenAccountPortal())
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
