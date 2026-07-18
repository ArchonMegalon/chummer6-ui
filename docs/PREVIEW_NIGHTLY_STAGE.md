# Stage-only Windows/Linux preview nightly

`scripts/build-preview-nightly-stage.sh` is the canonical fail-closed producer
for a complete preview shelf with freshly built Windows x64 and Linux x64
installers for both desktop heads. It does not publish. Its only successful
output is either an unsealed candidate awaiting native Windows evidence or a
sealed `nightly-run-<version>` directory suitable for the hardened Run
upload-session uploader's dry-run input.

The lane is deliberately split in two. `prepare` can execute on Linux and
forces both Windows compatibility smokes to fetch the exact staged payload over
HTTP (`CHUMMER_WINDOWS_STARTUP_SMOKE_PAYLOAD_MODE=download`). `seal` requires
new native-Windows startup receipts and installer progress/completion visual
proofs for both heads, each bound to its exact new installer and downloaded
payload bytes. There is no force or proof-only visual mode.

## Non-publication boundary

The orchestrator never calls an upload, deploy, `curl`, or public-release
command and never reads an upload ticket. It forces stage-only manifest
generation, disables remote proof inputs and remote proof probes, and points
every generated path into the candidate. `prepare` and `seal` are safe to run
without release credentials in the environment. Keep credentials out of that
environment anyway; the later uploader remains a separate authority boundary.
Seal and sealed-stage verification do make unauthenticated, read-only requests
to the public GitHub Actions REST API. Those requests authenticate the exact
candidate-export, capture, and finalization runs and artifact digests; they
cannot upload, mutate, or publish anything and have no token fallback. The shell
also removes inherited `GH_TOKEN` and `GITHUB_TOKEN` before these checks.

The sealed receipt is
`PREVIEW_NIGHTLY_STAGE_SEAL.generated.json` with contract
`chummer6-ui.preview-nightly-stage` version 1. It inventories every staged byte,
records exact source commits, records the incumbent shelf hashes, and marks
`uploadAuthorized=false` and `requiredFirstConsumerMode=dry_run`.
Its semantic proof embeds the producer run/artifact identity, both exact
exporter-provenance file hashes, and the five-row candidate content inventory in
addition to inventorying those bytes in the sealed tree.

The stage also contains `RELEASE_UPLOAD_CANDIDATE.generated.json`. Its inventory
is byte-for-byte compatible with the hosted bootstrap pinned by SHA-256
`74e5e19e7622cadf46880e140eff385d16ed136d200494f63529f4f01b7935fd`:
`releases.json`, `RELEASE_CHANNEL.generated.json`,
`release-evidence/public-promotion.json`, and every regular file under `files/`
and `startup-smoke/`. Proof, signing, AUR, seal, and handoff files remain local
seal evidence and are not falsely represented as uploaded by that consumer. The
receipt is not a completed upload handoff. Only Run may emit
`chummer.release-upload-handoff/v1`, and only after a real 2xx upload-session
completion; the stage seal records `postUploadHandoffEmitted=false`.

## Required exact source authorities

Every root must be an absolute physical Git top-level, every commit must be a
lowercase 40-character full commit, and every worktree must be clean. There are
no branch or sibling-directory defaults. Each role also has a tracked,
role-specific project/solution sentinel, so swapping two clean repositories or
placing unrelated Git contents at a configured root fails before any build.

| Source | Root | Commit |
| --- | --- | --- |
| Presentation | `CHUMMER_UI_ROOT` | `CHUMMER_UI_EXPECTED_COMMIT` |
| Core | `CHUMMER_CORE_ROOT` | `CHUMMER_CORE_EXPECTED_COMMIT` |
| Run services | `CHUMMER_RUN_ROOT` | `CHUMMER_RUN_EXPECTED_COMMIT` |
| UI kit | `CHUMMER_UI_KIT_ROOT` | `CHUMMER_UI_KIT_EXPECTED_COMMIT` |
| Hub Registry | `CHUMMER_HUB_REGISTRY_ROOT` | `CHUMMER_HUB_REGISTRY_EXPECTED_COMMIT` |
| Media Factory | `CHUMMER_MEDIA_FACTORY_ROOT` | `CHUMMER_MEDIA_FACTORY_EXPECTED_COMMIT` |
| Legacy Chummer | `CHUMMER_LEGACY_ROOT` | `CHUMMER_LEGACY_EXPECTED_COMMIT` |

`CHUMMER_UI_ROOT` must be the repository containing the orchestrator. The six
compatibility-tree roots must also resolve to the physical paths actually used
by the package plane (`chummer-core-engine`, `chummer.run-services`,
`chummer-ui-kit`, `chummer-hub-registry`, the Fleet media-factory checkout, and
the adjacent legacy checkout). Local package-plane project paths are derived
only from these seven validated source authorities.
The package plane clears inherited published-feed overrides, uses candidate-local
NuGet/DOTNET/feed state, holds the compatibility tree's shared package-plane
lock for the complete four-publish run, and invalidates only the known generated
reference assemblies after all authorities pass. This prevents a clean pinned
authority receipt from being paired with stale or concurrently replaced sibling
build outputs.

