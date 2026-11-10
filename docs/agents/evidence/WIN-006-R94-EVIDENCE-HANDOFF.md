# WIN-006 gen-6 item 1: R94 evidence handoff (authenticated, controller-reachable)

Date: 2026-11-10. Worker: friday-windows. Base: `a2020b9a3` (main).
Authoritative round record: Teable reports `recjQP1sL0PrHsvFKEd` (R96 intake transfer, gen 3)
and `recjQP1sL0PrHsvFKEd`-successor board history; this document is the durable,
in-repo completion of that transfer requested by the 10 November 2026 handoff (item 1).

The four R94 receipts from the 2026-11-09 native qualification round are committed
verbatim under `docs/agents/evidence/` in this branch. Each file is byte-exact as
produced on the Windows host; SHA-256 below is over the committed bytes.

No reruns were performed. The receipts include synthetic character labels,
workspace IDs, hashes, local paths and public installation-link metadata; they
do not contain a character document or credential. They are historical worker
observations, not a controller rerun or a release approval.

## Controller review — 10 November 2026

The receipt's top-level `pass` does **not** establish complete manual SR5
Creation or Creation-to-Career acceptance. Its own steps record that ordinary
Next timed out with deep stages pending, Review remained blocked, and
finalization was not attempted (`profileCreated=False`). The created workspace
was saved and refreshed; the subsequent print/export and Career checks used
the bundled demo runner. Several controls required routed Click fallbacks after
pointer delivery was swallowed. These observations cannot qualify normal
pointer interaction for those controls.

The handoff closes retrieval of the four receipts. It retains useful bounded
save/reload, moved-portable persistence and corrupt-update rejection evidence
for the identified 6408 candidate. Manual completion, an applied Career
advancement, independently checked PDF readability, autonomous successful
update, clean-machine installation and amended account-sync acceptance remain
separate. The controller did not execute Windows during this review.

## 1. Qualification receipt — `win094-qual-receipt.json`

- **sha256:** `71589decdfb71fa8ab6ed8e969198ff0dcb584ba180bf11826f0e52672557cad` (2 736 bytes)
- **Candidate:** release `windows-controller-intake-20261109-root6408`, version
  `1.0.0-preview.20261109.6408`, installer sha256 `E72C283F27A72A5CBA04CDAF05FED18716F046EB…`
  (full: `e72c283f27a72a5cba04cdaf05fed18716f046ebc894e531f29995b585684d46`, 63 206 474 B),
  payload sha256 `4D4516B1A61099E…`, intake dir `intake-6408`.
- **Recorded observations:** the wrapper says succeeded=true over four legs, but
  `sr5_creation_journey` stopped before finalization (154 steps / 38 pointer
  actions, workspace `0c0f0094…`). Career Award-Karma did not open. The later
  demo-runner `print_export_pdf` produced a `%PDF-` Soma-print.pdf, 2 645 B,
  sha256 `D776A0E16958DBFEF240215959618C7028228762570125D8E8A55D219084E9F6` +
  Soma-export.json 43 434 B. `portable_move` is a separate persistence leg;
  `installed_persistence` refers to earlier evidence rather than a new leg here.
- **Install identity:** HKCU uninstall key `Chummer6.avalonia`, display version
  `1.0.0-preview.20261109.6408`, install root
  `C:\Users\tibor\AppData\Local\Programs\Chummer6\AvaloniaDesktop-win-x64`,
  574 files / 332 core-content, embedded content digest match=true.
  Known agent-shell note (`NoDefaultCurrentDirectoryInExePath=1` cleared for the
  bundled checksum-verify cmd; checksum passed) is recorded in the receipt itself.

## 2. Journey receipt — `journey_receipt.json`

- **sha256:** `a3241064c1fcc4c1e3de55adbffe0308d66b5cfe0c263a8713154b86c851acdc` (89 499 bytes)
- **Identity:** `status=pass`, scenario `win094-sr5-priority-native`, platform
  `windows`/`x64`/`win-x64`, head `avalonia`, version `1.0.0-preview.20261109.6408`,
  channel `off`, artifactDigest `sha256:6efb92bf534496572ad5468368c6300e97fe62f0c02732c37c17ec43203697c4`
  (source: process_path), OS `Microsoft Windows 10.0.26200`,
  started 2026-11-09T14:19:55Z, completed 14:22:38Z.
- **Actions established:** the native runner exercised part of Creation and
  saved/refreshed an unfinished workspace on installed build 6408. Review and
  finalization remained blocked. It then loaded the bundled demo runner for
  output/Career checks. This is not a completed manual Creation-to-Career journey.

## 3. Moved-portable receipt — `win094-moved-portable-receipt.json`

- **sha256:** `9a40de440a9c88d2e87dfda4de16fa491daf53e61a7c391af326c0c936b92e44` (519 bytes)
- **Identity:** label `win094-moved-portable`, appRoot `…\runs\win094\moved-app`,
  save pid 11764 → reopen pid 20100 (distinct processes), workspace
  `4c8d4fb3…`, profileSha256 `39B11D5F517B1520C2C7E8F97DCA05CA16E1EC59CDD40D5B5418200EA7917300`,
  workspaceFileSha256 `3DA9AFCDEEF785072EE1FFEA6DE404B1CCE9A84B3E3A79504F3F6E1A9FFE9C69`,
  succeeded=true.
- **Actions established:** the 574-file installed bundle was moved to `D:` and the
  real application saved and reopened a character from the moved location with an
  unchanged profile baseline — portability leg of the qualification.

## 4. Corrupt-update rejection proof — `corrupt_proof.json`

- **sha256:** `45ae7967cafe7979c35505f04b2172eff48eaf8d2a9b5931b9a0b2153ae3f4bb` (3 063 bytes)
- **Identity:** label `win094-corrupt-update-rejection`; corrupt payload
  1 048 576 B, actual sha256 `de7b03337147871981967a1d0b9104a3ec5cb3294964ccb494e7cde04cfdcede`
  vs manifest-claimed `bbbb…`; `rejected=true`, failureReason `update_apply_failed`,
  lastError "Desktop update artifact … failed checksum validation"; app did not exit
  on its own; state journal captured.
- **Actions established:** the update runtime positively rejects a checksum-mismatched
  payload (native negative leg), the application survives, and the failure is journaled.

## Coverage statement

These four receipts are the complete R94 evidence set reported on 2026-11-09
(report `fed12d…`-generation board record, re-hashed into Teable `recjQP1sL0PrHsvFKEd`).
Reuse scope: these hashes cover exactly the unchanged inputs identified there
(release `windows-controller-intake-20261109-root6408`, version
`1.0.0-preview.20261109.6408`). Any newly delivered candidate requires fresh legs
per handoff item 4; this document does not extend the covered claims.
