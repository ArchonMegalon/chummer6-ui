#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH_ROOT="${CHUMMER_MOUSE_FIRST_PUBLISH_ROOT:-/tmp/chummer-linux-mousefirst-publish}"
LAUNCH_PATH="${1:-$PUBLISH_ROOT/Chummer.Avalonia}"
OUTPUT_ROOT="${2:-$REPO_ROOT/dist/mouse-first-journey-matrix}"

if [[ $# -eq 0 ]]; then
  dotnet publish "$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj" \
    -c Debug \
    -r linux-x64 \
    --self-contained false \
    -o "$PUBLISH_ROOT" \
    -v minimal
fi

if [[ ! -x "$LAUNCH_PATH" ]]; then
  echo "Launch target missing or not executable: $LAUNCH_PATH" >&2
  exit 1
fi

mkdir -p "$OUTPUT_ROOT"

run_with_optional_xvfb() {
  if [[ -n "${DISPLAY:-}" || -n "${WAYLAND_DISPLAY:-}" ]]; then
    "$@"
    return
  fi

  xvfb-run -a "$@"
}

run_scenario() {
  local scenario_id="$1"
  local character_name="$2"
  local character_alias="$3"
  local ruleset_id="$4"
  local build_method="$5"
  local metatype_category="${6:-}"
  local priority_heritage="${7:-}"
  local metatype="${8:-}"
  local priority_talent="${9:-}"
  local priority_talent_choice="${10:-}"

  local scenario_root="$OUTPUT_ROOT/$scenario_id"
  local receipt_path="$scenario_root/receipt.json"
  local failure_path="$scenario_root/failure.json"
  local trace_path="$scenario_root/trace.json"
  local screenshot_dir="$scenario_root/screens"
  local log_path="$scenario_root/run.log"
  mkdir -p "$scenario_root" "$screenshot_dir"

  echo "running $scenario_id"
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$receipt_path" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$failure_path" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$trace_path" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$screenshot_dir" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCENARIO_ID="$scenario_id" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_CHARACTER_NAME="$character_name" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_CHARACTER_ALIAS="$character_alias" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RULESET_ID="$ruleset_id" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_BUILD_METHOD="$build_method" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_METATYPE_CATEGORY="$metatype_category" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_HERITAGE="$priority_heritage" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_METATYPE="$metatype" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_TALENT="$priority_talent" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_PRIORITY_TALENT_CHOICE="$priority_talent_choice" \
  run_with_optional_xvfb "$LAUNCH_PATH" --mouse-first-user-journey >"$log_path" 2>&1
}

run_scenario "sr5-priority-standard-a-troll-mystic-adept" "Mysad Troll" "MouseTroll" "sr5" "Priority" "Standard" "A" "Troll" "B" "Mystic Adept"
run_scenario "sr4-bp" "Mouse SR4" "MouseSR4" "sr4" "BP"
run_scenario "sr5-priority" "Mouse SR5" "MouseSR5" "sr5" "Priority"
run_scenario "sr6-priority" "Mouse SR6" "MouseSR6" "sr6" "Priority"

python3 - <<'PY' "$OUTPUT_ROOT"
from __future__ import annotations
import json
import sys
from pathlib import Path

output_root = Path(sys.argv[1])
receipts = []
for receipt_path in sorted(output_root.glob("*/receipt.json")):
    try:
        payload = json.loads(receipt_path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        receipts.append({
            "scenarioId": receipt_path.parent.name,
            "status": "invalid",
            "error": str(exc),
            "receiptPath": str(receipt_path),
        })
        continue

    receipts.append({
        "scenarioId": payload.get("scenarioId") or receipt_path.parent.name,
        "status": payload.get("status"),
        "rulesetId": payload.get("rulesetId"),
        "buildMethod": payload.get("buildMethod"),
        "metatype": payload.get("metatype"),
        "priorityTalentChoice": payload.get("priorityTalentChoice"),
        "workspaceId": payload.get("workspaceId"),
        "receiptPath": str(receipt_path),
    })

datetime_module = __import__("datetime")
summary = {
    "generatedAtUtc": datetime_module.datetime.now(datetime_module.UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "scenarioCount": len(receipts),
    "allPassed": all(item.get("status") == "pass" for item in receipts),
    "receipts": receipts,
}
(output_root / "matrix-summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
PY

echo "mouse-first journey matrix complete: $OUTPUT_ROOT"
