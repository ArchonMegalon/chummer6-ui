#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

oracle_root="${CHUMMER4_LEGACY_SOURCE_ROOT:-/docker/fleet/repos/chummer4/Chummer}"
receipt_path="${CHUMMER4_LEGACY_UI_ELEMENT_PARITY_RECEIPT_PATH:-$repo_root/.codex-studio/published/CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json}"

LEGACY_UI_PARITY_SUBJECT="Chummer4" \
LEGACY_UI_PARITY_SUBJECT_SLUG="chummer4" \
LEGACY_UI_PARITY_SCRIPT_LABEL="chummer4-legacy-ui-element-parity" \
LEGACY_UI_PARITY_LEGACY_ROOTS="$oracle_root" \
LEGACY_UI_PARITY_VERIFY_BANNER="checking Chummer4 legacy UI element parity guard" \
LEGACY_UI_PARITY_VERIFY_INVOCATION="bash scripts/ai/milestones/chummer4-legacy-ui-element-parity-check.sh" \
LEGACY_UI_PARITY_B14_MARKERS="CHUMMER4_LEGACY_UI_ELEMENT_PARITY.generated.json|chummer4-legacy-ui-element-parity-check.sh|chummer4_legacy_ui_element_parity_receipt" \
LEGACY_UI_PARITY_CONTRACT_NAME="chummer6-ui.chummer4_legacy_ui_element_parity" \
LEGACY_UI_PARITY_RECEIPT_PATH="$receipt_path" \
  bash scripts/ai/milestones/chummer5a-legacy-ui-element-parity-check.sh
