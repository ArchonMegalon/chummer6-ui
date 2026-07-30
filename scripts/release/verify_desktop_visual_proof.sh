#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
import struct
from datetime import datetime, timezone
from pathlib import Path

MAX_AGE = 172800
MIN_BYTES = 10000
MIN_W = 200
MIN_H = 120
root = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published')
gate = json.loads((root / 'DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json').read_text())
status = str(gate.get('status') or '').lower()
if status not in {'pass', 'passed', 'ready'}:
    raise SystemExit(f"DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json status is not pass: {gate.get('status')}")
ts = gate.get('generated_at') or gate.get('generatedAt')
if not ts:
    raise SystemExit('DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json missing generated_at')
dt = datetime.fromisoformat(str(ts).replace('Z', '+00:00')).astimezone(timezone.utc)
if (datetime.now(timezone.utc) - dt).total_seconds() > MAX_AGE:
    raise SystemExit(f"DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json is stale for desktop gold: {ts}")

required = [
    root / 'ui-flagship-release-gate-screenshots' / '01-initial-shell-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '04-loaded-runner-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '05-dense-section-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '06-dense-section-dark.png',
    root / 'ui-flagship-release-gate-screenshots' / '16-master-index-dialog-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '17-character-roster-dialog-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '19-workflow-file-menu-loaded-light.png',
    root / 'ui-flagship-release-gate-screenshots' / '36-workflow-new-character-dialog-light.png',
    root / 'user-journey-tester-screenshots' / 'validation_or_export_smoke-before.png',
    root / 'user-journey-tester-screenshots' / 'validation_or_export_smoke-after.png',
]

def png_dims(path: Path):
    data = path.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise SystemExit(f"{path} is not a valid PNG signature")
    if data[12:16] != b'IHDR':
        raise SystemExit(f"{path} is missing PNG IHDR")
    width, height = struct.unpack('>II', data[16:24])
    return width, height

for path in required:
    if not path.is_file():
        raise SystemExit(f"required desktop screenshot missing: {path}")
    size = path.stat().st_size
    if size < MIN_BYTES:
        raise SystemExit(f"desktop screenshot is too small to count as visual proof: {path} ({size} bytes)")
    width, height = png_dims(path)
    if width < MIN_W or height < MIN_H:
        raise SystemExit(f"desktop screenshot dimensions are too small: {path} ({width}x{height})")

print('desktop gold visual proof ok')
PY
