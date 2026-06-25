# Blazor Workbench Restore/Checkpoint Staged Proof

## Purpose

This source-staged proof keeps restore and checkpoint affordances visible on the user-facing Chummer App route and proof-compatible Blazor workbench route.

The browser client should expose autosave, named checkpoint, backup, preview, rollback, retention, and same-origin help posture so recovery stays close to risky edits.

## Source-Staged Scope

The staged restore/checkpoint lane covers:

- a restore strip on the user-facing Chummer App route and proof-compatible workbench route
- autosave, named-checkpoint, backup, preview, rollback, retention, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-restore-checkpoint-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_RESTORE_CHECKPOINT_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and proof-compatible workbench restore/checkpoint source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, snapshot-persistence proof, restore-execution proof, backup-generation proof, rollback proof, retention-policy proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
