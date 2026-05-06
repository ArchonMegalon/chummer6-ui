#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipts_dir="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR:-$repo_root/.codex-studio/published/chummer5a-fixture-ui-reconstruction}"
scope="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE:-default}"
fixtures_root="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_ROOT:-$repo_root/Chummer.Tests/TestFiles}"
fixtures_file="${CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_FILE:-}"
tmp_fixtures_file=""
mkdir -p "$receipts_dir"

cleanup() {
  if [[ -n "$tmp_fixtures_file" && -f "$tmp_fixtures_file" ]]; then
    rm -f "$tmp_fixtures_file"
  fi
}
trap cleanup EXIT

if [[ -z "$fixtures_file" && "$scope" == "all" ]]; then
  if [[ ! -d "$fixtures_root" ]]; then
    echo "missing fixture root for all-scope reconstruction: $fixtures_root" >&2
    exit 2
  fi
  tmp_fixtures_file="$(mktemp)"
  find "$fixtures_root" -maxdepth 1 -name '*.chum5' -printf '%f\n' | sort >"$tmp_fixtures_file"
  fixtures_file="$tmp_fixtures_file"
fi

CHUMMER_FIXTURE_UI_RECONSTRUCTION_SCOPE="$scope" \
CHUMMER_FIXTURE_UI_RECONSTRUCTION_FIXTURES_FILE="$fixtures_file" \
CHUMMER_FIXTURE_UI_RECONSTRUCTION_RECEIPTS_DIR="$receipts_dir" \
  bash "$repo_root/scripts/ai/test.sh" Chummer.Tests/Chummer.Tests.csproj \
    --filter "FullyQualifiedName~Runtime_backed_chummer5a_fixture_ui_reconstruction_receipts_pass_for_default_first_slice" \
    -m:1 -v minimal >/dev/null

echo "[chummer5a-fixture-ui-reconstruction] PASS"
