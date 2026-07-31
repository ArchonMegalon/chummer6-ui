#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

receipt_path="${CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH:-$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_AUDIT.generated.json}"
trace_path="${CHUMMER_USER_JOURNEY_TESTER_TRACE_PATH:-$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_TRACE.generated.json}"
linux_gate_path="${CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH:-$repo_root/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json}"
screenshot_dir="${CHUMMER_USER_JOURNEY_TESTER_SCREENSHOT_DIR:-}"
flagship_gate_path="${CHUMMER_USER_JOURNEY_TESTER_FLAGSHIP_GATE_PATH:-$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
refresh_trace_from_flagship_gate="${CHUMMER_USER_JOURNEY_TESTER_REFRESH_TRACE_FROM_FLAGSHIP_GATE:-0}"
release_candidate_path="${CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH:-}"
bundle_pointer_path=""
if [[ -n "${CHUMMER_USER_JOURNEY_TESTER_BUNDLE_POINTER_PATH:-}" ]]; then
  bundle_pointer_path="$CHUMMER_USER_JOURNEY_TESTER_BUNDLE_POINTER_PATH"
elif [[ -z "${CHUMMER_USER_JOURNEY_TESTER_TRACE_PATH+x}" \
  && -z "${CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH+x}" \
  && -z "${CHUMMER_USER_JOURNEY_TESTER_FLAGSHIP_GATE_PATH+x}" \
  && -z "${CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH+x}" \
  && -z "${CHUMMER_USER_JOURNEY_TESTER_SCREENSHOT_DIR+x}" \
  && -f "$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json" ]]; then
  bundle_pointer_path="$repo_root/.codex-studio/published/USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json"
fi
linux_gate_temp_path=""

cleanup() {
  if [[ -n "$linux_gate_temp_path" && -f "$linux_gate_temp_path" ]]; then
    rm -f "$linux_gate_temp_path"
  fi
}

trap cleanup EXIT

if [[ "${CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE:-0}" == "1" ]]; then
  if [[ -z "${CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH:-}" ]]; then
    linux_gate_temp_path="$(mktemp)"
    linux_gate_path="$linux_gate_temp_path"
  fi
  CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH="$linux_gate_path" \
    bash scripts/materialize-linux-desktop-exit-gate.sh >/dev/null
fi

mkdir -p "$(dirname "$receipt_path")"

python3 - <<'PY' "$receipt_path" "$trace_path" "$linux_gate_path" "$screenshot_dir" "$repo_root" "$flagship_gate_path" "$refresh_trace_from_flagship_gate" "$release_candidate_path" "$bundle_pointer_path"
from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import stat
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path, PurePosixPath
from typing import Any

receipt_path = Path(sys.argv[1])
trace_path = Path(sys.argv[2])
linux_gate_path = Path(sys.argv[3])
screenshot_dir_text = sys.argv[4].strip()
screenshot_dir = Path(screenshot_dir_text) if screenshot_dir_text else None
repo_root = Path(sys.argv[5])
flagship_gate_path = Path(sys.argv[6])
trace_mutation_request_value = sys.argv[7].strip()
trace_mutation_requested = trace_mutation_request_value != "0"
release_candidate_path_text = sys.argv[8].strip()
release_candidate_path = Path(release_candidate_path_text) if release_candidate_path_text else None
evidence_root = Path(
    os.environ.get("CHUMMER_USER_JOURNEY_TESTER_EVIDENCE_ROOT", str(repo_root))
)
bundle_pointer_path_text = sys.argv[9].strip()
bundle_pointer_path = Path(bundle_pointer_path_text) if bundle_pointer_path_text else None

CONTRACT_NAME = "chummer6-ui.user_journey_tester_audit"
TRACE_CONTRACT_NAME = "chummer6-ui.user_journey_tester_trace"
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MIN_SCREENSHOT_BYTES = 1024
IMMUTABLE_JSON_MAX_BYTES = 1024 * 1024
IMMUTABLE_SCREENSHOT_MAX_BYTES = 32 * 1024 * 1024
IMMUTABLE_ARTIFACT_MAX_BYTES = 256 * 1024 * 1024
TRACE_MAX_AGE_RAW = os.environ.get("CHUMMER_USER_JOURNEY_TESTER_MAX_TRACE_AGE_HOURS", "24")
try:
    TRACE_MAX_AGE_CONFIGURED = int(TRACE_MAX_AGE_RAW)
except ValueError:
    TRACE_MAX_AGE_CONFIGURED = 0
TRACE_MAX_AGE_POLICY_VALID = TRACE_MAX_AGE_CONFIGURED >= 1
MAX_TRACE_AGE_HOURS = max(1, TRACE_MAX_AGE_CONFIGURED)
TRACE_FUTURE_SKEW_MINUTES = 5

REQUIRED_WORKFLOWS = [
    "master_index_search_focus_stability",
    "file_new_character_visible_workspace",
    "minimal_character_build_save_reload",
    "major_navigation_sanity",
    "validation_or_export_smoke",
]

REQUIRED_WORKFLOW_ASSERTIONS = {
    "master_index_search_focus_stability": [
        "focus_preserved_after_typing",
        "search_text_accumulates_keyboard_input",
    ],
    "file_new_character_visible_workspace": [
        "new_character_action_opened_visible_workspace",
        "visible_workspace_nonblank",
        "starter_attributes_match_seeded_workspace",
        "section_preview_omits_review_copy",
    ],
    "minimal_character_build_save_reload": [
        "character_created_saved_reloaded",
        "reload_preserved_character_identity",
    ],
    "major_navigation_sanity": [
        "primary_navigation_clicks_change_visible_content",
        "no_unhandled_errors",
    ],
    "validation_or_export_smoke": [
        "validation_or_export_action_completed",
        "result_visible_or_file_created",
    ],
}


def now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_timestamp(value: Any) -> datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def read_stable_regular_file(
    path: Path,
    label: str,
    *,
    max_bytes: int,
    required: bool = True,
) -> tuple[bytes, list[str]]:
    unsafe_reason = f"{label} must be a stable regular non-symlink file: {path}"
    absolute_path = Path(os.path.abspath(path))
    current = Path(absolute_path.anchor)
    for component in absolute_path.parts[1:]:
        current /= component
        try:
            component_state = os.stat(current, follow_symlinks=False)
        except FileNotFoundError:
            break
        except OSError:
            return b"", [f"unable to inspect {label} path components safely: {path}"]
        file_attributes = int(getattr(component_state, "st_file_attributes", 0))
        reparse_attribute = int(getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
        if stat.S_ISLNK(component_state.st_mode) or (
            reparse_attribute and file_attributes & reparse_attribute
        ):
            return b"", [unsafe_reason]
    try:
        path_before = os.stat(path, follow_symlinks=False)
    except FileNotFoundError:
        return b"", [f"{label} is missing: {path}"] if required else []
    except OSError:
        return b"", [f"unable to inspect {label} safely: {path}"]
    if not stat.S_ISREG(path_before.st_mode):
        return b"", [unsafe_reason]

    open_flags = os.O_RDONLY
    for optional_flag in ("O_CLOEXEC", "O_NOFOLLOW", "O_NONBLOCK"):
        open_flags |= int(getattr(os, optional_flag, 0))
    try:
        descriptor = os.open(path, open_flags)
    except FileNotFoundError:
        return b"", [f"{label} is missing: {path}"] if required else []
    except OSError:
        return b"", [unsafe_reason]

    try:
        try:
            before = os.fstat(descriptor)
        except OSError:
            return b"", [f"unable to inspect opened {label} safely: {path}"]
        if not stat.S_ISREG(before.st_mode):
            return b"", [unsafe_reason]
        if (path_before.st_dev, path_before.st_ino) != (before.st_dev, before.st_ino):
            return b"", [f"{label} changed while being opened: {path}"]
        if before.st_size < 0 or before.st_size > max_bytes:
            return b"", [f"{label} exceeds the {max_bytes}-byte safety limit: {path}"]

        chunks: list[bytes] = []
        bytes_read = 0
        try:
            while bytes_read <= max_bytes:
                chunk = os.read(descriptor, min(64 * 1024, max_bytes + 1 - bytes_read))
                if not chunk:
                    break
                chunks.append(chunk)
                bytes_read += len(chunk)
        except OSError:
            return b"", [f"unable to read {label} safely: {path}"]
        raw = b"".join(chunks)
        if len(raw) > max_bytes:
            return b"", [f"{label} exceeds the {max_bytes}-byte safety limit: {path}"]

        try:
            after = os.fstat(descriptor)
            path_after = os.stat(path, follow_symlinks=False)
        except OSError:
            return b"", [f"{label} changed while being read: {path}"]
        before_state = (
            before.st_dev,
            before.st_ino,
            before.st_mode,
            before.st_size,
            before.st_mtime_ns,
        )
        after_state = (
            after.st_dev,
            after.st_ino,
            after.st_mode,
            after.st_size,
            after.st_mtime_ns,
        )
        path_after_state = (
            path_after.st_dev,
            path_after.st_ino,
            path_after.st_mode,
            path_after.st_size,
            path_after.st_mtime_ns,
        )
        if before_state != after_state or after_state != path_after_state or len(raw) != after.st_size:
            return b"", [f"{label} changed while being read: {path}"]
        return raw, []
    finally:
        os.close(descriptor)


def load_immutable_json(
    path: Path,
    label: str,
    *,
    required: bool = True,
) -> tuple[dict[str, Any], bytes, list[str]]:
    raw, reasons = read_stable_regular_file(
        path,
        label,
        max_bytes=IMMUTABLE_JSON_MAX_BYTES,
        required=required,
    )
    if reasons or not raw:
        return {}, raw, reasons
    try:
        loaded = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError):
        return {}, raw, [f"{label} must contain valid UTF-8 JSON: {path}"]
    if not isinstance(loaded, dict):
        return {}, raw, [f"{label} must contain a JSON object: {path}"]
    return loaded, raw, []


