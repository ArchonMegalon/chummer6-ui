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
local_keychain_password="${CHUMMER_MAC_LOCAL_KEYCHAIN_PASSWORD:-chummer-local-signing}"
local_cert_common_name="${CHUMMER_MAC_LOCAL_CERT_COMMON_NAME:-Chummer Local Code Signing}"
local_cert_subject="${CHUMMER_MAC_LOCAL_CERT_SUBJECT:-/CN=${local_cert_common_name}/O=Chummer/OU=Local Signing}"

runner_tmp="${RUNNER_TEMP:-${TMPDIR:-/tmp}}"
default_keychain_path="$runner_tmp/chummer-signing.keychain-db"
if [[ "$(uname -s)" == "Darwin" ]]; then
  default_keychain_path="${HOME}/Library/Keychains/chummer-signing.keychain-db"
fi
keychain_path="${CHUMMER_MAC_KEYCHAIN_PATH:-$default_keychain_path}"
certificate_path="$runner_tmp/chummer-signing.p12"
local_cert_tmpdir=""

security_available() {
  command -v security >/dev/null 2>&1
}

xcrun_available() {
  command -v xcrun >/dev/null 2>&1
}

openssl_available() {
  command -v openssl >/dev/null 2>&1
}

macos_host() {
  [[ "$(uname -s)" == "Darwin" ]]
}

write_outputs() {
  local prepared="$1"
  local resolved_sign_identity="$2"
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    {
      echo "prepared=$prepared"
      if [[ -n "$keychain_path" ]]; then
        echo "keychain_path=$keychain_path"
      fi
      echo "sign_identity=$resolved_sign_identity"
      echo "notary_profile=$notary_profile"
    } >>"$GITHUB_OUTPUT"
  else
    cat <<EOF
prepared=$prepared
keychain_path=$keychain_path
sign_identity=$resolved_sign_identity
notary_profile=$notary_profile
EOF
  fi
}

prepare_keychain() {
  mkdir -p "$(dirname "$keychain_path")"
  if [[ ! -f "$keychain_path" ]]; then
    security create-keychain -p "$keychain_password" "$keychain_path"
  fi
  security set-keychain-settings -lut 21600 "$keychain_path"
  security unlock-keychain -p "$keychain_password" "$keychain_path"
  mapfile -t existing_keychains < <(security list-keychains -d user | tr -d '"' | sed '/^[[:space:]]*$/d')
  local merged_keychains=("$keychain_path")
  local existing_keychain
  for existing_keychain in "${existing_keychains[@]}"; do
    if [[ "$existing_keychain" != "$keychain_path" ]]; then
      merged_keychains+=("$existing_keychain")
    fi
  done
  security list-keychains -d user -s "${merged_keychains[@]}"
}

resolve_codesigning_identity() {
  local expected="${1:-}"
  if [[ -n "$expected" ]]; then
    security find-identity -v -p codesigning "$keychain_path" | awk -F'"' -v expected="$expected" '$2 == expected { print $2; exit }'
    return
  fi
  security find-identity -v -p codesigning "$keychain_path" | awk -F'"' '/"/ { print $2; exit }'
}

import_certificate_into_keychain() {
  local import_password="$1"
  security import "$certificate_path" -k "$keychain_path" -P "$import_password" -T /usr/bin/codesign -T /usr/bin/security
  security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "$keychain_password" "$keychain_path"
}

