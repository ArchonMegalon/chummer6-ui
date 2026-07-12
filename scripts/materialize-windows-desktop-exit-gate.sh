#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -L "$(dirname "${BASH_SOURCE[0]}")" && pwd -L)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
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
  RELEASE_PROMOTED_TUPLE=()
  while IFS= read -r tuple_value; do
    [[ -n "$tuple_value" ]] || continue
    RELEASE_PROMOTED_TUPLE+=("$tuple_value")
  done < <(python3 - "$RELEASE_CHANNEL_PATH" "$APP_KEY_OVERRIDE" "$RID_OVERRIDE" <<'PY'
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
DEFAULT_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"
RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"
RELEASE_CHANNEL_FILES_ROOT_DEFAULT=""
if [[ -n "$RELEASE_CHANNEL_DIRECTORY" ]]; then
  RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"
fi
if [[ -n "${CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT:-}" ]]; then
  WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"
elif [[ -n "$RELEASE_CHANNEL_FILES_ROOT_DEFAULT" && ( -d "$RELEASE_CHANNEL_FILES_ROOT_DEFAULT" || "$RELEASE_CHANNEL_PATH" != "$DEFAULT_RELEASE_CHANNEL_PATH" ) ]]; then
  WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"
else
  WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEFAULT_WINDOWS_LOCAL_DESKTOP_FILES_ROOT"
fi
UI_LOCAL_RELEASE_PROOF_PATH="${CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json}"
BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH="${CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_SELF_HOST_WORKBENCH_PROOF.generated.json}"
BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH="${CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json}"
BLAZOR_BROWSER_LANE_PROOF_SET_PATH="${CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_BROWSER_LANE_PROOF_SET.generated.json}"
UI_FLAGSHIP_RELEASE_GATE_PATH="${CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
DESKTOP_WORKFLOW_EXECUTION_GATE_PATH="${CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH:-$REPO_ROOT/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json}"
UI_WORKFLOW_PARITY_PATH="${CHUMMER_UI_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/CHUMMER5A_DESKTOP_WORKFLOW_PARITY.generated.json}"
SR4_WORKFLOW_PARITY_PATH="${CHUMMER_SR4_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/SR4_DESKTOP_WORKFLOW_PARITY.generated.json}"
SR6_WORKFLOW_PARITY_PATH="${CHUMMER_SR6_WORKFLOW_PARITY_PATH:-$REPO_ROOT/.codex-studio/published/SR6_DESKTOP_WORKFLOW_PARITY.generated.json}"
WINDOWS_INSTALLER_VISUAL_PROOF_PATH="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"

mkdir -p "$(dirname "$PROOF_PATH")"

python3 - "$PROOF_PATH" "$RELEASE_CHANNEL_PATH" "$WINDOWS_INSTALLER_PATH" "$WINDOWS_LOCAL_DESKTOP_FILES_ROOT" "$UI_LOCAL_RELEASE_PROOF_PATH" "$BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH" "$BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH" "$BLAZOR_BROWSER_LANE_PROOF_SET_PATH" "$UI_FLAGSHIP_RELEASE_GATE_PATH" "$DESKTOP_WORKFLOW_EXECUTION_GATE_PATH" "$UI_WORKFLOW_PARITY_PATH" "$SR4_WORKFLOW_PARITY_PATH" "$SR6_WORKFLOW_PARITY_PATH" "$WINDOWS_INSTALLER_VISUAL_PROOF_PATH" "$REPO_ROOT" "$HUB_REGISTRY_ROOT" "$APP_KEY" "$RID" <<'PY'
from __future__ import annotations

import hashlib
import json
import ntpath
import os
import platform
import re
import shutil
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List

PASSING_STARTUP_SMOKE_STATUSES = {"pass", "passed", "ready"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
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


def load_text(path: Path) -> str:
    if not path.is_file():
        return ""
    return path.read_text(encoding="utf-8-sig", errors="replace")


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


def extract_prefixed_line(text: str, prefix: str) -> str:
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if line.startswith(prefix):
            return line[len(prefix) :].strip()
    return ""


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
    if normalize_token(platform) == "windows":
        return "win" in normalized_host
    if normalize_token(platform) == "macos":
        return any(token in normalized_host for token in ("osx", "darwin", "macos"))
    if normalize_token(platform) == "linux":
        return "linux" in normalized_host
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def startup_smoke_channel_proves_release(
    startup_smoke_channel: str,
    release_channel_id: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    actual = normalize_token(startup_smoke_channel)
    expected = normalize_token(release_channel_id)
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not expected or not actual:
        return True
    if actual == expected:
        return True
    if expected in {"preview", "smoke", "local", "local_docker_preview"} and actual in {"docker", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    if expected == "docker" and actual in {"preview", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    return False


def startup_smoke_version_proves_release(
    startup_smoke_version: str,
    release_channel_version: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    version = str(startup_smoke_version or "").strip()
    release_version = str(release_channel_version or "").strip()
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not release_version:
        return True
    if expected_digest and startup_digest == expected_digest:
        return True
    if not version:
        return False
    if version == release_version:
        return True
    placeholder_version = version.lower().startswith("smoke-")
    return (
        placeholder_version
        and bool(expected_digest)
        and startup_digest == expected_digest
    )


def startup_smoke_stale_age_is_acceptable(
    *,
    host_supports_windows_smoke: bool,
    startup_smoke_age_seconds: int,
    max_age_seconds: int,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
    startup_smoke_version: str,
    release_channel_version: str,
) -> bool:
    if host_supports_windows_smoke:
        return False
    if startup_smoke_age_seconds <= max_age_seconds:
        return False
    if not expected_startup_smoke_digest:
        return False
    if normalize_token(startup_smoke_artifact_digest) != normalize_token(expected_startup_smoke_digest):
        return False
    return startup_smoke_version_proves_release(
        startup_smoke_version,
        release_channel_version,
        startup_smoke_artifact_digest,
        expected_startup_smoke_digest,
    )


def startup_smoke_is_incompatible_host_skip(payload: Dict[str, Any]) -> bool:
    if normalize_token(payload.get("status")) != "skipped":
        return False
    return (
        normalize_token(payload.get("verificationDisposition")) == "incompatible_host"
        or normalize_token(payload.get("skipClass")) == "incompatible_host"
    )


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


def _startup_smoke_candidate_timestamp(payload: Dict[str, Any], path: Path) -> float:
    for key in ("completedAtUtc", "recordedAtUtc", "generated_at", "generatedAt"):
        parsed = parse_iso_utc(payload.get(key))
        if parsed is not None:
            return parsed.timestamp()
    try:
        return path.stat().st_mtime
    except OSError:
        return 0.0


def select_startup_smoke_receipt(
    candidates: List[Path],
    *,
    expected_head: str,
    expected_platform: str,
    expected_rid: str,
    expected_channel: str,
    expected_digest: str,
) -> Path | None:
    best_path: Path | None = None
    best_score: tuple[int, int, int, int, int, int, int, float] | None = None
    normalized_expected_head = normalize_token(expected_head)
    normalized_expected_platform = normalize_token(expected_platform)
    normalized_expected_rid = normalize_token(expected_rid)
    normalized_expected_channel = normalize_token(expected_channel)
    normalized_expected_digest = normalize_token(expected_digest)

    for path in candidates:
        if not path.is_file():
            continue
        payload = load_json(path)
        status = normalize_token(payload.get("status"))
        checkpoint = normalize_token(payload.get("readyCheckpoint"))
        head = normalize_token(payload.get("headId"))
        platform_name = normalize_token(payload.get("platform"))
        rid = normalize_token(payload.get("rid"))
        channel = normalize_token(payload.get("channelId") or payload.get("channel"))
        digest = normalize_token(payload.get("artifactDigest"))
        timestamp = _startup_smoke_candidate_timestamp(payload, path)
        score = (
            int(head == normalized_expected_head),
            int(platform_name == normalized_expected_platform),
            int(rid == normalized_expected_rid),
            int(not normalized_expected_digest or digest == normalized_expected_digest),
            int(not normalized_expected_channel or channel == normalized_expected_channel),
            int(status in PASSING_STARTUP_SMOKE_STATUSES),
            int(checkpoint == "pre_ui_event_loop"),
            timestamp,
        )
        if best_score is None or score > best_score:
            best_score = score
            best_path = path

    return best_path


def path_is_within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def path_uses_legacy_chummer5a_root(path: Path) -> bool:
    normalized = str(path.resolve()).replace("\\", "/").lower()
    return "/chummer5a/" in normalized


def expected_bootstrap_payload_path(installer_path: Path) -> Path:
    name = installer_path.name
    if name.lower().endswith("-installer.exe"):
        return installer_path.with_name(name[:-len("-installer.exe")] + "-payload.zip")
    return installer_path.with_name(f"{installer_path.stem}-payload.zip")


def zip_contains_sample_character(payload_path: Path) -> bool:
    try:
        with zipfile.ZipFile(payload_path) as archive:
            names = {entry.filename.replace("\\", "/").lstrip("/") for entry in archive.infolist()}
            return "Samples/Legacy/Soma-Career.chum5" in names
    except (OSError, zipfile.BadZipFile):
        return False


def resolve_receipt_artifact_path(
    raw_candidates: List[str],
    repo_root: Path,
    downloads_roots: List[Path],
) -> tuple[str, List[str], Path | None]:
    candidate_paths: List[Path] = []
    for raw_value in raw_candidates:
        raw = str(raw_value or "").strip()
        if not raw:
            continue
        path = Path(raw).expanduser()
        if path.is_absolute():
            candidate_paths.append(path)
            continue
        for root in downloads_roots:
            candidate_paths.append(root / path)
        candidate_paths.append(repo_root / path)

    deduped_candidates = list(dict.fromkeys(candidate_paths))
    resolved = next((path for path in deduped_candidates if path.is_file()), None)
    return (
        str(next((value for value in raw_candidates if str(value or "").strip()), "")).strip(),
        [str(path) for path in deduped_candidates],
        resolved or (deduped_candidates[0] if deduped_candidates else None),
    )


def workflow_parity_is_external_only(path: Path, expected_contract: str, upstream_external_only: bool = False) -> bool:
    payload = load_json(path)
    contract = str(payload.get("contract_name") or payload.get("contractName") or "").strip()
    if contract != expected_contract:
        return False

    explicit_markers = [
        payload.get("failingParityReceiptsExternalOnly"),
        payload.get("failing_parity_receipts_external_only"),
        payload.get("externalOnly"),
        payload.get("external_only"),
    ]
    if any(str(marker).strip().lower() in {"1", "true", "yes"} for marker in explicit_markers):
        return True

    reasons = payload.get("reasons") if isinstance(payload.get("reasons"), list) else []
    normalized_reasons = [str(reason or "").strip() for reason in reasons if str(reason or "").strip()]
    if not normalized_reasons:
        return False

    filtered_reasons: List[str] = []
    for reason in normalized_reasons:
        if upstream_external_only and reason.startswith("SR4 desktop workflow parity must pass before SR6 carry-forward parity can close."):
            continue
        filtered_reasons.append(reason)
    if not filtered_reasons:
        return upstream_external_only

    external_marker = "external blocker: missing_api_surface_contract"
    return all(external_marker in reason for reason in filtered_reasons)


def status_is_passing(value: Any) -> bool:
    return normalize_token(value) in {"pass", "passed", "ready"}


def collect_installer_visual_screenshots(payload: Dict[str, Any]) -> Dict[str, Dict[str, Any]]:
    raw = payload.get("screenshots")
    rows: List[Dict[str, Any]] = []
    if isinstance(raw, list):
        rows.extend(item for item in raw if isinstance(item, dict))
    elif isinstance(raw, dict):
        for role, value in raw.items():
            if isinstance(value, dict):
                row = dict(value)
                row.setdefault("role", role)
                rows.append(row)

    captured = payload.get("capturedScreenshots")
    if isinstance(captured, list):
        rows.extend(item for item in captured if isinstance(item, dict))
    elif isinstance(captured, dict):
        for role, value in captured.items():
            if isinstance(value, dict):
                row = dict(value)
                row.setdefault("role", role)
                rows.append(row)

    result: Dict[str, Dict[str, Any]] = {}
    for row in rows:
        role = normalize_token(
            row.get("role")
            or row.get("surface")
            or row.get("stage")
            or row.get("name")
        )
        if role in {"splash", "installer_splash", "welcome"}:
            result["splash"] = row
        elif role in {"progress", "installer_progress", "install-progress", "installing"}:
            result["progress"] = row
        elif role in {"completion", "complete", "finished", "installer_completion"}:
            result["completion"] = row
    return result


def screenshot_digest(row: Dict[str, Any]) -> str:
    return normalize_token(
        row.get("sha256")
        or row.get("imageSha256")
        or row.get("screenshotSha256")
        or row.get("digest")
    )


def screenshot_path(row: Dict[str, Any]) -> str:
    return str(
        row.get("path")
        or row.get("imagePath")
        or row.get("screenshotPath")
        or row.get("file")
        or ""
    ).strip()


def resolve_visual_screenshot_file(
    raw_path: str,
    visual_proof_path: Path,
    repo_root: Path,
) -> tuple[List[str], Path | None]:
    raw = str(raw_path or "").strip()
    if not raw:
        return ([], None)

    path = Path(raw).expanduser()
    candidate_paths: List[Path] = []
    if path.is_absolute():
        candidate_paths.append(path)
    else:
        candidate_paths.append(visual_proof_path.parent / path)
        candidate_paths.append(repo_root / path)

    deduped_candidates = list(dict.fromkeys(candidate_paths))
    resolved = next((candidate for candidate in deduped_candidates if candidate.is_file()), None)
    return ([str(candidate) for candidate in deduped_candidates], resolved)


def nested_status(payload: Dict[str, Any], *keys: str) -> str:
    for key in keys:
        value = payload.get(key)
        if isinstance(value, dict):
            status = normalize_token(value.get("status"))
            if status:
                return status
        status = normalize_token(value)
        if status:
            return status
    return ""


def nested_reviewer(payload: Dict[str, Any], *keys: str) -> str:
    for key in keys:
        value = payload.get(key)
        if isinstance(value, dict):
            reviewer = normalize_token(value.get("reviewer"))
            if reviewer:
                return reviewer
    return ""


def reviewer_is_non_automated(reviewer: str) -> bool:
    normalized = normalize_token(reviewer)
    if not normalized:
        return False
    automated_markers = {
        "auto",
        "automated",
        "automation",
        "bot",
        "ci",
        "machine",
        "script",
        "scripted",
        "synthetic",
        "wine",
    }
    reviewer_tokens = {
        token
        for token in re.split(r"[^a-z0-9]+", normalized)
        if token
    }
    return reviewer_tokens.isdisjoint(automated_markers)


proof_path = Path(sys.argv[1])
release_channel_path = Path(sys.argv[2])
installer_path = Path(sys.argv[3])
windows_installer_path_override = Path(sys.argv[3]).expanduser() if str(sys.argv[3]).strip() else None
windows_local_desktop_files_root = Path(sys.argv[4])
ui_local_release_proof_path = Path(sys.argv[5])
blazor_self_host_workbench_proof_path = Path(sys.argv[6])
blazor_public_edge_workbench_proof_path = Path(sys.argv[7])
blazor_browser_lane_proof_set_path = Path(sys.argv[8])
ui_flagship_release_gate_path = Path(sys.argv[9])
desktop_workflow_execution_gate_path = Path(sys.argv[10])
ui_workflow_parity_path = Path(sys.argv[11])
sr4_workflow_parity_path = Path(sys.argv[12])
sr6_workflow_parity_path = Path(sys.argv[13])
windows_installer_visual_proof_path = Path(sys.argv[14])
repo_root = Path(sys.argv[15])
hub_registry_root_arg = str(sys.argv[16] or "").strip()
hub_registry_root = Path(hub_registry_root_arg).resolve() if hub_registry_root_arg else None
expected_head_override = normalize_token(sys.argv[17])
expected_rid_override = normalize_token(sys.argv[18])
host_os_name = platform.system().strip()
host_os_normalized = normalize_token(host_os_name)
host_supports_windows_smoke = bool(
    os.name == "nt"
    or shutil.which("wine")
    or shutil.which("wine64")
    or shutil.which("powershell.exe")
    or shutil.which("pwsh")
    or shutil.which("cygpath")
)

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "windows_installer_path_override": str(windows_installer_path_override) if windows_installer_path_override else "",
    "windows_local_desktop_files_root": str(windows_local_desktop_files_root),
    "ui_local_release_proof_path": str(ui_local_release_proof_path),
    "blazor_self_host_workbench_proof_path": str(blazor_self_host_workbench_proof_path),
    "blazor_public_edge_workbench_proof_path": str(blazor_public_edge_workbench_proof_path),
    "blazor_browser_lane_proof_set_path": str(blazor_browser_lane_proof_set_path),
    "ui_flagship_release_gate_path": str(ui_flagship_release_gate_path),
    "desktop_workflow_execution_gate_path": str(desktop_workflow_execution_gate_path),
    "ui_workflow_parity_path": str(ui_workflow_parity_path),
    "sr4_workflow_parity_path": str(sr4_workflow_parity_path),
    "sr6_workflow_parity_path": str(sr6_workflow_parity_path),
    "windows_installer_visual_proof_path": str(windows_installer_visual_proof_path),
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
desktop_tuple_coverage = release_channel.get("desktopTupleCoverage")
external_proof_requests = (
    desktop_tuple_coverage.get("externalProofRequests")
    if isinstance(desktop_tuple_coverage, dict)
    and isinstance(desktop_tuple_coverage.get("externalProofRequests"), list)
    else []
)
required_desktop_platforms = (
    [
        normalize_token(item)
        for item in (desktop_tuple_coverage.get("requiredDesktopPlatforms") or [])
        if normalize_token(item)
    ]
    if isinstance(desktop_tuple_coverage, dict)
    else []
)
expected_head = expected_head_override or "avalonia"
expected_rid = expected_rid_override or "win-x64"
expected_arch = "x64"
windows_artifact = None
fallback_external_request = None
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
    fallback_external_request = next(
        (
            request
            for request in external_proof_requests
            if isinstance(request, dict)
            and normalize_token(request.get("head")) == expected_head
            and normalize_token(request.get("platform")) == "windows"
            and normalize_token(request.get("rid")) == expected_rid
        ),
        None,
    )
    if fallback_external_request is not None:
        fallback_file_name = str(fallback_external_request.get("expectedInstallerFileName") or "").strip()
        fallback_sha = str(fallback_external_request.get("expectedInstallerSha256") or "").strip().lower()
        fallback_route = str(fallback_external_request.get("expectedPublicInstallRoute") or "").strip()
        fallback_artifact_id = str(fallback_external_request.get("expectedArtifactId") or "").strip()
        fallback_arch = expected_rid.split("-", 1)[1] if expected_rid.startswith("win-") and "-" in expected_rid else expected_arch
        windows_artifact = {
            "artifactId": fallback_artifact_id,
            "head": expected_head,
            "rid": expected_rid,
            "platform": "windows",
            "arch": fallback_arch,
            "kind": "installer",
            "fileName": fallback_file_name,
            "downloadUrl": fallback_route,
            "sha256": fallback_sha,
            "channelId": release_channel_id,
            "channel": release_channel_id,
            "version": release_channel_version,
            "releaseVersion": release_channel_version,
            "publicationSource": "desktopTupleCoverage.externalProofRequests",
        }
        evidence["release_channel_windows_external_proof_request"] = fallback_external_request

windows_platform_required = (
    "windows" in required_desktop_platforms
    or windows_artifact is not None
    or bool(windows_installer_path_override)
)
evidence["release_channel_required_desktop_platforms"] = required_desktop_platforms
evidence["windows_platform_required_for_release_channel"] = windows_platform_required

if not windows_platform_required:
    payload = {
        "contract_name": "chummer6-ui.windows_desktop_exit_gate",
        "generated_at": now_iso(),
        "channelId": release_channel_id,
        "releaseVersion": release_channel_version,
        "status": "passed" if release_channel_status == "published" and release_channel_version else "failed",
        "blockingMode": "none" if release_channel_status == "published" and release_channel_version else "mixed_or_local",
        "blocking_mode": "none" if release_channel_status == "published" and release_channel_version else "mixed_or_local",
        "reason": (
            "windows desktop exit gate is not required for this release channel"
            if release_channel_status == "published" and release_channel_version
            else "windows desktop exit gate checks failed"
        ),
        "summary": (
            "Windows desktop exit gate is not required for this release channel."
            if release_channel_status == "published" and release_channel_version
            else "Windows desktop exit gate failed: release channel is not published or is missing version."
        ),
        "reasons": (
            []
            if release_channel_status == "published" and release_channel_version
            else reasons
        ),
        "head": {
            "app_key": expected_head,
            "platform": "windows",
            "rid": expected_rid,
            "version": release_channel_version,
            "channelId": release_channel_id,
        },
        "checks": {
            **evidence,
            "windows_installer_visual_proof_found": False,
            "startup_smoke_receipt_found": False,
            "startup_smoke_external_blocker": "",
            "windows_installer_not_required_for_release_channel": True,
        },
    }
    proof_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    if payload["status"] == "passed":
        print("[windows-exit-gate] PASS not required for current release channel")
        raise SystemExit(0)
    print("[windows-exit-gate] FAIL release channel basics invalid", file=sys.stderr)
    raise SystemExit(1)

if windows_artifact is None:
    reasons.append(
        f"Release channel does not publish a promoted Windows install medium artifact for {expected_head} ({expected_rid})."
    )
    artifact_file_name = ""
    artifact_size = 0
    artifact_sha = ""
    artifact_installer_mode = ""
    artifact_payload_file_name = ""
    artifact_payload_sha256 = ""
    artifact_payload_size_bytes = 0
else:
    expected_head = normalize_token(windows_artifact.get("head")) or expected_head
    expected_rid = artifact_rid(windows_artifact) or expected_rid
    if expected_rid.startswith("win-") and len(expected_rid) > 4:
        expected_arch = expected_rid.split("-", 1)[1]
    artifact_file_name = str(windows_artifact.get("fileName") or "").strip()
    artifact_size = int(windows_artifact.get("sizeBytes") or 0)
    artifact_sha = str(windows_artifact.get("sha256") or "").strip().lower()
    artifact_installer_mode = normalize_token(windows_artifact.get("installerMode"))
    artifact_payload_file_name = str(windows_artifact.get("payloadFileName") or "").strip()
    artifact_payload_sha256 = normalize_token(windows_artifact.get("payloadSha256"))
    artifact_payload_size_bytes = int(windows_artifact.get("payloadSizeBytes") or 0)

default_file_name = artifact_file_name or f"chummer-{expected_head}-{expected_rid}-installer.exe"
primary_shelf_root = Path(os.path.abspath(str(windows_local_desktop_files_root)))
primary_shelf_candidates = [
    Path(os.path.abspath(str(windows_local_desktop_files_root / default_file_name))),
    primary_shelf_root / default_file_name,
]
override_candidates = []
if windows_installer_path_override:
    override_candidates.append(Path(os.path.abspath(str(windows_installer_path_override.expanduser()))))
installer_candidates = primary_shelf_candidates + override_candidates
installer_candidates = list(dict.fromkeys(installer_candidates))
installer_path = next((path for path in installer_candidates if path.is_file()), installer_candidates[0])

installer_exists = installer_path.is_file()
installer_size = installer_path.stat().st_size if installer_exists else 0
installer_sha = sha256_file(installer_path) if installer_exists else ""
expected_installer_digest = f"sha256:{installer_sha}" if installer_sha else ""
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
if artifact_installer_mode:
    evidence["expected_windows_installer_mode"] = artifact_installer_mode
if artifact_payload_file_name:
    evidence["expected_windows_payload_file_name"] = artifact_payload_file_name
if artifact_payload_sha256:
    evidence["expected_windows_payload_sha256"] = artifact_payload_sha256
if artifact_payload_size_bytes:
    evidence["expected_windows_payload_size_bytes"] = artifact_payload_size_bytes
installer_from_primary_shelf = path_is_within(installer_path, primary_shelf_root)
evidence["windows_installer_primary_shelf_root"] = str(primary_shelf_root)
evidence["windows_installer_from_primary_shelf"] = installer_from_primary_shelf
if windows_installer_path_override and installer_from_primary_shelf:
    evidence["windows_installer_override_ignored_for_promoted_shelf"] = True
if not installer_from_primary_shelf:
    reasons.append(
        "Promoted Windows installer was not resolved from the release-aligned desktop shelf."
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

if windows_artifact is not None and str(windows_artifact.get("publicationSource") or "").strip() == "desktopTupleCoverage.externalProofRequests":
    if installer_exists and not int(windows_artifact.get("sizeBytes") or 0):
        windows_artifact["sizeBytes"] = installer_size
    fallback_generated_at = str(
        windows_artifact.get("generated_at")
        or windows_artifact.get("generatedAt")
        or release_channel.get("generated_at")
        or release_channel.get("generatedAt")
        or ""
    ).strip()
    if fallback_generated_at:
        windows_artifact["generated_at"] = fallback_generated_at
        windows_artifact["generatedAt"] = fallback_generated_at
    if not str(windows_artifact.get("id") or "").strip():
        windows_artifact["id"] = str(windows_artifact.get("artifactId") or "").strip()

if windows_artifact is not None:
    evidence["release_channel_windows_artifact"] = windows_artifact

payload_marker_present = False
appended_payload_marker_present = False
sample_marker_present = False
bootstrap_payload_path = expected_bootstrap_payload_path(installer_path)
bootstrap_payload_exists = bootstrap_payload_path.is_file()
bootstrap_payload_sample_marker_present = False
if installer_exists:
    blob = installer_path.read_bytes()
    payload_marker_present = b"ChummerInstaller.Payload.zip" in blob
    appended_payload_marker_present = b"CHUMMER6PAYLOAD1" in blob
    sample_marker_present = b"Samples/Legacy/Soma-Career.chum5" in blob
if bootstrap_payload_exists:
    bootstrap_payload_sample_marker_present = zip_contains_sample_character(bootstrap_payload_path)
evidence["embedded_payload_marker_present"] = payload_marker_present
evidence["appended_payload_marker_present"] = appended_payload_marker_present
evidence["embedded_sample_marker_present"] = sample_marker_present
evidence["bootstrap_payload_path"] = str(bootstrap_payload_path)
evidence["bootstrap_payload_exists"] = bootstrap_payload_exists
evidence["bootstrap_payload_sample_marker_present"] = bootstrap_payload_sample_marker_present
evidence["installer_payload_validation_mode"] = "release-channel digest-size-and-payload-markers-or-bootstrap-sidecar"

has_recognizable_payload = payload_marker_present or appended_payload_marker_present or bootstrap_payload_exists
has_sample_marker = sample_marker_present or bootstrap_payload_sample_marker_present

if installer_exists and not has_recognizable_payload:
    reasons.append("Published Windows installer is missing a recognizable desktop payload marker.")
if installer_exists and not has_sample_marker:
    reasons.append("Published Windows installer is missing the bundled sample-character marker.")

visual_proof_payload = load_json(windows_installer_visual_proof_path)
visual_proof_contract = str(
    visual_proof_payload.get("contract_name")
    or visual_proof_payload.get("contractName")
    or ""
).strip()
visual_proof_status = normalize_token(visual_proof_payload.get("status"))
visual_proof_head = normalize_token(visual_proof_payload.get("headId") or visual_proof_payload.get("head"))
visual_proof_rid = normalize_token(visual_proof_payload.get("rid"))
visual_proof_version = str(
    visual_proof_payload.get("version")
    or visual_proof_payload.get("releaseVersion")
    or ""
).strip()
visual_proof_digest = normalize_token(
    visual_proof_payload.get("artifactDigest")
    or visual_proof_payload.get("installerDigest")
    or visual_proof_payload.get("installerSha256")
)
if visual_proof_digest and not visual_proof_digest.startswith("sha256:"):
    visual_proof_digest = f"sha256:{visual_proof_digest}"
visual_screenshots = collect_installer_visual_screenshots(visual_proof_payload)
visual_screenshot_digests = {
    role: screenshot_digest(row)
    for role, row in visual_screenshots.items()
}
visual_screenshot_paths = {
    role: screenshot_path(row)
    for role, row in visual_screenshots.items()
}
visual_screenshot_candidate_paths: Dict[str, List[str]] = {}
visual_screenshot_resolved_paths: Dict[str, str] = {}
visual_screenshot_file_exists: Dict[str, bool] = {}
visual_screenshot_actual_digests: Dict[str, str] = {}
for role, raw_path in visual_screenshot_paths.items():
    candidate_paths, resolved_path = resolve_visual_screenshot_file(
        raw_path,
        windows_installer_visual_proof_path,
        repo_root,
    )
    visual_screenshot_candidate_paths[role] = candidate_paths
    visual_screenshot_resolved_paths[role] = str(resolved_path) if resolved_path is not None else ""
    visual_screenshot_file_exists[role] = resolved_path is not None and resolved_path.is_file()
    visual_screenshot_actual_digests[role] = sha256_file(resolved_path) if resolved_path is not None and resolved_path.is_file() else ""
visual_required_roles = ["progress", "completion"]
visual_missing_roles = [
    role for role in visual_required_roles if role not in visual_screenshots
]
visual_roles_missing_files = [
    role
    for role in visual_required_roles
    if role in visual_screenshots and not visual_screenshot_file_exists.get(role)
]
visual_roles_missing_digests = [
    role for role in visual_required_roles if role in visual_screenshots and not visual_screenshot_digests.get(role)
]
visual_roles_digest_mismatch = [
    role
    for role in visual_required_roles
    if (
        role in visual_screenshots
        and visual_screenshot_file_exists.get(role)
        and visual_screenshot_digests.get(role)
        and visual_screenshot_actual_digests.get(role)
        and visual_screenshot_digests.get(role) != visual_screenshot_actual_digests.get(role)
    )
]
visual_unique_digest_count = len(
    {digest for digest in visual_screenshot_digests.values() if digest}
)
visual_actual_unique_digest_count = len(
    {
        digest
        for role, digest in visual_screenshot_actual_digests.items()
        if role in visual_required_roles and digest
    }
)
visual_readability_status = nested_status(
    visual_proof_payload,
    "readabilityReview",
    "textReadabilityReview",
    "readability",
)
visual_contrast_status = nested_status(
    visual_proof_payload,
    "contrastReview",
    "contrast",
)
visual_clipping_status = nested_status(
    visual_proof_payload,
    "clippingReview",
    "clipping",
)
visual_checks = (
    visual_proof_payload.get("checks")
    if isinstance(visual_proof_payload.get("checks"), dict)
    else {}
)
visual_capture_mode = normalize_token(visual_checks.get("capture_mode"))
visual_human_review_confirmed = visual_checks.get("human_review_confirmed") is True
visual_reviewers = {
    "readability": nested_reviewer(
        visual_proof_payload,
        "readabilityReview",
        "textReadabilityReview",
        "readability",
    ),
    "contrast": nested_reviewer(
        visual_proof_payload,
        "contrastReview",
        "contrast",
    ),
    "clipping": nested_reviewer(
        visual_proof_payload,
        "clippingReview",
        "clipping",
    ),
}
visual_reviewers_non_automated = {
    review_name: reviewer_is_non_automated(reviewer)
    for review_name, reviewer in visual_reviewers.items()
}
visual_invalid_reviewers = [
    review_name
    for review_name, is_non_automated in visual_reviewers_non_automated.items()
    if not is_non_automated
]
evidence["windows_installer_visual_proof_found"] = windows_installer_visual_proof_path.is_file()
evidence["windows_installer_visual_proof_contract"] = visual_proof_contract
evidence["windows_installer_visual_proof_status"] = visual_proof_status
evidence["windows_installer_visual_proof_head"] = visual_proof_head
evidence["windows_installer_visual_proof_rid"] = visual_proof_rid
evidence["windows_installer_visual_proof_version"] = visual_proof_version
evidence["windows_installer_visual_proof_artifact_digest"] = visual_proof_digest
evidence["windows_installer_visual_required_roles"] = visual_required_roles
evidence["windows_installer_visual_screenshot_paths"] = visual_screenshot_paths
evidence["windows_installer_visual_screenshot_candidate_paths"] = visual_screenshot_candidate_paths
evidence["windows_installer_visual_screenshot_resolved_paths"] = visual_screenshot_resolved_paths
evidence["windows_installer_visual_screenshot_file_exists"] = visual_screenshot_file_exists
evidence["windows_installer_visual_screenshot_digests"] = visual_screenshot_digests
evidence["windows_installer_visual_screenshot_actual_digests"] = visual_screenshot_actual_digests
evidence["windows_installer_visual_missing_roles"] = visual_missing_roles
evidence["windows_installer_visual_roles_missing_files"] = visual_roles_missing_files
evidence["windows_installer_visual_roles_missing_digests"] = visual_roles_missing_digests
evidence["windows_installer_visual_roles_digest_mismatch"] = visual_roles_digest_mismatch
evidence["windows_installer_visual_unique_digest_count"] = visual_unique_digest_count
evidence["windows_installer_visual_actual_unique_digest_count"] = visual_actual_unique_digest_count
evidence["windows_installer_visual_readability_status"] = visual_readability_status
evidence["windows_installer_visual_contrast_status"] = visual_contrast_status
evidence["windows_installer_visual_clipping_status"] = visual_clipping_status
evidence["windows_installer_visual_capture_mode"] = visual_capture_mode
evidence["windows_installer_visual_human_review_confirmed"] = visual_human_review_confirmed
evidence["windows_installer_visual_reviewers"] = visual_reviewers
evidence["windows_installer_visual_reviewers_non_automated"] = visual_reviewers_non_automated
evidence["windows_installer_visual_invalid_reviewers"] = visual_invalid_reviewers
windows_visual_proof_external_blocker = (
    "missing_windows_visual_proof_capture"
    if not windows_installer_visual_proof_path.is_file()
    else ""
)
evidence["windows_visual_proof_external_blocker"] = windows_visual_proof_external_blocker

if not windows_installer_visual_proof_path.is_file():
    reasons.append(
        "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host."
    )
elif visual_proof_contract != "chummer6-ui.windows_installer_visual_proof":
    reasons.append("Windows installer visual proof contract is not chummer6-ui.windows_installer_visual_proof.")
elif not status_is_passing(visual_proof_status):
    reasons.append("Windows installer visual proof status is not passing.")
if windows_installer_visual_proof_path.is_file() and visual_proof_head and visual_proof_head != expected_head:
    reasons.append(f"Windows installer visual proof head does not match promoted head {expected_head}.")
if windows_installer_visual_proof_path.is_file() and visual_proof_rid and visual_proof_rid != expected_rid:
    reasons.append(f"Windows installer visual proof rid does not match promoted RID {expected_rid}.")
if (
    windows_installer_visual_proof_path.is_file()
    and release_channel_version
    and visual_proof_version
    and visual_proof_version != release_channel_version
):
    reasons.append("Windows installer visual proof version does not match release channel.")
if (
    windows_installer_visual_proof_path.is_file()
    and expected_installer_digest
    and visual_proof_digest != expected_installer_digest
):
    reasons.append("Windows installer visual proof artifactDigest does not match promoted installer bytes.")
if windows_installer_visual_proof_path.is_file() and visual_missing_roles:
    reasons.append(
        "Windows installer visual proof is missing required screenshot roles: "
        + ", ".join(visual_missing_roles)
        + "."
    )
if windows_installer_visual_proof_path.is_file() and visual_roles_missing_files:
    reasons.append(
        "Windows installer visual proof screenshot files are missing for: "
        + ", ".join(visual_roles_missing_files)
        + "."
    )
if windows_installer_visual_proof_path.is_file() and visual_roles_missing_digests:
    reasons.append(
        "Windows installer visual proof screenshots are missing image digests for: "
        + ", ".join(visual_roles_missing_digests)
        + "."
    )
if windows_installer_visual_proof_path.is_file() and visual_roles_digest_mismatch:
    reasons.append(
        "Windows installer visual proof screenshot digests do not match the referenced files for: "
        + ", ".join(visual_roles_digest_mismatch)
        + "."
    )
if (
    windows_installer_visual_proof_path.is_file()
    and not visual_missing_roles
    and not visual_roles_missing_files
    and not visual_roles_missing_digests
    and not visual_roles_digest_mismatch
    and visual_actual_unique_digest_count < len(visual_required_roles)
):
    reasons.append("Windows installer visual proof screenshots are not distinct across progress and completion.")
for review_name, review_status in (
    ("readability", visual_readability_status),
    ("contrast", visual_contrast_status),
    ("clipping", visual_clipping_status),
):
    if windows_installer_visual_proof_path.is_file() and not status_is_passing(review_status):
        reasons.append(f"Windows installer visual proof {review_name} review is not passing.")
if windows_installer_visual_proof_path.is_file() and visual_capture_mode != "interactive":
    reasons.append("Windows installer visual proof capture_mode is not interactive.")
if windows_installer_visual_proof_path.is_file() and not visual_human_review_confirmed:
    reasons.append("Windows installer visual proof human_review_confirmed is not true.")
if windows_installer_visual_proof_path.is_file() and visual_invalid_reviewers:
    reasons.append(
        "Windows installer visual proof reviewers are missing or automated for: "
        + ", ".join(visual_invalid_reviewers)
        + "."
    )

startup_smoke_receipt_override = os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_RECEIPT_PATH", "").strip()
if startup_smoke_receipt_override:
    startup_smoke_receipt_path = Path(startup_smoke_receipt_override).resolve()
    startup_smoke_candidates = [startup_smoke_receipt_path]
else:
    startup_smoke_receipt_name = f"startup-smoke-{expected_head}-{expected_rid}.receipt.json"
    startup_smoke_candidates = [
        repo_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_receipt_name,
        release_channel_path.parent / "startup-smoke" / startup_smoke_receipt_name,
        release_channel_path.parent.parent / "startup-smoke" / startup_smoke_receipt_name,
        proof_path.parent / "startup-smoke" / startup_smoke_receipt_name,
        repo_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_receipt_name,
    ]
    if hub_registry_root is not None:
        startup_smoke_candidates.extend(
            [
                hub_registry_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_receipt_name,
                hub_registry_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_receipt_name,
            ]
        )
    startup_smoke_receipt_path = select_startup_smoke_receipt(
        startup_smoke_candidates,
        expected_head=expected_head,
        expected_platform="windows",
        expected_rid=expected_rid,
        expected_channel=release_channel_id,
        expected_digest=f"sha256:{installer_sha}" if installer_sha else "",
    ) or startup_smoke_candidates[0]

startup_smoke_payload = load_json(startup_smoke_receipt_path)
evidence["startup_smoke_receipt_path"] = str(startup_smoke_receipt_path)
evidence["startup_smoke_receipt_candidates"] = [str(path) for path in startup_smoke_candidates]
evidence["startup_smoke_receipt_found"] = startup_smoke_receipt_path.is_file()
startup_smoke_progress_log_override = os.environ.get("CHUMMER_WINDOWS_STARTUP_SMOKE_PROGRESS_LOG_PATH", "").strip()
startup_smoke_progress_log_name = f"windows-installer-progress-{expected_head}-{expected_rid}.log"
if startup_smoke_progress_log_override:
    startup_smoke_progress_log_path = Path(startup_smoke_progress_log_override).resolve()
    startup_smoke_progress_log_candidates = [startup_smoke_progress_log_path]
else:
    startup_smoke_progress_log_candidates = []
    if startup_smoke_receipt_path.parent:
        startup_smoke_progress_log_candidates.append(startup_smoke_receipt_path.parent / startup_smoke_progress_log_name)
    startup_smoke_progress_log_candidates.extend(
        [
            repo_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_progress_log_name,
            release_channel_path.parent / "startup-smoke" / startup_smoke_progress_log_name,
            release_channel_path.parent.parent / "startup-smoke" / startup_smoke_progress_log_name,
            proof_path.parent / "startup-smoke" / startup_smoke_progress_log_name,
            repo_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_progress_log_name,
        ]
    )
    if hub_registry_root is not None:
        startup_smoke_progress_log_candidates.extend(
            [
                hub_registry_root / ".codex-studio" / "published" / "startup-smoke" / startup_smoke_progress_log_name,
                hub_registry_root / "Docker" / "Downloads" / "startup-smoke" / startup_smoke_progress_log_name,
            ]
        )
    startup_smoke_progress_log_path = next(
        (candidate for candidate in startup_smoke_progress_log_candidates if candidate.is_file()),
        startup_smoke_progress_log_candidates[0],
    )
evidence["startup_smoke_progress_log_path"] = str(startup_smoke_progress_log_path)
evidence["startup_smoke_progress_log_candidates"] = [str(path) for path in startup_smoke_progress_log_candidates]
evidence["startup_smoke_progress_log_found"] = startup_smoke_progress_log_path.is_file()
startup_smoke_incompatible_host_skip = (
    startup_smoke_receipt_path.is_file()
    and startup_smoke_is_incompatible_host_skip(startup_smoke_payload)
)
startup_smoke_incompatible_host_skip_accepted = bool(
    startup_smoke_incompatible_host_skip
    and not host_supports_windows_smoke
)
evidence["startup_smoke_external_blocker"] = (
    "missing_windows_host_capability"
    if (
        (
            not startup_smoke_receipt_path.is_file()
            or startup_smoke_incompatible_host_skip
        )
        and not host_supports_windows_smoke
    )
    else ""
)
evidence["startup_smoke_incompatible_host_skip"] = startup_smoke_incompatible_host_skip
evidence["startup_smoke_incompatible_host_skip_accepted"] = startup_smoke_incompatible_host_skip_accepted
evidence["startup_smoke_skip_class"] = normalize_token(startup_smoke_payload.get("skipClass"))
evidence["startup_smoke_verification_disposition"] = normalize_token(
    startup_smoke_payload.get("verificationDisposition")
)
evidence["startup_smoke_skip_reason"] = str(startup_smoke_payload.get("skipReason") or "").strip()

startup_smoke_status = normalize_token(startup_smoke_payload.get("status"))
evidence["startup_smoke_status"] = startup_smoke_status
if not startup_smoke_receipt_path.is_file():
    reasons.append("Windows startup smoke receipt is missing for promoted installer bytes.")
    if not host_supports_windows_smoke:
        reasons.append(
            "Windows startup smoke requires a Windows-capable host; current host cannot run promoted Windows installer smoke."
        )
elif startup_smoke_incompatible_host_skip:
    if host_supports_windows_smoke:
        reasons.append("Windows startup smoke receipt is an incompatible-host skip on a Windows-capable host.")
    else:
        evidence["startup_smoke_incompatible_host_skip_accepted_reason"] = (
            "Rolling-release publication accepts this as an explicit incompatible-host boundary after matching "
            "the skipped receipt to the exact promoted Windows installer bytes, channel, version, head, RID, and arch."
        )
elif startup_smoke_status not in PASSING_STARTUP_SMOKE_STATUSES:
    reasons.append("Windows startup smoke receipt status is not passing.")

if startup_smoke_receipt_path.is_file() and path_uses_legacy_chummer5a_root(startup_smoke_receipt_path):
    reasons.append("Windows startup smoke receipt was resolved from a legacy chummer5a path.")

startup_smoke_checkpoint = normalize_token(startup_smoke_payload.get("readyCheckpoint"))
evidence["startup_smoke_ready_checkpoint"] = startup_smoke_checkpoint
startup_smoke_bootstrap_payload_mode = normalize_token(startup_smoke_payload.get("bootstrapPayloadAcquisitionMode"))
startup_smoke_bootstrap_payload_file_name = str(startup_smoke_payload.get("bootstrapPayloadFileName") or "").strip()
startup_smoke_bootstrap_payload_sha256 = normalize_token(startup_smoke_payload.get("bootstrapPayloadSha256"))
startup_smoke_bootstrap_payload_size_bytes = startup_smoke_payload.get("bootstrapPayloadSizeBytes") or 0
evidence["startup_smoke_bootstrap_payload_acquisition_mode"] = startup_smoke_bootstrap_payload_mode
evidence["startup_smoke_bootstrap_payload_file_name"] = startup_smoke_bootstrap_payload_file_name
evidence["startup_smoke_bootstrap_payload_sha256"] = startup_smoke_bootstrap_payload_sha256
evidence["startup_smoke_bootstrap_payload_size_bytes"] = startup_smoke_bootstrap_payload_size_bytes
startup_smoke_progress_log_text = load_text(startup_smoke_progress_log_path)
startup_smoke_progress_required_markers = [
    "Bootstrap temp root:",
    "Payload download target:",
    "Downloading application files",
    "Verifying payload size",
    "Verifying payload checksum",
    "Extracting application files",
    "Install complete",
]
startup_smoke_progress_markers_missing = [
    marker for marker in startup_smoke_progress_required_markers if marker not in startup_smoke_progress_log_text
]
startup_smoke_bootstrap_temp_root = extract_prefixed_line(
    startup_smoke_progress_log_text,
    "Bootstrap temp root:",
)
startup_smoke_payload_target = extract_prefixed_line(
    startup_smoke_progress_log_text,
    "Payload download target:",
)
startup_smoke_bootstrap_temp_root_normalized = startup_smoke_bootstrap_temp_root.replace("/", "\\").lower()
startup_smoke_payload_target_normalized = startup_smoke_payload_target.replace("/", "\\").lower()
startup_smoke_bootstrap_temp_root_contract_ok = startup_smoke_bootstrap_temp_root_normalized.endswith(
    "\\chummer6\\installer-temp"
)
startup_smoke_payload_target_root_level = startup_smoke_payload_target_normalized.startswith("\\")
startup_smoke_payload_target_uses_bootstrap_root = bool(
    startup_smoke_bootstrap_temp_root_normalized
    and startup_smoke_payload_target_normalized
    and startup_smoke_payload_target_normalized.startswith(startup_smoke_bootstrap_temp_root_normalized + "\\")
)
startup_smoke_payload_target_file_name = ntpath.basename(startup_smoke_payload_target_normalized)
evidence["startup_smoke_progress_log_markers_missing"] = startup_smoke_progress_markers_missing
evidence["startup_smoke_bootstrap_temp_root"] = startup_smoke_bootstrap_temp_root
evidence["startup_smoke_payload_download_target"] = startup_smoke_payload_target
evidence["startup_smoke_bootstrap_temp_root_contract_ok"] = startup_smoke_bootstrap_temp_root_contract_ok
evidence["startup_smoke_payload_target_root_level"] = startup_smoke_payload_target_root_level
evidence["startup_smoke_payload_target_uses_bootstrap_root"] = startup_smoke_payload_target_uses_bootstrap_root
evidence["startup_smoke_payload_target_file_name"] = startup_smoke_payload_target_file_name
if (
    startup_smoke_receipt_path.is_file()
    and not startup_smoke_incompatible_host_skip
    and startup_smoke_checkpoint != "pre_ui_event_loop"
):
    reasons.append("Windows startup smoke receipt readyCheckpoint is not pre_ui_event_loop.")

startup_smoke_digest = normalize_token(startup_smoke_payload.get("artifactDigest"))
evidence["startup_smoke_artifact_digest"] = startup_smoke_digest
evidence["expected_startup_smoke_artifact_digest"] = expected_installer_digest
if startup_smoke_receipt_path.is_file() and installer_exists and expected_installer_digest and startup_smoke_digest != expected_installer_digest:
    reasons.append("Windows startup smoke receipt artifactDigest does not match promoted installer bytes.")

startup_smoke_head = normalize_token(startup_smoke_payload.get("headId"))
startup_smoke_platform = normalize_token(startup_smoke_payload.get("platform"))
startup_smoke_arch = normalize_token(startup_smoke_payload.get("arch"))
startup_smoke_rid = normalize_token(startup_smoke_payload.get("rid"))
startup_smoke_channel = normalize_token(startup_smoke_payload.get("channelId") or startup_smoke_payload.get("channel"))
startup_smoke_version = str(
    startup_smoke_payload.get("version")
    or startup_smoke_payload.get("releaseVersion")
    or ""
).strip()
startup_smoke_host_class = normalize_token(startup_smoke_payload.get("hostClass"))
startup_smoke_operating_system = str(startup_smoke_payload.get("operatingSystem") or "").strip()
startup_smoke_artifact_path, startup_smoke_artifact_path_candidates, startup_smoke_artifact_path_obj = resolve_receipt_artifact_path(
    [
        startup_smoke_payload.get("artifactRelativePath"),
        startup_smoke_payload.get("artifactPath"),
    ],
    repo_root,
    [
        primary_shelf_root.parent,
        release_channel_path.parent,
        release_channel_path.parent.parent,
    ],
)
evidence["startup_smoke_head"] = startup_smoke_head
evidence["startup_smoke_platform"] = startup_smoke_platform
evidence["startup_smoke_arch"] = startup_smoke_arch
evidence["startup_smoke_rid"] = startup_smoke_rid
evidence["startup_smoke_channel"] = startup_smoke_channel
evidence["startup_smoke_version"] = startup_smoke_version
evidence["startup_smoke_host_class"] = startup_smoke_host_class
evidence["startup_smoke_operating_system"] = startup_smoke_operating_system
evidence["startup_smoke_artifact_path"] = startup_smoke_artifact_path
evidence["startup_smoke_artifact_path_candidates"] = startup_smoke_artifact_path_candidates
if startup_smoke_receipt_path.is_file() and startup_smoke_head != expected_head:
    reasons.append(f"Windows startup smoke receipt headId does not match promoted head {expected_head}.")
if startup_smoke_receipt_path.is_file() and startup_smoke_platform != "windows":
    reasons.append("Windows startup smoke receipt platform is not windows.")
if (
    startup_smoke_receipt_path.is_file()
    and not startup_smoke_incompatible_host_skip
    and not startup_smoke_host_class
):
    reasons.append("Windows startup smoke receipt hostClass is missing.")
if (
    startup_smoke_receipt_path.is_file()
    and not startup_smoke_incompatible_host_skip
    and startup_smoke_host_class
    and not host_class_matches_platform(startup_smoke_host_class, "windows")
):
    reasons.append("Windows startup smoke receipt hostClass does not identify a Windows host.")
if (
    startup_smoke_receipt_path.is_file()
    and not startup_smoke_incompatible_host_skip
    and not startup_smoke_operating_system
):
    reasons.append("Windows startup smoke receipt operatingSystem is missing.")
if startup_smoke_receipt_path.is_file() and startup_smoke_arch != expected_arch:
    reasons.append(f"Windows startup smoke receipt arch does not match promoted RID {expected_rid}.")
if startup_smoke_receipt_path.is_file() and not startup_smoke_rid:
    reasons.append("Windows startup smoke receipt rid is missing.")
if startup_smoke_receipt_path.is_file() and startup_smoke_rid and startup_smoke_rid != expected_rid:
    reasons.append(f"Windows startup smoke receipt rid does not match promoted RID {expected_rid}.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and not startup_smoke_progress_log_path.is_file()
):
    reasons.append("Windows bootstrap startup smoke progress log is missing for promoted installer bytes.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and startup_smoke_progress_log_path.is_file()
    and startup_smoke_progress_markers_missing
):
    reasons.append(
        "Windows bootstrap startup smoke progress log is missing required markers: "
        + ", ".join(startup_smoke_progress_markers_missing)
        + "."
    )
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and startup_smoke_progress_log_path.is_file()
    and not startup_smoke_bootstrap_temp_root_contract_ok
):
    reasons.append("Windows bootstrap startup smoke progress log does not prove the Chummer6 installer-temp workspace contract.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and startup_smoke_progress_log_path.is_file()
    and startup_smoke_payload_target_root_level
):
    reasons.append("Windows bootstrap startup smoke progress log captured a root-level payload target.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and startup_smoke_progress_log_path.is_file()
    and startup_smoke_payload_target
    and not startup_smoke_payload_target_root_level
    and not startup_smoke_payload_target_uses_bootstrap_root
):
    reasons.append("Windows bootstrap startup smoke progress log payload target is outside the bootstrap temp root.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and artifact_payload_file_name
    and startup_smoke_progress_log_path.is_file()
    and startup_smoke_payload_target_file_name
    and startup_smoke_payload_target_file_name != artifact_payload_file_name.lower()
):
    reasons.append("Windows bootstrap startup smoke progress log payload target file name does not match release-channel metadata.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and not startup_smoke_incompatible_host_skip
    and startup_smoke_bootstrap_payload_mode != "download"
):
    reasons.append("Windows startup smoke receipt did not exercise bootstrap payload download mode.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and artifact_payload_file_name
    and startup_smoke_bootstrap_payload_file_name != artifact_payload_file_name
):
    reasons.append("Windows startup smoke receipt bootstrap payload file name does not match release-channel metadata.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and artifact_payload_sha256
    and startup_smoke_bootstrap_payload_sha256 != artifact_payload_sha256
):
    reasons.append("Windows startup smoke receipt bootstrap payload SHA-256 does not match release-channel metadata.")
if (
    startup_smoke_receipt_path.is_file()
    and artifact_installer_mode == "bootstrap"
    and artifact_payload_size_bytes > 0
    and int(startup_smoke_bootstrap_payload_size_bytes or 0) != artifact_payload_size_bytes
):
    reasons.append("Windows startup smoke receipt bootstrap payload size does not match release-channel metadata.")
if (
    startup_smoke_receipt_path.is_file()
    and release_channel_id
    and not startup_smoke_channel_proves_release(
        startup_smoke_channel,
        release_channel_id,
        evidence.get("startup_smoke_artifact_digest", ""),
        evidence.get("expected_startup_smoke_artifact_digest", ""),
    )
):
    reasons.append(f"Windows startup smoke receipt channelId does not match release channel {release_channel_id}.")
if startup_smoke_receipt_path.is_file() and release_channel_version and not startup_smoke_version:
    reasons.append("Windows startup smoke receipt version is missing.")
if (
    startup_smoke_receipt_path.is_file()
    and release_channel_version
    and startup_smoke_version
    and not startup_smoke_version_proves_release(
        startup_smoke_version,
        release_channel_version,
        evidence.get("startup_smoke_artifact_digest", ""),
        evidence.get("expected_startup_smoke_artifact_digest", ""),
    )
):
    reasons.append(f"Windows startup smoke receipt version does not match release channel {release_channel_version}.")
startup_smoke_digest_matches_expected = bool(
    expected_installer_digest and startup_smoke_digest == expected_installer_digest
)
evidence["startup_smoke_digest_matches_expected"] = startup_smoke_digest_matches_expected
if (
    startup_smoke_receipt_path.is_file()
    and not startup_smoke_artifact_path
    and not startup_smoke_digest_matches_expected
):
    reasons.append("Windows startup smoke receipt artifactPath is missing.")
if (
    startup_smoke_receipt_path.is_file()
    and startup_smoke_artifact_path
    and not startup_smoke_digest_matches_expected
):
    if startup_smoke_artifact_path_obj is None:
        reasons.append("Windows startup smoke receipt artifactPath could not be resolved for promoted shelf verification.")
    elif path_uses_legacy_chummer5a_root(startup_smoke_artifact_path_obj):
        reasons.append("Windows startup smoke receipt artifactPath points into a legacy chummer5a root.")
    else:
        try:
            if installer_exists and startup_smoke_artifact_path_obj.resolve() != installer_path.resolve():
                reasons.append("Windows startup smoke receipt artifactPath does not resolve to promoted installer shelf bytes.")
        except Exception:
            reasons.append("Windows startup smoke receipt artifactPath could not be resolved for promoted shelf verification.")

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
            stale_age_acceptable = startup_smoke_stale_age_is_acceptable(
                host_supports_windows_smoke=host_supports_windows_smoke,
                startup_smoke_age_seconds=startup_smoke_age_seconds,
                max_age_seconds=STARTUP_SMOKE_MAX_AGE_SECONDS,
                startup_smoke_artifact_digest=startup_smoke_digest,
                expected_startup_smoke_digest=expected_installer_digest,
                startup_smoke_version=startup_smoke_version,
                release_channel_version=release_channel_version,
            )
            evidence["startup_smoke_stale_age_acceptable"] = stale_age_acceptable
            if stale_age_acceptable:
                evidence["startup_smoke_stale_age_accepted_reason"] = (
                    "Trusted Windows host proof still matches the exact promoted installer bytes and release version."
                )
            else:
                reasons.append(f"Windows startup smoke receipt is stale ({startup_smoke_age_seconds}s old).")

ui_local_release_status = read_status(
    ui_local_release_proof_path,
    expected_contract="chummer6-ui.local_release_proof",
)
blazor_self_host_workbench_proof_status = read_status(
    blazor_self_host_workbench_proof_path,
    expected_contract="chummer6-ui.blazor_self_host_workbench_proof",
)
blazor_public_edge_workbench_proof_status = read_status(
    blazor_public_edge_workbench_proof_path,
    expected_contract="chummer6-ui.blazor_public_edge_workbench_proof",
)
blazor_browser_lane_proof_set_status = read_status(
    blazor_browser_lane_proof_set_path,
    expected_contract="chummer6-ui.blazor_browser_lane_proof_set",
)
ui_flagship_gate_status = read_status(ui_flagship_release_gate_path)
desktop_workflow_execution_gate_status = read_status(
    desktop_workflow_execution_gate_path,
    expected_contract="chummer6-ui.desktop_workflow_execution_gate",
)
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
evidence["blazor_self_host_workbench_proof_status"] = blazor_self_host_workbench_proof_status
evidence["blazor_public_edge_workbench_proof_status"] = blazor_public_edge_workbench_proof_status
evidence["blazor_browser_lane_proof_set_status"] = blazor_browser_lane_proof_set_status
evidence["ui_flagship_release_gate_status"] = ui_flagship_gate_status
evidence["desktop_workflow_execution_gate_status"] = desktop_workflow_execution_gate_status
evidence["ui_workflow_parity_status"] = ui_workflow_parity_status
evidence["sr4_workflow_parity_status"] = sr4_workflow_parity_status
evidence["sr6_workflow_parity_status"] = sr6_workflow_parity_status
sr4_workflow_parity_external_only = workflow_parity_is_external_only(
    sr4_workflow_parity_path,
    "chummer6-ui.sr4_desktop_workflow_parity",
)
sr6_workflow_parity_external_only = workflow_parity_is_external_only(
    sr6_workflow_parity_path,
    "chummer6-ui.sr6_desktop_workflow_parity",
    upstream_external_only=sr4_workflow_parity_external_only,
)
evidence["sr4_workflow_parity_external_only"] = sr4_workflow_parity_external_only
evidence["sr6_workflow_parity_external_only"] = sr6_workflow_parity_external_only

if ui_local_release_status not in {"pass", "passed"}:
    reasons.append("UI local release proof is missing or not passed.")
if blazor_self_host_workbench_proof_status not in {"pass", "passed"}:
    reasons.append("Blazor self-host workbench proof is missing or not passed.")
if blazor_public_edge_workbench_proof_status not in {"pass", "passed", "ready"}:
    reasons.append("Blazor public-edge workbench proof is missing or not passed.")
if blazor_browser_lane_proof_set_status not in {"pass", "passed"}:
    reasons.append("Blazor browser-lane aggregate proof set is missing or not passed.")
aggregate_workflow_execution_pass = desktop_workflow_execution_gate_status in {"pass", "passed", "ready"}
if not aggregate_workflow_execution_pass and ui_workflow_parity_status not in {"pass", "passed", "ready"}:
    reasons.append("Chummer5a desktop workflow parity proof is missing or not passed.")
if (
    not aggregate_workflow_execution_pass
    and sr4_workflow_parity_status not in {"pass", "passed", "ready"}
    and not sr4_workflow_parity_external_only
):
    reasons.append("SR4 desktop workflow parity proof is missing or not passed.")
if (
    not aggregate_workflow_execution_pass
    and sr6_workflow_parity_status not in {"pass", "passed", "ready"}
    and not sr6_workflow_parity_external_only
):
    reasons.append("SR6 desktop workflow parity proof is missing or not passed.")

external_only_reason_checks = []
if "Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host." in reasons:
    external_only_reason_checks.append(
        windows_visual_proof_external_blocker == "missing_windows_visual_proof_capture"
    )
if "SR4 desktop workflow parity proof is missing or not passed." in reasons:
    external_only_reason_checks.append(sr4_workflow_parity_external_only)
if "SR6 desktop workflow parity proof is missing or not passed." in reasons:
    external_only_reason_checks.append(sr6_workflow_parity_external_only)

blocking_mode = (
    "none"
    if not reasons
    else "external_only"
    if external_only_reason_checks and all(external_only_reason_checks) and len(external_only_reason_checks) == len(reasons)
    else "mixed_or_local"
)

status = "passed" if not reasons else "failed"
summary = (
    "Windows desktop exit gate passed with an explicit incompatible-host startup-smoke boundary."
    if status == "passed" and startup_smoke_incompatible_host_skip_accepted
    else "Windows desktop exit gate passed."
    if status == "passed"
    else "Windows desktop exit gate failed: " + "; ".join(reasons)
)
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
    "summary": summary,
    "blockingMode": blocking_mode,
    "blocking_mode": blocking_mode,
    "checks": evidence,
    "reasons": reasons,
}
proof_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

if reasons:
    print(f"Windows desktop exit gate failed: {summary}", file=sys.stderr)
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

echo "[windows-exit-gate] PASS"
