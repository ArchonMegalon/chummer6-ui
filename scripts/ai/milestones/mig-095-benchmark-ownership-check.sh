#!/usr/bin/env bash
set -euo pipefail

echo "[MIG-095] checking workspace benchmark ownership mirror..."

if ! rg -q '^- \[x\] `MIG-095` Add benchmark guardrails for import/section/save paths\.$' docs/MIGRATION_BACKLOG.md; then
  echo "[MIG-095] FAIL: MIG-095 must be marked done in docs/MIGRATION_BACKLOG.md."
  exit 3
fi

if ! rg -q '\.\./chummer-core-engine/Chummer.Benchmarks|workspace\.import\.bastion|workspace\.section\.skills\.bastion|workspace\.save\.bastion|benchmark-guardrails\.yml' docs/MIGRATION_BACKLOG.md; then
  echo "[MIG-095] FAIL: MIGRATION_BACKLOG.md must point at core-engine benchmark ownership and named workloads."
  exit 4
fi

if ! rg -q '`Chummer\.Benchmarks/`|Migration-critical workspace benchmark budgets are owned and enforced in `chummer-core-engine`' docs/COMPATIBILITY_CARGO.md; then
  echo "[MIG-095] FAIL: COMPATIBILITY_CARGO.md must document retained benchmark cargo and core-engine ownership."
  exit 5
fi

if ! rg -q 'ForeachSplitComparison' Chummer.Benchmarks/Program.cs; then
  echo "[MIG-095] FAIL: the local compatibility benchmark harness should remain retained cargo."
  exit 6
fi

if [ ! -f ../chummer-core-engine/.github/workflows/benchmark-guardrails.yml ]; then
  echo "[MIG-095] FAIL: sibling core-engine benchmark workflow is missing."
  exit 7
fi

if [ ! -f ../chummer-core-engine/Chummer.Benchmarks/workspace-benchmark-budgets.json ]; then
  echo "[MIG-095] FAIL: sibling core-engine workspace benchmark budgets are missing."
  exit 8
fi

echo "[MIG-095] PASS"
