#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

artifacts_path="${CHUMMER5A_UI_PARITY_TESTER_ARTIFACTS_PATH:-$repo_root/.codex-studio/out/chummer5a-parity-tester/live}"
receipt_path="${CHUMMER5A_UI_PARITY_TESTER_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_UI_PARITY_TESTER.generated.json}"
mkdir -p "$(dirname "$receipt_path")"

if [[ "${CHUMMER5A_UI_PARITY_TESTER_RUN_USER_JOURNEY_AUDIT:-1}" == "1" ]]; then
  bash "$repo_root/scripts/ai/milestones/user-journey-tester-audit.sh" >/dev/null || true
fi

set +e
python3 "$repo_root/scripts/chummer5a_parity_tester.py" --artifacts "$artifacts_path" "$@"
tester_exit_code=$?
set -e

python3 - <<'PY' "$artifacts_path" "$receipt_path" "$tester_exit_code"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path


artifacts_path = Path(sys.argv[1])
receipt_path = Path(sys.argv[2])
tester_exit_code = int(sys.argv[3])
metadata_path = artifacts_path / "run-metadata.json"
failures_path = artifacts_path / "failures.json"


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


metadata = {}
failures = []
if metadata_path.is_file():
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
if failures_path.is_file():
    failures = (json.loads(failures_path.read_text(encoding="utf-8")) or {}).get("failures") or []

status = "pass" if tester_exit_code == 0 else ("fail" if tester_exit_code == 1 else "infrastructure-failure")
summary = metadata.get("summary") or (
    "Parity gate passed for the selected first-slice Chummer5a fixture set."
    if status == "pass"
    else "Chummer5a parity tester did not pass."
)
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.chummer5a_ui_parity_tester",
    "status": status,
    "summary": summary,
    "exitCode": tester_exit_code,
    "artifactRoot": str(artifacts_path),
    "runMetadataPath": str(metadata_path),
    "failuresPath": str(failures_path),
    "failureCount": len(failures),
    "selectedFixtures": metadata.get("selectedFixtures") or [],
    "workflowFamilyCount": metadata.get("workflowFamilyCount"),
    "proofScope": metadata.get("proofScope") or {},
    "proofClaims": metadata.get("proofClaims") or [],
    "proofLimitations": metadata.get("proofLimitations") or [],
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY

exit "$tester_exit_code"
