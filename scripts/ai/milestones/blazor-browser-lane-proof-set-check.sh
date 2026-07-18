#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH:-$repo_root/.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json}"

python3 "$repo_root/scripts/materialize-blazor-browser-lane-proof-set.py" \
  --output "$receipt_path" >/dev/null

python3 - <<'PY' "$receipt_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
payload = json.loads(receipt_path.read_text(encoding="utf-8"))
status = str(payload.get("status") or "").strip().lower()
if status not in {"pass", "passed", "ready"}:
    raise SystemExit(
        "Aggregate Blazor browser-lane proof-set receipt is not passing."
    )

contract_name = str(payload.get("contract_name") or "").strip()
if contract_name != "chummer6-ui.blazor_browser_lane_proof_set":
    raise SystemExit(
        "Aggregate Blazor browser-lane proof-set receipt has the wrong contract: "
        + (contract_name or "<missing>")
    )

required_receipt_count = int(payload.get("required_receipt_count") or 0)
passed_receipt_count = int(payload.get("passed_receipt_count") or -1)
if required_receipt_count <= 0:
    raise SystemExit(
        "Aggregate Blazor browser-lane proof-set receipt must report required receipts."
    )
if required_receipt_count != passed_receipt_count:
    raise SystemExit(
        "Aggregate Blazor browser-lane proof-set receipt does not have every required receipt passing."
    )

print(f"[blazor-browser-lane-proof-set] PASS: {receipt_path}")
PY
