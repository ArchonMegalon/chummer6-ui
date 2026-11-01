# Build Ghost AI-Finish — Implementation & Verification Evidence

**Branch:** `windows/creation-ai-finish`
**Base:** `feat/remote-windows-coordination-20261031`
**Date:** 2026-11-01
**Scope:** `Chummer.Avalonia/` only (allowed path). Core/API pins untouched.

## What was built

The "Finish with AI" (Build Ghost finishing) flow — the first runtime consumer of
the pure advice engine (`CompletionRecommendationBuilder`, `Chummer.Presentation`):

1. **`Chummer.Avalonia/Creation/CreationAiFinishService.cs`** (new) — orchestrator.
   - `BuildProposal`: owner-bound completion inputs via Core's
     `OwnerBoundCharacterCreationCompletionReader` → archetype auto-suggestion →
     `BuildMissingOnly` (digest-gated, built-in archetype packs) → review rows.
   - `Apply`: re-verifies the inputs snapshot digest before the first mutation,
     then applies missing-only selections through each domain's own
     Load → Preview → Confirm service (attributes → skills → qualities →
     resources → gear), with deterministic idempotency keys and per-domain
     results. Refuses to guess; aborts cleanly on any domain failure.
2. **`Controls/CreationAiFinishReviewWindow.cs`** (new) — Windows-desktop-style
   modal review dialog (left-step-pane wizard chrome per project directive):
   every recommendation with rationale + rule evidence, explicit Apply/Cancel.
   Apply is a human action; the AI never mutates on its own.
3. **Wizard surface** — "Finish with AI" button on the wizard footer (visible
   while creation is in progress — the whole point is filling blocked deep
   stages *before* review), busy/result plumbing, `FinishWithAiRequested` event,
   MainWindow handler with the same "Disable AI features" preference gate as
   the Rook chat.
4. **Starting-cash roll on the finalize step** — Core requires the player's
   starting-nuyen election (`CharacterCreationStartingCashChoice`); the Avalonia
   app previously had no such UI, so finalize always blocked with
   `creation-finalization-starting-cash-choice-required`. The app now rolls the
   dice **for the player** (per product decision, Chummer5a parity), displays
   `"{source}: rolled {N}d6 x{mult} = {total} nuyen"` on the finalize step, and
   offers Re-roll. Finalize passes the rolled choice to Review/Confirm.
5. **Journey instrumentation** (`DesktopMouseFirstJourneyRunner.cs`) — env-gated
   AI-finish leg (`CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_AI_FINISH=1`): click →
   dialog → apply → starting-cash evidence → finalize → editor-unlocked, with
   extended wait budgets (cold-engine apply/finalize exceed the 20s default)
   and a raised journey cap. The pre-existing T33 resources leg is skipped when
   finalization already happened (post-finalize the preview pipeline is
   correctly immutable).

## Verification

- **Build:** `dotnet build Chummer.Avalonia/Chummer.Avalonia.csproj -c Debug -r win-x64`
  → exit 0, **0 warnings, 0 errors** (WIN-001 sandbox SDK, harness-parity).
- **Tests:** `Chummer.CreationWindow.Tests` 274/288 with real Core content
  (`--test-parameter ChummerCoreContentRoot=<core checkout>`); all 14 failures
  are pre-existing `Sr6BuildMethodSelectionTests`, byte-identical on a clean
  main via `git stash` — no Completion/Build-Ghost test failures.
- **Native journey (this workspace):** receipt `status=pass`, scenario
  `sr5-priority`, 78 recorded steps, 10 evidence screenshots. Full chain:
  priority draft (attributes/qualities/skills/review all `blocked`) →
  Finish with AI → review dialog → Apply → *plan applied across domains* →
  review step available → starting cash rolled for the player → finalize →
  *authoritative finalization applied (advanced editor unlocked)* → saved.
- **Evidence artifacts:** `D:\sources\chummer\_scratch\ai-finish-journey\`
  (`receipt.json`, `trace.json`, `screenshots/01..05*.png` incl.
  `03f-ai-finish-review.png`, `03g-ai-finish-applied.png`,
  `04-finalized-editor-unlocked.png`).

## Compliance

- `MigrationComplianceTests.cs` untouched; DI pin text intact.
- Core/API pins unchanged (`v20261017.2` / `8706f38a7b5c2`).
- AI policy honored: proposal-only engine + explicit human Apply; the app rolls
  starting cash for the player but never invents rules outcomes.

## Known gaps / follow-ups

- The 14 pre-existing `Sr6BuildMethodSelectionTests` failures are unrelated to
  this diff (SR6 bootstrap/locale; reproduced on clean main) but should be
  triaged by the controller.
- Orchestrator unit tests (digest-guard refusal, per-domain failure isolation)
  are a natural follow-up PR; the domain services themselves are covered by the
  existing suite.
