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

requested_channel="$(normalize_token "${CHUMMER_DESKTOP_RELEASE_CHANNEL:-${CHUMMER_RELEASE_CHANNEL:-${1:-}}}")"
if [[ -z "$requested_channel" ]]; then
  requested_channel="preview"
fi

allow_unsigned_public_release=false
if env_truthy "${CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE:-0}"; then
  allow_unsigned_public_release=true
fi

case "$requested_channel" in
  preview|docker)
    public_release=false
    ;;
  release_candidate|public_stable)
    public_release=true
    ;;
  rc)
    requested_channel="release_candidate"
    public_release=true
    ;;
  stable|public)
    requested_channel="public_stable"
    public_release=true
    ;;
  *)
    echo "Unsupported desktop release channel '$requested_channel'. Expected preview, docker, release_candidate, or public_stable." >&2
    exit 1
    ;;
esac

if [[ "$public_release" == "true" ]]; then
  if [[ "$allow_unsigned_public_release" == "true" ]]; then
    windows_signing_required=false
    mac_signing_required=false
    mac_notarization_required=false
  else
    windows_signing_required=true
    mac_signing_required=true
    mac_notarization_required=true
  fi
else
  windows_signing_required=false
  mac_signing_required=false
  mac_notarization_required=false
fi

cat <<EOF
channel=$requested_channel
public_release=$public_release
allow_unsigned_public_release=$allow_unsigned_public_release
windows_signing_required=$windows_signing_required
mac_signing_required=$mac_signing_required
mac_notarization_required=$mac_notarization_required
EOF
