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
  if ! rg -q "^\\| B8 Runtime inspector \\+ Hub UX \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue published milestone-coverage modeling but WORKLIST milestone rows are not explicit."
    exit 11
  fi

  if ! rg -q "^\\| WL-212 \\| (queued|done) \\| P1 \\| Finish milestone coverage modeling for ui so ETA and completion truth are no longer partial\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has milestone-coverage modeling publication but WORKLIST lacks WL-212 runnable backlog entry."
    exit 40
  fi

  if ! rg -q "^\\| WL-212 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-212`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has milestone-coverage modeling publication but WORKLIST must either keep WL-212 active or mark it done."
    exit 41
  fi
fi

if rg -q "Final accessibility, deployment, and browser-constraint signoff\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q '^\| WL-203 \| (queued|done) \| P1 \| Close `F0` for UI by publishing explicit accessibility, localization, browser-constraint, and performance signoff evidence that survives normal verify runs\.' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has final accessibility/deployment/browser publication but WORKLIST lacks WL-203 coverage entry."
    exit 12
  fi

  if ! rg -q "^\\| WL-203 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-203`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has final accessibility/deployment/browser publication but WORKLIST must either keep WL-203 active or mark it done."
    exit 46
  fi
fi

if rg -q "Add milestone mapping or executable queue work for Final accessibility, deployment, and browser-constraint signoff\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B13 Accessibility signoff \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for final accessibility/deployment/browser milestone mapping but WORKLIST milestone registry lacks explicit B13 mapping."
    exit 47
  fi

  if ! rg -q '^\| WL-203 \| (queued|done) \| P1 \| Close `F0` for UI by publishing explicit accessibility, localization, browser-constraint, and performance signoff evidence that survives normal verify runs\.' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for final accessibility/deployment/browser executable queue work but WORKLIST lacks WL-203 mapping."
    exit 48
  fi
fi

if rg -q "Sync the approved Chummer design bundle into \`ui\` under \\.codex-design/ and refresh repo-local review context\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q '^\| WL-214 \| (queued|done) \| P1 \| Refresh local design mirror for `ui` and keep repo-local review context in sync with canonical `chummer6-design`\.' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has design-mirror refresh publication but WORKLIST lacks WL-214 coverage entry."
    exit 44
  fi

  if ! rg -q "^\\| WL-214 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-214`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has design-mirror refresh publication but WORKLIST must either keep WL-214 active or mark it done."
    exit 45
  fi
fi

if rg -q "Retire session/mobile and coach play heads from Presentation, keep workbench/UI-kit ownership there, and point the play split at the dedicated repo and API surface\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q '^\| WL-201 \| (queued|done) \| P1 \| Close `B2` boundary purity by moving remaining legacy desktop/helper roots out of the primary UI repo body or isolating them as explicit compatibility cargo with explicit rationale\.' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has play-head retirement publication but WORKLIST lacks WL-201 boundary coverage entry."
    exit 13
  fi
fi

if rg -q "Calendar, ledger, and downtime planner surfaces\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-210 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Calendar, ledger, and downtime planner surfaces queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has planner/calendar publication but WORKLIST lacks WL-210 runnable backlog entry."
    exit 14
  fi

  if ! rg -q "^\\| WL-210 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-210`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has planner/calendar publication but WORKLIST must either keep WL-210 active or mark it done."
    exit 34
  fi
fi

if rg -q "Runtime inspector, RuleProfile, and RulePack diagnostics\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-209 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Runtime inspector, RuleProfile, and RulePack diagnostics queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has runtime-inspector diagnostics publication but WORKLIST lacks WL-209 runnable backlog entry."
    exit 15
  fi

  if ! rg -q "^\\| WL-209 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-209`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has runtime-inspector diagnostics publication but WORKLIST must either keep WL-209 active or mark it done."
    exit 31
  fi
fi

if rg -q "Heat, faction, and favor continuity views\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-211 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Heat, faction, and favor continuity views queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has heat/faction/favor continuity publication but WORKLIST lacks WL-211 runnable backlog entry."
    exit 16
  fi

  if ! rg -q "^\\| WL-211 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-211`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has heat/faction/favor continuity publication but WORKLIST must either keep WL-211 active or mark it done."
    exit 37
  fi
fi

if rg -q "Richer Hub client UX\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-204 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Richer Hub client UX queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Richer Hub publication but WORKLIST lacks WL-204 runnable backlog entry."
    exit 17
  fi

  if ! rg -q "^\\| WL-204 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-204`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Richer Hub publication but WORKLIST must either keep WL-204 active or mark it done."
    exit 18
  fi
fi

if rg -q "Contact and relationship graph UI\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-205 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Contact and relationship graph UI queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Contact/relationship graph publication but WORKLIST lacks WL-205 runnable backlog entry."
    exit 19
  fi

  if ! rg -q "^\\| WL-205 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-205`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Contact/relationship graph publication but WORKLIST must either keep WL-205 active or mark it done."
    exit 20
  fi
fi

