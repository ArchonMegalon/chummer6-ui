# Desktop Mouse-First OODA

This is the desktop hardening loop for Chummer on Linux first, then the same journey contract can be extended to Windows and macOS.

## Goal

Prove that a user can launch the desktop, create a runner with pointer-driven inputs, and land in a saved workspace without hidden keyboard-only dependencies or stale UI state.

## Current live entrypoint

Use:

```bash
bash scripts/run-desktop-mouse-first-journey-matrix.sh
```

This script publishes the Avalonia desktop for `linux-x64`, runs the live binary under `xvfb` when needed, and writes receipts under:

```text
dist/mouse-first-journey-matrix
```

## OODA loop

### Observe

Capture only live-binary evidence:

- build result
- per-scenario `receipt.json`
- per-scenario `trace.json`
- per-scenario `run.log`
- ordered screenshots
- aggregate `matrix-summary.json`

Reject “it looked fine locally” without these artifacts.

### Orient

Sort failures into one of these buckets:

1. launch failure
2. menu reachability failure
3. dialog field not visible
4. combobox/list refresh failure
5. pointer interaction fallback used unexpectedly
6. workspace publication failure
7. save failure
8. visual coherence failure

Visual coherence failures are real failures when the desktop mixes incompatible palettes, unreadable contrast, clipped text, or stale shell chrome.

### Decide

Fix the smallest shared layer first:

1. shared theme resources
2. shared scaffold/helper
3. shared dialog factory
4. workflow presenter/runtime
5. one window-specific surface

Do not patch screenshots. Patch the layer that produced them.

### Act

After each fix:

1. rebuild `Chummer.Avalonia`
2. rerun the matrix
3. inspect at least:
   - `02b-priority-configured.png`
   - `05-workspace-saved.png`
4. confirm `matrix-summary.json` still reports all scenarios passing

## Required scenarios

The matrix is not complete unless it covers:

1. `sr4-bp`
2. `sr5-priority`
3. `sr5-priority-standard-a-troll-mystic-adept`
4. `sr6-priority`

The SR5 Mysad Troll scenario is the canary for:

- metatype priority refresh
- talent priority selection
- live list synchronization
- mouse-only completion through save

## Failure policy

A desktop release candidate is blocked when any of these occur:

- a scenario receipt is missing
- `status != pass`
- pointer workflow required keyboard-only rescue
- metatype list does not refresh after heritage change
- selected metatype remains invalid after priority change
- shell title shows opaque account ids when a valid email is available
- screenshots show mismatched chrome palettes on shared shell surfaces

## Screenshot review rule

Review screenshots as product evidence, not just test leftovers.

Minimum review set:

- dialog opened
- priority workflow configured
- workspace opened
- workspace saved

The review question is simple:

```text
Would a veteran Chummer user trust this surface after seeing only these screenshots?
```

If not, keep the loop open.

## Extending the matrix

When a bug report arrives:

1. add a scenario or targeted runtime UI test that reproduces it
2. name the scenario after the behavior, not the ticket
3. include the failing ruleset/build combination
4. keep the proof live-binary where possible

Use focused runtime tests for fast guards and the matrix for end-to-end proof.

## Current paired test lanes

- runtime UI guard:
  `AvaloniaFlagshipUiGateTests.Runtime_priority_workflow_heritage_change_refreshes_visible_metatype_list_and_repairs_invalid_selection`
- shell identity guard:
  `DesktopInstallLinkingShellChromeTests.BuildShellWindowTitle_prefers_linked_email_over_opaque_ids_when_available`
- live binary matrix:
  `scripts/run-desktop-mouse-first-journey-matrix.sh`

## Release rule

Do not call desktop polish complete unless:

- targeted runtime tests pass
- the live matrix passes
- screenshots were reviewed after the latest UI change
- the latest change reduced shared debt instead of adding another local exception