## Required release and incumbent-shelf identity

- `CHUMMER_PREVIEW_NIGHTLY_VERSION`: portable explicit version token.
- `CHUMMER_PREVIEW_NIGHTLY_PUBLISHED_AT`: explicit UTC RFC3339 timestamp.
- `CHUMMER_PREVIEW_NIGHTLY_CANDIDATE_DIR`: absolute
  `.nightly-run-<version>.candidate` path that does not exist.
- `CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR`: absolute sibling
  `nightly-run-<version>` path that does not exist.
- `CHUMMER_PREVIEW_NIGHTLY_RETAINED_SHELF_ROOT`: trusted local shelf containing
  `files/` and, when present, `startup-smoke/` and `signing/`.
- `CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_PATH` and
  `CHUMMER_PREVIEW_NIGHTLY_RETAINED_CANONICAL_SHA256`.
- `CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_PATH` and
  `CHUMMER_PREVIEW_NIGHTLY_RETAINED_RELEASES_SHA256`.

All incumbent files are copied before the four new tuples replace their exact
names. Manifest hashes and sizes are checked before copying. Seal also invokes
Run's `verify_release_shelf_replacement.py` against the incumbent canonical
manifest and the selected staged bytes, so an authoritative shelf contraction
cannot be sealed. The prepared-input receipt also records the complete incumbent
`files/` inventory. Seal rechecks its count, aggregate digest, and every exact
non-current or auxiliary byte, including permitted `.json` and `.sha256` files.

## Required pinned proof inputs

Each path must be an absolute local regular non-symlink file. Its matching
SHA-256 variable is mandatory. The files are copied into `proof/inputs` before
any build begins; later gates use only those copies.

| Path variable | SHA-256 variable |
| --- | --- |
| `CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH` | `CHUMMER_HUB_LOCAL_RELEASE_PROOF_SHA256` |
| `CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH` | `CHUMMER_UI_LOCALIZATION_RELEASE_GATE_SHA256` |
| `CHUMMER_UI_LOCAL_RELEASE_PROOF_PATH` | `CHUMMER_UI_LOCAL_RELEASE_PROOF_SHA256` |
| `CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_PATH` | `CHUMMER_BLAZOR_SELF_HOST_WORKBENCH_PROOF_SHA256` |
| `CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH` | `CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_SHA256` |
| `CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_PATH` | `CHUMMER_BLAZOR_BROWSER_LANE_PROOF_SET_SHA256` |
| `CHUMMER_UI_FLAGSHIP_RELEASE_GATE_PATH` | `CHUMMER_UI_FLAGSHIP_RELEASE_GATE_SHA256` |
| `CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_PATH` | `CHUMMER_DESKTOP_WORKFLOW_EXECUTION_GATE_SHA256` |
| `CHUMMER_UI_WORKFLOW_PARITY_PATH` | `CHUMMER_UI_WORKFLOW_PARITY_SHA256` |
| `CHUMMER_SR4_WORKFLOW_PARITY_PATH` | `CHUMMER_SR4_WORKFLOW_PARITY_SHA256` |
| `CHUMMER_SR6_WORKFLOW_PARITY_PATH` | `CHUMMER_SR6_WORKFLOW_PARITY_SHA256` |

At seal time the pinned Registry materializer parses the Hub and localization
proofs, requires them to equal the canonical manifest's embedded `releaseProof`,
and regenerates the exact Registry public-trust and boundary projections. The
pinned Presentation materializers then replay both Windows exit gates and
regenerate Windows release evidence and the release-build handoff. Non-fresh
proof or unsigned Windows evidence can seal only with canonical
`review_required` supportability and blocked public-trust posture. A matching
input hash never waives an expired, malformed, or blocked proof.

Reviewer authorization is not a caller-supplied stage input. The committed
finalization workflow runs in the protected `windows-visual-review` environment,
uses its repository reviewer variable, records `github.actor`, and rejects the
capture actor. Stage accepts that reviewer only after both workflow runs and the
finalized artifact are independently matched against GitHub's public Actions API.

## Prepare

After exporting the exact authorities and inputs:

```bash
bash scripts/build-preview-nightly-stage.sh prepare
```

The command builds and packages these exact tuples:

- `avalonia:windows:win-x64`
- `avalonia:linux:linux-x64`
- `blazor-desktop:windows:win-x64`
- `blazor-desktop:linux:linux-x64`

It runs per-installer startup smoke, generates the canonical and compatibility
manifests in stage-only mode, creates promotion evidence and the operator
handoff, verifies Windows bootstrap payload metadata, and preserves the Windows
download-mode compatibility receipts under
`proof/windows-compatibility-startup`. Success leaves only the hidden candidate
and explicitly reports that native evidence is still required.

## Native Windows evidence and seal

