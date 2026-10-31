# WIN-002 Phase 1 Evidence: New Runner creation-bootstrap-source-context-unavailable resolved

- Task: WIN-002 — Complete supported Windows SR5 manual Creation and Career
- Branch: `windows/WIN-002-creation-source-context` (from exact base `2fec7c7a`)
- Worker: `friday-windows`
- Date: 2026-10-31
- Scope: Phase 1 — resolve the New Runner blocker; prove native creation end-to-end.

## Summary

The New Runner dialog's `create_character` action was refused by the pinned core with
`creation-bootstrap-source-context-unavailable`. Root cause was established by probes
against the real pinned packages; the fix ships entirely inside WIN-002's allowed paths
(`Chummer.Avalonia/`, `tests/`, `docs/`), and native mouse-driven creation now succeeds.

## Root cause (probe-proven)

The pinned core's creation bootstrap mints a binding requiring settings-profile anchors
from `Chummer/data/settings-all-sources.xml`
(`SettingsSourceAnchor: settings-all-sources.xml#setting:<guid>`). Probe-proven facts:

1. The UI repository's `Chummer/data` mirror (94 files) **does not contain**
   `settings-all-sources.xml` — creation can never bootstrap against it.
2. Overlays cannot substitute: `CHUMMER_AMENDS_PATH` at Core content clears the
   source-context refusal but the pinned core then rejects metatype data via overlay
   (`metatype-overlay-unsupported`); overlays are amends-only by design. Pointing the
   content root directly at a verified Core checkout succeeds (`outcome: success`).

Therefore the headless core's content root must be the **verified Core content
checkout**: repo `https://github.com/ArchonMegalon/chummer6-core.git` at pinned commit
`8706f38a7b5c29308fe007baa101038f06f61906`, validated against the pinned inventory in
`scripts/ai/verify_fresh_checkout_package_plane.py`.

## Fix (all in allowed paths)

1. `Chummer.Avalonia/DesktopCoreContentRootResolver.cs` (new) resolves the Core content
   checkout. Discovery order: `CHUMMER_CORE_CONTENT_ROOT` (explicit; invalid values fail
   fast with the pinned Core repo URL in the message) → the UI repo root itself if it
   already is a Core checkout → the `chummer6-core` sibling of the repo root. Every
   candidate passes structural validation (`Chummer/data/settings-all-sources.xml`,
   `settings.xml`, `Chummer/lang/`, `Chummer/customdata/`) and identity validation (git
   HEAD equals a pinned Core commit mirrored from
   `scripts/ai/verify_fresh_checkout_package_plane.py`, the release authority).
2. `Chummer.Avalonia/App.axaml.cs`: when a Core checkout resolves, wire
   `AddChummerLocalRuntimeClient(coreContentRoot, coreContentRoot, "avalonia")` — the
   probe-proven configuration; otherwise the pre-existing
   `AddChummerLocalRuntimeClient(AppContext.BaseDirectory, contentRoot, "avalonia")`
   fallback is preserved byte-for-byte (no-Core-checkout behavior unchanged).
3. `tests/test_desktop_core_content_root.py` (new): 9 source-contract tests guarding
   discovery order, the settings-all-sources anchor, pinned-commit identity, fail-fast
   semantics, proven wiring, and preserved fallback. 9/9 pass.
4. `Chummer.Avalonia/DesktopMouseFirstJourneyRunner.cs`: WIN-001's failure-diagnostics
   hunks ported (+24/-2, additive) so failure packets carry presenter errors.

Deliberately untouched (outside allowed paths): `Chummer.Desktop.Runtime` (compliance
pin at `MigrationComplianceTests.cs:6893` keeps `AddChummerHeadlessCore(baseDirectory,
currentDirectory)` intact) and `Chummer.Blazor.Desktop` (compliance pin at line 6898
pins its exact 2-arg call text; parity fix needs an authorized pin update — open item).

## Verification

1. Contract tests: `python -m unittest tests.test_desktop_core_content_root` → 9/9 OK.
2. Full Avalonia build, pinned SDK 10.0.103 + pinned feed, rid `-r win-x64`: Build
   succeeded, 0 errors; launched binary confirmed rebuilt.
3. Regression suite `Chummer.Desktop.Runtime.Tests`: 168/172, 4 failed = exact pre-change
   baseline (verified by stash baseline: same 4 fail without these changes — 2× Origin
   wizard controls, 2× installer bootstrap payload; unrelated to content roots).
4. Native mouse-driven journey (`--mouse-first-user-journey`, isolated
   `CHUMMER_DESKTOP_STATE_ROOT`, `CHUMMER_CORE_CONTENT_ROOT` at the verified Core
   checkout): portal → file menu → `new_character` → fields typed (name
   `Content Root Probe`, alias `win002-phase1`, ruleset `sr5`, method `Priority`) →
   `create_character` delivered=True, enabled=True → **no failure dialog, no presenter
   error** (previously the refusal). Workspace persisted at
   `state-root/Chummer6/state/workspaces/8b37e3b7120c44549a4defa055301147.json`
   (2,667 bytes + lock/slot), containing `RulesetId: sr5`, `buildmethod: Priority`, the
   typed identity, and a minted `CharacterCreationBootstrapBinding` at stage
   `awaiting-foundation-selection` with
   `SettingsSourceAnchor: settings-all-sources.xml#setting:497f868a-962a-45d6-8e1f-571088607fd7`
   — the previously unavailable anchor — plus `MetatypeAuthorityDigest` and
   `PrerequisiteAuthorityDigest`.

## Remaining work (WIN-002 continuation)

1. Journey predicate: the runner waits for a legacy "new character continuation dialog";
   the working flow advances in-window (creation wizard) and the predicate times out.
   Product behavior is correct (workspace minted); runner expectation predates the
   working flow. Controller input welcome on updating the predicate vs extending the
   plan to the foundation stage.
2. Blazor parity for the same resolution; the compliance pin at
   `MigrationComplianceTests.cs:6898` pins Blazor's exact 2-arg call text, so the change
   needs an authorized pin update or a pin-compatible shape.
3. Full creation (foundation-stage steps, metatype/priority allocation) and Career routes
   — the minted binding at `awaiting-foundation-selection` is the entry point.
4. `verify_creation_projection_content.py` is POSIX-only (hardcodes `/usr/bin/bash`); a
   Windows-native verifier would help evidence generation on this host.

## Environment

- Pinned SDK/feed/cache at the WIN-001 task root
  (`C:\Users\tibor\AppData\Local\Chummer\friday\tasks\WIN-001\{sdk,NuGet.Config,nuget-cache}`).
- Core content checkout: `...\tasks\WIN-002\core-content` (HEAD
  `8706f38a7b5c29308fe007baa101038f06f61906`; `settings-all-sources.xml`, `lang/`,
  `customdata/` present), provisioned read-only from the pinned authority.
