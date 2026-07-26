# Mac Codex Release Pipeline To chummer.run

Purpose: let a Mac host with Codex build a real macOS desktop artifact, prove it, and publish it to the `chummer.run` downloads shelf through the authenticated HTTP promotion route.

This runbook is intentionally grounded on the release scripts that already exist in `chummer6-ui`:

- `scripts/build-desktop-installer.sh`
- `scripts/run-desktop-startup-smoke.sh`
- `scripts/generate-releases-manifest.sh`
- `scripts/publish-download-bundle.sh`
- `scripts/publish-download-bundle-s3.sh`
- `scripts/verify-releases-manifest.sh`

## Current truth

1. `chummer.run` now exposes a dedicated authenticated bundle upload route at `https://chummer.run/api/internal/releases/bundles`.
2. The easiest signed-in operator handoff is `https://chummer.run/downloads/release-upload`, which mints a short-lived upload ticket and gives a one-line bootstrap command for the Mac shell.
3. Public macOS promotion is gated. A built `.dmg` is not enough. The public shelf must not show macOS until the lane has:
   - a signed `.dmg`
   - notarization evidence
   - startup smoke proof
   - release-truth promotion
4. The release bundle also carries `Samples/Legacy/Soma-Career.chum5`, so the installed app ships a real completed SR5 demo runner.

## Governed GitHub Actions evidence lane

`.github/workflows/macos-flagship-evidence.yml` is the non-publishing production evidence entry point. It consumes four exact caller-owned inputs:

1. canonical `chummer6-ui.macos-flagship-build-authority` version `2` JSON, valid for at most 24 hours and pinning the global candidate/generation IDs, every source commit, a one-run anti-replay nonce, and the N-1/live-root/selected-tuple SHA-256 values
2. the exact canonical `chummer.release-scope-decision/v1` bytes named by that authority, approving only signed `avalonia`/`osx-arm64` `open_public` evidence for the same release version
3. canonical `chummer6-ui.macos-predecessor-handoff` version `2` JSON binding the public N-1 manifest, its release timestamp, and notarized DMG
4. the exact UTF-8 bytes expected at `https://chummer.run/downloads/RELEASE_CHANNEL.generated.json`

The Ubuntu authority preflight, secretless hosted ARM64 proof job, and
protected hosted ARM64 evidence job independently fetch that exact URL with
redirects and content decoding disabled and
identity/no-cache headers. Each fetch must be 2xx, bounded, strict UTF-8, and
byte-identical to the caller input. Duplicate keys, non-finite numbers,
contract/shape drift, a missing exact macOS tuple, or any digest change fails
closed. The shared platform-generic validator proves that the handoff is the
immediate macOS artifact selected by the public root; there is no first-release
bypass.

The native jobs are pinned to GitHub-hosted `macos-15` ARM64 runners. The
workflow rejects drift in `RUNNER_ARCH`, `RUNNER_ENVIRONMENT`, and `ImageOS`;
it validates and binds the observed `ImageVersion` into the capacity,
lifecycle, consumption, aggregate, and native-adapter receipts. Configure the
protected `macos-flagship-evidence` GitHub environment to require an independent
reviewer and disallow self-approval, then add:

- secrets `CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64` and `CHUMMER_MACOS_DEVELOPER_ID_P12_PASSWORD`
- secrets `CHUMMER_MACOS_NOTARY_KEY_P8_BASE64`, `CHUMMER_MACOS_NOTARY_KEY_ID`, and `CHUMMER_MACOS_NOTARY_ISSUER_ID`
- variables `CHUMMER_MACOS_DEVELOPER_ID_APPLICATION` and `CHUMMER_MACOS_TEAM_ID`
- lowercase 64-hex certificate pins `CHUMMER_MACOS_CERT_SHA256` and `CHUMMER_MACOS_CERT_SPKI_SHA256`
- base64-encoded RSA public key `CHUMMER_MACOS_ESCROW_RECIPIENT_PUBLIC_KEY_PEM_BASE64`
- lowercase 64-hex DER-SPKI pin `CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256`

