#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
workspace_root="$(cd "$repo_root/.." && pwd)"

tester_shard_id="${1:-}"
fix_shard_id="${2:-}"
capture_root_input="${3:-}"
release_candidate_input="${4:-$workspace_root/chummer-hub-registry/.codex-studio/published/RELEASE_CHANNEL.generated.json}"

if [[ -z "$tester_shard_id" || -z "$fix_shard_id" ]]; then
  echo "Usage: $0 <tester-shard-id> <distinct-fix-shard-id> [absolute-capture-root] [release-candidate.json]" >&2
  exit 2
fi
if [[ "$tester_shard_id" == "$fix_shard_id" ]]; then
  echo "Tester and fixer shard IDs must be distinct." >&2
  exit 2
fi

mkdir -p "$repo_root/.state"
if [[ -z "$capture_root_input" ]]; then
  capture_root="$(mktemp -d "$repo_root/.state/user-journey-capture.XXXXXX")"
else
  case "$capture_root_input" in
    /*) capture_root="$capture_root_input" ;;
    *)
      echo "The capture root must be an absolute caller-staged path." >&2
      exit 2
      ;;
  esac
  mkdir -p "$capture_root"
fi

release_candidate="$release_candidate_input"
if [[ ! -f "$release_candidate" || -L "$release_candidate" ]]; then
  echo "The release candidate manifest must be a regular non-symlink file: $release_candidate" >&2
  exit 2
fi

trace_path="$capture_root/USER_JOURNEY_TESTER_TRACE.generated.json"
linux_gate_path="$capture_root/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
audit_path="$capture_root/USER_JOURNEY_TESTER_AUDIT.generated.json"
gate_output_root="$capture_root/linux-desktop-exit-gate"
flagship_gate_path="$capture_root/UI_FLAGSHIP_RELEASE_GATE.generated.json"
flagship_screenshot_dir="$capture_root/ui-flagship-release-gate-screenshots"
flagship_gate_source="${CHUMMER_USER_JOURNEY_FLAGSHIP_GATE_SOURCE_PATH:-$repo_root/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
flagship_screenshot_source="${CHUMMER_USER_JOURNEY_FLAGSHIP_SCREENSHOT_SOURCE_DIR:-$repo_root/.codex-studio/published/ui-flagship-release-gate-screenshots}"

python3 - "$flagship_gate_source" "$flagship_screenshot_source" "$flagship_gate_path" "$flagship_screenshot_dir" <<'PY'
from __future__ import annotations

import os
import shutil
import stat
import sys
from pathlib import Path

source_receipt = Path(sys.argv[1])
source_screenshots = Path(sys.argv[2])
staged_receipt = Path(sys.argv[3])
staged_screenshots = Path(sys.argv[4])


def require_regular(path: Path, label: str) -> None:
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise SystemExit(f"{label} is unavailable: {path} ({exc})") from exc
    if not stat.S_ISREG(metadata.st_mode):
        raise SystemExit(f"{label} must be a regular non-symlink file: {path}")


def require_directory(path: Path, label: str) -> None:
    try:
        metadata = path.lstat()
    except OSError as exc:
        raise SystemExit(f"{label} is unavailable: {path} ({exc})") from exc
    if not stat.S_ISDIR(metadata.st_mode):
        raise SystemExit(f"{label} must be a directory, not a symlink: {path}")


require_regular(source_receipt, "flagship gate receipt")
require_directory(source_screenshots, "flagship screenshot source")
staged_receipt.parent.mkdir(parents=True, exist_ok=True)
if staged_receipt.exists() or staged_receipt.is_symlink():
    raise SystemExit(f"staged flagship gate receipt already exists: {staged_receipt}")
if staged_screenshots.exists() or staged_screenshots.is_symlink():
    raise SystemExit(f"staged flagship screenshot directory already exists: {staged_screenshots}")

temporary_screenshots = staged_screenshots.with_name(
    f".{staged_screenshots.name}.stage-{os.getpid()}"
)
temporary_screenshots.mkdir()
try:
    for source in sorted(source_screenshots.iterdir()):
        require_regular(source, "flagship screenshot-pack entry")
        destination = temporary_screenshots / source.name
        shutil.copy2(source, destination, follow_symlinks=False)
    shutil.copy2(source_receipt, staged_receipt, follow_symlinks=False)
    os.replace(temporary_screenshots, staged_screenshots)
finally:
    if temporary_screenshots.exists():
        shutil.rmtree(temporary_screenshots)
PY

CHUMMER_LINUX_DESKTOP_EXIT_GATE_RELEASE_CHANNEL_PATH="$release_candidate" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER:-1}" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY:-1}" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_OUTPUT_ROOT="$gate_output_root" \
CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH="$linux_gate_path" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_RECEIPT_PATH="$flagship_gate_path" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_SCREENSHOT_DIR="$flagship_screenshot_dir" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_TRACE_OUTPUT="$trace_path" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_TESTER_SHARD_ID="$tester_shard_id" \
CHUMMER_LINUX_DESKTOP_EXIT_GATE_USER_JOURNEY_FIX_SHARD_ID="$fix_shard_id" \
  bash "$repo_root/scripts/materialize-linux-desktop-exit-gate.sh"

if [[ ! -f "$trace_path" ]]; then
  echo "The promoted candidate did not emit a user-journey tester trace. Build and promote a candidate containing the live producer before retrying." >&2
  exit 1
fi

CHUMMER_USER_JOURNEY_TESTER_AUDIT_PATH="$audit_path" \
CHUMMER_USER_JOURNEY_TESTER_TRACE_PATH="$trace_path" \
CHUMMER_USER_JOURNEY_TESTER_LINUX_GATE_PATH="$linux_gate_path" \
CHUMMER_USER_JOURNEY_TESTER_FLAGSHIP_GATE_PATH="$flagship_gate_path" \
CHUMMER_USER_JOURNEY_TESTER_RELEASE_CANDIDATE_PATH="$release_candidate" \
CHUMMER_USER_JOURNEY_TESTER_EVIDENCE_ROOT="$capture_root" \
  bash "$repo_root/scripts/ai/milestones/user-journey-tester-audit.sh"

printf 'capture_root=%s\ntrace_path=%s\nlinux_gate_path=%s\naudit_path=%s\n' \
  "$capture_root" "$trace_path" "$linux_gate_path" "$audit_path"