def load_json_from_verified_bytes(
    path: Path,
    raw: bytes,
    label: str,
) -> tuple[dict[str, Any], bytes, list[str]]:
    try:
        loaded = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError):
        return {}, raw, [f"{label} must contain valid UTF-8 JSON: {path}"]
    if not isinstance(loaded, dict):
        return {}, raw, [f"{label} must contain a JSON object: {path}"]
    return loaded, raw, []


def normalize_sha256(value: Any) -> str:
    digest = str(value or "").strip().lower()
    if digest.startswith("sha256:"):
        digest = digest[7:]
    return digest if len(digest) == 64 and all(character in "0123456789abcdef" for character in digest) else ""


def status_ok(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


def bool_value(payload: dict[str, Any], key: str) -> Any:
    if key in payload:
        return payload.get(key)
    evidence = payload.get("evidence")
    if isinstance(evidence, dict):
        return evidence.get(key)
    return None


def string_value(payload: dict[str, Any], key: str) -> str:
    value = payload.get(key)
    if value is None:
        evidence = payload.get("evidence")
        if isinstance(evidence, dict):
            value = evidence.get(key)
    return str(value or "").strip()


def integer_value(payload: dict[str, Any], key: str) -> int | None:
    value = payload.get(key)
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if isinstance(value, str):
        text = value.strip()
        if text and text.lstrip("-").isdigit():
            return int(text)
    return None


def normalized_absolute_path(value: str | Path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(value)))


def path_is_within(path: str | Path, root: str | Path) -> bool:
    try:
        return os.path.commonpath(
            [normalized_absolute_path(path), normalized_absolute_path(root)]
        ) == normalized_absolute_path(root)
    except (OSError, ValueError):
        return False


def dict_rows(value: Any) -> list[dict[str, Any]]:
    if isinstance(value, list):
        return [row for row in value if isinstance(row, dict)]
    return []


def workflow_id(row: dict[str, Any]) -> str:
    return str(row.get("id") or row.get("workflow_id") or row.get("workflowId") or row.get("name") or "").strip()


def row_screenshots(row: dict[str, Any]) -> list[str]:
    value = row.get("screenshots") or row.get("screenshot_paths") or row.get("screenshotPaths") or []
    if not isinstance(value, list):
        return []
    return [str(item or "").strip() for item in value if str(item or "").strip()]


def screenshot_path(value: str) -> Path:
    candidate = Path(value)
    if candidate.is_absolute():
        return candidate
    if screenshot_dir is None:
        return repo_root / ".missing-user-journey-screenshot-root" / value
    return screenshot_dir / value


def screenshot_path_is_safe(value: str) -> bool:
    relative = PurePosixPath(value)
    return (
        bool(value)
        and value not in {".", ".."}
        and not relative.is_absolute()
        and "\\" not in value
        and all(component not in {".", ".."} for component in value.split("/"))
        and relative.as_posix() == value
    )


def credible_png(data: bytes) -> bool:
    return (
        len(data) >= 33
        and data.startswith(PNG_SIGNATURE)
        and int.from_bytes(data[8:12], "big") == 13
        and data[12:16] == b"IHDR"
        and int.from_bytes(data[16:20], "big") > 0
        and int.from_bytes(data[20:24], "big") > 0
    )


def path_within_evidence_root(path: Path) -> bool:
    try:
        path.resolve().relative_to(evidence_root.resolve())
        return True
    except Exception:
        return False


def screenshot_review(
    values: list[str],
    expected_hashes: dict[str, Any],
    seen_hashes: set[str],
) -> tuple[list[dict[str, Any]], list[str]]:
    rows: list[dict[str, Any]] = []
    reasons: list[str] = []
    for value in values:
        safe_path = screenshot_path_is_safe(value)
        bundled_entry = bundle_workflow_screenshots.get(value)
        path = (
            bundled_entry.path
            if bundled_entry is not None
            else (screenshot_path(value) if safe_path else None)
        )
        row: dict[str, Any] = {
            "path": str(path) if path is not None else value,
            "exists": False,
            "within_repo_root": path_within_evidence_root(path) if path is not None else False,
            "within_evidence_root": path_within_evidence_root(path) if path is not None else False,
            "within_verified_bundle": bundled_entry is not None,
            "is_png": False,
            "sha256": "",
            "expected_sha256": str(expected_hashes.get(value) or "").strip(),
            "digest_matches_trace": False,
        }
        if path is None:
            rows.append(row)
            continue
        if verified_bundle is not None and bundled_entry is None:
            data = b""
            read_reasons = [f"screenshot is not bound by the evidence bundle: {value}"]
        elif bundled_entry is not None:
            data = bundled_entry.data
            read_reasons = []
        else:
            data, read_reasons = read_stable_regular_file(
                path,
                "screenshot",
                max_bytes=IMMUTABLE_SCREENSHOT_MAX_BYTES,
            )
        if read_reasons:
            reasons.extend(read_reasons)
            rows.append(row)
            continue
        row["exists"] = True
        digest = hashlib.sha256(data).hexdigest()
        row["is_png"] = credible_png(data)
        row["sha256"] = digest
        row["size_bytes"] = len(data)
        expected_digest = normalize_sha256(expected_hashes.get(value))
        row["digest_matches_trace"] = bool(expected_digest and expected_digest == digest)
        if not row["within_repo_root"] and not row["within_verified_bundle"]:
            reasons.append(f"screenshot is outside governed evidence root: {path}")
        if not row["is_png"]:
            reasons.append(f"screenshot is not a PNG: {path}")
        if row["size_bytes"] < MIN_SCREENSHOT_BYTES:
            reasons.append(
                f"screenshot is too small to count as credible review evidence ({row['size_bytes']} bytes): {path}"
            )
        if digest in seen_hashes:
            reasons.append(f"screenshot is duplicated by content: {path}")
        seen_hashes.add(digest)
        if not expected_digest:
            reasons.append(f"screenshot SHA-256 binding is missing or malformed: {value}")
        elif expected_digest != digest:
            reasons.append(f"screenshot SHA-256 binding does not match current bytes: {value}")
        rows.append(row)
    return rows, reasons


def linux_binary_target_ok(trace: dict[str, Any]) -> bool:
    if bool_value(trace, "linux_binary_under_test") is True:
        return True
    if bool_value(trace, "actual_binary_under_test") is True:
        target = " ".join([string_value(trace, "binary_under_test"), string_value(trace, "run_target")]).lower()
        return "linux" in target or not target.strip()
    target = " ".join([string_value(trace, "binary_under_test"), string_value(trace, "run_target")]).lower()
    return "linux" in target and any(token in target for token in ("binary", "executable", "bin", "appimage"))


def trace_workflows(trace: dict[str, Any]) -> list[dict[str, Any]]:
    evidence = trace.get("evidence")
    if isinstance(evidence, dict):
        rows = dict_rows(evidence.get("workflows"))
        if rows:
            return rows
    return dict_rows(trace.get("workflows"))


reasons: list[str] = []
verified_bundle: Any = None
bundle_module: Any = None
bundle_entries_by_role: dict[str, tuple[Any, ...]] = {}
bundle_workflow_screenshots: dict[str, Any] = {}
bundle_mouse_screenshots: dict[str, Any] = {}
bundle_manifest_sha256 = ""
bundle_id = ""
if bundle_pointer_path is not None:
    module_path = repo_root / "scripts" / "ai" / "milestones" / "user_journey_evidence_bundle.py"
    try:
        module_spec = importlib.util.spec_from_file_location(
            "chummer_user_journey_evidence_bundle",
            module_path,
        )
        if module_spec is None or module_spec.loader is None:
            raise RuntimeError("bundle verifier module could not be loaded")
        bundle_module = importlib.util.module_from_spec(module_spec)
        sys.modules[module_spec.name] = bundle_module
        module_spec.loader.exec_module(bundle_module)
        verified_bundle = bundle_module.verify_bundle(bundle_pointer_path)
        for bundle_role in bundle_module.ALL_ROLES:
            bundle_entries_by_role[bundle_role] = verified_bundle.many(bundle_role)
        trace_path = verified_bundle.single("trace").path
        linux_gate_path = verified_bundle.single("linux_gate").path
        flagship_gate_path = verified_bundle.single("flagship_gate").path
        release_candidate_path = verified_bundle.single("release_candidate").path
        screenshot_dir = verified_bundle.manifest_path.parent / "workflow-screenshots"
        bundle_workflow_screenshots = {
            entry.declared_path: entry
            for entry in verified_bundle.many("workflow_screenshot")
        }
        bundle_mouse_screenshots = {
            entry.declared_path: entry
            for entry in verified_bundle.many("mouse_screenshot")
        }
        bundle_manifest_sha256 = verified_bundle.manifest_sha256
        bundle_id = verified_bundle.bundle_id
    except Exception as exc:
        reasons.append(f"user journey evidence bundle verification failed: {exc}")
        verified_bundle = None

if verified_bundle is not None:
    trace_entry = verified_bundle.single("trace")
    linux_gate_entry = verified_bundle.single("linux_gate")
    flagship_gate_entry = verified_bundle.single("flagship_gate")
    trace, trace_bytes, trace_read_reasons = load_json_from_verified_bytes(
        trace_entry.path,
        trace_entry.data,
        "user journey tester trace",
    )
    linux_gate, linux_gate_bytes, linux_gate_read_reasons = load_json_from_verified_bytes(
        linux_gate_entry.path,
        linux_gate_entry.data,
        "Linux desktop exit gate",
    )
    flagship_gate, flagship_gate_bytes, flagship_gate_read_reasons = load_json_from_verified_bytes(
        flagship_gate_entry.path,
        flagship_gate_entry.data,
        "flagship release gate",
    )
