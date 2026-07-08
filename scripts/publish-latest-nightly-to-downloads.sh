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

STAGING_ROOT="${CHUMMER_STAGING_ROOT:-$WORKSPACE_ROOT/_staging}"
DEPLOY_DIR="${1:-${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR:-$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads}}"
REDEPLOY_PUBLIC_EDGE="${CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH:-true}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}"
REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-0}"
PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS="${CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS:-0}"
SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-0}"
ALLOW_SKIPPED_STARTUP_SMOKE="${CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE:-0}"
ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH="${CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH:-1}"
# Nightly publication is a preview handoff lane. Stable/public release promotion
# must happen through the explicit stable release path instead.
PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-preview}"
ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH="${CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH:-0}"
DAILY_PUBLISH_TIMEZONE="${CHUMMER_DAILY_NIGHTLY_PUBLISH_TIMEZONE:-Europe/Vienna}"
DAILY_PUBLISH_HOUR="${CHUMMER_DAILY_NIGHTLY_PUBLISH_HOUR:-8}"
FORCE_NIGHTLY_PUBLISH="${CHUMMER_FORCE_NIGHTLY_PUBLISH:-0}"
PUBLIC_EDGE_VERIFY_BASE_URL="${CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL:-http://127.0.0.1:${CHUMMER_PUBLIC_EDGE_PORT:-8091}}"
PUBLIC_EDGE_VERIFY_HOST="${CHUMMER_PUBLIC_EDGE_VERIFY_HOST:-chummer.run}"
PUBLIC_EDGE_VERIFY_PROTO="${CHUMMER_PUBLIC_EDGE_VERIFY_PROTO:-https}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
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

validate_http_host_header() {
  local value="$1"
  local label="$2"
  python3 - "$value" "$label" <<'PY'
import sys

value = sys.argv[1].strip()
label = sys.argv[2]
if not value or any(ch.isspace() for ch in value) or "://" in value or "/" in value:
    print(
        f"Invalid {label}: {value!r} (expected bare host header value).",
        file=sys.stderr,
    )
    raise SystemExit(1)
PY
}

