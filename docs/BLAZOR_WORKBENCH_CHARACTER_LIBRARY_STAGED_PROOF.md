# Blazor Workbench Character Roster Staged Proof

## Purpose

This source-staged proof keeps Character Roster and recent-dossier management affordances visible on the user-facing Chummer Online route and /blazor/workbench compatibility route.

The browser client should preserve desktop Chummer's expectation that open, recent, pinned, cloned, archived, imported, and help-guided Character Roster recovery stay close to the active dossier.

## Source-Staged Scope

The staged Character Roster lane covers:

- a Character Roster strip on the user-facing Chummer Online route and /blazor/workbench compatibility route
- open, recent, pin, clone, archive, import, and same-origin help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-character-library-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_CHARACTER_LIBRARY_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and /blazor/workbench compatibility route Character Roster source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, file-open proof, roster-persistence proof, clone proof, archive proof, import proof, portal help runtime proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
