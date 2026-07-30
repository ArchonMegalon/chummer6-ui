#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DesktopWorkflowExecutionGateRefreshComplianceTests
{
    [TestMethod]
    public void Workflow_execution_gate_does_not_launder_semantic_no_op_refreshes()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "refresh_dependency_receipts=\"0\"");
        StringAssert.Contains(scriptText, "record_dependency_refresh_attempt");
        StringAssert.Contains(scriptText, "\"$before_generated_at\"");
        StringAssert.Contains(scriptText, "\"$after_generated_at\"");
        Assert.IsFalse(scriptText.Contains("refresh_receipt_generated_at_if_unchanged", StringComparison.Ordinal));
        Assert.IsFalse(scriptText.Contains("dependencyRefreshGeneratedAt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workflow_execution_gate_never_launders_non_zero_external_only_refreshes()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def add_dependency_refresh_failure_reason(");
        StringAssert.Contains(scriptText, "dependency_exit_code");
        StringAssert.Contains(scriptText, "record_dependency_refresh_attempt");
        StringAssert.Contains(scriptText, "evidence[\"workflow_family_external_only_deferred\"] = False");
        StringAssert.Contains(scriptText, "evidence[\"workflow_execution_external_only_deferred\"] = False");
        Assert.IsFalse(scriptText.Contains("refresh_receipt_generated_at_if_unchanged", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workflow_execution_gate_refreshes_visual_familiarity_before_screenshot_review()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        int flagshipIndex = scriptText.IndexOf("ui_flagship_release_gate|", StringComparison.Ordinal);
        int visualIndex = scriptText.IndexOf("desktop_visual_familiarity_gate|", StringComparison.Ordinal);
        int reviewIndex = scriptText.IndexOf("chummer5a_screenshot_review_gate|", StringComparison.Ordinal);

        Assert.IsTrue(flagshipIndex >= 0, "workflow gate must refresh the flagship release receipt.");
        Assert.IsTrue(visualIndex >= 0, "workflow gate must refresh the visual familiarity receipt.");
        Assert.IsTrue(reviewIndex >= 0, "workflow gate must refresh the screenshot review receipt.");
        Assert.IsTrue(
            flagshipIndex < visualIndex,
            "workflow gate must refresh the flagship release receipt before visual familiarity because flagship recaptures the review screenshots.");
        Assert.IsTrue(
            visualIndex < reviewIndex,
            "workflow gate must refresh visual familiarity before screenshot review because screenshot review depends on the visual receipt.");
    }

    [TestMethod]
    public void Workflow_execution_gate_frontloads_visual_proof_refresh_before_heavy_workflow_parity_packs()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        int flagshipIndex = scriptText.IndexOf("ui_flagship_release_gate|", StringComparison.Ordinal);
        int visualIndex = scriptText.IndexOf("desktop_visual_familiarity_gate|", StringComparison.Ordinal);
        int reviewIndex = scriptText.IndexOf("chummer5a_screenshot_review_gate|", StringComparison.Ordinal);
        int chummer5aParityIndex = scriptText.IndexOf("chummer5a_workflow_parity|", StringComparison.Ordinal);
        int sr4ParityIndex = scriptText.IndexOf("sr4_workflow_parity|", StringComparison.Ordinal);
        int sr6ParityIndex = scriptText.IndexOf("sr6_workflow_parity|", StringComparison.Ordinal);

        Assert.IsTrue(flagshipIndex >= 0, "workflow gate must refresh the flagship release receipt.");
        Assert.IsTrue(visualIndex >= 0, "workflow gate must refresh the visual familiarity receipt.");
        Assert.IsTrue(reviewIndex >= 0, "workflow gate must refresh the screenshot review receipt.");
        Assert.IsTrue(chummer5aParityIndex >= 0, "workflow gate must still refresh the Chummer5a workflow parity receipt.");
        Assert.IsTrue(sr4ParityIndex >= 0, "workflow gate must still refresh the SR4 workflow parity receipt.");
        Assert.IsTrue(sr6ParityIndex >= 0, "workflow gate must still refresh the SR6 workflow parity receipt.");
        Assert.IsTrue(
            reviewIndex < chummer5aParityIndex,
            "workflow gate must close stale screenshot-backed desktop proof before running the heavy Chummer5a workflow parity sweep.");
        Assert.IsTrue(
            reviewIndex < sr4ParityIndex,
            "workflow gate must close stale screenshot-backed desktop proof before running the heavy SR4 workflow parity sweep.");
        Assert.IsTrue(
            reviewIndex < sr6ParityIndex,
            "workflow gate must close stale screenshot-backed desktop proof before running the heavy SR6 workflow parity sweep.");
    }

    [TestMethod]
    public void Workflow_execution_gate_runs_flagship_refresh_without_recursive_downstream_materialization()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS=0");
        StringAssert.Contains(scriptText, "CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS=1");
        StringAssert.Contains(scriptText, "env \"${flagship_refresh_env[@]}\" \"${dependency_refresh_env[@]}\" bash \"$dependency_script\"");
    }

    [TestMethod]
    public void Workflow_execution_gate_passes_canonical_release_channel_and_target_receipt_paths_into_child_refresh_scripts()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "build_dependency_refresh_env()");
        StringAssert.Contains(scriptText, "\"CHUMMER_HUB_REGISTRY_ROOT=$hub_registry_root\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH=$release_channel_path\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH=$release_channel_path\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH=$dependency_receipt_target\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_RULESET_UI_ADAPTATION_RECEIPT_PATH=$dependency_receipt_target\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=1\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_NEXT90_M141_RELEASE_CHANNEL_PATH=$release_channel_path\"");
        StringAssert.Contains(scriptText, "\"CHUMMER_NEXT90_M141_UI_RECEIPT_PATH=$dependency_receipt_target\"");
        StringAssert.Contains(scriptText, "mapfile -t dependency_refresh_env < <(build_dependency_refresh_env \"$dependency_label\" \"$dependency_receipt_target\")");
        StringAssert.Contains(scriptText, "env \"${flagship_refresh_env[@]}\" \"${dependency_refresh_env[@]}\" bash \"$dependency_script\"");
    }

    [TestMethod]
    public void Workflow_execution_gate_atomically_publishes_before_optional_flagship_readiness_refresh()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "flagship_product_readiness_materializer_path=\"${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}\"");
        StringAssert.Contains(scriptText, "def write_json_atomic(path: Path, value: Dict[str, Any]) -> None:");
        StringAssert.Contains(scriptText, "os.replace(temporary_path, path)");
        StringAssert.Contains(scriptText, "write_json_atomic(receipt_path, payload)");
        StringAssert.Contains(scriptText, "if [[ \"$refresh_flagship_readiness\" == \"1\" ]]; then");
        StringAssert.Contains(scriptText, "python3 \"$flagship_product_readiness_materializer_path\" >/dev/null");
        Assert.IsFalse(scriptText.Contains("receipt_path.write_text(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workflow_execution_gate_requires_current_passing_m141_proof_without_stale_waiver()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "\"next90_m141_direct_import_route_proof\", next90_m141_direct_import_route_proof");
        StringAssert.Contains(scriptText, "next90_m141_direct_import_route_proof|$repo_root/scripts/ai/milestones/next90-m141-ui-direct-import-route-proof-check.sh");
        StringAssert.Contains(scriptText, "expected_contract=\"chummer6-ui.next90_m141_ui_direct_import_route_proof\"");
        StringAssert.Contains(scriptText, "evidence[\"direct_flagship_slice_waives_blockers\"] = False");
        Assert.IsFalse(scriptText.Contains("allow_stale_pass_receipt=True", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workflow_execution_gate_does_not_waive_route_local_flagship_failures()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "expected_contract=\"chummer6-ui.flagship_ui_release_gate\"");
        StringAssert.Contains(scriptText, "evidence[\"direct_flagship_slice_waives_blockers\"] = False");
        StringAssert.Contains(scriptText, "evidence[\"ui_flagship_release_gate_effective_status\"] = str(");
        StringAssert.Contains(scriptText, "evidence.get(\"ui_flagship_release_gate_status\") or \"\"");
    }

    [TestMethod]
    public void Workflow_execution_gate_does_not_waive_external_desktop_flagship_failures()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "evidence[\"ui_flagship_release_gate_external_desktop_only_deferred\"] = False");
        StringAssert.Contains(scriptText, "expected_contract=\"chummer6-ui.flagship_ui_release_gate\"");
        Assert.IsFalse(scriptText.Contains("if flagship_gate_route_local_only or flagship_gate_external_desktop_only", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Workflow_execution_gate_requires_screenshot_review_to_pass_its_exact_contract()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "chummer5a_screenshot_review_gate = check_receipt(");
        StringAssert.Contains(scriptText, "expected_contract=\"chummer6-ui.chummer5a_screenshot_review_gate\"");
        StringAssert.Contains(scriptText, "evidence[\"chummer5a_screenshot_review_gate_effective_status\"] = str(");
        StringAssert.Contains(scriptText, "evidence.get(\"chummer5a_screenshot_review_gate_status\") or \"\"");
    }

    [TestMethod]
    public void Workflow_execution_gate_requires_visual_familiarity_to_pass_its_exact_contract()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "visual_familiarity_gate = check_receipt(");
        StringAssert.Contains(scriptText, "expected_contract=\"chummer6-ui.desktop_visual_familiarity_exit_gate\"");
        StringAssert.Contains(scriptText, "evidence[\"desktop_visual_familiarity_gate_effective_status\"] = str(");
        StringAssert.Contains(scriptText, "evidence.get(\"desktop_visual_familiarity_gate_status\") or \"\"");
    }

    [TestMethod]
    public void Flagship_release_gate_does_not_define_route_local_readiness_waiver()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        Assert.IsFalse(scriptText.Contains("and not flagship_readiness_route_local_only", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Flagship_release_gate_tracks_effective_route_local_parity_closure_for_known_direct_proof_rows()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M142_UI_DIRECT_WORKFLOW_PROOF.generated.json");
        StringAssert.Contains(scriptText, "NEXT90_M143_UI_DIRECT_OUTPUT_PROOF.generated.json");
        StringAssert.Contains(scriptText, "ui_element_route_local_row_proofs");
        StringAssert.Contains(scriptText, "\"source:hero_lab_importer_route\"");
        StringAssert.Contains(scriptText, "\"family:dice_initiative_and_table_utilities\"");
        StringAssert.Contains(scriptText, "\"family:legacy_and_adjacent_import_oracles\"");
        StringAssert.Contains(scriptText, "\"family:sheet_export_print_viewer_and_exchange\"");
        StringAssert.Contains(scriptText, "\"sourceStatus\": ui_element_parity_audit_source_status");
        StringAssert.Contains(scriptText, "\"routeLocalOnly\": ui_element_parity_route_local_only");
        StringAssert.Contains(scriptText, "\"routeLocalRowProofs\": ui_element_route_local_row_proofs");
    }

    [TestMethod]
    public void Workflow_execution_gate_treats_missing_status_values_as_not_ready_instead_of_crashing()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def status_ok(value: Any) -> bool:");
        StringAssert.Contains(scriptText, "return normalize_token(value) in {\"pass\", \"passed\", \"ready\"}");
    }

    [TestMethod]
    public void Flagship_ui_release_gate_transactionally_publishes_digest_bound_screenshot_pack()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "screenshot_pack_transaction_path=");
        StringAssert.Contains(scriptText, "manage_screenshot_pack_transaction()");
        StringAssert.Contains(scriptText, "def rename_exchange(left: Path, right: Path) -> None:");
        StringAssert.Contains(scriptText, "newPackTreeSha256");
        StringAssert.Contains(scriptText, "atomic_write_bytes(");
        Assert.IsFalse(scriptText.Contains("os.utime(path, (proof_timestamp, proof_timestamp))", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Visual_familiarity_gate_validates_immutable_digest_bound_screenshot_snapshot()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "png_snapshot_bytes");
        StringAssert.Contains(scriptText, "screenshot_control_byte_mismatches");
        StringAssert.Contains(scriptText, "screenshot_pack_sha256");
        StringAssert.Contains(scriptText, "screenshot_snapshot_recheck");
        Assert.IsFalse(scriptText.Contains("republish_screenshot_pack_freshness_if_complete", StringComparison.Ordinal));
        Assert.IsFalse(scriptText.Contains("os.utime(path, (proof_timestamp, proof_timestamp))", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Visual_familiarity_gate_atomically_publishes_before_optional_flagship_readiness_refresh()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "flagship_product_readiness_materializer_path=\"${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}\"");
        StringAssert.Contains(scriptText, "def atomic_write_json(");
        StringAssert.Contains(scriptText, "os.replace(temporary_path, path)");
        StringAssert.Contains(scriptText, "atomic_write_json(receipt_path, payload)");
        StringAssert.Contains(scriptText, "if [[ \"$refresh_downstream_readiness\" == \"1\" && \"$skip_downstream_readiness\" != \"1\" ]]; then");
        StringAssert.Contains(scriptText, "python3 \"$flagship_product_readiness_materializer_path\" >/dev/null");
        Assert.IsFalse(scriptText.Contains("receipt_path.write_text(", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Chummer.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
