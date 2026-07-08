#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
SCRIPT_DIR="$REPO_ROOT/scripts"

STRICT_FILTER="${TEST_FILTER:-${1:-}}"
STRICT_FRAMEWORK="${TEST_FRAMEWORK:-${2:-net10.0}}"
STRICT_LOCAL_FILTER_DEFAULT="FullyQualifiedName!~Chummer.Tests.ApiIntegrationTests&FullyQualifiedName!~Chummer.Tests.ChummerTest"
STRICT_LOCAL_FILTER="${TEST_LOCAL_FILTER:-$STRICT_FILTER}"
STRICT_ALLOW_WORKTREE_DRIFT="${STRICT_ALLOW_WORKTREE_DRIFT:-0}"

if [[ -z "$STRICT_LOCAL_FILTER" ]]; then
  STRICT_LOCAL_FILTER="$STRICT_LOCAL_FILTER_DEFAULT"
fi

capture_worktree_state() {
  if command -v git >/dev/null 2>&1; then
    git -C "$REPO_ROOT" status --porcelain --untracked-files=no 2>/dev/null || true
  fi
}

run_local_tests() {
  echo "== strict local-tests gate =="
  if [[ -n "$STRICT_LOCAL_FILTER" ]]; then
    echo "filter: $STRICT_LOCAL_FILTER"
  fi
  if [[ -n "$STRICT_FRAMEWORK" ]]; then
    echo "framework: $STRICT_FRAMEWORK"
  fi

  RUNBOOK_MODE=local-tests \
  TEST_NUGET_SOFT_FAIL=0 \
  TEST_DISABLE_BUILD_SERVERS=1 \
  TEST_MAX_CPU=1 \
  TEST_FILTER="$STRICT_LOCAL_FILTER" \
  TEST_FRAMEWORK="$STRICT_FRAMEWORK" \
  bash "$REPO_ROOT/scripts/runbook.sh"
}

run_docker_tests() {
  echo "== strict docker-tests gate =="
  if [[ -n "$STRICT_FILTER" ]]; then
    echo "filter: $STRICT_FILTER"
  fi
  if [[ -n "$STRICT_FRAMEWORK" ]]; then
    echo "framework: $STRICT_FRAMEWORK"
  fi

  RUNBOOK_MODE=docker-tests \
  DOCKER_TESTS_SOFT_FAIL=0 \
  DOCKER_TESTS_BUILD="${DOCKER_TESTS_BUILD:-1}" \
  TEST_FILTER="$STRICT_FILTER" \
  TEST_FRAMEWORK="$STRICT_FRAMEWORK" \
  bash "$REPO_ROOT/scripts/runbook.sh"
}

echo "== strict host prerequisite gate =="
CHECK_DOCKER="${CHECK_DOCKER:-1}" \
CHECK_NUGET="${CHECK_NUGET:-1}" \
bash "$REPO_ROOT/scripts/check-host-gate-prereqs.sh"

baseline_worktree_state="$(capture_worktree_state)"
run_local_tests
run_docker_tests
post_worktree_state="$(capture_worktree_state)"

if [[ "$baseline_worktree_state" != "$post_worktree_state" ]]; then
  echo "Detected tracked worktree drift during strict host gates." >&2
  echo "Baseline tracked status:" >&2
  if [[ -n "$baseline_worktree_state" ]]; then
    echo "$baseline_worktree_state" >&2
  else
    echo "(clean)" >&2
  fi
  echo "Post-run tracked status:" >&2
  if [[ -n "$post_worktree_state" ]]; then
    echo "$post_worktree_state" >&2
  else
    echo "(clean)" >&2
  fi

  if [[ "$STRICT_ALLOW_WORKTREE_DRIFT" != "1" && "$STRICT_ALLOW_WORKTREE_DRIFT" != "true" && "$STRICT_ALLOW_WORKTREE_DRIFT" != "TRUE" ]]; then
    exit 1
  fi
fi

echo "Strict host gates completed successfully."
