# Native Windows nightly evidence

These workflows close the two Windows-only nightly evidence gaps without publishing anything:

1. `Windows native evidence capture` downloads one exact staged-candidate artifact from one successful workflow run, validates the supplied version, channel, manifest, installer, and payload SHA-256 bindings, then runs both desktop heads on `windows-latest`. It uploads only native startup receipts, per-head download-progress logs, and distinct interactive progress/completion PNG captures.
2. `Windows native evidence review and finalization` downloads one exact named capture artifact from one exact run. A reviewer must start the run, pass the protected `windows-visual-review` environment, be listed in the `WINDOWS_VISUAL_REVIEWER_ALLOWLIST` repository/environment variable, differ from the capture actor, and explicitly confirm readability, contrast, and clipping for each head. It revalidates every captured byte before producing the two stage-compatible visual-proof JSON files.

Both workflows have only `contents: read` and `actions: read`. They do not read release credentials, mutate a release, or publish/download-site bytes. Their only external mutation is GitHub Actions artifact upload with 14-day retention.

## Required configuration

Create a protected GitHub environment named `windows-visual-review`, require the accountable reviewers there, and set `WINDOWS_VISUAL_REVIEWER_ALLOWLIST` to a JSON array of GitHub logins, for example:

```json
["accountable-reviewer", "backup-reviewer"]
```

The allowlist does not replace environment approval. The actor who dispatches finalization must satisfy both controls and cannot be the actor recorded by capture.

Capture accepts nine dispatch inputs. The three compact JSON inputs have exact schemas and reject missing or extra keys:

```json
{"version":"preview-20260718.1","channel":"preview","manifestPath":"RELEASE_CHANNEL.generated.json","manifestSha256":"<64 lowercase hex>"}
```

```json
{"installerPath":"files/chummer-avalonia-win-x64-installer.exe","installerSha256":"<64 lowercase hex>","payloadPath":"files/chummer-avalonia-win-x64-payload.zip","payloadSha256":"<64 lowercase hex>"}
```

Use the same binding shape for `blazor_binding_json`. Finalization similarly accepts exact per-head review JSON:

```json
{"readability":true,"contrast":true,"clipping":true}
```

## Receipts and handoff

Capture uploads `windows-native-evidence-<run-id>-<attempt>` and prints its artifact ID, GitHub artifact digest, URL, and `WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json` SHA-256 in the job summary. The capture manifest binds repository, workflow path, run ID/attempt, ref, source SHA, actor, deterministic artifact name, candidate provenance, and both installer/payload byte identities.

Finalization uploads `windows-native-evidence-finalized-<run-id>-<attempt>` and prints the corresponding GitHub artifact identity. Its root contains the unchanged capture manifest/inventory, `startup-smoke/`, `screenshots/`, the two `WINDOWS_INSTALLER_VISUAL_PROOF-<head>-win-x64.generated.json` files, `WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json`, and `WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json`. The finalization receipt and each proof bind the authenticated finalization repository/workflow/run/ref/SHA/actor/artifact identity; capture and finalization source SHAs must match.

The final artifact remains evidence only. Supply its separately computed exact tree digest to the existing preview-nightly stage seal operation; publication remains a distinct credentialed operator action after every stage verifier passes.