Before the protected job can start, a separate job with no GitHub environment
or protected-secret references checks out the exact `hubCommit`, runs the Hub
bootstrap in stage-only mode, mounts the unsigned DMG read-only, copies its
ARM64 app into an isolated Applications-equivalent root, executes the real
`--startup-smoke` path, uninstalls the app, removes every build/stage byte, and
uploads receipts only. The protected job independently proves its own hosted
runner capacity and verifies the exact Actions artifact ID/digest plus every
source receipt before its first Apple or escrow secret reference.

The protected job then repeats the stage-only build. Only after proof
consumption and predecessor download does it import signing material into a
temporary keychain. Raw P12/P8 files are removed immediately after import, and
the keychain is restored, locked, and deleted before any predecessor or
candidate executable runs. The job proves DMG and app Gatekeeper acceptance,
explicit quarantine assessment, isolated-Applications install, installed core
startup, uninstall, N-1 candidate download/integrity validation, the
platform-required manual DMG handoff, candidate completion, second startup,
and final uninstall.

The bootstrap supplies `CHUMMER_HUB_LOCAL_PROOF_MUTATION_LOCK_PATH` as a fresh owned path beneath the per-run build root. This is the portable macOS proof-lock route; the container-only `/docker` fallback is never used.

The lane has no release upload credential or publication permission. Because Actions artifacts in a public repository are a distribution surface, it never uploads the plaintext DMG. Instead, `scripts/macos_flagship_candidate_escrow.mjs seal` encrypts the exact signed/notarized DMG with a random AES-256-GCM key and wraps that key to a protected RSA-OAEP-SHA256 recipient. The workflow independently validates the canonical escrow receipt and ciphertext, immediately unlinks the exact signed DMG, removes the unsigned stage, build, predecessor, and now-empty evidence-files roots, and asserts all governed plaintext paths are absent before either artifact upload. The final `always()` cleanup repeats removal and absence checks as defense in depth. The Actions artifact contains receipts plus ciphertext only and is retained for 30 days. The v3 coordinator handoff records `candidateBytesRetained: true` (encrypted custody), `candidatePlaintextDistributed: false`, the ciphertext digest and size, the recipient SPKI pin, the N-1/live-root/selected-tuple authority, and the exact GitHub runtime claims.

The original `actor`, current `triggeringActor`, `runId`, and `runAttempt` are bound from authority validation through signing identity, aggregate evidence, the native adapter, escrow AAD, and handoff. The explicit rerun policy is `same-actor-only`: a rerun is accepted only when `github.triggering_actor == github.actor`; a different operator cannot inherit the original actor's authority. That handoff is structural evidence, not self-authenticating authority: it explicitly records `provenanceAuthenticated: false`. A separate protected downstream workflow must authenticate the run, attempt, source SHA/ref, workflow, original actor, triggering actor, artifact ID/name/digest, and environment through the GitHub API before it may decrypt. It must then use `scripts/macos_flagship_candidate_escrow.mjs open` with the pinned private key and revalidate the recovered plaintext SHA-256 and size. The global assembler independently hashes those recovered bytes and cross-binds them to the signing/notary/native-E2E receipts. This workflow still cannot stage a public generation, mutate Registry authority, activate `CURRENT`, or publish downloads.

`FLAGSHIP_NATIVE_E2E.macos.generated.json` is the adapter consumed by the global
flagship assembler. It emits the exact
`chummer6-ui.flagship-native-e2e.macos.v1` candidate, artifact, native runner,
clean-install, installed startup workflow, predecessor-to-candidate update, and
live-predecessor authority schema. All three checks point to the aggregate
`chummer6-ui.macos-flagship-evidence` v3 receipt. That aggregate carries an
exact `{path,sha256,sizeBytes}` reference for every authority input, including
the pre-secret hosted-native proof consumption receipt, exact live public root,
v2 signing receipt, signing-identity receipt, raw accepted notary result,
authority receipt, inventory, both startup receipts, and predecessor
verification. Paths use the portable `receipts/<file>` layout; the candidate
packager must preserve that layout beside the global candidate manifest.

