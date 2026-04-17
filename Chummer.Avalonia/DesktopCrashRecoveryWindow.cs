using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopCrashRecoveryWindow : Window
{
    private readonly DesktopPendingCrashReport _pending;
    private readonly DesktopPreferenceState _preferences;
    private readonly bool _isPreview;
    private readonly TextBlock _statusText;
    private bool _closeAcknowledges;
    private bool _isSubmitting;

    private DesktopCrashRecoveryWindow(
        DesktopPendingCrashReport pending,
        DesktopPreferenceState preferences,
        bool isPreview)
    {
        _pending = pending;
        _preferences = preferences;
        _isPreview = isPreview;

        Title = S("desktop.crash.title");
        Width = 860;
        Height = 640;
        MinWidth = 720;
        MinHeight = 520;
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
            Text = isPreview
                ? S("desktop.crash.status.preview")
                : S("desktop.crash.status.current"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = S("desktop.crash.heading"),
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = BuildIntro(),
                            TextWrapping = TextWrapping.Wrap
                        },
                        _statusText,
                        CreateSection(
                            S("desktop.crash.section.summary"),
                            new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = BuildSummaryContext(),
                                        TextWrapping = TextWrapping.Wrap
                                    },
                                    new ScrollViewer
                                    {
                                        Content = detailsBox
                                    }
                                }
                            },
                            CreateActionRow(CreateEvidenceActions())),
                        CreateSection(
                            S("desktop.crash.section.recovery"),
                            new TextBlock
                            {
                                Text = BuildRecoveryBody(),
                                TextWrapping = TextWrapping.Wrap
                            },
                            CreateActionRow(CreateRecoveryActions())),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.continue"), ContinueAsync, closeWindow: true, isPrimary: true)
                            }
                        }
                    }
                }
            }
        };

        Opened += OnOpened;
    }

    public static async Task ShowPendingAsync(Window owner)
        => _ = await TryShowPendingAsync(owner).ConfigureAwait(true);

    public static async Task<bool> TryShowPendingAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DesktopPendingCrashReport? pending = DesktopCrashRuntime.TryLoadPendingCrashReport();
        if (pending is null)
        {
            return false;
        }

        DesktopCrashRecoveryWindow dialog = new(pending, CreatePreferences(pending.Report.HeadId), isPreview: false);
        await dialog.ShowDialog(owner);
        return true;
    }

    public static async Task ShowPreviewAsync(Window owner, string headId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(headId);

        DesktopCrashRecoveryWindow dialog = new(CreatePreviewPendingCrashReport(headId), CreatePreferences(headId), isPreview: true);
        await dialog.ShowDialog(owner);
    }

    private static DesktopPreferenceState CreatePreferences(string headId)
        => DesktopPreferenceRuntime.LoadOrCreateState(headId);

    private static DesktopPendingCrashReport CreatePreviewPendingCrashReport(string headId)
    {
        DateTimeOffset capturedAtUtc = DateTimeOffset.UtcNow;
        string reportDirectory = Path.Combine(Path.GetTempPath(), $"chummer-crash-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(reportDirectory);

        DesktopCrashReport report = new(
            CrashId: $"preview-{Guid.NewGuid():N}",
            HeadId: headId,
            CapturedAtUtc: capturedAtUtc,
            IsTerminating: true,
            ApplicationVersion: "preview-smoke",
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            OperatingSystem: RuntimeInformation.OSDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessName: "Chummer.Avalonia",
            BaseDirectoryLabel: Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            CurrentDirectoryLabel: Path.GetFileName(Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            ExceptionType: "System.InvalidOperationException",
            ExceptionMessage: "Synthetic crash recovery preview.",
            ExceptionDetail: "Synthetic crash recovery preview for flagship desktop smoke verification.");

        string reportPath = Path.Combine(reportDirectory, "report.json");
        string summaryPath = Path.Combine(reportDirectory, "summary.txt");
        string bundlePath = Path.Combine(reportDirectory, "diagnostics.zip");
        string summaryText = DesktopCrashRuntime.BuildRecoverySummary(report, reportDirectory);

        File.WriteAllText(reportPath, JsonSerializer.Serialize(report), System.Text.Encoding.UTF8);
        File.WriteAllText(summaryPath, summaryText, System.Text.Encoding.UTF8);
        using (ZipArchive archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(reportPath, Path.GetFileName(reportPath));
            archive.CreateEntryFromFile(summaryPath, Path.GetFileName(summaryPath));
        }

        return new DesktopPendingCrashReport(
            Report: report,
            ReportDirectory: reportDirectory,
            ReportPath: reportPath,
            SummaryPath: summaryPath,
            BundlePath: bundlePath,
            SummaryText: summaryText,
            ClaimSnapshot: null,
            SubmissionAttempts: 0,
            LastSubmissionAttemptUtc: null,
            LastSubmissionError: null,
            IncidentId: null,
            SubmittedAtUtc: null);
    }

    private string BuildIntro()
    {
        if (_isPreview)
        {
            return S("desktop.crash.intro.preview");
        }

        return S("desktop.crash.intro.current");
    }

    private string BuildSummaryContext()
    {
        return string.Join(
            "\n",
            new[]
            {
                F("desktop.crash.context.head", _pending.Report.HeadId),
                F("desktop.crash.context.version", _pending.Report.ApplicationVersion),
                F("desktop.crash.context.captured", _pending.Report.CapturedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")),
                F("desktop.crash.context.os", _pending.Report.OperatingSystem),
                F("desktop.crash.context.arch", _pending.Report.ProcessArchitecture)
            });
    }

    private string BuildRecoveryBody()
    {
        List<string> lines =
        [
            S("desktop.crash.recovery.private_local"),
            _isPreview
                ? S("desktop.crash.recovery.preview")
                : S("desktop.crash.recovery.retry")
        ];

        if (!_isPreview && !string.IsNullOrWhiteSpace(_pending.IncidentId))
        {
            lines.Add(F("desktop.crash.recovery.incident", _pending.IncidentId));
        }

        if (!_isPreview && !string.IsNullOrWhiteSpace(_pending.LastSubmissionError))
        {
            lines.Add(F("desktop.crash.recovery.last_error", _pending.LastSubmissionError));
        }

        return string.Join("\n", lines);
    }

    private IReadOnlyList<Button> CreateEvidenceActions()
        =>
        [
            CreateButton(S("desktop.crash.button.open_folder"), () => OpenPathAsync(_pending.ReportDirectory, S("desktop.crash.status.folder_opened"))),
            CreateButton(S("desktop.crash.button.open_bundle"), () => OpenPathAsync(_pending.BundlePath, S("desktop.crash.status.bundle_opened"))),
            CreateButton(S("desktop.crash.button.copy_summary"), CopySummaryAsync)
        ];

    private IReadOnlyList<Button> CreateRecoveryActions()
    {
        List<Button> actions = [];
        if (!_isPreview)
        {
            actions.Add(CreateButton(S("desktop.crash.button.retry_send"), RetrySendAsync, isPrimary: true));
        }

        actions.Add(CreateButton(S("desktop.home.button.open_report_issue"), OpenReportIssueWindowAsync, isPrimary: _isPreview));
        actions.Add(CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync));
        actions.Add(CreateButton(S("desktop.crash.button.keep_local_only"), KeepLocalOnlyAsync));
        return actions;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;

        if (_isPreview)
        {
            SetStatus(S("desktop.crash.status.preview"));
            return;
        }

        if (_pending.SubmittedAtUtc is not null && !string.IsNullOrWhiteSpace(_pending.IncidentId))
        {
            _closeAcknowledges = true;
            SetStatus(F("desktop.crash.status.already_submitted", _pending.IncidentId));
            return;
        }

        if (_pending.LastSubmissionAttemptUtc is not null && !string.IsNullOrWhiteSpace(_pending.LastSubmissionError))
        {
            SetStatus(F("desktop.crash.status.previous_send_failed", _pending.LastSubmissionError));
        }

        await RetrySendAsync();
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        StackPanel content = new()
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 15
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

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 104
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

    private Task ContinueAsync()
    {
        if (_closeAcknowledges && !_isPreview)
        {
            DesktopCrashRuntime.TryAcknowledgePendingCrashReport(_pending.Report.CrashId);
        }

        return Task.CompletedTask;
    }

    private async Task KeepLocalOnlyAsync()
    {
        if (!_isPreview)
        {
            DesktopCrashRuntime.TryAcknowledgePendingCrashReport(_pending.Report.CrashId);
        }

        SetStatus(S("desktop.crash.status.kept_local_only"));
        Close();
        await Task.CompletedTask;
    }

    private async Task CopySummaryAsync()
    {
        if (Clipboard is null)
        {
            SetStatus(S("desktop.crash.status.clipboard_unavailable"));
            return;
        }

        await Clipboard.SetTextAsync(_pending.SummaryText);
        SetStatus(S("desktop.crash.status.summary_copied"));
    }

    private Task OpenPathAsync(string path, string successMessage)
    {
        if (DesktopCrashRuntime.TryOpenPathInShell(path))
        {
            SetStatus(successMessage);
        }
        else
        {
            SetStatus(F("desktop.crash.status.unable_open_path", path));
        }

        return Task.CompletedTask;
    }

    private Task OpenReportIssueWindowAsync()
        => DesktopReportIssueWindow.ShowAsync(this, _pending.Report.HeadId);

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _pending.Report.HeadId);

    private async Task RetrySendAsync()
    {
        if (_isPreview)
        {
            SetStatus(S("desktop.crash.status.preview"));
            return;
        }

        if (_isSubmitting)
        {
            return;
        }

        _isSubmitting = true;
        try
        {
            SetStatus(S("desktop.crash.status.sending"));
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

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
