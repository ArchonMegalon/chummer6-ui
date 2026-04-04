#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CURRENT_STAGE="init"

APP_KEY_OVERRIDE="${CHUMMER_MACOS_DESKTOP_EXIT_GATE_APP_KEY:-}"
HUB_REGISTRY_ROOT="${CHUMMER_HUB_REGISTRY_ROOT:-$("$REPO_ROOT/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
CANONICAL_RELEASE_CHANNEL_PATH="${HUB_REGISTRY_ROOT:+$HUB_REGISTRY_ROOT/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
DEFAULT_RELEASE_CHANNEL_PATH="$REPO_ROOT/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$CANONICAL_RELEASE_CHANNEL_PATH" && -f "$CANONICAL_RELEASE_CHANNEL_PATH" ]]; then
  RELEASE_CHANNEL_PATH_DEFAULT="$CANONICAL_RELEASE_CHANNEL_PATH"
else
  RELEASE_CHANNEL_PATH_DEFAULT="$DEFAULT_RELEASE_CHANNEL_PATH"
fi
RELEASE_CHANNEL_PATH="${CHUMMER_MACOS_RELEASE_CHANNEL_PATH:-$RELEASE_CHANNEL_PATH_DEFAULT}"
RID_OVERRIDE="${CHUMMER_MACOS_DESKTOP_EXIT_GATE_RID:-}"
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
    if arch in {"arm64", "x64"}:
        return f"osx-{arch}"
    return ""

if not release_channel_path.is_file():
    raise SystemExit(0)

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = [
    item for item in (payload.get("artifacts") or [])
    if isinstance(item, dict)
    and normalize(item.get("platform")) == "macos"
    and normalize(item.get("kind")) in {"installer", "dmg", "pkg"}
    and artifact_rid(item)
]

if app_key_override:
    artifacts = [item for item in artifacts if normalize(item.get("head")) == app_key_override]
if rid_override:
    artifacts = [item for item in artifacts if artifact_rid(item) == rid_override]
if not artifacts:
    raise SystemExit(0)

preferred_order = ["osx-arm64", "osx-x64"]
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
RID="${RID_OVERRIDE:-${RELEASE_PROMOTED_TUPLE[1]:-osx-arm64}}"
RID="${RID:-osx-arm64}"
APP_KEY_PROOF_TOKEN="${APP_KEY^^}"
APP_KEY_PROOF_TOKEN="${APP_KEY_PROOF_TOKEN//-/_}"
RID_PROOF_TOKEN="${RID^^}"
RID_PROOF_TOKEN="${RID_PROOF_TOKEN//-/_}"

case "$APP_KEY" in
  avalonia)
    DEFAULT_LAUNCH_TARGET="Chummer.Avalonia"
    ;;
  blazor-desktop)
    DEFAULT_LAUNCH_TARGET="Chummer.Blazor.Desktop"
    ;;
  *)
    echo "Unsupported macOS desktop exit gate app key: $APP_KEY" >&2
    exit 1
    ;;
esac

PROOF_PATH="${CHUMMER_UI_MACOS_DESKTOP_EXIT_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_MACOS_${APP_KEY_PROOF_TOKEN}_${RID_PROOF_TOKEN}_DESKTOP_EXIT_GATE.generated.json}"
LAUNCH_TARGET="${CHUMMER_MACOS_DESKTOP_EXIT_GATE_LAUNCH_TARGET:-$DEFAULT_LAUNCH_TARGET}"
STARTUP_SMOKE_RECEIPT_PATH="${CHUMMER_MACOS_STARTUP_SMOKE_RECEIPT_PATH:-}"
INSTALLER_PATH="${CHUMMER_MACOS_INSTALLER_PATH:-}"

mkdir -p "$(dirname "$PROOF_PATH")"

CURRENT_STAGE="promoted_installer_proof_integrity"
python3 - "$PROOF_PATH" "$RELEASE_CHANNEL_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$STARTUP_SMOKE_RECEIPT_PATH" "$INSTALLER_PATH" "$REPO_ROOT" "$HUB_REGISTRY_ROOT" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, Iterable, List


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def parse_iso(value: Any) -> datetime | None:
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
    if arch in {"arm64", "x64"}:
        return f"osx-{arch}"
    return ""