`macos-signing-notarization-identity.json` binds the exact DMG digest to the protected Developer ID identity, team ID, certificate SHA-256, certificate SPKI SHA-256, accepted Apple notary submission ID/result, existing v2 signing receipt, and workflow authority. The aggregate evidence additionally binds stapler validation, Gatekeeper enabled state, DMG/app Gatekeeper checks, native `arm64` execution, N-1 state transitions, and the fail-closed nonpublishing posture. Coordinators can import `validate_aggregate_receipt(...)` from `scripts/macos_flagship_evidence.py` and supply the referenced bytes plus their trusted candidate, global identity, GitHub provenance, certificate pins, Developer ID, and team ID. The pure validator follows and hashes every reference; it does not infer filenames.

macOS intentionally does not auto-apply a downloaded DMG. The N-1 test therefore requires the app to download and hash the candidate, record `macos_manual_install_required`, and retain the exact pending installer identity. The job then performs the same Gatekeeper-visible manual replacement a user must perform and verifies that the candidate clears the pending state on launch. Until a signed, notarized public macOS predecessor exists, that N-1 handoff is an explicit external blocker rather than a skipped test.

The release coordinator is responsible for selecting the immediate prior public macOS release. The build authority must carry an immutable `predecessorSelectionAuthority` containing both release versions and the exact predecessor-handoff SHA-256; the protected independent reviewer approves that selection before the native job can start.

## Encrypted candidate custody

Generate the escrow key outside the repository and outside the macOS evidence runner. RSA must be 3072-8192 bits with exponent 65537; 4096 bits is the recommended operator default. Keep the private key in a separately protected assembly/publication authority and expose only its public key to `macos-flagship-evidence`.

```bash
umask 077
openssl genpkey \
  -algorithm RSA \
  -pkeyopt rsa_keygen_bits:4096 \
  -aes-256-cbc \
  -out chummer-macos-escrow-private.pem
openssl pkey \
  -in chummer-macos-escrow-private.pem \
  -pubout \
  -out chummer-macos-escrow-public.pem
base64 <chummer-macos-escrow-public.pem | tr -d '\n'
openssl pkey \
  -pubin \
  -in chummer-macos-escrow-public.pem \
  -outform DER |
  shasum -a 256
```

Store the one-line base64 result in `CHUMMER_MACOS_ESCROW_RECIPIENT_PUBLIC_KEY_PEM_BASE64` and the lowercase digest in `CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256`. Changing either is an authority rotation and requires the protected environment review process. Never put the private key or its passphrase in the evidence environment.

After a protected downstream workflow has authenticated the workflow run and downloaded an artifact whose provider digest equals the v3 handoff, recover the exact DMG into a new output path:

```bash
export CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PASSPHRASE='read-from-protected-secret-store'
node scripts/macos_flagship_candidate_escrow.mjs open \
  --receipt extracted/escrow/MACOS_FLAGSHIP_CANDIDATE_ESCROW.generated.json \
  --ciphertext extracted/escrow/chummer-avalonia-osx-arm64-installer.dmg.aes256gcm \
  --private-key /protected/chummer-macos-escrow-private.pem \
  --expected-recipient-spki-sha256 "$CHUMMER_MACOS_ESCROW_RECIPIENT_SPKI_SHA256" \
  --output candidate/files/chummer-avalonia-osx-arm64-installer.dmg
unset CHUMMER_MACOS_ESCROW_PRIVATE_KEY_PASSPHRASE
```

`open` verifies the canonical receipt, RSA authority and pin, OAEP label, AES-GCM tag, ciphertext digest/size, and recovered plaintext digest/size before atomically exposing the output. A mismatch removes the partial plaintext. It performs no network, upload, publication, Registry, or activation operation.

## Remaining external authorities and blockers

The checked-in lane deliberately cannot create or self-approve any of these prerequisites:

1. A protected `macos-flagship-evidence` GitHub environment with required independent review and self-approval disabled.
2. Available GitHub-hosted `macos-15` ARM64 capacity with at least 20 GiB free after the bounded Xcode cleanup and the pinned .NET/Node/native toolchain.
3. The Developer ID P12, App Store Connect notary key, identity/team variables, and certificate/SPKI pins listed above.
4. An RSA escrow recipient public key and SPKI pin in the evidence environment, with the matching private key held only by a separate protected downstream assembly/publication authority. No downstream private-key environment or provider-API authenticator is created by this workflow.
5. A signed, notarized, publicly fetchable immediate macOS N-1 release plus its exact canonical predecessor handoff. Without it, the required update test cannot run and must not be skipped.
6. Fresh canonical build-authority and scope-decision bytes that pin `main`, the exact workflow SHA, every source commit, the anti-replay nonce, the N-1 selection, candidate/generation identity, and an independent scope approver.
7. A protected downstream workflow that authenticates GitHub provider provenance, verifies the Actions artifact archive digest, decrypts the escrow, constructs the global candidate layout, and runs the global assembler before any separate publication transaction.