else:
    trace, trace_bytes, trace_read_reasons = load_immutable_json(
        trace_path,
        "user journey tester trace",
    )
    linux_gate, linux_gate_bytes, linux_gate_read_reasons = load_immutable_json(
        linux_gate_path,
        "Linux desktop exit gate",
    )
    flagship_gate, flagship_gate_bytes, flagship_gate_read_reasons = load_immutable_json(
        flagship_gate_path,
        "flagship release gate",
        required=False,
    )
reasons.extend(trace_read_reasons)
reasons.extend(linux_gate_read_reasons)
reasons.extend(flagship_gate_read_reasons)
evaluated_at = datetime.now(timezone.utc)
trace_generated_at_text = str(trace.get("generated_at_utc") or "").strip()
trace_generated_at = parse_timestamp(trace_generated_at_text)
for trace_timestamp_alias in ("generated_at", "generatedAt"):
    if trace_timestamp_alias in trace and str(trace.get(trace_timestamp_alias) or "").strip() != trace_generated_at_text:
        reasons.append(
            f"user journey tester trace {trace_timestamp_alias} conflicts with canonical generated_at_utc."
        )
trace_sha256 = hashlib.sha256(trace_bytes).hexdigest() if trace_bytes else ""
release_candidate: dict[str, Any] = {}
release_candidate_bytes = b""
release_candidate_sha256 = ""
linux_release_channel = linux_gate.get("release_channel")
source_candidate_mode = (
    isinstance(linux_release_channel, dict)
    and linux_release_channel.get("use_promoted_installer") is False
)
if release_candidate_path is None and isinstance(linux_release_channel, dict):
    inferred_release_candidate_path = str(linux_release_channel.get("path") or "").strip()
    if inferred_release_candidate_path:
        release_candidate_path = Path(inferred_release_candidate_path)
if release_candidate_path is None:
    reasons.append(
        "release candidate binding is required: set CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH "
        "or embed release_channel.path in the Linux desktop exit gate."
    )
else:
    if verified_bundle is not None:
        release_candidate_entry = verified_bundle.single("release_candidate")
        release_candidate, release_candidate_bytes, release_candidate_reasons = load_json_from_verified_bytes(
            release_candidate_entry.path,
            release_candidate_entry.data,
            "release candidate",
        )
    else:
        release_candidate, release_candidate_bytes, release_candidate_reasons = load_immutable_json(
            release_candidate_path,
            "release candidate",
        )
    reasons.extend(release_candidate_reasons)
    if release_candidate_bytes:
        release_candidate_sha256 = hashlib.sha256(release_candidate_bytes).hexdigest()

if trace_mutation_requested:
    reasons.append(
        "trace mutation request is prohibited: "
        "CHUMMER_USER_JOURNEY_TESTER_REFRESH_TRACE_FROM_FLAGSHIP_GATE must be 0; "
        "the audit never mutates external user-journey trace evidence."
    )

if not TRACE_MAX_AGE_POLICY_VALID:
    reasons.append(
        "CHUMMER_USER_JOURNEY_TESTER_MAX_TRACE_AGE_HOURS must be a positive integer."
    )

if not trace:
    reasons.append(f"user journey tester trace is missing: {trace_path}")
if trace and str(trace.get("contract_name") or "").strip() != TRACE_CONTRACT_NAME:
    reasons.append(f"user journey tester trace contract_name must be {TRACE_CONTRACT_NAME}.")
if trace and not status_ok(trace.get("status")):
    reasons.append("user journey tester trace status is not pass/passed/ready.")
if trace and trace_generated_at is None:
    reasons.append("user journey tester trace must include an offset-aware generated_at_utc timestamp.")
elif trace_generated_at is not None:
    if trace_generated_at > evaluated_at + timedelta(minutes=TRACE_FUTURE_SKEW_MINUTES):
        reasons.append("user journey tester trace generated_at_utc is in the future.")
    elif evaluated_at - trace_generated_at > timedelta(hours=MAX_TRACE_AGE_HOURS):
        reasons.append(
            "user journey tester trace is stale "
            f"(older than {MAX_TRACE_AGE_HOURS} hours)."
        )

if not linux_gate:
    reasons.append(f"Linux desktop exit gate is missing: {linux_gate_path}")
if linux_gate and not status_ok(linux_gate.get("status")):
    reasons.append("Linux desktop exit gate is not passing.")
if flagship_gate and not status_ok(flagship_gate.get("status")):
    reasons.append("flagship release gate is not passing.")

linux_gate_mouse_first = linux_gate.get("mouse_first_journey")
linux_gate_mouse_first_primary = (
    linux_gate_mouse_first.get("primary")
    if isinstance(linux_gate_mouse_first, dict)
    else {}
)
if not isinstance(linux_gate_mouse_first_primary, dict):
    reasons.append("Linux desktop exit gate mouse_first_journey.primary must be a JSON object.")
    linux_gate_mouse_first_primary = {}
linux_gate_mouse_first_receipt = (
    linux_gate_mouse_first_primary.get("receipt")
    if isinstance(linux_gate_mouse_first_primary, dict)
    else {}
)
if not isinstance(linux_gate_mouse_first_receipt, dict):
    linux_gate_mouse_first_receipt = {}

linux_gate_mouse_first_screenshots: list[Any] = []
linux_gate_mouse_first_trace_path = ""
linux_gate_mouse_first_pointer_action_count = 0
linux_gate_mouse_first_text_entry_action_count = 0
linux_gate_mouse_first_source_receipt_path = ""
source_mouse_receipt: dict[str, Any] = {}
source_mouse_receipt_bytes = b""
source_mouse_receipt_sha256 = ""
source_receipt_path: Path | None = None
mouse_first_screenshot_reviews: list[dict[str, Any]] = []
mouse_first_trace_review: dict[str, Any] = {}
mouse_first_evidence_digests: list[str] = []
mouse_first_compatibility_alias_groups: list[list[str]] = []
mouse_first_unexpected_duplicate_groups: list[list[str]] = []
source_screenshot_paths: list[Any] = []
if not linux_gate_mouse_first_receipt:
    reasons.append("Linux desktop exit gate must embed a mouse_first_journey primary receipt.")
else:
    if not status_ok(linux_gate_mouse_first_receipt.get("status")):
        reasons.append("Linux mouse_first_journey primary receipt is not passing.")
    if string_value(linux_gate_mouse_first_receipt, "journeyMode") != "mouse_first_live_binary":
        reasons.append("Linux mouse_first_journey primary receipt must prove mouse_first_live_binary.")
    if linux_gate_mouse_first_receipt.get("hasSavedWorkspace") is not True:
        reasons.append("Linux mouse_first_journey primary receipt must prove a saved workspace.")
    linux_gate_mouse_first_screenshots = linux_gate_mouse_first_receipt.get("screenshotPaths")
    linux_gate_mouse_first_trace_path = string_value(linux_gate_mouse_first_receipt, "tracePath")
    parsed_pointer_action_count = integer_value(linux_gate_mouse_first_receipt, "pointerActionCount")
    parsed_text_entry_action_count = integer_value(linux_gate_mouse_first_receipt, "textEntryActionCount")
    if parsed_pointer_action_count is None or parsed_pointer_action_count < 0:
        reasons.append("Linux mouse_first_journey primary receipt pointerActionCount must be a non-negative integer.")
    else:
        linux_gate_mouse_first_pointer_action_count = parsed_pointer_action_count
    if parsed_text_entry_action_count is None or parsed_text_entry_action_count < 0:
        reasons.append("Linux mouse_first_journey primary receipt textEntryActionCount must be a non-negative integer.")
    else:
        linux_gate_mouse_first_text_entry_action_count = parsed_text_entry_action_count
    if linux_gate_mouse_first_pointer_action_count <= linux_gate_mouse_first_text_entry_action_count:
        reasons.append("Linux mouse_first_journey primary receipt must prove a pointer-dominant interaction mix.")
    parsed_direct_text_mutation_count = integer_value(linux_gate_mouse_first_receipt, "directTextMutationCount")
    if parsed_direct_text_mutation_count != 0:
        reasons.append("Linux mouse_first_journey primary receipt directTextMutationCount must be zero.")
    if bool(linux_gate_mouse_first_receipt.get("usedForcedComboDropdownOpen")):
        reasons.append("Linux mouse_first_journey primary receipt must fail closed on forced combo dropdown open.")
    if bool(linux_gate_mouse_first_receipt.get("usedComboSelectionFallback")):
        reasons.append("Linux mouse_first_journey primary receipt must fail closed on combo selection fallback.")
    linux_gate_mouse_first_observed_input_events = linux_gate_mouse_first_receipt.get("observedInputEvents")
    if not isinstance(linux_gate_mouse_first_observed_input_events, list) or len(linux_gate_mouse_first_observed_input_events) < 8:
        reasons.append("Linux mouse_first_journey primary receipt must publish observed input events.")
    if not isinstance(linux_gate_mouse_first_screenshots, list) or len(linux_gate_mouse_first_screenshots) < 5:
        reasons.append("Linux mouse_first_journey primary receipt must publish five screenshot-backed review frames.")
    if not linux_gate_mouse_first_trace_path:
        reasons.append("Linux mouse_first_journey primary receipt must publish a tracePath.")

if isinstance(linux_gate_mouse_first_primary, dict):
    linux_gate_mouse_first_source_receipt_path = str(
        linux_gate_mouse_first_primary.get("receipt_path") or ""
    ).strip()
if not linux_gate_mouse_first_source_receipt_path:
    reasons.append("Linux mouse_first_journey primary evidence must publish receipt_path.")
else:
    if verified_bundle is not None:
        source_receipt_entry = verified_bundle.single("source_receipt")
        source_receipt_path = source_receipt_entry.path
        source_mouse_receipt, source_mouse_receipt_bytes, source_receipt_reasons = load_json_from_verified_bytes(
            source_receipt_entry.path,
            source_receipt_entry.data,
            "Linux mouse-first source receipt",
        )
    else:
        source_receipt_path = Path(linux_gate_mouse_first_source_receipt_path)
        source_mouse_receipt, source_mouse_receipt_bytes, source_receipt_reasons = load_immutable_json(
            source_receipt_path,
            "Linux mouse-first source receipt",
        )
    reasons.extend(source_receipt_reasons)
    if source_mouse_receipt_bytes:
        source_mouse_receipt_sha256 = hashlib.sha256(source_mouse_receipt_bytes).hexdigest()

