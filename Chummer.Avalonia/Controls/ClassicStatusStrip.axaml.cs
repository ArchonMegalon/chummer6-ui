using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia.Controls;

public partial class ClassicStatusStrip : UserControl, IStatusStripSurface
{
    public ClassicStatusStrip()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetState(StatusStripState state)
    {
        if (this.FindControl<TextBlock>("CharacterStateText") is { } characterStateText)
        {
            characterStateText.Text = state.CharacterState;
        }

        if (this.FindControl<TextBlock>("ServiceStateText") is { } serviceStateText)
        {
            serviceStateText.Text = state.ServiceState;
        }

        if (this.FindControl<TextBlock>("TimeStateText") is { } timeStateText)
        {
            timeStateText.Text = state.TimeState;
        }

        if (this.FindControl<TextBlock>("ComplianceStateText") is { } complianceStateText)
        {
            complianceStateText.Text = StatusStripDisplayText.Summary(state.ComplianceState);
            if (this.FindControl<Button>("StatusDetailsButton")?.Flyout is Flyout { Content: TextBlock details })
                details.Text = state.ComplianceState;
        }

        if (this.FindControl<ProgressBar>("WorkbenchProgressBar") is { } workbenchProgressBar)
        {
            workbenchProgressBar.IsIndeterminate = state.IsBusy;
            workbenchProgressBar.Value = state.IsBusy ? 0d : 100d;
        }

        // Gen6 F06: the default Classic surface must show the same current
        // save feedback as the alternate StatusStripControl -- the SaveState
        // segment appears only while a workspace is active (non-empty state).
        if (this.FindControl<Border>("SaveStateBorder") is { } saveStateBorder
            && this.FindControl<TextBlock>("SaveStateText") is { } saveStateText)
        {
            bool hasSaveState = !string.IsNullOrWhiteSpace(state.SaveState);
            saveStateBorder.IsVisible = hasSaveState;
            saveStateText.Text = state.SaveState;
        }

        ToolTip.SetTip(
            this,
            AccessibilityPrimitiveBoundary.BuildStatusAnnouncement(
                state.CharacterState,
                state.ServiceState,
                state.TimeState,
                state.ComplianceState,
                state.SaveState));
    }
}
