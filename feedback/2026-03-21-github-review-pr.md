# GitHub Codex Review

PR: https://github.com/ArchonMegalon/chummer6-ui/pull/10

Findings:
- [high] .codex-studio/published/QUEUE.generated.yaml [contracts] heat-continuity-queue-worklist-contract-drift
Queue still includes both heat continuity prompts: `Publish or append runnable backlog for Heat, faction, and favor continuity views.` and `Add milestone mapping or executable queue work for Heat, faction, and favor continuity views.` (`.codex-studio/published/QUEUE.generated.yaml:7-8`).; Coverage script only checks for a broad WL-202 E0 row when heat continuity is present, not a runnable slice-specific backlog entry (`scripts/ai/milestones/ui-milestone-coverage-check.sh:91-95`).; WORKLIST has no heat/faction/favor-specific runnable backlog row (WL-204..WL-210 cover other slices; no dedicated continuity slice row) (`WORKLIST.md:41-47`).; The script currently returns PASS despite unresolved heat continuity queue prompts, masking this gap (`bash scripts/ai/milestones/ui-milestone-coverage-check.sh` => PASS).
Expected fix: Materialize a dedicated runnable backlog/mapping entry for the heat/faction/favor continuity slice in WORKLIST (or explicitly retire those queue items), and update `ui-milestone-coverage-check.sh` to assert that exact slice mapping for both publish and add-mapping queue prompts.
