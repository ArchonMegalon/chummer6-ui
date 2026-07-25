# Native Windows nightly evidence

These workflows close the promoted Avalonia Windows nightly evidence gap without publishing anything. Blazor Desktop remains a bounded compatibility fallback and is excluded from the public preview shelf until it has an independent evidence lane:

The canonical manifest uses Registry's exact current Linux/Windows preview
target. The producer requires Avalonia to be the only head on every desktop
artifact row and permits only the exact Avalonia `win-x64` installer in the
Windows candidate. macOS remains buildable static policy and historical lane
evidence, but every macOS artifact row is rejected from this candidate.

1. `Preview nightly candidate artifact export` runs the byte exporter on the single-use JIT runner with only `contents: read`. Before allocating it, hosted preflight refetches the fixed public `RELEASE_CHANNEL.generated.json`, requires byte-for-byte equality with the held live-root input, and proves that the N−1 binding selects the live Windows artifact. It uploads the exact signed Windows publication subset and generated provenance contracts, then emits canonical handoff v4. A separate GitHub-hosted relay job has only job-scoped `actions: write`; it receives no candidate mount or release credential and dispatches only the fixed native-capture workflow. The relay forwards the byte-for-byte N−1 and live-root JSON plus the candidate handoff, and its v3 receipt binds both raw digests, the canonical selected-tuple digest, and Authenticode certificate/SPKI authority.
2. `Windows native evidence capture` requires all three canonical inputs. Before authenticating or downloading a candidate artifact, it independently refetches the same fixed public root and requires exact equality. The checked-in lifecycle validator then revalidates the exact N−1 schema, Windows/RID tuple, canonical serialization, immutable manifest/installer/payload URLs and hashes, live-root selection, fixed byte bounds, and repository-authorized signer pins. The workflow cross-checks both authority digests, the selected-tuple digest, and both pins against handoff v4. It hardcodes the current repository and exporter workflow, bounded-polls only the handed-off run ID and attempt for at most 60 seconds while it is queued or in progress, and rejects attempt drift, timeout, or any terminal result other than success before querying an artifact or downloading bytes. It authenticates the exact workflow-dispatch run, main ref/SHA, producer actor, artifact ID/name/REST digest, and unexpired timestamps. GitHubScript downloads the original ZIP by that exact REST artifact ID into a fresh private runner-temp directory and requires the raw ZIP SHA-256 to equal the authenticated `sha256:<hex>` digest. The Python contract re-hashes the same no-follow ZIP descriptor before parsing, rejects duplicate, traversal, linked/special, encrypted, oversized, or compression-abusive members, and exclusively extracts the exact versioned candidate tree into one private held root. Materialization writes a separate authenticated held-snapshot authority. The native step opens every candidate file with read-only/no-write-or-delete sharing, verifies each live handle's digest and length against that authority, repeats the Python preflight under those locks, and keeps the same handles alive across the lifecycle run, screenshots, provenance copying, and capture inventory. It rechecks every live handle before releasing the locks, so all later path opens can only resolve to the pinned immutable bytes.
3. `Windows native evidence review and finalization` downloads one exact named capture artifact from one exact run. An allowlisted human must start the run, pass the protected `windows-visual-review` environment, differ from the automated `github-actions[bot]` capture actor, and explicitly confirm readability, contrast, and clipping for the promoted head. This is an automated-bot-capture versus allowlisted-human-review separation; it does not claim that the reviewer is a second human distinct from the candidate producer. Finalization revalidates every captured byte plus the preserved candidate inventory/export-receipt path, hash, size, contract, release, source, and head bindings before producing the stage-compatible Avalonia visual-proof JSON file.

The JIT export job, capture workflow, and finalization workflow do not receive write credentials or release secrets. Capture and finalization have only `contents: read` and `actions: read`. The hosted relay alone has `actions: write`, cannot read the JIT candidate mount, and can dispatch only the fixed capture endpoint. None can mutate a release or publish download-site bytes; their artifact uploads retain evidence for 14 days.

## Required configuration

Create a protected GitHub environment named `preview-nightly-candidate-export`. Register one disposable non-root JIT runner with the exact `chummer-preview-nightly-export-<nonce>` label used by the dispatch, mount only the exact candidate subset at `/candidate-input` read-only, and do not mount a host home, Docker socket, credentials, sibling repositories, or other candidate files. Destroy that runner after its single export job.

Set repository variables `CHUMMER_WINDOWS_AUTHENTICODE_SIGNER_CERT_SHA256`
and `CHUMMER_WINDOWS_AUTHENTICODE_SIGNER_SPKI_SHA256` to the authorized
lowercase 64-hex signer pins. Missing, malformed, or host-supplied pins that
differ from these values stop the exporter preflight and native capture.

