#!/usr/bin/env bash
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"

RID="${1:?RID is required}"
RELEASE_VERSION="${2:?release version is required}"
OUTPUT_ROOT="${3:?output root is required}"
AUTHORITY_ROOT="${4:?authority checkout root is required}"
RUNNER_LABEL="${5:?runner label is required}"
SDK_RECEIPT="${6:?SDK receipt is required}"

case "$RID" in
  osx-arm64)
    EXPECTED_MACHINE="arm64"
    EXPECTED_RUNNER_ARCH="ARM64"
    EXPECTED_RUNNER_LABEL="macos-15"
    ;;
  osx-x64)
    EXPECTED_MACHINE="x86_64"
    EXPECTED_RUNNER_ARCH="X64"
    EXPECTED_RUNNER_LABEL="macos-15-intel"
    ;;
  *)
    echo "Unsupported native macOS RID: $RID" >&2
    exit 2
    ;;
esac

[[ "$(uname -s)" == "Darwin" ]] || {
  echo "Unsigned macOS artifacts must be built on a native macOS host." >&2
  exit 2
}
NATIVE_MACHINE="$(uname -m)"
[[ "$NATIVE_MACHINE" == "$EXPECTED_MACHINE" ]] || {
  echo "Native machine $NATIVE_MACHINE does not match $RID." >&2
  exit 2
}
[[ "$RUNNER_LABEL" == "$EXPECTED_RUNNER_LABEL" ]] || {
  echo "Runner label $RUNNER_LABEL does not match $RID." >&2
  exit 2
}
[[ "${RUNNER_OS:-}" == "macOS" \
  && "${RUNNER_ARCH:-}" == "$EXPECTED_RUNNER_ARCH" \
  && "${RUNNER_ENVIRONMENT:-}" == "github-hosted" \
  && "${ImageOS:-}" == "macos15" \
  && -n "${ImageVersion:-}" ]] || {
  echo "GitHub-hosted macOS runner identity is absent or mismatched." >&2
  exit 2
}
[[ "${GITHUB_EVENT_NAME:-}" == "workflow_dispatch" \
  && "${GITHUB_REPOSITORY:-}" == "ArchonMegalon/chummer6-ui" \
  && "${GITHUB_SHA:-}" == "$(git -C "$REPO_ROOT" rev-parse HEAD)" ]] || {
  echo "Manual GitHub source authority is absent or mismatched." >&2
  exit 2
}
[[ "$RELEASE_VERSION" == "0.0.0-ci.sha${GITHUB_SHA:0:12}" ]] || {
  echo "Internal version must be derived from the exact recipe SHA." >&2
  exit 2
}
[[ "$(dotnet --version)" == "10.0.103" ]] || {
  echo "The native build requires exact SDK 10.0.103." >&2
  exit 2
}
[[ "$OUTPUT_ROOT" == /* && "$AUTHORITY_ROOT" == /* && "$SDK_RECEIPT" == /* ]] || {
  echo "Output, authority, and SDK receipt paths must be absolute." >&2
  exit 2
}
[[ ! -e "$OUTPUT_ROOT" && ! -L "$OUTPUT_ROOT" ]] || {
  echo "Output root must be absent." >&2
  exit 2
}
[[ -f "$SDK_RECEIPT" && ! -L "$SDK_RECEIPT" ]] || {
  echo "Digest-locked SDK receipt is missing or linked." >&2
  exit 2
}

for forbidden_name in \
  CHUMMER_APP_SIGN_IDENTITY \
  CHUMMER_NOTARY_PROFILE \
  CHUMMER_MAC_APP_SIGN_IDENTITY \
  CHUMMER_MAC_NOTARY_PROFILE \
  CHUMMER_MACOS_DEVELOPER_ID_P12_BASE64 \
  CHUMMER_MACOS_NOTARY_KEY_P8_BASE64; do
  [[ -z "${!forbidden_name:-}" ]] || {
    echo "Unsigned build rejects signing or notarization authority: $forbidden_name" >&2
    exit 2
  }
done

while IFS= read -r environment_name; do
  normalized_environment_name="$(printf '%s' "$environment_name" | tr '[:upper:]' '[:lower:]')"
  case "$normalized_environment_name" in
    custombefore*props|customafter*props|custombefore*targets|customafter*targets|\
    directorybuildpropspath|directorybuildtargetspath|import*|\
    msbuildextensionspath*|msbuildprojectextensionspath|msbuildsdkspath|\
    msbuilduserextensionspath)
      echo "Unsigned macOS proof rejects ambient MSBuild authority: $environment_name" >&2
      exit 2
      ;;
  esac
done < <(compgen -e)

CORE_AUTHORITY="$AUTHORITY_ROOT/chummer-core-authority"
HUB_AUTHORITY="$AUTHORITY_ROOT/chummer.run-services"
UI_KIT_AUTHORITY="$AUTHORITY_ROOT/chummer-ui-kit"
OWNER_FEED="$AUTHORITY_ROOT/core-owner-feed-packet/feed"
OWNER_FEED_VALIDATION="$AUTHORITY_ROOT/core-owner-feed-packet/OWNER_CONTRACT_FEED.generated.json"
SOURCE_FEED="$AUTHORITY_ROOT/core-owner-feed-packet/source-feed"
SOURCE_FEED_VALIDATION="$AUTHORITY_ROOT/core-owner-feed-packet/LINUX_SOURCE_FEED.generated.json"
for authority_path in "$CORE_AUTHORITY" "$HUB_AUTHORITY" "$UI_KIT_AUTHORITY"; do
  [[ -d "$authority_path" && ! -L "$authority_path" ]] || {
    echo "Exact owner authority checkout is missing or linked: $authority_path" >&2
    exit 2
  }
done
[[ -d "$OWNER_FEED" && ! -L "$OWNER_FEED" \
  && -f "$OWNER_FEED_VALIDATION" && ! -L "$OWNER_FEED_VALIDATION" \
  && -d "$SOURCE_FEED" && ! -L "$SOURCE_FEED" \
  && -f "$SOURCE_FEED_VALIDATION" && ! -L "$SOURCE_FEED_VALIDATION" ]] || {
  echo "Validated cross-host Linux package packet is missing or linked." >&2
  exit 2
}

CONSUMER_PARENT="$(cd "$REPO_ROOT/.." && pwd -P)"
for sibling_name in chummer-core-engine chummer-core-authority chummer.run-services chummer-ui-kit chummer-hub-registry; do
  [[ ! -e "$CONSUMER_PARENT/$sibling_name" && ! -L "$CONSUMER_PARENT/$sibling_name" ]] || {
    echo "Fresh consumer checkout has a forbidden sibling fallback: $sibling_name" >&2
    exit 2
  }
done

WORK_ROOT=""
cleanup() {
  local status=$?
  set +e
  if [[ -n "${WORK_ROOT:-}" && -d "$WORK_ROOT" && ! -L "$WORK_ROOT" ]]; then
    case "$WORK_ROOT" in
      "${RUNNER_TEMP:?}"/chummer-unsigned-macos.*) rm -rf -- "$WORK_ROOT" ;;
      *) echo "Refusing to clean unexpected macOS proof path: $WORK_ROOT" >&2 ;;
    esac
  fi
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

WORK_ROOT="$(mktemp -d "${RUNNER_TEMP:?RUNNER_TEMP is required}/chummer-unsigned-macos.XXXXXXXX")"
mkdir -m 0700 "$OUTPUT_ROOT" "$OUTPUT_ROOT/files" "$OUTPUT_ROOT/receipts"
PUBLISH_ROOT="$WORK_ROOT/publish"
DIST_ROOT="$WORK_ROOT/dist"
SMOKE_ROOT="$WORK_ROOT/startup-smoke"
PACKAGE_FEED="$WORK_ROOT/package-feed"
PACKAGE_DOWNLOADS="$WORK_ROOT/package-downloads"
PACKAGE_CACHE="$WORK_ROOT/publish-package-cache"
PACK_CONFIG="$WORK_ROOT/PackagePlane.NuGet.Config"
PREPARE_RECEIPT="$WORK_ROOT/package-plane-prepare.json"
PACKAGE_MANIFEST="$WORK_ROOT/package-plane-manifest.json"
PACKAGE_RESOLUTION="$WORK_ROOT/package-resolution.json"
LOCK_PATH="$REPO_ROOT/config/unsigned-macos-package-plane.lock.json"
mkdir -m 0700 \
  "$PUBLISH_ROOT" \
  "$DIST_ROOT" \
  "$SMOKE_ROOT" \
  "$PACKAGE_CACHE" \
  "$WORK_ROOT/dotnet-home" \
  "$WORK_ROOT/tmp"

export DOTNET_CLI_HOME="$WORK_ROOT/dotnet-home"
export TMPDIR="$WORK_ROOT/tmp"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

python3 "$SCRIPT_DIR/prepare_unsigned_macos_package_plane.py" prepare-feed \
  --repo-root "$REPO_ROOT" \
  --lock "$LOCK_PATH" \
  --core-authority "$CORE_AUTHORITY" \
  --owner-feed "$OWNER_FEED" \
  --source-feed "$SOURCE_FEED" \
  --rid "$RID" \
  --download-root "$PACKAGE_DOWNLOADS" \
  --feed "$PACKAGE_FEED" \
  --pack-config "$PACK_CONFIG" \
  --output "$PREPARE_RECEIPT"

python3 "$SCRIPT_DIR/prepare_unsigned_macos_package_plane.py" seal-feed \
  --lock "$LOCK_PATH" \
  --core-authority "$CORE_AUTHORITY" \
  --hub-authority "$HUB_AUTHORITY" \
  --ui-kit-authority "$UI_KIT_AUTHORITY" \
  --rid "$RID" \
  --feed "$PACKAGE_FEED" \
  --pack-config "$PACK_CONFIG" \
  --prepare-receipt "$PREPARE_RECEIPT" \
  --output "$PACKAGE_MANIFEST"

export NUGET_PACKAGES="$PACKAGE_CACHE"
export CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=0
dotnet publish "$REPO_ROOT/Chummer.Avalonia/Chummer.Avalonia.csproj" \
  -c Release \
  -f net10.0 \
  -r "$RID" \
  --self-contained true \
  -p:ContinuousIntegrationBuild=true \
  -p:Deterministic=true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:ChummerDesktopReleaseVersion="$RELEASE_VERSION" \
  -p:ChummerDesktopReleaseChannel=preview \
  -p:ChummerUseLocalCompatibilityTree=false \
  -p:ChummerContractsPackageVersion=0.0.0-packageplane.candidate.sh7599f9f5d460 \
  -p:ChummerCampaignContractsPackageVersion=0.1.0-preview \
  -p:ChummerRunContractsPackageVersion=0.0.0-packageplane.20260721.1 \
  -p:ChummerHubRegistryContractsPackageVersion=0.0.0-packageplane.20260721.1 \
  -p:ChummerUiKitPackageVersion=0.1.0-preview \
  -p:ChummerCoreRuntimePackageVersion=0.0.0-packageplane.candidate.sh7599f9f5d460 \
  -p:RestoreSources="$PACKAGE_FEED" \
  -p:RestoreAdditionalProjectSources= \
  -p:RestoreConfigFile="$PACK_CONFIG" \
  -p:RestoreFallbackFolders= \
  -p:RestoreIgnoreFailedSources=false \
  -p:RestorePackagesPath="$PACKAGE_CACHE" \
  -p:DisableImplicitNuGetFallbackFolder=true \
  --configfile "$PACK_CONFIG" \
  --force \
  --no-cache \
  --output "$PUBLISH_ROOT" \
  --disable-build-servers \
  --nologo \
  -v minimal

[[ -f "$PUBLISH_ROOT/Chummer.Avalonia" && ! -L "$PUBLISH_ROOT/Chummer.Avalonia" ]] || {
  echo "Avalonia native launch target was not published." >&2
  exit 2
}

python3 "$SCRIPT_DIR/prepare_unsigned_macos_package_plane.py" verify-resolution \
  --lock "$LOCK_PATH" \
  --rid "$RID" \
  --feed "$PACKAGE_FEED" \
  --manifest "$PACKAGE_MANIFEST" \
  --assets "$REPO_ROOT/Chummer.Avalonia/obj/project.assets.json" \
  --package-cache "$PACKAGE_CACHE" \
  --published-executable "$PUBLISH_ROOT/Chummer.Avalonia" \
  --output "$PACKAGE_RESOLUTION"

export CHUMMER_MAC_SIGNING_REQUIRED=0
export CHUMMER_MAC_NOTARIZATION_REQUIRED=0
export CHUMMER_DESKTOP_RELEASE_CHANNEL=preview
export CHUMMER_MAC_SIGNING_RECEIPT_PATH="$DIST_ROOT/signing/signing-avalonia-$RID.receipt.json"
bash "$SCRIPT_DIR/build-desktop-installer.sh" \
  "$PUBLISH_ROOT" avalonia "$RID" Chummer.Avalonia "$DIST_ROOT" "$RELEASE_VERSION"

ARTIFACT_NAME="chummer-avalonia-$RID-installer.dmg"
ARTIFACT_PATH="$DIST_ROOT/$ARTIFACT_NAME"
[[ -f "$ARTIFACT_PATH" && ! -L "$ARTIFACT_PATH" ]] || {
  echo "Expected macOS DMG was not produced." >&2
  exit 2
}

export CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="github-hosted-$RUNNER_LABEL"
bash "$SCRIPT_DIR/run-desktop-startup-smoke.sh" \
  "$ARTIFACT_PATH" avalonia "$RID" Chummer.Avalonia "$SMOKE_ROOT" "$RELEASE_VERSION"

SIGNING_RECEIPT="$DIST_ROOT/signing/signing-avalonia-$RID.receipt.json"
STARTUP_RECEIPT="$SMOKE_ROOT/startup-smoke-avalonia-$RID.receipt.json"
install -m 0600 "$ARTIFACT_PATH" "$OUTPUT_ROOT/files/$ARTIFACT_NAME"
install -m 0600 "$SIGNING_RECEIPT" "$OUTPUT_ROOT/receipts/$(basename "$SIGNING_RECEIPT")"
install -m 0600 "$STARTUP_RECEIPT" "$OUTPUT_ROOT/receipts/$(basename "$STARTUP_RECEIPT")"
install -m 0600 "$SDK_RECEIPT" "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_SDK.generated.json"
install -m 0600 "$OWNER_FEED_VALIDATION" "$OUTPUT_ROOT/receipts/OWNER_CONTRACT_FEED.generated.json"
install -m 0600 "$SOURCE_FEED_VALIDATION" "$OUTPUT_ROOT/receipts/LINUX_SOURCE_FEED.generated.json"
install -m 0600 "$PACKAGE_MANIFEST" "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_PACKAGE_MANIFEST.generated.json"
install -m 0600 "$PACKAGE_RESOLUTION" "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_PACKAGE_RESOLUTION.generated.json"
if [[ -f "$SMOKE_ROOT/startup-smoke-avalonia-$RID.log" ]]; then
  install -m 0600 \
    "$SMOKE_ROOT/startup-smoke-avalonia-$RID.log" \
    "$OUTPUT_ROOT/receipts/startup-smoke-avalonia-$RID.log"
fi

export CHUMMER_MACOS_NATIVE_MACHINE="$NATIVE_MACHINE"
python3 "$SCRIPT_DIR/materialize_unsigned_macos_build_receipt.py" \
  --artifact "$OUTPUT_ROOT/files/$ARTIFACT_NAME" \
  --signing-receipt "$OUTPUT_ROOT/receipts/$(basename "$SIGNING_RECEIPT")" \
  --startup-receipt "$OUTPUT_ROOT/receipts/$(basename "$STARTUP_RECEIPT")" \
  --package-resolution "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_PACKAGE_RESOLUTION.generated.json" \
  --sdk-receipt "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_SDK.generated.json" \
  --source-repo "$REPO_ROOT" \
  --owner "chummer-core-authority=$CORE_AUTHORITY=c85ea198c19c149375913b44b304acd4d6353053" \
  --owner "chummer-ui-kit=$UI_KIT_AUTHORITY=d51ecd99cf72098d4adc8db0192bff7bf9fd8e61" \
  --owner "chummer.run-services=$HUB_AUTHORITY=9af3cec2620e87a3086e6ac503a5730763c3ce4c" \
  --rid "$RID" \
  --release-version "$RELEASE_VERSION" \
  --runner-label "$RUNNER_LABEL" \
  --output "$OUTPUT_ROOT/receipts/UNSIGNED_MACOS_NATIVE_BUILD.generated.json"

python3 - "$OUTPUT_ROOT" <<'PY'
from __future__ import annotations

import hashlib
import os
import stat
import sys
from pathlib import Path

root = Path(sys.argv[1])
rows: list[str] = []
for path in sorted(root.rglob("*")):
    if path.name == "SHA256SUMS":
        continue
    metadata = path.lstat()
    if path.is_symlink() or (not stat.S_ISDIR(metadata.st_mode) and not stat.S_ISREG(metadata.st_mode)):
        raise SystemExit("output contains a link or special entry")
    if not stat.S_ISREG(metadata.st_mode):
        continue
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    rows.append(f"{digest}  {path.relative_to(root).as_posix()}")
target = root / "SHA256SUMS"
descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
    stream.write("\n".join(rows) + "\n")
PY

[[ -z "$(git -C "$REPO_ROOT" status --porcelain=v1 --untracked-files=all)" ]] || {
  echo "UI proof checkout changed during the build." >&2
  exit 2
}
printf 'unsigned macOS build complete: %s\n' "$OUTPUT_ROOT"
