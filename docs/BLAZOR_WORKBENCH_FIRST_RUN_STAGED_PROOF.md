# Blazor Workbench First-Run Staged Proof

## Purpose

This source-staged proof keeps first-run onboarding affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should help users start with a new runner, import desktop files, try a sample runner, restore the last session, configure Docker self-hosting, open Chummer Online docs, and recover through same-origin help without depending on hidden menu discovery.

## Source-Staged Scope

The staged first-run lane covers:

- a first-run onboarding strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- new runner, desktop import, sample runner, restore session, self-host setup, docs, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-first-run-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_FIRST_RUN_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route first-run source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, setup persistence proof, migration proof, desktop import proof, Docker installer execution proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
