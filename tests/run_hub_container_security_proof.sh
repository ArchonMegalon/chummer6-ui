#!/bin/sh
set -eu
umask 077

image="${CHUMMER_HUB_PROOF_IMAGE:-chummer-hub-web:local}"
proof_root="$(mktemp -d "${TMPDIR:-/tmp}/chummer-hub-container-proof.XXXXXX")"
container="chummer-hub-security-proof-$$"
ring_a_volume="chummer-hub-data-protection-a-proof-$$"
ring_b_volume="chummer-hub-data-protection-b-proof-$$"
missing_password_volume="chummer-hub-data-protection-missing-password-proof-$$"
wrong_password_volume="chummer-hub-data-protection-wrong-password-proof-$$"
keys_path="/var/lib/chummer-hub/data-protection"
secrets_path="/run/secrets/chummer-config"
current_certificate_path="$secrets_path/certificates/hub-current.p12"
previous_certificate_path="$secrets_path/certificates/hub-previous.p12"

cleanup() {
    docker rm -f "$container" >/dev/null 2>&1 || true
    docker volume rm -f \
        "$ring_a_volume" \
        "$ring_b_volume" \
        "$missing_password_volume" \
        "$wrong_password_volume" >/dev/null 2>&1 || true
    if [ -d "$proof_root" ]; then
        docker run --rm \
            --user 0 \
            --volume "$proof_root:/proof" \
            --entrypoint /bin/chown \
            mcr.microsoft.com/dotnet/aspnet:10.0 \
            -R "$(id -u):$(id -g)" /proof >/dev/null 2>&1 || true
    fi
    rm -rf "$proof_root"
}
trap cleanup EXIT HUP INT TERM

fail_with_log_receipt() {
    message="$1"
    log_file="$2"
    log_digest="unavailable"
    if [ -f "$log_file" ]; then
        log_digest="$(sha256sum "$log_file")"
        log_digest="${log_digest%% *}"
    fi
    printf '%s (sanitized log sha256=%s)\n' "$message" "$log_digest" >&2
    exit 1
}

assert_log_is_sanitized() {
    log_file="$1"
    if ! python3 - "$log_file" "$proof_root/sentinels" "$proof_root" <<'PY'
from pathlib import Path
import sys

log_path = Path(sys.argv[1])
sentinel_root = Path(sys.argv[2])
host_proof_root = sys.argv[3].encode("utf-8")
payload = log_path.read_bytes() if log_path.exists() else b""
for sentinel in sentinel_root.iterdir():
    value = sentinel.read_bytes().strip()
    if value and value in payload:
        raise SystemExit(1)
if host_proof_root and host_proof_root in payload:
    raise SystemExit(1)
if b"BEGIN PRIVATE KEY" in payload or b"BEGIN RSA PRIVATE KEY" in payload:
    raise SystemExit(1)
PY
    then
        printf '%s\n' "Hub proof detected secret material in captured logs; refusing to print them." >&2
        exit 1
    fi
}

capture_logs() {
    label="$1"
    log_file="$proof_root/logs/$label.log"
    docker logs "$container" >"$log_file" 2>&1 || true
    chmod 0600 "$log_file"
    assert_log_is_sanitized "$log_file"
    printf '%s\n' "$log_file"
}

assert_data_protection_failure_log() {
    log_file="$1"
    if ! python3 - "$log_file" <<'PY'
from pathlib import Path
import sys

payload = Path(sys.argv[1]).read_bytes().lower()
if not any(marker in payload for marker in (
    b"chummer_hub_data_protection",
    b"data protection",
    b"dataprotection",
)):
    raise SystemExit(1)
PY
    then
        fail_with_log_receipt \
            "Hub rejection did not identify the Data Protection boundary." \
            "$log_file"
    fi
}

run_detached() {
    docker rm -f "$container" >/dev/null 2>&1 || true
    docker run -d \
        --name "$container" \
        --init \
        --cap-drop ALL \
        --security-opt no-new-privileges:true \
        --read-only \
        --tmpfs /tmp:rw,nosuid,nodev,noexec,size=64m,mode=1777 \
        --env ASPNETCORE_ENVIRONMENT=Production \
        "$@" \
        "$image" >/dev/null
}

