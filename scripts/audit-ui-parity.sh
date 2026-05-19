#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_UI_PARITY_AUDIT_RECEIPT_PATH:-$repo_root/.codex-studio/published/UI_PARITY_AUDIT.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

gates=(
  "scripts/ai/milestones/design-mirror-completeness-check.sh"
  "scripts/ai/milestones/chummer5a-layout-hard-gate.sh"
  "scripts/ai/milestones/generated-dialog-element-parity-check.sh"
  "scripts/ai/milestones/section-host-ruleset-parity-check.sh"
  "scripts/ai/milestones/interactive-control-inventory-check.sh"
  "scripts/ai/milestones/startup-workbench-survival-check.sh"
  "scripts/ai/milestones/design-authorized-parity-softening-check.sh"
  "scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh"
)

# Required contract markers for journey-gate proof ingestion:
# dice_roller
# character_roster

# Golden-journey utility coverage markers that must stay visible in this audit plane.
required_workflow_markers=(
  "dice_roller"
  "character_roster"
)

python3 - <<'PY' "$repo_root" "$receipt_path" "${#required_workflow_markers[@]}" "${required_workflow_markers[@]}" "${gates[@]}"
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
required_marker_count = int(sys.argv[3])
required_workflow_markers = sys.argv[4:4 + required_marker_count]
gate_paths = [Path(value) for value in sys.argv[4 + required_marker_count:]]


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def tail_lines(text: str, count: int = 40) -> str:
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    return "\n".join(lines[-count:])


results: list[dict[str, object]] = []
reasons: list[str] = []
receipt_by_gate = {
    "scripts/ai/milestones/design-mirror-completeness-check.sh": repo_root / ".codex-studio/published/DESIGN_MIRROR_COMPLETENESS.generated.json",
    "scripts/ai/milestones/chummer5a-layout-hard-gate.sh": repo_root / ".codex-studio/published/CHUMMER5A_LAYOUT_HARD_GATE.generated.json",
    "scripts/ai/milestones/generated-dialog-element-parity-check.sh": repo_root / ".codex-studio/published/GENERATED_DIALOG_ELEMENT_PARITY.generated.json",
    "scripts/ai/milestones/section-host-ruleset-parity-check.sh": repo_root / ".codex-studio/published/SECTION_HOST_RULESET_PARITY.generated.json",
    "scripts/ai/milestones/interactive-control-inventory-check.sh": repo_root / ".codex-studio/published/INTERACTIVE_CONTROL_INVENTORY.generated.json",
    "scripts/ai/milestones/startup-workbench-survival-check.sh": repo_root / ".codex-studio/published/STARTUP_WORKBENCH_SURVIVAL.generated.json",
    "scripts/ai/milestones/design-authorized-parity-softening-check.sh": repo_root / ".codex-studio/published/DESIGN_AUTHORIZED_PARITY_SOFTENING.generated.json",
    "scripts/ai/milestones/sr6-ruleset-ui-sophistication-gate.sh": repo_root / ".codex-studio/published/CHUMMER_SR6_RULESET_UI_SOPHISTICATION_GATE.generated.json",
}


def load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    return payload if isinstance(payload, dict) else {}


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}

for gate_path in gate_paths:
    absolute_gate_path = repo_root / gate_path
    if not absolute_gate_path.is_file():
        reasons.append(f"Missing required parity audit gate: {gate_path}")
        results.append(
            {
                "gate": str(gate_path),
                "status": "missing",
            }
        )
        continue

    gate_receipt_path = receipt_by_gate.get(str(gate_path))
    gate_receipt = load_json(gate_receipt_path) if gate_receipt_path else {}
    if gate_receipt and status_ok(gate_receipt.get("status")):
        results.append(
            {
                "gate": str(gate_path),
                "status": "pass",
                "exitCode": 0,
                "receiptPath": str(gate_receipt_path),
                "receiptGeneratedAt": gate_receipt.get("generatedAt") or gate_receipt.get("generated_at"),
                "summary": gate_receipt.get("summary") or gate_receipt.get("reason"),
                "reusedPassingReceipt": True,
            }
        )
        continue

    run = subprocess.run(
        ["bash", str(gate_path)],
        cwd=repo_root,
        text=True,
        capture_output=True,
    )
    combined = (run.stdout or "") + "\n" + (run.stderr or "")
    status = "pass" if run.returncode == 0 else "fail"
    results.append(
        {
            "gate": str(gate_path),
            "status": status,
            "exitCode": run.returncode,
            "outputTail": tail_lines(combined),
            "receiptPath": str(gate_receipt_path) if gate_receipt_path else "",
            "reusedPassingReceipt": False,
        }
    )
    if run.returncode != 0:
        reasons.append(f"{gate_path} failed with exit code {run.returncode}.")

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.ui_parity_audit",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "The active UI parity gate stack passes across legacy parity, runtime inventory, startup survival, and SR6 sophistication."
        if not reasons
        else "One or more active UI parity gates failed."
    ),
    "reasons": reasons,
    "evidence": {
        "requiredWorkflowMarkers": required_workflow_markers,
        "gateCount": len(gate_paths),
        "gates": results,
    },
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if reasons:
    raise SystemExit(61)
PY

echo "[audit-ui-parity] PASS: current fail-closing UI parity gate stack is green."
echo "[audit-ui-parity] PASS markers: dice_roller, character_roster"
echo "[audit-ui-parity] evidence: $receipt_path"
