using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

internal sealed class DesktopReportIssueWindow : Window
{
    private readonly DesktopInstallLinkingState _installState;
    private readonly DesktopUpdateClientStatus _updateStatus;
    private readonly DesktopPreferenceState _preferences;
    private readonly TextBlock _statusText;
    private readonly TextBlock _contextText;
    private readonly TextBox _bugTitleBox;
    private readonly TextBox _bugExpectedBox;
    private readonly TextBox _bugActualBox;
    private readonly TextBox _bugReproStepsBox;
    private readonly TextBox _bugEvidenceBox;
    private readonly TextBox _feedbackSummaryBox;
    private readonly TextBox _feedbackDetailBox;

    private DesktopReportIssueWindow(
        DesktopInstallLinkingState installState,
        DesktopUpdateClientStatus updateStatus,
        DesktopPreferenceState preferences)
    {
        _installState = installState;
        _updateStatus = updateStatus;
        _preferences = preferences;

        Title = S("desktop.report.title");
        Width = 920;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _statusText = new TextBlock
        {
            Text = S("desktop.report.status.ready"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _contextText = new TextBlock
        {
            Text = BuildContextBody(),
            TextWrapping = TextWrapping.Wrap
        };

        _bugTitleBox = CreateInputBox(S("desktop.report.bug.title_watermark"));
        _bugExpectedBox = CreateInputBox(S("desktop.report.bug.expected_watermark"), isMultiline: true, minHeight: 80);
        _bugActualBox = CreateInputBox(S("desktop.report.bug.actual_watermark"), isMultiline: true, minHeight: 80);
        _bugReproStepsBox = CreateInputBox(S("desktop.report.bug.repro_watermark"), isMultiline: true, minHeight: 100);
        _bugEvidenceBox = CreateInputBox(S("desktop.report.bug.evidence_watermark"), isMultiline: true, minHeight: 72);
        _feedbackSummaryBox = CreateInputBox(S("desktop.report.feedback.summary_watermark"));
        _feedbackDetailBox = CreateInputBox(S("desktop.report.feedback.detail_watermark"), isMultiline: true, minHeight: 120);

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
                            Text = S("desktop.report.heading"),
                            FontSize = 24,
                            FontWeight = FontWeight.SemiBold
                        },
                        new TextBlock
                        {
                            Text = S("desktop.report.intro"),
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = S("desktop.report.private_split"),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.DarkSlateGray
                        },
                        _statusText,
                        CreateSection(S("desktop.report.section.context"), _contextText, null),
                        CreateSection(
                            S("desktop.report.section.bug"),
                            CreateBugBody(),
                            CreateActionRow(
                            [
                                CreateButton(S("desktop.report.button.open_bug"), OpenBugDraftAsync, isPrimary: true),
                                CreateButton(S("desktop.report.button.copy_bug"), CopyBugDraftAsync)
                            ])),
                        CreateSection(
                            S("desktop.report.section.feedback"),
                            CreateFeedbackBody(),
                            CreateActionRow(
                            [
                                CreateButton(S("desktop.report.button.open_feedback"), OpenFeedbackDraftAsync, isPrimary: true),
                                CreateButton(S("desktop.report.button.copy_feedback"), CopyFeedbackDraftAsync)
                            ])),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync),
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

        DesktopReportIssueWindow dialog = Create(headId);
        await dialog.ShowDialog(owner);
    }

    private static DesktopReportIssueWindow Create(string headId)
    {
        DesktopInstallLinkingState installState = DesktopInstallLinkingRuntime.LoadOrCreateState(headId);
        DesktopUpdateClientStatus updateStatus = DesktopUpdateRuntime.GetCurrentStatus(headId);
        DesktopPreferenceState preferences = DesktopPreferenceRuntime.LoadOrCreateState(installState.HeadId);

        return new DesktopReportIssueWindow(installState, updateStatus, preferences);
    }

