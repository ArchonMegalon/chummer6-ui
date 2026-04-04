#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
HUB_REGISTRY_ROOT="${CHUMMER_HUB_REGISTRY_ROOT:-$("$REPO_ROOT/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
CANONICAL_RELEASE_CHANNEL_PATH="${HUB_REGISTRY_ROOT:+$HUB_REGISTRY_ROOT/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
DEFAULT_RELEASE_CHANNEL_PATH="$REPO_ROOT/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$CANONICAL_RELEASE_CHANNEL_PATH" && -f "$CANONICAL_RELEASE_CHANNEL_PATH" ]]; then
  RELEASE_CHANNEL_PATH_DEFAULT="$CANONICAL_RELEASE_CHANNEL_PATH"
else
  RELEASE_CHANNEL_PATH_DEFAULT="$DEFAULT_RELEASE_CHANNEL_PATH"
fi

PROOF_PATH="${CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json}"
RELEASE_CHANNEL_PATH="${CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH:-$RELEASE_CHANNEL_PATH_DEFAULT}"
APP_KEY_OVERRIDE="${CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY:-}"
RID_OVERRIDE="${CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID:-}"
if [[ -z "$APP_KEY_OVERRIDE" || -z "$RID_OVERRIDE" ]]; then
  mapfile -t RELEASE_PROMOTED_TUPLE < <(python3 - "$RELEASE_CHANNEL_PATH" "$APP_KEY_OVERRIDE" "$RID_OVERRIDE" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
app_key_override = sys.argv[2].strip().lower()
rid_override = sys.argv[3].strip().lower()

def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def artifact_rid(artifact: dict) -> str:
    rid = normalize(artifact.get("rid"))
    if rid:
        return rid
    arch = normalize(artifact.get("arch"))
    if arch in {"x64", "arm64"}:
        return f"win-{arch}"
    return ""


if not release_channel_path.is_file():
    raise SystemExit(0)

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = [
    item for item in (payload.get("artifacts") or [])
    if isinstance(item, dict)
    and normalize(item.get("platform")) == "windows"
    and normalize(item.get("kind")) in {"installer", "msix"}
    and normalize(item.get("head"))
    and artifact_rid(item)
]

if app_key_override:
    artifacts = [item for item in artifacts if normalize(item.get("head")) == app_key_override]
if rid_override:
    artifacts = [item for item in artifacts if artifact_rid(item) == rid_override]
if not artifacts:
    raise SystemExit(0)

preferred_order = ["win-x64", "win-arm64"]
ranked = sorted(
    artifacts,
    key=lambda artifact: (
        preferred_order.index(artifact_rid(artifact)) if artifact_rid(artifact) in preferred_order else len(preferred_order),
        0 if normalize(artifact.get("head")) == "avalonia" else 1,
        normalize(artifact.get("head")),
        artifact_rid(artifact),
    ),
)
chosen = ranked[0]
print(normalize(chosen.get("head")))
print(artifact_rid(chosen))
PY
)
fi
APP_KEY="${APP_KEY_OVERRIDE:-${RELEASE_PROMOTED_TUPLE[0]:-avalonia}}"
RID="${RID_OVERRIDE:-${RELEASE_PROMOTED_TUPLE[1]:-win-x64}}"
WINDOWS_INSTALLER_PATH="${CHUMMER_WINDOWS_INSTALLER_PATH:-}"
WINDOWS_LOCAL_DESKTOP_FILES_ROOT="${CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT:-$REPO_ROOT/Docker/Downloads/files}"
UI_LOCAL_RELEASE_PROOF_PATH="${CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json}"
UI_FLAGSHIP_RELEASE_GATE_PATH="${CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
UI_WORKFLOW_PARITY_PATH="${CHUMMER_UI_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json}"
SR4_WORKFLOW_PARITY_PATH="${CHUMMER_SR4_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json}"
SR6_WORKFLOW_PARITY_PATH="${CHUMMER_SR6_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json}"

