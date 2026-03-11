#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
CHECK_DIR="$SCRIPT_DIR/milestones"
LOG_DIR="$REPO_ROOT/scripts/ai/logs"
mkdir -p "$LOG_DIR"

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_FILE="$LOG_DIR/day1-all-milestones-${TIMESTAMP}.log"

MAX_ATTEMPTS="${DAY1_MAX_ATTEMPTS:-0}"       # 0 means infinite retries
SLEEP_SECONDS="${DAY1_SLEEP_SECONDS:-2}"
MODE="${DAY1_MILESTONE_MODE:-strict}"        # strict | warn
ALLOW_MISSING_GATES="${DAY1_ALLOW_MISSING_GATES:-0}"

if [[ "$MAX_ATTEMPTS" -lt 0 ]]; then
  MAX_ATTEMPTS=0
fi

declare -a GATES=(
  "B1|Explain Everywhere UI localization renderer|${CHECK_DIR}/b1-explain-check.sh"
  "B2|Browse workspace virtualization|${CHECK_DIR}/b2-browse-virtualization-check.sh"
  "B3|Build Lab engine-contract integration|${CHECK_DIR}/b3-build-lab-check.sh"
  "B4|GM Board / Spider Feed card controls|${CHECK_DIR}/b4-gm-board-spider-feed-check.sh"
  "B5|Session event-log state shell|${CHECK_DIR}/b5-session-event-log-check.sh"
  "B6|Asset preview and approval workflow|${CHECK_DIR}/b6-asset-preview-approval-check.sh"
  "B12|Generated-asset dispatch and review workflow depth|${CHECK_DIR}/b12-generated-asset-dispatch-check.sh"
  "B7|Browser isolation degraded-mode diagnostics|${CHECK_DIR}/b7-browser-isolation-check.sh"
  "B8|Runtime inspector shared surface|${CHECK_DIR}/b8-runtime-inspector-check.sh"
  "B9|Campaign journal continuity surface|${CHECK_DIR}/b9-campaign-journal-check.sh"
  "B10|Contact network continuity surface|${CHECK_DIR}/b10-contact-network-check.sh"
  "B11|Post-split session and coach ownership seams|${CHECK_DIR}/b11-post-split-ownership-check.sh"
  "B11-NPC|NPC Persona Studio backlog mapping|${CHECK_DIR}/b11-npc-persona-studio-check.sh"
  "B13|Post-B6 accessibility signoff|${CHECK_DIR}/b13-accessibility-signoff-check.sh"
  "UI-COVERAGE|UI milestone ETA/completion registry truth|${CHECK_DIR}/ui-milestone-coverage-check.sh"
  "P5|Ui kit shell chrome boundary|${CHECK_DIR}/p5-ui-kit-shell-chrome-check.sh"
  "P5-TOKENS|Ui kit design token and theme backlog mapping|${CHECK_DIR}/p5-ui-kit-design-token-check.sh"
  "P5/B13|Ui kit accessibility and state primitives|${CHECK_DIR}/p5-ui-kit-accessibility-state-check.sh"
)

run_gate() {
  local gate_id="$1"
  local gate_name="$2"
  local gate_script="$3"

  echo "[milestone] starting $gate_id — $gate_name"

  if [[ ! -f "$gate_script" ]]; then
    if [[ "$MODE" == "warn" ]] || [[ "$ALLOW_MISSING_GATES" == "1" ]]; then
      echo "[milestone] SKIP (missing): $gate_script"
      return 0
    fi
    echo "[milestone] FAIL: missing gate check script: $gate_script" >&2
    return 2
  fi

  if [[ ! -x "$gate_script" ]]; then
    chmod +x "$gate_script"
  fi

  if bash "$gate_script"; then
    echo "[milestone] PASSED: $gate_id"
    return 0
  fi

  local rc=$?
  echo "[milestone] FAIL: $gate_id (exit $rc) via $gate_script" >&2
  return "$rc"
}

run_milestone_pipeline() {
  local rc=0
  {
    echo "============================================================"
    echo "[milestone] started: $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
    echo "[milestone] mode: ${MODE}"
    echo "[milestone] attempt: ${attempt}"
    echo "[milestone] allow-missing-gates: ${ALLOW_MISSING_GATES}"
    echo "[milestone] max-attempts: ${MAX_ATTEMPTS}"

    source "$SCRIPT_DIR/_env.sh"

    echo "[milestone] running baseline isolate-compile step (day1-p1-run.sh)"
    bash "$SCRIPT_DIR/day1-p1-run.sh"

    for gate in "${GATES[@]}"; do
      IFS='|' read -r gate_id gate_name gate_script <<< "$gate"
      if run_gate "$gate_id" "$gate_name" "$gate_script"; then
        echo
      else
        rc=$?
        break
      fi
    done

    if (( rc == 0 )); then
      echo "[milestone] PASSED: all milestones"
    else
      echo "[milestone] FAILED: one or more gates failed"
    fi

    echo "[milestone] finished: $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
    echo "============================================================"
  } | tee "$LOG_FILE"

  return $rc
}

attempt=1
while true; do
  if run_milestone_pipeline; then
    exit 0
  fi

  rc=$?
  if (( MAX_ATTEMPTS != 0 && attempt >= MAX_ATTEMPTS )); then
    echo "[milestone] reached maximum attempts (${MAX_ATTEMPTS}); aborting."
    echo "[milestone] last failure log: $LOG_FILE" >&2
    exit "$rc"
  fi

  attempt=$((attempt + 1))
  echo "[milestone] retrying in ${SLEEP_SECONDS}s..."
  sleep "$SLEEP_SECONDS"
done
