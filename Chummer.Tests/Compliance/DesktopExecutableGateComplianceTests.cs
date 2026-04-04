#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DesktopExecutableGateComplianceTests
{
    [TestMethod]
    public void Desktop_executable_gate_fail_closes_missing_required_pair_coverage_in_required_rid_tuples()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "release_channel_required_platform_head_pairs_for_matrix");
        StringAssert.Contains(scriptText, "release_channel_required_platform_head_pairs_from_required_rid_tuples");
        StringAssert.Contains(scriptText, "release_channel_missing_required_platform_head_pairs_from_required_rid_tuples");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage requiredDesktopPlatformHeadRidTuples is missing required desktop platform/head pair coverage:");
    }

    [TestMethod]
    public void Desktop_executable_gate_binds_visual_and_workflow_receipts_to_release_channel_identity()
    {
        string repoRoot = FindRepoRoot();
        string executableScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string visualScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string workflowScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");

        string executableScriptText = File.ReadAllText(executableScriptPath);
        string visualScriptText = File.ReadAllText(visualScriptPath);
        string workflowScriptText = File.ReadAllText(workflowScriptPath);

        StringAssert.Contains(executableScriptText, "visual_familiarity.release_channel_channel_id");
        StringAssert.Contains(executableScriptText, "workflow_execution.release_channel_channel_id");
        StringAssert.Contains(executableScriptText, "visual_familiarity_release_channel_id");
        StringAssert.Contains(executableScriptText, "workflow_execution_release_channel_id");
        StringAssert.Contains(executableScriptText, "Desktop visual familiarity exit gate release-channel identity does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Desktop workflow execution gate release-channel identity does not match release channel channelId.");

        StringAssert.Contains(visualScriptText, "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(visualScriptText, "release_channel_channel_id");
        StringAssert.Contains(visualScriptText, "Desktop visual familiarity exit gate release channel receipt is missing channelId/channel.");

        StringAssert.Contains(workflowScriptText, "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(workflowScriptText, "release_channel_channel_id");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing channelId/channel.");
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.GetFullPath(Path.Combine(current, "..", "..", "..", ".."));
            string expectedScriptPath = Path.Combine(candidate, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
            if (File.Exists(expectedScriptPath))
            {
                return candidate;
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent ?? string.Empty;
        }

        throw new DirectoryNotFoundException("Unable to locate chummer6-ui repo root for compliance test.");
    }
}