Until all seven exist and the native job passes, macOS is not releasable and the global flagship must remain blocked.

## Recommended architecture

Use the fixed GitHub-hosted `macos-15` ARM64 lane for governed flagship
evidence. Keep ad-hoc or self-hosted shells outside the evidence authority.

Why:

1. macOS signing and notarization must happen on a Mac with Apple credentials available.
2. the existing workflow shape already matches the repo scripts
3. Codex can maintain the scripts and the runner can execute them repeatedly

Recommended topology:

1. Secretless hosted runner checks out `chummer6-ui` and the exact Hub bootstrap.
2. It builds and exercises `osx-arm64`, removes all app/build bytes, and uploads receipts only.
3. The independently approved protected hosted runner repeats the exact source build.
4. Runner packages the `.dmg`.
5. Runner codesigns, notarizes, and staples the `.dmg`.
6. Runner runs startup smoke on the notarized `.dmg`.
7. Runner stages the desktop bundle under `dist/`.
8. Runner publishes the bundle either through the controlled portal downloads filesystem root or the authenticated HTTP upload-session lane. The legacy fixed-key object-storage publisher is disabled fail-closed.
9. Runner verifies both the deployed manifest and the live `https://chummer.run/downloads/releases.json`.

## Mac prerequisites

Install these on the Mac host:

1. Xcode Command Line Tools
2. `.NET 10` SDK
3. `git`
4. `python3`
5. `jq`
6. `hdiutil` (ships with macOS)
7. Apple signing identity in the keychain
8. Apple notarization credentials stored as a `notarytool` keychain profile

Local-preview fallback:
1. `scripts/prepare-macos-signing-keychain.sh` can now bootstrap a persistent local code-signing identity when no hosted P12 is configured.
2. The default persistent keychain path is `~/Library/Keychains/chummer-signing.keychain-db`.
3. The local bootstrap path is intended for preview/dev continuity on a stable Mac host; public signing/notarization lanes should still use a real Apple identity and notarization credentials.

Example one-time notarization profile setup:

```bash
xcrun notarytool store-credentials "chummer-notary" \
  --apple-id "YOUR_APPLE_ID" \
  --team-id "YOUR_TEAM_ID" \
  --password "YOUR_APP_SPECIFIC_PASSWORD"
```

## Required secrets and variables

The Mac runner needs:

1. repo access for `chummer6-ui`
2. repo access for:
   - `ArchonMegalon/chummer6-core`
   - `ArchonMegalon/chummer6-hub`
   - `ArchonMegalon/chummer6-ui-kit`
3. Apple signing identity name
4. Apple team id
5. notarytool keychain profile name
6. publish target, either a controlled filesystem path on the server or authenticated HTTP upload-session credentials

Suggested env vars:

```bash
export CHUMMER_APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID)"
export CHUMMER_TEAM_ID="TEAMID"
export CHUMMER_NOTARY_PROFILE="chummer-notary"
export CHUMMER_RELEASE_CHANNEL="preview"
export CHUMMER_RELEASE_VERSION="run-$(date -u +%Y%m%d-%H%M%S)"
export CHUMMER_MAC_RELEASE_TMPDIR="$HOME/chummer-release-tmp"
export CHUMMER_DESKTOP_INSTALLER_TMPDIR="$CHUMMER_MAC_RELEASE_TMPDIR/desktop-installer"
# Optional local-preview fallback when no P12 is configured:
export CHUMMER_MAC_KEYCHAIN_PATH="$HOME/Library/Keychains/chummer-signing.keychain-db"
export CHUMMER_MAC_LOCAL_KEYCHAIN_PASSWORD="chummer-local-signing"
export CHUMMER_MAC_LOCAL_CERT_COMMON_NAME="Chummer Local Code Signing"
```

