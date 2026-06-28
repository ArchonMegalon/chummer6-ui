#nullable enable annotations

using Chummer.Avalonia;
using Chummer.Desktop.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace Chummer.Tests.Presentation;

[TestClass]
public sealed class DesktopStartupUpdateWindowTests
{
    [TestMethod]
    public void Startup_update_progress_bar_uses_explicit_shell_progress_brushes()
    {
        string repoRoot = TestContextLocator.ResolveChummerPresentationRepoRoot();
        string source = File.ReadAllText(Path.Combine(repoRoot, "Chummer.Avalonia", "DesktopStartupUpdateWindow.cs"));

        StringAssert.Contains(source, "Name = \"StartupUpdateProgressBar\"");
        StringAssert.Contains(source, "Background = DesktopShellTheme.ResolveThemeBrush(\"ChummerShellProgressTrackBrush\", \"#1E293B\")");
        StringAssert.Contains(source, "Foreground = DesktopShellTheme.ResolveThemeBrush(\"ChummerShellProgressValueBrush\", \"#90C39A\")");
    }

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
    public void BuildViewState_relaunching_stage_keeps_wait_copy_visible_with_determinate_progress()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("relaunching", "Installing update and restarting Chummer", 1000, 1000));

        Assert.AreEqual("Restarting Chummer", state.Title);
        Assert.AreEqual("Installing update and restarting Chummer", state.Body);
        Assert.IsTrue(state.ShowWaitText);
        Assert.IsFalse(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(1000, state.ProgressValue);
    }

    [TestMethod]
    public void BuildViewState_failed_stage_hides_wait_copy_and_stays_indeterminate()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("failed", "Update check failed. Chummer will continue.", null, null));

        Assert.AreEqual("Update needs attention", state.Title);
        Assert.AreEqual("Update check failed. Chummer will continue.", state.Body);
        Assert.IsFalse(state.ShowWaitText);
        Assert.IsTrue(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(0, state.ProgressValue);
    }

    [TestMethod]
    public void BuildViewState_manual_stage_uses_ready_copy_without_wait_text()
    {
        DesktopStartupUpdateViewState state = DesktopStartupUpdateWindow.BuildViewState(
            new DesktopUpdateProgressUpdate("manual", "Install the update manually from Downloads.", null, null));

        Assert.AreEqual("Update ready", state.Title);
        Assert.AreEqual("Install the update manually from Downloads.", state.Body);
        Assert.IsFalse(state.ShowWaitText);
        Assert.IsTrue(state.IsIndeterminate);
        Assert.AreEqual(1000, state.ProgressMaximum);
        Assert.AreEqual(0, state.ProgressValue);
    }

    [TestMethod]
    public void BuildAttentionMessage_uses_plain_recoverable_language()
    {
        Assert.AreEqual(
            "A newer build is available. Open Devices & Access when you want to update.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("notify_only"));
        Assert.AreEqual(
            "Chummer could not reach the update list. This copy will keep running.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("manifest_load_failed"));
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("update_schedule_failed"));
        Assert.AreEqual(
            "The newest build is paused. This copy will keep running.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("rollout_blocked"));
        Assert.AreEqual(
            "A macOS update is ready. Open Update Status to install it manually; this copy will stay usable.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("macos_manual_install_required"));
        Assert.AreEqual(
            "This copy will keep running.",
            DesktopStartupUpdateWindow.BuildAttentionMessage("anything_else"));
    }

    [TestMethod]
    public void Runtime_failure_attention_message_surfaces_specific_missing_payload_copy()
    {
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The installer payload was missing.",
            InvokeRuntimeFailureAttentionMessage("Update preparation failed: InvalidOperationException: Bundled desktop payload was not found."));
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The installer payload was missing.",
            InvokeRuntimeFailureAttentionMessage("Update preparation failed: InvalidOperationException: The staged desktop payload did not contain 'Chummer.Avalonia.exe'."));
    }

    [TestMethod]
    public void Runtime_failure_attention_message_surfaces_integrity_failure_copy()
    {
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The downloaded update did not pass integrity checks.",
            InvokeRuntimeFailureAttentionMessage("Update preparation failed: InvalidOperationException: Desktop update artifact 'foo.zip' failed checksum validation."));
    }

    [TestMethod]
    public void Runtime_failure_attention_message_surfaces_disposed_helper_copy()
    {
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The local update helper closed before the handoff finished.",
            InvokeRuntimeFailureAttentionMessage("Update preparation failed: ObjectDisposedException: Cannot access a disposed object."));
        Assert.AreEqual(
            "The update could not be prepared. This copy will keep running. The local update helper closed before the handoff finished.",
            InvokeRuntimeFailureAttentionMessage("ObjectDisposedException: Cannot access a disposed object."));
    }

    [TestMethod]
    public void GetCompletionDisplayDelayMs_keeps_relaunch_and_failures_visible_long_enough_to_perceive()
    {
        Assert.AreEqual(1200, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: true, reason: "apply_scheduled"));
        Assert.AreEqual(1800, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: false, reason: "failed"));
        Assert.AreEqual(1600, DesktopStartupUpdateWindow.GetCompletionDisplayDelayMs(exitRequested: false, reason: "notify_only"));
    }

    private static string InvokeRuntimeFailureAttentionMessage(string detail)
    {
        MethodInfo? method = typeof(DesktopUpdateRuntime).GetMethod(
            "BuildAttentionMessageForUpdateScheduleFailure",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (string)method.Invoke(null, [detail])!;
    }
}
