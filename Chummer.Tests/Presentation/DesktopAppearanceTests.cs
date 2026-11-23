using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopAppearanceTests
{
    [TestMethod]
    public void Explicit_appearance_choices_map_without_changing_legacy_values()
    {
        Assert.AreEqual(ThemeVariant.Light, DesktopAppearance.ResolveVariant("Light"));
        Assert.AreEqual(ThemeVariant.Dark, DesktopAppearance.ResolveVariant("dark"));
        Assert.AreEqual(ThemeVariant.Default, DesktopAppearance.ResolveVariant("system"));
        Assert.AreEqual(ThemeVariant.Dark, DesktopAppearance.ResolveVariant("classic"));
        var options = DesktopAppearance.Options("modern");
        Assert.AreEqual(4, options.Count);
        Assert.AreEqual("modern", options[3].Value);
        Assert.AreEqual(3, DesktopAppearance.Options("system").Count);
    }

    [TestMethod]
    public void Live_appearance_switch_preserves_open_input_and_control_identity()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var app = global::Avalonia.Application.Current!;
            var previous = app.RequestedThemeVariant;
            var previousResources = app.Resources;
            var resources = new ResourceDictionary();
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();
            light["ChummerShellInputBackgroundBrush"] = Brushes.White;
            light["ChummerShellInputForegroundBrush"] = Brushes.Black;
            light["ChummerShellBorderBrush"] = Brushes.Gray;
            light["ChummerShellSelectionBrush"] = Brushes.Blue;
            light["ChummerShellSelectionForegroundBrush"] = Brushes.White;
            dark["ChummerShellInputBackgroundBrush"] = Brushes.Black;
            dark["ChummerShellInputForegroundBrush"] = Brushes.White;
            dark["ChummerShellBorderBrush"] = Brushes.Gray;
            dark["ChummerShellSelectionBrush"] = Brushes.Blue;
            dark["ChummerShellSelectionForegroundBrush"] = Brushes.White;
            resources.ThemeDictionaries[ThemeVariant.Light] = light;
            resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
            app.Resources = resources;
            var input = new TextBox { Text = "unsaved character name" };
            DesktopShellTheme.ApplyShellTextInputTheme(input);
            var host = new Window { Content = input, Width = 640, Height = 240 };
            try
            {
                DesktopAppearance.Apply("light");
                host.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(Colors.White, ((ISolidColorBrush)input.Background!).Color);
                DesktopAppearance.Apply("dark");
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(Colors.Black, ((ISolidColorBrush)input.Background!).Color);
                Assert.AreSame(input, host.Content);
                Assert.AreEqual("unsaved character name", input.Text);
                DesktopAppearance.Apply("system");
                Assert.AreEqual(ThemeVariant.Default, app.RequestedThemeVariant);
                Assert.AreEqual("unsaved character name", input.Text);
            }
            finally
            {
                host.Close();
                app.Resources = previousResources;
                app.RequestedThemeVariant = previous;
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
