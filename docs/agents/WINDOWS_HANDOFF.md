# Friday Code: Windows completion handoff

Updated 2 November 2026 by the controller from the user's Windows audit.
This is the current Windows work order. Read it before
[WINDOWS_REMOTE.md](WINDOWS_REMOTE.md),
[WINDOWS_SCOPE.md](WINDOWS_SCOPE.md) and
[COORDINATION.md](COORDINATION.md). It supersedes older Windows priority and
completion claims where they conflict, while retaining account, data, package,
ownership and release boundaries.

## Mission and first milestone

Continue working through implementation, focused verification and reviewable
increments; do not stop after a package seal, audit, source patch or happy path.

The first credible Windows beta milestone is:

> A normal user can install Chummer on a clean Windows PC, manually create a
> supported SR5 runner, save and reopen it, make a Career change, produce a
> readable character sheet, and update the application without losing data or
> installing development tools.

**Do not wait for Android, Linux, SR6 or live Rook to complete this milestone.**
Windows uses its own host and build capacity. Full Origin, genuine amend
compatibility, the remaining supported methods/archetypes and broader
Chummer5a/app feature parity remain required before a replacement/gold claim.
The beta milestone does not reduce that full scope or close the original
62 requirement identities. Keep SR6 as a distinct later delivery stage.

## Resume from real state

1. Preserve current edits, commits, installed test data and uncertain operations.
   Inspect the current branch and working tree before fetching; never reset or
   clean a dirty checkout to match a handoff.
2. Fetch `origin/main` and inspect changes since the audited commit below.
   Start or update an isolated `windows/...` task branch from the current
   integration base after reconciling existing work. Do not resume blindly from
   the old remote-coordination branch or merge an older AI draft wholesale.
3. Read the current Teable assignment, session and generation using the existing
   coordination client. Retain that identity and report actual progress at least
   every 15 minutes while active and after each milestone. This document updates
   work priority and acceptance; it does not claim that any Teable row was edited.
   If a stale assignment excludes the current base or required shared paths,
   request the precise controller update and continue independent assigned work.
4. Use Friday Code's existing provider and authentication on Windows. No
   Headroom or Ubuntu connection is required for this remote lane. Keep one
   active session for the assigned identity. Use the existing GitHub CLI login
   and OS credential store; never put Teable/GitHub tokens in source or reports.
5. Keep rules and mutation authority in Core. UI/desktop adapters, packaging and
   tests stay in this repository. Shared Core/API/new package changes require
   controller coordination; already assigned desktop/Presentation work does not
   need repeated approval. Preserve the normal package graph and required merge
   checks. Push assigned branches and request review; protected integration and
   signing/publication remain controller-owned.

## Evidence baseline and limits

The user audited `chummer6-ui` at commit
`0d224e7a796b1058d68ce73a384eccf5ea861dce`, tree
`01ec53cc46b4ef890d7af87d31d175d802094f53`.
WIN-002 Career integration and its package seal are included. The user reports
that exact-main package-consumer verification finished successfully during the
audit. That proves its package/build boundary, not complete Windows workflows.

Retain the useful existing evidence: native import/save/new-process reopen with
matching file/profile hashes; a bounded AI-assisted Priority completion; Career
attribute/reward controls including the observed Karma 11 to 16 award; and source
fixes for parent-process installer handoff and disposed-splash relaunch.
Revalidate affected behavior on the eventual combined candidate rather than
claiming those historical results tested new bytes.

The audit inspected source, integrations, CI and retained native evidence.
Its PDF reproduction used synthetic output matching the inspected operators,
independently checked by MuPDF and Poppler. It did **not** compile this C# composer,
run Windows, inspect private installer binaries or test a signed public download.
No customer corruption or ownership exploit was reproduced. Treat the reported
mutation/recovery weaknesses as source findings requiring targeted negative tests.

The user supplied the reproduction filename
`chummer_windows_pdf_repro_2026-11-02.zip`. It is not a repository dependency and
is not asserted to have been uploaded here. If unavailable on Windows, reproduce
the failure directly from compiled composer output; do not block work on the zip.

## Ordered work and acceptance

### 1. Repair real PDF output — P1

