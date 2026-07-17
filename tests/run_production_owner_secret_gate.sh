#!/bin/sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"

assert_rejected() {
    assembly="$1"
    configured_value="$2"
    output_file="$(mktemp "${TMPDIR:-/tmp}/chummer-owner-secret-gate.XXXXXX")"

    if [ "$configured_value" = "__missing__" ]; then
        if env -u CHUMMER_PORTAL_OWNER_SHARED_KEY \
            ASPNETCORE_ENVIRONMENT=Production \
            timeout 15 dotnet "$assembly" >"$output_file" 2>&1; then
            rm -f "$output_file"
            echo "Production unexpectedly accepted a missing owner secret." >&2
            exit 1
        fi
    else
        if CHUMMER_PORTAL_OWNER_SHARED_KEY="$configured_value" \
            ASPNETCORE_ENVIRONMENT=Production \
            timeout 15 dotnet "$assembly" >"$output_file" 2>&1; then
            rm -f "$output_file"
            echo "Production unexpectedly accepted invalid owner material." >&2
            exit 1
        fi
    fi

    if ! sed -n '/Production requires CHUMMER_PORTAL_OWNER_SHARED_KEY/p' "$output_file" | read -r _; then
        sed -n '1,200p' "$output_file" >&2
        rm -f "$output_file"
        echo "Production did not return the expected owner-secret failure." >&2
        exit 1
    fi
    rm -f "$output_file"
}

for assembly in \
    "$repo_root/Chummer.Api/bin/Release/net10.0/Chummer.Api.dll" \
    "$repo_root/Chummer.Portal/bin/Release/net10.0/Chummer.Portal.dll"; do
    [ -f "$assembly" ] || {
        echo "Release assembly is missing: build API and Portal first." >&2
        exit 2
    }
    assert_rejected "$assembly" "__missing__"
    assert_rejected "$assembly" "too-short"
    assert_rejected "$assembly" "local-self-hosted-portal-shared-key"
done

printf '%s\n' "Production owner-secret rejection gate passed."
