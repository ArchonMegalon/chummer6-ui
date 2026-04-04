#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
REGISTRY_ROOT="$("$SCRIPT_DIR/resolve-hub-registry-root.sh")"

DOWNLOADS_DIR="${DOWNLOADS_DIR:-$REPO_ROOT/Docker/Downloads/files}"
MANIFEST_PATH="${MANIFEST_PATH:-$REPO_ROOT/Docker/Downloads/releases.json}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-$REPO_ROOT/Chummer.Portal/downloads/releases.json}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-$REPO_ROOT/Chummer.Portal/downloads}"
STARTUP_SMOKE_DIR="${STARTUP_SMOKE_DIR:-$(dirname "$DOWNLOADS_DIR")/startup-smoke}"
RELEASE_VERSION="${RELEASE_VERSION:-unpublished}"
RELEASE_CHANNEL="${RELEASE_CHANNEL:-docker}"
RELEASE_PUBLISHED_AT="${RELEASE_PUBLISHED_AT:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
REQUIRE_STARTUP_SMOKE_PROOF="${CHUMMER_RELEASE_REQUIRE_STARTUP_SMOKE_PROOF:-1}"
REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}"
CANONICAL_MANIFEST_PATH="${CANONICAL_MANIFEST_PATH:-$(dirname "$MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PORTAL_CANONICAL_MANIFEST_PATH="${PORTAL_CANONICAL_MANIFEST_PATH:-$(dirname "$PORTAL_MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PROMOTION_EVIDENCE_PATH="${PROMOTION_EVIDENCE_PATH:-$(dirname "$MANIFEST_PATH")/release-evidence/public-promotion.json}"
SOURCE_MANIFEST_PATH="${SOURCE_MANIFEST_PATH:-}"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"

resolve_path_allow_missing() {
  python3 - "$1" <<'PY'
import pathlib
import sys

print(pathlib.Path(sys.argv[1]).resolve(strict=False))
PY
}

json_contract_name() {
  local path="$1"
  python3 - "$path" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception:
    print("")
    raise SystemExit(0)
if isinstance(payload, dict):
    print(str(payload.get("contract_name") or payload.get("contractName") or "").strip())
else:
    print("")
PY
}

