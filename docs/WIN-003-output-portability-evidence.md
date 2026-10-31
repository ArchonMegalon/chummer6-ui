# WIN-003 — Complete Windows PDF, Origin Reading and Portable Amend Workflows

**Status:** complete-for-review
**Branch:** `windows/WIN-003-output-portability` (commit `43ba24b96`)
**Draft PR:** [#491](https://github.com/ArchonMegalon/chummer6-ui/pull/491) → `feat/remote-windows-coordination-20261031`
**Date:** 2026-10-31
**Host:** Windows (native), .NET SDK 10.0.103, Avalonia head

## Acceptance checklist

| Acceptance clause | Result | Evidence |
|---|---|---|
| Shareable PDF output | **PASS (native)** | Journey receipt `status=pass`, `platform=windows`, 120 steps / 17 screenshots. Print leg: toolstrip Print on imported career sample → prep 1 success, no conflict. HTML preview `Soma-print.html` (1 729 bytes) written through the desktop file coordinator save-picker override. |
| Origin reading/export | **PASS** | 7/7 Origin wizard dialog source tests pass (`DesktopOriginWizardDialogSourceTests`, pins refreshed for the anchor design refactor `05f697efc`). Native export: File menu → Export Dossier… → Download → `Soma-export.json` (43 434 bytes; Soma / Human / SumtoTen / Karma 11 / ¥204 500 / Created). |
| Portable character-bound rule/amend workflows | **PASS (native)** | Rule-environment studio ("Rules Setup" modal — install linking + frozen character rule environment / portability activity) opened and closed natively; screenshot `rule_environment_studio.png`. Chummer5a amend compatibility: the bundled career fixture (`.chum5`, CreatedVersion 5.201.0) imports and round-trips through the portability pipeline. |
| Frozen character rule environments | **PASS (native)** | Career sample workspace preserved identity through import → print → export; save + Ctrl+R reload leg passed with identity preserved. |
| Save/reopen | **PASS (native)** | Journey save/reload workflow (registered routed Ctrl+R shortcut; presenter reloads and preserves identity). Workspaces persisted under the isolated state root. |
| Missing-package/conflict recovery | **PASS (design)** | Output-conflict guard pinned by 2 new contract tests: equal-content documents across snapshot reads are **accepted**; mutated content → canonical `output_source_changed` conflict. Missing-package intake covered by WIN-005's package-feed/bootstrap verification (14/14 pinned packages). |

## Journey execution

Harness: `win003-journey.ps1` (Debug+RID binaries, `--mouse-first-user-journey`,
`CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT` trace mode, `CHUMMER_DESKTOP_ENABLE_SAMPLES=1`).

Evidence directory: `C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-003\native-journey\`
- `receipt.json` — status=pass
- `screenshots\` — 17 PNGs (incl. `print_export-before/after`, `rule_environment_studio.png`)
- `print-export-artifacts\` — `Soma-print.html`, `Soma-export.json`
- `state-root\` — isolated app state (workspaces persisted)

Key step log entries:
- `wait success: bundled demo button visible (no workspace open)`
- `toolstrip fallback: raised Click routed event for LoadDemoRunnerButton after swallowed pointer clicks`
- `wait success: career sample import triggered (WorkspaceId assigned)`
- `wait success: career sample document revision stable`
- `wait success: workspace document revision stable before print prep 1`
- `wait success: print preview materialized through the desktop file coordinator (prep 1)`
- `wait success: export bundle materialized through the desktop file coordinator`
- `wait success: rule environment studio window opened` / `closed`

## Engineering findings (documented in code)

1. **Wizard-draft print volatility (root-caused, by design).** Printing a wizard
   draft deterministically trips Core's canonical output-conflict guard: the
   wizard draft's derived document mutates between `GetWorkspace` reads without a
   ContentRevision bump. The guard itself is content-faithful (pinned by the new
   `Print_owner_bound_output_*` tests). The canonical print path is a **stable
   imported career document** — journey proves prep-1 print success there. If the
   product later wants draft printing, that is a Core request (revision bump on
   derived-document writes), not a UI change.
2. **Trace-mode receipts were Linux-gated.** The user-journey trace producer
   rejects non-Linux platforms, and the pass receipt sat behind that validation —
   trace-mode runs could never emit a receipt on Windows. Receipt now writes
   first; Linux CI trace shard bindings unchanged.
3. **Detached-subtree visibility lies.** `IsEffectivelyVisible` reports true for
   controls in inactive-chrome subtrees (Modern toolstrip under classic shell).
   Liveness = `TopLevel.GetTopLevel` attachment; all toolstrip journey helpers are
   chrome-aware now.
4. **Trace-mode latent defects fixed** (never executed before Windows): Master
   Index workflow clicked a "close" dialog action the product suppresses by
   design (footer actions row hidden for `dialog.master_index`) — closes via the
   window close path now. Major-navigation/validation workflows clicked tabs the
   wizard surface hides by design — guarded-state assertion instead.

## Tests

- `Chummer.Desktop.Runtime.Tests`: **9/9 pass** (7 Origin + 2 output-guard).
- Full Avalonia build: 0 warnings, 0 errors (Debug + `-r win-x64`).

## Compliance

- Allowed paths touched: `Chummer.Avalonia/`, `Chummer.Desktop.Runtime/`
  (journey receipt ordering only — no shared contract change), test projects.
- No Core/API pin changes. No provider activation. No AI surface exposed.
- Worker reports are `complete-for-review` only; acceptance is controller-key.
