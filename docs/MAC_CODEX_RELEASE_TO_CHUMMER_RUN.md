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

## Recommended architecture

Use a self-hosted runner on the Mac, not an ad-hoc manual shell.

Why:

1. macOS signing and notarization must happen on a Mac with Apple credentials available.
2. the existing workflow shape already matches the repo scripts
3. Codex can maintain the scripts and the runner can execute them repeatedly

Recommended topology:

1. Mac runner checks out `chummer6-ui`.
2. Runner checks out the compatibility trees into `.c/core`, `.c/hub`, and `.c/ui`.
3. Runner publishes the desktop head for `osx-arm64` or `osx-x64`.
4. Runner packages the `.dmg`.
5. Runner codesigns, notarizes, and staples the `.dmg`.
6. Runner runs startup smoke on the notarized `.dmg`.
7. Runner stages the desktop bundle under `dist/`.
8. Runner publishes the bundle either:
   - to the portal downloads filesystem root, or
   - to object storage
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
6. publish target, either:
   - a filesystem path on the server, or
   - an object storage URI and credentials

Suggested env vars:

```bash
export CHUMMER_APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID)"
export CHUMMER_TEAM_ID="TEAMID"
export CHUMMER_NOTARY_PROFILE="chummer-notary"
export CHUMMER_RELEASE_CHANNEL="preview"
export CHUMMER_RELEASE_VERSION="run-$(date -u +%Y%m%d-%H%M%S)"
```

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
```

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

If you have release proof JSON, pass it too:

```bash
DOWNLOADS_DIR="dist/files" \
MANIFEST_PATH="dist/releases.json" \
PORTAL_MANIFEST_PATH="dist/releases.json" \
RELEASE_VERSION="$CHUMMER_RELEASE_VERSION" \
RELEASE_CHANNEL="$CHUMMER_RELEASE_CHANNEL" \
RELEASE_PUBLISHED_AT="$RELEASE_PUBLISHED_AT" \
RELEASE_PROOF_PATH=".codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json" \
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

### Supported mode B: object storage publish

Use this when the portal serves downloads from S3/R2-compatible storage.

```bash
export CHUMMER_PORTAL_DOWNLOADS_S3_URI="s3://bucket/path"
export CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL="https://chummer.run/downloads/releases.json"
export AWS_ACCESS_KEY_ID="..."
export AWS_SECRET_ACCESS_KEY="..."
export AWS_DEFAULT_REGION="us-east-1"

bash scripts/publish-download-bundle-s3.sh dist
```

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

## Minimal GitHub Actions shape for the Mac

If you prefer CI, adapt the existing workflow and pin the mac build job to a self-hosted Mac:

```yaml
jobs:
  build-macos-public:
    runs-on: [self-hosted, macOS, ARM64]
    steps:
      - uses: actions/checkout@v4
        with:
          path: r
      - uses: actions/checkout@v4
        with:
          repository: ArchonMegalon/chummer6-core
          ref: fleet/core
          path: .c/core
      - uses: actions/checkout@v4
        with:
          repository: ArchonMegalon/chummer6-hub
          ref: main
          path: .c/hub
      - uses: actions/checkout@v4
        with:
          repository: ArchonMegalon/chummer6-ui-kit
          ref: fleet/ui-kit
          path: .c/ui
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Build, sign, notarize, smoke, publish
        working-directory: r
        run: bash scripts/your-macos-public-release-wrapper.sh
```

The wrapper script should do exactly the steps from this runbook and fail on the first broken gate.

## The clean exit condition

You are done when all of these are true:

1. the `.dmg` is signed
2. notarization succeeded
3. startup smoke passed on macOS
4. the generated manifest contains the mac artifact
5. `https://chummer.run/downloads/releases.json` shows the mac artifact
6. `https://chummer.run/downloads/files/<your dmg>` is fetchable
7. public release truth no longer says mac is withheld
