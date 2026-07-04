from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
MATERIALIZER = REPO_ROOT / "scripts" / "materialize-chummer6-public-edge-flagship-proof.py"
STATUS_SCRIPT = REPO_ROOT / "scripts" / "print_blazor_public_edge_proof_status.py"
RECEIPT = PUBLISHED / "CHUMMER6_PUBLIC_EDGE_FLAGSHIP_INTEGRATION.generated.json"
COMPILE_MANIFEST = PUBLISHED / "compile.manifest.json"
DOCS_INDEX = REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md"
SIGNOFF_DOC = REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md"

REQUIRED_CHECK_IDS = {
    "release_channel_contract",
    "download_install_routes_contract",
    "public_navigation_contract",
    "blazor_runtime_contract",
    "api_session_continuity_contract",
    "pwa_mobile_role_shell_contract",
    "living_world_opt_in_contract",
    "static_asset_and_offline_boundary_contract",
    "receipt_horizon_contract",
}


def _read_json(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    assert isinstance(payload, dict)
    return payload


def test_flagship_materializer_names_live_public_edge_scope() -> None:
    source = MATERIALIZER.read_text(encoding="utf-8")

    for token in [
        "/downloads/RELEASE_CHANNEL.generated.json",
        "/downloads/releases.json",
        "/downloads/install/",
        "/participate",
        "/blazor/health",
        "/api/health",
        "/play/continuity",
        "/play/continuity/history",
        "/session",
        "/mobile/gm?role=GameMaster",
        "/mobile/pwa/ledger.json",
        "/account/ledger/notifications",
        "/blazor/service-worker.js",
        "near_term_stabilization",
        "mid_term_pwa_session_utility",
        "long_term_living_world_expansion",
        "windows_visual_proof_required_for_stable_gold",
        "authenticated_living_world_execution_not_claimed",
    ]:
        assert token in source


def test_flagship_receipt_tracks_preview_integration_without_stable_claim() -> None:
    receipt = _read_json(RECEIPT)
    checks = {item["id"]: item for item in receipt["checks"]}
    horizons = {
        item["id"]: item
        for item in checks["receipt_horizon_contract"]["facts"]["horizons"]
    }

    assert receipt["contract_name"] == "chummer6-ui.public_edge_flagship_integration"
    assert receipt["status"] == "passed"
    assert receipt["scope"] == "live-public-edge-preview-integration-not-stable-gold"
    assert set(receipt["required_check_ids"]) == REQUIRED_CHECK_IDS
    assert set(checks) == REQUIRED_CHECK_IDS
    assert receipt["promotion_boundaries"]["nightly_preview_is_not_stable"] is True
    assert receipt["promotion_boundaries"]["windows_visual_proof_required_for_stable_gold"] is True
    assert receipt["promotion_boundaries"]["authenticated_living_world_execution_not_claimed"] is True
    assert receipt["promotion_boundaries"]["full_desktop_parity_not_claimed_by_this_receipt"] is True
    assert horizons["near_term_stabilization"]["status"] == "preview_deployed"
    assert "windows_visual_proof_missing" in horizons["near_term_stabilization"]["stable_promotion_blockers"]
    assert horizons["mid_term_pwa_session_utility"]["status"] == "pwa_shell_and_opt_in_boundary_proven"
    assert horizons["long_term_living_world_expansion"]["status"] == "public_opt_in_boundary_proven_expansion_not_claimed"


def test_flagship_receipt_proves_live_release_pwa_and_living_world_boundaries() -> None:
    receipt = _read_json(RECEIPT)
    checks = {item["id"]: item for item in receipt["checks"]}
    release = checks["release_channel_contract"]["facts"]
    downloads = checks["download_install_routes_contract"]["facts"]
    pwa = checks["pwa_mobile_role_shell_contract"]["facts"]
    continuity = checks["api_session_continuity_contract"]["facts"]
    living_world = checks["living_world_opt_in_contract"]["facts"]
    static = checks["static_asset_and_offline_boundary_contract"]["facts"]

    assert release["status"] == "published"
    assert release["channel"] == release["releases_channel"]
    assert release["version"] == release["releases_version"]
    assert {"avalonia-linux-x64-installer", "avalonia-win-x64-installer"}.issubset(release["artifact_ids"])
    assert downloads["downloads_page_mentions_version"] is True
    assert all(route["status_code"] == 302 for route in downloads["install_routes"])
    assert continuity["api_health_ok"] is True
    assert continuity["api_health_service"] == "chummer.run.api"
    assert continuity["session_status_code"] == 302
    assert continuity["session_location"] == "/play"
    assert "nexus_claimed_install_posture" in continuity["history_receipt_ids"]
    assert continuity["mobile_pwa_continuity_route"] == "/play/continuity"
    assert continuity["mobile_pwa_receipt_index_route"] == "/play/continuity/history"
    assert pwa["player_manifest_start_url"] == "/mobile/player?role=Player"
    assert pwa["gm_manifest_start_url"] == "/mobile/gm?role=GameMaster"
    assert living_world["ledger_status"] == "opt_in_required"
    assert living_world["account_notifications_status_code"] == 302
    assert any(asset["path"] == "/blazor/service-worker.js" and asset["excludes_api"] for asset in static["assets"])


def test_flagship_status_summary_docs_and_compile_manifest_are_wired() -> None:
    result = subprocess.run(
        [sys.executable, str(STATUS_SCRIPT)],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )
    artifacts = set(_read_json(COMPILE_MANIFEST)["artifacts"])
    docs_index = DOCS_INDEX.read_text(encoding="utf-8")
    signoff = SIGNOFF_DOC.read_text(encoding="utf-8")

    assert "flagship_integration_status=passed" in result.stdout
    assert "flagship_integration_check_count=9" in result.stdout
    assert "api_session_continuity_contract" in result.stdout
    assert "living_world_opt_in_contract" in result.stdout
    assert RECEIPT.name in artifacts
    assert "scripts/materialize-chummer6-public-edge-flagship-proof.py" in docs_index
    assert RECEIPT.name in docs_index
    assert RECEIPT.name in signoff
    assert "without upgrading nightly preview evidence into stable/gold" in signoff
