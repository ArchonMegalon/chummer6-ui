#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
CONTRACT_SCRIPT="$SCRIPT_DIR/desktop_native_lifecycle_evidence.py"
SIGNING_SCRIPT="$SCRIPT_DIR/linux_deb_signing.py"

usage() {
  printf '%s\n' \
    "usage: $0 --candidate PATH --candidate-sha256 HEX --candidate-size BYTES" \
    "  --candidate-version VERSION --n-minus-one-binding-json JSON --output-root DIR" \
    "  --live-release-channel-json JSON --n-minus-one-release-sha256 HEX" \
    "  --live-release-channel-sha256 HEX --selected-tuple-sha256 HEX" \
    "  --signing-receipt PATH --signing-receipt-sha256 HEX --signing-receipt-size BYTES" \
    "  --signed-export-receipt PATH --signed-export-receipt-sha256 HEX --signed-export-receipt-size BYTES" \
    "  --verification-policy PATH --verification-policy-sha256 HEX --verification-policy-size BYTES" \
    "  --public-keyring PATH --public-keyring-sha256 HEX --public-keyring-size BYTES" \
    "  --transaction-manifest PATH --transaction-manifest-sha256 HEX --transaction-manifest-size BYTES" \
    "  --expected-primary-fingerprint HEX" \
    "  --source-repository OWNER/REPO --source-workflow PATH --source-run-id ID" \
    "  --source-run-attempt N --source-ref REF --source-sha SHA --source-actor LOGIN" \
    "  --source-triggering-actor LOGIN" >&2
}

CANDIDATE=""
CANDIDATE_SHA256=""
CANDIDATE_SIZE_BYTES=""
CANDIDATE_VERSION=""
N_MINUS_ONE_BINDING_JSON=""
LIVE_RELEASE_CHANNEL_JSON=""
N_MINUS_ONE_RELEASE_SHA256=""
LIVE_RELEASE_CHANNEL_SHA256=""
SELECTED_TUPLE_SHA256=""
SIGNING_RECEIPT=""
SIGNING_RECEIPT_SHA256=""
SIGNING_RECEIPT_SIZE_BYTES=""
SIGNED_EXPORT_RECEIPT=""
SIGNED_EXPORT_RECEIPT_SHA256=""
SIGNED_EXPORT_RECEIPT_SIZE_BYTES=""
VERIFICATION_POLICY=""
VERIFICATION_POLICY_SHA256=""
VERIFICATION_POLICY_SIZE_BYTES=""
PUBLIC_KEYRING=""
PUBLIC_KEYRING_SHA256=""
PUBLIC_KEYRING_SIZE_BYTES=""
TRANSACTION_MANIFEST=""
TRANSACTION_MANIFEST_SHA256=""
TRANSACTION_MANIFEST_SIZE_BYTES=""
EXPECTED_PRIMARY_FINGERPRINT=""
OUTPUT_ROOT=""
SOURCE_REPOSITORY=""
SOURCE_WORKFLOW=""
SOURCE_RUN_ID=""
SOURCE_RUN_ATTEMPT=""
SOURCE_REF=""
SOURCE_SHA=""
SOURCE_ACTOR=""
SOURCE_TRIGGERING_ACTOR=""

while (($#)); do
  case "$1" in
    --candidate) CANDIDATE="${2:-}"; shift 2 ;;
    --candidate-sha256) CANDIDATE_SHA256="${2:-}"; shift 2 ;;
    --candidate-size) CANDIDATE_SIZE_BYTES="${2:-}"; shift 2 ;;
    --candidate-version) CANDIDATE_VERSION="${2:-}"; shift 2 ;;
    --n-minus-one-binding-json) N_MINUS_ONE_BINDING_JSON="${2:-}"; shift 2 ;;
    --live-release-channel-json) LIVE_RELEASE_CHANNEL_JSON="${2:-}"; shift 2 ;;
    --n-minus-one-release-sha256) N_MINUS_ONE_RELEASE_SHA256="${2:-}"; shift 2 ;;
    --live-release-channel-sha256) LIVE_RELEASE_CHANNEL_SHA256="${2:-}"; shift 2 ;;
    --selected-tuple-sha256) SELECTED_TUPLE_SHA256="${2:-}"; shift 2 ;;
    --signing-receipt) SIGNING_RECEIPT="${2:-}"; shift 2 ;;
    --signing-receipt-sha256) SIGNING_RECEIPT_SHA256="${2:-}"; shift 2 ;;
    --signing-receipt-size) SIGNING_RECEIPT_SIZE_BYTES="${2:-}"; shift 2 ;;
    --signed-export-receipt) SIGNED_EXPORT_RECEIPT="${2:-}"; shift 2 ;;
    --signed-export-receipt-sha256) SIGNED_EXPORT_RECEIPT_SHA256="${2:-}"; shift 2 ;;
    --signed-export-receipt-size) SIGNED_EXPORT_RECEIPT_SIZE_BYTES="${2:-}"; shift 2 ;;
    --verification-policy) VERIFICATION_POLICY="${2:-}"; shift 2 ;;
    --verification-policy-sha256) VERIFICATION_POLICY_SHA256="${2:-}"; shift 2 ;;
    --verification-policy-size) VERIFICATION_POLICY_SIZE_BYTES="${2:-}"; shift 2 ;;
    --public-keyring) PUBLIC_KEYRING="${2:-}"; shift 2 ;;
    --public-keyring-sha256) PUBLIC_KEYRING_SHA256="${2:-}"; shift 2 ;;
    --public-keyring-size) PUBLIC_KEYRING_SIZE_BYTES="${2:-}"; shift 2 ;;
    --transaction-manifest) TRANSACTION_MANIFEST="${2:-}"; shift 2 ;;
    --transaction-manifest-sha256) TRANSACTION_MANIFEST_SHA256="${2:-}"; shift 2 ;;
    --transaction-manifest-size) TRANSACTION_MANIFEST_SIZE_BYTES="${2:-}"; shift 2 ;;
    --expected-primary-fingerprint) EXPECTED_PRIMARY_FINGERPRINT="${2:-}"; shift 2 ;;
    --output-root) OUTPUT_ROOT="${2:-}"; shift 2 ;;
    --source-repository) SOURCE_REPOSITORY="${2:-}"; shift 2 ;;
    --source-workflow) SOURCE_WORKFLOW="${2:-}"; shift 2 ;;
    --source-run-id) SOURCE_RUN_ID="${2:-}"; shift 2 ;;
    --source-run-attempt) SOURCE_RUN_ATTEMPT="${2:-}"; shift 2 ;;
    --source-ref) SOURCE_REF="${2:-}"; shift 2 ;;
    --source-sha) SOURCE_SHA="${2:-}"; shift 2 ;;
    --source-actor) SOURCE_ACTOR="${2:-}"; shift 2 ;;
    --source-triggering-actor) SOURCE_TRIGGERING_ACTOR="${2:-}"; shift 2 ;;
    *) usage; exit 2 ;;
  esac
