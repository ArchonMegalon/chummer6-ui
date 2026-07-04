from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
MATERIALIZER_PATH = REPO_ROOT / "scripts" / "materialize-blazor-browser-lane-proof-set.py"
STATUS_SCRIPT = REPO_ROOT / "scripts" / "print_blazor_public_edge_proof_status.py"
DOCS_INDEX = REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md"
SIGNOFF_DOC = REPO_ROOT / "docs" / "WORKBENCH_RELEASE_SIGNOFF.md"
COMPILE_MANIFEST = PUBLISHED / "compile.manifest.json"
AGGREGATE_RECEIPT = PUBLISHED / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"

PWA_RECEIPT_NAME = "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json"
PWA_CONTRACT = "chummer6-ui.blazor_pwa_public_edge_proof"
PWA_REQUIRED_CHECKS = {
    "manifest_install_contract",
    "service_worker_static_privacy_contract",
    "offline_living_world_boundary",
    "app_head_and_registration",
    "clean_public_entry_route_contract",
    "player_pwa_alias_route_contract",
    "mobile_player_shell_route_contract",
    "player_manifest_install_contract",
    "mobile_pwa_living_world_boundary",
    "static_asset_fetch_contract",
    "mobile_viewport_shell_contract",
}


def _load_browser_lane_materializer():
    spec = importlib.util.spec_from_file_location("blazor_browser_lane_proof_set", MATERIALIZER_PATH)
    assert spec is not None
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


def _read_json(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    assert isinstance(payload, dict)
    return payload


def test_browser_lane_materializer_requires_hosted_pwa_public_edge_receipt() -> None:
    module = _load_browser_lane_materializer()
    pwa_spec = next(
        spec for spec in module.REQUIRED_RECEIPTS if spec["id"] == "hosted_pwa_play_shell"
    )

    assert pwa_spec["path"].name == PWA_RECEIPT_NAME
    assert pwa_spec["contract_name"] == PWA_CONTRACT
    assert pwa_spec["required_fields"] == {
        "proof_tier": "hosted_pwa_public_edge_execution",
        "route_lane": "blazor_pwa_play_shell",
    }
    assert pwa_spec["minimum_lengths"] == {"checks": 11}
    assert set(pwa_spec["required_check_ids"]) == PWA_REQUIRED_CHECKS

    result = module.evaluate_receipt(pwa_spec)
    assert result["passed"], result


def test_browser_lane_materializer_keeps_hosted_execution_scope_aware() -> None:
    module = _load_browser_lane_materializer()
    hosted_execution_spec = next(
        spec for spec in module.REQUIRED_RECEIPTS if spec["id"] == "hosted_execution"
    )

    assert hosted_execution_spec["allowed_fields"]["playwright_scope"] == {"smoke", "full"}
    assert hosted_execution_spec["minimum_lengths"] == {"workflow_families": 9}
    assert hosted_execution_spec["required_object_ids_from_field"]["workflow_families"] == {
        "source_field": "required_workflow_family_ids",
        "id_field": "id",
        "minimum_source_items": 9,
    }

    result = module.evaluate_receipt(hosted_execution_spec)
    assert result["passed"], result


def test_aggregate_receipt_includes_hosted_pwa_public_edge_result() -> None:
    payload = _read_json(AGGREGATE_RECEIPT)
    receipt_by_id = {
        receipt.get("id"): receipt
        for receipt in payload.get("receipts", [])
        if isinstance(receipt, dict)
    }

    assert payload["required_receipt_count"] >= 8
    assert "hosted_pwa_play_shell" in receipt_by_id
    pwa_result = receipt_by_id["hosted_pwa_play_shell"]
    assert pwa_result["passed"] is True
    assert pwa_result["status"] == "passed"
    check_ids = {
        check.get("id")
        for check in pwa_result.get("checks", [])
        if isinstance(check, dict)
    }
    assert "required_check_ids" in check_ids


def test_compile_manifest_indexes_pwa_and_aggregate_receipts() -> None:
    artifacts = set(_read_json(COMPILE_MANIFEST)["artifacts"])

    assert PWA_RECEIPT_NAME in artifacts
    assert "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json" in artifacts


def test_docs_name_hosted_pwa_public_edge_proof_boundary() -> None:
    docs_index = DOCS_INDEX.read_text(encoding="utf-8")
    signoff = SIGNOFF_DOC.read_text(encoding="utf-8")

    assert PWA_RECEIPT_NAME in docs_index
    assert "scripts/materialize-blazor-pwa-public-edge-proof.py" in docs_index
    assert "scripts/verify_blazor_pwa_public_edge_proof.py" in docs_index
    assert "clean `/app` entry, `/pwa` player companion alias, `/mobile` player shell, player manifest, mobile living-world opt-in boundary, static deployed assets, mobile viewport" in docs_index
    assert PWA_RECEIPT_NAME in signoff
    assert "not app-store acceptance or offline runner-data parity" in signoff
    assert "clean `/app` entry, `/pwa` player companion alias, `/mobile` player shell, player manifest, mobile living-world opt-in boundary, static deployed assets, mobile viewport" in signoff


def test_status_summary_reports_player_pwa_alias_and_living_world_checks() -> None:
    result = subprocess.run(
        [sys.executable, str(STATUS_SCRIPT)],
        cwd=REPO_ROOT,
        check=True,
        capture_output=True,
        text=True,
    )

    assert "pwa_public_edge_pwa_alias_url=https://chummer.run/pwa" in result.stdout
    assert "player_pwa_alias_route_contract" in result.stdout
    assert "mobile_player_shell_route_contract" in result.stdout
    assert "player_manifest_install_contract" in result.stdout
    assert "mobile_pwa_living_world_boundary" in result.stdout
