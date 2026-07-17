#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd)"
PLAYWRIGHT_SCRIPT="$SCRIPT_DIR/e2e-build-pwa-responsive-playwright.cjs"
PLAYWRIGHT_NODE_PATH=""

for candidate in \
  "${NODE_PATH:-}" \
  "$WORKSPACE_ROOT/chummer.run-services/node_modules" \
  "$WORKSPACE_ROOT/node_modules" \
  "$SCRIPT_DIR/node_modules"
do
  if [[ -n "$candidate" ]] \
    && [[ -d "$candidate" ]] \
    && NODE_PATH="$candidate" node -e "require('playwright');" >/dev/null 2>&1
  then
    PLAYWRIGHT_NODE_PATH="$candidate"
    break
  fi
done

if [[ -z "$PLAYWRIGHT_NODE_PATH" ]]; then
  echo "Build PWA responsive E2E requires a local Playwright installation." >&2
  exit 1
fi

port="$(python3 -c 'import socket; handle = socket.socket(); handle.bind(("127.0.0.1", 0)); print(handle.getsockname()[1]); handle.close()')"
base_url="http://127.0.0.1:${port}"
log_dir="$REPO_ROOT/.tmp"
log_file="$log_dir/build-pwa-responsive-e2e-${port}.log"
mkdir -p "$log_dir"
state_dir="$(mktemp -d "$log_dir/build-pwa-responsive-e2e-state.XXXXXX")"

server_pid=""
cleanup() {
  if [[ -n "$server_pid" ]] && kill -0 "$server_pid" >/dev/null 2>&1; then
    kill "$server_pid" >/dev/null 2>&1 || true
    wait "$server_pid" >/dev/null 2>&1 || true
  fi
  rm -rf -- "$state_dir"
}
trap cleanup EXIT INT TERM

cd "$REPO_ROOT"
bash scripts/ai/build.sh \
  Chummer.Blazor/Chummer.Blazor.csproj \
  -f net10.0 \
  --no-restore \
  -m:1 \
  -p:UseSharedCompilation=false \
  -v:minimal

ASPNETCORE_URLS="$base_url" \
ASPNETCORE_ENVIRONMENT=Test \
DOTNET_ENVIRONMENT=Test \
CHUMMER_BLAZOR_PATH_BASE= \
CHUMMER_ANALYTICS_PROVIDER=none \
CHUMMER_API_BASE_URL=http://127.0.0.1:65535 \
CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL=true \
CHUMMER_STATE_PATH="$state_dir" \
dotnet run \
  --project Chummer.Blazor/Chummer.Blazor.csproj \
  --no-launch-profile \
  --no-build \
  >"$log_file" 2>&1 &
server_pid=$!

deadline=$((SECONDS + 240))
while (( SECONDS < deadline )); do
  if ! kill -0 "$server_pid" >/dev/null 2>&1; then
    echo "Build PWA test server exited before readiness." >&2
    tail -n 120 "$log_file" >&2 || true
    exit 1
  fi
  if curl --fail --silent --show-error --max-time 3 "$base_url/health/live" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl --fail --silent --show-error --max-time 3 "$base_url/health/live" >/dev/null; then
  echo "Build PWA test server did not become ready at $base_url." >&2
  tail -n 120 "$log_file" >&2 || true
  exit 1
fi

NODE_PATH="$PLAYWRIGHT_NODE_PATH" \
CHUMMER_BLAZOR_BASE_URL="$base_url" \
CHUMMER_BUILD_PWA_URL="$base_url/app?fixture=blue&tab=tab-create" \
node "$PLAYWRIGHT_SCRIPT"
