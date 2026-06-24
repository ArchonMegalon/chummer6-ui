# Blazor Workbench Notifications-Jobs Staged Proof

## Purpose

This source-staged proof keeps async notifications and background job affordances visible on the promoted Blazor workbench route.

The browser client should make save, export, sync, import, validation, and support-bundle progress visible with retry, dismiss, notification settings, and completion history near the active character workspace.

## Source-Staged Scope

The staged notifications-jobs lane covers:

- a notifications/background-jobs strip on the promoted workbench route
- job queue, retry, dismiss, settings, history, and support shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-notifications-jobs-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_NOTIFICATIONS_JOBS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that promoted workbench notifications-jobs source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, toast-delivery proof, queue-execution proof, retry proof, background-worker proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
