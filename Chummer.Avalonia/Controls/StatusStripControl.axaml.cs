using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Chummer.Presentation.Overview;
using Chummer.Presentation.UiKit;

namespace Chummer.Avalonia.Controls;

public partial class StatusStripControl : UserControl
{
    private static readonly string UiKitAccessibilityAdapterMarker = AccessibilityPrimitiveBoundary.RootClass;

    public StatusStripControl()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public void SetState(StatusStripState state)
    {
        SetValues(
            characterState: state.CharacterState,
            serviceState: state.ServiceState,
            timeState: state.TimeState,
            complianceState: state.ComplianceState);
    }

    public void SetValues(
        string characterState,
        string serviceState,
        string timeState,
        string complianceState)
    {
        CharacterStateText.Text = characterState;
        ServiceStateText.Text = serviceState;
        TimeStateText.Text = timeState;
        ComplianceStateText.Text = complianceState;
        ToolTip.SetTip(
            this,
            AccessibilityPrimitiveBoundary.BuildStatusAnnouncement(
                characterState,
                serviceState,
                timeState,
                complianceState));
    }

    public void SetServiceAndTime(string serviceState, string timeState)
    {
        ServiceStateText.Text = serviceState;
        TimeStateText.Text = timeState;
    }

    private void ApplyLocalization()
    {
        string language = DesktopLocalizationCatalog.GetCurrentLanguage();
        CharacterStateText.Text = DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.status.character",
            language,
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.value.none", language));
        ServiceStateText.Text = DesktopLocalizationCatalog.GetRequiredFormattedString(
            "desktop.shell.status.service",
            language,
            DesktopLocalizationCatalog.GetRequiredString("desktop.shell.state.value.online", language));
        TimeStateText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.status.time_placeholder", language);
        ComplianceStateText.Text = DesktopLocalizationCatalog.GetRequiredString("desktop.shell.status.compliance_placeholder", language);
    }
}

public sealed record StatusStripState(
    string CharacterState,
    string ServiceState,
    string TimeState,
    string ComplianceState);
