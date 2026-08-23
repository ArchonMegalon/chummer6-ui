#!/usr/bin/env bash
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"

RID="${1:?RID is required}"
RELEASE_VERSION="${2:?release version is required}"
OUTPUT_ROOT="${3:?output root is required}"
OWNER_ROOT="${4:?owner checkout root is required}"
RUNNER_LABEL="${5:?runner label is required}"

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
  echo "Release version must be derived from the exact source SHA." >&2
  exit 2
}
[[ "$OUTPUT_ROOT" == /* && "$OWNER_ROOT" == /* ]] || {
  echo "Output and owner roots must be absolute." >&2
  exit 2
}
[[ ! -e "$OUTPUT_ROOT" && ! -L "$OUTPUT_ROOT" ]] || {
  echo "Output root must be absent." >&2
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

declare -a OWNER_NAMES=(
  "chummer-core-engine"
  "chummer-hub-registry"
  "chummer-ui-kit"
  "chummer.run-services"
)
declare -a OWNER_COMMITS=(
  "60c964b1962877f358f60f2e906fd5633b4db340"
  "af9a7e19c3bf331e96411dfb8f9e7820a98cab29"
  "d51ecd99cf72098d4adc8db0192bff7bf9fd8e61"
  "7b8e17f198178fac1e7569cc9953d7081c566069"
)

WORKSPACE_PARENT="$(cd "$REPO_ROOT/.." && pwd -P)"
declare -a COMPATIBILITY_LINKS=()
WORK_ROOT=""
cleanup() {
  local status=$?
  set +e
  if [[ -n "${WORK_ROOT:-}" && -d "$WORK_ROOT" && ! -L "$WORK_ROOT" ]]; then
    rm -rf -- "$WORK_ROOT"
  fi
  local link_path
  for link_path in "${COMPATIBILITY_LINKS[@]}"; do
    if [[ -L "$link_path" ]]; then
      rm -f -- "$link_path"
    fi
  done
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

for ((owner_index = 0; owner_index < ${#OWNER_NAMES[@]}; owner_index++)); do
  owner_name="${OWNER_NAMES[$owner_index]}"
  owner_commit="${OWNER_COMMITS[$owner_index]}"
  owner_path="$OWNER_ROOT/$owner_name"
  [[ -d "$owner_path" && ! -L "$owner_path" ]] || {
    echo "Owner checkout is missing or linked: $owner_name" >&2
    exit 2
  }
  [[ "$(git -C "$owner_path" rev-parse HEAD)" == "$owner_commit" ]] || {
    echo "Owner checkout commit differs: $owner_name" >&2
    exit 2
  }
  [[ -z "$(git -C "$owner_path" status --porcelain=v1 --untracked-files=no)" ]] || {
    echo "Owner checkout is not clean: $owner_name" >&2
    exit 2
  }
  compatibility_path="$WORKSPACE_PARENT/$owner_name"
  [[ ! -e "$compatibility_path" && ! -L "$compatibility_path" ]] || {
    echo "Compatibility path already exists: $compatibility_path" >&2
    exit 2
  }
  ln -s "$owner_path" "$compatibility_path"
  COMPATIBILITY_LINKS+=("$compatibility_path")
done

WORK_ROOT="$(mktemp -d "${RUNNER_TEMP:?RUNNER_TEMP is required}/chummer-unsigned-macos.XXXXXXXX")"
mkdir -m 0700 "$OUTPUT_ROOT"
mkdir -m 0700 "$OUTPUT_ROOT/files" "$OUTPUT_ROOT/receipts"
PUBLISH_ROOT="$WORK_ROOT/publish"
DIST_ROOT="$WORK_ROOT/dist"
SMOKE_ROOT="$WORK_ROOT/startup-smoke"
mkdir -m 0700 "$PUBLISH_ROOT" "$DIST_ROOT" "$SMOKE_ROOT"

export CHUMMER_USE_LOCAL_COMPATIBILITY_TREE=1
export CHUMMER_VERIFY_MODE=slice
export CHUMMER_PACKAGE_PLANE_LOCK_ROOT="$WORK_ROOT/package-plane-locks"
export CHUMMER_ENGINE_CONTRACTS_FEED="$WORK_ROOT/engine-contracts-feed"
export NUGET_PACKAGES="$WORK_ROOT/nuget-packages"
export DOTNET_CLI_HOME="$WORK_ROOT/dotnet-home"
export TMPDIR="$WORK_ROOT/tmp"
mkdir -m 0700 \
  "$CHUMMER_PACKAGE_PLANE_LOCK_ROOT" \
  "$CHUMMER_ENGINE_CONTRACTS_FEED" \
  "$NUGET_PACKAGES" \
  "$DOTNET_CLI_HOME" \
  "$TMPDIR"

bash "$SCRIPT_DIR/ai/with-package-plane.sh" publish \
  Chummer.Avalonia/Chummer.Avalonia.csproj \
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
  --output "$PUBLISH_ROOT" \
  --disable-build-servers \
  --nologo \
  -v minimal

[[ -f "$PUBLISH_ROOT/Chummer.Avalonia" && ! -L "$PUBLISH_ROOT/Chummer.Avalonia" ]] || {
  echo "Avalonia native launch target was not published." >&2
  exit 2
}
published_architectures="$(lipo -archs "$PUBLISH_ROOT/Chummer.Avalonia")"
case " $published_architectures " in
  *" $EXPECTED_MACHINE "*) ;;
  *)
    echo "Published launch target does not contain $EXPECTED_MACHINE." >&2
    exit 2
    ;;
esac

export CHUMMER_MAC_SIGNING_REQUIRED=0
export CHUMMER_MAC_NOTARIZATION_REQUIRED=0
export CHUMMER_DESKTOP_RELEASE_CHANNEL=preview
export CHUMMER_MAC_SIGNING_RECEIPT_PATH="$DIST_ROOT/signing/signing-avalonia-$RID.receipt.json"
bash "$SCRIPT_DIR/build-desktop-installer.sh" \
  "$PUBLISH_ROOT" \
  avalonia \
  "$RID" \
  Chummer.Avalonia \
  "$DIST_ROOT" \
  "$RELEASE_VERSION"

ARTIFACT_NAME="chummer-avalonia-$RID-installer.dmg"
ARTIFACT_PATH="$DIST_ROOT/$ARTIFACT_NAME"
[[ -f "$ARTIFACT_PATH" && ! -L "$ARTIFACT_PATH" ]] || {
  echo "Expected macOS DMG was not produced." >&2
  exit 2
}

export CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="github-hosted-$RUNNER_LABEL"
bash "$SCRIPT_DIR/run-desktop-startup-smoke.sh" \
  "$ARTIFACT_PATH" \
  avalonia \
  "$RID" \
  Chummer.Avalonia \
  "$SMOKE_ROOT" \
  "$RELEASE_VERSION"

SIGNING_RECEIPT="$DIST_ROOT/signing/signing-avalonia-$RID.receipt.json"
STARTUP_RECEIPT="$SMOKE_ROOT/startup-smoke-avalonia-$RID.receipt.json"
PACKAGE_INVENTORY="$CHUMMER_ENGINE_CONTRACTS_FEED/chummer-owner-contracts.inventory.json"
install -m 0600 "$ARTIFACT_PATH" "$OUTPUT_ROOT/files/$ARTIFACT_NAME"
install -m 0600 "$SIGNING_RECEIPT" "$OUTPUT_ROOT/receipts/$(basename "$SIGNING_RECEIPT")"
install -m 0600 "$STARTUP_RECEIPT" "$OUTPUT_ROOT/receipts/$(basename "$STARTUP_RECEIPT")"
install -m 0600 "$PACKAGE_INVENTORY" "$OUTPUT_ROOT/receipts/$(basename "$PACKAGE_INVENTORY")"
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
  --package-inventory "$OUTPUT_ROOT/receipts/$(basename "$PACKAGE_INVENTORY")" \
  --source-repo "$REPO_ROOT" \
  --owner "chummer-core-engine=$OWNER_ROOT/chummer-core-engine=${OWNER_COMMITS[0]}" \
  --owner "chummer-hub-registry=$OWNER_ROOT/chummer-hub-registry=${OWNER_COMMITS[1]}" \
  --owner "chummer-ui-kit=$OWNER_ROOT/chummer-ui-kit=${OWNER_COMMITS[2]}" \
  --owner "chummer.run-services=$OWNER_ROOT/chummer.run-services=${OWNER_COMMITS[3]}" \
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

[[ -z "$(git -C "$REPO_ROOT" status --porcelain=v1 --untracked-files=no)" ]] || {
  echo "UI tracked source changed during the build." >&2
  exit 2
}
printf 'unsigned macOS build complete: %s\n' "$OUTPUT_ROOT"
