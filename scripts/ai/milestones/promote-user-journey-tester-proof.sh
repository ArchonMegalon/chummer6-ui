#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
workspace_root="$(cd "$repo_root/.." && pwd)"
capture_root="${1:-}"
release_candidate="${2:-$workspace_root/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json}"

if [[ -z "$capture_root" ]]; then
  echo "Usage: $0 <absolute-passing-capture-root> [release-candidate.json]" >&2
  exit 2
fi
case "$capture_root" in
  /*) ;;
  *)
    echo "The capture root must be absolute." >&2
    exit 2
    ;;
esac

staged_trace="$capture_root/USER_JOURNEY_TESTER_TRACE.generated.json"
staged_gate="$capture_root/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
staged_flagship_gate="$capture_root/UI_FLAGSHIP_RELEASE_GATE.generated.json"
staged_audit="$capture_root/USER_JOURNEY_TESTER_AUDIT.generated.json"
published_root="${CHUMMER_USER_JOURNEY_TESTER_PUBLISHED_ROOT:-$repo_root/.codex-studio/published}"
canonical_trace="$published_root/USER_JOURNEY_TESTER_TRACE.generated.json"
canonical_audit="$published_root/USER_JOURNEY_TESTER_AUDIT.generated.json"
canonical_screenshot_base="$published_root/user-journey-tester-screenshots"
bundle_pointer="$published_root/USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json"

case "$published_root" in
  /*) ;;
  *)
    echo "The published evidence root must be absolute." >&2
    exit 2
    ;;
esac

canonical_screenshot_dir="$(python3 - "$staged_trace" "$staged_gate" "$staged_audit" "$release_candidate" "$canonical_trace" "$canonical_screenshot_base" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import stat
import sys
import tempfile
import uuid
from pathlib import Path, PurePosixPath

staged_trace = Path(sys.argv[1])
staged_gate = Path(sys.argv[2])
staged_audit = Path(sys.argv[3])
release_candidate = Path(sys.argv[4])
canonical_trace = Path(sys.argv[5])
canonical_screenshot_base = Path(sys.argv[6])


def stable_bytes(path: Path, label: str, maximum: int) -> bytes:
    nofollow = int(getattr(os, "O_NOFOLLOW", 0))
    directory_only = int(getattr(os, "O_DIRECTORY", 0))
    if not nofollow or not directory_only or os.open not in os.supports_dir_fd:
        raise SystemExit(
            f"{label} cannot be read safely because this platform lacks component-anchored open support."
        )

    absolute_path = Path(os.path.abspath(path))
    if not absolute_path.name:
        raise SystemExit(f"{label} must name a regular file: {path}")

    reparse_attribute = int(getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))

    def is_reparse_point(state: os.stat_result) -> bool:
        file_attributes = int(getattr(state, "st_file_attributes", 0))
        return bool(reparse_attribute and file_attributes & reparse_attribute)

    def stable_state(state: os.stat_result) -> tuple[int, int, int, int, int]:
        return (
            state.st_dev,
            state.st_ino,
            state.st_mode,
            state.st_size,
            state.st_mtime_ns,
        )

    directory_flags = os.O_RDONLY | nofollow | directory_only
    file_flags = os.O_RDONLY | nofollow
    for optional_flag in ("O_CLOEXEC", "O_NONBLOCK"):
        flag = int(getattr(os, optional_flag, 0))
        directory_flags |= flag
        file_flags |= flag

    directory_fd = -1
    file_fd = -1
    try:
        directory_fd = os.open(absolute_path.anchor, directory_flags)
        root_state = os.fstat(directory_fd)
        if not stat.S_ISDIR(root_state.st_mode) or is_reparse_point(root_state):
            raise SystemExit(f"{label} has an unsafe root path component: {path}")

        for component in absolute_path.parts[1:-1]:
            next_fd = os.open(component, directory_flags, dir_fd=directory_fd)
            next_state = os.fstat(next_fd)
            if not stat.S_ISDIR(next_state.st_mode) or is_reparse_point(next_state):
                os.close(next_fd)
                raise SystemExit(
                    f"{label} must not traverse symbolic-link or reparse-point ancestors: {path}"
                )
            os.close(directory_fd)
            directory_fd = next_fd

        file_fd = os.open(absolute_path.name, file_flags, dir_fd=directory_fd)
        state_before = os.fstat(file_fd)
        path_before = os.stat(
            absolute_path.name,
            dir_fd=directory_fd,
            follow_symlinks=False,
        )
        if (
            not stat.S_ISREG(state_before.st_mode)
            or is_reparse_point(state_before)
            or stable_state(path_before) != stable_state(state_before)
        ):
            raise SystemExit(f"{label} must be a regular non-symlink file: {path}")
        if state_before.st_size < 1 or state_before.st_size > maximum:
            raise SystemExit(f"{label} size is outside the promotion safety bound: {path}")

        chunks: list[bytes] = []
        bytes_read = 0
        while bytes_read <= maximum:
            chunk = os.read(file_fd, min(64 * 1024, maximum + 1 - bytes_read))
            if not chunk:
                break
            chunks.append(chunk)
            bytes_read += len(chunk)
        data = b"".join(chunks)
        if len(data) > maximum:
            raise SystemExit(f"{label} size is outside the promotion safety bound: {path}")

        state_after = os.fstat(file_fd)
        path_after = os.stat(
            absolute_path.name,
            dir_fd=directory_fd,
            follow_symlinks=False,
        )
        if (
            stable_state(state_before) != stable_state(state_after)
            or stable_state(state_after) != stable_state(path_after)
            or len(data) != state_after.st_size
        ):
            raise SystemExit(f"{label} changed while being read: {path}")
        return data
    except FileNotFoundError as exc:
        raise SystemExit(f"{label} is missing: {path}") from exc
    except OSError as exc:
        raise SystemExit(
            f"{label} must not traverse symbolic-link or reparse-point path components: {path}"
        ) from exc
    finally:
        if file_fd >= 0:
            os.close(file_fd)
        if directory_fd >= 0:
            os.close(directory_fd)


def json_object(path: Path, label: str) -> tuple[dict[str, object], bytes]:
    data = stable_bytes(path, label, 1024 * 1024)
    try:
        payload = json.loads(data.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise SystemExit(f"{label} is not valid UTF-8 JSON: {path}") from exc
    if not isinstance(payload, dict):
        raise SystemExit(f"{label} must be a JSON object: {path}")
    return payload, data


def digest(value: object) -> str:
    text = str(value or "").strip().lower()
    if text.startswith("sha256:"):
        text = text[7:]
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        return ""
    return text


trace, trace_bytes = json_object(staged_trace, "staged trace")
gate, gate_bytes = json_object(staged_gate, "staged Linux gate")
audit, _ = json_object(staged_audit, "staged owning audit")
_, candidate_bytes = json_object(release_candidate, "release candidate")
evidence = audit.get("evidence") if isinstance(audit.get("evidence"), dict) else {}
trace_sha256 = hashlib.sha256(trace_bytes).hexdigest()
if audit.get("status") != "pass" or evidence.get("release_candidate_binding_status") != "pass":
    raise SystemExit("The staged owning audit and candidate binding must both pass before promotion.")
if digest(evidence.get("trace_sha256")) != trace_sha256 \
    or digest(evidence.get("trace_sha256_after_audit")) != trace_sha256:
    raise SystemExit("The staged trace bytes no longer match the passing owning audit.")
if digest(evidence.get("linux_gate_sha256")) != hashlib.sha256(gate_bytes).hexdigest():
    raise SystemExit("The staged Linux gate bytes no longer match the passing owning audit.")
if digest(evidence.get("release_candidate_sha256")) != hashlib.sha256(candidate_bytes).hexdigest():
    raise SystemExit("The release candidate bytes no longer match the passing owning audit.")

mouse_first = gate.get("mouse_first_journey") if isinstance(gate.get("mouse_first_journey"), dict) else {}
primary = mouse_first.get("primary") if isinstance(mouse_first.get("primary"), dict) else {}
embedded_receipt = primary.get("receipt") if isinstance(primary.get("receipt"), dict) else {}
screenshot_root_text = str(
    primary.get("screenshot_dir")
    or embedded_receipt.get("screenshotDirectory")
    or evidence.get("screenshot_dir")
    or ""
).strip()
if not screenshot_root_text:
    raise SystemExit("The staged Linux gate does not bind a screenshot directory.")
screenshot_root = Path(screenshot_root_text)

workflows = trace.get("workflows")
if not isinstance(workflows, list) or len(workflows) != 5:
    raise SystemExit("The staged trace must contain exactly five workflow rows.")
frames: list[tuple[PurePosixPath, bytes]] = []
seen_paths: set[str] = set()
seen_hashes: set[str] = set()
for workflow in workflows:
    if not isinstance(workflow, dict):
        raise SystemExit("Every staged workflow row must be a JSON object.")
    screenshots = workflow.get("screenshots")
    declared_hashes = workflow.get("screenshot_sha256")
    if not isinstance(screenshots, list) or len(screenshots) != 2 or not isinstance(declared_hashes, dict):
        raise SystemExit("Every staged workflow must bind exactly two screenshot hashes.")
    for raw_path in screenshots:
        if not isinstance(raw_path, str):
            raise SystemExit("Screenshot paths must be strings.")
        relative = PurePosixPath(raw_path)
        if relative.is_absolute() or ".." in relative.parts or str(relative) != raw_path:
            raise SystemExit(f"Unsafe staged screenshot path: {raw_path}")
        if raw_path in seen_paths:
            raise SystemExit(f"Duplicate staged screenshot path: {raw_path}")
        seen_paths.add(raw_path)
        frame_path = screenshot_root.joinpath(*relative.parts)
        frame_bytes = stable_bytes(frame_path, "staged screenshot", 32 * 1024 * 1024)
        frame_digest = hashlib.sha256(frame_bytes).hexdigest()
        if digest(declared_hashes.get(raw_path)) != frame_digest:
            raise SystemExit(f"Staged screenshot digest mismatch: {raw_path}")
        if frame_digest in seen_hashes:
            raise SystemExit(f"Duplicate staged screenshot bytes: {raw_path}")
        seen_hashes.add(frame_digest)
        frames.append((relative, frame_bytes))
if len(frames) != 10:
    raise SystemExit("Exactly ten staged screenshot frames are required.")

canonical_screenshot_base.mkdir(parents=True, exist_ok=True)
destination = canonical_screenshot_base / trace_sha256
if not destination.exists():
    temporary_destination = canonical_screenshot_base / f".{trace_sha256}.{uuid.uuid4().hex}.tmp"
    temporary_destination.mkdir()
    try:
        for relative, frame_bytes in frames:
            frame_destination = temporary_destination.joinpath(*relative.parts)
            frame_destination.parent.mkdir(parents=True, exist_ok=True)
            frame_destination.write_bytes(frame_bytes)
        os.replace(temporary_destination, destination)
    finally:
        if temporary_destination.exists():
            for child in sorted(temporary_destination.rglob("*"), reverse=True):
                if child.is_file():
                    child.unlink()
                elif child.is_dir():
                    child.rmdir()
            temporary_destination.rmdir()
else:
    for relative, frame_bytes in frames:
        existing = stable_bytes(destination.joinpath(*relative.parts), "promoted screenshot", 32 * 1024 * 1024)
        if existing != frame_bytes:
            raise SystemExit("An immutable screenshot bundle already exists with different bytes.")

canonical_trace.parent.mkdir(parents=True, exist_ok=True)
temporary_trace = ""
try:
    with tempfile.NamedTemporaryFile(
        "wb",
        dir=canonical_trace.parent,
        prefix=f".{canonical_trace.name}.",
        suffix=".tmp",
        delete=False,
    ) as handle:
        handle.write(trace_bytes)
        handle.flush()
        os.fsync(handle.fileno())
        temporary_trace = handle.name
    os.replace(temporary_trace, canonical_trace)
finally:
    if temporary_trace and os.path.exists(temporary_trace):
        os.unlink(temporary_trace)

print(destination)
PY
)"

python3 "$repo_root/scripts/ai/milestones/user_journey_evidence_bundle.py" create \
  --published-root "$published_root" \
  --trace "$staged_trace" \
  --linux-gate "$staged_gate" \
  --flagship-gate "$staged_flagship_gate" \
  --staged-audit "$staged_audit" \
  --release-candidate "$release_candidate" >/dev/null

CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH="$canonical_audit" \
CHUMMER_USER_JOURNEY_TESTER_BUNDLE_POINTER_PATH="$bundle_pointer" \
  bash "$repo_root/scripts/ai/milestones/user-journey-tester-audit.sh"

printf 'canonical_trace=%s\ncanonical_screenshot_dir=%s\ncanonical_audit=%s\nbundle_pointer=%s\n' \
  "$canonical_trace" "$canonical_screenshot_dir" "$canonical_audit" "$bundle_pointer"
