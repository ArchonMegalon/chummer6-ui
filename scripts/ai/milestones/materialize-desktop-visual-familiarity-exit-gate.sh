#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
cd "$repo_root"

canonical_receipt_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
canonical_screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
receipt_path="${CHUMMER_DESKTOP_VISUAL_OUTPUT_PATH:-${CHUMMER_DESKTOP_VISUAL_RECEIPT_PATH:-$canonical_receipt_path}}"
flagship_gate_path="${CHUMMER_DESKTOP_VISUAL_FLAGSHIP_GATE_PATH:-$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
screenshot_dir="${CHUMMER_DESKTOP_VISUAL_SCREENSHOT_DIR:-$canonical_screenshot_dir}"
screenshot_control_evidence_path="${CHUMMER_DESKTOP_VISUAL_SCREENSHOT_CONTROL_EVIDENCE_PATH:-$screenshot_dir/SCREENSHOT_CONTROL_EVIDENCE.generated.json}"
flagship_product_readiness_materializer_path="${CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH:-/docker/fleet/scripts/materialize_flagship_product_readiness.py}"
release_channel_path_override="${CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH:-}"
hub_registry_root=""
if [[ -z "$release_channel_path_override" ]]; then
  hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
fi
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
run_services_release_channel_path="${CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH:-/docker/chummercomplete/chummer.run-services/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
presentation_release_channel_path="/docker/chummercomplete/chummer-presentation/Chummer.Portal/downloads/RELEASE_CHANNEL.generated.json"
verified_release_channel_path="$repo_root/.tmp/verify-release-channel/RELEASE_CHANNEL.generated.json"
if [[ -n "$release_channel_path_override" ]]; then
  release_channel_path_default="$release_channel_path_override"
elif [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
elif [[ -f "$verified_release_channel_path" ]]; then
  release_channel_path_default="$verified_release_channel_path"
elif [[ -f "$run_services_release_channel_path" ]]; then
  release_channel_path_default="$run_services_release_channel_path"
elif [[ -f "$default_release_channel_path" ]]; then
  release_channel_path_default="$default_release_channel_path"
elif [[ -f "$presentation_release_channel_path" ]]; then
  release_channel_path_default="$presentation_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="$release_channel_path_default"
release_gate_lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
release_gate_lock_owner_pid_path="$release_gate_lock_dir/owner.pid"
app_axaml_path="$repo_root/Chummer.Avalonia/App.axaml"
main_window_axaml_path="$repo_root/Chummer.Avalonia/MainWindow.axaml"
navigator_axaml_path="$repo_root/Chummer.Avalonia/Controls/NavigatorPaneControl.axaml"
toolstrip_axaml_path="$repo_root/Chummer.Avalonia/Controls/ClassicToolStrip.axaml"
toolstrip_codebehind_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"
summary_header_axaml_path="$repo_root/Chummer.Avalonia/Controls/SummaryHeaderControl.axaml"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
desktop_shell_ruleset_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"
legacy_frmcareer_designer_path="/docker/chummer5a/Chummer/Forms/Character Forms/CharacterCareer.Designer.cs"
b14_flagship_ui_release_gate_script_path="${CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SCRIPT_PATH:-$repo_root/scripts/ai/milestones/b14-flagship-ui-release-gate.sh}"
layout_hard_gate_receipt_path="${CHUMMER5A_LAYOUT_HARD_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_LAYOUT_HARD_GATE.generated.json}"
legacy_equivalent_chrome_gate_receipt_path="${CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_LEGACY_EQUIVALENT_CHROME_GATE.generated.json}"
muscle_memory_parity_gate_receipt_path="${CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE_PATH:-$repo_root/.codex-studio/published/CHUMMER5A_MUSCLE_MEMORY_PARITY_GATE.generated.json}"
skip_release_gate_lock_wait="${CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT:-0}"
skip_prerequisite_receipt_refresh="${CHUMMER_DESKTOP_VISUAL_SKIP_PREREQUISITE_RECEIPT_REFRESH:-0}"
force_prerequisite_receipt_refresh="${CHUMMER_DESKTOP_VISUAL_FORCE_PREREQUISITE_RECEIPT_REFRESH:-0}"
refresh_prerequisite_receipts="${CHUMMER_DESKTOP_VISUAL_REFRESH_PREREQUISITE_RECEIPTS:-0}"
refresh_screenshot_pack_when_stale="${CHUMMER_DESKTOP_VISUAL_REFRESH_SCREENSHOT_PACK_WHEN_STALE:-0}"
skip_downstream_readiness="${CHUMMER_DESKTOP_VISUAL_SKIP_DOWNSTREAM_READINESS:-0}"
refresh_downstream_readiness="${CHUMMER_DESKTOP_VISUAL_REFRESH_DOWNSTREAM_READINESS:-0}"
prerequisite_receipt_max_age_seconds="${CHUMMER_DESKTOP_VISUAL_PREREQUISITE_MAX_AGE_SECONDS:-${CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS:-86400}}"
prerequisite_receipt_max_future_skew_seconds="${CHUMMER_DESKTOP_VISUAL_PREREQUISITE_MAX_FUTURE_SKEW_SECONDS:-${CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:-300}}"
release_gate_lock_wait_seconds="${CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_WAIT_SECONDS:-300}"
release_gate_lock_poll_seconds="${CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_POLL_SECONDS:-2}"
release_gate_lock_stale_max_age_seconds="${CHUMMER_DESKTOP_VISUAL_RELEASE_GATE_LOCK_STALE_MAX_AGE_SECONDS:-900}"
if ! [[ "$release_gate_lock_wait_seconds" =~ ^[0-9]+$ ]]; then
  release_gate_lock_wait_seconds=300
fi
if ! [[ "$release_gate_lock_poll_seconds" =~ ^[0-9]+$ ]] || [[ "$release_gate_lock_poll_seconds" -lt 1 ]]; then
  release_gate_lock_poll_seconds=2
fi
if ! [[ "$release_gate_lock_stale_max_age_seconds" =~ ^[0-9]+$ ]]; then
  release_gate_lock_stale_max_age_seconds=900
fi
if ! [[ "$prerequisite_receipt_max_age_seconds" =~ ^[0-9]+$ ]]; then
  prerequisite_receipt_max_age_seconds=86400
fi
if ! [[ "$prerequisite_receipt_max_future_skew_seconds" =~ ^[0-9]+$ ]]; then
  prerequisite_receipt_max_future_skew_seconds=300
fi

mkdir -p "$(dirname "$receipt_path")"
# Screenshot refresh is intentionally delegated to the explicit b14 capture lane.
# Do not discover or promote arbitrary historical packs into the canonical shelf.
prune_release_gate_lock_if_stale() {
  if [[ ! -d "$release_gate_lock_dir" ]]; then
    return 0
  fi
  if [[ -f "$release_gate_lock_owner_pid_path" ]]; then
    owner_pid="$(tr -dc '0-9' <"$release_gate_lock_owner_pid_path")"
    if [[ -n "$owner_pid" ]] && kill -0 "$owner_pid" 2>/dev/null; then
      return 0
    fi
  fi
  if command -v pgrep >/dev/null 2>&1; then
    if pgrep -f "scripts/ai/milestones/b14-flagship-ui-release-gate.sh" >/dev/null 2>&1; then
      return 0
    fi
  fi

  lock_stale_probe="$(
    python3 - <<'PY' "$release_gate_lock_dir" "$release_gate_lock_owner_pid_path" "$release_gate_lock_stale_max_age_seconds"
from __future__ import annotations

import sys
import time
from pathlib import Path

lock_dir = Path(sys.argv[1])
owner_pid_path = Path(sys.argv[2])
max_age = int(sys.argv[3])
if not lock_dir.is_dir():
    print("absent")
    raise SystemExit(0)

entries = list(lock_dir.iterdir())
entries_without_owner = [entry for entry in entries if entry != owner_pid_path]
if entries_without_owner:
    print("nonempty")
    raise SystemExit(0)

age_seconds = max(0, int(time.time() - lock_dir.stat().st_mtime))
if owner_pid_path.exists():
    print(f"dead_owner_only:{age_seconds}")
    raise SystemExit(0)

if age_seconds < max_age:
    print(f"young:{age_seconds}")
    raise SystemExit(0)

print(f"stale_empty:{age_seconds}")
PY
  )"
  if [[ "$lock_stale_probe" == stale_empty:* || "$lock_stale_probe" == stale_owner_only:* || "$lock_stale_probe" == dead_owner_only:* ]]; then
    rm -rf "$release_gate_lock_dir"
  fi
}
if [[ "$skip_release_gate_lock_wait" != "1" \
  && ( "$refresh_screenshot_pack_when_stale" == "1" \
    || ( "$force_prerequisite_receipt_refresh" == "1" \
      && "$skip_prerequisite_receipt_refresh" != "1" ) \
    || ( "$refresh_prerequisite_receipts" == "1" \
      && "$skip_prerequisite_receipt_refresh" != "1" ) ) ]]; then
  release_gate_lock_wait_iterations=$((release_gate_lock_wait_seconds / release_gate_lock_poll_seconds))
  if [[ "$release_gate_lock_wait_iterations" -lt 1 ]]; then
    release_gate_lock_wait_iterations=1
  fi
  for _ in $(seq 1 "$release_gate_lock_wait_iterations"); do
    prune_release_gate_lock_if_stale
    if [[ ! -d "$release_gate_lock_dir" ]]; then
      break
    fi
    sleep "$release_gate_lock_poll_seconds"
  done
  prune_release_gate_lock_if_stale
  if [[ -d "$release_gate_lock_dir" ]]; then
    echo "[desktop-visual-familiarity-gate] FAIL: release gate lock did not clear within ${release_gate_lock_wait_seconds}s: $release_gate_lock_dir" >&2
    exit 52
  fi
fi

if [[ "$refresh_screenshot_pack_when_stale" == "1" ]]; then
  if [[ "$screenshot_dir" != "$canonical_screenshot_dir" ]]; then
    echo "[desktop-visual-familiarity-gate] FAIL: explicit b14 refresh only supports the canonical screenshot shelf; use a pre-captured isolated screenshot directory with refresh disabled." >&2
    exit 55
  fi
  if [[ ! -f "$b14_flagship_ui_release_gate_script_path" ]]; then
    echo "[desktop-visual-familiarity-gate] FAIL: explicit screenshot refresh requested but b14 capture lane is missing: $b14_flagship_ui_release_gate_script_path" >&2
    exit 56
  fi
  b14_flagship_readiness_materializer_path="/dev/null"
  if [[ "$refresh_downstream_readiness" == "1" && "$skip_downstream_readiness" != "1" ]]; then
    b14_flagship_readiness_materializer_path="$flagship_product_readiness_materializer_path"
  fi
  CHUMMER_FLAGSHIP_UI_RELEASE_GATE_REFRESH_SUPPORTING_RECEIPTS=0 \
    CHUMMER_FLAGSHIP_UI_RELEASE_GATE_SKIP_DOWNSTREAM_RECEIPTS=1 \
    CHUMMER_FLAGSHIP_UI_RELEASE_CHANNEL_PATH="$release_channel_path" \
    CHUMMER_FLAGSHIP_PRODUCT_READINESS_MATERIALIZER_PATH="$b14_flagship_readiness_materializer_path" \
    bash "$b14_flagship_ui_release_gate_script_path" >/dev/null
fi

prerequisite_receipts_ready=0
if python3 - <<'PY' "$prerequisite_receipt_max_age_seconds" "$prerequisite_receipt_max_future_skew_seconds" "$layout_hard_gate_receipt_path" "$legacy_equivalent_chrome_gate_receipt_path" "$muscle_memory_parity_gate_receipt_path"
from __future__ import annotations

import json
import sys
from datetime import datetime, timezone
from pathlib import Path


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def parse_iso(value: object) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    return parsed.astimezone(timezone.utc)


def receipt_ready(
    path_text: str,
    expected_contract: str,
    max_age_seconds: int,
    max_future_skew_seconds: int,
) -> bool:
    path = Path(path_text)
    if not path.is_file() or path.is_symlink():
        return False
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return False
    if (
        not isinstance(payload, dict)
        or not status_ok(payload.get("status"))
        or str(payload.get("contract_name") or payload.get("contractName") or "").strip()
        != expected_contract
    ):
        return False
    generated_at = parse_iso(payload.get("generatedAt") or payload.get("generated_at"))
    if generated_at is None:
        return False
    age_seconds = (datetime.now(timezone.utc) - generated_at).total_seconds()
    return -max_future_skew_seconds <= age_seconds <= max_age_seconds


max_age_seconds = int(sys.argv[1])
max_future_skew_seconds = int(sys.argv[2])
specs = [
    (sys.argv[3], "chummer6-ui.chummer5a_layout_hard_gate"),
    (sys.argv[4], "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate"),
]
raise SystemExit(
    0
    if all(
        receipt_ready(path, contract, max_age_seconds, max_future_skew_seconds)
        for path, contract in specs
    )
    else 1
)
PY
then
  prerequisite_receipts_ready=1
fi

if [[ "$skip_prerequisite_receipt_refresh" == "1" ]]; then
  echo "[desktop-visual-familiarity-gate] prerequisite refresh explicitly skipped; validating the selected receipts without mutation."
elif [[ "$force_prerequisite_receipt_refresh" != "1" && "$prerequisite_receipts_ready" == "1" ]]; then
  echo "[desktop-visual-familiarity-gate] reusing current passing prerequisite receipts."
elif [[ "$force_prerequisite_receipt_refresh" == "1" \
  || "$refresh_prerequisite_receipts" == "1" ]]; then
  echo "[desktop-visual-familiarity-gate] running Chummer5a layout hard gate..."
  bash scripts/ai/milestones/chummer5a-layout-hard-gate.sh >/dev/null
else
  echo "[desktop-visual-familiarity-gate] prerequisite refresh disabled; the final validator will fail closed on stale or invalid receipts."
fi

python3 - <<'PY' \
  "$repo_root" \
  "$receipt_path" \
  "$flagship_gate_path" \
  "$screenshot_dir" \
  "$screenshot_control_evidence_path" \
  "$app_axaml_path" \
  "$main_window_axaml_path" \
  "$navigator_axaml_path" \
  "$toolstrip_axaml_path" \
  "$toolstrip_codebehind_path" \
  "$summary_header_axaml_path" \
  "$ui_gate_tests_path" \
  "$desktop_shell_ruleset_tests_path" \
  "$legacy_frmcareer_designer_path" \
  "$release_channel_path" \
  "$layout_hard_gate_receipt_path" \
  "$legacy_equivalent_chrome_gate_receipt_path" \
  "$muscle_memory_parity_gate_receipt_path"
from __future__ import annotations

import binascii
import hashlib
import json
import os
import re
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List

DESKTOP_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_PROOF_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
PREREQUISITE_PROOF_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_PREREQUISITE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
PREREQUISITE_PROOF_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_PREREQUISITE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
SKIP_FLAGSHIP_GATE_DEPENDENCY = str(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SKIP_FLAGSHIP_GATE_DEPENDENCY") or "0"
).strip() == "1"
DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS")
    or str(DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS)
)
SCREENSHOT_CONTROL_SCHEMA_VERSION = 1
SCREENSHOT_CONTROL_CONTRACT_NAME = "chummer6-ui.screenshot_control_evidence"


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def atomic_write_json(path: Path, payload: Dict[str, Any]) -> None:
    rendered = json.dumps(payload, indent=2) + "\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    file_descriptor, temporary_path_text = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=str(path.parent),
    )
    temporary_path = Path(temporary_path_text)
    try:
        with os.fdopen(file_descriptor, "w", encoding="utf-8") as handle:
            handle.write(rendered)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
        directory_descriptor = os.open(
            path.parent,
            os.O_RDONLY | getattr(os, "O_DIRECTORY", 0),
        )
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        return {}
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return {}
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def trimmed_string_field(payload: Dict[str, Any], key: str) -> str:
    value = payload.get(key)
    return value.strip() if isinstance(value, str) else ""


