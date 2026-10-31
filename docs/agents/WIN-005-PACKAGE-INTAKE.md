# WIN-005 — Windows package-intake failures and WIN-001 handoff correction

Status: **complete-for-review (draft PR, no acceptance or merge)**

## Task

Resolve the six failing package-intake subtests, determine whether the fault is in
the validator or the test fixtures, preserve every archive-safety negative check,
verify intake of the fourteen pinned packages, and correct the stale WIN-001
evidence/PR text.

## Root cause determination: fixture-construction fault, not validator

Evidence chain:

1. **Direct module probe (same import path as the tests).** Loading
   `scripts/fetch-windows-package-feed.py` in isolation and calling
   `checked_zip` with hostile archives produced `IntakeError` for the reserved
   device name (`NUL.txt`), trailing-space name (`bad. `), case-collision
   (`duplicate`/`DUPLICATE`), symlink member (`S_IFLNK` ZipInfo), and
   file/parent collision (`parent` + `parent/child`). The validator is sound.
2. **Subtest 4 (`nested\escape`) cannot construct its fixture on Windows.**
   `zipfile.ZipInfo.__init__` normalizes the member name: the stored name is
   `nested/escape`, a benign flat member. Intake correctly accepts it, so
   `IntakeError` is never raised. This is a test-fidelity bug in fixture
   construction, not a safety hole.
3. **Subtests 5–9 cascade.** Because subtest 4's intake succeeded, it
   materialized a complete feed in the shared output directory. Every later
   subtest then hits `materialize`'s existing-output shortcut
   (`test_existing_incomplete_or_extra_output_is_never_repaired` shows intake
   returns without scanning when output exists), so their hostile bundles are
   never scanned at all. Non-isolated subtest state, not a product defect.

## Fix (test fidelity only — no production code change)

- `tests/test_windows_package_feed.py`:
  - `zip_bytes` now raises on any name that Python's zipfile would silently
    normalize or mangle (backslash normalization, trailing slash confusion),
    so hostile fixtures can never silently become benign.
  - New `unsafe_member` helper builds raw `ZipInfo` entries (bypassing name
    normalization) with explicit DOS/Unix flag attributes, then validates each
    stored name round-trips byte-identically; raises if not.
  - `run_intake` takes `isolate=True` by default: each subtest runs against a
    pristine cache/output/state, so no subtest can inherit a completed feed
    from a previous one.
  - The backslash fixture is now a real raw `ZipInfo("nested\\escape")` that
    stores byte-identically; subtests are isolated; all nine cases are asserted
    individually.

Validator semantics preserved: `safe_name`, `checked_zip`, `verify_package`
untouched; no check weakened.

## Verification

- `python -m unittest discover -s tests -p test_windows_package_feed.py -v`
  → all tests pass, including all nine unsafe-member subtests, the nested
  nuspec traversal case, and the 14-package intake verification.
- `python -m unittest discover -s tests -p test_prepare_agent_workspace.py -v`
  → 25/25 pass (unchanged).
- No changes to package IDs, versions, URLs, or SHA-256 pins; no changes to
  `scripts/fetch-windows-package-feed.py`; no shared Runtime/Presentation/Core
  changes; no broad app rebuild required (no production code impact).

## WIN-001 evidence correction (required by task text)

The WIN-001 evidence file overstated negative results. Corrections:

- The claim that no import/save success existed is stale: WIN-001's
  `docs/agents/WIN-001-WINDOWS-EVIDENCE.md` already recorded the native
  import/save/fresh-process reopen verification (two independent runs, matching
  full-profile SHA-256 and persisted-file SHA-256, negative verification passed)
  alongside the separate New Runner blocker. The WIN-005 draft PR text and the
  earlier WIN-005 branch evidence were the stale artifacts; PR #487's body was
  corrected by a separate commit on its branch per task authorization
  (`docs/agents/WIN-001-WINDOWS-EVIDENCE.md` is in WIN-005's allowed paths, and
  the WIN-001 branch tip `8366a0c` already carries the correct final text).
- The six package-intake failures are test-side, per the root cause above;
  WIN-001's evidence line describing them as "existing failing subcases
  documented" stands, now with this resolution attached.

No acceptance, merge, or release was performed. WIN-001 remains
complete-for-review pending controller acceptance.
