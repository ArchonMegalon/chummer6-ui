# Blazor Workbench Session-Locking Staged Proof

## Purpose

This staged proof keeps the promoted Blazor workbench source aligned around session-locking and edit-ownership posture for browser editing.

It covers visible source posture for lock status, owner handoff, read-only fallback, stale-session recovery, conflict owner, and takeover review affordances on `/blazor/workbench`.

## Canonical Command

```bash
bash scripts/ai/milestones/blazor-workbench-session-locking-staged-proof-check.sh
```

The command writes:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SESSION_LOCKING_STAGED_PROOF.generated.json
```

## Boundary

This is source-staged alignment only. It does not prove hosted browser execution, Docker self-host execution, lock acquisition, takeover mutation, cross-tab arbitration, stale lock cleanup, or conflict persistence.

Runtime promotion requires refreshed hosted public-edge execution proof and Docker self-host proof.
