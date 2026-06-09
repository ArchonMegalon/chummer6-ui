using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopInstallLinkingWindow : Window
{
    private DesktopInstallLinkingState _state;
    private readonly DesktopUpdateClientStatus _updateStatus;
    private readonly string _language;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _statusText;
    private readonly TextBlock _claimCodeHintText;
    private readonly TextBlock _claimCodeLabelText;
    private readonly TextBox _claimCodeTextBox;
    private readonly StackPanel _claimCodeEntryRow;
    private readonly TextBlock _moreToolsHeading;
    private readonly StackPanel _moreToolsPanel;
    private readonly Button _followThroughButton;
    private readonly Button _accountButton;
    private readonly Button _redeemClaimCodeButton;
    private readonly Button _exitButton;
    private bool _allowGuestClose;

    public DesktopInstallLinkingWindow(DesktopInstallLinkingStartupContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _state = context.State;
        _updateStatus = DesktopUpdateRuntime.GetCurrentStatus(context.State.HeadId);
        _language = DesktopPreferenceRuntime.LoadOrCreateState(context.State.HeadId).Language;
        Title = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", _language);
        Width = 700;
        Height = 460;
        MinWidth = 560;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _summaryText = new TextBlock
        {
            Text = BuildSummary(_state, _updateStatus, _language),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _statusText = new TextBlock
        {
            IsVisible = false,
            Foreground = Brushes.DarkSlateGray,
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeHintText = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.prompt_guest_claim", _language),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeLabelText = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_label", _language),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        _claimCodeTextBox = new TextBox
        {
            Watermark = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_watermark", _language),
            MinWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _followThroughButton = CreateButton(string.Empty, OpenFollowThroughAsync, isDefault: true);
        _accountButton = CreateButton(string.Empty, OpenAccountAsync);
        _redeemClaimCodeButton = CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.redeem_claim_code", _language),
            RedeemClaimCodeAsync);
        _exitButton = CreateButton(
            DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.exit_desktop", _language),
            ExitDesktopAsync);
        _claimCodeEntryRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                _claimCodeTextBox,
                _redeemClaimCodeButton
            }
        };
        _moreToolsHeading = new TextBlock
        {
            Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.more_tools", _language),
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _moreToolsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Spacing = 6,
            Children =
            {
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _language), OpenAccountAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_install_id", _language), CopyInstallIdAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _language), OpenDownloadsAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_support", _language), OpenSupportAsync),
                CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_report_issue", _language), OpenReportIssueAsync)
            }
        };

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EEF2F6")),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    _summaryText,
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.guest_status", _language),
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    _statusText,
                    new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.next_step", _language),
                                FontWeight = FontWeight.SemiBold,
                                TextWrapping = TextWrapping.Wrap
                            },
                            _claimCodeHintText,
                            _claimCodeLabelText,
                            _claimCodeEntryRow,
                            new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Spacing = 6,
                                Children =
                                {
                                    _followThroughButton,
                                    _accountButton,
                                    _exitButton
                                }
                            },
                            _moreToolsHeading,
                            _moreToolsPanel
                        }
                    }
                }
            }
        };

        Closing += OnClosing;
        Opened += (_, _) =>
        {
            if (context.ClaimResult is not null)
            {
                SetStatus(context.ClaimResult.Message);
                _state = context.ClaimResult.State;
                RefreshSummary();
                RefreshActionState();
            }
            else if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.guest_status", _language));
            }
        };

        RefreshActionState();
    }

    public static async Task ShowAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopInstallLinkingState state = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopInstallLinkingStartupContext context = new(
            State: state,
            ClaimResult: null,
            StartupClaimCode: null,
            ShouldPrompt: true,
            PromptReason: "desktop_shell");

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(owner);
    }

    public static async Task ShowIfNeededAsync(Window owner, DesktopInstallLinkingStartupContext context)
    {
        if (!context.ShouldPrompt)
        {
            return;
        }

        DesktopInstallLinkingWindow dialog = new(context);
        await dialog.ShowDialog(owner);
    }

    private Button CreateButton(string label, Func<Task> action, bool isDefault = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 104
        };
        if (isDefault)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        button.Click += async (_, _) => await action();
        return button;
    }

    private async Task CopyInstallIdAsync()
    {
        if (Clipboard is null)
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.clipboard_unavailable", _language));
            return;
        }

        await Clipboard.SetTextAsync(_state.InstallationId);
        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.install_id_copied", _language));
    }

    private async Task RedeemClaimCodeAsync()
    {
        string claimCode = _claimCodeTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(claimCode))
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.claim_code_required", _language));
            return;
        }

        SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.linking", _language));
        DesktopInstallClaimResult result = await DesktopInstallLinkingRuntime.RedeemClaimCodeAsync(
            _state.HeadId,
            claimCode,
            CancellationToken.None).ConfigureAwait(true);

        _state = result.State;
        _claimCodeTextBox.Text = result.Succeeded ? string.Empty : claimCode;
        RefreshSummary();
        RefreshActionState();
        if (Owner is MainWindow ownerWindow)
        {
            ownerWindow.ApplyInstallLinkingChrome(_state);
        }

        SetStatus(result.Message);
    }

    private Task OpenFollowThroughAsync()
    {
        if (DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            Window? ownerWindow = Owner as Window;
            if (ownerWindow is not null)
            {
                Close();
                return DesktopCampaignWorkspaceWindow.ShowAsync(ownerWindow, _state.HeadId);
            }

            return DesktopCampaignWorkspaceWindow.ShowAsync(this, _state.HeadId);
        }

        Window? followThroughOwner = Owner as Window;
        if (followThroughOwner is not null)
        {
            Close();
            return DesktopDevicesAccessWindow.ShowAsync(followThroughOwner, _state.HeadId);
        }

        return DesktopDevicesAccessWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenDownloadsAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_downloads", _language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_downloads", _language));
        }

        return Task.CompletedTask;
    }

    private Task OpenSupportAsync()
    {
        return DesktopSupportWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenReportIssueAsync()
    {
        return DesktopReportIssueWindow.ShowAsync(this, _state.HeadId);
    }

    private Task OpenAccountAsync()
    {
        bool opened = DesktopInstallLinkingRuntime.IsClaimed(_state)
            ? DesktopInstallLinkingRuntime.TryOpenAccountPortalForInstall(_state)
            : DesktopInstallLinkingRuntime.TryOpenClaimPortalForInstall(_state);

        if (opened)
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_account", _language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_account", _language));
        }

        return Task.CompletedTask;
    }

    private Task ExitDesktopAsync()
    {
        _allowGuestClose = true;
        Close();
        return Task.CompletedTask;
    }

    private void RefreshSummary()
    {
        _summaryText.Text = BuildSummary(_state, _updateStatus, _language);
    }

    private void RefreshActionState()
    {
        bool claimed = DesktopInstallLinkingRuntime.IsClaimed(_state);
        _followThroughButton.Content = claimed
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_work", _language)
            : DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_devices_access", _language);
        _accountButton.Content = claimed
            ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _language)
            : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.login_website", _language);
        _followThroughButton.IsVisible = claimed;
        _exitButton.IsVisible = !claimed;
        _claimCodeHintText.IsVisible = !claimed;
        _claimCodeLabelText.IsVisible = !claimed;
        _claimCodeEntryRow.IsVisible = !claimed;
        _moreToolsHeading.IsVisible = claimed;
        _moreToolsPanel.IsVisible = claimed;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DesktopInstallLinkingRuntime.IsClaimed(_state) || _allowGuestClose)
        {
            return;
        }

        _allowGuestClose = true;
        if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktopLifetime.Shutdown();
            return;
        }

        Close();
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
        _statusText.IsVisible = !string.IsNullOrWhiteSpace(message);
        ToolTip.SetTip(_statusText, message);
    }

    private static string BuildSummary(DesktopInstallLinkingState state, DesktopUpdateClientStatus updateStatus, string language)
    {
        string claimStatus = DesktopInstallLinkingRuntime.IsClaimed(state)
            ? DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.linked_status",
                language,
                state.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") ?? "Unknown")
            : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.guest_status", language);

        List<string> lines =
        [
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.installation_id", language, state.InstallationId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.head", language, state.HeadId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.version", language, state.ApplicationVersion),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.channel", language, state.ChannelId),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.platform", language, state.Platform, state.Arch),
            DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.status", language, claimStatus)
        ];

        if (state.LastClaimAttemptUtc is not null)
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
                "desktop.install_link.summary.last_claim_attempt",
                language,
                state.LastClaimAttemptUtc.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimMessage))
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.hub_message", language, state.LastClaimMessage));
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimError))
        {
            lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.summary.claim_error", language, state.LastClaimError));
        }

        lines.AddRange(DesktopSurfacePostureText.BuildLines(updateStatus));
        lines.Add(DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.install_link.shipping_locales",
            language,
            DesktopLocalizationCatalog.BuildSupportedLanguageSummary()));
        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(state)
                ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_claimed", language)
                : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_guest", language));

        return string.Join("\n", lines);
    }
}
