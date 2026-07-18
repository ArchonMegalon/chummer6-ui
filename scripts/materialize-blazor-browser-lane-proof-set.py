#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Sequence


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
OUTPUT_PATH = PUBLISHED / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json"
CONTRACT_NAME = "chummer6-ui.blazor_browser_lane_proof_set"

REQUIRED_RECEIPTS = [
    {
        "id": "self_host_workbench",
        "path": PUBLISHED / "BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_self_host_workbench_proof",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "runtime_required": True,
            "route_probe_executed": True,
        },
        "minimum_lengths": {
            "proof_routes": 16,
            "workflow_proofs": 20,
        },
    },
    {
        "id": "hosted_route_entry",
        "path": PUBLISHED / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_public_edge_workbench_proof",
        "allowed_statuses": {"passed", "ready"},
        "required_fields": {
            "runtime_required": True,
            "route_probe_executed": True,
        },
        "minimum_lengths": {
            "proof_routes": 10,
            "route_proof_markers": 10,
            "workflow_proofs": 7,
        },
        "required_list_items": {
            "proof_routes": [
                "/app",
                "/app?command=character_roster",
                "/blazor/",
                "/blazor/app",
                "/blazor/workbench",
            ],
            "route_proof_markers": [
                "public_chummer_app_route",
                "public_chummer_app_roster_route",
                "public_blazor_root_redirect",
                "public_blazor_home_roster_entry",
            ],
        },
    },
    {
        "id": "hosted_execution",
        "path": PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_public_edge_execution_proof",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "proof_tier": "hosted_promoted_route_execution",
            "route_lane": "promoted_blazor_workbench",
            "promoted_route_base": "/blazor/workbench",
        },
        "allowed_fields": {
            "playwright_scope": {"smoke", "full"},
        },
        "minimum_lengths": {
            "workflow_families": 9,
        },
        "required_object_ids_from_field": {
            "workflow_families": {
                "source_field": "required_workflow_family_ids",
                "id_field": "id",
                "minimum_source_items": 9,
            },
        },
    },
    {
        "id": "hosted_pwa_play_shell",
        "path": PUBLISHED / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json",
        "contract_name": "chummer6-ui.blazor_pwa_public_edge_proof",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "proof_tier": "hosted_pwa_public_edge_execution",
            "route_lane": "blazor_pwa_play_shell",
        },
        "minimum_lengths": {
            "checks": 13,
        },
        "required_check_ids": [
            "manifest_install_contract",
            "service_worker_static_privacy_contract",
            "offline_living_world_boundary",
            "app_head_and_registration",
            "clean_public_entry_route_contract",
            "player_pwa_alias_route_contract",
            "mobile_player_shell_route_contract",
            "player_manifest_install_contract",
            "player_manifest_route_targets_contract",
            "mobile_pwa_living_world_boundary",
            "account_ledger_notifications_opt_in_boundary",
            "static_asset_fetch_contract",
            "mobile_viewport_shell_contract",
        ],
    },
    {
        "id": "analytics_posture",
        "path": PUBLISHED / "BLAZOR_ANALYTICS_POSTURE.generated.json",
        "contract_name": "chummer6-ui.blazor_analytics_posture",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "self_host_default": "analytics-disabled",
            "hosted_public_edge": "rybbit-enabled-when-site-id-configured",
            "sensitive_data_policy": "route-and-workflow-metadata-only",
            "session_replay_policy": "disabled",
            "autocapture_policy": "disabled",
        },
        "minimum_lengths": {
            "checks": 8,
        },
    },
    {
        "id": "connected_runtime_posture",
        "path": PUBLISHED / "BLAZOR_CONNECTED_RUNTIME_POSTURE.generated.json",
        "contract_name": "chummer6-ui.blazor_connected_runtime_posture",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "owner_context_boundary": "signed-portal-owner-header-when-shared-key-configured",
            "scope": "posture-and-forwarding-boundary-not-full-runtime-parity",
        },
        "minimum_lengths": {
            "checks": 8,
            "connected_runtime_routes": 3,
        },
    },
    {
        "id": "external_host_blockers",
        "path": PUBLISHED / "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json",
        "contract_name": None,
        "allowed_statuses": {"ready"},
        "required_fields": {
            "browser_route_entry_proof_shape": "expanded",
            "browser_execution_proof_status": "passed",
        },
        "minimum_lengths": {},
    },
    {
        "id": "source_staged_release_boundary",
        "path": PUBLISHED / "BLAZOR_SOURCE_STAGED_RELEASE_BOUNDARY.generated.json",
        "contract_name": "chummer6-ui.blazor_source_staged_release_boundary",
        "allowed_statuses": {"passed"},
        "required_fields": {
            "proof_tier": "source_policy_no_browser_execution",
            "scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation",
        },
        "minimum_lengths": {
            "documentation_sources": 6,
            "status_reporting_sources": 1,
        },
    },
]

