# Windows delivery scope and Friday handoff

Updated 3 November 2026 from the user's latest Windows client audit, after
PRs #515 and #516 merged. This replaces the earlier work order in this file.
Read it before [WINDOWS_REMOTE.md](WINDOWS_REMOTE.md) and
[COORDINATION.md](COORDINATION.md). Preserve existing assignments, Core authority,
account/data boundaries, package controls and release permissions.

## Resume here

Continue the existing user-started Friday Code session. Fetch main, preserve local
work, and reconcile existing branches and the Teable assignment/session/generation.
Do not start a duplicate Windows worker. Use Friday Code's existing provider and
authentication; this remote lane does not require Ubuntu Headroom. Report concrete
progress through the existing coordination client at least every 15 minutes while
active and at milestones. Keep tokens, private character data and environment
values out of reports.

Start with **fresh Attributes entry**, then **repair #517's complete Quality
selection contract before merge**. Continue independent domain-editor and content
packaging work while controller reviews run. Keep AI Finish default-off and guarded
at every entry point. Do not use AI-populated drafts to qualify manual Creation.

The first Windows beta milestone remains:

> A normal user installs on a clean Windows PC, manually creates a supported SR5
> runner, enters Career, saves and reopens it in a new process, makes a Career
> change, exports a readable character sheet, and updates without losing the
> character or installing development tools.

Android, Linux, SR6 and live Rook do not block this Windows increment. The beta is
not the complete replacement: full Origin, genuine amend-package compatibility,
broader supported methods/archetypes and Chummer5a/app parity remain required.
Preserve the original requirement identities and full assigned Windows scope.

## Current source and evidence

The latest user audit inspected commit
`4bafdc282f94e887e11bb807ce27d019d07cae3b`, tree
`ccaac94654853b9190f2ae1b52d92d390f84e980`. The controller confirmed that exact
main and the still-open #517 head before preparing this handoff. Newer generations
must be identified separately.

| Finding | Current disposition |
| --- | --- |
| Red current-main publication after #512 | Resolved; current main passes preseal validation |
| PDF text operands, compounded placement and bold-font string prefix | Fixed on main by #515 and controller corrections |
| Method → Attributes and mundane Skills → Resources route edges | Fixed on main by #516; destination availability remains defective |
| Attributes completion through Quality readiness | Fixed: completion follows Core finalization; entry availability still uses Qualities |
| Starting-cash display | Fixed: display multiplies the dice sum; submitted choice remains the raw sum |
| Raw startup environment dump | Source-fixed by #509; preserve the constrained collector and sentinel tests |
| Incomplete AI Finish exposed by default | Contained by the default-off gate; underlying completion/recovery still open |
| Installer acceptance harness | Source corrections retained; combined-candidate native acceptance still open |
| Manual Creation and self-contained installation | Not complete; the work below is required |

Do not reopen the closed serialization, route-edge, cash or publication findings
as unchanged defects. The user audit traced source and repository evidence; it did
not compile/run the client, execute Windows/PowerShell, rerun MuPDF or operate an
installer. The controller's retained Linux checks and renderer results remain
separate from fresh native Windows acceptance.

## 1. P1: remove the circular Attributes-entry condition

Affected source:

- `Chummer.Presentation/Overview/CharacterCreationWizardProjector.cs`
- `Chummer.Presentation/Overview/CharacterCreationWizardDesktopSession.cs`

The Attributes stage still uses `isAvailable: qualitiesAuthority.IsReady`.
Qualities readiness requires an editable, blocker-free state. The pinned Core
Qualities service adds `AttributesDraftRequired` when no Attributes draft exists;
confirming Priority prerequisites creates their own draft, not an Attributes draft.
Thus a new runner can need an Attributes draft before the wizard lets the player
create it. The session correctly refuses an unavailable, incomplete, blocked stage;
adding the forward edge alone cannot repair this dependency.

Drive Attributes edit availability from its own authoritative state and admitted
prerequisites. Keep these distinct: can enter/edit Attributes; Attributes is
complete; the character can finalize. Do not mark Attributes complete, ignore Core
blockers or relax the session's general entry checks merely to make it reachable.
Use existing Core-backed Attributes state; coordinate any missing shared contract
with the controller instead of deriving rules in Avalonia.

Acceptance, with AI Finish disabled throughout:

1. Create a genuinely fresh Priority runner.
2. Confirm the required Priority/Heritage/Talent prerequisites.
3. Assert that no Attributes draft exists and Attributes finalization is incomplete.
4. Use the ordinary Next button to open the Attributes editor.
5. Manually allocate, preview and confirm Attributes.
6. Reload authoritative state and verify that Qualities now becomes available.

Add a focused regression for this initial state and retain negative prerequisite
cases. Test real controls on Windows. An AI-filled fixture or a direct test jump
into Attributes cannot establish this route.