done

for required in \
  CANDIDATE CANDIDATE_SHA256 CANDIDATE_SIZE_BYTES CANDIDATE_VERSION \
  N_MINUS_ONE_BINDING_JSON LIVE_RELEASE_CHANNEL_JSON \
  N_MINUS_ONE_RELEASE_SHA256 LIVE_RELEASE_CHANNEL_SHA256 \
  SELECTED_TUPLE_SHA256 SIGNING_RECEIPT SIGNING_RECEIPT_SHA256 \
  SIGNING_RECEIPT_SIZE_BYTES VERIFICATION_POLICY VERIFICATION_POLICY_SHA256 \
  SIGNED_EXPORT_RECEIPT SIGNED_EXPORT_RECEIPT_SHA256 \
  SIGNED_EXPORT_RECEIPT_SIZE_BYTES \
  VERIFICATION_POLICY_SIZE_BYTES PUBLIC_KEYRING PUBLIC_KEYRING_SHA256 \
  PUBLIC_KEYRING_SIZE_BYTES TRANSACTION_MANIFEST \
  TRANSACTION_MANIFEST_SHA256 TRANSACTION_MANIFEST_SIZE_BYTES \
  EXPECTED_PRIMARY_FINGERPRINT OUTPUT_ROOT \
  SOURCE_REPOSITORY SOURCE_WORKFLOW \
  SOURCE_RUN_ID SOURCE_RUN_ATTEMPT SOURCE_REF SOURCE_SHA SOURCE_ACTOR \
  SOURCE_TRIGGERING_ACTOR; do
  if [[ -z "${!required}" ]]; then
    echo "linux-native-lifecycle: missing $required" >&2
    usage
    exit 2
  fi
done

fail() {
  echo "linux-native-lifecycle: $*" >&2
  exit 1
}

utc_now() {
  date -u +'%Y-%m-%dT%H:%M:%SZ'
}

sha256_file() {
  sha256sum "$1" | awk '{print $1}'
}

size_file() {
  stat -c '%s' "$1"
}

assert_bound_regular_file() {
  local path="$1"
  local expected_sha="$2"
  local expected_size="$3"
  local label="$4"
  [[ "$expected_sha" =~ ^[0-9a-f]{64}$ ]] || fail "$label SHA-256 binding is malformed"
  [[ "$expected_size" =~ ^[1-9][0-9]*$ ]] || fail "$label size binding is malformed"
  [[ -f "$path" && ! -L "$path" ]] || fail "$label must be a regular non-symlink file"
  [[ "$(stat -c '%h' "$path")" == "1" ]] || fail "$label must be singly linked"
  [[ "$(size_file "$path")" == "$expected_size" ]] || fail "$label size differs"
  [[ "$(sha256_file "$path")" == "$expected_sha" ]] || fail "$label SHA-256 differs"
}

download_pinned() {
  local url="$1"
  local target="$2"
  local expected_sha="$3"
  local expected_size="$4"
  local maximum_size="$5"
  local label="$6"
  local effective_url=""
  effective_url="$(
    curl \
      --proto '=https' \
      --proto-redir '=https' \
      --location \
      --max-redirs 5 \
      --fail \
      --silent \
      --show-error \
      --connect-timeout 30 \
      --max-time 600 \
      --output "$target" \
      --write-out '%{url_effective}' \
      "$url"
  )"
  [[ "$effective_url" == https://chummer.run/* ]] \
    || fail "$label redirected outside the pinned chummer.run authority"
  [[ -f "$target" && ! -L "$target" ]] || fail "$label did not produce a regular file"
  local actual_size
  actual_size="$(size_file "$target")"
  ((actual_size > 0 && actual_size <= maximum_size)) || fail "$label size is outside its fixed bound"
  if [[ "$expected_size" != "0" ]]; then
    [[ "$actual_size" == "$expected_size" ]] || fail "$label size differs from its binding"
  fi
  [[ "$(sha256_file "$target")" == "$expected_sha" ]] \
    || fail "$label SHA-256 differs from its binding"
}

assert_passing_json() {
  python3 - "$1" "$2" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
label = sys.argv[2]
if not path.is_file() or path.is_symlink():
    raise SystemExit(f"{label} receipt is missing or unsafe")
payload = json.loads(path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict) or str(payload.get("status", "")).lower() not in {"pass", "passed"}:
    raise SystemExit(f"{label} receipt did not pass")
PY
}

if [[ "${RUNNER_OS:-}" != "Linux" || "$(uname -s)" != "Linux" ]]; then
  fail "this evidence lane requires a native GitHub Linux runner"
fi
case "$(uname -m)" in
  x86_64|amd64) ;;
  *) fail "this evidence lane requires a native Linux x64 runner" ;;
esac
[[ "$SOURCE_ACTOR" == "github-actions[bot]" ]] \
  || fail "the governed native lane must be dispatched by the producer relay"
[[ "$SOURCE_TRIGGERING_ACTOR" == "$SOURCE_ACTOR" ]] \
  || fail "the governed native lane permits only same-actor reruns"
[[ "$SOURCE_REPOSITORY" == "ArchonMegalon/chummer6-ui" ]] \
  || fail "the native source repository is not the governed UI repository"
[[ "$SOURCE_WORKFLOW" == ".github/workflows/linux-native-lifecycle-evidence.yml" ]] \
  || fail "the native source workflow is not the governed Linux lane"
[[ "$SOURCE_REF" == "refs/heads/main" ]] \
  || fail "the native source ref is not the governed main branch"
[[ "$SOURCE_SHA" =~ ^[0-9a-f]{40}$ ]] \
  || fail "the native source SHA is malformed"
[[ "$SOURCE_RUN_ID" =~ ^[1-9][0-9]*$ && "$SOURCE_RUN_ATTEMPT" =~ ^[1-9][0-9]*$ ]] \
  || fail "the native run identity is malformed"
[[ "$CANDIDATE_VERSION" =~ ^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$ ]] \
  || fail "the candidate version is not a portable release identifier"
if [[ -n "${GITHUB_SHA:-}" && "$SOURCE_SHA" != "$GITHUB_SHA" ]]; then
  fail "the native source SHA differs from the checked-out workflow commit"
fi
command -v sudo >/dev/null || fail "sudo is required for a normal system package lifecycle"
command -v dpkg-deb >/dev/null || fail "dpkg-deb is required"
command -v dpkg-query >/dev/null || fail "dpkg-query is required"
command -v debsig-verify >/dev/null || fail "debsig-verify is required"
command -v gpgv >/dev/null || fail "gpgv is required"
command -v xvfb-run >/dev/null || fail "xvfb-run is required for the native mouse-first workflow"

