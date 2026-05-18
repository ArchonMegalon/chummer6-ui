using Avalonia;
using Avalonia.Automation;
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
        Width = 780;
        Height = 620;
        MinWidth = 680;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _statusText = new TextBlock
        {
            Text = S("desktop.report.status.ready"),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DarkSlateGray
        };

        _contextText = new TextBlock
        {
            Text = BuildContextBody(),
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        _bugTitleBox = CreateInputBox(S("desktop.report.bug.title_watermark"), S("desktop.report.bug.title_label"));
        _bugExpectedBox = CreateInputBox(S("desktop.report.bug.expected_watermark"), S("desktop.report.bug.expected_label"), isMultiline: true, minHeight: 64);
        _bugActualBox = CreateInputBox(S("desktop.report.bug.actual_watermark"), S("desktop.report.bug.actual_label"), isMultiline: true, minHeight: 64);
        _bugReproStepsBox = CreateInputBox(S("desktop.report.bug.repro_watermark"), S("desktop.report.bug.repro_label"), isMultiline: true, minHeight: 84);
        _bugEvidenceBox = CreateInputBox(S("desktop.report.bug.evidence_watermark"), S("desktop.report.bug.evidence_label"), isMultiline: true, minHeight: 56);
        _feedbackSummaryBox = CreateInputBox(S("desktop.report.feedback.summary_watermark"), S("desktop.report.feedback.summary_label"));
        _feedbackDetailBox = CreateInputBox(S("desktop.report.feedback.detail_watermark"), S("desktop.report.feedback.detail_label"), isMultiline: true, minHeight: 84);

        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        _statusText,
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
            Spacing = 6,
            Children =
            {
                _bugTitleBox,
                _bugExpectedBox,
                _bugActualBox,
                _bugReproStepsBox,
                _bugEvidenceBox
            }
        };

    private Control CreateFeedbackBody()
        => new StackPanel
        {
            Spacing = 6,
            Children =
            {
                _feedbackSummaryBox,
                _feedbackDetailBox
            }
        };

    private string BuildContextBody()
    {
        List<string> lines =
        [
            $"{_installState.HeadId} · {_installState.Platform}/{_installState.Arch}",
            $"Version {_installState.ApplicationVersion} · {_installState.ChannelId}",
            $"Release: {_updateStatus.Status}"
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"Issue: {_updateStatus.LastError}");
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

    private static TextBox CreateInputBox(string tooltip, string automationName, bool isMultiline = false, double minHeight = 0)
    {
        TextBox box = new()
        {
            AcceptsReturn = isMultiline,
            TextWrapping = isMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = minHeight
        };
        ToolTip.SetTip(box, tooltip);
        AutomationProperties.SetName(box, automationName);
        return box;
    }

    private static Border CreateSection(string title, Control body, Control? actionContent)
    {
        ToolTip.SetTip(body, title);
        StackPanel content = new() { Spacing = 0 };

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
            MinWidth = 96
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
