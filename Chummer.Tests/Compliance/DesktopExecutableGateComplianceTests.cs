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
    public void Desktop_executable_gate_surfaces_linux_windows_and_macos_per_head_diagnostics_from_required_tuple_policy_when_release_artifacts_are_missing()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "required_linux_policy_tuples");
        StringAssert.Contains(scriptText, "linux_policy_tuples_missing_release_artifacts");
        StringAssert.Contains(scriptText, "required_windows_policy_tuples");
        StringAssert.Contains(scriptText, "windows_policy_tuples_missing_release_artifacts");
        StringAssert.Contains(scriptText, "required_macos_policy_tuples");
        StringAssert.Contains(scriptText, "macos_policy_tuples_missing_release_artifacts");
        StringAssert.Contains(scriptText, "required_tuple_policy_missing_release_artifact");
        StringAssert.Contains(scriptText, "evidence[\"linux_policy_required_head_rid_tuples\"]");
        StringAssert.Contains(scriptText, "evidence[\"linux_policy_tuples_missing_release_artifacts\"]");
        StringAssert.Contains(scriptText, "evidence[\"windows_policy_required_head_rid_tuples\"]");
        StringAssert.Contains(scriptText, "evidence[\"windows_policy_tuples_missing_release_artifacts\"]");
        StringAssert.Contains(scriptText, "evidence[\"macos_policy_required_head_rid_tuples\"]");
        StringAssert.Contains(scriptText, "evidence[\"macos_policy_tuples_missing_release_artifacts\"]");
        StringAssert.Contains(scriptText, "CHUMMER_LINUX_DESKTOP_EXIT_GATE_APP_KEY=\"$head\"");
        StringAssert.Contains(scriptText, "CHUMMER_LINUX_DESKTOP_EXIT_GATE_RID=\"$rid\"");
        StringAssert.Contains(scriptText, "CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH=\"$linux_gate_tuple_path\"");
        StringAssert.Contains(scriptText, "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY=\"$head\"");
        StringAssert.Contains(scriptText, "CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID=\"$rid\"");
        StringAssert.Contains(scriptText, "CHUMMER_MACOS_DESKTOP_EXIT_GATE_APP_KEY=\"$head\"");
        StringAssert.Contains(scriptText, "CHUMMER_MACOS_DESKTOP_EXIT_GATE_RID=\"$rid\"");
        StringAssert.Contains(scriptText, "requiredDesktopPlatformHeadRidTuples");
        StringAssert.Contains(scriptText, "UI_LINUX_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "UI_WINDOWS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "UI_MACOS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json");
        StringAssert.Contains(scriptText, "platform not in {\"windows\", \"macos\", \"linux\"}");
        StringAssert.Contains(scriptText, "evidence.setdefault(\"linux_gates\", {})[gate_label] = gate_evidence");
        StringAssert.Contains(scriptText, "expected_artifact_source = normalize_token(expected_artifact.get(\"source\"))");
        StringAssert.Contains(scriptText, "policy_missing_release_artifact = expected_artifact_source == \"required_tuple_policy_missing_release_artifact\"");
        StringAssert.Contains(scriptText, "if not policy_missing_release_artifact:");
    }

    [TestMethod]
    public void Desktop_executable_gate_fail_closes_stale_passing_linux_windows_and_macos_tuple_receipts_that_are_not_promoted()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "(\"linux\", \"UI_LINUX*_DESKTOP_EXIT_GATE.generated.json\")");
        StringAssert.Contains(scriptText, "promoted_linux_tuples");
        StringAssert.Contains(scriptText, "stale_linux_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "stale_passing_platform_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "Stale passing platform gate receipts exist for non-promoted desktop tuples:");
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
        StringAssert.Contains(executableScriptText, "visual_familiarity_release_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "workflow_execution_release_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "visual_familiarity_release_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "workflow_execution_release_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "visual_familiarity_release_version");
        StringAssert.Contains(executableScriptText, "workflow_execution_release_version");
        StringAssert.Contains(executableScriptText, "\"channelId\": release_channel_channel_id");
        StringAssert.Contains(executableScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(executableScriptText, "Desktop visual familiarity exit gate carries conflicting release-channel identity aliases across evidence and gate envelope.");
        StringAssert.Contains(executableScriptText, "Desktop workflow execution gate carries conflicting release-channel identity aliases across evidence and gate envelope.");
        StringAssert.Contains(executableScriptText, "Desktop visual familiarity exit gate carries conflicting release-version aliases across evidence and gate envelope.");
        StringAssert.Contains(executableScriptText, "Desktop workflow execution gate carries conflicting release-version aliases across evidence and gate envelope.");
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
        StringAssert.Contains(executableScriptText, "duplicate_desktop_install_artifact_tuples");
        StringAssert.Contains(executableScriptText, "duplicate_desktop_install_artifact_tuple_tokens");
        StringAssert.Contains(executableScriptText, "Release channel publishes duplicate desktop install media tuple entries (head:rid:platform):");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_tuple_coverage_complete");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt is missing releaseVersion/version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt carries conflicting releaseVersion/version alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt head channelId/channel does not match release channel for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt head carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt carries conflicting version/releaseVersion alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt status is not passing for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt rid is missing for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt rid does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "Release channel Linux artifact arch does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing channelId/channel:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) channelId/channel does not match release channel channelId:");
        StringAssert.Contains(executableScriptText, "Linux startup smoke external blocker must be blank when installer startup smoke receipt exists for promoted head");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt is missing releaseVersion/version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt releaseVersion/version does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt channelId/channel does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt checks.release_channel_id does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt checks.release_channel_version does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt carries conflicting releaseVersion/version alias values.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt is missing version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt version does not match release channel version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting channelId/channel alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting version/releaseVersion alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting arch/architecture alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt rid is missing for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt rid does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Release channel Windows artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "Release channel Windows artifact arch does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact channelId/channel does not match promoted release channel.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact is missing version/releaseVersion.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact version/releaseVersion does not match promoted release channel version.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting version/releaseVersion alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact arch does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke external blocker must be blank when startup smoke receipt exists for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt is missing releaseVersion/version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt channelId/channel does not match release channel channelId for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt checks.release_channel_id does not match release channel channelId for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt carries conflicting releaseVersion/version alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting version/releaseVersion alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting arch/architecture alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt rid is missing for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt rid does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "Release channel macOS artifact carries conflicting arch/architecture alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Release channel macOS artifact arch does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact channelId/channel does not match promoted release channel.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact is missing version/releaseVersion.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact version/releaseVersion does not match promoted release channel version.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact carries conflicting version/releaseVersion alias values.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact arch does not match promoted release channel RID.");
        StringAssert.Contains(executableScriptText, "macOS startup smoke external blocker must be blank when startup smoke receipt exists for promoted head");
        StringAssert.Contains(executableScriptText, "gate_release_version");
        StringAssert.Contains(executableScriptText, "gate_release_version_primary");
        StringAssert.Contains(executableScriptText, "gate_release_version_alias");
        StringAssert.Contains(executableScriptText, "gate_release_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "gate_head_channel_id");
        StringAssert.Contains(executableScriptText, "gate_head_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "gate_channel_id");
        StringAssert.Contains(executableScriptText, "gate_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "checks_release_channel_id");
        StringAssert.Contains(executableScriptText, "checks_release_channel_version");
        StringAssert.Contains(executableScriptText, "startup_smoke_version");
        StringAssert.Contains(executableScriptText, "primary_receipt_status");
        StringAssert.Contains(executableScriptText, "primary_receipt_rid");
        StringAssert.Contains(executableScriptText, "primary_receipt_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "primary_receipt_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "primary_receipt_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_rid");
        StringAssert.Contains(executableScriptText, "startup_smoke_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_version");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_rid");
        StringAssert.Contains(executableScriptText, "expected_artifact_arch");
        StringAssert.Contains(executableScriptText, "expected_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifact_channel_ids");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifact_versions");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_missing_head");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_missing_channel");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_channel_mismatch");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_missing_version");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_version_mismatch");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_missing_arch");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_arch_mismatch");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_channel_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing head:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing version/releaseVersion:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) version/releaseVersion does not match release channel version:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing arch:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) arch does not match RID-derived architecture:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting channelId/channel values:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting version/releaseVersion values:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting arch/architecture values:");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_channel_id");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_version");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_arch");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_channel_id");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_version");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_arch");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "macos_artifacts_missing_rid_by_head");
        StringAssert.Contains(executableScriptText, "Release channel publishes macOS desktop media for head");
        StringAssert.Contains(executableScriptText, "Release channel publishes macOS desktop media without explicit head/rid tuple metadata.");
        StringAssert.Contains(executableScriptText, "Release channel is missing canonical required promoted desktop head(s) for milestone-3 executable proof:");
        StringAssert.Contains(executableScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 executable proof:");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopPlatforms is missing required policy platform(s):");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing required policy head(s):");
        StringAssert.Contains(executableScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing canonical required head(s):");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState is missing for desktop install media; tuple-coverage posture cannot be proven.");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState is missing for desktop install media; support posture cannot be proven.");
        StringAssert.Contains(executableScriptText, "release_channel_allowed_rollout_states");
        StringAssert.Contains(executableScriptText, "release_channel_allowed_supportability_states");
        StringAssert.Contains(executableScriptText, "release_channel_rollout_state_invalid");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_invalid");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState is not a recognized registry rollout posture for desktop install media:");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState is not a recognized registry support posture for desktop install media:");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState cannot remain coverage_incomplete when required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState cannot remain review_required when required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_publishable_status");
        StringAssert.Contains(executableScriptText, "release_channel_publishable_status_with_incomplete_desktop_tuple_coverage");
        StringAssert.Contains(executableScriptText, "Release channel status cannot be publishable while required desktop tuple coverage is incomplete.");
        StringAssert.Contains(executableScriptText, "release_channel_rollout_state_blocked_for_publishable_complete_values");
        StringAssert.Contains(executableScriptText, "release_channel_rollout_state_blocks_publishable_complete");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState cannot be paused/revoked when status is publishable and required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_rollout_state_allowed_for_publishable_complete_values");
        StringAssert.Contains(executableScriptText, "release_channel_rollout_state_invalid_for_publishable_complete");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState must be promoted_preview/release_candidate/public_stable when status is publishable and required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_allowed_for_publishable_complete_values");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_invalid_for_publishable_complete");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState must be preview_supported when status is publishable and required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_version_uses_unpublished_sentinel");
        StringAssert.Contains(executableScriptText, "release_channel.releaseVersion");
        StringAssert.Contains(executableScriptText, "release_channel_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel.version and release_channel.releaseVersion disagree after normalization.");
        StringAssert.Contains(executableScriptText, "Release channel is missing version/releaseVersion, so installer/update truth cannot be aligned by release head.");
        StringAssert.Contains(executableScriptText, "Release channel version cannot be the unpublished sentinel when status is publishable.");
        StringAssert.Contains(executableScriptText, "release_channel_generated_at_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel.generated_at and release_channel.generatedAt disagree after normalization.");
        StringAssert.Contains(executableScriptText, "Release channel rolloutState cannot remain unpublished when required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState cannot remain unpublished when required desktop tuple coverage is complete.");

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
    public void Desktop_executable_gate_fail_closes_invalid_platform_gate_contract_names()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def normalize_contract_name(payload: Dict[str, Any]) -> str:");
        StringAssert.Contains(scriptText, "chummer6-ui.linux_desktop_exit_gate");
        StringAssert.Contains(scriptText, "chummer6-ui.windows_desktop_exit_gate");
        StringAssert.Contains(scriptText, "chummer6-ui.macos_desktop_exit_gate");
        StringAssert.Contains(scriptText, "Linux desktop exit gate receipt contract_name is invalid for promoted head");
        StringAssert.Contains(scriptText, "Windows desktop exit gate receipt contract_name is invalid.");
        StringAssert.Contains(scriptText, "macOS desktop exit gate receipt contract_name is invalid for promoted head");
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
