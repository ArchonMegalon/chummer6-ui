#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_BLAZOR_PLAY_SURFACE_HORIZON_PATH:-$repo_root/.codex-studio/published/BLAZOR_PLAY_SURFACE_HORIZON.generated.json}"

python3 scripts/materialize-blazor-play-surface-horizon.py >/dev/null

python3 - <<'PY' "$receipt_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
payload = json.loads(receipt_path.read_text(encoding="utf-8"))
status = str(payload.get("status") or "").strip().lower()
if status not in {"pass", "passed"}:
    failures = payload.get("failures") or ["missing failure detail"]
    raise SystemExit(
        "Blazor play-surface horizon receipt is not passing: "
        + ", ".join(str(failure) for failure in failures)
    )

contract_name = str(payload.get("contract_name") or "").strip()
if contract_name != "chummer6-ui.blazor_play_surface_horizon":
    raise SystemExit(
        "Blazor play-surface horizon receipt has the wrong contract: "
        + (contract_name or "<missing>")
    )

horizons = payload.get("horizons") or []
horizon_ids = {
    str(item.get("id") or "").strip()
    for item in horizons
    if isinstance(item, dict)
}
required_horizon_ids = {
    "near_term_stabilization",
    "mid_term_pwa_session_utility",
    "long_term_living_world_expansion",
}
missing_horizon_ids = sorted(required_horizon_ids - horizon_ids)
if missing_horizon_ids:
    raise SystemExit(
        "Blazor play-surface horizon receipt is missing required horizon ids: "
        + ", ".join(missing_horizon_ids)
    )

current_release_truth = payload.get("current_release_truth") or {}
if str(current_release_truth.get("pwa_public_edge_status") or "").strip().lower() != "passed":
    raise SystemExit(
        "Blazor play-surface horizon receipt must report a passing hosted PWA public-edge status."
    )

print(f"[blazor-play-surface-horizon] PASS: {receipt_path}")
PY