    private Control CreateBugBody()
        => new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = S("desktop.report.bug.intro"),
                    TextWrapping = TextWrapping.Wrap
                },
                CreateField(S("desktop.report.bug.title_label"), _bugTitleBox),
                CreateField(S("desktop.report.bug.expected_label"), _bugExpectedBox),
                CreateField(S("desktop.report.bug.actual_label"), _bugActualBox),
                CreateField(S("desktop.report.bug.repro_label"), _bugReproStepsBox),
                CreateField(S("desktop.report.bug.evidence_label"), _bugEvidenceBox)
            }
        };

    private Control CreateFeedbackBody()
        => new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = S("desktop.report.feedback.intro"),
                    TextWrapping = TextWrapping.Wrap
                },
                CreateField(S("desktop.report.feedback.summary_label"), _feedbackSummaryBox),
                CreateField(S("desktop.report.feedback.detail_label"), _feedbackDetailBox)
            }
        };

    private string BuildContextBody()
    {
        List<string> lines =
        [
            F("desktop.home.install_summary.install_id", _installState.InstallationId),
            F("desktop.home.install_summary.head", _installState.HeadId),
            F("desktop.home.install_summary.version", _installState.ApplicationVersion),
            F("desktop.home.install_summary.channel", _installState.ChannelId),
            F("desktop.home.install_summary.platform", _installState.Platform, _installState.Arch),
            F("desktop.support.context.release_status", _updateStatus.Status),
            F("desktop.support.context.recommended_action", _updateStatus.RecommendedAction)
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastManifestVersion))
        {
            lines.Add(F("desktop.report.context.manifest", _updateStatus.LastManifestVersion));
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.SupportabilityState))
        {
            lines.Add(F("desktop.report.context.supportability", _updateStatus.SupportabilityState));
        }

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add(F("desktop.support.context.last_error", _updateStatus.LastError));
        }

        return string.Join("\n", lines);
    }

    private async Task OpenBugDraftAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForBugReport(
                _installState,
                _updateStatus,
                _bugTitleBox.Text ?? string.Empty,
                _bugExpectedBox.Text ?? string.Empty,
                _bugActualBox.Text ?? string.Empty,
                _bugReproStepsBox.Text ?? string.Empty,
                _bugEvidenceBox.Text))
        {
            _statusText.Text = S("desktop.report.status.bug_opened");
            return;
        }

        _statusText.Text = await TryCopyDraftAsync(BuildBugDraftText()).ConfigureAwait(true)
            ? S("desktop.report.status.bug_copied_fallback")
            : S("desktop.report.status.portal_unavailable");
    }

    private async Task CopyBugDraftAsync()
    {
        _statusText.Text = await TryCopyDraftAsync(BuildBugDraftText()).ConfigureAwait(true)
            ? S("desktop.report.status.bug_copied")
            : S("desktop.report.status.clipboard_unavailable");
    }

    private async Task OpenFeedbackDraftAsync()
    {
        if (DesktopInstallLinkingRuntime.TryOpenSupportPortalForFeedback(
                _installState,
                _updateStatus,
                _feedbackSummaryBox.Text ?? string.Empty,
                _feedbackDetailBox.Text ?? string.Empty))
        {
            _statusText.Text = S("desktop.report.status.feedback_opened");
            return;
        }

        _statusText.Text = await TryCopyDraftAsync(BuildFeedbackDraftText()).ConfigureAwait(true)
            ? S("desktop.report.status.feedback_copied_fallback")
            : S("desktop.report.status.portal_unavailable");
    }

    private async Task CopyFeedbackDraftAsync()
    {
        _statusText.Text = await TryCopyDraftAsync(BuildFeedbackDraftText()).ConfigureAwait(true)
            ? S("desktop.report.status.feedback_copied")
            : S("desktop.report.status.clipboard_unavailable");
    }

    private Task OpenSupportWindowAsync()
        => DesktopSupportWindow.ShowAsync(this, _installState.HeadId);

    private async Task<bool> TryCopyDraftAsync(string draftText)
    {
        if (Clipboard is null)
        {
            return false;
        }

        await Clipboard.SetTextAsync(draftText).ConfigureAwait(true);
        return true;
    }

    private string BuildBugDraftText()
    {
        return string.Join(
            "\n",
            new[]
            {
                $"{S("desktop.report.bug.title_label")}: {NormalizeDraftField(_bugTitleBox.Text, $"Desktop bug report for {_installState.HeadId}")}",
                $"{S("desktop.report.bug.expected_label")}: {NormalizeDraftField(_bugExpectedBox.Text)}",
                $"{S("desktop.report.bug.actual_label")}: {NormalizeDraftField(_bugActualBox.Text)}",
                $"{S("desktop.report.bug.repro_label")}: {NormalizeDraftField(_bugReproStepsBox.Text)}",
                $"{S("desktop.report.bug.evidence_label")}: {NormalizeDraftField(_bugEvidenceBox.Text)}",
                string.Empty,
                BuildContextBody()
            });
    }

    private string BuildFeedbackDraftText()
    {
        return string.Join(
            "\n",
            new[]
            {
                $"{S("desktop.report.feedback.summary_label")}: {NormalizeDraftField(_feedbackSummaryBox.Text, $"Desktop feedback for {_installState.HeadId}")}",
                $"{S("desktop.report.feedback.detail_label")}: {NormalizeDraftField(_feedbackDetailBox.Text)}",
                string.Empty,
                BuildContextBody()
            });
    }

    private static string NormalizeDraftField(string? value, string fallback = "Not provided.")
        => string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

    private static StackPanel CreateField(string label, Control input)
        => new()
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeight.SemiBold
                },
                input
            }
        };

    private static TextBox CreateInputBox(string watermark, bool isMultiline = false, double minHeight = 0)
        => new()
        {
            Watermark = watermark,
            AcceptsReturn = isMultiline,
            TextWrapping = isMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = minHeight
        };

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

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
    {
        Button button = new()
        {
            Content = label,
            MinWidth = 140
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
