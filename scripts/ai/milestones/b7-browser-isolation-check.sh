#!/usr/bin/env bash
set -euo pipefail

echo "[B7] checking browser deployment signoff guardrails..."

portal_base_url="${CHUMMER_PORTAL_SIGNOFF_BASE_URL:-http://chummer-portal:8080}"
runtime_probe_required="${CHUMMER_B7_RUNTIME_REQUIRED:-}"
runtime_probe_skip_allowed="${CHUMMER_B7_ALLOW_RUNTIME_SKIP:-0}"
if [[ -z "$runtime_probe_required" ]]; then
  runtime_probe_required="1"
fi

require_contains() {
  local path="$1"
  local pattern="$2"
  local message="$3"
  if ! rg -q "$pattern" "$path"; then
    echo "$message"
    exit 3
  fi
}

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "FileExtensionContentTypeProvider" \
  "[B7] FAIL: Avalonia browser host is missing static-asset content-type configuration."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "Mappings\\[\"\\.wasm\"\\] = \"application/wasm\"" \
  "[B7] FAIL: Avalonia browser host is missing explicit .wasm MIME configuration."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "Cross-Origin-Opener-Policy" \
  "[B7] FAIL: Avalonia browser host is missing the COOP header."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "same-origin" \
  "[B7] FAIL: Avalonia browser host is missing the required COOP value."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "Cross-Origin-Embedder-Policy" \
  "[B7] FAIL: Avalonia browser host is missing the COEP header."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "require-corp" \
  "[B7] FAIL: Avalonia browser host is missing the required COEP value."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "MapFallbackToFile\\(\"index\\.html\"\\)" \
  "[B7] FAIL: Avalonia browser host is missing SPA fallback routing."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "crossOriginOpenerPolicy" \
  "[B7] FAIL: Avalonia browser health payload is missing COOP metadata."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "crossOriginEmbedderPolicy" \
  "[B7] FAIL: Avalonia browser health payload is missing COEP metadata."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "requiresCrossOriginIsolation = true" \
  "[B7] FAIL: Avalonia browser health payload is missing the cross-origin isolation requirement flag."

require_contains \
  "Chummer.Avalonia.Browser/Program.cs" \
  "wasmMimeType = \"application/wasm\"" \
  "[B7] FAIL: Avalonia browser health payload is missing wasm MIME metadata."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/index.html" \
  "crossOriginIsolated" \
  "[B7] FAIL: Avalonia browser host page is missing the browser isolation probe."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/index.html" \
  "Degraded browser mode" \
  "[B7] FAIL: Avalonia browser host page is missing the degraded browser banner."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/index.html" \
  "navigator\\.serviceWorker\\.register" \
  "[B7] FAIL: Avalonia browser host page is missing service-worker registration."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/index.html" \
  "service-worker\\.js" \
  "[B7] FAIL: Avalonia browser host page is missing the service-worker asset reference."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/index.html" \
  "health" \
  "[B7] FAIL: Avalonia browser host page is missing the health-route diagnostics wiring."

if [[ ! -f "Chummer.Avalonia.Browser/wwwroot/service-worker.js" ]]; then
  echo "[B7] FAIL: Avalonia browser host is missing its service-worker shell."
  exit 4
fi

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/service-worker.js" \
  "const CACHE_NAME = \"chummer-avalonia-browser-host-v[0-9]+\"" \
  "[B7] FAIL: service-worker cache is missing a versioned cache key."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/service-worker.js" \
  "caches\\.keys\\(" \
  "[B7] FAIL: service-worker cache cleanup is missing the cache-enumeration step."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/service-worker.js" \
  "caches\\.delete\\(" \
  "[B7] FAIL: service-worker cache cleanup is missing stale-cache deletion."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/service-worker.js" \
  "request\\.mode === \"navigate\"" \
  "[B7] FAIL: service-worker navigation fallback is missing navigation request detection."

require_contains \
  "Chummer.Avalonia.Browser/wwwroot/service-worker.js" \
  "caches\\.match\\(\"\\./index\\.html\"\\)" \
  "[B7] FAIL: service-worker navigation fallback is missing cached index.html fallback."

require_contains \
  "scripts/e2e-portal.cjs" \
  "deep-link-check" \
  "[B7] FAIL: portal deployment probe is missing blazor deep-link refresh coverage."

require_contains \
  "scripts/e2e-portal.cjs" \
  "deep-link-signoff" \
  "[B7] FAIL: portal deployment probe is missing avalonia deep-link refresh coverage."

require_contains \
  "scripts/e2e-portal.cjs" \
  "cross-origin-opener-policy" \
  "[B7] FAIL: portal deployment probe is missing COOP header assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "cross-origin-embedder-policy" \
  "[B7] FAIL: portal deployment probe is missing COEP header assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "payload\\?\\.staticAssets\\?\\.wasmMimeType === 'application/wasm'" \
  "[B7] FAIL: portal deployment probe is missing wasm MIME assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "service-worker\\.js" \
  "[B7] FAIL: portal deployment probe is missing service-worker asset assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "text\\.includes\\('chummer-avalonia-browser-host-v'\\)" \
  "[B7] FAIL: portal deployment probe is missing versioned cache assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "caches\\.open" \
  "[B7] FAIL: portal deployment probe is missing cache-open assertions."

require_contains \
  "scripts/e2e-portal.cjs" \
  "caches\\.keys" \
  "[B7] FAIL: portal deployment probe is missing cache-enumeration assertions."

portal_probe_ready=0
if node -e '
const url = process.argv[1];
const controller = new AbortController();
const timeout = setTimeout(() => controller.abort(), 3000);
fetch(`${url}/api/health`, { signal: controller.signal })
  .then(response => {
    clearTimeout(timeout);
    process.exit(response.ok ? 0 : 1);
  })
  .catch(() => {
    clearTimeout(timeout);
    process.exit(1);
  });
' "$portal_base_url"; then
  portal_probe_ready=1
fi

if [[ "$portal_probe_ready" -eq 1 ]]; then
  echo "[B7] executing runtime deployment probe against $portal_base_url..."
  CHUMMER_PORTAL_BASE_URL="$portal_base_url" node scripts/e2e-portal.cjs
elif [[ "$runtime_probe_required" == "1" ]]; then
  if [[ "$runtime_probe_skip_allowed" == "1" ]]; then
    echo "[B7] note: runtime deployment probe explicitly skipped because CHUMMER_B7_ALLOW_RUNTIME_SKIP=1 and $portal_base_url is unavailable."
  else
    echo "[B7] FAIL: portal runtime probe target is unavailable at $portal_base_url. Start the portal stack, set CHUMMER_PORTAL_SIGNOFF_BASE_URL to a reachable deployment, or set CHUMMER_B7_ALLOW_RUNTIME_SKIP=1 for an explicit local skip."
    exit 5
  fi
else
  echo "[B7] note: runtime deployment probe not required in this invocation."
fi

echo "[B7] PASS: browser deployment signoff guardrails are present (fallback, wasm MIME, isolation headers, service-worker cache behavior)."
