#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BUNDLE_DIR="${1:-$REPO_ROOT/dist}"
DEPLOY_DIR="${2:-$REPO_ROOT/Docker/Downloads}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-}"
DEPLOY_MODE="${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED:-false}"
LIVE_VERIFY_TARGET="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}"
MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
FILES_SOURCE="$BUNDLE_DIR/files"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"
STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE:-$BUNDLE_DIR/startup-smoke}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

bundle_manifest_matches_files() {
  local manifest_path="$1"
  local files_root="$2"
  python3 - "$manifest_path" "$files_root" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
files_root = Path(sys.argv[2])

payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
downloads = payload.get("downloads") or []
failures: list[str] = []
seen: set[str] = set()

for artifact in downloads:
    if not isinstance(artifact, dict):
        continue
    url = str(artifact.get("url") or "").strip()
    file_name = Path(url).name if url else ""
    if not file_name:
        continue
    seen.add(file_name)
    file_path = files_root / file_name
    if not file_path.is_file():
        failures.append(f"manifest references missing file {file_name}")
        continue
    actual_size = file_path.stat().st_size
    expected_size = int(artifact.get("sizeBytes") or 0)
    if expected_size and expected_size != actual_size:
        failures.append(f"{file_name}: size {actual_size} != manifest {expected_size}")
    expected_sha = str(artifact.get("sha256") or "").strip().lower()
    if expected_sha:
        digest = hashlib.sha256(file_path.read_bytes()).hexdigest()
        if digest != expected_sha:
            failures.append(f"{file_name}: sha256 {digest} != manifest {expected_sha}")

for file_path in sorted(files_root.iterdir()):
    if not file_path.is_file():
        continue
    if file_path.name not in seen:
        failures.append(f"bundle contains extra file not present in manifest: {file_path.name}")

if failures:
    print("false")
    for failure in failures:
        print(failure)
else:
    print("true")
PY
}

if [[ -z "$PORTAL_MANIFEST_PATH" ]]; then
  if [[ "$(realpath "$DEPLOY_DIR")" == "$(realpath "$REPO_ROOT/Docker/Downloads")" ]]; then
    PORTAL_MANIFEST_PATH="$REPO_ROOT/Chummer.Portal/downloads/releases.json"
  else
    PORTAL_MANIFEST_PATH="$DEPLOY_DIR/releases.json"
  fi
fi

if [[ -z "$PORTAL_DOWNLOADS_DIR" ]]; then
  PORTAL_DOWNLOADS_DIR="$(dirname "$PORTAL_MANIFEST_PATH")"
fi

if [[ ! -d "$BUNDLE_DIR" ]]; then
  echo "Bundle directory not found: $BUNDLE_DIR" >&2
  exit 1
fi

if [[ ! -d "$FILES_SOURCE" ]]; then
  echo "Bundle is missing files directory: $FILES_SOURCE" >&2
  echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2
  exit 1
fi