expect_rejected() {
    label="$1"
    shift
    run_detached "$@"

    attempt=0
    while [ "$attempt" -lt 20 ]; do
        running="$(docker inspect "$container" --format '{{.State.Running}}')"
        [ "$running" = "true" ] || break
        attempt=$((attempt + 1))
        sleep 1
    done

    if [ "$(docker inspect "$container" --format '{{.State.Running}}')" = "true" ]; then
        log_file="$(capture_logs "$label-unexpectedly-running")"
        docker stop --time 5 "$container" >/dev/null 2>&1 || true
        fail_with_log_receipt \
            "Hub Production unexpectedly remained running for rejected case '$label'." \
            "$log_file"
    fi

    exit_code="$(docker inspect "$container" --format '{{.State.ExitCode}}')"
    log_file="$(capture_logs "$label")"
    if [ "$exit_code" = "0" ]; then
        fail_with_log_receipt \
            "Hub Production unexpectedly exited successfully for rejected case '$label'." \
            "$log_file"
    fi
    assert_data_protection_failure_log "$log_file"
    docker rm "$container" >/dev/null
}

free_host_port() {
    python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}

start_positive() {
    label="$1"
    shift
    active_label="$label"
    host_port="$(free_host_port)"
    health_url="http://127.0.0.1:$host_port/hub/health"
    run_detached \
        --publish "127.0.0.1:$host_port:8080" \
        --env CHUMMER_HUB_PATH_BASE=/hub \
        "$@"
}

wait_for_health() {
    attempt=0
    while [ "$attempt" -lt 60 ]; do
        if curl --fail --silent --show-error "$health_url" >/dev/null 2>&1; then
            return 0
        fi
        if [ "$(docker inspect "$container" --format '{{.State.Running}}')" != "true" ]; then
            log_file="$(capture_logs "$active_label-startup-failed")"
            fail_with_log_receipt \
                "Hub Production exited before becoming healthy for '$active_label'." \
                "$log_file"
        fi
        attempt=$((attempt + 1))
        sleep 1
    done

    log_file="$(capture_logs "$active_label-health-timeout")"
    fail_with_log_receipt \
        "Hub Production did not become healthy for '$active_label'." \
        "$log_file"
}

assert_runtime_security() {
    previous_material="$1"
    curl --fail --silent --show-error "http://127.0.0.1:$host_port/hub/" >/dev/null
    test "$(docker exec "$container" id -u)" = "1654"
    test "$(docker exec "$container" id -g)" = "1654"
    test "$(docker exec "$container" stat -c %a "$keys_path")" = "700"
    test "$(docker exec "$container" stat -c %u:%g "$keys_path")" = "1654:1654"
    test "$(docker exec "$container" stat -c %a "$current_certificate_path")" = "400"
    test "$(docker exec "$container" stat -c %u:%g "$current_certificate_path")" = "1654:1654"
    test "$(docker exec "$container" stat -c %a "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD")" = "400"
    test "$(docker exec "$container" stat -c %u:%g "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD")" = "1654:1654"
    docker exec "$container" /bin/sh -c \
        'test ! -w "$1" && test ! -w "$2"' sh \
        "$current_certificate_path" \
        "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD"

    if [ "$previous_material" = "1" ]; then
        test "$(docker exec "$container" stat -c %a "$previous_certificate_path")" = "400"
        test "$(docker exec "$container" stat -c %u:%g "$previous_certificate_path")" = "1654:1654"
        test "$(docker exec "$container" stat -c %a "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD")" = "400"
        test "$(docker exec "$container" stat -c %u:%g "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD")" = "1654:1654"
        docker exec "$container" /bin/sh -c \
            'test ! -w "$1" && test ! -w "$2"' sh \
            "$previous_certificate_path" \
            "$secrets_path/CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD"
    fi

    docker exec "$container" /bin/sh -c 'test ! -w /app/Chummer.Hub.Web.dll'
    docker exec "$container" /bin/sh -c '! touch /app/root-filesystem-must-be-read-only 2>/dev/null'

    configured_environment="$(docker inspect "$container" --format '{{range .Config.Env}}{{println .}}{{end}}')"
    case "$configured_environment" in
        *CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD=*|\
        *CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD=*)
            printf '%s\n' "Hub certificate passwords must not be present in container environment configuration." >&2
            exit 1
            ;;
    esac

    child_pids="$(docker exec "$container" sed -n '1p' /proc/1/task/1/children)"
    # Intentional PID-list splitting; /proc emits a space-delimited numeric list.
    # shellcheck disable=SC2086
    set -- $child_pids
    if [ "$#" -ne 1 ]; then
        printf '%s\n' "Expected one direct Hub init child; refusing an ambiguous process-security proof." >&2
        exit 1
    fi
    dotnet_pid="$1"
    dotnet_executable="$(docker exec "$container" readlink "/proc/$dotnet_pid/exe")"
    case "$dotnet_executable" in
        */dotnet) ;;
        *)
            printf '%s\n' "Hub container init child was not the dotnet runtime." >&2
            exit 1
            ;;
    esac
    test "$(docker exec "$container" /bin/sh -c "sed -n 's/^CapEff:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "0000000000000000"
    test "$(docker exec "$container" /bin/sh -c "sed -n 's/^NoNewPrivs:[[:space:]]*//p' /proc/$dotnet_pid/status")" = "1"
}