mkdir -p "$OUTPUT_ROOT"
OUTPUT_ROOT="$(cd "$OUTPUT_ROOT" && pwd -P)"
CANDIDATE="$(readlink -f "$CANDIDATE")"
assert_bound_regular_file "$CANDIDATE" "$CANDIDATE_SHA256" "$CANDIDATE_SIZE_BYTES" "candidate package"
SIGNING_RECEIPT="$(readlink -f "$SIGNING_RECEIPT")"
SIGNED_EXPORT_RECEIPT="$(readlink -f "$SIGNED_EXPORT_RECEIPT")"
VERIFICATION_POLICY="$(readlink -f "$VERIFICATION_POLICY")"
PUBLIC_KEYRING="$(readlink -f "$PUBLIC_KEYRING")"
TRANSACTION_MANIFEST="$(readlink -f "$TRANSACTION_MANIFEST")"
assert_bound_regular_file \
  "$SIGNING_RECEIPT" "$SIGNING_RECEIPT_SHA256" \
  "$SIGNING_RECEIPT_SIZE_BYTES" "candidate signing receipt"
assert_bound_regular_file \
  "$SIGNED_EXPORT_RECEIPT" "$SIGNED_EXPORT_RECEIPT_SHA256" \
  "$SIGNED_EXPORT_RECEIPT_SIZE_BYTES" "candidate signed export receipt"
assert_bound_regular_file \
  "$VERIFICATION_POLICY" "$VERIFICATION_POLICY_SHA256" \
  "$VERIFICATION_POLICY_SIZE_BYTES" "candidate debsig policy"
assert_bound_regular_file \
  "$PUBLIC_KEYRING" "$PUBLIC_KEYRING_SHA256" \
  "$PUBLIC_KEYRING_SIZE_BYTES" "candidate public keyring"
assert_bound_regular_file \
  "$TRANSACTION_MANIFEST" "$TRANSACTION_MANIFEST_SHA256" \
  "$TRANSACTION_MANIFEST_SIZE_BYTES" \
  "candidate commit-last transaction manifest"
[[ "$EXPECTED_PRIMARY_FINGERPRINT" =~ ^[0-9A-F]{40}$ ]] \
  || fail "candidate primary fingerprint is malformed"

declare -A SIGNATURE_VERIFY=()
while IFS='=' read -r key value; do
  case "$key" in
    artifact_sha256|artifact_size_bytes|policy_sha256|policy_size_bytes|primary_fingerprint|public_keyring_sha256|public_keyring_size_bytes|signed_export_receipt_sha256|signed_export_receipt_size_bytes|signing_fingerprint|signing_receipt_sha256|signing_receipt_size_bytes|tamper_exit_code|transaction_manifest_sha256|transaction_manifest_size_bytes|verification_binary_sha256|verification_package_version)
      [[ -z "${SIGNATURE_VERIFY[$key]+x}" ]] \
        || fail "duplicate keyless signature result $key"
      SIGNATURE_VERIFY["$key"]="$value"
      ;;
  esac
done < <(
  python3 "$SIGNING_SCRIPT" verify \
    --package "$CANDIDATE" \
    --receipt "$SIGNING_RECEIPT" \
    --signed-export-receipt "$SIGNED_EXPORT_RECEIPT" \
    --transaction-manifest "$TRANSACTION_MANIFEST" \
    --policy "$VERIFICATION_POLICY" \
    --public-keyring "$PUBLIC_KEYRING" \
    --release-version "$CANDIDATE_VERSION" \
    --expected-primary-fingerprint "$EXPECTED_PRIMARY_FINGERPRINT" \
    --expected-public-keyring-sha256 "$PUBLIC_KEYRING_SHA256" \
    --expected-signed-export-receipt-sha256 "$SIGNED_EXPORT_RECEIPT_SHA256" \
    --expected-transaction-manifest-sha256 "$TRANSACTION_MANIFEST_SHA256"
)
for key in artifact_sha256 artifact_size_bytes policy_sha256 policy_size_bytes \
  primary_fingerprint public_keyring_sha256 public_keyring_size_bytes \
  signing_fingerprint signing_receipt_sha256 signing_receipt_size_bytes \
  signed_export_receipt_sha256 signed_export_receipt_size_bytes \
  transaction_manifest_sha256 transaction_manifest_size_bytes \
  tamper_exit_code verification_binary_sha256 verification_package_version; do
  [[ -n "${SIGNATURE_VERIFY[$key]:-}" ]] \
    || fail "keyless signature verification omitted $key"
done
[[ "${SIGNATURE_VERIFY[artifact_sha256]}" == "$CANDIDATE_SHA256" \
   && "${SIGNATURE_VERIFY[artifact_size_bytes]}" == "$CANDIDATE_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[policy_sha256]}" == "$VERIFICATION_POLICY_SHA256" \
   && "${SIGNATURE_VERIFY[policy_size_bytes]}" == "$VERIFICATION_POLICY_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[primary_fingerprint]}" == "$EXPECTED_PRIMARY_FINGERPRINT" \
   && "${SIGNATURE_VERIFY[signing_fingerprint]}" == "$EXPECTED_PRIMARY_FINGERPRINT" \
   && "${SIGNATURE_VERIFY[public_keyring_sha256]}" == "$PUBLIC_KEYRING_SHA256" \
   && "${SIGNATURE_VERIFY[public_keyring_size_bytes]}" == "$PUBLIC_KEYRING_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[signing_receipt_sha256]}" == "$SIGNING_RECEIPT_SHA256" \
   && "${SIGNATURE_VERIFY[signing_receipt_size_bytes]}" == "$SIGNING_RECEIPT_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[signed_export_receipt_sha256]}" == "$SIGNED_EXPORT_RECEIPT_SHA256" \
   && "${SIGNATURE_VERIFY[signed_export_receipt_size_bytes]}" == "$SIGNED_EXPORT_RECEIPT_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[transaction_manifest_sha256]}" == "$TRANSACTION_MANIFEST_SHA256" \
   && "${SIGNATURE_VERIFY[transaction_manifest_size_bytes]}" == "$TRANSACTION_MANIFEST_SIZE_BYTES" \
   && "${SIGNATURE_VERIFY[tamper_exit_code]}" == "13" \
   && "${SIGNATURE_VERIFY[verification_package_version]}" == "0.29" ]] \
  || fail "keyless signature verification differs from candidate authority"

