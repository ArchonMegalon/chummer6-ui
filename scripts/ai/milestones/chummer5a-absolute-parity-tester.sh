#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

ultimate_script="${CHUMMER5A_ULTIMATE_PARITY_SCRIPT:-$repo_root/scripts/ai/milestones/chummer5a-ultimate-parity-tester.sh}"
published_receipt="${CHUMMER5A_ABSOLUTE_PARITY_PUBLISHED_RECEIPT:-$repo_root/.codex-studio/published/CHUMMER5A_ABSOLUTE_PARITY_TESTER.generated.json}"
ultimate_receipt="${CHUMMER5A_ULTIMATE_PARITY_PUBLISHED_RECEIPT:-$repo_root/.codex-studio/published/CHUMMER5A_ULTIMATE_PARITY_TESTER.generated.json}"
visual_parity_audit="${CHUMMER5A_DESKTOP_VISUAL_PARITY_AUDIT_PATH:-$repo_root/.codex-studio/published/DESKTOP_VISUAL_PARITY_AUDIT.generated.json}"
screenshot_review_receipt="${CHUMMER5A_SCREENSHOT_REVIEW_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_SCREENSHOT_REVIEW_GATE.generated.json}"
visual_familiarity_receipt="${CHUMMER5A_VISUAL_FAMILIARITY_GATE_PATH:-$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json}"
ui_element_audit_receipt="${CHUMMER5A_UI_ELEMENT_PARITY_AUDIT_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_UI_ELEMENT_PARITY_AUDIT.generated.json}"
visual_difference_ledger="${CHUMMER5A_VISUAL_DIFFERENCE_LEDGER_PATH:-$repo_root/docs/CHUMMER5A_VISUAL_DIFFERENCE_LEDGER.json}"

mkdir -p "$(dirname "$published_receipt")"

ultimate_exit=0
if bash "$ultimate_script" >/dev/null; then
  ultimate_exit=0
else
  ultimate_exit=$?
fi

absolute_exit="$(
CHUMMER5A_ABSOLUTE_PARITY_PUBLISHED_RECEIPT="$published_receipt" \
CHUMMER5A_ULTIMATE_PARITY_PUBLISHED_RECEIPT="$ultimate_receipt" \
CHUMMER5A_DESKTOP_VISUAL_PARITY_AUDIT_PATH="$visual_parity_audit" \
CHUMMER5A_SCREENSHOT_REVIEW_GATE_PATH="$screenshot_review_receipt" \
CHUMMER5A_VISUAL_FAMILIARITY_GATE_PATH="$visual_familiarity_receipt" \
CHUMMER5A_UI_ELEMENT_PARITY_AUDIT_PATH="$ui_element_audit_receipt" \
CHUMMER5A_VISUAL_DIFFERENCE_LEDGER_PATH="$visual_difference_ledger" \
CHUMMER5A_ULTIMATE_PARITY_EXIT="$ultimate_exit" \
python3 - <<'PY'
import json
import os
from pathlib import Path


def load_json(path_str: str, reasons: list[str], label: str):
    path = Path(path_str)
    if not path.is_file():
        reasons.append(f"Missing {label}: {path}")
        return {}, path
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            return json.load(handle), path
    except Exception as exc:
        reasons.append(f"Could not read {label} at {path}: {exc}")
        return {}, path


reasons: list[str] = []
published_receipt_path = Path(os.environ["CHUMMER5A_ABSOLUTE_PARITY_PUBLISHED_RECEIPT"])
ultimate_exit = int(os.environ["CHUMMER5A_ULTIMATE_PARITY_EXIT"])

ultimate_receipt, ultimate_receipt_path = load_json(
    os.environ["CHUMMER5A_ULTIMATE_PARITY_PUBLISHED_RECEIPT"], reasons, "ultimate parity receipt"
)
visual_parity_audit, visual_parity_audit_path = load_json(
    os.environ["CHUMMER5A_DESKTOP_VISUAL_PARITY_AUDIT_PATH"], reasons, "desktop visual parity audit"
)
screenshot_review_receipt, screenshot_review_receipt_path = load_json(
    os.environ["CHUMMER5A_SCREENSHOT_REVIEW_GATE_PATH"], reasons, "Chummer5a screenshot review gate"
)
visual_familiarity_receipt, visual_familiarity_receipt_path = load_json(
    os.environ["CHUMMER5A_VISUAL_FAMILIARITY_GATE_PATH"], reasons, "desktop visual familiarity gate"
)
ui_element_audit_receipt, ui_element_audit_receipt_path = load_json(
    os.environ["CHUMMER5A_UI_ELEMENT_PARITY_AUDIT_PATH"], reasons, "UI element parity audit"
)
visual_difference_ledger, visual_difference_ledger_path = load_json(
    os.environ["CHUMMER5A_VISUAL_DIFFERENCE_LEDGER_PATH"], reasons, "visual difference ledger"
)