def flagship_gate_is_external_desktop_only(payload: Dict[str, Any]) -> bool:
    if not isinstance(payload, dict) or not payload:
        return False
    blocking_findings = payload.get("blockingFindings")
    if not isinstance(blocking_findings, list) or not blocking_findings:
        return False
    desktop_executable_finding = (
        "Top-level release gate cannot pass while desktop executable exit gate is not passed."
    )
    readiness_findings = {
        "Top-level release gate cannot pass while flagship readiness is not passed.",
        "Top-level release gate cannot pass while flagship readiness coverage.desktop_client is not ready.",
        "Top-level release gate cannot pass while flagship readiness still has open coverage keys: desktop_client.",
    }
    allowed_findings = {
        desktop_executable_finding,
        *readiness_findings,
    }
    normalized_blocking_findings = [
        str(finding).strip() for finding in blocking_findings if str(finding).strip()
    ]
    if len(normalized_blocking_findings) != len(blocking_findings):
        return False
    if any(finding not in allowed_findings for finding in normalized_blocking_findings):
        return False

    has_desktop_executable_finding = desktop_executable_finding in normalized_blocking_findings
    has_readiness_finding = any(
        finding in readiness_findings for finding in normalized_blocking_findings
    )
    if not has_desktop_executable_finding and not has_readiness_finding:
        return False

    if has_desktop_executable_finding:
        desktop_executable_proof = payload.get("desktopExecutableProof")
        if not isinstance(desktop_executable_proof, dict):
            return False
        local_blocking_findings = desktop_executable_proof.get("localBlockingFindings")
        if not isinstance(local_blocking_findings, list):
            return False
        normalized_local_blocking_findings = [
            str(finding).strip()
            for finding in local_blocking_findings
            if str(finding).strip()
        ]
        if (
            not normalized_local_blocking_findings
            or len(normalized_local_blocking_findings) != len(local_blocking_findings)
        ):
            return False
        allowed_local_findings = {
            "Windows desktop exit gate requires a Windows-capable host; current host cannot run promoted Windows installer smoke.",
            "Windows gate reason: Windows installer visual proof is missing; capture progress and completion screenshots on a Windows host.",
        }
        if any(
            finding not in allowed_local_findings
            for finding in normalized_local_blocking_findings
        ):
            return False

    if has_readiness_finding:
        flagship_readiness_proof = payload.get("flagshipReadinessProof")
        if not isinstance(flagship_readiness_proof, dict):
            return False
        coverage = flagship_readiness_proof.get("coverage")
        open_coverage_keys = flagship_readiness_proof.get("openCoverageKeys")
        if not isinstance(coverage, dict) or not isinstance(open_coverage_keys, list):
            return False
        normalized_open_coverage_keys = [
            str(key).strip() for key in open_coverage_keys if str(key).strip()
        ]
        if (
            normalized_open_coverage_keys != ["desktop_client"]
            or len(normalized_open_coverage_keys) != len(open_coverage_keys)
        ):
            return False
        if status_ok(str(coverage.get("desktop_client") or "")):
            return False
        if any(
            key != "desktop_client" and not status_ok(str(value or ""))
            for key, value in coverage.items()
        ):
            return False

    return True


def normalize_head_proof_statuses(
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
    for raw_key, raw_proof in values.items():
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
        if not isinstance(raw_proof, dict):
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a non-object proof payload for key '{key}'.")
            continue
        raw_status = raw_proof.get("status")
        if raw_status is None:
            normalized[key] = ""
            continue
        if not isinstance(raw_status, str):
            malformed_entries.append(key)
            reasons.append(f"{field_label} contains a non-string status for key '{key}'.")
            continue
        if raw_status != raw_status.strip():
            malformed_entries.append(key)
            reasons.append(
                f"{field_label} contains a status with leading/trailing whitespace for key '{key}'."
            )
            continue
        normalized[key] = normalize_token(raw_status)
    evidence[f"{field_label}_normalized"] = normalized
    evidence[f"{field_label}_malformed_entries"] = sorted(set(malformed_entries))
    evidence[f"{field_label}_non_canonical_keys"] = sorted(set(non_canonical_keys))
    evidence[f"{field_label}_duplicate_normalized_keys"] = sorted(set(duplicate_normalized_keys))
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
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    return parsed.astimezone(timezone.utc)


def payload_generated_at(payload: Dict[str, Any]) -> tuple[str, datetime | None]:
    for key in ("generated_at", "generatedAt"):
        if key in payload:
            raw = str(payload.get(key) or "").strip()
            return raw, parse_iso(raw)
    return "", None


def validate_receipt_freshness(
    label: str,
    payload: Dict[str, Any],
    reasons: List[str],
    evidence: Dict[str, Any],
    *,
    max_age_seconds: int = DESKTOP_PROOF_MAX_AGE_SECONDS,
    max_future_skew_seconds: int = DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS,
    enforce_max_age: bool = True,
) -> None:
    generated_at_raw, generated_at = payload_generated_at(payload)
    evidence[f"{label}_generated_at"] = generated_at_raw
    if not generated_at_raw or generated_at is None:
        reasons.append(f"{label} is missing a valid generatedAt/generated_at timestamp.")
        return
    age_seconds = int((datetime.now(timezone.utc) - generated_at).total_seconds())
    if age_seconds < 0:
        future_skew_seconds = abs(age_seconds)
        evidence[f"{label}_future_skew_seconds"] = future_skew_seconds
        if future_skew_seconds > max_future_skew_seconds:
            reasons.append(
                f"{label} generatedAt is in the future ({future_skew_seconds}s ahead; max {max_future_skew_seconds}s)."
            )
        age_seconds = 0
    evidence[f"{label}_age_seconds"] = age_seconds
    if enforce_max_age and age_seconds > max_age_seconds:
        reasons.append(
            f"{label} is stale ({age_seconds}s old; max {max_age_seconds}s)."
        )


def validate_png_bytes(data: bytes) -> tuple[str, int, int]:
    signature = b"\x89PNG\r\n\x1a\n"
    if not data.startswith(signature):
        return "missing PNG signature", 0, 0
    offset = len(signature)
    saw_iend = False
    saw_ihdr = False
    saw_idat = False
    width = 0
    height = 0
    while offset + 12 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk_type = data[offset + 4 : offset + 8]
        chunk_start = offset + 8
        chunk_end = chunk_start + length
        crc_start = chunk_end
        crc_end = crc_start + 4
        if crc_end > len(data):
            return f"truncated chunk {chunk_type.decode('ascii', 'replace')}", width, height
        if chunk_type == b"IHDR":
            if saw_ihdr or offset != len(signature) or length != 13:
                return "invalid IHDR chunk", width, height
            saw_ihdr = True
            width = int.from_bytes(data[chunk_start : chunk_start + 4], "big")
            height = int.from_bytes(data[chunk_start + 4 : chunk_start + 8], "big")
            if width <= 0 or height <= 0:
                return "invalid PNG dimensions", width, height
        elif chunk_type == b"IDAT":
            if not saw_ihdr or length <= 0:
                return "invalid IDAT chunk", width, height
            saw_idat = True
        elif chunk_type == b"IEND" and length != 0:
            return "invalid IEND chunk", width, height
        expected_crc = int.from_bytes(data[crc_start:crc_end], "big")
        actual_crc = binascii.crc32(chunk_type)
        actual_crc = binascii.crc32(data[chunk_start:chunk_end], actual_crc) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            return f"CRC mismatch in {chunk_type.decode('ascii', 'replace')}", width, height
        offset = crc_end
        if chunk_type == b"IEND":
            saw_iend = True
            break
    if not saw_iend:
        return "missing IEND chunk", width, height
    if not saw_ihdr:
        return "missing IHDR chunk", width, height
    if not saw_idat:
        return "missing IDAT chunk", width, height
    if offset != len(data):
        return "trailing bytes after IEND", width, height
    return "", width, height


def extract_test_method(text: str, method_name: str) -> str:
    markers = [
        f"public void {method_name}(",
        f"private void {method_name}(",
        f"protected void {method_name}(",
        f"internal void {method_name}(",
        f"void {method_name}(",
    ]
    starts = [text.find(marker) for marker in markers if text.find(marker) >= 0]
    if not starts:
        signature_pattern = re.compile(rf"\bvoid\s+{re.escape(method_name)}\s*\(\s*\)")
        match = signature_pattern.search(text)
        if match is None:
            return ""
        start = match.start()
    else:
        start = min(starts)
    next_test = text.find("[TestMethod]", start + 1)
    return text[start:] if next_test < 0 else text[start:next_test]


def segment_between(text: str, start_marker: str, end_marker: str) -> str:
    start = text.find(start_marker)
    if start < 0:
        return ""
    end = text.find(end_marker, start + len(start_marker))
    return text[start:] if end < 0 else text[start:end]


def segment_between_any(text: str, start_markers: List[str], end_markers: List[str]) -> str:
    start_candidates = [
        (text.find(marker), marker)
        for marker in start_markers
        if text.find(marker) >= 0
    ]
    if not start_candidates:
        return ""
    start, start_marker = min(start_candidates, key=lambda item: item[0])
    end_candidates = [
        (text.find(marker, start + len(start_marker)), marker)
        for marker in end_markers
        if text.find(marker, start + len(start_marker)) >= 0
    ]
    if not end_candidates:
        return text[start:]
    end, _ = min(end_candidates, key=lambda item: item[0])
    return text[start:end]


def capture_statement_variants(index: int) -> List[str]:
    return [
        f"CaptureCurrentFrame(expectedFiles[{index}]);",
        f"CaptureCurrentFrame(harness, expectedFiles[{index}]);",
        f"captured[expectedFiles[{index}]] = harness.CaptureScreenshotBytes();",
        f"captured[expectedFiles[{index}]] = CaptureScreenshotProof(harness, expectedFiles[{index}]);",
    ]


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def lstat_fingerprint(path: Path) -> Dict[str, int] | None:
    try:
        value = path.lstat()
    except OSError:
        return None
    return {
        "device": int(value.st_dev),
        "inode": int(value.st_ino),
        "mode": int(value.st_mode),
        "sizeBytes": int(value.st_size),
        "mtimeNs": int(value.st_mtime_ns),
        "ctimeNs": int(value.st_ctime_ns),
        "linkCount": int(value.st_nlink),
    }


def symlinked_path_components(path: Path) -> List[str]:
    absolute_path = Path(os.path.abspath(os.fspath(path)))
    components = [absolute_path, *absolute_path.parents]
    return [
        str(component)
        for component in reversed(components)
        if component.is_symlink()
    ]


(
    repo_root,
    receipt_path,
    flagship_gate_path,
    screenshot_dir,
    screenshot_control_evidence_path,
    app_axaml_path,
    main_window_axaml_path,
    navigator_axaml_path,
    toolstrip_axaml_path,
    toolstrip_codebehind_path,
    summary_header_axaml_path,
    ui_gate_tests_path,
    desktop_shell_ruleset_tests_path,
    legacy_frmcareer_designer_path,
    release_channel_path,
    layout_hard_gate_receipt_path,
    legacy_equivalent_chrome_gate_receipt_path,
    muscle_memory_parity_gate_receipt_path,
) = [Path(value) for value in sys.argv[1:19]]

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "flagship_gate_path": str(flagship_gate_path),
    "screenshot_dir": str(screenshot_dir),
    "screenshot_control_evidence_path": str(screenshot_control_evidence_path),
    "app_axaml_path": str(app_axaml_path),
    "main_window_axaml_path": str(main_window_axaml_path),
    "navigator_axaml_path": str(navigator_axaml_path),
    "toolstrip_axaml_path": str(toolstrip_axaml_path),
    "toolstrip_codebehind_path": str(toolstrip_codebehind_path),
    "ui_gate_tests_path": str(ui_gate_tests_path),
    "desktop_shell_ruleset_tests_path": str(desktop_shell_ruleset_tests_path),
    "legacy_frmcareer_designer_path": str(legacy_frmcareer_designer_path),
    "minimum_shell_review_size": {"width": 1280, "height": 800},
    "minimum_dialog_review_size": {"width": 900, "height": 700},
    "proof_freshness_max_age_seconds": DESKTOP_PROOF_MAX_AGE_SECONDS,
    "proof_freshness_max_future_skew_seconds": DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS,
    "screenshot_max_age_seconds": DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS,
    "screenshot_receipt_skew_max_seconds": DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS,
    "release_channel_path": str(release_channel_path),
    "layout_hard_gate_receipt_path": str(layout_hard_gate_receipt_path),
    "legacy_equivalent_chrome_gate_receipt_path": str(legacy_equivalent_chrome_gate_receipt_path),
    "muscle_memory_parity_gate_receipt_path": str(muscle_memory_parity_gate_receipt_path),
    "prerequisite_proof_max_age_seconds": PREREQUISITE_PROOF_MAX_AGE_SECONDS,
    "prerequisite_proof_max_future_skew_seconds": PREREQUISITE_PROOF_MAX_FUTURE_SKEW_SECONDS,
}

flagship_gate_review_start = len(reasons)
flagship_gate = load_json(flagship_gate_path)
flagship_contract_name = trimmed_string_field(flagship_gate, "contract_name")
flagship_contract_alias = trimmed_string_field(flagship_gate, "contractName")
flagship_status = str(flagship_gate.get("status") or "").strip().lower()
evidence["flagship_gate_contract_name"] = flagship_contract_name
evidence["flagship_gate_status"] = flagship_status
flagship_gate_external_desktop_only = (
    not status_ok(flagship_status)
    and flagship_gate_is_external_desktop_only(flagship_gate)
)
evidence["flagship_gate_external_desktop_only"] = flagship_gate_external_desktop_only
if not flagship_gate_path.is_file() or not flagship_gate:
    reasons.append("Flagship UI release gate receipt is missing or unreadable.")
if flagship_contract_name != "chummer6-ui.flagship_ui_release_gate":
    reasons.append(
        "Flagship UI release gate contract_name is not chummer6-ui.flagship_ui_release_gate."
    )
if flagship_contract_alias and flagship_contract_alias != flagship_contract_name:
    reasons.append(
        "Flagship UI release gate carries conflicting contract_name/contractName aliases."
    )
if not status_ok(flagship_status) and not flagship_gate_external_desktop_only:
    reasons.append(
        "Flagship UI release gate status is not passing and its blockers are not the tightly recognized external-desktop-only set."
    )
validate_receipt_freshness(
    "flagship_ui_release_gate",
    flagship_gate,
    reasons,
    evidence,
)
release_channel_bytes = b""
release_channel: Dict[str, Any] = {}
release_channel_initial_fingerprint = lstat_fingerprint(release_channel_path)
if (
    release_channel_initial_fingerprint is not None
    and stat.S_ISREG(release_channel_initial_fingerprint["mode"])
    and not release_channel_path.is_symlink()
):
    try:
        release_channel_bytes = release_channel_path.read_bytes()
        release_channel_after_read_fingerprint = lstat_fingerprint(release_channel_path)
        if release_channel_after_read_fingerprint != release_channel_initial_fingerprint:
            reasons.append(
                "Desktop visual familiarity exit gate release channel receipt changed while it was being read."
            )
        loaded_release_channel = json.loads(release_channel_bytes.decode("utf-8-sig"))
        if isinstance(loaded_release_channel, dict):
            release_channel = loaded_release_channel
    except (OSError, UnicodeError, json.JSONDecodeError):
        release_channel = {}
try:
    release_channel_resolved_path = str(release_channel_path.resolve(strict=True))
except OSError:
    release_channel_resolved_path = ""
evidence["release_channel_receipt_exists"] = (
    release_channel_initial_fingerprint is not None
    and stat.S_ISREG(release_channel_initial_fingerprint["mode"])
    and not release_channel_path.is_symlink()
)
evidence["release_channel_resolved_path"] = release_channel_resolved_path
evidence["release_channel_receipt_sha256"] = (
    hashlib.sha256(release_channel_bytes).hexdigest()
    if release_channel_bytes
    else ""
)
evidence["release_channel_receipt_size_bytes"] = len(release_channel_bytes)
if not evidence["release_channel_receipt_exists"] or not release_channel:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing, unsafe, unreadable, or not a JSON object."
    )
release_channel_contract_name = trimmed_string_field(release_channel, "contract_name")
release_channel_status = normalize_token(release_channel.get("status"))
release_channel_channel_id_value = trimmed_string_field(release_channel, "channelId")
release_channel_channel_alias = trimmed_string_field(release_channel, "channel")
release_channel_version_value = trimmed_string_field(release_channel, "releaseVersion")
release_channel_version_alias = trimmed_string_field(release_channel, "version")
release_channel_channel_id = normalize_token(release_channel_channel_id_value)
release_channel_version = release_channel_version_value
release_channel_generated_at_raw, release_channel_generated_at = payload_generated_at(release_channel)
release_channel_generated_at_value = trimmed_string_field(release_channel, "generatedAt")
release_channel_generated_at_alias = trimmed_string_field(release_channel, "generated_at")
flagship_channel_id_value = trimmed_string_field(flagship_gate, "channelId")
flagship_channel_alias = trimmed_string_field(flagship_gate, "channel")
flagship_version_value = trimmed_string_field(flagship_gate, "releaseVersion")
flagship_version_alias = trimmed_string_field(flagship_gate, "version")
flagship_channel_id = normalize_token(flagship_channel_id_value)
flagship_release_version = flagship_version_value
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version
evidence["release_channel_contract_name"] = release_channel_contract_name
evidence["release_channel_status"] = release_channel_status
evidence["release_channel_generated_at"] = release_channel_generated_at_raw
evidence["flagship_gate_channel_id"] = flagship_channel_id
evidence["flagship_gate_release_version"] = flagship_release_version
if release_channel_contract_name != "Chummer.Hub.Registry.Contracts":
    reasons.append(
        "Desktop visual familiarity exit gate release channel contract_name is not recognized."
    )
