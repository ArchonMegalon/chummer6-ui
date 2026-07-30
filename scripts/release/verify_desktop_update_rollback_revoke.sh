#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from pathlib import Path

release = json.loads(Path('/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json').read_text())
rows = (release.get('desktopTupleCoverage') or {}).get('desktopRouteTruth') or []
promoted_primary = [row for row in rows if str(row.get('routeRole') or '').lower() == 'primary' and str(row.get('promotionState') or '').lower() == 'promoted']
if not promoted_primary:
    raise SystemExit('release channel has no promoted primary desktop tuples')
for row in promoted_primary:
    tuple_id = row.get('tupleId')
    rollback_state = str(row.get('rollbackState') or '')
    install_posture = str(row.get('installPosture') or '')
    public_install_route = str(row.get('publicInstallRoute') or '')
    primary_installer_reinstall_available = (
        rollback_state == 'manual_recovery_required'
        and install_posture == 'installer_first'
        and bool(public_install_route)
    )
    if rollback_state != 'primary_reinstall_available' and not primary_installer_reinstall_available:
        raise SystemExit(
            f"{tuple_id} rollbackState is not primary_reinstall_available "
            "and does not expose a public primary-installer reinstall route"
        )
    if str(row.get('revokeState') or '') != 'not_revoked':
        raise SystemExit(f"{tuple_id} revokeState is not not_revoked")
    if not public_install_route:
        raise SystemExit(f"{tuple_id} missing publicInstallRoute")
print('desktop gold update rollback revoke ok')
PY
