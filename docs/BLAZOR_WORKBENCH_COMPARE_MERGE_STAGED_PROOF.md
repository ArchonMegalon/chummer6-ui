# Blazor Workbench Compare/Merge Staged Proof

## Purpose

This source-staged proof keeps compare and merge affordances visible on the promoted Blazor workbench route.

The browser client should expose diff view, conflict choice, source trace, dry run, apply, and rollback posture so imported changes remain reviewable before they affect a runner.

## Source-Staged Scope

The staged compare/merge lane covers:

- a compare strip on the promoted workbench route
- diff-view, conflict-choice, source-trace, dry-run, apply, and rollback shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-compare-merge-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_COMPARE_MERGE_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench compare/merge source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, diff execution proof, conflict-resolution proof, merge-application proof, rollback proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
