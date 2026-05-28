namespace Chummer.Avalonia.Controls;

public sealed class SettingsClassicPort : ClassicFormPortSurfaceControl
{
    public SettingsClassicPort()
        : base(
            "settings",
            "Global Settings Classic",
            ["Global", "Custom Data", "GitHub Issues", "Plugins"],
            "Chummer/Forms/EditGlobalSettings.Designer.cs")
    {
    }
}
