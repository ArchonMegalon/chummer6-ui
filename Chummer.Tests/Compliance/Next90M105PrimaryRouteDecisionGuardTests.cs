#nullable enable annotations

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
        StringAssert.Contains(markup, "Keep Local Work");
        StringAssert.Contains(markup, "Save Local Work");
        StringAssert.Contains(markup, "Review Campaign Workspace");
        StringAssert.Contains(markup, "Workspace Support");

        StringAssert.Contains(code, "StaleStateStatusText.Text = staleStateSummary ?? string.Empty;");
        StringAssert.Contains(code, "ConflictChoiceStatusText.Text = conflictChoiceSummary ?? string.Empty;");
        StringAssert.Contains(code, "|| !string.IsNullOrWhiteSpace(staleStateSummary)");
        StringAssert.Contains(code, "|| !string.IsNullOrWhiteSpace(conflictChoiceSummary)");
        StringAssert.Contains(code, "Decision gate: Chummer will not replace local work automatically");
        StringAssert.Contains(code, "Decision order: 1. keep local work visible, 2. save local work when available, 3. review Campaign Workspace, 4. open Workspace Support before accepting restore replacement.");
        StringAssert.Contains(code, "SaveLocalWorkButton.IsEnabled = canSaveLocalWorkBeforeRestore;");
        StringAssert.Contains(code, "restore-decision-keep-local-work");
        StringAssert.Contains(code, "restore-decision-review-campaign-workspace");
        StringAssert.Contains(code, "restore-decision-open-workspace-support");
        StringAssert.Contains(code, "AutomationProperties.SetName(RestoreContinuityStatusBorder, \"Restore continuity decision gate\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(StaleStateStatusText, \"Stale state visibility status\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(ConflictChoiceStatusText, \"Conflict choice status\")");
        StringAssert.Contains(code, "AutomationProperties.SetName(RestoreContinuityDecisionOrderText, \"Restore decision order\")");
        StringAssert.Contains(code, "AutomationProperties.SetHelpText(OpenWorkspaceSupportButton, \"Open support with restore, stale-state, and conflict-choice context.\")");

        StringAssert.Contains(projector, "RestoreContinuitySummary: BuildRestoreContinuitySummary(workspaceContext, language)");
        StringAssert.Contains(projector, "StaleStateSummary: BuildStaleStateSummary(shellSurface, workspaceContext, language)");
        StringAssert.Contains(projector, "ConflictChoiceSummary: BuildConflictChoiceSummary(workspaceContext, language)");
        StringAssert.Contains(projector, "CanSaveLocalWorkBeforeRestore: CanSaveLocalWorkBeforeRestore(workspaceContext)");

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
