#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

PUBLISH_DIR="${1:?publish directory is required}"
APP_KEY="${2:?app key is required}"
RID="${3:?rid is required}"
LAUNCH_TARGET="${4:?launch target name is required}"
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

resolve_demo_character_source() {
  local configured="${CHUMMER_RELEASE_SAMPLE_SOURCE:-}"
  local fixture_root="${CHUMMER_LEGACY_FIXTURE_ROOT:-}"
  local candidates=()

  if [[ -n "$configured" ]]; then
    candidates+=("$configured")
  fi
  if [[ -n "$fixture_root" ]]; then
    candidates+=("$fixture_root/Soma (Career).chum5")
  fi

  candidates+=(
    "$REPO_ROOT/../../chummer5a/Chummer.Tests/TestFiles/Soma (Career).chum5"
    "/docker/chummer5a/Chummer.Tests/TestFiles/Soma (Career).chum5"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      printf '%s' "$candidate"
      return 0
    fi
  done

  return 1
}

bundle_demo_character_fixture() {
  local source_path
  if ! source_path="$(resolve_demo_character_source)"; then
    echo "warning: bundled demo character fixture not found; release will not include the legacy sample." >&2
    return 0
  fi

  local samples_dir="$PUBLISH_DIR/Samples/Legacy"
  mkdir -p "$samples_dir"
  cp "$source_path" "$samples_dir/Soma-Career.chum5"
  cat > "$samples_dir/README.txt" <<'EOF'
Bundled legacy sample fixture:
- source repo: chummer5a
- source path: Chummer.Tests/TestFiles/Soma (Career).chum5
- purpose: load a completed SR5 runner in the desktop shell after install
EOF
}

build_payload_zip() {
  local target="$1"
  python3 - "$PUBLISH_DIR" "$target" <<'PY'
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
}

build_payload_tar_gz() {
  local target="$1"
  python3 - "$PUBLISH_DIR" "$target" <<'PY'
import sys
import tarfile
from pathlib import Path

source = Path(sys.argv[1])
target = Path(sys.argv[2])
if not source.exists():
    raise SystemExit(f"publish directory not found: {source}")
if target.exists():
    target.unlink()
with tarfile.open(target, "w:gz") as tf:
    for file in sorted(source.rglob("*")):
        if file.is_file():
            tf.add(file, arcname=file.relative_to(source))
print(target)
PY
}

normalize_deb_version() {
  python3 - "$VERSION" <<'PY'
import re
import sys

raw = sys.argv[1].strip() or "0~local"
value = re.sub(r"[^0-9A-Za-z.+~:-]+", "-", raw)
value = value.strip(".-:+~") or "0~local"
if not value[0].isdigit():
    value = f"0~{value}"
print(value)
PY
}

macos_bundle_identifier() {
  python3 - "$APP_KEY" "$RID" <<'PY'
import re
import sys

app_key = re.sub(r"[^A-Za-z0-9]+", "-", sys.argv[1]).strip("-").lower() or "desktop"
rid = re.sub(r"[^A-Za-z0-9]+", "-", sys.argv[2]).strip("-").lower() or "local"
print(f"net.chummer6.{app_key}.{rid}")
PY
}

linux_deb_arch() {
  case "$RID" in
    linux-x64) echo "amd64" ;;
    linux-arm64) echo "arm64" ;;
    *)
      echo "Unsupported Linux RID for deb packaging: $RID" >&2
      exit 1
      ;;
  esac
}

linux_deb_depends() {
  case "$APP_KEY" in
    avalonia)
      cat <<'EOF'
libfontconfig1, libfreetype6, zlib1g
EOF
      ;;
    blazor-desktop)
      cat <<'EOF'
libwebkit2gtk-4.1-0, libnotify4, libnss3, libxss1, libasound2 | libasound2t64, xdg-utils
EOF
      ;;
    *)
      return 0
      ;;
  esac
}

