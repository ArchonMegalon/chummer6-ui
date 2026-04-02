# Flagship UI Release Gate

Purpose: block desktop promotion when the Avalonia shell is merely buildable but not convincingly usable.

This repo-level gate implements the product requirement defined in `chummer6-design/products/chummer/FLAGSHIP_UI_RELEASE_GATE.md`.

## What this gate proves

The executable gate is green only when all of the following are true:

1. The promoted desktop output physically contains the bundled demo runner fixture at `Samples/Legacy/Soma-Career.chum5`.
2. Headless interaction tests prove that:
   - top-level menu clicks expose visible command choices
   - settings opens an in-shell interactive dialog state instead of only mutating hidden state
   - the shell remains responsive after settings opens
   - the bundled demo runner button really dispatches an import when the fixture is available
   - core keyboard shortcuts resolve to the same shell commands
3. The gate publishes screenshot evidence for:
   - initial shell
   - menu open
   - settings open
   - loaded runner
   - a dense shell state in light theme
   - a dense shell state in dark theme
3. `WORKBENCH_RELEASE_SIGNOFF.md` cites this gate as part of the release closeout bar.

## Executable lane

Run:

```bash
bash scripts/ai/milestones/b14-flagship-ui-release-gate.sh
```

That lane:

- builds `Chummer.Avalonia` in `Release`
- verifies `Samples/Legacy/Soma-Career.chum5` exists in the built desktop output
- runs the targeted Avalonia headless UI gate tests
- verifies the release signoff document cites the gate
- publishes screenshot evidence under `.codex-studio/published/ui-flagship-release-gate-screenshots/`
- publishes `.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json`

## Why this gate exists

A release can look "green" while still failing paying-user expectations:

- menu clicks do not visibly react
- settings appears to hang because the shell does not surface an obvious interactive state
- the shell advertises a demo runner that is not actually shipped

This gate closes exactly those seams.
