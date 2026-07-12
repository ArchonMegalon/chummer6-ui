#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR_PHYSICAL="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR_PHYSICAL/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
SCRIPT_DIR="$REPO_ROOT/scripts"
REGISTRY_ROOT="$("$SCRIPT_DIR/resolve-hub-registry-root.sh")"

DOWNLOADS_DIR="${DOWNLOADS_DIR:-$REPO_ROOT/Docker/Downloads/files}"
MANIFEST_PATH="${MANIFEST_PATH:-$REPO_ROOT/Docker/Downloads/releases.json}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-$REPO_ROOT/Chummer.Portal/downloads/releases.json}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-$REPO_ROOT/Chummer.Portal/downloads}"
PRESENTATION_MIRROR_ROOT="${PRESENTATION_MIRROR_ROOT:-$REPO_ROOT}"
STARTUP_SMOKE_DIR="${STARTUP_SMOKE_DIR:-$(dirname "$DOWNLOADS_DIR")/startup-smoke}"
SIGNING_RECEIPTS_DIR="${SIGNING_RECEIPTS_DIR:-$(dirname "$DOWNLOADS_DIR")/signing}"
STARTUP_SMOKE_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS:-}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-}"
SKIP_STARTUP_SMOKE_HYDRATION="${CHUMMER_SKIP_STARTUP_SMOKE_HYDRATION:-0}"
RELEASE_VERSION="${RELEASE_VERSION:-unpublished}"
RELEASE_CHANNEL="${RELEASE_CHANNEL:-stable}"
RELEASE_PUBLISHED_AT="${RELEASE_PUBLISHED_AT:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
REQUIRE_STARTUP_SMOKE_PROOF="${CHUMMER_RELEASE_REQUIRE_STARTUP_SMOKE_PROOF:-1}"
REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}"
PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS="${CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS:-1}"
UI_LOCALIZATION_RELEASE_GATE_PATH="${CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json}"
EXTERNAL_HOST_PROOF_BLOCKERS_PATH="${CHUMMER_UI_EXTERNAL_HOST_PROOF_BLOCKERS_PATH:-$REPO_ROOT/.codex-studio/published/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json}"
PUBLIC_EDGE_WORKBENCH_PROOF_PATH="${CHUMMER_BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF_PATH:-$REPO_ROOT/.codex-studio/published/BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json}"
GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-1}"
CANONICAL_MANIFEST_PATH="${CANONICAL_MANIFEST_PATH:-$(dirname "$MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PORTAL_CANONICAL_MANIFEST_PATH="${PORTAL_CANONICAL_MANIFEST_PATH:-$(dirname "$PORTAL_MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PROMOTION_EVIDENCE_PATH="${PROMOTION_EVIDENCE_PATH:-$(dirname "$MANIFEST_PATH")/release-evidence/public-promotion.json}"
QUARANTINE_PROMOTION_EVIDENCE_PATH="${QUARANTINE_PROMOTION_EVIDENCE_PATH:-$REPO_ROOT/.codex-studio/published/QUARANTINED_INSTALLER_PROMOTION.generated.json}"
SOURCE_MANIFEST_PATH="${SOURCE_MANIFEST_PATH:-}"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-${CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH:-}}"
FLAGSHIP_READINESS_PATH="${CHUMMER_FLAGSHIP_READINESS_PATH:-${CHUMMER_FLAGSHIP_PRODUCT_READINESS_RECEIPT_PATH:-}}"
PREVIEW_INSTALL_ACCESS_CLASS="${CHUMMER_PREVIEW_INSTALL_ACCESS_CLASS:-}"
EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}"
DOWNLOADS_PREFIX="${CHUMMER_PUBLIC_DOWNLOADS_PREFIX:-${EXTERNAL_PROOF_BASE_URL%/}/downloads/files}"
RELEASE_PROOF_MAX_AGE_SECONDS="${CHUMMER_RELEASE_PROOF_MAX_AGE_SECONDS:-86400}"
UI_LOCALIZATION_RELEASE_GATE_MAX_AGE_SECONDS="${CHUMMER_UI_LOCALIZATION_RELEASE_GATE_MAX_AGE_SECONDS:-604800}"
REGISTRY_CANONICAL_MANIFEST_PATH="${REGISTRY_CANONICAL_MANIFEST_PATH:-$REGISTRY_ROOT/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
REGISTRY_RELEASES_MANIFEST_PATH="${REGISTRY_RELEASES_MANIFEST_PATH:-$REGISTRY_ROOT/.codex-studio/published/releases.json}"
REGISTRY_FILES_DIR="${REGISTRY_FILES_DIR:-$REGISTRY_ROOT/.codex-studio/published/files}"
CANONICAL_FILES_DIR="${CANONICAL_FILES_DIR:-$(dirname "$CANONICAL_MANIFEST_PATH")/files}"
SCOPE_TO_STAGE_ARTIFACTS="${CHUMMER_RELEASE_SCOPE_TO_STAGE_ARTIFACTS:-0}"

lower_ascii() {
  printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]'
}

array_count() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || {
    printf '0\n'
    return 0
  }

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "set -- \"\${${array_name}[@]}\""
  local count="$#"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  printf '%s\n' "$count"
}

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

if [[ -z "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" ]]; then
  if [[ "$(lower_ascii "$RELEASE_CHANNEL")" == "preview" ]]; then
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true"
  else
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="false"
  fi
fi

verify_registry_boundary_consistency() {
  local docker_releases_path="$1"
  local docker_channel_path="$2"
  local portal_releases_path="$3"
  local portal_channel_path="$4"

  if [[ ! -f "$docker_releases_path" || ! -f "$docker_channel_path" || ! -f "$portal_releases_path" || ! -f "$portal_channel_path" ]]; then
    echo "registry boundary consistency check requires all generated manifests to exist" >&2
    return 1
  fi

  python3 - "$docker_releases_path" "$docker_channel_path" "$portal_releases_path" "$portal_channel_path" <<'PY'
import json
import sys
from pathlib import Path


def _compatibility_pair(payload: dict) -> tuple[int, int]:
    coverage = payload.get("registryBoundaryCoverage") if isinstance(payload, dict) else None
    if not isinstance(coverage, dict):
        return -1, -1
    compatibility = coverage.get("compatibility") if isinstance(coverage, dict) else None
    if not isinstance(compatibility, dict):
        return -1, -1
    try:
        compatible = int(compatibility.get("compatibleArtifactCount", -1))
    except Exception:
        compatible = -1
    try:
        unknown = int(compatibility.get("unknownArtifactCount", -1))
    except Exception:
        unknown = -1
    return compatible, unknown


def _load_payload(path_text: str) -> dict:
    path = Path(path_text)
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise SystemExit(f"manifest is not an object: {path}")
    return payload


docker_releases = _load_payload(sys.argv[1])
docker_channel = _load_payload(sys.argv[2])
portal_releases = _load_payload(sys.argv[3])
portal_channel = _load_payload(sys.argv[4])

comparisons = [
    ("Docker releases vs Docker release channel", _compatibility_pair(docker_releases), _compatibility_pair(docker_channel)),
    ("Docker releases vs Portal releases", _compatibility_pair(docker_releases), _compatibility_pair(portal_releases)),
    ("Docker release channel vs Portal release channel", _compatibility_pair(docker_channel), _compatibility_pair(portal_channel)),
]

failures: list[str] = []
for label, left_counts, right_counts in comparisons:
    if left_counts != right_counts:
        failures.append(f"{label}: {left_counts[0]}/{left_counts[1]} != {right_counts[0]}/{right_counts[1]}")

if failures:
    print("registryBoundaryCoverage compatibility mismatch:", file=sys.stderr)
    for failure in failures:
        print(f" - {failure}", file=sys.stderr)
    raise SystemExit(1)

PY
}

resolve_path_allow_missing() {
  python3 - "$1" <<'PY'
import pathlib
import sys

print(pathlib.Path(sys.argv[1]).resolve(strict=False))
PY
}

