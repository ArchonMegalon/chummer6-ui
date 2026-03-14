# Worklist Queue

Purpose: keep the live UI queue readable. Historical reruns, strict-signoff churn, and queue-overlay archaeology now live in `RECONCILIATION_LOG.md`.

## Status Keys
- `queued`
- `in_progress`
- `blocked`
- `done`

## Milestone Registry (UI)
| Milestone | Status | Completion | ETA (UTC) | Confidence | Backlog truth |
|---|---|---|---|---|---|
| B8 Runtime inspector + Hub UX | done | 100% | 2026-03-10 | high | Materially complete in shipped UI rails; design truth still keeps boundary purity separate from feature completion. |
| B9 Journal + planner/calendar | done | 100% | 2026-03-10 | high | Materially complete in shipped UI rails; no live repo-local implementation gap remains. |
| B10 Contact graph continuity | done | 100% | 2026-03-10 | high | Materially complete in shipped UI rails; no live repo-local implementation gap remains. |
| B11 Post-split ownership + NPC persona | done | 100% | 2026-03-10 | high | Materially complete in shipped UI rails; ownership closure and NPC Persona Studio surfaces are both regression-guarded. |
| B12 Dispatch/review rails depth | done | 100% | 2026-03-10 | high | Dispatch/review rails are materially complete; remaining repo risk is boundary bulk, not missing dispatch UX. |
| B13 Accessibility signoff | done | 100% | 2026-03-10 | high | Accessibility and browser signoff guards remain executable and closed. |
| P5 Ui-kit package boundary | open | 75% | 2026-03-25 | medium | Shared UI consumption is real, but canonical package-adoption proof still lives in `chummer6-ui-kit` plus design truth. |

## Boundary and release truth
| Lane | Status | Notes |
|---|---|---|
| Feature maturity | done | The major workbench feature lanes are materially implemented and regression-guarded. |
| Boundary purity (`B2`) | open in design canon | The UI repo still carries too much legacy desktop/helper/tooling cargo for the design repo to honestly call the boundary fully purified yet. |
| Installer-capable release lane | done | The desktop release path now stages portable archives and generated installers together instead of loose files only. |

## Queue
| ID | Status | Priority | Task | Owner | Notes |
|---|---|---|---|---|---|
| WL-079 | done | P1 | Milestone B11: build NPC Persona Studio screens on shared persona descriptor/policy contracts. | agent | Closure stays explicit so the verifier can prove NPC Persona Studio remains materially implemented on shared contract-driven surfaces. |
| WL-087 | done | P1 | Milestone P5: publish the remaining shared token/theme extraction backlog for `Chummer.Ui.Kit`. | agent | Runnable slice command chain: `bash scripts/ai/milestones/p5-ui-kit-design-token-check.sh && bash scripts/ai/verify.sh` |
| WL-197 | done | P1 | Close browser/deployment signoff without pretending the boundary reset is complete. | agent | Closed 2026-03-11: deploy/signoff guardrails are real, but the repo no longer treats that as proof that the UI boundary itself is pure. |
| WL-198 | done | P1 | Publish installer-capable desktop downloads instead of raw loose-file bundles only. | agent | Closed 2026-03-14: the desktop downloads lane now builds portable archives and a generated installer via `scripts/build-desktop-installer.sh` and the matrix workflow stages installer artifacts alongside portable bundles. |
| WL-199 | done | P1 | Archive strict-signoff and rerun sludge out of the live queue. | agent | Completed 2026-03-14: the historical ledger was preserved in `RECONCILIATION_LOG.md`, and this worklist now describes current repo truth instead of replaying every stale blocked signoff row. |

## Current repo truth

- Repo-local live queue: empty
- Installer/public-download work is now part of the normal desktop release path, not a “figure it out yourself” afterthought
- Remaining blocker is architectural, not feature-shaped: the repo body still needs to physically look like workbench/browser/desktop-only ownership before `B2` can honestly close

## Historical log

- Full queue history, repeated strict-signoff retries, and exhausted publication proof live in `RECONCILIATION_LOG.md`.
