#!/usr/bin/env bash
set -euo pipefail

normalize_token() {
  printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]' | xargs
}

env_truthy() {
  case "$(normalize_token "${1:-}")" in
    1|true|yes|on)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

required="${CHUMMER_MAC_SIGNING_REQUIRED:-false}"
if [[ -n "${CHUMMER_MAC_NOTARIZATION_REQUIRED:-}" ]] && env_truthy "${CHUMMER_MAC_NOTARIZATION_REQUIRED}"; then
  required=true
fi

cert_base64="${CHUMMER_MAC_CERTIFICATE_P12_BASE64:-}"
cert_password="${CHUMMER_MAC_CERTIFICATE_PASSWORD:-}"
keychain_password="${CHUMMER_MAC_KEYCHAIN_PASSWORD:-}"
sign_identity_hint="${CHUMMER_MAC_APP_SIGN_IDENTITY:-}"
notary_profile="${CHUMMER_MAC_NOTARY_PROFILE:-chummer-notary}"
apple_id="${CHUMMER_MAC_APPLE_ID:-}"
team_id="${CHUMMER_MAC_TEAM_ID:-}"
app_password="${CHUMMER_MAC_APPLE_APP_PASSWORD:-}"

if [[ -z "$cert_base64" ]]; then
  if env_truthy "$required" && [[ -z "$sign_identity_hint" ]]; then
    echo "macOS public release requires either CHUMMER_MAC_CERTIFICATE_P12_BASE64 or a preconfigured CHUMMER_MAC_APP_SIGN_IDENTITY." >&2
    exit 1
  fi

  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "prepared=false"
      echo "sign_identity=$sign_identity_hint"
      echo "notary_profile=$notary_profile"
    } >>"$GITHUB_OUTPUT"
  else
    cat <<EOF
prepared=false
sign_identity=$sign_identity_hint
notary_profile=$notary_profile
EOF
  fi
  exit 0
fi

if ! command -v security >/dev/null 2>&1; then
  echo "security CLI is required to import macOS signing identities." >&2
  exit 1
fi

if ! command -v xcrun >/dev/null 2>&1; then
  echo "xcrun is required to store macOS notarization credentials." >&2
  exit 1
fi

if [[ -z "$keychain_password" ]]; then
  echo "CHUMMER_MAC_KEYCHAIN_PASSWORD is required when importing a macOS signing certificate." >&2
  exit 1
fi

runner_tmp="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
keychain_path="${CHUMMER_MAC_KEYCHAIN_PATH:-$runner_tmp/chummer-signing.keychain-db}"
certificate_path="$runner_tmp/chummer-signing.p12"

cleanup() {
  rm -f "$certificate_path"
}
trap cleanup EXIT

python3 - "$certificate_path" "$cert_base64" <<'PY'
import base64
import pathlib
import re
import sys

target = pathlib.Path(sys.argv[1])
payload = re.sub(r"\s+", "", sys.argv[2] or "")
target.write_bytes(base64.b64decode(payload))
PY

security create-keychain -p "$keychain_password" "$keychain_path"
security set-keychain-settings -lut 21600 "$keychain_path"
security unlock-keychain -p "$keychain_password" "$keychain_path"
mapfile -t existing_keychains < <(security list-keychains -d user | tr -d '"')
security list-keychains -d user -s "$keychain_path" "${existing_keychains[@]}"
security import "$certificate_path" -k "$keychain_path" -P "$cert_password" -T /usr/bin/codesign -T /usr/bin/security
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$keychain_password" "$keychain_path"

if [[ -z "$sign_identity_hint" ]]; then
  sign_identity_hint="$(security find-identity -v -p codesigning "$keychain_path" | awk -F'"' '/"/ { print $2; exit }')"
fi

if [[ -z "$sign_identity_hint" ]]; then
  echo "Unable to resolve an imported codesigning identity from $keychain_path." >&2
  exit 1
fi

if [[ -n "$apple_id" && -n "$team_id" && -n "$app_password" ]]; then
  xcrun notarytool store-credentials "$notary_profile" \
    --apple-id "$apple_id" \
    --team-id "$team_id" \
    --password "$app_password"
elif env_truthy "$required"; then
  echo "macOS public release requires CHUMMER_MAC_APPLE_ID, CHUMMER_MAC_TEAM_ID, and CHUMMER_MAC_APPLE_APP_PASSWORD for notarization." >&2
  exit 1
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  {
    echo "prepared=true"
    echo "keychain_path=$keychain_path"
    echo "sign_identity=$sign_identity_hint"
    echo "notary_profile=$notary_profile"
  } >>"$GITHUB_OUTPUT"
else
  cat <<EOF
prepared=true
keychain_path=$keychain_path
sign_identity=$sign_identity_hint
notary_profile=$notary_profile
EOF
fi
