using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopStartupUpdateWindow : Window
{
    private readonly string _headId;
    private readonly string[] _relaunchArgs;
    private readonly TextBlock _titleText;
    private readonly TextBlock _bodyText;
    private readonly ProgressBar _progressBar;
    private bool _started;

    private DesktopStartupUpdateWindow(string headId, string[] relaunchArgs)
    {
        _headId = headId;
        _relaunchArgs = relaunchArgs;

        Title = "Chummer Update";
        Width = 520;
        Height = 220;
        MinWidth = 520;
        MinHeight = 220;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellWindowBackgroundBrush", "#050B16");

        _titleText = new TextBlock
        {
            Text = "Checking for updates",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellForegroundBrush", "#f8fafc")
        };

        _bodyText = new TextBlock
        {
            Text = "Chummer will continue automatically if this copy is current.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#cbd5e1")
        };

        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1000,
            IsIndeterminate = true,
            Height = 8
        };

        Content = new Border
        {
            Padding = new Thickness(24),
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellSurfaceBrush", "#111827"),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    _titleText,
                    _bodyText,
                    _progressBar
                }
            }
        };

        Opened += OnOpened;
    }

    public static async Task<bool> TryRunStartupUpdateAsync(Window owner, string headId, string[] relaunchArgs)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopStartupUpdateWindow dialog = new(headId, relaunchArgs);
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        try
        {
            Progress<DesktopUpdateProgressUpdate> progress = new(ApplyProgress);
            DesktopUpdateStartupResult result = await DesktopUpdateRuntime.CheckAndScheduleStartupUpdateAsync(
                _headId,
                _relaunchArgs,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            if (result.ExitRequested)
            {
                ApplyProgress(new DesktopUpdateProgressUpdate("relaunching", "Installing update and restarting Chummer", 1000, 1000));
                await Task.Delay(650).ConfigureAwait(true);
                Close(true);
                return;
            }

            if (string.Equals(result.Reason, "already_current", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "installed_ahead_of_manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "seeded_from_manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "manifest_not_configured", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "retry_backoff", StringComparison.OrdinalIgnoreCase))
            {
                Close(false);
                return;
            }

            ApplyProgress(new DesktopUpdateProgressUpdate("attention", BuildAttentionMessage(result.Reason), null, null));
            await Task.Delay(1200).ConfigureAwait(true);
            Close(false);
        }
        catch (Exception ex)
        {
            ApplyProgress(new DesktopUpdateProgressUpdate("failed", $"Update check failed. Chummer will continue. {ex.Message}", null, null));
            await Task.Delay(1400).ConfigureAwait(true);
            Close(false);
        }
    }

    private void ApplyProgress(DesktopUpdateProgressUpdate update)
    {
        _titleText.Text = update.Stage switch
        {
            "checking" => "Checking for updates",
            "downloading" => "Downloading update",
            "validating" => "Checking update",
            "staging" => "Preparing update",
            "relaunching" => "Restarting Chummer",
            "manual" => "Update ready",
            "failed" => "Update needs attention",
            "blocked" => "Update paused",
            _ => "Updating Chummer"
        };

        _bodyText.Text = update.Message;
        if (update.Total is > 0 && update.Completed is >= 0)
        {
            _progressBar.IsIndeterminate = false;
            _progressBar.Maximum = update.Total.Value;
            _progressBar.Value = Math.Clamp(update.Completed.Value, 0, update.Total.Value);
        }
        else
        {
            _progressBar.IsIndeterminate = true;
        }
    }

    private static string BuildAttentionMessage(string reason)
        => reason switch
        {
            "auto_apply_disabled" => "A newer build is available. Open Devices & Access when you want to update.",
            "macos_manual_install_required" => "A macOS update is ready. Open Downloads to install it manually; this copy will stay usable.",
            "manifest_load_failed" => "Chummer could not reach the update list. This copy will keep running.",
            "update_schedule_failed" => "The update could not be prepared. This copy will keep running.",
            "rollout_blocked" => "The newest build is paused. This copy will keep running.",
            _ => "This copy will keep running."
        };
}
