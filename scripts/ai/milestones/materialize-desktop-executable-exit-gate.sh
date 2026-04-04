#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_EXECUTABLE_EXIT_GATE.generated.json"
release_gate_lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_EXECUTABLE_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
linux_avalonia_gate_path="$repo_root/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
linux_blazor_gate_path="$repo_root/.codex-studio/published/UI_LINUX_BLAZOR_DESKTOP_EXIT_GATE.generated.json"
windows_gate_path_default="$repo_root/.codex-studio/published/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"
linux_gate_materializer_path="$repo_root/scripts/materialize-linux-desktop-exit-gate.sh"
windows_gate_materializer_path="$repo_root/scripts/materialize-windows-desktop-exit-gate.sh"
macos_gate_materializer_path="$repo_root/scripts/materialize-macos-desktop-exit-gate.sh"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
visual_familiarity_gate_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
workflow_execution_gate_path="$repo_root/.codex-studio/published/DESKTOP_WORKFLOW_EXECUTION_GATE.generated.json"
visual_familiarity_materializer_path="$repo_root/scripts/ai/milestones/materialize-desktop-visual-familiarity-exit-gate.sh"
workflow_execution_materializer_path="$repo_root/scripts/ai/milestones/materialize-desktop-workflow-execution-gate.sh"
skip_dependency_materialize="${CHUMMER_DESKTOP_EXECUTABLE_SKIP_DEPENDENCY_MATERIALIZE:-0}"
skip_release_gate_lock_wait="${CHUMMER_DESKTOP_EXECUTABLE_SKIP_RELEASE_GATE_LOCK_WAIT:-0}"
release_gate_lock_wait_seconds="${CHUMMER_DESKTOP_EXECUTABLE_RELEASE_GATE_LOCK_WAIT_SECONDS:-300}"
release_gate_lock_poll_seconds="${CHUMMER_DESKTOP_EXECUTABLE_RELEASE_GATE_LOCK_POLL_SECONDS:-2}"
if ! [[ "$release_gate_lock_wait_seconds" =~ ^[0-9]+$ ]]; then
  release_gate_lock_wait_seconds=300
fi
if ! [[ "$release_gate_lock_poll_seconds" =~ ^[0-9]+$ ]] || [[ "$release_gate_lock_poll_seconds" -lt 1 ]]; then
  release_gate_lock_poll_seconds=2
fi

mkdir -p "$(dirname "$receipt_path")"
if [[ "$skip_release_gate_lock_wait" != "1" ]]; then
  release_gate_lock_wait_iterations=$((release_gate_lock_wait_seconds / release_gate_lock_poll_seconds))
  if [[ "$release_gate_lock_wait_iterations" -lt 1 ]]; then
    release_gate_lock_wait_iterations=1
  fi
  for _ in $(seq 1 "$release_gate_lock_wait_iterations"); do
    if [[ ! -d "$release_gate_lock_dir" ]]; then
      break
    fi
    sleep "$release_gate_lock_poll_seconds"
  done
fi

if [[ "$skip_dependency_materialize" != "1" ]]; then
  if [[ -f "$visual_familiarity_materializer_path" ]]; then
    CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT="$skip_release_gate_lock_wait" \
      CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_WAIT_SECONDS="$release_gate_lock_wait_seconds" \
      CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_POLL_SECONDS="$release_gate_lock_poll_seconds" \
      bash "$visual_familiarity_materializer_path" >/dev/null
  fi
  if [[ -f "$workflow_execution_materializer_path" ]]; then
    bash "$workflow_execution_materializer_path" >/dev/null
  fi
  if [[ -f "$linux_gate_materializer_path" || -f "$windows_gate_materializer_path" || -f "$macos_gate_materializer_path" ]]; then
    while IFS=: read -r head rid platform; do
      [[ -n "$head" && -n "$rid" && -n "$platform" ]] || continue
      if [[ "$platform" == "linux" && -f "$linux_gate_materializer_path" ]]; then
        if [[ "$head" == "avalonia" && "$rid" == "linux-x64" ]]; then
          linux_gate_tuple_path="$linux_avalonia_gate_path"
        elif [[ "$head" == "blazor-desktop" && "$rid" == "linux-x64" ]]; then
          linux_gate_tuple_path="$linux_blazor_gate_path"
        else
          head_token="${head^^}"
          head_token="${head_token//-/_}"
          rid_token="${rid^^}"
          rid_token="${rid_token//-/_}"
          linux_gate_tuple_path="$repo_root/.codex-studio/published/UI_LINUX_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"
        fi
        if ! CHUMMER_LINUX_DESKTOP_EXIT_GATE_RELEASE_CHANNEL_PATH="$release_channel_path" \
          CHUMMER_LINUX_DESKTOP_EXIT_GATE_APP_KEY="$head" \
          CHUMMER_LINUX_DESKTOP_EXIT_GATE_RID="$rid" \
          CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH="$linux_gate_tuple_path" \
          bash "$linux_gate_materializer_path" >/dev/null 2>&1; then
          :
        fi
      fi
      if [[ "$platform" == "windows" && -f "$windows_gate_materializer_path" ]]; then
        if [[ "$head" == "avalonia" && "$rid" == "win-x64" ]]; then
          windows_gate_tuple_path="$windows_gate_path_default"
        else
          head_token="${head^^}"
          head_token="${head_token//-/_}"
          rid_token="${rid^^}"
          rid_token="${rid_token//-/_}"
          windows_gate_tuple_path="$repo_root/.codex-studio/published/UI_WINDOWS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"
        fi
        if ! CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$release_channel_path" \
          CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_APP_KEY="$head" \
          CHUMMER_WINDOWS_DESKTOP_EXIT_GATE_RID="$rid" \
          CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$windows_gate_tuple_path" \
          bash "$windows_gate_materializer_path" >/dev/null 2>&1; then
          :
        fi
      fi
      if [[ "$platform" == "macos" && -f "$macos_gate_materializer_path" ]]; then
        head_token="${head^^}"
        head_token="${head_token//-/_}"
        rid_token="${rid^^}"
        rid_token="${rid_token//-/_}"
        macos_gate_tuple_path="$repo_root/.codex-studio/published/UI_MACOS_${head_token}_${rid_token}_DESKTOP_EXIT_GATE.generated.json"
        if ! CHUMMER_MACOS_RELEASE_CHANNEL_PATH="$release_channel_path" \
          CHUMMER_MACOS_DESKTOP_EXIT_GATE_APP_KEY="$head" \
          CHUMMER_MACOS_DESKTOP_EXIT_GATE_RID="$rid" \
          CHUMMER_UI_MACOS_DESKTOP_EXIT_GATE_PATH="$macos_gate_tuple_path" \
          bash "$macos_gate_materializer_path" >/dev/null 2>&1; then
          :
        fi
      fi
    done < <(python3 - <<'PY' "$release_channel_path"
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
if not release_channel_path.is_file():
    raise SystemExit(0)

def normalize(value: object) -> str:
    return str(value or "").strip().lower()

try:
    payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

tuple_coverage = payload.get("desktopTupleCoverage")
if not isinstance(tuple_coverage, dict):
    raise SystemExit(0)

required = tuple_coverage.get("requiredDesktopPlatformHeadRidTuples")
if not isinstance(required, list):
    raise SystemExit(0)

tuples: list[tuple[str, str, str]] = []
seen: set[tuple[str, str, str]] = set()
for token in required:
    raw = str(token or "").strip()
    if not raw:
        continue
    parts = raw.split(":")
    if len(parts) != 3:
        continue
    head, rid, platform = (normalize(parts[0]), normalize(parts[1]), normalize(parts[2]))
    if not head or not rid or platform not in {"windows", "macos", "linux"}:
        continue
    key = (head, rid, platform)
    if key in seen:
        continue
    seen.add(key)
    tuples.append(key)

for head, rid, platform in sorted(tuples):
    print(f"{head}:{rid}:{platform}")
PY
)
  fi
fi

python3 - <<'PY' "$receipt_path" "$release_channel_path" "$linux_avalonia_gate_path" "$linux_blazor_gate_path" "$windows_gate_path_default" "$flagship_gate_path" "$visual_familiarity_gate_path" "$workflow_execution_gate_path" "$repo_root" "$hub_registry_root"
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone


DESKTOP_PROOF_MAX_AGE_SECONDS = int(os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_AGE_SECONDS", "86400"))
DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "86400"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_EXECUTABLE_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS")
    or str(DESKTOP_PROOF_MAX_AGE_SECONDS)
)
from pathlib import Path
from typing import Any, Dict, List


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def pick_status(payload: Dict[str, Any]) -> str:
    return str(payload.get("status") or "").strip().lower()


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def normalize_contract_name(payload: Dict[str, Any]) -> str:
    return str(payload.get("contract_name") or payload.get("contractName") or "").strip()


def dedupe_preserve_order(values: List[str]) -> List[str]:
    seen: set[str] = set()
    deduped: List[str] = []
    for value in values:
        if value in seen:
            continue
        seen.add(value)
        deduped.append(value)
    return deduped


def build_platform_head_rid_tuple(head: Any, rid: Any, platform: Any) -> str:
    head_token = normalize_token(head)
    rid_token = normalize_token(rid)
    platform_token = normalize_token(platform)
    if not head_token or not rid_token or not platform_token:
        return ""
    return f"{head_token}:{rid_token}:{platform_token}"


def normalized_token_list(values: Any) -> List[str]:
    if not isinstance(values, list):
        return []
    return dedupe_preserve_order(
        [normalize_token(value) for value in values if normalize_token(value)]
    )


