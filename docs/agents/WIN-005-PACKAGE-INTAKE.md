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
  - `zip_bytes` verifies every stored member name after writing and raises on
    any normalization, so zipfile can never silently defang a hostile fixture.
  - The backslash fixture is built as a raw `ZipInfo` whose `.filename` is
    assigned directly: the constructor normalizes separators, but direct
    assignment stores the hostile name byte-verbatim, read-back preserves it
    in `orig_filename` (the field `checked_zip` validates), and `safe_name`
    rejects it. Verified with an isolated round-trip probe before the fix.
  - Each subtest now starts by removing the output feed and asserting it is
    absent, so no subtest can inherit a completed feed produced by an earlier
    subtest's successful intake. All nine unsafe-member cases (three traversal,
    six other hostiles) are asserted individually against a pristine output.

Validator semantics preserved: `safe_name`, `checked_zip`, `verify_package`
untouched; no check weakened.

## Verification

- `python -m unittest discover -s tests -p test_windows_package_feed.py`
  → 18/18 pass (exit 0), including the previously failing six subtests:
- `python -m unittest discover -s tests -p test_prepare_agent_workspace.py -v`
  → 25/25 pass (unchanged).
- No changes to package IDs, versions, URLs, or SHA-256 pins; no changes to
  `scripts/fetch-windows-package-feed.py`; no shared Runtime/Presentation/Core
  changes; no broad app rebuild required (no production code impact).

## WIN-001 evidence correction (required by task text)

The WIN-001 evidence file overstated negative results. Corrections:

- The WIN-001 evidence file already recorded the native import/save/reopen
  verification alongside the separate New Runner blocker; the stale artifact
  is PR #487's description text, which WIN-005's task authorizes correcting
  via a direct edit of the existing draft PR on GitHub (no force-push, no
  history change).
- The six package-intake failures are test-side, per the root cause above;
  WIN-001's evidence line describing them as "existing failing subcases
  documented" stands, now with this resolution attached.

No acceptance, merge, or release was performed. WIN-001 remains
complete-for-review pending controller acceptance.
