#!/bin/sh
set -eu

umask 077

environment_name="${ASPNETCORE_ENVIRONMENT:-${DOTNET_ENVIRONMENT:-Production}}"
normalized_environment="$(printf '%s' "$environment_name" | tr '[:upper:]' '[:lower:]')"
repository="${CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY:-/var/lib/chummer-build/data-protection}"

if [ "$normalized_environment" = "production" ] \
    && [ -z "${CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD:-}" ]; then
    if [ ! -d "$repository" ]; then
        echo "Hosted Build production data-protection repository is unavailable." >&2
        exit 78
    fi

    # HostedBuildDataProtection takes ownership of this inherited descriptor,
    # validates its target/ownership/mode, duplicates it, and closes the source.
    exec 3<"$repository"
    export CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY_FD=3
fi

exec dotnet Chummer.Blazor.dll "$@"
