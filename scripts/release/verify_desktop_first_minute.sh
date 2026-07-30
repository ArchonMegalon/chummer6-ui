#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from datetime import datetime, timezone
from pathlib import Path

MAX_AGE = 172800
path = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published/USER_JOURNEY_TESTER_AUDIT.generated.json')
payload = json.loads(path.read_text())
status = str(payload.get('status') or '').lower()
if status not in {'pass', 'passed', 'ready'}:
    raise SystemExit(f"{path.name} status is not pass: {payload.get('status')}")
ts = payload.get('generated_at') or payload.get('generatedAt')
if not ts:
    raise SystemExit(f"{path.name} missing generated_at")
dt = datetime.fromisoformat(str(ts).replace('Z', '+00:00')).astimezone(timezone.utc)
if (datetime.now(timezone.utc) - dt).total_seconds() > MAX_AGE:
    raise SystemExit(f"{path.name} is stale for desktop gold: {ts}")
evidence = payload.get('evidence') or {}
required = [
    'master_index_search_focus_stability',
    'file_new_character_visible_workspace',
    'minimal_character_build_save_reload',
    'major_navigation_sanity',
    'validation_or_export_smoke',
]
seen = {item.get('id'): item for item in evidence.get('workflows') or []}
for workflow in required:
    if workflow not in seen:
        raise SystemExit(f"{path.name} missing workflow {workflow}")
    if str(seen[workflow].get('status') or '').lower() not in {'pass', 'passed', 'ready'}:
        raise SystemExit(f"{path.name} workflow {workflow} is not pass")
if evidence.get('used_internal_apis'):
    raise SystemExit(f"{path.name} used internal APIs")
if (payload.get('open_blocking_findings_count') or 0) != 0:
    raise SystemExit(f"{path.name} reports open blocking findings")
print('desktop gold first minute ok')
PY
