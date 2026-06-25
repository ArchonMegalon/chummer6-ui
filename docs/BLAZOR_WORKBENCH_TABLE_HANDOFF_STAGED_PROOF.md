# Blazor Workbench Table-Handoff Staged Proof

## Purpose

This source-staged proof keeps table handoff affordances visible on the promoted Blazor workbench route.

The browser client should expose GM packet, initiative card, condition tracker, public handout, private notes, and table export posture so play-session output stays close to the sheet.

## Source-Staged Scope

The staged table-handoff lane covers:

- a table strip on the promoted workbench route
- GM-packet, initiative-card, condition-tracker, public-handout, private-notes, and table-export shortcuts
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

This is source alignment only. It proves that promoted workbench table-handoff source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, packet-generation proof, initiative-card proof, condition-export proof, handout-filtering proof, private-note proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
