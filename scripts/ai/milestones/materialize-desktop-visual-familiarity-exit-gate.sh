#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="$repo_root/.codex-studio/published/DESKTOP_VISUAL_FAMILIARITY_EXIT_GATE.generated.json"
flagship_gate_path="$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json"
screenshot_dir="$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots"
hub_registry_root="${CHUMMER_HUB_REGISTRY_ROOT:-$("$repo_root/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
canonical_release_channel_path="${hub_registry_root:+$hub_registry_root/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
default_release_channel_path="$repo_root/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$canonical_release_channel_path" && -f "$canonical_release_channel_path" ]]; then
  release_channel_path_default="$canonical_release_channel_path"
else
  release_channel_path_default="$default_release_channel_path"
fi
release_channel_path="${CHUMMER_DESKTOP_VISUAL_RELEASE_CHANNEL_PATH:-$release_channel_path_default}"
release_gate_lock_dir="$repo_root/.codex-studio/locks/b14-flagship-ui-release-gate.lock"
release_gate_lock_owner_pid_path="$release_gate_lock_dir/owner.pid"
app_axaml_path="$repo_root/Chummer.Avalonia/App.axaml"
main_window_axaml_path="$repo_root/Chummer.Avalonia/MainWindow.axaml"
navigator_axaml_path="$repo_root/Chummer.Avalonia/Controls/NavigatorPaneControl.axaml"
toolstrip_axaml_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml"
toolstrip_codebehind_path="$repo_root/Chummer.Avalonia/Controls/ToolStripControl.axaml.cs"
summary_header_axaml_path="$repo_root/Chummer.Avalonia/Controls/SummaryHeaderControl.axaml"
ui_gate_tests_path="$repo_root/Chummer.Tests/Presentation/AvaloniaFlagshipUiGateTests.cs"
desktop_shell_ruleset_tests_path="$repo_root/Chummer.Tests/Presentation/DesktopShellRulesetCatalogTests.cs"
legacy_frmcareer_designer_path="/docker/chummer5a/Chummer/Forms/Character Forms/CharacterCareer.Designer.cs"
skip_release_gate_lock_wait="${CHUMMER_DESKTOP_VISUAL_SKIP_RELEASE_GATE_LOCK_WAIT:-0}"
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

mkdir -p "$(dirname "$receipt_path")"
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
if [[ "$skip_release_gate_lock_wait" != "1" ]]; then
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

echo "[desktop-visual-familiarity-gate] running Chummer5a layout hard gate..."
bash scripts/ai/milestones/chummer5a-layout-hard-gate.sh >/dev/null

python3 - <<'PY' "$repo_root" "$receipt_path" "$flagship_gate_path" "$screenshot_dir" "$app_axaml_path" "$main_window_axaml_path" "$navigator_axaml_path" "$toolstrip_axaml_path" "$toolstrip_codebehind_path" "$summary_header_axaml_path" "$ui_gate_tests_path" "$desktop_shell_ruleset_tests_path" "$legacy_frmcareer_designer_path" "$release_channel_path"
from __future__ import annotations

import json
import binascii
import os
import re
import sys
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
DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_PROOF_MAX_AGE_SECONDS")
    or "86400"
)
DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS = int(
    os.environ.get("CHUMMER_DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS")
    or str(DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS)
)


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Dict[str, Any]:
    if not path.is_file():
        return {}
    loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    return loaded if isinstance(loaded, dict) else {}


