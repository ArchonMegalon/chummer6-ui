# Blazor Workbench Sync-Presence Staged Proof

## Purpose

This source-staged proof keeps hosted and self-hosted session-state affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should preserve desktop-client confidence by showing connection, offline, local cache, sync queue, presence, help, and handoff cues near the active dossier for both Chummer Run and Docker self-hosted operators.

## Source-Staged Scope

The staged sync-presence lane covers:

- a hosted/self-hosted session strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- connection, offline, local cache, sync queue, presence, help, and handoff shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-sync-presence-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SYNC_PRESENCE_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route sync-presence source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, network sync proof, offline-cache proof, portal-help-runtime proof, multi-user presence proof, handoff proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