validate_forwarded_proto() {
  local value="$1"
  local label="$2"
  python3 - "$value" "$label" <<'PY'
import sys

value = sys.argv[1].strip().lower()
label = sys.argv[2]
if value not in {"http", "https"}:
    print(
        f"Invalid {label}: {sys.argv[1]!r} (expected 'http' or 'https').",
        file=sys.stderr,
    )
    raise SystemExit(1)
PY
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

normalized_public_release_channel="$(echo "$PUBLIC_RELEASE_CHANNEL" | tr '[:upper:]' '[:lower:]')"
if [[ "$normalized_public_release_channel" =~ ^(public_stable|stable)$ ]] && ! to_bool "$ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH"; then
  echo "Nightly publisher is the preview handoff lane. Refusing stable/public_stable publication from this script." >&2
  echo "Use the stable release publisher, or set CHUMMER_ALLOW_STABLE_CHANNEL_FROM_NIGHTLY_PUBLISH=true for an explicit one-off override." >&2
  exit 1
fi

if to_bool "$REDEPLOY_PUBLIC_EDGE" && [[ "$DEPLOY_DIR" == "$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads" ]]; then
  validate_absolute_http_url "$PUBLIC_EDGE_VERIFY_BASE_URL" "CHUMMER_PUBLIC_EDGE_VERIFY_BASE_URL"
  validate_http_host_header "$PUBLIC_EDGE_VERIFY_HOST" "CHUMMER_PUBLIC_EDGE_VERIFY_HOST"
  validate_forwarded_proto "$PUBLIC_EDGE_VERIFY_PROTO" "CHUMMER_PUBLIC_EDGE_VERIFY_PROTO"
fi

refresh_release_build_handoff() {
  local stage_dir="$1"
  local handoff_script="${CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH:-$SCRIPT_DIR/materialize_release_candidate_handoff.py}"

  if [[ ! -f "$handoff_script" ]]; then
    echo "Missing release build handoff materializer: $handoff_script" >&2
    exit 1
  fi

  if ! python3 "$handoff_script" "$stage_dir" >/dev/null; then
    echo "Failed to refresh release build handoff for nightly stage: $stage_dir" >&2
    exit 1
  fi

  if [[ ! -f "$stage_dir/RELEASE_BUILD_HANDOFF.generated.json" ]]; then
    echo "Nightly stage is missing RELEASE_BUILD_HANDOFF.generated.json after refresh: $stage_dir" >&2
    exit 1
  fi
}

emit_windows_visual_proof_handoff_guidance() {
  local stage_dir="$1"
  python3 - "$stage_dir" <<'PY'
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


stage_dir = Path(sys.argv[1])
handoff_payload = {}
handoff_path = None

direct_path = stage_dir / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
if direct_path.is_file():
    handoff_payload = load_json(direct_path)
    handoff_path = direct_path

if not handoff_payload:
    release_handoff_path = stage_dir / "RELEASE_BUILD_HANDOFF.generated.json"
    if release_handoff_path.is_file():
        release_handoff = load_json(release_handoff_path)
        candidate = release_handoff.get("windows_visual_proof_handoff")
        if isinstance(candidate, dict):
            handoff_payload = candidate
            candidate_path = normalize(candidate.get("json_path"))
            if candidate_path:
                handoff_path = Path(candidate_path)

if not handoff_payload:
    raise SystemExit(1)

status = normalize(handoff_payload.get("status"))
summary = normalize(handoff_payload.get("summary"))
next_actions = handoff_payload.get("next_actions") if isinstance(handoff_payload.get("next_actions"), list) else []
json_path = normalize(handoff_payload.get("json_path")) or str(handoff_path or "")
json_artifact_path = None
if handoff_path and handoff_path.is_file():
    json_artifact_path = handoff_path
elif json_path:
    candidate = Path(json_path)
    if candidate.is_file():
        json_artifact_path = candidate
blockers = handoff_payload.get("blockers")
blockers_present = isinstance(blockers, list) and any(str(item).strip() for item in blockers)
actionable = status in {"ready", "ready_for_windows_host"} and not blockers_present and json_artifact_path is not None

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
raise SystemExit(0 if actionable else 2)
PY
}

verify_latest_stage_windows_payload_gate() {
  local stage_dir="$1"
  local files_dir="$stage_dir/files"
  local releases_manifest="$stage_dir/releases.json"
  local release_channel_manifest="$stage_dir/RELEASE_CHANNEL.generated.json"
  local -a gate_args=(
    --files-dir "$files_dir"
    --allow-empty
    --require-embedded-bootstrap-metadata
    --require-manifest-row
  )

  if [[ ! -f "$SCRIPT_DIR/verify-windows-installer-payloads.py" ]]; then
    echo "Missing Windows installer payload gate: $SCRIPT_DIR/verify-windows-installer-payloads.py" >&2
    exit 1
  fi

  if [[ ! -d "$files_dir" ]]; then
    echo "Nightly stage is missing files directory: $files_dir" >&2
    exit 1
  fi

  [[ -f "$releases_manifest" ]] && gate_args+=(--manifest "$releases_manifest")
  [[ -f "$release_channel_manifest" ]] && gate_args+=(--manifest "$release_channel_manifest")

  if ! python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${gate_args[@]}"; then
    echo "Nightly stage failed Windows installer payload preflight. Build a fresh stage before publishing." >&2
    exit 1
  fi
}

verify_latest_stage_windows_startup_smoke_gate() {
  local stage_dir="$1"
  local files_dir="$stage_dir/files"
  local releases_manifest="$stage_dir/releases.json"
  local release_channel_manifest="$stage_dir/RELEASE_CHANNEL.generated.json"
  local startup_smoke_dir="$stage_dir/startup-smoke"

  if [[ ! -f "$release_channel_manifest" ]]; then
    echo "Nightly stage is missing release channel manifest: $release_channel_manifest" >&2
    exit 1
  fi

  if ! python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$release_channel_manifest" \
    --downloads-manifest "$releases_manifest" \
    --startup-smoke-dir "$startup_smoke_dir" \
    --files-dir "$files_dir"
  then
    echo "Nightly stage failed Windows installer startup smoke preflight. Build and smoke-test a fresh stage before publishing." >&2
    exit 1
  fi
}

verify_latest_stage_windows_exit_gate() {
  local stage_dir="$1"
  local files_dir="$stage_dir/files"
  local release_channel_manifest="$stage_dir/RELEASE_CHANNEL.generated.json"
  local visual_proof_path="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-$stage_dir/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"
  local gate_output
  gate_output="$(mktemp)"

  if [[ ! -f "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" ]]; then
    echo "Missing Windows desktop exit gate: $SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >&2
    rm -f "$gate_output"
    exit 1
  fi

  if ! CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$release_channel_manifest" \
    CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$files_dir" \
    CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path" \
    CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output" \
    bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null
  then
    local handoff_status=0
    rm -f "$gate_output"
    if emit_windows_visual_proof_handoff_guidance "$stage_dir"; then
      handoff_status=0
    else
      handoff_status="$?"
    fi
    if (( handoff_status == 0 )) && to_bool "$ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" && manifest_channel_is_preview "$release_channel_manifest"; then
      echo "Nightly stage is carrying a Windows visual proof handoff instead of a passable Windows visual proof. Continuing because this lane publishes preview handoffs, not stable releases." >&2
      return 0
    fi
    if (( handoff_status == 1 )); then
      echo "Nightly stage failed Windows desktop exit gate preflight and no actionable Windows visual proof handoff was materialized." >&2
    else
      echo "Nightly stage failed Windows desktop exit gate preflight. Use the Windows visual proof handoff above before publishing." >&2
    fi
    exit 1
  fi

  rm -f "$gate_output"
}

verify_latest_stage_layout() {
  local stage_dir="$1"
  local normalized_stage_dir="${stage_dir%/}"
  local parent_dir
  parent_dir="$(dirname "$normalized_stage_dir")"
  local files_dir="$stage_dir/files"
  local nested_files_dir="$files_dir/files"

  if [[ "$(basename "$normalized_stage_dir")" == "files" ]] \
    && [[ -f "$parent_dir/releases.json" || -f "$parent_dir/RELEASE_CHANNEL.generated.json" ]]; then
    echo "Nightly staging root points at files/ directory: $normalized_stage_dir" >&2
    echo "Build the nightly stage root, not its files/ child, before publishing." >&2
    exit 1
  fi

  if [[ -d "$nested_files_dir" ]] && find "$nested_files_dir" -mindepth 1 -maxdepth 1 | grep -q .; then
    echo "Nightly stage is malformed: found nested files directory under $nested_files_dir" >&2
    echo "Build the nightly stage root, not its files/ child, before publishing." >&2
    exit 1
  fi
}

is_publishable_nightly_stage() {
  local stage_dir="$1"
  local release_channel_manifest="$stage_dir/RELEASE_CHANNEL.generated.json"

  [[ -f "$stage_dir/RELEASE_CHANNEL.generated.json" ]] || return 1
  [[ -f "$stage_dir/releases.json" ]] || return 1
  [[ -d "$stage_dir/files" ]] || return 1

  local release_channel
  release_channel="$(python3 - "$release_channel_manifest" <<'PY'
import json
import sys
from pathlib import Path

payload = {}
try:
    payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
except Exception:
    sys.exit(1)

if not isinstance(payload, dict):
    sys.exit(1)

release_channel = str(payload.get("channel") or payload.get("channelId") or "").strip().lower()
if not release_channel:
    sys.exit(1)

print(release_channel)
PY
)" || return 1

  if [[ "$release_channel" != "preview" ]]; then
    return 1
  fi
}

