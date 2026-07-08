#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
SCRIPT_DIR="$REPO_ROOT/scripts"
WORKSPACE_ROOT="$(cd "$REPO_ROOT_PHYSICAL/.." && pwd -P)"

BUNDLE_DIR="${1:-$REPO_ROOT/dist}"
DEPLOY_DIR="${2:-$REPO_ROOT/Docker/Downloads}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-}"
DEPLOY_MODE="${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED:-false}"
LIVE_VERIFY_TARGET="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}"
REQUIRE_EXTERNAL_PUBLISH="${CHUMMER_DOWNLOADS_REQUIRE_EXTERNAL_PUBLISH:-false}"
MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
FILES_SOURCE="$BUNDLE_DIR/files"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"
STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE:-$BUNDLE_DIR/startup-smoke}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-}"
SYNC_LIVE_DOWNLOADS_MIRRORS="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS:-auto}"
ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH="${CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH:-0}"
ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$WORKSPACE_ROOT/RELEASE_BLOCKERS.generated.json}"
PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS:-86400}"
ALLOW_BUNDLE_FILES_SOURCE_FALLBACK="${CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK:-0}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

normalize_mirror_sync_mode() {
  local value
  value="$(echo "${1:-auto}" | tr '[:upper:]' '[:lower:]')"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  case "$value" in
    ""|auto)
      printf '%s\n' "auto"
      ;;
    1|true|yes|on)
      printf '%s\n' "true"
      ;;
    0|false|no|off)
      printf '%s\n' "false"
      ;;
    *)
      echo "Invalid CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS value: '$1' (expected auto|true|false)." >&2
      exit 1
      ;;
  esac
}

validate_absolute_http_url() {
  local value="$1"
  local label="$2"
  python3 - "$value" "$label" <<'PY'
import sys
from urllib.parse import urlparse

value = sys.argv[1].strip()
label = sys.argv[2]
parsed = urlparse(value)
if parsed.scheme.lower() not in {"http", "https"} or not parsed.netloc:
    print(
        f"Invalid {label}: {value!r} (expected absolute http:// or https:// URL).",
        file=sys.stderr,
    )
    raise SystemExit(1)
PY
}

array_count() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || {
    printf '0\n'
    return 0
  }

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "set -- \"\${${array_name}[@]}\""
  local count="$#"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  printf '%s\n' "$count"
}

array_values_nul() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || return 0

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "printf '%s\\0' \"\${${array_name}[@]}\""
  local status="$?"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  return "$status"
}

manifest_channel_is_preview() {
  local manifest_path="$1"
  python3 - "$manifest_path" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

channel = str(payload.get("channel") or payload.get("channelId") or "").strip().lower()
raise SystemExit(0 if channel == "preview" else 1)
PY
}

verify_bundle_layout() {
  local bundle_dir="$1"
  local files_dir="$2"
  local normalized_bundle_dir="${bundle_dir%/}"
  local parent_dir
  parent_dir="$(dirname "$normalized_bundle_dir")"
  local nested_files_dir="$files_dir/files"

  if [[ "$(basename "$normalized_bundle_dir")" == "files" ]] \
    && [[ -f "$parent_dir/releases.json" || -f "$parent_dir/RELEASE_CHANNEL.generated.json" ]]; then
    echo "Bundle root points at files/ directory: $normalized_bundle_dir" >&2
    echo "Publish from the stage or bundle root, not its files/ child." >&2
    exit 1
  fi

  if [[ -d "$nested_files_dir" ]] && find "$nested_files_dir" -mindepth 1 -maxdepth 1 | grep -q .; then
    echo "Bundle is malformed: found nested files directory under $nested_files_dir" >&2
    echo "Publish from the stage or bundle root, not its files/ child." >&2
    exit 1
  fi
}

refresh_release_build_handoff() {
  local bundle_dir="$1"
  local handoff_script="${CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH:-$SCRIPT_DIR/materialize_release_candidate_handoff.py}"

  if [[ ! -f "$bundle_dir/RELEASE_CHANNEL.generated.json" ]]; then
    return 0
  fi

  if [[ ! -f "$handoff_script" ]]; then
    echo "Skipping release build handoff refresh because the materializer is missing: $handoff_script" >&2
    return 0
  fi

  if ! python3 "$handoff_script" "$bundle_dir" >/dev/null; then
    echo "Skipping release build handoff refresh because materialization failed for bundle: $bundle_dir" >&2
    return 0
  fi
}

