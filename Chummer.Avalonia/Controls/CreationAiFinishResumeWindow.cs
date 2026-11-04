using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TextWrapping = global::Avalonia.Media.TextWrapping;

namespace Chummer.Avalonia.Controls;

/// <summary>
/// Windows-desktop-style modal confirmation for the qualified durable staged
/// resumption of an interrupted AI completion run. Explains exactly what a
/// resume sends (the single unobserved stage, original frozen plan, original
/// operation identity) and what it does not (no retry of observed stages, no
/// new plan). Explicit primary action; closing the dialog keeps the saved
/// record untouched.
/// </summary>
public sealed class CreationAiFinishResumeWindow : Window
{
    public CreationAiFinishResumeWindow(string uncertainDomain)
    {
        Title = "Resume interrupted AI completion";
        Width = 520;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(16) };
        body.Children.Add(new TextBlock
        {
            Text = "Resume interrupted AI completion",
            Classes = { "shell-section-title" },
        });
        body.Children.Add(new TextBlock
        {
            Text = "A previous AI completion was interrupted after dispatching the '"
                + uncertainDomain + "' stage. Its outcome stayed uncertain: it may or may not have been saved.",
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = "Resume re-dispatches ONLY that stage under the original frozen plan and operation identity, "
                + "inside the same exclusive record. Observed stages are never sent again. No new plan is created. "
                + "The resume requires the same linked owner; the character is re-read first.",
            TextWrapping = TextWrapping.Wrap,
            Classes = { "shell-caption" },
        });

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        // Windows dialog convention: the destructive action is the default
        // button of a resume confirmation ONLY when explicitly chosen; here the
        // safe action ("Keep as saved") is the cancel path, Resume is explicit.
        var resumeButton = new Button { Content = "Resume" };
        resumeButton.Click += (_, _) => Close(true);
        var keepButton = new Button { Content = "Keep as saved", IsCancel = true };
        keepButton.Click += (_, _) => Close(false);
        footer.Children.Add(keepButton);
        footer.Children.Add(resumeButton);

        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(body);
        Content = root;
    }
}
