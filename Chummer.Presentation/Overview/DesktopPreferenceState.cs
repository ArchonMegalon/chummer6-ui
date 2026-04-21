namespace Chummer.Presentation.Overview;

public sealed record DesktopPreferenceState(
    int UiScalePercent,
    string Theme,
    string Language,
    bool CompactMode,
    string CharacterPriority,
    int KarmaNuyenRatio,
    bool HouseRulesEnabled,
    string CharacterNotes,
    string StartupBehavior = "Restore last roster on startup",
    string UpdateChannel = "Preview channel · check weekly",
    bool CheckForUpdatesOnLaunch = true,
    string CharacterRosterPath = "/Characters",
    string PdfViewerPath = "/usr/bin/default-pdf-viewer",
    string VisibleChromePolicy = "Menu, toolstrip, dialogs, and status strip stay compact by default.")
{
    public static DesktopPreferenceState Default { get; } = new(
        UiScalePercent: 100,
        Theme: "classic",
        Language: "en-us",
        CompactMode: false,
        CharacterPriority: "SumToTen",
        KarmaNuyenRatio: 2,
        HouseRulesEnabled: false,
        CharacterNotes: string.Empty,
        StartupBehavior: "Restore last roster on startup",
        UpdateChannel: "Preview channel · check weekly",
        CheckForUpdatesOnLaunch: true,
        CharacterRosterPath: "/Characters",
        PdfViewerPath: "/usr/bin/default-pdf-viewer",
        VisibleChromePolicy: "Menu, toolstrip, dialogs, and status strip stay compact by default.");
}