Inspect `Chummer.Presentation/Printing/WorkspacePrintPdfComposer.cs`,
particularly `AppendTextOp`. The audited source sends escaped text to `Tj`
without PDF string delimiters and uses absolute-looking coordinates with the
relative `Td` operator. Fix both. Adding parentheses alone leaves later text
displaced; use correct text serialization and deliberate absolute/relative
positioning throughout.

Choose an explicit Unicode strategy appropriate to the supported character
sets, including font embedding/mapping where required. Preserve multilingual
names and dossier prose; no silent replacement of unsupported characters.
Account for glyph metrics in wrapping and page layout.

Generate output from the **compiled production composer**, parse/extract and
render it independently with MuPDF/Poppler or equivalent tools. Verify actual
title, labels and field values, escaped parentheses/backslashes, multilingual
names, wrapping, page bounds and page breaks. Count parsed pages rather than
substring matches: `/Type /Page` also matches `/Type /Pages`.
Header, EOF, deterministic bytes and object-name checks alone are insufficient.

Then export through the native Save dialog and open that exact PDF independently.
An HTML print file is not PDF acceptance.

### 2. Gate the incomplete Finish with AI action — P1

For the next ordinary Windows candidate, hide or explicitly feature-gate the
new AI Finish request action. An open Creation wizard plus enabled AI preferences
is not sufficient qualification. Preserve manual creation, existing saved
characters and safe recovery of existing requests.

The implementation is already in audited main through later integration:
draft PR status does not establish absence. Locate every menu, command,
shortcut and alternate entry point and verify ordinary users cannot start the
unqualified flow. Do not silently expose deterministic recommendations under a
claim of complete AI finishing. This gate can ship as a small independent fix.

### 3. Ship self-contained runtime content — P1

Inspect `Chummer.Avalonia/DesktopCoreContentRootResolver.cs` and the actual
publish/installer recipes. Package the exact required Core rules/content with
the application and verify its integrity/version against the existing package
authority. Resolve installed content relative to the installation or portable
bundle. Keep checkout overrides explicitly developmental.

A released application must not require `git rev-parse`, a sibling
`chummer6-core` checkout, a .NET SDK or `CHUMMER_CORE_CONTENT_ROOT`.
The historical missing-`lifemodules.xml` workaround is not clean-install proof.
Do not hardcode machine paths or weaken source/content verification.

On a fresh normal Windows user profile with no Git, SDK, repositories or Chummer
environment variables: install, launch, manually create a supported runner, save,
close/reboot and reopen the same runner. Test a portable build after moving its
directory. Missing or tampered packaged content must give actionable recovery,
not silently load unrelated rules. Keep build-time tooling separate from
installed runtime prerequisites.

### 4. Close manual Creation and Career correctness — P1/P2

Drive each wizard stage's completion and budget display from that domain's own
Core result. In `CharacterCreationWizardProjector`, Attributes must not be marked
complete using `qualitiesAuthority.IsReady`. Add a negative case where Qualities
is editable/ready but Attributes is incomplete. This is a projection defect;
do not claim Core finalization was shown to accept an illegal character.

Correct starting-cash display: a dice sum of 14 with a multiplier of 100 displays
1,400 nuyen, while `CharacterCreationStartingCashChoice` still submits 14 as the
dice result for Core to calculate. Check localized formatting and avoid applying
the multiplier twice.

Complete one native SR5 manual Creation -> finalization -> Career -> advancement
-> save -> process exit -> new-process reopen journey. Do not use AI to fill
blocked stages in this manual acceptance case. Preserve explicit player choices,
check the actual ledger and resulting values, and finalize/apply once.
Then close other supported methods, archetypes and Career variants with bounded
cases. Clearly distinguish unsupported scope, missing implementation and
unverified behavior.

### 5. Finish the completion transaction and recovery — P1

The audited `CreationAiFinishService` proposes deterministic missing-only packs
and writes Attributes -> Skills -> Qualities -> Resources -> Gear separately.
It checks the initial digest before mutation, generates fresh `Guid.NewGuid()`
keys, does not roll back earlier domains, and does not consistently stop all
later writes after a failure. Correct reports claiming deterministic keys or
universal fail-fast behavior.

