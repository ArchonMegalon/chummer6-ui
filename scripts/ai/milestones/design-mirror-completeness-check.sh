#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_DESIGN_MIRROR_COMPLETENESS_RECEIPT_PATH:-$repo_root/.codex-studio/published/DESIGN_MIRROR_COMPLETENESS.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$repo_root" "$receipt_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path

repo_root = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


required_files = {
    ".codex-design/product/README.md": [
        "FLAGSHIP_PRODUCT_BAR.md",
        "CHUMMER5A_FAMILIARITY_BRIDGE.md",
        "VETERAN_FIRST_MINUTE_GATE.yaml",
        "DENSE_WORKBENCH_BUDGET.yaml",
        "FLAGSHIP_RELEASE_ACCEPTANCE.yaml",
    ],
    ".codex-design/product/FLAGSHIP_PRODUCT_BAR.md": [
        "flagship grade",
        "workbench-first startup instead of decorative landing chrome",
        "Desktop familiarity must still read as Chummer5a",
    ],
    ".codex-design/product/CHUMMER5A_FAMILIARITY_BRIDGE.md": [
        "Classic top-level menu posture",
        "Immediate toolstrip under the menu",
        "dense workbench",
    ],
    ".codex-design/product/VETERAN_FIRST_MINUTE_GATE.yaml": [
        "reach_real_workbench",
        "locate_save_import_settings",
        "locate_master_index_and_roster",
    ],
    ".codex-design/product/DENSE_WORKBENCH_BUDGET.yaml": [
        "workbench_posture",
        "require_top_menu",
        "center_pane_must_dominate",
    ],
    ".codex-design/product/FLAGSHIP_RELEASE_ACCEPTANCE.yaml": [
        "veteran_first_minute_orientation",
        "desktop_familiarity_and_install_continuity",
        "rules_explainability",
    ],
}

reasons: list[str] = []
evidence: dict[str, object] = {"paths": {}}

for relative_path, markers in required_files.items():
    path = repo_root / relative_path
    path_evidence: dict[str, object] = {"exists": path.is_file(), "markers": {}}
    evidence["paths"][relative_path] = path_evidence
    if not path.is_file():
        reasons.append(f"Missing required design mirror file: {relative_path}")
        continue
    text = path.read_text(encoding="utf-8-sig")
    for marker in markers:
        found = marker in text
        path_evidence["markers"][marker] = found
        if not found:
            reasons.append(f"Design mirror file {relative_path} is missing marker: {marker}")

payload = {
    "generatedAt": now_iso(),
    "contractName": "chummer6-ui.design_mirror_completeness",
    "status": "pass" if not reasons else "fail",
    "summary": (
        "Flagship design mirror inputs are present and carry the required product bar markers."
        if not reasons
        else "Flagship design mirror inputs are incomplete."
    ),
    "reasons": reasons,
    "evidence": evidence,
}

receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if reasons:
    raise SystemExit(73)
PY

echo "[design-mirror-completeness] PASS: flagship design mirror inputs are present."
echo "[design-mirror-completeness] evidence: $receipt_path"
