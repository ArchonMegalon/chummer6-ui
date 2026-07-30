#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
workspace_root="$(cd "$repo_root/.." && pwd -P)"

if [[ "${CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_REFRESH:-0}" == "1" ]]; then
  manifest_path="${CHUMMER_BLAZOR_PUBLIC_EDGE_MANIFEST_PATH:-$workspace_root/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
  downloads_dir="${CHUMMER_BLAZOR_PUBLIC_EDGE_DOWNLOADS_DIR:-$workspace_root/chummer.run-services/Chummer.Portal/downloads/files}"
  startup_smoke_dir="${CHUMMER_BLAZOR_PUBLIC_EDGE_STARTUP_SMOKE_DIR:-$workspace_root/chummer.run-services/Chummer.Portal/downloads/startup-smoke}"
  blocker_output="${CHUMMER_BLAZOR_PUBLIC_EDGE_BLOCKER_OUTPUT:-$repo_root/.codex-studio/published/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json}"
  proof_output="${CHUMMER_BLAZOR_PUBLIC_EDGE_PROOF_OUTPUT:-$repo_root/.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json}"
  base_url="${CHUMMER_BLAZOR_PUBLIC_EDGE_BASE_URL:-https://chummer.run}"
  timeout_seconds="${CHUMMER_BLAZOR_PUBLIC_EDGE_TIMEOUT_SECONDS:-20}"
  max_receipt_age_seconds="${CHUMMER_BLAZOR_PUBLIC_EDGE_MAX_RECEIPT_AGE_SECONDS:-604800}"

  python3 "$repo_root/scripts/materialize-external-host-proof-blockers.py" \
    --manifest "$manifest_path" \
    --downloads-dir "$downloads_dir" \
    --startup-smoke-dir "$startup_smoke_dir" \
    --display-manifest "$manifest_path" \
    --display-downloads-dir "$downloads_dir" \
    --display-startup-smoke-dir "$startup_smoke_dir" \
    --output "$blocker_output" \
    --browser-proof-output "$proof_output" \
    --base-url "$base_url" \
    --timeout-seconds "$timeout_seconds" \
    --max-receipt-age-seconds "$max_receipt_age_seconds"
fi

python3 "$repo_root/scripts/verify_blazor_public_edge_workbench_proof.py"