def normalize_required_token_list(
    values: Any,
    field_label: str,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> List[str]:
    if values is None:
        return []
    if not isinstance(values, list):
        reasons.append(f"{field_label} must be a list when present.")
        return []
    normalized: List[str] = []
    whitespace_padded_indexes: List[int] = []
    for index, item in enumerate(values):
        if not isinstance(item, str):
            reasons.append(f"{field_label} contains a non-string item at index {index}.")
            continue
        if item != item.strip():
            whitespace_padded_indexes.append(index)
            reasons.append(
                f"{field_label} contains a token with leading/trailing whitespace at index {index}."
            )
            continue
        token = normalize_token(item)
        if not token:
            reasons.append(f"{field_label} contains a blank token at index {index}.")
            continue
        normalized.append(token)
    deduped = dedupe_preserve_order(normalized)
    duplicate_values = sorted({token for token in deduped if normalized.count(token) > 1})
    if duplicate_values:
        reasons.append(
            f"{field_label} contains duplicate token(s): {', '.join(duplicate_values)}."
        )
    evidence[f"{field_label}_normalized"] = deduped
    evidence[f"{field_label}_whitespace_padded_indexes"] = whitespace_padded_indexes
    return deduped


def normalize_required_tuple_list(
    values: Any,
    field_label: str,
    expected_part_count: int,
    allowed_platform_tokens: set[str] | None,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> List[str]:
    normalized_tokens = normalize_required_token_list(values, field_label, evidence, reasons)
    valid: List[str] = []
    malformed: List[str] = []
    for token in normalized_tokens:
        parts = token.split(":")
        if len(parts) != expected_part_count or any(not part for part in parts):
            malformed.append(token)
            continue
        if expected_part_count == 3 and allowed_platform_tokens is not None:
            platform_token = parts[2]
            if platform_token not in allowed_platform_tokens:
                malformed.append(token)
                continue
        valid.append(token)
    if malformed:
        reasons.append(
            f"{field_label} contains malformed token(s): {', '.join(sorted(set(malformed)))}."
        )
    evidence[f"{field_label}_malformed_tokens"] = sorted(set(malformed))
    return valid


def normalize_required_relative_file_list(
    values: Any,
    field_label: str,
    allowed_suffixes: List[str],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> List[str]:
    normalized_tokens = normalize_required_token_list(values, field_label, evidence, reasons)
    normalized_suffixes = [normalize_token(suffix) for suffix in allowed_suffixes if normalize_token(suffix)]
    malformed_location: List[str] = []
    malformed_suffix: List[str] = []
    valid: List[str] = []
    for token in normalized_tokens:
        file_name = Path(token).name
        if token != file_name or "\\" in token:
            malformed_location.append(token)
            continue
        if normalized_suffixes and not any(token.endswith(suffix) for suffix in normalized_suffixes):
            malformed_suffix.append(token)
            continue
        valid.append(token)
    if malformed_location:
        reasons.append(
            f"{field_label} contains non-basename token(s): {', '.join(sorted(set(malformed_location)))}."
        )
    if malformed_suffix:
        reasons.append(
            f"{field_label} contains token(s) without an allowed suffix ({', '.join(normalized_suffixes)}): "
            f"{', '.join(sorted(set(malformed_suffix)))}."
        )
    evidence[f"{field_label}_malformed_non_basename_tokens"] = sorted(set(malformed_location))
    evidence[f"{field_label}_malformed_suffix_tokens"] = sorted(set(malformed_suffix))
    return valid


def normalize_optional_string_scalar(
    value: Any,
    field_label: str,
    evidence: Dict[str, Any],
    reasons: List[str],
    *,
    lowercase: bool = True,
    required: bool = False,
) -> str:
    if value is None:
        evidence[f"{field_label}_present"] = False
        if required:
            reasons.append(f"{field_label} is missing.")
        return ""
    evidence[f"{field_label}_present"] = True
    evidence[f"{field_label}_raw_type"] = type(value).__name__
    if not isinstance(value, str):
        reasons.append(f"{field_label} must be a string when present.")
        return ""
    if value != value.strip():
        reasons.append(f"{field_label} contains leading/trailing whitespace.")
    normalized = value.strip()
    if lowercase:
        normalized = normalized.lower()
    if not normalized and required:
        reasons.append(f"{field_label} is blank.")
    evidence[f"{field_label}_normalized"] = normalized
    return normalized


def normalize_required_status_map(
    values: Any,
    field_label: str,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> Dict[str, str]:
    if values is None:
        return {}
    if not isinstance(values, dict):
        reasons.append(f"{field_label} must be an object when present.")
        return {}
    normalized: Dict[str, str] = {}
    malformed_entries: List[str] = []
    non_canonical_keys: List[str] = []
    duplicate_normalized_keys: List[str] = []
    for raw_key, raw_value in values.items():
        if not isinstance(raw_key, str):
            malformed_entries.append("<non-string-key>")
            reasons.append(f"{field_label} contains a non-string key.")
            continue
        if raw_key != raw_key.strip():
            malformed_entries.append(raw_key)
            reasons.append(f"{field_label} contains a key with leading/trailing whitespace: {raw_key!r}.")
            continue
        key = normalize_token(raw_key)
        if not key:
            malformed_entries.append(raw_key)
            reasons.append(f"{field_label} contains a blank key.")
            continue
        if raw_key != key:
            malformed_entries.append(raw_key)
            non_canonical_keys.append(raw_key)
            reasons.append(
                f"{field_label} contains a non-canonical key '{raw_key}' (expected '{key}')."
            )
            continue
        if key in normalized:
            malformed_entries.append(key)
            duplicate_normalized_keys.append(key)
            reasons.append(f"{field_label} contains duplicate normalized key '{key}'.")
            continue
        if not isinstance(raw_value, str):
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a non-string value for key '{key}'.")
            continue
        if raw_value != raw_value.strip():
            malformed_entries.append(key)
            reasons.append(
                f"{field_label} contains a value with leading/trailing whitespace for key '{key}'."
            )
            continue
        value = normalize_token(raw_value)
        if not value:
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a blank value for key '{key}'.")
            continue
        normalized[key] = value
    evidence[f"{field_label}_normalized"] = normalized
    evidence[f"{field_label}_malformed_entries"] = sorted(set(malformed_entries))
    evidence[f"{field_label}_non_canonical_keys"] = sorted(set(non_canonical_keys))
    evidence[f"{field_label}_duplicate_normalized_keys"] = sorted(
        set(duplicate_normalized_keys)
    )
    return normalized


def normalize_promoted_platform_heads(
    values: Any,
    field_label: str,
    allowed_platform_tokens: set[str] | None,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> Dict[str, List[str]]:
    if values is None:
        return {}
    if not isinstance(values, dict):
        reasons.append(f"{field_label} must be an object when present.")
        return {}
    normalized: Dict[str, List[str]] = {}
    raw_platform_keys_by_normalized: Dict[str, List[str]] = {}
    whitespace_padded_platform_keys: List[str] = []
    non_canonical_platform_keys: List[str] = []
    for raw_platform, raw_heads in values.items():
        if not isinstance(raw_platform, str):
            reasons.append(f"{field_label} contains a non-string platform key.")
            continue
        if raw_platform != raw_platform.strip():
            whitespace_padded_platform_keys.append(raw_platform)
            reasons.append(
                f"{field_label} contains a platform key with leading/trailing whitespace: {raw_platform!r}."
            )
            continue
        platform_token = normalize_token(raw_platform)
        if not platform_token:
            reasons.append(f"{field_label} contains a blank platform key.")
            continue
        if raw_platform != platform_token:
            non_canonical_platform_keys.append(raw_platform)
            reasons.append(
                f"{field_label} contains a non-canonical platform key '{raw_platform}' (expected '{platform_token}')."
            )
            continue
        if allowed_platform_tokens is not None and platform_token not in allowed_platform_tokens:
            reasons.append(f"{field_label} contains unsupported platform key '{platform_token}'.")
            continue
        raw_platform_keys_by_normalized.setdefault(platform_token, []).append(raw_platform)
        if len(raw_platform_keys_by_normalized[platform_token]) > 1:
            reasons.append(
                f"{field_label} contains duplicate normalized platform key '{platform_token}'."
            )
            continue
        head_field_label = f"{field_label}.{platform_token}"
        normalized[platform_token] = normalize_required_token_list(
            raw_heads,
            head_field_label,
            evidence,
            reasons,
        )
    evidence[f"{field_label}_normalized"] = normalized
    evidence[f"{field_label}_raw_platform_keys_by_normalized"] = raw_platform_keys_by_normalized
    evidence[f"{field_label}_whitespace_padded_platform_keys"] = sorted(
        set(whitespace_padded_platform_keys)
    )
    evidence[f"{field_label}_non_canonical_platform_keys"] = sorted(
        set(non_canonical_platform_keys)
    )
    evidence[f"{field_label}_duplicate_normalized_platform_keys"] = sorted(
        [
            platform_token
            for platform_token, raw_keys in raw_platform_keys_by_normalized.items()
            if len(raw_keys) > 1
        ]
    )
    return normalized


def parse_iso(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def payload_generated_at(payload: Dict[str, Any]) -> tuple[str, datetime | None]:
    for key in ("generated_at", "generatedAt"):
        if key in payload:
            raw = str(payload.get(key) or "").strip()
            return raw, parse_iso(raw)
    return "", None


def validate_receipt_freshness(label: str, payload: Dict[str, Any], evidence: Dict[str, Any], reasons: List[str]) -> None:
    generated_at_raw, generated_at = payload_generated_at(payload)
    evidence[f"{label}_generated_at"] = generated_at_raw
    if not generated_at_raw or generated_at is None:
        reasons.append(f"{label} is missing a valid generated_at timestamp.")
        return
    age_delta_seconds = int((datetime.now(timezone.utc) - generated_at).total_seconds())
    age_seconds = max(0, age_delta_seconds)
    if age_delta_seconds < 0:
        future_skew_seconds = abs(age_delta_seconds)
        evidence[f"{label}_future_skew_seconds"] = future_skew_seconds
        if future_skew_seconds > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
            reasons.append(f"{label} generated_at is in the future ({future_skew_seconds}s ahead).")
    evidence[f"{label}_age_seconds"] = age_seconds
    if age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        reasons.append(f"{label} is stale ({age_seconds}s old).")


def macos_rid_from_artifact(artifact: Dict[str, Any]) -> str:
    rid = normalize_token(artifact.get("rid"))
    if rid:
        return rid
    arch = normalize_token(artifact.get("arch"))
    if arch in {"arm64", "x64"}:
        return f"osx-{arch}"
    return ""


def is_desktop_install_media(platform: Any, kind: Any) -> bool:
    platform_token = normalize_token(platform)
    kind_token = normalize_token(kind)
    if platform_token == "macos":
        return kind_token in {"installer", "dmg", "pkg"}
    return kind_token == "installer"


def linux_gate_path_for_head(head: str, rid: str, avalonia_path: Path, blazor_path: Path, receipt_root: Path) -> Path:
    if head == "avalonia" and rid == "linux-x64":
        return avalonia_path
    if head == "blazor-desktop" and rid == "linux-x64":
        return blazor_path
    return receipt_root / f"UI_LINUX_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def macos_gate_path_for_head(head: str, rid: str, receipt_root: Path) -> Path:
    return receipt_root / f"UI_MACOS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def windows_gate_path_for_head(
    head: str,
    rid: str,
    receipt_root: Path,
    default_gate_path: Path,
) -> Path:
    if head == "avalonia" and rid == "win-x64":
        return default_gate_path
    return receipt_root / f"UI_WINDOWS_{head.upper().replace('-', '_')}_{rid.upper().replace('-', '_')}_DESKTOP_EXIT_GATE.generated.json"


def arch_from_rid(rid: str) -> str:
    normalized = normalize_token(rid)
    if normalized.endswith("x64"):
        return "x64"
    if normalized.endswith("arm64"):
        return "arm64"
    return ""


def expected_host_class_platform_token(platform: str) -> str:
    normalized = normalize_token(platform)
    if normalized == "windows":
        return "win"
    if normalized == "macos":
        return "osx"
    if normalized == "linux":
        return "linux"
    return normalized


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize_token(host_class)
    expected_token = expected_host_class_platform_token(platform)
    if not normalized_host or not expected_token:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def validate_receipt_path_scope(path: Path, repo_root: Path, reasons: List[str], evidence: Dict[str, Any], label: str) -> None:
    in_scope = path_within_root(path, repo_root)
    evidence.setdefault("receipt_scope", {})[label] = {
        "path": str(path),
        "within_repo_root": in_scope,
        "repo_root": str(repo_root.resolve()),
    }
    if not in_scope:
        reasons.append(
            f"{label} receipt path is outside this repo root and cannot be used as authoritative local proof."
        )


def path_within_any_root(path: Path, roots: List[Path]) -> bool:
    return any(path_within_root(path, root) for root in roots if isinstance(root, Path))


def validate_trusted_path_scope(
    path: Path,
    trusted_roots: List[Path],
    reasons: List[str],
    evidence: Dict[str, Any],
    label: str,
    reason_message: str,
) -> bool:
    in_scope = path_within_any_root(path, trusted_roots)
    evidence.setdefault("trusted_path_scope", {})[label] = {
        "path": str(path),
        "within_trusted_roots": in_scope,
        "trusted_roots": [str(root.resolve()) for root in trusted_roots],
    }
    if not in_scope:
        reasons.append(reason_message)
    return in_scope


def validate_flagship_head_proof(
    head: str,
    flagship_gate: Dict[str, Any],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
    proof = head_proofs.get(head) if isinstance(head_proofs.get(head), dict) else {}
    proof_status = normalize_token(proof.get("status"))
    evidence.setdefault("flagship_head_proofs", {})[head] = {
        "status": proof_status,
        "proof": proof,
    }
    if not status_ok(proof_status):
        reasons.append(f"Flagship UI proof is missing or not passing for promoted head '{head}'.")


def validate_cross_gate_head_proof(
    head: str,
    gate_label: str,
    proof_statuses: Dict[str, str],
    evidence_key: str,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    status = normalize_token(proof_statuses.get(head))
    evidence.setdefault(evidence_key, {})[head] = status
    if not status_ok(status):
        reasons.append(
            f"{gate_label} does not carry passing per-head proof for required desktop head '{head}'."
        )


def validate_linux_gate(
    gate_label: str,
    head: str,
    gate_path: Path,
    gate_payload: Dict[str, Any],
    expected_artifact: Dict[str, Any] | None,
    release_channel_id: str,
    release_channel_version: str,
    repo_root: Path,
    trusted_roots: List[Path],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness(f"linux desktop exit gate proof for {gate_label}", gate_payload, gate_evidence, reasons)
    gate_contract_name = normalize_contract_name(gate_payload)
    gate_evidence["contract_name"] = gate_contract_name
    if gate_contract_name != "chummer6-ui.linux_desktop_exit_gate":
        reasons.append(
            f"Linux desktop exit gate receipt contract_name is invalid for promoted head '{head}'."
        )
    gate_reasons = [
        str(item).strip()
        for item in (gate_payload.get("reasons") or [])
        if str(item).strip()
    ]
    if not gate_reasons and not status_ok(gate_status):
        fallback_gate_reason = str(gate_payload.get("reason") or "").strip()
        if fallback_gate_reason:
            gate_reasons = [fallback_gate_reason]
    gate_evidence["gate_reasons"] = gate_reasons

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_evidence["receipt_head"] = gate_head
    gate_release_version = str(
        gate_head.get("version")
        or gate_payload.get("releaseVersion")
        or gate_payload.get("version")
        or ""
    ).strip()
    gate_evidence["gate_release_version"] = gate_release_version
    if normalize_token(gate_head.get("app_key")) != head:
        reasons.append(f"Linux desktop exit gate receipt head does not match promoted head '{head}'.")
    if normalize_token(gate_head.get("platform")) != "linux":
        reasons.append(f"Linux desktop exit gate receipt platform does not match promoted head '{head}'.")
    if release_channel_version and not gate_release_version:
        reasons.append(
            f"Linux desktop exit gate receipt is missing releaseVersion/version for promoted head '{head}'."
        )
    elif release_channel_version and gate_release_version != release_channel_version:
        reasons.append(
            f"Linux desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head '{head}'."
        )

    if not status_ok(gate_status):
        reasons.append(f"Linux desktop exit gate is missing or not passing for promoted head '{head}'.")
    for gate_reason in gate_reasons:
        reasons.append(f"Linux gate reason ({head}): {gate_reason}")

    startup = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    release_channel_evidence = (
        gate_payload.get("release_channel")
        if isinstance(gate_payload.get("release_channel"), dict)
        else {}
    )
    host_supports_linux_startup_smoke = bool(release_channel_evidence.get("host_supports_linux_startup_smoke"))
    startup_smoke_external_blocker = normalize_token(release_channel_evidence.get("startup_smoke_external_blocker"))
    primary = startup.get("primary") if isinstance(startup.get("primary"), dict) else {}
    fallback = startup.get("fallback") if isinstance(startup.get("fallback"), dict) else {}
    unit_tests = gate_payload.get("unit_tests") if isinstance(gate_payload.get("unit_tests"), dict) else {}

    primary_status = normalize_token(primary.get("status"))
    fallback_status = normalize_token(fallback.get("status"))
    unit_test_status = normalize_token(unit_tests.get("status"))

    gate_evidence["primary_smoke_status"] = primary_status
    gate_evidence["fallback_smoke_status"] = fallback_status
    gate_evidence["unit_test_status"] = unit_test_status
    gate_evidence["host_supports_linux_startup_smoke"] = host_supports_linux_startup_smoke
    gate_evidence["startup_smoke_external_blocker"] = startup_smoke_external_blocker
    gate_evidence["unit_test_summary"] = unit_tests.get("summary") if isinstance(unit_tests.get("summary"), dict) else {}

    if primary_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux installer startup smoke is not passing for promoted head '{head}'.")
    if fallback_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux archive startup smoke is not passing for promoted head '{head}'.")
    if unit_test_status not in {"pass", "passed", "ready"}:
        reasons.append(f"Linux desktop runtime unit tests are not passing for promoted head '{head}'.")

    primary_receipt = primary.get("receipt") if isinstance(primary.get("receipt"), dict) else {}
    primary_receipt_path_raw = str(primary.get("receipt_path") or "").strip()
    primary_receipt_path = Path(primary_receipt_path_raw) if primary_receipt_path_raw else None
    primary_receipt_file_exists = primary_receipt_path is not None and primary_receipt_path.is_file()
    primary_receipt_file = load_json(primary_receipt_path) if primary_receipt_file_exists and primary_receipt_path is not None else {}
    primary_receipt_for_validation = primary_receipt_file if primary_receipt_file else {}

    gate_evidence["primary_receipt_path"] = primary_receipt_path_raw
    gate_evidence["primary_receipt_file_exists"] = primary_receipt_file_exists
    if not primary_receipt_file_exists:
        reasons.append(f"Linux installer startup smoke receipt path is missing/unreadable for promoted head '{head}'.")
        if not host_supports_linux_startup_smoke and startup_smoke_external_blocker != "missing_linux_host_capability":
            reasons.append(
                f"Linux startup smoke external blocker must be missing_linux_host_capability when installer startup smoke receipt is missing for promoted head '{head}' on a non-Linux-capable host."
            )
        if host_supports_linux_startup_smoke and startup_smoke_external_blocker:
            reasons.append(
                f"Linux startup smoke external blocker must be blank when installer startup smoke receipt is missing for promoted head '{head}' on a Linux-capable host."
            )
    elif primary_receipt_path is not None:
        validate_trusted_path_scope(
            primary_receipt_path,
            trusted_roots,
            reasons,
            gate_evidence,
            f"linux_startup_smoke:{head}",
            f"Linux installer startup smoke receipt path is outside trusted local roots for promoted head '{head}'.",
        )

    gate_evidence["primary_receipt_artifact_digest"] = normalize_token(primary_receipt.get("artifactDigest"))
    gate_evidence["primary_receipt_ready_checkpoint"] = normalize_token(primary_receipt.get("readyCheckpoint"))
    gate_evidence["primary_receipt_source"] = "file" if primary_receipt_file else "missing"
    if primary_receipt_file_exists and not primary_receipt_file:
        reasons.append(f"Linux installer startup smoke receipt file is unreadable or not a JSON object for promoted head '{head}'.")
    recorded_at_raw = (
        str(primary_receipt_for_validation.get("completedAtUtc") or "").strip()
        or str(primary_receipt_for_validation.get("recordedAtUtc") or "").strip()
        or str(primary_receipt_for_validation.get("startedAtUtc") or "").strip()
    )
    gate_evidence["primary_receipt_head_id"] = normalize_token(primary_receipt_for_validation.get("headId"))
    gate_evidence["primary_receipt_platform"] = normalize_token(primary_receipt_for_validation.get("platform"))
    gate_evidence["primary_receipt_rid"] = normalize_token(primary_receipt_for_validation.get("rid"))
    gate_evidence["primary_receipt_arch"] = normalize_token(primary_receipt_for_validation.get("arch"))
    gate_evidence["primary_receipt_channel_id"] = normalize_token(
        primary_receipt_for_validation.get("channelId") or primary_receipt_for_validation.get("channel")
    )
    gate_evidence["primary_receipt_host_class"] = normalize_token(primary_receipt_for_validation.get("hostClass"))
    gate_evidence["primary_receipt_operating_system"] = str(primary_receipt_for_validation.get("operatingSystem") or "").strip()
    gate_evidence["primary_receipt_artifact_digest"] = normalize_token(primary_receipt_for_validation.get("artifactDigest"))
    gate_evidence["primary_receipt_ready_checkpoint"] = normalize_token(primary_receipt_for_validation.get("readyCheckpoint"))
    gate_evidence["primary_receipt_version"] = str(
        primary_receipt_for_validation.get("version")
        or primary_receipt_for_validation.get("releaseVersion")
        or ""
    ).strip()
    gate_evidence["primary_receipt_recorded_at"] = recorded_at_raw
    recorded_at = parse_iso(recorded_at_raw)
    if not recorded_at_raw or recorded_at is None:
        reasons.append(f"Linux installer startup smoke receipt timestamp is missing/invalid for promoted head '{head}'.")
    else:
        age_delta_seconds = int((datetime.now(timezone.utc) - recorded_at).total_seconds())
        age_seconds = max(0, age_delta_seconds)
        if age_delta_seconds < 0:
            future_skew_seconds = abs(age_delta_seconds)
            gate_evidence["primary_receipt_future_skew_seconds"] = future_skew_seconds
            if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                reasons.append(
                    f"Linux installer startup smoke receipt timestamp is in the future for promoted head '{head}' "
                    f"({future_skew_seconds}s ahead)."
                )
        gate_evidence["primary_receipt_age_seconds"] = age_seconds
        if age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
            reasons.append(
                f"Linux installer startup smoke receipt is stale for promoted head '{head}' ({age_seconds}s old)."
            )
    if gate_evidence["primary_receipt_ready_checkpoint"] != "pre_ui_event_loop":
        reasons.append(f"Linux installer startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted head '{head}'.")
    if gate_evidence["primary_receipt_head_id"] != head:
        reasons.append(f"Linux installer startup smoke receipt headId does not match promoted head '{head}'.")
    if gate_evidence["primary_receipt_platform"] != "linux":
        reasons.append(f"Linux installer startup smoke receipt platform is not linux for promoted head '{head}'.")
    if not gate_evidence["primary_receipt_rid"]:
        reasons.append(f"Linux installer startup smoke receipt rid is missing for promoted head '{head}'.")
    if not gate_evidence["primary_receipt_host_class"]:
        reasons.append(f"Linux installer startup smoke receipt hostClass is missing for promoted head '{head}'.")
    elif not host_class_matches_platform(gate_evidence["primary_receipt_host_class"], "linux"):
        reasons.append(f"Linux installer startup smoke receipt hostClass does not identify a Linux host for promoted head '{head}'.")
    if not gate_evidence["primary_receipt_operating_system"]:
        reasons.append(f"Linux installer startup smoke receipt operatingSystem is missing for promoted head '{head}'.")
    if release_channel_id and gate_evidence["primary_receipt_channel_id"] != release_channel_id:
        reasons.append(f"Linux installer startup smoke receipt channelId does not match release channel for promoted head '{head}'.")
    if release_channel_version and not gate_evidence["primary_receipt_version"]:
        reasons.append(f"Linux installer startup smoke receipt is missing version for promoted head '{head}'.")
    elif release_channel_version and gate_evidence["primary_receipt_version"] != release_channel_version:
        reasons.append(f"Linux installer startup smoke receipt version does not match release channel version for promoted head '{head}'.")

    if expected_artifact is not None:
        expected_rid = normalize_token(expected_artifact.get("rid"))
        expected_sha = normalize_token(expected_artifact.get("sha256"))
        expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
        expected_arch = arch_from_rid(expected_rid)
        expected_artifact_source = normalize_token(expected_artifact.get("source"))
        policy_missing_release_artifact = (
            expected_artifact_source == "required_tuple_policy_missing_release_artifact"
        )
        gate_evidence["expected_artifact_source"] = expected_artifact_source
        if expected_rid and normalize_token(gate_head.get("rid")) != expected_rid:
            reasons.append(f"Linux desktop exit gate receipt RID does not match promoted head '{head}' ({expected_rid}).")
        if expected_rid and gate_evidence["primary_receipt_rid"] and gate_evidence["primary_receipt_rid"] != expected_rid:
            reasons.append(f"Linux installer startup smoke receipt rid does not match promoted RID for head '{head}'.")
        if not policy_missing_release_artifact:
            if expected_arch and gate_evidence["primary_receipt_arch"] != expected_arch:
                reasons.append(f"Linux installer startup smoke receipt arch does not match promoted RID for head '{head}'.")
            if expected_digest and gate_evidence["primary_receipt_artifact_digest"] != expected_digest:
                reasons.append(
                    f"Linux installer startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes for head '{head}'."
                )

    for key, value in (
        ("install_launch_capture_path", str(primary_receipt.get("artifactInstallLaunchCapturePath") or "").strip()),
        ("install_wrapper_capture_path", str(primary_receipt.get("artifactInstallWrapperCapturePath") or "").strip()),
        ("install_desktop_entry_capture_path", str(primary_receipt.get("artifactInstallDesktopEntryCapturePath") or "").strip()),
        ("install_verification_path", str(primary_receipt.get("artifactInstallVerificationPath") or "").strip()),
    ):
        gate_evidence[key] = value
        if not value:
            reasons.append(f"Linux installer proof is missing {key} for promoted head '{head}'.")
            continue
        proof_path = Path(value)
        if not proof_path.exists():
            reasons.append(f"Linux installer proof path does not exist for promoted head '{head}': {value}")
            continue
        validate_trusted_path_scope(
            proof_path,
            trusted_roots,
            reasons,
            gate_evidence,
            f"linux_installer_capture:{head}:{key}",
            f"Linux installer proof path is outside trusted local roots for promoted head '{head}' ({key}).",
        )

    evidence.setdefault("linux_gates", {})[gate_label] = gate_evidence


def validate_windows_gate(
    gate_label: str,
    gate_path: Path,
    gate_payload: Dict[str, Any],
    expected_artifact: Dict[str, Any],
    release_channel_id: str,
    release_channel_version: str,
    desktop_files_root: Path,
    repo_root: Path,
    trusted_roots: List[Path],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness("windows desktop exit gate proof", gate_payload, gate_evidence, reasons)
    gate_contract_name = normalize_contract_name(gate_payload)
    gate_evidence["contract_name"] = gate_contract_name
    if gate_contract_name != "chummer6-ui.windows_desktop_exit_gate":
        reasons.append("Windows desktop exit gate receipt contract_name is invalid.")

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_checks = gate_payload.get("checks") if isinstance(gate_payload.get("checks"), dict) else {}
    gate_reasons = [
        str(item).strip()
        for item in (gate_payload.get("reasons") or [])
        if str(item).strip()
    ]
    channel_artifact = (
        gate_checks.get("release_channel_windows_artifact")
        if isinstance(gate_checks.get("release_channel_windows_artifact"), dict)
        else {}
    )

    gate_evidence["receipt_head"] = gate_head
    gate_evidence["release_channel_windows_artifact"] = channel_artifact
    gate_evidence["windows_installer_path"] = str(gate_checks.get("windows_installer_path") or "").strip()
    gate_evidence["gate_reasons"] = gate_reasons
    gate_evidence["startup_smoke_receipt_path"] = str(gate_checks.get("startup_smoke_receipt_path") or "").strip()
    host_supports_windows_startup_smoke = bool(gate_checks.get("host_supports_windows_startup_smoke"))
    startup_smoke_external_blocker = normalize_token(gate_checks.get("startup_smoke_external_blocker"))
    gate_evidence["host_supports_windows_startup_smoke"] = host_supports_windows_startup_smoke
    gate_evidence["startup_smoke_external_blocker"] = startup_smoke_external_blocker
    embedded_payload_marker_present = bool(gate_checks.get("embedded_payload_marker_present"))
    embedded_sample_marker_present = bool(gate_checks.get("embedded_sample_marker_present"))
    gate_evidence["embedded_payload_marker_present"] = embedded_payload_marker_present
    gate_evidence["embedded_sample_marker_present"] = embedded_sample_marker_present
    gate_release_version = str(
        gate_payload.get("releaseVersion")
        or gate_checks.get("release_channel_version")
        or gate_checks.get("releaseVersion")
        or gate_head.get("version")
        or ""
    ).strip()
    gate_evidence["gate_release_version"] = gate_release_version

    if normalize_token(gate_head.get("platform")) != "windows":
        reasons.append("Windows desktop exit gate receipt platform is not 'windows'.")
    if release_channel_version and not gate_release_version:
        reasons.append("Windows desktop exit gate receipt is missing releaseVersion/version.")
    elif release_channel_version and gate_release_version != release_channel_version:
        reasons.append("Windows desktop exit gate receipt releaseVersion/version does not match release channel version.")
    if not status_ok(gate_status):
        reasons.append("Windows desktop exit gate is missing or not passing.")
    for gate_reason in gate_reasons:
        reasons.append(f"Windows gate reason: {gate_reason}")

    expected_head = normalize_token(expected_artifact.get("head"))
    expected_rid = normalize_token(expected_artifact.get("rid"))
    expected_tuple = (expected_head, expected_rid)
    gate_tuple = (
        normalize_token(gate_head.get("app_key")),
        normalize_token(gate_head.get("rid")),
    )
    if gate_tuple != expected_tuple:
        reasons.append(
            f"Windows desktop exit gate receipt head/RID does not match promoted release-channel Windows artifact tuple {gate_label}."
        )
        evidence.setdefault("windows_gates", {})[gate_label] = gate_evidence
        return

    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_size = int(expected_artifact.get("sizeBytes") or 0)
    expected_artifact_source = normalize_token(expected_artifact.get("source"))
    policy_missing_release_artifact = expected_artifact_source == "required_tuple_policy_missing_release_artifact"
    gate_evidence["expected_artifact_source"] = expected_artifact_source

    if not policy_missing_release_artifact:
        if normalize_token(channel_artifact.get("head")) != expected_head:
            reasons.append("Windows gate embedded release_channel_windows_artifact head does not match promoted release channel.")
        if normalize_token(channel_artifact.get("rid")) != expected_rid:
            reasons.append("Windows gate embedded release_channel_windows_artifact RID does not match promoted release channel.")
        if normalize_token(channel_artifact.get("platform")) != "windows":
            reasons.append("Windows gate embedded release_channel_windows_artifact platform is not 'windows'.")
        if str(channel_artifact.get("fileName") or "").strip() != expected_file_name:
            reasons.append("Windows gate embedded release_channel_windows_artifact fileName does not match promoted release channel.")
        if expected_sha and normalize_token(channel_artifact.get("sha256")) != expected_sha:
            reasons.append("Windows gate embedded release_channel_windows_artifact sha256 does not match promoted release channel.")
        if expected_size and int(channel_artifact.get("sizeBytes") or 0) != expected_size:
            reasons.append("Windows gate embedded release_channel_windows_artifact sizeBytes does not match promoted release channel.")

    installer_path = Path(str(gate_checks.get("windows_installer_path") or "").strip())
    if not installer_path.is_file():
        reasons.append("Windows desktop exit gate windows_installer_path does not exist.")
    else:
        gate_evidence["windows_installer_size_bytes"] = int(installer_path.stat().st_size)
        gate_evidence["windows_installer_sha256"] = hashlib.sha256(installer_path.read_bytes()).hexdigest().lower()
        if expected_size and gate_evidence["windows_installer_size_bytes"] != expected_size:
            reasons.append("Windows desktop exit gate installer size does not match promoted release-channel artifact bytes.")
        if expected_sha and normalize_token(gate_evidence["windows_installer_sha256"]) != expected_sha:
            reasons.append("Windows desktop exit gate installer sha256 does not match promoted release-channel artifact bytes.")

    shelf_path = desktop_files_root / expected_file_name
    gate_evidence["expected_windows_shelf_path"] = str(shelf_path)
    if shelf_path.is_file() and installer_path.is_file():
        shelf_sha = hashlib.sha256(shelf_path.read_bytes()).hexdigest().lower()
        gate_evidence["expected_windows_shelf_sha256"] = shelf_sha
        if shelf_sha != normalize_token(gate_evidence.get("windows_installer_sha256")):
            reasons.append("Windows desktop exit gate installer bytes do not match the local promoted desktop shelf artifact.")

    startup_smoke_receipt_path = (
        Path(gate_evidence["startup_smoke_receipt_path"]) if gate_evidence["startup_smoke_receipt_path"] else None
    )
    startup_smoke_receipt_exists = startup_smoke_receipt_path is not None and startup_smoke_receipt_path.is_file()
    startup_smoke_receipt_payload = (
        load_json(startup_smoke_receipt_path)
        if startup_smoke_receipt_exists and startup_smoke_receipt_path is not None
        else {}
    )
    if not startup_smoke_receipt_exists:
        reasons.append("Windows startup smoke receipt path is missing/unreadable for promoted installer bytes.")
        if not host_supports_windows_startup_smoke and startup_smoke_external_blocker != "missing_windows_host_capability":
            reasons.append(
                "Windows startup smoke external blocker must be missing_windows_host_capability when startup smoke receipt is missing on a non-Windows-capable host."
            )
        if host_supports_windows_startup_smoke and startup_smoke_external_blocker:
            reasons.append(
                "Windows startup smoke external blocker must be blank when startup smoke receipt is missing on a Windows-capable host."
            )
    elif startup_smoke_receipt_path is not None and not validate_trusted_path_scope(
        startup_smoke_receipt_path,
        trusted_roots,
        reasons,
        gate_evidence,
        "windows_startup_smoke",
        "Windows startup smoke receipt path is outside trusted local roots.",
    ):
        pass
    else:
        startup_smoke_receipt_source = "file" if startup_smoke_receipt_payload else "missing"
        gate_evidence["startup_smoke_receipt_source"] = startup_smoke_receipt_source
        if startup_smoke_receipt_exists and not startup_smoke_receipt_payload:
            reasons.append("Windows startup smoke receipt file is unreadable or not a JSON object for promoted installer bytes.")
        startup_smoke_status = normalize_token(
            startup_smoke_receipt_payload.get("status")
        )
        startup_smoke_ready_checkpoint = normalize_token(
            startup_smoke_receipt_payload.get("readyCheckpoint")
        )
        startup_smoke_artifact_digest = normalize_token(
            startup_smoke_receipt_payload.get("artifactDigest")
        )
        startup_smoke_head_id = normalize_token(
            startup_smoke_receipt_payload.get("headId")
        )
        startup_smoke_platform = normalize_token(
            startup_smoke_receipt_payload.get("platform")
        )
        startup_smoke_rid = normalize_token(
            startup_smoke_receipt_payload.get("rid")
        )
        startup_smoke_arch = normalize_token(
            startup_smoke_receipt_payload.get("arch")
        )
        startup_smoke_channel_id = normalize_token(
            startup_smoke_receipt_payload.get("channelId")
            or startup_smoke_receipt_payload.get("channel")
        )
        startup_smoke_version = str(
            startup_smoke_receipt_payload.get("version")
            or startup_smoke_receipt_payload.get("releaseVersion")
            or ""
        ).strip()
        startup_smoke_host_class = normalize_token(
            startup_smoke_receipt_payload.get("hostClass")
        )
        startup_smoke_operating_system = str(
            startup_smoke_receipt_payload.get("operatingSystem")
            or ""
        ).strip()
        startup_smoke_recorded_at_raw = str(
            startup_smoke_receipt_payload.get("completedAtUtc")
            or startup_smoke_receipt_payload.get("recordedAtUtc")
            or startup_smoke_receipt_payload.get("startedAtUtc")
            or ""
        ).strip()
        startup_smoke_recorded_at = parse_iso(startup_smoke_recorded_at_raw)
        expected_startup_smoke_arch = arch_from_rid(expected_rid)
        expected_startup_smoke_digest = (
            f"sha256:{expected_sha}"
            if expected_sha
            else f"sha256:{normalize_token(gate_evidence.get('windows_installer_sha256'))}"
            if normalize_token(gate_evidence.get("windows_installer_sha256"))
            else ""
        )

        gate_evidence["startup_smoke_status"] = startup_smoke_status
        gate_evidence["startup_smoke_ready_checkpoint"] = startup_smoke_ready_checkpoint
        gate_evidence["startup_smoke_artifact_digest"] = startup_smoke_artifact_digest
        gate_evidence["startup_smoke_head_id"] = startup_smoke_head_id
        gate_evidence["startup_smoke_platform"] = startup_smoke_platform
        gate_evidence["startup_smoke_rid"] = startup_smoke_rid
        gate_evidence["startup_smoke_arch"] = startup_smoke_arch
        gate_evidence["startup_smoke_channel"] = startup_smoke_channel_id
        gate_evidence["startup_smoke_version"] = startup_smoke_version
        gate_evidence["startup_smoke_host_class"] = startup_smoke_host_class
        gate_evidence["startup_smoke_operating_system"] = startup_smoke_operating_system
        gate_evidence["startup_smoke_recorded_at"] = startup_smoke_recorded_at_raw

        if startup_smoke_status not in {"pass", "passed", "ready"}:
            reasons.append("Windows startup smoke receipt status is not passing for promoted installer bytes.")
        if startup_smoke_ready_checkpoint != "pre_ui_event_loop":
            reasons.append("Windows startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted installer bytes.")
        if expected_startup_smoke_digest and startup_smoke_artifact_digest != expected_startup_smoke_digest:
            reasons.append("Windows startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes.")
        if startup_smoke_head_id != expected_head:
            reasons.append("Windows startup smoke receipt headId does not match promoted release-channel head.")
        if startup_smoke_platform != "windows":
            reasons.append("Windows startup smoke receipt platform is not windows for promoted installer bytes.")
        if not startup_smoke_rid:
            reasons.append("Windows startup smoke receipt rid is missing for promoted installer bytes.")
        elif startup_smoke_rid != expected_rid:
            reasons.append("Windows startup smoke receipt rid does not match promoted release-channel RID.")
        if not startup_smoke_host_class:
            reasons.append("Windows startup smoke receipt hostClass is missing for promoted installer bytes.")
        elif not host_class_matches_platform(startup_smoke_host_class, "windows"):
            reasons.append("Windows startup smoke receipt hostClass does not identify a Windows host for promoted installer bytes.")
        if not startup_smoke_operating_system:
            reasons.append("Windows startup smoke receipt operatingSystem is missing for promoted installer bytes.")
        if expected_startup_smoke_arch and startup_smoke_arch != expected_startup_smoke_arch:
            reasons.append("Windows startup smoke receipt arch does not match promoted release-channel RID.")
        if release_channel_id and startup_smoke_channel_id != release_channel_id:
            reasons.append("Windows startup smoke receipt channelId does not match release-channel channelId for promoted installer bytes.")
        if release_channel_version and not startup_smoke_version:
            reasons.append("Windows startup smoke receipt is missing version for promoted installer bytes.")
        elif release_channel_version and startup_smoke_version != release_channel_version:
            reasons.append("Windows startup smoke receipt version does not match release channel version for promoted installer bytes.")
        if not startup_smoke_recorded_at_raw or startup_smoke_recorded_at is None:
            reasons.append("Windows startup smoke receipt timestamp is missing/invalid for promoted installer bytes.")
        else:
            startup_smoke_age_delta_seconds = int((datetime.now(timezone.utc) - startup_smoke_recorded_at).total_seconds())
            startup_smoke_age_seconds = max(0, startup_smoke_age_delta_seconds)
            if startup_smoke_age_delta_seconds < 0:
                startup_smoke_future_skew_seconds = abs(startup_smoke_age_delta_seconds)
                gate_evidence["startup_smoke_receipt_future_skew_seconds"] = startup_smoke_future_skew_seconds
                if startup_smoke_future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                    reasons.append(
                        "Windows startup smoke receipt timestamp is in the future for promoted installer bytes "
                        f"({startup_smoke_future_skew_seconds}s ahead)."
                    )
            gate_evidence["startup_smoke_receipt_age_seconds"] = startup_smoke_age_seconds
            if startup_smoke_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
                reasons.append(
                    f"Windows startup smoke receipt is stale for promoted installer bytes ({startup_smoke_age_seconds}s old)."
                )

    if not embedded_payload_marker_present:
        reasons.append(f"Windows installer receipt does not confirm embedded payload marker for promoted tuple {gate_label}.")
    if not embedded_sample_marker_present:
        reasons.append(f"Windows installer receipt does not confirm bundled demo sample marker for promoted tuple {gate_label}.")

    evidence.setdefault("windows_gates", {})[gate_label] = gate_evidence


def validate_macos_gate(
    head: str,
    rid: str,
    expected_artifact: Dict[str, Any],
    gate_path: Path,
    gate_payload: Dict[str, Any],
    release_channel_id: str,
    release_channel_version: str,
    desktop_files_root: Path,
    repo_root: Path,
    trusted_roots: List[Path],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    gate_evidence: Dict[str, Any] = {
        "path": str(gate_path),
    }
    gate_status = pick_status(gate_payload)
    gate_evidence["status"] = gate_status
    validate_receipt_freshness(f"macOS desktop exit gate proof for {head} ({rid})", gate_payload, gate_evidence, reasons)
    gate_contract_name = normalize_contract_name(gate_payload)
    gate_evidence["contract_name"] = gate_contract_name
    if gate_contract_name != "chummer6-ui.macos_desktop_exit_gate":
        reasons.append(f"macOS desktop exit gate receipt contract_name is invalid for promoted head '{head}' ({rid}).")

    gate_head = gate_payload.get("head") if isinstance(gate_payload.get("head"), dict) else {}
    gate_checks = gate_payload.get("checks") if isinstance(gate_payload.get("checks"), dict) else {}
    gate_reasons = [
        str(item).strip()
        for item in (gate_payload.get("reasons") or [])
        if str(item).strip()
    ]
    channel_artifact = (
        gate_checks.get("release_channel_macos_artifact")
        if isinstance(gate_checks.get("release_channel_macos_artifact"), dict)
        else {}
    )
    gate_evidence["receipt_head"] = gate_head
    gate_evidence["gate_reasons"] = gate_reasons
    gate_release_version = str(
        gate_payload.get("releaseVersion")
        or gate_checks.get("release_channel_version")
        or gate_checks.get("releaseVersion")
        or gate_head.get("version")
        or ""
    ).strip()
    gate_evidence["gate_release_version"] = gate_release_version
    if normalize_token(gate_head.get("app_key")) != head:
        reasons.append(f"macOS desktop exit gate receipt head does not match promoted head '{head}'.")
    if normalize_token(gate_head.get("rid")) != rid:
        reasons.append(f"macOS desktop exit gate receipt RID does not match promoted head '{head}' ({rid}).")
    if normalize_token(gate_head.get("platform")) != "macos":
        reasons.append(f"macOS desktop exit gate receipt platform does not match promoted head '{head}'.")
    if release_channel_version and not gate_release_version:
        reasons.append(f"macOS desktop exit gate receipt is missing releaseVersion/version for promoted head '{head}' ({rid}).")
    elif release_channel_version and gate_release_version != release_channel_version:
        reasons.append(f"macOS desktop exit gate receipt releaseVersion/version does not match release channel version for promoted head '{head}' ({rid}).")
    if not status_ok(gate_status):
        reasons.append(f"macOS desktop exit gate is missing or not passing for promoted head '{head}' ({rid}).")
    for gate_reason in gate_reasons:
        reasons.append(f"macOS gate reason ({head}/{rid}): {gate_reason}")

    startup = gate_payload.get("startup_smoke") if isinstance(gate_payload.get("startup_smoke"), dict) else {}
    artifact = gate_payload.get("artifact") if isinstance(gate_payload.get("artifact"), dict) else {}
    host_supports_macos_startup_smoke = bool(gate_checks.get("host_supports_macos_startup_smoke"))
    startup_smoke_external_blocker = normalize_token(startup.get("external_blocker"))
    startup_receipt = startup.get("receipt") if isinstance(startup.get("receipt"), dict) else {}
    artifact_exists = bool(artifact.get("installer_exists"))
    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    expected_size = int(expected_artifact.get("sizeBytes") or 0)
    expected_arch = "arm64" if rid.endswith("arm64") else "x64" if rid.endswith("x64") else ""
    expected_artifact_source = normalize_token(expected_artifact.get("source"))
    policy_missing_release_artifact = expected_artifact_source == "required_tuple_policy_missing_release_artifact"
    gate_evidence["expected_artifact_source"] = expected_artifact_source
    startup_receipt_path = Path(str(startup.get("receipt_path") or "").strip()) if startup.get("receipt_path") else None
    startup_receipt_exists = startup_receipt_path is not None and startup_receipt_path.is_file()
    startup_receipt_file = (
        load_json(startup_receipt_path)
        if startup_receipt_exists and startup_receipt_path is not None
        else {}
    )
    startup_receipt_for_validation = startup_receipt_file if startup_receipt_file else {}
    startup_smoke_status = normalize_token(
        startup_receipt_for_validation.get("status")
        or startup.get("status")
    )
    startup_smoke_ready_checkpoint = normalize_token(
        startup_receipt_for_validation.get("readyCheckpoint")
        or startup.get("ready_checkpoint")
    )
    startup_smoke_artifact_digest = normalize_token(
        startup_receipt_for_validation.get("artifactDigest")
        or startup.get("artifact_digest")
    )
    startup_smoke_head_id = normalize_token(startup_receipt_for_validation.get("headId"))
    startup_smoke_platform = normalize_token(startup_receipt_for_validation.get("platform"))
    startup_smoke_rid = normalize_token(startup_receipt_for_validation.get("rid"))
    startup_smoke_arch = normalize_token(startup_receipt_for_validation.get("arch"))
    startup_smoke_channel_id = normalize_token(
        startup_receipt_for_validation.get("channelId") or startup_receipt_for_validation.get("channel")
    )
    startup_smoke_version = str(
        startup_receipt_for_validation.get("version")
        or startup_receipt_for_validation.get("releaseVersion")
        or ""
    ).strip()
    startup_smoke_host_class = normalize_token(startup_receipt_for_validation.get("hostClass"))
    startup_smoke_operating_system = str(startup_receipt_for_validation.get("operatingSystem") or "").strip()
    startup_receipt_recorded_at_raw = str(
        startup_receipt_for_validation.get("completedAtUtc")
        or startup_receipt_for_validation.get("recordedAtUtc")
        or startup_receipt_for_validation.get("startedAtUtc")
        or startup.get("receipt_recorded_at")
        or ""
    ).strip()
    startup_receipt_recorded_at = parse_iso(startup_receipt_recorded_at_raw)

    gate_evidence["startup_smoke_status"] = startup_smoke_status
    gate_evidence["artifact"] = artifact
    gate_evidence["release_channel_macos_artifact"] = channel_artifact
    gate_evidence["startup_smoke_receipt_path"] = str(startup.get("receipt_path") or "").strip()
    gate_evidence["startup_smoke_receipt_file_exists"] = startup_receipt_exists
    gate_evidence["startup_smoke_receipt_source"] = "file" if startup_receipt_file else "missing"
    gate_evidence["host_supports_macos_startup_smoke"] = host_supports_macos_startup_smoke
    gate_evidence["startup_smoke_external_blocker"] = startup_smoke_external_blocker
    if startup_receipt_exists and not startup_receipt_file:
        reasons.append(f"macOS startup smoke receipt file is unreadable or not a JSON object for promoted head '{head}' ({rid}).")
    gate_evidence["startup_smoke_ready_checkpoint"] = startup_smoke_ready_checkpoint
    gate_evidence["startup_smoke_artifact_digest"] = startup_smoke_artifact_digest
    gate_evidence["startup_smoke_receipt_head_id"] = startup_smoke_head_id
    gate_evidence["startup_smoke_receipt_platform"] = startup_smoke_platform
    gate_evidence["startup_smoke_receipt_rid"] = startup_smoke_rid
    gate_evidence["startup_smoke_receipt_arch"] = startup_smoke_arch
    gate_evidence["startup_smoke_receipt_channel_id"] = startup_smoke_channel_id
    gate_evidence["startup_smoke_receipt_version"] = startup_smoke_version
    gate_evidence["startup_smoke_receipt_host_class"] = startup_smoke_host_class
    gate_evidence["startup_smoke_receipt_operating_system"] = startup_smoke_operating_system
    gate_evidence["startup_smoke_receipt_recorded_at"] = startup_receipt_recorded_at_raw

    if startup_smoke_status not in {"pass", "passed", "ready"}:
        reasons.append(f"macOS startup smoke is not passing for promoted head '{head}' ({rid}).")
    if not artifact_exists:
        reasons.append(f"macOS installer artifact is missing for promoted head '{head}' ({rid}).")
    if not policy_missing_release_artifact:
        if normalize_token(channel_artifact.get("head")) != head:
            reasons.append("macOS gate embedded release_channel_macos_artifact head does not match promoted release channel.")
        if normalize_token(channel_artifact.get("rid")) != rid:
            reasons.append("macOS gate embedded release_channel_macos_artifact RID does not match promoted release channel.")
        if normalize_token(channel_artifact.get("platform")) != "macos":
            reasons.append("macOS gate embedded release_channel_macos_artifact platform is not macOS.")
        if expected_file_name and str(channel_artifact.get("fileName") or "").strip() != expected_file_name:
            reasons.append("macOS gate embedded release_channel_macos_artifact fileName does not match promoted release channel.")
        if expected_sha and normalize_token(channel_artifact.get("sha256")) != expected_sha:
            reasons.append("macOS gate embedded release_channel_macos_artifact sha256 does not match promoted release channel.")
        if expected_size and int(channel_artifact.get("sizeBytes") or 0) != expected_size:
            reasons.append("macOS gate embedded release_channel_macos_artifact sizeBytes does not match promoted release channel.")

    installer_path = Path(str(artifact.get("installer_path") or "").strip()) if artifact.get("installer_path") else None
    if installer_path is None or not installer_path.is_file():
        reasons.append(f"macOS gate installer path is missing or unreadable for promoted head '{head}' ({rid}).")
    else:
        installer_size = int(installer_path.stat().st_size)
        installer_sha = normalize_token(hashlib.sha256(installer_path.read_bytes()).hexdigest())
        gate_evidence["installer_size_bytes"] = installer_size
        gate_evidence["installer_sha256"] = installer_sha
        if expected_size and installer_size != expected_size:
            reasons.append("macOS desktop exit gate installer size does not match promoted release-channel artifact bytes.")
        if expected_sha and installer_sha != expected_sha:
            reasons.append("macOS desktop exit gate installer sha256 does not match promoted release-channel artifact bytes.")

        shelf_path = desktop_files_root / expected_file_name if expected_file_name else desktop_files_root
        gate_evidence["expected_macos_shelf_path"] = str(shelf_path)
        if expected_file_name and shelf_path.is_file():
            shelf_sha = normalize_token(hashlib.sha256(shelf_path.read_bytes()).hexdigest())
            gate_evidence["expected_macos_shelf_sha256"] = shelf_sha
            if shelf_sha != installer_sha:
                reasons.append("macOS desktop exit gate installer bytes do not match the local promoted desktop shelf artifact.")

    if not startup_receipt_exists:
        reasons.append(f"macOS startup smoke receipt path is missing or unreadable for promoted head '{head}' ({rid}).")
        if not host_supports_macos_startup_smoke and startup_smoke_external_blocker != "missing_macos_host_capability":
            reasons.append(
                f"macOS startup smoke external blocker must be missing_macos_host_capability when startup smoke receipt is missing for promoted head '{head}' ({rid}) on a non-macOS-capable host."
            )
        if host_supports_macos_startup_smoke and startup_smoke_external_blocker:
            reasons.append(
                f"macOS startup smoke external blocker must be blank when startup smoke receipt is missing for promoted head '{head}' ({rid}) on a macOS-capable host."
            )
    elif startup_receipt_path is not None and not validate_trusted_path_scope(
        startup_receipt_path,
        trusted_roots,
        reasons,
        gate_evidence,
        f"macos_startup_smoke:{head}:{rid}",
        f"macOS startup smoke receipt path is outside trusted local roots for promoted head '{head}' ({rid}).",
    ):
        pass
    else:
        if startup_smoke_status not in {"pass", "passed", "ready"}:
            reasons.append(f"macOS startup smoke receipt status is not passing for promoted head '{head}' ({rid}).")
        if startup_smoke_ready_checkpoint != "pre_ui_event_loop":
            reasons.append(f"macOS startup smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted head '{head}' ({rid}).")
        if expected_digest and startup_smoke_artifact_digest != expected_digest:
            reasons.append(f"macOS startup smoke receipt artifactDigest does not match promoted release-channel artifact bytes for head '{head}' ({rid}).")
        if startup_smoke_head_id != head:
            reasons.append(f"macOS startup smoke receipt headId does not match promoted head '{head}' ({rid}).")
        if startup_smoke_platform != "macos":
            reasons.append(f"macOS startup smoke receipt platform is not macOS for promoted head '{head}' ({rid}).")
        if not startup_smoke_rid:
            reasons.append(f"macOS startup smoke receipt rid is missing for promoted head '{head}' ({rid}).")
        elif startup_smoke_rid != rid:
            reasons.append(f"macOS startup smoke receipt rid does not match promoted RID for head '{head}' ({rid}).")
        if not startup_smoke_host_class:
            reasons.append(f"macOS startup smoke receipt hostClass is missing for promoted head '{head}' ({rid}).")
        elif not host_class_matches_platform(startup_smoke_host_class, "macos"):
            reasons.append(f"macOS startup smoke receipt hostClass does not identify a macOS host for promoted head '{head}' ({rid}).")
        if not startup_smoke_operating_system:
            reasons.append(f"macOS startup smoke receipt operatingSystem is missing for promoted head '{head}' ({rid}).")
        if expected_arch and startup_smoke_arch != expected_arch:
            reasons.append(f"macOS startup smoke receipt arch does not match promoted RID for head '{head}' ({rid}).")
        if release_channel_id and startup_smoke_channel_id != release_channel_id:
            reasons.append(f"macOS startup smoke receipt channelId does not match release-channel channelId for promoted head '{head}' ({rid}).")
        if release_channel_version and not startup_smoke_version:
            reasons.append(f"macOS startup smoke receipt is missing version for promoted head '{head}' ({rid}).")
        elif release_channel_version and startup_smoke_version != release_channel_version:
            reasons.append(f"macOS startup smoke receipt version does not match release channel version for promoted head '{head}' ({rid}).")
        if not startup_receipt_recorded_at_raw or startup_receipt_recorded_at is None:
            reasons.append(f"macOS startup smoke receipt timestamp is missing/invalid for promoted head '{head}' ({rid}).")
        else:
            startup_age_delta_seconds = int((datetime.now(timezone.utc) - startup_receipt_recorded_at).total_seconds())
            startup_age_seconds = max(0, startup_age_delta_seconds)
            if startup_age_delta_seconds < 0:
                startup_future_skew_seconds = abs(startup_age_delta_seconds)
                gate_evidence["startup_smoke_receipt_future_skew_seconds"] = startup_future_skew_seconds
                if startup_future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                    reasons.append(
                        f"macOS startup smoke receipt timestamp is in the future for promoted head '{head}' ({rid}) "
                        f"({startup_future_skew_seconds}s ahead)."
                    )
            gate_evidence["startup_smoke_receipt_age_seconds"] = startup_age_seconds
            if startup_age_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
                reasons.append(
                    f"macOS startup smoke receipt is stale for promoted head '{head}' ({rid}) ({startup_age_seconds}s old)."
                )

    evidence.setdefault("macos_gates", {})[f"{head}:{rid}"] = gate_evidence


def validate_local_release_artifact_file(
    artifact: Dict[str, Any],
    desktop_files_root: Path,
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    artifact_id = str(artifact.get("artifactId") or "").strip()
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        download_url = str(artifact.get("downloadUrl") or "").strip()
        file_name = Path(download_url).name if download_url else ""

    evidence_key = artifact_id or file_name or "unknown-artifact"
    local_path = desktop_files_root / file_name if file_name else desktop_files_root
    exists = bool(file_name) and local_path.is_file()

    artifact_evidence: Dict[str, Any] = {
        "artifact_id": artifact_id,
        "file_name": file_name,
        "path": str(local_path),
        "exists": exists,
    }

    expected_size = int(artifact.get("sizeBytes") or 0)
    expected_sha = normalize_token(artifact.get("sha256"))
    if exists:
        artifact_evidence["size_bytes"] = int(local_path.stat().st_size)
        artifact_evidence["sha256"] = normalize_token(hashlib.sha256(local_path.read_bytes()).hexdigest())
    else:
        artifact_evidence["size_bytes"] = 0
        artifact_evidence["sha256"] = ""

    artifact_evidence["expected_size_bytes"] = expected_size
    artifact_evidence["expected_sha256"] = expected_sha
    evidence.setdefault("release_artifacts_local", {})[evidence_key] = artifact_evidence

    if not file_name:
        reasons.append("Release channel desktop artifact is missing fileName/downloadUrl basename, so local proof cannot verify shipped bytes.")
        return

    if not exists:
        reasons.append(f"Promoted release-channel artifact is missing from local desktop downloads shelf: {file_name}.")
        return

    if expected_size and artifact_evidence["size_bytes"] != expected_size:
        reasons.append(f"Promoted release-channel artifact size does not match local bytes for {file_name}.")
    if expected_sha and artifact_evidence["sha256"] != expected_sha:
        reasons.append(f"Promoted release-channel artifact sha256 does not match local bytes for {file_name}.")


def validate_no_unpromoted_desktop_shelf_installers(
    desktop_files_root: Path,
    promoted_desktop_installer_files: List[str],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    if not desktop_files_root.is_dir():
        evidence["desktop_shelf_installer_candidates"] = []
        evidence["release_channel_promoted_desktop_installer_files"] = sorted(
            set(file_name for file_name in promoted_desktop_installer_files if file_name)
        )
        evidence["unpromoted_desktop_shelf_installers"] = []
        return

    desktop_shelf_installer_candidates = sorted(
        {
            item.name
            for item in desktop_files_root.iterdir()
            if item.is_file()
            and item.name.startswith("chummer-")
            and "-installer." in item.name
            and item.suffix.lower() in {".deb", ".exe", ".dmg", ".pkg"}
        }
    )
    promoted_set = {
        file_name
        for file_name in promoted_desktop_installer_files
        if file_name
    }
    unpromoted_desktop_shelf_installers = [
        file_name
        for file_name in desktop_shelf_installer_candidates
        if file_name not in promoted_set
    ]
    evidence["desktop_shelf_installer_candidates"] = desktop_shelf_installer_candidates
    evidence["release_channel_promoted_desktop_installer_files"] = sorted(promoted_set)
    evidence["unpromoted_desktop_shelf_installers"] = unpromoted_desktop_shelf_installers
    if unpromoted_desktop_shelf_installers:
        reasons.append(
            "Desktop downloads shelf contains installer artifact(s) not promoted in release-channel truth: "
            + ", ".join(unpromoted_desktop_shelf_installers)
            + "."
        )


def collect_stale_platform_gate_receipts_without_promoted_tuples(
    receipt_root: Path,
    promoted_linux_tuples: set[str],
    promoted_windows_tuples: set[str],
    promoted_macos_tuples: set[str],
    evidence: Dict[str, Any],
    reasons: List[str],
) -> None:
    linux_stale: List[Dict[str, str]] = []
    windows_stale: List[Dict[str, str]] = []
    macos_stale: List[Dict[str, str]] = []
    stale_passing: List[str] = []
    patterns = (
        ("linux", "UI_LINUX*_DESKTOP_EXIT_GATE.generated.json"),
        ("windows", "UI_WINDOWS*_DESKTOP_EXIT_GATE.generated.json"),
        ("macos", "UI_MACOS*_DESKTOP_EXIT_GATE.generated.json"),
    )
    for platform, pattern in patterns:
        for gate_path in sorted(receipt_root.glob(pattern)):
            payload = load_json(gate_path)
            head_payload = payload.get("head") if isinstance(payload.get("head"), dict) else {}
            gate_head = normalize_token(head_payload.get("app_key"))
            gate_rid = normalize_token(head_payload.get("rid"))
            if not gate_head or not gate_rid:
                continue
            tuple_key = f"{gate_head}:{gate_rid}"
            if platform == "linux":
                promoted_set = promoted_linux_tuples
            elif platform == "windows":
                promoted_set = promoted_windows_tuples
            else:
                promoted_set = promoted_macos_tuples
            if tuple_key in promoted_set:
                continue
            gate_status = normalize_token(payload.get("status"))
            record = {
                "path": str(gate_path),
                "tuple": tuple_key,
                "status": gate_status,
            }
            if platform == "linux":
                linux_stale.append(record)
            elif platform == "windows":
                windows_stale.append(record)
            else:
                macos_stale.append(record)
            if status_ok(gate_status):
                stale_passing.append(f"{platform}:{tuple_key}")
    evidence["stale_linux_gate_receipts_without_promoted_tuples"] = linux_stale
    evidence["stale_windows_gate_receipts_without_promoted_tuples"] = windows_stale
    evidence["stale_macos_gate_receipts_without_promoted_tuples"] = macos_stale
    evidence["stale_passing_platform_gate_receipts_without_promoted_tuples"] = stale_passing
    if stale_passing:
        reasons.append(
            "Stale passing platform gate receipts exist for non-promoted desktop tuples: "
            + ", ".join(stale_passing)
            + "."
        )


receipt_path, release_channel_path, linux_avalonia_gate_path, linux_blazor_gate_path, windows_gate_path_default, flagship_gate_path, visual_familiarity_gate_path, workflow_execution_gate_path, repo_root = [Path(v) for v in sys.argv[1:10]]
hub_registry_root_raw = str(sys.argv[10]).strip() if len(sys.argv) > 10 else ""
hub_registry_root = Path(hub_registry_root_raw) if hub_registry_root_raw else None
trusted_roots = [repo_root]
hub_registry_release_channel_path = None
hub_registry_root_trusted = False
if hub_registry_root is not None:
    hub_registry_release_channel_path = (
        hub_registry_root / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    )
    if (
        hub_registry_release_channel_path.is_file()
        and release_channel_path.resolve() == hub_registry_release_channel_path.resolve()
    ):
        trusted_roots.append(hub_registry_root)
        hub_registry_root_trusted = True
deduped_trusted_roots: List[Path] = []
seen_trusted_roots: set[str] = set()
for candidate_root in trusted_roots:
    candidate_resolved = str(candidate_root.resolve())
    if candidate_resolved in seen_trusted_roots:
        continue
    seen_trusted_roots.add(candidate_resolved)
    deduped_trusted_roots.append(candidate_root)
trusted_roots = deduped_trusted_roots

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "release_channel_path": str(release_channel_path),
    "linux_avalonia_gate_path": str(linux_avalonia_gate_path),
    "linux_blazor_gate_path": str(linux_blazor_gate_path),
    "windows_gate_path_default": str(windows_gate_path_default),
    "flagship_gate_path": str(flagship_gate_path),
    "visual_familiarity_gate_path": str(visual_familiarity_gate_path),
    "workflow_execution_gate_path": str(workflow_execution_gate_path),
    "repo_root": str(repo_root.resolve()),
    "hub_registry_root": str(hub_registry_root.resolve()) if hub_registry_root is not None else "",
    "hub_registry_release_channel_path": (
        str(hub_registry_release_channel_path.resolve())
        if hub_registry_release_channel_path is not None
        else ""
    ),
    "hub_registry_root_trusted_for_startup_smoke_proof": hub_registry_root_trusted,
    "trusted_local_roots": [str(root.resolve()) for root in trusted_roots],
}

release_channel = load_json(release_channel_path)
flagship_gate = load_json(flagship_gate_path)
visual_familiarity_gate = load_json(visual_familiarity_gate_path)
workflow_execution_gate = load_json(workflow_execution_gate_path)
validate_receipt_freshness("flagship UI release gate proof", flagship_gate, evidence, reasons)
validate_receipt_freshness("desktop visual familiarity gate proof", visual_familiarity_gate, evidence, reasons)
validate_receipt_freshness("desktop workflow execution gate proof", workflow_execution_gate, evidence, reasons)

flagship_status = pick_status(flagship_gate)
visual_familiarity_status = pick_status(visual_familiarity_gate)
workflow_execution_status = pick_status(workflow_execution_gate)

evidence["flagship_status"] = flagship_status
evidence["visual_familiarity_status"] = visual_familiarity_status
evidence["workflow_execution_status"] = workflow_execution_status

if not status_ok(flagship_status):
    reasons.append("Flagship UI release gate is missing or not passing.")
if not status_ok(visual_familiarity_status):
    reasons.append("Desktop visual familiarity exit gate is missing or not passing.")
if not status_ok(workflow_execution_status):
    reasons.append("Desktop workflow execution gate is missing or not passing.")
validate_receipt_path_scope(flagship_gate_path, repo_root, reasons, evidence, "flagship_gate")
validate_receipt_path_scope(visual_familiarity_gate_path, repo_root, reasons, evidence, "visual_familiarity_gate")
validate_receipt_path_scope(workflow_execution_gate_path, repo_root, reasons, evidence, "workflow_execution_gate")

visual_familiarity_evidence = (
    visual_familiarity_gate.get("evidence")
    if isinstance(visual_familiarity_gate.get("evidence"), dict)
    else {}
)
visual_screenshot_dir_raw = str(visual_familiarity_evidence.get("screenshot_dir") or "").strip()
visual_screenshot_dir = Path(visual_screenshot_dir_raw) if visual_screenshot_dir_raw else None
visual_required_screenshots = normalize_required_relative_file_list(
    visual_familiarity_evidence.get("required_screenshots"),
    "visual_familiarity.required_screenshots",
    [".png"],
    evidence,
    reasons,
)
evidence["visual_familiarity_screenshot_dir"] = visual_screenshot_dir_raw
evidence["visual_familiarity_required_screenshots"] = visual_required_screenshots
if visual_screenshot_dir is None:
    reasons.append("Desktop visual familiarity exit gate evidence is missing screenshot_dir.")
else:
    if not visual_screenshot_dir.is_dir():
        reasons.append("Desktop visual familiarity screenshot_dir does not exist on disk.")
    elif not path_within_root(visual_screenshot_dir, repo_root):
        reasons.append("Desktop visual familiarity screenshot_dir is outside this repo root.")
if not visual_required_screenshots:
    reasons.append("Desktop visual familiarity exit gate evidence is missing required_screenshots.")
else:
    missing_visual_screenshots = [
        name
        for name in visual_required_screenshots
        if visual_screenshot_dir is None or not (visual_screenshot_dir / name).is_file()
    ]
    evidence["visual_familiarity_missing_screenshots_now"] = missing_visual_screenshots
    if missing_visual_screenshots:
        reasons.append(
            "Desktop visual familiarity required screenshots are missing on disk: "
            + ", ".join(missing_visual_screenshots)
        )
    screenshot_generated_at_raw, screenshot_generated_at = payload_generated_at(visual_familiarity_gate)
    evidence["visual_familiarity_screenshot_reference_generated_at"] = screenshot_generated_at_raw
    screenshot_file_timestamps: Dict[str, str] = {}
    screenshot_stale_reasons: List[str] = []
    screenshot_older_than_receipt: List[str] = []
    screenshot_newer_than_receipt: List[str] = []
    for name in visual_required_screenshots:
        if visual_screenshot_dir is None:
            continue
        screenshot_path = visual_screenshot_dir / name
        if not screenshot_path.is_file():
            continue
        screenshot_mtime = datetime.fromtimestamp(screenshot_path.stat().st_mtime, timezone.utc)
        screenshot_mtime_raw = screenshot_mtime.isoformat().replace("+00:00", "Z")
        screenshot_file_timestamps[name] = screenshot_mtime_raw
        screenshot_age_seconds = max(0, int((datetime.now(timezone.utc) - screenshot_mtime).total_seconds()))
        if screenshot_age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
            screenshot_stale_reasons.append(f"{name} ({screenshot_age_seconds}s old)")
        if screenshot_generated_at is not None:
            skew_seconds = int((screenshot_generated_at - screenshot_mtime).total_seconds())
            if skew_seconds > VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS:
                screenshot_older_than_receipt.append(f"{name} ({skew_seconds}s older)")
            elif abs(skew_seconds) > VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS:
                screenshot_newer_than_receipt.append(
                    f"{name} ({abs(skew_seconds)}s newer)"
                )
    evidence["visual_familiarity_screenshot_file_timestamps"] = screenshot_file_timestamps
    evidence["visual_familiarity_stale_screenshots"] = screenshot_stale_reasons
    evidence["visual_familiarity_screenshots_older_than_receipt"] = screenshot_older_than_receipt
    evidence["visual_familiarity_screenshots_newer_than_receipt"] = screenshot_newer_than_receipt
    if screenshot_stale_reasons:
        reasons.append(
            "Desktop visual familiarity required screenshots are stale: "
            + ", ".join(screenshot_stale_reasons)
        )
    if screenshot_older_than_receipt:
        reasons.append(
            "Desktop visual familiarity screenshot evidence predates the visual familiarity receipt generation time: "
            + ", ".join(screenshot_older_than_receipt)
        )
    if screenshot_newer_than_receipt:
        reasons.append(
            "Desktop visual familiarity screenshot evidence is newer than the visual familiarity receipt generation time: "
            + ", ".join(screenshot_newer_than_receipt)
        )

raw_artifacts = release_channel.get("artifacts")
artifacts: List[Dict[str, Any]] = []
non_object_artifact_indexes: List[int] = []
if raw_artifacts is None:
    pass
elif not isinstance(raw_artifacts, list):
    reasons.append("Release channel artifacts must be a list when present.")
else:
    for artifact_index, artifact_item in enumerate(raw_artifacts):
        if not isinstance(artifact_item, dict):
            non_object_artifact_indexes.append(artifact_index)
            reasons.append(
                f"Release channel artifacts contains a non-object item at index {artifact_index}."
            )
            continue
        artifacts.append(artifact_item)
evidence["release_channel_artifacts_total_count"] = len(raw_artifacts) if isinstance(raw_artifacts, list) else 0
evidence["release_channel_artifacts_object_count"] = len(artifacts)
evidence["release_channel_artifacts_non_object_indexes"] = non_object_artifact_indexes
release_channel_status = normalize_optional_string_scalar(
    release_channel.get("status"),
    "release_channel.status",
    evidence,
    reasons,
    required=True,
)
release_channel_channel_id_primary = normalize_optional_string_scalar(
    release_channel.get("channelId"),
    "release_channel.channelId",
    evidence,
    reasons,
)
release_channel_channel_id_fallback = normalize_optional_string_scalar(
    release_channel.get("channel"),
    "release_channel.channel",
    evidence,
    reasons,
)
if (
    release_channel_channel_id_primary
    and release_channel_channel_id_fallback
    and release_channel_channel_id_primary != release_channel_channel_id_fallback
):
    reasons.append(
        "release_channel.channelId and release_channel.channel disagree after normalization."
    )
release_channel_channel_id = (
    release_channel_channel_id_primary or release_channel_channel_id_fallback
)
release_channel_version = normalize_optional_string_scalar(
    release_channel.get("version"),
    "release_channel.version",
    evidence,
    reasons,
    lowercase=False,
    required=True,
)

evidence["release_channel_status"] = release_channel_status
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version
release_channel_generated_at_raw, release_channel_generated_at = payload_generated_at(release_channel)
evidence["release_channel_generated_at"] = release_channel_generated_at_raw
evidence["release_channel_freshness_max_age_seconds"] = RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS
evidence["release_channel_freshness_max_future_skew_seconds"] = RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    reasons.append("Release channel is missing a valid generated_at timestamp.")
else:
    release_channel_age_delta_seconds = int((datetime.now(timezone.utc) - release_channel_generated_at).total_seconds())
    release_channel_age_seconds = max(0, release_channel_age_delta_seconds)
    if release_channel_age_delta_seconds < 0:
        release_channel_future_skew_seconds = abs(release_channel_age_delta_seconds)
        evidence["release_channel_future_skew_seconds"] = release_channel_future_skew_seconds
        if release_channel_future_skew_seconds > RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS:
            reasons.append(
                "Release channel generated_at is in the future "
                f"({release_channel_future_skew_seconds}s ahead; max {RELEASE_CHANNEL_PROOF_MAX_FUTURE_SKEW_SECONDS}s)."
            )
    evidence["release_channel_age_seconds"] = release_channel_age_seconds
    if release_channel_age_seconds > RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS:
        reasons.append(
            f"Release channel receipt is stale ({release_channel_age_seconds}s old; max {RELEASE_CHANNEL_PROOF_MAX_AGE_SECONDS}s)."
        )
desktop_tuple_coverage = (
    release_channel.get("desktopTupleCoverage")
    if isinstance(release_channel.get("desktopTupleCoverage"), dict)
    else {}
)
desktop_tuple_coverage_present = isinstance(release_channel.get("desktopTupleCoverage"), dict)
desktop_platform_tokens = {"linux", "windows", "macos"}
tuple_coverage_required_desktop_platforms = normalize_required_token_list(
    desktop_tuple_coverage.get("requiredDesktopPlatforms"),
    "desktopTupleCoverage.requiredDesktopPlatforms",
    evidence,
    reasons,
)
tuple_coverage_required_desktop_heads = normalize_required_token_list(
    desktop_tuple_coverage.get("requiredDesktopHeads"),
    "desktopTupleCoverage.requiredDesktopHeads",
    evidence,
    reasons,
)
tuple_coverage_promoted_platform_heads = normalize_promoted_platform_heads(
    desktop_tuple_coverage.get("promotedPlatformHeads"),
    "desktopTupleCoverage.promotedPlatformHeads",
    desktop_platform_tokens,
    evidence,
    reasons,
)
tuple_coverage_reported_missing_platform_head_pairs = normalize_required_tuple_list(
    desktop_tuple_coverage.get("missingRequiredPlatformHeadPairs"),
    "desktopTupleCoverage.missingRequiredPlatformHeadPairs",
    2,
    None,
    evidence,
    reasons,
)
tuple_coverage_reported_missing_platforms = normalize_required_token_list(
    desktop_tuple_coverage.get("missingRequiredPlatforms"),
    "desktopTupleCoverage.missingRequiredPlatforms",
    evidence,
    reasons,
)
tuple_coverage_reported_missing_heads = normalize_required_token_list(
    desktop_tuple_coverage.get("missingRequiredHeads"),
    "desktopTupleCoverage.missingRequiredHeads",
    evidence,
    reasons,
)
tuple_coverage_required_platform_head_rid_tuples = normalize_required_tuple_list(
    desktop_tuple_coverage.get("requiredDesktopPlatformHeadRidTuples"),
    "desktopTupleCoverage.requiredDesktopPlatformHeadRidTuples",
    3,
    desktop_platform_tokens,
    evidence,
    reasons,
)
tuple_coverage_reported_promoted_platform_head_rid_tuples = normalize_required_tuple_list(
    desktop_tuple_coverage.get("promotedPlatformHeadRidTuples"),
    "desktopTupleCoverage.promotedPlatformHeadRidTuples",
    3,
    desktop_platform_tokens,
    evidence,
    reasons,
)
tuple_coverage_reported_missing_platform_head_rid_tuples = normalize_required_tuple_list(
    desktop_tuple_coverage.get("missingRequiredPlatformHeadRidTuples"),
    "desktopTupleCoverage.missingRequiredPlatformHeadRidTuples",
    3,
    desktop_platform_tokens,
    evidence,
    reasons,
)
tuple_coverage_declares_missing_required_platform_head_pairs = (
    "missingRequiredPlatformHeadPairs" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
tuple_coverage_declares_missing_required_platforms = (
    "missingRequiredPlatforms" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
tuple_coverage_declares_missing_required_heads = (
    "missingRequiredHeads" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
tuple_coverage_declares_required_platform_head_rid_tuples = (
    "requiredDesktopPlatformHeadRidTuples" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
tuple_coverage_declares_promoted_platform_head_rid_tuples = (
    "promotedPlatformHeadRidTuples" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
tuple_coverage_declares_missing_required_platform_head_rid_tuples = (
    "missingRequiredPlatformHeadRidTuples" in desktop_tuple_coverage
    if desktop_tuple_coverage_present
    else False
)
evidence["release_channel_tuple_coverage_required_desktop_platforms"] = tuple_coverage_required_desktop_platforms
evidence["release_channel_tuple_coverage_required_desktop_heads"] = tuple_coverage_required_desktop_heads
evidence["release_channel_tuple_coverage_promoted_platform_heads"] = tuple_coverage_promoted_platform_heads
evidence["release_channel_tuple_coverage_reported_missing_required_platform_head_pairs"] = (
    tuple_coverage_reported_missing_platform_head_pairs
)
evidence["release_channel_tuple_coverage_reported_missing_required_platforms"] = (
    tuple_coverage_reported_missing_platforms
)
evidence["release_channel_tuple_coverage_reported_missing_required_heads"] = (
    tuple_coverage_reported_missing_heads
)
evidence["release_channel_tuple_coverage_required_platform_head_rid_tuples"] = (
    tuple_coverage_required_platform_head_rid_tuples
)
evidence["release_channel_tuple_coverage_promoted_platform_head_rid_tuples"] = (
    tuple_coverage_reported_promoted_platform_head_rid_tuples
)
evidence["release_channel_tuple_coverage_reported_missing_required_platform_head_rid_tuples"] = (
    tuple_coverage_reported_missing_platform_head_rid_tuples
)
evidence["release_channel_tuple_coverage_present"] = desktop_tuple_coverage_present
evidence["release_channel_tuple_coverage_declares_missing_required_platform_head_pairs"] = (
    tuple_coverage_declares_missing_required_platform_head_pairs
)
evidence["release_channel_tuple_coverage_declares_missing_required_platforms"] = (
    tuple_coverage_declares_missing_required_platforms
)
evidence["release_channel_tuple_coverage_declares_missing_required_heads"] = (
    tuple_coverage_declares_missing_required_heads
)
evidence["release_channel_tuple_coverage_declares_required_platform_head_rid_tuples"] = (
    tuple_coverage_declares_required_platform_head_rid_tuples
)
evidence["release_channel_tuple_coverage_declares_promoted_platform_head_rid_tuples"] = (
    tuple_coverage_declares_promoted_platform_head_rid_tuples
)
evidence["release_channel_tuple_coverage_declares_missing_required_platform_head_rid_tuples"] = (
    tuple_coverage_declares_missing_required_platform_head_rid_tuples
)

if not release_channel_channel_id:
    reasons.append("Release channel is missing channelId, so installer/update truth cannot be aligned by channel.")
if not release_channel_version:
    reasons.append("Release channel is missing version, so installer/update truth cannot be aligned by release head.")
if release_channel_status not in {"published", "ready", "pass", "passed"}:
    reasons.append("Release channel status is not in a publishable state for desktop executable proof.")
release_channel_rollout_state = normalize_optional_string_scalar(
    release_channel.get("rolloutState"),
    "release_channel.rolloutState",
    evidence,
    reasons,
)
release_channel_supportability_state = normalize_optional_string_scalar(
    release_channel.get("supportabilityState"),
    "release_channel.supportabilityState",
    evidence,
    reasons,
)
evidence["release_channel_rollout_state"] = release_channel_rollout_state
evidence["release_channel_supportability_state"] = release_channel_supportability_state

desktop_install_artifacts = [
    item for item in artifacts
    if normalize_token(item.get("platform")) in {"linux", "windows", "macos"}
    and is_desktop_install_media(item.get("platform"), item.get("kind"))
]
if desktop_install_artifacts and not desktop_tuple_coverage_present:
    reasons.append(
        "Release channel is missing desktopTupleCoverage metadata for promoted desktop install artifacts."
    )
if desktop_install_artifacts and not tuple_coverage_required_desktop_platforms:
    reasons.append(
        "Release channel desktopTupleCoverage is missing requiredDesktopPlatforms for desktop install media."
    )
if desktop_install_artifacts and not tuple_coverage_required_desktop_heads:
    reasons.append(
        "Release channel desktopTupleCoverage is missing requiredDesktopHeads for desktop install media."
    )
if desktop_install_artifacts and not tuple_coverage_promoted_platform_heads:
    reasons.append(
        "Release channel desktopTupleCoverage is missing promotedPlatformHeads mapping for desktop install media."
    )
if desktop_install_artifacts and not tuple_coverage_declares_missing_required_platform_head_pairs:
    reasons.append(
        "Release channel desktopTupleCoverage must declare missingRequiredPlatformHeadPairs explicitly (empty list when complete)."
    )
if desktop_install_artifacts and not tuple_coverage_declares_missing_required_platforms:
    reasons.append(
        "Release channel desktopTupleCoverage must declare missingRequiredPlatforms explicitly (empty list when complete)."
    )
if desktop_install_artifacts and not tuple_coverage_declares_missing_required_heads:
    reasons.append(
        "Release channel desktopTupleCoverage must declare missingRequiredHeads explicitly (empty list when complete)."
    )
if desktop_install_artifacts and not tuple_coverage_declares_required_platform_head_rid_tuples:
    reasons.append(
        "Release channel desktopTupleCoverage must declare requiredDesktopPlatformHeadRidTuples explicitly for architecture-aware desktop install coverage."
    )
if desktop_install_artifacts and not tuple_coverage_declares_promoted_platform_head_rid_tuples:
    reasons.append(
        "Release channel desktopTupleCoverage must declare promotedPlatformHeadRidTuples explicitly for architecture-aware desktop install coverage."
    )
if desktop_install_artifacts and not tuple_coverage_declares_missing_required_platform_head_rid_tuples:
    reasons.append(
        "Release channel desktopTupleCoverage must declare missingRequiredPlatformHeadRidTuples explicitly (empty list when complete)."
    )
required_desktop_platforms = ("linux", "windows", "macos")
platform_artifact_counts = {
    platform: len(
        [
            item for item in desktop_install_artifacts
            if normalize_token(item.get("platform")) == platform
        ]
    )
    for platform in required_desktop_platforms
}
platform_heads_from_release_channel = {
    platform: sorted(
        {
            normalize_token(item.get("head"))
            for item in desktop_install_artifacts
            if normalize_token(item.get("platform")) == platform and normalize_token(item.get("head"))
        }
    )
    for platform in required_desktop_platforms
}
evidence["required_desktop_platforms"] = list(required_desktop_platforms)
evidence["platform_artifact_counts"] = platform_artifact_counts
evidence["platform_heads_from_release_channel"] = platform_heads_from_release_channel
desktop_files_root = repo_root / "Docker" / "Downloads" / "files"
evidence["desktop_files_root"] = str(desktop_files_root)
for required_platform in required_desktop_platforms:
    if platform_artifact_counts.get(required_platform, 0) < 1:
        reasons.append(
            f"Release channel does not publish desktop install media for required platform '{required_platform}'."
        )
for desktop_install_artifact in desktop_install_artifacts:
    validate_local_release_artifact_file(desktop_install_artifact, desktop_files_root, evidence, reasons)
promoted_desktop_installer_files = [
    str(item.get("fileName") or "").strip()
    for item in desktop_install_artifacts
    if str(item.get("fileName") or "").strip()
]
validate_no_unpromoted_desktop_shelf_installers(
    desktop_files_root,
    promoted_desktop_installer_files,
    evidence,
    reasons,
)

expected_windows_artifacts = [
    item
    for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "windows"
    and normalize_token(item.get("head"))
    and normalize_token(item.get("rid"))
]
windows_artifacts_missing_rid_by_head = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) == "windows"
        and normalize_token(item.get("head"))
        and not normalize_token(item.get("rid"))
    }
)
evidence["windows_artifacts_missing_rid_by_head"] = windows_artifacts_missing_rid_by_head
for missing_rid_head in windows_artifacts_missing_rid_by_head:
    reasons.append(
        f"Release channel publishes Windows desktop media for head '{missing_rid_head}' without explicit head/rid tuple metadata."
    )
windows_artifact_map_by_tuple: Dict[tuple[str, str], Dict[str, Any]] = {
    (
        normalize_token(item.get("head")),
        normalize_token(item.get("rid")),
    ): item
    for item in expected_windows_artifacts
}
required_windows_policy_tuples = sorted(
    {
        (head, rid)
        for token in tuple_coverage_required_platform_head_rid_tuples
        for head, rid, platform in [tuple(token.split(":", 2))]
        if platform == "windows" and head and rid
    }
)
windows_policy_tuples_missing_release_artifacts: List[str] = []
for head, rid in required_windows_policy_tuples:
    tuple_key = (head, rid)
    if tuple_key in windows_artifact_map_by_tuple:
        continue
    windows_policy_tuples_missing_release_artifacts.append(f"{head}:{rid}")
    expected_windows_artifacts.append(
        {
            "head": head,
            "rid": rid,
            "platform": "windows",
            "kind": "installer",
            "fileName": "",
            "sha256": "",
            "sizeBytes": 0,
            "source": "required_tuple_policy_missing_release_artifact",
        }
    )
promoted_windows_tuples = {
    f"{normalize_token(item.get('head'))}:{normalize_token(item.get('rid'))}"
    for item in expected_windows_artifacts
}
evidence["windows_heads_expected"] = [
    {
        "head": normalize_token(item.get("head")),
        "rid": normalize_token(item.get("rid")),
        "fileName": str(item.get("fileName") or "").strip(),
    }
    for item in expected_windows_artifacts
]
evidence["windows_policy_required_head_rid_tuples"] = [
    f"{head}:{rid}" for head, rid in required_windows_policy_tuples
]
evidence["windows_policy_tuples_missing_release_artifacts"] = (
    windows_policy_tuples_missing_release_artifacts
)
if not expected_windows_artifacts and platform_artifact_counts.get("windows", 0) > 0:
    reasons.append("Release channel publishes Windows desktop media without explicit head/rid tuple metadata.")
windows_statuses: Dict[str, str] = {}
for expected_windows_artifact in expected_windows_artifacts:
    expected_windows_head = normalize_token(expected_windows_artifact.get("head"))
    expected_windows_rid = normalize_token(expected_windows_artifact.get("rid"))
    gate_label = f"{expected_windows_head}:{expected_windows_rid}"
    gate_path = windows_gate_path_for_head(
        expected_windows_head,
        expected_windows_rid,
        receipt_path.parent,
        windows_gate_path_default,
    )
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"windows_gate:{gate_label}")
    reason_count_before = len(reasons)
    validate_windows_gate(
        gate_label,
        gate_path,
        load_json(gate_path),
        expected_windows_artifact,
        release_channel_channel_id,
        release_channel_version,
        desktop_files_root,
        repo_root,
        trusted_roots,
        evidence,
        reasons,
    )
    windows_statuses[gate_label] = "pass" if len(reasons) == reason_count_before else "fail"
evidence["windows_statuses"] = windows_statuses

expected_linux_artifacts = [
    item
    for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "linux"
    and normalize_token(item.get("head"))
    and normalize_token(item.get("rid"))
]
linux_artifacts_missing_rid_by_head = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) == "linux"
        and normalize_token(item.get("head"))
        and not normalize_token(item.get("rid"))
    }
)
evidence["linux_artifacts_missing_rid_by_head"] = linux_artifacts_missing_rid_by_head
for missing_rid_head in linux_artifacts_missing_rid_by_head:
    reasons.append(
        f"Release channel publishes Linux desktop media for head '{missing_rid_head}' without explicit head/rid tuple metadata."
    )
linux_artifact_map_by_tuple: Dict[tuple[str, str], Dict[str, Any]] = {
    (
        normalize_token(item.get("head")),
        normalize_token(item.get("rid")),
    ): item
    for item in expected_linux_artifacts
}
required_linux_policy_tuples = sorted(
    {
        (head, rid)
        for token in tuple_coverage_required_platform_head_rid_tuples
        for head, rid, platform in [tuple(token.split(":", 2))]
        if platform == "linux" and head and rid
    }
)
linux_policy_tuples_missing_release_artifacts: List[str] = []
for head, rid in required_linux_policy_tuples:
    tuple_key = (head, rid)
    if tuple_key in linux_artifact_map_by_tuple:
        continue
    linux_policy_tuples_missing_release_artifacts.append(f"{head}:{rid}")
    expected_linux_artifacts.append(
        {
            "head": head,
            "rid": rid,
            "platform": "linux",
            "kind": "installer",
            "fileName": "",
            "sha256": "",
            "sizeBytes": 0,
            "source": "required_tuple_policy_missing_release_artifact",
        }
    )
promoted_linux_tuples = {
    f"{normalize_token(item.get('head'))}:{normalize_token(item.get('rid'))}"
    for item in expected_linux_artifacts
}
evidence["linux_heads_expected"] = [
    {
        "head": normalize_token(item.get("head")),
        "rid": normalize_token(item.get("rid")),
        "fileName": str(item.get("fileName") or "").strip(),
    }
    for item in expected_linux_artifacts
]
evidence["linux_policy_required_head_rid_tuples"] = [
    f"{head}:{rid}" for head, rid in required_linux_policy_tuples
]
evidence["linux_policy_tuples_missing_release_artifacts"] = (
    linux_policy_tuples_missing_release_artifacts
)
if not expected_linux_artifacts and platform_artifact_counts.get("linux", 0) > 0:
    reasons.append("Release channel publishes Linux desktop media without explicit head/rid tuple metadata.")

promoted_desktop_heads = sorted(
    {
        normalize_token(item.get("head"))
        for item in desktop_install_artifacts
        if normalize_token(item.get("platform")) in {"linux", "windows", "macos"}
        and normalize_token(item.get("head"))
    }
)
canonical_required_desktop_heads = ["avalonia", "blazor-desktop"]
flagship_required_desktop_heads_source = flagship_gate.get("desktopHeads")
if flagship_required_desktop_heads_source is None and "desktopHead" in flagship_gate:
    flagship_required_desktop_heads_source = [flagship_gate.get("desktopHead")]
flagship_required_desktop_heads = sorted(
    set(
        normalize_required_token_list(
            flagship_required_desktop_heads_source,
            "flagship.desktop_heads",
            evidence,
            reasons,
        )
    )
)
if not promoted_desktop_heads:
    reasons.append("Release channel does not publish any promoted desktop install media artifacts.")
if not flagship_required_desktop_heads:
    reasons.append("Flagship UI release gate is missing required desktopHeads desktop head inventory.")
evidence["promoted_desktop_heads"] = promoted_desktop_heads
evidence["flagship_required_desktop_heads"] = flagship_required_desktop_heads
evidence["canonical_required_desktop_heads"] = canonical_required_desktop_heads
missing_canonical_promoted_heads = [
    head for head in canonical_required_desktop_heads
    if head not in promoted_desktop_heads
]
missing_canonical_flagship_heads = [
    head for head in canonical_required_desktop_heads
    if head not in flagship_required_desktop_heads
]
evidence["missing_canonical_promoted_desktop_heads"] = missing_canonical_promoted_heads
evidence["missing_canonical_flagship_desktop_heads"] = missing_canonical_flagship_heads
if missing_canonical_promoted_heads:
    reasons.append(
        "Release channel is missing canonical required promoted desktop head(s) for milestone-3 executable proof: "
        + ", ".join(missing_canonical_promoted_heads)
        + "."
    )
if missing_canonical_flagship_heads:
    reasons.append(
        "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 executable proof: "
        + ", ".join(missing_canonical_flagship_heads)
        + "."
    )
missing_required_promoted_heads = [
    head for head in flagship_required_desktop_heads
    if head not in promoted_desktop_heads
]
evidence["missing_promoted_desktop_heads"] = missing_required_promoted_heads
if missing_required_promoted_heads:
    reasons.append(
        "Release channel is missing promoted desktop install media for flagship-required head(s): "
        + ", ".join(missing_required_promoted_heads)
        + "."
    )
heads_requiring_flagship_proof = sorted(
    set(promoted_desktop_heads)
    .union(set(flagship_required_desktop_heads))
    .union(set(canonical_required_desktop_heads))
)
evidence["heads_requiring_flagship_proof"] = heads_requiring_flagship_proof
tuple_coverage_missing_required_platforms_from_policy = sorted(
    set(required_desktop_platforms).difference(set(tuple_coverage_required_desktop_platforms))
)
tuple_coverage_missing_required_heads_from_policy = sorted(
    set(heads_requiring_flagship_proof).difference(set(tuple_coverage_required_desktop_heads))
)
tuple_coverage_missing_canonical_required_heads = sorted(
    set(canonical_required_desktop_heads).difference(set(tuple_coverage_required_desktop_heads))
)
evidence["release_channel_tuple_coverage_missing_required_platforms_from_policy"] = (
    tuple_coverage_missing_required_platforms_from_policy
)
evidence["release_channel_tuple_coverage_missing_required_heads_from_policy"] = (
    tuple_coverage_missing_required_heads_from_policy
)
evidence["release_channel_tuple_coverage_missing_canonical_required_heads"] = (
    tuple_coverage_missing_canonical_required_heads
)
if desktop_install_artifacts and tuple_coverage_missing_required_platforms_from_policy:
    reasons.append(
        "Release channel desktopTupleCoverage requiredDesktopPlatforms is missing required policy platform(s): "
        + ", ".join(tuple_coverage_missing_required_platforms_from_policy)
        + "."
    )
if desktop_install_artifacts and tuple_coverage_missing_required_heads_from_policy:
    reasons.append(
        "Release channel desktopTupleCoverage requiredDesktopHeads is missing required policy head(s): "
        + ", ".join(tuple_coverage_missing_required_heads_from_policy)
        + "."
    )
if desktop_install_artifacts and tuple_coverage_missing_canonical_required_heads:
    reasons.append(
        "Release channel desktopTupleCoverage requiredDesktopHeads is missing canonical required head(s): "
        + ", ".join(tuple_coverage_missing_canonical_required_heads)
        + "."
    )
required_platforms_for_pair_matrix = tuple_coverage_required_desktop_platforms or list(required_desktop_platforms)
required_heads_for_pair_matrix = tuple_coverage_required_desktop_heads or heads_requiring_flagship_proof
promoted_platform_heads_for_pair_matrix: Dict[str, List[str]] = {}
for platform in required_platforms_for_pair_matrix:
    promoted_platform_heads_for_pair_matrix[platform] = (
        tuple_coverage_promoted_platform_heads.get(platform)
        or platform_heads_from_release_channel.get(platform, [])
    )
missing_required_platform_head_pairs_derived = sorted(
    {
        f"{head}:{platform}"
        for platform in required_platforms_for_pair_matrix
        for head in required_heads_for_pair_matrix
        if head and head not in set(promoted_platform_heads_for_pair_matrix.get(platform, []))
    }
)
missing_required_platform_head_pairs = (
    tuple_coverage_reported_missing_platform_head_pairs
    if tuple_coverage_reported_missing_platform_head_pairs
    else missing_required_platform_head_pairs_derived
)
missing_required_platform_head_pairs_by_platform: Dict[str, List[str]] = {
    "linux": [],
    "windows": [],
    "macos": [],
}
for pair in missing_required_platform_head_pairs:
    pair_token = normalize_token(pair)
    if ":" not in pair_token:
        continue
    head_token, platform_token = pair_token.split(":", 1)
    if not head_token or platform_token not in missing_required_platform_head_pairs_by_platform:
        continue
    missing_required_platform_head_pairs_by_platform[platform_token].append(pair_token)
for platform_token in missing_required_platform_head_pairs_by_platform:
    missing_required_platform_head_pairs_by_platform[platform_token] = sorted(
        set(missing_required_platform_head_pairs_by_platform[platform_token])
    )
tuple_coverage_missing_pair_inventory_mismatch = sorted(
    set(tuple_coverage_reported_missing_platform_head_pairs).symmetric_difference(
        set(missing_required_platform_head_pairs_derived)
    )
)
evidence["required_desktop_platform_head_pair_platforms"] = required_platforms_for_pair_matrix
evidence["required_desktop_platform_head_pair_heads"] = required_heads_for_pair_matrix
evidence["promoted_platform_heads_for_required_pair_matrix"] = promoted_platform_heads_for_pair_matrix
evidence["missing_required_desktop_platform_head_pairs"] = missing_required_platform_head_pairs
evidence["missing_required_desktop_platform_head_pairs_derived"] = (
    missing_required_platform_head_pairs_derived
)
evidence["missing_required_desktop_platform_head_pairs_by_platform"] = (
    missing_required_platform_head_pairs_by_platform
)
required_platform_head_rid_tuples_from_artifacts = sorted(
    {
        build_platform_head_rid_tuple(item.get("head"), item.get("rid"), item.get("platform"))
        for item in desktop_install_artifacts
        if build_platform_head_rid_tuple(item.get("head"), item.get("rid"), item.get("platform"))
    }
)
required_platform_head_pairs_from_required_rid_tuples = sorted(
    {
        f"{tuple_token.split(':', 2)[0]}:{tuple_token.split(':', 2)[2]}"
        for tuple_token in tuple_coverage_required_platform_head_rid_tuples
        if tuple_token.count(":") == 2
    }
)
required_platform_head_pairs_for_matrix = sorted(
    {
        f"{head}:{platform}"
        for platform in required_platforms_for_pair_matrix
        for head in required_heads_for_pair_matrix
        if head
    }
)
missing_required_platform_head_pairs_from_required_rid_tuples = sorted(
    set(required_platform_head_pairs_for_matrix).difference(
        set(required_platform_head_pairs_from_required_rid_tuples)
    )
)
missing_required_platform_head_rid_tuples_derived = sorted(
    {
        tuple_token
        for tuple_token in tuple_coverage_required_platform_head_rid_tuples
        if tuple_token and tuple_token not in set(required_platform_head_rid_tuples_from_artifacts)
    }
)
tuple_coverage_promoted_platform_head_rid_tuple_inventory_mismatch = sorted(
    set(tuple_coverage_reported_promoted_platform_head_rid_tuples).symmetric_difference(
        set(required_platform_head_rid_tuples_from_artifacts)
    )
)
tuple_coverage_missing_platform_head_rid_tuple_inventory_mismatch = sorted(
    set(tuple_coverage_reported_missing_platform_head_rid_tuples).symmetric_difference(
        set(missing_required_platform_head_rid_tuples_derived)
    )
)
evidence["release_channel_promoted_platform_head_rid_tuples_from_artifacts"] = (
    required_platform_head_rid_tuples_from_artifacts
)
evidence["release_channel_required_platform_head_pairs_for_matrix"] = (
    required_platform_head_pairs_for_matrix
)
evidence["release_channel_required_platform_head_pairs_from_required_rid_tuples"] = (
    required_platform_head_pairs_from_required_rid_tuples
)
evidence["release_channel_missing_required_platform_head_pairs_from_required_rid_tuples"] = (
    missing_required_platform_head_pairs_from_required_rid_tuples
)
evidence["release_channel_missing_required_platform_head_rid_tuples_derived"] = (
    missing_required_platform_head_rid_tuples_derived
)
evidence["release_channel_tuple_coverage_promoted_platform_head_rid_tuple_inventory_mismatch"] = (
    tuple_coverage_promoted_platform_head_rid_tuple_inventory_mismatch
)
evidence["release_channel_tuple_coverage_missing_platform_head_rid_tuple_inventory_mismatch"] = (
    tuple_coverage_missing_platform_head_rid_tuple_inventory_mismatch
)
missing_required_platforms_derived = sorted(
    {
        platform
        for platform in required_platforms_for_pair_matrix
        if platform not in platform_heads_from_release_channel
        or not platform_heads_from_release_channel.get(platform)
    }
)
missing_required_heads_derived = sorted(
    {
        head
        for head in required_heads_for_pair_matrix
        if head and head not in promoted_desktop_heads
    }
)
tuple_coverage_missing_platform_inventory_mismatch = sorted(
    set(tuple_coverage_reported_missing_platforms).symmetric_difference(
        set(missing_required_platforms_derived)
    )
)
tuple_coverage_missing_head_inventory_mismatch = sorted(
    set(tuple_coverage_reported_missing_heads).symmetric_difference(
        set(missing_required_heads_derived)
    )
)
evidence["missing_required_desktop_platforms_derived"] = missing_required_platforms_derived
evidence["missing_required_desktop_heads_derived"] = missing_required_heads_derived
evidence["release_channel_tuple_coverage_missing_platform_inventory_mismatch"] = (
    tuple_coverage_missing_platform_inventory_mismatch
)
evidence["release_channel_tuple_coverage_missing_head_inventory_mismatch"] = (
    tuple_coverage_missing_head_inventory_mismatch
)
evidence["release_channel_tuple_coverage_missing_pair_inventory_mismatch"] = (
    tuple_coverage_missing_pair_inventory_mismatch
)
coverage_incomplete = bool(
    missing_required_platforms_derived
    or missing_required_heads_derived
    or missing_required_platform_head_pairs
)
evidence["release_channel_desktop_tuple_coverage_incomplete"] = coverage_incomplete
if tuple_coverage_missing_pair_inventory_mismatch:
    reasons.append(
        "Release channel desktopTupleCoverage missingRequiredPlatformHeadPairs inventory does not match promoted installer tuples."
    )
if tuple_coverage_missing_platform_inventory_mismatch:
    reasons.append(
        "Release channel desktopTupleCoverage missingRequiredPlatforms inventory does not match promoted installer tuples."
    )
if tuple_coverage_missing_head_inventory_mismatch:
    reasons.append(
        "Release channel desktopTupleCoverage missingRequiredHeads inventory does not match promoted installer tuples."
    )
if tuple_coverage_promoted_platform_head_rid_tuple_inventory_mismatch:
    reasons.append(
        "Release channel desktopTupleCoverage promotedPlatformHeadRidTuples inventory does not match promoted installer tuples."
    )
if tuple_coverage_missing_platform_head_rid_tuple_inventory_mismatch:
    reasons.append(
        "Release channel desktopTupleCoverage missingRequiredPlatformHeadRidTuples inventory does not match promoted installer tuples."
    )
if missing_required_platform_head_pairs_from_required_rid_tuples:
    reasons.append(
        "Release channel desktopTupleCoverage requiredDesktopPlatformHeadRidTuples is missing required desktop platform/head pair coverage: "
        + ", ".join(missing_required_platform_head_pairs_from_required_rid_tuples)
        + "."
    )
if missing_required_platform_head_pairs:
    reasons.append(
        "Release channel is missing required desktop platform/head installer tuple pair(s): "
        + ", ".join(missing_required_platform_head_pairs)
        + "."
    )
if missing_required_platform_head_rid_tuples_derived:
    reasons.append(
        "Release channel is missing required desktop platform/head/rid installer tuple(s): "
        + ", ".join(missing_required_platform_head_rid_tuples_derived)
        + "."
    )
if coverage_incomplete and release_channel_rollout_state != "coverage_incomplete":
    reasons.append(
        "Release channel must set rolloutState=coverage_incomplete when required desktop tuple coverage is incomplete."
    )
if coverage_incomplete and release_channel_supportability_state != "review_required":
    reasons.append(
        "Release channel must set supportabilityState=review_required when required desktop tuple coverage is incomplete."
    )

visual_required_heads = normalize_required_token_list(
    visual_familiarity_evidence.get("flagship_required_desktop_heads"),
    "visual_familiarity.flagship_required_desktop_heads",
    evidence,
    reasons,
)
visual_head_statuses_raw = (
    visual_familiarity_evidence.get("flagship_head_proof_statuses")
    if isinstance(visual_familiarity_evidence.get("flagship_head_proof_statuses"), dict)
    else {}
)
visual_head_statuses = normalize_required_status_map(
    visual_head_statuses_raw,
    "visual_familiarity.flagship_head_proof_statuses",
    evidence,
    reasons,
)
if not visual_head_statuses:
    # Backward-compatible fallback for older visual receipts.
    visual_head_statuses = {
        "avalonia": normalize_token(visual_familiarity_evidence.get("flagship_avalonia_head_proof_status")),
        "blazor-desktop": normalize_token(visual_familiarity_evidence.get("flagship_blazor_head_proof_status")),
    }
visual_head_contract_marker_statuses_raw = (
    visual_familiarity_evidence.get("flagship_head_contract_marker_statuses")
    if isinstance(visual_familiarity_evidence.get("flagship_head_contract_marker_statuses"), dict)
    else {}
)
visual_head_contract_marker_statuses = {
    normalize_token(head): {
        normalize_token(marker): normalize_token(status)
        for marker, status in marker_statuses.items()
        if normalize_token(marker)
    }
    for head, marker_statuses in visual_head_contract_marker_statuses_raw.items()
    if normalize_token(head) and isinstance(marker_statuses, dict)
}
visual_head_missing_contract_markers_raw = (
    visual_familiarity_evidence.get("flagship_head_missing_contract_markers")
    if isinstance(visual_familiarity_evidence.get("flagship_head_missing_contract_markers"), dict)
    else {}
)
visual_head_missing_contract_markers = {
    normalize_token(head): [
        normalize_token(marker)
        for marker in markers
        if normalize_token(marker)
    ]
    for head, markers in visual_head_missing_contract_markers_raw.items()
    if normalize_token(head) and isinstance(markers, list)
}
workflow_execution_evidence = (
    workflow_execution_gate.get("evidence")
    if isinstance(workflow_execution_gate.get("evidence"), dict)
    else {}
)
workflow_required_heads = normalize_required_token_list(
    workflow_execution_evidence.get("flagship_required_desktop_heads"),
    "workflow_execution.flagship_required_desktop_heads",
    evidence,
    reasons,
)
workflow_head_statuses_raw = (
    workflow_execution_evidence.get("flagship_head_proof_statuses")
    if isinstance(workflow_execution_evidence.get("flagship_head_proof_statuses"), dict)
    else {}
)
workflow_head_statuses = normalize_required_status_map(
    workflow_head_statuses_raw,
    "workflow_execution.flagship_head_proof_statuses",
    evidence,
    reasons,
)
workflow_head_contract_marker_statuses_raw = (
    workflow_execution_evidence.get("flagship_head_contract_marker_statuses")
    if isinstance(workflow_execution_evidence.get("flagship_head_contract_marker_statuses"), dict)
    else {}
)
workflow_head_contract_marker_statuses = {
    normalize_token(head): {
        normalize_token(marker): normalize_token(status)
        for marker, status in marker_statuses.items()
        if normalize_token(marker)
    }
    for head, marker_statuses in workflow_head_contract_marker_statuses_raw.items()
    if normalize_token(head) and isinstance(marker_statuses, dict)
}
workflow_head_missing_contract_markers_raw = (
    workflow_execution_evidence.get("flagship_head_missing_contract_markers")
    if isinstance(workflow_execution_evidence.get("flagship_head_missing_contract_markers"), dict)
    else {}
)
workflow_head_missing_contract_markers = {
    normalize_token(head): [
        normalize_token(marker)
        for marker in markers
        if normalize_token(marker)
    ]
    for head, markers in workflow_head_missing_contract_markers_raw.items()
    if normalize_token(head) and isinstance(markers, list)
}
visual_release_channel_id = normalize_optional_string_scalar(
    visual_familiarity_evidence.get("release_channel_channel_id")
    or visual_familiarity_evidence.get("release_channel_id")
    or visual_familiarity_gate.get("channelId")
    or visual_familiarity_gate.get("channel"),
    "visual_familiarity.release_channel_channel_id",
    evidence,
    reasons,
    required=True,
)
workflow_release_channel_id = normalize_optional_string_scalar(
    workflow_execution_evidence.get("release_channel_channel_id")
    or workflow_execution_evidence.get("release_channel_id")
    or workflow_execution_gate.get("channelId")
    or workflow_execution_gate.get("channel"),
    "workflow_execution.release_channel_channel_id",
    evidence,
    reasons,
    required=True,
)
visual_release_version = normalize_optional_string_scalar(
    visual_familiarity_evidence.get("release_channel_version")
    or visual_familiarity_gate.get("releaseVersion"),
    "visual_familiarity.release_channel_version",
    evidence,
    reasons,
    lowercase=False,
    required=True,
)
workflow_release_version = normalize_optional_string_scalar(
    workflow_execution_evidence.get("release_channel_version")
    or workflow_execution_gate.get("releaseVersion"),
    "workflow_execution.release_channel_version",
    evidence,
    reasons,
    lowercase=False,
    required=True,
)
evidence["visual_familiarity_release_channel_id"] = visual_release_channel_id
evidence["workflow_execution_release_channel_id"] = workflow_release_channel_id
evidence["visual_familiarity_release_version"] = visual_release_version
evidence["workflow_execution_release_version"] = workflow_release_version
if release_channel_channel_id and visual_release_channel_id and visual_release_channel_id != release_channel_channel_id:
    reasons.append(
        "Desktop visual familiarity exit gate release-channel identity does not match release channel channelId."
    )
if release_channel_channel_id and workflow_release_channel_id and workflow_release_channel_id != release_channel_channel_id:
    reasons.append(
        "Desktop workflow execution gate release-channel identity does not match release channel channelId."
    )
if release_channel_version and visual_release_version and visual_release_version != release_channel_version:
    reasons.append(
        "Desktop visual familiarity exit gate releaseVersion does not match release channel version."
    )
if release_channel_version and workflow_release_version and workflow_release_version != release_channel_version:
    reasons.append(
        "Desktop workflow execution gate releaseVersion does not match release channel version."
    )
evidence["visual_familiarity_required_desktop_heads"] = visual_required_heads
evidence["workflow_execution_required_desktop_heads"] = workflow_required_heads
evidence["visual_familiarity_head_contract_marker_statuses"] = (
    visual_head_contract_marker_statuses
)
evidence["visual_familiarity_head_missing_contract_markers"] = (
    visual_head_missing_contract_markers
)
evidence["workflow_execution_head_contract_marker_statuses"] = (
    workflow_head_contract_marker_statuses
)
evidence["workflow_execution_head_missing_contract_markers"] = (
    workflow_head_missing_contract_markers
)
if not visual_required_heads:
    reasons.append("Desktop visual familiarity exit gate evidence is missing required per-head desktop inventory.")
if not workflow_required_heads:
    reasons.append("Desktop workflow execution gate evidence is missing required per-head desktop inventory.")
missing_visual_required_heads = [
    head for head in heads_requiring_flagship_proof if head not in visual_required_heads
]
missing_workflow_required_heads = [
    head for head in heads_requiring_flagship_proof if head not in workflow_required_heads
]
evidence["visual_familiarity_missing_required_heads"] = missing_visual_required_heads
evidence["workflow_execution_missing_required_heads"] = missing_workflow_required_heads
if missing_visual_required_heads:
    reasons.append(
        "Desktop visual familiarity exit gate does not declare required per-head inventory for: "
        + ", ".join(missing_visual_required_heads)
    )
if missing_workflow_required_heads:
    reasons.append(
        "Desktop workflow execution gate does not declare required per-head inventory for: "
        + ", ".join(missing_workflow_required_heads)
    )
if heads_requiring_flagship_proof and not visual_head_contract_marker_statuses:
    reasons.append(
        "Desktop visual familiarity exit gate evidence is missing per-head proof contract markers."
    )
if heads_requiring_flagship_proof and not workflow_head_contract_marker_statuses:
    reasons.append(
        "Desktop workflow execution gate evidence is missing per-head proof contract markers."
    )
for promoted_head in heads_requiring_flagship_proof:
    contract_marker_statuses_for_head = visual_head_contract_marker_statuses.get(promoted_head, {})
    failing_contract_markers_for_head = sorted(
        {
            marker
            for marker, status in contract_marker_statuses_for_head.items()
            if status not in {"pass", "passed", "ready"}
        }
    )
    explicitly_missing_contract_markers_for_head = sorted(
        set(visual_head_missing_contract_markers.get(promoted_head, []))
    )
    missing_contract_markers_for_head = sorted(
        set(failing_contract_markers_for_head).union(
            set(explicitly_missing_contract_markers_for_head)
        )
    )
    evidence.setdefault("visual_familiarity_head_contract_markers_by_head", {})[
        promoted_head
    ] = {
        "marker_statuses": contract_marker_statuses_for_head,
        "missing_or_failing_markers": missing_contract_markers_for_head,
    }
    if not contract_marker_statuses_for_head:
        reasons.append(
            f"Desktop visual familiarity exit gate does not carry per-head proof contract markers for required desktop head '{promoted_head}'."
        )
    elif missing_contract_markers_for_head:
        reasons.append(
            f"Desktop visual familiarity exit gate has missing/failing per-head proof contract markers for required desktop head '{promoted_head}': "
            + ", ".join(missing_contract_markers_for_head)
        )
    workflow_contract_marker_statuses_for_head = workflow_head_contract_marker_statuses.get(promoted_head, {})
    failing_workflow_contract_markers_for_head = sorted(
        {
            marker
            for marker, status in workflow_contract_marker_statuses_for_head.items()
            if status not in {"pass", "passed", "ready"}
        }
    )
    explicitly_missing_workflow_contract_markers_for_head = sorted(
        set(workflow_head_missing_contract_markers.get(promoted_head, []))
    )
    missing_workflow_contract_markers_for_head = sorted(
        set(failing_workflow_contract_markers_for_head).union(
            set(explicitly_missing_workflow_contract_markers_for_head)
        )
    )
    evidence.setdefault("workflow_execution_head_contract_markers_by_head", {})[
        promoted_head
    ] = {
        "marker_statuses": workflow_contract_marker_statuses_for_head,
        "missing_or_failing_markers": missing_workflow_contract_markers_for_head,
    }
    if not workflow_contract_marker_statuses_for_head:
        reasons.append(
            f"Desktop workflow execution gate does not carry per-head proof contract markers for required desktop head '{promoted_head}'."
        )
    elif missing_workflow_contract_markers_for_head:
        reasons.append(
            f"Desktop workflow execution gate has missing/failing per-head proof contract markers for required desktop head '{promoted_head}': "
            + ", ".join(missing_workflow_contract_markers_for_head)
        )
    validate_flagship_head_proof(promoted_head, flagship_gate, evidence, reasons)
    validate_cross_gate_head_proof(
        promoted_head,
        "Desktop visual familiarity exit gate",
        visual_head_statuses,
        "visual_familiarity_head_proofs",
        evidence,
        reasons,
    )
    validate_cross_gate_head_proof(
        promoted_head,
        "Desktop workflow execution gate",
        workflow_head_statuses,
        "workflow_execution_head_proofs",
        evidence,
        reasons,
    )

linux_statuses: Dict[str, str] = {}
for expected_linux_artifact in expected_linux_artifacts:
    expected_linux_head = normalize_token(expected_linux_artifact.get("head"))
    expected_linux_rid = normalize_token(expected_linux_artifact.get("rid"))
    gate_label = f"{expected_linux_head}:{expected_linux_rid}"
    gate_path = linux_gate_path_for_head(
        expected_linux_head,
        expected_linux_rid,
        linux_avalonia_gate_path,
        linux_blazor_gate_path,
        receipt_path.parent,
    )
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"linux_gate:{gate_label}")
    reason_count_before = len(reasons)
    validate_linux_gate(
        gate_label,
        expected_linux_head,
        gate_path,
        load_json(gate_path),
        expected_linux_artifact,
        release_channel_channel_id,
        release_channel_version,
        repo_root,
        trusted_roots,
        evidence,
        reasons,
    )
    linux_statuses[gate_label] = "pass" if len(reasons) == reason_count_before else "fail"
evidence["linux_statuses"] = linux_statuses

expected_macos_artifacts = [
    item for item in desktop_install_artifacts
    if normalize_token(item.get("platform")) == "macos"
    and normalize_token(item.get("head"))
    and macos_rid_from_artifact(item)
]
macos_artifact_map_by_tuple: Dict[tuple[str, str], Dict[str, Any]] = {
    (
        normalize_token(item.get("head")),
        macos_rid_from_artifact(item),
    ): item
    for item in expected_macos_artifacts
}
required_macos_policy_tuples = sorted(
    {
        (head, rid)
        for token in tuple_coverage_required_platform_head_rid_tuples
        for head, rid, platform in [tuple(token.split(":", 2))]
        if platform == "macos" and head and rid
    }
)
macos_policy_tuples_missing_release_artifacts: List[str] = []
for head, rid in required_macos_policy_tuples:
    tuple_key = (head, rid)
    if tuple_key in macos_artifact_map_by_tuple:
        continue
    macos_policy_tuples_missing_release_artifacts.append(f"{head}:{rid}")
    expected_macos_artifacts.append(
        {
            "head": head,
            "rid": rid,
            "platform": "macos",
            "kind": "dmg",
            "fileName": "",
            "sha256": "",
            "sizeBytes": 0,
            "source": "required_tuple_policy_missing_release_artifact",
        }
    )
promoted_macos_tuples = {
    f"{normalize_token(item.get('head'))}:{macos_rid_from_artifact(item)}"
    for item in expected_macos_artifacts
}
evidence["macos_heads_expected"] = [
    {
        "head": normalize_token(item.get("head")),
        "rid": macos_rid_from_artifact(item),
        "fileName": str(item.get("fileName") or "").strip(),
    }
    for item in expected_macos_artifacts
]
evidence["macos_policy_required_head_rid_tuples"] = [
    f"{head}:{rid}" for head, rid in required_macos_policy_tuples
]
evidence["macos_policy_tuples_missing_release_artifacts"] = (
    macos_policy_tuples_missing_release_artifacts
)
collect_stale_platform_gate_receipts_without_promoted_tuples(
    receipt_path.parent,
    promoted_linux_tuples,
    promoted_windows_tuples,
    promoted_macos_tuples,
    evidence,
    reasons,
)
macos_statuses: Dict[str, str] = {}
for macos_artifact in expected_macos_artifacts:
    expected_head = normalize_token(macos_artifact.get("head"))
    expected_rid = macos_rid_from_artifact(macos_artifact)
    gate_label = f"{expected_head}:{expected_rid}"
    gate_path = macos_gate_path_for_head(expected_head, expected_rid, receipt_path.parent)
    validate_receipt_path_scope(gate_path, repo_root, reasons, evidence, f"macos_gate:{gate_label}")
    reason_count_before = len(reasons)
    validate_macos_gate(
        expected_head,
        expected_rid,
        macos_artifact,
        gate_path,
        load_json(gate_path),
        release_channel_channel_id,
        release_channel_version,
        desktop_files_root,
        repo_root,
        trusted_roots,
        evidence,
        reasons,
    )
    macos_statuses[gate_label] = "pass" if len(reasons) == reason_count_before else "fail"
evidence["macos_statuses"] = macos_statuses

def missing_or_failing_keys_for_platform(
    statuses: Dict[str, str],
    missing_required_pairs: List[str],
) -> List[str]:
    failing_keys = [
        normalize_token(key)
        for key, status in statuses.items()
        if normalize_token(key) and normalize_token(status) not in {"pass", "passed", "ready"}
    ]
    failing_keys.extend(normalize_token(item) for item in missing_required_pairs if normalize_token(item))
    return sorted(set(failing_keys))

linux_missing_or_failing_keys = missing_or_failing_keys_for_platform(
    linux_statuses,
    missing_required_platform_head_pairs_by_platform.get("linux", []),
)
windows_missing_or_failing_keys = missing_or_failing_keys_for_platform(
    windows_statuses,
    missing_required_platform_head_pairs_by_platform.get("windows", []),
)
macos_missing_or_failing_keys = missing_or_failing_keys_for_platform(
    macos_statuses,
    missing_required_platform_head_pairs_by_platform.get("macos", []),
)
evidence["linux_missing_or_failing_keys"] = linux_missing_or_failing_keys
evidence["windows_missing_or_failing_keys"] = windows_missing_or_failing_keys
evidence["macos_missing_or_failing_keys"] = macos_missing_or_failing_keys

platform_tokens: List[str] = []
if expected_linux_artifacts:
    platform_tokens.append("Linux")
if expected_macos_artifacts:
    platform_tokens.append("macOS")
if platform_artifact_counts.get("windows", 0) > 0:
    platform_tokens.append("Windows")
platform_scope = ", ".join(platform_tokens) if platform_tokens else "none"

status = "pass" if not reasons else "fail"
reasons = dedupe_preserve_order(reasons)
generated_at = now_iso()
payload = {
    "generated_at": generated_at,
    "generatedAt": generated_at,
    "contract_name": "chummer6-ui.desktop_executable_exit_gate",
    "channelId": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "status": status,
    "summary": (
        f"Desktop executable exit gate is proven by passing packaged-head receipts for promoted desktop platforms ({platform_scope}) and per-head flagship UI release proof."
        if status == "pass"
        else "Desktop executable exit gate is not fully proven."
    ),
    "reasons": reasons,
    "evidence": evidence,
}
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    print("[desktop-executable-exit-gate] FAIL", file=sys.stderr)
    print(f"[desktop-executable-exit-gate] receipt: {receipt_path}", file=sys.stderr)
    if reasons:
        for reason in reasons:
            print(f"[desktop-executable-exit-gate] reason: {reason}", file=sys.stderr)
    else:
        print("[desktop-executable-exit-gate] reason: unknown failure", file=sys.stderr)
    raise SystemExit(43)
PY

echo "[desktop-executable-exit-gate] PASS"
