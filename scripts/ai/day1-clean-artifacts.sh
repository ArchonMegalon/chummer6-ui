#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LOG_DIR="$SCRIPT_DIR/logs"
mkdir -p "$LOG_DIR"

TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
LOG_FILE="$LOG_DIR/day1-clean-artifacts-${TIMESTAMP}.log"

{
  echo "============================================================"
  echo "[clean-artifacts] started: $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  echo "[clean-artifacts] repository: $REPO_ROOT"

  cd "$REPO_ROOT"

  if [[ -f .codex.boot.prompt.txt ]]; then
    rm -f .codex.boot.prompt.txt
    echo "[clean-artifacts] removed: .codex.boot.prompt.txt"
  fi

  if [[ -f LICENSE.txt ]]; then
    rm -f LICENSE.txt
    echo "[clean-artifacts] removed: LICENSE.txt"
  fi

  if [[ -f day1.prompt.txt ]]; then
    rm -f day1.prompt.txt
    echo "[clean-artifacts] removed: day1.prompt.txt"
  fi

  if [[ -f solution.pp ]]; then
    rm -f solution.pp
    echo "[clean-artifacts] removed: solution.pp"
  fi

  if [[ -d bar ]]; then
    rm -rf bar
    echo "[clean-artifacts] removed: bar/"
  fi

  if ls tmp*.binlog tmp*.txt tmp*.log tmp*.xml tmp*_diag.log tmp_aftersetup_plain.log tmp_base.log tmp_default_build.log tmp_default_new.log tmp_default_solutionprops.log tmp_default_static_false.log tmp_direct_diag.log tmp_m1_diag.log tmp_no_restore_default.log tmp_plain_diag.log tmp_plain_vd.log tmp_single.log build.log build2.log msbuild.log 2>/dev/null; then
    rm -f tmp*.binlog tmp*.txt tmp*.log tmp*.xml tmp*_diag.log tmp_aftersetup_plain.log tmp_base.log tmp_default_build.log tmp_default_new.log tmp_default_solutionprops.log tmp_default_static_false.log tmp_direct_diag.log tmp_m1_diag.log tmp_no_restore_default.log tmp_plain_diag.log tmp_plain_vd.log tmp_single.log build.log build2.log msbuild.log msbuild.rsp tmp_solution_pre.xml >/dev/null 2>&1 || true
    echo "[clean-artifacts] removed matching tmp/build diagnostic files"
  else
    echo "[clean-artifacts] no matching tmp/build artifacts found"
  fi

  echo "[clean-artifacts] finished: $(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  echo "============================================================"
} | tee "$LOG_FILE"
