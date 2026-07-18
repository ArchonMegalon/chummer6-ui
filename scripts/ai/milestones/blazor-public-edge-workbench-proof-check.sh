#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

receipt_path="${CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH:-$repo_root/.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json}"
base_url="${CHUMMER_BLAZOR_PUBLIC_EDGE_BASE_URL:-https://chummer.run}"
timeout_seconds="${CHUMMER_BLAZOR_PUBLIC_EDGE_TIMEOUT_SECONDS:-20}"
max_redirects="${CHUMMER_BLAZOR_PUBLIC_EDGE_MAX_REDIRECTS:-5}"

python3 "$repo_root/scripts/materialize-blazor-public-edge-workbench-proof.py" \
  --base-url "$base_url" \
  --output "$receipt_path" \
  --timeout-seconds "$timeout_seconds" \
  --max-redirects "$max_redirects"

python3 "$repo_root/scripts/verify_blazor_public_edge_workbench_proof.py" \
  --receipt-path "$receipt_path"