if source_mouse_receipt and source_receipt_path is not None:
    source_screenshot_paths = source_mouse_receipt.get("screenshotPaths")
    source_screenshot_directory_text = string_value(
        source_mouse_receipt,
        "screenshotDirectory",
    )
    source_screenshot_directory = (
        Path(source_screenshot_directory_text)
        if source_screenshot_directory_text
        else source_receipt_path.parent
    )
    if not isinstance(source_screenshot_paths, list) or not 5 <= len(source_screenshot_paths) <= 20:
        reasons.append(
            "Linux mouse-first source receipt must bind between five and twenty screenshot paths."
        )
        source_screenshot_paths = []
    seen_mouse_paths: set[str] = set()
    seen_mouse_digests: set[str] = set()
    mouse_digest_paths: dict[str, list[str]] = {}
    for screenshot_index, raw_mouse_path in enumerate(source_screenshot_paths):
        declared_path = str(raw_mouse_path or "").strip()
        review: dict[str, Any] = {
            "index": screenshot_index,
            "declared_path": declared_path,
            "resolved_path": "",
            "sha256": "",
            "size_bytes": 0,
            "is_png": False,
        }
        if not declared_path or declared_path in seen_mouse_paths:
            reasons.append(
                "Linux mouse-first source receipt screenshot paths must be non-empty and unique."
            )
            mouse_first_screenshot_reviews.append(review)
            continue
        seen_mouse_paths.add(declared_path)
        declared = Path(declared_path)
        if "\\" in declared_path or any(part in {".", ".."} for part in PurePosixPath(declared_path).parts):
            reasons.append(
                f"Linux mouse-first source receipt screenshot path is unsafe: {declared_path}"
            )
            mouse_first_screenshot_reviews.append(review)
            continue
        bundled_mouse_entry = bundle_mouse_screenshots.get(declared_path)
        resolved = (
            bundled_mouse_entry.path
            if bundled_mouse_entry is not None
            else (declared if declared.is_absolute() else source_screenshot_directory / declared)
        )
        review["resolved_path"] = str(resolved)
        if verified_bundle is not None and bundled_mouse_entry is None:
            screenshot_bytes = b""
            screenshot_reasons = [
                f"Linux mouse-first screenshot is not bound by the evidence bundle: {declared_path}"
            ]
        elif bundled_mouse_entry is not None:
            screenshot_bytes = bundled_mouse_entry.data
            screenshot_reasons = []
        else:
            screenshot_bytes, screenshot_reasons = read_stable_regular_file(
                resolved,
                "Linux mouse-first screenshot",
                max_bytes=IMMUTABLE_SCREENSHOT_MAX_BYTES,
            )
        reasons.extend(screenshot_reasons)
        if screenshot_bytes:
            screenshot_digest = hashlib.sha256(screenshot_bytes).hexdigest()
            review["sha256"] = screenshot_digest
            review["size_bytes"] = len(screenshot_bytes)
            review["is_png"] = credible_png(screenshot_bytes)
            mouse_first_evidence_digests.append(screenshot_digest)
            if not review["is_png"]:
                reasons.append(f"Linux mouse-first screenshot is not a PNG: {resolved}")
            if len(screenshot_bytes) < MIN_SCREENSHOT_BYTES:
                reasons.append(
                    f"Linux mouse-first screenshot is too small to count as credible evidence: {resolved}"
                )
            seen_mouse_digests.add(screenshot_digest)
            mouse_digest_paths.setdefault(screenshot_digest, []).append(declared_path)
        mouse_first_screenshot_reviews.append(review)

    allowed_compatibility_alias_name_groups = (
        frozenset({
            "01-new-character-dialog.png",
            "file_new_character_visible_workspace-before.png",
        }),
        frozenset({
            "03-post-dialog-close.png",
            "04-workspace-opened.png",
            "file_new_character_visible_workspace-after.png",
        }),
        frozenset({
            "05-workspace-saved.png",
            "minimal_character_build_save_reload-before.png",
        }),
    )
    for duplicate_paths in mouse_digest_paths.values():
        if len(duplicate_paths) < 2:
            continue
        duplicate_names = frozenset(Path(path).name for path in duplicate_paths)
        normalized_group = sorted(duplicate_paths)
        if any(
            duplicate_names.issubset(allowed_group)
            for allowed_group in allowed_compatibility_alias_name_groups
        ):
            mouse_first_compatibility_alias_groups.append(normalized_group)
        else:
            mouse_first_unexpected_duplicate_groups.append(normalized_group)

    declared_mouse_trace_path = string_value(source_mouse_receipt, "tracePath")
    mouse_first_trace_review = {
        "declared_path": declared_mouse_trace_path,
        "resolved_path": "",
        "sha256": "",
        "size_bytes": 0,
        "valid_json_object": False,
    }
    if not declared_mouse_trace_path:
        reasons.append("Linux mouse-first source receipt tracePath is missing.")
    elif "\\" in declared_mouse_trace_path or any(
        part in {".", ".."} for part in PurePosixPath(declared_mouse_trace_path).parts
    ):
        reasons.append(
            f"Linux mouse-first source receipt tracePath is unsafe: {declared_mouse_trace_path}"
        )
    else:
        declared_mouse_trace = Path(declared_mouse_trace_path)
        bundled_mouse_trace_entry = (
            verified_bundle.single("mouse_trace") if verified_bundle is not None else None
        )
        resolved_mouse_trace = (
            bundled_mouse_trace_entry.path
            if bundled_mouse_trace_entry is not None
            else (
                declared_mouse_trace
                if declared_mouse_trace.is_absolute()
                else source_receipt_path.parent / declared_mouse_trace
            )
        )
        mouse_first_trace_review["resolved_path"] = str(resolved_mouse_trace)
        if bundled_mouse_trace_entry is not None:
            if bundled_mouse_trace_entry.declared_path != declared_mouse_trace_path:
                mouse_trace, mouse_trace_bytes = {}, b""
                mouse_trace_reasons = [
                    "Linux mouse-first trace declaration does not match the evidence bundle."
                ]
            else:
                mouse_trace, mouse_trace_bytes, mouse_trace_reasons = load_json_from_verified_bytes(
                    bundled_mouse_trace_entry.path,
                    bundled_mouse_trace_entry.data,
                    "Linux mouse-first trace",
                )
        else:
            mouse_trace, mouse_trace_bytes, mouse_trace_reasons = load_immutable_json(
                resolved_mouse_trace,
                "Linux mouse-first trace",
            )
        reasons.extend(mouse_trace_reasons)
        if mouse_trace_bytes:
            mouse_trace_digest = hashlib.sha256(mouse_trace_bytes).hexdigest()
            mouse_first_trace_review["sha256"] = mouse_trace_digest
            mouse_first_trace_review["size_bytes"] = len(mouse_trace_bytes)
            mouse_first_trace_review["valid_json_object"] = bool(mouse_trace)
            mouse_first_evidence_digests.append(mouse_trace_digest)
        if mouse_trace and "status" in mouse_trace and not status_ok(mouse_trace.get("status")):
            reasons.append("Linux mouse-first trace is not passing.")

if screenshot_dir is None:
    inferred_screenshot_dir = ""
    if isinstance(linux_gate_mouse_first_primary, dict):
        inferred_screenshot_dir = str(
            linux_gate_mouse_first_primary.get("screenshot_dir") or ""
        ).strip()
    if not inferred_screenshot_dir:
        inferred_screenshot_dir = string_value(
            linux_gate_mouse_first_receipt,
            "screenshotDirectory",
        )
    if inferred_screenshot_dir:
        screenshot_dir = Path(inferred_screenshot_dir)
    else:
        reasons.append(
            "user journey screenshot directory must be explicit or embedded in Linux primary evidence."
        )
if screenshot_dir is not None and not screenshot_dir.is_absolute():
    reasons.append("user journey screenshot directory must be an absolute path.")

trace_release_version = string_value(trace, "release_version")
trace_release_channel = string_value(trace, "release_channel")
trace_artifact_digest = normalize_sha256(string_value(trace, "artifact_digest"))
trace_artifact_digest_source = string_value(trace, "artifact_digest_source")
trace_source_mouse_receipt_name = string_value(trace, "source_mouse_receipt_name")
trace_source_mouse_receipt_path = string_value(trace, "source_mouse_receipt_path")
trace_source_mouse_receipt_sha256 = normalize_sha256(
    string_value(trace, "source_mouse_receipt_sha256")
)
mouse_release_version = string_value(linux_gate_mouse_first_receipt, "releaseVersion") \
    or string_value(linux_gate_mouse_first_receipt, "version")
mouse_release_channel = string_value(linux_gate_mouse_first_receipt, "channelId") \
    or string_value(linux_gate_mouse_first_receipt, "channel")
mouse_artifact_digest = normalize_sha256(
    string_value(linux_gate_mouse_first_receipt, "artifactDigest")
)
mouse_artifact_digest_source = string_value(
    linux_gate_mouse_first_receipt,
    "artifactDigestSource",
)

for field_name, trace_value, mouse_value in (
    ("release_version", trace_release_version, mouse_release_version),
    ("release_channel", trace_release_channel, mouse_release_channel),
    ("artifact_digest", trace_artifact_digest, mouse_artifact_digest),
    ("artifact_digest_source", trace_artifact_digest_source, mouse_artifact_digest_source),
):
    if not trace_value:
        reasons.append(f"tester trace {field_name} binding is missing or malformed.")
    elif not mouse_value:
        reasons.append(f"Linux mouse-first receipt {field_name} binding is missing or malformed.")
    elif trace_value != mouse_value:
        reasons.append(f"tester trace {field_name} does not match the Linux mouse-first receipt.")

