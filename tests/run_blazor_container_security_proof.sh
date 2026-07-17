#!/bin/sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
entrypoint="$repo_root/Chummer.Blazor/docker-entrypoint.sh"
proof_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-blazor-container-proof.XXXXXX")"
publish_dir="$proof_root/publish"
container="chummer-blazor-security-proof-$$"
provided_image="${CHUMMER_BLAZOR_PROOF_IMAGE:-}"
image="${provided_image:-$container:local}"

cleanup() {
    docker rm -f "$container" >/dev/null 2>&1 || true
    if [ -z "$provided_image" ]; then
        docker image rm -f "$image" >/dev/null 2>&1 || true
    fi
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

if [ -z "$provided_image" ]; then
    dotnet publish "$repo_root/Chummer.Blazor/Chummer.Blazor.csproj" \
        -c Release \
        --no-restore \
        --nologo \
        -o "$publish_dir"
fi

install -d -m 0700 "$proof_root/state"
install -d -m 0700 "$proof_root/data-protection"
install -d -m 0700 "$proof_root/secrets"
install -d -m 0700 "$proof_root/secrets/certificates"
if [ -z "$provided_image" ]; then
    install -d -m 0755 "$proof_root/app"
    cp -R "$publish_dir/." "$proof_root/app/"
    chmod -R a+rX "$proof_root/app"
    install -m 0555 "$entrypoint" "$proof_root/chummer-blazor-entrypoint"
fi

openssl req -x509 -newkey rsa:3072 -sha256 -nodes \
    -subj "/CN=Chummer Build container proof" \
    -keyout "$proof_root/certificate.key" \
    -out "$proof_root/certificate.crt" \
    -days 1 >/dev/null 2>&1
printf '%s' "$(openssl rand -hex 24)" \
    >"$proof_root/secrets/CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD"
openssl pkcs12 -export \
    -inkey "$proof_root/certificate.key" \
    -in "$proof_root/certificate.crt" \
    -out "$proof_root/secrets/certificates/chummer-build-data-protection.p12" \
    -passout "file:$proof_root/secrets/CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD" \
    >/dev/null 2>&1
printf '%s' "$(openssl rand -base64 32)" \
    >"$proof_root/secrets/CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64"

chmod 0400 "$proof_root/secrets/CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PASSWORD"
chmod 0400 "$proof_root/secrets/CHUMMER_BUILD_OWNER_CHANNEL_HMAC_KEY_BASE64"
chmod 0400 "$proof_root/secrets/certificates/chummer-build-data-protection.p12"
docker run --rm \
    --volume "$proof_root:/proof" \
    --entrypoint /bin/chown \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -R 1654:1654 /proof/state /proof/data-protection /proof/secrets

if [ -z "$provided_image" ]; then
    docker build \
        --file "$repo_root/tests/Dockerfile.blazor-runtime-proof" \
        --tag "$image" \
        "$proof_root" >/dev/null
fi

test "$(docker image inspect "$image" --format '{{.Config.User}}')" = "1654:1654"

host_port="$(python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"

docker run -d \
    --name "$container" \
    --init \
    --cap-drop ALL \
    --security-opt no-new-privileges:true \
    --read-only \
    --tmpfs /tmp:rw,nosuid,nodev,noexec,size=64m,mode=1777 \
    --publish "127.0.0.1:$host_port:8080" \
    --workdir /app \
    --env ASPNETCORE_ENVIRONMENT=Production \
    --env ASPNETCORE_URLS=http://+:8080 \
    --env CHUMMER_ANALYTICS_PROVIDER=none \
    --env CHUMMER_STATE_PATH=/app/state \
    --env CHUMMER_BLAZOR_DATA_PROTECTION_KEYS_DIRECTORY=/var/lib/chummer-build/data-protection \
    --env CHUMMER_BLAZOR_DATA_PROTECTION_CERTIFICATE_PATH=/run/secrets/chummer-config/certificates/chummer-build-data-protection.p12 \
    --volume "$proof_root/state:/app/state" \
    --volume "$proof_root/data-protection:/var/lib/chummer-build/data-protection" \
    --volume "$proof_root/secrets:/run/secrets/chummer-config:ro" \
    "$image" >/dev/null

health_url="http://127.0.0.1:$host_port/health/ready"

attempt=0
until curl --fail --silent --show-error "$health_url" >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 60 ]; then
        docker logs "$container" >&2
        exit 1
    fi
    sleep 1
done

[ "${CHUMMER_CONTAINER_PROOF_DEBUG:-0}" != 1 ] || set -x
test "$(docker exec "$container" id -u)" = "1654"
test "$(docker exec "$container" id -g)" = "1654"
docker exec "$container" /bin/sh -c 'test -w /app/state'
docker exec "$container" /bin/sh -c 'test -w /var/lib/chummer-build/data-protection'
docker exec "$container" /bin/sh -c 'test ! -w /app/Chummer.Blazor.dll'
docker exec "$container" /bin/sh -c '! touch /app/root-filesystem-must-be-read-only 2>/dev/null'
child_pids="$(docker exec "$container" sed -n '1p' /proc/1/task/1/children)"
set -- $child_pids
if [ "$#" -ne 1 ]; then
    printf '%s\n' "Expected one direct init child, observed $#; refusing an ambiguous descriptor proof." >&2
    for child_pid in $child_pids; do
        docker exec "$container" readlink "/proc/$child_pid/exe" >&2 || true
    done
    exit 1
fi
dotnet_pid="$1"
dotnet_executable="$(docker exec "$container" readlink "/proc/$dotnet_pid/exe")"
case "$dotnet_executable" in
    */dotnet) ;;
    *)
        printf '%s\n' "Container init child was not the dotnet runtime." >&2
        exit 1
        ;;
esac
test -n "$dotnet_pid"
transferred_source_target="$(docker exec "$container" readlink "/proc/$dotnet_pid/fd/3" 2>/dev/null || true)"
test "$transferred_source_target" != "/var/lib/chummer-build/data-protection"
test "$(docker exec "$container" /bin/sh -c "sed -n 's/^CapEff:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "0000000000000000"
test "$(docker exec "$container" /bin/sh -c "sed -n 's/^NoNewPrivs:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "1"

curl --fail --silent --show-error "http://127.0.0.1:$host_port/app" >/dev/null
docker exec "$container" /bin/sh -c 'touch /app/state/container-restart-proof'
docker restart "$container" >/dev/null

attempt=0
until curl --fail --silent --show-error "$health_url" >/dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 60 ]; then
        docker logs "$container" >&2
        exit 1
    fi
    sleep 1
done

docker exec "$container" /bin/sh -c 'test -f /app/state/container-restart-proof'
docker exec "$container" rm /app/state/container-restart-proof
test -n "$(docker exec "$container" find /var/lib/chummer-build/data-protection -maxdepth 1 -type f -print -quit)"
docker stop --time 30 "$container" >/dev/null
test "$(docker inspect "$container" --format '{{.State.ExitCode}}')" = "0"

printf '%s\n' "Blazor Production container security proof passed."
