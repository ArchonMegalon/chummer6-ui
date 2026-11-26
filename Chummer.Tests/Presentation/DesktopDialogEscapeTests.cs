using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Chummer.Avalonia;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopDialogEscapeTests
{
    [TestMethod]
    public void Escape_refuses_disabled_cancel_without_dispatch()
        => Verify(buttonEnabled: false, parentEnabled: true, parentVisible: true, expected: false);

    [TestMethod]
    public void Escape_refuses_effectively_disabled_cancel_without_dispatch()
        => Verify(buttonEnabled: true, parentEnabled: false, parentVisible: true, expected: false);

    [TestMethod]
    public void Escape_refuses_effectively_hidden_cancel_without_dispatch()
        => Verify(buttonEnabled: true, parentEnabled: true, parentVisible: false, expected: false);

    [TestMethod]
    public void Escape_dispatches_enabled_cancel_exactly_once()
        => Verify(buttonEnabled: true, parentEnabled: true, parentVisible: true, expected: true);

    private static void Verify(bool buttonEnabled, bool parentEnabled, bool parentVisible, bool expected)
    {
        CharacterCreationAiFinishGateAvaloniaTests.WithHeadlessUi(() =>
        {
            Button cancel = new() { Content = "Cancel", Tag = "cancel", IsEnabled = buttonEnabled };
            StackPanel panel = new() { IsEnabled = parentEnabled, IsVisible = parentVisible };
            panel.Children.Add(cancel);
            Window host = new() { Content = panel, Width = 400, Height = 200 };
            host.Show();
            Dispatcher.UIThread.RunJobs();
            try
            {
                int dispatches = 0;
                string? action = null;
                bool handled = DesktopDialogWindow.TryExecuteEscapeAction(new[] { cancel }, id =>
                {
                    dispatches++;
                    action = id;
                });
                Assert.AreEqual(expected, handled, "Only an effectively interactive affordance handles Escape.");
                Assert.AreEqual(expected ? 1 : 0, dispatches, "Refused Escape must not reach the adapter callback.");
                Assert.AreEqual(expected ? "cancel" : null, action);
            }
            finally
            {
                host.Close();
                Dispatcher.UIThread.RunJobs();
            }
        });
    }
}