ensure_self_contained_publish() {
  local launch_stem
  launch_stem="$LAUNCH_TARGET"
  if [[ "$launch_stem" == *.exe ]]; then
    launch_stem="${launch_stem%.exe}"
  fi
  local runtimeconfig_path="$PUBLISH_DIR/$launch_stem.runtimeconfig.json"

  if [[ ! -f "$runtimeconfig_path" ]]; then
    return 0
  fi

  python3 - "$runtimeconfig_path" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
payload = json.loads(path.read_text(encoding="utf-8"))
runtime_options = payload.get("runtimeOptions") or {}

# Framework-dependent desktop publishes still carry framework/frameworks.
# Self-contained desktop publishes should not require a shared runtime here.
if runtime_options.get("framework") or runtime_options.get("frameworks"):
    raise SystemExit(
        f"framework-dependent desktop publish detected: {path}. "
        "Re-publish with --self-contained true before building installers."
    )
PY
}

build_portable_artifacts() {
  ensure_self_contained_publish

  case "$RID" in
    win-*)
      local portable_exe="$DIST_DIR/chummer-$APP_KEY-$RID.exe"
      local portable_zip="$DIST_DIR/chummer-$APP_KEY-$RID.zip"
      if [[ ! -f "$PUBLISH_DIR/$LAUNCH_TARGET" ]]; then
        echo "Launch target not found in Windows publish directory: $PUBLISH_DIR/$LAUNCH_TARGET" >&2
        exit 1
      fi
      cp "$PUBLISH_DIR/$LAUNCH_TARGET" "$portable_exe"
      build_payload_zip "$portable_zip"
      echo "built portable $portable_exe"
      echo "built archive $portable_zip"
      ;;
    linux-*|osx-*)
      local portable_archive="$DIST_DIR/chummer-$APP_KEY-$RID.tar.gz"
      build_payload_tar_gz "$portable_archive"
      echo "built archive $portable_archive"
      ;;
    *)
      echo "Unsupported portable target RID: $RID" >&2
      exit 1
      ;;
  esac
}

build_macos_installer() {
  ensure_self_contained_publish

  if ! command -v hdiutil >/dev/null 2>&1; then
    echo "hdiutil is required for macOS dmg packaging." >&2
    exit 1
  fi

  local installer_name="chummer-$APP_KEY-$RID-installer.dmg"
  local stage_root="$DIST_DIR/package-$APP_KEY-$RID"
  local app_bundle="$stage_root/$APP_DISPLAY.app"
  local contents_dir="$app_bundle/Contents"
  local macos_dir="$contents_dir/MacOS"
  local plist_path="$contents_dir/Info.plist"
  local bundle_identifier
  bundle_identifier="$(macos_bundle_identifier)"

  rm -rf "$stage_root"
  mkdir -p "$macos_dir" "$contents_dir/Resources"
  cp -a "$PUBLISH_DIR"/. "$macos_dir"/

  if [[ ! -f "$macos_dir/$LAUNCH_TARGET" ]]; then
    echo "Launch target not found in macOS publish directory: $macos_dir/$LAUNCH_TARGET" >&2
    exit 1
  fi
  chmod 0755 "$macos_dir/$LAUNCH_TARGET"

  cat > "$plist_path" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_DISPLAY</string>
  <key>CFBundleExecutable</key>
  <string>$LAUNCH_TARGET</string>
  <key>CFBundleIdentifier</key>
  <string>$bundle_identifier</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$APP_DISPLAY</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
</dict>
</plist>
EOF

  rm -f "$DIST_DIR/$installer_name"
  hdiutil create \
    -volname "$APP_DISPLAY" \
    -srcfolder "$stage_root" \
    -ov \
    -format UDZO \
    "$DIST_DIR/$installer_name" >/dev/null

  rm -rf "$stage_root"
  echo "built installer $DIST_DIR/$installer_name"
}

