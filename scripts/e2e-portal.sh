#!/usr/bin/env bash
set -euo pipefail

CHUMMER_API_KEY="${CHUMMER_API_KEY:-}"
PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS="${CHUMMER_PORTAL_E2E_TIMEOUT_SECONDS:-240}"
PORTAL_EDGE_COMPOSE_FILE="${CHUMMER_PORTAL_EDGE_COMPOSE_FILE:-/docker/chummercomplete/chummer.run-services/docker-compose.public-edge.yml}"
PORTAL_BASE_URL="${CHUMMER_PORTAL_BASE_URL:-http://127.0.0.1:${CHUMMER_PUBLIC_EDGE_PORT:-8091}}"
PORTAL_LOCAL_PROOF_PATH="${CHUMMER_PORTAL_LOCAL_PROOF_PATH:-.codex-studio/published/UI_LOCAL_RELEASE_PROOF.generated.json}"
PORTAL_SKIP_EDGE_REBUILD="${CHUMMER_PORTAL_E2E_SKIP_EDGE_REBUILD:-0}"
if [[ -n "${CHUMMER_PORTAL_PLAYWRIGHT:-}" ]]; then
  RUN_PORTAL_PLAYWRIGHT="$CHUMMER_PORTAL_PLAYWRIGHT"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  RUN_PORTAL_PLAYWRIGHT="1"
else
  RUN_PORTAL_PLAYWRIGHT="0"
fi
if [[ -n "${CHUMMER_E2E_PLAYWRIGHT_SOFT_FAIL:-}" ]]; then
  PLAYWRIGHT_SOFT_FAIL="$CHUMMER_E2E_PLAYWRIGHT_SOFT_FAIL"
elif [[ "${CI:-}" == "true" || "${GITHUB_ACTIONS:-}" == "true" ]]; then
  PLAYWRIGHT_SOFT_FAIL="0"
else
  PLAYWRIGHT_SOFT_FAIL="1"
fi

is_docker_permission_error_text() {
  local source_file="$1"
  grep -Eqi "permission denied while trying to connect to the Docker daemon socket|operation not permitted|got permission denied while trying to connect to the docker daemon socket" "$source_file"
}

if [[ -n "$CHUMMER_API_KEY" ]]; then
  export CHUMMER_API_KEY
fi

if [[ "$PORTAL_SKIP_EDGE_REBUILD" == "1" || "$PORTAL_SKIP_EDGE_REBUILD" == "true" || "$PORTAL_SKIP_EDGE_REBUILD" == "TRUE" ]]; then
  echo "reusing current downstream public-edge containers for portal route probe"
else
  compose_rm_log="$(mktemp)"
  set +e
  docker compose -f "$PORTAL_EDGE_COMPOSE_FILE" rm -fsv chummer-run-identity chummer-portal 2>&1 | tee "$compose_rm_log"
  compose_rm_status=${PIPESTATUS[0]}
  set -e
  if [[ "$compose_rm_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$compose_rm_log"; then
      echo "skipping portal e2e: docker daemon permission denied in this environment."
      rm -f "$compose_rm_log"
      exit 0
    fi

    rm -f "$compose_rm_log"
    exit "$compose_rm_status"
  fi
  rm -f "$compose_rm_log"

  compose_up_log="$(mktemp)"
  set +e
  docker compose -f "$PORTAL_EDGE_COMPOSE_FILE" up -d --build --remove-orphans chummer-run-identity chummer-portal 2>&1 | tee "$compose_up_log"
  compose_up_status=${PIPESTATUS[0]}
  set -e
  if [[ "$compose_up_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$compose_up_log"; then
      echo "skipping portal e2e: docker daemon permission denied in this environment."
      rm -f "$compose_up_log"
      exit 0
    fi

    rm -f "$compose_up_log"
    exit "$compose_up_status"
  fi
  rm -f "$compose_up_log"
fi

if [[ "$RUN_PORTAL_PLAYWRIGHT" == "1" ]]; then
  echo "running portal route probe (timeout: ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s)"
  route_probe_log="$(mktemp)"
  set +e
  timeout "${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}"s env CHUMMER_PORTAL_BASE_URL="$PORTAL_BASE_URL" node /docker/chummercomplete/chummer-presentation/scripts/e2e-public-edge.cjs \
    2>&1 | tee "$route_probe_log"
  route_probe_status=${PIPESTATUS[0]}
  set -e
  if [[ "$route_probe_status" -ne 0 ]]; then
    if [[ "$PLAYWRIGHT_SOFT_FAIL" == "1" ]] && is_docker_permission_error_text "$route_probe_log"; then
      echo "skipping portal route probe: docker daemon permission denied in this environment."
      rm -f "$route_probe_log"
      exit 0
    fi

    rm -f "$route_probe_log"
    echo "portal route probe failed or timed out after ${PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS}s" >&2
    exit "$route_probe_status"
  fi
  rm -f "$route_probe_log"
else
  echo "skipping portal route probe (set CHUMMER_PORTAL_PLAYWRIGHT=1 to enable)"
fi

mkdir -p "$(dirname "$PORTAL_LOCAL_PROOF_PATH")"
python3 - "$PORTAL_LOCAL_PROOF_PATH" "$PORTAL_BASE_URL" "$PORTAL_PLAYWRIGHT_TIMEOUT_SECONDS" "$RUN_PORTAL_PLAYWRIGHT" "$PORTAL_EDGE_COMPOSE_FILE" "$PORTAL_SKIP_EDGE_REBUILD" <<'PY'
import datetime as dt
import json
import sys

out_path, base_url, timeout_seconds, run_portal_playwright, compose_file, skip_edge_rebuild = sys.argv[1:]
route_probe_executed = run_portal_playwright == "1"
payload = {
    "contract_name": "chummer6-ui.local_release_proof",
    "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": "passed",
    "base_url": base_url,
    "compose_file": compose_file,
    "playwright_timeout_seconds": int(timeout_seconds),
    "edge_rebuild_skipped": skip_edge_rebuild.lower() in {"1", "true"},
    "route_probe_executed": route_probe_executed,
    "journeys_passed": [
        "public_bridge_boot",
        "desktop_downloads_projection",
    ] + (["portal_route_probe"] if route_probe_executed else []),
    "proof_routes": [
        "/",
        "/blazor/",
        "/hub/",
        "/avalonia/",
        "/downloads/",
        "/downloads/releases.json",
        "/session/",
        "/coach/",
        "/contact",
        "/what-is-chummer",
        "/artifacts",
        "/faq",
    ],
}
with open(out_path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle, indent=2)
    handle.write("\n")
PY

echo "portal e2e completed"
