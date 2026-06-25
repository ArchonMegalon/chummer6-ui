# Blazor Workbench Data-Packs Staged Proof

## Purpose

This source-staged proof keeps rules and data-pack management affordances visible on the user-facing Chummer App route and proof-compatible Blazor workbench route.

The browser client should preserve desktop Chummer's expectation that sourcebooks, errata, custom data, update packs, validation scope, help, and data-folder context are visible near the active character workspace.

## Source-Staged Scope

The staged data-packs lane covers:

- a rules/data-pack strip on the user-facing Chummer App route and proof-compatible workbench route
- sourcebooks, errata, custom data, update pack, validation scope, same-origin help, and data folder shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-data-packs-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_DATA_PACKS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and proof-compatible workbench data-packs source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, live sourcebook-loading proof, custom-data import proof, data-update proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
