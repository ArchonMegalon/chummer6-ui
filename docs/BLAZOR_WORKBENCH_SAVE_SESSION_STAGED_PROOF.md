# Blazor Workbench Save-Session Staged Proof

## Purpose

This source-staged proof keeps save and session lifecycle affordances visible on the promoted Blazor workbench route.

The browser client should preserve desktop Chummer's user expectation that save state, Save As handoff, autosave cues, dirty-state warnings, recovery, and export paths are available near the active character workspace.

## Source-Staged Scope

The staged save-session lane covers:

- a save/session lifecycle strip on the promoted workbench route
- save, Save As, autosave, dirty state, recovery, and export shortcuts
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

This is source alignment only. It proves that promoted workbench save-session source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, autosave execution proof, browser file-write proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
