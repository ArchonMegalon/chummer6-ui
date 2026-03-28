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
    private readonly TextBlock _summaryText;
    private readonly TextBlock _statusText;
    private readonly TextBox _claimCodeBox;
    private bool _isSubmitting;

    public DesktopInstallLinkingWindow(DesktopInstallLinkingStartupContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _state = context.State;
        Title = "Link this copy";
        Width = 760;
        Height = 520;
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _summaryText = new TextBlock
        {
            Text = BuildSummary(_state),
            TextWrapping = TextWrapping.Wrap
        };

        _claimCodeBox = new TextBox
        {
            Watermark = "Paste the claim code from your Hub account",
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
                        Text = "Link this desktop copy to your account",
                        FontSize = 22,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Chummer keeps the binary canonical. Linking happens through an install claim code and a Hub-issued installation grant instead of mutating the installer per user.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"Shipping locales: {DesktopLocalizationCatalog.BuildSupportedLanguageSummary()}. Install, update, and support trust flows should stay aligned across this desktop wave.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    _summaryText,
                    new TextBlock
                    {
                        Text = "Install claim code",
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
                            CreateButton("Copy Install ID", CopyInstallIdAsync),
                            CreateButton("Open Downloads", OpenDownloadsAsync),
                            CreateButton("Open Support", OpenSupportAsync),
                            CreateButton(
                                DesktopInstallLinkingRuntime.IsClaimed(_state) ? "Open Work" : "Open Account",
                                OpenFollowThroughAsync),
                            CreateButton("Link This Copy", LinkAsync, isDefault: true),
                            CreateButton("Continue as Guest", ContinueAsGuestAsync)
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
                SetStatus("If you downloaded while signed in, copy the pending claim code from your Hub account and paste it here.");
            }
        };
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
            SetStatus("Clipboard access is unavailable in this host.");
            return;
        }

        await Clipboard.SetTextAsync(_state.InstallationId);
        SetStatus("Copied the installation id to the clipboard.");
    }

    private Task OpenFollowThroughAsync()
    {
        if (DesktopInstallLinkingRuntime.IsClaimed(_state))
        {
            if (DesktopInstallLinkingRuntime.TryOpenWorkPortal())
            {
                SetStatus("Opened the account-aware work route so you can confirm restore, support, and update follow-through on this claimed install.");
            }
            else
            {
                SetStatus("Unable to open the account-aware work route from this host.");
            }
        }
        else
        {
            if (DesktopInstallLinkingRuntime.TryOpenAccountPortal())
            {
                SetStatus("Opened your Hub account so you can review or copy the install claim code.");
            }
            else
            {
                SetStatus("Unable to open the Hub account page from this host.");
            }
        }

        return Task.CompletedTask;
    }

    private Task OpenDownloadsAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenDownloadsPortal())
        {
            SetStatus("Opened downloads so you can review the current release and installer posture before linking.");
        }
        else
        {
            SetStatus("Unable to open downloads from this host.");
        }

        return Task.CompletedTask;
    }

    private Task OpenSupportAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForInstall(_state))
        {
            SetStatus("Opened install-aware support so the closure path stays tied to this exact copy while you link it.");
        }
        else
        {
            SetStatus("Unable to open support from this host.");
        }

        return Task.CompletedTask;
    }

    private async Task LinkAsync()
    {
        if (_isSubmitting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_claimCodeBox.Text))
        {
            SetStatus("Paste the claim code from your Hub account first.");
            return;
        }

        _isSubmitting = true;
        try
        {
            SetStatus("Linking this installation with Hub...");
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
        _summaryText.Text = BuildSummary(_state);
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
        _statusText.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private static string BuildSummary(DesktopInstallLinkingState state)
    {
        string claimStatus = DesktopInstallLinkingRuntime.IsClaimed(state)
            ? $"Linked. Grant expires {state.GrantExpiresAtUtc?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")} UTC."
            : "Not linked yet. You can keep using this copy as a guest.";

        List<string> lines =
        [
            $"Installation ID: {state.InstallationId}",
            $"Head: {state.HeadId}",
            $"Version: {state.ApplicationVersion}",
            $"Channel: {state.ChannelId}",
            $"Platform: {state.Platform}/{state.Arch}",
            $"Status: {claimStatus}"
        ];

        if (state.LastClaimAttemptUtc is not null)
        {
            lines.Add($"Last claim attempt: {state.LastClaimAttemptUtc.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.");
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimMessage))
        {
            lines.Add($"Hub message: {state.LastClaimMessage}");
        }

        if (!string.IsNullOrWhiteSpace(state.LastClaimError))
        {
            lines.Add($"Claim error: {state.LastClaimError}");
        }

        lines.Add(
            DesktopInstallLinkingRuntime.IsClaimed(state)
                ? "Next safe action: open the account-aware work route and confirm restore, update, and support follow-through from this claimed install."
                : "Next safe action: copy the installation id, redeem the Hub claim code, and keep install-aware support open until the grant lands.");

        return string.Join("\n", lines);
    }
}
