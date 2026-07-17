#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_path="$repo_root/Chummer.Workspaces.Postgres.IntegrationTests/Chummer.Workspaces.Postgres.IntegrationTests.csproj"
postgres_image="${CHUMMER_BUILD_POSTGRES_TEST_IMAGE:-postgres:17-alpine}"
container_name="chummer-build-postgres-test-$$"
test_password="chummer-postgres-disposable-test-only"

command -v docker >/dev/null 2>&1 || {
  printf 'docker is required for the Hosted Build PostgreSQL contract test.\n' >&2
  exit 1
}
command -v dotnet >/dev/null 2>&1 || {
  printf 'dotnet is required for the Hosted Build PostgreSQL contract test.\n' >&2
  exit 1
}

cleanup() {
  docker rm --force "$container_name" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

docker run \
  --detach \
  --rm \
  --name "$container_name" \
  --env "POSTGRES_PASSWORD=$test_password" \
  --publish 127.0.0.1::5432 \
  "$postgres_image" >/dev/null

for attempt in $(seq 1 30); do
  if docker exec "$container_name" \
      pg_isready --username postgres --dbname postgres >/dev/null 2>&1; then
    break
  fi
  if [[ "$attempt" == "30" ]]; then
    printf 'Disposable PostgreSQL did not become ready.\n' >&2
    exit 1
  fi
  sleep 1
done

published_port="$(docker port "$container_name" 5432/tcp | sed -n 's/.*://p' | head -n 1)"
if [[ -z "$published_port" ]]; then
  printf 'Docker did not publish the disposable PostgreSQL port.\n' >&2
  exit 1
fi

export CHUMMER_BUILD_POSTGRES_TEST_CONNECTION_STRING="Host=127.0.0.1;Port=${published_port};Database=postgres;Username=postgres;Password=${test_password};SSL Mode=Disable"

dotnet test \
  --project "$project_path" \
  -c Release \
  --output Normal \
  --minimum-expected-tests 24
