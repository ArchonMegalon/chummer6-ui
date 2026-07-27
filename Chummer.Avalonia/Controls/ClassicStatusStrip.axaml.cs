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
            complianceStateText.Text = state.ComplianceState;
        }

        if (this.FindControl<ProgressBar>("WorkbenchProgressBar") is { } workbenchProgressBar)
        {
            workbenchProgressBar.IsIndeterminate = state.IsBusy;
            workbenchProgressBar.Value = state.IsBusy ? 0d : 100d;
        }

        ToolTip.SetTip(
            this,
            AccessibilityPrimitiveBoundary.BuildStatusAnnouncement(
                state.CharacterState,
                state.ServiceState,
                state.TimeState,
                state.ComplianceState));
    }
}
