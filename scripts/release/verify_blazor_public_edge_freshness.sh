#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from datetime import datetime, timezone
from pathlib import Path

MAX_AGE = 172800
root = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published')
receipts = {
    'BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json': root / 'BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json',
    'BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json': root / 'BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json',
}
allowed_statuses = {'pass', 'passed', 'ready'}
now = datetime.now(timezone.utc)

for label, path in receipts.items():
    payload = json.loads(path.read_text(encoding='utf-8-sig'))
    status = str(payload.get('status') or '').lower()
    if status not in allowed_statuses:
        raise SystemExit(f"{label} status is not pass: {payload.get('status')}")
    raw_ts = payload.get('generated_at') or payload.get('generatedAt')
    if not raw_ts:
        raise SystemExit(f"{label} missing generated_at")
    generated_at = datetime.fromisoformat(str(raw_ts).replace('Z', '+00:00')).astimezone(timezone.utc)
    if (now - generated_at).total_seconds() > MAX_AGE:
        raise SystemExit(f"{label} is stale for blazor gold: {raw_ts}")

print('blazor public-edge proof freshness ok')
PY
