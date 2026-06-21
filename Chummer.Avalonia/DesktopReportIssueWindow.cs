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
    private const string ReportBugTitleBoxName = "ReportBugTitleBox";
    private const string ReportBugExpectedBoxName = "ReportBugExpectedBox";
    private const string ReportBugActualBoxName = "ReportBugActualBox";
    private const string ReportBugReproStepsBoxName = "ReportBugReproStepsBox";
    private const string ReportBugEvidenceBoxName = "ReportBugEvidenceBox";
    private const string ReportFeedbackSummaryBoxName = "ReportFeedbackSummaryBox";
    private const string ReportFeedbackDetailBoxName = "ReportFeedbackDetailBox";
    private const string ReportBugTitleBoxLabelName = "ReportBugTitleBoxLabel";
    private const string ReportBugExpectedBoxLabelName = "ReportBugExpectedBoxLabel";
    private const string ReportBugActualBoxLabelName = "ReportBugActualBoxLabel";
    private const string ReportBugReproStepsBoxLabelName = "ReportBugReproStepsBoxLabel";
    private const string ReportBugEvidenceBoxLabelName = "ReportBugEvidenceBoxLabel";
    private const string ReportFeedbackSummaryBoxLabelName = "ReportFeedbackSummaryBoxLabel";
    private const string ReportFeedbackDetailBoxLabelName = "ReportFeedbackDetailBoxLabel";

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
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _contextText = new TextBlock
        {
            Text = BuildContextBody(),
            IsVisible = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

        _bugTitleBox = CreateInputBox(S("desktop.report.bug.title_watermark"), S("desktop.report.bug.title_label"), ReportBugTitleBoxName);
        _bugExpectedBox = CreateInputBox(S("desktop.report.bug.expected_watermark"), S("desktop.report.bug.expected_label"), ReportBugExpectedBoxName, isMultiline: true, minHeight: 64);
        _bugActualBox = CreateInputBox(S("desktop.report.bug.actual_watermark"), S("desktop.report.bug.actual_label"), ReportBugActualBoxName, isMultiline: true, minHeight: 64);
        _bugReproStepsBox = CreateInputBox(S("desktop.report.bug.repro_watermark"), S("desktop.report.bug.repro_label"), ReportBugReproStepsBoxName, isMultiline: true, minHeight: 84);
        _bugEvidenceBox = CreateInputBox(S("desktop.report.bug.evidence_watermark"), S("desktop.report.bug.evidence_label"), ReportBugEvidenceBoxName, isMultiline: true, minHeight: 56);
        _feedbackSummaryBox = CreateInputBox(S("desktop.report.feedback.summary_watermark"), S("desktop.report.feedback.summary_label"), ReportFeedbackSummaryBoxName);
        _feedbackDetailBox = CreateInputBox(S("desktop.report.feedback.detail_watermark"), S("desktop.report.feedback.detail_label"), ReportFeedbackDetailBoxName, isMultiline: true, minHeight: 84);

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
                            Text = S("desktop.report.heading"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellForegroundBrush", "#0f172a")
                        },
                        CreateIntroText(S("desktop.report.intro")),
                        CreateIntroText(S("desktop.report.private_split")),
                        _statusText,
                        CreateSection(
                            S("desktop.report.section.context"),
                            _contextText,
                            null),
                        CreateSection(
                            S("desktop.report.section.bug"),
                            CreateBugBody(),
                            CreateActionRow(
                            [
                                CreateButton(S("desktop.report.button.open_bug"), OpenBugDraftAsync, isPrimary: true),
                                CreateButton(S("desktop.report.button.copy_bug"), CopyBugDraftAsync)
                            ]),
                            includeHeading: true),
                        CreateSection(
                            S("desktop.report.section.feedback"),
                            CreateFeedbackBody(),
                            CreateActionRow(
                            [
                                CreateButton(S("desktop.report.button.open_feedback"), OpenFeedbackDraftAsync, isPrimary: true),
                                CreateButton(S("desktop.report.button.copy_feedback"), CopyFeedbackDraftAsync)
                            ]),
                            includeHeading: true),
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children =
                            {
                                CreateButton(S("desktop.home.button.open_support_center"), OpenSupportWindowAsync),
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
            Spacing = 6,
            Children =
            {
                CreateField(S("desktop.report.feedback.summary_label"), _feedbackSummaryBox),
                CreateField(S("desktop.report.feedback.detail_label"), _feedbackDetailBox)
            }
        };

    private static Control CreateField(string label, TextBox input)
    {
        TextBlock labelBlock = new()
        {
            Name = ResolveFieldLabelName(input.Name),
            Text = label,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellForegroundBrush", "#0f172a")
        };
        AutomationProperties.SetName(labelBlock, label);
        AutomationProperties.SetHelpText(labelBlock, label);

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                labelBlock,
                input
            }
        };
    }

    private static string? ResolveFieldLabelName(string? inputName)
        => inputName switch
        {
            ReportBugTitleBoxName => ReportBugTitleBoxLabelName,
            ReportBugExpectedBoxName => ReportBugExpectedBoxLabelName,
            ReportBugActualBoxName => ReportBugActualBoxLabelName,
            ReportBugReproStepsBoxName => ReportBugReproStepsBoxLabelName,
            ReportBugEvidenceBoxName => ReportBugEvidenceBoxLabelName,
            ReportFeedbackSummaryBoxName => ReportFeedbackSummaryBoxLabelName,
            ReportFeedbackDetailBoxName => ReportFeedbackDetailBoxLabelName,
            _ => string.IsNullOrWhiteSpace(inputName) ? null : $"{inputName}Label"
        };

    private static TextBlock CreateIntroText(string text)
        => new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DesktopShellTheme.ResolveThemeBrush("ChummerShellMutedForegroundBrush", "#334155")
        };

    private string BuildContextBody()
    {
        List<string> lines =
        [
            $"{_installState.HeadId} · {_installState.Platform}/{_installState.Arch}",
            $"Version {_installState.ApplicationVersion} · {_installState.ChannelId}",
            $"Update channel status: {_updateStatus.Status}"
        ];

        if (!string.IsNullOrWhiteSpace(_updateStatus.LastError))
        {
            lines.Add($"What still needs attention: {_updateStatus.LastError}");
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

    private static TextBox CreateInputBox(string tooltip, string automationName, string name, bool isMultiline = false, double minHeight = 0)
    {
        TextBox box = new()
        {
            Name = name,
            AcceptsReturn = isMultiline,
            TextWrapping = isMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = minHeight,
            Watermark = tooltip
        };
        DesktopShellTheme.ApplyShellTextInputTheme(box);
        AutomationProperties.SetName(box, automationName);
        AutomationProperties.SetHelpText(box, $"{automationName}. {tooltip}");
        ToolTip.SetTip(box, null);
        return box;
    }

    private static Border CreateSection(string title, Control body, Control? actionContent, bool includeHeading = false)
        => DesktopShellTheme.CreateSection(title, body, actionContent, padding: 10, cornerRadius: 4, includeHeading: includeHeading, spacing: includeHeading ? 6 : 0);

    private static StackPanel CreateActionRow(IReadOnlyList<Button> actions)
        => DesktopShellTheme.CreateStackActionRow(actions, spacing: 6);

    private static Button CreateButton(string label, Func<Task> action, bool closeWindow = false, bool isPrimary = false)
        => DesktopShellTheme.CreateButton(label, action, closeWindow, isPrimary, minWidth: 96);

    private string S(string key)
        => DesktopLocalizationCatalog.GetRequiredString(key, _preferences.Language);

    private string F(string key, params object[] values)
        => DesktopLocalizationCatalog.GetRequiredFormattedString(key, _preferences.Language, values);
}