Temp-root note:

1. `scripts/build-desktop-installer.sh` now honors `CHUMMER_DESKTOP_INSTALLER_TMPDIR` and otherwise falls back to `${TMPDIR:-$DIST_DIR/tmp}` for `hdiutil`.
2. Point `CHUMMER_MAC_RELEASE_TMPDIR` at a workspace-backed path on the target SSD when the default temp volume is not the right disk for DMG creation.
3. Override `CHUMMER_DESKTOP_INSTALLER_TMPDIR` separately only when installer-image temp files must live on a different volume.

## Checkout layout

Use the same layout the existing workflow already expects:

```bash
mkdir -p ~/work/chummer-release
cd ~/work/chummer-release

git clone git@github.com:ArchonMegalon/chummer6-ui.git r
git clone git@github.com:ArchonMegalon/chummer6-core.git .c/core
git clone git@github.com:ArchonMegalon/chummer6-hub.git .c/hub
git clone git@github.com:ArchonMegalon/chummer6-ui-kit.git .c/ui

cd r
```

If you need pinned refs:

```bash
git -C .c/core checkout fleet/core
git -C .c/hub checkout main
git -C .c/ui checkout fleet/ui-kit
```

## Build, package, sign, notarize, smoke

Use the repo’s existing build and smoke flow, then add the missing Apple signing/notary layer around it.

Example for Avalonia on Apple Silicon:

```bash
set -euo pipefail

export CHUMMER_LOCAL_CONTRACTS_PROJECT="$PWD/../.c/core/Chummer.Contracts/Chummer.Contracts.csproj"
export CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT="$PWD/../.c/hub/Chummer.Run.Contracts/Chummer.Run.Contracts.csproj"
export CHUMMER_LOCAL_UI_KIT_PROJECT="$PWD/../.c/ui/src/Chummer.Ui.Kit/Chummer.Ui.Kit.csproj"

RID="osx-arm64"
APP="avalonia"
PROJECT="Chummer.Avalonia/Chummer.Avalonia.csproj"
LAUNCH_TARGET="Chummer.Avalonia"
OUT_DIR="out/$APP/$RID"
DIST_DIR="dist"

dotnet restore "$PROJECT" \
  -r "$RID" \
  -p:UseChummerEngineContractsLocalFeed=false \
  -p:ChummerUseLocalCompatibilityTree=true \
  -p:ChummerLocalContractsProject="$CHUMMER_LOCAL_CONTRACTS_PROJECT" \
  -p:ChummerLocalRunContractsProject="$CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT" \
  -p:ChummerLocalUiKitProject="$CHUMMER_LOCAL_UI_KIT_PROJECT"

dotnet publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:UseChummerEngineContractsLocalFeed=false \
  -p:ChummerUseLocalCompatibilityTree=true \
  -p:ChummerLocalContractsProject="$CHUMMER_LOCAL_CONTRACTS_PROJECT" \
  -p:ChummerLocalRunContractsProject="$CHUMMER_LOCAL_RUN_CONTRACTS_PROJECT" \
  -p:ChummerLocalUiKitProject="$CHUMMER_LOCAL_UI_KIT_PROJECT" \
  -p:ChummerDesktopReleaseVersion="$CHUMMER_RELEASE_VERSION" \
  -p:ChummerDesktopReleaseChannel="$CHUMMER_RELEASE_CHANNEL" \
  -o "$OUT_DIR"

bash scripts/build-desktop-installer.sh \
  "$OUT_DIR" \
  "$APP" \
  "$RID" \
  "$LAUNCH_TARGET" \
  "$DIST_DIR" \
  "$CHUMMER_RELEASE_VERSION"
```

If this step fails with `hdiutil: create failed - No space left on device`, keep the publish output and rerun with `CHUMMER_MAC_RELEASE_TMPDIR` pointed at a workspace-backed path on the target SSD. Clear unneeded old `run-*` directories under the same parent before retrying.

At this point you will have a DMG like:

```text
dist/chummer-avalonia-osx-arm64-installer.dmg
```

Now sign and notarize it:

