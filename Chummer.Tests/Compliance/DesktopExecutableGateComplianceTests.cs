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
    public void Verify_entrypoint_refreshes_verified_release_channel_mirror_before_desktop_gate_checks()
    {
        string repoRoot = FindRepoRoot();
        string verifyScriptPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyScriptText = File.ReadAllText(verifyScriptPath);
        string helperPath = Path.Combine(repoRoot, "scripts", "materialize-verified-release-channel-mirror.py");
        string helperText = File.ReadAllText(helperPath);

        StringAssert.Contains(verifyScriptText, "refreshing verified release-channel mirror");
        StringAssert.Contains(verifyScriptText, "python3 scripts/materialize-verified-release-channel-mirror.py >/dev/null");
        StringAssert.Contains(verifyScriptText, ".tmp/verify-release-channel/RELEASE_CHANNEL.generated.json");
        StringAssert.Contains(helperText, "verify-releases-manifest.sh");
        StringAssert.Contains(helperText, "\"generated_at\"] = now");
        StringAssert.Contains(helperText, "\"generatedAt\"] = now");
        StringAssert.Contains(helperText, "\"verifiedFromPath\"]");
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
        StringAssert.Contains(guardScriptText, "grep -E '^\\.codex-studio/(locks/|generated/|tmp/)'");
        StringAssert.Contains(guardScriptText, "ephemeral .codex-studio lock/generated/tmp artifacts may not be tracked.");
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
        StringAssert.Contains(blockerMaterializerScriptText, "public_startup_workbench_command_routes");
        StringAssert.Contains(blockerMaterializerScriptText, "public_advanced_action_routes");
        StringAssert.Contains(blockerMaterializerScriptText, "public_advanced_committed_action_routes");
        StringAssert.Contains(blockerMaterializerScriptText, "startup_command_route_shapes");
        StringAssert.Contains(blockerMaterializerScriptText, "advanced_action_route_shapes");
        StringAssert.Contains(blockerMaterializerScriptText, "advanced_committed_action_route_shapes");
    }

    [TestMethod]
    public void Hosted_public_edge_workbench_proof_shape_contract_stays_aligned_across_verifier_docs_and_examples()
    {
        string repoRoot = FindRepoRoot();
        string verifierPath = Path.Combine(repoRoot, "scripts", "verify_blazor_public_edge_workbench_proof.py");
        string verifierText = File.ReadAllText(verifierPath);
        string contractDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        string contractDocText = File.ReadAllText(contractDocPath);
        string coreExamplePath = Path.Combine(repoRoot, "docs", "examples", "blazor-public-edge-workbench-proof.receipt.example.json");
        string coreExampleText = File.ReadAllText(coreExamplePath);
        string expandedExamplePath = Path.Combine(repoRoot, "docs", "examples", "blazor-public-edge-workbench-proof.expanded.receipt.example.json");
        string expandedExampleText = File.ReadAllText(expandedExamplePath);
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);

        StringAssert.Contains(verifierText, "ALLOWED_PROOF_SHAPES = {\"core\", \"expanded\"}");
        StringAssert.Contains(verifierText, "proof_shape='core' is inconsistent with expanded hosted route-entry markers, workflows, or routes");
        StringAssert.Contains(verifierText, "proof_shape='expanded' requires the full expanded hosted route-entry marker/workflow/route set");
        StringAssert.Contains(contractDocText, "`core` for the currently published minimal route family");
        StringAssert.Contains(contractDocText, "`expanded` for the newer promoted startup-command and advanced-action route family");
        StringAssert.Contains(contractDocText, "older receipts may omit this field");
        StringAssert.Contains(coreExampleText, "\"proof_shape\": \"core\"");
        StringAssert.Contains(expandedExampleText, "\"proof_shape\": \"expanded\"");
        StringAssert.Contains(statusScriptText, "explicit_shape = str(route.get(\"proof_shape\") or \"\").strip()");
    }

    [TestMethod]
    public void Hosted_public_edge_workbench_proof_shape_propagates_to_downstream_milestone_consumers()
    {
        string repoRoot = FindRepoRoot();
        string m113Path = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m113-ui-gm-prep-roster-surface-check.sh");
        string m113Text = File.ReadAllText(m113Path);
        string m142Path = Path.Combine(repoRoot, "scripts", "ai", "milestones", "next90-m142-ui-direct-workflow-proof-check.sh");
        string m142Text = File.ReadAllText(m142Path);
        string goldGatePath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "ui-gold-proof-depth-gate.sh");
        string goldGateText = File.ReadAllText(goldGatePath);
        string b14Path = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string b14Text = File.ReadAllText(b14Path);

        StringAssert.Contains(m113Text, "public_edge_workbench_proof_shape = str(public_edge_workbench_proof.get(\"proof_shape\") or \"\").strip()");
        StringAssert.Contains(m113Text, "\"public_edge_workbench_proof_shape_known\": public_edge_workbench_proof_shape in {\"core\", \"expanded\"}");
        StringAssert.Contains(m113Text, "\"proofShape\": public_edge_workbench_proof_shape");

        StringAssert.Contains(m142Text, "public_edge_workbench_proof_shape = str(public_edge_workbench_receipt.get(\"proof_shape\") or \"\").strip()");
        StringAssert.Contains(m142Text, "\"public_edge_workbench_proof_shape_known\": public_edge_workbench_proof_shape in {\"core\", \"expanded\"}");
        StringAssert.Contains(m142Text, "\"proofShape\": public_edge_workbench_proof_shape");

        StringAssert.Contains(goldGateText, "def classify_workbench_proof_shape(payload: dict) -> str:");
        StringAssert.Contains(goldGateText, "\"blazor_public_edge_workbench_proof_shape\"] = public_edge_workbench_proof_shape");
        StringAssert.Contains(goldGateText, "\"hosted_route_entry_proof_shape\"] = public_edge_workbench_proof_shape");

        StringAssert.Contains(b14Text, "\"proof_shape_known\": str(public_edge_workbench_receipt.get(\"proof_shape\") or \"\").strip() in {\"core\", \"expanded\"}");
    }

    [TestMethod]
    public void Hosted_public_edge_workbench_proof_shape_stays_wired_through_materializer_status_and_docs_index()
    {
        string repoRoot = FindRepoRoot();
        string materializerPath = Path.Combine(repoRoot, "scripts", "materialize-external-host-proof-blockers.py");
        string materializerText = File.ReadAllText(materializerPath);
        string statusPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusText = File.ReadAllText(statusPath);
        string docsIndexPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_DOCS_INDEX.md");
        string docsIndexText = File.ReadAllText(docsIndexPath);
        string routeProofDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        string routeProofDocText = File.ReadAllText(routeProofDocPath);

        StringAssert.Contains(materializerText, "\"proof_shape\": \"expanded\"");
        StringAssert.Contains(materializerText, "payload[\"browser_route_entry_proof_shape\"] = classify_route_entry_proof_shape(route_payload)");
        StringAssert.Contains(statusText, "explicit_shape = str(route.get(\"proof_shape\") or \"\").strip()");
        StringAssert.Contains(statusText, "blocker_route_entry_shape=");
        StringAssert.Contains(docsIndexText, "blazor-public-edge-workbench-proof.expanded.receipt.example.json");
        StringAssert.Contains(routeProofDocText, "expanded example receipt shape:");
        StringAssert.Contains(routeProofDocText, "docs/examples/blazor-public-edge-workbench-proof.expanded.receipt.example.json");
    }

    [TestMethod]
    public void Published_hosted_public_edge_workbench_receipt_stays_self_describing_and_contract_aligned()
    {
        string repoRoot = FindRepoRoot();
        string publishedReceiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        string publishedReceiptText = File.ReadAllText(publishedReceiptPath);
        string routeProofDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        string routeProofDocText = File.ReadAllText(routeProofDocPath);
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);

        StringAssert.Contains(publishedReceiptText, "\"contract_name\": \"chummer6-ui.blazor_public_edge_workbench_proof\"");
        StringAssert.Contains(publishedReceiptText, "\"proof_shape\": \"core\"");
        StringAssert.Contains(publishedReceiptText, "\"route_probe_executed\": true");
        StringAssert.Contains(publishedReceiptText, "\"route_probe_failures\": []");
        StringAssert.Contains(routeProofDocText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        StringAssert.Contains(statusScriptText, "route_proof_shape=");
    }

    [TestMethod]
    public void Hosted_public_edge_workbench_route_family_inventory_stays_aligned_across_verifier_and_examples()
    {
        string repoRoot = FindRepoRoot();
        string verifierPath = Path.Combine(repoRoot, "scripts", "verify_blazor_public_edge_workbench_proof.py");
        string verifierText = File.ReadAllText(verifierPath);
        string coreExamplePath = Path.Combine(repoRoot, "docs", "examples", "blazor-public-edge-workbench-proof.receipt.example.json");
        string coreExampleText = File.ReadAllText(coreExamplePath);
        string expandedExamplePath = Path.Combine(repoRoot, "docs", "examples", "blazor-public-edge-workbench-proof.expanded.receipt.example.json");
        string expandedExampleText = File.ReadAllText(expandedExamplePath);

        StringAssert.Contains(verifierText, "\"public_blazor_root_redirect\"");
        StringAssert.Contains(verifierText, "\"public_blazor_health\"");
        StringAssert.Contains(verifierText, "\"public_workbench_route\"");
        StringAssert.Contains(verifierText, "\"public_workspace_restore_route\"");
        StringAssert.Contains(verifierText, "\"public_startup_deep_link_route\"");
        StringAssert.Contains(verifierText, "\"public_result_continuation_routes\"");
        StringAssert.Contains(verifierText, "\"public_action_continuation_routes\"");
        StringAssert.Contains(verifierText, "\"public_committed_action_route\"");
        StringAssert.Contains(verifierText, "\"public_startup_workbench_command_routes\"");
        StringAssert.Contains(verifierText, "\"public_advanced_action_routes\"");
        StringAssert.Contains(verifierText, "\"public_advanced_committed_action_routes\"");

        StringAssert.Contains(coreExampleText, "\"proof_shape\": \"core\"");
        StringAssert.Contains(coreExampleText, "\"public_blazor_root_redirect\"");
        StringAssert.Contains(coreExampleText, "\"public_blazor_health\"");
        StringAssert.Contains(coreExampleText, "\"public_workbench_route\"");
        StringAssert.Contains(coreExampleText, "\"public_workspace_restore_route\"");
        StringAssert.Contains(coreExampleText, "\"public_startup_deep_link_route\"");
        StringAssert.Contains(coreExampleText, "\"public_result_continuation_routes\"");
        StringAssert.Contains(coreExampleText, "\"public_action_continuation_routes\"");
        StringAssert.Contains(coreExampleText, "\"public_committed_action_route\"");
        StringAssert.Contains(coreExampleText, "\"/blazor/workbench?workspace=ws-1&command=save_character_as\"");
        StringAssert.Contains(coreExampleText, "\"/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add\"");

        StringAssert.Contains(expandedExampleText, "\"proof_shape\": \"expanded\"");
        StringAssert.Contains(expandedExampleText, "\"public_startup_workbench_command_routes\"");
        StringAssert.Contains(expandedExampleText, "\"public_advanced_action_routes\"");
        StringAssert.Contains(expandedExampleText, "\"public_advanced_committed_action_routes\"");
        StringAssert.Contains(expandedExampleText, "\"startup_command_route_shapes\"");
        StringAssert.Contains(expandedExampleText, "\"advanced_action_route_shapes\"");
        StringAssert.Contains(expandedExampleText, "\"advanced_committed_action_route_shapes\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?command=new_character\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?command=open_character\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?command=open_for_printing\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?command=open_for_export\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add\"");
        StringAssert.Contains(expandedExampleText, "\"/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add\"");
    }

    [TestMethod]
    public void Hosted_public_edge_execution_proof_contract_stays_aligned_across_verifier_docs_example_and_placeholder()
    {
        string repoRoot = FindRepoRoot();
        string verifierPath = Path.Combine(repoRoot, "scripts", "verify_blazor_public_edge_execution_proof.py");
        string verifierText = File.ReadAllText(verifierPath);
        string contractDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        string contractDocText = File.ReadAllText(contractDocPath);
        string examplePath = Path.Combine(repoRoot, "docs", "examples", "blazor-public-edge-execution-proof.receipt.example.json");
        string exampleText = File.ReadAllText(examplePath);
        string placeholderPath = Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        string placeholderText = File.ReadAllText(placeholderPath);
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);

        StringAssert.Contains(verifierText, "EXPECTED_CONTRACT = \"chummer6-ui.blazor_public_edge_execution_proof\"");
        StringAssert.Contains(verifierText, "EXPECTED_PROOF_TIER = \"hosted_promoted_route_execution\"");
        StringAssert.Contains(verifierText, "EXPECTED_ROUTE_LANE = \"promoted_blazor_workbench\"");
        StringAssert.Contains(verifierText, "EXPECTED_PROMOTED_ROUTE_BASE = \"/blazor/workbench\"");
        StringAssert.Contains(verifierText, "\"promoted_advanced_committed_actions\"");

        StringAssert.Contains(contractDocText, "`chummer6-ui.blazor_public_edge_execution_proof`");
        StringAssert.Contains(contractDocText, "`hosted_promoted_route_execution`");
        StringAssert.Contains(contractDocText, "`promoted_blazor_workbench`");
        StringAssert.Contains(contractDocText, "docs/examples/blazor-public-edge-execution-proof.receipt.example.json");
        StringAssert.Contains(contractDocText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        StringAssert.Contains(contractDocText, "Committed complex-form execution is now part of the promoted advanced committed-action lane.");

        StringAssert.Contains(exampleText, "\"contract_name\": \"chummer6-ui.blazor_public_edge_execution_proof\"");
        StringAssert.Contains(exampleText, "\"proof_tier\": \"hosted_promoted_route_execution\"");
        StringAssert.Contains(exampleText, "\"route_lane\": \"promoted_blazor_workbench\"");
        StringAssert.Contains(exampleText, "\"promoted_route_base\": \"/blazor/workbench\"");
        StringAssert.Contains(exampleText, "\"promoted_advanced_committed_actions\"");

        StringAssert.Contains(placeholderText, "\"contract_name\": \"chummer6-ui.blazor_public_edge_execution_proof\"");
        StringAssert.Contains(placeholderText, "\"status\": \"not_run\"");
        StringAssert.Contains(placeholderText, "\"proof_tier\": \"hosted_promoted_route_execution\"");
        StringAssert.Contains(placeholderText, "\"route_lane\": \"promoted_blazor_workbench\"");
        StringAssert.Contains(placeholderText, "\"promoted_route_base\": \"/blazor/workbench\"");
        StringAssert.Contains(placeholderText, "Committed complex-form execution is now part of the promoted advanced committed-action lane.");

        StringAssert.Contains(statusScriptText, "execution_proof_tier=");
        StringAssert.Contains(statusScriptText, "execution_route_lane=");
        StringAssert.Contains(statusScriptText, "execution_promoted_route_base=");
        StringAssert.Contains(statusScriptText, "execution_workflow_family_ids=");
    }

    [TestMethod]
    public void Hosted_public_edge_execution_proof_metadata_propagates_to_downstream_consumers()
    {
        string repoRoot = FindRepoRoot();
        string uiGoldPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "ui-gold-proof-depth-gate.sh");
        string uiGoldText = File.ReadAllText(uiGoldPath);
        string releaseSignoffPath = Path.Combine(repoRoot, "docs", "WORKBENCH_RELEASE_SIGNOFF.md");
        string releaseSignoffText = File.ReadAllText(releaseSignoffPath);
        string docsIndexPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_DOCS_INDEX.md");
        string docsIndexText = File.ReadAllText(docsIndexPath);

        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_proof_tier\": HOSTED_EXECUTION_PROOF_TIER");
        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_route_lane\": HOSTED_EXECUTION_ROUTE_LANE");
        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_promoted_route_base\": HOSTED_EXECUTION_ROUTE_BASE");
        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_required_workflow_family_ids\": HOSTED_EXECUTION_REQUIRED_FAMILY_IDS");
        StringAssert.Contains(uiGoldText, "\"hosted_execution_proof_tier\"] = HOSTED_EXECUTION_PROOF_TIER");
        StringAssert.Contains(uiGoldText, "\"hosted_execution_route_lane\"] = HOSTED_EXECUTION_ROUTE_LANE");
        StringAssert.Contains(uiGoldText, "\"hosted_execution_promoted_route_base\"] = HOSTED_EXECUTION_ROUTE_BASE");
        StringAssert.Contains(uiGoldText, "\"hosted_execution_required_workflow_family_ids\"] = HOSTED_EXECUTION_REQUIRED_FAMILY_IDS");

        StringAssert.Contains(releaseSignoffText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        StringAssert.Contains(releaseSignoffText, "with an explicit `not_run` status until a real hosted run succeeds");
        StringAssert.Contains(releaseSignoffText, "Hosted `chummer.run` workflow execution proof is separately published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`");

        StringAssert.Contains(docsIndexText, "docs/examples/blazor-public-edge-execution-proof.receipt.example.json");
        StringAssert.Contains(docsIndexText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
    }

    [TestMethod]
    public void Verify_entrypoint_keeps_both_hosted_public_edge_blazor_proof_tiers_wired()
    {
        string repoRoot = FindRepoRoot();
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);

        StringAssert.Contains(verifyText, "checking hosted public-edge Blazor route-entry proof receipt guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh");
        StringAssert.Contains(verifyText, "checking hosted public-edge Blazor execution-proof receipt guard");
        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh");

        StringAssert.Contains(statusScriptText, "route_proof_shape=");
        StringAssert.Contains(statusScriptText, "execution_proof_tier=");
        StringAssert.Contains(statusScriptText, "execution_route_lane=");
        StringAssert.Contains(statusScriptText, "execution_promoted_route_base=");
    }

    [TestMethod]
    public void Shared_public_edge_reporting_surfaces_keep_route_and_execution_tier_metadata_together()
    {
        string repoRoot = FindRepoRoot();
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);
        string releaseSignoffPath = Path.Combine(repoRoot, "docs", "WORKBENCH_RELEASE_SIGNOFF.md");
        string releaseSignoffText = File.ReadAllText(releaseSignoffPath);
        string docsIndexPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_DOCS_INDEX.md");
        string docsIndexText = File.ReadAllText(docsIndexPath);

        StringAssert.Contains(statusScriptText, "route_proof_shape=");
        StringAssert.Contains(statusScriptText, "execution_proof_tier=");
        StringAssert.Contains(statusScriptText, "execution_route_lane=");
        StringAssert.Contains(statusScriptText, "execution_promoted_route_base=");
        StringAssert.Contains(statusScriptText, "execution_workflow_family_ids=");

        StringAssert.Contains(releaseSignoffText, "hosted `chummer.run` route-entry posture exists and is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`");
        StringAssert.Contains(releaseSignoffText, "hosted `chummer.run` workflow execution is a stricter proof tier, published separately as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`");

        StringAssert.Contains(docsIndexText, "docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        StringAssert.Contains(docsIndexText, "docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        StringAssert.Contains(docsIndexText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        StringAssert.Contains(docsIndexText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
    }

    [TestMethod]
    public void Shared_public_edge_status_surface_keeps_route_and_execution_blocker_summary_fields()
    {
        string repoRoot = FindRepoRoot();
        string statusScriptPath = Path.Combine(repoRoot, "scripts", "print_blazor_public_edge_proof_status.py");
        string statusScriptText = File.ReadAllText(statusScriptPath);
        string blockerMaterializerPath = Path.Combine(repoRoot, "scripts", "materialize-external-host-proof-blockers.py");
        string blockerMaterializerText = File.ReadAllText(blockerMaterializerPath);

        StringAssert.Contains(statusScriptText, "route_proof_receipt=");
        StringAssert.Contains(statusScriptText, "route_proof_shape=");
        StringAssert.Contains(statusScriptText, "route_proof_marker_ids=");
        StringAssert.Contains(statusScriptText, "route_workflow_shape_ids=");
        StringAssert.Contains(statusScriptText, "execution_proof_receipt=");
        StringAssert.Contains(statusScriptText, "execution_workflow_family_ids=");
        StringAssert.Contains(statusScriptText, "blocker_route_entry_shape=");
        StringAssert.Contains(statusScriptText, "blocker_execution_summary=");

        StringAssert.Contains(blockerMaterializerText, "payload[\"browser_route_entry_proof_shape\"] = classify_route_entry_proof_shape(route_payload)");
        StringAssert.Contains(blockerMaterializerText, "payload[\"browser_execution_proof_status\"] = execution_receipt_status");
        StringAssert.Contains(blockerMaterializerText, "payload[\"browser_execution_proof_contract\"] = execution_receipt_contract");
    }

    [TestMethod]
    public void Hosted_public_edge_execution_tooling_paths_stay_wired_across_docs_and_downstream_capture_surfaces()
    {
        string repoRoot = FindRepoRoot();
        string executionDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        string executionDocText = File.ReadAllText(executionDocPath);
        string docsIndexPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_DOCS_INDEX.md");
        string docsIndexText = File.ReadAllText(docsIndexPath);
        string uiGoldPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "ui-gold-proof-depth-gate.sh");
        string uiGoldText = File.ReadAllText(uiGoldPath);
        string verifyPath = Path.Combine(repoRoot, "scripts", "ai", "verify.sh");
        string verifyText = File.ReadAllText(verifyPath);

        StringAssert.Contains(executionDocText, "`scripts/e2e-public-edge-playwright.cjs`");
        StringAssert.Contains(executionDocText, "`scripts/e2e-public-edge-execution.sh`");
        StringAssert.Contains(executionDocText, "`scripts/verify_blazor_public_edge_execution_proof.py`");
        StringAssert.Contains(executionDocText, "`scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`");
        StringAssert.Contains(executionDocText, "`scripts/print_blazor_public_edge_proof_status.py`");

        StringAssert.Contains(docsIndexText, "scripts/verify_blazor_public_edge_execution_proof.py");
        StringAssert.Contains(docsIndexText, "scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh");
        StringAssert.Contains(docsIndexText, "scripts/print_blazor_public_edge_proof_status.py");

        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_runner\": str(repo / \"scripts\" / \"e2e-public-edge-execution.sh\")");
        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_status_summary\": str(repo / \"scripts\" / \"print_blazor_public_edge_proof_status.py\")");
        StringAssert.Contains(uiGoldText, "\"blazor_public_edge_execution_verifier\": str(repo / \"scripts\" / \"verify_blazor_public_edge_execution_proof.py\")");

        StringAssert.Contains(verifyText, "bash scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh");
    }

    [TestMethod]
    public void Hosted_public_edge_proof_wrapper_scripts_keep_direct_verifier_bindings()
    {
        string repoRoot = FindRepoRoot();
        string routeWrapperPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "blazor-public-edge-workbench-proof-check.sh");
        string routeWrapperText = File.ReadAllText(routeWrapperPath);
        string executionWrapperPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "blazor-public-edge-execution-proof-check.sh");
        string executionWrapperText = File.ReadAllText(executionWrapperPath);
        string routeDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        string routeDocText = File.ReadAllText(routeDocPath);
        string executionDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        string executionDocText = File.ReadAllText(executionDocPath);

        StringAssert.Contains(routeWrapperText, "python3 \"$repo_root/scripts/verify_blazor_public_edge_workbench_proof.py\"");
        StringAssert.Contains(executionWrapperText, "python3 \"$repo_root/scripts/verify_blazor_public_edge_execution_proof.py\"");

        StringAssert.Contains(routeDocText, "`scripts/ai/milestones/blazor-public-edge-workbench-proof-check.sh`");
        StringAssert.Contains(routeDocText, "`scripts/verify_blazor_public_edge_workbench_proof.py`");
        StringAssert.Contains(executionDocText, "`scripts/ai/milestones/blazor-public-edge-execution-proof-check.sh`");
        StringAssert.Contains(executionDocText, "`scripts/verify_blazor_public_edge_execution_proof.py`");
    }

    [TestMethod]
    public void Hosted_public_edge_execution_required_workflow_family_inventory_stays_aligned_across_placeholder_verifier_and_downstream_consumers()
    {
        string repoRoot = FindRepoRoot();
        string verifierPath = Path.Combine(repoRoot, "scripts", "verify_blazor_public_edge_execution_proof.py");
        string verifierText = File.ReadAllText(verifierPath);
        string placeholderPath = Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        string placeholderText = File.ReadAllText(placeholderPath);
        string uiGoldPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "ui-gold-proof-depth-gate.sh");
        string uiGoldText = File.ReadAllText(uiGoldPath);

        StringAssert.Contains(verifierText, "\"promoted_startup_command_executions\"");
        StringAssert.Contains(verifierText, "\"promoted_resumed_workspace\"");
        StringAssert.Contains(verifierText, "\"promoted_recent_work_affordances\"");
        StringAssert.Contains(verifierText, "\"promoted_restored_section_continuations\"");
        StringAssert.Contains(verifierText, "\"promoted_restored_tab_landings\"");
        StringAssert.Contains(verifierText, "\"promoted_restored_section_content\"");
        StringAssert.Contains(verifierText, "\"promoted_result_continuations\"");
        StringAssert.Contains(verifierText, "\"promoted_action_continuations\"");
        StringAssert.Contains(verifierText, "\"promoted_advanced_action_affordances\"");
        StringAssert.Contains(verifierText, "\"promoted_advanced_action_executions\"");
        StringAssert.Contains(verifierText, "\"promoted_committed_actions\"");
        StringAssert.Contains(verifierText, "\"promoted_advanced_committed_actions\"");

        StringAssert.Contains(placeholderText, "\"promoted_startup_command_executions\"");
        StringAssert.Contains(placeholderText, "\"promoted_resumed_workspace\"");
        StringAssert.Contains(placeholderText, "\"promoted_recent_work_affordances\"");
        StringAssert.Contains(placeholderText, "\"promoted_restored_section_continuations\"");
        StringAssert.Contains(placeholderText, "\"promoted_restored_tab_landings\"");
        StringAssert.Contains(placeholderText, "\"promoted_restored_section_content\"");
        StringAssert.Contains(placeholderText, "\"promoted_result_continuations\"");
        StringAssert.Contains(placeholderText, "\"promoted_action_continuations\"");
        StringAssert.Contains(placeholderText, "\"promoted_advanced_action_affordances\"");
        StringAssert.Contains(placeholderText, "\"promoted_advanced_action_executions\"");
        StringAssert.Contains(placeholderText, "\"promoted_committed_actions\"");
        StringAssert.Contains(placeholderText, "\"promoted_advanced_committed_actions\"");

        StringAssert.Contains(uiGoldText, "\"promoted_startup_command_executions\"");
        StringAssert.Contains(uiGoldText, "\"promoted_resumed_workspace\"");
        StringAssert.Contains(uiGoldText, "\"promoted_recent_work_affordances\"");
        StringAssert.Contains(uiGoldText, "\"promoted_restored_section_continuations\"");
        StringAssert.Contains(uiGoldText, "\"promoted_restored_tab_landings\"");
        StringAssert.Contains(uiGoldText, "\"promoted_restored_section_content\"");
        StringAssert.Contains(uiGoldText, "\"promoted_result_continuations\"");
        StringAssert.Contains(uiGoldText, "\"promoted_action_continuations\"");
        StringAssert.Contains(uiGoldText, "\"promoted_advanced_action_affordances\"");
        StringAssert.Contains(uiGoldText, "\"promoted_advanced_action_executions\"");
        StringAssert.Contains(uiGoldText, "\"promoted_committed_actions\"");
        StringAssert.Contains(uiGoldText, "\"promoted_advanced_committed_actions\"");
    }

    [TestMethod]
    public void Published_hosted_public_edge_route_and_execution_receipts_stay_explicitly_separate()
    {
        string repoRoot = FindRepoRoot();
        string routeReceiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        string routeReceiptText = File.ReadAllText(routeReceiptPath);
        string executionReceiptPath = Path.Combine(repoRoot, ".codex-studio", "published", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        string executionReceiptText = File.ReadAllText(executionReceiptPath);
        string executionDocPath = Path.Combine(repoRoot, "docs", "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        string executionDocText = File.ReadAllText(executionDocPath);
        string releaseSignoffPath = Path.Combine(repoRoot, "docs", "WORKBENCH_RELEASE_SIGNOFF.md");
        string releaseSignoffText = File.ReadAllText(releaseSignoffPath);

        StringAssert.Contains(routeReceiptText, "\"contract_name\": \"chummer6-ui.blazor_public_edge_workbench_proof\"");
        Assert.IsFalse(routeReceiptText.Contains("\"contract_name\": \"chummer6-ui.blazor_public_edge_execution_proof\"", StringComparison.Ordinal));
        StringAssert.Contains(executionReceiptText, "\"contract_name\": \"chummer6-ui.blazor_public_edge_execution_proof\"");
        Assert.IsFalse(executionReceiptText.Contains("\"contract_name\": \"chummer6-ui.blazor_public_edge_workbench_proof\"", StringComparison.Ordinal));
        StringAssert.Contains(executionReceiptText, "\"status\": \"not_run\"");

        StringAssert.Contains(executionDocText, "Route-entry proof lives in BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json and is not equivalent to this execution-proof receipt.");
        StringAssert.Contains(releaseSignoffText, "hosted `chummer.run` route-entry posture exists and is published as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json`");
        StringAssert.Contains(releaseSignoffText, "hosted `chummer.run` workflow execution is a stricter proof tier, published separately as `.codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json`");
    }

    [TestMethod]
    public void Browser_client_top_level_docs_keep_route_entry_and_execution_proof_contracts_visible()
    {
        string repoRoot = FindRepoRoot();
        string docsIndexPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_DOCS_INDEX.md");
        string docsIndexText = File.ReadAllText(docsIndexPath);
        string parityGoalPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_PARITY_GOAL.md");
        string parityGoalText = File.ReadAllText(parityGoalPath);
        string selfHostRunbookPath = Path.Combine(repoRoot, "docs", "BLAZOR_SELF_HOST_RUNBOOK.md");
        string selfHostRunbookText = File.ReadAllText(selfHostRunbookPath);

        StringAssert.Contains(docsIndexText, "docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        StringAssert.Contains(docsIndexText, "docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        StringAssert.Contains(docsIndexText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        StringAssert.Contains(docsIndexText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");

        StringAssert.Contains(parityGoalText, "docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md");
        StringAssert.Contains(parityGoalText, "docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        StringAssert.Contains(parityGoalText, "The current hosted route-entry proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md`.");
        StringAssert.Contains(parityGoalText, "The current hosted execution-proof target for the public edge is defined in `docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md`.");

        StringAssert.Contains(selfHostRunbookText, "docs/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md");
        StringAssert.Contains(selfHostRunbookText, "Use [BLAZOR_WEB_CLIENT_DOCS_INDEX.md]");
    }

    [TestMethod]
    public void Browser_lane_release_docs_keep_self_host_route_entry_and_execution_proof_tiers_separate()
    {
        string repoRoot = FindRepoRoot();
        string releaseSignoffPath = Path.Combine(repoRoot, "docs", "WORKBENCH_RELEASE_SIGNOFF.md");
        string releaseSignoffText = File.ReadAllText(releaseSignoffPath);
        string parityGoalPath = Path.Combine(repoRoot, "docs", "BLAZOR_WEB_CLIENT_PARITY_GOAL.md");
        string parityGoalText = File.ReadAllText(parityGoalPath);

        StringAssert.Contains(releaseSignoffText, ".codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json");
        StringAssert.Contains(releaseSignoffText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json");
        StringAssert.Contains(releaseSignoffText, ".codex-studio/published/BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json");
        StringAssert.Contains(releaseSignoffText, "Release truth for the browser lane therefore splits into three separate statements:");
        StringAssert.Contains(releaseSignoffText, "Until the hosted execution tier passes");

        StringAssert.Contains(parityGoalText, "public hosted and Docker self-hosted lanes have separate proof so local success is not mistaken for `chummer.run` readiness");
        StringAssert.Contains(parityGoalText, "a separate hosted execution-proof lane for `chummer.run` browser workflows, not only hosted route-entry posture");
        StringAssert.Contains(parityGoalText, "separate self-host receipt proof for portal-backed `/blazor/workbench` and `/blazor/preview` routes under Docker");
        StringAssert.Contains(parityGoalText, "separate hosted route-entry proof for the `https://chummer.run/blazor/` public edge");
        StringAssert.Contains(parityGoalText, "a dedicated hosted execution-proof contract, runner scaffold, verifier, and published placeholder receipt");
    }

    [TestMethod]
    public void Linux_desktop_exit_gate_only_requires_promoted_installer_byte_parity_in_promoted_mode()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "elif promoted_mode:");
        StringAssert.Contains(scriptText, "if promoted_mode and expected_digest and receipt_digest != expected_digest:");
        StringAssert.Contains(scriptText, "if promoted_mode and expected_digest and mouse_digest != expected_digest:");
        StringAssert.Contains(scriptText, "elif [[ -n \"${CI:-}\" ]]; then");
        StringAssert.Contains(scriptText, "USE_PROMOTED_INSTALLER=\"0\"");
    }

    [TestMethod]
    public void Release_manifest_generation_prefers_startup_smoke_receipts_that_match_current_download_bytes()
    {
        string repoRoot = FindRepoRoot();
        string releaseManifestScriptPath = Path.Combine(repoRoot, "scripts", "generate-releases-manifest.sh");
        string releaseManifestScriptText = File.ReadAllText(releaseManifestScriptPath);

        StringAssert.Contains(releaseManifestScriptText, "def receipt_matches_download_bytes(payload: dict) -> bool:");
        StringAssert.Contains(releaseManifestScriptText, "selection_rank = 1 if receipt_matches_download_bytes(payload) else 0");
        StringAssert.Contains(releaseManifestScriptText, "artifact_digest_cache: dict[Path, str] = {}");
    }

    [TestMethod]
    public void Release_manifest_generation_restores_missing_receipt_backed_artifacts_from_trusted_shelves()
    {
        string repoRoot = FindRepoRoot();
        string releaseManifestScriptPath = Path.Combine(repoRoot, "scripts", "generate-releases-manifest.sh");
        string releaseManifestScriptText = File.ReadAllText(releaseManifestScriptPath);

        StringAssert.Contains(releaseManifestScriptText, "def trusted_receipt_artifact_dirs(repo_root: Path, registry_root: Path) -> list[Path]:");
        StringAssert.Contains(releaseManifestScriptText, "repo_root.parent / \"chummer.run-services\" / \"Chummer.Portal\" / \"downloads\" / \"files\"");
        StringAssert.Contains(releaseManifestScriptText, "def restore_missing_receipt_backed_artifact(payload: dict) -> Path | None:");
        StringAssert.Contains(releaseManifestScriptText, "shutil.copy2(candidate_path, target_path)");
        StringAssert.Contains(releaseManifestScriptText, "restore_missing_receipt_backed_artifact(payload)");
    }

    [TestMethod]
    public void Release_manifest_generation_scans_all_published_desktop_gate_receipts_for_startup_smoke_hydration()
    {
        string repoRoot = FindRepoRoot();
        string releaseManifestScriptPath = Path.Combine(repoRoot, "scripts", "generate-releases-manifest.sh");
        string releaseManifestScriptText = File.ReadAllText(releaseManifestScriptPath);

        StringAssert.Contains(releaseManifestScriptText, "gate_paths = sorted((repo_root / \".codex-studio\" / \"published\").glob(\"UI_*_DESKTOP_EXIT_GATE.generated.json\"))");
        StringAssert.Contains(releaseManifestScriptText, "embedded_gate_receipts_dir = Path(tempfile.mkdtemp(prefix=\"chummer-startup-smoke-gate-\"))");
        StringAssert.Contains(releaseManifestScriptText, "embedded_receipt = startup_smoke.get(\"receipt\")");
        StringAssert.Contains(releaseManifestScriptText, "candidate_dirs.append(Path(receipt_path).resolve(strict=False).parent)");
        StringAssert.Contains(releaseManifestScriptText, "\"$DOWNLOADS_DIR\"");
    }

    [TestMethod]
    public void Workflow_parity_receipts_pin_script_execution_to_repo_root()
    {
        string repoRoot = FindRepoRoot();
        string sr4ScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "sr4-desktop-workflow-parity-check.sh");
        string sr6ScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "sr6-desktop-workflow-parity-check.sh");
        string sr4ScriptText = File.ReadAllText(sr4ScriptPath);
        string sr6ScriptText = File.ReadAllText(sr6ScriptPath);

        StringAssert.Contains(sr4ScriptText, "cd \"$repo_root\"");
        StringAssert.Contains(sr4ScriptText, "dotnet test --project Chummer.Tests/Chummer.Tests.csproj");
        StringAssert.Contains(sr6ScriptText, "cd \"$repo_root\"");
        StringAssert.Contains(sr6ScriptText, "dotnet test --project Chummer.Tests/Chummer.Tests.csproj");
    }

    [TestMethod]
    public void Test_wrapper_strips_positional_project_argument_before_invoking_mstest_runner()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "test.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "normalized_project_path");
        StringAssert.Contains(scriptText, "positional_target");
        StringAssert.Contains(scriptText, "if [[ -n \"$normalized_project_path\" && \"$positional_target\" == \"$normalized_project_path\" ]]; then");
        StringAssert.Contains(scriptText, "run_mstest_runner");
    }

    [TestMethod]
    public void Gold_critical_gate_test_sources_are_compiled_into_main_test_project()
    {
        string repoRoot = FindRepoRoot();
        string projectPath = Path.Combine(repoRoot, "Chummer.Tests", "Chummer.Tests.csproj");
        string projectText = File.ReadAllText(projectPath);

        string[] requiredIncludes =
        {
            "Compliance\\ArchitectureGuardrailTests.cs",
            "Compliance\\DesktopVisualFamiliarityGateGuardTests.cs",
            "Compliance\\Next90M101ReleaseTrainGuardTests.cs",
            "Compliance\\Next90M117ArtifactShelfGuardTests.cs",
            "Compliance\\Next90M141UiImportRouteProofGuardTests.cs",
            "Compliance\\Next90M144DesktopProofGuardTests.cs",
            "Compliance\\ParityChecklistComplianceTests.cs",
            "Compliance\\XmlBoundaryGuardrailTests.cs",
            "Presentation\\DesktopInstallLinkingShellChromeTests.cs",
        };

        foreach (string requiredInclude in requiredIncludes)
        {
            StringAssert.Contains(projectText, $"<Compile Include=\"{requiredInclude}\" />");
        }
    }

    [TestMethod]
    public void Visual_familiarity_gate_refreshes_stale_screenshot_pack_via_b14_without_recursing_into_downstream_receipts()
    {
        string repoRoot = FindRepoRoot();
        string visualScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string flagshipGateScriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string visualScriptText = File.ReadAllText(visualScriptPath);
        string flagshipGateScriptText = File.ReadAllText(flagshipGateScriptPath);

        StringAssert.Contains(visualScriptText, "CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE");
        StringAssert.Contains(visualScriptText, "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS=1");
        StringAssert.Contains(visualScriptText, "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS=0");
        StringAssert.Contains(flagshipGateScriptText, "skip_downstream_receipt_materialization");
        StringAssert.Contains(flagshipGateScriptText, "skipping downstream proof materialization for screenshot refresh-only pass");
        StringAssert.Contains(flagshipGateScriptText, "rm -f \"$lock_owner_pid_path\"");
        StringAssert.Contains(flagshipGateScriptText, "rmdir \"$lock_dir\" 2>/dev/null || rm -rf \"$lock_dir\" 2>/dev/null || true");
    }

    [TestMethod]
    public void Desktop_executable_gate_uses_env_wrapped_visual_dependency_invocation()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "if ! env");
        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT");
        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_WAIT_SECONDS");
        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_POLL_SECONDS");
    }

    [TestMethod]
    public void Desktop_executable_gate_forces_workflow_dependency_refresh_against_the_same_release_channel()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH=\"$release_channel_path\"");
        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_WORKFLOW_REFRESH_DEPENDENCY_RECEIPTS=1");
        StringAssert.Contains(scriptText, "CHUMMER_HUB_REGISTRY_ROOT=\"$hub_registry_root\"");
        StringAssert.Contains(scriptText, "bash \"$workflow_execution_materializer_path\"");
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
    public void Desktop_executable_gate_republishes_fleet_flagship_readiness_after_writing_a_new_ui_receipt()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "flagship_product_readiness_materializer_path=\"${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}\"");
        StringAssert.Contains(scriptText, "receipt_path.write_text(json.dumps(payload, indent=2) + \"\\n\", encoding=\"utf-8\")");
        StringAssert.Contains(scriptText, "python3 \"$flagship_product_readiness_materializer_path\" >/dev/null 2>&1 || true");
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
        StringAssert.Contains(scriptText, "def receipt_is_current_and_passing(");
        StringAssert.Contains(scriptText, "release_gate_lock_observed_but_receipts_current = (");
        StringAssert.Contains(scriptText, "receipt_is_current_and_passing(flagship_gate, allow_stale_pass_receipt=True)");
        StringAssert.Contains(scriptText, "receipt_is_current_and_passing(visual_familiarity_gate)");
        StringAssert.Contains(scriptText, "receipt_is_current_and_passing(workflow_execution_gate)");
        StringAssert.Contains(scriptText, "if not release_gate_lock_observed_but_receipts_current:");
        StringAssert.Contains(scriptText, "evidence[\"release_gate_lock_observed_but_receipts_current\"] = release_gate_lock_observed_but_receipts_current");
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
    public void Desktop_executable_gate_fail_closes_stale_passing_startup_smoke_receipts_that_do_not_match_published_artifacts()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-executable-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def startup_smoke_receipt_matches_any_published_artifact(");
        StringAssert.Contains(scriptText, "published_desktop_artifacts_by_tuple");
        StringAssert.Contains(scriptText, "published_startup_smoke_roots");
        StringAssert.Contains(scriptText, "collect_stale_passing_startup_smoke_receipts_against_published_artifacts(");
        StringAssert.Contains(scriptText, "stale_linux_startup_smoke_receipts_against_published_artifacts");
        StringAssert.Contains(scriptText, "stale_windows_startup_smoke_receipts_against_published_artifacts");
        StringAssert.Contains(scriptText, "stale_macos_startup_smoke_receipts_against_published_artifacts");
        StringAssert.Contains(scriptText, "stale_passing_startup_smoke_receipts_against_published_artifacts");
        StringAssert.Contains(scriptText, "matchesPublishedArtifact");
        StringAssert.Contains(scriptText, "artifactRelativePath");
        StringAssert.Contains(scriptText, "Stale passing startup smoke receipts exist for non-promoted or artifact-drifted desktop proof:");
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
        string windowsGateScriptPath = Path.Combine(repoRoot, "scripts", "materialize-windows-desktop-exit-gate.sh");

        string executableScriptText = File.ReadAllText(executableScriptPath);
        string visualScriptText = File.ReadAllText(visualScriptPath);
        string workflowScriptText = File.ReadAllText(workflowScriptPath);
        string windowsGateScriptText = File.ReadAllText(windowsGateScriptPath);

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
        StringAssert.Contains(workflowScriptText, "dependency_refresh_timeout_seconds");
        StringAssert.Contains(workflowScriptText, "dependency_refresh_report_path");
        StringAssert.Contains(workflowScriptText, "record_dependency_refresh_attempt");
        StringAssert.Contains(workflowScriptText, "timeout --foreground");
        StringAssert.Contains(workflowScriptText, "\"dependency_refresh_attempts\"");
        StringAssert.Contains(workflowScriptText, "dependency refresh failed via");
        StringAssert.Contains(workflowScriptText, "dependency refresh did not update receipt timestamp or mtime");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt is missing version for promoted head");
        StringAssert.Contains(executableScriptText, "Linux installer startup smoke receipt version does not match release channel version for promoted head");
        StringAssert.Contains(executableScriptText, "startup_smoke_version_proves_release(");
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
        StringAssert.Contains(executableScriptText, "Release channel rolloutState must be local_docker_preview/promoted_preview/release_candidate/public_stable/stable when status is publishable and required desktop tuple coverage is complete.");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_allowed_for_publishable_complete_values");
        StringAssert.Contains(executableScriptText, "release_channel_supportability_state_invalid_for_publishable_complete");
        StringAssert.Contains(executableScriptText, "Release channel supportabilityState must be local_docker_proven/preview_supported/gold_supported when status is publishable and required desktop tuple coverage is complete.");
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
        StringAssert.Contains(visualScriptText, "if not flagship_gate_path.is_file() or not flagship_gate:");
        StringAssert.Contains(visualScriptText, "\"flagship_gate_path\": str(flagship_gate_path)");

        StringAssert.Contains(workflowScriptText, "CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH");
        StringAssert.Contains(workflowScriptText, "release_channel_channel_id");
        StringAssert.Contains(workflowScriptText, "release_channel_version");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing channelId/channel.");
        StringAssert.Contains(workflowScriptText, "Desktop workflow execution gate release channel receipt is missing version.");
        StringAssert.Contains(workflowScriptText, "\"releaseVersion\": release_channel_version");
        StringAssert.Contains(workflowScriptText, "canonical_required_desktop_heads = [\"avalonia\"]");
        StringAssert.Contains(workflowScriptText, "collect_release_channel_head_requirements");
        StringAssert.Contains(workflowScriptText, "release_channel_required_desktop_heads");
        StringAssert.Contains(workflowScriptText, "release_channel_primary_desktop_heads");
        StringAssert.Contains(workflowScriptText, "release_channel_promoted_non_fallback_desktop_heads");
        StringAssert.Contains(workflowScriptText, "flagship_primary_desktop_heads");
        StringAssert.Contains(workflowScriptText, "flagship_declared_desktop_fallback_heads");
        StringAssert.Contains(workflowScriptText, "flagship_missing_canonical_required_desktop_heads");
        StringAssert.Contains(workflowScriptText, "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head workflow execution proof:");
        StringAssert.Contains(workflowScriptText, "collect_external_blockers");
        StringAssert.Contains(workflowScriptText, "external_blockers_are_only_missing_api_surface_contract");
        StringAssert.Contains(workflowScriptText, "workflow_family_receipts_outside_repo_root");
        StringAssert.Contains(workflowScriptText, "workflow_execution_receipts_outside_repo_root");
        StringAssert.Contains(workflowScriptText, "SR4/SR6 family-level workflow receipts resolve outside this repo root:");
        StringAssert.Contains(workflowScriptText, "SR4/SR6 family-level execution receipts resolve outside this repo root:");
        StringAssert.Contains(workflowScriptText, "outside_repo_root:{entry}");
        StringAssert.Contains(
            executableScriptText,
            "Flagship UI release gate is missing or not passing."
        );
        StringAssert.Contains(
            windowsGateScriptText,
            "UI local release proof is missing or not passed."
        );
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
        StringAssert.Contains(scriptText, "INSTALLER_SMOKE_ARTIFACT_PATH=\"$INSTALLER_PATH\"");
        StringAssert.Contains(scriptText, "receipt.get(\"artifactPath\"),");
        StringAssert.Contains(scriptText, "receipt.get(\"artifactRelativePath\"),");
        Assert.IsTrue(
            scriptText.IndexOf("receipt.get(\"artifactPath\"),", StringComparison.Ordinal)
            < scriptText.IndexOf("receipt.get(\"artifactRelativePath\"),", StringComparison.Ordinal),
            "Linux promoted installer receipt resolution must prefer artifactPath before artifactRelativePath so canonical gate-run proof wins over stale repo-root dist copies.");
        StringAssert.Contains(scriptText, "Linux startup smoke installer artifact path is neither the promoted repo-local shelf bytes nor a canonical gate-run copy.");
        StringAssert.Contains(scriptText, "Linux startup smoke receipt artifactPath is neither the promoted installer shelf bytes nor a canonical gate-run copy.");
        Assert.IsFalse(
            scriptText.Contains("INSTALLER_SMOKE_ARTIFACT_PATH=\"$PROMOTED_INSTALLER_PATH\"", StringComparison.Ordinal),
            "Linux promoted installer smoke must run against the canonical gate-run copy so embedded proof paths stay inside the canonical output root.");
    }

    [TestMethod]
    public void Linux_exit_gate_validates_flagship_screenshot_pack_without_recursing_into_b14()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "FLAGSHIP_UI_SCREENSHOT_CONTROL_EVIDENCE_PATH=");
        StringAssert.Contains(scriptText, "control_evidence_path = pathlib.Path(sys.argv[3])");
        StringAssert.Contains(scriptText, "def load_json(path: pathlib.Path) -> object:");
        StringAssert.Contains(scriptText, "control_evidence = load_json(control_evidence_path)");
        StringAssert.Contains(scriptText, "workflow_coverage = control_evidence.get(\"workflowCoverage\") or []");
        StringAssert.Contains(scriptText, "for entry in control_evidence.get(\"entries\") or []");
        Assert.IsFalse(
            scriptText.Contains("bash \"$FLAGSHIP_UI_GATE_SCRIPT\"", StringComparison.Ordinal),
            "Linux exit gate must validate screenshot proof directly instead of recursing through b14 and deadlocking on desktop executable coverage.");
    }

    [TestMethod]
    public void Linux_exit_gate_excludes_generated_build_outputs_from_source_snapshot_identity()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def is_generated_build_output(relative_path: str) -> bool:");
        StringAssert.Contains(scriptText, "return any(part in {\"bin\", \"obj\", \"TestResults\"} for part in parts)");
        StringAssert.Contains(scriptText, "if is_generated_build_output(relative):");
        StringAssert.Contains(scriptText, "\"$SOURCE_SNAPSHOT_ROOT/TestResults\"");
        StringAssert.Contains(scriptText, "\"$SOURCE_SNAPSHOT_ROOT/Chummer.Desktop.Runtime.Tests/TestResults\"");
    }

    [TestMethod]
    public void Linux_exit_gate_source_snapshot_prefers_link_or_copy_materialization_to_avoid_disk_exhaustion()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def clone_regular_file(src_path: pathlib.Path, dest_path: pathlib.Path) -> None:");
        StringAssert.Contains(scriptText, "os.link(src_path, dest_path)");
        StringAssert.Contains(scriptText, "shutil.copytree(src_path, dest_path, dirs_exist_ok=True, copy_function=clone_regular_file)");
        StringAssert.Contains(scriptText, "\"mode\": \"filesystem_link_or_copy\"");
        StringAssert.Contains(scriptText, "\"source_snapshot_root\": str(source_snapshot.get(\"snapshot_root\") or \"\")");
    }

    [TestMethod]
    public void Linux_exit_gate_copies_tracked_snapshot_inputs_without_shared_inodes()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def copy_tracked_regular_file(src_path: pathlib.Path, dest_path: pathlib.Path) -> None:");
        StringAssert.Contains(scriptText, "restore/publish can mutate some source-adjacent files in place.");
        StringAssert.Contains(scriptText, "copy_tracked_regular_file(src_path, dest_path)");
    }

    [TestMethod]
    public void Linux_exit_gate_source_snapshot_defaults_to_copy_isolation()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "SOURCE_SNAPSHOT_CLONE_MODE=\"${CHUMMER_LINUX_DESKTOP_EXIT_GATE_SOURCE_SNAPSHOT_CLONE_MODE:-copy}\"");
        StringAssert.Contains(scriptText, "if clone_mode in {\"link\", \"link_or_copy\"}:");
        StringAssert.Contains(scriptText, "\"mode\": \"filesystem_link_or_copy\" if clone_mode in {\"link\", \"link_or_copy\"} else \"filesystem_copy\"");
    }

    [TestMethod]
    public void Linux_exit_gate_publishes_proof_git_identity_from_finish_snapshot_not_current_snapshot()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "\"git\": {");
        StringAssert.Contains(scriptText, "**git_finish");
        StringAssert.Contains(scriptText, "\"current\": current_git");
        Assert.IsFalse(
            scriptText.Contains("\"git\": {\n        **current_git,", StringComparison.Ordinal),
            "Linux exit gate proof identity must be anchored to the proof finish snapshot, not the mutable current worktree snapshot.");
    }

    [TestMethod]
    public void Linux_exit_gate_git_metadata_keeps_cross_head_inputs_aligned_with_the_source_snapshot()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def read_git_metadata(repo_root_text: str, output_base_root_text: str, canonical_proof_path_text: str):");
        StringAssert.Contains(scriptText, "\"Chummer.Blazor/\"");
        StringAssert.Contains(scriptText, "\"Chummer.Blazor.Desktop/\"");
        StringAssert.Contains(scriptText, "\"Chummer/chummer.ico\"");
        StringAssert.Contains(scriptText, "\"Chummer/chummer6-icon-preview.png\"");
        StringAssert.Contains(scriptText, "\"Chummer/changelog.txt\"");
        Assert.IsTrue(
            scriptText.IndexOf("def read_git_metadata(repo_root_text: str, output_base_root_text: str, canonical_proof_path_text: str):", StringComparison.Ordinal) < scriptText.LastIndexOf("\"Chummer/changelog.txt\"", StringComparison.Ordinal),
            "Linux exit gate git metadata must cover the same cross-head desktop inputs as the immutable source snapshot so proof fingerprints cannot drift on untouched code.");
    }

    [TestMethod]
    public void Linux_exit_gate_normalizes_mstest_platform_trx_output_into_canonical_receipt_path()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "normalize_test_trx_path()");
        StringAssert.Contains(scriptText, "find \"$candidate_dir\" -maxdepth 1 -type f -name '*.trx'");
        StringAssert.Contains(scriptText, "cp \"$discovered_trx\" \"$TEST_TRX_PATH\"");
        StringAssert.Contains(scriptText, "desktop runtime unit tests did not produce a TRX report");
        StringAssert.Contains(scriptText, "--logger \"trx;LogFileName=$(basename \"$TEST_TRX_PATH\")\"");
        StringAssert.Contains(scriptText, "dotnet test wrapper did not produce runnable desktop runtime test results; retrying via direct MSTest host");
        StringAssert.Contains(scriptText, "run_runtime_test_host_direct()");
        StringAssert.Contains(scriptText, "--report-trx");
        StringAssert.Contains(scriptText, "--report-trx-filename \"$(basename \"$TEST_TRX_PATH\")\"");
        StringAssert.Contains(scriptText, "test_trx_has_runnable_results()");
        StringAssert.Contains(scriptText, "desktop runtime unit tests did not produce any passing runnable test results");
        StringAssert.Contains(scriptText, "if ! test_trx_has_runnable_results; then");
        StringAssert.Contains(scriptText, "rm -f \"$TEST_TRX_PATH\"");
        StringAssert.Contains(scriptText, "assert_test_trx_passes");
        Assert.IsTrue(
            scriptText.Contains("normalize_test_trx_path\ntest -f \"$TEST_TRX_PATH\"\nassert_test_trx_passes", StringComparison.Ordinal),
            "Linux exit gate must normalize MSTest runner TRX output and fail closed unless the canonical receipt path contains runnable passing tests.");
    }

    [TestMethod]
    public void Linux_exit_gate_publishes_same_identity_failures_except_for_early_infra_lock_failures()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "git_start = dict(git.get(\"start\") or {})");
        StringAssert.Contains(scriptText, "source_snapshot = dict(payload.get(\"source_snapshot\") or {})");
        StringAssert.Contains(scriptText, "source_snapshot.get(\"worktree_sha256\")");
        StringAssert.Contains(scriptText, "source_snapshot.get(\"entry_count\")");
        StringAssert.Contains(scriptText, "early_infra_failure_stages = {\"source_snapshot\", \"build_lock\"}");
        StringAssert.Contains(scriptText, "and same_identity");
        StringAssert.Contains(scriptText, "and new_stage in early_infra_failure_stages");
        StringAssert.Contains(scriptText, "publish = False");
        Assert.IsFalse(
            scriptText.Contains("if same_identity and str(existing_payload.get(\"status\") or \"\").strip() == \"passed\" and str(new_payload.get(\"status\") or \"\").strip() != \"passed\":", StringComparison.Ordinal),
            "Linux exit gate must not suppress same-identity substantive failures behind an older passing receipt.");
    }

    [TestMethod]
    public void Test_wrapper_preserves_relative_project_paths_for_mstest_runner_projects()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "test.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "detect_mstest_runner");
        StringAssert.Contains(scriptText, "if [[ \"$use_mstest_runner\" -eq 0 ]]; then");
        StringAssert.Contains(scriptText, "normalize_projectish_args");
        Assert.IsTrue(
            scriptText.LastIndexOf("detect_mstest_runner", StringComparison.Ordinal) < scriptText.LastIndexOf("normalize_projectish_args", StringComparison.Ordinal),
            "The test wrapper must decide MSTest runner mode before normalizing project paths so .NET 10 can keep MSTest project arguments relative.");
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
        StringAssert.Contains(linuxScriptText, "mkdir -p \"$(dirname \"$BUILD_LOCK_PATH\")\"");
        StringAssert.Contains(linuxScriptText, "eval \"exec ${BUILD_LOCK_FD}>\\\"\\$BUILD_LOCK_PATH\\\"\"");
        StringAssert.Contains(linuxScriptText, "flock -n \"$BUILD_LOCK_FD\"");
        StringAssert.Contains(linuxScriptText, "flock -w \"$wait_seconds\" \"$BUILD_LOCK_FD\"");
        Assert.IsFalse(
            linuxScriptText.Contains("exec {BUILD_LOCK_FD}>", StringComparison.Ordinal),
            "The Linux gate must not use dynamic fd assignment for the build lock; it has produced build_lock failures in worker shells.");
    }

    [TestMethod]
    public void Linux_desktop_exit_gate_preserves_latest_same_identity_pass_when_early_infra_reruns_fail()
    {
        string repoRoot = FindRepoRoot();
        string linuxScriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string linuxScriptText = File.ReadAllText(linuxScriptPath);

        StringAssert.Contains(linuxScriptText, "def latest_passing_receipt_for_identity(identity, root: pathlib.Path):");
        StringAssert.Contains(linuxScriptText, "for receipt_path in sorted(root.glob(f\"run.*/{canonical_path.name}\")):");
        StringAssert.Contains(linuxScriptText, "if not payload or normalized_status(payload) != \"passed\":");
        StringAssert.Contains(linuxScriptText, "if normalized_status(new_payload) != \"passed\" and new_stage in early_infra_failure_stages:");
        StringAssert.Contains(linuxScriptText, "publish_source_path = best_receipt_path");
        StringAssert.Contains(linuxScriptText, "publish_run_root = best_receipt_path.parent");
        StringAssert.Contains(linuxScriptText, "temp_path.write_text(publish_source_path.read_text(encoding=\"utf-8\"), encoding=\"utf-8\")");
        StringAssert.Contains(linuxScriptText, "latest_link_path.symlink_to(publish_run_root)");
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
        StringAssert.Contains(windowsScriptText, "desktop_tuple_coverage.get(\"externalProofRequests\")");
        StringAssert.Contains(windowsScriptText, "\"publicationSource\": \"desktopTupleCoverage.externalProofRequests\"");
        StringAssert.Contains(windowsScriptText, "if windows_artifact is not None and str(windows_artifact.get(\"publicationSource\") or \"\").strip() == \"desktopTupleCoverage.externalProofRequests\":");
        StringAssert.Contains(windowsScriptText, "windows_artifact[\"sizeBytes\"] = installer_size");
        StringAssert.Contains(windowsScriptText, "or release_channel.get(\"generated_at\")");
        StringAssert.Contains(windowsScriptText, "windows_artifact[\"generatedAt\"] = fallback_generated_at");
        StringAssert.Contains(windowsScriptText, "windows_artifact[\"id\"] = str(windows_artifact.get(\"artifactId\") or \"\").strip()");
        StringAssert.Contains(executableGateScriptText, "def synthesize_external_proof_request_install_artifacts(");
        StringAssert.Contains(executableGateScriptText, "desktop_install_artifacts.extend(");
        StringAssert.Contains(executableGateScriptText, "\"publicationSource\": \"desktopTupleCoverage.externalProofRequests\"");
        StringAssert.Contains(executableGateScriptText, "startup_smoke_stale_age_acceptable = bool(");
        StringAssert.Contains(executableGateScriptText, "if startup_smoke_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS and not startup_smoke_stale_age_acceptable:");
        StringAssert.Contains(executableGateScriptText, "installer-preflight-sha256-mismatch");
        StringAssert.Contains(executableGateScriptText, "tuple_token not in published_installer_tuples");
        StringAssert.Contains(executableGateScriptText, "no_published_promoted_installer_tuple");
        StringAssert.Contains(executableGateScriptText, "ignored_linux_startup_smoke_receipts_without_published_installer_tuple");
        StringAssert.Contains(executableGateScriptText, "ignored_windows_startup_smoke_receipts_without_published_installer_tuple");
        StringAssert.Contains(executableGateScriptText, "ignored_macos_startup_smoke_receipts_without_published_installer_tuple");
        StringAssert.Contains(executableGateScriptText, "published_installer_startup_smoke_tuples");
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

    [TestMethod]
    public void Linux_desktop_exit_gate_requires_mouse_first_journey_and_headless_coverage()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "materialize-linux-desktop-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "DesktopMouseFirstJourneyRuntimeTests");
        StringAssert.Contains(scriptText, "AvaloniaHeadlessSmokeTests");
        StringAssert.Contains(scriptText, "ARCHIVE_MOUSE_FIRST_JOURNEY_RECEIPT_PATH");
        StringAssert.Contains(scriptText, "INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH");
        StringAssert.Contains(scriptText, "CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT");
        StringAssert.Contains(scriptText, "\"mouse_first_journey\"");
        StringAssert.Contains(scriptText, "Linux mouse-first journey receipt is missing");
        StringAssert.Contains(scriptText, "Linux mouse-first journey receipt does not prove a saved workspace.");
        StringAssert.Contains(scriptText, "Linux mouse-first journey receipt does not prove the File menu path.");
        StringAssert.Contains(scriptText, "Linux mouse-first journey receipt does not prove authentication portal was opened.");
        StringAssert.Contains(scriptText, "Linux mouse-first journey receipt authentication portal uri is missing or points to a non-public host.");
    }
}