emit_windows_visual_proof_handoff_guidance() {
  python3 - "$@" <<'PY' || true
from __future__ import annotations

import json
import sys
from pathlib import Path


def load_json(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def normalize(value: object) -> str:
    return str(value or "").strip()


roots = [Path(item) for item in sys.argv[1:] if normalize(item)]
handoff_payload = {}
handoff_path = None

for root in roots:
    direct_path = root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    if direct_path.is_file():
        handoff_payload = load_json(direct_path)
        handoff_path = direct_path
        break

for root in roots:
    if handoff_payload:
        break
    release_handoff_path = root / "RELEASE_BUILD_HANDOFF.generated.json"
    if release_handoff_path.is_file():
        release_handoff = load_json(release_handoff_path)
        candidate = release_handoff.get("windows_visual_proof_handoff")
        if isinstance(candidate, dict):
            handoff_payload = candidate
            candidate_path = normalize(candidate.get("json_path"))
            if candidate_path:
                handoff_path = Path(candidate_path)
            break

if not handoff_payload:
    raise SystemExit(0)

status = normalize(handoff_payload.get("status"))
summary = normalize(handoff_payload.get("summary"))
next_actions = handoff_payload.get("next_actions") if isinstance(handoff_payload.get("next_actions"), list) else []
json_path = normalize(handoff_payload.get("json_path")) or str(handoff_path or "")

if json_path:
    print(f"Windows visual proof handoff: {json_path}", file=sys.stderr)
if status:
    print(f"Windows visual proof status: {status}", file=sys.stderr)
if summary:
    print(f"Windows visual proof summary: {summary}", file=sys.stderr)
if next_actions:
    first_action = normalize(next_actions[0])
    if first_action:
        print(f"Windows visual proof next action: {first_action}", file=sys.stderr)
PY
}

require_public_stable_root_blocker_clearance() {
  local release_channel="${1:-}"
  local normalized_release_channel=""

  normalized_release_channel="$(echo "$release_channel" | tr '[:upper:]' '[:lower:]')"
  if [[ "$normalized_release_channel" != "public_stable" ]]; then
    return 0
  fi

  python3 - "$ROOT_RELEASE_BLOCKERS_PATH" "$PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path


ALLOWED_BLOCKERS = {"release_posture:non_flagship_channel"}
MAX_AGE_ENV_LABEL = "CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS"


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


path = Path(sys.argv[1])
if not path.is_file():
    fail(f"Public stable publication requires root release blocker truth, but the receipt is missing: {path}")

raw_max_age_seconds = str(sys.argv[2]).strip()
try:
    max_age_seconds = int(raw_max_age_seconds)
except ValueError:
    fail(
        f"Invalid {MAX_AGE_ENV_LABEL} value: {sys.argv[2]!r} "
        "(expected integer max age in seconds)."
    )
if max_age_seconds < 0:
    fail(
        f"Invalid {MAX_AGE_ENV_LABEL} value: {sys.argv[2]!r} "
        "(expected integer max age in seconds)."
    )

try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception as exc:  # pragma: no cover - surfaced via stderr
    fail(f"Public stable publication requires readable root release blocker truth: {path} ({exc})")

generated_at = str(payload.get("generated_at") or "").strip()
if not generated_at:
    fail(f"Public stable publication requires fresh root release blocker truth, but generated_at is missing: {path}")
try:
    normalized_generated_at = generated_at[:-1] + "+00:00" if generated_at.endswith("Z") else generated_at
    generated_at_dt = datetime.fromisoformat(normalized_generated_at)
except ValueError as exc:
    fail(
        "Public stable publication requires parseable generated_at in root release blocker truth: "
        f"{path} ({generated_at!r}; {exc})"
    )
if generated_at_dt.tzinfo is None:
    generated_at_dt = generated_at_dt.replace(tzinfo=timezone.utc)
generated_at_utc = generated_at_dt.astimezone(timezone.utc)
age_seconds = (datetime.now(timezone.utc) - generated_at_utc).total_seconds()
if age_seconds > max_age_seconds:
    fail(
        "Public stable publication requires fresh root release blocker truth. "
        f"source={path} generated_at={generated_at} "
        f"age_seconds={int(age_seconds)} max_age_seconds={max_age_seconds}"
    )

blockers = payload.get("blockers")
if not isinstance(blockers, list):
    blockers = payload.get("root_blockers") if isinstance(payload.get("root_blockers"), list) else None
if not isinstance(blockers, list):
    fail(f"Public stable publication requires a blockers list in root release blocker truth: {path}")

blocker_ids: list[str] = []
for entry in blockers:
    if not isinstance(entry, dict):
        continue
    blocker_id = str(entry.get("blocker_id") or entry.get("id") or "").strip()
    if blocker_id:
        blocker_ids.append(blocker_id)

disallowed = [blocker_id for blocker_id in blocker_ids if blocker_id not in ALLOWED_BLOCKERS]
if disallowed:
    fail(
        "Public stable publication is blocked by root release truth. "
        f"source={path} generated_at={generated_at or 'unknown'} "
        f"root_blocker_ids={','.join(blocker_ids) or '(none)'} "
        f"disallowed_blockers={','.join(disallowed)}"
    )
PY
}

is_public_artifact() {
  local artifact_name
  artifact_name="$(basename "$1")"
  case "$artifact_name" in
    chummer-*-win-*-payload.zip.json)
      return 0
      ;;
    chummer-*-win-*-payload.zip)
      return 0
      ;;
    chummer-*-osx-*installer.dmg|chummer-*-osx-*installer.pkg|chummer-*-macos-*installer.dmg|chummer-*-macos-*installer.pkg)
      return 1
      ;;
    chummer-*-win-*.zip|chummer-*-win-*.tar.gz|chummer-*-win-*.exe)
      if [[ "$artifact_name" != *-installer.exe ]]; then
        return 1
      fi
      ;;
  esac
  return 0
}

verify_windows_installer_payload_gate() {
  if [[ ! -f "$SCRIPT_DIR/verify-windows-installer-payloads.py" ]]; then
    echo "Missing Windows installer payload gate: $SCRIPT_DIR/verify-windows-installer-payloads.py" >&2
    exit 1
  fi

  local -a gate_args=(--files-dir "$FILES_SOURCE" --require-embedded-bootstrap-metadata --require-manifest-row)
  local -a installer_candidates=()
  [[ -f "$MANIFEST_SOURCE" ]] && gate_args+=(--manifest "$MANIFEST_SOURCE")
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$BUNDLE_DIR" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$FILES_SOURCE" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  local installer_candidate_count
  installer_candidate_count="$(array_count installer_candidates)"
  if (( installer_candidate_count == 0 )); then
    gate_args+=(--allow-empty)
  else
    local installer_path=""
    for installer_path in "${installer_candidates[@]}"; do
      gate_args+=(--installer "$installer_path")
    done
  fi
  python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${gate_args[@]}"
}