if release_channel_status != "published":
    reasons.append(
        "Desktop visual familiarity exit gate release channel status is not published."
    )
if not release_channel_channel_id_value:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing required channelId alias."
    )
if not release_channel_channel_alias:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing required channel alias."
    )
if normalize_token(release_channel_channel_id_value) != normalize_token(
    release_channel_channel_alias
):
    reasons.append(
        "Desktop visual familiarity exit gate release channel carries conflicting channelId/channel aliases."
    )
if not release_channel_version_value:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing required releaseVersion alias."
    )
if not release_channel_version_alias:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing required version alias."
    )
if release_channel_version_value != release_channel_version_alias:
    reasons.append(
        "Desktop visual familiarity exit gate release channel carries conflicting releaseVersion/version aliases."
    )
if (
    release_channel_generated_at_value
    and release_channel_generated_at_alias
    and release_channel_generated_at_value != release_channel_generated_at_alias
):
    reasons.append(
        "Desktop visual familiarity exit gate release channel carries conflicting generatedAt/generated_at aliases."
    )
if not flagship_channel_id_value:
    reasons.append(
        "Flagship UI release gate receipt is missing required channelId alias."
    )
if not flagship_channel_alias:
    reasons.append("Flagship UI release gate receipt is missing required channel alias.")
if normalize_token(flagship_channel_id_value) != normalize_token(flagship_channel_alias):
    reasons.append(
        "Flagship UI release gate carries conflicting channelId/channel aliases."
    )
if not flagship_version_value:
    reasons.append(
        "Flagship UI release gate receipt is missing required releaseVersion alias."
    )
if not flagship_version_alias:
    reasons.append("Flagship UI release gate receipt is missing required version alias.")
if flagship_version_value != flagship_version_alias:
    reasons.append(
        "Flagship UI release gate carries conflicting releaseVersion/version aliases."
    )
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )
if flagship_channel_id and flagship_channel_id != release_channel_channel_id:
    reasons.append(
        "Flagship UI release gate channelId does not match the selected release channel "
        f"({flagship_channel_id!r} != {release_channel_channel_id!r})."
    )
if flagship_release_version and flagship_release_version != release_channel_version:
    reasons.append(
        "Flagship UI release gate releaseVersion does not match the selected release channel "
        f"({flagship_release_version!r} != {release_channel_version!r})."
    )

flagship_release_channel_evidence = flagship_gate.get("releaseChannelEvidence")
if not isinstance(flagship_release_channel_evidence, dict):
    flagship_release_channel_evidence = {}
    reasons.append(
        "Flagship UI release gate receipt is missing releaseChannelEvidence."
    )
release_channel_sha256 = (
    hashlib.sha256(release_channel_bytes).hexdigest()
    if release_channel_bytes
    else ""
)
flagship_release_evidence_mismatches: Dict[str, Dict[str, Any]] = {}


def record_release_evidence_mismatch(
    key: str,
    expected: Any,
    observed: Any,
) -> None:
    if observed != expected:
        flagship_release_evidence_mismatches[key] = {
            "expected": expected,
            "observed": observed,
        }


record_release_evidence_mismatch(
    "path",
    release_channel_resolved_path,
    trimmed_string_field(flagship_release_channel_evidence, "path"),
)
record_release_evidence_mismatch(
    "contract_name",
    release_channel_contract_name,
    trimmed_string_field(flagship_release_channel_evidence, "contract_name"),
)
record_release_evidence_mismatch(
    "status",
    release_channel_status,
    normalize_token(trimmed_string_field(flagship_release_channel_evidence, "status")),
)
record_release_evidence_mismatch(
    "channelId",
    release_channel_channel_id,
    normalize_token(trimmed_string_field(flagship_release_channel_evidence, "channelId")),
)
record_release_evidence_mismatch(
    "releaseVersion",
    release_channel_version,
    trimmed_string_field(flagship_release_channel_evidence, "releaseVersion"),
)
record_release_evidence_mismatch(
    "sha256",
    release_channel_sha256,
    trimmed_string_field(flagship_release_channel_evidence, "sha256"),
)
release_evidence_size = flagship_release_channel_evidence.get("sizeBytes")
record_release_evidence_mismatch(
    "sizeBytes",
    len(release_channel_bytes),
    release_evidence_size
    if isinstance(release_evidence_size, int) and not isinstance(release_evidence_size, bool)
    else None,
)
record_release_evidence_mismatch(
    "generatedAt",
    release_channel_generated_at_raw,
    trimmed_string_field(flagship_release_channel_evidence, "generatedAt"),
)
evidence["flagship_release_channel_evidence"] = flagship_release_channel_evidence
evidence["flagship_release_channel_evidence_mismatches"] = (
    flagship_release_evidence_mismatches
)
if flagship_release_evidence_mismatches:
    reasons.append(
        "Flagship UI release gate releaseChannelEvidence does not bind the exact selected release channel bytes and identity: "
        + ", ".join(sorted(flagship_release_evidence_mismatches))
    )
validate_receipt_freshness(
    "release_channel",
    release_channel,
    reasons,
    evidence,
    enforce_max_age=False,
)
flagship_gate_review_reasons = list(reasons[flagship_gate_review_start:])

prerequisite_receipt_review_start = len(reasons)
prerequisite_receipts = {
    "chummer5a_layout_hard_gate": (
        layout_hard_gate_receipt_path,
        load_json(layout_hard_gate_receipt_path),
    ),
    "chummer5a_legacy_equivalent_chrome_gate": (
        legacy_equivalent_chrome_gate_receipt_path,
        load_json(legacy_equivalent_chrome_gate_receipt_path),
    ),
}
prerequisite_contracts = {
    "chummer5a_layout_hard_gate": "chummer6-ui.chummer5a_layout_hard_gate",
    "chummer5a_legacy_equivalent_chrome_gate": "chummer6-ui.chummer5a_legacy_equivalent_chrome_gate",
}
for prerequisite_label, (prerequisite_path, prerequisite_payload) in prerequisite_receipts.items():
    prerequisite_status = str(prerequisite_payload.get("status") or "").strip().lower()
    evidence[f"{prerequisite_label}_path"] = str(prerequisite_path)
    evidence[f"{prerequisite_label}_status"] = prerequisite_status
    if not prerequisite_path.is_file() or not prerequisite_payload:
        reasons.append(f"{prerequisite_label} receipt is missing or unreadable.")
    if not status_ok(prerequisite_status):
        reasons.append(f"{prerequisite_label} receipt is not passing.")
    prerequisite_contract = str(
        prerequisite_payload.get("contract_name")
        or prerequisite_payload.get("contractName")
        or ""
    ).strip()
    evidence[f"{prerequisite_label}_contract"] = prerequisite_contract
    if prerequisite_contract != prerequisite_contracts[prerequisite_label]:
        reasons.append(f"{prerequisite_label} receipt contract is not recognized.")
    validate_receipt_freshness(
        prerequisite_label,
        prerequisite_payload,
        reasons,
        evidence,
        max_age_seconds=PREREQUISITE_PROOF_MAX_AGE_SECONDS,
        max_future_skew_seconds=PREREQUISITE_PROOF_MAX_FUTURE_SKEW_SECONDS,
    )
# Muscle-memory is a consumer of local screenshot comparison, which consumes
# screenshot review, which in turn consumes this visual receipt. Keep its
# status visible here, but never make that downstream receipt a prerequisite.
muscle_memory_parity_gate_receipt = load_json(muscle_memory_parity_gate_receipt_path)
evidence["chummer5a_muscle_memory_parity_gate_path"] = str(
    muscle_memory_parity_gate_receipt_path
)
evidence["chummer5a_muscle_memory_parity_gate_status"] = normalize_token(
    muscle_memory_parity_gate_receipt.get("status")
)
evidence["chummer5a_muscle_memory_parity_gate_role"] = (
    "downstream_observation"
)
prerequisite_receipt_review_reasons = list(reasons[prerequisite_receipt_review_start:])

head_proof_review_start = len(reasons)
interaction_proof = flagship_gate.get("interactionProof") if isinstance(flagship_gate.get("interactionProof"), dict) else {}
head_proofs = flagship_gate.get("headProofs") if isinstance(flagship_gate.get("headProofs"), dict) else {}
flagship_required_desktop_heads = sorted(
    {
        normalize_token(item)
        for item in (
            flagship_gate.get("desktopHeads")
            if isinstance(flagship_gate.get("desktopHeads"), list)
            else [flagship_gate.get("desktopHead")] if flagship_gate.get("desktopHead") else []
        )
        if normalize_token(item)
    }
)
canonical_required_desktop_heads = ["avalonia"]
missing_canonical_required_desktop_heads = [
    head for head in canonical_required_desktop_heads
    if head not in flagship_required_desktop_heads
]
flagship_head_proof_statuses = normalize_head_proof_statuses(
    head_proofs,
    "flagship_gate.headProofs.status",
    evidence,
    reasons,
)
required_head_contract_markers = {
    "avalonia": [
        "status",
        "visualReview",
        "themeReadabilityContrast",
        "bundledDemoRunner",
        "requiredRuntimeBackedTests",
        "sourceTestFile",
        "testSuites",
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
        "requiredShellTests",
        "sourceTestFile",
        "testSuites",
    ],
}
required_head_status_markers = {
    "avalonia": [
        "status",
        "visualReview",
        "themeReadabilityContrast",
        "bundledDemoRunner",
    ],
    "blazor-desktop": [
        "status",
        "shellChrome",
        "commandSurface",
        "dialogSurface",
        "journeyPanels",
    ],
}
required_head_list_markers = {
    "avalonia": [
        "requiredRuntimeBackedTests",
        "testSuites",
    ],
    "blazor-desktop": [
        "requiredShellTests",
        "testSuites",
    ],
}
flagship_head_contract_marker_statuses: Dict[str, Dict[str, str]] = {}
flagship_head_missing_contract_markers: Dict[str, List[str]] = {}
flagship_head_source_test_file_paths: Dict[str, str] = {}
flagship_head_source_test_file_exists: Dict[str, bool] = {}
flagship_head_source_test_file_within_repo_root: Dict[str, bool] = {}
canonical_head_source_test_files = {
    "avalonia": ui_gate_tests_path,
    "blazor-desktop": desktop_shell_ruleset_tests_path,
}
for required_head in flagship_required_desktop_heads:
    proof_payload = head_proofs.get(required_head) if isinstance(head_proofs.get(required_head), dict) else {}
    required_markers = required_head_contract_markers.get(required_head, ["status", "sourceTestFile", "testSuites"])
    status_markers = set(required_head_status_markers.get(required_head, ["status"]))
    list_markers = set(required_head_list_markers.get(required_head, ["testSuites"]))
    marker_statuses: Dict[str, str] = {}
    missing_markers: List[str] = []
    source_test_file_value = str(proof_payload.get("sourceTestFile") or "").strip()
    source_test_file_path = Path(source_test_file_value) if source_test_file_value else None
    source_test_file_exists = source_test_file_path is not None and source_test_file_path.is_file()
    source_test_file_within_repo_root = (
        path_within_root(source_test_file_path, repo_root) if source_test_file_path is not None else False
    )
    canonical_source_test_file_path = canonical_head_source_test_files.get(required_head)
    if (
        canonical_source_test_file_path is not None
        and canonical_source_test_file_path.is_file()
        and (
            source_test_file_path is None
            or not source_test_file_exists
            or not source_test_file_within_repo_root
        )
    ):
        source_test_file_path = canonical_source_test_file_path
        source_test_file_value = str(canonical_source_test_file_path)
        source_test_file_exists = True
        source_test_file_within_repo_root = True
    flagship_head_source_test_file_paths[required_head] = source_test_file_value
    flagship_head_source_test_file_exists[required_head] = source_test_file_exists
    flagship_head_source_test_file_within_repo_root[required_head] = source_test_file_within_repo_root
    for marker in required_markers:
        marker_value = proof_payload.get(marker)
        marker_ok = False
        if marker == "sourceTestFile":
            marker_ok = source_test_file_exists and source_test_file_within_repo_root
        elif marker in list_markers:
            marker_ok = (
                isinstance(marker_value, list)
                and any(str(item).strip() for item in marker_value)
            )
        elif marker in status_markers:
            marker_ok = status_ok(str(marker_value or "").strip().lower())
        else:
            marker_ok = bool(str(marker_value or "").strip())
        marker_statuses[marker] = "pass" if marker_ok else "fail"
        if not marker_ok:
            missing_markers.append(marker)
    flagship_head_contract_marker_statuses[required_head] = marker_statuses
    flagship_head_missing_contract_markers[required_head] = missing_markers
    if missing_markers:
        reasons.append(
            f"Flagship UI release gate head proof for required desktop head '{required_head}' is missing required contract marker(s): "
            + ", ".join(missing_markers)
        )
    if source_test_file_value and source_test_file_exists and not source_test_file_within_repo_root:
        reasons.append(
            f"Flagship UI release gate sourceTestFile for required desktop head '{required_head}' is outside this repo root."
        )
    if source_test_file_value and not source_test_file_exists:
        reasons.append(
            f"Flagship UI release gate sourceTestFile for required desktop head '{required_head}' is missing/unreadable on disk."
        )
avalonia_head_proof = head_proofs.get("avalonia") if isinstance(head_proofs.get("avalonia"), dict) else {}
blazor_head_proof = head_proofs.get("blazor-desktop") if isinstance(head_proofs.get("blazor-desktop"), dict) else {}
theme_readability_contrast = str(interaction_proof.get("themeReadabilityContrast") or "").strip().lower()
menu_surface = str(interaction_proof.get("menuSurface") or "").strip().lower()
settings_inline_dialog = str(interaction_proof.get("settingsInlineDialog") or "").strip().lower()
demo_runner_dispatch = str(interaction_proof.get("demoRunnerDispatch") or "").strip().lower()
keyboard_shortcut_parity = str(interaction_proof.get("keyboardShortcutParity") or "").strip().lower()
cross_head_workflow_parity = str(interaction_proof.get("crossHeadWorkflowParity") or "").strip().lower()
install_update_recovery_lifecycle = str(interaction_proof.get("installUpdateRecoveryLifecycle") or "").strip().lower()
runtime_backed_sr4_codex_orientation_model = str(interaction_proof.get("runtimeBackedSr4CodexOrientationModel") or "").strip().lower()
runtime_backed_sr5_codex_orientation_model = str(interaction_proof.get("runtimeBackedSr5CodexOrientationModel") or "").strip().lower()
runtime_backed_sr6_codex_orientation_model = str(interaction_proof.get("runtimeBackedSr6CodexOrientationModel") or "").strip().lower()
evidence["flagship_theme_readability_contrast"] = theme_readability_contrast
evidence["flagship_menu_surface"] = menu_surface
evidence["flagship_settings_inline_dialog"] = settings_inline_dialog
evidence["flagship_demo_runner_dispatch"] = demo_runner_dispatch
evidence["flagship_keyboard_shortcut_parity"] = keyboard_shortcut_parity
evidence["flagship_cross_head_workflow_parity"] = cross_head_workflow_parity
evidence["flagship_install_update_recovery_lifecycle"] = install_update_recovery_lifecycle
evidence["flagship_runtime_backed_sr4_codex_orientation_model"] = runtime_backed_sr4_codex_orientation_model
evidence["flagship_runtime_backed_sr5_codex_orientation_model"] = runtime_backed_sr5_codex_orientation_model
evidence["flagship_runtime_backed_sr6_codex_orientation_model"] = runtime_backed_sr6_codex_orientation_model
evidence["flagship_avalonia_head_proof_status"] = str(avalonia_head_proof.get("status") or "").strip().lower()
evidence["flagship_blazor_head_proof_status"] = str(blazor_head_proof.get("status") or "").strip().lower()
evidence["flagship_required_desktop_heads"] = flagship_required_desktop_heads
evidence["canonical_required_desktop_heads"] = canonical_required_desktop_heads
evidence["flagship_missing_canonical_required_desktop_heads"] = (
    missing_canonical_required_desktop_heads
)
evidence["flagship_head_proof_statuses"] = flagship_head_proof_statuses
evidence["required_head_contract_markers"] = required_head_contract_markers
evidence["flagship_head_contract_marker_statuses"] = flagship_head_contract_marker_statuses
evidence["flagship_head_missing_contract_markers"] = flagship_head_missing_contract_markers
evidence["flagship_head_source_test_file_paths"] = flagship_head_source_test_file_paths
evidence["flagship_head_source_test_file_exists"] = flagship_head_source_test_file_exists
evidence["flagship_head_source_test_file_within_repo_root"] = (
    flagship_head_source_test_file_within_repo_root
)
runtime_backed_shell_menu = str(interaction_proof.get("runtimeBackedShellMenu") or "").strip().lower()
runtime_backed_menu_bar_labels = str(interaction_proof.get("runtimeBackedMenuBarLabels") or "").strip().lower()
runtime_backed_clickable_primary_menus = str(interaction_proof.get("runtimeBackedClickablePrimaryMenus") or "").strip().lower()
runtime_backed_toolstrip_actions = str(interaction_proof.get("runtimeBackedToolstripActions") or "").strip().lower()
runtime_backed_codex_tree = str(interaction_proof.get("runtimeBackedCodexTree") or "").strip().lower()
default_single_runner_keeps_workspace_chrome_collapsed = str(
    interaction_proof.get("defaultSingleRunnerKeepsWorkspaceChromeCollapsed") or ""
).strip().lower()
runtime_backed_classic_chrome_copy = str(interaction_proof.get("runtimeBackedClassicChromeCopy") or "").strip().lower()
runtime_backed_tab_panel_only_header = str(interaction_proof.get("runtimeBackedTabPanelOnlyHeader") or "").strip().lower()
runtime_backed_chrome_enabled_after_runner_load = str(interaction_proof.get("runtimeBackedChromeEnabledAfterRunnerLoad") or "").strip().lower()
full_interactive_control_inventory = str(interaction_proof.get("fullInteractiveControlInventory") or "").strip().lower()
main_window_interaction_inventory = str(interaction_proof.get("mainWindowInteractionInventory") or "").strip().lower()
# Backward-compatible aliasing: some generated flagship receipts carry only runtimeBackedShellMenu.
if not runtime_backed_menu_bar_labels:
    runtime_backed_menu_bar_labels = runtime_backed_shell_menu
