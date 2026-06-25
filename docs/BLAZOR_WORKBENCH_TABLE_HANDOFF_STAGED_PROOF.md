# Blazor Workbench Table-Handoff Staged Proof

## Purpose

This source-staged proof keeps table handoff affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

Chummer Online should expose GM packet, initiative card, condition tracker, public handout, private notes, table export, and same-origin help posture so play-session output stays close to the dossier.

## Source-Staged Scope

The staged table-handoff lane covers:

- a table strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- GM-packet, initiative-card, condition-tracker, public-handout, private-notes, table-export, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-table-handoff-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_TABLE_HANDOFF_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route table-handoff source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, packet-generation proof, initiative-card proof, condition-export proof, handout-filtering proof, private-note proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