def is_macos_install_media_kind(kind: Any) -> bool:
    return normalize_token(kind) in {"installer", "dmg", "pkg"}


def expected_arch_from_rid(rid: str) -> str:
    return "arm64" if rid.endswith("arm64") else "x64" if rid.endswith("x64") else ""


def resolve_existing_path(explicit_path: str, candidates: Iterable[Path]) -> Path | None:
    if explicit_path:
        path = Path(explicit_path)
        return path if path.is_file() else None
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    return None


def path_is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


proof_path = Path(sys.argv[1])
release_channel_path = Path(sys.argv[2])
app_key = sys.argv[3]
rid = sys.argv[4]
launch_target = sys.argv[5]
startup_smoke_receipt_arg = sys.argv[6].strip()
installer_path_arg = sys.argv[7].strip()
repo_root = Path(sys.argv[8])
hub_registry_root_arg = str(sys.argv[9] or "").strip()
hub_registry_root = Path(hub_registry_root_arg).resolve() if hub_registry_root_arg else None
startup_smoke_max_age_seconds = int(
    str(
        os.environ.get("CHUMMER_MACOS_STARTUP_SMOKE_MAX_AGE_SECONDS")
        or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
        or "86400"
    ).strip()
)
startup_smoke_max_future_skew_seconds = int(
    str(
        os.environ.get("CHUMMER_MACOS_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
        or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
        or "300"
    ).strip()
)

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "app_key": app_key,
    "rid": rid,
    "launch_target": launch_target,
}

release_channel = load_json(release_channel_path)
release_channel_status = normalize_token(release_channel.get("status"))
release_channel_id = normalize_token(release_channel.get("channelId") or release_channel.get("channel"))
release_channel_version = str(release_channel.get("version") or "").strip()
evidence["release_channel_status"] = release_channel_status
evidence["release_channel_id"] = release_channel_id
evidence["release_channel_version"] = release_channel_version
if release_channel_status != "published":
    reasons.append("macOS release-channel proof status is not published.")
if not release_channel_version:
    reasons.append("macOS release channel is missing version.")

artifacts = [
    item for item in (release_channel.get("artifacts") or [])
    if isinstance(item, dict)
]
macos_artifact = None
for artifact in artifacts:
    if (
        normalize_token(artifact.get("head")) == normalize_token(app_key)
        and normalize_token(artifact.get("platform")) == "macos"
        and is_macos_install_media_kind(artifact.get("kind"))
        and artifact_rid(artifact) == normalize_token(rid)
    ):
        macos_artifact = artifact
        break

if macos_artifact is None:
    reasons.append(f"Release channel does not publish a promoted macOS install medium artifact for {app_key} ({rid}).")
    file_name = f"chummer-{app_key}-{rid}-installer.dmg"
else:
    evidence["release_channel_macos_artifact"] = macos_artifact
    file_name = str(macos_artifact.get("fileName") or "").strip() or f"chummer-{app_key}-{rid}-installer.dmg"

downloads_candidates = [
    repo_root / "Docker" / "Downloads" / "files" / file_name,
]
downloads_candidates = list(dict.fromkeys(candidate.resolve() for candidate in downloads_candidates))
installer_path = resolve_existing_path(installer_path_arg, downloads_candidates)
artifact_exists = installer_path is not None
artifact_size = installer_path.stat().st_size if installer_path else 0
artifact_sha = sha256_file(installer_path) if installer_path else ""
primary_shelf_root = (repo_root / "Docker" / "Downloads" / "files").resolve()
installer_from_primary_shelf = installer_path is not None and path_is_within(installer_path, primary_shelf_root)
evidence["artifact"] = {
    "installer_path": str(installer_path) if installer_path else "",
    "installer_exists": artifact_exists,
    "installer_size_bytes": artifact_size,
    "installer_sha256": artifact_sha,
    "file_name": file_name,
    "installer_candidate_paths": [str(candidate) for candidate in downloads_candidates],
    "installer_primary_shelf_root": str(primary_shelf_root),
    "installer_from_primary_shelf": installer_from_primary_shelf,
}
if not artifact_exists:
    reasons.append(f"Promoted macOS installer file is missing locally for {app_key} ({rid}).")
