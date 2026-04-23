using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Reflection;

namespace Chummer.Avalonia;

internal sealed class DesktopAboutWindow : Window
{
    public DesktopAboutWindow()
    {
        Title = "About Chummer6";
        Width = 784;
        Height = 561;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        AboutAssemblyProjection projection = BuildProjection();

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#EEF2F6")),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("0.35*,0.65*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,*"),
                ColumnSpacing = 12,
                RowSpacing = 8,
                Children =
                {
                    BuildLogoPanel(),
                    BuildTextBlock(projection.Product, fontSize: 18, fontWeight: FontWeight.SemiBold).At(0, 1),
                    BuildTextBlock($"Version {projection.Version}").At(1, 1),
                    BuildTextBlock(projection.Copyright).At(2, 1),
                    BuildTextBlock(projection.Company).At(3, 1),
                    BuildReadOnlyBox(projection.Description).At(4, 1),
                    BuildReadOnlyBox(projection.Disclaimer).At(5, 1),
                    BuildReadOnlyBox(projection.Contributors).At(5, 0)
                }
            }
        };
    }

    public static async Task ShowAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DesktopAboutWindow dialog = new();
        if (owner.Icon is not null)
        {
            dialog.Icon = owner.Icon;
        }

        await dialog.ShowDialog(owner);
    }

    private static Control BuildLogoPanel()
    {
        Control content;
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Chummer.Avalonia/Assets/chummer6-icon-preview.png"));
            content = new Image
            {
                Source = new Bitmap(stream),
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }
        catch
        {
            content = BuildTextBlock("Chummer6", fontSize: 26, fontWeight: FontWeight.Bold, horizontalAlignment: HorizontalAlignment.Center);
        }

        Border border = new()
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#BBC7D4")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Child = content
        };

        Grid.SetColumn(border, 0);
        Grid.SetRow(border, 0);
        Grid.SetRowSpan(border, 5);
        return border;
    }

    private static TextBlock BuildTextBlock(
        string text,
        double fontSize = 13,
        FontWeight? fontWeight = null,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        => new()
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Center
        };

    private static TextBox BuildReadOnlyBox(string text)
        => new()
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#BBC7D4")),
            BorderThickness = new Thickness(1),
            MinHeight = 160
        };

    private static AboutAssemblyProjection BuildProjection()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(DesktopAboutWindow).Assembly;
        string product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product?.Trim()
            ?? "Chummer6";
        string version = ReadAssemblyMetadata(assembly, "ChummerDesktopReleaseVersion")
            ?? assembly.GetName().Version?.ToString()
            ?? "dev";
        string? description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "Chummer6 desktop preview focused on Chummer5a-faithful workflow parity.";
        }

        string? company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company?.Trim();
        if (string.IsNullOrWhiteSpace(company))
        {
            company = "ArchonMegalon";
        }

        string? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright?.Trim();
        if (string.IsNullOrWhiteSpace(copyright))
        {
            copyright = "Licensed GNU GPLv3";
        }

        return new AboutAssemblyProjection(
            Product: product,
            Version: version,
            Description: description,
            Company: company,
            Copyright: copyright,
            Contributors: "Thank you to all GitHub contributors, testers, and table crews keeping Chummer usable.",
            Disclaimer: "Chummer is community software. Shadowrun and related marks remain the property of their respective owners.");
    }

    private static string? ReadAssemblyMetadata(Assembly assembly, string key)
        => assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;

    private readonly record struct AboutAssemblyProjection(
        string Product,
        string Version,
        string Description,
        string Company,
        string Copyright,
        string Contributors,
        string Disclaimer);
}

internal static class DesktopAboutWindowLayoutExtensions
{
    public static T At<T>(this T control, int row, int column)
        where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }
}