SIGNING_RECEIPT_EVIDENCE="$OUTPUT_ROOT/candidate-linux-signing-receipt.json"
SIGNED_EXPORT_RECEIPT_EVIDENCE="$OUTPUT_ROOT/candidate-linux-signed-export-receipt.json"
VERIFICATION_POLICY_EVIDENCE="$OUTPUT_ROOT/candidate-linux-debsig-policy.pol"
PUBLIC_KEYRING_EVIDENCE="$OUTPUT_ROOT/candidate-linux-public-keyring.pgp"
TRANSACTION_MANIFEST_EVIDENCE="$OUTPUT_ROOT/candidate-linux-signing-transaction.json"
install -m 0600 "$SIGNING_RECEIPT" "$SIGNING_RECEIPT_EVIDENCE"
install -m 0600 "$SIGNED_EXPORT_RECEIPT" "$SIGNED_EXPORT_RECEIPT_EVIDENCE"
install -m 0600 "$VERIFICATION_POLICY" "$VERIFICATION_POLICY_EVIDENCE"
install -m 0600 "$PUBLIC_KEYRING" "$PUBLIC_KEYRING_EVIDENCE"
install -m 0600 "$TRANSACTION_MANIFEST" "$TRANSACTION_MANIFEST_EVIDENCE"
assert_bound_regular_file \
  "$SIGNING_RECEIPT_EVIDENCE" "$SIGNING_RECEIPT_SHA256" \
  "$SIGNING_RECEIPT_SIZE_BYTES" "copied candidate signing receipt"
assert_bound_regular_file \
  "$SIGNED_EXPORT_RECEIPT_EVIDENCE" "$SIGNED_EXPORT_RECEIPT_SHA256" \
  "$SIGNED_EXPORT_RECEIPT_SIZE_BYTES" "copied candidate signed export receipt"
assert_bound_regular_file \
  "$VERIFICATION_POLICY_EVIDENCE" "$VERIFICATION_POLICY_SHA256" \
  "$VERIFICATION_POLICY_SIZE_BYTES" "copied candidate debsig policy"
assert_bound_regular_file \
  "$PUBLIC_KEYRING_EVIDENCE" "$PUBLIC_KEYRING_SHA256" \
  "$PUBLIC_KEYRING_SIZE_BYTES" "copied candidate public keyring"
assert_bound_regular_file \
  "$TRANSACTION_MANIFEST_EVIDENCE" "$TRANSACTION_MANIFEST_SHA256" \
  "$TRANSACTION_MANIFEST_SIZE_BYTES" \
  "copied candidate signing transaction manifest"

LIVE_RELEASE_CHANNEL_EVIDENCE="$OUTPUT_ROOT/live-release-channel-root.json"
python3 "$CONTRACT_SCRIPT" fetch-live-predecessor-authority \
  --binding-json "$N_MINUS_ONE_BINDING_JSON" \
  --expected-live-release-channel-json "$LIVE_RELEASE_CHANNEL_JSON" \
  --platform linux \
  --rid linux-x64 \
  --expected-n-minus-one-sha256 "$N_MINUS_ONE_RELEASE_SHA256" \
  --expected-live-release-channel-sha256 "$LIVE_RELEASE_CHANNEL_SHA256" \
  --expected-selected-tuple-sha256 "$SELECTED_TUPLE_SHA256" \
  --output-live-release-channel "$LIVE_RELEASE_CHANNEL_EVIDENCE" \
  >/dev/null
assert_bound_regular_file \
  "$LIVE_RELEASE_CHANNEL_EVIDENCE" \
  "$LIVE_RELEASE_CHANNEL_SHA256" \
  "$(size_file "$LIVE_RELEASE_CHANNEL_EVIDENCE")" \
  "live release-channel root"

declare -A PREVIOUS=()
while IFS='=' read -r key value; do
  case "$key" in
    artifact_file_name|artifact_sha256|artifact_size_bytes|artifact_url|generation_id|manifest_sha256|manifest_url|version)
      [[ -z "${PREVIOUS[$key]+x}" ]] || fail "duplicate N-1 binding $key"
      PREVIOUS["$key"]="$value"
      ;;
  esac
done < <(
  python3 "$CONTRACT_SCRIPT" validate-n-minus-one \
    --binding-json "$N_MINUS_ONE_BINDING_JSON" \
    --platform linux \
    --rid linux-x64
)
for key in artifact_file_name artifact_sha256 artifact_size_bytes artifact_url \
  generation_id manifest_sha256 manifest_url version; do
  [[ -n "${PREVIOUS[$key]:-}" ]] || fail "N-1 binding $key is missing"
done
[[ "${PREVIOUS[version]}" != "$CANDIDATE_VERSION" ]] \
  || fail "candidate and N-1 versions must be distinct"

PRIVATE_ROOT="$(mktemp -d "${RUNNER_TEMP:-/tmp}/chummer-linux-lifecycle.XXXXXX")"
ROOT_PACKAGE_STAGE=""
PACKAGE_NAME=""
cleanup() {
  if [[ -n "$PACKAGE_NAME" ]] && dpkg-query -W -f='${db:Status-Status}' "$PACKAGE_NAME" 2>/dev/null | grep -q '^installed$'; then
    sudo DEBIAN_FRONTEND=noninteractive apt-get remove --purge -y "$PACKAGE_NAME" >/dev/null 2>&1 || true
  fi
  if [[ -n "${PRIVATE_ROOT:-}" && -d "$PRIVATE_ROOT" ]]; then
    rm -rf "$PRIVATE_ROOT"
  fi
  if [[ "$ROOT_PACKAGE_STAGE" == /var/tmp/chummer-linux-lifecycle-packages.* ]]; then
    sudo rm -rf -- "$ROOT_PACKAGE_STAGE" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

OLD_PACKAGE="$PRIVATE_ROOT/${PREVIOUS[artifact_file_name]}"
OLD_MANIFEST="$PRIVATE_ROOT/N_MINUS_ONE_RELEASE_CHANNEL.generated.json"
AUTH_START="$(utc_now)"
download_pinned \
  "${PREVIOUS[manifest_url]}" \
  "$OLD_MANIFEST" \
  "${PREVIOUS[manifest_sha256]}" \
  0 \
  $((8 * 1024 * 1024)) \
  "N-1 release manifest"
download_pinned \
  "${PREVIOUS[artifact_url]}" \
  "$OLD_PACKAGE" \
  "${PREVIOUS[artifact_sha256]}" \
  "${PREVIOUS[artifact_size_bytes]}" \
  "${PREVIOUS[artifact_size_bytes]}" \
  "N-1 package"
python3 "$CONTRACT_SCRIPT" validate-n-minus-one-manifest \
  --manifest "$OLD_MANIFEST" \
  --binding-json "$N_MINUS_ONE_BINDING_JSON" \
  --platform linux \
  --rid linux-x64 >/dev/null
MANIFEST_EVIDENCE="$OUTPUT_ROOT/n-minus-one-release-manifest.json"
cp -- "$OLD_MANIFEST" "$MANIFEST_EVIDENCE"
assert_bound_regular_file \
  "$MANIFEST_EVIDENCE" \
  "${PREVIOUS[manifest_sha256]}" \
  "$(size_file "$OLD_MANIFEST")" \
  "copied N-1 release manifest"

protected_output="$(
  sudo -H /usr/bin/python3 "$SIGNING_SCRIPT" stage-lifecycle-packages \
    --candidate "$CANDIDATE" \
    --expected-candidate-sha256 "$CANDIDATE_SHA256" \
    --expected-candidate-size "$CANDIDATE_SIZE_BYTES" \
    --n-minus-one "$OLD_PACKAGE" \
    --expected-n-minus-one-sha256 "${PREVIOUS[artifact_sha256]}" \
    --expected-n-minus-one-size "${PREVIOUS[artifact_size_bytes]}"
)"
declare -A PROTECTED_PACKAGE=()
while IFS='=' read -r key value; do
  case "$key" in
    candidate_path|n_minus_one_path|protected_root)
      [[ -z "${PROTECTED_PACKAGE[$key]+x}" ]] \
        || fail "duplicate protected package result $key"
      PROTECTED_PACKAGE["$key"]="$value"
      ;;
  esac
