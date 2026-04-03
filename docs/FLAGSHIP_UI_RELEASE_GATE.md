# Flagship UI Release Gate

Purpose: block desktop promotion when the Avalonia shell is merely buildable but not convincingly usable.

This repo-level gate implements the product requirement defined in `chummer6-design/products/chummer/FLAGSHIP_UI_RELEASE_GATE.md`.

## What this gate proves

The executable gate is green only when all of the following are true:

1. The promoted desktop output physically contains the bundled demo runner fixture at `Samples/Legacy/Soma-Career.chum5`.
2. Headless interaction tests prove that:
   - top-level menu clicks expose visible command choices
   - the real runtime-backed menu bar keeps the classic `File / Edit / Special / Tools / Windows / Help` labels visible
   - the primary top-level menus that are supposed to be actionable actually open visible command choices under runtime-backed interaction
   - settings opens an in-shell interactive dialog state instead of only mutating hidden state
   - the shell remains responsive after settings opens
   - the bundled demo runner button really dispatches an import when the fixture is available
   - core keyboard shortcuts resolve to the same shell commands
   - full dual-head desktop workflow equivalence is present for legacy builder/editor families, not only shell-level interaction smoke
   - explicit Chummer5a parity, SR4 parity, SR6 parity, and SR4/SR6 frontier parity receipts are all passing in the same gate run
   - the aggregate desktop workflow-execution gate receipt is passing in the same gate run
   - the menu/bootstrap proof and demo-runner import proof are runtime-backed: real `ShellPresenter`, real `CharacterOverviewPresenter`, and a fixture-backed runtime client must be used instead of recording presenters or synthetic-only shell state
3. The gate publishes screenshot evidence for:
   - initial shell
   - menu open
   - settings open
   - loaded runner
   - explicit character-creation section posture
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
- verifies `DualHeadAcceptanceTests` still contains the required full-workflow equivalence tests
- fail-closes on `chummer5a-desktop-workflow-parity-check.sh`, `sr4-sr6-desktop-parity-frontier-receipt.sh`, and `materialize-desktop-workflow-execution-gate.sh`
- fail-closes when SR4/SR6 workflow ledgers drop canonical required family IDs or omit per-family audit-test declarations
- verifies the release signoff document cites the gate
- publishes screenshot evidence under `.codex-studio/published/ui-flagship-release-gate-screenshots/`
- publishes `.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json`

## Why this gate exists

A release can look "green" while still failing paying-user expectations:

- menu clicks do not visibly react
- settings appears to hang because the shell does not surface an obvious interactive state
- the shell advertises a demo runner that is not actually shipped

This gate closes exactly those seams.

It must not be satisfiable by a recording/stub harness alone. If the shell bootstrap is broken in the promoted runtime, the gate must fail even when a synthetic harness can still paint the menu and dialog chrome.

The legacy visual oracle is old `frmCareer` / `CharacterCareer` from Chummer5a. The promoted workbench is allowed to modernize, but it must keep the recognizable landmarks from that form: top menu, immediate toolstrip, visible character-tab workbench, dense browse rhythm, visible detail pane, and compact progress/status strip.
The left rail must now read like a Chummer codex tree instead of stacked dashboard cards: a visible `Codex` heading, a tree navigator for open characters/tabs/actions/workflows, and no second left-side tab control.
The shell must fail closed on obviously non-Chummer dashboard copy as well: `Career-style workbench`, `Command Palette`, and `Coach Sidecar` are release-blocking regressions in the promoted desktop chrome.
