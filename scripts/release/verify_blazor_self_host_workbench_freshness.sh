#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from datetime import datetime, timezone
from pathlib import Path

MAX_AGE = 172800
path = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json')
payload = json.loads(path.read_text(encoding='utf-8-sig'))
status = str(payload.get('status') or '').lower()
if status not in {'pass', 'passed', 'ready'}:
    raise SystemExit(f"BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json status is not pass: {payload.get('status')}")

contract_name = str(payload.get('contract_name') or '').strip()
if contract_name != 'chummer6-ui.blazor_self_host_workbench_proof':
    raise SystemExit(f"BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json contract mismatch: {contract_name}")

playwright_scope = str(payload.get('playwright_scope') or '').strip().lower()
if playwright_scope not in {'smoke', 'full'}:
    raise SystemExit(f"BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json missing supported playwright_scope: {payload.get('playwright_scope')}")

raw_ts = payload.get('generated_at') or payload.get('generatedAt')
if not raw_ts:
    raise SystemExit('BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json missing generated_at')

generated_at = datetime.fromisoformat(str(raw_ts).replace('Z', '+00:00')).astimezone(timezone.utc)
now = datetime.now(timezone.utc)
if (now - generated_at).total_seconds() > MAX_AGE:
    raise SystemExit(f"BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json is stale for blazor gold: {raw_ts}")

print('blazor self-host workbench proof freshness ok')
PY