done <<<"$protected_output"
for key in candidate_path n_minus_one_path protected_root; do
  [[ -n "${PROTECTED_PACKAGE[$key]:-}" ]] \
    || fail "protected package staging omitted $key"
done
ROOT_PACKAGE_STAGE="${PROTECTED_PACKAGE[protected_root]}"
PROTECTED_CANDIDATE="${PROTECTED_PACKAGE[candidate_path]}"
PROTECTED_OLD_PACKAGE="${PROTECTED_PACKAGE[n_minus_one_path]}"
[[ "$ROOT_PACKAGE_STAGE" == /var/tmp/chummer-linux-lifecycle-packages.* \
   && "$PROTECTED_CANDIDATE" == "$ROOT_PACKAGE_STAGE/candidate.deb" \
   && "$PROTECTED_OLD_PACKAGE" == "$ROOT_PACKAGE_STAGE/n-minus-one.deb" ]] \
  || fail "protected package staging returned unsafe paths"

OLD_PACKAGE_NAME="$(sudo dpkg-deb -f "$PROTECTED_OLD_PACKAGE" Package)"
CANDIDATE_PACKAGE_NAME="$(sudo dpkg-deb -f "$PROTECTED_CANDIDATE" Package)"
OLD_ARCH="$(sudo dpkg-deb -f "$PROTECTED_OLD_PACKAGE" Architecture)"
CANDIDATE_ARCH="$(sudo dpkg-deb -f "$PROTECTED_CANDIDATE" Architecture)"
OLD_DPKG_VERSION="$(sudo dpkg-deb -f "$PROTECTED_OLD_PACKAGE" Version)"
CANDIDATE_DPKG_VERSION="$(sudo dpkg-deb -f "$PROTECTED_CANDIDATE" Version)"
[[ -n "$OLD_PACKAGE_NAME" && "$OLD_PACKAGE_NAME" == "$CANDIDATE_PACKAGE_NAME" ]] \
  || fail "N-1 and candidate package identities differ"
[[ "$OLD_ARCH" == "amd64" && "$CANDIDATE_ARCH" == "amd64" ]] \
  || fail "package architecture does not match linux-x64"
[[ -n "$OLD_DPKG_VERSION" && -n "$CANDIDATE_DPKG_VERSION" && "$OLD_DPKG_VERSION" != "$CANDIDATE_DPKG_VERSION" ]] \
  || fail "candidate package version did not advance from N-1"
dpkg --compare-versions "$CANDIDATE_DPKG_VERSION" gt "$OLD_DPKG_VERSION" \
  || fail "candidate Debian version does not sort after N-1"
PACKAGE_NAME="$OLD_PACKAGE_NAME"
if dpkg-query -W -f='${db:Status-Status}' "$PACKAGE_NAME" 2>/dev/null | grep -q '^installed$'; then
  fail "Linux runner is not clean: $PACKAGE_NAME is already installed"
fi
AUTH_END="$(utc_now)"

STATE_ROOT="$PRIVATE_ROOT/user-state"
RUNTIME_HOME="$PRIVATE_ROOT/user-home"
mkdir -p "$STATE_ROOT" "$RUNTIME_HOME"
SENTINEL="$STATE_ROOT/lifecycle-user-state.txt"
printf 'chummer-native-lifecycle-state-%s' "$(date +%s%N)" >"$SENTINEL"
SENTINEL_BEFORE="$(sha256_file "$SENTINEL")"
export CHUMMER_DESKTOP_STATE_ROOT="$STATE_ROOT"
export XDG_CONFIG_HOME="$RUNTIME_HOME/.config"
export XDG_DATA_HOME="$RUNTIME_HOME/.local/share"
export XDG_STATE_HOME="$RUNTIME_HOME/.local/state"
export XDG_CACHE_HOME="$RUNTIME_HOME/.cache"

LAUNCHER="/opt/chummer6/avalonia-linux-x64/Chummer.Avalonia"
WRAPPER="/usr/bin/chummer6-avalonia"
DESKTOP_ENTRY="/usr/share/applications/chummer6-avalonia.desktop"

INSTALL_START="$(utc_now)"
# The evidence log is deliberately opened by the unprivileged runner shell.
# shellcheck disable=SC2024
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y "$PROTECTED_OLD_PACKAGE" \
  >"$OUTPUT_ROOT/n-minus-one-install.log" 2>&1
[[ "$(dpkg-query -W -f='${db:Status-Status}' "$PACKAGE_NAME")" == "installed" ]] \
  || fail "N-1 package did not reach installed state"
[[ "$(dpkg-query -W -f='${Version}' "$PACKAGE_NAME")" == "$OLD_DPKG_VERSION" ]] \
  || fail "installed N-1 Debian version differs"
[[ -x "$LAUNCHER" && -x "$WRAPPER" && -f "$DESKTOP_ENTRY" ]] \
  || fail "N-1 normal install did not create all native launchers"
OLD_LAUNCHER_SHA="$(sha256_file "$LAUNCHER")"
INSTALL_END="$(utc_now)"