if not runtime_backed_clickable_primary_menus:
    runtime_backed_clickable_primary_menus = runtime_backed_shell_menu
if not runtime_backed_toolstrip_actions:
    runtime_backed_toolstrip_actions = runtime_backed_shell_menu
if not runtime_backed_chrome_enabled_after_runner_load:
    runtime_backed_chrome_enabled_after_runner_load = runtime_backed_shell_menu
runtime_backed_demo_runner_import = str(interaction_proof.get("runtimeBackedDemoRunnerImport") or "").strip().lower()
runtime_backed_legacy_workbench = str(interaction_proof.get("runtimeBackedLegacyWorkbench") or "").strip().lower()
runtime_backed_file_menu_routes = str(interaction_proof.get("runtimeBackedFileMenuRoutes") or "").strip().lower()
runtime_backed_master_index = str(interaction_proof.get("runtimeBackedMasterIndex") or "").strip().lower()
runtime_backed_character_roster = str(interaction_proof.get("runtimeBackedCharacterRoster") or "").strip().lower()
if not runtime_backed_codex_tree:
    runtime_backed_codex_tree = runtime_backed_legacy_workbench or runtime_backed_shell_menu
if not runtime_backed_file_menu_routes:
    runtime_backed_file_menu_routes = (
        runtime_backed_clickable_primary_menus
        or runtime_backed_shell_menu
        or str(interaction_proof.get("menuSurface") or "").strip().lower()
    )
if not runtime_backed_master_index:
    runtime_backed_master_index = runtime_backed_codex_tree or runtime_backed_legacy_workbench
if not runtime_backed_character_roster:
    runtime_backed_character_roster = (
        main_window_interaction_inventory
        or full_interactive_control_inventory
        or runtime_backed_legacy_workbench
    )
legacy_dense_builder_rhythm = str(interaction_proof.get("legacyDenseBuilderRhythm") or "").strip().lower()
legacy_creation_workflow_rhythm = str(interaction_proof.get("legacyCreationWorkflowRhythm") or "").strip().lower()
legacy_advancement_workflow_rhythm = str(interaction_proof.get("legacyAdvancementWorkflowRhythm") or "").strip().lower()
legacy_browse_detail_confirm_rhythm = str(interaction_proof.get("legacyBrowseDetailConfirmRhythm") or "").strip().lower()
legacy_gear_workflow_rhythm = str(interaction_proof.get("legacyGearWorkflowRhythm") or "").strip().lower()
legacy_vehicles_builder_rhythm = str(interaction_proof.get("legacyVehiclesBuilderRhythm") or "").strip().lower()
legacy_cyberware_dialog_rhythm = str(interaction_proof.get("legacyCyberwareDialogRhythm") or "").strip().lower()
legacy_contacts_diary_rhythm = str(interaction_proof.get("legacyContactsDiaryRhythm") or "").strip().lower()
legacy_contacts_workflow_rhythm = str(interaction_proof.get("legacyContactsWorkflowRhythm") or "").strip().lower()
legacy_diary_workflow_rhythm = str(interaction_proof.get("legacyDiaryWorkflowRhythm") or "").strip().lower()
legacy_magic_workflow_rhythm = str(interaction_proof.get("legacyMagicWorkflowRhythm") or "").strip().lower()
legacy_matrix_workflow_rhythm = str(interaction_proof.get("legacyMatrixWorkflowRhythm") or "").strip().lower()
legacy_mainframe_visual_similarity = str(interaction_proof.get("legacyMainframeVisualSimilarity") or "").strip().lower()
legacy_familiarity_bridge = str(interaction_proof.get("legacyFamiliarityBridge") or "").strip().lower()
if not legacy_mainframe_visual_similarity:
    legacy_mainframe_visual_similarity = legacy_familiarity_bridge or runtime_backed_legacy_workbench
# Backward-compatible aliases let older flagship receipts satisfy the newer canonical interaction surface contract.
required_legacy_interaction_statuses = {
    "runtimeBackedLegacyWorkbench": runtime_backed_legacy_workbench,
    "runtimeBackedFileMenuRoutes": runtime_backed_file_menu_routes,
    "runtimeBackedMasterIndex": runtime_backed_master_index,
    "runtimeBackedCharacterRoster": runtime_backed_character_roster,
    "defaultSingleRunnerKeepsWorkspaceChromeCollapsed": default_single_runner_keeps_workspace_chrome_collapsed,
    "legacyMainframeVisualSimilarity": legacy_mainframe_visual_similarity,
    "legacyDenseBuilderRhythm": legacy_dense_builder_rhythm,
    "legacyCreationWorkflowRhythm": legacy_creation_workflow_rhythm,
    "legacyAdvancementWorkflowRhythm": legacy_advancement_workflow_rhythm,
    "legacyBrowseDetailConfirmRhythm": legacy_browse_detail_confirm_rhythm,
    "legacyContactsDiaryRhythm": legacy_contacts_diary_rhythm,
    "legacyMagicWorkflowRhythm": legacy_magic_workflow_rhythm,
    "legacyMatrixWorkflowRhythm": legacy_matrix_workflow_rhythm,
    "legacyGearWorkflowRhythm": legacy_gear_workflow_rhythm,
    "legacyCyberwareDialogRhythm": legacy_cyberware_dialog_rhythm,
    "legacyVehiclesBuilderRhythm": legacy_vehicles_builder_rhythm,
    "legacyContactsWorkflowRhythm": legacy_contacts_workflow_rhythm,
    "legacyDiaryWorkflowRhythm": legacy_diary_workflow_rhythm,
}
required_legacy_interaction_keys = list(required_legacy_interaction_statuses)
missing_required_legacy_interaction_keys = [
    key for key, value in required_legacy_interaction_statuses.items()
    if not str(value or "").strip()
]
evidence["runtime_backed_shell_menu"] = runtime_backed_shell_menu
evidence["runtime_backed_menu_bar_labels"] = runtime_backed_menu_bar_labels
evidence["runtime_backed_clickable_primary_menus"] = runtime_backed_clickable_primary_menus
evidence["runtime_backed_toolstrip_actions"] = runtime_backed_toolstrip_actions
evidence["runtime_backed_codex_tree"] = runtime_backed_codex_tree
evidence["default_single_runner_keeps_workspace_chrome_collapsed"] = default_single_runner_keeps_workspace_chrome_collapsed
evidence["runtime_backed_classic_chrome_copy"] = runtime_backed_classic_chrome_copy
evidence["runtime_backed_tab_panel_only_header"] = runtime_backed_tab_panel_only_header
evidence["runtime_backed_chrome_enabled_after_runner_load"] = runtime_backed_chrome_enabled_after_runner_load
evidence["full_interactive_control_inventory"] = full_interactive_control_inventory
evidence["main_window_interaction_inventory"] = main_window_interaction_inventory
evidence["runtime_backed_demo_runner_import"] = runtime_backed_demo_runner_import
evidence["runtime_backed_legacy_workbench"] = runtime_backed_legacy_workbench
evidence["runtime_backed_file_menu_routes"] = runtime_backed_file_menu_routes
evidence["runtime_backed_master_index"] = runtime_backed_master_index
evidence["runtime_backed_character_roster"] = runtime_backed_character_roster
evidence["legacy_dense_builder_rhythm"] = legacy_dense_builder_rhythm
evidence["legacy_creation_workflow_rhythm"] = legacy_creation_workflow_rhythm
evidence["legacy_advancement_workflow_rhythm"] = legacy_advancement_workflow_rhythm
evidence["legacy_browse_detail_confirm_rhythm"] = legacy_browse_detail_confirm_rhythm
evidence["legacy_gear_workflow_rhythm"] = legacy_gear_workflow_rhythm
evidence["legacy_vehicles_builder_rhythm"] = legacy_vehicles_builder_rhythm
evidence["legacy_cyberware_dialog_rhythm"] = legacy_cyberware_dialog_rhythm
evidence["legacy_contacts_diary_rhythm"] = legacy_contacts_diary_rhythm
evidence["legacy_contacts_workflow_rhythm"] = legacy_contacts_workflow_rhythm
evidence["legacy_diary_workflow_rhythm"] = legacy_diary_workflow_rhythm
evidence["legacy_magic_workflow_rhythm"] = legacy_magic_workflow_rhythm
evidence["legacy_matrix_workflow_rhythm"] = legacy_matrix_workflow_rhythm
evidence["legacy_mainframe_visual_similarity"] = legacy_mainframe_visual_similarity
evidence["legacy_familiarity_bridge"] = legacy_familiarity_bridge
evidence["required_legacy_interaction_keys"] = required_legacy_interaction_keys
evidence["missing_required_legacy_interaction_keys"] = missing_required_legacy_interaction_keys
if missing_required_legacy_interaction_keys:
    reasons.append(
        "Flagship UI release gate is missing explicit legacy workflow interaction proof keys: "
        + ", ".join(missing_required_legacy_interaction_keys)
    )
if not status_ok(theme_readability_contrast):
    reasons.append("Flagship UI release gate does not report a passing readability contrast proof.")
if not status_ok(menu_surface):
    reasons.append("Flagship UI release gate does not prove runtime-backed menu surface interaction parity.")
if not status_ok(settings_inline_dialog):
    reasons.append("Flagship UI release gate does not prove interactive settings inline-dialog parity.")
if not status_ok(demo_runner_dispatch):
    reasons.append("Flagship UI release gate does not prove runtime-backed demo-runner dispatch.")
if not status_ok(keyboard_shortcut_parity):
    reasons.append("Flagship UI release gate does not prove keyboard shortcut parity.")
if not status_ok(cross_head_workflow_parity):
    reasons.append("Flagship UI release gate does not prove cross-head workflow parity.")
if not status_ok(install_update_recovery_lifecycle):
    reasons.append("Flagship UI release gate does not prove install/update/recovery lifecycle parity.")
if not status_ok(runtime_backed_sr4_codex_orientation_model):
    reasons.append("Flagship UI release gate does not prove SR4 codex orientation parity.")
if not status_ok(runtime_backed_sr5_codex_orientation_model):
    reasons.append("Flagship UI release gate does not prove SR5 codex orientation parity.")
if not status_ok(runtime_backed_sr6_codex_orientation_model):
    reasons.append("Flagship UI release gate does not prove SR6 codex orientation parity.")
if not status_ok(str(avalonia_head_proof.get("status") or "").strip().lower()):
    reasons.append("Flagship UI release gate does not carry a passing Avalonia head proof.")
if not status_ok(str(blazor_head_proof.get("status") or "").strip().lower()):
    reasons.append("Flagship UI release gate does not carry a passing Blazor desktop head proof.")
if not flagship_required_desktop_heads:
    reasons.append("Flagship UI release gate is missing required desktopHeads inventory for per-head visual proof.")
if missing_canonical_required_desktop_heads:
    reasons.append(
        "Flagship UI release gate desktopHeads is missing canonical required desktop head(s) for milestone-3 per-head visual proof: "
        + ", ".join(missing_canonical_required_desktop_heads)
    )
for required_head in flagship_required_desktop_heads:
    required_head_status = flagship_head_proof_statuses.get(required_head, "")
    if not status_ok(required_head_status):
        reasons.append(
            f"Flagship UI release gate does not carry a passing head proof for required desktop head '{required_head}'."
        )
head_proof_review_reasons = list(reasons[head_proof_review_start:])

interaction_proof_review_start = len(reasons)
if not status_ok(runtime_backed_shell_menu):
    reasons.append("Flagship UI release gate does not prove runtime-backed shell menu behavior.")
if not status_ok(runtime_backed_menu_bar_labels):
    reasons.append("Flagship UI release gate does not prove runtime-backed classic menu labels.")
if not status_ok(runtime_backed_clickable_primary_menus):
    reasons.append("Flagship UI release gate does not prove runtime-backed clickable primary menus.")
if not status_ok(runtime_backed_toolstrip_actions):
    reasons.append("Flagship UI release gate does not prove runtime-backed labeled workbench actions.")
if not status_ok(runtime_backed_codex_tree):
    reasons.append("Flagship UI release gate does not prove the auxiliary runtime-backed navigator/workspace rail contract.")
if not status_ok(default_single_runner_keeps_workspace_chrome_collapsed):
    reasons.append("Flagship UI release gate does not prove the default single-runner shell collapses workspace chrome and preserves center-first density.")
if not status_ok(runtime_backed_classic_chrome_copy):
    reasons.append("Flagship UI release gate does not prove runtime-backed classic chrome copy and anti-dashboard posture.")
if not status_ok(runtime_backed_tab_panel_only_header):
    reasons.append("Flagship UI release gate does not prove the loaded-runner header stays tab-panel-only.")
if not status_ok(runtime_backed_chrome_enabled_after_runner_load):
    reasons.append("Flagship UI release gate does not prove runtime-backed shell chrome stays enabled after a real runner load.")
if not status_ok(full_interactive_control_inventory):
    reasons.append("Flagship UI release gate does not prove the standalone interactive control inventory.")
if not status_ok(main_window_interaction_inventory):
    reasons.append("Flagship UI release gate does not prove the main-window interaction inventory.")
if not status_ok(runtime_backed_demo_runner_import):
    reasons.append("Flagship UI release gate does not prove runtime-backed demo-runner import.")
if not status_ok(runtime_backed_legacy_workbench):
    reasons.append("Flagship UI release gate does not prove a runtime-backed legacy frmCareer workbench.")
if not status_ok(legacy_dense_builder_rhythm):
    reasons.append("Flagship UI release gate does not prove dense builder rhythm familiarity.")
