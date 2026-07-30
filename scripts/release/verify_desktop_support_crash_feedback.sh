#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from pathlib import Path

support = json.loads(Path('/docker/chummercomplete/chummer.run-services/.codex-studio/published/NEXT90_M125_HUB_PUBLIC_SIGNAL_PACKETS.generated.json').read_text())
status = str(support.get('status') or '').lower()
if status not in {'pass', 'passed', 'ready', 'published'}:
    raise SystemExit(f"support packets status is not pass: {support.get('status')}")
text = json.dumps(support).lower()
for forbidden in ['localhost', '127.0.0.1', 'host.docker.internal']:
    if forbidden in text:
        raise SystemExit(f"support packets expose internal host reference: {forbidden}")

release = json.loads(Path('/docker/chummercomplete/.codex-studio/published/PUBLIC_RELEASE_SNAPSHOT.generated.json').read_text())
support_posture = release.get('support_posture') or {}
if str(support_posture.get('status') or '').lower() not in {'pass', 'passed', 'ready', 'published'}:
    raise SystemExit(f"public release snapshot support posture is not pass: {support_posture.get('status')}")
print('desktop gold support crash feedback ok')
PY