EXAMPLE_RECEIPT_PATH = REPO_ROOT / "docs" / "examples" / "blazor-browser-lane-proof-set.receipt.example.json"
EXAMPLE_RECEIPT_TOKENS = [
    '"contract_name": "chummer6-ui.blazor_browser_lane_proof_set"',
    '"scope": "aggregate-browser-lane-proof-set-not-full-desktop-parity"',
    '"id": "hosted_route_entry"',
    '"minimum": 10',
    '"id": "required_list_items:route_proof_markers"',
    '"public_chummer_app_roster_route"',
    '"id": "required_list_items:proof_routes"',
    '"/app?command=character_roster"',
    '"id": "hosted_execution"',
    '"id": "required_object_ids:workflow_families"',
    '"promoted_startup_command_executions"',
    '"promoted_dense_tool_surfaces"',
    '"promoted_origin_rules_continuity"',
    '"promoted_build_lab_continuity"',
    '"promoted_resumed_workspace"',
    '"promoted_result_continuations"',
    '"promoted_action_continuations"',
    '"promoted_committed_actions"',
    '"promoted_advanced_action_executions"',
    '"id": "analytics_posture"',
    '"field:session_replay_policy"',
    '"field:autocapture_policy"',
    '"id": "hosted_pwa_play_shell"',
    '"contract_name": "chummer6-ui.blazor_pwa_public_edge_proof"',
    '"field:proof_tier"',
    '"hosted_pwa_public_edge_execution"',
    '"field:route_lane"',
    '"blazor_pwa_play_shell"',
    '"id": "required_check_ids"',
    '"manifest_install_contract"',
    '"service_worker_static_privacy_contract"',
    '"offline_living_world_boundary"',
    '"app_head_and_registration"',
    '"clean_public_entry_route_contract"',
    '"player_pwa_alias_route_contract"',
    '"mobile_player_shell_route_contract"',
    '"player_manifest_install_contract"',
    '"mobile_pwa_living_world_boundary"',
    '"static_asset_fetch_contract"',
    '"mobile_viewport_shell_contract"',
    '"id": "source_staged_release_boundary"',
    '"contract_name": "chummer6-ui.blazor_source_staged_release_boundary"',
    '"scope": "staged_and_source_plan_receipts_must_not_enter_release_readiness_aggregation"',
    'MIG-106 through MIG-109',
    '"The source_staged_release_boundary receipt is required as source-policy evidence only; it does not execute hosted or Docker browser workflows."',
    '"MIG-106 through MIG-109 remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim."',
]


def load_json(path: Path) -> tuple[dict[str, Any], str | None]:
    if not path.is_file():
        return {}, f"missing receipt: {path}"

    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        return {}, f"invalid JSON in {path}: {exc}"

    if not isinstance(loaded, dict):
        return {}, f"receipt root must be an object: {path}"

    return loaded, None


def normalize_status(payload: dict[str, Any]) -> str:
    return str(payload.get("status") or "").strip().lower()


