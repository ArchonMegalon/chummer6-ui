# Blazor Workbench File Intake Staged Proof

## Purpose

This source-staged proof keeps browser file-intake posture visible on the user-facing Chummer Online route and proof-compatible Blazor workbench route.

Avalonia can use native file dialogs directly. The web client needs browser-safe open/import, Hero Lab import, XML editor, support, and native desktop handoff paths without pretending that browser file access is identical to native desktop file access.

## Source-Staged Scope

The staged file-intake lane covers:

- a file-intake strip on the user-facing Chummer Online route and proof-compatible workbench route
- open/import, Hero Lab import, XML editor, desktop installer handoff, and support affordances
- source alignment with the shared desktop-shaped import dialogs
- scoped responsive styling for desktop and mobile browser use

## Source Check

```bash
bash scripts/ai/milestones/blazor-workbench-file-intake-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_WORKBENCH_FILE_INTAKE_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It proves that Chummer Online and proof-compatible workbench file-intake source, style, status reporting, shared dialog source, and docs agree.

It is not hosted browser execution proof, Docker self-host proof, file picker proof, XML mutation proof, import execution proof, native file-system proof, screenshot proof, or desktop-equivalent workflow parity.
