using Chummer.Desktop.Runtime;
using Chummer.Presentation.Overview;

namespace Chummer.Avalonia;

public partial class MainWindow
{
    private CharacterOverviewState PrepareStateForRefresh(CharacterOverviewState state)
    {
        if (ReferenceEquals(state, CharacterOverviewState.Empty) || _preferPersistedPreferencesOnNextRefresh)
        {
            DesktopPreferenceStateRuntime.SetCurrent(_persistedPreferences);
            DesktopLocalizationCatalog.SetCurrentLanguageOverride(_persistedPreferences.Language);
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

    internal void ApplyExternalPreferenceState(DesktopPreferenceState preferences)
    {
        DesktopPreferenceState normalized = DesktopPreferenceStateRuntime.Normalize(preferences);
        _persistedPreferences = normalized;
        DesktopPreferenceRuntime.SaveState(DesktopHeadId, normalized);
        DesktopPreferenceStateRuntime.SetCurrent(normalized);
        DesktopLocalizationCatalog.SetCurrentLanguageOverride(normalized.Language);

        _preferPersistedPreferencesOnNextRefresh = true;
        try
        {
            RefreshState();
        }
        finally
        {
            _preferPersistedPreferencesOnNextRefresh = false;
        }

        ApplyInstallLinkingChrome(_installLinkingState);
    }
}
