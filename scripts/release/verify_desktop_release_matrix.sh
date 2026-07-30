#!/usr/bin/env bash
set -euo pipefail
repo_root="/docker/chummercomplete/chummer-presentation"
public_downloads_root="/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads"
hub_proof_public_base="${CHUMMER_PUBLIC_BASE_URL:-https://chummer.run}"
cd "$repo_root"
python3 "$repo_root/scripts/verify_desktop_artifact_size_budget.py" --check-only >/dev/null
(
  cd /docker/chummercomplete/chummer.run-services
  python3 scripts/materialize_hub_local_release_proof.py \
    .codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json \
    "$hub_proof_public_base" \
    docker-compose.yml \
    120 \
    true
) >/dev/null
python3 /docker/chummercomplete/chummer.run-services/scripts/verify_desktop_native_trust_receipts.py >/dev/null
dotnet build Chummer.Tests/Chummer.Tests.csproj --no-restore -v minimal -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:RunDesktopReleaseMatrixTestsOnly=true >/dev/null
dotnet Chummer.Tests/bin/Debug/net10.0/Chummer.Tests.dll \
  --filter "Name~CheckAndScheduleStartupUpdateAsync_bootstrap_installer_handoff_stages_payload_and_sidecar|Name~BuildInstallerBootstrapPayloadArtifact_requires_payload_metadata|Name~StageInstallerBootstrapPayloadIfNeededAsync_downloads_payload_and_writes_sidecar" \
  >/dev/null
payload_gate_args=(
  --files-dir "$public_downloads_root/files"
  --manifest "$public_downloads_root/releases.json"
  --require-embedded-bootstrap-metadata
)
if python3 - "$public_downloads_root/releases.json" <<'PY' >/dev/null
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
payload = json.loads(path.read_text(encoding="utf-8-sig"))
downloads = payload.get("downloads") or []
for item in downloads:
    if not isinstance(item, dict):
        continue
    if str(item.get("fileName") or "").strip() == "chummer-avalonia-win-x64-installer.exe":
        raise SystemExit(0)
raise SystemExit(1)
PY
then
  payload_gate_args+=(--require-manifest-row)
else
  payload_gate_args+=(--allow-empty)
fi
python3 /docker/chummercomplete/chummer-presentation/scripts/verify-windows-installer-payloads.py \
  "${payload_gate_args[@]}" >/dev/null
python3 - <<'PY'
import json
from pathlib import Path
path = Path('/docker/chummercomplete/chummer-presentation/.codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json')
payload = json.loads(path.read_text())
if str(payload.get('status')).lower() not in {'pass','passed','ready'}:
    summary = str(payload.get('summary') or '').strip()
    windows_review = payload.get('reviews', {}).get('windowsPlatformReview', {})
    windows_status = windows_review.get('status')
    blocking_mode = str(payload.get('blockingMode') or payload.get('blocking_mode') or '').strip()
    blocked_by_external_constraints_only = bool(
        payload.get('blockedByExternalConstraintsOnly')
        if 'blockedByExternalConstraintsOnly' in payload
        else payload.get('blocked_by_external_constraints_only')
    )
    external_count = payload.get('externalBlockingFindingsCount')
    local_count = payload.get('localBlockingFindingsCount')
    external_blockers = payload.get('evidence', {}).get('external_blockers') or []
    blockers = [
        str(item.get('blocker') or '').strip()
        for item in external_blockers
        if isinstance(item, dict) and str(item.get('blocker') or '').strip()
    ]
    external_only = (
        blocking_mode == 'external_only'
        and blocked_by_external_constraints_only
        and isinstance(external_count, int)
        and external_count > 0
        and isinstance(local_count, int)
        and local_count == 0
    )
    if external_only:
        detail_parts = ['desktop release matrix ok: local installer/update gates passed; only external host proof is pending']
        if summary:
            detail_parts.append(f"summary: {summary}")
        if blockers:
            detail_parts.append("external blockers: " + ", ".join(blockers))
        print("; ".join(detail_parts))
        raise SystemExit(0)
    detail_parts = [f"desktop exit gate status is not pass: {payload.get('status')}"]
    if summary:
        detail_parts.append(f"summary: {summary}")
    if windows_status:
        detail_parts.append(f"windows review: {windows_status}")
    if blocking_mode:
        detail_parts.append(f"blocking mode: {blocking_mode}")
    if isinstance(external_count, int):
        detail_parts.append(f"external findings: {external_count}")
    if isinstance(local_count, int):
        detail_parts.append(f"local findings: {local_count}")
    if blockers:
        detail_parts.append("external blockers: " + ", ".join(blockers))
    raise SystemExit("; ".join(detail_parts))
print('desktop release matrix ok')
PY
