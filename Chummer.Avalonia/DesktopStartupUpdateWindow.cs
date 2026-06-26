using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopStartupUpdateWindow : Window
{
    private const int RelaunchVisibilityDelayMs = 1200;
    private const int AttentionVisibilityDelayMs = 1600;
    private const int FailureVisibilityDelayMs = 1800;

    private readonly string _headId;
    private readonly string[] _relaunchArgs;
    private readonly TextBlock _titleText;
    private readonly TextBlock _bodyText;
    private readonly TextBlock _waitText;
    private readonly ProgressBar _progressBar;
    private bool _started;

    private DesktopStartupUpdateWindow(string headId, string[] relaunchArgs)
    {
        _headId = headId;
        _relaunchArgs = relaunchArgs;

        Title = "Chummer Update";
        Width = 560;
        Height = 280;
        MinWidth = 520;
        MinHeight = 260;
        CanResize = true;
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
            Text = "Chummer will open automatically if this copy is already current.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#cbd5e1")
        };

        _waitText = new TextBlock
        {
            Text = "Keep this window open. Starting another copy can interrupt the update.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#cbd5e1")
        };

        _progressBar = new ProgressBar
        {
            Name = "StartupUpdateProgressBar",
            Minimum = 0,
            Maximum = 1000,
            IsIndeterminate = true,
            ShowProgressText = false,
            Height = 8,
            Background = DesktopShellTheme.ResolveThemeBrush("ChummerShellProgressTrackBrush", "#1E293B"),
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellProgressValueBrush", "#90C39A")
        };

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
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
                        _progressBar,
                        _waitText
                    }
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
                await Task.Delay(GetCompletionDisplayDelayMs(exitRequested: true, reason: result.Reason)).ConfigureAwait(true);
                Close(true);
                return;
            }

            if (string.Equals(result.Reason, "already_current", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "installed_ahead_of_manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "seeded_from_manifest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "manifest_not_configured", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "update_mode_off", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Reason, "retry_backoff", StringComparison.OrdinalIgnoreCase))
            {
                Close(false);
                return;
            }

            ApplyProgress(new DesktopUpdateProgressUpdate("attention", result.Message ?? BuildAttentionMessage(result.Reason), null, null));
            await Task.Delay(GetCompletionDisplayDelayMs(exitRequested: false, reason: result.Reason)).ConfigureAwait(true);
            Close(false);
        }
        catch (Exception ex)
        {
            ApplyProgress(new DesktopUpdateProgressUpdate("failed", $"Update check failed. Chummer will continue. {ex.Message}", null, null));
            await Task.Delay(GetCompletionDisplayDelayMs(exitRequested: false, reason: "failed")).ConfigureAwait(true);
            Close(false);
        }
    }

    private void ApplyProgress(DesktopUpdateProgressUpdate update)
    {
        DesktopStartupUpdateViewState state = BuildViewState(update);
        _titleText.Text = state.Title;
        _bodyText.Text = state.Body;
        _waitText.IsVisible = state.ShowWaitText;
        _progressBar.IsIndeterminate = state.IsIndeterminate;
        _progressBar.Maximum = state.ProgressMaximum;
        _progressBar.Value = state.ProgressValue;
    }

    internal static DesktopStartupUpdateViewState BuildViewState(DesktopUpdateProgressUpdate update)
    {
        string title = update.Stage switch
        {
            "checking" => "Checking for updates",
            "downloading" => "Downloading update",
            "validating" => "Checking update",
            "staging" => "Preparing update",
            "relaunching" => "Restarting Chummer",
            "available" => "Update available",
            "skipped" => "Update check skipped",
            "manual" => "Update ready",
            "failed" => "Update needs attention",
            "blocked" => "Update paused",
            _ => "Updating Chummer"
        };
        bool showWaitText = update.Stage is "downloading" or "validating" or "staging" or "relaunching";
        bool determinate = update.Total is > 0 && update.Completed is >= 0;
        int progressMaximum = determinate ? update.Total!.Value : 1000;
        int progressValue = determinate ? Math.Clamp(update.Completed!.Value, 0, progressMaximum) : 0;
        return new DesktopStartupUpdateViewState(
            Title: title,
            Body: update.Message,
            ShowWaitText: showWaitText,
            IsIndeterminate: !determinate,
            ProgressMaximum: progressMaximum,
            ProgressValue: progressValue);
    }

    internal static int GetCompletionDisplayDelayMs(bool exitRequested, string? reason)
    {
        if (exitRequested)
        {
            return RelaunchVisibilityDelayMs;
        }

        if (string.Equals(reason, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return FailureVisibilityDelayMs;
        }

        return AttentionVisibilityDelayMs;
    }

    internal static string BuildAttentionMessage(string reason)
        => reason switch
        {
            "auto_apply_disabled" => "A newer build is available. Open Devices & Access when you want to update.",
            "notify_only" => "A newer build is available. Open Devices & Access when you want to update.",
            "macos_manual_install_required" => "A macOS update is ready. Open Downloads to install it manually; this copy will stay usable.",
            "manifest_load_failed" => "Chummer could not reach the update list. This copy will keep running.",
            "update_schedule_failed" => "The update could not be prepared. This copy will keep running.",
            "rollout_blocked" => "The newest build is paused. This copy will keep running.",
            _ => "This copy will keep running."
        };
}

internal sealed record DesktopStartupUpdateViewState(
    string Title,
    string Body,
    bool ShowWaitText,
    bool IsIndeterminate,
    int ProgressMaximum,
    int ProgressValue);
