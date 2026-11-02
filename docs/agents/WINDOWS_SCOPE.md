# Windows delivery scope and Friday handoff

Updated 2 November 2026 by the controller from the user's **Windows client
re-audit** of `a3cdfe9b94f3edc4d0af37bde4fe0c8e9334a80e`.

This is the current Friday work order. Read it before
[WINDOWS_REMOTE.md](WINDOWS_REMOTE.md) and [COORDINATION.md](COORDINATION.md).
It supersedes the earlier audit's priorities and any conflicting completion
claims. Existing account, data, package, ownership and release boundaries remain.

## Mission and first milestone

Continue implementation, focused verification and reviewable increments until
the assigned Windows work is complete. Do not stop at a package seal, source
patch, audit report or assisted happy path.

The first credible Windows beta milestone is:

> A normal user can install Chummer on a clean Windows PC, manually create a
> supported SR5 runner, enter Career, save and reopen in a new process, make a
> Career change, produce a readable PDF character sheet, and update the
> application autonomously with character data preserved and no development tools.

Do not wait for Android, Linux, SR6 or live Rook to complete this milestone.
Windows uses its own host and build capacity. Full Origin, genuine amend
compatibility, the remaining supported methods and archetypes, and broader
Chummer5a/app feature parity remain required before a replacement/gold claim.
This beta milestone does not reduce the full scope or close the original 62
requirement identities. Keep SR6 a distinct later delivery stage.

## Resume from real state

1. Preserve current edits, commits, installed test data and uncertain operations.
   Inspect the branch and working tree before fetching; never reset or clean a
   dirty checkout to match this handoff.
2. Fetch `origin/main` and inspect changes since the audited commit. Start or
   update an isolated `windows/...` task branch from the current integration base
   after reconciling existing work. Do not resume blindly from the old remote
   coordination branch or merge an older AI draft wholesale.
3. Read the current Teable assignment, session and generation through the existing
   coordination client. Retain identity and report actual progress at least every
   15 minutes while active and after each milestone. This document changes the
   work order; it does not claim that Teable assignments were edited. If a stale
   assignment excludes the current base or necessary shared paths, request the
   precise controller update and continue independent assigned work.
4. Use Friday Code's existing provider and authentication on Windows. No Headroom
   or Ubuntu connection is required for this remote lane. Keep one active session
   for the assigned identity. Use the existing GitHub CLI login and OS credential
   store; never put Teable/GitHub tokens in source, diagnostics or reports.
5. Keep rules and mutation authority in Core. Desktop/UI adapters, packaging and
   tests belong here. Shared Core/API/new package changes require controller
   coordination; already assigned desktop/Presentation work does not need
   repeated approval. Preserve the normal package graph and required merge
   checks. Push assigned branches and request review; protected integration and
   signing/publication remain controller-owned.

## Re-audit baseline and evidence limits

The user inspected `chummer6-ui` at
`a3cdfe9b94f3edc4d0af37bde4fe0c8e9334a80e`, newer than the earlier audit's
`0d224e7a796b1058d68ce73a384eccf5ea861dce`
(tree `01ec53cc46b4ef890d7af87d31d175d802094f53`). The intervening changes
primarily published the work order, gated AI Finish, added gate tests and
clarified installer-test commentary. They do not establish broad product repair.

| Area | Current disposition |
| --- | --- |
| Incomplete AI Finish exposed to ordinary users | Closed in source by the default-off gate and guarded entry points |
| AI completion transaction/recovery | Open, contained behind that gate |
| Startup diagnostic confidentiality | New immediate P1: full child environment written on startup failure |
| PDF text serialization/placement | Open; composer unchanged and synthetic reproduction fails again |
| Self-contained installed content | Open; resolver and installer override remain checkout-based |
| Manual Creation | Open; concrete Method and Mundane forward-route gaps identified |
| Starting-cash display | Open; displays the dice sum rather than sum times multiplier |
| Installer implementation | Earlier parent-exit/disposed-splash source fixes remain present |
| Autonomous-update acceptance | Open; harness can still admit misleading passes |
| Origin manuscript and amend portability | Not closed by this update |
| Current green CI | Split-preseal validation, not a fresh build or sealed package-consumer result |