Create a protected GitHub environment named `windows-visual-review`, require the accountable reviewers there, and set `WINDOWS_VISUAL_REVIEWER_ALLOWLIST` to a JSON array of GitHub logins, for example:

```json
["accountable-reviewer", "backup-reviewer"]
```

The allowlist does not replace environment approval. The actor who dispatches finalization must satisfy both controls and cannot be the `github-actions[bot]` actor recorded by the relayed capture.

Capture accepts exactly three dispatch inputs:

- `candidate_handoff_json` is byte-for-byte canonical handoff v4 (sorted
  keys, compact separators, no trailing newline). In addition to the exact
  producer run, artifact, inventory, publication-scope, full-shelf,
  Registry-PREPARE, and signing-receipt digests, it contains
  `nMinusOneReleaseSha256`, `liveReleaseChannelSha256`,
  `selectedTupleSha256`,
  `authenticodeSignerCertificateSha256`, and
  `authenticodeSignerSpkiSha256`.
- `n_minus_one_release_json` is the same byte sequence held and validated by
  the host launcher. It uses the exact
  `chummer6-ui.desktop-native-lifecycle-n-minus-one` schema for
  `windows`/`win-x64`; missing or extra fields, alternate JSON formatting,
  changed hashes or URLs, and candidate-version substitution are rejected.
- `live_release_channel_json` is the exact UTF-8 response body fetched from
  `https://chummer.run/downloads/RELEASE_CHANNEL.generated.json`. The hosted
  preflight and native capture each refetch that fixed URL without accepting
  redirects or content encoding and require byte equality before using it.
  The selected Windows row must also carry verified
  `native_windows`/`native_windows_startup` host evidence; a Wine-only
  compatibility row is not an eligible flagship predecessor.

The hosted producer preflight, bot relay, and native capture recompute the
authority digests. The platform-generic validator hashes a canonical selected
tuple containing the platform/RID, generation/version/time, exact artifact,
manifest, and Windows payload bindings, plus both raw authority digests. The
capture actor and triggering actor must both be `github-actions[bot]`, proving
that the checked-in relay—not an operator-crafted direct dispatch—supplied all
three inputs. Capture receipts, independent Authenticode receipts, lifecycle
evidence, and the global adapter bind both actors plus the exact run ID and
attempt under the explicit `same-actor-only` rerun policy. A human rerun of a
bot-dispatched capture is therefore rejected instead of inheriting the
relay's original authority.
There is no first-release exception: a generationless or macOS-only incumbent,
or the current unsigned/Wine-only Windows compatibility shelf, cannot seed
this lane. Publish a signed cross-platform baseline through its own governed
release before using that baseline as flagship N−1.

The capture workflow itself must run from that same exact main SHA. Finalization still requires the exact full `capture_ref`; bare branch claims are rejected, and any qualified REST workflow path must agree with the recorded ref. Finalization accepts exact Avalonia review JSON:

```json
{"readability":true,"contrast":true,"clipping":true}
```

## Receipts and handoff

Capture uploads `windows-native-evidence-<run-id>-<attempt>` and prints its artifact ID, GitHub artifact digest, URL, and `WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json` SHA-256 in the job summary. The capture manifest binds repository, workflow path, run ID/attempt, ref, source SHA, relay actor, triggering actor, rerun policy, deterministic artifact name, producer artifact ID/API digest/timestamps, canonical handoff and authenticated-API hashes, live-root and selected-tuple digests, deterministic content inventory, exporter receipt, and the installer/payload byte identities. The unchanged producer bytes are preserved at:

- `candidate-provenance/PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json`
- `candidate-provenance/PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json`

Finalization uploads `windows-native-evidence-finalized-<run-id>-<attempt>` and prints the corresponding GitHub artifact identity. Its root contains the unchanged capture manifest/inventory, `startup-smoke/`, `screenshots/`, `WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json`, `WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json`, and `WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json`. The finalization receipt and proof bind the authenticated finalization repository/workflow/run/ref/SHA/actor/artifact identity; capture and finalization source SHAs must match.

The final artifact remains evidence only. Download the finalized artifact through GitHub Actions as its original ZIP and do not extract, recompress, or repack it. Supply that ZIP's absolute path as `CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE` to the preview-nightly stage seal operation. Keep the capture and finalization artifacts unexpired: seal and later sealed-stage verification authenticate the exact workflow runs, attempts, actors, refs, commits, artifact names, IDs, and GitHub `sha256:` artifact digests through unauthenticated, read-only GitHub Actions REST API requests. There is no token or locally computed tree-digest substitute. Publication remains a distinct credentialed operator action after every stage verifier passes.
