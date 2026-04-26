#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH:-$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_AUDIT.generated.json}"
trace_path="${CHUMMER_USER_JOURNEY_TESTER_TRACE_PATH:-$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_TRACE.generated.json}"
linux_gate_path="${CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH:-$repo_root/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json}"
screenshot_dir="${CHUMMER_USER_JOURNEY_TESTER_SCREENSHOT_DIR:-$repo_root/.codex-studio/published/user-journey-tester-screenshots}"

if [[ "${CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE:-0}" == "1" ]]; then
  bash scripts/materialize-linux-desktop-exit-gate.sh >/dev/null
fi

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$trace_path" "$linux_gate_path" "$screenshot_dir" "$repo_root"
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

receipt_path = Path(sys.argv[1])
trace_path = Path(sys.argv[2])
linux_gate_path = Path(sys.argv[3])
screenshot_dir = Path(sys.argv[4])
repo_root = Path(sys.argv[5])

CONTRACT_NAME = "chummer6-ui.user_journey_tester_audit"
TRACE_CONTRACT_NAME = "chummer6-ui.user_journey_tester_trace"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"

REQUIRED_WORKFLOWS = [
    "master_index_search_focus_stability",
    "file_new_character_visible_workspace",
    "minimal_character_build_save_reload",
    "major_navigation_sanity",
    "validation_or_export_smoke",
]