def evaluate_receipt(spec: dict[str, Any]) -> dict[str, Any]:
    path = spec["path"]
    payload, load_error = load_json(path)
    checks: list[dict[str, Any]] = []

    if load_error is not None:
        checks.append({"id": "load_receipt", "passed": False, "detail": load_error})
        return {
            "id": spec["id"],
            "path": str(path),
            "status": "missing",
            "passed": False,
            "checks": checks,
        }

    expected_contract = spec.get("contract_name")
    if expected_contract:
        actual_contract = str(payload.get("contract_name") or "").strip()
        checks.append(
            {
                "id": "contract_name",
                "passed": actual_contract == expected_contract,
                "expected": expected_contract,
                "actual": actual_contract,
            }
        )

    status = normalize_status(payload)
    allowed_statuses = spec["allowed_statuses"]
    checks.append(
        {
            "id": "status",
            "passed": status in allowed_statuses,
            "expected": sorted(allowed_statuses),
            "actual": status or "missing",
        }
    )

    for field, expected in spec.get("required_fields", {}).items():
        actual = payload.get(field)
        checks.append(
            {
                "id": f"field:{field}",
                "passed": actual == expected,
                "expected": expected,
                "actual": actual,
            }
        )

    for field, allowed_values in spec.get("allowed_fields", {}).items():
        actual = payload.get(field)
        checks.append(
            {
                "id": f"allowed_field:{field}",
                "passed": actual in allowed_values,
                "expected": sorted(allowed_values),
                "actual": actual,
            }
        )

    for field, minimum in spec.get("minimum_lengths", {}).items():
        actual = payload.get(field)
        actual_length = len(actual) if isinstance(actual, list) else -1
        checks.append(
            {
                "id": f"minimum_length:{field}",
                "passed": actual_length >= minimum,
                "minimum": minimum,
                "actual": actual_length,
            }
        )

    for field, expected_items in spec.get("required_list_items", {}).items():
        actual = payload.get(field)
        actual_items = {str(item).strip() for item in actual} if isinstance(actual, list) else set()
        missing_items = [
            item for item in expected_items if str(item).strip() not in actual_items
        ]
        checks.append(
            {
                "id": f"required_list_items:{field}",
                "passed": not missing_items,
                "expected": expected_items,
                "missing": missing_items,
            }
        )

    for field, expected_items in spec.get("required_object_ids", {}).items():
        actual = payload.get(field)
        actual_items = {
            str(item.get("id") or "").strip()
            for item in actual
            if isinstance(item, dict) and str(item.get("id") or "").strip()
        } if isinstance(actual, list) else set()
        missing_items = [
            item for item in expected_items if str(item).strip() not in actual_items
        ]
        checks.append(
            {
                "id": f"required_object_ids:{field}",
                "passed": not missing_items,
                "expected": expected_items,
                "missing": missing_items,
            }
        )

    for field, config in spec.get("required_object_ids_from_field", {}).items():
        actual = payload.get(field)
        actual_items = {
            str(item.get(config["id_field"]) or "").strip()
            for item in actual
            if isinstance(item, dict) and str(item.get(config["id_field"]) or "").strip()
        } if isinstance(actual, list) else set()
        expected_items = [
            str(item).strip()
            for item in payload.get(config["source_field"], [])
            if str(item).strip()
        ]
        missing_items = [
            item for item in expected_items if item not in actual_items
        ]
        minimum_source_items = int(config.get("minimum_source_items", 1))
        checks.append(
            {
                "id": f"required_object_ids_from_field:{field}:{config['source_field']}",
                "passed": len(expected_items) >= minimum_source_items and not missing_items,
                "expected": expected_items,
                "minimum_source_items": minimum_source_items,
                "missing": missing_items,
            }
        )

    required_check_ids = spec.get("required_check_ids") or []
    if required_check_ids:
        actual_checks = payload.get("checks")
        actual_check_ids = {
            str(item.get("id") or "").strip()
            for item in actual_checks
            if isinstance(item, dict) and str(item.get("id") or "").strip()
        } if isinstance(actual_checks, list) else set()
        missing_check_ids = [
            check_id for check_id in required_check_ids if check_id not in actual_check_ids
        ]
        checks.append(
            {
                "id": "required_check_ids",
                "passed": not missing_check_ids,
                "expected": required_check_ids,
                "missing": missing_check_ids,
            }
        )

    return {
        "id": spec["id"],
        "path": str(path),
        "status": status or "missing",
        "passed": all(bool(check["passed"]) for check in checks),
        "checks": checks,
    }


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize the aggregate Blazor browser-lane proof set."
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(
            os.environ.get(
                "CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH",
                str(OUTPUT_PATH),
            )
        ),
    )
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = ()) -> int:
    args = parse_args(argv)
    receipt_results = [evaluate_receipt(spec) for spec in REQUIRED_RECEIPTS]
    try:
        example_text = EXAMPLE_RECEIPT_PATH.read_text(encoding="utf-8")
        missing_example_tokens = [
            token for token in EXAMPLE_RECEIPT_TOKENS if token not in example_text
        ]
    except FileNotFoundError:
        missing_example_tokens = [f"missing example receipt: {EXAMPLE_RECEIPT_PATH}"]

    source_checks = [
        {
        "id": "example_receipt_shape",
        "path": str(EXAMPLE_RECEIPT_PATH),
        "status": "passed" if not missing_example_tokens else "failed",
        "passed": not missing_example_tokens,
        "missing_tokens": missing_example_tokens,
        }
    ]
    status = "passed" if all(bool(result["passed"]) for result in receipt_results + source_checks) else "failed"
    payload = {
        "contract_name": CONTRACT_NAME,
        "status": status,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "required_receipt_count": len(REQUIRED_RECEIPTS),
        "passed_receipt_count": sum(1 for result in receipt_results if bool(result["passed"])),
        "source_check_count": len(source_checks),
        "passed_source_check_count": sum(1 for result in source_checks if bool(result["passed"])),
        "receipts": receipt_results,
        "source_checks": source_checks,
        "scope": "aggregate-browser-lane-proof-set-not-full-desktop-parity",
        "notes": [
            "The source_staged_release_boundary receipt is required as source-policy evidence only; it does not execute hosted or Docker browser workflows.",
            "Hosted execution breadth follows the receipt playwright_scope; smoke receipts must cover every smoke-required family, and full receipts must cover every full-required family before this aggregate accepts them.",
            "MIG-106 through MIG-109 remain open until refreshed hosted route-entry, hosted execution, Docker self-host, analytics posture, connected-runtime, source-boundary, and aggregate browser-lane receipts prove the Chummer Online release claim.",
        ],
    }

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    if status != "passed":
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 1

    print(f"wrote {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(None))
