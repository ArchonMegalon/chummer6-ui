# Blazor Workbench Import/Reconcile Staged Proof

## Purpose

This staged proof keeps the promoted Blazor workbench source aligned around the import/reconcile workflow needed to make the web client feel like another Chummer desktop client for existing runners.

It covers visible source posture for file selection, parse summary, rules mapping, custom data review, conflict review, and commit-import affordances on `/blazor/workbench`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-import-reconcile-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_IMPORT_RECONCILE_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, file upload, XML parsing, data migration, conflict resolution, or import persistence.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof.