REQUIRED_WORKFLOW_ASSERTIONS = {
    "master_index_search_focus_stability": [
        "focus_preserved_after_typing",
        "search_text_accumulates_keyboard_input",
    ],
    "file_new_character_visible_workspace": [
        "new_character_action_opened_visible_workspace",
        "visible_workspace_nonblank",
    ],
    "minimal_character_build_save_reload": [
        "character_created_saved_reloaded",
        "reload_preserved_character_identity",
    ],
    "major_navigation_sanity": [
        "primary_navigation_clicks_change_visible_content",
        "no_unhandled_errors",
    ],
    "validation_or_export_smoke": [
        "validation_or_export_action_completed",
        "result_visible_or_file_created",
    ],
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def bool_value(payload: dict[str, Any], key: str) -> Any:
    if key in payload:
        return payload.get(key)
    evidence = payload.get("evidence")
    if isinstance(evidence, dict):
        return evidence.get(key)
    return None


def string_value(payload: dict[str, Any], key: str) -> str:
    value = payload.get(key)
    if value is None:
        evidence = payload.get("evidence")
        if isinstance(evidence, dict):
            value = evidence.get(key)
    return str(value or "").strip()


def dict_rows(value: Any) -> list[dict[str, Any]]:
    if isinstance(value, list):
        return [row for row in value if isinstance(row, dict)]
    return []


def workflow_id(row: dict[str, Any]) -> str:
    return str(row.get("id") or row.get("workflow_id") or row.get("workflowId") or row.get("name") or "").strip()


def row_screenshots(row: dict[str, Any]) -> list[str]:
    value = row.get("screenshots") or row.get("screenshot_paths") or row.get("screenshotPaths") or []
    if not isinstance(value, list):
        return []
    return [str(item or "").strip() for item in value if str(item or "").strip()]


def screenshot_path(value: str) -> Path:
    candidate = Path(value)
    if candidate.is_absolute():
        return candidate
    return screenshot_dir / value


def path_within_repo(path: Path) -> bool:
    try:
        path.resolve().relative_to(repo_root.resolve())
        return True
    except Exception:
        return False


def screenshot_review(values: list[str]) -> tuple[list[dict[str, Any]], list[str]]:
    rows: list[dict[str, Any]] = []
    reasons: list[str] = []
    seen_hashes: set[str] = set()
    for value in values:
        path = screenshot_path(value)
        row: dict[str, Any] = {
            "path": str(path),
            "exists": path.is_file(),
            "within_repo_root": path_within_repo(path),
            "is_png": False,
            "sha256": "",
        }
        if not path.is_file():
            reasons.append(f"screenshot is missing: {path}")
            rows.append(row)
            continue
        data = path.read_bytes()
        digest = hashlib.sha256(data).hexdigest()
        row["is_png"] = data.startswith(PNG_SIGNATURE)
        row["sha256"] = digest
        row["size_bytes"] = len(data)
        if not row["within_repo_root"]:
            reasons.append(f"screenshot is outside repo root: {path}")
        if not row["is_png"]:
            reasons.append(f"screenshot is not a PNG: {path}")
        if digest in seen_hashes:
            reasons.append(f"screenshot is duplicated by content: {path}")
        seen_hashes.add(digest)
        rows.append(row)
    return rows, reasons


def linux_binary_target_ok(trace: dict[str, Any]) -> bool:
    if bool_value(trace, "linux_binary_under_test") is True:
        return True
    if bool_value(trace, "actual_binary_under_test") is True:
        target = " ".join([string_value(trace, "binary_under_test"), string_value(trace, "run_target")]).lower()
        return "linux" in target or not target.strip()
    target = " ".join([string_value(trace, "binary_under_test"), string_value(trace, "run_target")]).lower()
    return "linux" in target and any(token in target for token in ("binary", "executable", "bin", "appimage"))


def trace_workflows(trace: dict[str, Any]) -> list[dict[str, Any]]:
    evidence = trace.get("evidence")
    if isinstance(evidence, dict):
        rows = dict_rows(evidence.get("workflows"))
        if rows:
            return rows
    return dict_rows(trace.get("workflows"))


trace = load_json(trace_path)
linux_gate = load_json(linux_gate_path)
reasons: list[str] = []

if not trace:
    reasons.append(f"user journey tester trace is missing: {trace_path}")
if trace and str(trace.get("contract_name") or "").strip() != TRACE_CONTRACT_NAME:
    reasons.append(f"user journey tester trace contract_name must be {TRACE_CONTRACT_NAME}.")
if trace and not status_ok(trace.get("status")):
    reasons.append("user journey tester trace status is not pass/passed/ready.")

if not linux_gate:
    reasons.append(f"Linux desktop exit gate is missing: {linux_gate_path}")
if linux_gate and not status_ok(linux_gate.get("status")):
    reasons.append("Linux desktop exit gate is not passing.")

tester_shard_id = string_value(trace, "tester_shard_id")
fix_shard_id = string_value(trace, "fix_shard_id")
fix_shard_separate = bool(tester_shard_id and fix_shard_id and tester_shard_id != fix_shard_id)
if not fix_shard_separate:
    reasons.append("tester_shard_id and fix_shard_id must both be present and different.")

used_internal_apis = bool_value(trace, "used_internal_apis")
if used_internal_apis is not False:
    reasons.append("tester trace must declare used_internal_apis=false.")

linux_binary_under_test = linux_binary_target_ok(trace)
if not linux_binary_under_test:
    reasons.append("tester trace must prove it exercised the Linux desktop binary, not only in-process APIs.")

blocking_findings = trace.get("open_blocking_findings")
if blocking_findings is None:
    evidence = trace.get("evidence")
    if isinstance(evidence, dict):
        blocking_findings = evidence.get("open_blocking_findings")
if not isinstance(blocking_findings, list):
    blocking_findings = []
open_blocking_findings_count = len([item for item in blocking_findings if str(item or "").strip()])
if open_blocking_findings_count:
    reasons.append("tester trace has open blocking findings.")

workflow_rows = trace_workflows(trace)
workflow_by_id = {workflow_id(row): row for row in workflow_rows if workflow_id(row)}
workflow_reviews: list[dict[str, Any]] = []
missing_workflows: list[str] = []
nonpassing_workflows: list[str] = []
insufficient_screenshot_workflows: list[str] = []
missing_assertion_workflows: dict[str, list[str]] = {}

for required_id in REQUIRED_WORKFLOWS:
    row = workflow_by_id.get(required_id)
    if row is None:
        missing_workflows.append(required_id)
        workflow_reviews.append(
            {
                "id": required_id,
                "status": "missing",
                "screenshots": [],
                "screenshotReview": [],
                "missingAssertions": REQUIRED_WORKFLOW_ASSERTIONS[required_id],
            }
        )
        continue

    status = str(row.get("status") or row.get("result") or row.get("state") or "").strip().lower()
    if not status_ok(status):
        nonpassing_workflows.append(required_id)

    screenshots = row_screenshots(row)
    if len(screenshots) < 2:
        insufficient_screenshot_workflows.append(required_id)

    screenshot_rows, screenshot_reasons = screenshot_review(screenshots)
    reasons.extend([f"{required_id}: {reason}" for reason in screenshot_reasons])

    assertions = row.get("assertions")
    if not isinstance(assertions, dict):
        assertions = {}
    missing_assertions = [
        assertion
        for assertion in REQUIRED_WORKFLOW_ASSERTIONS[required_id]
        if assertions.get(assertion) is not True
    ]
    if missing_assertions:
        missing_assertion_workflows[required_id] = missing_assertions

    workflow_reviews.append(
        {
            "id": required_id,
            "status": status,
            "screenshots": screenshots,
            "screenshotReview": screenshot_rows,
            "assertions": {key: bool(assertions.get(key) is True) for key in REQUIRED_WORKFLOW_ASSERTIONS[required_id]},
            "missingAssertions": missing_assertions,
        }
    )

if missing_workflows:
    reasons.append("tester trace is missing required workflow(s): " + ", ".join(sorted(missing_workflows)))
if nonpassing_workflows:
    reasons.append("tester trace has nonpassing workflow(s): " + ", ".join(sorted(nonpassing_workflows)))
if insufficient_screenshot_workflows:
    reasons.append(
        "tester trace has fewer than two screenshots for workflow(s): "
        + ", ".join(sorted(insufficient_screenshot_workflows))
    )
if missing_assertion_workflows:
    reasons.append(
        "tester trace is missing required user-observable assertion(s): "
        + "; ".join(
            f"{workflow}: {', '.join(assertions)}"
            for workflow, assertions in sorted(missing_assertion_workflows.items())
        )
    )

status = "pass" if not reasons else "fail"
generated_at = now_iso()
payload: dict[str, Any] = {
    "contract_name": CONTRACT_NAME,
    "status": status,
    "generated_at": generated_at,
    "generatedAt": generated_at,
    "reasons": reasons,
    "open_blocking_findings_count": open_blocking_findings_count,
    "linux_binary_under_test": linux_binary_under_test,
    "used_internal_apis": used_internal_apis,
    "fix_shard_separate": fix_shard_separate,
    "evidence": {
        "trace_path": str(trace_path),
        "linux_gate_path": str(linux_gate_path),
        "screenshot_dir": str(screenshot_dir),
        "linux_gate_status": str(linux_gate.get("status") or "").strip(),
        "tester_shard_id": tester_shard_id,
        "fix_shard_id": fix_shard_id,
        "required_workflows": REQUIRED_WORKFLOWS,
        "required_workflow_assertions": REQUIRED_WORKFLOW_ASSERTIONS,
        "workflows": workflow_reviews,
        "missing_workflows": sorted(missing_workflows),
        "nonpassing_workflows": sorted(nonpassing_workflows),
        "insufficient_screenshot_workflows": sorted(insufficient_screenshot_workflows),
        "missing_assertion_workflows": missing_assertion_workflows,
        "open_blocking_findings_count": open_blocking_findings_count,
        "used_internal_apis": used_internal_apis,
        "fix_shard_separate": fix_shard_separate,
        "linux_binary_under_test": linux_binary_under_test,
        "run_linux_gate_requested": os.environ.get("CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE", "0") == "1",
    },
}

receipt_path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")

if status != "pass":
    raise SystemExit("[USER-JOURNEY-TESTER] FAIL: " + "; ".join(reasons))
print("[USER-JOURNEY-TESTER] PASS: adversarial Linux user-journey audit passed.")
PY
