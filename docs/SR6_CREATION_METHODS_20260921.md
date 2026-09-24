# SR6 build-method selection — incremental implementation

The user requested SR6 build types alongside SR5 Life Modules and its Origin
book. This branch is based on UI seal `578f1658e32091944e62ef03a9c02dc252c1bc2d`
and depends on Core SR6 bootstrap commit `913e4b331` (not a sealed package).

## Implemented here

- SR6 lists `Priority`, `SumtoTen`, `PointBuy`, `LifePath`, and `Karma (SR6)`.
  The fifth option is the optional SR6 system confirmed in the user's owned
  Schattenkompendium (printed pp156–157), not the SR5 Karma workflow.
  SR5 retains its existing four choices.
- Each SR6 choice survives dialog rebuilding. Switching to SR5 cannot carry a
  Point Buy or Life Path choice into an SR5 profile.
- Canonical `SumtoTen` retains the existing priority preview and total display.
- Point Buy, Life Path and SR6 Karma never enter the legacy SR5 Karma continuation.
- The dialog now dispatches an edition-bound SR6 bootstrap request with the
  Core-owned SR6 profile for the selected method. It opens the returned draft
  only after validating the exact receipt; an otherwise valid SR5 receipt cannot
  satisfy this request. Generic import is never a fallback.
- SR6 activation may return a committed receipt without an aggregate bundle;
  this reloads once and never invents an SR5 projection or creates a second draft.
- The dialog and completion notice explicitly label this a pending SR6 draft.
  Remaining SR6 wizard steps are unavailable, not finalized or substituted with
  SR5 math. Native integration, budgets, allocation editors and finalization are
  still open.

## Verified locally

Local keyless Docker, .NET 10.0.103, explicit Core/UI source roots:

- Current bootstrap increment: **31 method tests PASS**, zero failed/skipped,
  `sr6-ui-bootstrap-1.log`. Covers all five requests/opened receipts, unavailable
  Core responses retaining selection, committed activation reload, foreign SR5
  receipt rejection and existing SR5 method dispatch. The affected build has zero
  errors and seven unchanged MSTEST0032 warnings. Core's matching bootstrap build
  and 74 focused tests also pass, including save and cold file-store reopen.
- Earlier selection-only evidence (not the current bootstrap increment):
- Affected Presentation and CreationWizard test project build: zero errors.
- 21 build-method tests passed, zero failed/skipped (12 SR6 selection/routing
  cases and 9 existing SR5 method-dispatch cases).
- Seven existing MSTEST0032 warnings remain in unchanged Magic/Resonance tests;
  this is not a warning-free build claim.
- Log: `sr6-ui-selection-4.log` in the local
  `life-module-book-tests-20260921.L0D7pON5` packet.

No package seal, native Android smoke, APK, AAB, signing or Play upload was
performed. This is **pending-draft creation, not completed native SR6 creation**.

## Remaining implementation

1. Connect native SR6 foundation editors to the new edition-owned bootstrap.
2. Typed Priority/Sum-to-Ten wizard adapters and persistence/finalization.
3. Source-backed Companion Point Buy/Life Path/Karma rules and their editors.
4. Native save/reopen/process-restart smoke for each offered complete method.
5. Package integration and a local Android candidate only when the route works.

The checked-in Core SR6 profile currently excludes Companion mechanics; SR5
Karma/Life Modules must not supply those missing rules. Existing SR5 Life Modules
and book completion remain open and Windows follows that work.

## Owned-book follow-up

The optional SR6 Karma method is now a fifth selection, labelled `Karma (SR6)`.
The explicit SR6 continuation guard also covers it, so its shared wire name never
routes into the SR5 Karma editor. The owned German Schattenkompendium distinguishes
Point Buy's 100 CP from optional Karma creation's ordinary 1,000-Karma budget;
neither budget is enabled by this UI change.

The affected local build and **24 focused method tests passed** (15 SR6 cases plus
9 existing SR5 dispatch cases), zero failed/skipped. Seven pre-existing
MSTEST0032 warnings remain; zero build errors. Log:
`sr6-ui-five-methods-1.log`. Core's matching input passed 82 focused tests.
No APK/AAB, native SR6 smoke, package seal, main merge or Play publication.