generate_local_codesigning_certificate() {
  local export_password="$1"
  local common_name="$2"
  local subject="$3"
  local_cert_tmpdir="$(mktemp -d "$runner_tmp/chummer-local-signing.XXXXXX")"
  local config_path="$local_cert_tmpdir/openssl.cnf"
  local key_path="$local_cert_tmpdir/local-signing.key"
  local cert_path="$local_cert_tmpdir/local-signing.crt"

  cat >"$config_path" <<EOF
[ req ]
distinguished_name = req_dn
x509_extensions = v3_codesign
prompt = no

[ req_dn ]
CN = $common_name
O = Chummer
OU = Local Signing

[ v3_codesign ]
basicConstraints = critical, CA:false
keyUsage = critical, digitalSignature
extendedKeyUsage = critical, codeSigning
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid,issuer
EOF

  openssl req \
    -x509 \
    -newkey rsa:2048 \
    -sha256 \
    -nodes \
    -days 3650 \
    -config "$config_path" \
    -extensions v3_codesign \
    -subj "$subject" \
    -keyout "$key_path" \
    -out "$cert_path"

  openssl pkcs12 -export \
    -out "$certificate_path" \
    -inkey "$key_path" \
    -in "$cert_path" \
    -name "$common_name" \
    -passout "pass:$export_password"
}

store_notary_credentials_if_configured() {
  if [[ -n "$apple_id" && -n "$team_id" && -n "$app_password" ]]; then
    if ! xcrun_available; then
      echo "xcrun is required to store macOS notarization credentials." >&2
      exit 1
    fi
    xcrun notarytool store-credentials "$notary_profile" \
      --apple-id "$apple_id" \
      --team-id "$team_id" \
      --password "$app_password"
  elif env_truthy "$required"; then
    echo "macOS public release requires CHUMMER_MAC_APPLE_ID, CHUMMER_MAC_TEAM_ID, and CHUMMER_MAC_APPLE_APP_PASSWORD for notarization." >&2
    exit 1
  fi
}

cleanup() {
  rm -f "$certificate_path"
  if [[ -n "$local_cert_tmpdir" ]]; then
    rm -rf "$local_cert_tmpdir"
  fi
}
trap cleanup EXIT

if [[ -z "$cert_base64" ]]; then
  if env_truthy "$required" && [[ -z "$sign_identity_hint" ]]; then
    echo "macOS public release requires either CHUMMER_MAC_CERTIFICATE_P12_BASE64 or a preconfigured CHUMMER_MAC_APP_SIGN_IDENTITY." >&2
    exit 1
  fi

  if [[ -z "$sign_identity_hint" ]] && macos_host && security_available && openssl_available; then
    if [[ -z "$keychain_password" ]]; then
      keychain_password="$local_keychain_password"
    fi
    prepare_keychain
    sign_identity_hint="$(resolve_codesigning_identity "$local_cert_common_name")"
    if [[ -z "$sign_identity_hint" ]]; then
      generate_local_codesigning_certificate "$keychain_password" "$local_cert_common_name" "$local_cert_subject"
      import_certificate_into_keychain "$keychain_password"
      sign_identity_hint="$(resolve_codesigning_identity "$local_cert_common_name")"
    fi
    if [[ -z "$sign_identity_hint" ]]; then
      echo "Unable to resolve a local codesigning identity from $keychain_path." >&2
      exit 1
    fi
    store_notary_credentials_if_configured
    write_outputs true "$sign_identity_hint"
    exit 0
  fi

  write_outputs false "$sign_identity_hint"
  exit 0
fi

if ! security_available; then
  echo "security CLI is required to import macOS signing identities." >&2
  exit 1
fi

if [[ -z "$keychain_password" ]]; then
  echo "CHUMMER_MAC_KEYCHAIN_PASSWORD is required when importing a macOS signing certificate." >&2
  exit 1
fi

python3 - "$certificate_path" "$cert_base64" <<'PY'
import base64
import pathlib
import re
import sys

target = pathlib.Path(sys.argv[1])
payload = re.sub(r"\s+", "", sys.argv[2] or "")
target.write_bytes(base64.b64decode(payload))
PY

prepare_keychain
import_certificate_into_keychain "$cert_password"

if [[ -z "$sign_identity_hint" ]]; then
  sign_identity_hint="$(resolve_codesigning_identity)"
fi

if [[ -z "$sign_identity_hint" ]]; then
  echo "Unable to resolve an imported codesigning identity from $keychain_path." >&2
  exit 1
fi

store_notary_credentials_if_configured
write_outputs true "$sign_identity_hint"
