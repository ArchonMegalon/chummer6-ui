#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class Next90M105PrimaryRouteDecisionGuardTests
{
    [TestMethod]
    public void Primary_route_restore_decision_gate_keeps_stale_and_conflict_choices_separate()
    {
        string repoRoot = FindRepoRoot();
        string markup = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "Controls",
            "SummaryHeaderControl.axaml"));
        string code = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "Controls",
            "SummaryHeaderControl.axaml.cs"));
        string projector = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "MainWindow.ShellFrameProjector.cs"));
        string eventHandlers = File.ReadAllText(Path.Combine(
            repoRoot,
            "Chummer.Avalonia",
            "MainWindow.EventHandlers.cs"));

        StringAssert.Contains(markup, "RestoreContinuityStatusText");
        StringAssert.Contains(markup, "StaleStateStatusText");
        StringAssert.Contains(markup, "ConflictChoiceStatusText");
        StringAssert.Contains(markup, "RestoreContinuityDecisionOrderText");
        StringAssert.Contains(markup, "Keep Local");
        StringAssert.Contains(markup, "Save");
        StringAssert.Contains(markup, "Campaign");
        StringAssert.Contains(markup, "Support");

        StringAssert.Contains(code, "StaleStateStatusText.Text = state.StaleStateSummary ?? string.Empty;");
        StringAssert.Contains(code, "ConflictChoiceStatusText.Text = state.ConflictChoiceSummary ?? string.Empty;");
        StringAssert.Contains(code, "|| !string.IsNullOrWhiteSpace(state.StaleStateSummary)");
        StringAssert.Contains(code, "|| !string.IsNullOrWhiteSpace(state.ConflictChoiceSummary)");
        StringAssert.Contains(code, "Save first if needed before changing this desktop copy.");
        StringAssert.Contains(code, "Keep local work or open support before changing this desktop copy.");
        StringAssert.Contains(code, "SaveLocalWorkButton.IsEnabled = state.CanSaveLocalWorkBeforeRestore;");
        StringAssert.Contains(code, "restore-decision-keep-local-work");
        StringAssert.Contains(code, "restore-decision-review-campaign-workspace");
        StringAssert.Contains(code, "restore-decision-open-workspace-support");
        StringAssert.Contains(code, "AutomationProperties.SetName(RestoreContinuityStatusBorder, \"Workspace continuity gate\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(StaleStateStatusText, \"Stale state visibility status\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(ConflictChoiceStatusText, \"Workspace review status\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(RestoreContinuityDecisionOrderText, \"Workspace decision order\")");
        StringAssert.Contains(code, "AutomationProperties.SetHelpText(OpenWorkspaceSupportButton, \"Open Workspace Support with the current workspace context attached.\")");

        StringAssert.Contains(projector, "HasVisibleContent: false");
        Assert.IsFalse(projector.Contains("RestoreContinuitySummary:", StringComparison.Ordinal));
        Assert.IsFalse(projector.Contains("StaleStateSummary:", StringComparison.Ordinal));
        Assert.IsFalse(projector.Contains("ConflictChoiceSummary:", StringComparison.Ordinal));
        Assert.IsFalse(projector.Contains("CanSaveLocalWorkBeforeRestore:", StringComparison.Ordinal));

        StringAssert.Contains(eventHandlers, "SummaryHeader_OnWorkspaceSupportRequested");
        StringAssert.Contains(eventHandlers, "DesktopInstallLinkingRuntime.TryOpenSupportPortalForWorkspace(installState, ResolveActiveSupportWorkspace())");
    }

    private static string FindRepoRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Chummer.sln")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Could not locate Chummer.sln from the current test directory.");
        return string.Empty;
    }
}
