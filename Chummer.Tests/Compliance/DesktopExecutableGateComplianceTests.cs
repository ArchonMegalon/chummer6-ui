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
    public void Desktop_executable_gate_fail_closes_unexpected_desktop_tuple_coverage_keys()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "allowed_desktop_tuple_coverage_keys");
        StringAssert.Contains(scriptText, "promotedInstallerTuples");
        StringAssert.Contains(scriptText, "externalProofRequests");
        StringAssert.Contains(scriptText, "unexpected_desktop_tuple_coverage_keys");
        StringAssert.Contains(scriptText, "release_channel_tuple_coverage_unexpected_keys");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage has unexpected keys:");
    }

    [TestMethod]
    public void Desktop_executable_gate_fail_closes_external_proof_request_contract_drift()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "tuple_coverage_declares_external_proof_requests");
        StringAssert.Contains(scriptText, "allowed_external_proof_request_row_keys");
        StringAssert.Contains(scriptText, "release_channel_external_proof_request_rows_expected");
        StringAssert.Contains(scriptText, "release_channel_external_proof_request_rows_reported");
        StringAssert.Contains(scriptText, "channelId");
        StringAssert.Contains(scriptText, "startupSmokeReceiptContract");
        StringAssert.Contains(scriptText, "proofCaptureCommands");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage.externalProofRequests does not match missing desktop tuple inventory.");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage.externalProofRequests object rows do not match canonical missing-tuple external proof contract.");
    }

    [TestMethod]
    public void Desktop_executable_gate_fail_closes_unexpected_desktop_install_artifact_keys()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "allowed_desktop_install_artifact_keys");
        StringAssert.Contains(scriptText, "desktop_install_artifact_unexpected_keys_tokens");
        StringAssert.Contains(scriptText, "release_channel_desktop_install_artifacts_unexpected_keys");
        StringAssert.Contains(scriptText, "Release channel desktop install artifact(s) have unexpected keys:");
    }

    [TestMethod]
    public void Desktop_executable_gate_fail_closes_promoted_installer_tuple_row_drift()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "build_promoted_installer_tuple_id");
        StringAssert.Contains(scriptText, "allowed_promoted_installer_tuple_row_keys");
        StringAssert.Contains(scriptText, "release_channel_promoted_installer_tuple_rows_expected");
        StringAssert.Contains(scriptText, "release_channel_promoted_installer_tuple_rows_reported");
        StringAssert.Contains(scriptText, "release_channel_promoted_installer_tuple_duplicate_tuple_ids");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage.promotedInstallerTuples does not match promoted installer tuple inventory.");
        StringAssert.Contains(scriptText, "Release channel desktopTupleCoverage.promotedInstallerTuples object rows do not match promoted installer artifact metadata.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_unexpected_desktop_tuple_coverage_keys()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "bonus_noncanonical_tuple_coverage_key");
        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject unexpected desktopTupleCoverage keys");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit unexpected desktopTupleCoverage key marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage has unexpected keys:");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_unexpected_desktop_install_artifact_keys()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "bonus_noncanonical_install_artifact_key");
        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject unexpected desktop install artifact keys");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit unexpected desktop install artifact key marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktop install artifact(s) have unexpected keys:");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_codex_studio_tracked_artifact_guard()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);
        string guardScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "codex-studio-tracking-check.sh");
        string guardScriptText = File.ReadAllText(guardScriptPath);

        StringAssert.Contains(verifyScriptText, "checking codex-studio tracked artifact guard");
        StringAssert.Contains(verifyScriptText, "bash scripts/ai/milestones/codex-studio-tracking-check.sh");
        StringAssert.Contains(guardScriptText, "git ls-files .codex-studio");
        StringAssert.Contains(guardScriptText, ".codex-studio/published/(QUEUE|WORKPACKAGES)\\.generated\\.yaml");
        StringAssert.Contains(guardScriptText, "only .codex-studio/published/QUEUE.generated.yaml and WORKPACKAGES.generated.yaml may be tracked.");
    }

    [TestMethod]
    public void Release_manifest_generation_materializes_external_host_proof_blocker_artifact()
    {
        string repoRoot = FindRepoRoot();
        string releaseManifestScriptPath = Path.Combine(repoRoot, "scripts", "generate-releases-manifest.sh");
        string releaseManifestScriptText = File.ReadAllText(releaseManifestScriptPath);
        string blockerMaterializerScriptPath = Path.Combine(repoRoot, "scripts", "materialize-external-host-proof-blockers.py");
        string blockerMaterializerScriptText = File.ReadAllText(blockerMaterializerScriptPath);

        StringAssert.Contains(releaseManifestScriptText, "materialize-external-host-proof-blockers.py");
        StringAssert.Contains(releaseManifestScriptText, "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json");
        StringAssert.Contains(releaseManifestScriptText, "infer_release_version_from_startup_smoke");
        StringAssert.Contains(releaseManifestScriptText, "if [[ \"$RELEASE_VERSION\" == \"unpublished\" ]]");
        StringAssert.Contains(releaseManifestScriptText, "artifactDigest");
        StringAssert.Contains(releaseManifestScriptText, "if sha256_file(artifact_path) != digest:");
        StringAssert.Contains(releaseManifestScriptText, "CHUMMER_EXTERNAL_PROOF_MAX_RECEIPT_AGE_SECONDS:-604800");
        StringAssert.Contains(blockerMaterializerScriptText, "chummer6-ui.external_host_proof_blockers");
        StringAssert.Contains(blockerMaterializerScriptText, "default=604800");
        StringAssert.Contains(blockerMaterializerScriptText, "receipt_stale");
        StringAssert.Contains(blockerMaterializerScriptText, "public_route_unhealthy");
        StringAssert.Contains(blockerMaterializerScriptText, "installAccessClass");
        StringAssert.Contains(blockerMaterializerScriptText, "account_required");
        StringAssert.Contains(blockerMaterializerScriptText, "route_probe[\"authChallengeAccepted\"]");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_promoted_installer_tuple_row_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "tampered-promoted-installer-artifact-id");
        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject promotedInstallerTuples artifact metadata drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit promotedInstallerTuples metadata drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage.promotedInstallerTuples object rows do not match promoted installer artifact metadata.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_promoted_platform_head_rid_tuple_inventory_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "tampered-head:tampered-rid:windows");
        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject promotedPlatformHeadRidTuples inventory drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit promotedPlatformHeadRidTuples inventory drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage promotedPlatformHeadRidTuples inventory does not match promoted installer tuples.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_platform_head_rid_tuple_inventory_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missingRequiredPlatformHeadRidTuples inventory drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missingRequiredPlatformHeadRidTuples inventory drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage missingRequiredPlatformHeadRidTuples inventory does not match promoted installer tuples.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_platform_head_pairs_inventory_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missingRequiredPlatformHeadPairs inventory drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missingRequiredPlatformHeadPairs inventory drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage missingRequiredPlatformHeadPairs inventory does not match promoted installer tuples.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_platforms_inventory_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missingRequiredPlatforms inventory drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missingRequiredPlatforms inventory drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage missingRequiredPlatforms inventory does not match promoted installer tuples.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_heads_inventory_drift()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missingRequiredHeads inventory drift");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missingRequiredHeads inventory drift marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage missingRequiredHeads inventory does not match promoted installer tuples.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_required_platform_head_rid_tuples_missing_required_pair_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject requiredDesktopPlatformHeadRidTuples missing required desktop platform/head pair coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit requiredDesktopPlatformHeadRidTuples missing required desktop platform/head pair coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage requiredDesktopPlatformHeadRidTuples is missing required desktop platform/head pair coverage:");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_required_desktop_platforms_missing_required_policy_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject requiredDesktopPlatforms missing required policy platform coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit requiredDesktopPlatforms missing required policy platform coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage requiredDesktopPlatforms is missing required policy platform(s):");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_required_desktop_heads_missing_required_policy_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject requiredDesktopHeads missing required policy head coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit requiredDesktopHeads missing required policy head coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing required policy head(s):");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_required_desktop_heads_missing_canonical_required_head_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject requiredDesktopHeads missing canonical required head coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit requiredDesktopHeads missing canonical required head coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage requiredDesktopHeads is missing canonical required head(s):");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_desktop_tuple_coverage_metadata()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missing desktopTupleCoverage metadata");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missing desktopTupleCoverage metadata marker");
        StringAssert.Contains(verifyScriptText, "Release channel is missing desktopTupleCoverage metadata for promoted desktop install artifacts.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_desktop_platforms_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missing requiredDesktopPlatforms coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missing requiredDesktopPlatforms coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage is missing requiredDesktopPlatforms for desktop install media.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_required_desktop_heads_coverage()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missing requiredDesktopHeads coverage");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missing requiredDesktopHeads coverage marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage is missing requiredDesktopHeads for desktop install media.");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_active_mutation_for_missing_promoted_platform_heads_mapping()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate should reject missing promotedPlatformHeads mapping");
        StringAssert.Contains(verifyScriptText, "desktop executable gate mutation did not emit missing promotedPlatformHeads mapping marker");
        StringAssert.Contains(verifyScriptText, "Release channel desktopTupleCoverage is missing promotedPlatformHeads mapping for desktop install media.");
    }

    [TestMethod]
    public void Verify_entrypoint_checks_desktop_executable_gate_blocking_findings_alias_alignment()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "desktop executable gate blocking findings aliases");
        StringAssert.Contains(verifyScriptText, "blocking-findings alias drift between reasons/blockingFindings/blocking_findings");
        StringAssert.Contains(verifyScriptText, "blockingFindingsCount does not match reasons count");
        StringAssert.Contains(verifyScriptText, "blocking_findings_count does not match reasons count");
    }

    [TestMethod]
    public void Desktop_executable_gate_fail_closes_when_flagship_release_lock_is_still_active_after_wait_window()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "release_gate_lock_blocked=0");
        StringAssert.Contains(scriptText, "release_gate_lock_stale_removed=0");
        StringAssert.Contains(scriptText, "release_gate_lock_stale_reason=\"\"");
        StringAssert.Contains(scriptText, "prune_release_gate_lock_if_stale()");
        StringAssert.Contains(scriptText, "release_gate_lock_stale_max_age_seconds");
        StringAssert.Contains(scriptText, "release_gate_lock_owner_pid_path=\"$release_gate_lock_dir/owner.pid\"");
        StringAssert.Contains(scriptText, "owner_pid_path = Path(sys.argv[2])");
        StringAssert.Contains(scriptText, "entries_without_owner = [entry for entry in entries if entry != owner_pid_path]");
        StringAssert.Contains(scriptText, "stale_owner_only:");
        StringAssert.Contains(scriptText, "stale_owner_only_lock_dir_removed_after_");
        StringAssert.Contains(scriptText, "stale_empty_lock_dir_removed_after_");
        StringAssert.Contains(scriptText, "without_active_b14_process");
        StringAssert.Contains(scriptText, "if pgrep -f \"scripts/ai/milestones/b14-flagship-ui-release-gate.sh\" >/dev/null 2>&1; then");
        StringAssert.Contains(scriptText, "if [[ -d \"$release_gate_lock_dir\" ]]; then");
        StringAssert.Contains(scriptText, "release_gate_lock_blocked=1");
        StringAssert.Contains(scriptText, "skip_dependency_materialize=1");
        StringAssert.Contains(scriptText, "\"release_gate_lock_blocked\": release_gate_lock_blocked");
        StringAssert.Contains(scriptText, "\"release_gate_lock_stale_removed\": release_gate_lock_stale_removed");
        StringAssert.Contains(scriptText, "\"release_gate_lock_stale_reason\": release_gate_lock_stale_reason");
        StringAssert.Contains(scriptText, "\"release_gate_lock_dir\": str(repo_root / \".codex-studio\" / \"locks\" / \"b14-flagship-ui-release-gate.lock\")");
        StringAssert.Contains(scriptText, "Flagship release gate lock remained active after wait window; executable gate skipped dependency rematerialization and fail-closes to prevent partial proof races.");
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
        StringAssert.Contains(scriptText, "(\"windows\", \"UI_WINDOWS*_DESKTOP_EXIT_GATE.generated.json\")");
        StringAssert.Contains(scriptText, "(\"macos\", \"UI_MACOS*_DESKTOP_EXIT_GATE.generated.json\")");
        StringAssert.Contains(scriptText, "promoted_linux_tuples");
        StringAssert.Contains(scriptText, "promoted_windows_tuples");
        StringAssert.Contains(scriptText, "promoted_macos_tuples");
        StringAssert.Contains(scriptText, "stale_linux_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "stale_windows_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "stale_macos_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "stale_passing_platform_gate_receipts_without_promoted_tuples");
        StringAssert.Contains(scriptText, "Stale passing platform gate receipts exist for non-promoted desktop tuples:");
    }

    [TestMethod]
    public void Desktop_executable_gate_emits_blocking_findings_aliases_aligned_with_reasons()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "blocking_findings = list(reasons)");
        StringAssert.Contains(scriptText, "\"blockingFindings\": blocking_findings");
        StringAssert.Contains(scriptText, "\"blocking_findings\": blocking_findings");
        StringAssert.Contains(scriptText, "\"blockingFindingsCount\": blocking_findings_count");
        StringAssert.Contains(scriptText, "\"blocking_findings_count\": blocking_findings_count");

        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "not isinstance(blocking_findings_count, int)");
        StringAssert.Contains(verifyScriptText, "blocking_findings_count != len(reasons)");
        Assert.IsFalse(
            verifyScriptText.Contains("int(blocking_findings_count or -1)", StringComparison.Ordinal),
            "The verifier must accept a valid zero blockingFindingsCount on a passing desktop executable gate.");
    }

    [TestMethod]
    public void Desktop_executable_gate_derives_upstream_release_cross_gate_and_platform_reviews()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "upstream_receipt_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "release_channel_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "windows_platform_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "cross_gate_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "linux_platform_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "macos_platform_review_start = len(reasons)");
        StringAssert.Contains(scriptText, "\"upstreamReceiptReview\"");
        StringAssert.Contains(scriptText, "\"releaseChannelReview\"");
        StringAssert.Contains(scriptText, "\"windowsPlatformReview\"");
        StringAssert.Contains(scriptText, "\"crossGateReview\"");
        StringAssert.Contains(scriptText, "\"linuxPlatformReview\"");
        StringAssert.Contains(scriptText, "\"macosPlatformReview\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not upstream_receipt_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not release_channel_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not windows_platform_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not cross_gate_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not linux_platform_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"status\": \"pass\" if not macos_platform_review_reasons else \"fail\"");
        StringAssert.Contains(scriptText, "\"reasonCount\": len(upstream_receipt_review_reasons)");
        StringAssert.Contains(scriptText, "\"reasonCount\": len(macos_platform_review_reasons)");
        StringAssert.Contains(scriptText, "\"requiredReceipts\": [");
        StringAssert.Contains(scriptText, "\"requiredPlatforms\": list(required_desktop_platforms)");
        StringAssert.Contains(scriptText, "\"gateStatuses\": windows_statuses");
        StringAssert.Contains(scriptText, "\"gateStatuses\": linux_statuses");
        StringAssert.Contains(scriptText, "\"gateStatuses\": macos_statuses");
        StringAssert.Contains(scriptText, "payload[\"evidence\"][\"failureCount\"] = len(reasons)", StringComparison.Ordinal);
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
        StringAssert.Contains(executableScriptText, "canonical_required_desktop_heads = [\"avalonia\"]");
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
        StringAssert.Contains(executableScriptText, "linux desktop exit gate proof for ");
        StringAssert.Contains(executableScriptText, "carries conflicting generated_at/generatedAt alias values.");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt head channelId/channel does not match release channel for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt head carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt checks.release_channel_id does not match release channel channelId for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "or gate_checks.get(\"startup_smoke_external_blocker\")");
        StringAssert.Contains(executableScriptText, "checks_startup_smoke_receipt_found");
        StringAssert.Contains(executableScriptText, "checks_startup_smoke_receipt_path");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate checks.startup_smoke_receipt_found disagrees with startup_smoke.primary receipt file presence for promoted head");
        StringAssert.Contains(executableScriptText, "Linux desktop exit gate checks.startup_smoke_receipt_path disagrees with startup_smoke.primary.receipt_path for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt carries conflicting version/releaseVersion alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt carries conflicting completedAtUtc/recordedAtUtc alias values for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt status is not passing for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt rid is missing for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt rid does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact is missing for promoted head");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact channelId/channel does not match promoted release channel.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact is missing version/releaseVersion.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact version/releaseVersion does not match promoted release channel version.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact carries conflicting version/releaseVersion alias values.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact is missing a valid generated_at/generatedAt.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact arch does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Release channel Linux artifact arch does not match promoted RID for head");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing channelId/channel:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) channelId/channel does not match release channel channelId:");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_invalid_generated_at");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry invalid generated_at/generatedAt timestamps:");
        StringAssert.Contains(executableScriptText, "Linux startup smoke external blocker must be blank when installer startup smoke receipt exists for promoted head");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt is missing releaseVersion/version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt releaseVersion/version does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt channelId/channel does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "windows desktop exit gate proof");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt checks.release_channel_id does not match release channel channelId.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt checks.release_channel_version does not match release channel version.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate receipt carries conflicting releaseVersion/version alias values.");
        StringAssert.Contains(executableScriptText, "checks_startup_smoke_receipt_found = bool(gate_checks.get(\"startup_smoke_receipt_found\"))");
        StringAssert.Contains(executableScriptText, "checks_startup_smoke_receipt_path = str(gate_checks.get(\"startup_smoke_receipt_path\") or \"\").strip()");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate checks.startup_smoke_receipt_found disagrees with startup smoke receipt file presence for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows desktop exit gate checks.startup_smoke_receipt_path disagrees with startup smoke receipt path for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt is missing version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt version does not match release channel version for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting channelId/channel alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting version/releaseVersion alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting arch/architecture alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt carries conflicting completedAtUtc/recordedAtUtc alias values for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt rid is missing for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke receipt rid does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Release channel Windows artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "Release channel Windows artifact arch does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "def infer_installer_file_name(head: str, rid: str, platform: str) -> str:");
        StringAssert.Contains(executableScriptText, "def collect_matching_quarantine_paths(file_name: str, quarantine_roots: List[Path]) -> List[str]:");
        StringAssert.Contains(executableScriptText, "def summarize_quarantine_installer_markers(paths: List[str], platform: str = \"\") -> Dict[str, Any]:");
        StringAssert.Contains(executableScriptText, "def register_external_blocker(");
        StringAssert.Contains(executableScriptText, "def infer_external_blockers_from_reasons(platform: str, reasons: List[str]) -> List[str]:");
        StringAssert.Contains(executableScriptText, "evidence.setdefault(\"external_blockers\", [])");
        StringAssert.Contains(executableScriptText, "source=\"windows_gate_reason\"");
        StringAssert.Contains(executableScriptText, "source=\"macos_gate_reason\"");
        StringAssert.Contains(executableScriptText, "source=\"linux_gate_reason\"");
        StringAssert.Contains(executableScriptText, "source=\"global_reason\"");
        StringAssert.Contains(executableScriptText, "evidence[\"quarantine_roots\"]");
        StringAssert.Contains(executableScriptText, "gate_evidence[\"quarantined_installer_candidates\"] = quarantine_candidates");
        StringAssert.Contains(executableScriptText, "gate_evidence[\"quarantined_installer_marker_summary\"] = quarantine_marker_summary");
        StringAssert.Contains(executableScriptText, "gate_evidence[\"expected_linux_shelf_path\"] = str(shelf_path)");
        StringAssert.Contains(executableScriptText, "Linux promoted installer bytes appear only in quarantine for head");
        StringAssert.Contains(executableScriptText, "Windows promoted installer bytes appear only in quarantine and cannot count as shipped proof:");
        StringAssert.Contains(executableScriptText, "Windows quarantine contains payload-valid installer candidate bytes, but promotion remains blocked until matching startup smoke proof exists:");
        StringAssert.Contains(executableScriptText, "Windows quarantine contains installer candidate bytes that fail embedded payload/sample marker checks and cannot be promoted:");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact channelId/channel does not match promoted release channel.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting channelId/channel alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact is missing version/releaseVersion.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact version/releaseVersion does not match promoted release channel version.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting version/releaseVersion alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting arch/architecture alias values.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact is missing a valid generated_at/generatedAt.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact arch does not match promoted release-channel RID.");
        StringAssert.Contains(executableScriptText, "Windows startup smoke external blocker must be blank when startup smoke receipt exists for promoted installer bytes.");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt is missing releaseVersion/version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt channelId/channel does not match release channel channelId for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate proof for ");
        StringAssert.Contains(executableScriptText, "carries conflicting generated_at/generatedAt alias values.");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt checks.release_channel_id does not match release channel channelId for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt checks.release_channel_version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate receipt carries conflicting releaseVersion/version alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate checks.startup_smoke_receipt_found disagrees with startup_smoke.receipt_path file presence for promoted head");
        StringAssert.Contains(executableScriptText, "macOS desktop exit gate checks.startup_smoke_receipt_path disagrees with startup_smoke.receipt_path for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting channelId/channel alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting version/releaseVersion alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting arch/architecture alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS startup smoke receipt carries conflicting completedAtUtc/recordedAtUtc alias values for promoted head");
        StringAssert.Contains(executableScriptText, "macOS promoted installer bytes appear only in quarantine for head");
        StringAssert.Contains(executableScriptText, "macOS quarantine contains payload-valid installer candidate bytes for head");
        StringAssert.Contains(executableScriptText, "macOS quarantine installer marker checks are skipped for unsupported artifact formats on this host; payload/sample markers were not asserted for head");
        StringAssert.Contains(executableScriptText, "macOS quarantine contains installer candidate bytes that fail embedded payload/sample marker checks for head");
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
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact is missing a valid generated_at/generatedAt.");
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
        StringAssert.Contains(executableScriptText, "primary_receipt_timestamp_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_rid");
        StringAssert.Contains(executableScriptText, "startup_smoke_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_timestamp_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_version");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "startup_smoke_receipt_timestamp_alias_conflict");
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
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_missing_generated_at");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_generated_at_mismatch");
        StringAssert.Contains(executableScriptText, "release_channel_desktop_install_artifacts_generated_at_alias_conflict");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing head:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing version/releaseVersion:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) version/releaseVersion does not match release channel version:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing arch:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) arch does not match RID-derived architecture:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting channelId/channel values:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting version/releaseVersion values:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting arch/architecture values:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) are missing generated_at/generatedAt:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) generated_at does not match release channel generated_at:");
        StringAssert.Contains(executableScriptText, "Release channel desktop install artifact(s) carry conflicting generated_at/generatedAt values:");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_channel_id");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_version");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_arch");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_generated_at");
        StringAssert.Contains(executableScriptText, "release_channel_windows_artifact_generated_at_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_channel_id");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_version");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_arch");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_generated_at");
        StringAssert.Contains(executableScriptText, "release_channel_linux_artifact_generated_at_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_channel_id");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_channel_id_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_version");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_version_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_arch");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_arch_alias_conflict");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_generated_at");
        StringAssert.Contains(executableScriptText, "release_channel_macos_artifact_generated_at_alias_conflict");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact is missing a valid generated_at/generatedAt.");
        StringAssert.Contains(executableScriptText, "Windows gate embedded release_channel_windows_artifact carries conflicting generated_at/generatedAt alias values.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact is missing a valid generated_at/generatedAt.");
        StringAssert.Contains(executableScriptText, "Linux gate embedded release_channel_linux_artifact carries conflicting generated_at/generatedAt alias values.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact is missing a valid generated_at/generatedAt.");
        StringAssert.Contains(executableScriptText, "macOS gate embedded release_channel_macos_artifact carries conflicting generated_at/generatedAt alias values.");
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
        StringAssert.Contains(executableScriptText, "Release channel rolloutState must be local_docker_preview/promoted_preview/release_candidate/public_stable when status is publishable and required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_allowed_for_publishable_complete_values");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_invalid_for_publishable_complete");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState must be local_docker_proven/preview_supported when status is publishable and required desktop tuple coverage is complete.");
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
        StringAssert.Contains(visualScriptText, "canonical_required_desktop_heads = [\"avalonia\"]");
        StringAssert.Contains(visualScriptText, "flagship_missing_canonical_required_desktop_heads");
        StringAssert.Contains(visualScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head visual proof:");

        StringAssert.Contains(workflowScriptText, "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(workflowScriptText, "release_channel_channel_id");
        StringAssert.Contains(workflowScriptText, "release_channel_version");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing channelId/channel.");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing version.");
        StringAssert.Contains(workflowScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(workflowScriptText, "canonical_required_desktop_heads = [\"avalonia\"]");
        StringAssert.Contains(workflowScriptText, "flagship_missing_canonical_required_desktop_heads");
        StringAssert.Contains(workflowScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head workflow execution proof:");
    }

    [TestMethod]
    public void Linux_exit_gate_materializer_embeds_release_channel_artifact_identity_in_checks_envelope()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "\"channelId\": release_channel_channel_id");
        StringAssert.Contains(scriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(scriptText, "\"checks\": {");
        StringAssert.Contains(scriptText, "\"release_channel_id\": release_channel_channel_id");
        StringAssert.Contains(scriptText, "\"release_channel_version\": release_channel_version");
        StringAssert.Contains(scriptText, "\"release_channel_linux_artifact\": release_channel_linux_artifact");
        StringAssert.Contains(scriptText, "\"startup_smoke_receipt_found\": startup_smoke_receipt_exists");
        StringAssert.Contains(scriptText, "\"startup_smoke_receipt_path\": installer_receipt_path");
        StringAssert.Contains(scriptText, "\"startup_smoke_external_blocker\": startup_smoke_external_blocker");
        StringAssert.Contains(scriptText, "release_channel_payload.get(\"artifacts\")");
        StringAssert.Contains(scriptText, "normalize_token(artifact.get(\"platform\")) == \"linux\"");
        StringAssert.Contains(scriptText, "normalize_token(artifact.get(\"kind\")) == \"installer\"");
        StringAssert.Contains(scriptText, "normalize_token(artifact.get(\"head\")) == normalize_token(app_key)");
        StringAssert.Contains(scriptText, "normalize_token(artifact.get(\"rid\")) == normalize_token(rid)");
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
    public void Desktop_executable_gate_materializer_uses_tuple_specific_windows_receipts_for_non_default_heads()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "if [[ \"$head\" == \"avalonia\" && \"$rid\" == \"win-x64\" ]]; then");
        StringAssert.Contains(scriptText, "windows_gate_tuple_path=\"$windows_gate_path_default\"");
        StringAssert.Contains(scriptText, "windows_gate_tuple_path=\"$repo_root/.codex-studio/published/UI_WINDOWS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json\"");
        StringAssert.Contains(scriptText, "CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH=\"$windows_gate_tuple_path\"");
        StringAssert.Contains(scriptText, "def windows_gate_path_for_head(");
        StringAssert.Contains(scriptText, "if head == \"avalonia\" and rid == \"win-x64\":");
        StringAssert.Contains(scriptText, "return receipt_root / f\"UI_WINDOWS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json\"");
    }

    [TestMethod]
    public void Desktop_executable_gate_materializer_preserves_linux_head_specific_receipt_paths_for_avalonia_and_blazor()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "if [[ \"$head\" == \"avalonia\" && \"$rid\" == \"linux-x64\" ]]; then");
        StringAssert.Contains(scriptText, "linux_gate_tuple_path=\"$linux_avalonia_gate_path\"");
        StringAssert.Contains(scriptText, "elif [[ \"$head\" == \"blazor-desktop\" && \"$rid\" == \"linux-x64\" ]]; then");
        StringAssert.Contains(scriptText, "linux_gate_tuple_path=\"$linux_blazor_gate_path\"");
        StringAssert.Contains(scriptText, "linux_gate_tuple_path=\"$repo_root/.codex-studio/published/UI_LINUX_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json\"");
        StringAssert.Contains(scriptText, "CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH=\"$linux_gate_tuple_path\"");
        StringAssert.Contains(scriptText, "def linux_gate_path_for_head(head: str, rid: str, avalonia_path: Path, blazor_path: Path, receipt_root: Path) -> Path:");
        StringAssert.Contains(scriptText, "if head == \"avalonia\" and rid == \"linux-x64\":");
        StringAssert.Contains(scriptText, "if head == \"blazor-desktop\" and rid == \"linux-x64\":");
        StringAssert.Contains(scriptText, "return receipt_root / f\"UI_LINUX_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json\"");
    }

    [TestMethod]
    public void Desktop_executable_gate_materializer_uses_tuple_specific_macos_receipt_paths()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "macos_gate_tuple_path=\"$repo_root/.codex-studio/published/UI_MACOS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json\"");
        StringAssert.Contains(scriptText, "CHUMMER_UI_MACOS_DESKTOP_EXIT_GATE_PATH=\"$macos_gate_tuple_path\"");
        StringAssert.Contains(scriptText, "def macos_gate_path_for_head(head: str, rid: str, receipt_root: Path) -> Path:");
        StringAssert.Contains(scriptText, "return receipt_root / f\"UI_MACOS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json\"");
    }

    [TestMethod]
    public void Avalonia_primary_route_proof_verifier_requires_primary_head_receipts_for_all_platforms()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "verify-avalonia-primary-route-proof.py");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "PRIMARY_HEAD = \"avalonia\"");
        StringAssert.Contains(scriptText, "FALLBACK_HEADS = {\"blazor-desktop\"}");
        StringAssert.Contains(scriptText, "REQUIRED_PLATFORMS = (\"linux\", \"macos\", \"windows\")");
        StringAssert.Contains(scriptText, "receipt_matches_artifact");
        StringAssert.Contains(scriptText, "normalize(receipt.get(\"headId\")) == PRIMARY_HEAD");
        StringAssert.Contains(scriptText, "fallbackReceiptsAccepted\": False");
        StringAssert.Contains(scriptText, "desktopTupleCoverage.requiredDesktopHeads must not require fallback head");
        StringAssert.Contains(scriptText, "desktopTupleCoverage.desktopRouteTruth is missing");
        StringAssert.Contains(scriptText, "validate_route_truth_row");
        StringAssert.Contains(scriptText, "routeRole\")) != \"primary\"");
        StringAssert.Contains(scriptText, "promotionState\")) != \"promoted\"");
        StringAssert.Contains(scriptText, "parityPosture\")) != \"flagship_primary\"");
        StringAssert.Contains(scriptText, "validate_fallback_route_truth_rows");
        StringAssert.Contains(scriptText, "must not carry flagship_primary parity posture");
        StringAssert.Contains(scriptText, "\"routeTruthProof\": route_truth_proof");
        StringAssert.Contains(scriptText, "chummer6-ui.avalonia_primary_route_proof");
    }

    [TestMethod]
    public void Verify_entrypoint_runs_avalonia_primary_route_proof_guard()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);

        StringAssert.Contains(verifyScriptText, "checking next90 Avalonia primary route proof guard");
        StringAssert.Contains(verifyScriptText, "verify-avalonia-primary-route-proof.py");
        StringAssert.Contains(verifyScriptText, "NEXT90_M101_AVALONIA_PRIMARY_ROUTE_PROOF.generated.json");
        StringAssert.Contains(verifyScriptText, "CHUMMER_VERIFY_AVALONIA_PRIMARY_ROUTE_PROOF:-1");
        StringAssert.Contains(verifyScriptText, "CHUMMER_AVALONIA_PRIMARY_ROUTE_PROOF_ALLOW_MISSING_RECEIPTS");
    }

    [TestMethod]
    public void Linux_desktop_exit_gate_uses_stable_build_lock_descriptor()
    {
        string repoRoot = FindRepoRoot();
        string linuxScriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string linuxScriptText = File.ReadAllText(linuxScriptPath);

        StringAssert.Contains(linuxScriptText, "BUILD_LOCK_FD=\"8\"");
        StringAssert.Contains(linuxScriptText, "eval \"exec ${BUILD_LOCK_FD}>\\\"\\$BUILD_LOCK_PATH\\\"\"");
        StringAssert.Contains(linuxScriptText, "flock \"$BUILD_LOCK_FD\"");
        Assert.IsFalse(
            linuxScriptText.Contains("exec {BUILD_LOCK_FD}>", StringComparison.Ordinal),
            "The Linux gate must not use dynamic fd assignment for the build lock; it has produced build_lock failures in worker shells.");
    }

    [TestMethod]
    public void Windows_and_macos_exit_gate_materializers_do_not_resolve_proof_from_legacy_chummer5a_paths()
    {
        string repoRoot = FindRepoRoot();
        string executableGateScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string linuxScriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string windowsScriptPath = Path.Combine(repoRoot, "scripts", "materialize-windows-desktop-exit-gate.sh");
        string macosScriptPath = Path.Combine(repoRoot, "scripts", "materialize-macos-desktop-exit-gate.sh");

        string executableGateScriptText = File.ReadAllText(executableGateScriptPath);
        string linuxScriptText = File.ReadAllText(linuxScriptPath);
        string windowsScriptText = File.ReadAllText(windowsScriptPath);
        string macosScriptText = File.ReadAllText(macosScriptPath);

        StringAssert.Contains(executableGateScriptText, "repo_root_alias_candidate=\"${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}\"");
        StringAssert.Contains(executableGateScriptText, "repo_root_physical=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")/../../..\" && pwd -P)\"");
        StringAssert.Contains(executableGateScriptText, "repo_root=\"$(cd -L \"$repo_root_alias_candidate\" && pwd -L)\"");
        StringAssert.Contains(linuxScriptText, "REPO_ROOT_ALIAS_CANDIDATE=\"${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}\"");
        StringAssert.Contains(linuxScriptText, "REPO_ROOT_PHYSICAL=\"$(cd \"$SCRIPT_DIR/..\" && pwd -P)\"");
        StringAssert.Contains(linuxScriptText, "REPO_ROOT=\"$(cd -L \"$REPO_ROOT_ALIAS_CANDIDATE\" && pwd -L)\"");
        StringAssert.Contains(windowsScriptText, "REPO_ROOT_ALIAS_CANDIDATE=\"${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}\"");
        StringAssert.Contains(windowsScriptText, "REPO_ROOT_PHYSICAL=\"$(cd \"$SCRIPT_DIR/..\" && pwd -P)\"");
        StringAssert.Contains(windowsScriptText, "REPO_ROOT=\"$(cd -L \"$REPO_ROOT_ALIAS_CANDIDATE\" && pwd -L)\"");
        StringAssert.Contains(macosScriptText, "REPO_ROOT_ALIAS_CANDIDATE=\"${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}\"");
        StringAssert.Contains(macosScriptText, "REPO_ROOT_PHYSICAL=\"$(cd \"$SCRIPT_DIR/..\" && pwd -P)\"");
        StringAssert.Contains(macosScriptText, "REPO_ROOT=\"$(cd -L \"$REPO_ROOT_ALIAS_CANDIDATE\" && pwd -L)\"");
        StringAssert.Contains(windowsScriptText, "Promoted Windows installer was not resolved from the repo-local desktop shelf.");
        StringAssert.Contains(macosScriptText, "Promoted macOS installer was not resolved from the repo-local desktop shelf");
        StringAssert.Contains(windowsScriptText, "\"summary\": summary");
        StringAssert.Contains(macosScriptText, "\"summary\": summary");
        StringAssert.Contains(windowsScriptText, "Windows desktop exit gate failed:");
        StringAssert.Contains(macosScriptText, "macOS desktop exit gate failed:");
        StringAssert.Contains(macosScriptText, "evidence[\"startup_smoke_external_blocker\"] = startup_smoke_external_blocker");
        StringAssert.Contains(macosScriptText, "\"external_blocker\": startup_smoke_external_blocker");
        StringAssert.Contains(macosScriptText, "evidence[\"startup_smoke_receipt_found\"] = startup_smoke_receipt_found");
        Assert.IsFalse(windowsScriptText.Contains("/docker/chummer5a/", StringComparison.Ordinal));
        Assert.IsFalse(macosScriptText.Contains("/docker/chummer5a/", StringComparison.Ordinal));
        StringAssert.Contains(executableGateScriptText, "startup.get(\"external_blocker\")");
        StringAssert.Contains(executableGateScriptText, "or gate_checks.get(\"startup_smoke_external_blocker\")");
        StringAssert.Contains(executableGateScriptText, "\"expectedInstallerRelativePath\": row_expected_installer_relative_path");
        StringAssert.Contains(executableGateScriptText, "\"expectedInstallerSha256\": row_expected_installer_sha256");
        StringAssert.Contains(executableGateScriptText, "expected_installer_sha256=row_expected_installer_sha256");
        StringAssert.Contains(executableGateScriptText, "installer-preflight-sha256-mismatch");
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
