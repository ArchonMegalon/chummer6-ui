using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Chummer.Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopStatusStripTests
{
    [TestMethod]
    public void Empty_status_does_not_invent_ruleset_identity()
        => Assert.AreEqual(string.Empty, StatusStripDisplayText.Summary(""));

    [TestMethod]
    public void Classic_strip_keeps_diagnostics_in_secondary_details()
        => Verify(classic: true, width: 1280);

    [TestMethod]
    public void Alternate_strip_keeps_diagnostics_in_secondary_details()
        => Verify(classic: false, width: 1280);

    [TestMethod]
    public void Classic_strip_keeps_details_reachable_at_narrow_width()
        => Verify(classic: true, width: 640);

    [TestMethod]
    public void Alternate_strip_keeps_details_reachable_at_narrow_width()
        => Verify(classic: false, width: 640);

    private static void Verify(bool classic, double width)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            UserControl strip = classic ? new ClassicStatusStrip() : new StatusStripControl();
            Window host = new() { Content = strip, Width = width, Height = 160 };
            host.Show();
            try
            {
                foreach (int edition in new[] { 4, 5, 6 })
                {
                    string summary = $"Ruleset: Shadowrun {edition} .chum{edition}";
                    string full = summary + " | Workflows: 42 defs / 17 surfaces | Prefs: 125%/dark/en-us";
                    StatusStripState state = new("Character ready", "Services ready", "12:00", full) { SaveState = "Saved" };
                    if (strip is ClassicStatusStrip c) c.SetState(state);
                    else ((StatusStripControl)strip).SetState(state);
                    Dispatcher.UIThread.RunJobs();
                    TextBlock visible = strip.FindControl<TextBlock>("ComplianceStateText")!;
                    Assert.AreEqual(summary, visible.Text);
                    Assert.IsFalse(visible.Text!.Contains("Workflows:", StringComparison.Ordinal));
                    Button button = strip.FindControl<Button>("StatusDetailsButton")!;
                    Assert.IsTrue(button.Focusable, "Secondary details must remain keyboard reachable.");
                    Assert.IsTrue(button.IsEffectivelyVisible);
                    Assert.IsTrue(button.IsEffectivelyEnabled);
                    Assert.IsTrue(button.Bounds.Width > 0);
                    Assert.IsTrue(button.Bounds.Right <= strip.Bounds.Width + 1, "Details must not be clipped horizontally.");
                    Flyout flyout = (Flyout)button.Flyout!;
                    Assert.AreEqual(full, ((TextBlock)flyout.Content!).Text);
                    flyout.ShowAt(button);
                    Dispatcher.UIThread.RunJobs();
                    Assert.IsTrue(flyout.IsOpen, "Actual secondary disclosure opens.");
                    flyout.Hide();
                    Assert.AreEqual("Saved", strip.FindControl<TextBlock>("SaveStateText")!.Text);
                    Assert.IsTrue((ToolTip.GetTip(strip)?.ToString() ?? "").Contains("Prefs:", StringComparison.Ordinal));
                }
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
