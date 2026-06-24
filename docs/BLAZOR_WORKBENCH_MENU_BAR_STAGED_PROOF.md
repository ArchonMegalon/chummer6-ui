# Blazor Workbench Menu-Bar Staged Proof

## Purpose

This source-staged proof keeps familiar menu affordances visible on the promoted Blazor workbench route.

The browser client should feel like another Chummer client head, so File, Build, View, Character, Tools, and Help entry points should remain visible even before full browser keyboard/menu execution parity is proven.

## Source-Staged Scope

The staged menu-bar lane covers:

- a menu rail on the promoted workbench route
- File, Build, View, Character, Tools, and Help shortcuts
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

This is source alignment only. It proves that promoted workbench menu-bar source, style, status reporting, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, keyboard-event proof, keyboard-accelerator proof, menu-command execution proof, screenshot proof, or desktop-equivalent workflow parity.