## 2. P1: repair #517's Quality selection contract before merge

[PR #517](https://github.com/ArchonMegalon/chummer6-ui/pull/517) remains unmerged at
`f5fe32d66152ad0e09943a8b23f67edd62cc8196`. **Do not merge that head unchanged.**
Rebase the corrected work onto current main, retaining the merged PDF, route,
privacy, installer and AI-gate corrections.

The proposed window handler calls both Preview and Confirm with
`[option.OptionId]`. Core evaluates `SelectedOptionIds` as the complete proposed
set and replaces the pending Creation Quality draft; it does not implicitly add
to existing selections. Clicking B after A can therefore replace `[A]` with `[B]`.
This finding concerns the pending Creation draft, not deletion of all saved or
finalized Career qualities.

Initialize an explicit proposed selection set from the current authoritative draft.
Add changes that set; Remove must be explicit. Send the entire intended set to
Core for both preview and confirmation. Provide a deliberate way to preview and
confirm an empty selection when Core permits it. Item-click-only interaction is
not a complete multi-selection editor.

Show the authoritative before/after selection, retained/added/removed qualities,
cost changes, resulting budgets and blockers. The clicked card's label and price
alone do not explain replacement of another selection. Core owns legality,
exclusions, pricing and final acceptance; the UI collects choices and presents
Core's result.

Minimum focused behavioral coverage:

| Action | Required result |
| --- | --- |
| Select A, then add independently legal B | A and B both remain selected |
| Cancel B's review | A and the stored draft remain unchanged |
| Explicitly remove A | Removal is shown before confirmation; only the intended set is accepted |
| Confirm a permitted empty set | Empty intent is explicit, reviewed and retained |
| Character/owner/revision changes during review | Stale review cannot mutate |
| Duplicate click while preparing or confirming | One owned operation; no duplicate dispatch |
| Save, exit and reopen in a new process | Exact accepted selection is retained |

The draft's `_qualitiesMutationBusy` is not an effective guard while it is only
written. Use the existing per-workspace interaction guard and disable conflicting
controls while busy. Do not discard prior selections during reload or recovery.

## 3. P1: finish domain-specific editors and visible recovery

Inspect:

- `Chummer.Avalonia/Controls/CharacterCreationWizardControl.axaml.cs`
- `Chummer.Avalonia/MainWindow.CreationWizard.cs`
- `Chummer.Presentation/Overview/CharacterCreationWizardProjector.cs`

Current main special-cases Resources and routes the other option cards to the
Foundation handler, which recognizes metatype/nationality but not the advertised
Quality or Magic options. #517 distinguishes the domains but its Magic/Resonance
branch still reports an unavailable structured editor. That is containment, not
completion. Present unavailable editing before offering an enabled-looking card.
Then implement the typed input surface required by the supported manual journey.

A structured Magic editor does not require a second rules engine. Collect the
applicable tradition, spell, power or other typed choices and pass them to the
existing Core service. Use this sequence for each supported domain:

`select/edit → prepare domain preview → show changes/costs/blockers → explicit confirmation → apply once → reload authoritative state`

The Quality handler must not catch `IOException`/`InvalidOperationException` and
silently return. Distinguish preview failure before mutation (visible recoverable
error, draft unchanged) from uncertain confirmation (retain the stable operation
identity and reconcile its outcome). Reuse existing owner-bound interactions and
receipt recovery. Do not blindly resubmit, generate a new operation identity for
an uncertain write, expose raw exceptions or build another recovery framework.

Complete a manual SR5 Creation → Career → advancement → save/new-process reopen
journey with AI disabled, ordinary Next/navigation and actual input controls.
Include a real Quality selection and, for an applicable archetype, an actual
Magic/Resonance choice. Verify budgets and blockers. Page visits, routed-event
fallbacks or a walkthrough after AI fills drafts must be labelled accurately;
they cannot stand in for manual editor acceptance.

## 4. P1 delivery: bundle verified runtime content

`DesktopCoreContentRootResolver.cs` still discovers content through an environment
override, repository root or sibling Core checkout and invokes Git to check its
pinned identity. Installer success with `CHUMMER_CORE_CONTENT_ROOT` does not prove
self-contained delivery.

Ship the required verified rules/content in the installed and portable bundles.
Resolve it relative to the bundle and validate its expected identity. Keep checkout
overrides explicitly developmental. Do not remove integrity checks or silently
fall back to unrelated/default rules. Missing or tampered content needs an
actionable recovery state.

On a fresh normal Windows profile with **no Git, SDK, source repositories or
Chummer environment overrides**, install, launch from Start, manually create a
supported runner, save, exit/reboot and reopen it. Move a portable directory to a
different path and repeat. Separate the implementation, clean-machine native
proof and signed public delivery claims. Shared Core/package changes still need
controller coordination; assigned Windows packaging/adapters can proceed now.

## 5. PDF: finish layout, language coverage and native Save acceptance

Keep #515's parenthesized text operands, independent text objects and correct
byte serialization of the bold-font dictionary. The retained controller record
has seven compiled composer tests plus independent MuPDF rendering/extraction of
one-page and nine-page samples. The latest auditor did not rerun those checks.
These close the specific serialization defects, not the entire export feature.

`DrawRuns()` still does not wrap to the available width. Add width-based wrapping,
page bounds and sensible page breaks using actual long character values. Define
and implement intended language/character coverage; the current Latin-1/WinAnsi
path is insufficient for arbitrary Unicode names and prose. Unsupported user
content must not be silently substituted. A bounded readable sheet can establish
the first beta milestone; full book-quality formatting remains separate.

Generate the compiled production output, inspect actual extracted values and
independent renders, and save/open a real PDF through the native Windows Save
route on the identified candidate. Cover labels, values, parentheses/backslashes,
long fields, multiple pages and supported multilingual names. Header/EOF markers,
HTML export, deterministic bytes or a renderer's zero exit alone do not establish
readability.

## 6. Qualify the combined candidate using the existing publication process

[Main run 41630899503](https://github.com/ArchonMegalon/chummer6-ui/actions/runs/41630899503)
passes on the audited main. It executes “Validate current main split-preseal
without consumer or release claims”; locked SDK setup and sealed package-consumer
verification are skipped. Close the old red-publication finding, while keeping
fresh application/package/native qualification open.

Follow README's existing recipe P → marker-only Q → lock-only seal S process.
Publish P/Q through **rebase merge**, preserve the verifier's exact topology and
lock checks, and complete the existing owner-package/seal/consumer qualification
for the intended combined candidate. Do not force-push main, weaken checks,
fabricate receipts, reuse an old marker for changed bytes or invent a new release
framework. The controller owns protected integration. Green preseal alone is not
a Windows build, install, native journey or release. Independent editor/content
work should continue while review and package coordination proceed.

## 7. Execute the corrected installer journey on the identified candidate

Preserve the existing parent-exit/disposed-splash fixes and #512's stronger harness:
autonomous relaunch, retained shortcuts and targets, exact executable/version and
process-birth checks, positive checksum rejection, file-content comparison and the
isolated real saved-runner store. They are not undone merely because main advanced.
No new combined-candidate native result was established by this audit.

Run:

`installed v1 saves a runner → app-triggered update → installer autonomously relaunches v2 → v2 reopens that exact runner → corrupt update is rejected → runner reopens unchanged`

Keep shortcuts in place. Fail autonomous-update acceptance if the harness launches
the app. Prove the bad artifact was evaluated and rejected for checksum mismatch,
no installer was invoked for it, and installed identity/content stayed unchanged.
Bind the actual reopened workspace/profile/stored file and executable precisely.
Keep the operator's ordinary store outside the test and reconcile interruptions
before retrying. An unsigned local run is not signed public-channel delivery;
this update test is also separate from clean-machine content acceptance.

## Remaining scope and reporting

After the manual beta works, finish retained Origin prose/manuscript export and
reopen, genuine character-bound amend import/conflict resolution/frozen-environment
export/clean-profile reopen, broader methods/archetypes, and the full assigned
Chummer5a/app parity checklist. JSON character export is not manuscript export;
legacy `.chum5` import alone is not amend-syntax compatibility. Missing/incompatible
packages must produce actionable recovery. Basic retained Origin acceptance does
not require a new paid generation.

Finish AI transaction/recovery and useful supported-archetype behavior behind the
gate before exposing it. Use Core's atomic or deliberately resumable completion
contract, stable identities, owner/revision binding and explicit uncertain results.
Classify relevant failed tests on the exact candidate; unchanged failures are not
passes. Verify ordinary-user permissions, real mouse/keyboard use, 125–200% scaling
and long/localized text. Preserve #509's diagnostic allowlist and sentinel tests;
never restore raw environment dumping or copy suspected secrets into reports.

Report exact source/artifact identities, behavior changed, focused tests actually
run, native actions, remaining gaps and next steps. Keep source merged, preseal
validated, package consumer qualified, native verified and published distinct.
This handoff changes no Teable assignment, provider, credentials or Friday-owned
status and does not start Friday. Continue independent authorized work until the
full assigned Windows product is complete; request precise coordination only for
an actual shared-owner or permission boundary.

Priority: fresh Attributes entry; safe full-set Quality editing; remaining manual
domain editors/recovery; bundled content; PDF layout/languages/native Save; exact
package/native Creation/Career/update acceptance; remaining Origin/amends/AI/parity.
