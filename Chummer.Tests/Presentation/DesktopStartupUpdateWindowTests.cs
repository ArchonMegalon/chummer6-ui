#nullable enable annotations

using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopStartupUpdateWindowTests
{
    [TestMethod]
    public void BuildViewState_maps_progress_stage_to_visible_copy_and_determinate_progress()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("downloading", "Downloading the next build.", 320, 1000));

        Assert.AreEqual("Downloading update", state.Title);
        Assert.AreEqual("Downloading the next build.", state.Body);
        Assert.IsTrue(state.ShowWaitText);
        Assert.IsFalse(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(320, state.ProgressValue);
    }

    [TestMethod]
    public void BuildViewState_keeps_non_transfer_stages_indeterminate_and_hides_wait_copy()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("available", "A newer build is available."));

        Assert.AreEqual("Update available", state.Title);
        Assert.AreEqual("A newer build is available.", state.Body);
        Assert.IsFalse(state.ShowWaitText);
        Assert.IsTrue(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(0, state.ProgressValue);
    }

    [TestMethod]
    public void BuildViewState_clamps_progress_value_and_falls_back_for_unknown_stage()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("mystery", "Working.", 1500, 1000));

        Assert.AreEqual("Updating Chummer", state.Title);
        Assert.AreEqual("Working.", state.Body);
        Assert.IsFalse(state.ShowWaitText);
        Assert.IsFalse(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(1000, state.ProgressValue);
    }

    [TestMethod]
    public void BuildAttentionMessage_uses_plain_recoverable_language()
    {
        Assert.AreEqual(
            "A newer build is available. Open Devices & Access when you want to update.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("notify_only"));
        Assert.AreEqual(
            "A macOS update is ready. Open Downloads to install it manually; this copy will stay usable.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("macos_manual_install_required"));
        Assert.AreEqual(
            "This copy will keep running.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("anything_else"));
    }

    [TestMethod]
    public void GetCompletionDisplayDelayMs_keeps_relaunch_and_failures_visible_long_enough_to_perceive()
    {
        Assert.AreEqual(1200, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: true, reason: "apply_scheduled"));
        Assert.AreEqual(1800, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: false, reason: "failed"));
        Assert.AreEqual(1600, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: false, reason: "notify_only"));
    }
}
