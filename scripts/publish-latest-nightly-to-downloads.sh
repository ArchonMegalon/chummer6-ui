#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd)"

STAGING_ROOT="${CHUMMER_STAGING_ROOT:-$WORKSPACE_ROOT/_staging}"
DEPLOY_DIR="${1:-${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR:-$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads}}"
REDEPLOY_PUBLIC_EDGE="${CHUMMER_REDEPLOY_PUBLIC_EDGE_AFTER_NIGHTLY_PUBLISH:-true}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-true}"
REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-0}"
PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS="${CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS:-0}"
SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-1}"
ALLOW_SKIPPED_STARTUP_SMOKE="${CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE:-1}"
PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-public_stable}"
DAILY_PUBLISH_TIMEZONE="${CHUMMER_DAILY_NIGHTLY_PUBLISH_TIMEZONE:-Europe/Vienna}"
DAILY_PUBLISH_HOUR="${CHUMMER_DAILY_NIGHTLY_PUBLISH_HOUR:-8}"
FORCE_NIGHTLY_PUBLISH="${CHUMMER_FORCE_NIGHTLY_PUBLISH:-0}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
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

latest_stage=""
while IFS= read -r candidate; do
  latest_stage="$candidate"
done < <(find "$STAGING_ROOT" -maxdepth 1 -mindepth 1 -type d -name 'nightly-run-*' | sort)

if [[ -z "$latest_stage" ]]; then
  echo "No nightly stage found under $STAGING_ROOT" >&2
  exit 1
fi

if [[ ! -f "$latest_stage/RELEASE_CHANNEL.generated.json" ]]; then
  echo "Nightly stage is missing RELEASE_CHANNEL.generated.json: $latest_stage" >&2
  exit 1
fi

if [[ ! -f "$latest_stage/RELEASE_BUILD_HANDOFF.generated.json" ]]; then
  echo "Nightly stage is missing RELEASE_BUILD_HANDOFF.generated.json: $latest_stage" >&2
  exit 1
fi

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
bash "$SCRIPT_DIR/publish-download-bundle.sh" "$latest_stage" "$DEPLOY_DIR"

if to_bool "$REDEPLOY_PUBLIC_EDGE" && [[ "$DEPLOY_DIR" == "$WORKSPACE_ROOT/chummer.run-services/Chummer.Portal/downloads" ]]; then
  echo "Redeploying public edge to pick up refreshed downloads shelf"
  (
    cd "$WORKSPACE_ROOT/chummer.run-services"
    docker compose -f docker-compose.public-edge.yml up -d
  )
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
