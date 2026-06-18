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
PUBLIC_RELEASE_CHANNEL="${CHUMMER_PUBLIC_DEFAULT_RELEASE_CHANNEL:-stable}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

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
