#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"

STAGE_DIR="${1:?stage directory is required}"
OUTPUT_PATH="${2:?output path is required}"

STAGE_DIR="$(python3 - "$STAGE_DIR" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
)"
OUTPUT_PATH="$(python3 - "$OUTPUT_PATH" <<'PY'
from pathlib import Path
import sys

print(Path(sys.argv[1]).resolve())
PY
)"

CONFIG_PATH="$STAGE_DIR/bootstrap-config.nsh"
if [[ ! -f "$CONFIG_PATH" ]]; then
  echo "Missing bootstrap config: $CONFIG_PATH" >&2
  exit 1
fi

mkdir -p "$STAGE_DIR/7zip" "$(dirname "$OUTPUT_PATH")"
mkdir -p "$STAGE_DIR/curl"

SEVENZIP_EXTRA_URL="${CHUMMER_WINDOWS_7ZIP_EXTRA_URL:-https://github.com/ip7z/7zip/releases/download/26.02/7z2602-extra.7z}"
SEVENZIP_EXTRA_SHA256="${CHUMMER_WINDOWS_7ZIP_EXTRA_SHA256:-081df9e9311dfd9c9e0e98c1c80180b99bb51e4cb24156b5f3057fe3c259d70a}"
CURL_WINDOWS_URL="${CHUMMER_WINDOWS_CURL_URL:-https://curl.se/windows/dl-8.21.0_1/curl-8.21.0_1-win64-mingw.zip}"
CURL_WINDOWS_SHA256="${CHUMMER_WINDOWS_CURL_SHA256:-157068447d5b0b178dcc650f29d4746049fa4c7cc12db5f2bc050c0b84e48e7a}"

docker run --rm \
  -e HOST_UID="$(id -u)" \
  -e HOST_GID="$(id -g)" \
  -v "$REPO_ROOT:/repo:ro" \
  -v "$STAGE_DIR:/work" \
  -w /work \
  debian:bookworm-slim \
  bash -lc '
    set -euo pipefail
    export DEBIAN_FRONTEND=noninteractive
    apt-get update >/dev/null
    apt-get install -y --no-install-recommends ca-certificates curl nsis p7zip-full >/dev/null

    if [[ ! -f /work/7zip/7za.exe || ! -f /work/7zip/7za.dll || ! -f /work/7zip/7zxa.dll ]]; then
      tmpdir="$(mktemp -d)"
      curl -L --fail --retry 5 --retry-delay 2 -o "$tmpdir/7z-extra.7z" "'"$SEVENZIP_EXTRA_URL"'"
      echo "'"$SEVENZIP_EXTRA_SHA256"'  $tmpdir/7z-extra.7z" | sha256sum -c -
      7z e -aoa -o/work/7zip "$tmpdir/7z-extra.7z" 7za.exe 7za.dll 7zxa.dll License.txt >/dev/null
      rm -rf "$tmpdir"
    fi

    if [[ ! -f /work/curl/curl.exe || ! -f /work/curl/libcurl-x64.dll || ! -f /work/curl/curl-ca-bundle.crt ]]; then
      tmpdir="$(mktemp -d)"
      curl -L --fail --retry 5 --retry-delay 2 -o "$tmpdir/curl-win64.zip" "'"$CURL_WINDOWS_URL"'"
      echo "'"$CURL_WINDOWS_SHA256"'  $tmpdir/curl-win64.zip" | sha256sum -c -
      7z e -aoa -o/work/curl "$tmpdir/curl-win64.zip" "*/bin/curl.exe" "*/bin/libcurl-x64.dll" "*/bin/curl-ca-bundle.crt" "*/COPYING.txt" >/dev/null
      rm -rf "$tmpdir"
    fi

    makensis \
      -DCHUMMER_BOOTSTRAP_CONFIG=/work/bootstrap-config.nsh \
      -DCHUMMER_OUTPUT_PATH=/work/output-installer.exe \
      /repo/scripts/windows-bootstrap/installer.nsi >/work/makensis.log

    if command -v chown >/dev/null 2>&1; then
      chown -R "${HOST_UID}:${HOST_GID}" /work
    fi
  '

if [[ ! -f "$STAGE_DIR/output-installer.exe" ]]; then
  echo "NSIS bootstrap build did not produce $STAGE_DIR/output-installer.exe" >&2
  exit 1
fi

mv -f "$STAGE_DIR/output-installer.exe" "$OUTPUT_PATH"