if linux_gate_mouse_first_source_receipt_path:
    expected_source_name = Path(linux_gate_mouse_first_source_receipt_path).name
    if trace_source_mouse_receipt_name != expected_source_name:
        reasons.append("tester trace source_mouse_receipt_name does not match Linux primary receipt_path.")
    if not trace_source_mouse_receipt_path or normalized_absolute_path(trace_source_mouse_receipt_path) != normalized_absolute_path(linux_gate_mouse_first_source_receipt_path):
        reasons.append("tester trace source_mouse_receipt_path does not match Linux primary receipt_path.")
if not trace_source_mouse_receipt_sha256:
    reasons.append("tester trace source_mouse_receipt_sha256 binding is missing or malformed.")
elif source_mouse_receipt_sha256 and trace_source_mouse_receipt_sha256 != source_mouse_receipt_sha256:
    reasons.append("tester trace source_mouse_receipt_sha256 does not match current source receipt bytes.")

if source_mouse_receipt:
    for field_name, external_value, embedded_value in (
        ("releaseVersion", string_value(source_mouse_receipt, "releaseVersion"), mouse_release_version),
        ("channelId", string_value(source_mouse_receipt, "channelId"), mouse_release_channel),
        (
            "artifactDigest",
            normalize_sha256(string_value(source_mouse_receipt, "artifactDigest")),
            mouse_artifact_digest,
        ),
        (
            "artifactDigestSource",
            string_value(source_mouse_receipt, "artifactDigestSource"),
            mouse_artifact_digest_source,
        ),
    ):
        if not external_value or external_value != embedded_value:
            reasons.append(
                f"Linux mouse-first source receipt {field_name} does not match the embedded primary receipt."
            )
    if not status_ok(source_mouse_receipt.get("status")):
        reasons.append("Linux mouse-first source receipt is not passing.")
    if string_value(source_mouse_receipt, "journeyMode") != "mouse_first_live_binary":
        reasons.append("Linux mouse-first source receipt must prove mouse_first_live_binary.")

release_candidate_version = string_value(release_candidate, "releaseVersion") \
    or string_value(release_candidate, "version")
release_candidate_channel = string_value(release_candidate, "channel") \
    or string_value(release_candidate, "channelId")
release_candidate_linux_artifacts = [
    row
    for row in dict_rows(release_candidate.get("artifacts"))
    if string_value(row, "platform").lower() == "linux"
    and string_value(row, "rid").lower() == "linux-x64"
    and string_value(row, "head").lower() == "avalonia"
    and string_value(row, "kind").lower() == "installer"
]
release_candidate_artifact: dict[str, Any] = {}
release_candidate_artifact_digest = ""
if release_candidate:
    if len(release_candidate_linux_artifacts) != 1:
        reasons.append(
            "release candidate must contain exactly one Avalonia linux/linux-x64 installer artifact."
        )
    else:
        release_candidate_artifact = release_candidate_linux_artifacts[0]
        release_candidate_artifact_digest = normalize_sha256(
            string_value(release_candidate_artifact, "sha256")
            or string_value(release_candidate_artifact, "artifactDigest")
            or string_value(release_candidate_artifact, "artifact_digest")
        )
        if not release_candidate_artifact_digest:
            reasons.append("release candidate Linux installer SHA-256 is missing or malformed.")

for field_name, trace_value, candidate_value in (
    ("release_version", trace_release_version, release_candidate_version),
    ("release_channel", trace_release_channel, release_candidate_channel),
    ("artifact_digest", trace_artifact_digest, release_candidate_artifact_digest),
):
    if (
        release_candidate
        and not (source_candidate_mode and field_name == "artifact_digest")
        and (not candidate_value or trace_value != candidate_value)
    ):
        reasons.append(f"tester trace {field_name} does not match the release candidate.")

release_candidate_contract_name = str(release_candidate.get("contract_name") or "").strip()
release_candidate_contract_alias = str(release_candidate.get("contractName") or "").strip()
release_candidate_version_alias = str(release_candidate.get("version") or "").strip()
release_candidate_release_version_alias = str(release_candidate.get("releaseVersion") or "").strip()
release_candidate_channel_alias = str(release_candidate.get("channel") or "").strip()
release_candidate_channel_id_alias = str(release_candidate.get("channelId") or "").strip()
release_candidate_published_value = str(release_candidate.get("publishedAt") or "").strip()
release_candidate_generated_values = [
    str(release_candidate.get(key) or "").strip()
    for key in ("generated_at", "generatedAt")
]
release_candidate_published_at = parse_timestamp(release_candidate_published_value)
release_candidate_generated_at = parse_timestamp(release_candidate_generated_values[0])
if release_candidate:
    if release_candidate_contract_name != "Chummer.Hub.Registry.Contracts" \
        or release_candidate_contract_alias != release_candidate_contract_name:
        reasons.append("release candidate contract aliases must both equal Chummer.Hub.Registry.Contracts.")
    if release_candidate.get("schemaVersion") != 1:
        reasons.append("release candidate schemaVersion must equal 1.")
    if release_candidate.get("status") != "published":
        reasons.append("release candidate status must equal published.")
    if not release_candidate_version_alias \
        or release_candidate_version_alias != release_candidate_release_version_alias:
        reasons.append("release candidate version and releaseVersion must be equal and non-empty.")
    if not release_candidate_channel_alias \
        or release_candidate_channel_alias != release_candidate_channel_id_alias:
        reasons.append("release candidate channel and channelId must be equal and non-empty.")
    if not release_candidate_published_value or release_candidate_published_at is None:
        reasons.append("release candidate publishedAt must be offset-aware.")
    if any(not value for value in release_candidate_generated_values) \
        or len(set(release_candidate_generated_values)) != 1 \
        or release_candidate_generated_at is None:
        reasons.append("release candidate generated timestamp aliases must be equal and offset-aware.")
    if release_candidate_published_at is not None \
        and release_candidate_generated_at is not None \
        and release_candidate_generated_at < release_candidate_published_at:
        reasons.append("release candidate generated timestamp must not predate publishedAt.")

release_candidate_artifact_id = string_value(release_candidate_artifact, "artifactId")
release_candidate_artifact_file_name = string_value(release_candidate_artifact, "fileName")
release_candidate_artifact_size = integer_value(release_candidate_artifact, "sizeBytes")
if release_candidate_artifact:
    expected_artifact_fields = {
        "artifactId": "avalonia-linux-x64-installer",
        "head": "avalonia",
        "platform": "linux",
        "rid": "linux-x64",
        "arch": "x64",
        "kind": "installer",
        "version": release_candidate_version,
        "releaseVersion": release_candidate_version,
        "channel": release_candidate_channel,
        "channelId": release_candidate_channel,
    }
    for field_name, expected_value in expected_artifact_fields.items():
        if string_value(release_candidate_artifact, field_name) != expected_value:
            reasons.append(f"release candidate Linux artifact {field_name} does not match candidate truth.")
    if not release_candidate_artifact_file_name \
        or Path(release_candidate_artifact_file_name).name != release_candidate_artifact_file_name \
        or release_candidate_artifact_file_name in {".", ".."}:
        reasons.append("release candidate Linux artifact fileName must be a safe basename.")
    if release_candidate_artifact_size is None or release_candidate_artifact_size <= 0:
        reasons.append("release candidate Linux artifact sizeBytes must be a positive integer.")

publication_bindings = [
    row
    for row in dict_rows(release_candidate.get("artifactPublicationBindings"))
    if string_value(row, "artifactId") == "avalonia-linux-x64-installer"
    and string_value(row, "head") == "avalonia"
    and string_value(row, "platform") == "linux"
    and string_value(row, "rid") == "linux-x64"
    and string_value(row, "arch") == "x64"
    and string_value(row, "kind") == "installer"
]
if release_candidate:
    if len(publication_bindings) != 1:
        reasons.append("release candidate must contain exactly one published Linux artifact binding.")
    else:
        publication_binding = publication_bindings[0]
        expected_publication_fields = {
            "artifactId": release_candidate_artifact_id,
            "head": "avalonia",
            "platform": "linux",
            "rid": "linux-x64",
            "arch": "x64",
            "kind": "installer",
            "tupleId": "avalonia:linux:linux-x64",
            "releaseVersion": release_candidate_version,
            "channelId": release_candidate_channel,
            "publicationState": (
                "published"
                if release_candidate_channel in {"public_stable", "stable"}
                else release_candidate_channel
            ),
        }
        for field_name, expected_value in expected_publication_fields.items():
            if string_value(publication_binding, field_name) != expected_value:
                reasons.append(
                    f"release candidate Linux publication binding {field_name} does not match candidate truth."
                )

gate_contract_name = str(linux_gate.get("contract_name") or "").strip()
gate_generated_at = parse_timestamp(linux_gate.get("generated_at"))
gate_release_version = string_value(linux_gate, "releaseVersion")
gate_release_channel = string_value(linux_gate, "channelId")
gate_head = linux_gate.get("head") if isinstance(linux_gate.get("head"), dict) else {}
gate_build = linux_gate.get("build") if isinstance(linux_gate.get("build"), dict) else {}
gate_checks = linux_gate.get("checks") if isinstance(linux_gate.get("checks"), dict) else {}
gate_projected_artifact = (
    gate_checks.get("release_channel_linux_artifact")
    if isinstance(gate_checks.get("release_channel_linux_artifact"), dict)
    else {}
)
gate_release_channel_details = (
    linux_gate.get("release_channel")
    if isinstance(linux_gate.get("release_channel"), dict)
    else {}
)
if linux_gate:
    if gate_contract_name != "chummer6-ui.linux_desktop_exit_gate":
        reasons.append("Linux desktop exit gate contract_name is invalid.")
    if linux_gate.get("status") != "passed":
        reasons.append("Linux desktop exit gate status must equal passed.")
    if gate_generated_at is None:
        reasons.append("Linux desktop exit gate generated_at must be offset-aware.")
    expected_gate_head = {
        "app_key": "avalonia",
        "platform": "linux",
        "rid": "linux-x64",
        "version": release_candidate_version,
        "channel": release_candidate_channel,
    }
    for field_name, expected_value in expected_gate_head.items():
        if string_value(gate_head, field_name) != expected_value:
            reasons.append(f"Linux desktop exit gate head.{field_name} does not match release candidate.")
    if gate_release_version != release_candidate_version:
        reasons.append("Linux desktop exit gate releaseVersion does not match release candidate.")
    if gate_release_channel != release_candidate_channel:
        reasons.append("Linux desktop exit gate channelId does not match release candidate.")