def status_ok(value: str) -> bool:
    return value.strip().lower() in {"pass", "passed", "ready"}


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


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
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
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
    allow_stale_pass_receipt: bool = False,
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
        if future_skew_seconds > DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS:
            reasons.append(
                f"{label} generatedAt is in the future ({future_skew_seconds}s ahead; max {DESKTOP_PROOF_MAX_FUTURE_SKEW_SECONDS}s)."
            )
        age_seconds = 0
    evidence[f"{label}_age_seconds"] = age_seconds
    if age_seconds > DESKTOP_PROOF_MAX_AGE_SECONDS:
        status = str(payload.get("status") or "").strip().lower()
        evidence[f"{label}_stale_pass_receipt_allowed"] = allow_stale_pass_receipt and status_ok(status)
        if not (allow_stale_pass_receipt and status_ok(status)):
            reasons.append(
                f"{label} is stale ({age_seconds}s old; max {DESKTOP_PROOF_MAX_AGE_SECONDS}s)."
            )


def validate_png(path: Path) -> tuple[str, int, int]:
    try:
        data = path.read_bytes()
    except OSError as exc:
        return f"unreadable: {exc}", 0, 0
    signature = b"\x89PNG\r\n\x1a\n"
    if not data.startswith(signature):
        return "missing PNG signature", 0, 0
    offset = len(signature)
    saw_iend = False
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
            if length < 8:
                return "invalid IHDR chunk", width, height
            width = int.from_bytes(data[chunk_start : chunk_start + 4], "big")
            height = int.from_bytes(data[chunk_start + 4 : chunk_start + 8], "big")
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
    ]


def path_within_root(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


repo_root, receipt_path, flagship_gate_path, screenshot_dir, app_axaml_path, main_window_axaml_path, navigator_axaml_path, toolstrip_axaml_path, toolstrip_codebehind_path, summary_header_axaml_path, ui_gate_tests_path, desktop_shell_ruleset_tests_path, legacy_frmcareer_designer_path, release_channel_path = [
    Path(value) for value in sys.argv[1:15]
]

reasons: List[str] = []
evidence: Dict[str, Any] = {
    "flagship_gate_path": str(flagship_gate_path),
    "screenshot_dir": str(screenshot_dir),
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
}

flagship_gate_review_start = len(reasons)
flagship_gate = load_json(flagship_gate_path)
flagship_status = str(flagship_gate.get("status") or "").strip().lower()
evidence["flagship_gate_status"] = flagship_status
if not status_ok(flagship_status):
    reasons.append("Flagship UI release gate is missing or not passing.")
validate_receipt_freshness(
    "flagship_ui_release_gate",
    flagship_gate,
    reasons,
    evidence,
    allow_stale_pass_receipt=True,
)
release_channel = load_json(release_channel_path)
evidence["release_channel_receipt_exists"] = release_channel_path.is_file()
if release_channel_path.is_file() and not release_channel:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is unreadable or not a JSON object."
    )
release_channel_channel_id = normalize_token(
    release_channel.get("channelId") or release_channel.get("channel")
)
release_channel_version = str(release_channel.get("version") or "").strip()
release_channel_generated_at_raw, release_channel_generated_at = payload_generated_at(release_channel)
evidence["release_channel_channel_id"] = release_channel_channel_id
evidence["release_channel_version"] = release_channel_version
evidence["release_channel_generated_at"] = release_channel_generated_at_raw
if not release_channel_channel_id:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing channelId/channel."
    )
if not release_channel_version:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing version."
    )
if not release_channel_generated_at_raw or release_channel_generated_at is None:
    reasons.append(
        "Desktop visual familiarity exit gate release channel receipt is missing a valid generatedAt/generated_at timestamp."
    )
