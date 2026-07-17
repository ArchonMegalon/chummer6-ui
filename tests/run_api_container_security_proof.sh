#!/bin/sh
set -eu

image="${CHUMMER_API_PROOF_IMAGE:-chummer-api:local}"
proof_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-api-container-proof.XXXXXX")"
container="chummer-api-security-proof-$$"

cleanup() {
    docker rm -f "$container" >/dev/null 2>&1 || true
    if [ -d "$proof_root" ]; then
        docker run --rm \
            --volume "$proof_root:/proof" \
            --entrypoint /bin/chown \
            mcr.microsoft.com/dotnet/aspnet:10.0 \
            -R "$(id -u):$(id -g)" /proof >/dev/null 2>&1 || true
    fi
    rm -rf "$proof_root"
}
trap cleanup EXIT HUP INT TERM

test "$(docker image inspect "$image" --format '{{.Config.User}}')" = "1654:1654"
install -d -m 0700 "$proof_root/state"
install -d -m 0700 "$proof_root/secrets"
printf '%s' "$(openssl rand -hex 32)" >"$proof_root/secrets/CHUMMER_PORTAL_OWNER_SHARED_KEY"
chmod 0400 "$proof_root/secrets/CHUMMER_PORTAL_OWNER_SHARED_KEY"
docker run --rm \
    --volume "$proof_root:/proof" \
    --entrypoint /bin/chown \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -R 1654:1654 /proof/state /proof/secrets

host_port="$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"
docker run -d \
    --name "$container" \
    --init \
    --cap-drop ALL \
    --security-opt no-new-privileges:true \
    --read-only \
    --tmpfs /tmp:rw,nosuid,nodev,noexec,size=64m,mode=1777 \
    --publish "127.0.0.1:$host_port:8080" \
    --env ASPNETCORE_ENVIRONMENT=Production \
    --env CHUMMER_STATE_PATH=/app/state \
    --volume "$proof_root/state:/app/state" \
    --volume "$proof_root/secrets:/run/secrets/chummer-config:ro" \
    "$image" >/dev/null

readiness_url="http://127.0.0.1:$host_port/health/ready"
attempt=0
until curl --fail --silent --show-error "$readiness_url" >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 60 ]; then
        docker logs "$container" >&2
        exit 1
    fi
    sleep 1
done

test -z "$(docker exec "$container" find /app/state -maxdepth 1 -name '.chummer-readiness-*' -print -quit)"
docker exec "$container" /bin/sh -c 'test ! -w /app/Chummer.Api.dll'
docker exec "$container" /bin/sh -c '! touch /app/root-filesystem-must-be-read-only 2>/dev/null'

child_pids="$(docker exec "$container" sed -n '1p' /proc/1/task/1/children)"
set -- $child_pids
test "$#" -eq 1
dotnet_pid="$1"
dotnet_executable="$(docker exec "$container" readlink "/proc/$dotnet_pid/exe")"
case "$dotnet_executable" in
    */dotnet) ;;
    *)
        printf '%s\n' "Container init child was not the dotnet runtime." >&2
        exit 1
        ;;
esac
test "$(docker exec "$container" /bin/sh -c "sed -n 's/^CapEff:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "0000000000000000"
test "$(docker exec "$container" /bin/sh -c "sed -n 's/^NoNewPrivs:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "1"

docker run --rm \
    --volume "$proof_root/state:/state" \
    --entrypoint /bin/chmod \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    0750 /state
test "$(curl --silent --output /dev/null --write-out '%{http_code}' "$readiness_url")" = "503"
docker run --rm \
    --volume "$proof_root/state:/state" \
    --entrypoint /bin/chmod \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    0700 /state
curl --fail --silent --show-error "$readiness_url" >/dev/null

docker exec "$container" /bin/sh -c 'touch /app/state/container-restart-proof'
docker restart "$container" >/dev/null
attempt=0
until curl --fail --silent --show-error "$readiness_url" >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 60 ]; then
        docker logs "$container" >&2
        exit 1
    fi
    sleep 1
done
docker exec "$container" test -f /app/state/container-restart-proof
docker exec "$container" rm /app/state/container-restart-proof
docker stop --time 30 "$container" >/dev/null
test "$(docker inspect "$container" --format '{{.State.ExitCode}}')" = "0"

printf '%s\n' "API Production container security, readiness, and restart persistence proof passed."