verify_windows_desktop_exit_gate() {
  local gate_output
  local visual_proof_path="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-${BUNDLE_DIR}/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"
  gate_output="$(mktemp)"

  if [[ ! -f "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" ]]; then
    echo "Missing Windows desktop exit gate: $SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >&2
    rm -f "$gate_output"
    exit 1
  fi

  if ! CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEPLOY_DIR/files" \
    CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path" \
    CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output" \
    bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null
  then
    rm -f "$gate_output"
    emit_windows_visual_proof_handoff_guidance "$BUNDLE_DIR" "$DEPLOY_DIR"
    if to_bool "$ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" && manifest_channel_is_preview "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json"; then
      echo "Published preview downloads shelf is carrying a Windows visual proof handoff instead of a passable Windows visual proof. Continuing because this lane publishes preview handoffs, not stable releases." >&2
      return 0
    fi
    echo "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." >&2
    exit 1
  fi

  rm -f "$gate_output"
}

strip_non_public_manifest_rows() {
  local manifest_path="$1"
  python3 - "$manifest_path" "$REPO_ROOT" <<'PY'
import importlib.util
import json
import sys
from pathlib import Path


def file_name_for(row: object) -> str:
    if not isinstance(row, dict):
        return ""
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return file_name
    raw = str(row.get("downloadUrl") or row.get("url") or "").strip()
    return Path(raw).name if raw else ""


def is_public_file_name(file_name: str) -> bool:
    name = file_name.strip().lower()
    if not name:
        return False
    if name.endswith(("-installer.deb", "-installer.exe", "-installer.msix")):
        return True
    if name.endswith(("-installer.dmg", "-installer.pkg")):
        return False
    if name.endswith((".zip", ".tar.gz")):
        return False
    if name.endswith(".exe") and not name.endswith("-installer.exe"):
        return False
    return False


path = Path(sys.argv[1])
repo_root = Path(sys.argv[2])
payload = json.loads(path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(0)

allowed_artifact_ids: set[str] = set()
for key in ("artifacts", "downloads"):
    rows = payload.get(key)
    if not isinstance(rows, list):
        continue
    filtered = []
    for row in rows:
        name = file_name_for(row)
        if not is_public_file_name(name):
            continue
        filtered.append(row)
        if isinstance(row, dict):
            artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
            if artifact_id:
                allowed_artifact_ids.add(artifact_id)
    payload[key] = filtered

for key in ("installAwareArtifactRegistry", "desktopSurfaceRefs", "artifactIdentityRegistry", "artifactPublicationBindings"):
    rows = payload.get(key)
    if not isinstance(rows, list) or not allowed_artifact_ids:
        continue
    payload[key] = [
        row for row in rows
        if isinstance(row, dict) and str(row.get("artifactId") or row.get("id") or "").strip() in allowed_artifact_ids
    ]

verifier_path = repo_root.parent / "chummer-hub-registry" / "scripts" / "verify_public_release_channel.py"
if verifier_path.is_file():
    spec = importlib.util.spec_from_file_location("verify_public_release_channel", verifier_path)
    if spec is not None and spec.loader is not None:
        verifier = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(verifier)
        payload["registryBoundaryCoverage"] = verifier.expected_registry_boundary_coverage(payload)

path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

if [[ -z "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" ]]; then
  if [[ "${RELEASE_CHANNEL:-preview}" =~ ^[Pp][Rr][Ee][Vv][Ii][Ee][Ww]$ ]]; then
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true"
  else
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="false"
  fi
fi

bundle_manifest_matches_files() {
  local manifest_path="$1"
  local files_root="$2"
  python3 - "$manifest_path" "$files_root" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
files_root = Path(sys.argv[2])

payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
downloads = payload.get("downloads") or []
failures: list[str] = []
seen: set[str] = set()
sidecars = {
    "aur-packages.json",
    "chummer6-bin-aur-source.tar.gz",
    "chummer6-bin.PKGBUILD",
    "chummer6-bin.SRCINFO",
}

for artifact in downloads:
    if not isinstance(artifact, dict):
        continue
    url = str(artifact.get("url") or "").strip()
    file_name = Path(url).name if url else ""
    if not file_name:
        continue
    seen.add(file_name)
    file_path = files_root / file_name
    if not file_path.is_file():
        failures.append(f"manifest references missing file {file_name}")
        continue
    actual_size = file_path.stat().st_size
    expected_size = int(artifact.get("sizeBytes") or 0)
    if expected_size and expected_size != actual_size:
        failures.append(f"{file_name}: size {actual_size} != manifest {expected_size}")
    expected_sha = str(artifact.get("sha256") or "").strip().lower()
    if expected_sha:
        digest = hashlib.sha256(file_path.read_bytes()).hexdigest()
        if digest != expected_sha:
            failures.append(f"{file_name}: sha256 {digest} != manifest {expected_sha}")

for file_path in sorted(files_root.iterdir()):
    if not file_path.is_file():
        continue
    if file_path.name in sidecars:
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip"):
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip.json"):
        continue
    if file_path.name not in seen:
        failures.append(f"bundle contains extra file not present in manifest: {file_path.name}")

if failures:
    print("false")
    for failure in failures:
        print(failure)
else:
    print("true")
PY
}

if [[ -z "$PORTAL_MANIFEST_PATH" ]]; then
  if [[ "$(realpath -m "$DEPLOY_DIR")" == "$(realpath -m "$REPO_ROOT/Docker/Downloads")" ]]; then
    PORTAL_MANIFEST_PATH="$REPO_ROOT/Chummer.Portal/downloads/releases.json"
  else
    PORTAL_MANIFEST_PATH="$DEPLOY_DIR/releases.json"
  fi
fi

if [[ -z "$PORTAL_DOWNLOADS_DIR" ]]; then
  PORTAL_DOWNLOADS_DIR="$(dirname "$PORTAL_MANIFEST_PATH")"
fi

if [[ ! -d "$BUNDLE_DIR" ]]; then
  echo "Bundle directory not found: $BUNDLE_DIR" >&2
  exit 1
fi

verify_bundle_layout "$BUNDLE_DIR" "$FILES_SOURCE"

if [[ ! -d "$FILES_SOURCE" ]]; then
  if to_bool "$ALLOW_BUNDLE_FILES_SOURCE_FALLBACK"; then
    for fallback_files_source in \
      "$REPO_ROOT/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads/files" \
      "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published/files"
    do
      if [[ -d "$fallback_files_source" ]]; then
        FILES_SOURCE="$fallback_files_source"
        break
      fi
    done
  fi
fi

if [[ ! -d "$FILES_SOURCE" ]]; then
  echo "Bundle is missing files directory: $FILES_SOURCE" >&2
  echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2
  echo "Refusing to fall back to unrelated downloads/files roots unless CHUMMER_ALLOW_BUNDLE_FILES_SOURCE_FALLBACK=true is set explicitly." >&2
  exit 1
fi

artifacts=()
while IFS= read -r artifact_path; do
  [[ -n "$artifact_path" ]] || continue
  artifacts+=("$artifact_path")
done < <(find "$FILES_SOURCE" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o \
     -name "chummer-avalonia-*.tar.gz" -o -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
     -name "chummer-avalonia-*-payload.zip.json" -o \
     -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" \) \
  | sort)

artifact_count="$(array_count artifacts)"
if (( artifact_count == 0 )); then
  echo "No desktop artifacts found under $FILES_SOURCE" >&2
  exit 1
fi

verify_windows_installer_payload_gate
refresh_release_build_handoff "$BUNDLE_DIR"

if to_bool "$DEPLOY_MODE"; then
  export CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION="${CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION:-true}"
  export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS:-true}"
  if [[ -z "$LIVE_VERIFY_TARGET" ]]; then
    echo "Deployment mode requires CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL for live manifest verification." >&2
    exit 1
  fi
fi

if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  validate_absolute_http_url "$LIVE_VERIFY_TARGET" "CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL"
fi

sync_source_dir="$(mktemp -d)"
cleanup() {
  rm -rf "$sync_source_dir"
}
trap cleanup EXIT

append_unique_downloads_mirror_dir() {
  local candidate="$1"
  local resolved_candidate=""
  local existing=""

  [[ -n "$candidate" ]] || return 0
  resolved_candidate="$(realpath -m "$candidate")"
  while IFS= read -r -d '' existing; do
    [[ -n "$existing" ]] || continue
    if [[ "$(realpath -m "$existing")" == "$resolved_candidate" ]]; then
      return 0
    fi
  done < <(array_values_nul live_downloads_mirror_dirs)
  live_downloads_mirror_dirs+=("$candidate")
}

deploy_dir_is_live_downloads_root() {
  local candidate="$1"
  local resolved_candidate=""
  local known_root=""

  resolved_candidate="$(realpath -m "$candidate")"
  for known_root in \
    "$REPO_ROOT/Docker/Downloads" \
    "$REPO_ROOT/Chummer.Portal/downloads" \
    "$REPO_ROOT/.codex-studio/published/portal" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ "$resolved_candidate" == "$(realpath -m "$known_root")" ]]; then
      return 0
    fi
  done

  return 1
}