mkdir -p "$(dirname "$PROOF_PATH")"

python3 - "$PROOF_PATH" "$RELEASE_CHANNEL_PATH" "$WINDOWS_INSTALLER_PATH" "$WINDOWS_LOCAL_DESKTOP_FILES_ROOT" "$UI_LOCAL_RELEASE_PROOF_PATH" "$UI_FLAGSHIP_RELEASE_GATE_PATH" "$UI_WORKFLOW_PARITY_PATH" "$SR4_WORKFLOW_PARITY_PATH" "$SR6_WORKFLOW_PARITY_PATH" "$REPO_ROOT" "$HUB_REGISTRY_ROOT" "$APP_KEY" "$RID" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import platform
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List

PASSING_STARTUP_SMOKE_STATUSES = {"pass", "passed", "ready"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "86400"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def read_status(path: Path, expected_contract: str | None = None) -> str:
    payload = load_json(path)
    if expected_contract:
        contract = str(payload.get("contract_name") or payload.get("contractName") or "").strip()
        if contract != expected_contract:
            return ""
    return str(payload.get("status") or "").strip().lower()


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def expected_host_class_platform_token(platform: str) -> str:
    normalized = normalize_token(platform)
    if normalized == "windows":
        return "win"
    if normalized == "macos":
        return "osx"
    if normalized == "linux":
        return "linux"
    return normalized


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize_token(host_class)
    expected_token = expected_host_class_platform_token(platform)
    if not normalized_host or not expected_token:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def artifact_rid(artifact: Dict[str, Any]) -> str:
    rid = normalize_token(artifact.get("rid"))
    if rid:
        return rid
    arch = normalize_token(artifact.get("arch"))
    if arch in {"x64", "arm64"}:
        return f"win-{arch}"
    return ""


def parse_iso_utc(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def path_is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def path_uses_legacy_chummer5a_root(path: Path) -> bool:
    normalized = str(path.resolve()).replace("\\", "/").lower()
    return "/chummer5a/" in normalized


proof_path = Path(sys.argv[1])
release_channel_path = Path(sys.argv[2])
installer_path = Path(sys.argv[3])
windows_installer_path_override = Path(sys.argv[3]).expanduser() if str(sys.argv[3]).strip() else None
windows_local_desktop_files_root = Path(sys.argv[4])
ui_local_release_proof_path = Path(sys.argv[5])
ui_flagship_release_gate_path = Path(sys.argv[6])
ui_workflow_parity_path = Path(sys.argv[7])
sr4_workflow_parity_path = Path(sys.argv[8])
sr6_workflow_parity_path = Path(sys.argv[9])
repo_root = Path(sys.argv[10])
hub_registry_root_arg = str(sys.argv[11] or "").strip()
hub_registry_root = Path(hub_registry_root_arg).resolve() if hub_registry_root_arg else None
expected_head_override = normalize_token(sys.argv[12])
expected_rid_override = normalize_token(sys.argv[13])
host_os_name = platform.system().strip()
host_os_normalized = normalize_token(host_os_name)
host_supports_windows_smoke = bool(os.name == "nt" or shutil.which("cygpath"))

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "windows_installer_path_override": str(windows_installer_path_override) if windows_installer_path_override else "",
    "windows_local_desktop_files_root": str(windows_local_desktop_files_root),
    "ui_local_release_proof_path": str(ui_local_release_proof_path),
    "ui_flagship_release_gate_path": str(ui_flagship_release_gate_path),
    "ui_workflow_parity_path": str(ui_workflow_parity_path),
    "sr4_workflow_parity_path": str(sr4_workflow_parity_path),
    "sr6_workflow_parity_path": str(sr6_workflow_parity_path),
    "host_operating_system": host_os_name,
    "host_operating_system_normalized": host_os_normalized,
    "host_supports_windows_startup_smoke": host_supports_windows_smoke,
}

release_channel = load_json(release_channel_path)
release_channel_status = str(release_channel.get("status") or "").strip().lower()
release_channel_id = normalize_token(release_channel.get("channelId") or release_channel.get("channel"))
release_channel_version = str(release_channel.get("version") or "").strip()
evidence["release_channel_status"] = release_channel_status
evidence["release_channel_id"] = release_channel_id
evidence["release_channel_version"] = release_channel_version
if release_channel_status != "published":
    reasons.append("Release channel is not published.")
if not release_channel_version:
    reasons.append("Release channel is missing version.")

artifacts = [
    item for item in (release_channel.get("artifacts") or [])
    if isinstance(item, dict)
]
expected_head = expected_head_override or "avalonia"
expected_rid = expected_rid_override or "win-x64"
expected_arch = "x64"
windows_artifact = None
for artifact in artifacts:
    if (
        normalize_token(artifact.get("head")) == expected_head
        and normalize_token(artifact.get("platform")) == "windows"
        and normalize_token(artifact.get("kind")) in {"installer", "msix"}
        and artifact_rid(artifact) == expected_rid
    ):
        windows_artifact = artifact
        break

if windows_artifact is None:
    reasons.append(
        f"Release channel does not publish a promoted Windows install medium artifact for {expected_head} ({expected_rid})."
    )
    artifact_file_name = ""
    artifact_size = 0
    artifact_sha = ""
else:
    expected_head = normalize_token(windows_artifact.get("head")) or expected_head
    expected_rid = artifact_rid(windows_artifact) or expected_rid
    if expected_rid.startswith("win-") and len(expected_rid) > 4:
        expected_arch = expected_rid.split("-", 1)[1]
    artifact_file_name = str(windows_artifact.get("fileName") or "").strip()
    artifact_size = int(windows_artifact.get("sizeBytes") or 0)
    artifact_sha = str(windows_artifact.get("sha256") or "").strip().lower()
    evidence["release_channel_windows_artifact"] = windows_artifact

default_file_name = artifact_file_name or f"chummer-{expected_head}-{expected_rid}-installer.exe"
if windows_installer_path_override:
    installer_candidates = [windows_installer_path_override.expanduser().resolve()]
else:
    installer_candidates = [
        (windows_local_desktop_files_root / default_file_name).resolve(),
        (repo_root / "Docker" / "Downloads" / "files" / default_file_name).resolve(),
    ]
installer_candidates = list(dict.fromkeys(path.resolve() for path in installer_candidates))
installer_path = next((path for path in installer_candidates if path.is_file()), installer_candidates[0])

installer_exists = installer_path.is_file()
installer_size = installer_path.stat().st_size if installer_exists else 0
installer_sha = sha256_file(installer_path) if installer_exists else ""
evidence["windows_installer_path"] = str(installer_path)
evidence["windows_installer_candidate_paths"] = [str(path) for path in installer_candidates]
evidence["installer_exists"] = installer_exists
evidence["installer_size_bytes"] = installer_size
evidence["installer_sha256"] = installer_sha
evidence["expected_windows_head"] = expected_head
evidence["expected_windows_rid"] = expected_rid
evidence["expected_windows_arch"] = expected_arch
if artifact_file_name:
    evidence["expected_windows_file_name"] = artifact_file_name
primary_shelf_root = (repo_root / "Docker" / "Downloads" / "files").resolve()
installer_from_primary_shelf = path_is_within(installer_path, primary_shelf_root)
evidence["windows_installer_primary_shelf_root"] = str(primary_shelf_root)
evidence["windows_installer_from_primary_shelf"] = installer_from_primary_shelf
if not windows_installer_path_override and not installer_from_primary_shelf:
    reasons.append(
        "Promoted Windows installer was not resolved from the repo-local desktop shelf."
    )
if installer_exists and path_uses_legacy_chummer5a_root(installer_path):
    reasons.append("Promoted Windows installer was resolved from legacy chummer5a shelf bytes.")

if not installer_exists:
    reasons.append("Promoted Windows installer is missing from the active public downloads shelf.")
if artifact_file_name and artifact_file_name != installer_path.name:
    reasons.append("Release-channel Windows artifact fileName does not match the selected installer path.")
if installer_exists and artifact_size and artifact_size != installer_size:
    reasons.append("Release-channel Windows artifact size does not match installer bytes.")
if installer_exists and artifact_sha and artifact_sha != installer_sha:
    reasons.append("Release-channel Windows artifact sha256 does not match installer digest.")

payload_marker_present = False
sample_marker_present = False
if installer_exists:
    blob = installer_path.read_bytes()
    payload_marker_present = b"ChummerInstaller.Payload.zip" in blob
    sample_marker_present = b"Samples/Legacy/Soma-Career.chum5" in blob
evidence["embedded_payload_marker_present"] = payload_marker_present
evidence["embedded_sample_marker_present"] = sample_marker_present
evidence["installer_payload_validation_mode"] = "release-channel digest-size-and-embedded-markers"

if installer_exists and not payload_marker_present:
    reasons.append("Published Windows installer is missing the embedded desktop payload marker.")
if installer_exists and not sample_marker_present:
    reasons.append("Published Windows installer is missing the bundled demo runner sample marker.")

startup_smoke_receipt_override = os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH", "").strip()
if startup_smoke_receipt_override:
    startup_smoke_receipt_path = Path(startup_smoke_receipt_override).resolve()
    startup_smoke_candidates = [startup_smoke_receipt_path]
else:
    startup_smoke_receipt_name = f"startup-smoke-{expected_head}-{expected_rid}.receipt.json"
    startup_smoke_candidates = [
        release_channel_path.parent / "startup-smoke" / startup_smoke_receipt_name,
        release_channel_path.parent.parent / "startup-smoke" / startup_smoke_receipt_name,
        proof_path.parent / "startup-smoke" / startup_smoke_receipt_name,
        repo_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_receipt_name,
        repo_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_receipt_name,
    ]
    if hub_registry_root is not None:
        startup_smoke_candidates.extend(
            [
                hub_registry_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_receipt_name,
                hub_registry_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_receipt_name,
            ]
        )
    startup_smoke_receipt_path = next((path for path in startup_smoke_candidates if path.is_file()), startup_smoke_candidates[0])

startup_smoke_payload = load_json(startup_smoke_receipt_path)
evidence["startup_smoke_receipt_path"] = str(startup_smoke_receipt_path)
evidence["startup_smoke_receipt_candidates"] = [str(path) for path in startup_smoke_candidates]
evidence["startup_smoke_receipt_found"] = startup_smoke_receipt_path.is_file()
evidence["startup_smoke_external_blocker"] = (
    "missing_windows_host_capability"
    if (not startup_smoke_receipt_path.is_file() and not host_supports_windows_smoke)
    else ""
)

startup_smoke_status = normalize_token(startup_smoke_payload.get("status"))
evidence["startup_smoke_status"] = startup_smoke_status
if not startup_smoke_receipt_path.is_file():
    reasons.append("Windows startup smoke receipt is missing for promoted installer bytes.")
    if not host_supports_windows_smoke:
        reasons.append(
            "Windows startup smoke requires a Windows-capable host; current host cannot run promoted Windows installer smoke."
        )
elif startup_smoke_status not in PASSING_STARTUP_SMOKE_STATUSES:
    reasons.append("Windows startup smoke receipt status is not passing.")

if startup_smoke_receipt_path.is_file() and path_uses_legacy_chummer5a_root(startup_smoke_receipt_path):
    reasons.append("Windows startup smoke receipt was resolved from a legacy chummer5a path.")

startup_smoke_checkpoint = normalize_token(startup_smoke_payload.get("readyCheckpoint"))
evidence["startup_smoke_ready_checkpoint"] = startup_smoke_checkpoint
if startup_smoke_receipt_path.is_file() and startup_smoke_checkpoint != "pre_ui_event_loop":
    reasons.append("Windows startup smoke receipt readyCheckpoint is not pre_ui_event_loop.")

startup_smoke_digest = normalize_token(startup_smoke_payload.get("artifactDigest"))
evidence["startup_smoke_artifact_digest"] = startup_smoke_digest
expected_installer_digest = f"sha256:{installer_sha}" if installer_sha else ""
evidence["expected_startup_smoke_artifact_digest"] = expected_installer_digest
if startup_smoke_receipt_path.is_file() and installer_exists and expected_installer_digest and startup_smoke_digest != expected_installer_digest:
    reasons.append("Windows startup smoke receipt artifactDigest does not match promoted installer bytes.")

startup_smoke_head = normalize_token(startup_smoke_payload.get("headId"))
startup_smoke_platform = normalize_token(startup_smoke_payload.get("platform"))
startup_smoke_arch = normalize_token(startup_smoke_payload.get("arch"))
startup_smoke_channel = normalize_token(startup_smoke_payload.get("channelId") or startup_smoke_payload.get("channel"))
startup_smoke_version = str(
    startup_smoke_payload.get("version")
    or startup_smoke_payload.get("releaseVersion")
    or ""
).strip()
startup_smoke_host_class = normalize_token(startup_smoke_payload.get("hostClass"))
startup_smoke_operating_system = str(startup_smoke_payload.get("operatingSystem") or "").strip()
evidence["startup_smoke_head"] = startup_smoke_head
evidence["startup_smoke_platform"] = startup_smoke_platform
evidence["startup_smoke_arch"] = startup_smoke_arch
evidence["startup_smoke_channel"] = startup_smoke_channel
evidence["startup_smoke_version"] = startup_smoke_version
evidence["startup_smoke_host_class"] = startup_smoke_host_class
evidence["startup_smoke_operating_system"] = startup_smoke_operating_system
if startup_smoke_receipt_path.is_file() and startup_smoke_head != expected_head:
    reasons.append(f"Windows startup smoke receipt headId does not match promoted head {expected_head}.")
if startup_smoke_receipt_path.is_file() and startup_smoke_platform != "windows":
    reasons.append("Windows startup smoke receipt platform is not windows.")
if startup_smoke_receipt_path.is_file() and not startup_smoke_host_class:
    reasons.append("Windows startup smoke receipt hostClass is missing.")
if startup_smoke_receipt_path.is_file() and startup_smoke_host_class and not host_class_matches_platform(startup_smoke_host_class, "windows"):
    reasons.append("Windows startup smoke receipt hostClass does not identify a Windows host.")
if startup_smoke_receipt_path.is_file() and not startup_smoke_operating_system:
    reasons.append("Windows startup smoke receipt operatingSystem is missing.")
if startup_smoke_receipt_path.is_file() and startup_smoke_arch != expected_arch:
    reasons.append(f"Windows startup smoke receipt arch does not match promoted RID {expected_rid}.")
if startup_smoke_receipt_path.is_file() and release_channel_id and startup_smoke_channel != release_channel_id:
    reasons.append(f"Windows startup smoke receipt channelId does not match release channel {release_channel_id}.")
if startup_smoke_receipt_path.is_file() and release_channel_version and not startup_smoke_version:
    reasons.append("Windows startup smoke receipt version is missing.")
if startup_smoke_receipt_path.is_file() and release_channel_version and startup_smoke_version and startup_smoke_version != release_channel_version:
    reasons.append(f"Windows startup smoke receipt version does not match release channel {release_channel_version}.")

launch_target_by_head = {
    "avalonia": "Chummer.Avalonia.exe",
    "blazor-desktop": "Chummer.Blazor.Desktop.exe",
}
launch_target = launch_target_by_head.get(expected_head, "Chummer.Avalonia.exe")

startup_smoke_timestamp = parse_iso_utc(
    startup_smoke_payload.get("completedAtUtc")
    or startup_smoke_payload.get("recordedAtUtc")
    or startup_smoke_payload.get("startedAtUtc")
)
evidence["startup_smoke_completed_at"] = (
    startup_smoke_timestamp.replace(microsecond=0).isoformat().replace("+00:00", "Z")
    if startup_smoke_timestamp
    else ""
)
if startup_smoke_receipt_path.is_file():
    if startup_smoke_timestamp is None:
        reasons.append("Windows startup smoke receipt timestamp is missing or invalid.")
    else:
        startup_smoke_age_delta_seconds = int((datetime.now(timezone.utc) - startup_smoke_timestamp).total_seconds())
        startup_smoke_age_seconds = max(0, startup_smoke_age_delta_seconds)
        if startup_smoke_age_delta_seconds < 0:
            startup_smoke_future_skew_seconds = abs(startup_smoke_age_delta_seconds)
            evidence["startup_smoke_future_skew_seconds"] = startup_smoke_future_skew_seconds
            if startup_smoke_future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                reasons.append(
                    f"Windows startup smoke receipt timestamp is in the future ({startup_smoke_future_skew_seconds}s ahead)."
                )
        evidence["startup_smoke_age_seconds"] = startup_smoke_age_seconds
        evidence["startup_smoke_max_age_seconds"] = STARTUP_SMOKE_MAX_AGE_SECONDS
        evidence["startup_smoke_max_future_skew_seconds"] = STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS
        if startup_smoke_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
            reasons.append(f"Windows startup smoke receipt is stale ({startup_smoke_age_seconds}s old).")

ui_local_release_status = read_status(
    ui_local_release_proof_path,
    expected_contract="chummer6-ui.local_release_proof",
)
ui_flagship_gate_status = read_status(ui_flagship_release_gate_path)
ui_workflow_parity_status = read_status(
    ui_workflow_parity_path,
    expected_contract="chummer6-ui.chummer5a_desktop_workflow_parity",
)
sr4_workflow_parity_status = read_status(
    sr4_workflow_parity_path,
    expected_contract="chummer6-ui.sr4_desktop_workflow_parity",
)
sr6_workflow_parity_status = read_status(
    sr6_workflow_parity_path,
    expected_contract="chummer6-ui.sr6_desktop_workflow_parity",
)
evidence["ui_local_release_status"] = ui_local_release_status
evidence["ui_flagship_release_gate_status"] = ui_flagship_gate_status
evidence["ui_workflow_parity_status"] = ui_workflow_parity_status
evidence["sr4_workflow_parity_status"] = sr4_workflow_parity_status
evidence["sr6_workflow_parity_status"] = sr6_workflow_parity_status

if ui_local_release_status not in {"pass", "passed"}:
    reasons.append("UI local release proof is missing or not passed.")
if ui_flagship_gate_status not in {"pass", "passed", "ready"}:
    reasons.append("Flagship UI release gate proof is missing or not passed.")
if ui_workflow_parity_status not in {"pass", "passed", "ready"}:
    reasons.append("Chummer5a desktop workflow parity proof is missing or not passed.")
if sr4_workflow_parity_status not in {"pass", "passed", "ready"}:
    reasons.append("SR4 desktop workflow parity proof is missing or not passed.")
if sr6_workflow_parity_status not in {"pass", "passed", "ready"}:
    reasons.append("SR6 desktop workflow parity proof is missing or not passed.")

status = "passed" if not reasons else "failed"
payload = {
    "contract_name": "chummer6-ui.windows_desktop_exit_gate",
    "generated_at": now_iso(),
    "channelId": release_channel_id,
    "releaseVersion": release_channel_version,
    "status": status,
    "reason": (
        "windows desktop release-channel publication and workflow proof checks passed"
        if status == "passed"
        else "windows desktop exit gate checks failed"
    ),
    "head": {
        "app_key": expected_head,
        "platform": "windows",
        "rid": expected_rid,
        "launch_target": launch_target,
    },
    "checks": evidence,
    "reasons": reasons,
}
proof_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if reasons:
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

echo "[windows-exit-gate] PASS"
