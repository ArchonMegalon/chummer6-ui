# Windows delivery scope and Friday handoff

Updated 3 November 2026 from the user's re-audit of the corrected, merged
Quality editor. This replaces the previous work order. Read it before
[WINDOWS_REMOTE.md](WINDOWS_REMOTE.md) and [COORDINATION.md](COORDINATION.md).
Preserve existing assignments, Core authority, account/data boundaries, package
controls and release permissions.

## Resume here

Continue the existing user-started Friday Code session. Fetch main, preserve local
work, and reconcile existing branches and the Teable assignment/session/generation.
Do not start a duplicate Windows worker. Use Friday Code's existing provider and
authentication; this remote lane does not require Ubuntu Headroom. Report concrete
progress through the existing coordination client at least every 15 minutes while
active and at milestones. Keep tokens, private character data and environment
values out of reports.

**PR #517 is fixed and merged. Do not repair or resubmit the old singleton-selection
head.** Start with fresh-character Attributes entry; fix the small asynchronous
Quality-display race alongside it. Deliver verified bundled runtime content next.
Continue the remaining manual editors and recovery acceptance within the assigned
scope. Keep AI Finish default-off at every entry point. AI-populated drafts cannot
qualify a manual Creation journey.

The first Windows beta milestone remains:

> A normal user installs on a clean Windows PC, manually creates a supported SR5
> runner, enters Career, saves and reopens it in a new process, makes a Career
> change, exports a readable character sheet, and updates without losing the
> character or installing development tools.

Android, Linux, SR6 and live Rook do not block this Windows increment. A declared
Mundane-first candidate is an intermediate delivery scope, not permission to drop
magical/resonance archetypes, broader methods, Origin, genuine amend compatibility
or the full assigned Chummer5a/app parity mission. Preserve requirement identities.

## Current source and evidence

The user audited `chummer6-ui` at:

```text
Commit: b4d25bdb440f940aeaee80ccaacc8f25d843db53
Tree:   bd9abdbb4d244dac10118ae376e7b4e8b41d3b12
```

