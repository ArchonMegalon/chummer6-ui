#!/bin/sh
set -eu

repo_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
workspace_root="$(CDPATH= cd -- "$repo_root/.." && pwd)"
canary_name=".container-context-proof-$$"
canary="$repo_root/$canary_name"
dockerfile="$(mktemp "${TMPDIR:-/tmp}/chummer-context-proof.Dockerfile.XXXXXX")"
image="chummer-context-secret-proof-$$:local"

cleanup() {
    docker image rm -f "$image" >/dev/null 2>&1 || true
    rm -rf "$canary" "$dockerfile"
}
trap cleanup EXIT HUP INT TERM

mkdir -m 0700 "$canary"
printf '%s' "public" >"$canary/public.txt"
for secret in \
    .env \
    .env.production \
    private.key \
    certificate.pem \
    certificate.pfx \
    certificate.p12 \
    credentials-proof.json \
    secrets-proof.yaml; do
    printf '%s' "must-not-enter-build-context" >"$canary/$secret"
done

printf '%s\n' \
    'FROM mcr.microsoft.com/dotnet/aspnet:10.0' \
    "COPY chummer-presentation/$canary_name/ /proof/" \
    'RUN test -f /proof/public.txt' \
    'RUN test ! -e /proof/.env' \
    'RUN test ! -e /proof/.env.production' \
    'RUN test ! -e /proof/private.key' \
    'RUN test ! -e /proof/certificate.pem' \
    'RUN test ! -e /proof/certificate.pfx' \
    'RUN test ! -e /proof/certificate.p12' \
    'RUN test ! -e /proof/credentials-proof.json' \
    'RUN test ! -e /proof/secrets-proof.yaml' \
    >"$dockerfile"

docker build \
    --file "$dockerfile" \
    --tag "$image" \
    "$workspace_root" >/dev/null

printf '%s\n' "Docker build-context secret exclusion proof passed."
