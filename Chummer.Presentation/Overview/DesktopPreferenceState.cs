namespace Chummer.Presentation.Overview;

public sealed record DesktopPreferenceState(
    int UiScalePercent,
    string Theme,
    string Language,
    bool CompactMode,
    string CharacterPriority,
    int KarmaNuyenRatio,
    bool HouseRulesEnabled,
    string CharacterNotes)
{
    public static DesktopPreferenceState Default { get; } = new(
        UiScalePercent: 100,
        Theme: "classic",
        Language: "en-us",
        CompactMode: false,
        CharacterPriority: "SumToTen",
        KarmaNuyenRatio: 2,
        HouseRulesEnabled: false,
        CharacterNotes: string.Empty);
}