verify_public_edge_open_public_install_routes() {
  local manifest_path="$1"
  local base_url="$2"
  local public_host="$3"
  local forwarded_proto="$4"

  if [[ ! -f "$manifest_path" ]]; then
    echo "Published downloads shelf is missing canonical manifest for install-route verification: $manifest_path" >&2
    exit 1
  fi

  if ! python3 - "$manifest_path" "$base_url" "$public_host" "$forwarded_proto" <<'PY'
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import unquote

manifest_path = Path(sys.argv[1])
base_url = sys.argv[2].rstrip("/")
public_host = sys.argv[3].strip()
forwarded_proto = sys.argv[4].strip()

payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
downloads = []
for key in ("downloads", "artifacts"):
    downloads.extend(
        item for item in payload.get(key) or []
        if isinstance(item, dict) and str(item.get("artifactId") or item.get("id") or "").strip()
    )

def norm(value):
    return str(value or "").strip().lower()

def is_public_desktop_installer(download):
    install_access_class = norm(download.get("installAccessClass"))
    platform = norm(download.get("platformId") or download.get("platform"))
    kind = norm(download.get("kind") or download.get("format"))
    return (
        install_access_class == "open_public"
        and ("windows" in platform or "linux" in platform or platform.startswith("win-") or platform.startswith("linux-"))
        and kind in {"installer", "msix", "deb"}
    )

class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

opener = urllib.request.build_opener(NoRedirectHandler)
errors = []

for download in downloads:
    if not is_public_desktop_installer(download):
        continue
    artifact_id = str(download.get("artifactId") or download.get("id") or "").strip()
    route = f"/downloads/install/{artifact_id}"
    expected_location = f"/downloads/get/{artifact_id}"
    headers = {}
    if public_host:
        headers["Host"] = public_host
    if forwarded_proto:
        headers["X-Forwarded-Proto"] = forwarded_proto
    request = urllib.request.Request(f"{base_url}{route}", method="HEAD", headers=headers)
    try:
        response = opener.open(request)
        status = getattr(response, "status", None) or response.getcode()
        location = response.headers.get("Location", "")
    except urllib.error.HTTPError as exc:
        status = exc.code
        location = exc.headers.get("Location", "")
    except Exception as exc:
        errors.append(f"{route}: request failed ({exc})")
        continue

    decoded_location = unquote(location or "")
    if status not in {301, 302, 303, 307, 308}:
        errors.append(f"{route}: expected redirect status, got {status}")
        continue
    if decoded_location != expected_location and not decoded_location.endswith(expected_location):
        errors.append(f"{route}: expected redirect to {expected_location}, got {location or '<empty>'}")
        continue
    if "/login?next=" in decoded_location:
        errors.append(f"{route}: redirected back to login instead of direct public download")
        continue

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    raise SystemExit(1)
PY
  then
    echo "Published downloads shelf failed open-public installer route verification." >&2
    exit 1
  fi
}