The controller confirmed this main after rebase-merging the corrected
[PR #517](https://github.com/ArchonMegalon/chummer6-ui/pull/517). All six PR checks
passed. The merged tree exactly matches the reviewed tree. This publication updates
the work order and CI checkout inputs; identify later product generations separately.

| Finding | Current disposition |
| --- | --- |
| Selecting Quality B replaces earlier A | Fixed in merged #517 |
| Quality Add/Remove/empty-set review | Implemented with focused tests |
| Quality stale-review/duplicate-confirmation protections | Improved; preserve the guards |
| Quality dialog after asynchronous loading | New stale-context display race; targeted correction required |
| Quality uncertain-result recovery | Same-session read-only reconciliation; restart/account-transition acceptance remains open |
| Fresh Attributes entry | Still circular despite the corrected Method route |
| Magic/Resonance option cards | Explicitly unavailable; misleading clicks contained, editor not implemented |
| Installed Core content | Still checkout-dependent; clean-machine delivery unresolved |
| PDF text/font serialization and positioning | Repaired; do not reopen these specific defects |
| PDF wrapping/language/native Save | Remaining acceptance and implementation work |
| Installer/update harness | Earlier source corrections retained; exact-candidate native run remains separate |
| Current-main publication | Successful preseal validation, not a new application/package-consumer build |
| Complete native Windows beta | Not established |

The controller ran the pinned-SDK Linux Avalonia build, ten Quality interaction
tests and seven headless control/gate tests for #517. The user auditor inspected
those reported results but did not rerun them. They are not native Windows
save/new-process acceptance. Preserve the repaired route edges, independent
Attributes completion flag, correct starting-cash display, diagnostic allowlist
and AI gate from the earlier work.

## 1. P1: make fresh Attributes entry possible

Affected source:

- `Chummer.Presentation/Overview/CharacterCreationWizardProjector.cs`
- `Chummer.Presentation/Overview/CharacterCreationWizardDesktopSession.cs`

The Method-to-Attributes route exists, but Attributes still uses
`isAvailable: qualitiesAuthority.IsReady`. Its forward route to Qualities also
uses Quality readiness. The pinned Core Qualities service adds
`AttributesDraftRequired` when no Attributes draft exists. Confirming the
Priority/Heritage/Talent prerequisites does not create that draft.

This requires the draft before allowing the player to create it. Drive Attributes
edit availability from its own authoritative state and admitted prerequisites,
not a downstream stage. Keep these separate: can enter/edit; has completed; can
finalize. Do not prepopulate Attributes, mark it complete, ignore Core blockers,
or relax general session entry checks merely to make navigation work. Coordinate
any missing shared Core contract with the controller; do not derive rules in UI.

Acceptance with AI Finish disabled:

1. Create a genuinely fresh Priority runner and confirm required prerequisites.
2. Assert that no Attributes draft exists and Attributes finalization is incomplete.
3. Use the ordinary Next button to enter the Attributes editor.
4. Manually allocate, preview and confirm Attributes.
5. Reload authoritative state and proceed to Qualities.

Add a focused initial-state regression and retain negative prerequisite cases.
Exercise real Windows controls. AI-filled fixtures and direct test jumps do not
establish this route.

## 2. P2: revalidate asynchronous Quality results before display

Affected method:
`Chummer.Avalonia/MainWindow.CreationWizard.cs` /
`CreationWizard_EditQualitiesAsync`.

The current method validates the captured overview, awaits
`Task.Run(interaction.Load)`, then creates and opens the dialog without checking
fresh `_adapter.State`. A user can switch from character A to B, or change account,
while loading; A's data can then appear over the current window. The busy flag
prevents duplicate Quality requests but is not a global navigation lock.

This is a source-level stale-dialog race, not demonstrated wrong-character
mutation. Confirmation has further workspace/revision/owner guards; retain them.

Immediately after loading and before constructing/showing the dialog:

- Check that the captured workspace and owner context are still current.
- Check that the displayed revision matches the loaded state.
- Check cancellation and window lifetime.
- Discard stale uncommitted display results instead of presenting them.

Apply the same rule after asynchronous review preparation, before publishing its
result. Keep progress and busy-state cleanup correct when discarding a result;
do not clear an uncertain operation merely to make the UI available. Reuse the
existing interaction/context checks rather than adding an interaction framework.

Regression coverage: pause loading, switch character, release loading, and assert
that the old character's dialog is never shown. Repeat for owner transition,
revision change and closed/cancelled window as applicable. Cover a stale prepared
review too. Keep the existing confirmation protections and focused tests.

## 3. Preserve the corrected Quality selection contract

Merged #517 initializes the editor from the entire authoritative selection,
explicitly adds/removes the clicked choice, and previews/confirms the complete
intended set. Review shows retained, added and removed qualities, the full result
and Core-calculated budgets. Empty selection can be explicitly reviewed where
Core permits it. Do not return to `[option.OptionId]` singleton writes.

The interaction binds workspace, revision and owner; copies proposed IDs; retains
one stable idempotency key/transaction identity; rejects repeat confirmation;
validates the receipt; and reconciles an uncertain write with read-only loading.
Core owns legality, exclusions and pricing. The UI collects and displays choices.

Retain existing tests for A then B preserving both, cancellation, explicit removal,
empty selection, stale character/revision/owner, duplicate confirmation, mutated
input and lost response. The reopened-editor test uses controlled retained service
state; it is not a new native process or a full persistence claim.

## 4. Qualify uncertain results across restart and account transition

Current pending state in `_pendingQualitySelections`, the captured owner stamp,
prepared review and dispatched flag is in memory. Describe the implemented path
as **same-session read-only reconciliation** until stronger evidence exists.

A new process can load saved character data, but this does not itself establish
recovery of the original uncertain operation. A pending session with an obsolete
owner transition stamp is correctly rejected; retaining that dictionary entry
does not establish that returning to the original account can recover it under a
new stamp. These are recovery limitations, not evidence of corrupted data.

Use fresh authorization to inspect the original persisted operation through the
existing Core-owned receipt/recovery contract. Do not weaken stamp equality,
reuse stale authorization, silently discard pending state or blindly resubmit.
Coordinate an actual missing shared contract; do not invent a second ledger.

Required native acceptance:

```text
Commit succeeds -> response is lost -> process terminates
-> new process opens the original character with fresh authorization
-> the saved outcome is identified without another write
```

Separately transition accounts while a result is uncertain, then return under a
fresh owner context and recover the original outcome. Verify no cross-account
display, no extra mutation and exact accepted selection/budgets. Keep the original
operation identity and distinguish saved result from unapplied proposal.

## 5. P1 delivery: package verified runtime rules/content

`Chummer.Avalonia/DesktopCoreContentRootResolver.cs` still discovers content through
an environment override, repository root or sibling Core checkout, then invokes
Git for the pinned identity. This remains a development path. An installed app
launched with `CHUMMER_CORE_CONTENT_ROOT` does not prove self-contained delivery.

Ship the exact required verified content with installed and portable bundles.
Resolve it relative to the bundle and validate the approved identity. Keep source
checkout overrides explicitly developmental. Preserve integrity checks; missing
or tampered content needs actionable recovery rather than unrelated/default rules.

On a fresh normal Windows profile with **no Git, .NET SDK, Core checkout or Chummer
content override**: install, launch from Start, manually create a supported runner,
save, exit/reboot and reopen it. Move the portable directory and repeat launch and
reopen. Distinguish packaging implementation, clean-machine evidence and signed
public delivery. Coordinate shared Core/package ownership; assigned Windows
packaging/adapters can proceed now.

## 6. Finish the advertised manual editors

Magic/Resonance now shows an unavailable message before creating option buttons.
The headless test verifies no enabled spell card. This closes misleading silent
clicks but does not implement the structured editor.

Collect applicable tradition, spell, power and resonance choices as typed inputs;
ask the existing Core service to preview and validate them. This needs a UI, not
a second rules engine. Use the ordinary sequence:

`select/edit -> Core preview -> costs/changes/blockers -> explicit confirmation -> apply once -> authoritative reload`

Every archetype advertised as supported needs a complete manual path. If the first
candidate is explicitly Mundane-only, retain the magical/resonance work as open
in the full mission. Do not unhide AI Finish to fill missing manual stages.

Qualify manual Creation -> Career -> advancement -> save/new-process reopen with
AI disabled, real Next/navigation and actual input controls. Include a real Quality
selection, and an actual Magic/Resonance choice for each applicable supported
acceptance case. Page visits and routed-click fallbacks must be labelled honestly.

## 7. Finish output and installer acceptance on the combined candidate

Keep #515's parenthesized PDF text operands, independent text objects and correct
bold-font byte serialization. The serializer is repaired. The earlier seven
compiled composer tests and independent MuPDF one-page/nine-page results retain
their original scope; the latest auditor did not rerun them.

Add width-based wrapping, page bounds and sensible breaks for real long values.
Implement intended language/character coverage; Latin-1/WinAnsi does not preserve
arbitrary names/prose. Do not silently replace unsupported content. Verify actual
extracted values and independent renders, then a real native Save PDF/open route.
Headers, HTML, deterministic bytes or renderer exit code alone are insufficient.

Preserve installer parent-exit/disposed-splash fixes and the stronger harness:
autonomous relaunch, retained shortcuts/targets, exact executable bytes/version
and process birth, positively observed checksum rejection, file-content comparison
and the isolated real saved-runner store. Run it on the actual combined candidate:

`installed v1 saves runner -> app-triggered update -> autonomous v2 relaunch -> exact runner reopens -> corrupt update rejected -> runner still reopens unchanged`

Fail autonomous-update acceptance if the harness launches the app. Prove the bad
artifact was evaluated, no installer ran for it, and installed content stayed
unchanged. Keep the operator's ordinary store outside the test. Reconcile
interruptions before retrying. Do not rerun historical workflows just to refresh
timestamps; use relevant tests for changed inputs and the selected delivery build.
Unsigned local execution is separate from signed public-channel verification.

## 8. Complete existing package qualification and report evidence accurately

[Main run 41671733822](https://github.com/ArchonMegalon/chummer6-ui/actions/runs/41671733822)
succeeded on the audited head. It ran current-main split-preseal validation without
consumer/release claims. Locked SDK setup, sealed package-consumer verification
and sealed receipt upload were skipped. This validates source publication, not a
fresh Windows application build, native execution or release readiness. Retain
#517's separately established Linux build and focused test evidence too.

Follow the existing recipe P -> marker-only Q -> lock-only seal S process. Publish
P/Q with **rebase merge**, preserve exact topology/lock checks, and complete the
existing owner-package/seal/consumer qualification for the combined candidate.
Do not force-push main, weaken checks, fabricate a seal, reuse a stale marker or
add another release framework. Controller owns protected integration. Independent
editor/content work should continue during review and package coordination.

## Remaining mission and reporting

After the manual beta works, finish retained Origin prose/manuscript export and
reopen; genuine character-bound amend import/conflict resolution, frozen-environment
export and clean-profile reopen; broader methods/archetypes; and full assigned
Chummer5a/app parity. Character JSON export is not manuscript export; `.chum5`
import alone is not amend syntax compatibility. Missing/incompatible packages need
actionable recovery. Basic retained Origin acceptance needs no new paid generation.

Complete AI transaction/recovery and useful supported-archetype behavior behind the
gate before exposure. Use Core-owned atomic or deliberately resumable completion,
stable identities, owner/revision binding and explicit uncertain results. Classify
relevant failures on the exact candidate; unchanged failures are not passes. Check
ordinary-user permissions, actual mouse/keyboard interaction, 125–200% scaling and
long/localized text. Preserve the diagnostic allowlist and sentinel tests.

Report exact source/artifact identity, behavior changed, tests actually run, native
actions, remaining gaps and next steps. Keep merged source, preseal validation,
package-consumer qualification, native verification and publication distinct.

This handoff changes no Teable assignment, provider, credentials or Friday-owned
status and does not start Friday. Continue authorized work until the full assigned
Windows mission is complete; raise only actual shared-owner/permission boundaries.
The user audit inspected source/diffs/tests/CI but did not compile or execute the
client, Windows/PowerShell, PDF renderers or an installer. No runtime race, wrong-
character write or corruption was demonstrated by that audit.

Priority: fresh Attributes entry; targeted asynchronous display guard; verified
bundled content; restart/account-transition recovery; complete manual editors;
exact-candidate package/native Creation/Career/PDF/update acceptance; remaining
Origin/amend/AI/parity work.