if rg -q "Coach, Shadowfeed, player dispatch, and review workflows\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-206 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Coach, Shadowfeed, player dispatch, and review workflow queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Coach/Shadowfeed/player-dispatch publication but WORKLIST lacks WL-206 runnable backlog entry."
    exit 21
  fi

  if ! rg -q "^\\| WL-206 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-206`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Coach/Shadowfeed/player-dispatch publication but WORKLIST must either keep WL-206 active or mark it done."
    exit 22
  fi
fi

if rg -q "NPC Persona Studio screens\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-207 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for NPC Persona Studio screens queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has NPC Persona Studio publication but WORKLIST lacks WL-207 runnable backlog entry."
    exit 23
  fi

  if ! rg -q "^\\| WL-207 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-207`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has NPC Persona Studio publication but WORKLIST must either keep WL-207 active or mark it done."
    exit 24
  fi
fi

if rg -q "Portrait Forge selection and reroll UX depth\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-208 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Portrait Forge selection and reroll UX depth queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Portrait Forge selection/reroll publication but WORKLIST lacks WL-208 runnable backlog entry."
    exit 27
  fi

  if ! rg -q "^\\| WL-208 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-208`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has Portrait Forge selection/reroll publication but WORKLIST must either keep WL-208 active or mark it done."
    exit 28
  fi
fi

if rg -q "Add milestone mapping or executable queue work for NPC Persona Studio screens\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B11 Post-split ownership \\+ NPC persona \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for NPC Persona milestone mapping but WORKLIST milestone registry lacks explicit B11 mapping."
    exit 25
  fi

  if ! rg -q "^\\| WL-207 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for NPC Persona Studio screens queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for NPC Persona executable queue work but WORKLIST lacks WL-207 mapping."
    exit 26
  fi
fi

if rg -q "Add milestone mapping or executable queue work for Portrait Forge selection and reroll UX depth\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B12 Dispatch/review rails depth \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for Portrait Forge milestone mapping but WORKLIST milestone registry lacks explicit B12 mapping."
    exit 29
  fi

  if ! rg -q "^\\| WL-208 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Portrait Forge selection and reroll UX depth queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for Portrait Forge executable queue work but WORKLIST lacks WL-208 mapping."
    exit 30
  fi
fi

if rg -q "Add milestone mapping or executable queue work for Runtime inspector, RuleProfile, and RulePack diagnostics\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B8 Runtime inspector \\+ Hub UX \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for runtime-inspector diagnostics milestone mapping but WORKLIST milestone registry lacks explicit B8 mapping."
    exit 32
  fi

  if ! rg -q "^\\| WL-209 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Runtime inspector, RuleProfile, and RulePack diagnostics queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for runtime-inspector diagnostics executable queue work but WORKLIST lacks WL-209 mapping."
    exit 33
  fi
fi

if rg -q "Add milestone mapping or executable queue work for Calendar, ledger, and downtime planner surfaces\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B9 Journal \\+ planner/calendar \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for planner/calendar milestone mapping but WORKLIST milestone registry lacks explicit B9 mapping."
    exit 35
  fi

  if ! rg -q "^\\| WL-210 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Calendar, ledger, and downtime planner surfaces queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for planner/calendar executable queue work but WORKLIST lacks WL-210 mapping."
    exit 36
  fi
fi

if rg -q "Add milestone mapping or executable queue work for Heat, faction, and favor continuity views\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| B10 Contact graph continuity \\| (open|done) \\| [0-9]+% \\| [0-9]{4}-[0-9]{2}-[0-9]{2} \\| (low|medium|high) \\|" WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for heat/faction/favor continuity milestone mapping but WORKLIST milestone registry lacks explicit B10 mapping."
    exit 38
  fi

  if ! rg -q "^\\| WL-211 \\| (queued|done) \\| P1 \\| Publish runnable backlog evidence for Heat, faction, and favor continuity views queue coverage and enforce queue/worklist consistency for this slice\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue asks for heat/faction/favor continuity executable queue work but WORKLIST lacks WL-211 mapping."
    exit 39
  fi
fi

if rg -q "Replace duplicated \`Chummer.Contracts\` source in UI with package consumption from the canonical shared contract owner\\." .codex-studio/published/QUEUE.generated.yaml; then
  if ! rg -q "^\\| WL-213 \\| (queued|done) \\| P1 \\| Replace duplicated \`Chummer.Contracts\` source in UI with package consumption from the canonical shared contract owner\\." WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has contracts package-boundary publication but WORKLIST lacks WL-213 runnable backlog entry."
    exit 42
  fi

  if ! rg -q "^\\| WL-213 \\| done \\|" WORKLIST.md && ! rg -q '^- Repo-local live queue: active \(`WL-213`\)' WORKLIST.md; then
    echo "[UI-MILESTONES] FAIL: queue has contracts package-boundary publication but WORKLIST must either keep WL-213 active or mark it done."
    exit 43
  fi
fi

echo "[UI-MILESTONES] PASS: milestone coverage registry is explicit and queue publication is normalized."