Retain useful historical evidence without presenting it as a test of changed
bytes: native import/save/new-process reopen with matching file/profile hashes;
bounded AI-assisted Priority completion; Career controls including the observed
Karma 11 to 16 award; and the source fixes for installer parent exit and
disposed-splash reuse. A newer worker report claims an `installer-auto` run on
the earlier main; attribute that claim to its exact candidate and report.

The re-auditor inspected pinned source, changes, tests, installer harness and CI,
and reran and visually checked a synthetic PDF operator reproduction with
MuPDF/Poppler. They did **not** execute Windows, the PowerShell journey, the C#
application, a signed installer or private provider operations. They found a
source-confirmed diagnostic disclosure path, not an actual leaked credential;
no customer corruption or ownership exploit was demonstrated.

The user referenced `chummer_windows_reaudit_2026-11-02.zip`. This handoff does
not assert that the packet was uploaded to this repository or is present on
Friday's host. Do not publish a sandbox-only download link. If unavailable,
reproduce the defects using actual compiled output; do not block on the zip.

## Ordered work and acceptance

### 1. Remove the full diagnostic environment dump — immediate P1

In `scripts/win004-installer-update-recovery-journey.ps1`, native-start failure
handling currently enumerates `$psi.EnvironmentVariables` and writes complete
name/value pairs to `start-failure-env.txt`. This can place credentials in
plaintext diagnostic material later shared as evidence.

Replace it with a small explicit allowlist. Retain safe values only where needed;
for sensitive configuration record presence/absence, not values. Suitable
diagnostics include whether a Core-content override or SDK override is configured
and the safe update mode. Omit credential variables. Audit related error/report
paths so the replacement does not leak the same values indirectly.

Add a focused test supplying a harmless sentinel credential and exercise the
startup-failure path. Assert that the sentinel never appears in **any generated
diagnostic artifact**, not only the replacement environment report.

Privately inspect existing `start-failure-env.txt` files in owned evidence and
remove them from shareable evidence. Do not copy suspected secrets into commits,
issues, Teable or remediation reports. Report only affected artifact identity
and exposure disposition. Coordinate credential rotation with the account owner
only where actual exposure is established; this finding does not authorize
unrelated account/security changes or a speculative wholesale rotation.

### 2. Repair real PDF output — P1

`Chummer.Presentation/Printing/WorkspacePrintPdfComposer.cs` remains at Git blob
`d3f9f9b7ff4a02cb1b7adbb7006cadc6df88be67` in the audited head.
`AppendTextOp` has two independent defects: escaped text is passed to `Tj`
without PDF string delimiters, and absolute-looking coordinates are repeatedly
passed to relative `Td` positioning.

Fix serialization and positioning together. The synthetic reproduction yields
syntax errors, no sample text and a blank page with the current construction.
Adding only delimiters leaves values displaced; adding correct absolute
placement also fixes the demonstrated field layout. **Poppler returned exit code
zero for the malformed sample**, so exit-code-only checks are insufficient.

Generate PDFs from the **compiled production composer**, independently extract
and render them, and assert actual title, labels and character field values.
Check parentheses/backslashes, long values, wrapping, page bounds and page breaks.
Use a real parsed page count: `/Type /Page` substring counts also match
`/Type /Pages`. Define and verify a Unicode/font embedding or mapping strategy
that preserves multilingual names and dossier text; silently substituting
unsupported characters is unacceptable. Account for glyph metrics in layout.

Exercise the native Save PDF dialog, open the exact saved file independently and
retain readable output evidence. An HTML export, PDF header/EOF, deterministic
bytes or object-name assertions do not close this task.

### 3. Complete the ordinary manual Creation route and cash display — P1

