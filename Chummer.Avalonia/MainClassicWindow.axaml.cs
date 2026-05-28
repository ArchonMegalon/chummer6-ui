using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia;

public partial class MainClassicWindow : Window
{
    public MainClassicWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