build_windows_installer() {
  ensure_self_contained_publish

  local payload_zip="$DIST_DIR/chummer-$APP_KEY-$RID-payload.zip"
  local payload_resource_name="ChummerInstaller.Payload.zip"
  local installer_name="chummer-$APP_KEY-$RID-installer.exe"
  local installer_out_dir="$DIST_DIR/installer-$APP_KEY-$RID"

  build_payload_zip "$payload_zip"

  rm -rf "$installer_out_dir"
  "$REPO_ROOT/scripts/ai/with-package-plane.sh" publish "$REPO_ROOT/Chummer.Desktop.Installer/Chummer.Desktop.Installer.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:ChummerInstallerEmbedPayload=true \
    -p:ChummerInstallerAssemblyName="Chummer6Installer-$APP_KEY-$RID" \
    -p:InstallerPayloadZip="$payload_zip" \
    -p:ChummerInstallerPayloadResourceName="$payload_resource_name" \
    -p:ChummerInstallerAppId="$APP_KEY-$RID" \
    -p:ChummerInstallerDisplayName="$APP_DISPLAY" \
    -p:ChummerInstallerInstallDirName="$INSTALL_DIR_NAME-$RID" \
    -p:ChummerInstallerLaunchExecutable="$LAUNCH_TARGET" \
    -p:ChummerInstallerVersion="$VERSION" \
    -p:ChummerInstallerShortcutName="$SHORTCUT_NAME" \
    -p:ChummerInstallerOutputName="Chummer6Installer-$APP_KEY-$RID" \
    -o "$installer_out_dir"

  local installer_source
  installer_source="$(find "$installer_out_dir" -maxdepth 1 -type f -name '*.exe' | sort | head -n 1)"
  if [[ -z "$installer_source" ]]; then
    echo "Installer publish output did not produce a .exe in $installer_out_dir" >&2
    exit 1
  fi

  cp "$installer_source" "$DIST_DIR/$installer_name"
  rm -f "$payload_zip"
  echo "built installer $DIST_DIR/$installer_name"
}

build_linux_installer() {
  ensure_self_contained_publish

  local deb_arch
  deb_arch="$(linux_deb_arch)"
  local deb_version
  deb_version="$(normalize_deb_version)"
  local installer_name="chummer-$APP_KEY-$RID-installer.deb"
  local stage_root="$DIST_DIR/package-$APP_KEY-$RID"
  local install_root="$stage_root/opt/chummer6/$APP_KEY-$RID"
  local wrapper_path="$stage_root/usr/bin/chummer6-$APP_KEY"
  local desktop_path="$stage_root/usr/share/applications/chummer6-$APP_KEY.desktop"
  local deb_depends
  deb_depends="$(linux_deb_depends || true)"

  rm -rf "$stage_root"
  mkdir -p "$stage_root/DEBIAN" "$install_root" "$(dirname "$wrapper_path")" "$(dirname "$desktop_path")"
  cp -a "$PUBLISH_DIR"/. "$install_root"/

  if [[ ! -f "$install_root/$LAUNCH_TARGET" ]]; then
    echo "Launch target not found in publish directory: $install_root/$LAUNCH_TARGET" >&2
    exit 1
  fi
  chmod 0755 "$install_root/$LAUNCH_TARGET"

  cat > "$stage_root/DEBIAN/control" <<EOF
Package: chummer6-$APP_KEY
Version: $deb_version
Section: games
Priority: optional
Architecture: $deb_arch
Maintainer: ArchonMegalon
Description: $APP_DISPLAY
 Installer package for the $APP_DISPLAY head.
EOF
  if [[ -n "$deb_depends" ]]; then
    printf 'Depends: %s\n' "$deb_depends" >> "$stage_root/DEBIAN/control"
  fi

  cat > "$wrapper_path" <<EOF
#!/usr/bin/env bash
set -euo pipefail
exec "/opt/chummer6/$APP_KEY-$RID/$LAUNCH_TARGET" "\$@"
EOF
  chmod 0755 "$wrapper_path"

  cat > "$desktop_path" <<EOF
[Desktop Entry]
Type=Application
Name=$APP_DISPLAY
Exec=/usr/bin/chummer6-$APP_KEY
Terminal=false
Categories=Game;
StartupNotify=true
EOF

  if dpkg-deb --help 2>&1 | grep -q -- '--root-owner-group'; then
    dpkg-deb --root-owner-group --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  elif command -v fakeroot >/dev/null 2>&1; then
    fakeroot dpkg-deb --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  else
    dpkg-deb --build "$stage_root" "$DIST_DIR/$installer_name" >/dev/null
  fi

  rm -rf "$stage_root"
  echo "built installer $DIST_DIR/$installer_name"
}

case "$RID" in
  win-*)
    bundle_demo_character_fixture
    build_portable_artifacts
    build_windows_installer
    ;;
  linux-*)
    bundle_demo_character_fixture
    build_portable_artifacts
    build_linux_installer
    ;;
  osx-*)
    bundle_demo_character_fixture
    build_portable_artifacts
    build_macos_installer
    ;;
  *)
    echo "Unsupported installer target RID: $RID" >&2
    exit 1
    ;;
esac