```bash
DMG="dist/chummer-avalonia-osx-arm64-installer.dmg"
MOUNT_DIR="$(mktemp -d)"
hdiutil attach -nobrowse -mountpoint "$MOUNT_DIR" "$DMG"
APP_BUNDLE="$(find "$MOUNT_DIR" -maxdepth 1 -type d -name '*.app' | head -n 1)"

codesign --force --deep --options runtime --timestamp \
  --sign "$CHUMMER_APP_SIGN_IDENTITY" \
  "$APP_BUNDLE"

hdiutil detach "$MOUNT_DIR"

codesign --force --timestamp \
  --sign "$CHUMMER_APP_SIGN_IDENTITY" \
  "$DMG"

xcrun notarytool submit "$DMG" \
  --keychain-profile "$CHUMMER_NOTARY_PROFILE" \
  --wait

xcrun stapler staple "$DMG"
```

Then run startup smoke on the notarized artifact:

```bash
mkdir -p dist/startup-smoke

CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHUMMER_RELEASE_CHANNEL" \
CHUMMER_DESKTOP_RELEASE_VERSION="$CHUMMER_RELEASE_VERSION" \
CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="mac-codex-runner" \
bash scripts/run-desktop-startup-smoke.sh \
  "$DMG" \
  "$APP" \
  "$RID" \
  "$LAUNCH_TARGET" \
  "dist/startup-smoke" \
  "$CHUMMER_RELEASE_VERSION"
```

Required outcome:

1. DMG exists
2. notarization succeeds
3. stapling succeeds
4. startup smoke produces a receipt, not a regression packet

## Stage the bundle

The downloads shelf expects the normal desktop bundle layout:

```text
dist/
  files/
  releases.json
  RELEASE_CHANNEL.generated.json
  proof/
    build-provenance/
      v1/
        invocations/
        sbom/
```

For a Mac shelf, `proof/build-provenance/v1` is part of the release candidate, not optional side evidence. It must contain the governed invocation receipts and SBOM material for the exact artifact bytes named by both manifests. Do not replace this directory with a hand-written receipt or point the publisher at an accept-all validator.

Move artifacts into `dist/files` and materialize the manifests:

```bash
mkdir -p dist/files
mv dist/chummer-avalonia-osx-arm64-installer.dmg dist/files/

RELEASE_PUBLISHED_AT="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

DOWNLOADS_DIR="dist/files" \
MANIFEST_PATH="dist/releases.json" \
PORTAL_MANIFEST_PATH="dist/releases.json" \
RELEASE_VERSION="$CHUMMER_RELEASE_VERSION" \
RELEASE_CHANNEL="$CHUMMER_RELEASE_CHANNEL" \
RELEASE_PUBLISHED_AT="$RELEASE_PUBLISHED_AT" \
bash scripts/generate-releases-manifest.sh
```

Before manifest generation, materialize a fresh local Hub release proof from the checked-out
`chummer6-hub` compatibility tree and export it as the default proof source:

```bash
python3 ../.c/hub/scripts/materialize_hub_local_release_proof.py \
  ../.c/hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json \
  https://chummer.run \
  docker-compose.yml \
  120 \
  true

export CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH="$PWD/../.c/hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
```

Then materialize the manifests. `generate-releases-manifest.sh` now honors
`CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH` directly, so you do not need a second proof override if
that export is present:

```bash
DOWNLOADS_DIR="dist/files" \
MANIFEST_PATH="dist/releases.json" \
PORTAL_MANIFEST_PATH="dist/releases.json" \
RELEASE_VERSION="$CHUMMER_RELEASE_VERSION" \
RELEASE_CHANNEL="$CHUMMER_RELEASE_CHANNEL" \
RELEASE_PUBLISHED_AT="$RELEASE_PUBLISHED_AT" \
bash scripts/generate-releases-manifest.sh
```

If you need to pass the proof path explicitly for a one-off shell, use either variable:

```bash
DOWNLOADS_DIR="dist/files" \
MANIFEST_PATH="dist/releases.json" \
PORTAL_MANIFEST_PATH="dist/releases.json" \
RELEASE_VERSION="$CHUMMER_RELEASE_VERSION" \
RELEASE_CHANNEL="$CHUMMER_RELEASE_CHANNEL" \
RELEASE_PUBLISHED_AT="$RELEASE_PUBLISHED_AT" \
CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH="$PWD/../.c/hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json" \
bash scripts/generate-releases-manifest.sh
```