volume_digest() {
    volume="$1"
    docker run --rm \
        --user 1654:1654 \
        --read-only \
        --volume "$volume:/ring:ro" \
        --entrypoint /bin/sh \
        "$image" \
        -c 'cd /ring && find . -maxdepth 1 -type f -exec sha256sum {} \; | sort'
}

container_ring_digest() {
    docker exec "$container" /bin/sh -c \
        'cd /var/lib/chummer-hub/data-protection && find . -maxdepth 1 -type f -exec sha256sum {} \; | sort'
}

validate_encrypted_ring() {
    label="$1"
    snapshot="$proof_root/snapshots/$label"
    mkdir -p "$snapshot"
    docker cp "$container:$keys_path/." "$snapshot/" >/dev/null
    docker run --rm \
        --user 0 \
        --volume "$snapshot:/snapshot" \
        --entrypoint /bin/chown \
        "$image" \
        -R "$(id -u):$(id -g)" /snapshot >/dev/null

    python3 - "$snapshot" "$proof_root/sentinels" <<'PY'
from base64 import b64decode
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1])
sentinel_root = Path(sys.argv[2])
key_files = sorted(
    path for path in root.iterdir()
    if path.is_file() and path.name.startswith("key-") and path.suffix == ".xml"
)
if not key_files:
    raise SystemExit("encrypted key-ring proof found no key XML files")

def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]

for key_file in key_files:
    payload = key_file.read_bytes()
    for sentinel in sentinel_root.iterdir():
        value = sentinel.read_bytes().strip()
        if value and value in payload:
            raise SystemExit(f"{key_file.name} contains secret password material")
    if b"BEGIN PRIVATE KEY" in payload or b"BEGIN RSA PRIVATE KEY" in payload:
        raise SystemExit(f"{key_file.name} contains private-key material")
    document = ET.parse(key_file)
    elements = list(document.getroot().iter())
    if any(local_name(element.tag) == "masterKey" for element in elements):
        raise SystemExit(f"{key_file.name} contains a plaintext masterKey")
    encrypted = [
        element for element in elements
        if local_name(element.tag) == "encryptedSecret"
    ]
    if len(encrypted) != 1 or not encrypted[0].attrib.get("decryptorType", "").strip():
        raise SystemExit(f"{key_file.name} does not contain one typed encryptedSecret")
    cipher_values = [
        "".join((element.text or "").split())
        for element in elements
        if local_name(element.tag) == "CipherValue"
    ]
    if not cipher_values or any(not value for value in cipher_values):
        raise SystemExit(f"{key_file.name} lacks encrypted CipherValue material")
    for value in cipher_values:
        try:
            b64decode(value, validate=True)
        except Exception as exception:
            raise SystemExit(f"{key_file.name} contains invalid encrypted CipherValue material") from exception
PY
}