elif not installer_path_arg and not installer_from_primary_shelf:
    reasons.append(f"Promoted macOS installer was not resolved from the repo-local desktop shelf for {app_key} ({rid}).")

if macos_artifact is not None:
    artifact_size_expected = int(macos_artifact.get("sizeBytes") or 0)
    artifact_sha_expected = normalize_token(macos_artifact.get("sha256"))
    if artifact_exists and artifact_size_expected and artifact_size != artifact_size_expected:
        reasons.append("macOS release-channel artifact size does not match promoted installer bytes.")
    if artifact_exists and artifact_sha_expected and artifact_sha != artifact_sha_expected:
        reasons.append("macOS release-channel artifact sha256 does not match promoted installer bytes.")

startup_smoke_candidates = [
    Path(startup_smoke_receipt_arg) if startup_smoke_receipt_arg else None,
    release_channel_path.parent / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    release_channel_path.parent.parent / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    repo_root / ".codex-studio" / "published" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    repo_root / "Docker" / "Downloads" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
]
if hub_registry_root is not None:
    startup_smoke_candidates.extend(
        [
            hub_registry_root / ".codex-studio" / "published" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
            hub_registry_root / "Docker" / "Downloads" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
        ]
    )
startup_smoke_candidate_paths = list(
    dict.fromkeys(str(candidate) for candidate in startup_smoke_candidates if candidate is not None)
)
evidence["startup_smoke_candidate_paths"] = startup_smoke_candidate_paths
startup_smoke_path = resolve_existing_path(
    startup_smoke_receipt_arg,
    [candidate for candidate in startup_smoke_candidates if candidate is not None],
)
startup_smoke_payload = load_json(startup_smoke_path) if startup_smoke_path else {}
startup_smoke_status = normalize_token(startup_smoke_payload.get("status")) or ("pass" if startup_smoke_payload else "fail")
startup_smoke_checkpoint = normalize_token(startup_smoke_payload.get("readyCheckpoint"))
startup_smoke_artifact_digest = normalize_token(startup_smoke_payload.get("artifactDigest"))
startup_smoke_channel = normalize_token(startup_smoke_payload.get("channelId") or startup_smoke_payload.get("channel"))
startup_smoke_version = str(
    startup_smoke_payload.get("version")
    or startup_smoke_payload.get("releaseVersion")
    or ""
).strip()
startup_smoke_host_class = normalize_token(startup_smoke_payload.get("hostClass"))
startup_smoke_operating_system = str(startup_smoke_payload.get("operatingSystem") or "").strip()
startup_smoke_recorded_at_raw = str(
    startup_smoke_payload.get("completedAtUtc")
    or startup_smoke_payload.get("recordedAtUtc")
    or startup_smoke_payload.get("startedAtUtc")
    or ""
).strip()
startup_smoke_recorded_at = parse_iso(startup_smoke_recorded_at_raw)
startup_smoke_age_delta_seconds = (
    int((datetime.now(timezone.utc) - startup_smoke_recorded_at).total_seconds())
    if startup_smoke_recorded_at is not None
    else None
)
startup_smoke_age_seconds = (
    max(0, startup_smoke_age_delta_seconds)
    if startup_smoke_age_delta_seconds is not None
    else None
)
evidence["startup_smoke"] = {
    "status": startup_smoke_status,
    "receipt_path": str(startup_smoke_path) if startup_smoke_path else "",
    "candidate_paths": startup_smoke_candidate_paths,
    "ready_checkpoint": startup_smoke_checkpoint,
    "artifact_digest": startup_smoke_artifact_digest,
    "channel_id": startup_smoke_channel,
    "version": startup_smoke_version,
    "host_class": startup_smoke_host_class,
    "operating_system": startup_smoke_operating_system,
    "receipt_recorded_at": startup_smoke_recorded_at_raw,
    "receipt_age_seconds": startup_smoke_age_seconds,
    "receipt_max_age_seconds": startup_smoke_max_age_seconds,
    "receipt_max_future_skew_seconds": startup_smoke_max_future_skew_seconds,
    "receipt": startup_smoke_payload,
}
if not startup_smoke_payload:
    reasons.append(f"macOS startup smoke receipt is missing for {app_key} ({rid}).")