if release_candidate_artifact:
    projected_string_fields = (
        "artifactId",
        "head",
        "platform",
        "rid",
        "arch",
        "kind",
        "fileName",
        "version",
        "releaseVersion",
        "channel",
        "channelId",
    )
    if not source_candidate_mode:
        projected_string_fields += ("sha256",)
    for field_name in projected_string_fields:
        if string_value(gate_projected_artifact, field_name) != string_value(
            release_candidate_artifact,
            field_name,
        ):
            reasons.append(
                f"Linux desktop exit gate projected artifact {field_name} does not match release candidate."
            )
    if (
        not source_candidate_mode
        and integer_value(gate_projected_artifact, "sizeBytes") != release_candidate_artifact_size
    ):
        reasons.append("Linux desktop exit gate projected artifact sizeBytes does not match release candidate.")

release_candidate_file_bytes = b""
release_candidate_file_sha256 = ""
release_candidate_file_size = 0
tested_installer_bytes = b""
tested_installer_sha256 = ""
tested_installer_size = 0
release_candidate_files_root = ""
release_candidate_file_path = ""
release_candidate_file_declared_path = ""
tested_installer_path = string_value(gate_release_channel_details, "installer_smoke_artifact_path")
tested_installer_resolved_path = ""
gate_build_installer_path = string_value(gate_build, "installer_path")
gate_build_installer_sha256 = normalize_sha256(string_value(gate_build, "installer_sha256"))
gate_build_installer_size = integer_value(gate_build, "installer_bytes")
if (
    not source_candidate_mode
    and release_candidate_path is not None
    and release_candidate_artifact_file_name
):
    declared_candidate_manifest_path = string_value(gate_release_channel_details, "path")
    semantic_candidate_path = (
        Path(declared_candidate_manifest_path)
        if declared_candidate_manifest_path
        else release_candidate_path
    )
    release_candidate_files_root = str(semantic_candidate_path.parent / "files")
    declared_files_root = string_value(gate_release_channel_details, "local_desktop_files_root")
    if not declared_files_root \
        or normalized_absolute_path(declared_files_root) != normalized_absolute_path(release_candidate_files_root):
        reasons.append("Linux desktop exit gate local_desktop_files_root does not match candidate files root.")
    release_candidate_file_declared_path = str(
        Path(release_candidate_files_root) / release_candidate_artifact_file_name
    )
    if not path_is_within(release_candidate_file_declared_path, release_candidate_files_root):
        reasons.append("release candidate artifact path escapes the candidate files root.")
    else:
        if verified_bundle is not None:
            candidate_artifact_entry = verified_bundle.single("candidate_artifact")
            release_candidate_file_path = str(candidate_artifact_entry.path)
            if normalized_absolute_path(candidate_artifact_entry.declared_path) != normalized_absolute_path(
                release_candidate_file_declared_path
            ):
                release_candidate_file_bytes = b""
                candidate_file_reasons = [
                    "release candidate artifact declaration does not match the evidence bundle."
                ]
            else:
                release_candidate_file_bytes = candidate_artifact_entry.data
                candidate_file_reasons = []
        else:
            release_candidate_file_path = release_candidate_file_declared_path
            release_candidate_file_bytes, candidate_file_reasons = read_stable_regular_file(
                Path(release_candidate_file_path),
                "release candidate Linux installer bytes",
                max_bytes=IMMUTABLE_ARTIFACT_MAX_BYTES,
            )
        reasons.extend(candidate_file_reasons)
        if release_candidate_file_bytes:
            release_candidate_file_sha256 = hashlib.sha256(release_candidate_file_bytes).hexdigest()
            release_candidate_file_size = len(release_candidate_file_bytes)

if not tested_installer_path:
    reasons.append("Linux desktop exit gate installer_smoke_artifact_path is missing.")
else:
    if gate_build_installer_path \
        and normalized_absolute_path(gate_build_installer_path) != normalized_absolute_path(tested_installer_path):
        reasons.append("Linux desktop exit gate build.installer_path does not match tested installer path.")
    gate_dist_dir = string_value(gate_build, "dist_dir")
    if not gate_dist_dir or not path_is_within(tested_installer_path, gate_dist_dir):
        reasons.append("Linux desktop exit gate tested installer path escapes build.dist_dir.")
    if release_candidate_artifact_file_name \
        and Path(tested_installer_path).name != release_candidate_artifact_file_name:
        reasons.append("Linux desktop exit gate tested installer basename does not match release candidate.")
    if verified_bundle is not None:
        tested_installer_entry = verified_bundle.single("tested_installer")
        tested_installer_resolved_path = str(tested_installer_entry.path)
        if normalized_absolute_path(tested_installer_entry.declared_path) != normalized_absolute_path(
            tested_installer_path
        ):
            tested_installer_bytes = b""
            tested_installer_reasons = [
                "tested installer declaration does not match the evidence bundle."
            ]
        else:
            tested_installer_bytes = tested_installer_entry.data
            tested_installer_reasons = []
    else:
        tested_installer_resolved_path = tested_installer_path
        tested_installer_bytes, tested_installer_reasons = read_stable_regular_file(
            Path(tested_installer_path),
            "Linux desktop exit gate tested installer bytes",
            max_bytes=IMMUTABLE_ARTIFACT_MAX_BYTES,
        )
    reasons.extend(tested_installer_reasons)
    if tested_installer_bytes:
        tested_installer_sha256 = hashlib.sha256(tested_installer_bytes).hexdigest()
        tested_installer_size = len(tested_installer_bytes)

if not gate_build_installer_sha256 or (
    tested_installer_sha256 and gate_build_installer_sha256 != tested_installer_sha256
):
    reasons.append("Linux desktop exit gate build.installer_sha256 does not match tested bytes.")
if gate_build_installer_size is None or (
    tested_installer_bytes and gate_build_installer_size != tested_installer_size
):
    reasons.append("Linux desktop exit gate build.installer_bytes does not match tested bytes.")
if release_candidate_file_sha256 and release_candidate_file_sha256 != release_candidate_artifact_digest:
    reasons.append("release candidate artifact SHA-256 does not match registry file bytes.")
if release_candidate_file_bytes and release_candidate_file_size != release_candidate_artifact_size:
    reasons.append("release candidate artifact sizeBytes does not match registry file bytes.")

candidate_digest_values = {
    "trace": trace_artifact_digest,
    "source_receipt": mouse_artifact_digest,
    "gate_build": gate_build_installer_sha256,
    "tested_installer": tested_installer_sha256,
}
if not source_candidate_mode:
    candidate_digest_values.update(
        {
            "gate_projection": normalize_sha256(string_value(gate_projected_artifact, "sha256")),
            "candidate_manifest": release_candidate_artifact_digest,
            "candidate_file": release_candidate_file_sha256,
        }
    )
if any(not value for value in candidate_digest_values.values()) \
    or len(set(candidate_digest_values.values())) != 1:
    binding_scope = (
        "trace, source receipt, gate build, and tested installer"
        if source_candidate_mode
        else "trace, source receipt, tested installer, gate projection, and promoted candidate bytes"
    )
    reasons.append(f"candidate_artifact_digest_mismatch: {binding_scope} must share one SHA-256 digest.")

use_promoted_installer = gate_release_channel_details.get("use_promoted_installer")
promoted_installer_path = string_value(gate_release_channel_details, "promoted_installer_path")
if not isinstance(use_promoted_installer, bool):
    reasons.append("Linux desktop exit gate use_promoted_installer must be a boolean.")
elif use_promoted_installer:
    if not promoted_installer_path:
        reasons.append("Linux desktop exit gate promoted_installer_path is missing.")
    elif verified_bundle is None \
        and normalized_absolute_path(promoted_installer_path) != normalized_absolute_path(release_candidate_file_path):
        reasons.append("Linux desktop exit gate promoted_installer_path must equal the release-candidate shelf path.")

declared_mouse_receipt_path = string_value(
    gate_release_channel_details,
    "mouse_first_journey_receipt_path",
)
if not declared_mouse_receipt_path \
    or normalized_absolute_path(declared_mouse_receipt_path) != normalized_absolute_path(linux_gate_mouse_first_source_receipt_path):
    reasons.append("Linux desktop exit gate mouse-first receipt path projections do not agree.")
if source_mouse_receipt and source_mouse_receipt != linux_gate_mouse_first_receipt:
    reasons.append("Linux desktop exit gate embedded mouse-first receipt does not match captured receipt bytes.")

source_receipt_completed_at = parse_timestamp(source_mouse_receipt.get("completedAtUtc"))
if source_mouse_receipt:
    expected_source_receipt_fields = {
        "status": "pass",
        "journeyMode": "mouse_first_live_binary",
        "headId": "avalonia",
        "platform": "linux",
        "arch": "x64",
        "rid": "linux-x64",
        "channelId": release_candidate_channel,
        "artifactDigestSource": "environment",
    }
    for field_name, expected_value in expected_source_receipt_fields.items():
        if string_value(source_mouse_receipt, field_name) != expected_value:
            reasons.append(f"Linux mouse-first source receipt {field_name} is invalid.")
    source_version = string_value(source_mouse_receipt, "version")
    source_release_version = string_value(source_mouse_receipt, "releaseVersion")
    if not source_version or source_version != source_release_version \
        or source_version != release_candidate_version:
        reasons.append("Linux mouse-first source receipt version bindings do not match release candidate.")
    if source_receipt_completed_at is None:
        reasons.append("Linux mouse-first source receipt completedAtUtc must be offset-aware.")

