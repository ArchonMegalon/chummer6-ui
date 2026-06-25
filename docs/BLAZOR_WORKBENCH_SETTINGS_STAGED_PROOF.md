# Blazor Workbench Settings Staged Proof

## Purpose

This source-staged proof keeps global settings, character settings, ruleset choice, update posture, and support preferences visible on the user-facing Chummer App route and proof-compatible Blazor workbench route.

Desktop Chummer users expect preferences to be reachable without leaving the workbench. The browser client should preserve that workflow slot while staying honest about which settings are only surfaced versus persisted at runtime.

## Source-Staged Scope

The staged settings lane covers:

- a settings strip on the user-facing Chummer App route and proof-compatible workbench route
- global settings, character settings, ruleset, update status, support settings, and same-origin help affordances
- source alignment with the shared desktop-shaped settings dialogs
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-settings-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SETTINGS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and proof-compatible workbench settings source, style, status reporting, shared dialog source, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, persisted preference proof, settings save proof, portal help runtime proof, screenshot proof, or desktop-equivalent workflow parity.
