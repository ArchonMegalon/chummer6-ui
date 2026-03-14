#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PUBLISH_DIR="${1:?publish directory is required}"
APP_KEY="${2:?app key is required}"
RID="${3:?rid is required}"
LAUNCH_EXE="${4:?launch executable name is required}"
DIST_DIR="${5:-$REPO_ROOT/dist}"
VERSION="${6:-local}"

case "$APP_KEY" in
  avalonia)
    APP_DISPLAY="Chummer6 Avalonia Desktop"
    INSTALL_DIR_NAME="AvaloniaDesktop"
    SHORTCUT_NAME="Chummer6 Avalonia"
    ;;
  blazor-desktop)
    APP_DISPLAY="Chummer6 Blazor Desktop"
    INSTALL_DIR_NAME="BlazorDesktop"
    SHORTCUT_NAME="Chummer6 Blazor Desktop"
    ;;
  *)
    echo "Unsupported app key: $APP_KEY" >&2
    exit 1
    ;;
esac

mkdir -p "$DIST_DIR"
PORTABLE_ARCHIVE="$DIST_DIR/chummer-$APP_KEY-$RID.zip"
INSTALLER_NAME="chummer-$APP_KEY-$RID-installer.exe"
INSTALLER_OUT_DIR="$DIST_DIR/installer-$APP_KEY-$RID"

python3 - "$PUBLISH_DIR" "$PORTABLE_ARCHIVE" <<'PY'
import os
import sys
import zipfile
from pathlib import Path

source = Path(sys.argv[1])
target = Path(sys.argv[2])
if not source.exists():
    raise SystemExit(f"publish directory not found: {source}")
if target.exists():
    target.unlink()
with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED) as zf:
    for file in sorted(source.rglob("*")):
        if file.is_file():
            zf.write(file, file.relative_to(source))
print(target)
PY

rm -rf "$INSTALLER_OUT_DIR"
dotnet publish "$REPO_ROOT/Chummer.Desktop.Installer/Chummer.Desktop.Installer.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:ChummerInstallerAssemblyName="Chummer6Installer-$APP_KEY-$RID" \
  -p:InstallerPayloadZip="$PORTABLE_ARCHIVE" \
  -p:ChummerInstallerAppId="$APP_KEY-$RID" \
  -p:ChummerInstallerDisplayName="$APP_DISPLAY" \
  -p:ChummerInstallerInstallDirName="$INSTALL_DIR_NAME-$RID" \
  -p:ChummerInstallerLaunchExecutable="$LAUNCH_EXE" \
  -p:ChummerInstallerVersion="$VERSION" \
  -p:ChummerInstallerShortcutName="$SHORTCUT_NAME" \
  -p:ChummerInstallerOutputName="Chummer6Installer-$APP_KEY-$RID" \
  -o "$INSTALLER_OUT_DIR"

installer_source="$(find "$INSTALLER_OUT_DIR" -maxdepth 1 -type f -name '*.exe' | sort | head -n 1)"
if [[ -z "$installer_source" ]]; then
  echo "Installer publish output did not produce a .exe in $INSTALLER_OUT_DIR" >&2
  exit 1
fi
cp "$installer_source" "$DIST_DIR/$INSTALLER_NAME"
echo "built portable archive $PORTABLE_ARCHIVE"
echo "built installer $DIST_DIR/$INSTALLER_NAME"
