using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopClaimPresentationTests
{
    private static DesktopInstallLinkingState Guest() => new(
        InstallationId: "ins-passive-presentation-test", HeadId: "avalonia",
        ApplicationVersion: "1.0.0", ChannelId: "preview", Platform: "windows", Arch: "x64",
        Status: "guest", CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow,
        LaunchCount: 1, LastStartedAtUtc: null, ClaimedAtUtc: null, LastPromptDismissedAtUtc: null,
        PublicKey: "inert-test-key", PrivateKey: "inert-test-key");

    [TestMethod]
    public void Guest_shell_title_does_not_retain_claim_modal_title()
        => Assert.AreEqual("Chummer Desktop", MainWindow.BuildDesktopWindowTitle("Chummer Desktop", "Claim your copy", Guest()));

    [TestMethod]
    public void Guest_claim_has_one_visible_link_action_and_short_window_scroll()
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            var context = new DesktopInstallLinkingStartupContext(Guest(), null, null, true, "presentation_test");
            var window = new DesktopInstallLinkingWindow(context, loginVideoPreview: true)
            { Width = 880, Height = 540 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var buttons = window.GetVisualDescendants().OfType<Button>().ToArray();
                var links = buttons.Where(b => b.IsEffectivelyVisible
                    && (b.Content?.ToString() ?? "").Contains("Link installation", StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(1, links.Length, "Primary claim remains the only visible guest Link action.");
                var scroll = window.GetVisualDescendants().OfType<ScrollViewer>()
                    .First(s => s.VerticalScrollBarVisibility == global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
                Assert.IsTrue(scroll.Viewport.Height > 0);
                Assert.IsTrue(scroll.Extent.Height > scroll.Viewport.Height, "Short-window content must scroll rather than clip.");
                scroll.ScrollToEnd();
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(scroll.Offset.Y > 0, "Bottom content can be reached without resizing.");
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
