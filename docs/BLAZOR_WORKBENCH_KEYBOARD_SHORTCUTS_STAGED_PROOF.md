# Blazor Workbench Keyboard-Shortcuts Staged Proof

## Purpose

This source-staged proof keeps keyboard help and accelerator intent visible on the user-facing Chummer Online route and proof-compatible /blazor/workbench compatibility route.

The browser client should support desktop-speed users by making command help, save/output, section jump, density toggle, portal help, and support escape affordances visible before any browser key-event parity is claimed.

## Source-Staged Scope

The staged keyboard-shortcuts lane covers:

- a keyboard-shortcuts rail on the user-facing Chummer Online route and proof-compatible compatibility route
- command help, save/output, section jump, density toggle, help, and support escape shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-keyboard-shortcuts-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_KEYBOARD_SHORTCUTS_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench keyboard-shortcuts source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, key-event proof, portal-help-runtime proof, accelerator-execution proof, route-click proof, screenshot proof, or desktop-equivalent workflow parity.