if trace_generated_at is not None and source_receipt_completed_at is not None \
    and trace_generated_at != source_receipt_completed_at:
    reasons.append("tester trace generated_at_utc must equal source receipt completedAtUtc.")
if release_candidate_published_at is not None and source_receipt_completed_at is not None \
    and release_candidate_published_at > source_receipt_completed_at:
    reasons.append("release candidate was published after the captured user journey completed.")
if source_receipt_completed_at is not None and gate_generated_at is not None \
    and source_receipt_completed_at > gate_generated_at + timedelta(minutes=TRACE_FUTURE_SKEW_MINUTES):
    reasons.append("source receipt completedAtUtc is later than Linux gate generated_at.")

tester_shard_id = string_value(trace, "tester_shard_id")
fix_shard_id = string_value(trace, "fix_shard_id")
fix_shard_separate = bool(tester_shard_id and fix_shard_id and tester_shard_id != fix_shard_id)
if not fix_shard_separate:
    reasons.append("tester_shard_id and fix_shard_id must both be present and different.")

used_internal_apis = bool_value(trace, "used_internal_apis")
if used_internal_apis is not False:
    reasons.append("tester trace must declare used_internal_apis=false.")

linux_binary_under_test = linux_binary_target_ok(trace)
if not linux_binary_under_test:
    reasons.append("tester trace must prove it exercised the Linux desktop binary, not only in-process APIs.")

blocking_findings = trace.get("open_blocking_findings")
if not isinstance(blocking_findings, list):
    reasons.append("tester trace open_blocking_findings must be an empty JSON array.")
    blocking_findings = []
open_blocking_findings_count = len([item for item in blocking_findings if str(item or "").strip()])
if blocking_findings:
    reasons.append("tester trace has open blocking findings.")

raw_workflows = trace.get("workflows")
if not isinstance(raw_workflows, list):
    reasons.append("tester trace workflows must be a JSON array.")
    raw_workflows = []
if any(not isinstance(row, dict) for row in raw_workflows):
    reasons.append("tester trace workflows must contain only JSON objects.")
workflow_rows = [row for row in raw_workflows if isinstance(row, dict)]
workflow_ids = [workflow_id(row) for row in workflow_rows]
if len(workflow_rows) != len(REQUIRED_WORKFLOWS):
    reasons.append(f"tester trace must contain exactly {len(REQUIRED_WORKFLOWS)} workflow rows.")
if len(set(workflow_ids)) != len(workflow_ids):
    reasons.append("tester trace workflow IDs must be unique.")
unexpected_workflows = sorted(set(workflow_ids) - set(REQUIRED_WORKFLOWS))
if unexpected_workflows:
    reasons.append("tester trace has unexpected workflow(s): " + ", ".join(unexpected_workflows))
workflow_by_id = {workflow_id(row): row for row in workflow_rows if workflow_id(row)}
workflow_reviews: list[dict[str, Any]] = []
missing_workflows: list[str] = []
nonpassing_workflows: list[str] = []
insufficient_screenshot_workflows: list[str] = []
missing_assertion_workflows: dict[str, list[str]] = {}
seen_screenshot_hashes: set[str] = set()
seen_screenshot_paths: set[str] = set()

for required_id in REQUIRED_WORKFLOWS:
    row = workflow_by_id.get(required_id)
    if row is None:
        missing_workflows.append(required_id)
        workflow_reviews.append(
            {
                "id": required_id,
                "status": "missing",
                "screenshots": [],
                "screenshotReview": [],
                "missingAssertions": REQUIRED_WORKFLOW_ASSERTIONS[required_id],
            }
        )
        continue

    status = str(row.get("status") or "").strip().lower()
    if status != "pass":
        nonpassing_workflows.append(required_id)

    screenshots = row_screenshots(row)
    if not isinstance(row.get("screenshots"), list) or len(screenshots) != 2:
        insufficient_screenshot_workflows.append(required_id)
    for screenshot in screenshots:
        if not screenshot_path_is_safe(screenshot):
            reasons.append(
                f"{required_id}: screenshot path must be normalized repo-relative POSIX text "
                f"without dot or dotdot segments: {screenshot}"
            )
            continue
        normalized_screenshot_path = normalized_absolute_path(screenshot_path(screenshot))
        if normalized_screenshot_path in seen_screenshot_paths:
            reasons.append(f"{required_id}: screenshot path is reused: {screenshot}")
        seen_screenshot_paths.add(normalized_screenshot_path)

    declared_screenshot_hashes = row.get("screenshot_sha256")
    if not isinstance(declared_screenshot_hashes, dict):
        reasons.append(f"{required_id}: screenshot_sha256 must be a JSON object.")
        declared_screenshot_hashes = {}
    if set(str(key) for key in declared_screenshot_hashes) != set(screenshots):
        reasons.append(f"{required_id}: screenshot_sha256 keys must exactly match screenshots.")

    screenshot_rows, screenshot_reasons = screenshot_review(
        screenshots,
        declared_screenshot_hashes,
        seen_screenshot_hashes,
    )
    reasons.extend([f"{required_id}: {reason}" for reason in screenshot_reasons])

    assertions = row.get("assertions")
    if not isinstance(assertions, dict):
        assertions = {}
    required_assertions = REQUIRED_WORKFLOW_ASSERTIONS[required_id]
    if set(str(key) for key in assertions) != set(required_assertions):
        reasons.append(f"{required_id}: assertion keys must exactly match the producer contract.")
    missing_assertions = [
        assertion
        for assertion in required_assertions
        if assertions.get(assertion) is not True
    ]
    if missing_assertions:
        missing_assertion_workflows[required_id] = missing_assertions

    interaction_notes = row.get("interaction_notes")
    if interaction_notes is not None and (
        not isinstance(interaction_notes, list)
        or len(interaction_notes) > 20
        or any(
            not isinstance(note, str) or not note.strip() or len(note) > 1000
            for note in interaction_notes
        )
    ):
        reasons.append(
            f"{required_id}: interaction_notes must be null or a bounded list of non-empty strings."
        )

    workflow_reviews.append(
        {
            "id": required_id,
            "status": status,
            "screenshots": screenshots,
            "screenshotReview": screenshot_rows,
            "declaredScreenshotSha256": declared_screenshot_hashes,
            "assertions": {key: bool(assertions.get(key) is True) for key in REQUIRED_WORKFLOW_ASSERTIONS[required_id]},
            "interactionNotes": interaction_notes,
            "missingAssertions": missing_assertions,
        }
    )

if missing_workflows:
    reasons.append("tester trace is missing required workflow(s): " + ", ".join(sorted(missing_workflows)))
if nonpassing_workflows:
    reasons.append("tester trace has nonpassing workflow(s): " + ", ".join(sorted(nonpassing_workflows)))
if insufficient_screenshot_workflows:
    reasons.append(
        "tester trace must have exactly two screenshots for workflow(s): "
        + ", ".join(sorted(insufficient_screenshot_workflows))
    )
if missing_assertion_workflows:
    reasons.append(
        "tester trace is missing required user-observable assertion(s): "
        + "; ".join(
            f"{workflow}: {', '.join(assertions)}"
            for workflow, assertions in sorted(missing_assertion_workflows.items())
        )
    )
if len(seen_screenshot_paths) != 10 or len(seen_screenshot_hashes) != 10:
    reasons.append("tester trace must bind exactly ten unique screenshot paths and SHA-256 digests.")

trace_bytes_after_audit, trace_after_read_reasons = read_stable_regular_file(
    trace_path,
    "user journey tester trace after audit",
    max_bytes=IMMUTABLE_JSON_MAX_BYTES,
)
reasons.extend(trace_after_read_reasons)
trace_sha256_after_audit = (
    hashlib.sha256(trace_bytes_after_audit).hexdigest()
    if trace_bytes_after_audit
    else ""
)
trace_bytes_unchanged_during_audit = trace_sha256 == trace_sha256_after_audit
if not trace_bytes_unchanged_during_audit:
    reasons.append("user journey tester trace bytes changed while the immutable audit was running.")

candidate_version_values = {
    trace_release_version,
    mouse_release_version,
    string_value(source_mouse_receipt, "releaseVersion"),
    gate_release_version,
    string_value(gate_head, "version"),
    release_candidate_version,
    string_value(release_candidate_artifact, "releaseVersion"),
}
candidate_channel_values = {
    trace_release_channel,
    mouse_release_channel,
    string_value(source_mouse_receipt, "channelId"),
    gate_release_channel,
    string_value(gate_head, "channel"),
    release_candidate_channel,
    string_value(release_candidate_artifact, "channelId"),
}
mouse_first_evidence_binding_passes = (
    5 <= len(mouse_first_screenshot_reviews) <= 20
    and all(
        bool(row.get("sha256"))
        and bool(row.get("is_png"))
        and int(row.get("size_bytes") or 0) >= MIN_SCREENSHOT_BYTES
        for row in mouse_first_screenshot_reviews
    )
    and bool(mouse_first_trace_review.get("sha256"))
    and mouse_first_trace_review.get("valid_json_object") is True
    and len(mouse_first_evidence_digests) == len(mouse_first_screenshot_reviews) + 1
    and len(seen_mouse_digests) >= 5
    and not mouse_first_unexpected_duplicate_groups
    and str(mouse_first_trace_review.get("sha256") or "") not in seen_mouse_digests
)
if not mouse_first_evidence_binding_passes:
    reasons.append(
        "Linux mouse-first screenshot and trace evidence must be readable, sufficiently distinct, credible, and byte-bound."
    )
