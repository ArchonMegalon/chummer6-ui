# Blazor Workbench Menu-Bar Staged Proof

## Purpose

This source-staged proof keeps familiar menu affordances visible on the user-facing Chummer App route and proof-compatible Blazor workbench route.

The browser client should feel like another Chummer client head, so File, Build, View, Character, Tools, and a same-origin Help entry point should remain visible even before full browser keyboard/menu execution parity is proven.

## Source-Staged Scope

The staged menu-bar lane covers:

- a menu rail on the user-facing Chummer App route and proof-compatible workbench route
- File, Build, View, Character, Tools, and same-origin Help shortcuts
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-menu-bar-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_MENU_BAR_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer App and proof-compatible workbench menu-bar source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, keyboard-event proof, keyboard-accelerator proof, portal-help-runtime proof, menu-command execution proof, screenshot proof, or desktop-equivalent workflow parity.
