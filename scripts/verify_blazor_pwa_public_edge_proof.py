#!/usr/bin/env python3
from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
RECEIPT_PATH = REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json"
EXPECTED_CONTRACT = "chummer6-ui.blazor_pwa_public_edge_proof"
EXPECTED_PROOF_TIER = "hosted_pwa_public_edge_execution"
EXPECTED_ROUTE_LANE = "blazor_pwa_play_shell"
REQUIRED_CHECK_IDS = {
    "manifest_install_contract",
    "service_worker_static_privacy_contract",
    "offline_living_world_boundary",
    "app_head_and_registration",
    "clean_public_entry_route_contract",
    "static_asset_fetch_contract",
    "mobile_viewport_shell_contract",
}


def main() -> int:
    if not RECEIPT_PATH.is_file():
        print(f"missing receipt: {RECEIPT_PATH}")
        return 1

    payload = json.loads(RECEIPT_PATH.read_text(encoding="utf-8-sig"))
    reasons: list[str] = []
    contract = str(payload.get("contract_name") or "")
    status = str(payload.get("status") or "").lower()
    proof_tier = str(payload.get("proof_tier") or "")
    route_lane = str(payload.get("route_lane") or "")
    base_url = str(payload.get("base_url") or "")
    public_entry_url = str(payload.get("public_entry_url") or "")
    checks = payload.get("checks")

    if contract != EXPECTED_CONTRACT:
        reasons.append(f"contract mismatch: expected {EXPECTED_CONTRACT!r}, got {contract!r}")
    if status != "passed":
        reasons.append(f"status must be 'passed', got {status!r}")
    if proof_tier != EXPECTED_PROOF_TIER:
        reasons.append(f"proof_tier mismatch: expected {EXPECTED_PROOF_TIER!r}, got {proof_tier!r}")
    if route_lane != EXPECTED_ROUTE_LANE:
        reasons.append(f"route_lane mismatch: expected {EXPECTED_ROUTE_LANE!r}, got {route_lane!r}")
    if not base_url.startswith("https://"):
        reasons.append(f"base_url must be https, got {base_url!r}")
    if not public_entry_url.startswith("https://"):
        reasons.append(f"public_entry_url must be https, got {public_entry_url!r}")
    if not isinstance(checks, list):
        reasons.append("checks must be a list")
        checks = []

    check_ids = set()
    for index, check in enumerate(checks, start=1):
        if not isinstance(check, dict):
            reasons.append(f"check #{index} must be an object")
            continue
        check_id = str(check.get("id") or "")
        check_status = str(check.get("status") or "").lower()
        assertion = str(check.get("assertion") or "")
        url = str(check.get("url") or "")
        check_ids.add(check_id)
        if check_status != "passed":
            reasons.append(f"check {check_id!r} must be passed, got {check_status!r}")
        if not assertion:
            reasons.append(f"check {check_id!r} missing assertion")
        if (
            url != base_url
            and not url.startswith(f"{base_url}/")
            and url != public_entry_url
            and not url.startswith(f"{public_entry_url}?")
        ):
            reasons.append(
                f"check {check_id!r} url must stay under {base_url!r} or match public entry "
                f"{public_entry_url!r}, got {url!r}"
            )

    missing = REQUIRED_CHECK_IDS - check_ids
    extra = check_ids - REQUIRED_CHECK_IDS
    if missing:
        reasons.append(f"missing required checks: {sorted(missing)!r}")
    if extra:
        reasons.append(f"unexpected checks: {sorted(extra)!r}")

    if reasons:
        print("blazor_pwa_public_edge_proof:failed")
        for reason in reasons:
            print(f"- {reason}")
        return 1

    print(f"blazor_pwa_public_edge_proof:ok {RECEIPT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
