using Chummer.Presentation.Overview;

namespace Chummer.Desktop.Runtime;

public static class DesktopAiFeaturePreferenceFilter
{
    private static readonly string[] PreferenceHeadIds = ["avalonia", "winforms"];

    public static bool AreAiCharacterOptionsDisabled()
    {
        if (DesktopPreferenceStateRuntime.Current.DisableAiFeatures)
        {
            return true;
        }

        foreach (string headId in PreferenceHeadIds)
        {
            try
            {
                if (DesktopPreferenceRuntime.TryLoadState(headId, out DesktopPreferenceState preferences)
                    && preferences.DisableAiFeatures)
                {
                    return true;
                }
            }
            catch
            {
                // Preference files must not block character creation.
            }
        }

        return false;
    }

    public static bool ShouldHideCharacterOrCompanionOption(bool aiCharacterOptionsDisabled, params string?[] values)
        => aiCharacterOptionsDisabled
           && values.Any(value =>
               !string.IsNullOrWhiteSpace(value)
               && OverviewCommandPolicy.IsAiFeatureCharacterOrCompanionOption(value));
}
