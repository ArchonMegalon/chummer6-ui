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
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"
PREVIEW_INSTALL_ACCESS_CLASS="${CHUMMER_PREVIEW_INSTALL_ACCESS_CLASS:-}"
EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}"

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

    if str(artifact.get("installAccessClass") or "").strip().lower() == access_class:
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
  python3 - "$source_dir" "$output_dir" "$release_channel" "$release_version" <<'PY'
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

source_dir = Path(sys.argv[1])
output_dir = Path(sys.argv[2])
release_channel = str(sys.argv[3]).strip()
release_version = str(sys.argv[4]).strip()

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
    receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

RELEASE_PROOF_PATH="$(resolve_release_proof_path "$RELEASE_PROOF_PATH")"
SANITIZED_RELEASE_PROOF_PATH=""
SANITIZED_UI_LOCALIZATION_RELEASE_GATE_PATH=""
SANITIZED_STARTUP_SMOKE_DIR=""
SANITIZED_SOURCE_MANIFEST_PATH=""
cleanup_generate_release_manifest() {
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
  SANITIZED_STARTUP_SMOKE_DIR="$(mktemp -d)"
  sanitize_startup_smoke_dir \
    "$STARTUP_SMOKE_DIR" \
    "$SANITIZED_STARTUP_SMOKE_DIR" \
    "$RELEASE_CHANNEL" \
    "$RELEASE_VERSION"
  STARTUP_SMOKE_DIR="$SANITIZED_STARTUP_SMOKE_DIR"
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

if [[ -n "$UI_LOCALIZATION_RELEASE_GATE_PATH" && -f "$UI_LOCALIZATION_RELEASE_GATE_PATH" ]]; then
  materialize_args+=(--ui-localization-release-gate "$UI_LOCALIZATION_RELEASE_GATE_PATH")
fi

materializer_help="$(python3 "$REGISTRY_ROOT/scripts/materialize_public_release_channel.py" --help 2>&1 || true)"
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

resolved_manifest_path="$(resolve_path_allow_missing "$MANIFEST_PATH")"
resolved_portal_manifest_path="$(resolve_path_allow_missing "$PORTAL_MANIFEST_PATH")"
if [[ "$resolved_manifest_path" == "$resolved_portal_manifest_path" ]]; then
  echo "portal manifest path matches manifest output; skipped secondary sync"
else
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
    # Force overwrite so repeated manifest publication stays idempotent even when
    # the portal mirror already contains a prior copy of the promoted artifact set.
    cp -f "${portal_artifacts[@]}" "$portal_files_dir"/
    echo "synced ${#portal_artifacts[@]} local portal artifact(s) -> $portal_files_dir"
  else
    echo "no local desktop artifacts found in $DOWNLOADS_DIR for portal file sync"
  fi
fi
