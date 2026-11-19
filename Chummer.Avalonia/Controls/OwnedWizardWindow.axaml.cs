using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Gen6 T795: reusable owned-window host for wizards. Windows created with
/// this host are owned by the main shell (ShowInTaskbar=False, CenterOwner),
/// render their content on the desktop design tokens, and expose the hosted
/// wizard title through <see cref="WizardTitle"/> for the native title bar.
/// </summary>
public partial class OwnedWizardWindow : Window
{
    public static readonly StyledProperty<string> WizardTitleProperty =
        AvaloniaProperty.Register<OwnedWizardWindow, string>(
            nameof(WizardTitle), "Wizard");

    public OwnedWizardWindow()
    {
        InitializeComponent();
        AttachCaptionBinding();
    }

    public OwnedWizardWindow(string wizardTitle, Control content)
        : this()
    {
        WizardTitle = wizardTitle;
        WizardContentHost.Content = content;
    }

    public string WizardTitle
    {
        get => GetValue(WizardTitleProperty);
        set => SetValue(WizardTitleProperty, value);
    }

    /// <summary>Replaces or clears the hosted wizard content (null clears).</summary>
    public void SetContent(Control? content)
    {
        WizardContentHost.Content = content;
    }

    /// <summary>Updates the chrome strip caption without changing the title.</summary>
    public void SetStepCaption(string caption)
    {
        if (WizardContentHost.Content is not null
            && this.FindControl<OwnedWizardChrome>("WizardChrome") is { } chrome)
        {
            chrome.Caption = caption;
        }
    }

    private void AttachCaptionBinding()
    {
        if (this.FindControl<OwnedWizardChrome>("WizardChrome") is { } chrome)
        {
            var binding = new global::Avalonia.Data.Binding(nameof(WizardTitle))
            {
                Source = this,
                Mode = global::Avalonia.Data.BindingMode.OneWay,
            };
            chrome[!OwnedWizardChrome.CaptionProperty] = binding;
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
