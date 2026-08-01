from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]


def test_b14_refreshes_frontier_subgates_for_the_current_release_channel() -> None:
    script = (
        REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh"
    ).read_text(encoding="utf-8")

    assert "CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=0" in script
    assert "CHUMMER_SR4_SR6_FRONTIER_SKIP_SUBGATE_REFRESH=1" not in script
    assert 'CHUMMER_DESKTOP_WORKFLOW_RELEASE_CHANNEL_PATH="$release_channel_path"' in script

    dual_head_run = script.index(
        'run_with_retry 3 "cross-head workflow parity tests" run_dual_head_acceptance_tests'
    )
    runtime_stop = script.index("stop_local_api_runtime", dual_head_run)
    frontier_run = script.index(
        'echo "[b14] running explicit SR4/SR6 desktop parity frontier gate..."'
    )
    assert dual_head_run < runtime_stop < frontier_run
