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
| P5 Ui-kit package boundary | done | 100% | 2026-03-19 | high | Shared UI consumption is package-only and regression-guarded in both presentation and mobile verification paths. |

## Boundary and release truth
| Lane | Status | Notes |
|---|---|---|
| Feature maturity | done | The major workbench feature lanes are materially implemented and regression-guarded. |
| Boundary purity (`B2`) | done | Dedicated play/mobile heads stay out, shared UI is package-owned, and remaining legacy roots are isolated as explicit compatibility cargo in `docs/COMPATIBILITY_CARGO.md`. |
| Installer-capable release lane | done | The desktop release path now stages portable archives and generated installers together instead of loose files only. |

## Queue
| ID | Status | Priority | Task | Owner | Notes |
|---|---|---|---|---|---|
| WL-200 | done | P1 | Publish CI guardrail slice for package-only B1 primitives so local copies of `Chummer.Ui.Kit` shell/accessibility primitives cannot be reintroduced. | agent | Closed 2026-03-19: `scripts/ai/verify.sh` now enforces package-only shell/state primitive consumption and blocks repo-local copies of shell, accessibility, banner, stale, approval, and offline state primitives. |
| WL-201 | done | P1 | Close `B2` boundary purity by moving remaining legacy desktop/helper roots out of the primary UI repo body or isolating them as explicit compatibility cargo with explicit rationale. | agent | Closed 2026-03-19: retained legacy roots are now explicitly documented in `docs/COMPATIBILITY_CARGO.md`, and verification requires that compatibility-cargo inventory to remain present. |
| WL-202 | done | P1 | Close the remaining `E0` workbench depth by finishing the unclosed graph, continuity, planner/calendar, diagnostics, moderation, and richer Hub UX surfaces. | agent | Closed 2026-03-19: `docs/WORKBENCH_RELEASE_SIGNOFF.md` now ties builder, graph/continuity, planner/calendar, diagnostics, moderation/admin, publish, and richer Hub UX depth directly to the existing milestone verifier set, so the remaining debt is no longer missing workbench capability. |
| WL-203 | done | P1 | Close `F0` for UI by publishing explicit accessibility, localization, browser-constraint, and performance signoff evidence that survives normal verify runs. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b13-accessibility-signoff-check.sh && bash scripts/ai/milestones/b7-browser-isolation-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; `docs/WORKBENCH_RELEASE_SIGNOFF.md` and runtime milestone probes now verify final accessibility/deployment/browser-constraint signoff together. |
| WL-079 | done | P1 | Milestone B11: build NPC Persona Studio screens on shared persona descriptor/policy contracts. | agent | Closure stays explicit so the verifier can prove NPC Persona Studio remains materially implemented on shared contract-driven surfaces. |
| WL-087 | done | P1 | Milestone P5: publish the remaining shared token/theme extraction backlog for `Chummer.Ui.Kit`. | agent | Runnable slice command chain: `bash scripts/ai/milestones/p5-ui-kit-design-token-check.sh && bash scripts/ai/verify.sh` |
| WL-197 | done | P1 | Close browser/deployment signoff without pretending the boundary reset is complete. | agent | Closed 2026-03-11: deploy/signoff guardrails are real, but the repo no longer treats that as proof that the UI boundary itself is pure. |
| WL-198 | done | P1 | Publish installer-capable desktop downloads instead of raw loose-file bundles only. | agent | Closed 2026-03-14: the desktop downloads lane now builds portable archives and a generated installer via `scripts/build-desktop-installer.sh` and the matrix workflow stages installer artifacts alongside portable bundles. |
| WL-199 | done | P1 | Archive strict-signoff and rerun sludge out of the live queue. | agent | Completed 2026-03-14: the historical ledger was preserved in `RECONCILIATION_LOG.md`, and this worklist now describes current repo truth instead of replaying every stale blocked signoff row. |
| WL-204 | done | P1 | Publish runnable backlog evidence for Richer Hub client UX queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b8-runtime-inspector-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and richer Hub UX coverage now verify together. |
| WL-205 | done | P1 | Publish runnable backlog evidence for Contact and relationship graph UI queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b10-contact-network-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and contact/relationship graph coverage now verify together. |
| WL-206 | done | P1 | Publish runnable backlog evidence for Coach, Shadowfeed, player dispatch, and review workflow queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b4-gm-board-spider-feed-check.sh && bash scripts/ai/milestones/b12-generated-asset-dispatch-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and coach/shadowfeed dispatch-review coverage now verify together. |
| WL-207 | done | P1 | Publish runnable backlog evidence for NPC Persona Studio screens queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b11-npc-persona-studio-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and NPC Persona Studio coverage now verify together. |
| WL-208 | done | P1 | Publish runnable backlog evidence for Portrait Forge selection and reroll UX depth queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b12-generated-asset-dispatch-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and Portrait Forge selection/reroll depth coverage now verify together. |
| WL-209 | done | P1 | Publish runnable backlog evidence for Runtime inspector, RuleProfile, and RulePack diagnostics queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b8-runtime-inspector-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and runtime inspector/RuleProfile/RulePack diagnostics coverage now verify together. |
| WL-210 | done | P1 | Publish runnable backlog evidence for Calendar, ledger, and downtime planner surfaces queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b9-campaign-journal-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and calendar/ledger/downtime planner coverage now verify together. |
| WL-211 | done | P1 | Publish runnable backlog evidence for Heat, faction, and favor continuity views queue coverage and enforce queue/worklist consistency for this slice. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/b10-contact-network-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; queue/worklist consistency and heat/faction/favor continuity coverage now verify together. |
| WL-212 | done | P1 | Finish milestone coverage modeling for ui so ETA and completion truth are no longer partial. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; milestone ETA/completion registry coverage now has explicit queue-to-worklist mapping and enforcement. |
| WL-213 | done | P1 | Replace duplicated `Chummer.Contracts` source in UI with package consumption from the canonical shared contract owner. | agent | Closed 2026-03-21: executed `bash scripts/ai/milestones/p5-contract-package-boundary-check.sh && bash scripts/ai/milestones/ui-milestone-coverage-check.sh && bash scripts/ai/verify.sh`; package-only `Chummer.Engine.Contracts` consumption and queue/worklist consistency now verify together. |
| WL-214 | done | P1 | Refresh local design mirror for `ui` and keep repo-local review context in sync with canonical `chummer6-design`. | agent | Closed 2026-03-21: compared `.codex-design/product/*` against `/docker/chummercomplete/chummer-design/products/chummer/*`, `.codex-design/repo/IMPLEMENTATION_SCOPE.md` against `/docker/chummercomplete/chummer-design/products/chummer/projects/ui.md`, and `.codex-design/review/REVIEW_CONTEXT.md` against `/docker/chummercomplete/chummer-design/products/chummer/review/ui.AGENTS.template.md`; mirror files are current and queue/worklist mapping now explicitly covers this publication. |

