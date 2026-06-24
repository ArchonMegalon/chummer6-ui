#!/usr/bin/env python3
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_SOURCE_STAGED_PROOF_SET_PATH",
        PUBLISHED / "BLAZOR_SOURCE_STAGED_PROOF_SET.generated.json",
    )
)

REQUIRED_RECEIPTS = [
    {
        "id": "career_support",
        "path": PUBLISHED / "BLAZOR_CAREER_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_career_support_staged_proof",
    },
    {
        "id": "identity_license",
        "path": PUBLISHED / "BLAZOR_IDENTITY_LICENSE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_identity_license_staged_proof",
    },
    {
        "id": "combat_support",
        "path": PUBLISHED / "BLAZOR_COMBAT_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_combat_support_staged_proof",
    },
    {
        "id": "skill_maintenance",
        "path": PUBLISHED / "BLAZOR_SKILL_MAINTENANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_skill_maintenance_staged_proof",
    },
    {
        "id": "magic_support",
        "path": PUBLISHED / "BLAZOR_MAGIC_SUPPORT_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_magic_support_staged_proof",
    },
    {
        "id": "gear_maintenance",
        "path": PUBLISHED / "BLAZOR_GEAR_MAINTENANCE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_gear_maintenance_staged_proof",
    },
    {
        "id": "source_gear_utility",
        "path": PUBLISHED / "BLAZOR_SOURCE_GEAR_UTILITY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_source_gear_utility_staged_proof",
    },
    {
        "id": "magic_cleanup",
        "path": PUBLISHED / "BLAZOR_MAGIC_CLEANUP_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_magic_cleanup_staged_proof",
    },
    {
        "id": "browser_output_handoff",
        "path": PUBLISHED / "BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_browser_output_handoff_staged_proof",
    },
    {
        "id": "workbench_portal_handoff",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_portal_handoff_staged_proof",
    },
    {
        "id": "workbench_polish",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_POLISH_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_polish_staged_proof",
    },
    {
        "id": "workbench_recovery",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_RECOVERY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_recovery_staged_proof",
    },
    {
        "id": "workbench_hosting_privacy",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_HOSTING_PRIVACY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_hosting_privacy_staged_proof",
    },
    {
        "id": "workbench_command_palette",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_command_palette_staged_proof",
    },
    {
        "id": "workbench_density",
        "path": PUBLISHED / "BLAZOR_WORKBENCH_DENSITY_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_workbench_density_staged_proof",
    },
    {
        "id": "legacy_control_coverage",
        "path": PUBLISHED / "BLAZOR_LEGACY_CONTROL_COVERAGE_STAGED_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_legacy_control_coverage_staged_proof",
    },
]


def load_json(path: Path) -> dict:
    if not path.is_file():
        return {}
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {"_invalid_json": True}
    return loaded if isinstance(loaded, dict) else {"_invalid_json": True}


def main() -> int:
    rows = []
    failures = []
    passed_count = 0
    expected_route_count = 0
    source_check_count = 0

    for receipt in REQUIRED_RECEIPTS:
        path = receipt["path"]
        payload = load_json(path)
        status = str(payload.get("status") or "missing").strip().lower() if payload else "missing"
        contract_name = str(payload.get("contract_name") or "").strip()
        proof_tier = str(payload.get("proof_tier") or "").strip()
        expected_contract = receipt["contract_name"]
        route_count = len(payload.get("expected_routes") or []) if isinstance(payload.get("expected_routes"), list) else 0
        checks = payload.get("checks") or []
        checks_count = len(checks) if isinstance(checks, list) else 0
        control_count = payload.get("control_count") if receipt["id"] == "legacy_control_coverage" else None
        covered_control_count = payload.get("covered_control_count") if receipt["id"] == "legacy_control_coverage" else None

        if not payload:
            failures.append(f"{receipt['id']}: missing {path}")
        elif payload.get("_invalid_json"):
            failures.append(f"{receipt['id']}: invalid JSON at {path}")
        elif contract_name != expected_contract:
            failures.append(f"{receipt['id']}: contract mismatch {contract_name or 'missing'}")
        elif status != "passed":
            failures.append(f"{receipt['id']}: status {status or 'missing'}")
        elif proof_tier != "source_staged_no_browser_execution":
            failures.append(f"{receipt['id']}: proof_tier {proof_tier or 'missing'}")
        else:
            passed_count += 1

        expected_route_count += route_count
        source_check_count += checks_count
        rows.append(
            {
                "id": receipt["id"],
                "path": str(path),
                "expected_contract_name": expected_contract,
                "contract_name": contract_name or "missing",
                "status": status or "missing",
                "proof_tier": proof_tier or "missing",
                "expected_route_count": route_count,
                "source_check_count": checks_count,
                "control_count": control_count,
                "covered_control_count": covered_control_count,
            }
        )

    payload = {
        "contract_name": "chummer6-ui.blazor_source_staged_proof_set",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "proof_tier": "source_staged_no_browser_execution",
        "route_lane": "promoted_blazor_workbench",
        "required_receipt_count": len(REQUIRED_RECEIPTS),
        "passed_receipt_count": passed_count,
        "expected_route_count": expected_route_count,
        "source_check_count": source_check_count,
        "required_receipts": rows,
        "failures": failures,
        "notes": [
            "This aggregate only summarizes source-staged receipts.",
            "It is not a hosted Playwright execution receipt and is not Docker self-host browser execution evidence.",
            "Keep this aggregate separate from BLAZOR_BROWSER_LANE_PROOF_SET.generated.json release readiness.",
        ],
    }

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if failures:
        print("\n".join(failures))
        return 1

    print(f"blazor_source_staged_proof_set:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