if not status_ok(legacy_creation_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove character creation workflow familiarity.")
if not status_ok(legacy_advancement_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove advancement workflow familiarity.")
if not status_ok(legacy_browse_detail_confirm_rhythm):
    reasons.append("Flagship UI release gate does not prove browse-detail-confirm familiarity.")
if not status_ok(legacy_gear_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove gear workflow familiarity.")
if not status_ok(legacy_vehicles_builder_rhythm):
    reasons.append("Flagship UI release gate does not prove vehicles/drones browse-detail-confirm familiarity.")
if not status_ok(legacy_cyberware_dialog_rhythm):
    reasons.append("Flagship UI release gate does not prove cyberware dialog familiarity.")
if not status_ok(legacy_contacts_diary_rhythm):
    reasons.append("Flagship UI release gate does not prove contacts/diary familiarity.")
if not status_ok(legacy_contacts_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove contacts workflow familiarity.")
if not status_ok(legacy_diary_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove diary workflow familiarity.")
if not status_ok(legacy_magic_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove magic workflow familiarity.")
if not status_ok(legacy_matrix_workflow_rhythm):
    reasons.append("Flagship UI release gate does not prove matrix workflow familiarity.")
interaction_proof_review_reasons = list(reasons[interaction_proof_review_start:])

source_anchor_review_start = len(reasons)
required_theme_tokens = {
    "ChummerShellActiveMenuBorderBrush_light": "#1C4A2D",
    "ChummerShellAccentButtonBrush": "#1C4A2D",
    "ChummerShellSuccessBrush": "#1C4A2D",
    "ChummerShellActiveMenuBackgroundBrush_dark": "#1C4A2D",
    "ChummerShellActiveMenuBorderBrush_dark": "#90C39A",
}
theme_text = app_axaml_path.read_text(encoding="utf-8") if app_axaml_path.is_file() else ""
missing_theme_tokens: List[str] = []
for label, value in required_theme_tokens.items():
    if value not in theme_text:
        missing_theme_tokens.append(f"{label}={value}")
evidence["missing_theme_tokens"] = missing_theme_tokens
if missing_theme_tokens:
    reasons.append("Theme familiarity anchors are missing: " + ", ".join(missing_theme_tokens))

required_test_names = [
    "Desktop_shell_preserves_chummer5a_familiarity_cues",
    "Desktop_shell_preserves_classic_dense_three_pane_workbench_posture",
    "Desktop_shell_preserves_classic_dense_center_first_workbench_posture",
    "Theme_tokens_preserve_chummer5a_palette_and_readability",
    "Loaded_runner_preserves_visible_character_tab_posture",
    "Loaded_runner_header_stays_tab_panel_only_without_metric_cards",
    "Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks",
    "Character_creation_preserves_familiar_dense_builder_rhythm",
    "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm",
    "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Vehicles_and_drones_builder_preserves_familiar_browse_detail_confirm_rhythm",
    "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues",
    "Contacts_diary_and_support_routes_execute_with_public_path_visibility",
    "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions",
    "Runtime_backed_menu_bar_preserves_classic_labels_and_clickable_primary_menus",
    "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions",
    "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
    "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
    "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome",
    "Veteran_first_minute_flow_keeps_menu_toolstrip_settings_import_master_index_and_roster_reachable_on_promoted_head",
    "Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff",
    "Standalone_summary_header_tab_buttons_raise_expected_events",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
    "Opening_mainframe_preserves_chummer5a_successor_workbench_posture",
    "Master_index_is_a_first_class_runtime_backed_workbench_route",
    "Character_roster_is_a_first_class_runtime_backed_workbench_route",
]
test_text = ui_gate_tests_path.read_text(encoding="utf-8") if ui_gate_tests_path.is_file() else ""
desktop_shell_test_text = desktop_shell_ruleset_tests_path.read_text(encoding="utf-8") if desktop_shell_ruleset_tests_path.is_file() else ""
required_test_aliases = {
    "Desktop_shell_preserves_classic_dense_center_first_workbench_posture": [
        ["Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks"],
    ],
    "Runtime_backed_file_menu_preserves_working_open_save_import_routes": [
        [
            "Menu_click_surfaces_visible_command_choices_in_shell_using_runtime_backed_presenters",
            "File_menu_new_character_creates_runtime_workspace",
        ],
    ],
    "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist": [
        [
            "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
            "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
        ],
    ],
    "Standalone_summary_header_tab_buttons_raise_expected_events": [
        ["Standalone_summary_header_keeps_navigation_tabs_visible_without_restore_handoff"],
    ],
    "Opening_mainframe_preserves_chummer5a_successor_workbench_posture": [
        ["Loaded_runner_workbench_preserves_legacy_frmcareer_landmarks"],
    ],
    "Master_index_is_a_first_class_runtime_backed_workbench_route": [
        ["Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome"],
    ],
    "Character_roster_is_a_first_class_runtime_backed_workbench_route": [
        ["Desktop_surface_commands_open_settings_master_index_and_roster_from_visible_chrome"],
    ],
}

def source_contains(required_name: str) -> bool:
    if required_name in test_text or required_name in desktop_shell_test_text:
        return True
    for bundle in required_test_aliases.get(required_name, []):
        if all(alias in test_text or alias in desktop_shell_test_text for alias in bundle):
            return True
    return False

missing_tests = [name for name in required_test_names if not source_contains(name)]
evidence["required_tests"] = required_test_names
evidence["missing_tests"] = missing_tests
evidence["required_test_aliases"] = required_test_aliases
if missing_tests:
    reasons.append("Visual familiarity tests are missing: " + ", ".join(missing_tests))

required_desktop_shell_test_names = [
    "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
    "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
]
missing_desktop_shell_tests = [name for name in required_desktop_shell_test_names if name not in desktop_shell_test_text]
evidence["required_desktop_shell_tests"] = required_desktop_shell_test_names
evidence["missing_desktop_shell_tests"] = missing_desktop_shell_tests
if missing_desktop_shell_tests:
    reasons.append("Desktop shell layout tests are missing: " + ", ".join(missing_desktop_shell_tests))

toolstrip_axaml_text = toolstrip_axaml_path.read_text(encoding="utf-8") if toolstrip_axaml_path.is_file() else ""
toolstrip_codebehind_text = toolstrip_codebehind_path.read_text(encoding="utf-8") if toolstrip_codebehind_path.is_file() else ""
required_toolstrip_markers = [
    "WrapPanel x:Name=\"ClassicActionStrip\"",
    "Orientation=\"Horizontal\"",
    "ItemHeight=\"28\"",
    "button.Content = label;",
]
missing_toolstrip_markers = [
    marker
    for marker in required_toolstrip_markers
    if marker not in toolstrip_axaml_text and marker not in toolstrip_codebehind_text
]
disallowed_toolstrip_markers = [
    "shell-action-badge",
    "shell-action-caption",
    "Quick Actions",
    "Workbench State",
    "BuildActionContent(",
]
present_disallowed_toolstrip_markers = [
    marker
    for marker in disallowed_toolstrip_markers
    if marker in toolstrip_axaml_text or marker in toolstrip_codebehind_text or marker in theme_text
]
evidence["required_toolstrip_markers"] = required_toolstrip_markers
evidence["missing_toolstrip_markers"] = missing_toolstrip_markers
evidence["disallowed_toolstrip_markers"] = disallowed_toolstrip_markers
evidence["present_disallowed_toolstrip_markers"] = present_disallowed_toolstrip_markers
if missing_toolstrip_markers:
    reasons.append("Classic toolbar source anchors are missing: " + ", ".join(missing_toolstrip_markers))
if present_disallowed_toolstrip_markers:
    reasons.append("Dashboard-style toolbar chrome is still present in source: " + ", ".join(present_disallowed_toolstrip_markers))

summary_header_text = summary_header_axaml_path.read_text(encoding="utf-8") if summary_header_axaml_path.is_file() else ""
section_host_axaml_path = repo_root / "Chummer.Avalonia/Controls/SectionHostControl.axaml"
section_host_text = section_host_axaml_path.read_text(encoding="utf-8") if section_host_axaml_path.is_file() else ""
loaded_runner_tab_host_text = summary_header_text + "\n" + section_host_text
evidence["loaded_runner_tab_host_axaml_path"] = str(section_host_axaml_path)
required_summary_header_markers = [
    "x:Name=\"LoadedRunnerTabStripBorder\"",
    "x:Name=\"LoadedRunnerTabStrip\"",
]
missing_summary_header_markers = [
    marker for marker in required_summary_header_markers if marker not in loaded_runner_tab_host_text
]
disallowed_summary_header_markers = [
    "NameValueText",
    "AliasValueText",
    "KarmaValueText",
    "SkillsValueText",
    "RuntimeValueText",
    "RuntimeInspectButton",
    "Text=\"Name\"",
    "Text=\"Alias\"",
    "Text=\"Karma\"",
    "Text=\"Skills\"",
    "Text=\"Runtime\"",
]
present_disallowed_summary_header_markers = [
    marker for marker in disallowed_summary_header_markers if marker in summary_header_text
]
evidence["required_summary_header_markers"] = required_summary_header_markers
evidence["missing_summary_header_markers"] = missing_summary_header_markers
evidence["disallowed_summary_header_markers"] = disallowed_summary_header_markers
evidence["present_disallowed_summary_header_markers"] = present_disallowed_summary_header_markers
if missing_summary_header_markers:
    reasons.append("Loaded-runner header no longer guarantees the visible tab-panel posture: " + ", ".join(missing_summary_header_markers))
if present_disallowed_summary_header_markers:
    reasons.append("Loaded-runner header still carries metric-card chrome instead of a tab panel: " + ", ".join(present_disallowed_summary_header_markers))

classic_copy_disallowed_markers = [
    "Career-style workbench",
    "Command Palette",
    "Coach Sidecar",
    "Coach Launch",
    "Recent Coach Guidance",
]
classic_copy_present_markers: List[str] = []
for extra_path in (
    repo_root / "Chummer.Avalonia/Controls/ShellMenuBarControl.axaml",
    repo_root / "Chummer.Avalonia/Controls/CommandDialogPaneControl.axaml",
    repo_root / "Chummer.Avalonia/Controls/CoachSidecarControl.axaml",
):
    if not extra_path.is_file():
        continue
    extra_text = extra_path.read_text(encoding="utf-8")
    for marker in classic_copy_disallowed_markers:
        if marker in extra_text and marker not in classic_copy_present_markers:
            classic_copy_present_markers.append(marker)
evidence["classic_copy_disallowed_markers"] = classic_copy_disallowed_markers
evidence["classic_copy_present_markers"] = classic_copy_present_markers
if classic_copy_present_markers:
    reasons.append("Modern dashboard copy is still present in source: " + ", ".join(classic_copy_present_markers))

toolstrip_labels_method = extract_test_method(test_text, "Runtime_backed_toolstrip_preserves_classic_labeled_workbench_actions")
toolstrip_posture_method = extract_test_method(test_text, "Runtime_backed_toolstrip_preserves_flat_classic_toolbar_posture")
toolstrip_flat_label_markers = [
    "Assert.IsTrue(button.IsVisible",
    "Assert.IsTrue(button.IsEnabled",
    "button.Bounds.Width > 0d && button.Bounds.Height > 0d",
]
missing_toolstrip_flat_label_markers = [
    marker for marker in toolstrip_flat_label_markers if marker not in toolstrip_labels_method
]
toolstrip_posture_markers = [
    "shell-action-badge",
    "shell-action-caption",
    "Quick Actions",
    "Workbench State",
]
missing_toolstrip_posture_markers = [
    marker for marker in toolstrip_posture_markers if marker not in toolstrip_posture_method
]
evidence["missing_toolstrip_flat_label_markers"] = missing_toolstrip_flat_label_markers
evidence["missing_toolstrip_posture_markers"] = missing_toolstrip_posture_markers
if missing_toolstrip_flat_label_markers:
    reasons.append("Toolstrip familiarity proof is too soft: flat-label assertions are missing from the runtime-backed toolbar test.")
if missing_toolstrip_posture_markers:
    reasons.append("Toolstrip familiarity proof is too soft: classic-toolbar posture assertions are missing from the runtime-backed toolbar posture test.")

legacy_frmcareer_text = legacy_frmcareer_designer_path.read_text(encoding="utf-8") if legacy_frmcareer_designer_path.is_file() else ""
legacy_frmcareer_markers = [
    "StatusStrip",
    "pgbProgress",
    "tabCharacterTabs",
    "tabInfo",
    "treQualities",
    "treCyberware",
    "treGear",
    "treArmor",
    "treWeapons",
    "treVehicles",
]
missing_legacy_frmcareer_markers = [marker for marker in legacy_frmcareer_markers if marker not in legacy_frmcareer_text]
evidence["legacy_frmcareer_markers"] = legacy_frmcareer_markers
evidence["missing_legacy_frmcareer_markers"] = missing_legacy_frmcareer_markers
if not legacy_frmcareer_text:
    reasons.append("Legacy frmCareer oracle is unavailable; Chummer5a visual parity cannot be audited honestly.")
elif missing_legacy_frmcareer_markers:
    reasons.append("Legacy frmCareer oracle is incomplete or moved: " + ", ".join(missing_legacy_frmcareer_markers))

screen_capture_review_start = len(reasons)
canonical_screenshot_inventory = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "07-loaded-runner-tabs-light.png",
    "08-cyberware-dialog-light.png",
    "09-vehicles-section-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "15-creation-section-light.png",
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
    "19-workflow-file-menu-loaded-light.png",
    "20-workflow-skills-section-light.png",
    "21-workflow-skill-add-dialog-light.png",
    "22-workflow-qualities-section-light.png",
    "23-workflow-quality-add-dialog-light.png",
    "24-workflow-gear-section-light.png",
    "25-workflow-gear-add-dialog-light.png",
    "26-workflow-weapons-section-light.png",
    "27-workflow-weapon-add-dialog-light.png",
    "28-workflow-armor-section-light.png",
    "29-workflow-armor-add-dialog-light.png",
    "30-workflow-cyberware-section-light.png",
    "31-workflow-powers-section-light.png",
    "32-workflow-adept-power-dialog-light.png",
    "33-workflow-complex-form-dialog-light.png",
    "34-workflow-validate-section-light.png",
    "35-workflow-rules-section-light.png",
    "36-workflow-new-character-dialog-light.png",
    "37-workflow-calendar-section-light.png",
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
    "41-horizons-hub-light.png",
    "42-horizon-karma-forge-light.png",
    "43-horizon-alice-light.png",
    "44-horizon-black-ledger-light.png",
    "45-horizon-run-control-light.png",
    "46-horizon-runsite-light.png",
    "47-horizon-jackpoint-light.png",
    "48-horizon-table-pulse-light.png",
    "49-horizon-community-hub-light.png",
    "50-horizon-nexus-pan-light.png",
    "51-horizon-quicksilver-light.png",
    "52-horizon-runner-passport-light.png",
    "53-horizon-runbook-press-light.png",
    "54-horizon-creator-os-light.png",
    "55-horizon-local-co-processor-light.png",
    "56-horizon-anarchy-light.png",
    "57-horizon-ghostwire-light.png",
    "58-horizon-ready-for-tonight-light.png",
    "60-horizon-knowledge-fabric-light.png",
]
canonical_workflow_coverage = {
    "create-open-import-save-save-as-print-export": [
        "19-workflow-file-menu-loaded-light.png",
        "36-workflow-new-character-dialog-light.png",
        "18-import-dialog-light.png",
        "40-hero-lab-importer-dialog-light.png",
    ],
    "metatype-priorities-karma-entry": [
        "15-creation-section-light.png",
        "11-diary-dialog-light.png",
        "36-workflow-new-character-dialog-light.png",
    ],
    "attributes-skills-skill-groups-specializations-knowledge-languages": [
        "15-creation-section-light.png",
        "20-workflow-skills-section-light.png",
        "21-workflow-skill-add-dialog-light.png",
    ],
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources": [
        "10-contacts-section-light.png",
        "22-workflow-qualities-section-light.png",
        "23-workflow-quality-add-dialog-light.png",
        "37-workflow-calendar-section-light.png",
    ],
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers": [
        "09-vehicles-section-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
        "26-workflow-weapons-section-light.png",
        "27-workflow-weapon-add-dialog-light.png",
        "28-workflow-armor-section-light.png",
        "29-workflow-armor-add-dialog-light.png",
    ],
    "cyberware-bioware-modular-hierarchies-nested-plugins": [
        "08-cyberware-dialog-light.png",
        "30-workflow-cyberware-section-light.png",
    ],
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms": [
        "12-magic-dialog-light.png",
        "13-matrix-dialog-light.png",
        "14-advancement-dialog-light.png",
        "31-workflow-powers-section-light.png",
        "32-workflow-adept-power-dialog-light.png",
        "33-workflow-complex-form-dialog-light.png",
    ],
    "improvements-explain-result-parity": [
        "14-advancement-dialog-light.png",
        "16-master-index-dialog-light.png",
        "34-workflow-validate-section-light.png",
        "35-workflow-rules-section-light.png",
    ],
    "recovery-reload-migration-roundtrips": [
        "04-loaded-runner-light.png",
        "18-import-dialog-light.png",
        "19-workflow-file-menu-loaded-light.png",
    ],
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare": [
        "05-dense-section-light.png",
        "06-dense-section-dark.png",
        "07-loaded-runner-tabs-light.png",
        "24-workflow-gear-section-light.png",
        "25-workflow-gear-add-dialog-light.png",
    ],
    "native-horizons-surface-catalog": [
        "41-horizons-hub-light.png",
        "42-horizon-karma-forge-light.png",
        "43-horizon-alice-light.png",
        "44-horizon-black-ledger-light.png",
        "45-horizon-run-control-light.png",
        "46-horizon-runsite-light.png",
        "47-horizon-jackpoint-light.png",
        "48-horizon-table-pulse-light.png",
        "49-horizon-community-hub-light.png",
        "50-horizon-nexus-pan-light.png",
        "51-horizon-quicksilver-light.png",
        "52-horizon-runner-passport-light.png",
        "53-horizon-runbook-press-light.png",
        "54-horizon-creator-os-light.png",
        "55-horizon-local-co-processor-light.png",
        "56-horizon-anarchy-light.png",
        "57-horizon-ghostwire-light.png",
        "58-horizon-ready-for-tonight-light.png",
        "60-horizon-knowledge-fabric-light.png",
    ],
}
required_screenshots = [
    "01-initial-shell-light.png",
    "02-menu-open-light.png",
    "03-settings-open-light.png",
    "04-loaded-runner-light.png",
    "05-dense-section-light.png",
    "06-dense-section-dark.png",
    "07-loaded-runner-tabs-light.png",
    "08-cyberware-dialog-light.png",
    "09-vehicles-section-light.png",
    "10-contacts-section-light.png",
    "11-diary-dialog-light.png",
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "15-creation-section-light.png",
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
]


def parse_control_generated_at(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        return None
    return parsed.astimezone(timezone.utc)


screenshot_control_bytes = b""
screenshot_control_evidence: Dict[str, Any] = {}
screenshot_dir_symlink_components = symlinked_path_components(screenshot_dir)
screenshot_control_symlink_components = symlinked_path_components(
    screenshot_control_evidence_path
)
screenshot_dir_initial_fingerprint = lstat_fingerprint(screenshot_dir)
screenshot_control_initial_fingerprint = lstat_fingerprint(
    screenshot_control_evidence_path
)
evidence["screenshot_dir_symlink_components"] = screenshot_dir_symlink_components
evidence["screenshot_control_symlink_components"] = (
    screenshot_control_symlink_components
)
if screenshot_dir_symlink_components:
    reasons.append(
        "Screenshot directory path contains symlinked component(s): "
        + ", ".join(screenshot_dir_symlink_components)
    )
if screenshot_control_symlink_components:
    reasons.append(
        "Screenshot control evidence path contains symlinked component(s): "
        + ", ".join(screenshot_control_symlink_components)
    )
if (
    screenshot_dir_initial_fingerprint is None
    or not stat.S_ISDIR(screenshot_dir_initial_fingerprint["mode"])
):
    reasons.append("Screenshot directory must be an existing non-symlink directory.")
if (
    screenshot_control_initial_fingerprint is None
    or not stat.S_ISREG(screenshot_control_initial_fingerprint["mode"])
):
    reasons.append("Screenshot control evidence must be an existing regular file.")

control_path_safe_to_read = (
    not screenshot_control_symlink_components
    and screenshot_control_initial_fingerprint is not None
    and stat.S_ISREG(screenshot_control_initial_fingerprint["mode"])
)
if control_path_safe_to_read:
    try:
        screenshot_control_bytes = screenshot_control_evidence_path.read_bytes()
        screenshot_control_after_read_fingerprint = lstat_fingerprint(
            screenshot_control_evidence_path
        )
        if screenshot_control_after_read_fingerprint != screenshot_control_initial_fingerprint:
            reasons.append("Screenshot control evidence changed while it was being read.")
        loaded_screenshot_control_evidence = json.loads(
            screenshot_control_bytes.decode("utf-8-sig")
        )
        if isinstance(loaded_screenshot_control_evidence, dict):
            screenshot_control_evidence = loaded_screenshot_control_evidence
    except (OSError, UnicodeError, json.JSONDecodeError):
        screenshot_control_evidence = {}
evidence["screenshot_control_schema_version"] = screenshot_control_evidence.get("schemaVersion")
evidence["screenshot_control_contract_name"] = str(
    screenshot_control_evidence.get("contract_name") or ""
).strip()
evidence["screenshot_control_receipt_exists"] = screenshot_control_evidence_path.is_file()
evidence["screenshot_control_receipt_size_bytes"] = len(screenshot_control_bytes)
evidence["screenshot_control_receipt_sha256"] = (
    hashlib.sha256(screenshot_control_bytes).hexdigest()
    if screenshot_control_bytes
    else ""
)
if not screenshot_control_evidence_path.is_file() or not screenshot_control_evidence:
    reasons.append("Screenshot control evidence is missing, unreadable, or not a JSON object.")
if screenshot_control_evidence.get("schemaVersion") != SCREENSHOT_CONTROL_SCHEMA_VERSION:
    reasons.append(
        f"Screenshot control evidence schemaVersion must be {SCREENSHOT_CONTROL_SCHEMA_VERSION}."
    )
if str(screenshot_control_evidence.get("contract_name") or "").strip() != SCREENSHOT_CONTROL_CONTRACT_NAME:
    reasons.append(
        f"Screenshot control evidence contract_name must be {SCREENSHOT_CONTROL_CONTRACT_NAME}."
    )

control_authority = (
    screenshot_control_evidence.get("authority")
    if isinstance(screenshot_control_evidence.get("authority"), dict)
    else {}
)
required_control_authority = {
    "visualBaseline": "Chummer5a",
    "designAuthorityPlatform": "windows",
    "captureHead": "avalonia",
    "captureMode": "avalonia_headless_test_harness",
}
control_authority_mismatches = {
    key: {
        "expected": expected,
        "observed": str(control_authority.get(key) or "").strip(),
    }
    for key, expected in required_control_authority.items()
    if str(control_authority.get(key) or "").strip() != expected
}
evidence["screenshot_control_authority"] = control_authority
evidence["screenshot_control_authority_mismatches"] = control_authority_mismatches
capture_operating_system = str(
    control_authority.get("actualCaptureOperatingSystem") or ""
).strip()
capture_architecture = str(
    control_authority.get("actualCaptureArchitecture") or ""
).strip()
release_candidate_bound = control_authority.get("releaseCandidateBound")
evidence["screenshot_control_actual_capture_operating_system"] = (
    capture_operating_system
)
evidence["screenshot_control_actual_capture_architecture"] = capture_architecture
evidence["screenshot_control_release_candidate_bound"] = release_candidate_bound
if control_authority_mismatches:
    reasons.append(
        "Screenshot control evidence authority does not match the release-authority contract: "
        + ", ".join(
            f"{key}={details['observed']!r} (expected {details['expected']!r})"
            for key, details in sorted(control_authority_mismatches.items())
        )
    )
if not capture_operating_system or not capture_architecture:
    reasons.append(
        "Screenshot control evidence authority is missing actual capture operating-system or architecture identity."
    )
if release_candidate_bound is not False:
    reasons.append(
        "Screenshot control evidence must honestly declare releaseCandidateBound=false for the headless source-build capture lane."
    )

control_generated_at_raw = str(screenshot_control_evidence.get("generatedAt") or "").strip()
control_generated_at = parse_control_generated_at(control_generated_at_raw)
evidence["screenshot_control_generated_at"] = control_generated_at_raw
if control_generated_at is None:
    reasons.append("Screenshot control evidence generatedAt must be a valid offset-aware timestamp.")
else:
    control_age_seconds = int((datetime.now(timezone.utc) - control_generated_at).total_seconds())
    evidence["screenshot_control_age_seconds"] = max(0, control_age_seconds)
    if control_age_seconds < -DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
        reasons.append(
            "Screenshot control evidence generatedAt is too far in the future "
            f"({abs(control_age_seconds)}s ahead; max {DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS}s)."
        )
    elif control_age_seconds > DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS:
        reasons.append(
            "Screenshot control evidence is stale "
            f"({control_age_seconds}s old; max {DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS}s)."
        )

control_entries_raw = screenshot_control_evidence.get("entries")
control_entries = control_entries_raw if isinstance(control_entries_raw, list) else []
control_screenshot_count = screenshot_control_evidence.get("screenshotCount")
evidence["screenshot_control_screenshot_count"] = control_screenshot_count
evidence["screenshot_control_entry_count"] = len(control_entries)
if not isinstance(control_entries_raw, list):
    reasons.append("Screenshot control evidence entries must be an array.")
if (
    not isinstance(control_screenshot_count, int)
    or isinstance(control_screenshot_count, bool)
    or control_screenshot_count <= 0
):
    reasons.append("Screenshot control evidence screenshotCount must be a positive integer.")

control_entries_by_name: Dict[str, Dict[str, Any]] = {}
malformed_control_entries: List[str] = []
duplicate_control_entries: List[str] = []
for index, raw_entry in enumerate(control_entries):
    if not isinstance(raw_entry, dict):
        malformed_control_entries.append(f"index:{index}:not_object")
        continue
    screenshot_name = str(raw_entry.get("screenshot") or "").strip()
    sha256_value = str(raw_entry.get("sha256") or "").strip()
    size_bytes = raw_entry.get("sizeBytes")
    if (
        not screenshot_name
        or Path(screenshot_name).name != screenshot_name
        or not screenshot_name.lower().endswith(".png")
    ):
        malformed_control_entries.append(f"index:{index}:invalid_screenshot")
        continue
    if screenshot_name in control_entries_by_name:
        duplicate_control_entries.append(screenshot_name)
        continue
    if not re.fullmatch(r"[0-9a-f]{64}", sha256_value):
        malformed_control_entries.append(f"{screenshot_name}:invalid_sha256")
        continue
    if not isinstance(size_bytes, int) or isinstance(size_bytes, bool) or size_bytes <= 0:
        malformed_control_entries.append(f"{screenshot_name}:invalid_sizeBytes")
        continue
    control_entries_by_name[screenshot_name] = raw_entry

if isinstance(control_screenshot_count, int) and not isinstance(control_screenshot_count, bool):
    if control_screenshot_count != len(control_entries_by_name):
        reasons.append(
            "Screenshot control evidence screenshotCount does not equal its total unique valid entries count."
        )
if duplicate_control_entries:
    reasons.append(
        "Screenshot control evidence contains duplicate screenshot entries: "
        + ", ".join(sorted(set(duplicate_control_entries)))
    )
if malformed_control_entries:
    reasons.append(
        "Screenshot control evidence contains malformed entries: "
        + ", ".join(malformed_control_entries)
    )

workflow_coverage_raw = screenshot_control_evidence.get("workflowCoverage")
workflow_coverage = workflow_coverage_raw if isinstance(workflow_coverage_raw, list) else []
workflow_coverage_family_ids: List[str] = []
workflow_coverage_duplicate_family_ids: List[str] = []
workflow_coverage_duplicate_screenshots: Dict[str, List[str]] = {}
workflow_coverage_missing_declared_entries: Dict[str, List[str]] = {}
workflow_coverage_malformed_rows: List[str] = []
workflow_coverage_by_family: Dict[str, List[str]] = {}
seen_workflow_family_ids: set[str] = set()
if not isinstance(workflow_coverage_raw, list) or not workflow_coverage:
    reasons.append("Screenshot control evidence workflowCoverage must be a non-empty array.")
for index, raw_row in enumerate(workflow_coverage):
    if not isinstance(raw_row, dict):
        workflow_coverage_malformed_rows.append(f"index:{index}:not_object")
        continue
    family_id = str(raw_row.get("workflowFamilyId") or "").strip()
    normalized_family_id = family_id.lower()
    screenshot_files_raw = raw_row.get("screenshotFiles")
    screenshot_files = (
        [str(item or "").strip() for item in screenshot_files_raw]
        if isinstance(screenshot_files_raw, list)
        else []
    )
    row_screenshot_count = raw_row.get("screenshotCount")
    row_label = family_id or f"index:{index}"
    if not family_id:
        workflow_coverage_malformed_rows.append(f"index:{index}:missing_workflowFamilyId")
    elif normalized_family_id in seen_workflow_family_ids:
        workflow_coverage_duplicate_family_ids.append(family_id)
    else:
        seen_workflow_family_ids.add(normalized_family_id)
        workflow_coverage_family_ids.append(family_id)
    if not isinstance(screenshot_files_raw, list) or not screenshot_files:
        workflow_coverage_malformed_rows.append(f"{row_label}:invalid_screenshotFiles")
    invalid_screenshot_references = [
        name
        for name in screenshot_files
        if not name or Path(name).name != name or not name.lower().endswith(".png")
    ]
    if invalid_screenshot_references:
        workflow_coverage_malformed_rows.append(
            f"{row_label}:invalid_screenshot_references"
        )
    duplicate_screenshot_references = sorted(
        {
            name
            for name in screenshot_files
            if screenshot_files.count(name) > 1
        }
    )
    if duplicate_screenshot_references:
        workflow_coverage_duplicate_screenshots[row_label] = (
            duplicate_screenshot_references
        )
    unique_screenshot_files = set(screenshot_files)
    if family_id:
        workflow_coverage_by_family[family_id] = sorted(unique_screenshot_files)
    if (
        not isinstance(row_screenshot_count, int)
        or isinstance(row_screenshot_count, bool)
        or row_screenshot_count <= 0
        or row_screenshot_count != len(unique_screenshot_files)
    ):
        workflow_coverage_malformed_rows.append(
            f"{row_label}:invalid_screenshotCount"
        )
    missing_declared_references = sorted(
        name
        for name in unique_screenshot_files
        if name not in control_entries_by_name
    )
    if missing_declared_references:
        workflow_coverage_missing_declared_entries[row_label] = (
            missing_declared_references
        )

canonical_workflow_family_ids = set(canonical_workflow_coverage)
observed_workflow_family_ids = set(workflow_coverage_by_family)
missing_canonical_workflow_families = sorted(
    canonical_workflow_family_ids - observed_workflow_family_ids
)
unexpected_workflow_families = sorted(
    observed_workflow_family_ids - canonical_workflow_family_ids
)
mismatched_workflow_family_screenshots = {
    family_id: {
        "expected": sorted(canonical_workflow_coverage[family_id]),
        "observed": workflow_coverage_by_family[family_id],
    }
    for family_id in sorted(
        canonical_workflow_family_ids & observed_workflow_family_ids
    )
    if workflow_coverage_by_family[family_id]
    != sorted(canonical_workflow_coverage[family_id])
}

evidence["screenshot_control_workflow_family_ids"] = workflow_coverage_family_ids
evidence["screenshot_control_workflow_duplicate_family_ids"] = sorted(
    set(workflow_coverage_duplicate_family_ids)
)
evidence["screenshot_control_workflow_duplicate_screenshots"] = (
    workflow_coverage_duplicate_screenshots
)
evidence["screenshot_control_workflow_missing_declared_entries"] = (
    workflow_coverage_missing_declared_entries
)
evidence["screenshot_control_workflow_malformed_rows"] = (
    workflow_coverage_malformed_rows
)
evidence["screenshot_control_workflow_coverage_by_family"] = (
    workflow_coverage_by_family
)
evidence["screenshot_control_workflow_missing_canonical_families"] = (
    missing_canonical_workflow_families
)
evidence["screenshot_control_workflow_unexpected_families"] = (
    unexpected_workflow_families
)
evidence["screenshot_control_workflow_mismatched_family_screenshots"] = (
    mismatched_workflow_family_screenshots
)
if workflow_coverage_duplicate_family_ids:
    reasons.append(
        "Screenshot control evidence workflowCoverage contains duplicate workflowFamilyId values: "
        + ", ".join(sorted(set(workflow_coverage_duplicate_family_ids)))
    )
if workflow_coverage_duplicate_screenshots:
    reasons.append(
        "Screenshot control evidence workflowCoverage rows contain duplicate screenshot names: "
        + "; ".join(
            f"{family_id} ({', '.join(names)})"
            for family_id, names in sorted(workflow_coverage_duplicate_screenshots.items())
        )
    )
if workflow_coverage_missing_declared_entries:
    reasons.append(
        "Screenshot control evidence workflowCoverage references screenshots missing from entries: "
        + "; ".join(
            f"{family_id} ({', '.join(names)})"
            for family_id, names in sorted(workflow_coverage_missing_declared_entries.items())
        )
    )
if workflow_coverage_malformed_rows:
    reasons.append(
        "Screenshot control evidence workflowCoverage contains malformed rows: "
        + ", ".join(workflow_coverage_malformed_rows)
    )
if missing_canonical_workflow_families:
    reasons.append(
        "Screenshot control evidence workflowCoverage is missing canonical workflow families: "
        + ", ".join(missing_canonical_workflow_families)
    )
if unexpected_workflow_families:
    reasons.append(
        "Screenshot control evidence workflowCoverage contains non-canonical workflow families: "
        + ", ".join(unexpected_workflow_families)
    )
if mismatched_workflow_family_screenshots:
    reasons.append(
        "Screenshot control evidence workflowCoverage does not match canonical family screenshot bindings: "
        + ", ".join(sorted(mismatched_workflow_family_screenshots))
    )

initial_png_inventory_names: List[str] = []
png_initial_fingerprints: Dict[str, Dict[str, int] | None] = {}
png_snapshot_bytes: Dict[str, bytes] = {}
invalid_top_level_png_entries: Dict[str, str] = {}
screenshot_dir_safe_to_enumerate = (
    not screenshot_dir_symlink_components
    and screenshot_dir_initial_fingerprint is not None
    and stat.S_ISDIR(screenshot_dir_initial_fingerprint["mode"])
)
if screenshot_dir_safe_to_enumerate:
    try:
        top_level_png_paths = sorted(
            (
                path
                for path in screenshot_dir.iterdir()
                if path.name.lower().endswith(".png")
            ),
            key=lambda path: path.name,
        )
    except OSError as exc:
        top_level_png_paths = []
        reasons.append(f"Screenshot directory inventory could not be read: {exc}.")
    for screenshot_path in top_level_png_paths:
        screenshot_name = screenshot_path.name
        initial_png_inventory_names.append(screenshot_name)
        initial_fingerprint = lstat_fingerprint(screenshot_path)
        png_initial_fingerprints[screenshot_name] = initial_fingerprint
        if initial_fingerprint is None:
            invalid_top_level_png_entries[screenshot_name] = "unreadable"
            continue
        if stat.S_ISLNK(initial_fingerprint["mode"]):
            invalid_top_level_png_entries[screenshot_name] = "symlink"
            continue
        if not stat.S_ISREG(initial_fingerprint["mode"]):
            invalid_top_level_png_entries[screenshot_name] = "non_regular"
            continue
        try:
            screenshot_bytes = screenshot_path.read_bytes()
        except OSError as exc:
            invalid_top_level_png_entries[screenshot_name] = f"unreadable:{exc}"
            continue
        after_read_fingerprint = lstat_fingerprint(screenshot_path)
        if after_read_fingerprint != initial_fingerprint:
            reasons.append(
                f"Screenshot PNG changed while it was being read: {screenshot_name}."
            )
        png_snapshot_bytes[screenshot_name] = screenshot_bytes

screenshot_dir_after_inventory_fingerprint = lstat_fingerprint(screenshot_dir)
if (
    screenshot_dir_safe_to_enumerate
    and screenshot_dir_after_inventory_fingerprint != screenshot_dir_initial_fingerprint
):
    reasons.append("Screenshot directory changed while its PNG inventory was being read.")
pack_png_names = sorted(png_snapshot_bytes)
declared_png_names = sorted(control_entries_by_name)
undeclared_pack_png_names = sorted(set(pack_png_names) - set(declared_png_names))
declared_missing_png_names = sorted(set(declared_png_names) - set(pack_png_names))
required_control_entries_missing = sorted(
    set(canonical_screenshot_inventory) - set(declared_png_names)
)
unexpected_control_entries = sorted(
    set(declared_png_names) - set(canonical_screenshot_inventory)
)
evidence["screenshot_control_declared_png_names"] = declared_png_names
evidence["screenshot_control_pack_png_names"] = pack_png_names
evidence["screenshot_control_undeclared_pack_png_names"] = undeclared_pack_png_names
evidence["screenshot_control_declared_missing_png_names"] = declared_missing_png_names
evidence["screenshot_control_required_entries_missing"] = required_control_entries_missing
evidence["screenshot_control_unexpected_entries"] = unexpected_control_entries
evidence["canonical_screenshot_inventory"] = canonical_screenshot_inventory
evidence["screenshot_control_malformed_entries"] = malformed_control_entries
evidence["screenshot_control_duplicate_entries"] = sorted(set(duplicate_control_entries))
evidence["screenshot_top_level_png_inventory_names"] = initial_png_inventory_names
evidence["screenshot_invalid_top_level_png_entries"] = invalid_top_level_png_entries
if invalid_top_level_png_entries:
    reasons.append(
        "Screenshot directory contains symlinked, non-regular, or unreadable top-level PNG entries: "
        + ", ".join(
            f"{name} ({entry_type})"
            for name, entry_type in sorted(invalid_top_level_png_entries.items())
        )
    )
if undeclared_pack_png_names:
    reasons.append(
        "Screenshot pack contains PNG files not declared by control evidence: "
        + ", ".join(undeclared_pack_png_names)
    )
if declared_missing_png_names:
    reasons.append(
        "Screenshot control evidence declares PNG files missing from the pack: "
        + ", ".join(declared_missing_png_names)
    )
if required_control_entries_missing:
    reasons.append(
        "Screenshot control evidence is missing canonical producer inventory entries: "
        + ", ".join(required_control_entries_missing)
    )
if unexpected_control_entries:
    reasons.append(
        "Screenshot control evidence contains entries outside the canonical producer inventory: "
        + ", ".join(unexpected_control_entries)
    )

control_byte_mismatches: Dict[str, str] = {}
for screenshot_name, control_entry in control_entries_by_name.items():
    screenshot_bytes = png_snapshot_bytes.get(screenshot_name)
    if screenshot_bytes is None:
        continue
    expected_size = int(control_entry["sizeBytes"])
    expected_sha256 = str(control_entry["sha256"])
    actual_size = len(screenshot_bytes)
    actual_sha256 = hashlib.sha256(screenshot_bytes).hexdigest()
    if actual_size != expected_size or actual_sha256 != expected_sha256:
        control_byte_mismatches[screenshot_name] = (
            f"expected {expected_size} bytes/{expected_sha256}; "
            f"observed {actual_size} bytes/{actual_sha256}"
        )
evidence["screenshot_control_byte_mismatches"] = control_byte_mismatches
if control_byte_mismatches:
    reasons.append(
        "Screenshot pack bytes do not match control evidence: "
        + "; ".join(
            f"{name} ({detail})" for name, detail in sorted(control_byte_mismatches.items())
        )
    )

screenshot_pack_inventory_bytes = b"".join(
    screenshot_name.encode("utf-8")
    + b"\0"
    + str(control_entries_by_name[screenshot_name]["sha256"]).encode("ascii")
    + b"\0"
    + str(control_entries_by_name[screenshot_name]["sizeBytes"]).encode("ascii")
    + b"\n"
    for screenshot_name in sorted(control_entries_by_name)
)
screenshot_pack_sha256 = hashlib.sha256(screenshot_pack_inventory_bytes).hexdigest()
screenshot_pack_digest_algorithm = "sha256-canonical-inventory-v1"
evidence["screenshot_pack_sha256"] = screenshot_pack_sha256
evidence["screenshot_pack_digest_algorithm"] = screenshot_pack_digest_algorithm
control_pack_sha256 = str(
    screenshot_control_evidence.get("screenshotPackSha256") or ""
).strip()
control_pack_digest_algorithm = str(
    screenshot_control_evidence.get("screenshotPackDigestAlgorithm") or ""
).strip()
evidence["screenshot_control_pack_sha256"] = control_pack_sha256
evidence["screenshot_control_pack_digest_algorithm"] = (
    control_pack_digest_algorithm
)
if control_pack_sha256 != screenshot_pack_sha256:
    reasons.append(
        "Screenshot control evidence screenshotPackSha256 does not match its canonical entry inventory."
    )
if control_pack_digest_algorithm != screenshot_pack_digest_algorithm:
    reasons.append(
        "Screenshot control evidence screenshotPackDigestAlgorithm is missing or unsupported."
    )

flagship_visual_review_evidence = (
    flagship_gate.get("visualReviewEvidence")
    if isinstance(flagship_gate.get("visualReviewEvidence"), dict)
    else {}
)
expected_control_path = Path(
    os.path.abspath(os.fspath(screenshot_control_evidence_path))
)
observed_control_path_raw = str(
    flagship_visual_review_evidence.get("screenshotControlEvidencePath") or ""
).strip()
observed_control_path = (
    Path(observed_control_path_raw)
    if Path(observed_control_path_raw).is_absolute()
    else repo_root / observed_control_path_raw
)
observed_control_path = Path(os.path.abspath(os.fspath(observed_control_path)))
flagship_visual_binding_mismatches: Dict[str, Dict[str, Any]] = {}


def bind_flagship_visual_field(field: str, expected: Any) -> None:
    observed = flagship_visual_review_evidence.get(field)
    integer_field = field in {
        "screenshotControlSizeBytes",
        "screenshotControlSchemaVersion",
        "screenshotCount",
    }
    if (
        observed != expected
        or (
            integer_field
            and (not isinstance(observed, int) or isinstance(observed, bool))
        )
    ):
        flagship_visual_binding_mismatches[field] = {
            "expected": expected,
            "observed": observed,
        }


bind_flagship_visual_field(
    "screenshotControlSha256",
    evidence["screenshot_control_receipt_sha256"],
)
bind_flagship_visual_field(
    "screenshotControlSizeBytes",
    evidence["screenshot_control_receipt_size_bytes"],
)
bind_flagship_visual_field("screenshotControlGeneratedAt", control_generated_at_raw)
bind_flagship_visual_field(
    "screenshotControlSchemaVersion",
    SCREENSHOT_CONTROL_SCHEMA_VERSION,
)
bind_flagship_visual_field("screenshotCount", control_screenshot_count)
bind_flagship_visual_field("screenshotPackSha256", screenshot_pack_sha256)
bind_flagship_visual_field(
    "screenshotPackDigestAlgorithm",
    screenshot_pack_digest_algorithm,
)
if observed_control_path != expected_control_path:
    flagship_visual_binding_mismatches["screenshotControlEvidencePath"] = {
        "expected": str(expected_control_path),
        "observed": observed_control_path_raw,
    }
evidence["flagship_visual_review_evidence"] = flagship_visual_review_evidence
evidence["flagship_visual_binding_mismatches"] = flagship_visual_binding_mismatches
if not flagship_visual_review_evidence:
    reasons.append("Flagship UI release gate is missing visualReviewEvidence binding.")
if flagship_visual_binding_mismatches:
    reasons.append(
        "Flagship UI release gate visualReviewEvidence does not match the validated screenshot control/pack: "
        + ", ".join(sorted(flagship_visual_binding_mismatches))
    )

evidence["screenshot_snapshot_initial"] = {
    "directory": screenshot_dir_initial_fingerprint,
    "control": {
        "fingerprint": screenshot_control_initial_fingerprint,
        "sha256": evidence["screenshot_control_receipt_sha256"],
        "sizeBytes": evidence["screenshot_control_receipt_size_bytes"],
    },
    "pngInventoryNames": initial_png_inventory_names,
    "pngs": {
        screenshot_name: {
            "fingerprint": png_initial_fingerprints.get(screenshot_name),
            "sha256": (
                hashlib.sha256(png_snapshot_bytes[screenshot_name]).hexdigest()
                if screenshot_name in png_snapshot_bytes
                else ""
            ),
            "sizeBytes": (
                len(png_snapshot_bytes[screenshot_name])
                if screenshot_name in png_snapshot_bytes
                else 0
            ),
        }
        for screenshot_name in initial_png_inventory_names
    },
}

missing_screenshots = [
    name for name in required_screenshots if name not in png_snapshot_bytes
]
invalid_screenshots = {
    name: error
    for name in canonical_screenshot_inventory
    if name in png_snapshot_bytes
    for error, _, _ in [validate_png_bytes(png_snapshot_bytes[name])]
    if error
}
minimum_shell_width = 1280
minimum_shell_height = 800
minimum_dialog_width = 900
minimum_dialog_height = 700
dialog_screenshot_names = {
    "03-settings-open-light.png",
    "08-cyberware-dialog-light.png",
    "11-diary-dialog-light.png",
    "12-magic-dialog-light.png",
    "13-matrix-dialog-light.png",
    "14-advancement-dialog-light.png",
    "16-master-index-dialog-light.png",
    "17-character-roster-dialog-light.png",
    "18-import-dialog-light.png",
    "38-translator-dialog-light.png",
    "39-xml-editor-dialog-light.png",
    "40-hero-lab-importer-dialog-light.png",
}
undersized_screenshots = {
    name: {"width": width, "height": height}
    for name in required_screenshots
    if name in png_snapshot_bytes
    for error, width, height in [validate_png_bytes(png_snapshot_bytes[name])]
    if not error and (
        (
            name not in dialog_screenshot_names
            and (width < minimum_shell_width or height < minimum_shell_height)
        )
        or (
            name in dialog_screenshot_names
            and (width < minimum_dialog_width or height < minimum_dialog_height)
        )
    )
}
evidence["dialog_screenshot_names"] = sorted(dialog_screenshot_names)
evidence["required_screenshots"] = required_screenshots
evidence["missing_screenshots"] = missing_screenshots
evidence["invalid_screenshots"] = invalid_screenshots
evidence["undersized_screenshots"] = undersized_screenshots
screenshot_timestamps: Dict[str, str] = {}
screenshot_mtime_age_diagnostics: Dict[str, int] = {}
flagship_generated_at_raw, flagship_generated_at = payload_generated_at(flagship_gate)
evidence["flagship_gate_reference_generated_at"] = flagship_generated_at_raw
for name in required_screenshots:
    screenshot_fingerprint = png_initial_fingerprints.get(name)
    if (
        screenshot_fingerprint is None
        or not stat.S_ISREG(screenshot_fingerprint["mode"])
    ):
        continue
    screenshot_mtime = datetime.fromtimestamp(
        screenshot_fingerprint["mtimeNs"] / 1_000_000_000,
        timezone.utc,
    )
    screenshot_timestamps[name] = screenshot_mtime.isoformat().replace("+00:00", "Z")
    screenshot_age_seconds = max(0, int((datetime.now(timezone.utc) - screenshot_mtime).total_seconds()))
    screenshot_mtime_age_diagnostics[name] = screenshot_age_seconds
evidence["screenshot_timestamps"] = screenshot_timestamps
evidence["screenshot_mtime_age_diagnostics"] = screenshot_mtime_age_diagnostics
control_older_than_flagship_receipt_seconds = 0
if flagship_generated_at is not None and control_generated_at is not None:
    control_older_than_flagship_receipt_seconds = max(
        0,
        int((flagship_generated_at - control_generated_at).total_seconds()),
    )
evidence["control_older_than_flagship_receipt_seconds"] = (
    control_older_than_flagship_receipt_seconds
)
if missing_screenshots:
    reasons.append("Visual familiarity screenshots are missing: " + ", ".join(missing_screenshots))
if invalid_screenshots:
    reasons.append(
        "Visual familiarity screenshots are unreadable or corrupted: "
        + ", ".join(f"{name} ({reason})" for name, reason in invalid_screenshots.items())
    )
if undersized_screenshots:
    reasons.append(
        "Visual familiarity screenshots are too small for trusted review: "
        + ", ".join(
            f"{name} ({size['width']}x{size['height']})"
            for name, size in undersized_screenshots.items()
        )
    )
if (
    control_older_than_flagship_receipt_seconds
    > DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS
):
    reasons.append(
        "Screenshot control evidence predates the flagship release gate receipt beyond the allowed skew "
        f"({control_older_than_flagship_receipt_seconds}s older; "
        f"max {DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS}s)."
    )
screen_capture_review_end = len(reasons)

navigator_text = navigator_axaml_path.read_text(encoding="utf-8") if navigator_axaml_path.is_file() else ""
navigator_codebehind_text = navigator_axaml_path.with_suffix(".axaml.cs").read_text(encoding="utf-8") if navigator_axaml_path.with_suffix(".axaml.cs").is_file() else ""
main_window_text = main_window_axaml_path.read_text(encoding="utf-8") if main_window_axaml_path.is_file() else ""
required_navigator_markers = [
    "x:Name=\"NavigatorTree\"",
    "TreeDataTemplate",
    "Codex",
]
missing_navigator_markers = [
    marker for marker in required_navigator_markers if marker not in navigator_text and marker not in navigator_codebehind_text
]
disallowed_navigator_markers = [
    "x:Name=\"LoadedRunnerTabStrip\"",
    "x:Name=\"NavigationTabsList\"",
    "x:Name=\"OpenWorkspacesList\"",
    "x:Name=\"SectionActionsList\"",
    "x:Name=\"WorkflowSurfacesList\"",
]
present_disallowed_navigator_markers = [
    marker for marker in disallowed_navigator_markers if marker in navigator_text or marker in navigator_codebehind_text
]
has_navigation_tabs = "NavigatorTree" in navigator_text
tab_strip_markers = ["TabControl", "TabStrip", "TabView", "LoadedRunnerTabStrip", "CharacterTabStrip", "NavigatorTree"]
has_tab_strip_control = any(marker in navigator_text or marker in main_window_text for marker in tab_strip_markers)
evidence["required_navigator_markers"] = required_navigator_markers
evidence["missing_navigator_markers"] = missing_navigator_markers
evidence["disallowed_navigator_markers"] = disallowed_navigator_markers
evidence["present_disallowed_navigator_markers"] = present_disallowed_navigator_markers
evidence["loaded_runner_tab_posture_control_present"] = has_navigation_tabs
evidence["loaded_runner_tab_strip_control_present"] = has_tab_strip_control
evidence["tab_strip_markers"] = tab_strip_markers
if missing_navigator_markers:
    reasons.append("Codex tree source anchors are missing: " + ", ".join(missing_navigator_markers))
if present_disallowed_navigator_markers:
    reasons.append("Legacy-incompatible navigator chrome is still present in source: " + ", ".join(present_disallowed_navigator_markers))
if not has_navigation_tabs:
    reasons.append("Loaded-runner tab posture control is missing from the shell.")
if not has_tab_strip_control:
    reasons.append("Loaded-runner visual familiarity is not proven: the shell still has no explicit tab strip / tab panel control for character work.")
source_anchor_review_reasons = (
    list(reasons[source_anchor_review_start:screen_capture_review_start])
    + list(reasons[screen_capture_review_end:])
)
screen_capture_review_reasons = list(reasons[screen_capture_review_start:screen_capture_review_end])

legacy_familiarity_review_start = len(reasons)
visual_review_method = extract_test_method(test_text, "Visual_review_evidence_is_published_for_light_and_dark_shell_states")
cyberware_method = extract_test_method(test_text, "Cyberware_and_cyberlimb_builder_preserve_legacy_dialog_familiarity_cues")

dense_section_capture_segment = segment_between(
    visual_review_method,
    next((marker for marker in capture_statement_variants(3) if marker in visual_review_method), ""),
    next((marker for marker in capture_statement_variants(4) if marker in visual_review_method), ""),
)
dense_section_state_change_markers = [
    'Click("',
    'PressKey(',
    'InvokeDialogAction(',
    'SelectedItem =',
    'SectionRowsList',
    'NavigatorTree',
]
dense_section_capture_advances = any(marker in dense_section_capture_segment for marker in dense_section_state_change_markers)
evidence["dense_section_capture_advances_past_loaded_runner"] = dense_section_capture_advances
if not dense_section_capture_advances:
    reasons.append("Dense-section visual proof is not trusted: the dense-section screenshot is captured without moving past the loaded-runner posture.")

cyberware_dialog_markers = ["DialogTitleText", "DialogFieldsHost", "DialogActionsHost", "InvokeDialogAction("]
cyberware_dialog_test_has_visible_dialog = any(marker in cyberware_method for marker in cyberware_dialog_markers)
cyberware_capture_segment = segment_between(
    visual_review_method,
    'harness.SetActiveSectionForTesting("cyberwares");',
    next((marker for marker in capture_statement_variants(7) if marker in visual_review_method), ""),
)
cyberware_capture_markers = cyberware_dialog_markers + capture_statement_variants(7)
cyberware_capture_opens_dialog = any(marker in cyberware_capture_segment for marker in cyberware_capture_markers)
magic_capture_segment = segment_between_any(
    visual_review_method,
    capture_statement_variants(10),
    capture_statement_variants(11),
)
magic_capture_markers = [
    "SectionQuickAction_spell_add",
    "Add Spell",
    *capture_statement_variants(11),
]
magic_capture_opens_dialog = any(marker in magic_capture_segment for marker in magic_capture_markers)
matrix_capture_segment = segment_between_any(
    visual_review_method,
    capture_statement_variants(11),
    capture_statement_variants(12),
)
matrix_capture_markers = [
    "SectionQuickAction_matrix_program_add",
    "Add Program / Cyberdeck Item",
    *capture_statement_variants(12),
]
matrix_capture_opens_dialog = any(marker in matrix_capture_segment for marker in matrix_capture_markers)
evidence["cyberware_dialog_test_has_visible_dialog_posture"] = cyberware_dialog_test_has_visible_dialog
evidence["cyberware_capture_opens_dialog_posture"] = cyberware_capture_opens_dialog
evidence["magic_capture_opens_dialog_posture"] = magic_capture_opens_dialog
evidence["matrix_capture_opens_dialog_posture"] = matrix_capture_opens_dialog
if not cyberware_dialog_test_has_visible_dialog:
    reasons.append("Cyberware/cyberlimb familiarity is not proven: the dedicated test never opens a visible dialog with confirm controls.")
if not cyberware_capture_opens_dialog:
    reasons.append("Cyberware screenshot proof is not trusted: the screenshot capture does not open an explicit dialog posture before recording evidence.")
magic_method = extract_test_method(test_text, "Magic_workflows_execute_with_specific_dialog_fields_and_confirm_actions")
magic_method_markers = ["sectionId: \"spells\"", "actionControlId: \"spell_add\"", "actionControlId: \"adept_power_add\""]
magic_method_has_rhythm = all(marker in magic_method for marker in magic_method_markers) if magic_method else False
evidence["magic_method_has_rhythm_markers"] = magic_method_has_rhythm
if not magic_method:
    reasons.append("Magic familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not magic_method_has_rhythm:
    reasons.append("Magic familiarity is not proven: required spell/power markers are missing from the dedicated workflow method.")
if not magic_capture_opens_dialog:
    reasons.append("Magic screenshot proof is not trusted: the visual review proof does not open a dedicated magic dialog before recording evidence.")

matrix_method = extract_test_method(test_text, "Matrix_workflows_execute_with_specific_dialog_fields_and_confirm_actions")
matrix_method_markers = ["sectionId: \"complexforms\"", "actionControlId: \"complex_form_add\"", "actionControlId: \"matrix_program_add\""]
matrix_method_has_rhythm = all(marker in matrix_method for marker in matrix_method_markers) if matrix_method else False
evidence["matrix_method_has_rhythm_markers"] = matrix_method_has_rhythm
if not matrix_method:
    reasons.append("Matrix familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not matrix_method_has_rhythm:
    reasons.append("Matrix familiarity is not proven: required complex-form/program markers are missing from the dedicated workflow method.")
if not matrix_capture_opens_dialog:
    reasons.append("Matrix screenshot proof is not trusted: the visual review proof does not open a dedicated matrix dialog before recording evidence.")

creation_method = extract_test_method(test_text, "Character_creation_preserves_familiar_dense_builder_rhythm")
creation_method_marker_bundles = [
    [
        "AttributeBaseEditor_BOD",
        "AttributeKarmaEditor_BOD",
        "edits.Any(edit =>",
        "edit.AttributeName, \"Body\"",
        "edit.Bucket, \"base\"",
    ],
    [
        "attributes.body = 5",
        "skills.firearms[0] = Automatics 6",
    ],
]
creation_method_has_rhythm = any(
    all(marker in creation_method for marker in bundle)
    for bundle in creation_method_marker_bundles
) if creation_method else False
evidence["creation_method_has_rhythm_markers"] = creation_method_has_rhythm
evidence["creation_method_marker_bundles"] = creation_method_marker_bundles
if not creation_method:
    reasons.append("Character creation familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not creation_method_has_rhythm:
    reasons.append("Character creation familiarity is not proven: dense-builder rhythm markers are missing from the dedicated test.")

advancement_method = extract_test_method(test_text, "Advancement_and_karma_journal_workflows_preserve_familiar_progression_rhythm")
advancement_method_markers = ["sectionId: \"progress\"", "actionControlId: \"create_entry\"", "actionControlId: \"initiation_add\""]
advancement_method_has_rhythm = all(marker in advancement_method for marker in advancement_method_markers) if advancement_method else False
evidence["advancement_method_has_rhythm_markers"] = advancement_method_has_rhythm
if not advancement_method:
    reasons.append("Advancement familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not advancement_method_has_rhythm:
    reasons.append("Advancement familiarity is not proven: progression/journal action markers are missing from the dedicated test.")

gear_method = extract_test_method(test_text, "Gear_builder_preserves_familiar_browse_detail_confirm_rhythm")
gear_method_markers = ["gear.weapons[0] = Ares Alpha", "gear.armor[0] = Armor Jacket"]
gear_method_has_rhythm = all(marker in gear_method for marker in gear_method_markers) if gear_method else False
evidence["gear_method_has_rhythm_markers"] = gear_method_has_rhythm
if not gear_method:
    reasons.append("Gear familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not gear_method_has_rhythm:
    reasons.append("Gear familiarity is not proven: browse/detail rhythm markers are missing from the dedicated test.")

contacts_diary_method = extract_test_method(test_text, "Contacts_diary_and_support_routes_execute_with_public_path_visibility")
contacts_diary_markers = ["actionControlId: \"contact_add\"", "actionControlId: \"create_entry\""]
contacts_diary_method_has_rhythm = all(marker in contacts_diary_method for marker in contacts_diary_markers) if contacts_diary_method else False
evidence["contacts_diary_method_has_rhythm_markers"] = contacts_diary_method_has_rhythm
if not contacts_diary_method:
    reasons.append("Contacts/diary familiarity is not proven: the dedicated workflow method is not present in test sources.")
elif not contacts_diary_method_has_rhythm:
    reasons.append("Contacts/diary familiarity is not proven: contact + diary action markers are missing from the dedicated test.")

ruleset_orientation_method = extract_test_method(test_text, "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks")
required_ruleset_orientation_markers = [
    "RulesetDefaults.Sr4",
    "RulesetDefaults.Sr5",
    "RulesetDefaults.Sr6",
    "SetPreferredRulesetAsync(",
    "BuildOpenWorkspacesHeading",
    "SnapshotRosterItems(rosterTree)",
    "grouped roster empty until a workspace is opened",
]
missing_ruleset_orientation_markers = [
    marker for marker in required_ruleset_orientation_markers if marker not in ruleset_orientation_method
]
ruleset_orientation_method_has_markers = not missing_ruleset_orientation_markers
evidence["ruleset_orientation_method_has_markers"] = ruleset_orientation_method_has_markers
evidence["missing_ruleset_orientation_markers"] = missing_ruleset_orientation_markers
if not ruleset_orientation_method:
    reasons.append("SR4/SR5/SR6 codex orientation familiarity is not proven: the dedicated runtime-backed ruleset switch test is not present in test sources.")
elif not ruleset_orientation_method_has_markers:
    reasons.append(
        "SR4/SR5/SR6 roster orientation familiarity is not proven: the dedicated runtime-backed ruleset switch test is missing markers: "
        + ", ".join(missing_ruleset_orientation_markers)
    )
legacy_familiarity_review_reasons = list(reasons[legacy_familiarity_review_start:])

snapshot_stability_review_start = len(reasons)
snapshot_recheck_changes: Dict[str, Any] = {}
current_release_channel_fingerprint = lstat_fingerprint(release_channel_path)
if current_release_channel_fingerprint != release_channel_initial_fingerprint:
    snapshot_recheck_changes["releaseChannelFingerprint"] = {
        "initial": release_channel_initial_fingerprint,
        "current": current_release_channel_fingerprint,
    }
current_screenshot_dir_symlink_components = symlinked_path_components(screenshot_dir)
current_control_symlink_components = symlinked_path_components(
    screenshot_control_evidence_path
)
if current_screenshot_dir_symlink_components != screenshot_dir_symlink_components:
    snapshot_recheck_changes["screenshotDirectorySymlinkComponents"] = {
        "initial": screenshot_dir_symlink_components,
        "current": current_screenshot_dir_symlink_components,
    }
if current_control_symlink_components != screenshot_control_symlink_components:
    snapshot_recheck_changes["controlSymlinkComponents"] = {
        "initial": screenshot_control_symlink_components,
        "current": current_control_symlink_components,
    }
current_screenshot_dir_fingerprint = lstat_fingerprint(screenshot_dir)
current_control_fingerprint = lstat_fingerprint(screenshot_control_evidence_path)
if current_screenshot_dir_fingerprint != screenshot_dir_initial_fingerprint:
    snapshot_recheck_changes["screenshotDirectoryFingerprint"] = {
        "initial": screenshot_dir_initial_fingerprint,
        "current": current_screenshot_dir_fingerprint,
    }
if current_control_fingerprint != screenshot_control_initial_fingerprint:
    snapshot_recheck_changes["controlFingerprint"] = {
        "initial": screenshot_control_initial_fingerprint,
        "current": current_control_fingerprint,
    }

current_png_inventory_names: List[str] = []
if (
    not current_screenshot_dir_symlink_components
    and current_screenshot_dir_fingerprint is not None
    and stat.S_ISDIR(current_screenshot_dir_fingerprint["mode"])
):
    try:
        current_png_inventory_names = sorted(
            path.name
            for path in screenshot_dir.iterdir()
            if path.name.lower().endswith(".png")
        )
    except OSError as exc:
        snapshot_recheck_changes["pngInventoryReadError"] = str(exc)
if current_png_inventory_names != initial_png_inventory_names:
    snapshot_recheck_changes["pngInventoryNames"] = {
        "initial": initial_png_inventory_names,
        "current": current_png_inventory_names,
    }
changed_png_fingerprints: Dict[str, Any] = {}
for screenshot_name in sorted(set(initial_png_inventory_names) | set(current_png_inventory_names)):
    initial_fingerprint = png_initial_fingerprints.get(screenshot_name)
    current_fingerprint = lstat_fingerprint(screenshot_dir / screenshot_name)
    if current_fingerprint != initial_fingerprint:
        changed_png_fingerprints[screenshot_name] = {
            "initial": initial_fingerprint,
            "current": current_fingerprint,
        }
if changed_png_fingerprints:
    snapshot_recheck_changes["pngFingerprints"] = changed_png_fingerprints
evidence["screenshot_snapshot_recheck"] = {
    "directory": current_screenshot_dir_fingerprint,
    "control": current_control_fingerprint,
    "pngInventoryNames": current_png_inventory_names,
    "changes": snapshot_recheck_changes,
}
evidence["release_channel_snapshot_recheck"] = {
    "initial": release_channel_initial_fingerprint,
    "current": current_release_channel_fingerprint,
}
if snapshot_recheck_changes:
    reasons.append(
        "Release channel or screenshot control/PNG snapshot changed during validation: "
        + ", ".join(sorted(snapshot_recheck_changes))
    )
snapshot_stability_review_reasons = list(reasons[snapshot_stability_review_start:])

status = "pass" if not reasons else "fail"
reviews = {
    "flagshipGateReview": {
        "status": "pass" if not flagship_gate_review_reasons else "fail",
        "reasonCount": len(flagship_gate_review_reasons),
        "reasons": flagship_gate_review_reasons,
        "receiptPath": str(flagship_gate_path),
        "releaseChannelPath": str(release_channel_path),
    },
    "prerequisiteReceiptReview": {
        "status": "pass" if not prerequisite_receipt_review_reasons else "fail",
        "reasonCount": len(prerequisite_receipt_review_reasons),
        "reasons": prerequisite_receipt_review_reasons,
        "receiptPaths": [
            str(layout_hard_gate_receipt_path),
            str(legacy_equivalent_chrome_gate_receipt_path),
            str(muscle_memory_parity_gate_receipt_path),
        ],
    },
    "headProofReview": {
        "status": "pass" if not head_proof_review_reasons else "fail",
        "reasonCount": len(head_proof_review_reasons),
        "reasons": head_proof_review_reasons,
        "requiredHeads": flagship_required_desktop_heads,
        "canonicalRequiredHeads": canonical_required_desktop_heads,
    },
    "interactionProofReview": {
        "status": "pass" if not interaction_proof_review_reasons else "fail",
        "reasonCount": len(interaction_proof_review_reasons),
        "reasons": interaction_proof_review_reasons,
        "requiredInteractionKeys": required_legacy_interaction_keys,
    },
    "sourceAnchorReview": {
        "status": "pass" if not source_anchor_review_reasons else "fail",
        "reasonCount": len(source_anchor_review_reasons),
        "reasons": source_anchor_review_reasons,
        "requiredTests": required_test_names,
        "requiredDesktopShellTests": required_desktop_shell_test_names,
    },
    "screenCaptureReview": {
        "status": "pass" if not screen_capture_review_reasons else "fail",
        "reasonCount": len(screen_capture_review_reasons),
        "reasons": screen_capture_review_reasons,
        "requiredScreenshots": required_screenshots,
    },
    "snapshotStabilityReview": {
        "status": "pass" if not snapshot_stability_review_reasons else "fail",
        "reasonCount": len(snapshot_stability_review_reasons),
        "reasons": snapshot_stability_review_reasons,
    },
    "legacyFamiliarityReview": {
        "status": "pass" if not legacy_familiarity_review_reasons else "fail",
        "reasonCount": len(legacy_familiarity_review_reasons),
        "reasons": legacy_familiarity_review_reasons,
        "workflowMarkers": [
            "dense_section_capture_advances_past_loaded_runner",
            "cyberware_capture_opens_dialog_posture",
            "magic_method_has_rhythm_markers",
            "matrix_method_has_rhythm_markers",
            "creation_method_has_rhythm_markers",
            "advancement_method_has_rhythm_markers",
            "gear_method_has_rhythm_markers",
            "contacts_diary_method_has_rhythm_markers",
            "ruleset_orientation_method_has_markers",
        ],
    },
    "muscleMemoryParityReview": {
        "status": "pass" if not (source_anchor_review_reasons or legacy_familiarity_review_reasons) else "fail",
        "reasonCount": len(source_anchor_review_reasons) + len(legacy_familiarity_review_reasons),
        "reasons": source_anchor_review_reasons + legacy_familiarity_review_reasons,
        "gateScript": "scripts/ai/milestones/chummer5a-muscle-memory-parity-gate.sh",
        "sourceReviews": ["sourceAnchorReview", "legacyFamiliarityReview"],
    },
}
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_visual_familiarity_exit_gate",
    "channelId": release_channel_channel_id,
    "channel": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "version": release_channel_version,
    "status": status,
    "summary": (
        "Desktop visual familiarity is proven for shell chrome, loaded-runner tabs, dense builder posture, and explicit milestone-2 surface cues across creation, advancement, magic, matrix, gear, cyberware, vehicles, contacts, and diary plus SR4/SR5/SR6 codex orientation."
        if status == "pass"
        else "Desktop visual familiarity is not fully proven."
    ),
    "reasons": reasons,
    "reviews": reviews,
    "evidence": evidence,
}
payload["evidence"]["failureCount"] = len(reasons)
atomic_write_json(receipt_path, payload)
if status != "pass":
    raise SystemExit(43)
PY

if [[ "$refresh_downstream_readiness" == "1" && "$skip_downstream_readiness" != "1" ]]; then
  python3 "$flagship_product_readiness_materializer_path" >/dev/null
else
  echo "[desktop-visual-familiarity-gate] downstream flagship readiness refresh skipped."
fi

echo "[desktop-visual-familiarity-exit-gate] PASS"