deploy_dir_is_repo_owned_live_downloads_root() {
  local candidate="$1"
  local resolved_candidate=""
  local known_root=""

  resolved_candidate="$(realpath -m "$candidate")"
  for known_root in \
    "$REPO_ROOT/Docker/Downloads" \
    "$REPO_ROOT/Chummer.Portal/downloads" \
    "$REPO_ROOT/.codex-studio/published/portal"
  do
    if [[ "$resolved_candidate" == "$(realpath -m "$known_root")" ]]; then
      return 0
    fi
  done

  return 1
}

discover_live_downloads_mirror_dirs() {
  local mode="${1:-auto}"
  local configured="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS:-}"
  local deploy_dir_physical=""
  local canonical_downloads_physical=""
  local portal_downloads_physical=""
  local candidate=""
  local sibling_root=""

  if [[ -n "$configured" ]]; then
    IFS=',' read -r -a configured_dirs <<<"$configured"
    for candidate in "${configured_dirs[@]}"; do
      candidate="${candidate#"${candidate%%[![:space:]]*}"}"
      candidate="${candidate%"${candidate##*[![:space:]]}"}"
      [[ -n "$candidate" ]] || continue
      append_unique_downloads_mirror_dir "$candidate"
    done
  fi

  deploy_dir_physical="$(realpath -m "$DEPLOY_DIR")"
  canonical_downloads_physical="$(realpath -m "$REPO_ROOT/Docker/Downloads")"
  portal_downloads_physical="$(realpath -m "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads")"

  if [[ "$mode" == "auto" ]] && ! deploy_dir_is_repo_owned_live_downloads_root "$deploy_dir_physical"; then
    return 0
  fi

  if [[ "$mode" != "auto" ]] && ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then
    return 0
  fi

  if [[ "$deploy_dir_physical" != "$canonical_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/Docker/Downloads"
  fi

  if [[ "$deploy_dir_physical" != "$portal_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads"
  fi

  for sibling_root in \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ -d "$(dirname "$sibling_root")" ]]; then
      append_unique_downloads_mirror_dir "$sibling_root"
    fi
  done
}

resolve_aur_materializer() {
  local configured="${CHUMMER_AUR_MATERIALIZER:-}"
  local candidate=""

  if [[ -n "$configured" ]]; then
    if [[ -f "$configured" ]]; then
      printf '%s\n' "$configured"
      return 0
    fi
    echo "Configured AUR materializer is missing: $configured" >&2
    return 1
  fi

  for candidate in \
    "$REPO_ROOT/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer.run-services/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer6-hub/scripts/materialize-aur-package.py"
  do
    if [[ -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

remove_aur_sidecar() {
  rm -f \
    "$DEPLOY_DIR/aur-packages.json" \
    "$DEPLOY_DIR/files/chummer6-bin-aur-source.tar.gz" \
    "$DEPLOY_DIR/files/chummer6-bin.PKGBUILD" \
    "$DEPLOY_DIR/files/chummer6-bin.SRCINFO"
}

materialize_aur_sidecar() {
  local materializer=""

  if materializer="$(resolve_aur_materializer)"; then
    python3 "$materializer" \
      --manifest "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
      --files-root "$DEPLOY_DIR/files" \
      --output-root "$DEPLOY_DIR" \
      --downloads-prefix "${CHUMMER_PUBLIC_DOWNLOADS_PREFIX:-https://chummer.run/downloads/files}" \
      --optional >/dev/null
    return 0
  fi

  remove_aur_sidecar
  echo "AUR materializer not found; removed stale AUR sidecar files from $DEPLOY_DIR." >&2
}

sync_live_downloads_mirror_dir() {
  local target_dir="$1"
  local target_label="$2"
  local resolved_target_dir=""
  local resolved_deploy_dir=""
  local resolved_portal_dir=""
  local files_dir=""
  local startup_smoke_dir=""
  local source_path=""
  local file_name=""

  resolved_target_dir="$(realpath -m "$target_dir")"
  resolved_deploy_dir="$(realpath -m "$DEPLOY_DIR")"
  if [[ -n "$PORTAL_DOWNLOADS_DIR" ]]; then
    resolved_portal_dir="$(realpath -m "$PORTAL_DOWNLOADS_DIR")"
  else
    resolved_portal_dir="$resolved_deploy_dir"
  fi

  if [[ "$resolved_target_dir" == "$resolved_deploy_dir" || "$resolved_target_dir" == "$resolved_portal_dir" ]]; then
    return 0
  fi

  mkdir -p "$target_dir"
  cp "$DEPLOY_DIR/releases.json" "$target_dir/releases.json"
  cp "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$target_dir/RELEASE_CHANNEL.generated.json"
  if [[ -f "$DEPLOY_DIR/aur-packages.json" ]]; then
    cp "$DEPLOY_DIR/aur-packages.json" "$target_dir/aur-packages.json"
  else
    rm -f "$target_dir/aur-packages.json"
  fi

  startup_smoke_dir="$target_dir/startup-smoke"
  mkdir -p "$startup_smoke_dir"
  find "$startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
  if [[ -d "$DEPLOY_DIR/startup-smoke" ]] && find "$DEPLOY_DIR/startup-smoke" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp -f "$DEPLOY_DIR"/startup-smoke/* "$startup_smoke_dir"/
  fi

  files_dir="$target_dir/files"
  mkdir -p "$files_dir"
  rm -f \
    "$files_dir"/chummer6-bin-aur-source.tar.gz \
    "$files_dir"/chummer6-bin.PKGBUILD \
    "$files_dir"/chummer6-bin.SRCINFO
  find "$files_dir" -maxdepth 1 -type f \
    \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
       -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
       -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
       -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
       -name "chummer-avalonia-*-payload.zip.json" -o \
       -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
       -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
       -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
       -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
       -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" -o \
       -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
       -name "chummer-6-*-installer.deb" -o -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
       -name "chummer-6-*-installer.msix" -o -name "chummer-6-*-payload.zip" -o \
       -name "chummer-6-*-payload.zip.json" \) \
    -delete

  while IFS= read -r -d '' file_name; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ ! -f "$source_path" ]]; then
      echo "promoted artifact missing from deploy root for $target_label mirror: $source_path" >&2
      exit 1
    fi
    cp "$source_path" "$files_dir/"
  done < <(array_values_nul promoted_file_names)
  for file_name in chummer6-bin-aur-source.tar.gz chummer6-bin.PKGBUILD chummer6-bin.SRCINFO; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ -f "$source_path" ]]; then
      cp "$source_path" "$files_dir/"
    fi
  done

  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$target_dir/RELEASE_CHANNEL.generated.json" >/dev/null
  echo "synced ${promoted_file_count} promoted artifact(s) -> $target_label mirror $target_dir"
}

while IFS= read -r -d '' artifact; do
  if is_public_artifact "$artifact"; then
    cp "$artifact" "$sync_source_dir/"
  fi
done < <(array_values_nul artifacts)

release_version="${RELEASE_VERSION:-}"
release_channel="${RELEASE_CHANNEL:-}"
release_published_at="${RELEASE_PUBLISHED_AT:-}"
default_published_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [[ -f "$MANIFEST_SOURCE" ]]; then
  manifest_meta=()
  while IFS= read -r line; do
    manifest_meta+=("$line")
  done < <(python3 - "$MANIFEST_SOURCE" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
print(str(data.get("version", "unpublished")))
print(str(data.get("channel", "docker")))
print(str(data.get("publishedAt", "")))
PY
)

  manifest_integrity=()
  while IFS= read -r line; do
    manifest_integrity+=("$line")
  done < <(bundle_manifest_matches_files "$MANIFEST_SOURCE" "$FILES_SOURCE")
  manifest_matches_files="${manifest_integrity[0]:-false}"

  if [[ "$manifest_matches_files" != "true" && -z "${RELEASE_VERSION:-}" ]]; then
    echo "Bundle files no longer match $MANIFEST_SOURCE, so reusing its release version would be dishonest." >&2
    printf '%s\n' "${manifest_integrity[@]:1}" >&2
    echo "Set RELEASE_VERSION and RELEASE_PUBLISHED_AT explicitly for this republish." >&2
    exit 1
  fi

  if [[ -z "$release_version" && -n "${manifest_meta[0]:-}" ]]; then
    release_version="${manifest_meta[0]}"
  fi
  if [[ -z "$release_channel" && -n "${manifest_meta[1]:-}" ]]; then
    release_channel="${manifest_meta[1]}"
  fi
  if [[ -z "$release_published_at" && -n "${manifest_meta[2]:-}" ]]; then
    release_published_at="${manifest_meta[2]}"
  fi
fi

release_version="${release_version:-unpublished}"
release_channel="${release_channel:-docker}"
release_published_at="${release_published_at:-$default_published_at}"
require_public_stable_root_blocker_clearance "$release_channel"
live_downloads_mirror_dirs=()
sync_live_downloads_mirrors_mode="$(normalize_mirror_sync_mode "$SYNC_LIVE_DOWNLOADS_MIRRORS")"
if [[ "$sync_live_downloads_mirrors_mode" != "false" ]]; then
  discover_live_downloads_mirror_dirs "$sync_live_downloads_mirrors_mode"
fi
live_downloads_mirror_dir_count="$(array_count live_downloads_mirror_dirs)"

DOWNLOADS_DIR="$sync_source_dir" \
MANIFEST_PATH="$DEPLOY_DIR/releases.json" \
PORTAL_MANIFEST_PATH="$PORTAL_MANIFEST_PATH" \
PORTAL_DOWNLOADS_DIR="$PORTAL_DOWNLOADS_DIR" \
RELEASE_VERSION="$release_version" \
RELEASE_CHANNEL="$release_channel" \
RELEASE_PUBLISHED_AT="$release_published_at" \
SOURCE_MANIFEST_PATH="$MANIFEST_SOURCE" \
RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH" \
STARTUP_SMOKE_DIR="$STARTUP_SMOKE_SOURCE" \
CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS:-}" \
CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \
bash "$SCRIPT_DIR/generate-releases-manifest.sh"

strip_non_public_manifest_rows "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json"
strip_non_public_manifest_rows "$DEPLOY_DIR/releases.json"

promoted_file_names=()
while IFS= read -r file_name; do
  [[ -n "$file_name" ]] || continue
  promoted_file_names+=("$file_name")
done < <(python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$sync_source_dir" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
source_root = Path(sys.argv[2])
seen = set()
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue
    names = []
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        file_name = Path(str(artifact.get("downloadUrl") or "").strip()).name
    names.append(file_name)
    payload_name = str(artifact.get("payloadFileName") or "").strip()
    if not payload_name:
        payload_name = Path(str(artifact.get("payloadDownloadUrl") or "").strip()).name
    names.append(payload_name)
    if payload_name:
        payload_metadata_name = payload_name + ".json"
        if (source_root / payload_metadata_name).is_file():
            names.append(payload_metadata_name)
    for candidate in names:
        if candidate and candidate not in seen:
            print(candidate)
            seen.add(candidate)
PY
)
promoted_file_count="$(array_count promoted_file_names)"

mkdir -p "$DEPLOY_DIR/files"
find "$DEPLOY_DIR/files" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
     -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
     -name "chummer-avalonia-*-payload.zip.json" -o \
     -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" -o \
     -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
     -name "chummer-6-*-installer.deb" -o \
     -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
     -name "chummer-6-*-installer.msix" -o -name "chummer-6-*-payload.zip" -o \
     -name "chummer-6-*-payload.zip.json" \) \
  -delete

while IFS= read -r -d '' file_name; do
  source_path="$sync_source_dir/$file_name"
  if [[ ! -f "$source_path" ]]; then
    echo "promoted artifact missing from bundle source: $source_path" >&2
    exit 1
  fi
  cp "$source_path" "$DEPLOY_DIR/files/"
done < <(array_values_nul promoted_file_names)

materialize_aur_sidecar

if (( live_downloads_mirror_dir_count > 0 )); then
  while IFS= read -r -d '' mirror_dir; do
    sync_live_downloads_mirror_dir "$mirror_dir" "public-edge"
  done < <(array_values_nul live_downloads_mirror_dirs)
fi

if [[ -d "$STARTUP_SMOKE_SOURCE" ]]; then
  verified_startup_smoke_tmp="$(mktemp)"
  if ! python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$STARTUP_SMOKE_SOURCE" "$DEPLOY_DIR/files" >"$verified_startup_smoke_tmp" <<'PY'
import os
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PASSING_STATUSES = {"pass", "passed", "ready"}
INSTALL_MEDIA_KINDS = {"installer", "dmg", "pkg", "msix"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
PUBLIC_SKIP_STARTUP_SMOKE_FILTER = (
    str(os.environ.get("CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)
ALLOW_SKIPPED_STARTUP_SMOKE = (
    str(os.environ.get("CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)

release_channel_path = Path(sys.argv[1])
startup_smoke_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = payload.get("artifacts") or []
errors: list[str] = []
verified_receipts: list[str] = []
seen: set[str] = set()

def normalize(value: Any) -> str:
    return str(value or "").strip().lower()

def expected_host_class_platform_tokens(platform: str) -> tuple[str, ...]:
    normalized = normalize(platform)
    if normalized == "windows":
        return ("win", "windows")
    if normalized == "macos":
        return ("osx", "macos")
    if normalized == "linux":
        return ("linux",)
    return (normalized,) if normalized else ()

def host_class_matches_platform(host_class: str, platform: str, operating_system: str = "") -> bool:
    normalized_host = normalize(host_class)
    normalized_os = normalize(operating_system)
    expected_tokens = expected_host_class_platform_tokens(platform)
    if not normalized_host or not expected_tokens:
        if normalize(platform) == "windows":
            return "windows" in normalized_os
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    if any(token in host_tokens for token in expected_tokens):
        return True
    if normalize(platform) == "windows":
        return "windows" in normalized_os and "wine" in normalized_host
    return False

def rid_to_arch(rid: str) -> str:
    token = normalize(rid)
    if token.startswith("win-") or token.startswith("linux-") or token.startswith("osx-"):
        _, _, arch = token.partition("-")
        return arch
    return token

def is_windows_incompatible_host_skip(receipt: dict[str, Any], platform: str, rid: str) -> bool:
    if normalize(receipt.get("status")) != "skipped":
        return False
    if normalize(platform) != "windows" and not normalize(rid).startswith("win-"):
        return False
    verification_disposition = normalize(receipt.get("verificationDisposition"))
    skip_class = normalize(receipt.get("skipClass"))
    return verification_disposition == "incompatible_host" or skip_class == "incompatible_host"

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

for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    kind = normalize(artifact.get("kind"))
    if kind not in INSTALL_MEDIA_KINDS:
        continue
    head = normalize(artifact.get("head"))
    platform = normalize(artifact.get("platform"))
    rid = normalize(artifact.get("rid"))
    file_name = str(artifact.get("fileName") or "").strip()
    if not head or not platform or not rid or not file_name:
        errors.append(f"promoted install-medium artifact is missing required tuple fields (head/platform/rid/fileName): {artifact}")
        continue
    receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
    if receipt_name in seen:
        continue
    seen.add(receipt_name)
    receipt_path = startup_smoke_root / receipt_name
    if not receipt_path.is_file():
        errors.append(f"startup-smoke receipt missing for promoted install medium {head}/{platform}/{rid}: {receipt_name}")
        continue
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # pragma: no cover - shell guard
        errors.append(f"startup-smoke receipt is unreadable for promoted install medium {head}/{platform}/{rid}: {receipt_path} ({exc})")
        continue
    status = normalize(receipt.get("status"))
    incompatible_host_skip = is_windows_incompatible_host_skip(receipt, platform, rid)
    if status not in PASSING_STATUSES:
        if incompatible_host_skip or (ALLOW_SKIPPED_STARTUP_SMOKE and status == "skipped"):
            verified_receipts.append(str(receipt_path))
        else:
            errors.append(f"startup-smoke receipt status is not passing for promoted install medium {head}/{platform}/{rid}: {status or 'missing'}")
    checkpoint = normalize(receipt.get("readyCheckpoint"))
    if not incompatible_host_skip and checkpoint != "pre_ui_event_loop":
        errors.append(f"startup-smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted install medium {head}/{platform}/{rid}.")
    receipt_head = normalize(receipt.get("headId"))
    receipt_platform = normalize(receipt.get("platform"))
    receipt_arch = normalize(receipt.get("arch"))
    receipt_rid = normalize(receipt.get("rid"))
    receipt_host_class = normalize(receipt.get("hostClass"))
    receipt_operating_system = str(receipt.get("operatingSystem") or "").strip()
    expected_arch = rid_to_arch(rid)
    if receipt_head != head:
        errors.append(f"startup-smoke receipt headId mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_head or 'missing'}")
    if receipt_platform != platform:
        errors.append(f"startup-smoke receipt platform mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_platform or 'missing'}")
    if not incompatible_host_skip:
        if not receipt_host_class:
            errors.append(f"startup-smoke receipt hostClass is missing for promoted install medium {head}/{platform}/{rid}.")
        elif not host_class_matches_platform(receipt_host_class, platform, receipt_operating_system):
            errors.append(f"startup-smoke receipt hostClass does not identify the {platform} host for promoted install medium {head}/{platform}/{rid}.")
        if not receipt_operating_system:
            errors.append(f"startup-smoke receipt operatingSystem is missing for promoted install medium {head}/{platform}/{rid}.")
    if expected_arch and receipt_arch != expected_arch:
        errors.append(f"startup-smoke receipt arch mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_arch or 'missing'}")
    if not receipt_rid:
        errors.append(f"startup-smoke receipt rid is missing for promoted install medium {head}/{platform}/{rid}.")
    elif receipt_rid != rid:
        errors.append(f"startup-smoke receipt rid mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_rid}")
    promoted_file_path = files_root / file_name
    expected_sha = normalize(artifact.get("sha256"))
    if promoted_file_path.is_file():
        expected_sha = hashlib.sha256(promoted_file_path.read_bytes()).hexdigest().lower()
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    receipt_digest = normalize(receipt.get("artifactDigest"))
    if expected_digest and receipt_digest != expected_digest:
        errors.append(f"startup-smoke receipt artifactDigest mismatch for promoted install medium {head}/{platform}/{rid}.")
    recorded_at_raw = (
        receipt.get("completedAtUtc")
        or receipt.get("recordedAtUtc")
        or receipt.get("startedAtUtc")
    )
    recorded_at = parse_iso_utc(recorded_at_raw)
    if recorded_at is None:
        errors.append(
            f"startup-smoke receipt timestamp is missing/invalid for promoted install medium {head}/{platform}/{rid}."
        )
    else:
        now_utc = datetime.now(timezone.utc)
        age_delta_seconds = int((now_utc - recorded_at).total_seconds())
        if age_delta_seconds < 0:
            future_skew_seconds = abs(age_delta_seconds)
            if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                errors.append(
                    "startup-smoke receipt timestamp is in the future for promoted install medium "
                    f"{head}/{platform}/{rid}: {future_skew_seconds}s ahead (max {STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS}s)."
                )
        elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS and not PUBLIC_SKIP_STARTUP_SMOKE_FILTER:
            errors.append(
                "startup-smoke receipt is stale for promoted install medium "
                f"{head}/{platform}/{rid}: {age_delta_seconds}s old (max {STARTUP_SMOKE_MAX_AGE_SECONDS}s)."
            )
    verified_receipts.append(str(receipt_path))

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    raise SystemExit(1)

for verified in sorted(verified_receipts):
    print(verified)
PY
  then
    rm -f "$verified_startup_smoke_tmp"
    exit 1
  fi
  verified_startup_smoke_receipts=()
  while IFS= read -r receipt_path; do
    verified_startup_smoke_receipts+=("$receipt_path")
  done <"$verified_startup_smoke_tmp"
  rm -f "$verified_startup_smoke_tmp"

  if ! python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$DEPLOY_DIR/releases.json" \
    --startup-smoke-dir "$STARTUP_SMOKE_SOURCE" \
    --files-dir "$DEPLOY_DIR/files" >/dev/null
  then
    exit 1
  fi

  startup_smoke_deploy_dir="$DEPLOY_DIR/startup-smoke"
  startup_smoke_stage_dir="$(mktemp -d)"
  startup_smoke_deploy_dir_real="$(realpath -m "$startup_smoke_deploy_dir")"
  deploy_files_dir_real="$(realpath -m "$DEPLOY_DIR/files")"
  mkdir -p "$startup_smoke_deploy_dir"
  startup_smoke_fallback_dir="$PORTAL_DOWNLOADS_DIR/startup-smoke"
  run_services_startup_smoke_dir="$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/startup-smoke"
  python3 - "$startup_smoke_stage_dir" "$startup_smoke_deploy_dir_real" "$deploy_files_dir_real" "$release_channel" "$release_version" "$startup_smoke_fallback_dir" "$run_services_startup_smoke_dir" "${verified_startup_smoke_receipts[@]}" <<'PY'
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

stage_root = Path(sys.argv[1])
final_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])
release_channel = str(sys.argv[4]).strip()
release_version = str(sys.argv[5]).strip()
fallback_roots = [Path(item) for item in sys.argv[6:8] if str(item).strip()]
receipt_paths = [Path(item) for item in sys.argv[8:]]


def resolve_companion(source_root: Path, value: object) -> Path | None:
    raw = str(value or "").strip()
    if not raw:
        return None

    token = Path(raw)
    candidates: list[Path] = []
    if token.is_absolute():
        candidates.append(token)
    else:
        candidates.append(source_root / token)
    candidates.append(source_root / token.name)
    for fallback_root in fallback_roots:
        candidates.append(fallback_root / token.name)

    seen: set[Path] = set()
    for candidate in candidates:
        candidate = candidate.resolve(strict=False)
        if candidate in seen:
            continue
        seen.add(candidate)
        if candidate.is_file():
            return candidate
    return None


def copy_companion(source_root: Path, value: object) -> str:
    source_path = resolve_companion(source_root, value)
    if source_path is None:
        return ""

    stage_path = stage_root / source_path.name
    final_path = final_root / source_path.name
    if source_path.resolve() != stage_path.resolve():
        shutil.copy2(source_path, stage_path)
    return str(final_path)


def rewrite_install_verification(stage_verification_path: Path, source_root: Path) -> None:
    payload = json.loads(stage_verification_path.read_text(encoding="utf-8-sig"))
    for key in (
        "dpkgLogPath",
        "installedLaunchCapturePath",
        "wrapperCapturePath",
        "desktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied
    stage_verification_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


for receipt_path in receipt_paths:
    source_root = receipt_path.parent
    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

    if release_channel:
        payload["channelId"] = release_channel
        payload["channel"] = release_channel
    if release_version:
        payload["releaseVersion"] = release_version
        payload["version"] = release_version

    verification_dest = copy_companion(source_root, payload.get("artifactInstallVerificationPath"))
    if verification_dest:
        payload["artifactInstallVerificationPath"] = verification_dest
        rewrite_install_verification(stage_root / Path(verification_dest).name, source_root)

    for key in (
        "artifactInstallDpkgLogPath",
        "artifactInstallLaunchCapturePath",
        "artifactInstallWrapperCapturePath",
        "artifactInstallDesktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied

    artifact_name = Path(str(payload.get("artifactPath") or "").strip()).name
    if artifact_name:
        published_artifact = files_root / artifact_name
        if published_artifact.is_file():
            payload["artifactPath"] = str(published_artifact)

    (stage_root / receipt_path.name).write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )
PY
  find "$startup_smoke_deploy_dir" -maxdepth 1 -type f \( \
    -name "startup-smoke-*.receipt.json" -o \
    -name "install-verification-*.json" -o \
    -name "dpkg-*.log" -o \
    -name "installed-launch-*" -o \
    -name "installed-wrapper-*" -o \
    -name "installed-desktop-entry-*" -o \
    -name "windows-installer-progress-*.log" \
  \) -exec rm -f -- {} +
  if find "$startup_smoke_stage_dir" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp "$startup_smoke_stage_dir"/* "$startup_smoke_deploy_dir"/
  fi
  if [[ -d "$STARTUP_SMOKE_SOURCE" ]] && find "$STARTUP_SMOKE_SOURCE" -maxdepth 1 -type f -name 'windows-installer-progress-*.log' | grep -q .; then
    cp -f "$STARTUP_SMOKE_SOURCE"/windows-installer-progress-*.log "$startup_smoke_deploy_dir"/
  fi
  rm -rf "$startup_smoke_stage_dir"
fi

refresh_release_build_handoff "$DEPLOY_DIR"
verify_windows_desktop_exit_gate

CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$DEPLOY_DIR"

if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$LIVE_VERIFY_TARGET"
fi

scope_args=(
  --output "$DEPLOY_DIR/PUBLICATION_SCOPE.generated.json"
  --deploy-dir "$DEPLOY_DIR"
  --release-version "$release_version"
  --release-channel "$release_channel"
  --promoted-artifact-count "$promoted_file_count"
)
if to_bool "$DEPLOY_MODE"; then
  scope_args+=(--deploy-mode)
fi
if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  scope_args+=(--live-verify-target "$LIVE_VERIFY_TARGET")
fi
if to_bool "$REQUIRE_EXTERNAL_PUBLISH"; then
  scope_args+=(--require-external-publish)
fi
python3 "$SCRIPT_DIR/materialize-downloads-publication-scope.py" "${scope_args[@]}"

if to_bool "$DEPLOY_MODE"; then
  echo "Published ${promoted_file_count} desktop artifact(s) through verified external downloads lane: $LIVE_VERIFY_TARGET"
else
  echo "Updated local downloads shelf with ${promoted_file_count} desktop artifact(s): $DEPLOY_DIR"
fi
