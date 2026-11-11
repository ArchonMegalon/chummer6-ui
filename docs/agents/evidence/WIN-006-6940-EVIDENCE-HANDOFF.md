# WIN-006 gen-8: windows_unsigned_preview_v1 — 6940 native qualification evidence handoff

Date: 2026-11-11. Worker: friday-windows (session `friday-windows-session-01`).
Base: `33764033a` (current main, "Mark contained Windows preview recipe for source publication").

## Candidate identity (authenticated draft release)

- Draft release ID 440805314, tag `windows-controller-intake-20261110-root6940`,
  repository ArchonMegalon/chummer6-ui (REST releases/tags route returned 404 for the
  draft; `gh release view`/`gh release download` by tag succeeded — no duplicate uploads).
- `chummer-avalonia-win-x64-installer.exe`: 63 207 343 B,
  SHA-256 `8C20B3AC5D932F604E64766CFB59CEF22A757438C8D2FF2988F88FACD86AEAA0`.
- `chummer-avalonia-win-x64-payload.zip`: 61 297 391 B,
  SHA-256 `286EA7260365D7F0AAC169E518669BDB7AAF4DB6E99FB9BB8B56E222AED858F4`.
- `chummer-avalonia-win-x64-payload.zip.json`: 470 B,
  SHA-256 `072C3467436807458AAC35373391472142D2832A48454984CC30ED4E7BBD868F`.
- All three files were byte-verified against the controller-provided hashes before
  execution. Binary source `1aa273c4ebc0954c31f2ddba01834f85690141a9`, branch
  `root/contained-windows-preview-6940-candidate`, bundled Core content `c28c87a1`.
- The installed executable also hashes `93EC81711D9E5574333DE11304D1F785F032A84AE3D5F5799D8D66DD0BE0A286`
  (installer-written `Chummer.Avalonia.exe`, 574-file install).

## Startup receipt (windows_unsigned_preview_v1 deliverable)

`win106-6940-startup-smoke-receipt.json` is the exact, verbatim, installer-bound
startup receipt from leg A (isolated smoke install of the draft installer +
`--startup-smoke` against the installed bytes):

- `readyCheckpoint: "pre_ui_event_loop"`, `status: "pass"`.
- `version` / `releaseVersion`: `1.0.0-preview.20261110.6940`; channel `preview`;
  platform `windows`, arch `x64`, rid `win-x64`.
- `executionEnvironment: "native_windows"`, `verificationScope: "native_windows_startup"`;
  `nativeHostEvidence.status: "verified"` (host kernel `Windows_NT`, native-msys-direct
  selection) — annotation describes the real run; produced by
  `scripts/run-desktop-startup-smoke.sh` and the shipped annotation contract, not the
  old hosted-only capture script. No GitHub Actions identity is claimed.
- `artifactDigest: "sha256:8c20b3ac…aeaa0"` matches the verified installer bytes
  (`artifactDigestSource: "environment"`).
- `operatingSystem: "Microsoft Windows 10.0.26200"`, `.NET 10.0.3`,
  recorded 2026-11-11T09:13:37Z.
- `installLinkingStatus: "guest"` with reason `local_channel_no_claim_required`
  (no account claim was made; no credentials exist in the receipt).

## Retained leg results and controller-reviewed limits

1. **Leg A — isolated smoke install + startup smoke (PASS).**
   The installer's embedded-payload smoke mode (`/smoke-install=`) ran in a fresh
   isolated root; its bundled 7-Zip checksum-verify cmd confirmed the payload digest
   before extraction (agent-shell note: `NODEFAULTCURRENTDIRECTORYINEXEPATH` was
   removed from the child environment, not merely blanked, so the bundled cmd could
   resolve `7za.exe` by current directory; checksum passed). `--startup-smoke` then
   produced the receipt above. Smoke mode is `SetSilent silent` by design
   (installer.nsi ~line 406), so leg A shows no installer window; visible-UI evidence
   comes from the interactive leg below.
