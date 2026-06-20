using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Campaign.Contracts;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopDevicesAccessWindow : Window
{
    private DesktopInstallLinkingState _installState;
    private DesktopUpdateClientStatus _updateStatus;
    private DesktopPreferenceState _preferences;
    private DesktopInstallLinkingSummaryProjection _installLinkingSummary;
    private AccountCampaignSummary? _campaignSummary;
    private readonly TextBlock _introText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _guidedPreferenceStatusText;
    private readonly RadioButton _guidedToolsRadioButton;
    private readonly RadioButton _manualInterfaceRadioButton;
    private readonly TextBlock _currentText;
    private readonly TextBlock _devicesText;
    private readonly TextBlock _claimsText;
    private readonly TextBlock _accessText;
    private readonly StackPanel _currentActionsRow;
    private readonly StackPanel _devicesActionsRow;
    private readonly StackPanel _claimsActionsRow;
    private readonly StackPanel _accessActionsRow;

    private DesktopDevicesAccessWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences,
        DesktopInstallLinkingSummaryProjection installLinkingSummary,
        AccountCampaignSummary? campaignSummary)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;
        _installLinkingSummary = installLinkingSummary;
        _campaignSummary = campaignSummary;

        Title = S("desktop.devices.title");
        Width = 900;
        Height = 680;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _introText = new TextBlock
        {
            Text = BuildIntro(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            Text = S("desktop.devices.status.current"),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _guidedPreferenceStatusText = new TextBlock
        {
            Text = DesktopInstallLinkingWindow.BuildGuidedToolsPreferenceStatus(_preferences, _preferences.Language),
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _guidedToolsRadioButton = CreateGuidedFeatureRadioButton(
            name: "DevicesAccessGuidedToolsVisibleOption",
            label: S("desktop.install_link.preference.visible_choice"),
            isChecked: !_preferences.DisableAiFeatures,
            disableAiFeatures: false);
        _manualInterfaceRadioButton = CreateGuidedFeatureRadioButton(
            name: "DevicesAccessGuidedToolsHiddenOption",
            label: S("desktop.install_link.preference.hidden_choice"),
            isChecked: _preferences.DisableAiFeatures,
            disableAiFeatures: true);

        _currentText = new TextBlock
        {
            Text = BuildCurrentBody(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap
        };

        _devicesText = new TextBlock
        {
            Text = BuildDevicesBody(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap
        };

        _claimsText = new TextBlock
        {
            Text = BuildClaimsBody(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap
        };

        _accessText = new TextBlock
        {
            Text = BuildAccessBody(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap
        };

        _currentActionsRow = CreateActionRow(CreateCurrentActions());
        _devicesActionsRow = CreateActionRow(CreateDevicesActions());
        _claimsActionsRow = CreateActionRow(CreateClaimsActions());
        _accessActionsRow = CreateActionRow(CreateAccessActions());

        Content = new ScrollViewer
        {
            Content = DesktopShellTheme.CreateWindowSurface(
                new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.devices.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        _introText,
                        _statusText,
                        CreateGuidedToolsPreferenceSection(),
                        CreateSection(S("desktop.devices.section.current"), S("desktop.devices.section.current_description"), _currentText, _currentActionsRow),
                        CreateSection(S("desktop.devices.section.claimed"), S("desktop.devices.section.claimed_description"), _devicesText, _devicesActionsRow),
                        CreateSection(S("desktop.devices.section.claims"), S("desktop.devices.section.claims_description"), _claimsText, _claimsActionsRow),
                        CreateSection(S("desktop.devices.section.follow_through"), S("desktop.devices.section.follow_through_description"), _accessText, _accessActionsRow),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.devices.button.reload"), RefreshDevicesAccessStateAsync),
                                CreateButton(S("desktop.dialog.action.close"), static () => Task.CompletedTask, closeWindow: true)
                            }
                        }
                    }
                })
        };
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopDevicesAccessWindow dialog = await CreateAsync(headId).ConfigureAwait(true);
        await dialog.ShowDialog(owner);
    }

    private static async Task<DesktopDevicesAccessWindow> CreateAsync(string headId)
    {
        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(installState.HeadId);
        (DesktopInstallLinkingSummaryProjection installLinkingSummary, AccountCampaignSummary? campaignSummary) = await ReadAccountStateAsync().ConfigureAwait(true);

        return new DesktopDevicesAccessWindow(installState, updateStatus, preferences, installLinkingSummary, campaignSummary);
    }

    private static async Task<(DesktopInstallLinkingSummaryProjection InstallLinkingSummary, AccountCampaignSummary? CampaignSummary)> ReadAccountStateAsync()
    {
        try
        {
            IChummerClient client = (IChummerClient)(App.Services?.GetService(typeof(IChummerClient))
                ?? throw new InvalidOperationException("Desktop devices and access requires an IChummerClient instance."));
            Task<DesktopInstallLinkingSummaryProjection> installLinkingTask = client.GetDesktopInstallLinkingSummaryAsync(CancellationToken.None);
            Task<AccountCampaignSummary?> campaignSummaryTask = client.GetAccountCampaignSummaryAsync(CancellationToken.None);
            await Task.WhenAll(installLinkingTask, campaignSummaryTask).ConfigureAwait(false);
            return (installLinkingTask.Result, campaignSummaryTask.Result);
        }
        catch
        {
            return (DesktopInstallLinkingSummaryProjection.Empty, null);
        }
    }

    private string BuildIntro()
    {
        if (ClaimedRestoreDevices.Count > 1 || _installLinkingSummary.ClaimedInstallations.Count > 1)
        {
            return S("desktop.devices.intro.claimed_multi");
        }

        if (DesktopInstallLinkingRuntime.IsClaimed(_installState))
        {
            return S("desktop.devices.intro.claimed_single");
        }

        if (LatestPendingClaim is not null)
        {
            return S("desktop.devices.intro.pending");
        }

        return S("desktop.devices.intro.guest");
    }

    private string BuildCurrentBody()
    {
        List<string> lines =
        [
            F("desktop.install_link.summary.installation_id", _installState.InstallationId),
            F("desktop.install_link.summary.head", _installState.HeadId),
            F("desktop.install_link.summary.version", _installState.ApplicationVersion),
            F("desktop.install_link.summary.channel", _installState.ChannelId),
            F("desktop.install_link.summary.platform", _installState.Platform, _installState.Arch),
            F("desktop.install_link.summary.status", _installState.Status),
            S("desktop.devices.context.current_local")
        ];

        if (CurrentClaimedInstallation is not null)
        {
            lines.Add(F(
                "desktop.devices.context.current_account_match",
                CurrentClaimedInstallation.HostLabel ?? CurrentClaimedInstallation.InstallationId,
                CurrentClaimedInstallation.Platform ?? _installState.Platform,
                CurrentClaimedInstallation.HeadId ?? _installState.HeadId,
                FormatDisplayTime(CurrentClaimedInstallation.UpdatedAtUtc)));
        }
        else
        {
            lines.Add(S("desktop.devices.context.current_unlinked"));
        }

        if (CurrentGrant is not null)
        {
            lines.Add(F(
                "desktop.devices.context.current_grant",
                CurrentGrant.GrantId,
                FormatDisplayTime(CurrentGrant.ExpiresAtUtc)));
        }
        else if (_installState.GrantExpiresAtUtc is not null)
        {
            lines.Add(F("desktop.install_link.summary.linked_status", FormatDisplayTime(_installState.GrantExpiresAtUtc.Value)));
        }

        if (_installState.LastClaimAttemptUtc is not null)
        {
            lines.Add(F("desktop.install_link.summary.last_claim_attempt", FormatDisplayTime(_installState.LastClaimAttemptUtc.Value)));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimMessage))
        {
            lines.Add(F("desktop.install_link.summary.hub_message", _installState.LastClaimMessage));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimError))
        {
            lines.Add(F("desktop.install_link.summary.claim_error", _installState.LastClaimError));
        }

        lines.AddRange(DesktopSurfacePostureText.BuildLines(_updateStatus));
        return string.Join("\n", lines);
    }

    private string BuildDevicesBody()
    {
        if (ClaimedRestoreDevices.Count > 0)
        {
            List<string> lines = [];
            foreach (ClaimedDeviceRestoreProjection device in ClaimedRestoreDevices.Take(4))
            {
                lines.Add(F(
                    "desktop.devices.context.claimed_device",
                    device.HostLabel ?? device.InstallationId,
                    device.DeviceRole,
                    device.Platform,
                    device.HeadId,
                    device.Channel));
                lines.Add(F("desktop.devices.context.claimed_restore", device.RestoreSummary));
                lines.Add(string.Empty);
            }

            return string.Join("\n", TrimTrailingBlankLines(lines));
        }

        if (_installLinkingSummary.ClaimedInstallations.Count > 0)
        {
            return string.Join(
                "\n",
                _installLinkingSummary.ClaimedInstallations
                    .Take(4)
                    .Select(installation => F(
                        "desktop.devices.context.claimed_fallback",
                        installation.HostLabel ?? installation.InstallationId,
                        installation.Channel,
                        installation.Version,
                        installation.Platform ?? "unknown",
                        installation.HeadId ?? "desktop")));
        }

        return S("desktop.devices.context.claimed_none");
    }

    private string BuildClaimsBody()
    {
        List<string> lines = [];

        foreach (DesktopPendingClaimTicket ticket in _installLinkingSummary.PendingClaimTickets.Take(3))
        {
            lines.Add(
                string.IsNullOrWhiteSpace(ticket.InstallationId)
                    ? F(
                        "desktop.devices.context.claims_pending",
                        ticket.ClaimCode,
                        ticket.Channel,
                        ticket.Version,
                        FormatDisplayTime(ticket.ExpiresAtUtc))
                    : F(
                        "desktop.devices.context.claims_pending_install",
                        ticket.ClaimCode,
                        ticket.InstallationId,
                        FormatDisplayTime(ticket.ExpiresAtUtc)));
        }

        if (!string.IsNullOrWhiteSpace(_installState.LastClaimCode)
            && !_installLinkingSummary.PendingClaimTickets.Any(ticket => string.Equals(ticket.ClaimCode, _installState.LastClaimCode, StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add(F("desktop.devices.context.claims_local_last", _installState.LastClaimCode));
        }

        foreach (DesktopRecentInstallReceipt receipt in _installLinkingSummary.RecentReceipts.Take(3))
        {
            lines.Add(F(
                "desktop.devices.context.claims_receipt",
                receipt.ArtifactLabel,
                receipt.Channel,
                receipt.Version,
                receipt.Platform,
                receipt.Arch,
                FormatDisplayTime(receipt.IssuedAtUtc)));
        }

        return lines.Count == 0
            ? S("desktop.devices.context.claims_none")
            : string.Join("\n", lines);
    }

    private string BuildAccessBody()
    {
        List<string> lines =
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? S("desktop.devices.context.access_claimed")
                : S("desktop.devices.context.access_guest")
        ];

        if (_installLinkingSummary.ActiveGrants.Count == 0)
        {
            lines.Add(S("desktop.devices.context.access_no_grants"));
            return string.Join("\n", lines);
        }

        foreach (DesktopInstallationGrantProjection grant in _installLinkingSummary.ActiveGrants.Take(4))
        {
            lines.Add(F(
                "desktop.devices.context.access_grant",
                grant.GrantId,
                grant.InstallationId,
                grant.Status,
                FormatDisplayTime(grant.ExpiresAtUtc)));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateCurrentActions()
        =>
        [
            DesktopInstallLinkingRuntime.IsClaimed(_installState)
                ? CreateButton(S("desktop.home.button.open_current_campaign_workspace"), OpenWorkRouteAsync, isPrimary: true)
                : CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_install_id", _preferences.Language), CopyInstallIdAsync),
            CreateButton(S("desktop.home.button.open_update_status"), OpenUpdateWindowAsync)
        ];

    private IReadOnlyList<Button> CreateDevicesActions()
        =>
        [
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language), OpenAccountAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync)
        ];

    private IReadOnlyList<Button> CreateClaimsActions()
        =>
        [
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.login_website", _preferences.Language), OpenInstallLinkingAsync, isPrimary: true),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _preferences.Language), OpenDownloadsAsync),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_install_id", _preferences.Language), CopyInstallIdAsync)
        ];

    private IReadOnlyList<Button> CreateAccessActions()
        =>
        [
            CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync, isPrimary: true),
            CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync),
            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _preferences.Language), OpenAccountAsync)
        ];

    private async Task CopyInstallIdAsync()
    {
        if (Clipboard is null)
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.clipboard_unavailable", _preferences.Language));
            return;
        }

        await Clipboard.SetTextAsync(_installState.InstallationId).ConfigureAwait(true);
        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.install_id_copied", _preferences.Language));
    }

    private async Task OpenLatestInstallHandoffAsync()
    {
        string? handoffCode = LatestPendingClaim?.ClaimCode;
        if (string.IsNullOrWhiteSpace(handoffCode))
        {
            await OpenInstallLinkingAsync().ConfigureAwait(true);
            return;
        }

        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.linking", _preferences.Language));
        DesktopInstallClaimResult result = await DesktopInstallLinkingRuntime.RedeemClaimCodeAsync(
            _installState.HeadId,
            handoffCode,
            CancellationToken.None).ConfigureAwait(true);
        _installState = result.State;
        await RefreshDevicesAccessStateAsync().ConfigureAwait(true);
        SetStatus(result.Message);

        if (!result.Succeeded)
        {
            await OpenInstallLinkingAsync().ConfigureAwait(true);
        }
    }

    private Task OpenUpdateWindowAsync()
        => DesktopUpdateWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _installState.HeadId);

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _installState.HeadId);

    private async Task OpenInstallLinkingAsync()
    {
        DesktopInstallLinkingStartupContext context = new(
            State: _installState,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_devices_access");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(this).ConfigureAwait(true);
        await RefreshDevicesAccessStateAsync().ConfigureAwait(true);
    }

    private Task OpenDownloadsAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_downloads", _preferences.Language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_downloads", _preferences.Language));
        }

        return Task.CompletedTask;
    }

    private Task OpenWorkRouteAsync()
    {
        return DesktopCampaignWorkspaceWindow.ShowAsync(this, _installState.HeadId);
    }

    private Task OpenAccountAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_installState))
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_account", _preferences.Language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_account", _preferences.Language));
        }

        return Task.CompletedTask;
    }

    private async Task RefreshDevicesAccessStateAsync()
    {
        try
        {
            _installState = DesktopInstallLinkingRuntime.LoadOrCreateState(_installState.HeadId);
            _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(_installState.HeadId);
            _preferences = DesktopPreferenceRuntime.LoadOrCreateState(_installState.HeadId);
            (DesktopInstallLinkingSummaryProjection installLinkingSummary, AccountCampaignSummary? campaignSummary) = await ReadAccountStateAsync().ConfigureAwait(true);
            _installLinkingSummary = installLinkingSummary;
            _campaignSummary = campaignSummary;
        }
        catch
        {
            SetStatus(S("desktop.devices.status.refresh_failed"));
            return;
        }

        _introText.Text = BuildIntro();
        _statusText.Text = S("desktop.devices.status.current");
        _guidedPreferenceStatusText.Text = DesktopInstallLinkingWindow.BuildGuidedToolsPreferenceStatus(_preferences, _preferences.Language);
        if (_guidedToolsRadioButton.IsChecked != !_preferences.DisableAiFeatures)
        {
            _guidedToolsRadioButton.IsChecked = !_preferences.DisableAiFeatures;
        }

        if (_manualInterfaceRadioButton.IsChecked != _preferences.DisableAiFeatures)
        {
            _manualInterfaceRadioButton.IsChecked = _preferences.DisableAiFeatures;
        }

        _currentText.Text = BuildCurrentBody();
        _devicesText.Text = BuildDevicesBody();
        _claimsText.Text = BuildClaimsBody();
        _accessText.Text = BuildAccessBody();
        ResetActionRow(_currentActionsRow, CreateCurrentActions());
        ResetActionRow(_devicesActionsRow, CreateDevicesActions());
        ResetActionRow(_claimsActionsRow, CreateClaimsActions());
        ResetActionRow(_accessActionsRow, CreateAccessActions());
    }

    private void SetStatus(string message)
        => _statusText.Text = message;

    private DesktopClaimedInstallProjection? CurrentClaimedInstallation
        => _installLinkingSummary.ClaimedInstallations.FirstOrDefault(item => string.Equals(item.InstallationId, _installState.InstallationId, StringComparison.OrdinalIgnoreCase));

    private DesktopInstallationGrantProjection? CurrentGrant
        => _installLinkingSummary.ActiveGrants.FirstOrDefault(item => string.Equals(item.InstallationId, _installState.InstallationId, StringComparison.OrdinalIgnoreCase));

    private DesktopPendingClaimTicket? LatestPendingClaim
        => _installLinkingSummary.PendingClaimTickets
            .OrderByDescending(static ticket => ticket.CreatedAtUtc)
            .FirstOrDefault();

    private IReadOnlyList<ClaimedDeviceRestoreProjection> ClaimedRestoreDevices
        => _campaignSummary?.Restore.ClaimedDevices ?? Array.Empty<ClaimedDeviceRestoreProjection>();

    private static IReadOnlyList<string> TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static string FormatDisplayTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    private static Border CreateSection(string title, string description, Control body, Control? actionContent)
    {
        TextBlock descriptionText = new()
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        return DesktopShellTheme.CreateSection(
            title,
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    descriptionText,
                    body
                }
            },
            actionContent,
            padding: 8,
            cornerRadius: 4,
            includeHeading: true,
            spacing: 6);
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 8, cornerRadius: 4, includeHeading: true, spacing: 6);

    private Control CreateGuidedToolsPreferenceSection()
        => CreateSection(
            S("desktop.devices.section.interface"),
            new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = S("desktop.install_link.preference.summary"),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
                    },
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            _guidedToolsRadioButton,
                            _manualInterfaceRadioButton
                        }
                    },
                    _guidedPreferenceStatusText
                }
            },
            null);

    private RadioButton CreateGuidedFeatureRadioButton(string name, string label, bool isChecked, bool disableAiFeatures)
    {
        RadioButton button = new()
        {
            Name = name,
            GroupName = "DevicesAccessFeatureVisibility",
            Content = label,
            IsChecked = isChecked
        };
        string tip = S("desktop.install_link.preference.tip");
        ToolTip.SetTip(button, tip);
        AutomationProperties.SetName(button, label);
        AutomationProperties.SetHelpText(button, tip);
        button.IsCheckedChanged += (_, _) =>
        {
            if (button.IsChecked == true)
            {
                ApplyGuidedFeaturePreference(disableAiFeatures);
            }
        };

        return button;
    }

    private void ApplyGuidedFeaturePreference(bool disableAiFeatures)
    {
        DesktopPreferenceState nextPreferences = DesktopPreferenceStateRuntime.Normalize(
            _preferences with { DisableAiFeatures = disableAiFeatures });
        _preferences = nextPreferences;
        DesktopPreferenceRuntime.SaveState(_installState.HeadId, nextPreferences);
        DesktopPreferenceStateRuntime.SetCurrent(nextPreferences);
        _guidedPreferenceStatusText.Text = DesktopInstallLinkingWindow.BuildGuidedToolsPreferenceStatus(nextPreferences, nextPreferences.Language);

        if (_guidedToolsRadioButton.IsChecked != !disableAiFeatures)
        {
            _guidedToolsRadioButton.IsChecked = !disableAiFeatures;
        }

        if (_manualInterfaceRadioButton.IsChecked != disableAiFeatures)
        {
            _manualInterfaceRadioButton.IsChecked = disableAiFeatures;
        }

        if (Owner is MainWindow ownerWindow)
        {
            ownerWindow.ApplyExternalPreferenceState(nextPreferences);
        }

        SetStatus(DesktopLocalizationCatalog.GetRequiredString(
            disableAiFeatures
                ? "desktop.install_link.status.guided_tools_hidden"
                : "desktop.install_link.status.guided_tools_visible",
            nextPreferences.Language));
    }

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 6);

    private static void ResetActionRow(StackPanel actionRow, IReadOnlyList<Button> actions)
        => DesktopShellTheme.ResetActionRow(actionRow, actions);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