run_core_workflow() {
  local label="$1"
  local version="$2"
  local artifact_sha="$3"
  local startup_receipt="$OUTPUT_ROOT/$label-startup.receipt.json"
  local mouse_receipt="$OUTPUT_ROOT/$label-mouse-first.receipt.json"
  local screenshot_dir="$OUTPUT_ROOT/$label-mouse-first-screenshots"
  mkdir -p "$screenshot_dir"

  export CHUMMER_DESKTOP_STARTUP_SMOKE_RECEIPT="$startup_receipt"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_FAILURE_PACKET="$OUTPUT_ROOT/$label-startup.failure.json"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_ARTIFACT_DIGEST="sha256:$artifact_sha"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_HOST_CLASS="github-actions-linux-x64"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_RELEASE_VERSION="$version"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_RID="linux-x64"
  export CHUMMER_DESKTOP_STARTUP_SMOKE_READY_CHECKPOINT="pre_ui_event_loop"
  export CHUMMER_DESKTOP_UPDATE_ENABLED=0
  xvfb-run -a "$LAUNCHER" --startup-smoke >"$OUTPUT_ROOT/$label-startup.log" 2>&1
  assert_passing_json "$startup_receipt" "$label startup"

  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$mouse_receipt"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$OUTPUT_ROOT/$label-mouse-first.failure.json"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$screenshot_dir"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$OUTPUT_ROOT/$label-mouse-first.trace.json"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_ARTIFACT_DIGEST="sha256:$artifact_sha"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_HOST_CLASS="github-actions-linux-x64"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RELEASE_VERSION="$version"
  export CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RID="linux-x64"
  export CHUMMER_DESKTOP_RELEASE_CHANNEL="flagship"
  xvfb-run -a "$LAUNCHER" --mouse-first-user-journey \
    >"$OUTPUT_ROOT/$label-mouse-first.log" 2>&1
  assert_passing_json "$mouse_receipt" "$label mouse-first"
}

OLD_CORE_START="$(utc_now)"
run_core_workflow "n-minus-one" "${PREVIOUS[version]}" "${PREVIOUS[artifact_sha256]}"
OLD_CORE_END="$(utc_now)"

UPDATE_START="$(utc_now)"
# The evidence log is deliberately opened by the unprivileged runner shell.
# shellcheck disable=SC2024
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y "$PROTECTED_CANDIDATE" \
  >"$OUTPUT_ROOT/candidate-update.log" 2>&1
[[ "$(dpkg-query -W -f='${db:Status-Status}' "$PACKAGE_NAME")" == "installed" ]] \
  || fail "candidate package did not reach installed state"
[[ "$(dpkg-query -W -f='${Version}' "$PACKAGE_NAME")" == "$CANDIDATE_DPKG_VERSION" ]] \
  || fail "installed candidate Debian version differs"
[[ -x "$LAUNCHER" && -x "$WRAPPER" && -f "$DESKTOP_ENTRY" ]] \
  || fail "candidate update did not retain all native launchers"
CANDIDATE_LAUNCHER_SHA="$(sha256_file "$LAUNCHER")"
[[ "$CANDIDATE_LAUNCHER_SHA" != "$OLD_LAUNCHER_SHA" ]] \
  || fail "candidate update left the N-1 launcher bytes installed"
SENTINEL_AFTER_UPDATE="$(sha256_file "$SENTINEL")"
[[ "$SENTINEL_AFTER_UPDATE" == "$SENTINEL_BEFORE" ]] \
  || fail "candidate update changed user state"
UPDATE_END="$(utc_now)"

CANDIDATE_CORE_START="$(utc_now)"
run_core_workflow "candidate" "$CANDIDATE_VERSION" "$CANDIDATE_SHA256"
CANDIDATE_CORE_END="$(utc_now)"

UNINSTALL_START="$(utc_now)"
# The evidence log is deliberately opened by the unprivileged runner shell.
# shellcheck disable=SC2024
sudo DEBIAN_FRONTEND=noninteractive apt-get remove --purge -y "$PACKAGE_NAME" \
  >"$OUTPUT_ROOT/candidate-uninstall.log" 2>&1
if dpkg-query -W -f='${db:Status-Status}' "$PACKAGE_NAME" 2>/dev/null | grep -q '^installed$'; then
  fail "normal package uninstall left $PACKAGE_NAME installed"
fi
[[ ! -e "$LAUNCHER" && ! -e "$WRAPPER" && ! -e "$DESKTOP_ENTRY" ]] \
  || fail "normal package uninstall left native launcher files"
SENTINEL_AFTER_UNINSTALL="$(sha256_file "$SENTINEL")"
[[ "$SENTINEL_AFTER_UNINSTALL" == "$SENTINEL_BEFORE" ]] \
  || fail "normal package uninstall changed user state"
UNINSTALL_END="$(utc_now)"

