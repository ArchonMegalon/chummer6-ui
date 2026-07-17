#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/SR5_SR6_UI_PARITY_AUDIT.generated.json"

python3 scripts/materialize_sr5_sr6_ui_parity_audit.py >/dev/null

python3 - <<'PY' "$receipt_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

receipt_path = Path(sys.argv[1])
payload = json.loads(receipt_path.read_text(encoding="utf-8"))
status = str(payload.get("status") or "").strip().lower()
if status not in {"pass", "passed", "ready"}:
    reasons = payload.get("reasons") or ["missing reason"]
    raise SystemExit(
        "SR5/SR6 UI parity audit receipt is not passing: " + ", ".join(str(reason) for reason in reasons)
    )

evidence = payload.get("evidence") or {}
if any(int(evidence.get(key) or 0) != 0 for key in (
    "partialTabCount",
    "missingTabCount",
    "partialControlCount",
    "missingControlCount",
)):
    raise SystemExit("SR5/SR6 UI parity audit still reports explicit legacy-to-SR6 gaps.")

if any(int(evidence.get(key) or 0) != 0 for key in (
    "missingLegacyElementDispositionCount",
    "familyFallbackLegacyElementDispositionCount",
    "familyReviewsWithUnavailableMappedCurrentIds",
    "legacyElementsWithUnavailableMappedCurrentIds",
    "unavailableMappedCurrentIdCount",
    "nonPendantMappedCurrentIdCount",
    "legacyElementsMissingExplicitSr6Pendants",
    "unsupportedMappedCurrentIdCount",
)):
    raise SystemExit("SR5/SR6 UI parity audit still reports explicit full-spectrum SR5-to-SR6 gaps.")

print(f"[sr5-sr6-ui-parity-audit] PASS: {receipt_path}")
PY