stop_gracefully() {
    label="$1"
    docker stop --time 30 "$container" >/dev/null
    exit_code="$(docker inspect "$container" --format '{{.State.ExitCode}}')"
    log_file="$(capture_logs "$label")"
    if [ "$exit_code" != "0" ]; then
        fail_with_log_receipt \
            "Hub did not stop gracefully for '$label'." \
            "$log_file"
    fi
    docker rm "$container" >/dev/null
}

make_secret_set() {
    target="$1"
    current_certificate="$2"
    current_password="$3"
    install -d -m 0700 "$target" "$target/certificates"
    install -m 0400 "$current_certificate" "$target/certificates/hub-current.p12"
    install -m 0400 "$current_password" "$target/CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PASSWORD"
    if [ "$#" -eq 5 ]; then
        previous_certificate="$4"
        previous_password="$5"
        install -m 0400 "$previous_certificate" "$target/certificates/hub-previous.p12"
        install -m 0400 "$previous_password" "$target/CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PASSWORD"
    fi
}

install -d -m 0700 \
    "$proof_root/logs" \
    "$proof_root/sentinels" \
    "$proof_root/snapshots"
openssl rand -hex 32 | tr -d '\n' >"$proof_root/sentinels/password-a"
openssl rand -hex 32 | tr -d '\n' >"$proof_root/sentinels/password-b"
openssl rand -hex 32 | tr -d '\n' >"$proof_root/sentinels/password-wrong"
chmod 0600 "$proof_root/sentinels/"*

openssl req -x509 -newkey rsa:3072 -sha256 -nodes \
    -subj "/CN=Chummer Hub Data Protection proof A" \
    -keyout "$proof_root/certificate-a.key" \
    -out "$proof_root/certificate-a.crt" \
    -days 1 >/dev/null 2>&1
openssl pkcs12 -export \
    -inkey "$proof_root/certificate-a.key" \
    -in "$proof_root/certificate-a.crt" \
    -out "$proof_root/certificate-a.p12" \
    -passout "file:$proof_root/sentinels/password-a" >/dev/null 2>&1

openssl req -x509 -newkey rsa:3072 -sha256 -nodes \
    -subj "/CN=Chummer Hub Data Protection proof B" \
    -keyout "$proof_root/certificate-b.key" \
    -out "$proof_root/certificate-b.crt" \
    -days 1 >/dev/null 2>&1
openssl pkcs12 -export \
    -inkey "$proof_root/certificate-b.key" \
    -in "$proof_root/certificate-b.crt" \
    -out "$proof_root/certificate-b.p12" \
    -passout "file:$proof_root/sentinels/password-b" >/dev/null 2>&1

make_secret_set \
    "$proof_root/secrets-a" \
    "$proof_root/certificate-a.p12" \
    "$proof_root/sentinels/password-a"
make_secret_set \
    "$proof_root/secrets-b" \
    "$proof_root/certificate-b.p12" \
    "$proof_root/sentinels/password-b"
make_secret_set \
    "$proof_root/secrets-b-with-a" \
    "$proof_root/certificate-b.p12" \
    "$proof_root/sentinels/password-b" \
    "$proof_root/certificate-a.p12" \
    "$proof_root/sentinels/password-a"
make_secret_set \
    "$proof_root/secrets-wrong-a" \
    "$proof_root/certificate-a.p12" \
    "$proof_root/sentinels/password-wrong"
install -d -m 0700 \
    "$proof_root/secrets-missing-password-a" \
    "$proof_root/secrets-missing-password-a/certificates"
install -m 0400 \
    "$proof_root/certificate-a.p12" \
    "$proof_root/secrets-missing-password-a/certificates/hub-current.p12"

docker run --rm \
    --user 0 \
    --volume "$proof_root:/proof" \
    --entrypoint /bin/chown \
    mcr.microsoft.com/dotnet/aspnet:10.0 \
    -R 1654:1654 \
    /proof/secrets-a \
    /proof/secrets-b \
    /proof/secrets-b-with-a \
    /proof/secrets-wrong-a \
    /proof/secrets-missing-password-a >/dev/null

test "$(docker image inspect "$image" --format '{{.Config.User}}')" = "1654:1654"
docker volume create "$ring_a_volume" >/dev/null
docker volume create "$ring_b_volume" >/dev/null
docker volume create "$missing_password_volume" >/dev/null
docker volume create "$wrong_password_volume" >/dev/null

