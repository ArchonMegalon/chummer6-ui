#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

APP_KEY="${CHUMMER_MACOS_DESKTOP_EXIT_GATE_APP_KEY:-avalonia}"
CANONICAL_RELEASE_CHANNEL_PATH="/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json"
DEFAULT_RELEASE_CHANNEL_PATH="$REPO_ROOT/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -f "$CANONICAL_RELEASE_CHANNEL_PATH" ]]; then
  RELEASE_CHANNEL_PATH_DEFAULT="$CANONICAL_RELEASE_CHANNEL_PATH"
else
  RELEASE_CHANNEL_PATH_DEFAULT="$DEFAULT_RELEASE_CHANNEL_PATH"
fi
RELEASE_CHANNEL_PATH="${CHUMMER_MACOS_RELEASE_CHANNEL_PATH:-$RELEASE_CHANNEL_PATH_DEFAULT}"
RID="${CHUMMER_MACOS_DESKTOP_EXIT_GATE_RID:-}"
if [[ -z "$RID" ]]; then
  RID="$(python3 - "$RELEASE_CHANNEL_PATH" "$APP_KEY" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
app_key = sys.argv[2].strip().lower()

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
    raise SystemExit("")

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = [
    item for item in (payload.get("artifacts") or [])
    if isinstance(item, dict)
    and normalize(item.get("head")) == app_key
    and normalize(item.get("platform")) == "macos"
    and normalize(item.get("kind")) in {"installer", "dmg", "pkg"}
    and artifact_rid(item)
]

preferred_order = ["osx-arm64", "osx-x64"]
for preferred in preferred_order:
    for artifact in artifacts:
        if artifact_rid(artifact) == preferred:
            print(preferred)
            raise SystemExit(0)

if artifacts:
    print(artifact_rid(artifacts[0]))
PY
)"
fi
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

python3 - "$PROOF_PATH" "$RELEASE_CHANNEL_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$STARTUP_SMOKE_RECEIPT_PATH" "$INSTALLER_PATH" "$REPO_ROOT" <<'PY'
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