mapfile -t artifacts < <(find "$FILES_SOURCE" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o \
     -name "chummer-avalonia-*.tar.gz" -o -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" \) \
  | sort)

if [[ "${#artifacts[@]}" -eq 0 ]]; then
  echo "No desktop artifacts found under $FILES_SOURCE" >&2
  exit 1
fi

sync_source_dir="$(mktemp -d)"
cleanup() {
  rm -rf "$sync_source_dir"
}
trap cleanup EXIT

for artifact in "${artifacts[@]}"; do
  cp "$artifact" "$sync_source_dir/"
done

release_version="${RELEASE_VERSION:-}"
release_channel="${RELEASE_CHANNEL:-}"
release_published_at="${RELEASE_PUBLISHED_AT:-}"
default_published_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [[ -f "$MANIFEST_SOURCE" ]]; then
  readarray -t manifest_meta < <(python3 - "$MANIFEST_SOURCE" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
print(str(data.get("version", "unpublished")))
print(str(data.get("channel", "docker")))
print(str(data.get("publishedAt", "")))
PY
)

  readarray -t manifest_integrity < <(bundle_manifest_matches_files "$MANIFEST_SOURCE" "$FILES_SOURCE")
  manifest_matches_files="${manifest_integrity[0]:-false}"

  if [[ "$manifest_matches_files" != "true" && -z "${RELEASE_VERSION:-}" ]]; then
    echo "Bundle files no longer match $MANIFEST_SOURCE, so reusing its release version would be dishonest." >&2
    printf '%s\n' "${manifest_integrity[@]:1}" >&2
    echo "Set RELEASE_VERSION and RELEASE_PUBLISHED_AT explicitly for this republish." >&2
    exit 1
  fi

  if [[ -z "$release_version" && -n "${manifest_meta[0]:-}" ]]; then
    release_version="${manifest_meta[0]}"
  fi
  if [[ -z "$release_channel" && -n "${manifest_meta[1]:-}" ]]; then
    release_channel="${manifest_meta[1]}"
  fi
  if [[ -z "$release_published_at" && -n "${manifest_meta[2]:-}" ]]; then
    release_published_at="${manifest_meta[2]}"
  fi
fi

release_version="${release_version:-unpublished}"
release_channel="${release_channel:-docker}"
release_published_at="${release_published_at:-$default_published_at}"

DOWNLOADS_DIR="$sync_source_dir" \
MANIFEST_PATH="$DEPLOY_DIR/releases.json" \
PORTAL_MANIFEST_PATH="$PORTAL_MANIFEST_PATH" \
PORTAL_DOWNLOADS_DIR="$PORTAL_DOWNLOADS_DIR" \
RELEASE_VERSION="$release_version" \
RELEASE_CHANNEL="$release_channel" \
RELEASE_PUBLISHED_AT="$release_published_at" \
SOURCE_MANIFEST_PATH="$MANIFEST_SOURCE" \
RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH" \
STARTUP_SMOKE_DIR="$STARTUP_SMOKE_SOURCE" \
bash "$SCRIPT_DIR/generate-releases-manifest.sh"

readarray -t promoted_file_names < <(python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
seen = set()
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        file_name = Path(str(artifact.get("downloadUrl") or "").strip()).name
    if file_name and file_name not in seen:
        print(file_name)
        seen.add(file_name)
PY
)

mkdir -p "$DEPLOY_DIR/files"
find "$DEPLOY_DIR/files" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
     -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
     -name "chummer-6-*-installer.deb" -o \
     -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
     -name "chummer-6-*-installer.msix" \) \
  -delete

for file_name in "${promoted_file_names[@]}"; do
  source_path="$sync_source_dir/$file_name"
  if [[ ! -f "$source_path" ]]; then
    echo "promoted artifact missing from bundle source: $source_path" >&2
    exit 1
  fi
  cp "$source_path" "$DEPLOY_DIR/files/"
done

if [[ -d "$STARTUP_SMOKE_SOURCE" ]]; then
  verified_startup_smoke_tmp="$(mktemp)"
  if ! python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$STARTUP_SMOKE_SOURCE" "$DEPLOY_DIR/files" >"$verified_startup_smoke_tmp" <<'PY'
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

PASSING_STATUSES = {"pass", "passed", "ready"}
INSTALL_MEDIA_KINDS = {"installer", "dmg", "pkg", "msix"}

release_channel_path = Path(sys.argv[1])
startup_smoke_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = payload.get("artifacts") or []
errors: list[str] = []
verified_receipts: list[str] = []
seen: set[str] = set()

def normalize(value: Any) -> str:
    return str(value or "").strip().lower()

def rid_to_arch(rid: str) -> str:
    token = normalize(rid)
    if token.startswith("win-") or token.startswith("linux-") or token.startswith("osx-"):
        _, _, arch = token.partition("-")
        return arch
    return token

