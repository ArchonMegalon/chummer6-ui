# Blazor Browser Output Handoff Staged Proof

## Purpose

This source-staged proof keeps browser save, save-as, export, print, and download handoff routes aligned before the hosted and Docker runtime proof receipts are refreshed.

These workflows are desktop-sensitive because Avalonia can use native file dialogs and platform print integration. The Blazor web client must keep the same user workflow slot while using browser-native substitutes: visible save result state, browser download preparation, export download handoff, and browser print preview preparation.

## Expected Routes

```text
/blazor/workbench?workspace=ws-1&command=save_character
/blazor/workbench?workspace=ws-1&command=save_character_as
/blazor/workbench?workspace=ws-1&command=save_character_as&dialog_action=download
/blazor/workbench?workspace=ws-1&command=export_character
/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download
/blazor/workbench?workspace=ws-1&command=print_character
```

## Source Check

```bash
bash scripts/ai/milestones/blazor-browser-output-handoff-staged-proof-check.sh
```

Expected receipt:

```text
.codex-studio/published/BLAZOR_BROWSER_OUTPUT_HANDOFF_STAGED_PROOF.generated.json
```

## Boundary

This is source alignment only. It is not a hosted or Docker browser execution receipt, and it must not be used to claim save/export/print/download parity on `chummer.run`.

Promotion requires refreshed Docker self-host proof, hosted public-edge execution proof, and browser-lane aggregate proof after these routes are represented in runtime receipts.