proof_path = Path(sys.argv[1])
release_channel_path = Path(sys.argv[2])
app_key = sys.argv[3]
rid = sys.argv[4]
launch_target = sys.argv[5]
startup_smoke_receipt_arg = sys.argv[6].strip()
installer_path_arg = sys.argv[7].strip()
repo_root = Path(sys.argv[8])
startup_smoke_max_age_seconds = int(
    str(
        os.environ.get("CHUMMER_MACOS_STARTUP_SMOKE_MAX_AGE_SECONDS")
        or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
        or "86400"
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
evidence["release_channel_status"] = release_channel_status
if release_channel_status != "published":
    reasons.append("Release channel is not published.")

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
    reasons.append(f"Release channel does not publish a macOS install medium for {app_key} ({rid}).")
    file_name = f"chummer-{app_key}-{rid}-installer.dmg"
else:
    evidence["release_channel_macos_artifact"] = macos_artifact
    file_name = str(macos_artifact.get("fileName") or "").strip() or f"chummer-{app_key}-{rid}-installer.dmg"

downloads_candidates = [
    repo_root / "Docker" / "Downloads" / "files" / file_name,
    Path("/docker/chummercomplete/chummer-presentation/Docker/Downloads/files") / file_name,
    Path("/docker/chummercomplete/chummer6-ui/Docker/Downloads/files") / file_name,
    Path("/docker/chummer5a/Docker/Downloads/files") / file_name,
    Path("/docker/chummercomplete/chummer5a/Docker/Downloads/files") / file_name,
]
installer_path = resolve_existing_path(installer_path_arg, downloads_candidates)
artifact_exists = installer_path is not None
artifact_size = installer_path.stat().st_size if installer_path else 0
artifact_sha = sha256_file(installer_path) if installer_path else ""
evidence["artifact"] = {
    "installer_path": str(installer_path) if installer_path else "",
    "installer_exists": artifact_exists,
    "installer_size_bytes": artifact_size,
    "installer_sha256": artifact_sha,
    "file_name": file_name,
}
if not artifact_exists:
    reasons.append(f"Promoted macOS installer file is missing locally for {app_key} ({rid}).")

if macos_artifact is not None:
    artifact_size_expected = int(macos_artifact.get("sizeBytes") or 0)
    artifact_sha_expected = normalize_token(macos_artifact.get("sha256"))
    if artifact_exists and artifact_size_expected and artifact_size != artifact_size_expected:
        reasons.append("Release-channel macOS artifact size does not match installer bytes.")
    if artifact_exists and artifact_sha_expected and artifact_sha != artifact_sha_expected:
        reasons.append("Release-channel macOS artifact sha256 does not match installer digest.")

startup_smoke_candidates = [
    Path(startup_smoke_receipt_arg) if startup_smoke_receipt_arg else None,
    release_channel_path.parent / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    release_channel_path.parent.parent / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    repo_root / ".codex-studio" / "published" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    repo_root / "Docker" / "Downloads" / "startup-smoke" / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer-hub-registry/.codex-studio/published/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer-hub-registry/Docker/Downloads/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer-presentation/.codex-studio/published/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer-presentation/Docker/Downloads/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer6-ui/Docker/Downloads/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummer5a/Docker/Downloads/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
    Path("/docker/chummercomplete/chummer5a/Docker/Downloads/startup-smoke") / f"startup-smoke-{app_key}-{rid}.receipt.json",
]
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
startup_smoke_recorded_at_raw = str(
    startup_smoke_payload.get("completedAtUtc")
    or startup_smoke_payload.get("recordedAtUtc")
    or startup_smoke_payload.get("startedAtUtc")
    or ""
).strip()
startup_smoke_recorded_at = parse_iso(startup_smoke_recorded_at_raw)
startup_smoke_age_seconds = (
    max(0, int((datetime.now(timezone.utc) - startup_smoke_recorded_at).total_seconds()))
    if startup_smoke_recorded_at is not None
    else None
)
evidence["startup_smoke"] = {
    "status": startup_smoke_status,
    "receipt_path": str(startup_smoke_path) if startup_smoke_path else "",
    "candidate_paths": startup_smoke_candidate_paths,
    "ready_checkpoint": startup_smoke_checkpoint,
    "artifact_digest": startup_smoke_artifact_digest,
    "receipt_recorded_at": startup_smoke_recorded_at_raw,
    "receipt_age_seconds": startup_smoke_age_seconds,
    "receipt": startup_smoke_payload,
}
if not startup_smoke_payload:
    reasons.append(f"macOS startup smoke receipt is missing for {app_key} ({rid}).")
else:
    expected_arch = expected_arch_from_rid(rid)
    if normalize_token(startup_smoke_payload.get("headId")) != normalize_token(app_key):
        reasons.append("macOS startup smoke receipt headId does not match promoted app key.")
    if normalize_token(startup_smoke_payload.get("platform")) != "macos":
        reasons.append("macOS startup smoke receipt platform is not macOS.")
    if expected_arch and normalize_token(startup_smoke_payload.get("arch")) != expected_arch:
        reasons.append("macOS startup smoke receipt arch does not match promoted RID.")
    if startup_smoke_status not in {"pass", "passed", "ready"}:
        reasons.append("macOS startup smoke receipt status is not passing.")
    if startup_smoke_checkpoint != "pre_ui_event_loop":
        reasons.append("macOS startup smoke receipt did not reach pre_ui_event_loop.")
    if not artifact_sha:
        reasons.append("Promoted macOS installer digest could not be computed.")
    elif startup_smoke_artifact_digest != f"sha256:{artifact_sha}":
        reasons.append("macOS startup smoke receipt artifactDigest does not match promoted installer bytes.")
    if startup_smoke_recorded_at is None:
        reasons.append("macOS startup smoke receipt is missing a valid recorded/completed timestamp.")
    elif startup_smoke_age_seconds is not None and startup_smoke_age_seconds > startup_smoke_max_age_seconds:
        reasons.append(
            f"macOS startup smoke receipt is stale ({startup_smoke_age_seconds}s old; max {startup_smoke_max_age_seconds}s)."
        )

status = "passed" if not reasons else "failed"
payload = {
    "contract_name": "chummer6-ui.macos_desktop_exit_gate",
    "generated_at": now_iso(),
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
