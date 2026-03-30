namespace Chummer.Presentation.Overview;

public static class DesktopPreferenceStateRuntime
{
    private static DesktopPreferenceState _current = DesktopPreferenceState.Default;

    public static DesktopPreferenceState Current
        => _current;

    public static void SetCurrent(DesktopPreferenceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _current = Normalize(state);
    }

    public static DesktopPreferenceState Normalize(DesktopPreferenceState state)
        => state with
        {
            Theme = string.IsNullOrWhiteSpace(state.Theme) ? DesktopPreferenceState.Default.Theme : state.Theme.Trim(),
            Language = DesktopLocalizationCatalog.NormalizeOrDefault(state.Language),
            CharacterPriority = string.IsNullOrWhiteSpace(state.CharacterPriority) ? DesktopPreferenceState.Default.CharacterPriority : state.CharacterPriority.Trim(),
            CharacterNotes = state.CharacterNotes ?? string.Empty
        };
}