2. **Leg B — real install + ordinary launch (PASS).**
   The previously installed 6408 build was first uninstalled with its own installer
   binary (registry key removed, install dir emptied). 6940 was then installed with
   `--unattended`: registry key `Chummer6.avalonia`, display version
   `1.0.0-preview.20261110.6940`, 574 files at
   `C:\Users\tibor\AppData\Local\Programs\Chummer6\AvaloniaDesktop-win-x64`.
   Ordinary installed launch stayed alive 30 s with the visible "Claim your copy"
   window, then closed cleanly. Existing installations/accounts/characters outside
   the isolated state roots were preserved.
3. **Leg C — local fixture save + fresh-process reopen (PASS).**
   Persistence probe (`--windows-persistence-probe`, isolated state root): save then
   fresh-process reopen both succeeded (`PORTABLE_PERSISTENCE_OK`,
   workspaceId `42c41273cc8e41c4b17277ded5c6c801`). Successful local save with sync
   unavailable is covered here and by the leg D disclosure.
4. **Leg D — SR5 Creation→Career mouse-first journey (PASS).**
   Against the *installed* exe (not the repo build): `status=pass`, 151 steps /
   37 pointer actions, scenario `win106-6940-sr5-priority-native`. AI Finish was not
   enabled and the run never finalized into an automated acceptance. Roaming was not
   enabled; no Core-content checkout override was used.
   - **Sign-in-page observation only:** `win106-6940-legD-disclosure-home.png`
     shows the browser's email-sign-in-unavailable message. It does not show the
     installed application's roaming/sync status. The raw worker receipt's broader
     sync-disclosure PASS is not accepted by the controller.
   - **SR6 boundary:** the 6940 source retains the legacy XML advancement refusal.
     This older binary does not contain the later Core-backed SR6 advancement
     dialog from PR #585. No native SR6 advancement journey is established here.
   - **Manual Creation/Career boundary:** the retained automated journey is useful
     interaction evidence, but is not acceptance of complete manual Creation or
     all confirmation/save assertions. Those guards are being repaired separately.

## Installer progress/completion visuals (interactive run)

Because smoke mode is silent by design, the visible-UI evidence comes from a
separate interactive (non-`--unattended`, non-smoke) installer run over the already
installed 6940 build, captured at 1.5 s intervals (117 frames + completion window;
full frame sets and meta remain on-host, not committed):

- `win106-6940-installer-ui-01.png` — installer wizard first page.
- `win106-6940-installer-ui-06.png` — "Installing" mid-extraction; progress log shows
  removal of the previous install directory; Cancel/Close disabled.
- `win106-6940-installer-ui-100.png` — progress bar 100% (green), status
  "Install complete", modal prompt "Open Chummer now?" (Yes/No).
- `win106-6940-installer-completed-window.png` — window title "...: Completed",
  status "Completed", full green bar. The prompt was answered No (ordinary launch
  already proven in leg B) and the wizard was closed via its Close button; the
  process exited cleanly and the registry retained
  `1.0.0-preview.20261110.6940` (574 files).
- `win106-6940-installer-ui-meta.json` — capture metadata for the full frame set.

## Committed files (byte-custody; SHA-256 over committed bytes)

The text receipts and logs below are retained verbatim under `docs/agents/evidence/`.
Their raw worker PASS labels remain historical observations and must be read with
the controller limitations above. The existing `-text whitespace=cr-at-eol`
attribute preserves their bytes. Full-desktop screenshots and capture metadata
are retained privately by the controller; they are not part of this merge or the
public download candidate.