Dispatch the committed
`.github/workflows/preview-nightly-candidate-export.yml` from
`refs/heads/main` at the exact Presentation authority commit. Its immutable
artifact must contain exactly the canonical manifest, the two fixed Windows x64
bootstrap installers, the two fixed payload ZIPs, the deterministic
`PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json`, and the run-bound
`PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json`. Then dispatch
`.github/workflows/windows-native-evidence-capture.yml` for that exact artifact,
followed by
`.github/workflows/windows-native-evidence-finalize.yml` from the same pinned
Presentation commit. Each capture and finalization source binding must record
one unambiguous full source ref: either `refs/heads/<head_branch>` or
`refs/tags/<head_branch>`; bare refs are rejected. The Actions REST run path may
be the exact bare workflow path or that path qualified by the API head branch,
the claimed full ref, or the exact lowercase source/head SHA. No other path
shape or opposite ref kind is accepted. The second workflow requires a distinct
authenticated reviewer and emits one finalized artifact whose ZIP contains:

```text
native-evidence-finalized/
├── WINDOWS_NATIVE_CAPTURE.generated.json
├── WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json
├── WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json
├── WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json
├── candidate-provenance/
│   ├── PREVIEW_NIGHTLY_CANDIDATE_CONTENT_INVENTORY.generated.json
│   └── PREVIEW_NIGHTLY_CANDIDATE_EXPORT.generated.json
├── WINDOWS_INSTALLER_VISUAL_PROOF-avalonia-win-x64.generated.json
├── WINDOWS_INSTALLER_VISUAL_PROOF-blazor-desktop-win-x64.generated.json
├── startup-smoke/
│   ├── startup-smoke-avalonia-win-x64.receipt.json
│   ├── startup-smoke-blazor-desktop-win-x64.receipt.json
│   ├── windows-installer-progress-avalonia-win-x64.log
│   └── windows-installer-progress-blazor-desktop-win-x64.log
└── screenshots/ ... four distinct validated PNG captures
```

Stage requires the capture manifest's producer binding to use the Presentation
repository, fixed exporter workflow, `refs/heads/main`, exact Presentation
authority SHA, exact run ID/attempt/actor, and the exact API artifact ID, name,
creation/expiry timestamps, and lowercase API `sha256:` digest (stored in the
capture binding without the prefix). It reconstructs and rehashes both the
candidate handoff and authenticated-API contracts, validates
the copied exporter receipt and deterministic inventory, and compares their
five exact path/hash/size rows with the staged manifest, installers, and
payloads. Producer artifacts that are expired, paginated out of the first API
page, or no longer reported as a successful `workflow_dispatch` fail closed.

Both startup receipts must be bound to the candidate installer bytes and report
`executionEnvironment=native_windows` with verified native-host evidence. The
visual proofs must respectively target `avalonia:win-x64` and
`blazor-desktop:win-x64`, match the preview release version/channel and exact
installer SHA-256, and each include exactly one digest-bound `progress` and one
distinct `completion` screenshot using evidence-root-relative paths. Readability,
contrast, and clipping reviews must all pass under one independently authorized
reviewer per proof; capture mode must be `interactive` and human review must be
confirmed. Both download progress logs are bound into separate head-specific
desktop exit gates. Download the finalized artifact as the original ZIP (do not
repack it), export its absolute path as
`CHUMMER_PREVIEW_NIGHTLY_NATIVE_WINDOWS_EVIDENCE_ARCHIVE`, then run:

```bash
bash scripts/build-preview-nightly-stage.sh seal
```

Seal works on a private byte-identical sibling copy. Cleanup first moves only
the recorded directory device/inode into a private tombstone; a changed path is
left untouched. It reruns the native Windows smoke
verifier, both desktop exit gates, Windows
cross-evidence verifier, shelf-replacement verifier, artifact-scope verifier,
and complete-manifest verifier. Only then does it write the sealed inventory and
install the exact recorded directory with Linux `renameat2(RENAME_NOREPLACE)`.
A concurrently created destination is preserved, and an identity mismatch is
quarantined rather than exposed as `nightly-run-<version>`. The original
candidate is consumed through the same identity-bound tombstone only after the
installed target is rehashed and fully reverified. Any boundary mutation causes
only that newly installed inode to be quarantined and removed.

The resulting stage can be checked without source roots or proof inputs while
all three GitHub artifacts remain unexpired and the public Actions API is
reachable:

```bash
CHUMMER_PREVIEW_NIGHTLY_STAGE_DIR=/absolute/path/to/nightly-run-VERSION \
  bash scripts/build-preview-nightly-stage.sh verify
```

`verify` rederives release identity, authority receipts, retained-shelf and
`releases.json` bindings, downloaded-payload evidence, both native proof trees,
both exit gates, cross-evidence, promotion evidence, the Run dry-run candidate,
the seal-time Registry/Presentation source and output hashes, and the complete
byte inventory. It also re-queries the candidate producer, capture, and
finalization GitHub Actions runs and artifacts, revalidates the exact five local
candidate bytes against the retained exporter contracts, and rechecks the
retained finalized ZIP against GitHub's `sha256:` artifact digest. It does not
re-execute external source repositories. Changing seal metadata or recomputing
only its inventory cannot bypass those checks. Passing `verify` authorizes
only an uploader dry-run; actual upload remains a distinct, credentialed,
operator-approved action.
