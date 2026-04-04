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
    public void Desktop_executable_gate_surfaces_windows_and_macos_per_head_diagnostics_from_required_tuple_policy_when_release_artifacts_are_missing()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "required_windows_policy_tuples");
        StringAssert.Contains(scriptText, "windows_policy_tuples_missing_release_artifacts");
        StringAssert.Contains(scriptText, "required_macos_policy_tuples");
        StringAssert.Contains(scriptText, "macos_policy_tuples_missing_release_artifacts");
        StringAssert.Contains(scriptText, "required_tuple_policy_missing_release_artifact");
        StringAssert.Contains(scriptText, "evidence[\"windows_policy_required_head_rid_tuples\"]");
        StringAssert.Contains(scriptText, "evidence[\"windows_policy_tuples_missing_release_artifacts\"]");
        StringAssert.Contains(scriptText, "evidence[\"macos_policy_required_head_rid_tuples\"]");
        StringAssert.Contains(scriptText, "evidence[\"macos_policy_tuples_missing_release_artifacts\"]");
        StringAssert.Contains(scriptText, "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY=\"$head\"");
        StringAssert.Contains(scriptText, "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID=\"$rid\"");
        StringAssert.Contains(scriptText, "CHUMMER_MACOS_DESKTOP_EXIT_GATE_APP_KEY=\"$head\"");
        StringAssert.Contains(scriptText, "CHUMMER_MACOS_DESKTOP_EXIT_GATE_RID=\"$rid\"");
        StringAssert.Contains(scriptText, "requiredDesktopPlatformHeadRidTuples");
        StringAssert.Contains(scriptText, "UI_WINDOWS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "UI_MACOS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "expected_artifact_source = normalize_token(expected_artifact.get(\"source\"))");
        StringAssert.Contains(scriptText, "policy_missing_release_artifact = expected_artifact_source == \"required_tuple_policy_missing_release_artifact\"");
        StringAssert.Contains(scriptText, "if not policy_missing_release_artifact:");
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
        StringAssert.Contains(executableScriptText, "visual_familiarity_release_version");
        StringAssert.Contains(executableScriptText, "workflow_execution_release_version");
        StringAssert.Contains(executableScriptText, "\"channelId\": release_channel_channel_id");
        StringAssert.Contains(executableScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(executableScriptText, "Desktop visual familiarity exit gate release-channel identity does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Desktop workflow execution gate release-channel identity does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Desktop visual familiarity exit gate releaseVersion does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Desktop workflow execution gate releaseVersion does not match release channel version.");
        StringAssert.Contains(executableScriptText, "canonical_required_desktop_heads = [\"avalonia\", \"blazor-desktop\"]");
        StringAssert.Contains(executableScriptText, "missing_canonical_promoted_desktop_heads");
        StringAssert.Contains(executableScriptText, "missing_canonical_flagship_desktop_heads");
        StringAssert.Contains(executableScriptText, "release_channel_tuple_coverage_missing_required_platforms_from_policy");
        StringAssert.Contains(executableScriptText, "release_channel_tuple_coverage_missing_required_heads_from_policy");
        StringAssert.Contains(executableScriptText, "release_channel_tuple_coverage_missing_canonical_required_heads");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt is missing releaseVersion/version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt is missing releaseVersion/version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt releaseVersion/version does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt is missing version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt version does not match release channel version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt is missing releaseVersion/version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "gate_release_version");
        StringAssert.Contains(executableScriptText, "startup_smoke_version");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_version");
        StringAssert.Contains(executableScriptText, "Release channel is missing canonical required promoted desktop head(s) for milestone-3 executable proof:");
        StringAssert.Contains(executableScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 executable proof:");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopPlatforms is missing required policy platform(s):");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing required policy head(s):");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing canonical required head(s):");

        StringAssert.Contains(visualScriptText, "CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(visualScriptText, "release_channel_channel_id");
        StringAssert.Contains(visualScriptText, "release_channel_version");
        StringAssert.Contains(visualScriptText, "Desktop visual familiarity exit gate release channel receipt is missing channelId/channel.");
        StringAssert.Contains(visualScriptText, "Desktop visual familiarity exit gate release channel receipt is missing version.");
        StringAssert.Contains(visualScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(visualScriptText, "canonical_required_desktop_heads = [\"avalonia\", \"blazor-desktop\"]");
        StringAssert.Contains(visualScriptText, "flagship_missing_canonical_required_desktop_heads");
        StringAssert.Contains(visualScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head visual proof:");

        StringAssert.Contains(workflowScriptText, "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(workflowScriptText, "release_channel_channel_id");
        StringAssert.Contains(workflowScriptText, "release_channel_version");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing channelId/channel.");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing version.");
        StringAssert.Contains(workflowScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(workflowScriptText, "canonical_required_desktop_heads = [\"avalonia\", \"blazor-desktop\"]");
        StringAssert.Contains(workflowScriptText, "flagship_missing_canonical_required_desktop_heads");
        StringAssert.Contains(workflowScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head workflow execution proof:");
    }

    [TestMethod]
    public void Windows_and_macos_exit_gate_materializers_do_not_resolve_proof_from_legacy_chummer5a_paths()
    {
        string repoRoot = FindRepoRoot();
        string windowsScriptPath = Path.Combine(repoRoot, "scripts", "materialize-windows-desktop-exit-gate.sh");
        string macosScriptPath = Path.Combine(repoRoot, "scripts", "materialize-macos-desktop-exit-gate.sh");

        string windowsScriptText = File.ReadAllText(windowsScriptPath);
        string macosScriptText = File.ReadAllText(macosScriptPath);

        StringAssert.Contains(windowsScriptText, "Promoted Windows installer was not resolved from the repo-local desktop shelf.");
        StringAssert.Contains(macosScriptText, "Promoted macOS installer was not resolved from the repo-local desktop shelf");
        Assert.IsFalse(windowsScriptText.Contains("/docker/chummer5a/", StringComparison.Ordinal));
        Assert.IsFalse(macosScriptText.Contains("/docker/chummer5a/", StringComparison.Ordinal));
        Assert.IsFalse(windowsScriptText.Contains("legacy chummer5a", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(macosScriptText.Contains("legacy chummer5a", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoRoot()
    {
        string current = Path.GetFullPath(AppContext.BaseDirectory);
        while (!string.IsNullOrEmpty(current))
        {
            string directCandidateScriptPath = Path.Combine(
                current,
                "scripts",
                "ai",
                "milestones",
                "materialize-desktop-executable-exit-gate.sh");
            if (File.Exists(directCandidateScriptPath))
            {
                return current;
            }

            string siblingCandidateRoot = Path.Combine(current, "chummer6-ui");
            string siblingCandidateScriptPath = Path.Combine(
                siblingCandidateRoot,
                "scripts",
                "ai",
                "milestones",
                "materialize-desktop-executable-exit-gate.sh");
            if (File.Exists(siblingCandidateScriptPath))
            {
                return siblingCandidateRoot;
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