candidate_binding_passes = (
    bool(release_candidate)
    and release_candidate.get("status") == "published"
    and len(release_candidate_linux_artifacts) == 1
    and len(publication_bindings) == 1
    and bool(source_mouse_receipt)
    and source_mouse_receipt == linux_gate_mouse_first_receipt
    and len(candidate_version_values) == 1
    and "" not in candidate_version_values
    and len(candidate_channel_values) == 1
    and "" not in candidate_channel_values
    and all(candidate_digest_values.values())
    and len(set(candidate_digest_values.values())) == 1
    and release_candidate_file_size == release_candidate_artifact_size
    and tested_installer_size == release_candidate_artifact_size
)
source_candidate_binding_passes = (
    source_candidate_mode
    and bool(source_mouse_receipt)
    and source_mouse_receipt == linux_gate_mouse_first_receipt
    and len(candidate_version_values) == 1
    and "" not in candidate_version_values
    and len(candidate_channel_values) == 1
    and "" not in candidate_channel_values
    and all(candidate_digest_values.values())
    and len(set(candidate_digest_values.values())) == 1
    and tested_installer_size > 0
    and tested_installer_size == gate_build_installer_size
)
bundle_verification_status = "not_requested"
if bundle_pointer_path is not None:
    bundle_verification_status = "fail"
if verified_bundle is not None:
    try:
        verified_bundle_after_audit = bundle_module.verify_bundle(bundle_pointer_path)
        if (
            verified_bundle_after_audit.bundle_id != bundle_id
            or verified_bundle_after_audit.manifest_sha256 != bundle_manifest_sha256
        ):
            raise RuntimeError("bundle identity changed during audit")
        bundle_verification_status = "pass"
    except Exception as exc:
        reasons.append(f"user journey evidence bundle changed during audit: {exc}")
status = "pass" if not reasons else "fail"
generated_at = now_iso()
payload: dict[str, Any] = {
    "contract_name": CONTRACT_NAME,
    "status": status,
    "generated_at": generated_at,
    "generatedAt": generated_at,
    "reasons": reasons,
    "open_blocking_findings_count": open_blocking_findings_count,
    "linux_binary_under_test": linux_binary_under_test,
    "used_internal_apis": used_internal_apis,
    "fix_shard_separate": fix_shard_separate,
    "trace_mutation_requested": trace_mutation_requested,
    "trace_mutation_performed": False,
    "evidence": {
        "trace_path": str(trace_path),
        "trace_sha256": trace_sha256,
        "trace_sha256_after_audit": trace_sha256_after_audit,
        "trace_bytes_unchanged_during_audit": trace_bytes_unchanged_during_audit,
        "trace_mutation_request_value": trace_mutation_request_value,
        "trace_mutation_requested": trace_mutation_requested,
        "trace_mutation_allowed": False,
        "trace_mutation_performed": False,
        "trace_generated_at_utc": (
            trace_generated_at.isoformat().replace("+00:00", "Z")
            if trace_generated_at is not None
            else ""
        ),
        "trace_max_age_hours": MAX_TRACE_AGE_HOURS,
        "trace_future_skew_minutes": TRACE_FUTURE_SKEW_MINUTES,
        "bundle_pointer_path": str(bundle_pointer_path) if bundle_pointer_path is not None else "",
        "bundle_verification_status": bundle_verification_status,
        "bundle_id": bundle_id,
        "bundle_manifest_path": (
            str(verified_bundle.manifest_path) if verified_bundle is not None else ""
        ),
        "bundle_manifest_sha256": bundle_manifest_sha256,
        "bundle_entry_count": len(verified_bundle.entries) if verified_bundle is not None else 0,
        "linux_gate_path": str(linux_gate_path),
        "linux_gate_sha256": hashlib.sha256(linux_gate_bytes).hexdigest() if linux_gate_bytes else "",
        "flagship_gate_path": str(flagship_gate_path),
        "flagship_gate_sha256": (
            hashlib.sha256(flagship_gate_bytes).hexdigest() if flagship_gate_bytes else ""
        ),
        "screenshot_dir": str(screenshot_dir) if screenshot_dir is not None else "",
        "evidence_root": str(evidence_root),
        "linux_gate_status": str(linux_gate.get("status") or "").strip(),
        "linux_gate_mouse_first_journey_status": str(linux_gate_mouse_first_primary.get("status") or "").strip(),
        "linux_gate_mouse_first_journey_mode": string_value(linux_gate_mouse_first_receipt, "journeyMode"),
        "linux_gate_mouse_first_journey_pointer_action_count": linux_gate_mouse_first_pointer_action_count,
        "linux_gate_mouse_first_journey_text_entry_action_count": linux_gate_mouse_first_text_entry_action_count,
        "linux_gate_mouse_first_journey_screenshot_count": (
            len(linux_gate_mouse_first_receipt.get("screenshotPaths"))
            if isinstance(linux_gate_mouse_first_receipt.get("screenshotPaths"), list)
            else 0
        ),
        "mouse_first_evidence_binding_status": (
            "pass" if mouse_first_evidence_binding_passes else "fail"
        ),
        "mouse_first_screenshot_reviews": mouse_first_screenshot_reviews,
        "mouse_first_compatibility_alias_groups": mouse_first_compatibility_alias_groups,
        "mouse_first_unexpected_duplicate_groups": mouse_first_unexpected_duplicate_groups,
        "mouse_first_trace_review": mouse_first_trace_review,
        "flagship_gate_status": str(flagship_gate.get("status") or "").strip(),
        "tester_shard_id": tester_shard_id,
        "fix_shard_id": fix_shard_id,
        "required_workflows": REQUIRED_WORKFLOWS,
        "required_workflow_assertions": REQUIRED_WORKFLOW_ASSERTIONS,
        "workflows": workflow_reviews,
        "missing_workflows": sorted(missing_workflows),
        "nonpassing_workflows": sorted(nonpassing_workflows),
        "insufficient_screenshot_workflows": sorted(insufficient_screenshot_workflows),
        "missing_assertion_workflows": missing_assertion_workflows,
        "unique_screenshot_path_count": len(seen_screenshot_paths),
        "unique_screenshot_sha256_count": len(seen_screenshot_hashes),
        "open_blocking_findings_count": open_blocking_findings_count,
        "used_internal_apis": used_internal_apis,
        "fix_shard_separate": fix_shard_separate,
        "linux_binary_under_test": linux_binary_under_test,
        "run_linux_gate_requested": os.environ.get("CHUMMER_USER_JOURNEY_TESTER_RUN_LINUX_GATE", "0") == "1",
        "release_candidate_path": str(release_candidate_path) if release_candidate_path is not None else "",
        "release_candidate_sha256": release_candidate_sha256,
        "release_candidate_binding_status": (
            "source"
            if source_candidate_binding_passes
            else "pass"
            if candidate_binding_passes
            else "fail"
        ),
        "artifact_binding_mode": "source" if source_candidate_mode else "promoted",
        "release_candidate_version": str(
            release_candidate.get("releaseVersion") or release_candidate.get("version") or ""
        ).strip(),
        "release_candidate_channel": str(
            release_candidate.get("channel") or release_candidate.get("channelId") or ""
        ).strip(),
        "release_candidate_status": str(release_candidate.get("status") or "").strip(),
        "release_candidate_rollout_state": str(
            release_candidate.get("rolloutState") or release_candidate.get("rollout_state") or ""
        ).strip(),
        "release_candidate_supportability_state": str(
            release_candidate.get("supportabilityState")
            or release_candidate.get("supportability_state")
            or ""
        ).strip(),
        "release_candidate_artifact_id": release_candidate_artifact_id,
        "release_candidate_artifact_file_name": release_candidate_artifact_file_name,
        "release_candidate_artifact_size_bytes": release_candidate_artifact_size,
        "release_candidate_artifact_sha256": release_candidate_artifact_digest,
        "release_candidate_file_path": release_candidate_file_path,
        "release_candidate_file_declared_path": release_candidate_file_declared_path,
        "release_candidate_file_sha256": release_candidate_file_sha256,
        "release_candidate_file_size_bytes": release_candidate_file_size,
        "tested_installer_path": tested_installer_path,
        "tested_installer_resolved_path": tested_installer_resolved_path,
        "tested_installer_sha256": tested_installer_sha256,
        "tested_installer_size_bytes": tested_installer_size,
        "source_mouse_receipt_path": linux_gate_mouse_first_source_receipt_path,
        "source_mouse_receipt_resolved_path": (
            str(source_receipt_path) if source_receipt_path is not None else ""
        ),
        "source_mouse_receipt_sha256": source_mouse_receipt_sha256,
        "trace_release_version": trace_release_version,
        "trace_release_channel": trace_release_channel,
        "trace_artifact_digest": trace_artifact_digest,
        "trace_artifact_digest_source": trace_artifact_digest_source,
        "candidate_digest_bindings": candidate_digest_values,
    },
}

receipt_text = json.dumps(payload, indent=2, sort_keys=True) + "\n"
temp_receipt_path = ""
try:
    with tempfile.NamedTemporaryFile(
        "w",
        encoding="utf-8",
        dir=receipt_path.parent,
        prefix=f".{receipt_path.name}.",
        suffix=".tmp",
        delete=False,
    ) as handle:
        handle.write(receipt_text)
        handle.flush()
        os.fsync(handle.fileno())
        temp_receipt_path = handle.name
    os.replace(temp_receipt_path, receipt_path)
finally:
    if temp_receipt_path and os.path.exists(temp_receipt_path):
        os.unlink(temp_receipt_path)

if status != "pass":
    raise SystemExit("[USER-JOURNEY-TESTER] FAIL: " + "; ".join(reasons))
print("[USER-JOURNEY-TESTER] PASS: adversarial Linux user-journey audit passed.")
PY