publish_guard_result="$(
  python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$DAILY_PUBLISH_TIMEZONE" "$DAILY_PUBLISH_HOUR" "$FORCE_NIGHTLY_PUBLISH" <<'PY'
import json
import pathlib
import sys
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

manifest_path = pathlib.Path(sys.argv[1])
timezone_name = sys.argv[2]
publish_hour_raw = sys.argv[3]
force_raw = sys.argv[4].strip().lower()
force = force_raw in {"1", "true", "yes", "on"}

try:
    publish_hour = int(publish_hour_raw)
except ValueError as exc:
    raise SystemExit(f"Invalid CHUMMER_DAILY_NIGHTLY_PUBLISH_HOUR: {publish_hour_raw!r}") from exc
if publish_hour < 0 or publish_hour > 23:
    raise SystemExit(f"Invalid CHUMMER_DAILY_NIGHTLY_PUBLISH_HOUR: {publish_hour!r}")

try:
    local_tz = ZoneInfo(timezone_name)
except Exception as exc:
    raise SystemExit(f"Invalid CHUMMER_DAILY_NIGHTLY_PUBLISH_TIMEZONE: {timezone_name!r}") from exc

now = datetime.now(local_tz)
stamp = now.strftime("%Y-%m-%d %H:%M:%S %Z")

if force:
    print(f"ALLOW manual force override at {stamp}")
    raise SystemExit(0)

if now.hour < publish_hour:
    print(f"SKIP daily publish window opens at {publish_hour:02d}:00 {timezone_name}; now {stamp}")
    raise SystemExit(0)

if manifest_path.is_file():
    payload = json.loads(manifest_path.read_text(encoding="utf-8"))
    published_raw = payload.get("publishedAt") or payload.get("generatedAt") or payload.get("generated_at_utc")
    if isinstance(published_raw, str) and published_raw.strip():
        normalized = published_raw.strip()
        if normalized.endswith("Z"):
            normalized = normalized[:-1] + "+00:00"
        published_at = datetime.fromisoformat(normalized)
        if published_at.tzinfo is None:
            published_at = published_at.replace(tzinfo=timezone.utc)
        published_local = published_at.astimezone(local_tz)
        if published_local.date() == now.date():
            published_stamp = published_local.strftime("%Y-%m-%d %H:%M:%S %Z")
            print(f"SKIP downloads shelf already published today at {published_stamp}")
            raise SystemExit(0)