else:
    expected_arch = expected_arch_from_rid(rid)
    if normalize_token(startup_smoke_payload.get("headId")) != normalize_token(app_key):
        reasons.append(f"macOS startup smoke receipt headId does not match promoted head {app_key}.")
    if normalize_token(startup_smoke_payload.get("platform")) != "macos":
        reasons.append("macOS startup smoke receipt platform is not macOS.")
    if not startup_smoke_host_class:
        reasons.append("macOS startup smoke receipt hostClass is missing.")
    elif not host_class_matches_platform(startup_smoke_host_class, "macos"):
        reasons.append("macOS startup smoke receipt hostClass does not identify a macOS host.")
    if not startup_smoke_operating_system:
        reasons.append("macOS startup smoke receipt operatingSystem is missing.")
    if expected_arch and normalize_token(startup_smoke_payload.get("arch")) != expected_arch:
        reasons.append(f"macOS startup smoke receipt arch does not match promoted RID {rid}.")
    if release_channel_id and startup_smoke_channel != release_channel_id:
        reasons.append(f"macOS startup smoke receipt channelId does not match release channel {release_channel_id}.")
    if release_channel_version and not startup_smoke_version:
        reasons.append("macOS startup smoke receipt version is missing.")
    if release_channel_version and startup_smoke_version and startup_smoke_version != release_channel_version:
        reasons.append(f"macOS startup smoke receipt version does not match release channel {release_channel_version}.")
    if startup_smoke_status not in {"pass", "passed", "ready"}:
        reasons.append("macOS startup smoke receipt status is not passing.")
    if startup_smoke_checkpoint != "pre_ui_event_loop":
        reasons.append("macOS startup smoke receipt readyCheckpoint is not pre_ui_event_loop.")
    if not artifact_sha:
        reasons.append("Promoted macOS installer digest could not be computed.")
    elif startup_smoke_artifact_digest != f"sha256:{artifact_sha}":
        reasons.append("macOS startup smoke receipt artifactDigest does not match promoted installer bytes.")
    if startup_smoke_recorded_at is None:
        reasons.append("macOS startup smoke receipt timestamp is missing or invalid.")
    elif startup_smoke_age_delta_seconds is not None and startup_smoke_age_delta_seconds < 0:
        startup_smoke_future_skew_seconds = abs(startup_smoke_age_delta_seconds)
        evidence["startup_smoke"]["receipt_future_skew_seconds"] = startup_smoke_future_skew_seconds
        if startup_smoke_future_skew_seconds > startup_smoke_max_future_skew_seconds:
            reasons.append(
                f"macOS startup smoke receipt timestamp is in the future ({startup_smoke_future_skew_seconds}s ahead)."
            )
    elif startup_smoke_age_seconds is not None and startup_smoke_age_seconds > startup_smoke_max_age_seconds:
        reasons.append(f"macOS startup smoke receipt is stale ({startup_smoke_age_seconds}s old).")

status = "passed" if not reasons else "failed"
payload = {
    "contract_name": "chummer6-ui.macos_desktop_exit_gate",
    "generated_at": now_iso(),
    "channelId": release_channel_id,
    "releaseVersion": release_channel_version,
    "status": status,
    "reason": (
        "macOS desktop release-channel publication and startup smoke checks passed"
        if status == "passed"
        else "macOS desktop exit gate checks failed"
    ),
    "head": {
        "app_key": app_key,
        "platform": "macos",
        "rid": rid,
        "launch_target": launch_target,
    },
    "artifact": evidence["artifact"],
    "startup_smoke": evidence["startup_smoke"],
    "checks": evidence,
    "reasons": reasons,
}
proof_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if reasons:
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

echo "[macos-exit-gate] PASS"
