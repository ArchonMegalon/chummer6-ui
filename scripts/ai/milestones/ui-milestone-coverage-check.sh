#!/usr/bin/env bash
set -euo pipefail

echo "[UI-MILESTONES] checking milestone registry ETA/completion coverage..."

if ! rg -q "^## Milestone Registry \\(UI\\)$" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: missing 'Milestone Registry (UI)' section in WORKLIST.md."
  exit 3
fi

if ! rg -q "\\| B8 Runtime inspector \\+ Hub UX \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B8 milestone row missing completion/ETA/confidence."
  exit 4
fi

if ! rg -q "\\| B9 Journal \\+ planner/calendar \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B9 milestone row missing completion/ETA/confidence."
  exit 5
fi

if ! rg -q "\\| B10 Contact graph continuity \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B10 milestone row missing completion/ETA/confidence."
  exit 6
fi

if ! rg -q "\\| B11 Post-split ownership \\+ NPC persona \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B11 milestone row missing completion/ETA/confidence."
  exit 7
fi

if ! rg -q "\\| B12 Dispatch/review rails depth \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B12 milestone row missing completion/ETA/confidence."
  exit 8
fi

if ! rg -q "\\| B13 Accessibility signoff \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: B13 milestone row missing completion/ETA/confidence."
  exit 9
fi

if ! rg -q "\\| P5 Ui-kit package boundary \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
  echo "[UI-MILESTONES] FAIL: P5 milestone row missing completion/ETA/confidence."
  exit 10
fi

if rg -q "Finish milestone coverage modeling for ui so ETA and completion truth are no longer partial\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue still advertises milestone-coverage-incomplete publication."
  exit 11
fi

if rg -q "Final accessibility, deployment, and browser-constraint signoff\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue re-published the closed final accessibility/deployment/browser signoff slice."
  exit 12
fi

if rg -q "Retire session/mobile and coach play heads from Presentation, keep workbench/UI-kit ownership there, and point the play split at the dedicated repo and API surface\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue re-published the closed post-split play-head retirement slice."
  exit 13
fi

if rg -q "Calendar, ledger, and downtime planner surfaces\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue re-published the closed B9 planner/calendar slice."
  exit 14
fi

if rg -q "Runtime inspector, RuleProfile, and RulePack diagnostics\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue re-published the closed B8 runtime-inspector diagnostics slice."
  exit 15
fi

if rg -q "Heat, faction, and favor continuity views\\." .codex-studio/published/QUEUE.generated.yaml; then
  echo "[UI-MILESTONES] FAIL: queue re-published the closed B10 continuity slice."
  exit 16
fi

if rg -q "Richer Hub client UX\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-204 \\| queued \\| P1 \\| Publish runnable backlog evidence for Richer Hub client UX queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Richer Hub publication but WORKLIST lacks queued WL-204 runnable backlog entry."
    exit 17
  fi

  if ! rg -q '^- Repo-local live queue: active \(`WL-204`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Richer Hub publication but WORKLIST current-truth section does not declare active live queue."
    exit 18
  fi
fi

echo "[UI-MILESTONES] PASS: milestone coverage registry is explicit and queue publication is normalized."