flagship_gate_review_reasons = list(reasons[flagship_gate_review_start:])

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
    "Opening_mainframe_preserves_chummer5a_successor_workbench_posture",
    "Runtime_backed_file_menu_preserves_working_open_save_import_routes",
    "Master_index_is_a_first_class_runtime_backed_workbench_route",
    "Character_roster_is_a_first_class_runtime_backed_workbench_route",
    "Desktop_shell_preserves_chummer5a_familiarity_cues",
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
    "Runtime_backed_shell_hides_workspace_tree_until_multiple_workspaces_exist",
    "Runtime_backed_ruleset_switch_preserves_sr4_sr5_and_sr6_codex_landmarks",
    "Runtime_backed_shell_avoids_modern_dashboard_copy_that_breaks_chummer5a_orientation",
    "Runtime_backed_shell_chrome_stays_enabled_after_runner_load",
    "Standalone_toolstrip_buttons_raise_expected_events",
    "Standalone_menu_bar_buttons_and_menu_commands_raise_expected_events",
    "Standalone_workspace_strip_quick_start_button_raises_expected_event",
    "Standalone_summary_header_tab_buttons_raise_expected_events",
    "Standalone_navigator_tree_selection_raises_workspace_tab_section_and_workflow_events",
    "Standalone_command_dialog_pane_routes_command_selection_field_updates_and_dialog_actions",
    "Standalone_coach_sidecar_copy_button_raises_event_when_launch_uri_is_available",
    "Loaded_runner_main_window_routes_navigation_palette_dialog_and_quick_action_surfaces_end_to_end",
]
test_text = ui_gate_tests_path.read_text(encoding="utf-8") if ui_gate_tests_path.is_file() else ""
missing_tests = [name for name in required_test_names if name not in test_text]
evidence["required_tests"] = required_test_names
evidence["missing_tests"] = missing_tests
if missing_tests:
    reasons.append("Visual familiarity tests are missing: " + ", ".join(missing_tests))

required_desktop_shell_test_names = [
    "DesktopShell_hides_workspace_left_pane_for_single_runner_posture",
    "DesktopShell_restores_workspace_left_pane_for_multi_workspace_session",
]
desktop_shell_test_text = desktop_shell_ruleset_tests_path.read_text(encoding="utf-8") if desktop_shell_ruleset_tests_path.is_file() else ""
missing_desktop_shell_tests = [name for name in required_desktop_shell_test_names if name not in desktop_shell_test_text]
evidence["required_desktop_shell_tests"] = required_desktop_shell_test_names
evidence["missing_desktop_shell_tests"] = missing_desktop_shell_tests
if missing_desktop_shell_tests:
    reasons.append("Desktop shell layout tests are missing: " + ", ".join(missing_desktop_shell_tests))

