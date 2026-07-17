#!/bin/sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
migration="$repo_root/Docker/migrate-state-volume-ownership.sh"
volume="chummer-state-migration-proof-$$"
rejected_volume="$volume-rejected"

cleanup() {
    docker volume rm -f "$volume" "$rejected_volume" >/dev/null 2>&1 || true
}
trap cleanup EXIT HUP INT TERM

docker volume create "$volume" >/dev/null
docker run --rm \
    --volume "$volume:/app/state" \
    --entrypoint /bin/sh \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -c 'umask 077; chmod 0700 /app/state; mkdir /app/state/workspaces; printf "%s" "proof-content" > /app/state/workspaces/receipt.json'

receipt="$(docker run --rm \
    --user 0:0 \
    --volume "$volume:/app/state" \
    --volume "$migration:/migration:ro" \
    --entrypoint /bin/sh \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    /migration)"

case "$receipt" in
    '{"status":"passed","uid":1654,"gid":1654,"contentSha256":"'*) ;;
    *) echo "Migration receipt was not canonical." >&2; exit 1 ;;
esac

docker run --rm \
    --user 1654:1654 \
    --volume "$volume:/app/state:ro" \
    --entrypoint /bin/sh \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -c 'test "$(stat -c %u:%g /app/state/workspaces/receipt.json)" = "1654:1654" && test "$(cat /app/state/workspaces/receipt.json)" = "proof-content"'

docker volume create "$rejected_volume" >/dev/null
docker run --rm \
    --volume "$rejected_volume:/app/state" \
    --entrypoint /bin/sh \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -c 'umask 077; chmod 0700 /app/state; ln -s /etc/passwd /app/state/escape'

if docker run --rm \
    --user 0:0 \
    --volume "$rejected_volume:/app/state" \
    --volume "$migration:/migration:ro" \
    --entrypoint /bin/sh \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    /migration >/dev/null 2>&1; then
    echo "Migration unexpectedly accepted a symbolic link." >&2
    exit 1
fi

printf '%s\n' "State volume ownership migration proof passed."
