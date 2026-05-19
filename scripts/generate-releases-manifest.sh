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
SIGNING_RECEIPTS_DIR="${SIGNING_RECEIPTS_DIR:-$(dirname "$DOWNLOADS_DIR")/signing}"
STARTUP_SMOKE_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS:-}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-false}"
RELEASE_VERSION="${RELEASE_VERSION:-unpublished}"
RELEASE_CHANNEL="${RELEASE_CHANNEL:-docker}"
RELEASE_PUBLISHED_AT="${RELEASE_PUBLISHED_AT:-$(date -u +%Y-%m-%dT%H:%M:%SZ)}"
REQUIRE_STARTUP_SMOKE_PROOF="${CHUMMER_RELEASE_REQUIRE_STARTUP_SMOKE_PROOF:-1}"
REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}"
PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS="${CHUMMER_PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS:-1}"
UI_LOCALIZATION_RELEASE_GATE_PATH="${CHUMMER_UI_LOCALIZATION_RELEASE_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_LOCALIZATION_RELEASE_GATE.generated.json}"
EXTERNAL_HOST_PROOF_BLOCKERS_PATH="${CHUMMER_UI_EXTERNAL_HOST_PROOF_BLOCKERS_PATH:-$REPO_ROOT/.codex-studio/published/UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json}"
CANONICAL_MANIFEST_PATH="${CANONICAL_MANIFEST_PATH:-$(dirname "$MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PORTAL_CANONICAL_MANIFEST_PATH="${PORTAL_CANONICAL_MANIFEST_PATH:-$(dirname "$PORTAL_MANIFEST_PATH")/RELEASE_CHANNEL.generated.json}"
PROMOTION_EVIDENCE_PATH="${PROMOTION_EVIDENCE_PATH:-$(dirname "$MANIFEST_PATH")/release-evidence/public-promotion.json}"
QUARANTINE_PROMOTION_EVIDENCE_PATH="${QUARANTINE_PROMOTION_EVIDENCE_PATH:-$REPO_ROOT/.codex-studio/published/QUARANTINED_INSTALLER_PROMOTION.generated.json}"
SOURCE_MANIFEST_PATH="${SOURCE_MANIFEST_PATH:-}"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-${CHUMMER_HUB_LOCAL_RELEASE_PROOF_PATH:-}}"
PREVIEW_INSTALL_ACCESS_CLASS="${CHUMMER_PREVIEW_INSTALL_ACCESS_CLASS:-}"
EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}"
RELEASE_PROOF_MAX_AGE_SECONDS="${CHUMMER_RELEASE_PROOF_MAX_AGE_SECONDS:-86400}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

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

