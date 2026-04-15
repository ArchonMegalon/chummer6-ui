using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private CharacterOverviewState PrepareStateForRefresh(CharacterOverviewState state)
    {
        if (ReferenceEquals(state, CharacterOverviewState.Empty))
        {
            return state with { Preferences = _persistedPreferences };
        }

        DesktopPreferenceState normalized = DesktopPreferenceStateRuntime.Normalize(state.Preferences);
        DesktopPreferenceStateRuntime.SetCurrent(normalized);
        DesktopLocalizationCatalog.SetCurrentLanguageOverride(normalized.Language);

        if (normalized != _persistedPreferences)
        {
            DesktopPreferenceRuntime.SaveState(DesktopHeadId, normalized);
            _persistedPreferences = normalized;
        }

        return state.Preferences == normalized
            ? state
            : state with { Preferences = normalized };
    }
}
