#!/usr/bin/env bash
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
PYTHON_BIN="${CHUMMER_PYTHON_BIN:-$(command -v python3)}"
EVIDENCE_TOOL="$SCRIPT_DIR/macos_flagship_evidence.py"

STAGE_ROOT="${1:?stage-only bundle root is required}"
PREDECESSOR_DMG="${2:?verified predecessor DMG is required}"
PREDECESSOR_VERIFICATION="${3:?predecessor verification receipt is required}"
AUTHORITY_RECEIPT="${4:?authority validation receipt is required}"
EVIDENCE_ROOT="${5:?evidence output root is required}"
RELEASE_VERSION="${6:?release version is required}"
RID="${7:?RID is required}"

APP_KEY="avalonia"
LAUNCH_TARGET="Chummer.Avalonia"
SIGN_IDENTITY="${CHUMMER_MAC_APP_SIGN_IDENTITY:-}"
TEAM_ID="${CHUMMER_MAC_TEAM_ID:-}"
NOTARY_PROFILE="${CHUMMER_MAC_NOTARY_PROFILE:-}"
EXPECTED_CERT_SHA="${CHUMMER_MAC_CERT_SHA256:-}"
EXPECTED_CERT_SPKI_SHA="${CHUMMER_MAC_CERT_SPKI_SHA256:-}"
RUN_ROOT=""
ACTIVE_MOUNT=""
ACTIVE_PID=""
SIGNING_AUTHORITY_ACTIVE=0

die() {
  printf 'macOS flagship evidence failed: %s\n' "$*" >&2
  exit 1
}