def status_pass(payload: dict) -> bool:
    return str(payload.get("status") or "").strip().lower() in {"pass", "passed", "ready"}


ultimate_receipt_pass = status_pass(ultimate_receipt) if isinstance(ultimate_receipt, dict) else False
if ultimate_exit != 0 and not ultimate_receipt_pass:
    reasons.append(f"Ultimate parity tester exited with code {ultimate_exit}.")

for payload, label in (
    (ultimate_receipt, "ultimate parity receipt"),
    (visual_parity_audit, "desktop visual parity audit"),
    (screenshot_review_receipt, "Chummer5a screenshot review gate"),
    (visual_familiarity_receipt, "desktop visual familiarity gate"),
    (ui_element_audit_receipt, "UI element parity audit"),
):
    if payload and not status_pass(payload):
        reasons.append(f"{label} is not passing.")

proof_scope = ultimate_receipt.get("proofScope") if isinstance(ultimate_receipt, dict) else {}
if not isinstance(proof_scope, dict):
    proof_scope = {}
for key in (
    "uiReconstructionExecuted",
    "certifiesSelectedFixturesCanBeRebuiltOnlyUsingUi",
    "certifiesEveryFixtureCanBeRebuiltOnlyUsingUi",
    "recursiveSettingsAndElementsCertified",
):
    if proof_scope.get(key) is not True:
        reasons.append(f"ultimate proofScope.{key} is not true.")

visual_entries = visual_difference_ledger.get("entries") if isinstance(visual_difference_ledger, dict) else None
if not isinstance(visual_entries, list) or not visual_entries:
    reasons.append(f"Visual difference ledger must expose a non-empty entries array: {visual_difference_ledger_path}")
    visual_entries = []

required_screenshots = (
    visual_familiarity_receipt.get("evidence", {}).get("required_screenshots")
    if isinstance(visual_familiarity_receipt, dict)
    else []
)
if not isinstance(required_screenshots, list):
    required_screenshots = []
required_screenshot_set = {str(item).strip() for item in required_screenshots if str(item).strip()}
ledger_screenshot_set = {
    str(entry.get("screenshot")).strip()
    for entry in visual_entries
    if isinstance(entry, dict) and str(entry.get("screenshot")).strip()
}
missing_screenshots = sorted(required_screenshot_set - ledger_screenshot_set)
if missing_screenshots:
    reasons.append(
        "Visual difference ledger is missing required screenshot coverage: " + ", ".join(missing_screenshots) + "."
    )

ui_element_notes = ui_element_audit_receipt.get("notes") if isinstance(ui_element_audit_receipt, dict) else []
if not isinstance(ui_element_notes, list):
    ui_element_notes = []
pixel_diff_caveat = next(
    (
        str(note)
        for note in ui_element_notes
        if "true dual-product per-control pixel diff" in str(note).lower()
    ),
    "",
)

status = "pass" if not reasons else "fail"
payload = {
    "contract_name": "chummer6-ui.chummer5a_absolute_parity_tester",
    "status": status,
    "summary": (
        "Absolute parity gate passed across the full fixture corpus, recursive workflow proof, screenshot review, and per-control visual-difference corpus."
        if status == "pass"
        else "Absolute parity gate failed because the repo still lacks at least one literal every-detail proof lane."
    ),
    "proofScope": {
        **proof_scope,
        "visualParityAuditCertified": status_pass(visual_parity_audit) if isinstance(visual_parity_audit, dict) else False,
        "screenshotReviewCertified": status_pass(screenshot_review_receipt) if isinstance(screenshot_review_receipt, dict) else False,
        "visualFamiliarityCertified": status_pass(visual_familiarity_receipt) if isinstance(visual_familiarity_receipt, dict) else False,
        "visualDifferenceLedgerPresent": bool(visual_entries),
        "visualDifferenceLedgerCoverageComplete": not missing_screenshots,
        "perControlPixelDiffCorpusCertified": not bool(pixel_diff_caveat),
    },
    "strictFailureReasons": reasons,
    "notes": ([pixel_diff_caveat] if pixel_diff_caveat else []),
    "evidenceSources": {
        "ultimateParityReceipt": str(ultimate_receipt_path),
        "desktopVisualParityAudit": str(visual_parity_audit_path),
        "screenshotReviewGate": str(screenshot_review_receipt_path),
        "visualFamiliarityGate": str(visual_familiarity_receipt_path),
        "uiElementParityAudit": str(ui_element_audit_receipt_path),
        "visualDifferenceLedger": str(visual_difference_ledger_path),
    },
}

published_receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
print(0 if status == "pass" else 1)
PY
)"

if [[ "$absolute_exit" -eq 0 ]]; then
  echo "[chummer5a-absolute-parity-tester] PASS"
else
  echo "[chummer5a-absolute-parity-tester] FAIL" >&2
fi

exit "$absolute_exit"