resolve_release_proof_path() {
  local requested="${1:-}"
  local -a candidates=()
  local contract_name=""

  if [[ -n "$requested" && -f "$requested" ]]; then
    contract_name="$(json_contract_name "$requested")"
    if [[ "$contract_name" == "chummer6-hub.local_release_proof" ]]; then
      printf '%s\n' "$requested"
      return 0
    fi
    echo "Ignoring RELEASE_PROOF_PATH because it is not a hub local release proof contract: $requested" >&2
  fi

  candidates+=(
    "$REPO_ROOT/../chummer.run-services/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "/docker/chummercomplete/chummer.run-services/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
  )

  for candidate in "${candidates[@]}"; do
    [[ -f "$candidate" ]] || continue
    contract_name="$(json_contract_name "$candidate")"
    if [[ "$contract_name" == "chummer6-hub.local_release_proof" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  printf '%s\n' ""
}

if [[ ! -f "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" ]]; then
  echo "Missing registry materializer: $REGISTRY_ROOT/scripts/materialize_public_release_channel.py" >&2
  exit 1
fi

RELEASE_PROOF_PATH="$(resolve_release_proof_path "$RELEASE_PROOF_PATH")"

mkdir -p "$(dirname "$MANIFEST_PATH")"
mkdir -p "$(dirname "$PORTAL_MANIFEST_PATH")"
mkdir -p "$DOWNLOADS_DIR"

readarray -t promoted_file_names < <(true)

materialize_args=(
  --downloads-dir "$DOWNLOADS_DIR"
  --channel "$RELEASE_CHANNEL"
  --version "$RELEASE_VERSION"
  --published-at "$RELEASE_PUBLISHED_AT"
  --output "$CANONICAL_MANIFEST_PATH"
  --compat-output "$MANIFEST_PATH"
)

if [[ -n "$SOURCE_MANIFEST_PATH" && -f "$SOURCE_MANIFEST_PATH" ]]; then
  materialize_args+=(--manifest "$SOURCE_MANIFEST_PATH")
fi

if [[ -n "$RELEASE_PROOF_PATH" && -f "$RELEASE_PROOF_PATH" ]]; then
  materialize_args+=(--proof "$RELEASE_PROOF_PATH")
fi

materializer_help="$(python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" --help 2>&1 || true)"
if [[ -d "$STARTUP_SMOKE_DIR" ]] && find "$STARTUP_SMOKE_DIR" -type f -name 'startup-smoke-*.receipt.json' | grep -q .; then
  if [[ "$materializer_help" != *"--startup-smoke-dir"* ]]; then
    echo "Registry materializer CLI mismatch: $REGISTRY_ROOT/scripts/materialize_public_release_channel.py does not support --startup-smoke-dir." >&2
    exit 1
  fi
  materialize_args+=(--startup-smoke-dir "$STARTUP_SMOKE_DIR")
fi

python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" "${materialize_args[@]}" >/dev/null
verify_args=()
if [[ "$REQUIRE_COMPLETE_DESKTOP_COVERAGE" != "0" ]]; then
  verify_args+=(--require-complete-desktop-coverage)
fi
python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "${verify_args[@]}" "$CANONICAL_MANIFEST_PATH" >/dev/null
readarray -t promoted_file_names < <(python3 - "$CANONICAL_MANIFEST_PATH" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
seen = set()
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        file_name = Path(str(artifact.get("downloadUrl") or "").strip()).name
    if file_name and file_name not in seen:
        print(file_name)
        seen.add(file_name)
PY
)
python3 "$SCRIPT_DIR/generate-public-promotion-evidence.py" \
  --manifest "$CANONICAL_MANIFEST_PATH" \
  --startup-smoke-dir "$STARTUP_SMOKE_DIR" \
  --output "$PROMOTION_EVIDENCE_PATH" \
  --channel "$RELEASE_CHANNEL" \
  --generated-at "$RELEASE_PUBLISHED_AT"

if [[ "$REQUIRE_STARTUP_SMOKE_PROOF" != "0" ]]; then
  if ! python3 - "$PROMOTION_EVIDENCE_PATH" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
failures: list[str] = []
artifacts = payload.get("artifacts") or []
for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    kind = str(artifact.get("kind") or "").strip().lower()
    if kind not in {"installer", "dmg", "pkg", "msix"}:
        continue
    startup_status = str(artifact.get("startupSmokeStatus") or "").strip().lower()
    if startup_status == "pass":
        continue
    file_name = str(artifact.get("fileName") or "").strip() or str(artifact.get("artifactId") or "unknown-artifact")
    reason = str(artifact.get("startupSmokeReason") or "startup smoke proof missing").strip()
    failures.append(f"{file_name}: {reason}")

if failures:
    print("startup smoke proof is required for promoted installer artifacts:", file=sys.stderr)
    for failure in failures:
        print(f" - {failure}", file=sys.stderr)
    raise SystemExit(1)
PY
  then
    exit 1
  fi
fi

resolved_manifest_path="$(resolve_path_allow_missing "$MANIFEST_PATH")"
resolved_portal_manifest_path="$(resolve_path_allow_missing "$PORTAL_MANIFEST_PATH")"
if [[ "$resolved_manifest_path" == "$resolved_portal_manifest_path" ]]; then
  echo "portal manifest path matches manifest output; skipped secondary sync"
else
  cp "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH"
  cp "$CANONICAL_MANIFEST_PATH" "$PORTAL_CANONICAL_MANIFEST_PATH"
  echo "synced portal manifest -> $PORTAL_MANIFEST_PATH"

  portal_files_dir="$PORTAL_DOWNLOADS_DIR/files"
  mkdir -p "$portal_files_dir"
  portal_artifacts=()
  for file_name in "${promoted_file_names[@]}"; do
    artifact_path="$DOWNLOADS_DIR/$file_name"
    if [[ ! -f "$artifact_path" ]]; then
      echo "promoted artifact missing from downloads source: $artifact_path" >&2
      exit 1
    fi
    portal_artifacts+=("$artifact_path")
  done
  if [[ "${#portal_artifacts[@]}" -gt 0 ]]; then
    rm -f \
      "$portal_files_dir"/chummer-*.exe \
      "$portal_files_dir"/chummer-*.zip \
      "$portal_files_dir"/chummer-*.tar.gz \
      "$portal_files_dir"/chummer-*-installer.deb \
      "$portal_files_dir"/chummer-*-installer.pkg \
      "$portal_files_dir"/chummer-*-installer.dmg \
      "$portal_files_dir"/chummer-*-installer.msix
    cp "${portal_artifacts[@]}" "$portal_files_dir"/
    echo "synced ${#portal_artifacts[@]} local portal artifact(s) -> $portal_files_dir"
  else
    echo "no local desktop artifacts found in $DOWNLOADS_DIR for portal file sync"
  fi
fi
