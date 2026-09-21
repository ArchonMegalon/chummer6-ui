# SR6 build-method selection — incremental implementation

The user requested SR6 build types alongside SR5 Life Modules and its Origin
book. This branch is based on UI seal `578f1658e32091944e62ef03a9c02dc252c1bc2d`
and depends on Core feature commit `a382745fb` (not yet a sealed package).

## Implemented here

- SR6 lists its own `Priority`, `SumtoTen`, `PointBuy`, and `LifePath` identities.
  It no longer offers SR5 `Karma`; SR5 retains its existing four choices.
- Each SR6 choice survives dialog rebuilding. Switching to SR5 cannot carry a
  Point Buy or Life Path choice into an SR5 profile.
- Canonical `SumtoTen` retains the existing priority preview and total display.
- Point Buy and Life Path never enter the legacy SR5 Karma continuation.
- The dialog explicitly states that SR6 creation is not connected yet. The
  existing SR5-only bootstrap guard stays intact; no generic import fallback,
  invented budgets, automatic metatype, or alternate-edition runner is created.

## Verified locally

Local keyless Docker, .NET 10.0.103, explicit Core/UI source roots:

- Affected Presentation and CreationWizard test project build: zero errors.
- 21 build-method tests passed, zero failed/skipped (12 SR6 selection/routing
  cases and 9 existing SR5 method-dispatch cases).
- Seven existing MSTEST0032 warnings remain in unchanged Magic/Resonance tests;
  this is not a warning-free build claim.
- Log: `sr6-ui-selection-4.log` in the local
  `life-module-book-tests-20260921.L0D7pON5` packet.

No package seal, native Android smoke, APK, AAB, signing or Play upload was
performed. This selection increment is **not enabled SR6 character creation**.

## Remaining implementation

1. Core-owned SR6 bootstrap/source context and edition-specific settings.
2. Typed Priority/Sum-to-Ten wizard adapters and persistence/finalization.
3. Source-backed Companion Point Buy/Life Path rules and their editors.
4. Native save/reopen/process-restart smoke for each offered complete method.
5. Package integration and a local Android candidate only when the route works.

The checked-in Core SR6 profile currently excludes Companion mechanics; SR5
Karma/Life Modules must not supply those missing rules. Existing SR5 Life Modules
and book completion remain open and Windows follows that work.