export LIFECYCLE_OUTPUT_ROOT="$OUTPUT_ROOT"
export LIFECYCLE_CANDIDATE_PATH="$CANDIDATE"
export LIFECYCLE_CANDIDATE_SHA256="$CANDIDATE_SHA256"
export LIFECYCLE_CANDIDATE_SIZE_BYTES="$CANDIDATE_SIZE_BYTES"
export LIFECYCLE_CANDIDATE_VERSION="$CANDIDATE_VERSION"
export LIFECYCLE_PREVIOUS_JSON="$N_MINUS_ONE_BINDING_JSON"
export LIFECYCLE_N_MINUS_ONE_RELEASE_SHA256="$N_MINUS_ONE_RELEASE_SHA256"
export LIFECYCLE_LIVE_RELEASE_CHANNEL_SHA256="$LIVE_RELEASE_CHANNEL_SHA256"
export LIFECYCLE_SELECTED_TUPLE_SHA256="$SELECTED_TUPLE_SHA256"
export LIFECYCLE_SIGNING_RECEIPT_SHA256="$SIGNING_RECEIPT_SHA256"
export LIFECYCLE_SIGNED_EXPORT_RECEIPT_SHA256="$SIGNED_EXPORT_RECEIPT_SHA256"
export LIFECYCLE_VERIFICATION_POLICY_SHA256="$VERIFICATION_POLICY_SHA256"
export LIFECYCLE_PUBLIC_KEYRING_SHA256="$PUBLIC_KEYRING_SHA256"
export LIFECYCLE_TRANSACTION_MANIFEST_SHA256="$TRANSACTION_MANIFEST_SHA256"
export LIFECYCLE_PRIMARY_FINGERPRINT="$EXPECTED_PRIMARY_FINGERPRINT"
export LIFECYCLE_VERIFICATION_BINARY_SHA256="${SIGNATURE_VERIFY[verification_binary_sha256]}"
export LIFECYCLE_VERIFICATION_PACKAGE_VERSION="${SIGNATURE_VERIFY[verification_package_version]}"
export LIFECYCLE_TAMPER_EXIT_CODE="${SIGNATURE_VERIFY[tamper_exit_code]}"
export LIFECYCLE_SOURCE_REPOSITORY="$SOURCE_REPOSITORY"
export LIFECYCLE_SOURCE_WORKFLOW="$SOURCE_WORKFLOW"
export LIFECYCLE_SOURCE_RUN_ID="$SOURCE_RUN_ID"
export LIFECYCLE_SOURCE_RUN_ATTEMPT="$SOURCE_RUN_ATTEMPT"
export LIFECYCLE_SOURCE_REF="$SOURCE_REF"
export LIFECYCLE_SOURCE_SHA="$SOURCE_SHA"
export LIFECYCLE_SOURCE_ACTOR="$SOURCE_ACTOR"
export LIFECYCLE_SOURCE_TRIGGERING_ACTOR="$SOURCE_TRIGGERING_ACTOR"
export LIFECYCLE_AUTH_START="$AUTH_START"
export LIFECYCLE_AUTH_END="$AUTH_END"
export LIFECYCLE_INSTALL_START="$INSTALL_START"
export LIFECYCLE_INSTALL_END="$INSTALL_END"
export LIFECYCLE_OLD_CORE_START="$OLD_CORE_START"
export LIFECYCLE_OLD_CORE_END="$OLD_CORE_END"
export LIFECYCLE_UPDATE_START="$UPDATE_START"
export LIFECYCLE_UPDATE_END="$UPDATE_END"
export LIFECYCLE_CANDIDATE_CORE_START="$CANDIDATE_CORE_START"
export LIFECYCLE_CANDIDATE_CORE_END="$CANDIDATE_CORE_END"
export LIFECYCLE_UNINSTALL_START="$UNINSTALL_START"
export LIFECYCLE_UNINSTALL_END="$UNINSTALL_END"
export LIFECYCLE_SENTINEL_BEFORE="$SENTINEL_BEFORE"
export LIFECYCLE_SENTINEL_AFTER_UPDATE="$SENTINEL_AFTER_UPDATE"
export LIFECYCLE_SENTINEL_AFTER_UNINSTALL="$SENTINEL_AFTER_UNINSTALL"
LIFECYCLE_KERNEL_VALUE="$(uname -sr | tr ' ' '-')"
export LIFECYCLE_KERNEL="$LIFECYCLE_KERNEL_VALUE"
export LIFECYCLE_PACKAGE_NAME="$PACKAGE_NAME"
export LIFECYCLE_OLD_DPKG_VERSION="$OLD_DPKG_VERSION"
export LIFECYCLE_CANDIDATE_DPKG_VERSION="$CANDIDATE_DPKG_VERSION"

RECEIPT_PATH="$OUTPUT_ROOT/DESKTOP_NATIVE_LIFECYCLE-linux-linux-x64.generated.json"
python3 - "$RECEIPT_PATH" <<'PY'
import hashlib
import json
import os
import pathlib
import sys
from datetime import UTC, datetime

receipt_path = pathlib.Path(sys.argv[1])
root = pathlib.Path(os.environ["LIFECYCLE_OUTPUT_ROOT"])
previous = json.loads(os.environ["LIFECYCLE_PREVIOUS_JSON"])


def binding(name: str, role: str) -> dict[str, object]:
    path = root / name
    data = path.read_bytes()
    return {
        "path": name,
        "role": role,
        "sha256": hashlib.sha256(data).hexdigest(),
        "sizeBytes": len(data),
    }


old_startup = binding("n-minus-one-startup.receipt.json", "n-minus-one-core-startup")
old_mouse = binding("n-minus-one-mouse-first.receipt.json", "n-minus-one-core-mouse-first")
candidate_startup = binding("candidate-startup.receipt.json", "candidate-core-startup")
candidate_mouse = binding("candidate-mouse-first.receipt.json", "candidate-core-mouse-first")
previous_manifest = binding(
    "n-minus-one-release-manifest.json",
    "n-minus-one-release-manifest",
)
live_release_root = binding(
    "live-release-channel-root.json",
    "live-release-channel-root",
)
signing_receipt = binding(
    "candidate-linux-signing-receipt.json",
    "candidate-linux-signing-receipt",
)
signed_export_receipt = binding(
    "candidate-linux-signed-export-receipt.json",
    "candidate-linux-signed-export-receipt",
)
verification_policy = binding(
    "candidate-linux-debsig-policy.pol",
    "candidate-linux-debsig-policy",
)
public_keyring = binding(
    "candidate-linux-public-keyring.pgp",
    "candidate-linux-public-keyring",
)
transaction_manifest = binding(
    "candidate-linux-signing-transaction.json",
    "candidate-linux-signing-transaction-manifest",
)
evidence = sorted(
    [
        old_startup,
        old_mouse,
        candidate_startup,
        candidate_mouse,
        live_release_root,
        previous_manifest,
        public_keyring,
        signing_receipt,
        signed_export_receipt,
        transaction_manifest,
        verification_policy,
    ],
    key=lambda row: row["path"],
)

phases = [
    {
        "name": "artifact_authentication",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_AUTH_START"],
        "completedAt": os.environ["LIFECYCLE_AUTH_END"],
        "details": {
            "candidateDigestVerified": True,
            "candidateOriginSignatureVerified": True,
            "liveReleaseRootVerified": True,
            "nMinusOneDigestVerified": True,
            "nativePackageAuthorityVerified": True,
            "tamperNegativeVerified": True,
        },
    },
    {
        "name": "clean_install_n_minus_one",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_INSTALL_START"],
        "completedAt": os.environ["LIFECYCLE_INSTALL_END"],
        "details": {"installed": True, "launcherPresent": True},
    },
    {
        "name": "core_workflow_n_minus_one",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_OLD_CORE_START"],
        "completedAt": os.environ["LIFECYCLE_OLD_CORE_END"],
        "details": {"mouseFirstJourneyPassed": True, "startupSmokePassed": True},
    },
    {
        "name": "update_to_candidate",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_UPDATE_START"],
        "completedAt": os.environ["LIFECYCLE_UPDATE_END"],
        "details": {
            "candidateBytesInstalled": True,
            "installedVersionChanged": True,
            "statePreserved": True,
        },
    },
    {
        "name": "core_workflow_candidate",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_CANDIDATE_CORE_START"],
        "completedAt": os.environ["LIFECYCLE_CANDIDATE_CORE_END"],
        "details": {"mouseFirstJourneyPassed": True, "startupSmokePassed": True},
    },
    {
        "name": "normal_uninstall",
        "status": "passed",
        "startedAt": os.environ["LIFECYCLE_UNINSTALL_START"],
        "completedAt": os.environ["LIFECYCLE_UNINSTALL_END"],
        "details": {
            "launcherAbsent": True,
            "packageAbsent": True,
            "uninstallerInvoked": True,
        },
    },
]