remove_apple_signing_authority() {
  local keychain_path="${CHUMMER_MAC_KEYCHAIN_PATH:-}"
  local original_default="$RUNNER_TEMP/chummer-original-default-keychain.txt"
  local original_list="$RUNNER_TEMP/chummer-original-keychains.txt"
  local default_keychain=""
  local keychain_line=""
  local original_keychains=()

  [[ -n "$keychain_path" ]] || return 1
  [[ "$keychain_path" == "$RUNNER_TEMP/chummer-flagship-signing.keychain-db" ]] \
    || return 1
  [[ -f "$original_default" && ! -L "$original_default" ]] || return 1
  [[ -f "$original_list" && ! -L "$original_list" ]] || return 1

  while IFS= read -r keychain_line; do
    keychain_line="${keychain_line#*\"}"
    keychain_line="${keychain_line%\"*}"
    if [[ -n "$keychain_line" ]]; then
      original_keychains[${#original_keychains[@]}]="$keychain_line"
    fi
  done <"$original_list"
  (( ${#original_keychains[@]} > 0 )) || return 1
  security list-keychains -d user -s "${original_keychains[@]}" || return 1

  default_keychain="$(
    sed -e 's/^[[:space:]]*"//' -e 's/"[[:space:]]*$//' "$original_default"
  )"
  [[ -n "$default_keychain" ]] || return 1
  security default-keychain -d user -s "$default_keychain" || return 1
  security lock-keychain "$keychain_path" || return 1
  security delete-keychain "$keychain_path" || return 1
  rm -f \
    "$RUNNER_TEMP/chummer-developer-id.p12" \
    "$RUNNER_TEMP/AuthKey.p8" \
    "$original_default" \
    "$original_list" || return 1

  CHUMMER_MAC_KEYCHAIN_PATH=""
  NOTARY_PROFILE=""
  SIGNING_AUTHORITY_ACTIVE=0
}

cleanup() {
  local status=$?
  set +e
  if (( SIGNING_AUTHORITY_ACTIVE )); then
    remove_apple_signing_authority
  fi
  if [[ -n "$ACTIVE_PID" ]]; then
    kill "$ACTIVE_PID" >/dev/null 2>&1 || true
    wait "$ACTIVE_PID" >/dev/null 2>&1 || true
    ACTIVE_PID=""
  fi
  if [[ -n "$ACTIVE_MOUNT" ]]; then
    hdiutil detach "$ACTIVE_MOUNT" >/dev/null 2>&1 || true
    ACTIVE_MOUNT=""
  fi
  if [[ -n "$RUN_ROOT" && -d "$RUN_ROOT" ]]; then
    case "$RUN_ROOT" in
      "${RUNNER_TEMP:-${TMPDIR:-/tmp}}"/chummer-macos-flagship.*)
        rm -rf -- "$RUN_ROOT"
        ;;
    esac
  fi
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "required command is missing: $1"
}

sha256_file() {
  "$PYTHON_BIN" - "$1" <<'PY'
from __future__ import annotations

import hashlib
import sys
from pathlib import Path

path = Path(sys.argv[1])
hasher = hashlib.sha256()
with path.open("rb") as handle:
    for chunk in iter(lambda: handle.read(1024 * 1024), b""):
        hasher.update(chunk)
print(hasher.hexdigest())
PY
}

resolve_existing_directory() {
  "$PYTHON_BIN" - "$1" <<'PY'
from __future__ import annotations

import os
import stat
import sys
from pathlib import Path

path = Path(sys.argv[1])
metadata = path.lstat()
if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
    raise SystemExit("expected a real directory")
print(path.resolve())
PY
}

resolve_existing_file() {
  "$PYTHON_BIN" - "$1" <<'PY'
from __future__ import annotations

import stat
import sys
from pathlib import Path

path = Path(sys.argv[1])
metadata = path.lstat()
if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
    raise SystemExit("expected a regular non-symlink file")
print(path.resolve())
PY
}

find_single_app_bundle() {
  "$PYTHON_BIN" - "$1" <<'PY'
from __future__ import annotations

import sys
from pathlib import Path

root = Path(sys.argv[1])
matches = sorted(path for path in root.glob("*.app") if path.is_dir() and not path.is_symlink())
if len(matches) != 1:
    raise SystemExit(f"expected exactly one app bundle, observed {len(matches)}")
print(matches[0])
PY
}

mount_dmg() {
  local dmg="$1"
  local mount_root="$2"
  [[ -z "$ACTIVE_MOUNT" ]] || die "attempted nested DMG mount"
  mkdir -m 0700 "$mount_root"
  hdiutil attach -nobrowse -readonly -mountpoint "$mount_root" "$dmg" >/dev/null
  ACTIVE_MOUNT="$mount_root"
}

detach_dmg() {
  [[ -n "$ACTIVE_MOUNT" ]] || return 0
  hdiutil detach "$ACTIVE_MOUNT" >/dev/null
  ACTIVE_MOUNT=""
}

safe_remove_isolated_app() {
  local target="$1"
  case "$target" in
    "$RUN_ROOT"/Applications/*.app)
      rm -rf -- "$target"
      ;;
    *)
      die "refusing to remove app outside the isolated Applications root"
      ;;
  esac
}

wait_for_update_state() {
  local state_path="$1"
  local mode="$2"
  local output_path="$3"
  local candidate_sha="$4"
  local pending_receipt_path="${5:-}"
  "$PYTHON_BIN" - \
    "$state_path" \
    "$mode" \
    "$output_path" \
    "$RELEASE_VERSION" \
    "$candidate_sha" \
    "$pending_receipt_path" \
    "$PREDECESSOR_VERSION" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import sys
import time
from pathlib import Path

state_path = Path(sys.argv[1])
mode = sys.argv[2]
output_path = Path(sys.argv[3])
release_version = sys.argv[4]
candidate_sha = sys.argv[5]
pending_receipt_path = Path(sys.argv[6]) if sys.argv[6] else None
predecessor_version = sys.argv[7]
deadline = time.monotonic() + 300
last = "state file was not created"


def digest(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


while time.monotonic() < deadline:
    try:
        source_raw = state_path.read_bytes()
        payload = json.loads(source_raw.decode("utf-8"))
        if not isinstance(payload, dict):
            raise ValueError("state was not an object")
        if mode == "manual":
            pending = Path(str(payload.get("PendingInstallerPath") or ""))
            passed = (
                payload.get("LastFailureReason") == "macos_manual_install_required"
                and payload.get("InstalledVersion") == predecessor_version
                and payload.get("PendingUpdateVersion") == release_version
                and pending.is_file()
                and digest(pending) == candidate_sha
            )
        elif mode == "completed":
            passed = (
                payload.get("InstalledVersion") == release_version
                and payload.get("PendingUpdateVersion") in (None, "")
                and payload.get("PendingInstallerPath") in (None, "")
                and payload.get("LastFailureReason") in (None, "")
            )
        else:
            raise ValueError(f"unsupported mode {mode}")
        if passed:
            output_path.parent.mkdir(parents=True, exist_ok=True)
            projected = dict(payload)
            projected["ObservedStateSha256"] = hashlib.sha256(source_raw).hexdigest()
            if mode == "manual":
                if pending_receipt_path is None:
                    raise ValueError("manual state requires a pending-delivery receipt path")
                projected["PendingInstallerPath"] = pending.name
                projected["PendingInstallerPathDisclosure"] = "file_name_only"
            elif pending_receipt_path is not None:
                raise ValueError("completed state cannot emit a pending-delivery receipt")
            temporary = output_path.with_name(f".{output_path.name}.tmp-{os.getpid()}")
            projected_raw = (
                json.dumps(projected, indent=2, sort_keys=True) + "\n"
            ).encode("utf-8")
            temporary.write_bytes(projected_raw)
            os.replace(temporary, output_path)
            if pending_receipt_path is not None:
                pending_receipt_path.parent.mkdir(parents=True, exist_ok=True)
                receipt = {
                    "contractName": "chummer6-ui.macos-pending-installer-delivery",
                    "contractVersion": 1,
                    "pendingInstallerFileName": pending.name,
                    "pendingInstallerSha256": candidate_sha,
                    "pendingInstallerSizeBytes": pending.stat().st_size,
                    "releaseVersion": release_version,
                    "stateSha256": hashlib.sha256(projected_raw).hexdigest(),
                    "status": "pass",
                }
                receipt_temporary = pending_receipt_path.with_name(
                    f".{pending_receipt_path.name}.tmp-{os.getpid()}"
                )
                receipt_temporary.write_text(
                    json.dumps(receipt, indent=2, sort_keys=True) + "\n",
                    encoding="utf-8",
                )
                os.replace(receipt_temporary, pending_receipt_path)
            raise SystemExit(0)
        last = json.dumps(
            {
                "InstalledVersion": payload.get("InstalledVersion"),
                "LastFailureReason": payload.get("LastFailureReason"),
                "PendingInstallerPath": payload.get("PendingInstallerPath"),
                "PendingUpdateVersion": payload.get("PendingUpdateVersion"),
            },
            sort_keys=True,
        )
    except (OSError, ValueError, json.JSONDecodeError) as error:
        last = str(error)
    time.sleep(1)

print(f"timed out waiting for {mode} update state: {last}", file=sys.stderr)
raise SystemExit(1)
PY
}

stop_active_process() {
  if [[ -z "$ACTIVE_PID" ]]; then
    return 0
  fi
  kill "$ACTIVE_PID" >/dev/null 2>&1 || true
  local remaining=20
  while kill -0 "$ACTIVE_PID" >/dev/null 2>&1 && (( remaining > 0 )); do
    sleep 1
    remaining=$((remaining - 1))
  done
  if kill -0 "$ACTIVE_PID" >/dev/null 2>&1; then
    kill -KILL "$ACTIVE_PID" >/dev/null 2>&1 || true
  fi
  wait "$ACTIVE_PID" >/dev/null 2>&1 || true
  ACTIVE_PID=""
}

for forbidden in \
  CHUMMER_RELEASE_UPLOAD_TOKEN \
  CHUMMER_RELEASE_UPLOAD_TOKEN_FILE \
  CHUMMER_RELEASE_UPLOAD_TICKET \
  CHUMMER_RELEASE_UPLOAD_TICKET_FILE \
  CHUMMER_RELEASE_PUBLISH_MODE \
  CHUMMER_RELEASE_UPLOAD_URL \
  CHUMMER_RELEASE_UPLOAD_SESSIONS_URL \
  CHUMMER_RELEASE_SSH_TARGET \
  CHUMMER_PORTAL_DOWNLOADS_DEPLOY_DIR \
  CHUMMER_PORTAL_DOWNLOADS_S3_URI \
  CHUMMER_REGISTRY_CONTROL_API_KEY \
  REGISTRY_CONTROL_API_KEY \
  FLEET_INTERNAL_API_TOKEN; do
  [[ -z "${!forbidden:-}" ]] || die "non-publishing lane rejects $forbidden"
done

[[ "$(uname -s)" == "Darwin" ]] || die "this workflow requires a native macOS host"
[[ "$RID" == "osx-arm64" ]] || die "this governed lane is fixed to osx-arm64"
[[ "$(uname -m)" == "arm64" ]] || die "osx-arm64 evidence requires an Apple Silicon runner"
[[ "$SIGN_IDENTITY" == "Developer ID Application:"* ]] \
  || die "CHUMMER_MAC_APP_SIGN_IDENTITY must name a Developer ID Application identity"
[[ "$TEAM_ID" =~ ^[A-Z0-9]{10}$ ]] || die "CHUMMER_MAC_TEAM_ID must be an exact Apple team ID"
[[ "$EXPECTED_CERT_SHA" =~ ^[0-9a-f]{64}$ ]] \
  || die "CHUMMER_MAC_CERT_SHA256 must be an exact lowercase SHA-256"
[[ "$EXPECTED_CERT_SPKI_SHA" =~ ^[0-9a-f]{64}$ ]] \
  || die "CHUMMER_MAC_CERT_SPKI_SHA256 must be an exact lowercase SHA-256"
[[ -n "$NOTARY_PROFILE" && "$NOTARY_PROFILE" != *[[:space:]]* ]] \
  || die "CHUMMER_MAC_NOTARY_PROFILE must be a bounded keychain profile name"
[[ "${CHUMMER_MAC_KEYCHAIN_PATH:-}" == "$RUNNER_TEMP/chummer-flagship-signing.keychain-db" ]] \
  || die "CHUMMER_MAC_KEYCHAIN_PATH must identify the governed ephemeral keychain"
[[ -x "$EVIDENCE_TOOL" || -f "$EVIDENCE_TOOL" ]] || die "evidence collector is missing"

for command_name in \
  codesign \
  ditto \
  hdiutil \
  lipo \
  openssl \
  security \
  spctl \
  xattr \
  xcrun; do
  require_cmd "$command_name"
done
[[ "$(spctl --status)" == "assessments enabled" ]] \
  || die "Gatekeeper assessments must be enabled"

STAGE_ROOT="$(resolve_existing_directory "$STAGE_ROOT")"
PREDECESSOR_DMG="$(resolve_existing_file "$PREDECESSOR_DMG")"
PREDECESSOR_VERIFICATION="$(resolve_existing_file "$PREDECESSOR_VERIFICATION")"
AUTHORITY_RECEIPT="$(resolve_existing_file "$AUTHORITY_RECEIPT")"
PREDECESSOR_VERSION="$(
  "$PYTHON_BIN" - "$PREDECESSOR_VERIFICATION" <<'PY'
import json
import re
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
value = payload.get("releaseVersion")
if not isinstance(value, str) or re.fullmatch(r"run-[0-9]{8}-[0-9]{6}", value) is None:
    raise SystemExit("predecessor verification releaseVersion is invalid")
print(value)
PY
)"
SOURCE_DMG="$STAGE_ROOT/files/chummer-$APP_KEY-$RID-installer.dmg"
STAGE_MANIFEST="$STAGE_ROOT/RELEASE_CHANNEL.generated.json"
STAGE_RECEIPT="$STAGE_ROOT/release-evidence/mac-stage-only.json"
SOURCE_DMG="$(resolve_existing_file "$SOURCE_DMG")"
STAGE_MANIFEST="$(resolve_existing_file "$STAGE_MANIFEST")"
STAGE_RECEIPT="$(resolve_existing_file "$STAGE_RECEIPT")"

if [[ -e "$EVIDENCE_ROOT" || -L "$EVIDENCE_ROOT" ]]; then
  die "evidence output root must not already exist"
fi
mkdir -m 0700 "$EVIDENCE_ROOT"
EVIDENCE_ROOT="$(resolve_existing_directory "$EVIDENCE_ROOT")"

SIGNING_AUTHORITY_ACTIVE=1
identity_count="$(
  security find-identity -v -p codesigning | \
    "$PYTHON_BIN" -c 'import sys; expected=sys.argv[1]; print(sum(1 for line in sys.stdin if f"\"{expected}\"" in line))' \
    "$SIGN_IDENTITY"
)"
[[ "$identity_count" == "1" ]] || die "expected exactly one matching Developer ID Application identity"
xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" >/dev/null

RUN_ROOT="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/chummer-macos-flagship.XXXXXX")"
chmod 0700 "$RUN_ROOT"
mkdir -m 0700 "$RUN_ROOT/Applications"
mkdir -m 0700 "$EVIDENCE_ROOT/files" "$EVIDENCE_ROOT/receipts" "$EVIDENCE_ROOT/logs"

authority_receipt_copy="$EVIDENCE_ROOT/receipts/AUTHORITY_VALIDATION.generated.json"
predecessor_verification_copy="$EVIDENCE_ROOT/receipts/PREDECESSOR_VERIFICATION.generated.json"
stage_manifest_copy="$EVIDENCE_ROOT/receipts/STAGE_RELEASE_CHANNEL.generated.json"
stage_receipt_copy="$EVIDENCE_ROOT/receipts/MAC_STAGE_ONLY.projected.json"
cp "$AUTHORITY_RECEIPT" "$authority_receipt_copy"
cp "$PREDECESSOR_VERIFICATION" "$predecessor_verification_copy"
cp "$STAGE_MANIFEST" "$stage_manifest_copy"
"$PYTHON_BIN" - "$STAGE_RECEIPT" "$stage_receipt_copy" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import sys
from pathlib import Path

source = Path(sys.argv[1])
output = Path(sys.argv[2])
raw = source.read_bytes()
payload = json.loads(raw.decode("utf-8"))
output_path = str(payload.get("outputPath") or "").rstrip("/\\")
payload["outputPath"] = Path(output_path).name if output_path else ""
payload["outputPathDisclosure"] = "directory_name_only"
payload["sourceReceiptSha256"] = hashlib.sha256(raw).hexdigest()
temporary = output.with_name(f".{output.name}.tmp-{os.getpid()}")
temporary.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
os.replace(temporary, output)
PY
AUTHORITY_RECEIPT="$authority_receipt_copy"
PREDECESSOR_VERIFICATION="$predecessor_verification_copy"
STAGE_MANIFEST="$stage_manifest_copy"
STAGE_RECEIPT="$stage_receipt_copy"

source_mount="$RUN_ROOT/source-mount"
mount_dmg "$SOURCE_DMG" "$source_mount"
source_app="$(find_single_app_bundle "$source_mount")"
source_publish="$RUN_ROOT/source-publish"
ditto "$source_app/Contents/MacOS" "$source_publish"
icon_path="$(
  "$PYTHON_BIN" - "$source_app/Contents/Resources" <<'PY'
from pathlib import Path
import sys

root = Path(sys.argv[1])
matches = sorted(path for path in root.glob("*.icns") if path.is_file())
print(matches[0] if matches else "")
PY
)"
if [[ -n "$icon_path" ]]; then
  cp "$icon_path" "$source_publish/chummer.icns"
fi
detach_dmg

candidate_build="$RUN_ROOT/candidate-build"
mkdir -m 0700 "$candidate_build"
notary_result="$candidate_build/notary-result-$APP_KEY-$RID.json"
CHUMMER_DESKTOP_RELEASE_CHANNEL="preview" \
CHUMMER_MAC_APP_SIGN_IDENTITY="$SIGN_IDENTITY" \
CHUMMER_MAC_NOTARY_PROFILE="$NOTARY_PROFILE" \
CHUMMER_MAC_SIGNING_REQUIRED="1" \
CHUMMER_MAC_NOTARIZATION_REQUIRED="1" \
CHUMMER_MAC_NOTARY_RESULT_PATH="$notary_result" \
CHUMMER_MAC_SIGNING_RECEIPT_PATH="$candidate_build/signing-$APP_KEY-$RID.receipt.json" \
CHUMMER_DESKTOP_INSTALLER_TMPDIR="$RUN_ROOT/installer-tmp" \
CHUMMER_MACOS_ICON_SOURCE="${icon_path:+$source_publish/chummer.icns}" \
bash "$SCRIPT_DIR/build-desktop-installer.sh" \
  "$source_publish" \
  "$APP_KEY" \
  "$RID" \
  "$LAUNCH_TARGET" \
  "$candidate_build" \
  "$RELEASE_VERSION"

built_candidate="$candidate_build/chummer-$APP_KEY-$RID-installer.dmg"
built_signing_receipt="$candidate_build/signing-$APP_KEY-$RID.receipt.json"
[[ -f "$built_candidate" && ! -L "$built_candidate" ]] || die "signed candidate DMG was not produced"
[[ -f "$built_signing_receipt" && ! -L "$built_signing_receipt" ]] || die "signing receipt was not produced"
[[ -f "$notary_result" && ! -L "$notary_result" ]] || die "notarytool result was not produced"
CANDIDATE_DMG="$EVIDENCE_ROOT/files/$(basename "$built_candidate")"
SIGNING_RECEIPT="$EVIDENCE_ROOT/receipts/$(basename "$built_signing_receipt")"
NOTARY_RESULT="$EVIDENCE_ROOT/receipts/$(basename "$notary_result")"
SIGNING_IDENTITY_RECEIPT="$EVIDENCE_ROOT/receipts/macos-signing-notarization-identity.json"
mv "$built_candidate" "$CANDIDATE_DMG"
cp "$built_signing_receipt" "$SIGNING_RECEIPT"
cp "$notary_result" "$NOTARY_RESULT"
CANDIDATE_SHA="$(sha256_file "$CANDIDATE_DMG")"

codesign --verify --strict --verbose=4 "$CANDIDATE_DMG" \
  >"$EVIDENCE_ROOT/logs/candidate-dmg-codesign.log" 2>&1
xcrun stapler validate "$CANDIDATE_DMG" \
  >"$EVIDENCE_ROOT/logs/candidate-dmg-staple.log" 2>&1
spctl --assess --type open --verbose=4 "$CANDIDATE_DMG" \
  >"$EVIDENCE_ROOT/logs/candidate-dmg-gatekeeper.log" 2>&1
certificate_pem="$RUN_ROOT/developer-id-certificate.pem"
certificate_der="$RUN_ROOT/developer-id-certificate.der"
certificate_public_key="$RUN_ROOT/developer-id-public-key.pem"
certificate_spki_der="$RUN_ROOT/developer-id-spki.der"
security find-certificate \
  -c "$SIGN_IDENTITY" \
  -p \
  "$CHUMMER_MAC_KEYCHAIN_PATH" >"$certificate_pem"
openssl x509 -in "$certificate_pem" -outform DER -out "$certificate_der"
openssl x509 -in "$certificate_pem" -pubkey -noout \
  >"$certificate_public_key"
openssl pkey \
  -pubin \
  -in "$certificate_public_key" \
  -outform DER \
  -out "$certificate_spki_der"
observed_cert_sha="$(sha256_file "$certificate_der")"
observed_cert_spki_sha="$(sha256_file "$certificate_spki_der")"
"$PYTHON_BIN" "$EVIDENCE_TOOL" emit-signing-identity \
  --authority-receipt "$AUTHORITY_RECEIPT" \
  --candidate-artifact "$CANDIDATE_DMG" \
  --signing-receipt "$SIGNING_RECEIPT" \
  --notary-result "$NOTARY_RESULT" \
  --identity "$SIGN_IDENTITY" \
  --team-id "$TEAM_ID" \
  --certificate-sha256 "$observed_cert_sha" \
  --certificate-spki-sha256 "$observed_cert_spki_sha" \
  --expected-certificate-sha256 "$EXPECTED_CERT_SHA" \
  --expected-certificate-spki-sha256 "$EXPECTED_CERT_SPKI_SHA" \
  --output "$SIGNING_IDENTITY_RECEIPT"
remove_apple_signing_authority \
  || die "could not destroy Apple signing authority before runtime execution"
unset CHUMMER_MAC_NOTARY_PROFILE CHUMMER_MAC_NOTARY_PROFILE_NAME

candidate_mount="$RUN_ROOT/candidate-mount"
mount_dmg "$CANDIDATE_DMG" "$candidate_mount"
candidate_app_on_dmg="$(find_single_app_bundle "$candidate_mount")"
installed_candidate="$RUN_ROOT/Applications/$(basename "$candidate_app_on_dmg")"
ditto "$candidate_app_on_dmg" "$installed_candidate"
detach_dmg
xattr -w com.apple.quarantine "0081;$(date +%s);GitHubActions;macos-flagship" "$installed_candidate"
xattr -p com.apple.quarantine "$installed_candidate" \
  >"$EVIDENCE_ROOT/logs/installed-app-quarantine.log"
codesign --verify --deep --strict --verbose=4 "$installed_candidate" \
  >"$EVIDENCE_ROOT/logs/installed-app-codesign.log" 2>&1
candidate_archs="$(
  lipo -archs "$installed_candidate/Contents/MacOS/$LAUNCH_TARGET"
)"
printf '%s\n' "$candidate_archs" \
  >"$EVIDENCE_ROOT/logs/installed-app-architectures.log"
case " $candidate_archs " in
  *" arm64 "*) ;;
  *) die "installed candidate launch executable does not contain arm64" ;;
esac
codesign -d --verbose=4 "$installed_candidate" \
  >"$EVIDENCE_ROOT/logs/installed-app-authority.log" 2>&1
spctl --assess --type execute --verbose=4 "$installed_candidate" \
  >"$EVIDENCE_ROOT/logs/installed-app-gatekeeper.log" 2>&1
"$PYTHON_BIN" - "$EVIDENCE_ROOT/logs/installed-app-authority.log" "$TEAM_ID" "$SIGN_IDENTITY" <<'PY'
from pathlib import Path
import sys

text = Path(sys.argv[1]).read_text(encoding="utf-8", errors="replace")
team_id = sys.argv[2]
identity = sys.argv[3]
if f"TeamIdentifier={team_id}" not in text:
    raise SystemExit("signed app TeamIdentifier mismatch")
if f"Authority={identity}" not in text:
    raise SystemExit("signed app Developer ID authority mismatch")
PY

clean_startup_receipt="$EVIDENCE_ROOT/receipts/startup-smoke-clean-install-$APP_KEY-$RID.receipt.json"
clean_startup_failure="$EVIDENCE_ROOT/receipts/startup-smoke-clean-install-$APP_KEY-$RID.failure.json"
CHUMMER_DESKTOP_STATE_ROOT="$RUN_ROOT/clean-install-state" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$clean_startup_receipt" \
CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$clean_startup_failure" \
CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:$CANDIDATE_SHA" \
CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="github-actions-macos-arm64-clean-install" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$RELEASE_VERSION" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RID="$RID" \
CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
CHUMMER_DESKTOP_RELEASE_CHANNEL="preview" \
"$installed_candidate/Contents/MacOS/$LAUNCH_TARGET" --startup-smoke \
  >"$EVIDENCE_ROOT/logs/clean-install-startup.log" 2>&1
safe_remove_isolated_app "$installed_candidate"
[[ ! -e "$installed_candidate" && ! -L "$installed_candidate" ]] || die "clean uninstall left the candidate app installed"

predecessor_mount="$RUN_ROOT/predecessor-mount"
mount_dmg "$PREDECESSOR_DMG" "$predecessor_mount"
predecessor_app_on_dmg="$(find_single_app_bundle "$predecessor_mount")"
installed_update_app="$RUN_ROOT/Applications/$(basename "$predecessor_app_on_dmg")"
ditto "$predecessor_app_on_dmg" "$installed_update_app"
detach_dmg
xattr -w com.apple.quarantine "0081;$(date +%s);GitHubActions;macos-predecessor" "$installed_update_app"
predecessor_archs="$(
  lipo -archs "$installed_update_app/Contents/MacOS/$LAUNCH_TARGET"
)"
printf '%s\n' "$predecessor_archs" \
  >"$EVIDENCE_ROOT/logs/predecessor-app-architectures.log"
case " $predecessor_archs " in
  *" arm64 "*) ;;
  *) die "installed predecessor launch executable does not contain arm64" ;;
esac
spctl --assess --type execute --verbose=4 "$installed_update_app" \
  >"$EVIDENCE_ROOT/logs/predecessor-app-gatekeeper.log" 2>&1

update_shelf="$RUN_ROOT/update-shelf"
mkdir -m 0700 "$update_shelf" "$update_shelf/files"
cp "$CANDIDATE_DMG" "$update_shelf/files/$(basename "$CANDIDATE_DMG")"
candidate_size="$(stat -f '%z' "$CANDIDATE_DMG")"
update_manifest="$update_shelf/RELEASE_CHANNEL.generated.json"
"$PYTHON_BIN" - "$update_manifest" "$RELEASE_VERSION" "$RID" "$(basename "$CANDIDATE_DMG")" "$CANDIDATE_SHA" "$candidate_size" <<'PY'
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

path = Path(sys.argv[1])
version = sys.argv[2]
rid = sys.argv[3]
file_name = sys.argv[4]
sha256 = sys.argv[5]
size_bytes = int(sys.argv[6])
payload = {
    "artifacts": [
        {
            "arch": "arm64",
            "artifactId": f"avalonia-{rid}-installer",
            "downloadUrl": f"files/{file_name}",
            "fileName": file_name,
            "head": "avalonia",
            "kind": "dmg",
            "platform": "macos",
            "rid": rid,
            "sha256": sha256,
            "sizeBytes": size_bytes,
        }
    ],
    "channelId": "preview",
    "publishedAt": "2026-01-01T00:00:00Z",
    "rolloutState": "active",
    "status": "published",
    "version": version,
}
temporary = path.with_name(f".{path.name}.tmp-{os.getpid()}")
temporary.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
os.replace(temporary, path)
PY

update_state_root="$RUN_ROOT/update-state"
update_state_path="$update_state_root/Chummer6/desktop-update/avalonia/macos/arm64/state.json"
manual_update_state="$EVIDENCE_ROOT/receipts/update-state-manual-handoff.json"
pending_delivery_receipt="$EVIDENCE_ROOT/receipts/pending-installer-delivery.json"
completed_update_state="$EVIDENCE_ROOT/receipts/update-state-completed.json"

CHUMMER_DESKTOP_STATE_ROOT="$update_state_root" \
CHUMMER_DESKTOP_UPDATE_MANIFEST="$update_manifest" \
CHUMMER_DESKTOP_UPDATE_MODE="full" \
CHUMMER_DESKTOP_UPDATE_ENABLED="1" \
CHUMMER_DESKTOP_UPDATE_AUTO_APPLY="1" \
CHUMMER_API_BASE_URL="http://127.0.0.1:9" \
"$installed_update_app/Contents/MacOS/$LAUNCH_TARGET" \
  >"$EVIDENCE_ROOT/logs/predecessor-update-delivery.log" 2>&1 &
ACTIVE_PID=$!
wait_for_update_state \
  "$update_state_path" \
  "manual" \
  "$manual_update_state" \
  "$CANDIDATE_SHA" \
  "$pending_delivery_receipt"
stop_active_process

safe_remove_isolated_app "$installed_update_app"
candidate_update_mount="$RUN_ROOT/candidate-update-mount"
mount_dmg "$CANDIDATE_DMG" "$candidate_update_mount"
candidate_update_app="$(find_single_app_bundle "$candidate_update_mount")"
installed_update_app="$RUN_ROOT/Applications/$(basename "$candidate_update_app")"
ditto "$candidate_update_app" "$installed_update_app"
detach_dmg
xattr -w com.apple.quarantine "0081;$(date +%s);GitHubActions;macos-update" "$installed_update_app"
spctl --assess --type execute --verbose=4 "$installed_update_app" \
  >"$EVIDENCE_ROOT/logs/post-update-app-gatekeeper.log" 2>&1

CHUMMER_DESKTOP_STATE_ROOT="$update_state_root" \
CHUMMER_DESKTOP_UPDATE_MANIFEST="$update_manifest" \
CHUMMER_DESKTOP_UPDATE_MODE="full" \
CHUMMER_DESKTOP_UPDATE_ENABLED="1" \
CHUMMER_DESKTOP_UPDATE_AUTO_APPLY="1" \
CHUMMER_API_BASE_URL="http://127.0.0.1:9" \
"$installed_update_app/Contents/MacOS/$LAUNCH_TARGET" \
  >"$EVIDENCE_ROOT/logs/post-update-completion.log" 2>&1 &
ACTIVE_PID=$!
wait_for_update_state "$update_state_path" "completed" "$completed_update_state" "$CANDIDATE_SHA"
stop_active_process

post_update_startup_receipt="$EVIDENCE_ROOT/receipts/startup-smoke-post-update-$APP_KEY-$RID.receipt.json"
post_update_failure="$EVIDENCE_ROOT/receipts/startup-smoke-post-update-$APP_KEY-$RID.failure.json"
CHUMMER_DESKTOP_STATE_ROOT="$update_state_root" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$post_update_startup_receipt" \
CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$post_update_failure" \
CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:$CANDIDATE_SHA" \
CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="github-actions-macos-arm64-post-update" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$RELEASE_VERSION" \
CHUMMER_DESKTOP_STARTUP_SMOKE_RID="$RID" \
CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop" \
CHUMMER_DESKTOP_RELEASE_CHANNEL="preview" \
"$installed_update_app/Contents/MacOS/$LAUNCH_TARGET" --startup-smoke \
  >"$EVIDENCE_ROOT/logs/post-update-startup.log" 2>&1
safe_remove_isolated_app "$installed_update_app"
[[ ! -e "$installed_update_app" && ! -L "$installed_update_app" ]] \
  || die "post-update uninstall left the candidate app installed"

observations="$EVIDENCE_ROOT/receipts/MACOS_FLAGSHIP_RUNTIME_OBSERVATIONS.generated.json"
"$PYTHON_BIN" - "$observations" "$RELEASE_VERSION" "$RID" "$SIGN_IDENTITY" "$TEAM_ID" <<'PY'
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

path = Path(sys.argv[1])
payload = {
    "checks": {
        "candidateDmgCodesign": True,
        "candidateDmgGatekeeper": True,
        "candidateDmgStaple": True,
        "candidateHostArchitecture": True,
        "cleanInstallCopied": True,
        "coreStartup": True,
        "gatekeeperAssessmentsEnabled": True,
        "installedAppCodesign": True,
        "installedAppGatekeeper": True,
        "postUpdateStartup": True,
        "postUpdateUninstallRemoved": True,
        "predecessorAppGatekeeper": True,
        "predecessorUpdateStateObserved": True,
        "quarantineApplied": True,
        "uninstallRemoved": True,
        "updateCompletionStateObserved": True,
        "updateManualInstallCopied": True,
    },
    "contractName": "chummer6-ui.macos-flagship-runtime-observations",
    "contractVersion": 1,
    "releaseVersion": sys.argv[2],
    "rid": sys.argv[3],
    "signingAuthority": {
        "identity": sys.argv[4],
        "teamId": sys.argv[5],
    },
}
temporary = path.with_name(f".{path.name}.tmp-{os.getpid()}")
temporary.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
os.replace(temporary, path)
PY

inventory_output="$EVIDENCE_ROOT/receipts/MACOS_FLAGSHIP_EVIDENCE_INVENTORY.generated.json"
evidence_output="$EVIDENCE_ROOT/receipts/MACOS_FLAGSHIP_EVIDENCE.generated.json"
native_adapter_output="$EVIDENCE_ROOT/receipts/FLAGSHIP_NATIVE_E2E.macos.generated.json"
runner_os="macos-$(sw_vers -productVersion)"
"$PYTHON_BIN" "$EVIDENCE_TOOL" collect \
  --authority-receipt "$AUTHORITY_RECEIPT" \
  --predecessor-verification "$PREDECESSOR_VERIFICATION" \
  --predecessor-artifact "$PREDECESSOR_DMG" \
  --stage-receipt "$STAGE_RECEIPT" \
  --stage-manifest "$STAGE_MANIFEST" \
  --source-artifact "$SOURCE_DMG" \
  --candidate-artifact "$CANDIDATE_DMG" \
  --signing-receipt "$SIGNING_RECEIPT" \
  --signing-identity-receipt "$SIGNING_IDENTITY_RECEIPT" \
  --notary-result "$NOTARY_RESULT" \
  --clean-startup-receipt "$clean_startup_receipt" \
  --post-update-startup-receipt "$post_update_startup_receipt" \
  --manual-update-state "$manual_update_state" \
  --pending-delivery-receipt "$pending_delivery_receipt" \
  --completed-update-state "$completed_update_state" \
  --observations "$observations" \
  --inventory-output "$inventory_output" \
  --output "$evidence_output" \
  --native-adapter-output "$native_adapter_output" \
  --run-id "${GITHUB_RUN_ID:?GitHub run ID is required}" \
  --run-attempt "${GITHUB_RUN_ATTEMPT:?GitHub run attempt is required}" \
  --runner-os "$runner_os" \
  --runner-arch "$(uname -m)"

printf 'macos_flagship_candidate=%s\n' "$CANDIDATE_DMG"
printf 'macos_flagship_evidence=%s\n' "$evidence_output"
printf 'macos_flagship_inventory=%s\n' "$inventory_output"
printf 'macos_flagship_native_adapter=%s\n' "$native_adapter_output"
