from __future__ import annotations

import json
import os
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "materialize-blazor-play-surface-horizon.py"


def _write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def test_play_surface_horizon_materializer_summarizes_runtime_and_staged_boundaries(tmp_path: Path) -> None:
    browser_lane = tmp_path / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
    execution_horizon = tmp_path / "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json"
    pwa = tmp_path / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json"
    touch = tmp_path / "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json"
    campaign = tmp_path / "BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF.generated.json"
    table = tmp_path / "BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.generated.json"
    workflow = tmp_path / "BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json"
    progression = tmp_path / "BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF.generated.json"
    output = tmp_path / "BLAZOR_PLAY_SURFACE_HORIZON.generated.json"

    _write_json(browser_lane, {"contract_name": "test.browser_lane", "status": "passed"})
    _write_json(
        execution_horizon,
        {
            "contract_name": "test.execution_horizon",
            "status": "passed",
            "current_published_execution": {
                "playwright_scope": "smoke",
                "base_url": "https://chummer.run",
                "promoted_route_base": "/blazor/workbench",
            },
            "horizons": [
                {"id": "near_term_hosted_smoke_execution", "status": "proven"},
                {"id": "mid_term_full_live_public_edge_execution_matrix", "status": "not_proven"},
                {"id": "long_term_full_browser_desktop_parity_breadth", "status": "not_claimed"},
            ],
        },
    )
    for receipt_path, contract_name in [
        (pwa, "test.pwa"),
        (touch, "test.touch"),
        (campaign, "test.campaign"),
        (table, "test.table"),
        (workflow, "test.workflow"),
        (progression, "test.progression"),
    ]:
        _write_json(receipt_path, {"contract_name": contract_name, "status": "passed"})

    env = os.environ.copy()
    env.update(
        {
            "CHUMMER_BLAZOR_PLAY_SURFACE_HORIZON_PATH": str(output),
            "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH": str(browser_lane),
            "CHUMMER_BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON_PATH": str(execution_horizon),
            "CHUMMER_BLAZOR_PWA_PUBLIC_EDGE_PROOF_PATH": str(pwa),
            "CHUMMER_BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF_PATH": str(touch),
            "CHUMMER_BLAZOR_WORKBENCH_CAMPAIGN_SESSION_STAGED_PROOF_PATH": str(campaign),
            "CHUMMER_BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF_PATH": str(table),
            "CHUMMER_BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF_PATH": str(workflow),
            "CHUMMER_BLAZOR_WORKBENCH_PROGRESSION_LEDGER_STAGED_PROOF_PATH": str(progression),
        }
    )

    result = subprocess.run(
        ["python3", str(SCRIPT)],
        cwd=REPO_ROOT,
        env=env,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    payload = json.loads(output.read_text(encoding="utf-8"))

    assert payload["contract_name"] == "chummer6-ui.blazor_play_surface_horizon"
    assert payload["status"] == "passed"
    assert payload["current_release_truth"]["current_execution_scope"] == "smoke"
    assert payload["current_release_truth"]["public_entry_route"] == "/app"
    assert payload["current_release_truth"]["public_roster_entry_route"] == "/app?command=character_roster"
    assert payload["current_release_truth"]["public_blazor_root_route"] == "/blazor/"
    assert payload["current_release_truth"]["hosted_app_route"] == "/blazor/app"
    assert payload["current_release_truth"]["compatibility_route_base"] == "/blazor/workbench"
    assert payload["current_release_truth"]["execution_route_base"] == "/blazor/workbench"

    horizons = {item["id"]: item for item in payload["horizons"]}
    assert horizons["near_term_stabilization"]["status"] == "proven"
    assert horizons["mid_term_pwa_session_utility"]["status"] == "mixed"
    assert horizons["long_term_living_world_expansion"]["status"] == "staged"
    assert horizons["mid_term_pwa_session_utility"]["source_staged_receipts"][0]["public_download_relative_path"].startswith(
        "release-evidence/browser-lane/"
    )
    assert "mobile browser execution parity" in horizons["mid_term_pwa_session_utility"]["unproven_claims"]
    assert "session state" in horizons["mid_term_pwa_session_utility"]["server_bound_boundaries"]
    assert "live Black Ledger mutation" in horizons["long_term_living_world_expansion"]["unproven_claims"]


def test_release_manifest_script_syncs_play_surface_browser_lane_evidence() -> None:
    generator = (REPO_ROOT / "scripts" / "generate-releases-manifest.sh").read_text(encoding="utf-8")

    assert "sync_browser_lane_release_evidence_dir()" in generator
    assert 'target_dir="$target_root/release-evidence/browser-lane"' in generator
    assert "BLAZOR_PLAY_SURFACE_HORIZON.generated.json" in generator
    assert "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json" in generator
    assert "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json" in generator
    assert "BLAZOR_WORKBENCH_TOUCH_MOBILE_STAGED_PROOF.generated.json" in generator
    assert 'sync_browser_lane_release_evidence_dir "$PORTAL_DOWNLOADS_DIR" "local portal"' in generator
    assert 'sync_browser_lane_release_evidence_dir "$(dirname "$CANONICAL_MANIFEST_PATH")" "canonical release"' in generator


def test_play_surface_horizon_wrapper_verify_gate_and_docs_stay_wired() -> None:
    browser_wrapper = (REPO_ROOT / "scripts" / "ai" / "milestones" / "blazor-browser-lane-proof-set-check.sh").read_text(encoding="utf-8")
    wrapper = (REPO_ROOT / "scripts" / "ai" / "milestones" / "blazor-play-surface-horizon-check.sh").read_text(encoding="utf-8")
    verify = (REPO_ROOT / "scripts" / "ai" / "verify.sh").read_text(encoding="utf-8")
    b14 = (REPO_ROOT / "scripts" / "ai" / "milestones" / "b14-flagship-ui-release-gate.sh").read_text(encoding="utf-8")
    docs_index = (REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md").read_text(encoding="utf-8")
    signoff = (REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md").read_text(encoding="utf-8")

    assert "materialize-blazor-browser-lane-proof-set.py" in browser_wrapper
    assert "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json" in browser_wrapper
    assert "chummer6-ui.blazor_browser_lane_proof_set" in browser_wrapper
    assert "required_receipt_count" in browser_wrapper
    assert "passed_receipt_count" in browser_wrapper

    assert "materialize-blazor-play-surface-horizon.py" in wrapper
    assert "BLAZOR_PLAY_SURFACE_HORIZON.generated.json" in wrapper
    assert "near_term_stabilization" in wrapper
    assert "mid_term_pwa_session_utility" in wrapper
    assert "long_term_living_world_expansion" in wrapper
    assert "pwa_public_edge_status" in wrapper

    assert "blazor-browser-lane-proof-set-check.sh" in verify
    assert "blazor-play-surface-horizon-check.sh" in verify

    assert "browser_lane_proof_set_receipt_path" in b14
    assert "play_surface_horizon_receipt_path" in b14
    assert "blazor-browser-lane-proof-set-check.sh" in b14
    assert "blazor-play-surface-horizon-check.sh" in b14
    assert "playSurfaceHorizonProof" in b14
    assert "playSurfaceHorizonReceiptChecks" in b14

    assert "blazor-play-surface-horizon-check.sh" in docs_index
    assert "blazor-play-surface-horizon-check.sh" in signoff
    assert "BLAZOR_PLAY_SURFACE_HORIZON.generated.json" in signoff
