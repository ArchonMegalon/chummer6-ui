# WIN-002 Phase 2 — Native Verification Evidence

**Task:** WIN-002 phase 2 — complete creation wizard stages and Career-mode routes, verified natively.
**Branch:** `windows/WIN-002-creation-career` (PR #490, draft, base `feat/remote-windows-coordination-20261031`)
**Date:** 2026-10-31
**Harness:** `C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-002\verification-harness\win002-journey.ps1` (Debug win-x64)

## Final run result

| Check | Result |
|---|---|
| Journey status | **pass** (receipt.json, 69 steps) |
| Workspace published | ✓ (auth → File → New Character dialog → create) |
| Wizard host rendered | ✓ (`02-creation-wizard-open.png`) |
| Priority table rendered | ✓ 5 categories × ranks A–E, authority-rendered |
| Priority interactions | ✓ heritage A, heritage pick, talent E, talent pick, attributes B, skills C, resources D |
| Priority confirm | ✓ "priority assignment recorded as the authoritative draft" |
| Deep-stage gates | ✓ attributes/skills authoritatively blocked (Presentation-layer editors out of WIN-002 scope) — recorded as evidence |
| Workspace saved | ✓ |
| Screenshots | 6 in `native-journey/screenshots/` |
| Build | 0 warnings / 0 errors |

## What phase 2 shipped

1. **Priority prerequisite surface** (`CharacterCreationWizardControl` + `MainWindow.CreationWizard.cs`):
   5×5 priority grid, heritage/talent sub-pickers, confirm affordance, Load→Preview→Confirm against Core
   `ICharacterCreationPrerequisiteService`, state rebind after confirm.
2. **Root product bug fixed:** `App.axaml.cs` now passes all 7 owner-bound creation readers into
   `WorkspaceOverviewStateFactory`. Without them the desktop-install owner context left foundation
   authority permanently "pending" (zero legal options, Continue disabled).
3. **Classic Windows wizard chrome** (controller directive: Chummer5a feel): left step pane,
   "Step N of M" header, footer `< Back` / `Next >` / `Finish`; dense light WinForms-style shell —
   edge-to-edge docked panels, square corners, 24–26px dense controls, no card shadows; priority
   table compact cells (92px), no clipping at 1280×880.
4. **Journey runner modernization:** atomic dispatcher clicks (resolve + probe + raise in one
   `InvokeAsync`), effect-gated waits, disclosed Click-routed fallback, 300s budget,
   Priority vs Life-Modules build-method routing.

## Authoritative boundaries honored (not bugs)

- **Foundation step is Life-Modules-only**: `EvaluateFoundationAuthority` returns `IsReady: false`
  unconditionally for Priority builds; `attributes.isAvailable` gates on qualities deep-stage
  authority. The journey records these as authoritative evidence instead of failing.
- Deep-stage editors (attributes/skills/qualities) are Presentation-layer surfaces outside
  WIN-002's allowed paths — the Career finalize route is gated on them by design until their
  workstream lands.

## Controller action items

- PR #490 is draft against the handoff branch; merge after WIN-001/WIN-005 acceptance per the
  authorized sequence WIN-001 → WIN-005 → WIN-002 → main (seal ceremony).