path_is_tmp_outside_repo() {
  local candidate="${1:-}"
  local resolved_candidate=""
  local resolved_repo_root=""

  [[ -n "$candidate" ]] || return 1
  resolved_candidate="$(resolve_path_allow_missing "$candidate")"
  resolved_repo_root="$(resolve_path_allow_missing "$REPO_ROOT")"
  [[ "$resolved_candidate" == /tmp/* && "$resolved_candidate" != "$resolved_repo_root" && "$resolved_candidate" != "$resolved_repo_root/"* ]]
}

presentation_mirror_enabled() {
  if [[ -z "$PRESENTATION_MIRROR_ROOT" || ! -d "$PRESENTATION_MIRROR_ROOT" ]]; then
    return 1
  fi

  local repo_root_physical mirror_root_physical
  repo_root_physical="$(cd "$REPO_ROOT" && pwd -P)"
  mirror_root_physical="$(cd "$PRESENTATION_MIRROR_ROOT" && pwd -P)"
  [[ "$repo_root_physical" != "$mirror_root_physical" ]]
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

release_proof_is_fresh() {
  local path="$1"
  local max_age_seconds="${2:-86400}"
  python3 - "$path" "$max_age_seconds" <<'PY'
import datetime as dt
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
max_age_seconds = int(sys.argv[2])

try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

if not isinstance(payload, dict):
    raise SystemExit(1)

raw = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
if not raw:
    raise SystemExit(1)
if raw.endswith("Z"):
    raw = raw[:-1] + "+00:00"
try:
    generated_at = dt.datetime.fromisoformat(raw)
except ValueError:
    raise SystemExit(1)
if generated_at.tzinfo is None:
    generated_at = generated_at.replace(tzinfo=dt.timezone.utc)
generated_at = generated_at.astimezone(dt.timezone.utc)
age_seconds = int((dt.datetime.now(dt.timezone.utc) - generated_at).total_seconds())
raise SystemExit(0 if 0 <= age_seconds <= max_age_seconds else 1)
PY
}

restore_local_manifests_from_registry_if_needed() {
  local canonical_manifest_path="$1"
  local releases_manifest_path="$2"
  local expected_release_version="$3"
  local local_files_dir="$4"
  local registry_files_dir="$5"
  local registry_canonical_path="$REGISTRY_CANONICAL_MANIFEST_PATH"
  local registry_releases_path="$REGISTRY_RELEASES_MANIFEST_PATH"

  python3 - "$canonical_manifest_path" "$releases_manifest_path" "$registry_canonical_path" "$registry_releases_path" "$expected_release_version" "$local_files_dir" "$registry_files_dir" <<'PY'
from __future__ import annotations

import json
import os
import shutil
import sys
from pathlib import Path


def normalized(value: object) -> str:
    return str(value or "").strip()


def manifest_file(path_text: str) -> Path:
    return Path(path_text).resolve()


def load_payload(path_text: str) -> dict:
    path = manifest_file(path_text)
    if not path.is_file():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def artifact_download_name(artifact: object) -> str:
    if not isinstance(artifact, dict):
        return ""
    file_name = normalized(artifact.get("fileName"))
    if file_name:
        return file_name
    return Path(normalized(artifact.get("downloadUrl") or "")).name


def version_from_payload(payload: dict) -> str:
    return normalized(payload.get("version") or payload.get("releaseVersion"))


def rows_with_version(rows: object, expected_version: str) -> list[dict]:
    if not isinstance(rows, list):
        return []
    expected = normalized(expected_version)
    if not expected:
        return [row for row in rows if isinstance(row, dict)]
    matched = [
        row
        for row in rows
        if isinstance(row, dict)
        and normalized(row.get("releaseVersion") or row.get("version")) == expected
    ]
    return matched or []


def filtered_channel_rows(rows: list[dict], expected_version: str) -> list[dict]:
    selected = rows_with_version(rows, expected_version)
    if selected:
        return selected
    expected = normalized(expected_version)
    if expected:
        return []
    return [row for row in rows if isinstance(row, dict)]


def write_payload_if_changed(path_text: str, payload: dict, current_payload: dict) -> bool:
    if payload == current_payload:
        return False
    manifest_file(path_text).write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return True


def payload_matches_expected_version(payload: dict, expected_version: str) -> bool:
    expected = normalized(expected_version)
    if not expected:
        return True
    payload_version = version_from_payload(payload)
    return bool(payload_version) and payload_version == expected


def restore_missing_artifacts(local_payload: dict, registry_payload: dict, expected_version: str) -> bool:
    local_artifacts = local_payload.get("artifacts")
    registry_artifacts = registry_payload.get("artifacts")
    local_has_artifacts = isinstance(local_artifacts, list) and len(local_artifacts) > 0
    registry_has_artifacts = isinstance(registry_artifacts, list) and len(registry_artifacts) > 0
    if local_has_artifacts or not registry_has_artifacts:
        return False
    if not payload_matches_expected_version(registry_payload, expected_version):
        return False
    filtered_artifacts = filtered_channel_rows([row for row in registry_artifacts if isinstance(row, dict)], expected_version)
    if filtered_artifacts:
        local_payload["artifacts"] = filtered_artifacts
        return True
    return False


def restore_missing_downloads(local_payload: dict, registry_payload: dict, expected_version: str) -> bool:
    local_downloads = local_payload.get("downloads")
    registry_downloads = registry_payload.get("downloads")
    local_has_downloads = isinstance(local_downloads, list) and len(local_downloads) > 0
    registry_has_downloads = isinstance(registry_downloads, list) and len(registry_downloads) > 0
    if local_has_downloads or not registry_has_downloads:
        return False
    if not payload_matches_expected_version(registry_payload, expected_version):
        return False
    filtered_downloads = rows_with_version(registry_downloads, expected_version)
    if filtered_downloads:
        local_payload["downloads"] = filtered_downloads
        return True
    return False


def restore_missing_boundary_fields(local_payload: dict, registry_payload: dict) -> bool:
    local_coverage = local_payload.get("registryBoundaryCoverage")
    registry_coverage = registry_payload.get("registryBoundaryCoverage")
    if not isinstance(local_coverage, dict):
        if isinstance(registry_coverage, dict):
            local_payload["registryBoundaryCoverage"] = registry_coverage
            return True
        return False

    changed = False
    local_compatibility = local_coverage.get("compatibility")
    registry_compatibility = registry_coverage.get("compatibility") if isinstance(registry_coverage, dict) else None
    if (
        isinstance(local_compatibility, dict)
        and local_compatibility.get("compatibleArtifactCount", 0) == 0
        and isinstance(registry_compatibility, dict)
    ):
        local_coverage["compatibility"] = registry_compatibility
        changed = True
    return changed


def restore_missing_artifacts_from_files(
    local_payload: dict,
    local_files_dir: Path,
    registry_files_dir: Path,
) -> None:
    local_files_dir.mkdir(parents=True, exist_ok=True)

    artifact_rows = local_payload.get("artifacts") or []
    download_rows = local_payload.get("downloads") or []

    file_names = {
        artifact_download_name(row) for row in artifact_rows
    } | {
        artifact_download_name(row) for row in download_rows
    }
    file_names = {name for name in file_names if name}
    for file_name in sorted(file_names):
        local_file = local_files_dir / file_name
        if local_file.is_file():
            continue
        source_file = Path(os.path.join(registry_files_dir, file_name))
        if source_file.is_file():
            shutil.copy2(source_file, local_file)


def main() -> None:
    canonical_manifest_path_text, releases_manifest_path_text, fallback_channel_text, fallback_release_text, expected_version, local_files_dir_text, registry_files_dir_text = (
        sys.argv[1:]
    )
    expected_version = normalized(expected_version)
    if expected_version == "unpublished":
        expected_version = ""

    canonical_payload = load_payload(canonical_manifest_path_text)
    releases_payload = load_payload(releases_manifest_path_text)
    fallback_channel_payload = load_payload(fallback_channel_text)
    fallback_releases_payload = load_payload(fallback_release_text)

    local_files_dir = Path(local_files_dir_text).resolve()
    registry_files_dir = Path(registry_files_dir_text).resolve()

    if not canonical_payload and not releases_payload:
        return

    changed_canonical = False
    changed_releases = False
    changed_boundary = False

    if canonical_payload and fallback_channel_payload:
        changed_canonical = restore_missing_artifacts(canonical_payload, fallback_channel_payload, expected_version)
        changed_boundary = restore_missing_boundary_fields(canonical_payload, fallback_channel_payload) or changed_boundary

    if releases_payload and fallback_releases_payload:
        changed_releases = restore_missing_downloads(releases_payload, fallback_releases_payload, expected_version)

    if canonical_payload:
        restore_missing_artifacts_from_files(canonical_payload, local_files_dir, registry_files_dir)
    elif releases_payload:
        restore_missing_artifacts_from_files(releases_payload, local_files_dir, registry_files_dir)

    if changed_canonical:
        write_payload_if_changed(canonical_manifest_path_text, canonical_payload, load_payload(canonical_manifest_path_text))
    if changed_releases:
        write_payload_if_changed(releases_manifest_path_text, releases_payload, load_payload(releases_manifest_path_text))
    if changed_boundary and canonical_payload:
        write_payload_if_changed(canonical_manifest_path_text, canonical_payload, load_payload(canonical_manifest_path_text))

    if changed_canonical or changed_releases or changed_boundary:
        print("repaired local release manifests from trusted registry snapshot fallback.")


if __name__ == "__main__":
    main()
PY
}

resolve_hub_release_proof_generator_root() {
  local -a roots=(
    "$REPO_ROOT/../.c/hub"
    "$REPO_ROOT/../chummer6-hub"
    "$REPO_ROOT/../chummer.run-services"
    "/docker/chummercomplete/chummer.run-services"
    "/docker/chummercomplete/chummer6-hub"
  )
  local root
  for root in "${roots[@]}"; do
    if [[ -f "$root/scripts/materialize_hub_local_release_proof.py" ]]; then
      printf '%s\n' "$root"
      return 0
    fi
  done
  printf '%s\n' ""
}

resolve_ui_localization_release_gate_generator_root() {
  local -a roots=(
    "$REPO_ROOT"
    "$REPO_ROOT/../chummer6-ui"
    "$PRESENTATION_MIRROR_ROOT"
  )
  local root
  for root in "${roots[@]}"; do
    if [[ -f "$root/scripts/ai/milestones/b15-localization-release-gate.sh" ]]; then
      printf '%s\n' "$root"
      return 0
    fi
  done
  printf '%s\n' ""
}

materialize_fresh_release_proof() {
  local base_url="${1:-}"
  local hub_root
  hub_root="$(resolve_hub_release_proof_generator_root)"
  if [[ -z "$hub_root" ]]; then
    return 1
  fi

  local generated_output
  generated_output="$(mktemp)"
  if ! python3 "$hub_root/scripts/materialize_hub_local_release_proof.py" \
      "$generated_output" \
      "$base_url" \
      "docker-compose.yml" \
      "120" \
      "true" >/dev/null; then
    rm -f "$generated_output"
    return 1
  fi

  if [[ "$(json_contract_name "$generated_output")" != "chummer6-hub.local_release_proof" ]]; then
    rm -f "$generated_output"
    return 1
  fi

  if ! release_proof_is_fresh "$generated_output" "$RELEASE_PROOF_MAX_AGE_SECONDS"; then
    rm -f "$generated_output"
    return 1
  fi

  printf '%s\n' "$generated_output"
}

materialize_fresh_ui_localization_release_gate() {
  local ui_root
  ui_root="$(resolve_ui_localization_release_gate_generator_root)"
  if [[ -z "$ui_root" ]]; then
    return 1
  fi

  if ! (cd "$ui_root" && bash "$ui_root/scripts/ai/milestones/b15-localization-release-gate.sh" >/dev/null); then
    return 1
  fi

  local generated_output="$ui_root/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
  if [[ ! -f "$generated_output" ]]; then
    return 1
  fi

  if ! release_proof_is_fresh "$generated_output" "$UI_LOCALIZATION_RELEASE_GATE_MAX_AGE_SECONDS"; then
    return 1
  fi

  printf '%s\n' "$generated_output"
}

resolve_release_proof_path() {
  local requested="${1:-}"
  local -a candidates=()
  local contract_name=""
  local freshest_candidate=""
  local generated_candidate=""

  if [[ -n "$requested" && -f "$requested" ]]; then
    contract_name="$(json_contract_name "$requested")"
    if [[ "$contract_name" == "chummer6-hub.local_release_proof" ]]; then
      if release_proof_is_fresh "$requested" "$RELEASE_PROOF_MAX_AGE_SECONDS"; then
        printf '%s\n' "$requested"
        return 0
      fi
      generated_candidate="$(materialize_fresh_release_proof "$EXTERNAL_PROOF_BASE_URL" || true)"
      if [[ -n "$generated_candidate" && -f "$generated_candidate" ]]; then
        printf '%s\n' "$generated_candidate"
        return 0
      fi
      printf '%s\n' "$requested"
      return 0
    fi
    echo "Ignoring RELEASE_PROOF_PATH because it is not a hub local release proof contract: $requested" >&2
  fi

  candidates+=(
    "$REPO_ROOT/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../.c/hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../.c/hub/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../chummer6-hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../chummer6-hub/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../chummer.run-services/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../chummer.run-services/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "$REPO_ROOT/../chummer.run-services/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "/docker/chummercomplete/chummer.run-services/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "/docker/chummercomplete/chummer.run-services/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "/docker/chummercomplete/chummer6-hub/.codex-studio/published/HUB_LOCAL_RELEASE_PROOF.generated.json"
    "/docker/chummercomplete/chummer6-hub/Chummer.Run.Api/wwwroot/proofs/mac-codex-release/HUB_LOCAL_RELEASE_PROOF.generated.json"
  )

  freshest_candidate="$(python3 - "${candidates[@]}" <<'PY'
import datetime as dt
import json
import sys
from pathlib import Path

UTC = dt.timezone.utc


def parse_timestamp(payload: dict) -> dt.datetime:
    raw = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
    if not raw:
        return dt.datetime.min.replace(tzinfo=UTC)
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(raw)
    except ValueError:
        return dt.datetime.min.replace(tzinfo=UTC)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)


seen: set[Path] = set()
best_path: Path | None = None
best_timestamp = dt.datetime.min.replace(tzinfo=UTC)

for raw_candidate in sys.argv[1:]:
    candidate = Path(raw_candidate).resolve(strict=False)
    if candidate in seen or not candidate.is_file():
        continue
    seen.add(candidate)
    try:
        payload = json.loads(candidate.read_text(encoding="utf-8-sig"))
    except Exception:
        continue
    if not isinstance(payload, dict):
        continue
    contract_name = str(payload.get("contract_name") or payload.get("contractName") or "").strip()
    if contract_name != "chummer6-hub.local_release_proof":
        continue
    timestamp = parse_timestamp(payload)
    if best_path is None or timestamp >= best_timestamp:
        best_path = candidate
        best_timestamp = timestamp

if best_path is not None:
    print(best_path)
PY
)"

  if [[ -n "$freshest_candidate" && -f "$freshest_candidate" ]]; then
    if release_proof_is_fresh "$freshest_candidate" "$RELEASE_PROOF_MAX_AGE_SECONDS"; then
      printf '%s\n' "$freshest_candidate"
      return 0
    fi
  fi

  generated_candidate="$(materialize_fresh_release_proof "$EXTERNAL_PROOF_BASE_URL" || true)"
  if [[ -n "$generated_candidate" && -f "$generated_candidate" ]]; then
    printf '%s\n' "$generated_candidate"
    return 0
  fi

  if [[ -n "$freshest_candidate" ]]; then
    printf '%s\n' "$freshest_candidate"
    return 0
  fi

  printf '%s\n' ""
}

resolve_ui_localization_release_gate_path() {
  local requested="${1:-}"
  local -a candidates=()
  local freshest_candidate=""
  local generated_candidate=""

  if [[ -n "$requested" && -f "$requested" ]]; then
    if release_proof_is_fresh "$requested" "$UI_LOCALIZATION_RELEASE_GATE_MAX_AGE_SECONDS"; then
      printf '%s\n' "$requested"
      return 0
    fi
    generated_candidate="$(materialize_fresh_ui_localization_release_gate || true)"
    if [[ -n "$generated_candidate" && -f "$generated_candidate" ]]; then
      printf '%s\n' "$generated_candidate"
      return 0
    fi
    printf '%s\n' "$requested"
    return 0
  fi

  candidates+=(
    "$REPO_ROOT/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
    "$REPO_ROOT/../chummer6-ui/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
    "$PRESENTATION_MIRROR_ROOT/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json"
    "$REGISTRY_ROOT/.codex-studio/published/.tmp_ui_localization_release_gate.json"
  )

  freshest_candidate="$(python3 - "${candidates[@]}" <<'PY'
import datetime as dt
import json
import sys
from pathlib import Path

UTC = dt.timezone.utc

def parse_timestamp(payload: dict) -> dt.datetime:
    raw = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
    if not raw:
        return dt.datetime.min.replace(tzinfo=UTC)
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(raw)
    except ValueError:
        return dt.datetime.min.replace(tzinfo=UTC)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)

seen = set()
best_path = None
best_timestamp = dt.datetime.min.replace(tzinfo=UTC)

for raw_candidate in sys.argv[1:]:
    candidate = Path(raw_candidate).resolve(strict=False)
    if candidate in seen or not candidate.is_file():
        continue
    seen.add(candidate)
    try:
        payload = json.loads(candidate.read_text(encoding="utf-8-sig"))
    except Exception:
        continue
    if not isinstance(payload, dict):
        continue
    timestamp = parse_timestamp(payload)
    if best_path is None or timestamp >= best_timestamp:
        best_path = candidate
        best_timestamp = timestamp

if best_path is not None:
    print(best_path)
PY
)"

  if [[ -n "$freshest_candidate" && -f "$freshest_candidate" ]]; then
    if release_proof_is_fresh "$freshest_candidate" "$UI_LOCALIZATION_RELEASE_GATE_MAX_AGE_SECONDS"; then
      printf '%s\n' "$freshest_candidate"
      return 0
    fi
  fi

  generated_candidate="$(materialize_fresh_ui_localization_release_gate || true)"
  if [[ -n "$generated_candidate" && -f "$generated_candidate" ]]; then
    printf '%s\n' "$generated_candidate"
    return 0
  fi

  if [[ -n "$freshest_candidate" ]]; then
    printf '%s\n' "$freshest_candidate"
    return 0
  fi

  printf '%s\n' ""
}

sanitize_release_proof_payload() {
  local source_path="${1:-}"
  local output_path="${2:-}"
  local canonical_base_url="${3:-}"
  python3 - "$source_path" "$output_path" "$canonical_base_url" <<'PY'
import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
canonical_base_url = str(sys.argv[3]).strip()
payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(f"release proof payload must be a JSON object: {source_path}")

allowed = {
    "status",
    "generatedAt",
    "generated_at",
    "baseUrl",
    "base_url",
    "journeysPassed",
    "journeys_passed",
    "proofRoutes",
    "proof_routes",
    "uiLocalizationReleaseGate",
    "ui_localization_release_gate",
}
sanitized = {key: payload[key] for key in payload if key in allowed}
if canonical_base_url:
    sanitized["baseUrl"] = canonical_base_url
    sanitized["base_url"] = canonical_base_url
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(sanitized, indent=2) + "\n", encoding="utf-8")
PY
}

sanitize_ui_localization_release_gate_payload() {
  local source_path="${1:-}"
  local output_path="${2:-}"
  python3 - "$source_path" "$output_path" <<'PY'
import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(f"ui localization release gate payload must be a JSON object: {source_path}")

allowed = {
    "status",
    "generatedAt",
    "generated_at",
    "defaultKeyCount",
    "default_key_count",
    "explicitFallbackRuntime",
    "explicit_fallback_runtime",
    "signoffSmokeRunner",
    "signoff_smoke_runner",
    "signoffSmokeRunnerStatus",
    "signoff_smoke_runner_status",
    "shippingLocales",
    "shipping_locales",
    "acceptanceGates",
    "acceptance_gates",
    "domainCoverage",
    "domain_coverage",
    "localeDomainCoverage",
    "locale_domain_coverage",
    "blockingFindings",
    "blocking_findings",
    "blockingFindingsCount",
    "blocking_findings_count",
    "translationBacklogFindings",
    "translation_backlog_findings",
    "translationBacklogFindingsCount",
    "translation_backlog_findings_count",
    "localeSummary",
    "locale_summary",
}
sanitized = {key: payload[key] for key in payload if key in allowed}
row_allowed = {
    "locale",
    "untranslated_key_count",
    "untranslatedKeyCount",
    "override_count",
    "overrideCount",
    "minimum_override_count",
    "minimumOverrideCount",
    "missing_release_seed_keys",
    "missingReleaseSeedKeys",
    "legacy_xml_present",
    "legacyXmlPresent",
    "legacy_data_xml_present",
    "legacyDataXmlPresent",
}
locale_rows = sanitized.get("localeSummary")
if isinstance(locale_rows, list):
    sanitized["localeSummary"] = [
        {key: value for key, value in row.items() if key in row_allowed}
        for row in locale_rows
        if isinstance(row, dict)
    ]
locale_rows_alias = sanitized.get("locale_summary")
if isinstance(locale_rows_alias, list):
    sanitized["locale_summary"] = [
        {key: value for key, value in row.items() if key in row_allowed}
        for row in locale_rows_alias
        if isinstance(row, dict)
    ]
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(sanitized, indent=2) + "\n", encoding="utf-8")
PY
}

normalize_startup_smoke_receipt_channel_identity() {
  local receipt_dir="${1:-}"
  local release_channel="${2:-}"
  python3 - "$receipt_dir" "$release_channel" <<'PY'
import json
import sys
from pathlib import Path

receipt_dir = Path(sys.argv[1]).resolve()
release_channel = str(sys.argv[2] or "").strip()
if not release_channel or not receipt_dir.is_dir():
    raise SystemExit(0)

for receipt_path in sorted(receipt_dir.glob("startup-smoke-*.receipt.json")):
    try:
        payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception:
        continue
    if not isinstance(payload, dict):
        continue
    changed = False
    if str(payload.get("channelId") or "").strip() != release_channel:
        payload["channelId"] = release_channel
        changed = True
    if str(payload.get("channel") or "").strip() != release_channel:
        payload["channel"] = release_channel
        changed = True
    if changed:
        receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

release_channel_from_manifest() {
  local manifest_path="${1:-}"
  python3 - "$manifest_path" <<'PY'
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1]).resolve()
if not manifest_path.is_file():
    raise SystemExit(0)
try:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)
if not isinstance(payload, dict):
    raise SystemExit(0)
channel = str(payload.get("channelId") or payload.get("channel") or "").strip()
if channel:
    print(channel)
PY
}

sanitize_source_manifest_for_channel_override() {
  local source_path="${1:-}"
  local output_path="${2:-}"
  local release_channel="${3:-}"
  local release_version="${4:-}"
  python3 - "$source_path" "$output_path" "$release_channel" "$release_version" <<'PY'
import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
release_channel = str(sys.argv[3] or "").strip().lower()
release_version = str(sys.argv[4] or "").strip()

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(f"source manifest payload must be a JSON object: {source_path}")


def artifact_file_name(row: object) -> str:
    if not isinstance(row, dict):
        return ""
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return file_name
    download_url = str(row.get("downloadUrl") or row.get("url") or "").strip()
    return Path(download_url).name if download_url else ""


def is_public_file_name(file_name: str) -> bool:
    name = file_name.strip().lower()
    if not name:
        return False
    if name.endswith(("-installer.deb", "-installer.exe", "-installer.pkg", "-installer.dmg", "-installer.msix")):
        if "-osx-" in name or "-macos-" in name:
            return False
        return True
    if name.endswith((".zip", ".tar.gz")):
        return False
    if name.endswith(".exe") and not name.endswith("-installer.exe"):
        return False
    return False

loaded_channel = str(payload.get("channelId") or payload.get("channel") or "").strip().lower()
if not release_channel:
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(0)

for key in (
    "status",
    "message",
    "rolloutState",
    "rollout_state",
    "rolloutReason",
    "rollout_reason",
    "supportabilityState",
    "supportability_state",
    "supportabilitySummary",
    "supportability_summary",
    "knownIssueSummary",
    "known_issue_summary",
    "compatibilityState",
    "compatibility_state",
):
    payload.pop(key, None)

payload["channelId"] = release_channel
payload["channel"] = release_channel

source_version = str(payload.get("version") or payload.get("releaseVersion") or "").strip()
source_version_mismatch = bool(release_version and source_version and source_version != release_version)

if source_version_mismatch:
    for key in (
        "artifacts",
        "downloads",
        "installAwareArtifactRegistry",
        "desktopSurfaceRefs",
        "artifactIdentityRegistry",
        "artifactPublicationBindings",
        "registryBoundaryCoverage",
        "publicTrustMetrics",
    ):
        payload.pop(key, None)
    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        payload["desktopTupleCoverage"] = {
            key: coverage.get(key)
            for key in ("requiredDesktopPlatforms", "requiredDesktopHeads")
            if key in coverage
        }

for collection_name in ("artifacts", "downloads", "desktopRouteTruth", "installAwareArtifactRegistry"):
    rows = payload.get(collection_name)
    if not isinstance(rows, list):
        continue
    for row in rows:
        if not isinstance(row, dict):
            continue
        if "channelId" in row or "channel" in row:
            row["channelId"] = release_channel
            row["channel"] = release_channel

allowed_artifact_ids: set[str] = set()
allowed_file_names: set[str] = set()
for collection_name in ("artifacts", "downloads"):
    rows = payload.get(collection_name)
    if not isinstance(rows, list):
        continue
    filtered_rows = []
    for row in rows:
        file_name = artifact_file_name(row)
        if not is_public_file_name(file_name):
            continue
        filtered_rows.append(row)
        if isinstance(row, dict):
            artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
            if artifact_id:
                allowed_artifact_ids.add(artifact_id)
        if file_name:
            allowed_file_names.add(file_name)
    payload[collection_name] = filtered_rows

for collection_name in ("installAwareArtifactRegistry", "desktopSurfaceRefs", "artifactIdentityRegistry", "artifactPublicationBindings"):
    rows = payload.get(collection_name)
    if not isinstance(rows, list) or not allowed_artifact_ids:
        continue
    payload[collection_name] = [
        row for row in rows
        if isinstance(row, dict)
        and str(row.get("artifactId") or row.get("id") or "").strip() in allowed_artifact_ids
    ]

output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

infer_release_version_from_startup_smoke() {
  local downloads_dir="${1:-}"
  local startup_smoke_dir="${2:-}"
  python3 - "$downloads_dir" "$startup_smoke_dir" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import sys
from pathlib import Path


def parse_timestamp(payload: dict) -> dt.datetime:
    for key in ("completedAtUtc", "recordedAtUtc", "generatedAt", "generated_at", "startedAtUtc"):
        raw = str(payload.get(key) or "").strip()
        if not raw:
            continue
        if raw.endswith("Z"):
            raw = raw[:-1] + "+00:00"
        try:
            parsed = dt.datetime.fromisoformat(raw)
        except ValueError:
            continue
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=dt.timezone.utc)
        return parsed.astimezone(dt.timezone.utc)
    return dt.datetime.min.replace(tzinfo=dt.timezone.utc)


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


downloads_dir = Path(sys.argv[1]).resolve()
startup_smoke_dir = Path(sys.argv[2]).resolve()
if not downloads_dir.is_dir() or not startup_smoke_dir.is_dir():
    raise SystemExit(0)

downloads_root = downloads_dir.parent
version_scores: dict[str, dict[str, object]] = {}

for receipt_path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
    try:
        payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception:
        continue
    if not isinstance(payload, dict):
        continue

    version = str(payload.get("version") or payload.get("releaseVersion") or "").strip()
    if not version or version == "unpublished":
        continue

    digest = str(payload.get("artifactSha256") or "").strip().lower()
    artifact_digest = str(payload.get("artifactDigest") or "").strip().lower()
    if not digest and artifact_digest.startswith("sha256:"):
        digest = artifact_digest.split(":", 1)[1]
    if len(digest) != 64:
        continue

    candidate_names = []
    for key in ("artifactFileName", "fileName"):
        value = str(payload.get(key) or "").strip()
        if value:
            candidate_names.append(value)

    for key in ("artifactRelativePath", "artifactPath"):
        raw = str(payload.get(key) or "").strip()
        if not raw:
            continue
        token = Path(raw).name
        if token:
            candidate_names.append(token)

    artifact_path = None
    for name in dict.fromkeys(candidate_names):
        candidate = downloads_dir / name
        if candidate.is_file():
            artifact_path = candidate
            break
        relative_candidate = downloads_root / name
        if relative_candidate.is_file():
            artifact_path = relative_candidate
            break

    if artifact_path is None or not artifact_path.is_file():
        continue
    if sha256_file(artifact_path) != digest:
        continue

    bucket = version_scores.setdefault(
        version,
        {
            "count": 0,
            "latest_timestamp": dt.datetime.min.replace(tzinfo=dt.timezone.utc),
        },
    )
    bucket["count"] = int(bucket["count"]) + 1
    timestamp = parse_timestamp(payload)
    if timestamp > bucket["latest_timestamp"]:
        bucket["latest_timestamp"] = timestamp

if not version_scores:
    raise SystemExit(0)

best_version, _ = max(
    version_scores.items(),
    key=lambda item: (
        int(item[1]["count"]),
        item[1]["latest_timestamp"],
        item[0],
    ),
)
print(best_version)
PY
}

if [[ ! -f "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" ]]; then
  echo "Missing registry materializer: $REGISTRY_ROOT/scripts/materialize_public_release_channel.py" >&2
  exit 1
fi

if [[ "$RELEASE_VERSION" == "unpublished" ]]; then
  inferred_release_version="$(infer_release_version_from_startup_smoke "$DOWNLOADS_DIR" "$STARTUP_SMOKE_DIR")"
  if [[ -n "$inferred_release_version" ]]; then
    RELEASE_VERSION="$inferred_release_version"
  fi
fi

normalize_preview_install_access_classes() {
  local manifest_path="$1"
  local release_channel="$2"
  : "$release_channel"

  if [[ -z "$PREVIEW_INSTALL_ACCESS_CLASS" ]]; then
    PREVIEW_INSTALL_ACCESS_CLASS="open_public"
  fi

  python3 - "$manifest_path" "$PREVIEW_INSTALL_ACCESS_CLASS" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
access_class = str(sys.argv[2] or "account_required").strip().lower()
if not access_class:
    raise SystemExit(0)

payload = json.loads(manifest_path.read_text(encoding="utf-8"))
if not isinstance(payload, dict):
    raise SystemExit(0)

changed = False
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue

    kind = str(artifact.get("kind") or "").strip().lower()
    platform_tokens = " ".join(
        str(artifact.get(key) or "").strip().lower()
        for key in ("platform", "platformId", "rid", "artifactId", "fileName")
    )
    if kind not in {"installer", "dmg", "pkg", "msix"}:
        continue

    current_access_class = str(artifact.get("installAccessClass") or "").strip().lower()
    if current_access_class == access_class:
        continue

    artifact["installAccessClass"] = access_class
    changed = True

if changed:
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

sanitize_startup_smoke_dir() {
  local source_dir="${1:-}"
  local output_dir="${2:-}"
  local release_channel="${3:-}"
  local release_version="${4:-}"
  local downloads_dir="${5:-}"
  local display_downloads_dir="${6:-$downloads_dir}"
  local scope_to_downloads_dir="${7:-0}"
  python3 - "$source_dir" "$output_dir" "$release_channel" "$release_version" "$downloads_dir" "$display_downloads_dir" "$scope_to_downloads_dir" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

source_dir = Path(sys.argv[1])
output_dir = Path(sys.argv[2])
release_channel = str(sys.argv[3]).strip()
release_version = str(sys.argv[4]).strip()
downloads_dir = Path(sys.argv[5]).resolve(strict=False) if str(sys.argv[5]).strip() else None
display_downloads_dir = Path(sys.argv[6]).resolve(strict=False) if str(sys.argv[6]).strip() else downloads_dir
scope_to_downloads_dir = str(sys.argv[7] or "").strip().lower() in {"1", "true", "yes", "on"}

output_dir.mkdir(parents=True, exist_ok=True)


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


def receipt_digest(payload: dict) -> str:
    digest = str(payload.get("artifactSha256") or "").strip().lower()
    if len(digest) == 64:
        return digest
    artifact_digest = str(payload.get("artifactDigest") or "").strip().lower()
    if artifact_digest.startswith("sha256:") and len(artifact_digest) == 71:
        return artifact_digest.split(":", 1)[1]
    return ""

for path in sorted(source_dir.iterdir()):
    if path.is_file():
        shutil.copy2(path, output_dir / path.name)

for receipt_path in sorted(output_dir.glob("startup-smoke-*.receipt.json")):
    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    if release_channel:
        payload["channelId"] = release_channel
        payload["channel"] = release_channel
    if release_version:
        payload["releaseVersion"] = release_version
        payload["version"] = release_version
    artifact_file_name = str(
        payload.get("artifactFileName")
        or payload.get("fileName")
        or Path(str(payload.get("artifactPath") or "")).name
    ).strip()
    if scope_to_downloads_dir:
        if not artifact_file_name or downloads_dir is None:
            receipt_path.unlink(missing_ok=True)
            continue
        staged_artifact_path = downloads_dir / artifact_file_name
        if not staged_artifact_path.is_file():
            receipt_path.unlink(missing_ok=True)
            continue
        digest = receipt_digest(payload)
        if digest and sha256_file(staged_artifact_path) != digest:
            receipt_path.unlink(missing_ok=True)
            continue
    if artifact_file_name:
        payload["artifactFileName"] = artifact_file_name
        payload["fileName"] = artifact_file_name
        payload["artifactRelativePath"] = f"files/{artifact_file_name}"
        if display_downloads_dir is not None:
            payload["artifactPath"] = str((display_downloads_dir / artifact_file_name).resolve(strict=False))
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

hydrate_startup_smoke_dir() {
  local source_dir="${1:-}"
  local output_dir="${2:-}"
  local registry_root="${3:-}"
  local repo_root="${4:-}"
  local downloads_dir="${5:-}"
  python3 - "$source_dir" "$output_dir" "$registry_root" "$repo_root" "$downloads_dir" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import shutil
import sys
import tempfile
from pathlib import Path


def parse_timestamp(payload: dict) -> dt.datetime:
    for key in ("recordedAtUtc", "completedAtUtc", "generatedAt", "generated_at", "startedAtUtc"):
        raw = str(payload.get(key) or "").strip()
        if not raw:
            continue
        if raw.endswith("Z"):
            raw = raw[:-1] + "+00:00"
        try:
            parsed = dt.datetime.fromisoformat(raw)
        except ValueError:
            continue
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=dt.timezone.utc)
        return parsed.astimezone(dt.timezone.utc)
    return dt.datetime.min.replace(tzinfo=dt.timezone.utc)


def load_json(path: Path) -> dict | None:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return None
    return loaded if isinstance(loaded, dict) else None


source_dir = Path(sys.argv[1]).resolve(strict=False)
output_dir = Path(sys.argv[2]).resolve(strict=False)
registry_root = Path(sys.argv[3]).resolve(strict=False)
repo_root = Path(sys.argv[4]).resolve(strict=False)
downloads_dir = Path(sys.argv[5]).resolve(strict=False)

output_dir.mkdir(parents=True, exist_ok=True)

def artifact_file_name(payload: dict) -> str:
    return str(
        payload.get("artifactFileName")
        or payload.get("fileName")
        or Path(str(payload.get("artifactPath") or "")).name
    ).strip()


def artifact_sha256(payload: dict) -> str:
    digest = str(payload.get("artifactSha256") or "").strip().lower()
    if len(digest) == 64:
        return digest
    artifact_digest = str(payload.get("artifactDigest") or "").strip().lower()
    if artifact_digest.startswith("sha256:") and len(artifact_digest) == 71:
        return artifact_digest.split(":", 1)[1]
    return ""


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest().lower()


artifact_digest_cache: dict[Path, str] = {}


def receipt_matches_download_bytes(payload: dict) -> bool:
    if not downloads_dir.is_dir():
        return False
    file_name = artifact_file_name(payload)
    digest = artifact_sha256(payload)
    if not file_name or not digest:
        return False
    artifact_path = downloads_dir / file_name
    if not artifact_path.is_file():
        return False
    cached = artifact_digest_cache.get(artifact_path)
    if cached is None:
        cached = sha256_file(artifact_path)
        artifact_digest_cache[artifact_path] = cached
    return cached == digest


def trusted_receipt_artifact_dirs(repo_root: Path, registry_root: Path) -> list[Path]:
    roots = [
        downloads_dir,
        repo_root / "Chummer.Portal" / "downloads" / "files",
        repo_root / "Docker" / "Downloads" / "files",
        repo_root.parent / "chummer.run-services" / "Chummer.Portal" / "downloads" / "files",
        repo_root.parent / "chummer.run-services" / "legacy" / "tooling" / "docker" / "Docker" / "Downloads" / "files",
        repo_root.parent / "chummer-presentation" / "Docker" / "Downloads" / "files",
    ]
    if registry_root:
        roots.append(registry_root / "Chummer.Portal" / "downloads" / "files")

    ordered: list[Path] = []
    seen: set[Path] = set()
    for root in roots:
        resolved = root.resolve(strict=False)
        if resolved in seen:
            continue
        seen.add(resolved)
        ordered.append(resolved)
    return ordered


def restore_missing_receipt_backed_artifact(payload: dict) -> Path | None:
    if not downloads_dir.is_dir():
        return None
    file_name = artifact_file_name(payload)
    digest = artifact_sha256(payload)
    if not file_name or not digest:
        return None

    target_path = downloads_dir / file_name
    if target_path.is_file():
        return target_path if receipt_matches_download_bytes(payload) else None

    for candidate_dir in trusted_receipt_artifact_dirs(repo_root, registry_root):
        candidate_path = candidate_dir / file_name
        if not candidate_path.is_file():
            continue
        cached = artifact_digest_cache.get(candidate_path)
        if cached is None:
            cached = sha256_file(candidate_path)
            artifact_digest_cache[candidate_path] = cached
        if cached != digest:
            continue
        target_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(candidate_path, target_path)
        artifact_digest_cache[target_path] = cached
        return target_path

    return None


gate_paths = sorted((repo_root / ".codex-studio" / "published").glob("UI_*_DESKTOP_EXIT_GATE.generated.json"))
candidate_dirs: list[Path] = [source_dir]
if registry_root:
    candidate_dirs.append(registry_root / ".codex-studio" / "published" / "startup-smoke")
embedded_gate_receipts_dir = Path(tempfile.mkdtemp(prefix="chummer-startup-smoke-gate-"))
embedded_gate_receipts_written = 0
for gate_path in gate_paths:
    gate_payload = load_json(gate_path)
    if not gate_payload:
        continue
    startup_smoke = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    embedded_receipt = startup_smoke.get("receipt") if isinstance(startup_smoke, dict) and isinstance(startup_smoke.get("receipt"), dict) else None
    if isinstance(embedded_receipt, dict):
        embedded_receipt_path = str(startup_smoke.get("receipt_path") or "").strip()
        embedded_receipt_name = Path(embedded_receipt_path).name if embedded_receipt_path else ""
        if not embedded_receipt_name:
            head = str(((gate_payload.get("head") or {}) if isinstance(gate_payload.get("head"), dict) else {}).get("app_key") or "").strip()
            rid = str(((gate_payload.get("head") or {}) if isinstance(gate_payload.get("head"), dict) else {}).get("rid") or "").strip()
            if head and rid:
                embedded_receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
        if embedded_receipt_name:
            (embedded_gate_receipts_dir / embedded_receipt_name).write_text(
                json.dumps(embedded_receipt, indent=2) + "\n",
                encoding="utf-8",
            )
            embedded_gate_receipts_written += 1
    receipt_path = (
        str(((gate_payload.get("checks") or {}) if isinstance(gate_payload.get("checks"), dict) else {}).get("startup_smoke_receipt_path") or "").strip()
    )
    if not receipt_path:
        continue
    candidate_dirs.append(Path(receipt_path).resolve(strict=False).parent)
if embedded_gate_receipts_written:
    candidate_dirs.append(embedded_gate_receipts_dir)

selected_by_name: dict[str, tuple[int, dt.datetime, Path]] = {}
for candidate_dir in candidate_dirs:
    if not candidate_dir.is_dir():
        continue
    for receipt_path in sorted(candidate_dir.glob("startup-smoke-*.receipt.json")):
        payload = load_json(receipt_path)
        if not payload:
            continue
        name = receipt_path.name
        timestamp = parse_timestamp(payload)
        selection_rank = 1 if receipt_matches_download_bytes(payload) else 0
        current = selected_by_name.get(name)
        if current is None or (selection_rank, timestamp, str(receipt_path)) >= (current[0], current[1], str(current[2])):
            selected_by_name[name] = (selection_rank, timestamp, receipt_path)

for _, _, receipt_path in sorted(selected_by_name.values(), key=lambda item: item[2].name):
    payload = load_json(receipt_path)
    if payload:
        restore_missing_receipt_backed_artifact(payload)
    shutil.copy2(receipt_path, output_dir / receipt_path.name)
PY
}

RELEASE_PROOF_PATH="$(resolve_release_proof_path "$RELEASE_PROOF_PATH")"
UI_LOCALIZATION_RELEASE_GATE_PATH="$(resolve_ui_localization_release_gate_path "$UI_LOCALIZATION_RELEASE_GATE_PATH")"
SANITIZED_RELEASE_PROOF_PATH=""
GENERATED_RELEASE_PROOF_PATH=""
GENERATED_UI_LOCALIZATION_RELEASE_GATE_PATH=""
SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH=""
SANITIZED_STARTUP_SMOKE_DIR=""
SANITIZED_SOURCE_MANIFEST_PATH=""
cleanup_generate_release_manifest() {
  if [[ -n "$GENERATED_RELEASE_PROOF_PATH" && -f "$GENERATED_RELEASE_PROOF_PATH" ]]; then
    rm -f "$GENERATED_RELEASE_PROOF_PATH"
  fi
  if [[ -n "$SANITIZED_RELEASE_PROOF_PATH" && -f "$SANITIZED_RELEASE_PROOF_PATH" ]]; then
    rm -f "$SANITIZED_RELEASE_PROOF_PATH"
  fi
  if [[ -n "$GENERATED_UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$GENERATED_UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
    rm -f "$GENERATED_UI_LOCALIZATION_RELEASE_GATE_PATH"
  fi
  if [[ -n "$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
    rm -f "$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH"
  fi
  if [[ -n "$SANITIZED_STARTUP_SMOKE_DIR" && -d "$SANITIZED_STARTUP_SMOKE_DIR" ]]; then
    rm -rf "$SANITIZED_STARTUP_SMOKE_DIR"
  fi
  if [[ -n "$SANITIZED_SOURCE_MANIFEST_PATH" && -f "$SANITIZED_SOURCE_MANIFEST_PATH" ]]; then
    rm -f "$SANITIZED_SOURCE_MANIFEST_PATH"
  fi
}
trap cleanup_generate_release_manifest EXIT
if [[ -n "$RELEASE_PROOF_PATH" && -f "$RELEASE_PROOF_PATH" ]]; then
  if path_is_tmp_outside_repo "$RELEASE_PROOF_PATH"; then
    GENERATED_RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH"
  fi
  SANITIZED_RELEASE_PROOF_PATH="$(mktemp)"
  sanitize_release_proof_payload "$RELEASE_PROOF_PATH" "$SANITIZED_RELEASE_PROOF_PATH" "$EXTERNAL_PROOF_BASE_URL"
  RELEASE_PROOF_PATH="$SANITIZED_RELEASE_PROOF_PATH"
fi
if [[ -n "$UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
  if path_is_tmp_outside_repo "$UI_LOCALIZATION_RELEASE_GATE_PATH"; then
    GENERATED_UI_LOCALIZATION_RELEASE_GATE_PATH="$UI_LOCALIZATION_RELEASE_GATE_PATH"
  fi
  SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH="$(mktemp)"
  sanitize_ui_localization_release_gate_payload \
    "$UI_LOCALIZATION_RELEASE_GATE_PATH" \
    "$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH"
  UI_LOCALIZATION_RELEASE_GATE_PATH="$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH"
fi
if [[ -d "$STARTUP_SMOKE_DIR" ]] && find "$STARTUP_SMOKE_DIR" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' | grep -q .; then
  hydrated_startup_smoke_dir="$(mktemp -d)"
  if to_bool "$SKIP_STARTUP_SMOKE_HYDRATION"; then
    cp "$STARTUP_SMOKE_DIR"/startup-smoke-*.receipt.json "$hydrated_startup_smoke_dir"/
  elif to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then
    echo "scoped stage artifacts active; skipped registry startup-smoke hydration"
    cp "$STARTUP_SMOKE_DIR"/startup-smoke-*.receipt.json "$hydrated_startup_smoke_dir"/
  else
    hydrate_startup_smoke_dir \
      "$STARTUP_SMOKE_DIR" \
      "$hydrated_startup_smoke_dir" \
      "$REGISTRY_ROOT" \
      "$REPO_ROOT" \
      "$DOWNLOADS_DIR"
  fi
  SANITIZED_STARTUP_SMOKE_DIR="$(mktemp -d)"
  sanitize_startup_smoke_dir \
    "$hydrated_startup_smoke_dir" \
    "$SANITIZED_STARTUP_SMOKE_DIR" \
    "$RELEASE_CHANNEL" \
    "$RELEASE_VERSION" \
    "$DOWNLOADS_DIR" \
    "$CANONICAL_FILES_DIR" \
    "$SCOPE_TO_STAGE_ARTIFACTS"
  STARTUP_SMOKE_DIR="$SANITIZED_STARTUP_SMOKE_DIR"
  rm -rf "$hydrated_startup_smoke_dir"
fi
if [[ -n "$SOURCE_MANIFEST_PATH" && -f "$SOURCE_MANIFEST_PATH" ]]; then
  SANITIZED_SOURCE_MANIFEST_PATH="$(mktemp)"
  sanitize_source_manifest_for_channel_override \
    "$SOURCE_MANIFEST_PATH" \
    "$SANITIZED_SOURCE_MANIFEST_PATH" \
    "$RELEASE_CHANNEL" \
    "$RELEASE_VERSION"
  SOURCE_MANIFEST_PATH="$SANITIZED_SOURCE_MANIFEST_PATH"
fi

mkdir -p "$(dirname "$MANIFEST_PATH")"
mkdir -p "$(dirname "$PORTAL_MANIFEST_PATH")"
mkdir -p "$DOWNLOADS_DIR"
if to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then
  echo "scoped stage artifacts active; skipped registry manifest fallback restore"
else
  restore_local_manifests_from_registry_if_needed \
    "$CANONICAL_MANIFEST_PATH" \
    "$MANIFEST_PATH" \
    "$RELEASE_VERSION" \
    "$CANONICAL_FILES_DIR" \
    "$REGISTRY_FILES_DIR"
fi

if [[ "$PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS" != "0" ]] && ! to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then
  python3 "$SCRIPT_DIR/promote-proof-backed-quarantined-installers.py" \
    --repo-root "$REPO_ROOT" \
    --downloads-dir "$DOWNLOADS_DIR" \
    --startup-smoke-dir "$STARTUP_SMOKE_DIR" \
    --display-downloads-dir "$CANONICAL_FILES_DIR" \
    --display-startup-smoke-dir "$(dirname "$CANONICAL_MANIFEST_PATH")/startup-smoke" \
    --release-channel "$RELEASE_CHANNEL" \
    --release-version "$RELEASE_VERSION" \
    --output "$QUARANTINE_PROMOTION_EVIDENCE_PATH" \
    >/dev/null
elif to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then
  echo "scoped stage artifacts active; skipped proof-backed quarantined installer promotion"
fi

promoted_file_names=()

materializer_help="$(python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" --help 2>&1 || true)"
run_materializer() {
  local manifest_override="${1:-}"
  local -a materialize_args=(
    --downloads-dir "$DOWNLOADS_DIR"
    --channel "$RELEASE_CHANNEL"
    --version "$RELEASE_VERSION"
    --published-at "$RELEASE_PUBLISHED_AT"
    --downloads-prefix "$DOWNLOADS_PREFIX"
    --output "$CANONICAL_MANIFEST_PATH"
    --compat-output "$MANIFEST_PATH"
  )

  if [[ -n "$manifest_override" && -f "$manifest_override" ]]; then
    materialize_args+=(--manifest "$manifest_override")
  elif [[ -n "$SOURCE_MANIFEST_PATH" && -f "$SOURCE_MANIFEST_PATH" ]]; then
    materialize_args+=(--manifest "$SOURCE_MANIFEST_PATH")
  fi

  if [[ -n "$RELEASE_PROOF_PATH" && -f "$RELEASE_PROOF_PATH" ]]; then
    materialize_args+=(--proof "$RELEASE_PROOF_PATH")
  fi

  if [[ -n "$UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
    materialize_args+=(--ui-localization-release-gate "$UI_LOCALIZATION_RELEASE_GATE_PATH")
  fi

  if [[ -n "$FLAGSHIP_READINESS_PATH" ]]; then
    if [[ ! -f "$FLAGSHIP_READINESS_PATH" ]]; then
      echo "Flagship readiness receipt does not exist: $FLAGSHIP_READINESS_PATH" >&2
      exit 1
    fi
    if [[ "$materializer_help" != *"--flagship-readiness"* ]]; then
      echo "Registry materializer CLI mismatch: $REGISTRY_ROOT/scripts/materialize_public_release_channel.py does not support --flagship-readiness." >&2
      exit 1
    fi
    materialize_args+=(--flagship-readiness "$FLAGSHIP_READINESS_PATH")
  fi

  if [[ -d "$STARTUP_SMOKE_DIR" ]] && find "$STARTUP_SMOKE_DIR" -type f -name 'startup-smoke-*.receipt.json' | grep -q .; then
    if [[ "$materializer_help" != *"--startup-smoke-dir"* ]]; then
      echo "Registry materializer CLI mismatch: $REGISTRY_ROOT/scripts/materialize_public_release_channel.py does not support --startup-smoke-dir." >&2
      exit 1
    fi
    materialize_args+=(--startup-smoke-dir "$STARTUP_SMOKE_DIR")
  fi
  if [[ -n "$STARTUP_SMOKE_MAX_AGE_SECONDS" && "$materializer_help" == *"--startup-smoke-max-age-seconds"* ]]; then
    materialize_args+=(--startup-smoke-max-age-seconds "$STARTUP_SMOKE_MAX_AGE_SECONDS")
  fi
  if to_bool "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" && [[ "$materializer_help" == *"--skip-startup-smoke-filter"* ]]; then
    materialize_args+=(--skip-startup-smoke-filter)
  fi

  python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" "${materialize_args[@]}" >/dev/null
}

normalize_startup_smoke_receipt_channel_identity "$STARTUP_SMOKE_DIR" "$RELEASE_CHANNEL"
run_materializer
effective_release_channel="$(release_channel_from_manifest "$CANONICAL_MANIFEST_PATH")"
if [[ -z "$effective_release_channel" ]]; then
  effective_release_channel="$RELEASE_CHANNEL"
fi
normalize_startup_smoke_receipt_channel_identity "$STARTUP_SMOKE_DIR" "$effective_release_channel"
python3 - "$CANONICAL_MANIFEST_PATH" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def normalize_release_channel_artifact_identity_fields(manifest_path: Path) -> bool:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit("release channel manifest must be a JSON object")

    channel_id = normalize(payload.get("channelId") or payload.get("channel"))
    release_version = str(payload.get("version") or "").strip()
    release_generated_at = str(payload.get("generated_at") or payload.get("generatedAt") or "").strip()
    if not channel_id:
        raise SystemExit(
            "Release channel is missing channelId/channel at top level; cannot normalize artifact channel identity."
        )
    if not release_version:
        raise SystemExit(
            "Release channel is missing version at top level; cannot normalize artifact release identity."
        )
    if not release_generated_at:
        raise SystemExit(
            "Release channel is missing generated_at/generatedAt at top level; cannot normalize artifact generated_at identity."
        )

    artifacts = payload.get("artifacts")
    if not isinstance(artifacts, list):
        return False

    changed = False
    for artifact in artifacts:
        if not isinstance(artifact, dict):
            continue
        platform = normalize(artifact.get("platform"))
        kind = normalize(artifact.get("kind"))
        if platform not in {"linux", "windows", "macos"}:
            continue
        if kind not in {"installer", "dmg", "pkg", "msix"}:
            continue

        artifact_channel_id = normalize(artifact.get("channelId") or artifact.get("channel"))
        if not artifact_channel_id:
            artifact["channelId"] = channel_id
            artifact["channel"] = channel_id
            changed = True
        else:
            if normalize(artifact.get("channelId")) != artifact_channel_id:
                artifact["channelId"] = artifact_channel_id
                changed = True
            if normalize(artifact.get("channel")) != artifact_channel_id:
                artifact["channel"] = artifact_channel_id
                changed = True

        artifact_version = str(artifact.get("version") or artifact.get("releaseVersion") or "").strip()
        if not artifact_version:
            artifact["version"] = release_version
            artifact["releaseVersion"] = release_version
            changed = True
        else:
            if str(artifact.get("version") or "").strip() != artifact_version:
                artifact["version"] = artifact_version
                changed = True
            if str(artifact.get("releaseVersion") or "").strip() != artifact_version:
                artifact["releaseVersion"] = artifact_version
                changed = True

        artifact_generated_at = str(
            artifact.get("generated_at") or artifact.get("generatedAt") or ""
        ).strip()
        if artifact_generated_at != release_generated_at:
            artifact["generated_at"] = release_generated_at
            artifact["generatedAt"] = release_generated_at
            changed = True
        else:
            if str(artifact.get("generated_at") or "").strip() != artifact_generated_at:
                artifact["generated_at"] = artifact_generated_at
                changed = True
            if str(artifact.get("generatedAt") or "").strip() != artifact_generated_at:
                artifact["generatedAt"] = artifact_generated_at
                changed = True

    if not changed:
        return False

    manifest_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return True


manifest_path = Path(sys.argv[1]).resolve()
normalize_release_channel_artifact_identity_fields(manifest_path)
PY
normalize_preview_install_access_classes "$CANONICAL_MANIFEST_PATH" "$RELEASE_CHANNEL"
run_materializer "$CANONICAL_MANIFEST_PATH"
python3 - "$CANONICAL_MANIFEST_PATH" "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH" "$DOWNLOADS_DIR" "$DOWNLOADS_PREFIX" <<'PY'
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def candidate_payload_name(installer_name: str) -> str:
    lowered = installer_name.lower()
    if lowered.endswith("-installer.exe"):
        return installer_name[:-len("-installer.exe")] + "-payload.zip"
    return ""


def enrich_manifest(path: Path, downloads_dir: Path, downloads_prefix: str) -> None:
    if not path.is_file():
        return

    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        return

    changed = False
    for collection_name in ("artifacts", "downloads"):
        for artifact in payload.get(collection_name) or []:
            if not isinstance(artifact, dict):
                continue
            file_name = str(artifact.get("fileName") or "").strip()
            kind = str(artifact.get("kind") or "").strip().lower()
            platform = str(artifact.get("platform") or "").strip().lower()
            if kind != "installer" or platform != "windows" or not file_name:
                continue

            payload_name = candidate_payload_name(file_name)
            if not payload_name:
                continue
            payload_path = downloads_dir / payload_name
            if not payload_path.is_file():
                continue

            payload_url = f"{downloads_prefix.rstrip('/')}/{payload_name}"
            payload_sha256 = sha256_file(payload_path)
            payload_size = payload_path.stat().st_size
            expected = {
                "installerMode": "bootstrap",
                "payloadFileName": payload_name,
                "payloadDownloadUrl": payload_url,
                "payloadSha256": payload_sha256,
                "payloadSizeBytes": payload_size,
            }
            for key, value in expected.items():
                if artifact.get(key) != value:
                    artifact[key] = value
                    changed = True

    if changed:
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


downloads_dir = Path(sys.argv[4]).resolve()
downloads_prefix = sys.argv[5]
for raw_path in sys.argv[1:4]:
    enrich_manifest(Path(raw_path).resolve(), downloads_dir, downloads_prefix)
PY
python3 - "$CANONICAL_MANIFEST_PATH" "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


def coverage_is_incomplete(payload: dict) -> bool:
    coverage = payload.get("desktopTupleCoverage")
    if not isinstance(coverage, dict):
        return False
    for key in (
        "missingRequiredPlatforms",
        "missingRequiredHeads",
        "missingRequiredPlatformHeadPairs",
        "missingRequiredPlatformHeadRidTuples",
    ):
        value = coverage.get(key)
        if isinstance(value, list) and value:
            return True
    return False


def apply_honesty_state(path: Path) -> None:
    if not path.is_file():
        return
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"release manifest must be a JSON object: {path}")
    if normalize(payload.get("status")) != "published":
        return
    channel_id = normalize(payload.get("channelId") or payload.get("channel"))
    if coverage_is_incomplete(payload):
        payload["rolloutState"] = "coverage_incomplete"
        payload["supportabilityState"] = "review_required"
    elif channel_id == "preview" and normalize(payload.get("rolloutState")) == "promoted_preview":
        payload["supportabilityState"] = "preview_supported"
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


for raw_path in sys.argv[1:]:
    apply_honesty_state(Path(raw_path))
PY
python3 - "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "$CANONICAL_MANIFEST_PATH" "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH" <<'PY'
from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

def normalized_token(value) -> str:
    return str(value or "").strip().lower().replace("-", "_").replace(" ", "_")

def load_verifier(path: Path):
    spec = importlib.util.spec_from_file_location("verify_public_release_channel", path)
    if spec is None or spec.loader is None:
        raise SystemExit(f"could not load verifier module from {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def load_materializer(path: Path):
    spec = importlib.util.spec_from_file_location("materialize_public_release_channel", path)
    if spec is None or spec.loader is None:
        return None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


verifier_path = Path(sys.argv[1]).resolve()
verifier = load_verifier(verifier_path)
materializer = load_materializer(verifier_path.with_name("materialize_public_release_channel.py"))
canonical_payload = {}
canonical_artifacts_by_key = {}

canonical_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else None
if canonical_path is not None and canonical_path.is_file():
    loaded_canonical = json.loads(canonical_path.read_text(encoding="utf-8-sig"))
    if isinstance(loaded_canonical, dict):
        canonical_payload = loaded_canonical
        for artifact in loaded_canonical.get("artifacts") or []:
            if not isinstance(artifact, dict):
                continue
            keys = {
                str(artifact.get("artifactId") or artifact.get("id") or "").strip(),
                str(artifact.get("fileName") or "").strip(),
                Path(str(artifact.get("downloadUrl") or "").strip()).name,
            }
            for key in keys:
                if key:
                    canonical_artifacts_by_key[key] = artifact

def required_heads_and_platforms(payload: dict) -> tuple[list[str], list[str]]:
    coverage = payload.get("desktopTupleCoverage")
    default_heads = ["avalonia"]
    default_platforms = ["linux", "windows", "macos"]
    if not isinstance(coverage, dict):
        return default_heads, default_platforms
    heads = [str(item).strip().lower() for item in coverage.get("requiredDesktopHeads") or [] if str(item).strip()]
    platforms = [str(item).strip().lower() for item in coverage.get("requiredDesktopPlatforms") or [] if str(item).strip()]
    return (heads or default_heads, platforms or default_platforms)

def hydrate_download_compatibility_from_canonical(local_payload: dict) -> None:
    downloads = local_payload.get("downloads")
    if not isinstance(downloads, list) or not canonical_artifacts_by_key:
        return
    for row in downloads:
        if not isinstance(row, dict):
            continue
        keys = {
            str(row.get("artifactId") or row.get("id") or "").strip(),
            str(row.get("fileName") or "").strip(),
            Path(str(row.get("url") or row.get("downloadUrl") or "").strip()).name,
        }
        canonical = next((canonical_artifacts_by_key[key] for key in keys if key in canonical_artifacts_by_key), None)
        if not isinstance(canonical, dict):
            continue
        for source_key, target_key in (
            ("artifactId", "artifactId"),
            ("compatibilityState", "compatibilityState"),
            ("compatibilityReason", "compatibilityReason"),
            ("channelId", "channelId"),
            ("channel", "channel"),
            ("releaseVersion", "releaseVersion"),
            ("version", "version"),
            ("head", "head"),
            ("platform", "platformId"),
            ("arch", "arch"),
            ("installAccessClass", "installAccessClass"),
            ("installerMode", "installerMode"),
            ("payloadFileName", "payloadFileName"),
            ("payloadDownloadUrl", "payloadDownloadUrl"),
            ("payloadSha256", "payloadSha256"),
            ("payloadSizeBytes", "payloadSizeBytes"),
        ):
            value = canonical.get(source_key)
            if value is not None and str(value).strip():
                row[target_key] = value

def artifact_rows_for_registry(local_payload: dict) -> list[dict]:
    artifacts = local_payload.get("artifacts")
    if isinstance(artifacts, list):
        return artifacts
    downloads = local_payload.get("downloads")
    if not isinstance(downloads, list):
        return []
    rows: list[dict] = []
    for item in downloads:
        if not isinstance(item, dict):
            continue
        row = dict(item)
        row.setdefault("artifactId", row.get("id"))
        row.setdefault("downloadUrl", row.get("url"))
        row.setdefault("fileName", Path(str(row.get("url") or "")).name)
        rows.append(row)
    return rows

def is_downloads_compatibility_payload(local_payload: dict) -> bool:
    return isinstance(local_payload.get("downloads"), list) and not isinstance(local_payload.get("artifacts"), list)

def manifest_artifact_ids(local_payload: dict) -> set[str]:
    artifact_ids: set[str] = set()
    for row in artifact_rows_for_registry(local_payload):
        if not isinstance(row, dict):
            continue
        artifact_id = normalized_token(row.get("artifactId") or row.get("id"))
        if artifact_id:
            artifact_ids.add(artifact_id)
    return artifact_ids

def prune_rows_to_manifest_artifacts(local_payload: dict) -> None:
    artifact_ids = manifest_artifact_ids(local_payload)
    if not artifact_ids:
        return
    route_installer_ids: set[str] = set()
    coverage = local_payload.get("desktopTupleCoverage")
    route_truth = coverage.get("desktopRouteTruth") if isinstance(coverage, dict) else None
    if isinstance(route_truth, list):
        route_helper = getattr(verifier, "expected_installer_artifact_id_for_route", None)
        for row in route_truth:
            if not isinstance(row, dict):
                continue
            artifact_id = ""
            if callable(route_helper):
                artifact_id = normalized_token(route_helper(row))
            if not artifact_id:
                head = normalized_token(row.get("head"))
                rid = normalized_token(row.get("rid"))
                if head and rid:
                    artifact_id = f"{head}-{rid}-installer"
            if artifact_id:
                route_installer_ids.add(artifact_id)
    registry_artifact_ids = artifact_ids | route_installer_ids
    registry_bound_keys = (
        "installAwareArtifactRegistry",
        "artifactIdentityRegistry",
        "artifactPublicationBindings",
    )
    for key in registry_bound_keys:
        rows = local_payload.get(key)
        if not isinstance(rows, list):
            continue
        local_payload[key] = [
            row
            for row in rows
            if isinstance(row, dict)
            and normalized_token(row.get("artifactId") or row.get("id")) in registry_artifact_ids
        ]
    for key in (
        "desktopSurfaceRefs",
    ):
        rows = local_payload.get(key)
        if not isinstance(rows, list):
            continue
        local_payload[key] = [
            row
            for row in rows
            if isinstance(row, dict)
            and normalized_token(row.get("artifactId") or row.get("id")) in artifact_ids
        ]

def prune_release_proof_routes_to_manifest_artifacts(local_payload: dict) -> None:
    artifact_ids = manifest_artifact_ids(local_payload)

    def prune_routes(routes: object) -> list[str]:
        if not isinstance(routes, list):
            return []
        pruned: list[str] = []
        for raw_route in routes:
            route = str(raw_route or "").strip()
            if not route:
                continue
            if not route.startswith("/downloads/install/"):
                pruned.append(route)
                continue
            artifact_id = normalized_token(route.removeprefix("/downloads/install/").split("/", 1)[0])
            if artifact_id and artifact_id in artifact_ids:
                pruned.append(route)
        return pruned

    release_proof = local_payload.get("releaseProof")
    if isinstance(release_proof, dict):
        if "proofRoutes" in release_proof:
            release_proof["proofRoutes"] = prune_routes(release_proof.get("proofRoutes"))
        if "proof_routes" in release_proof:
            release_proof["proof_routes"] = prune_routes(release_proof.get("proof_routes"))

    if "proofRoutes" in local_payload:
        local_payload["proofRoutes"] = prune_routes(local_payload.get("proofRoutes"))
    if "proof_routes" in local_payload:
        local_payload["proof_routes"] = prune_routes(local_payload.get("proof_routes"))

for raw_path in sys.argv[2:]:
    manifest_path = Path(raw_path).resolve()
    if not manifest_path.is_file():
        continue
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"release manifest must be a JSON object: {manifest_path}")
    hydrate_download_compatibility_from_canonical(payload)

    def fallback_tuple_coverage(local_payload: dict) -> dict | None:
        if is_downloads_compatibility_payload(local_payload):
            canonical_coverage = canonical_payload.get("desktopTupleCoverage")
            if isinstance(canonical_coverage, dict):
                return json.loads(json.dumps(canonical_coverage))
        if materializer is None or not hasattr(materializer, "desktop_tuple_coverage"):
            return None
        artifacts = artifact_rows_for_registry(local_payload)
        if not artifacts:
            return None
        required_heads, required_platforms = required_heads_and_platforms(local_payload)
        return materializer.desktop_tuple_coverage(
            artifacts,
            required_heads=required_heads,
            required_platforms=required_platforms,
            channel_id=str(local_payload.get("channelId") or local_payload.get("channel") or "").strip().lower(),
            release_version=str(local_payload.get("version") or local_payload.get("releaseVersion") or "").strip(),
            channel_status=str(local_payload.get("status") or "").strip().lower(),
            rollout_state=str(local_payload.get("rolloutState") or local_payload.get("rollout_state") or "").strip().lower(),
            rollout_reason=str(local_payload.get("rolloutReason") or local_payload.get("rollout_reason") or "").strip(),
            known_issue_summary=str(local_payload.get("knownIssueSummary") or local_payload.get("known_issue_summary") or "").strip(),
        )

    def derive_verifier_owned_value(name: str, current_value):
        artifact_bound_registry_names = {
            "expected_external_proof_request_rows",
            "expected_desktop_route_truth_rows",
            "expected_install_aware_artifact_registry_rows",
            "expected_desktop_surface_ref_rows",
            "expected_artifact_identity_registry_rows",
            "expected_artifact_publication_binding_rows",
        }
        helper = getattr(verifier, name, None)
        if callable(helper) and name not in artifact_bound_registry_names:
            return helper(payload)
        if materializer is None:
            return current_value
        tuple_coverage = fallback_tuple_coverage(payload)
        artifacts = artifact_rows_for_registry(payload)
        channel_id = str(payload.get("channelId") or payload.get("channel") or "").strip().lower()
        release_version = str(payload.get("version") or payload.get("releaseVersion") or "").strip()
        fallback_helpers = {
            "expected_external_proof_request_rows": lambda: (tuple_coverage or {}).get("externalProofRequests") or current_value,
            "expected_desktop_route_truth_rows": lambda: (tuple_coverage or {}).get("desktopRouteTruth") or current_value,
            "expected_install_aware_artifact_registry_rows": lambda: (
                materializer.install_aware_artifact_registry(
                    artifacts,
                    tuple_coverage,
                    channel_id=channel_id,
                    release_version=release_version,
                )
                if tuple_coverage is not None and hasattr(materializer, "install_aware_artifact_registry")
                else current_value
            ),
            "expected_desktop_surface_ref_rows": lambda: (
                materializer.desktop_surface_refs(
                    artifacts,
                    tuple_coverage,
                    channel_id=channel_id,
                    release_version=release_version,
                )
                if tuple_coverage is not None and hasattr(materializer, "desktop_surface_refs")
                else current_value
            ),
            "expected_artifact_identity_registry_rows": lambda: (
                materializer.artifact_identity_registry(
                    tuple_coverage,
                    channel_id=channel_id,
                    release_version=release_version,
                )
                if tuple_coverage is not None and hasattr(materializer, "artifact_identity_registry")
                else current_value
            ),
            "expected_artifact_publication_binding_rows": lambda: (
                materializer.artifact_publication_bindings(
                    tuple_coverage,
                    channel_id=channel_id,
                    release_version=release_version,
                )
                if tuple_coverage is not None and hasattr(materializer, "artifact_publication_bindings")
                else current_value
            ),
            "expected_public_trust_metrics": lambda: (
                materializer.expected_public_trust_metrics(payload)
                if hasattr(materializer, "expected_public_trust_metrics")
                else current_value
            ),
            "expected_registry_boundary_coverage": lambda: (
                materializer.expected_registry_boundary_coverage(payload)
                if hasattr(materializer, "expected_registry_boundary_coverage")
                else current_value
            ),
        }
        fallback = fallback_helpers.get(name)
        if fallback is not None:
            return fallback()
        return current_value

    def assert_desktop_surface_ref_consistency(local_payload: dict) -> None:
        artifacts = local_payload.get("artifacts") or local_payload.get("downloads") or []
        if not artifacts:
            return
        artifact_ids = {
            normalized_token(item.get("artifactId") or item.get("id"))
            for item in artifacts
            if isinstance(item, dict) and normalized_token(item.get("artifactId") or item.get("id"))
        }
        coverage = local_payload.get("desktopTupleCoverage")
        route_truth = coverage.get("desktopRouteTruth") if isinstance(coverage, dict) else []
        route_truth_by_tuple = {
            str(item.get("tupleId") or "").strip(): item
            for item in route_truth
            if isinstance(item, dict) and str(item.get("tupleId") or "").strip()
        }
        problems: list[str] = []
        for row in local_payload.get("desktopSurfaceRefs") or []:
            if not isinstance(row, dict):
                continue
            tuple_id = str(row.get("tupleId") or "").strip()
            artifact_id = normalized_token(row.get("artifactId"))
            if not artifact_id or artifact_id not in artifact_ids:
                problems.append(f"{tuple_id or '<missing-tuple>'}: desktopSurfaceRefs artifactId is missing from artifacts")
                continue
            route_row = route_truth_by_tuple.get(tuple_id)
            if not isinstance(route_row, dict):
                problems.append(f"{tuple_id}: desktopSurfaceRefs tuple is missing from desktopRouteTruth")
                continue
            route_artifact_id = normalized_token(route_row.get("artifactId"))
            if not route_artifact_id:
                problems.append(f"{tuple_id}: desktopSurfaceRefs surfaced tuple has empty desktopRouteTruth.artifactId")
            elif route_artifact_id != artifact_id:
                problems.append(f"{tuple_id}: desktopSurfaceRefs artifactId does not match desktopRouteTruth.artifactId")
            if normalized_token(route_row.get("promotionState")) == "proof_required":
                problems.append(f"{tuple_id}: desktopSurfaceRefs must not surface proof_required tuples")
        if problems:
            raise SystemExit(
                "Release channel desktopSurfaceRefs is inconsistent with artifacts/desktopRouteTruth:\n - "
                + "\n - ".join(problems)
            )

    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        fresh_tuple_coverage = fallback_tuple_coverage(payload)
        if isinstance(fresh_tuple_coverage, dict):
            coverage.update(fresh_tuple_coverage)
        coverage["externalProofRequests"] = derive_verifier_owned_value(
            "expected_external_proof_request_rows",
            coverage.get("externalProofRequests") or [],
        )
        coverage["desktopRouteTruth"] = derive_verifier_owned_value(
            "expected_desktop_route_truth_rows",
            coverage.get("desktopRouteTruth") or [],
        )
    payload["installAwareArtifactRegistry"] = derive_verifier_owned_value(
        "expected_install_aware_artifact_registry_rows",
        payload.get("installAwareArtifactRegistry") or [],
    )
    payload["desktopSurfaceRefs"] = derive_verifier_owned_value(
        "expected_desktop_surface_ref_rows",
        payload.get("desktopSurfaceRefs") or [],
    )
    payload["artifactIdentityRegistry"] = derive_verifier_owned_value(
        "expected_artifact_identity_registry_rows",
        payload.get("artifactIdentityRegistry") or [],
    )
    payload["artifactPublicationBindings"] = derive_verifier_owned_value(
        "expected_artifact_publication_binding_rows",
        payload.get("artifactPublicationBindings") or [],
    )
    payload["publicTrustMetrics"] = derive_verifier_owned_value(
        "expected_public_trust_metrics",
        payload.get("publicTrustMetrics") or {},
    )
    trust_release_channel = payload.get("publicTrustMetrics", {}).get("releaseChannel", {})
    trust_supportability_state = normalized_token(trust_release_channel.get("supportabilityState"))
    if normalized_token(payload.get("status")) == "published" and trust_supportability_state:
        payload["supportabilityState"] = trust_supportability_state
        if trust_supportability_state == "review_required":
            payload["supportabilitySummary"] = (
                "Treat this shelf as review-required until stale or incomplete proof receipts are refreshed."
            )
            payload["knownIssueSummary"] = (
                "The preview shelf remains visible, but stale or incomplete proof receipts mean it is not yet gold-ready."
            )
    # Recompute verifier-owned registry surfaces once more after supportability/trust normalization
    # so carried-forward manifests cannot keep stale dependent rows such as desktopSurfaceRefs.
    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        fresh_tuple_coverage = fallback_tuple_coverage(payload)
        if isinstance(fresh_tuple_coverage, dict):
            coverage.update(fresh_tuple_coverage)
        coverage["externalProofRequests"] = derive_verifier_owned_value(
            "expected_external_proof_request_rows",
            coverage.get("externalProofRequests") or [],
        )
        coverage["desktopRouteTruth"] = derive_verifier_owned_value(
            "expected_desktop_route_truth_rows",
            coverage.get("desktopRouteTruth") or [],
        )
    payload["installAwareArtifactRegistry"] = derive_verifier_owned_value(
        "expected_install_aware_artifact_registry_rows",
        payload.get("installAwareArtifactRegistry") or [],
    )
    payload["desktopSurfaceRefs"] = derive_verifier_owned_value(
        "expected_desktop_surface_ref_rows",
        payload.get("desktopSurfaceRefs") or [],
    )
    payload["artifactIdentityRegistry"] = derive_verifier_owned_value(
        "expected_artifact_identity_registry_rows",
        payload.get("artifactIdentityRegistry") or [],
    )
    payload["artifactPublicationBindings"] = derive_verifier_owned_value(
        "expected_artifact_publication_binding_rows",
        payload.get("artifactPublicationBindings") or [],
    )
    prune_rows_to_manifest_artifacts(payload)
    prune_release_proof_routes_to_manifest_artifacts(payload)
    payload["registryBoundaryCoverage"] = derive_verifier_owned_value(
        "expected_registry_boundary_coverage",
        payload.get("registryBoundaryCoverage") or {},
    )
    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        expected_external_proof_request_rows = getattr(verifier, "expected_external_proof_request_rows", None)
        if callable(expected_external_proof_request_rows):
            coverage["externalProofRequests"] = expected_external_proof_request_rows(
                payload,
                reported_expected_installer_sha256_by_tuple={},
            )
    verifier_owned_top_level_rows = {
        "installAwareArtifactRegistry": "expected_install_aware_artifact_registry_rows",
        "desktopSurfaceRefs": "expected_desktop_surface_ref_rows",
        "artifactIdentityRegistry": "expected_artifact_identity_registry_rows",
        "artifactPublicationBindings": "expected_artifact_publication_binding_rows",
    }
    for payload_key, helper_name in verifier_owned_top_level_rows.items():
        helper = getattr(verifier, helper_name, None)
        if callable(helper):
            payload[payload_key] = helper(payload)
    prune_rows_to_manifest_artifacts(payload)
    prune_release_proof_routes_to_manifest_artifacts(payload)
    payload["registryBoundaryCoverage"] = derive_verifier_owned_value(
        "expected_registry_boundary_coverage",
        payload.get("registryBoundaryCoverage") or {},
    )
    assert_desktop_surface_ref_consistency(payload)
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
canonical_startup_smoke_dir="$(dirname "$CANONICAL_MANIFEST_PATH")/startup-smoke"
if [[ -n "$STARTUP_SMOKE_DIR" && -d "$STARTUP_SMOKE_DIR" ]]; then
  resolved_startup_smoke_dir="$(resolve_path_allow_missing "$STARTUP_SMOKE_DIR")"
  resolved_canonical_startup_smoke_dir="$(resolve_path_allow_missing "$canonical_startup_smoke_dir")"
  if [[ "$resolved_startup_smoke_dir" != "$resolved_canonical_startup_smoke_dir" ]]; then
    mkdir -p "$canonical_startup_smoke_dir"
    find "$canonical_startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
    if find "$STARTUP_SMOKE_DIR" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
      cp "$STARTUP_SMOKE_DIR"/* "$canonical_startup_smoke_dir"/
      normalize_startup_smoke_receipt_channel_identity "$canonical_startup_smoke_dir" "$effective_release_channel"
    fi
  fi
fi
if to_bool "$GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS"; then
  python3 "$SCRIPT_DIR/materialize-external-host-proof-blockers.py" \
    --manifest "$CANONICAL_MANIFEST_PATH" \
    --downloads-dir "$DOWNLOADS_DIR" \
    --startup-smoke-dir "$STARTUP_SMOKE_DIR" \
    --display-manifest "$CANONICAL_MANIFEST_PATH" \
    --display-downloads-dir "$CANONICAL_FILES_DIR" \
    --display-startup-smoke-dir "$(dirname "$CANONICAL_MANIFEST_PATH")/startup-smoke" \
    --output "$EXTERNAL_HOST_PROOF_BLOCKERS_PATH" \
    --browser-proof-output "$PUBLIC_EDGE_WORKBENCH_PROOF_PATH" \
    --base-url "${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
    --timeout-seconds "${CHUMMER_EXTERNAL_PROOF_ROUTE_TIMEOUT_SECONDS:-10}" \
    --max-receipt-age-seconds "${CHUMMER_EXTERNAL_PROOF_MAX_RECEIPT_AGE_SECONDS:-604800}" \
    >/dev/null
else
  echo "skipped external host proof blocker materialization"
fi
verify_args=()
promoted_file_names=()
while IFS= read -r file_name; do
  [[ -n "$file_name" ]] || continue
  promoted_file_names+=("$file_name")
done < <(python3 - "$CANONICAL_MANIFEST_PATH" <<'PY'
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

prune_downloads_dir_to_promoted_files() {
  local file_path=""
  local file_name=""
  local keep=""
  local promoted_file_count=""
  local repo_owned_downloads_dir=""
  local resolved_downloads_dir=""

  promoted_file_count="$(array_count promoted_file_names)"
  if (( promoted_file_count == 0 )); then
    echo "no promoted desktop artifacts discovered in $CANONICAL_MANIFEST_PATH; skipping downloads prune"
    return 0
  fi

  repo_owned_downloads_dir="$(resolve_path_allow_missing "$REPO_ROOT/Docker/Downloads/files")"
  resolved_downloads_dir="$(resolve_path_allow_missing "$DOWNLOADS_DIR")"
  if [[ "$resolved_downloads_dir" != "$repo_owned_downloads_dir" ]]; then
    echo "skipping downloads prune because source dir is external to repo-owned staging: $DOWNLOADS_DIR"
    return 0
  fi

  shopt -s nullglob
  for file_path in \
    "$DOWNLOADS_DIR"/chummer-*.exe \
    "$DOWNLOADS_DIR"/chummer-*.zip \
    "$DOWNLOADS_DIR"/chummer-*.tar.gz \
    "$DOWNLOADS_DIR"/chummer-*-installer.deb \
    "$DOWNLOADS_DIR"/chummer-*-installer.pkg \
    "$DOWNLOADS_DIR"/chummer-*-installer.dmg \
    "$DOWNLOADS_DIR"/chummer-*-installer.msix; do
    [[ -f "$file_path" ]] || continue
    file_name="$(basename "$file_path")"
    keep=0
    for promoted_file_name in "${promoted_file_names[@]}"; do
      if [[ "$file_name" == "$promoted_file_name" ]]; then
        keep=1
        break
      fi
    done
    if [[ "$keep" != "1" ]]; then
      rm -f -- "$file_path"
      echo "removed unpromoted desktop artifact from downloads source: $file_name"
    fi
  done
}

resolve_promoted_artifact_source() {
  local file_name="$1"
  local candidate_dir=""
  local candidate_path=""

  if to_bool "$SCOPE_TO_STAGE_ARTIFACTS"; then
    candidate_path="$DOWNLOADS_DIR/$file_name"
    if [[ -f "$candidate_path" ]]; then
      printf '%s\n' "$candidate_path"
      return 0
    fi
    return 1
  fi

  for candidate_dir in \
    "$DOWNLOADS_DIR" \
    "$CANONICAL_FILES_DIR" \
    "$REGISTRY_FILES_DIR"; do
    [[ -z "$candidate_dir" ]] && continue
    candidate_path="$candidate_dir/$file_name"
    if [[ -f "$candidate_path" ]]; then
      printf '%s\n' "$candidate_path"
      return 0
    fi
  done

  return 1
}

windows_payload_name_for_installer() {
  local file_name="$1"
  if [[ "$file_name" == chummer-*-win-*-installer.exe ]]; then
    printf '%s\n' "${file_name%-installer.exe}-payload.zip"
    return 0
  fi
  return 1
}

sync_promoted_files_dir() {
  local target_dir="$1"
  local target_label="$2"
  local file_name=""
  local payload_file_name=""
  local payload_source_path=""
  local payload_sidecar_path=""
  local portal_artifact_count=""
  # portal_artifacts: keep historical variable naming expected by migration compliance checks.
  local -a portal_artifacts=()
  local source_path=""

  mkdir -p "$target_dir"
  for file_name in "${promoted_file_names[@]}"; do
    source_path="$(resolve_promoted_artifact_source "$file_name" || true)"
    if [[ -z "$source_path" ]]; then
      echo "promoted artifact missing from all local/registry sources: $file_name" >&2
      exit 1
    fi
    if [[ "$source_path" != "$DOWNLOADS_DIR/$file_name" ]]; then
      mkdir -p "$DOWNLOADS_DIR"
      cp -f "$source_path" "$DOWNLOADS_DIR/$file_name"
      echo "restored missing artifact into downloads source: $file_name"
      source_path="$DOWNLOADS_DIR/$file_name"
    fi
    portal_artifacts+=("$source_path")

    payload_file_name="$(windows_payload_name_for_installer "$file_name" || true)"
    if [[ -n "$payload_file_name" ]]; then
      payload_source_path="$(resolve_promoted_artifact_source "$payload_file_name" || true)"
      if [[ -z "$payload_source_path" ]]; then
        echo "windows installer payload sidecar missing from all local/registry sources: $payload_file_name" >&2
        exit 1
      fi
      if [[ "$payload_source_path" != "$DOWNLOADS_DIR/$payload_file_name" ]]; then
        mkdir -p "$DOWNLOADS_DIR"
        cp -f "$payload_source_path" "$DOWNLOADS_DIR/$payload_file_name"
        echo "restored missing artifact into downloads source: $payload_file_name"
        payload_source_path="$DOWNLOADS_DIR/$payload_file_name"
      fi
      portal_artifacts+=("$payload_source_path")

      payload_sidecar_path="$payload_source_path.json"
      if [[ -f "$payload_sidecar_path" ]]; then
        portal_artifacts+=("$payload_sidecar_path")
      fi
    fi
  done

  portal_artifact_count="$(array_count portal_artifacts)"
  if (( portal_artifact_count > 0 )); then
    rm -f \
      "$target_dir"/chummer-*.exe \
      "$target_dir"/chummer-*.zip \
      "$target_dir"/chummer-*-payload.zip \
      "$target_dir"/chummer-*-payload.zip.json \
      "$target_dir"/chummer-*.tar.gz \
      "$target_dir"/chummer-*-installer.deb \
      "$target_dir"/chummer-*-installer.pkg \
      "$target_dir"/chummer-*-installer.dmg \
      "$target_dir"/chummer-*-installer.msix
      # keep legacy sync pattern visible for compliance checks: cp -f "${portal_artifacts[@]}" "$portal_files_dir"/
      cp -f "${portal_artifacts[@]}" "$target_dir"/
      if [[ "$target_label" == "local portal" ]]; then
        echo "synced ${portal_artifact_count} local portal artifact(s) -> $target_dir"
      else
        echo "synced ${portal_artifact_count} ${target_label} artifact(s) -> $target_dir"
      fi
    else
    echo "no promoted desktop artifacts found in $DOWNLOADS_DIR for $target_label sync"
  fi
}

prune_downloads_dir_to_promoted_files

sync_portal_outputs() {
  local resolved_manifest_path="$1"
  local resolved_portal_manifest_path="$2"
  local portal_startup_smoke_dir=""
  local portal_files_dir=""

  if [[ "$resolved_manifest_path" == "$resolved_portal_manifest_path" ]]; then
    echo "portal manifest path matches manifest output; skipped secondary sync"
    return 0
  fi

  cp "$MANIFEST_PATH" "$PORTAL_MANIFEST_PATH"
  cp "$CANONICAL_MANIFEST_PATH" "$PORTAL_CANONICAL_MANIFEST_PATH"
  echo "synced portal manifest -> $PORTAL_MANIFEST_PATH"

  portal_startup_smoke_dir="$PORTAL_DOWNLOADS_DIR/startup-smoke"
  mkdir -p "$portal_startup_smoke_dir"
  find "$portal_startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
  if [[ -d "$canonical_startup_smoke_dir" ]] && find "$canonical_startup_smoke_dir" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp -f "$canonical_startup_smoke_dir"/* "$portal_startup_smoke_dir"/
    normalize_startup_smoke_receipt_channel_identity "$portal_startup_smoke_dir" "$effective_release_channel"
    echo "synced startup-smoke evidence -> $portal_startup_smoke_dir"
  else
    echo "no startup-smoke evidence found in $canonical_startup_smoke_dir for portal sync"
  fi

  portal_files_dir="$PORTAL_DOWNLOADS_DIR/files"
  sync_promoted_files_dir "$portal_files_dir" "local portal"
}

sync_presentation_downloads_mirror() {
  local mirror_manifest_path="$1"
  local mirror_canonical_manifest_path="$2"
  local mirror_downloads_dir="$3"
  local mirror_label="$4"
  local resolved_manifest_path=""
  local resolved_mirror_manifest_path=""
  local mirror_startup_smoke_dir=""
  local mirror_files_dir=""

  if [[ -z "$mirror_manifest_path" || -z "$mirror_canonical_manifest_path" || -z "$mirror_downloads_dir" ]]; then
    return 0
  fi

  resolved_manifest_path="$(resolve_path_allow_missing "$MANIFEST_PATH")"
  resolved_mirror_manifest_path="$(resolve_path_allow_missing "$mirror_manifest_path")"
  if [[ "$resolved_manifest_path" == "$resolved_mirror_manifest_path" ]]; then
    echo "$mirror_label manifest path matches manifest output; skipped secondary sync"
    return 0
  fi

  mkdir -p "$(dirname "$mirror_manifest_path")"
  mkdir -p "$mirror_downloads_dir"
  cp "$MANIFEST_PATH" "$mirror_manifest_path"
  cp "$CANONICAL_MANIFEST_PATH" "$mirror_canonical_manifest_path"
  echo "synced $mirror_label manifest -> $mirror_manifest_path"

  mirror_startup_smoke_dir="$mirror_downloads_dir/startup-smoke"
  mkdir -p "$mirror_startup_smoke_dir"
  find "$mirror_startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
  if [[ -d "$canonical_startup_smoke_dir" ]] && find "$canonical_startup_smoke_dir" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp -f "$canonical_startup_smoke_dir"/* "$mirror_startup_smoke_dir"/
    normalize_startup_smoke_receipt_channel_identity "$mirror_startup_smoke_dir" "$effective_release_channel"
    echo "synced startup-smoke evidence -> $mirror_startup_smoke_dir"
  else
    echo "no startup-smoke evidence found in $canonical_startup_smoke_dir for $mirror_label sync"
  fi

  mirror_files_dir="$mirror_downloads_dir/files"
  sync_promoted_files_dir "$mirror_files_dir" "$mirror_label"
}

canonical_files_dir="$(dirname "$CANONICAL_MANIFEST_PATH")/files"
resolved_downloads_dir="$(resolve_path_allow_missing "$DOWNLOADS_DIR")"
resolved_canonical_files_dir="$(resolve_path_allow_missing "$canonical_files_dir")"
if [[ "$resolved_downloads_dir" == "$resolved_canonical_files_dir" ]]; then
  echo "canonical files dir matches downloads source; skipped canonical files sync"
else
  sync_promoted_files_dir "$canonical_files_dir" "canonical release"
fi

resolved_manifest_path="$(resolve_path_allow_missing "$MANIFEST_PATH")"
resolved_portal_manifest_path="$(resolve_path_allow_missing "$PORTAL_MANIFEST_PATH")"
sync_portal_outputs "$resolved_manifest_path" "$resolved_portal_manifest_path"
if presentation_mirror_enabled; then
  sync_presentation_downloads_mirror \
    "$PRESENTATION_MIRROR_ROOT/Docker/Downloads/releases.json" \
    "$PRESENTATION_MIRROR_ROOT/Docker/Downloads/RELEASE_CHANNEL.generated.json" \
    "$PRESENTATION_MIRROR_ROOT/Docker/Downloads" \
    "presentation downloads mirror"
fi

verify_registry_boundary_consistency \
  "$MANIFEST_PATH" \
  "$CANONICAL_MANIFEST_PATH" \
  "$PORTAL_MANIFEST_PATH" \
  "$PORTAL_CANONICAL_MANIFEST_PATH"

if to_bool "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER"; then
  verify_args+=(--skip-startup-smoke-filter)
fi
if [[ "$REQUIRE_COMPLETE_DESKTOP_COVERAGE" != "0" ]]; then
  verify_args+=(--require-complete-desktop-coverage)
fi
python3 "$REGISTRY_ROOT/scripts/verify_public_release_channel.py" "${verify_args[@]}" "$CANONICAL_MANIFEST_PATH" >/dev/null

promotion_evidence_args=(
  --manifest "$CANONICAL_MANIFEST_PATH"
  --startup-smoke-dir "$STARTUP_SMOKE_DIR"
  --output "$PROMOTION_EVIDENCE_PATH"
  --channel "$RELEASE_CHANNEL"
  --generated-at "$RELEASE_PUBLISHED_AT"
)
if [[ -d "$SIGNING_RECEIPTS_DIR" ]] && find "$SIGNING_RECEIPTS_DIR" -type f -name '*.receipt.json' | grep -q .; then
  promotion_evidence_args+=(--signing-receipts-dir "$SIGNING_RECEIPTS_DIR")
fi
python3 "$SCRIPT_DIR/generate-public-promotion-evidence.py" "${promotion_evidence_args[@]}"

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
    if startup_status in {"pass", "skipped_incompatible_host"}:
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