receipt = {
    "candidate": {
        "artifactFileName": pathlib.Path(os.environ["LIFECYCLE_CANDIDATE_PATH"]).name,
        "sha256": os.environ["LIFECYCLE_CANDIDATE_SHA256"],
        "sizeBytes": int(os.environ["LIFECYCLE_CANDIDATE_SIZE_BYTES"]),
        "sourceCommit": os.environ["LIFECYCLE_SOURCE_SHA"],
        "version": os.environ["LIFECYCLE_CANDIDATE_VERSION"],
    },
    "contractName": "chummer6-ui.desktop-native-lifecycle-evidence",
    "contractVersion": 3,
    "coreWorkflow": {
        "candidate": {
            "mouseFirstReceipt": candidate_mouse,
            "startupReceipt": candidate_startup,
        },
        "nMinusOne": {
            "mouseFirstReceipt": old_mouse,
            "startupReceipt": old_startup,
        },
    },
    "evidenceFiles": evidence,
    "generatedAt": datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "livePredecessorAuthority": {
        "liveReleaseChannel": live_release_root,
        "liveReleaseChannelSha256": os.environ[
            "LIFECYCLE_LIVE_RELEASE_CHANNEL_SHA256"
        ],
        "nMinusOneReleaseSha256": os.environ[
            "LIFECYCLE_N_MINUS_ONE_RELEASE_SHA256"
        ],
        "selectedTupleSha256": os.environ[
            "LIFECYCLE_SELECTED_TUPLE_SHA256"
        ],
        "url": "https://chummer.run/downloads/RELEASE_CHANNEL.generated.json",
    },
    "nMinusOne": {
        "artifactFileName": previous["artifactFileName"],
        "artifactUrl": previous["artifactUrl"],
        "generationId": previous["generationId"],
        "manifestSha256": previous["manifestSha256"],
        "manifestUrl": previous["manifestUrl"],
        "releasedAt": previous["releasedAt"],
        "sha256": previous["artifactSha256"],
        "sizeBytes": previous["artifactSizeBytes"],
        "version": previous["version"],
    },
    "nativeRunner": {
        "architecture": "x64",
        "environment": "native",
        "kernel": os.environ["LIFECYCLE_KERNEL"],
        "runnerName": "GitHub-Actions",
        "runnerOs": "Linux",
        "source": {
            "actor": os.environ["LIFECYCLE_SOURCE_ACTOR"],
            "ref": os.environ["LIFECYCLE_SOURCE_REF"],
            "repository": os.environ["LIFECYCLE_SOURCE_REPOSITORY"],
            "rerunPolicy": "same-actor-only",
            "runAttempt": os.environ["LIFECYCLE_SOURCE_RUN_ATTEMPT"],
            "runId": os.environ["LIFECYCLE_SOURCE_RUN_ID"],
            "sha": os.environ["LIFECYCLE_SOURCE_SHA"],
            "triggeringActor": os.environ["LIFECYCLE_SOURCE_TRIGGERING_ACTOR"],
            "workflow": os.environ["LIFECYCLE_SOURCE_WORKFLOW"],
        },
    },
    "packageAuthority": {
        "candidate": {
            "architecture": "amd64",
            "packageName": os.environ["LIFECYCLE_PACKAGE_NAME"],
            "packageVersion": os.environ["LIFECYCLE_CANDIDATE_DPKG_VERSION"],
            "publicKeyring": public_keyring,
            "signer": {
                "longKeyId": os.environ["LIFECYCLE_PRIMARY_FINGERPRINT"][-16:],
                "primaryFingerprint": os.environ[
                    "LIFECYCLE_PRIMARY_FINGERPRINT"
                ],
                "signingFingerprint": os.environ[
                    "LIFECYCLE_PRIMARY_FINGERPRINT"
                ],
            },
            "signingReceipt": signing_receipt,
            "signedExportReceipt": signed_export_receipt,
            "transactionManifest": transaction_manifest,
            "verification": {
                "backend": "debsig-verify",
                "policySha256": os.environ[
                    "LIFECYCLE_VERIFICATION_POLICY_SHA256"
                ],
                "primaryFingerprint": os.environ[
                    "LIFECYCLE_PRIMARY_FINGERPRINT"
                ],
                "publicKeyringSha256": os.environ[
                    "LIFECYCLE_PUBLIC_KEYRING_SHA256"
                ],
                "signingReceiptSha256": os.environ[
                    "LIFECYCLE_SIGNING_RECEIPT_SHA256"
                ],
                "signedExportReceiptSha256": os.environ[
                    "LIFECYCLE_SIGNED_EXPORT_RECEIPT_SHA256"
                ],
                "transactionManifestSha256": os.environ[
                    "LIFECYCLE_TRANSACTION_MANIFEST_SHA256"
                ],
                "tamperExitCode": int(
                    os.environ["LIFECYCLE_TAMPER_EXIT_CODE"]
                ),
                "verificationBinarySha256": os.environ[
                    "LIFECYCLE_VERIFICATION_BINARY_SHA256"
                ],
                "verificationPackageVersion": os.environ[
                    "LIFECYCLE_VERIFICATION_PACKAGE_VERSION"
                ],
            },
            "verificationPolicy": verification_policy,
        },
        "manifestSha256": previous["manifestSha256"],
        "manifestReceipt": previous_manifest,
        "mode": "debsigs-origin-openpgp-and-immutable-manifest",
        "nMinusOne": {
            "architecture": "amd64",
            "packageName": os.environ["LIFECYCLE_PACKAGE_NAME"],
            "packageVersion": os.environ["LIFECYCLE_OLD_DPKG_VERSION"],
        },
    },
    "phases": phases,
    "platform": "linux",
    "rid": "linux-x64",
    "statePreservation": {
        "preservedAfterUninstall": True,
        "preservedAfterUpdate": True,
        "sentinelSha256AfterUninstall": os.environ["LIFECYCLE_SENTINEL_AFTER_UNINSTALL"],
        "sentinelSha256AfterUpdate": os.environ["LIFECYCLE_SENTINEL_AFTER_UPDATE"],
        "sentinelSha256BeforeUpdate": os.environ["LIFECYCLE_SENTINEL_BEFORE"],
    },
    "status": "passed",
    "uninstall": {
        "installRootRemoved": True,
        "launchersRemoved": True,
        "mode": "apt-remove-purge",
        "statusAfter": "not-installed",
    },
}
receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY

python3 "$CONTRACT_SCRIPT" verify-receipt \
  --receipt "$RECEIPT_PATH" \
  --evidence-root "$OUTPUT_ROOT"
printf 'lifecycle_receipt_sha256=%s\n' "$(sha256_file "$RECEIPT_PATH")"
printf 'lifecycle_receipt_path=%s\n' "$RECEIPT_PATH"
