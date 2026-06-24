# Blazor Workbench Portal Handoff Staged Proof

## Purpose

This source-staged proof keeps the promoted Blazor workbench connected to portal-owned downloads, status, support, and account work routes.

The browser client should behave like another desktop client in the web: a user can continue the core workbench task, then move to install/update/support/account surfaces through the same product origin when needed.

## Expected Routes

```text
/downloads/
/status
/contact
/account/work
/blazor/workbench
```

## Required UX Contract

The promoted workbench must expose visible same-origin handoff affordances for:

- desktop installer/download handoff
- current release/status truth
- product support
- account/work continuation when authenticated owner context is required

These cards do not prove account, support, installer, or portal runtime behavior. They keep the Blazor workbench product-shaped while runtime evidence remains owned by the local portal and hosted proof receipts.

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-portal-handoff-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_PORTAL_HANDOFF_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It is not a hosted or Docker browser execution receipt, and it must not be used as authentication, support-submission, installer, or account-runtime proof.
