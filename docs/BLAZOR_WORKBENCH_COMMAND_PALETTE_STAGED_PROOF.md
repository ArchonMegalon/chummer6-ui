# Blazor Workbench Command Palette Staged Proof

## Purpose

This source-staged proof keeps the Chummer Online and /blazor/workbench compatibility route command-palette posture tied to the browser-client parity goal.

The web client should preserve desktop habits where the browser platform allows it. Common Chummer actions should be discoverable through keyboard-style hints and reload-safe links instead of forcing users through disconnected cards.

## Source-Staged Scope

The staged command-palette lane covers:

- a command-palette strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- keyboard-style hints for new runner, open/import, Build Lab, gear, save/download, print, support, and same-origin help
- reload-safe workbench links for every visible command
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-command-palette-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_COMMAND_PALETTE_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route command-palette source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, actual keyboard-event proof, command execution proof, portal help runtime proof, screenshot proof, or desktop-equivalent workflow parity.
