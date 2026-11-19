using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Gen6 T795: compact chrome strip for owned wizard windows. Carries the
/// wizard step caption; the native OS title bar stays responsible for the
/// window controls (minimize/maximize/close), matching the desktop shell.
/// </summary>
public partial class OwnedWizardChrome : UserControl
{
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<OwnedWizardChrome, string>(
            nameof(Caption), string.Empty);

    public OwnedWizardChrome()
    {
        InitializeComponent();
    }

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    private TextBlock? CaptionHost => this.FindControl<TextBlock>("CaptionText");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CaptionProperty && CaptionHost is { } caption)
        {
            caption.Text = Caption;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