Inspect `Chummer.Presentation/Overview/CharacterCreationWizardProjector.cs` and
`CharacterCreationWizardDesktopSession.cs`. The re-audit identified two existing
route gaps, not regressions caused by the new AI gate:

* Method currently has a successor only for an authoritative Life Modules method
  (`methodAuthoritative && usesLifeModules ? [Foundation] : []`). Authoritative
  non-Life-Modules methods therefore have no ordinary Next target.
  `CanContinue`/`TryContinue()` consume the same successor list; finalization
  status projection does not repair it.
* Mundane Skills proceeds directly to Contacts/lifestyles while Resources is
  still required. Mundane characters should bypass Magic, not Resources.

Derive method-specific forward routes from actual Core prerequisite state.
After prerequisite confirmation, supported Priority creation must reach the
appropriate admitted stage. Verify each required applicable stage is reachable
through ordinary Next and that Back works consistently with that route.

Qualify the earlier Attributes finding correctly: an applicable Core finalization
step **overwrites** the initial completion flag. `qualitiesAuthority.IsReady` is
a questionable fallback dependency, not proof that every Attributes display is
wrong. Use domain-appropriate Core readiness/budgets and add a negative test for
the fallback case where Qualities is ready but Attributes is incomplete. Do not
infer that Core accepts illegal characters from a UI projection defect.

Fix starting-cash display to show dice sum times multiplier: a result of 14 with
multiplier 100 displays 1,400¥. Continue submitting the dice result **14** to Core;
the existing choice payload is correct and must not receive a second multiplier.

Run a native supported SR5 manual journey using the ordinary **Next buttons**:

> Prerequisites → Attributes → Qualities → Skills → Resources → all remaining
> applicable steps → Review → Career.

Follow the approved design's precise order and prerequisites. No direct test
jumps, sidebar shortcuts, routed-event substitutes or AI Finish may stand in for
the normal forward route. Retain explicit player choices, finalize once, make a
Career advancement, save, terminate and reopen in a fresh native process.
Add bounded acceptance cases for other supported methods and archetypes without
making SR6 a prerequisite for this first SR5 increment.

### 4. Deliver verified Core content with installed and portable apps — P1

`Chummer.Avalonia/DesktopCoreContentRootResolver.cs` remains unchanged in the
audited head. It discovers content via `CHUMMER_CORE_CONTENT_ROOT`, the repository
or a sibling Core checkout and runs Git to validate pinned checkout identity.
The installer journey still injects the Core-content environment override.
Another pass with that workaround does not demonstrate a self-contained install.

Package the exact required rules/content alongside the app and resolve it relative
to the installation with its expected content identity and integrity checks.
Keep source-checkout overrides explicitly developmental. Do not remove integrity
validation or silently fall back to different rules to make startup pass.

On a fresh Windows profile with **no Git, no .NET SDK, no Chummer repositories and
no Core-content environment override**, install, launch from Start, manually
create a supported runner, save, exit/reboot and reopen the same runner. Repeat
for a portable build after moving its directory. Do not inherit a developer
override through the launcher or update chain.

### 5. Make installer acceptance enforce ordinary-user behavior

The parent-process exit and disposed-splash source corrections remain present.
Do not relabel those historical bugs as current implementation failures without
new evidence. Repair these distinct checked-in harness weaknesses:

1. **Autonomous relaunch:** a recognized installer crash can still trigger a
   harness launch, record `relaunchMode = harness-fallback`, and write
   `update-relaunch.status = pass`. Separate diagnostic recovery from acceptance.
   A harness-launched app must fail the autonomous-update leg.
2. **Existing shortcuts:** the journey deletes Chummer shortcuts before updating
   to avoid an overwrite timeout. Retain them in the acceptance case and resolve
   any overwrite issue in the installer. Cleanup belongs after the tested
   behavior, not before it.
3. **Checksum rejection:** being alive, retaining an executable and restarting
   does not prove that a bad artifact was evaluated. Require positive evidence
   of the attempted check and the expected checksum-mismatch rejection, no
   installer invocation, and unchanged installed identity/content. A no-op update
   check must fail. Bind the relaunched process to the expected executable and
   version, not a process name alone.

