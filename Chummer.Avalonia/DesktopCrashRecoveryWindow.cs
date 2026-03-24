using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;

namespace Chummer.Avalonia;

internal sealed class DesktopCrashRecoveryWindow : Window
{
    private readonly DesktopPendingCrashReport _pending;
    private readonly TextBlock _statusText;
    private bool _closeAcknowledges;
    private bool _isSubmitting;

    public DesktopCrashRecoveryWindow(DesktopPendingCrashReport pending)
    {
        _pending = pending;
        Title = "Chummer closed unexpectedly";
        Width = 760;
        Height = 560;
        MinWidth = 600;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBox detailsBox = new()
        {
            Text = pending.SummaryText,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 280
        };

        _statusText = new TextBlock
        {
            IsVisible = false,
            Foreground = Brushes.DarkSlateGray
        };

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                CreateButton("Open Folder", async () => await OpenPathAsync(_pending.ReportDirectory, "Opened the crash report folder.")),
                CreateButton("Open Bundle", async () => await OpenPathAsync(_pending.BundlePath, "Opened the diagnostics bundle.")),
                CreateButton("Copy Summary", CopySummaryAsync),
                CreateButton("Retry Send", RetrySendAsync),
                CreateButton("Close", CloseWindowAsync, isDefault: true)
            }
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
                        Text = "Chummer saved a local crash report from the previous run.",
                        FontSize = 22,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Chummer keeps the full diagnostics bundle local. On restart it tries to send a small redacted crash envelope to Hub so triage can begin without waiting on a manual bug report.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new ScrollViewer
                    {
                        Content = detailsBox
                    },
                    _statusText,
                    actions
                }
            }
        };

        Opened += OnOpened;
    }

    public static async Task ShowPendingAsync(Window owner)
    {
        DesktopPendingCrashReport? pending = DesktopCrashRuntime.TryLoadPendingCrashReport();
        if (pending is null)
        {
            return;
        }

        DesktopCrashRecoveryWindow dialog = new(pending);
        await dialog.ShowDialog(owner);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;

        if (_pending.SubmittedAtUtc is not null && !string.IsNullOrWhiteSpace(_pending.IncidentId))
        {
            _closeAcknowledges = true;
            SetStatus($"Crash report already reached Hub as {_pending.IncidentId}.");
            return;
        }

        if (_pending.LastSubmissionAttemptUtc is not null && !string.IsNullOrWhiteSpace(_pending.LastSubmissionError))
        {
            SetStatus($"Previous automatic send failed: {_pending.LastSubmissionError}");
        }

        await RetrySendAsync();
    }

    private Button CreateButton(string label, Func<Task> action, bool isDefault = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 110
        };
        if (isDefault)
        {
            button.FontWeight = FontWeight.SemiBold;
        }

        button.Click += async (_, _) => await action();
        return button;
    }

    private Task CloseWindowAsync()
    {
        if (_closeAcknowledges)
        {
            DesktopCrashRuntime.TryAcknowledgePendingCrashReport(_pending.Report.CrashId);
        }

        Close();
        return Task.CompletedTask;
    }

    private async Task CopySummaryAsync()
    {
        if (Clipboard is null)
        {
            SetStatus("Clipboard access is unavailable in this host.");
            return;
        }

        await Clipboard.SetTextAsync(_pending.SummaryText);
        SetStatus("Copied the crash summary to the clipboard.");
    }

    private Task OpenPathAsync(string path, string successMessage)
    {
        if (DesktopCrashRuntime.TryOpenPathInShell(path))
        {
            SetStatus(successMessage);
        }
        else
        {
            SetStatus($"Unable to open: {path}");
        }

        return Task.CompletedTask;
    }

    private async Task RetrySendAsync()
    {
        if (_isSubmitting)
        {
            return;
        }

        _isSubmitting = true;
        try
        {
            SetStatus("Sending a redacted crash envelope to Hub...");
            DesktopCrashSubmissionResult result = await DesktopCrashRuntime.SubmitPendingCrashReportAsync(_pending, CancellationToken.None);
            _closeAcknowledges = result.Succeeded;
            SetStatus(result.Message);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
        _statusText.IsVisible = !string.IsNullOrWhiteSpace(message);
    }
}
