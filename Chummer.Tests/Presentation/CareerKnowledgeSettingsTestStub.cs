using System.Xml.Linq;

namespace Chummer.Presentation.Overview;

internal sealed record Chummer5CharacterSettingsProfile(string Id, string Name, string Xml);

internal sealed record Chummer5CharacterSettingsCatalog(
    string ActiveProfileId,
    IReadOnlyList<Chummer5CharacterSettingsProfile> Profiles);

internal static class Chummer5CharacterSettingsProfiles
{
    private const string SettingsId = "223a11ff-80e0-428b-89a9-6ef1c243b8b6";

    internal static Chummer5CharacterSettingsCatalog ParseCatalog(string? json)
    {
        XElement settings = new(
            "settings",
            new XElement("id", SettingsId),
            new XElement("maxknowledgeskillrating", "12"),
            new XElement(
                "karmacost",
                new XElement("karmanewknowledgeskill", "2"),
                new XElement("karmaimproveknowledgeskill", "1"),
                new XElement("karmanewactiveskill", "2"),
                new XElement("karmaimproveactiveskill", "2"),
                new XElement("karmanewskillgroup", "5"),
                new XElement("karmaimproveskillgroup", "5")));
        Chummer5CharacterSettingsProfile profile = new(
            SettingsId,
            "Test",
            settings.ToString(SaveOptions.DisableFormatting));
        return new Chummer5CharacterSettingsCatalog(SettingsId, [profile]);
    }
}