print(f"ALLOW daily publish window open at {stamp}")
PY
)"
echo "$publish_guard_result"
case "$publish_guard_result" in
  ALLOW\ *) ;;
  SKIP\ *) exit 0 ;;
  *)
    echo "Unexpected publish guard result: $publish_guard_result" >&2
    exit 1
    ;;
esac

if [[ ! -d "$STAGING_ROOT" ]]; then
  echo "Nightly staging root not found: $STAGING_ROOT" >&2
  exit 1
fi

latest_stage=""
verify_latest_stage_layout "$STAGING_ROOT"

if is_publishable_nightly_stage "$STAGING_ROOT"; then
  latest_stage="$STAGING_ROOT"
else
  while IFS= read -r candidate; do
    if ! is_publishable_nightly_stage "$candidate"; then
      continue
    fi
    latest_stage="$candidate"
  done < <(find "$STAGING_ROOT" -maxdepth 1 -mindepth 1 \( -type d -o -type l \) -name 'nightly-run-*' | sort)
fi

if [[ -z "$latest_stage" ]]; then
  echo "No publishable nightly stage found under $STAGING_ROOT" >&2
  exit 1
fi

if [[ ! -f "$latest_stage/RELEASE_CHANNEL.generated.json" ]]; then
  echo "Nightly stage is missing RELEASE_CHANNEL.generated.json: $latest_stage" >&2
  exit 1
fi

refresh_release_build_handoff "$latest_stage"

verify_latest_stage_layout "$latest_stage"
verify_latest_stage_windows_payload_gate "$latest_stage"
verify_latest_stage_windows_startup_smoke_gate "$latest_stage"
verify_latest_stage_windows_exit_gate "$latest_stage"

echo "Publishing latest nightly stage: $latest_stage"
echo "Target downloads shelf: $DEPLOY_DIR"
echo "Public release channel: $PUBLIC_RELEASE_CHANNEL"

expected_version="$(
  python3 - "$latest_stage/RELEASE_CHANNEL.generated.json" <<'PY'
import json
import pathlib
import sys

payload = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
version = payload.get("version")
if not isinstance(version, str) or not version.strip():
    raise SystemExit("Nightly stage manifest is missing a non-empty version.")
print(version.strip())
PY
)"

RELEASE_CHANNEL="$PUBLIC_RELEASE_CHANNEL" \
CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" \
CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="$REQUIRE_COMPLETE_DESKTOP_COVERAGE" \
CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS="$PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS" \
CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION="$SKIP_STARTUP_SMOKE_HYDRATION" \
CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE="$ALLOW_SKIPPED_STARTUP_SMOKE" \
CHUMMER_ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH="$ALLOW_WINDOWS_VISUAL_PROOF_HANDOFF_PUBLISH" \
bash "$SCRIPT_DIR/publish-download-bundle.sh" "$latest_stage" "$DEPLOY_DIR"

if to_bool "$REDEPLOY_PUBLIC_EDGE" && [[ "$DEPLOY_DIR" == "$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads" ]]; then
  echo "Redeploying public edge to pick up refreshed downloads shelf"
  (
    cd "$WORKSPACE_ROOT/chummer.run-services"
    docker compose -f docker-compose.public-edge.yml up -d
  )
  verify_public_edge_open_public_install_routes \
    "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    "$PUBLIC_EDGE_VERIFY_BASE_URL" \
    "$PUBLIC_EDGE_VERIFY_HOST" \
    "$PUBLIC_EDGE_VERIFY_PROTO"
fi

python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$expected_version" <<'PY'
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
expected_version = sys.argv[2]
payload = json.loads(manifest_path.read_text(encoding="utf-8"))
actual_version = payload.get("version")
if actual_version != expected_version:
    raise SystemExit(
        f"Published downloads shelf version mismatch: expected {expected_version!r}, got {actual_version!r}."
    )
print(f"Verified published downloads shelf version: {actual_version}")
PY

echo "Published latest nightly to downloads shelf."
