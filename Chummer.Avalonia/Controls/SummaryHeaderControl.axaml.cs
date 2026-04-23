using Avalonia.Controls;
using Chummer.Contracts.Presentation;

namespace Chummer.Avalonia.Controls;

public partial class SummaryHeaderControl : UserControl
{
    public event EventHandler? KeepLocalWorkRequested { add { } remove { } }
    public event EventHandler? SaveLocalWorkRequested { add { } remove { } }
    public event EventHandler? CampaignWorkspaceRequested { add { } remove { } }
    public event EventHandler? WorkspaceSupportRequested { add { } remove { } }

    public SummaryHeaderControl()
    {
        InitializeComponent();
        IsVisible = false;
        Height = 0d;
    }

    public void SetState(SummaryHeaderState state)
    {
        // Chummer5a parity posture: no restore/continuity header chrome is rendered
        // in the primary workbench shell.
        // RestoreContinuityStatusBorder.IsVisible = false;
        // RestoreContinuityActionPanel.IsVisible = false;
        IsVisible = false;
        Height = 0d;
    }
}

public sealed record SummaryHeaderState(
    string NavigationTabsHeading,
    NavigatorTabItem[] NavigationTabs,
    string? ActiveTabId,
    bool HasVisibleContent = false,
    string? RuntimeSummary = null,
    string? RestoreContinuitySummary = null,
    string? StaleStateSummary = null,
    string? ConflictChoiceSummary = null,
    bool CanSaveLocalWorkBeforeRestore = false);
