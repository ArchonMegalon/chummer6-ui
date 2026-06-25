# Blazor Workbench Workflow Ledger Staged Proof

## Purpose

This source-staged proof keeps the Chummer Online and proof-compatible Blazor workbench workflow summary tied to the browser-client parity goal.

The web client should be transparent about what it can do and when desktop is still the better tool. Capability and handoff boundaries should be visible in the client instead of being discoverable only in release docs.

## Source-Staged Scope

The staged workflow-ledger lane covers:

- a workflow summary strip on the user-facing Chummer Online route and proof-compatible workbench route
- visible rows for startup, editing, output, recovery, portal handoff, and desktop-only boundaries
- explicit wording that some desktop-only actions still open Chummer desktop
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-workflow-ledger-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_WORKFLOW_LEDGER_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench workflow-summary source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, runtime capability proof, screenshot proof, or desktop-equivalent workflow parity.