# A Production host may not silently run without any Data Protection material.
expect_rejected "missing-material"

# A password-protected PKCS#12 file must not be accepted without its file-backed password.
expect_rejected "missing-certificate-password" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$missing_password_volume:$keys_path" \
    --volume "$proof_root/secrets-missing-password-a:$secrets_path:ro"
test -z "$(volume_digest "$missing_password_volume")"

# An incorrect file-backed password must fail without creating a plaintext fallback key.
expect_rejected "wrong-certificate-password" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$wrong_password_volume:$keys_path" \
    --volume "$proof_root/secrets-wrong-a:$secrets_path:ro"
test -z "$(volume_digest "$wrong_password_volume")"

# Ring A is created with certificate A and remains stable across an exact-image restart.
start_positive "ring-a-current-a" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$ring_a_volume:$keys_path" \
    --volume "$proof_root/secrets-a:$secrets_path:ro"
wait_for_health
assert_runtime_security 0
validate_encrypted_ring "ring-a-current-a-before-restart"
ring_a_digest="$(container_ring_digest)"
test -n "$ring_a_digest"
docker restart "$container" >/dev/null
wait_for_health
assert_runtime_security 0
validate_encrypted_ring "ring-a-current-a-after-restart"
test "$(container_ring_digest)" = "$ring_a_digest"
stop_gracefully "ring-a-current-a"

# B alone cannot read ring A, and rejection must not mutate the old ring.
expect_rejected "ring-a-current-b-without-previous-a" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$ring_a_volume:$keys_path" \
    --volume "$proof_root/secrets-b:$secrets_path:ro"
test "$(volume_digest "$ring_a_volume")" = "$ring_a_digest"

# B current plus A previous must unprotect A's persisted key ring.
start_positive "ring-a-current-b-previous-a" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH="$previous_certificate_path" \
    --volume "$ring_a_volume:$keys_path" \
    --volume "$proof_root/secrets-b-with-a:$secrets_path:ro"
wait_for_health
assert_runtime_security 1
validate_encrypted_ring "ring-a-current-b-previous-a"
stop_gracefully "ring-a-current-b-previous-a"

# A fresh ring created while B is current and A is previous must be encrypted by B.
start_positive "ring-b-current-b-previous-a" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_PREVIOUS_CERTIFICATE_PATH="$previous_certificate_path" \
    --volume "$ring_b_volume:$keys_path" \
    --volume "$proof_root/secrets-b-with-a:$secrets_path:ro"
wait_for_health
assert_runtime_security 1
validate_encrypted_ring "ring-b-current-b-previous-a"
ring_b_digest="$(container_ring_digest)"
test -n "$ring_b_digest"
stop_gracefully "ring-b-current-b-previous-a"

# A alone cannot read the fresh B ring, proving previous material never encrypts new keys.
expect_rejected "ring-b-current-a" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$ring_b_volume:$keys_path" \
    --volume "$proof_root/secrets-a:$secrets_path:ro"
test "$(volume_digest "$ring_b_volume")" = "$ring_b_digest"

# B alone reads its fresh ring and retains all security properties after restart.
start_positive "ring-b-current-b" \
    --env CHUMMER_HUB_DATA_PROTECTION_KEYS_PATH="$keys_path" \
    --env CHUMMER_HUB_DATA_PROTECTION_CERTIFICATE_PATH="$current_certificate_path" \
    --volume "$ring_b_volume:$keys_path" \
    --volume "$proof_root/secrets-b:$secrets_path:ro"
wait_for_health
assert_runtime_security 0
validate_encrypted_ring "ring-b-current-b-before-restart"
test "$(container_ring_digest)" = "$ring_b_digest"
docker restart "$container" >/dev/null
wait_for_health
assert_runtime_security 0
validate_encrypted_ring "ring-b-current-b-after-restart"
test "$(container_ring_digest)" = "$ring_b_digest"
stop_gracefully "ring-b-current-b"

printf '%s\n' "Hub Production certificate encryption, rotation, container security, and restart persistence proof passed."