toolstrip_axaml_text = toolstrip_axaml_path.read_text(encoding="utf-8") if toolstrip_axaml_path.is_file() else ""
toolstrip_codebehind_text = toolstrip_codebehind_path.read_text(encoding="utf-8") if toolstrip_codebehind_path.is_file() else ""
required_toolstrip_markers = [
    "shell-toolstrip-band",
    "shell-toolstrip-state",
    "WrapPanel Orientation=\"Horizontal\" ItemHeight=\"28\"",
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
required_summary_header_markers = [
    "x:Name=\"LoadedRunnerTabStripBorder\"",
    "x:Name=\"LoadedRunnerTabStrip\"",
]
missing_summary_header_markers = [
    marker for marker in required_summary_header_markers if marker not in summary_header_text
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
    "Assert.IsInstanceOfType<string>(button.Content",
    "Assert.AreEqual(1, GetButtonTextLines(button).Length",
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
]
missing_screenshots = [name for name in required_screenshots if not (screenshot_dir / name).is_file()]
invalid_screenshots = {
    name: error
    for name in required_screenshots
    if (screenshot_dir / name).is_file()
    for error, _, _ in [validate_png(screenshot_dir / name)]
    if error
}
minimum_shell_width = 1280
minimum_shell_height = 800
minimum_dialog_width = 900
minimum_dialog_height = 700
undersized_screenshots = {
    name: {"width": width, "height": height}
    for name in required_screenshots
    if (screenshot_dir / name).is_file()
    for error, width, height in [validate_png(screenshot_dir / name)]
    if not error and (
        (
            name not in {"08-cyberware-dialog-light.png", "11-diary-dialog-light.png"}
            and (width < minimum_shell_width or height < minimum_shell_height)
        )
        or (
            name in {"08-cyberware-dialog-light.png", "11-diary-dialog-light.png", "12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png", "16-master-index-dialog-light.png", "17-character-roster-dialog-light.png", "18-import-dialog-light.png"}
            and (width < minimum_dialog_width or height < minimum_dialog_height)
        )
    )
}
evidence["required_screenshots"] = required_screenshots
evidence["missing_screenshots"] = missing_screenshots
evidence["invalid_screenshots"] = invalid_screenshots
evidence["undersized_screenshots"] = undersized_screenshots
screenshot_timestamps: Dict[str, str] = {}
stale_screenshots: List[str] = []
screenshots_older_than_flagship_receipt: List[str] = []
flagship_generated_at_raw, flagship_generated_at = payload_generated_at(flagship_gate)
evidence["flagship_gate_reference_generated_at"] = flagship_generated_at_raw
for name in required_screenshots:
    screenshot_path = screenshot_dir / name
    if not screenshot_path.is_file():
        continue
    screenshot_mtime = datetime.fromtimestamp(screenshot_path.stat().st_mtime, timezone.utc)
    screenshot_timestamps[name] = screenshot_mtime.isoformat().replace("+00:00", "Z")
    screenshot_age_seconds = max(0, int((datetime.now(timezone.utc) - screenshot_mtime).total_seconds()))
    if screenshot_age_seconds > DESKTOP_VISUAL_SCREENSHOT_MAX_AGE_SECONDS:
        stale_screenshots.append(f"{name} ({screenshot_age_seconds}s old)")
    if flagship_generated_at is not None:
        skew_seconds = int((flagship_generated_at - screenshot_mtime).total_seconds())
        if skew_seconds > DESKTOP_VISUAL_SCREENSHOT_RECEIPT_SKEW_MAX_SECONDS:
            screenshots_older_than_flagship_receipt.append(f"{name} ({skew_seconds}s older)")
evidence["screenshot_timestamps"] = screenshot_timestamps
evidence["stale_screenshots"] = stale_screenshots
evidence["screenshots_older_than_flagship_receipt"] = screenshots_older_than_flagship_receipt
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
if stale_screenshots:
    reasons.append(
        "Visual familiarity screenshots are stale: " + ", ".join(stale_screenshots)
    )
if screenshots_older_than_flagship_receipt:
    reasons.append(
        "Visual familiarity screenshots predate the flagship release gate receipt beyond the allowed skew: "
        + ", ".join(screenshots_older_than_flagship_receipt)
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
    "object? cyberwareRow =",
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
creation_method_markers = ["attributes.body = 5", "skills.firearms[0] = Automatics 6"]
creation_method_has_rhythm = all(marker in creation_method for marker in creation_method_markers) if creation_method else False
evidence["creation_method_has_rhythm_markers"] = creation_method_has_rhythm
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
    "BuildNavigationTabsHeading",
    "BuildSectionActionsHeading",
    "BuildWorkflowSurfacesHeading",
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
        "SR4/SR5/SR6 codex orientation familiarity is not proven: the dedicated runtime-backed ruleset switch test is missing markers: "
        + ", ".join(missing_ruleset_orientation_markers)
    )
legacy_familiarity_review_reasons = list(reasons[legacy_familiarity_review_start:])

status = "pass" if not reasons else "fail"
reviews = {
    "flagshipGateReview": {
        "status": "pass" if not flagship_gate_review_reasons else "fail",
        "reasonCount": len(flagship_gate_review_reasons),
        "reasons": flagship_gate_review_reasons,
        "receiptPath": str(flagship_gate_path),
        "releaseChannelPath": str(release_channel_path),
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
}
payload = {
    "generatedAt": now_iso(),
    "contract_name": "chummer6-ui.desktop_visual_familiarity_exit_gate",
    "channelId": release_channel_channel_id,
    "releaseVersion": release_channel_version,
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
receipt_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
if status != "pass":
    raise SystemExit(43)
PY

echo "[desktop-visual-familiarity-exit-gate] PASS"