Use Core's atomic completion path, or a deliberately resumable staged operation
with stable identities persisted before dispatch, original plan/owner/revision/
rules binding, explicit partial results and recovery of uncertain writes.
Do not create a second rules engine or treat retrying a new key as recovery.

Verify at least: later-domain failure after an earlier commit; process death
between stages; committed write with lost response; changed character during
preparation; changed owner/account; stale review; explicit cancellation; and
retry/reopen of the original uncertain operation. Prove no duplicate spend,
duplicate application, silent partial success or cross-owner result adoption.
Do not invent compensating writes without authoritative support.

Retain the prior successful Priority case, but keep the new action gated until
complete legal behavior for all offered archetypes plus native review/apply/
save/reopen/process-restart/recovery is verified against the agreed AI design.
Coordinate with the controller's AI implementation rather than forking it.

### 6. Execute actual output and portable-rule workflows

Correct the older WIN-003 report by preserving its historical observations and
adding explicit scope corrections:

| Old PASS label | What it actually established | Still required |
| --- | --- | --- |
| Shareable PDF | `Soma-print.html` | Real native PDF, independent rendering and values/layout |
| Origin reading/export | Character JSON and dialog-source tests | Complete retained prose, intended book export, same accepted content after restart |
| Portable amend workflows | Rules Setup open/close and legacy character import | A real character-bound amend/package workflow |
| Missing-package/conflict recovery | Output-conflict contracts and build-package intake | Actual rule-environment conflict and missing/incompatible package recovery |

Use retained Origin content; no new paid generation is needed for basic reading/
export/reopen acceptance. A character JSON export is not manuscript export.

Import a genuine character-bound rule package; deliberately introduce a conflict;
resolve it explicitly; save the frozen environment; export it; reopen in a clean
profile. Test missing/incompatible packages with actionable recovery and no
silent default-rule fallback. Importing `.chum5` alone does not prove Chummer5a
`custom_`/`override_`/`amend_` semantics, order or compatibility.
Preserve original package bytes and edition isolation.

### 7. Verify the corrected installer/update candidate

The source fixes for the disposed splash and parent process exit already exist.
Do not report those as still unfixed without new evidence. The old WIN-004
9/9 run used `relaunchMode=harness-fallback`,
`installerAutoRelaunchDefectDetected=true`, unsigned local artifacts and a
developer-content environment override. Narrow that claim accordingly.

On the exact combined candidate: clean install -> Start-menu launch -> save a
character -> application-triggered update -> old process exits -> installer
completes -> updated app relaunches **itself** -> same character reopens unchanged.
Fail the automatic-update leg if the harness launches the app.

Retain checksum corruption, interrupted update, rollback/recovery and stored-data
compatibility checks. Record unsigned local, signed artifact and published-channel
verification separately. Never relabel a developer exercise as signed delivery.

### 8. Close relevant test debt and ordinary Windows usability

Classify each relevant remaining failure as product defect, stale test,
unsupported scope or environment problem. The audit's historical Career,
CreationWizard and WSL-root permission failures are not fresh current-main counts.
Run affected suites on the exact merged candidate and test filesystem permissions
as a normal Windows user.

Exercise real mouse and keyboard interactions. A routed Click fallback proves
handler behavior but does not pass an ordinary pointer interaction that failed.
Verify keyboard navigation, focus, cancellation, 125–200% scaling, long/localized
text and consistent dense desktop layouts. Preserve data through these routes.

## Delivery and reporting

Ship small usable increments once their focused checks pass. Do not build a new
receipt/test/release framework or rerun unrelated suites to refresh timestamps.
Use existing scripts, package controls, installer and native journey harnesses;
fix a harness only where a concrete flaw prevents observing the real workflow.

For each task report the exact source commit, actual Windows environment, commands
executed, focused results, artifact identity/location and remaining defects.
Separate source-fixed, compiled, managed-tested, native-tested, installed-tested,
signed and published states. Correct overbroad historical summaries without
deleting their original evidence. Never substitute a package seal for product
acceptance or declare all Windows complete from the beta journey alone.

Keep useful independent work moving while a shared change awaits review.
Submit reviewable commits/PRs and retain pending operation state. Continue until
the first beta milestone and then the full supported Windows parity scope are
actually closed; stop only for a real external blocker, required authority or a
user instruction.