for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    kind = normalize(artifact.get("kind"))
    if kind not in INSTALL_MEDIA_KINDS:
        continue
    head = normalize(artifact.get("head"))
    platform = normalize(artifact.get("platform"))
    rid = normalize(artifact.get("rid"))
    file_name = str(artifact.get("fileName") or "").strip()
    if not head or not platform or not rid or not file_name:
        errors.append(f"promoted install-medium artifact is missing required tuple fields (head/platform/rid/fileName): {artifact}")
        continue
    receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
    if receipt_name in seen:
        continue
    seen.add(receipt_name)
    receipt_path = startup_smoke_root / receipt_name
    if not receipt_path.is_file():
        errors.append(f"startup-smoke receipt missing for promoted install medium {head}/{platform}/{rid}: {receipt_name}")
        continue
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # pragma: no cover - shell guard
        errors.append(f"startup-smoke receipt is unreadable for promoted install medium {head}/{platform}/{rid}: {receipt_path} ({exc})")
        continue
    status = normalize(receipt.get("status"))
    if status not in PASSING_STATUSES:
        errors.append(f"startup-smoke receipt status is not passing for promoted install medium {head}/{platform}/{rid}: {status or 'missing'}")
    checkpoint = normalize(receipt.get("readyCheckpoint"))
    if checkpoint != "pre_ui_event_loop":
        errors.append(f"startup-smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted install medium {head}/{platform}/{rid}.")
    receipt_head = normalize(receipt.get("headId"))
    receipt_platform = normalize(receipt.get("platform"))
    receipt_arch = normalize(receipt.get("arch"))
    expected_arch = rid_to_arch(rid)
    if receipt_head != head:
        errors.append(f"startup-smoke receipt headId mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_head or 'missing'}")
    if receipt_platform != platform:
        errors.append(f"startup-smoke receipt platform mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_platform or 'missing'}")
    if expected_arch and receipt_arch != expected_arch:
        errors.append(f"startup-smoke receipt arch mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_arch or 'missing'}")
    promoted_file_path = files_root / file_name
    expected_sha = normalize(artifact.get("sha256"))
    if promoted_file_path.is_file():
        expected_sha = hashlib.sha256(promoted_file_path.read_bytes()).hexdigest().lower()
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    receipt_digest = normalize(receipt.get("artifactDigest"))
    if expected_digest and receipt_digest != expected_digest:
        errors.append(f"startup-smoke receipt artifactDigest mismatch for promoted install medium {head}/{platform}/{rid}.")
    verified_receipts.append(str(receipt_path))

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    raise SystemExit(1)

for verified in sorted(verified_receipts):
    print(verified)
PY
  then
    rm -f "$verified_startup_smoke_tmp"
    exit 1
  fi
  readarray -t verified_startup_smoke_receipts <"$verified_startup_smoke_tmp"
  rm -f "$verified_startup_smoke_tmp"

  startup_smoke_deploy_dir="$DEPLOY_DIR/startup-smoke"
  mkdir -p "$startup_smoke_deploy_dir"
  find "$startup_smoke_deploy_dir" -maxdepth 1 -type f -name "startup-smoke-*.receipt.json" -delete
  for smoke_path in "${verified_startup_smoke_receipts[@]}"; do
    cp "$smoke_path" "$startup_smoke_deploy_dir/$(basename "$smoke_path")"
  done
fi

if to_bool "$DEPLOY_MODE"; then
  export CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION="${CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION:-true}"
  export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS:-true}"
  if [[ -z "$LIVE_VERIFY_TARGET" ]]; then
    echo "Deployment mode requires CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL for live manifest verification." >&2
    exit 1
  fi
fi

bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$DEPLOY_DIR"

if [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$LIVE_VERIFY_TARGET"
fi

echo "Published ${#promoted_file_names[@]} desktop artifact(s) into $DEPLOY_DIR"
