# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-ui/pull/4

Findings:
- [high] WORKLIST.md [contracts] queue-worklist-live-queue-contradiction
`WORKLIST.md` says `Repo-local live queue: none` (line 44), but `.codex-studio/published/QUEUE.generated.yaml` currently contains active items including `Publish or append runnable backlog for Richer Hub client UX.` and `Add milestone mapping or executable queue work for Richer Hub client UX.` (lines 3-4).; `RECONCILIATION_LOG.md` records prior normalization for this exact slice (`WL-311`), claiming those Richer Hub queue lines were removed, but they are present again in the live queue file.
Expected fix: Make queue truth and worklist truth consistent for this slice: either keep queue items and update `WORKLIST.md` to reflect an active live queue, or remove/normalize these republished lines if already materialized.
- [high] scripts/ai/milestones/ui-milestone-coverage-check.sh [tests] missing-guardrail-for-richer-hub-queue-residue
`scripts/ai/milestones/ui-milestone-coverage-check.sh` only blocks a fixed subset of stale queue strings (milestone coverage, final accessibility, play-head retirement, planner/runtime/heat lines) and does not check for residual Richer Hub/Contact/Coach queue publications currently present in `.codex-studio/published/QUEUE.generated.yaml`.; Because this guard omits the current slice lines, `verify` can pass while queue/worklist contract drift persists.
Expected fix: Add/extend queue-residue checks in `ui-milestone-coverage-check.sh` (or equivalent guard) to fail when already-materialized Richer Hub/backlog publication lines reappear without corresponding live queue/worklist updates.