| File | Bytes | SHA-256 |
| --- | --- | --- |
| win106-6940-qual-receipt.json | 5467 | ee3806b15cd055cbbea3e2733f90ee8e758da1cf2663fda7a8b0d2d49d6c9417 |
| win106-6940-startup-smoke-receipt.json | 1490 | 6ddeccf3a4646a3aa64c72bc66d4495dac3406c2766750bb229c041b1539ca6b |
| win106-6940-legA.log | 750 | 73dd550fceb5fdbf9630bbd3bc9367cc7961c20e6a23f3e05e893a90ed8b62c8 |
| win106-6940-legA-installer-progress.log | 599 | be27c4242bcf7448cb83c905798c2920033b8214c7a77d621c52aed77c3ca435 |
| win106-6940-legB-install-verify.json | 885 | cc78ffbd66b4bfcafc199a0effd062ca6dc48984aece74f1192aa558aea8e4ab |
| win106-6940-legB-launch-observe.json | 98 | 5ba423c9bc1e35cf293220b088b01140d8cb880af05ff371dcb860380004f3db |
| win106-6940-legC-persistence-receipt.json | 524 | 1f678f46d7fd5d8f799a1f4ecea9ba84fdfd63855b35e5ec9bea00e5a2ca1809 |
| win106-6940-legC-save.json | 452 | 72f0e1222c004b82556827b93008cd5fbbd97dbddea2f09595632fdac5780a9f |
| win106-6940-legC-reopen.json | 454 | 3c7f8b03a751f9c65f0c8a28e6071b927d9d0deacf1038dc48acbe644cc6f540 |
| win106-6940-legD-journey-receipt.json | 87837 | 3f55f7d173c12d0bc5b816494f8c347a753b54515ad97ccd23b70bfc84b02050 |
| win106-6940-legD-disclosure-observe.json | 139 | 6fd9679f9e3729561b55dc87a7a5d9eb2d020d0c8e00ee5a612d1b6ebfe50391 |

## Scope and custody limits

- This handoff qualifies the **6940 preview candidate** only. It does not relabel
  6408/R94 evidence, does not rebuild or substitute any runtime bytes, does not
  broaden into full beta/SR6/sync acceptance, and does not constitute a release
  approval. Publication of the download remains a controller/root decision.
- Full-desktop screenshots include unrelated browser/profile context and an
  install-link identifier. No credential leak is established, but these originals
  are excluded from this merge and from public download files. The controller
  inspected the actual installer progress and completion images and retained the
  exact bytes privately; no generated or altered image substitutes for that evidence.
- Receipts include synthetic character labels, workspace IDs, hashes, local paths
  and public install-link metadata (same disclosure class as the accepted R94
  handoff). No character document or credential is included.
- Visual inspection was automated, not human review. The installer status,
  progress bar and completion control were readable in the inspected captures;
  no DPI matrix, app sync disclosure, or full product acceptance is implied.
  No user visual-review relay is pending.

## Private visual evidence retained by the controller

These exact originals were reviewed, including progress and completion. They are
excluded from the public source change and distributable shelf. Their hashes
identify retained evidence, not an assertion of native functionality beyond the
observed installer surfaces.

| File | Bytes | SHA-256 |
| --- | --- | --- |
| win106-6940-legD-disclosure-home.png | 220207 | ff170a2e1ca884b81945128e1560645fabe15e30a835e3a9a307d7f7565fe0cb |
| win106-6940-installer-ui-01.png | 275613 | b6698fd975f5263a50e017d00c6666f051ddb1b67cd5135456ebb5837cb7d416 |
| win106-6940-installer-ui-06.png | 343085 | aefa62814747ccd547f6fae46283064517eeb7b14fe4c102bb8d3111bbcdce05 |
| win106-6940-installer-ui-100.png | 295016 | 7b9f69732890349cfd1e7b89ad5ee23e668cff84f09c91697d97b15e60f1e01a |
| win106-6940-installer-completed-window.png | 305419 | 086fa49a43b33e22a51714abbe173d4f8502044fc244d6c5dce4f320bc0880cc |
| win106-6940-installer-ui-meta.json | 15054 | 448b76e983cf1deb18553b5e5cf527c31880df353bbf8130725acd6fdf6773ad |