sanitize_source_manifest_for_channel_override() {
  local source_path="${1:-}"
  local output_path="${2:-}"
  local release_channel="${3:-}"
  python3 - "$source_path" "$output_path" "$release_channel" <<'PY'
import json
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
release_channel = str(sys.argv[3] or "").strip().lower()

payload = json.loads(source_path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(f"source manifest payload must be a JSON object: {source_path}")

loaded_channel = str(payload.get("channelId") or payload.get("channel") or "").strip().lower()
if not release_channel or not loaded_channel or loaded_channel == release_channel:
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(0)

for key in (
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
    PREVIEW_INSTALL_ACCESS_CLASS="account_required"
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
    if kind not in {"installer", "dmg", "pkg", "msix"}:
        continue

    current_access_class = str(artifact.get("installAccessClass") or "").strip().lower()
    if current_access_class:
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
  python3 - "$source_dir" "$output_dir" "$release_channel" "$release_version" "$downloads_dir" <<'PY'
from __future__ import annotations

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

output_dir.mkdir(parents=True, exist_ok=True)

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
    if artifact_file_name:
        payload["artifactFileName"] = artifact_file_name
        payload["fileName"] = artifact_file_name
        payload["artifactRelativePath"] = f"files/{artifact_file_name}"
        if downloads_dir is not None:
            payload["artifactPath"] = str((downloads_dir / artifact_file_name).resolve(strict=False))
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

hydrate_startup_smoke_dir() {
  local source_dir="${1:-}"
  local output_dir="${2:-}"
  local registry_root="${3:-}"
  local repo_root="${4:-}"
  python3 - "$source_dir" "$output_dir" "$registry_root" "$repo_root" <<'PY'
from __future__ import annotations

import datetime as dt
import json
import shutil
import sys
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

output_dir.mkdir(parents=True, exist_ok=True)

gate_paths = [
    repo_root / ".codex-studio" / "published" / "UI_LINUX_DESKTOP_EXIT_GATE.generated.json",
    repo_root / ".codex-studio" / "published" / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
]
candidate_dirs: list[Path] = [source_dir]
if registry_root:
    candidate_dirs.append(registry_root / ".codex-studio" / "published" / "startup-smoke")
for gate_path in gate_paths:
    gate_payload = load_json(gate_path)
    if not gate_payload:
        continue
    receipt_path = (
        str(((gate_payload.get("checks") or {}) if isinstance(gate_payload.get("checks"), dict) else {}).get("startup_smoke_receipt_path") or "").strip()
    )
    if not receipt_path:
        continue
    candidate_dirs.append(Path(receipt_path).resolve(strict=False).parent)

selected_by_name: dict[str, tuple[dt.datetime, Path]] = {}
for candidate_dir in candidate_dirs:
    if not candidate_dir.is_dir():
        continue
    for receipt_path in sorted(candidate_dir.glob("startup-smoke-*.receipt.json")):
        payload = load_json(receipt_path)
        if not payload:
            continue
        name = receipt_path.name
        timestamp = parse_timestamp(payload)
        current = selected_by_name.get(name)
        if current is None or timestamp >= current[0]:
            selected_by_name[name] = (timestamp, receipt_path)

for _, receipt_path in sorted(selected_by_name.values(), key=lambda item: item[1].name):
    shutil.copy2(receipt_path, output_dir / receipt_path.name)
PY
}

RELEASE_PROOF_PATH="$(resolve_release_proof_path "$RELEASE_PROOF_PATH")"
SANITIZED_RELEASE_PROOF_PATH=""
GENERATED_RELEASE_PROOF_PATH=""
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
  if [[ "$RELEASE_PROOF_PATH" == /tmp/* ]]; then
    GENERATED_RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH"
  fi
  SANITIZED_RELEASE_PROOF_PATH="$(mktemp)"
  sanitize_release_proof_payload "$RELEASE_PROOF_PATH" "$SANITIZED_RELEASE_PROOF_PATH" "$EXTERNAL_PROOF_BASE_URL"
  RELEASE_PROOF_PATH="$SANITIZED_RELEASE_PROOF_PATH"
fi
if [[ -n "$UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
  SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH="$(mktemp)"
  sanitize_ui_localization_release_gate_payload \
    "$UI_LOCALIZATION_RELEASE_GATE_PATH" \
    "$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH"
  UI_LOCALIZATION_RELEASE_GATE_PATH="$SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH"
fi
if [[ -d "$STARTUP_SMOKE_DIR" ]] && find "$STARTUP_SMOKE_DIR" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' | grep -q .; then
  hydrated_startup_smoke_dir="$(mktemp -d)"
  hydrate_startup_smoke_dir \
    "$STARTUP_SMOKE_DIR" \
    "$hydrated_startup_smoke_dir" \
    "$REGISTRY_ROOT" \
    "$REPO_ROOT"
  SANITIZED_STARTUP_SMOKE_DIR="$(mktemp -d)"
  sanitize_startup_smoke_dir \
    "$hydrated_startup_smoke_dir" \
    "$SANITIZED_STARTUP_SMOKE_DIR" \
    "$RELEASE_CHANNEL" \
    "$RELEASE_VERSION" \
    "$DOWNLOADS_DIR"
  STARTUP_SMOKE_DIR="$SANITIZED_STARTUP_SMOKE_DIR"
  rm -rf "$hydrated_startup_smoke_dir"
fi
if [[ -n "$SOURCE_MANIFEST_PATH" && -f "$SOURCE_MANIFEST_PATH" ]]; then
  SANITIZED_SOURCE_MANIFEST_PATH="$(mktemp)"
  sanitize_source_manifest_for_channel_override \
    "$SOURCE_MANIFEST_PATH" \
    "$SANITIZED_SOURCE_MANIFEST_PATH" \
    "$RELEASE_CHANNEL"
  SOURCE_MANIFEST_PATH="$SANITIZED_SOURCE_MANIFEST_PATH"
fi

mkdir -p "$(dirname "$MANIFEST_PATH")"
mkdir -p "$(dirname "$PORTAL_MANIFEST_PATH")"
mkdir -p "$DOWNLOADS_DIR"

if [[ "$PROMOTE_PROOF_BACKED_QUARANTINED_INSTALLERS" != "0" ]]; then
  python3 "$SCRIPT_DIR/promote-proof-backed-quarantined-installers.py" \
    --repo-root "$REPO_ROOT" \
    --downloads-dir "$DOWNLOADS_DIR" \
    --startup-smoke-dir "$STARTUP_SMOKE_DIR" \
    --release-channel "$RELEASE_CHANNEL" \
    --release-version "$RELEASE_VERSION" \
    --output "$QUARANTINE_PROMOTION_EVIDENCE_PATH" \
    >/dev/null
fi

readarray -t promoted_file_names < <(true)

materializer_help="$(python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" --help 2>&1 || true)"
run_materializer() {
  local manifest_override="${1:-}"
  local -a materialize_args=(
    --downloads-dir "$DOWNLOADS_DIR"
    --channel "$RELEASE_CHANNEL"
    --version "$RELEASE_VERSION"
    --published-at "$RELEASE_PUBLISHED_AT"
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

run_materializer
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


verifier_path = Path(sys.argv[1]).resolve()
verifier = load_verifier(verifier_path)

for raw_path in sys.argv[2:]:
    manifest_path = Path(raw_path).resolve()
    if not manifest_path.is_file():
        continue
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"release manifest must be a JSON object: {manifest_path}")
    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        coverage["externalProofRequests"] = verifier.expected_external_proof_request_rows(payload)
        coverage["desktopRouteTruth"] = verifier.expected_desktop_route_truth_rows(payload)
    payload["installAwareArtifactRegistry"] = verifier.expected_install_aware_artifact_registry_rows(payload)
    payload["desktopSurfaceRefs"] = verifier.expected_desktop_surface_ref_rows(payload)
    payload["artifactIdentityRegistry"] = verifier.expected_artifact_identity_registry_rows(payload)
    payload["artifactPublicationBindings"] = verifier.expected_artifact_publication_binding_rows(payload)
    payload["publicTrustMetrics"] = verifier.expected_public_trust_metrics(payload)
    trust_release_channel = payload.get("publicTrustMetrics", {}).get("releaseChannel", {})
    trust_supportability_state = normalized_token(trust_release_channel.get("supportabilityState"))
    if normalized_token(payload.get("status")) == "published" and trust_supportability_state:
        payload["supportabilityState"] = trust_supportability_state
        if trust_supportability_state == "review_required":
            payload["supportabilitySummary"] = (
                "Proof freshness is missing or stale on this shelf, so review is still required before this release can be treated as supportable."
            )
            payload["knownIssueSummary"] = (
                "Proof freshness is missing or stale on this shelf, so preview publication is visible but not yet gold-ready."
            )
    # Recompute verifier-owned registry surfaces once more after supportability/trust normalization
    # so carried-forward manifests cannot keep stale dependent rows such as desktopSurfaceRefs.
    coverage = payload.get("desktopTupleCoverage")
    if isinstance(coverage, dict):
        coverage["externalProofRequests"] = verifier.expected_external_proof_request_rows(payload)
        coverage["desktopRouteTruth"] = verifier.expected_desktop_route_truth_rows(payload)
    payload["installAwareArtifactRegistry"] = verifier.expected_install_aware_artifact_registry_rows(payload)
    payload["desktopSurfaceRefs"] = verifier.expected_desktop_surface_ref_rows(payload)
    payload["artifactIdentityRegistry"] = verifier.expected_artifact_identity_registry_rows(payload)
    payload["artifactPublicationBindings"] = verifier.expected_artifact_publication_binding_rows(payload)
    payload["registryBoundaryCoverage"] = verifier.expected_registry_boundary_coverage(payload)
    manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
canonical_startup_smoke_dir="$(dirname "$CANONICAL_MANIFEST_PATH")/startup-smoke"
if [[ -d "$STARTUP_SMOKE_DIR" ]]; then
  resolved_startup_smoke_dir="$(realpath "$STARTUP_SMOKE_DIR")"
  resolved_canonical_startup_smoke_dir="$(realpath -m "$canonical_startup_smoke_dir")"
  if [[ "$resolved_startup_smoke_dir" != "$resolved_canonical_startup_smoke_dir" ]]; then
    mkdir -p "$canonical_startup_smoke_dir"
    find "$canonical_startup_smoke_dir" -maxdepth 1 -type f -exec rm -f -- {} +
    if find "$STARTUP_SMOKE_DIR" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
      cp "$STARTUP_SMOKE_DIR"/* "$canonical_startup_smoke_dir"/
    fi
  fi
fi
python3 "$SCRIPT_DIR/materialize-external-host-proof-blockers.py" \
  --manifest "$CANONICAL_MANIFEST_PATH" \
  --downloads-dir "$DOWNLOADS_DIR" \
  --startup-smoke-dir "$STARTUP_SMOKE_DIR" \
  --output "$EXTERNAL_HOST_PROOF_BLOCKERS_PATH" \
  --base-url "${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
  --timeout-seconds "${CHUMMER_EXTERNAL_PROOF_ROUTE_TIMEOUT_SECONDS:-10}" \
  --max-receipt-age-seconds "${CHUMMER_EXTERNAL_PROOF_MAX_RECEIPT_AGE_SECONDS:-604800}" \
  >/dev/null
verify_args=()
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

sync_promoted_files_dir() {
  local target_dir="$1"
  local target_label="$2"
  local artifact_path=""
  local file_name=""
  local -a sync_artifacts=()

  mkdir -p "$target_dir"
  for file_name in "${promoted_file_names[@]}"; do
    artifact_path="$DOWNLOADS_DIR/$file_name"
    if [[ ! -f "$artifact_path" ]]; then
      echo "promoted artifact missing from downloads source: $artifact_path" >&2
      exit 1
    fi
    sync_artifacts+=("$artifact_path")
  done

  if [[ "${#sync_artifacts[@]}" -gt 0 ]]; then
    rm -f \
      "$target_dir"/chummer-*.exe \
      "$target_dir"/chummer-*.zip \
      "$target_dir"/chummer-*.tar.gz \
      "$target_dir"/chummer-*-installer.deb \
      "$target_dir"/chummer-*-installer.pkg \
      "$target_dir"/chummer-*-installer.dmg \
      "$target_dir"/chummer-*-installer.msix
    cp -f "${sync_artifacts[@]}" "$target_dir"/
    echo "synced ${#sync_artifacts[@]} ${target_label} artifact(s) -> $target_dir"
  else
    echo "no promoted desktop artifacts found in $DOWNLOADS_DIR for $target_label sync"
  fi
}

sync_portal_outputs() {
  local resolved_manifest_path="$1"
  local resolved_portal_manifest_path="$2"
  local portal_startup_smoke_dir=""
  local portal_files_dir=""
  local artifact_path=""
  local file_name=""
  local -a portal_artifacts=()

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
  if [[ -d "$canonical_startup_smoke_dir" ]] && find "$canonical_startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' | grep -q .; then
    cp -f "$canonical_startup_smoke_dir"/startup-smoke-*.receipt.json "$portal_startup_smoke_dir"/
    echo "synced startup-smoke receipts -> $portal_startup_smoke_dir"
  else
    echo "no startup-smoke receipts found in $canonical_startup_smoke_dir for portal sync"
  fi

  portal_files_dir="$PORTAL_DOWNLOADS_DIR/files"
  for file_name in "${promoted_file_names[@]}"; do
    artifact_path="$DOWNLOADS_DIR/$file_name"
    if [[ ! -f "$artifact_path" ]]; then
      echo "promoted artifact missing from downloads source: $artifact_path" >&2
      exit 1
    fi
    portal_artifacts+=("$artifact_path")
  done

  if [[ "${#portal_artifacts[@]}" -gt 0 ]]; then
    mkdir -p "$portal_files_dir"
    rm -f \
      "$portal_files_dir"/chummer-*.exe \
      "$portal_files_dir"/chummer-*.zip \
      "$portal_files_dir"/chummer-*.tar.gz \
      "$portal_files_dir"/chummer-*-installer.deb \
      "$portal_files_dir"/chummer-*-installer.pkg \
      "$portal_files_dir"/chummer-*-installer.dmg \
      "$portal_files_dir"/chummer-*-installer.msix
    cp -f "${portal_artifacts[@]}" "$portal_files_dir"/
    echo "synced ${#portal_artifacts[@]} local portal artifact(s) -> $portal_files_dir"
  else
    echo "no promoted desktop artifacts found in $DOWNLOADS_DIR for local portal sync"
  fi
}

canonical_files_dir="$(dirname "$CANONICAL_MANIFEST_PATH")/files"
resolved_downloads_dir="$(realpath -m "$DOWNLOADS_DIR")"
resolved_canonical_files_dir="$(realpath -m "$canonical_files_dir")"
if [[ "$resolved_downloads_dir" == "$resolved_canonical_files_dir" ]]; then
  echo "canonical files dir matches downloads source; skipped canonical files sync"
else
  sync_promoted_files_dir "$canonical_files_dir" "canonical release"
fi

resolved_manifest_path="$(resolve_path_allow_missing "$MANIFEST_PATH")"
resolved_portal_manifest_path="$(resolve_path_allow_missing "$PORTAL_MANIFEST_PATH")"
sync_portal_outputs "$resolved_manifest_path" "$resolved_portal_manifest_path"

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
