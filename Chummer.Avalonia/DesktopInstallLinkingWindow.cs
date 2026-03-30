using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopInstallLinkingWindow : Window
{
    private DesktopInstallLinkingState _state;
    private readonly string _language;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _statusText;
    private readonly TextBox _claimCodeBox;
    private bool _isSubmitting;

    public DesktopInstallLinkingWindow(DesktopInstallLinkingStartupContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _state = context.State;
        _language = DesktopPreferenceRuntime.LoadOrCreateState(context.State.HeadId).Language;
        Title = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.title", _language);
        Width = 760;
        Height = 520;
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _summaryText = new TextBlock
        {
            Text = BuildSummary(_state, _language),
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeBox = new TextBox
        {
            Watermark = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_watermark", _language),
            Text = context.StartupClaimCode ?? string.Empty
        };

        _statusText = new TextBlock
        {
            IsVisible = false,
            Foreground = Brushes.DarkSlateGray,
            TextWrapping = TextWrapping.Wrap
        };

        Content = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.heading", _language),
                        FontSize = 22,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary", _language),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredFormattedString("desktop.install_link.shipping_locales", _language, DesktopLocalizationCatalog.BuildSupportedLanguageSummary()),
                        TextWrapping = TextWrapping.Wrap
                    },
                    _summaryText,
                    new TextBlock
                    {
                        Text = DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.claim_code_label", _language),
                        FontWeight = FontWeight.SemiBold
                    },
                    _claimCodeBox,
                    _statusText,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.copy_install_id", _language), CopyInstallIdAsync),
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_downloads", _language), OpenDownloadsAsync),
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_support", _language), OpenSupportAsync),
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.home.button.open_report_issue", _language), OpenReportIssueAsync),
                            CreateButton(
                                DesktopInstallLinkingRuntime.IsClaimed(_state)
                                    ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_work", _language)
                                    : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.open_account", _language),
                                OpenFollowThroughAsync),
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.link_copy", _language), LinkAsync, isDefault: true),
                            CreateButton(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.button.continue_guest", _language), ContinueAsGuestAsync)
                        }
                    }
                }
            }
        };

        Opened += (_, _) =>
        {
            if (context.ClaimResult is not null)
            {
                SetStatus(context.ClaimResult.Message);
                _state = context.ClaimResult.State;
                RefreshSummary();
            }
            else if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
            {
                SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.prompt_guest_claim", _language));
            }
        };
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
            MinWidth = 120
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

        if (DesktopInstallLinkingRuntime.TryOpenAccountPortal())
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.opened_account", _language));
        }
        else
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.unable_open_account", _language));
        }

        return Task.CompletedTask;
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

    private async Task LinkAsync()
    {
        if (_isSubmitting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_claimCodeBox.Text))
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.claim_code_required", _language));
            return;
        }

        _isSubmitting = true;
        try
        {
            SetStatus(DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.status.linking", _language));
            DesktopInstallClaimResult result = await DesktopInstallLinkingRuntime.RedeemClaimCodeAsync(
                _state.HeadId,
                _claimCodeBox.Text,
                CancellationToken.None);
            _state = result.State;
            RefreshSummary();
            SetStatus(result.Message);
            if (result.Succeeded)
            {
                Close();
            }
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private Task ContinueAsGuestAsync()
    {
        if (!DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            DesktopInstallLinkingRuntime.MarkPromptDismissed(_state.HeadId);
        }

        Close();
        return Task.CompletedTask;
    }

    private void RefreshSummary()
    {
        _summaryText.Text = BuildSummary(_state, _language);
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
        _statusText.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private static string BuildSummary(DesktopInstallLinkingState state, string language)
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

        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(state)
                ? DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_claimed", language)
                : DesktopLocalizationCatalog.GetRequiredString("desktop.install_link.summary.next_safe_action_guest", language));

        return string.Join("\n", lines);
    }
}
