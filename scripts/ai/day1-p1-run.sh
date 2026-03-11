#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOG_DIR="$REPO_ROOT/scripts/ai/logs"
mkdir -p "$LOG_DIR"

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_FILE="$LOG_DIR/day1-p1-${TIMESTAMP}.log"

{
  echo "============================================================"
  echo "[day1-p1-run] started: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "[day1-p1-run] log: $LOG_FILE"

  source "$SCRIPT_DIR/_env.sh"

  echo "[day1-p1-run] running: scripts/ai/day1-p1-setup.sh"
  cd "$REPO_ROOT"
  bash "$SCRIPT_DIR/day1-p1-setup.sh"

  echo "[day1-p1-run] running: scripts/ai/build.sh $*"
  bash "$SCRIPT_DIR/build.sh" "$REPO_ROOT/Chummer.Presentation.sln" "$@"

  echo "[day1-p1-run] completed successfully"
  echo "[day1-p1-run] finished: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  echo "============================================================"
} | tee "$LOG_FILE"