Run the exact current candidate through clean install → Start-menu launch →
character save → application-triggered update → old process exit → installer
completion → autonomous updated-app relaunch → unchanged character reopen.
Retain interrupted-update and stored-data compatibility checks. Perform signed
artifact and published-channel verification separately through the authorized
release lane; unsigned local artifacts are not signed public delivery.

### 6. Finish the existing package seal and affected native tests

At the audited head, [run 41493313162](https://github.com/ArchonMegalon/chummer6-ui/actions/runs/41493313162)
passed **Validate current main split-preseal without consumer or release claims**.
It skipped locked .NET SDK setup, the sealed current-main package consumer and
the sealed consumer receipt, and uploaded a non-authoritative preseal receipt.
The controller independently read back those run steps when writing this handoff.

This is an intentional intermediate state, not a fresh application build or a
newly qualified package graph. Complete the existing seal/consumer process before
candidate distribution so exact source, package graph and tested artifact agree.
Do not invent a new receipt/release framework, bypass protected checks or describe
preseal validation as release acceptance.

The gate report records four passing focused headless tests. The re-auditor
inspected but did not rerun them. Other worker reports describe full-test-build
debt and Windows-incompatible update tests; their counts belong to their recorded
branches/environments, not a fresh count on main. Classify relevant failures as
product defects, stale tests, unsupported scope or environment problems, and run
the affected Windows suite on the final candidate. An unchanged failure baseline
does not establish acceptance. Use normal-user filesystem permissions and actual
mouse/keyboard interaction. Routed Click fallbacks establish handler behavior,
not reliable ordinary input.

### 7. Finish AI recovery, Origin and amend workflows after the manual beta

**Keep AI Finish gated.** The current default-off gate contains the incomplete
action: one visibility path governs availability, busy and result updates; delayed
results or `SetFinishWithAiBusy(false)` cannot reopen it; control and window
handlers both check it. This closes ordinary-user exposure in source. Keep the
operator qualification switch out of normal installer launchers, shortcuts and
published configuration. Do not restore it to work around missing manual editors
or navigation.

The underlying completion flow still needs its agreed transaction/recovery
contract. Sequential Attributes/Skills/Qualities/Resources/Gear writes, fresh
operation GUIDs and an initial input-digest check alone do not provide atomicity
or durable retry identity. Use the Core-owned atomic completion path or a
deliberately resumable staged transaction, with stable operation identities bound
to owner, character revision and reviewed plan. Expose partial outcomes and
reconcile uncertain writes; do not blindly replay or invent another rules engine
in Avalonia. Test later-domain failure, process exit after an early commit, lost
responses, concurrent character edits, owner/account change and retry recovery.
Retain the successful native Priority case, then qualify legal all-archetype
review/apply/save/recovery before enabling the ordinary action.

Execute the actual output and portability workflows:

* **Origin:** display complete retained prose, export the intended manuscript/book
  format and reopen the same accepted content after process restart. Character
  JSON and dialog-source tests do not prove book export. Basic acceptance needs
  no new paid provider job.
* **Portable rules/amend:** import a real character-bound package, expose and
  explicitly resolve a conflict, save the frozen rule environment, export it and
  reopen in a clean profile. Missing/incompatible packages must yield actionable
  recovery without silently substituting default rules. Demonstrate applicable
  Chummer5a `custom_`, `override_` and `amend_` compatibility/import behavior.
  Opening Rules Setup or importing a `.chum5` character alone proves neither
  amend compatibility nor a conflict/missing-package roundtrip.

Narrow the older WIN-003 PASS labels to their actual evidence: HTML was not PDF,
character JSON was not Origin manuscript export, Rules Setup/legacy import was
not amend-package portability, and contract/build-intake tests were not native
missing-package recovery. Preserve the useful artifacts and correct their claims.

Finish with normal-user permissions, keyboard navigation, 125–200% scaling,
long/localized text and consistent dense desktop layout. Do not substitute a
larger evidence framework for executing the real workflows.

## Delivery report for each increment

Report the exact source/package/artifact identities, the concrete behavior fixed,
tests actually executed and their environment, remaining failing/unrun cases,
and whether the output is source-only, locally qualified, signed or published.
Keep synthetic reproductions, compiled tests, native interactions and release
acceptance distinct. State the next concrete blocker; give an ETA only when the
remaining work supports it. Submit small reviewable PRs and continue independent
assigned work while review or controller-owned integration is pending.

## Standing scope retained from the previous handoff

The user starts Friday Code on a separate Windows host. Continue the existing
`Chummer.Avalonia` desktop client, with `Chummer.Blazor.Desktop` as its documented
compatibility head. The controller retains Android, shared Core integration,
hosted services and provider operations. Windows work can progress independently
of Android releases and uses the Windows host's own build capacity.

### Delivery order

1. Build the pinned checkout on Windows. Launch the application, open or import
   a character, save it, close the process and reopen the saved character. Fix
   concrete Windows startup, interaction and persistence defects first.
2. Complete supported SR5 manual Creation and Career routes on Windows. Cover
   supported creation methods and archetypes, legal rules calculations, editing,
   resource spending, training, equipment and state changes. Verify cancellation,
   save/reopen and process restart for affected persistence routes. Shared rules
   come from Core packages; request an explicit shared change when necessary.
3. Complete Windows Origin reading/export and character-sheet PDF export, then
   portable per-character rule environments and amend-package workflows. Keep
   Chummer5a amend compatibility/import as the design target. Follow the existing
   rule-environment and portability specifications for character-bound rules,
   synchronization, missing-package recovery and conflict handling.
4. Produce the existing Windows installer/update artifact and verify installation,
   relaunch and recovery on Windows. Distinguish unsigned development artifacts,
   signed artifacts and published releases. Ask the controller for any required
   signing or release authority through the assigned task.

The complete SR5 AI Finish redesign is developed in parallel by the controller.
Its new-request action stays hidden until complete legal behavior across supported
archetypes and native review/apply/save/recovery are verified. Do not expose an
older partial implementation on Windows. SR6 and Linux retain their separate
delivery stages; they are not prerequisites for the first Windows increment.

Read `.codex-design/product/DESKTOP_CLIENT_PRODUCT_CUT.md`,
`.codex-design/product/DESKTOP_PLATFORM_ACCEPTANCE_MATRIX.yaml`,
`.codex-design/product/RULE_ENVIRONMENT_AND_AMEND_SYSTEM.md`, and
`.codex-design/product/INTEROP_AND_PORTABILITY_MODEL.md` for the existing design.
`config/product-requirements.reference.json` preserves the controller's 62
requirement identities and wording as a reference snapshot. Some requirements
are Android-specific or belong to later stages. The current Teable assignment
defines the Windows task and acceptance scope; the reference contains no claim
that an item is implemented, tested or delivered.

### Ownership and integration

Work on a dedicated `windows/...` branch from the assigned base. Preserve existing
work when resuming. Windows-specific UI, adapters, packaging and tests belong to
this lane within the exact assigned paths. Core rules, persistence authority,
shared Presentation APIs, package versions and contract changes need a controller
assignment. Submit the affected paths, problem, proposed change and Android
impact with the coordination client's `request` command; continue independent
assigned Windows work while the controller responds.

Own-branch commits and pull requests for assigned work are authorized. The
controller coordinates protected integration and shared package publication.
Do not force-push, change accounts/providers, operate Android signing/Play, or
restart hosted services as part of a Windows task.

Report the actual commit, Windows OS/runtime, build command, relevant tests,
artifact location and remaining blockers in Teable. A source patch, Linux
cross-build or passing managed harness is not a Windows launch/installation
result. Report incomplete routes explicitly and retain working user data.