## Publish to chummer.run

### Supported mode A: filesystem publish

Use this when the deploy job can write directly to the server-side downloads root.

Current live topology uses a downloads root equivalent to:

```text
/docker/chummer5a/Docker/Downloads
```

From the Mac, that usually means:

1. sync `dist/` to a staging path on the server with `rsync` or `scp`
2. run `scripts/publish-download-bundle.sh` on the server against the real downloads root

Example:

```bash
rsync -avz dist/ release@YOUR_SERVER:/tmp/chummer-mac-release/

ssh release@YOUR_SERVER <<'EOF'
set -euo pipefail
cd /docker/chummercomplete/chummer6-ui
bash scripts/publish-download-bundle.sh \
  /tmp/chummer-mac-release \
  /docker/chummer5a/Docker/Downloads
EOF
```

### Disabled mode B: object storage publish

This mode is disabled. `scripts/publish-download-bundle-s3.sh` exits `78` without invoking a validator, generator, mirror sync, or AWS command. Use supported mode A or the authenticated HTTP upload-session lane.

To inspect the refusal, execute `./scripts/publish-download-bundle-s3.sh` (or `/bin/bash -p scripts/publish-download-bundle-s3.sh`) directly. Do not wrap it in ordinary `bash`: caller-controlled `BASH_ENV` or exported functions can execute before any script body. `RUNBOOK_MODE=downloads-sync-s3 ./scripts/runbook.sh` uses the same absolute privileged-Bash boundary.

The fixed-key S3/R2 layout cannot preserve the old shelf across every artifact, proof, manifest, canonical-manifest, or latest-alias failure, and synchronization does not establish exact remote bytes. Re-enabling it requires immutable versioned artifact/proof keys, checksum-and-size verified remote inventory, and a single atomic canonical pointer that the portal understands. This is a serving-topology migration, not an operator override.

## Verify live publication

These checks must pass before you claim public promotion:

```bash
bash scripts/verify-releases-manifest.sh dist/releases.json
bash scripts/verify-releases-manifest.sh https://chummer.run/downloads/releases.json
curl -fsS https://chummer.run/downloads/
curl -fsS https://chummer.run/downloads/releases.json
curl -I https://chummer.run/downloads/files/chummer-avalonia-osx-arm64-installer.dmg
```

For public mac promotion, the live manifest must actually contain the mac artifact and the portal must return `200` or `206` for the `.dmg`.

## If you want a real upload endpoint on chummer.run

That is not the current supported path, but the right shape is small:

1. `POST /api/v1/releases/intakes`
   - authenticated
   - declares `version`, `channel`, `artifactId`, `sha256`, `sizeBytes`
   - returns an `intakeId` plus upload target
2. upload bytes
   - either a presigned object-storage URL
   - or a server-side staging path owned by the intake service
3. `POST /api/v1/releases/intakes/{id}/finalize`
   - verifies sha256 and file name
   - regenerates `RELEASE_CHANNEL.generated.json` and `releases.json`
   - writes artifacts into the same storage root the portal already serves
   - runs `verify-releases-manifest.sh` against the live URL
   - only then flips the intake to `published`

Important rule:

Do not stream directly into the live public downloads directory and call that “published”. The intake endpoint must stage, verify, and promote atomically, or you will eventually publish a broken manifest or a half-written artifact.

## Legacy local Mac release wrapper

This local wrapper is for operator preview or legacy manual release work; it is
not accepted as governed flagship evidence. Run it only from a controlled Mac
host with the compatibility tree expected by the UI build scripts:

```bash
bash scripts/your-macos-public-release-wrapper.sh
```

## The clean exit condition

You are done when all of these are true:

1. the `.dmg` is signed
2. notarization succeeded
3. startup smoke passed on macOS
4. the generated manifest contains the mac artifact
5. the exact staged `proof/build-provenance/v1` validates against the artifact bytes and is published with the shelf
6. `https://chummer.run/downloads/releases.json` shows the mac artifact
7. `https://chummer.run/downloads/files/<your dmg>` is fetchable
8. public release truth no longer says mac is withheld
