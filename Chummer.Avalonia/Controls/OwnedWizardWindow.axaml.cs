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

    // Gen6 F08: the native Window.Title must reflect the wizard, not the
    // static "Wizard" default -- bind it to the WizardTitle property.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == WizardTitleProperty)
            Title = e.GetNewValue<string>();
    }

    public OwnedWizardWindow(string wizardTitle, Control content)
        : this()
    {
        WizardTitle = wizardTitle;
        SetContent(content);
    }

    private global::Avalonia.Controls.Presenters.ContentPresenter? ResolveContentHost()
        => this.FindControl<global::Avalonia.Controls.Presenters.ContentPresenter>("WizardContentHost");

    public string WizardTitle
    {
        get => GetValue(WizardTitleProperty);
        set => SetValue(WizardTitleProperty, value);
    }

    /// <summary>Replaces or clears the hosted wizard content (null clears).</summary>
    public void SetContent(Control? content)
    {
        if (ResolveContentHost() is { } host)
            host.Content = content;
    }

    /// <summary>Updates the chrome strip caption without changing the title.</summary>
    public void SetStepCaption(string caption)
    {
        if (ResolveContentHost() is { }
            && this.FindControl<OwnedWizardChrome>("WizardChrome") is { } chrome)
        {
            chrome.Caption = caption;
        }
    }

    /// <summary>
    /// Gen6 F08: publishes the readable selected-step context (label plus
    /// "Step n of m") to the chrome caption AND the native window title, so
    /// Back/Next navigation and step requests are reflected immediately.
    /// </summary>
    public void SetStepContext(string? stepLabel, int stepIndex, int stepCount)
    {
        string caption = stepCount > 0 && stepIndex > 0
            ? $"Step {stepIndex} of {stepCount}"
                + (string.IsNullOrWhiteSpace(stepLabel) ? "" : $": {stepLabel}")
            : (stepLabel ?? "");
        SetStepCaption(caption);
        string title = WizardTitle;
        Title = string.IsNullOrEmpty(caption) ? title : $"{title} \u2014 {caption}";
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
