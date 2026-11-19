# Windows UI Redesign — Inventory & Status (WIN-009 gen6)

**Worker:** friday-windows (friday-windows-session-01)
**Base:** UI `a763f05edf947c8a8112bb559f99cbaddb9111f7` / Core `23c0f7bd`
**Assignment:** Controller9014, WIN-009 generation 6 (2026-11-19).
**Status of this document:** live inventory of actual Windows surfaces; updated as waves land.

## 1. Evidence basis

Captures come from real native Windows runs of the exact base bytes
(EXE `07b3e229072a311b674e2f72b880523dd815810cf0270fd196cf5b255cf87287`,
payload `b65e5b4b…`, per the gen5 transfer received by the controller), via the
proven synthetic journey runner (trace mode: `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_*`
+ `CHUMMER_DESKTOP_USER_JOURNEY_TRACE_OUTPUT` + `CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR`).
37 representative screenshots staged under `_scratch/gen33/screens/`; the
credential-free subset ships to release 438932651 under `win-gen33-screens-*`.

## 2. Inventory of real routes/windows/dialogs

| # | Surface | Route/window | Current pattern | Redesign wave |
|---|---------|--------------|-----------------|---------------|
| 1 | App shell | `MainWindow` | Custom dark chrome, **no native titlebar**, no minimize/maximize buttons | **W1** |
| 2 | Top menu | `MainWindow` menu bar | Standard text menu (File/Edit/Special/…) — retained | **W1** (styling) |
| 3 | Primary toolbar | second header row | Icon+text buttons (Open/Save/Print/ALICE/Origin) | **W1** (compact toolbar, status feedback) |
| 4 | Character roster | left sidebar tree | Tree grouped by ruleset | **W1** (desktop density tuning) |
| 5 | New Runner dialog | modal ("New Runner") | **Custom-chrome modal, no OS title bar**, no Esc affordance | **W1** |
| 6 | SR5 Creation wizard | **embedded card/page flow inside main workspace** | Embedded step-left pattern; steps 1..N with Back/Next docked in content | **W1 (owned window)** |
| 7 | Creation review step | embedded | Embedded | **W1 (owned window)** |
| 8 | Skills modal (creation) | modal | Custom chrome | **W2** |
| 9 | SR6 Creation | embedded flow | Same embedded pattern as SR5 | **W2** |
| 10 | Career/attributes workspace | main tabs (12 primary tabs, nested sub-tabs ×2 levels) | Deep three-level nesting; dense data grids | **W2** |
| 11 | SR6 attribute advance (quote/apply) | **embedded buttons + inline flow in Attributes tab** | Advance/Award/Adjust buttons inline; quote/receipt flow inside tab | **W2** |
| 12 | Natural-cap refusal | inline text | Inline blocker text | **W2** |
| 13 | Rules/amend studio | separate window | Already separate window, own menu bar | **W3** (styling only) |
| 14 | Import/export/print/share | toolbar + dialogs | Toolbar-driven | **W2** |
| 15 | Reopen/restore | workspace load route | N/A (functional) | covered by shell |
| 16 | Loading/empty/error/recovery states | status bar + inline | Multi-part status footer incl. "Service: error" + progress bar | **W1** (consistency) |

Not applicable / genuinely absent in current build: campaign/sync conflict
surfaces are covered by the status bar + amend studio; no separate
account/settings window exists yet (flagged in §5 as wave-3 item).

## 3. Diagnosis (why it feels like a badly-ported mobile app)

Despite high data density, the following concrete defects drive the "ported"
feel (confirmed by native captures):

1. **No native window chrome.** Custom dark titlebar rows replace the OS
   title bar; minimize/maximize/resize affordances are absent or custom-drawn.
2. **Wizards embedded in the main workspace.** The SR5 Creation wizard renders
   as a long step-card flow *inside* the main window (step-left embedded
   pattern) — the exact anti-pattern the controller forbids.
3. **Modal dialogs without OS title bars** ("New Runner" is internally titled
   and custom-chromed).
4. **Action buttons docked inside content tabs** (SR6 advance/award/adjust
   inline in Attributes tab rather than toolbar/contextual commands).
5. **Three-level nested tabs** for character workspaces.
6. **Inconsistent status feedback** (multi-part footer with raw service/error
   strings rather than a coherent status/dirty/save/sync presentation).

## 4. Design decisions (locked)

- **Stack:** existing Avalonia 11.3.7; no framework replacement; no Core domain
  logic changes for styling.
- **Theme:** one coherent Fluent-influenced desktop theme: readable Segoe UI
  typography with fallbacks, explicit spacing/control-height/density tokens,
  borders/iconography, and complete state coverage
  (enabled/disabled/hover/focus/error/selection). Follows system light/dark
  and preserves user preference. Branding restrained.
- **Shell:** native titlebar/chrome, minimize/maximize/resize; top menu +
  compact toolbars with real commands; resizable split panes; sortable dense
  tables; character/document tabs where appropriate; contextual commands.
- **Wizard pattern:** every true multistep wizard opens in its **OWNED EXTRA
  WINDOW or modal dialog** — proper Windows title, step list/current
  step/progress, resizable grouped content, stable footer
  (Back/Next/Cancel/Finish — final Review/Finish), real step-validity-gated
  actions, draft progress preserved across Back/Next, explicit Close/Cancel
  handling, focus/keyboard navigation retained, focus returned to invoking
  control, Enter/Escape semantics, no nested-modal maze. Normal editor tabs
  remain editors. Applies to SR5/SR6 Creation, LifeModules, and applicable
  Career/BeforeRun/AfterRun/Downtime multistep workflows.
- **No phone-width centered columns, no huge card stacks, no touch-only
  spacing, no web-dashboard composition.**
- No internal build/package/worker details in ordinary UI.

## 5. Wave plan

- **Wave 1 (this delivery):** theme tokens (`App.axaml`), shell chrome
  (native titlebar, menu, compact toolbar, status bar consistency), owned
  wizard window pattern + representative **SR5 Creation** and **Career**
  wizards moved into owned windows; native before/after screenshots on real
  Windows; resize/scaling/keyboard checks.
- **Wave 2:** remaining creation (SR6, LifeModules), workspace tabs
  flattening review, catalog pickers, import/export/print surfaces, skill
  modal, SR6 advance flow as owned modal flow.
- **Wave 3:** rules/amend studio restyle, account/settings, campaign/sync
  surfaces, loading/empty/error polish pass.

## 6. R1 carryover (from Controller9000)

Earlier SR5 cold-reopen skipped the expected-karma-12 assertion. If no existing
proof exists, a focused reopen of already-saved workspace
`9fb2f1d6df064efeb4f689095c64487f` with explicit expected-karma-12 and
mandatory UI assertion will be executed this generation. Status: **resolved**
(gen6 T792 — existing award proof on gen5 bytes a763f05ed/EXE 07b3e229 plus
focused reopen journey with the runner's mandatory UI karma gate:
"career award reopen verified: workspaceId=9fb2f1d6…; karma 12 matches the
receipt; alias T78D (document-backed identity)", receipt
`_scratch/gen33/t792_reopen/journey_receipt.json` status=pass).
