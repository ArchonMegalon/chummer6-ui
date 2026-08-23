# Avalonia Character Creation Wizard: Windows and macOS smoke plan

## Scope and current truth

The wizard session, route graph consumption, budget rail, legal-option projection,
checkpoint codec, Build Ghost context, keyboard routing, and Avalonia storage-provider
integration are shared desktop source. Linux unit/headless execution may validate those
contracts, but it does **not** prove a Windows or macOS binary, native picker, package,
signature, notarization, or launch.

The unrestricted editor must remain hidden while `CharacterOverviewState.CreationWizard`
is non-null. Canonical finalization makes that projection null; only then may the existing
advanced editor surface return.

## Shared preflight

For the exact candidate revision:

1. Run `Chummer.CreationWizard.Presentation.Tests` and the focused Avalonia wizard
   headless/source tests.
2. Build the Avalonia project with the repository's exact Core/contract graph.
3. Verify a corrupt, wrong-workspace, wrong-revision, and wrong-snapshot checkpoint falls
   back to the authoritative active step without changing character data.
4. Verify the current canonical snapshot's empty runtime fingerprint plus explicit
   runtime/context blockers keep Build Ghost input and send controls disabled and cause zero
   `SendBuildTurnAsync` transport invocations. This foundation slice does not claim an
   authority-bearing conversation.

## Windows native gate

Run on the repository's governed Windows runner, using the existing
`windows-native-evidence-capture.yml` / `windows-native-evidence-finalize.yml` lane:

1. Build and package the exact Avalonia commit for `win-x64`.
2. Verify Authenticode with `scripts/verify-windows-authenticode.ps1`. If the signing
   identity or timestamp authority is unavailable, stop with `pending-signing`; never emit
   a signed/pass claim.
3. Launch the packaged executable and prove the creation wizard owns the center surface for
   an unfinished runner while the unrestricted editor is absent.
4. Exercise mouse and keyboard navigation, including Alt+Left/Right and Ctrl+Shift+G.
5. Exercise the native open/save picker for checkpoint export/recovery, restart the process,
   and prove exact-revision resume plus stale-revision invalidation.
6. Prove Build Ghost remains authority-pending, its input/send controls remain disabled, and
   no transport invocation occurs for the canonical snapshot.

Required receipt fields: commit, package SHA-256, signer/timestamp verdict, launched process
identity, workspace/revision/snapshot digest, checkpoint digest, selected step before/after
restart, stale-checkpoint outcome, Build Ghost authority blockers, disabled-control state, and
a zero transport invocation outcome.

## macOS native gate

Run on the governed macOS runner through `macos-flagship-evidence.yml` and the existing
`scripts/run-macos-flagship-evidence.sh` / `scripts/preflight-macos-packaging.sh` lane:

1. Build and package the exact Avalonia commit for the intended macOS RID and `.dmg`.
2. Verify Developer ID signing, hardened runtime, entitlements, and notarization/stapling.
   Missing signing or notarization credentials must produce `pending-signing` or
   `pending-notarization`, never pass.
3. Launch from the mounted package and prove the same unfinished-runner editor lock as on
   Windows.
4. Exercise mouse and keyboard navigation, including Alt+Left/Right and ⌘+Shift+G.
5. Exercise the native NSOpenPanel/NSSavePanel path exposed through Avalonia's
   `IStorageProvider`, restart, and prove exact resume and stale invalidation.
6. Prove the same Build Ghost authority-pending, disabled-control, and zero-transport behavior.

Required receipt fields mirror Windows and additionally bind codesign identity, Gatekeeper
assessment, notarization request/result, staple verification, mounted app identity, and the
macOS RID.

## Explicitly deferred Build Ghost interaction proof

Interactive follow-up and stale-response rejection require a later canonical projector that
supplies a non-empty runtime fingerprint and removes both runtime/context authority blockers.
Only that authority-bearing slice may enable the controls and run transport-bound conversational
tests. This foundation candidate must not simulate those facts or treat the disabled seam as an
interactive pass.

## Promotion rule

Windows and macOS are independently fail-closed. A green shared/Linux test result plus one
native platform is not cross-platform proof. Promotion requires both native receipts bound
to the same source commit and package digests, or an explicit release statement that the
unproven platform remains postponed.
