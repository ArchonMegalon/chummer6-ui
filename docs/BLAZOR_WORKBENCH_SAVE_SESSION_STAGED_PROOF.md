# Blazor Workbench Save-Session Staged Proof

## Purpose

This source-staged proof keeps save and session lifecycle affordances visible on the user-facing Chummer Online route and proof-compatible /blazor/workbench compatibility route.

The browser client should preserve desktop Chummer's user expectation that save state, Save As handoff, autosave cues, dirty-state warnings, recovery, help, and export paths are available near the active character workspace.

## Source-Staged Scope

The staged save-session lane covers:

- a save/session lifecycle strip on the user-facing Chummer Online route and proof-compatible compatibility route
- save, Save As, autosave, dirty state, recovery, help, and export shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-save-session-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_SAVE_SESSION_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench save-session source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, autosave execution proof, portal-help-runtime proof, browser file-write proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
