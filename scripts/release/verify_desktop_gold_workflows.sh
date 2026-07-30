#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from datetime import datetime, timezone
from pathlib import Path

MAX_AGE = 172800
root = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published')
proofs = {
    'workflow': root / 'DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json',
    'journey': root / 'USER_JOURNEY_TESTER_AUDIT.generated.json',
    'import': root / 'NEXT90_M141_UI_DIRECT_IMPORT_ROUTE_PROOF.generated.json',
}

def parse_ts(raw):
    if not raw:
        return None
    return datetime.fromisoformat(str(raw).replace('Z', '+00:00')).astimezone(timezone.utc)

now = datetime.now(timezone.utc)
for label, path in proofs.items():
    payload = json.loads(path.read_text())
    status = str(payload.get('status') or '').lower()
    if status not in {'pass', 'passed', 'ready'}:
        raise SystemExit(f"{path.name} status is not pass: {payload.get('status')}")
    ts = payload.get('generated_at') or payload.get('generatedAt')
    dt = parse_ts(ts)
    if dt is None:
        raise SystemExit(f"{path.name} missing generated_at")
    if (now - dt).total_seconds() > MAX_AGE:
        raise SystemExit(f"{path.name} is stale for desktop gold: {ts}")

journey = json.loads((root / 'USER_JOURNEY_TESTER_AUDIT.generated.json').read_text())
evidence = journey.get('evidence') or {}
required_workflows = [
    'master_index_search_focus_stability',
    'file_new_character_visible_workspace',
    'minimal_character_build_save_reload',
    'major_navigation_sanity',
    'validation_or_export_smoke',
]
actual = set(evidence.get('required_workflows') or [])
missing = [item for item in required_workflows if item not in actual]
if missing:
    raise SystemExit(f"user journey audit is missing required workflows: {missing}")
if evidence.get('used_internal_apis'):
    raise SystemExit('user journey audit used internal APIs')
nonpassing = evidence.get('nonpassing_workflows') or []
if nonpassing:
    raise SystemExit(f"user journey audit has nonpassing workflows: {nonpassing}")
print('desktop gold workflows ok')
PY
