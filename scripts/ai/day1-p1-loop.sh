#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

MAX_ATTEMPTS="${DAY1_MAX_ATTEMPTS:-0}"   # 0 means infinite retries
SLEEP_SECONDS="${DAY1_SLEEP_SECONDS:-2}"

if [[ "$MAX_ATTEMPTS" -lt 0 ]]; then
  MAX_ATTEMPTS=0
fi

LOG_DIR="$REPO_ROOT/scripts/ai/logs"
mkdir -p "$LOG_DIR"

attempt=1
while true; do
  log_file="$LOG_DIR/day1-p1-loop-${attempt}-$(date -u +%Y%m%dT%H%M%SZ).log"

  echo "============================================================"
  echo "[day1-p1-loop] attempt ${attempt} start: $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  echo "[day1-p1-loop] output log: $log_file"
  echo "[day1-p1-loop] max attempts: ${MAX_ATTEMPTS:-0} (0 = infinite)"
  echo "[day1-p1-loop] starting day1 wrapper..."

  bash "$SCRIPT_DIR/day1-p1-run.sh" "$@" | tee "$log_file"
  rc=${PIPESTATUS[0]}

  if (( rc == 0 )); then
    echo "[day1-p1-loop] attempt ${attempt} succeeded."
    break
  fi

  echo "[day1-p1-loop] attempt ${attempt} failed (exit ${rc})."
  if (( MAX_ATTEMPTS != 0 && attempt >= MAX_ATTEMPTS )); then
    echo "[day1-p1-loop] reached max attempts (${MAX_ATTEMPTS}); aborting."
    exit "$rc"
  fi

  echo "[day1-p1-loop] retrying in ${SLEEP_SECONDS}s..."
  sleep "$SLEEP_SECONDS"
  ((attempt++))
done
