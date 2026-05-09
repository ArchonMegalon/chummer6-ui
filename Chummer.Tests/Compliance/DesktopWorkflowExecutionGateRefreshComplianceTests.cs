#nullable enable annotations

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Chummer.Tests.Compliance;

[TestClass]
public sealed class DesktopWorkflowExecutionGateRefreshComplianceTests
{
    [TestMethod]
    public void Workflow_execution_gate_refreshes_dependency_receipts_when_child_refresh_is_a_semantic_no_op()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "refresh_receipt_generated_at_if_unchanged");
        StringAssert.Contains(scriptText, "dependencyRefreshGeneratedAt");
        StringAssert.Contains(scriptText, "if [[ \"$dependency_exit_code\" -eq 0 && \"$before_generated_at\" == \"$after_generated_at\" && \"$before_mtime\" == \"$after_mtime\" ]]");
        StringAssert.Contains(scriptText, "refresh_receipt_generated_at_if_unchanged \"$dependency_receipt_target\"");
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
        StringAssert.Contains(scriptText, "env \"${flagship_refresh_env[@]}\" bash \"$dependency_script\"");
    }

    [TestMethod]
    public void Workflow_execution_gate_defers_stale_m141_refresh_failures_once_direct_flagship_proof_is_current()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "\"next90_m141_direct_import_route_proof\", next90_m141_direct_import_route_proof");
        StringAssert.Contains(scriptText, "next90_m141_direct_import_route_proof dependency refresh failed via ");
        StringAssert.Contains(
            scriptText,
            "\"next90_m141_direct_import_route_proof dependency refresh failed via \",",
            "The workflow gate must defer stale M141 dependency-refresh failures once the direct flagship slice proof already closes the route-local desktop workflow bar.");
    }

    [TestMethod]
    public void Workflow_execution_gate_treats_route_local_only_flagship_release_failures_as_effectively_passing()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-workflow-execution-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "def flagship_gate_is_route_local_only(payload: Dict[str, Any]) -> bool:");
        StringAssert.Contains(scriptText, "evidence[\"ui_flagship_release_gate_route_local_only\"] = flagship_gate_route_local_only");
        StringAssert.Contains(scriptText, "evidence[\"ui_flagship_release_gate_effective_status\"] = (");
        StringAssert.Contains(
            scriptText,
            "\"Top-level release gate cannot pass while flagship readiness coverage.desktop_client is not ready.\",",
            "The workflow gate must only defer the known route-local flagship recursion findings, not unrelated flagship failures.");
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
    public void Flagship_ui_release_gate_republishes_screenshot_pack_with_gate_run_freshness()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "b14-flagship-ui-release-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "proof_timestamp = datetime.now(timezone.utc).timestamp()");
        StringAssert.Contains(scriptText, "for path in list(target_dir.glob(\"*.png\")) + [target_dir / control_evidence_path.name]:");
        StringAssert.Contains(scriptText, "os.utime(path, (proof_timestamp, proof_timestamp))");
        StringAssert.Contains(scriptText, "cp \"$staged_screenshot_dir\"/*.png \"$screenshot_dir\"/");
        StringAssert.Contains(scriptText, "python3 - <<'PY' \"$screenshot_dir\"");
        StringAssert.Contains(scriptText, "for path in list(screenshot_dir.glob(\"*.png\")) + [screenshot_dir / \"SCREENSHOT_CONTROL_EVIDENCE.generated.json\"]:");
        StringAssert.Contains(
            scriptText,
            "The published proof pack must reflect when this gate ran, even if a test copied",
            "The flagship UI gate must keep screenshot freshness anchored to the proof run, not preserved fixture mtimes.");
    }

    [TestMethod]
    public void Visual_familiarity_gate_republishes_promoted_screenshot_pack_with_current_proof_freshness()
    {
        string repoRoot = FindRepoRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "ai", "milestones", "materialize-desktop-visual-familiarity-exit-gate.sh");
        string scriptText = File.ReadAllText(scriptPath);

        StringAssert.Contains(scriptText, "republish_screenshot_pack_freshness_if_complete()");
        StringAssert.Contains(scriptText, "control_evidence_path = target / \"SCREENSHOT_CONTROL_EVIDENCE.generated.json\"");
        StringAssert.Contains(scriptText, "for path in list(target.glob(\"*.png\")) + [control_evidence_path]:");
        StringAssert.Contains(scriptText, "os.utime(path, (proof_timestamp, proof_timestamp))");
        StringAssert.Contains(
            scriptText,
            "republish_screenshot_pack_freshness_if_complete \"$screenshot_dir\"",
            "The visual familiarity materializer must republish a complete promoted screenshot pack with current proof freshness before evaluating stale screenshot failures.");
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
