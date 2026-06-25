# Blazor Workbench Offline/Cache Staged Proof

## Purpose

This staged proof keeps the Chummer Online and proof-compatible Blazor workbench source aligned around offline/cache continuity posture for browser editing.

It covers visible source posture for cache status, queued edits, reconnect review, local export, stale data, sync health, and same-origin help affordances on `/blazor/workbench`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-offline-cache-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_OFFLINE_CACHE_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, service-worker caching, queued mutation persistence, reconnect execution, offline export generation, sync reconciliation, or portal help runtime behavior.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof.