## Current repo truth

- Repo-local live queue: none (WL-204, WL-205, WL-206, WL-207, WL-208, WL-209, WL-210, WL-211, WL-212, WL-213, and WL-214 closed 2026-03-21; external queue overlay still lists future publications).
- Queue overlay hygiene refresh (2026-03-22): removed republished runtime-inspector (`WL-209`) and calendar/ledger/downtime planner (`WL-210`) publication lines because both slices are already closed with runnable verification evidence.
- Auditor publication incorporation (2026-03-22): restored final accessibility/deployment/browser (`WL-203`) queue publication lines and tightened `ui-milestone-coverage-check.sh` so queue/worklist/milestone mapping for this signoff cannot drift silently.
- Auditor publication incorporation (2026-03-22): incorporated Contact/relationship graph publication pair (`WL-205`) by enforcing both runnable backlog and milestone-mapping checks in `ui-milestone-coverage-check.sh`.
- Milestone coverage modeling is now explicit and verifier-enforced: queue publication for ETA/completion closure maps to WL-212 and fails verification if queue/worklist status drifts.
- Workbench completion and cross-head signoff are now explicit and verifier-backed; remaining work is compatibility-cargo cleanup and future product depth, not missing release proof.
- Installer/public-download work is now part of the normal desktop release path, not a “figure it out yourself” afterthought
- Remaining work is product evolution, not split confusion: workbench/browser/desktop ownership is explicit and the retained legacy cargo is documented instead of implied

## Historical log

- Full queue history, repeated strict-signoff retries, and exhausted publication proof live in `RECONCILIATION_LOG.md`.
