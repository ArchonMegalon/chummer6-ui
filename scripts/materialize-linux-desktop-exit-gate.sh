#!/usr/bin/env bash
set -euo pipefail
set -o errtrace

SCRIPT_DIR="$(cd -L "$(dirname "${BASH_SOURCE[0]}")" && pwd -L)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR/.." && pwd -P)"
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-/docker/chummercomplete/chummer6-ui}"
REPO_ROOT="$REPO_ROOT_PHYSICAL"
if [[ -n "$REPO_ROOT_ALIAS_CANDIDATE" && -d "$REPO_ROOT_ALIAS_CANDIDATE" ]]; then
  ALIAS_PHYSICAL="$(cd "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -P)"
  if [[ "$ALIAS_PHYSICAL" == "$REPO_ROOT_PHYSICAL" ]]; then
    REPO_ROOT="$(cd -L "$REPO_ROOT_ALIAS_CANDIDATE" && pwd -L)"
  fi
fi
WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd -P)"
HUB_REGISTRY_ROOT="${CHUMMER_HUB_REGISTRY_ROOT:-$("$REPO_ROOT/scripts/resolve-hub-registry-root.sh" 2>/dev/null || true)}"
CANONICAL_RELEASE_CHANNEL_PATH="${HUB_REGISTRY_ROOT:+$HUB_REGISTRY_ROOT/.codex-studio/published/RELEASE_CHANNEL.generated.json}"
DEFAULT_RELEASE_CHANNEL_PATH="$REPO_ROOT/Docker/Downloads/RELEASE_CHANNEL.generated.json"
if [[ -n "$CANONICAL_RELEASE_CHANNEL_PATH" && -f "$CANONICAL_RELEASE_CHANNEL_PATH" ]]; then
  RELEASE_CHANNEL_PATH_DEFAULT="$CANONICAL_RELEASE_CHANNEL_PATH"
else
  RELEASE_CHANNEL_PATH_DEFAULT="$DEFAULT_RELEASE_CHANNEL_PATH"
fi

RELEASE_CHANNEL_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RELEASE_CHANNEL_PATH:-$RELEASE_CHANNEL_PATH_DEFAULT}"
APP_KEY_OVERRIDE="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_APP_KEY:-}"
RID_OVERRIDE="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RID:-}"
if [[ -z "$APP_KEY_OVERRIDE" || -z "$RID_OVERRIDE" ]]; then
  mapfile -t RELEASE_PROMOTED_TUPLE < <(python3 - "$RELEASE_CHANNEL_PATH" "$APP_KEY_OVERRIDE" "$RID_OVERRIDE" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
app_key_override = sys.argv[2].strip().lower()
rid_override = sys.argv[3].strip().lower()

def normalize(value: object) -> str:
    return str(value or "").strip().lower()

if not release_channel_path.is_file():
    raise SystemExit(0)

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = [
    item for item in (payload.get("artifacts") or [])
    if isinstance(item, dict)
    and normalize(item.get("platform")) == "linux"
    and normalize(item.get("kind")) == "installer"
    and normalize(item.get("head"))
    and normalize(item.get("rid"))
]

if app_key_override:
    artifacts = [item for item in artifacts if normalize(item.get("head")) == app_key_override]
if rid_override:
    artifacts = [item for item in artifacts if normalize(item.get("rid")) == rid_override]
if not artifacts:
    raise SystemExit(0)

preferred_order = ["linux-x64", "linux-arm64"]
ranked = sorted(
    artifacts,
    key=lambda artifact: (
        preferred_order.index(normalize(artifact.get("rid"))) if normalize(artifact.get("rid")) in preferred_order else len(preferred_order),
        0 if normalize(artifact.get("head")) == "avalonia" else 1,
        normalize(artifact.get("head")),
        normalize(artifact.get("rid")),
    ),
)
chosen = ranked[0]
print(normalize(chosen.get("head")))
print(normalize(chosen.get("rid")))
PY
)
fi

APP_KEY="${APP_KEY_OVERRIDE:-${RELEASE_PROMOTED_TUPLE[0]:-avalonia}}"
RID="${RID_OVERRIDE:-${RELEASE_PROMOTED_TUPLE[1]:-linux-x64}}"

case "$APP_KEY" in
  avalonia)
    DEFAULT_PROJECT_PATH="Chummer.Avalonia/Chummer.Avalonia.csproj"
    DEFAULT_LAUNCH_TARGET="Chummer.Avalonia"
    DEFAULT_PROOF_PATH="$REPO_ROOT/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
    ;;
  blazor-desktop)
    DEFAULT_PROJECT_PATH="Chummer.Blazor.Desktop/Chummer.Blazor.Desktop.csproj"
    DEFAULT_LAUNCH_TARGET="Chummer.Blazor.Desktop"
    DEFAULT_PROOF_PATH="$REPO_ROOT/.codex-studio/published/UI_LINUX_BLAZOR_DESKTOP_EXIT_GATE.generated.json"
    ;;
  *)
    echo "Unsupported linux desktop exit gate app key: $APP_KEY" >&2
    exit 1
    ;;
esac

PROJECT_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROJECT_PATH:-$DEFAULT_PROJECT_PATH}"
TEST_PROJECT_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_TEST_PROJECT_PATH:-Chummer.Desktop.Runtime.Tests/Chummer.Desktop.Runtime.Tests.csproj}"
RID="${RID:-linux-x64}"
LAUNCH_TARGET="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_LAUNCH_TARGET:-$DEFAULT_LAUNCH_TARGET}"
RELEASE_CHANNEL_ID_DEFAULT="$(
  python3 - "$RELEASE_CHANNEL_PATH" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
if not release_channel_path.is_file():
    raise SystemExit(0)

try:
    payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

channel_id = str(payload.get("channelId") or payload.get("channel") or "").strip().lower()
if channel_id:
    print(channel_id)
PY
)"
RELEASE_CHANNEL_VERSION_DEFAULT="$(
  python3 - "$RELEASE_CHANNEL_PATH" <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

release_channel_path = Path(sys.argv[1])
if not release_channel_path.is_file():
    raise SystemExit(0)

try:
    payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

version = str(payload.get("version") or "").strip()
if version:
    print(version)
PY
)"
VERSION="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_VERSION:-${RELEASE_CHANNEL_VERSION_DEFAULT:-local-hard-gate}}"
CHANNEL="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_CHANNEL:-${RELEASE_CHANNEL_ID_DEFAULT:-local-hard-gate}}"
FRAMEWORK="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FRAMEWORK:-net10.0}"
READY_CHECKPOINT="pre_ui_event_loop"
OUTPUT_BASE_ROOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_OUTPUT_ROOT:-$REPO_ROOT/.codex-studio/out/linux-desktop-exit-gate}"
RUN_RETENTION_COUNT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RUN_RETENTION_COUNT:-40}"
PROOF_PATH="${CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH:-$DEFAULT_PROOF_PATH}"
BUILD_LOCK_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_BUILD_LOCK_PATH:-$WORKSPACE_ROOT/.linux-desktop-exit-gate.build.lock}"
LOCAL_DESKTOP_FILES_ROOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT:-$REPO_ROOT/Docker/Downloads/files}"
USE_PROMOTED_INSTALLER="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER:-1}"
FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RUN_FLAGSHIP_UI_GATE:-1}"
FLAGSHIP_UI_GATE_SCRIPT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_SCRIPT:-$REPO_ROOT/scripts/ai/milestones/b14-flagship-ui-release-gate.sh}"
FLAGSHIP_UI_GATE_RECEIPT_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_RECEIPT_PATH:-$REPO_ROOT/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
FLAGSHIP_UI_GATE_SCREENSHOT_DIR="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_SCREENSHOT_DIR:-$REPO_ROOT/.codex-studio/published/ui-flagship-release-gate-screenshots}"

mkdir -p "$OUTPUT_BASE_ROOT"
RUN_ROOT="$(mktemp -d "$OUTPUT_BASE_ROOT/run.XXXXXX")"
LATEST_LINK="$OUTPUT_BASE_ROOT/latest"
PUBLISH_LOCK_PATH="$OUTPUT_BASE_ROOT/publish.lock"
RUN_PROOF_PATH="$RUN_ROOT/$(basename "$PROOF_PATH")"
FAILURE_REASONS_PATH="$RUN_ROOT/failure-reasons.json"
GIT_START_PATH="$RUN_ROOT/git-start.json"
GIT_FINISH_PATH="$RUN_ROOT/git-finish.json"
SOURCE_SNAPSHOT_MANIFEST_PATH="$RUN_ROOT/source-snapshot.json"
SOURCE_SNAPSHOT_ENTRIES_PATH="$RUN_ROOT/source-snapshot.entries.txt"

PUBLISH_DIR="$RUN_ROOT/publish/$APP_KEY-$RID"
DIST_DIR="$RUN_ROOT/dist"
TEST_RESULTS_DIR="$RUN_ROOT/test-results"
SMOKE_ARCHIVE_DIR="$RUN_ROOT/startup-smoke-archive"
SMOKE_INSTALLER_DIR="$RUN_ROOT/startup-smoke-installer"
SOURCE_SNAPSHOT_ROOT=""

ARCHIVE_PATH="$DIST_DIR/chummer-$APP_KEY-$RID.tar.gz"
INSTALLER_PATH="$DIST_DIR/chummer-$APP_KEY-$RID-installer.deb"
TEST_TRX_PATH="$TEST_RESULTS_DIR/desktop-runtime-tests.trx"
ARCHIVE_RECEIPT_PATH="$SMOKE_ARCHIVE_DIR/startup-smoke-$APP_KEY-$RID.receipt.json"
INSTALLER_RECEIPT_PATH="$SMOKE_INSTALLER_DIR/startup-smoke-$APP_KEY-$RID.receipt.json"
BUILD_LOCK_FD=""
BUILD_LOCK_DIR=""

CURRENT_STAGE="init"
GIT_IDENTITY_NOTE=""
INSTALLER_SMOKE_ARTIFACT_PATH=""
PROMOTED_INSTALLER_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_INSTALLER_PATH:-}"

resolve_promoted_installer_path() {
  python3 - "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" <<'PY'
import json
import pathlib
import sys

release_channel_path = pathlib.Path(sys.argv[1])
local_files_root = pathlib.Path(sys.argv[2])
head = str(sys.argv[3]).strip().lower()
rid = str(sys.argv[4]).strip().lower()

if not release_channel_path.is_file():
    raise SystemExit(1)

try:
    payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

artifacts = payload.get("artifacts") if isinstance(payload, dict) else []
if not isinstance(artifacts, list):
    raise SystemExit(1)

for item in artifacts:
    if not isinstance(item, dict):
        continue
    platform = str(item.get("platform") or "").strip().lower()
    kind = str(item.get("kind") or "").strip().lower()
    item_head = str(item.get("head") or "").strip().lower()
    item_rid = str(item.get("rid") or "").strip().lower()
    file_name = str(item.get("fileName") or "").strip()
    if platform != "linux" or kind != "installer":
        continue
    if item_head != head or item_rid != rid:
        continue
    if not file_name:
        continue
    candidate = local_files_root / file_name
    if candidate.is_file():
        print(str(candidate))
        raise SystemExit(0)
    raise SystemExit(1)

raise SystemExit(1)
PY
}

resolve_promoted_startup_smoke_receipt_path() {
  python3 - "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" <<'PY'
import pathlib
import sys

release_channel_path = pathlib.Path(sys.argv[1])
local_files_root = pathlib.Path(sys.argv[2])
head = str(sys.argv[3]).strip().lower()
rid = str(sys.argv[4]).strip().lower()

receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
candidates = [
    local_files_root.parent / "startup-smoke" / receipt_name,
    release_channel_path.parent / "startup-smoke" / receipt_name,
    release_channel_path.parent.parent / "startup-smoke" / receipt_name,
]
for candidate in candidates:
    if candidate.is_file():
        print(str(candidate))
        raise SystemExit(0)
raise SystemExit(1)
PY
}

capture_git_metadata() {
  local output_path="$1"

  python3 - "$output_path" "$REPO_ROOT" "$OUTPUT_BASE_ROOT" "$PROOF_PATH" <<'PY'
import hashlib
import json
import os
import pathlib
import stat
import subprocess
import sys

output_path = pathlib.Path(sys.argv[1])
repo_root = pathlib.Path(sys.argv[2]).resolve()
output_base_root = pathlib.Path(sys.argv[3]).resolve()
canonical_proof_path = pathlib.Path(sys.argv[4]).resolve()

payload = {
    "repo_root": str(repo_root),
    "available": False,
    "head": "",
    "tracked_diff_sha256": "",
    "tracked_diff_line_count": 0,
}

GATE_INPUT_MARKERS = (
    "Chummer.Avalonia/",
    "Chummer.Blazor/",
    "Chummer.Blazor.Desktop/",
    "Chummer/chummer.ico",
    "Chummer/chummer6-icon-preview.png",
    "Chummer/changelog.txt",
    "Chummer.Desktop.Assets/",
    "Chummer.Desktop.Runtime/",
    "Chummer.Desktop.Runtime.Tests/",
    "Chummer.Tests/",
    "Chummer.Presentation/",
    "scripts/ai/",
    "scripts/build-desktop-installer.sh",
    "scripts/run-desktop-startup-smoke.sh",
    "scripts/materialize-linux-desktop-exit-gate.sh",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "NuGet.Config",
    "global.json",
)

def normalize_markers():
    markers = []
    for candidate in (output_base_root, canonical_proof_path):
        try:
            relative = candidate.resolve().relative_to(repo_root)
        except Exception:
            continue
        marker = relative.as_posix().rstrip("/")
        if marker:
            markers.append(marker)
    return markers


def is_excluded(relative_path: str, markers):
    for marker in markers:
        if relative_path == marker or relative_path.startswith(f"{marker}/"):
            return True
    return False


def is_gate_input(relative_path: str) -> bool:
    for marker in GATE_INPUT_MARKERS:
        if marker.endswith("/"):
            if relative_path.startswith(marker):
                return True
        elif relative_path == marker:
            return True
    return False


def iter_repo_entries(markers):
    try:
        cache_listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in cache_listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            if not is_gate_input(relative):
                continue
            seen.add(relative)
            entries.append(relative)
        try:
            untracked_listing = subprocess.run(
                ["git", "-C", str(repo_root), "ls-files", "-z", "--others", "--exclude-standard"],
                check=True,
                capture_output=True,
            ).stdout.decode("utf-8", errors="surrogateescape")
            for raw_item in untracked_listing.split("\0"):
                relative = raw_item.strip()
                if not relative or relative in seen or is_excluded(relative, markers):
                    continue
                if not is_gate_input(relative):
                    continue
                seen.add(relative)
                entries.append(relative)
        except Exception:
            pass
        if entries:
            return sorted(entries)
    except Exception:
        entries = []
        for path in sorted(repo_root.rglob("*")):
            if path == repo_root / ".git":
                continue
            try:
                relative = path.relative_to(repo_root).as_posix()
            except Exception:
                continue
            if relative == ".git" or relative.startswith(".git/") or is_excluded(relative, markers):
                continue
            if path.is_dir():
                continue
            if not is_gate_input(relative):
                continue
            entries.append(relative)
        if entries:
            return entries
    try:
        untracked_listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in untracked_listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            if not is_gate_input(relative):
                continue
            seen.add(relative)
            entries.append(relative)
        return sorted(entries)
    except Exception:
        return []


try:
    head = subprocess.run(
        ["git", "-C", str(repo_root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
except Exception:
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raise SystemExit(0)

digest = hashlib.sha256()
entry_count = 0
for relative in iter_repo_entries(normalize_markers()):
    path = repo_root / relative
    try:
        stat_result = os.lstat(path)
    except FileNotFoundError:
        digest.update(f"missing\0{relative}\0".encode("utf-8"))
        entry_count += 1
        continue
    mode = stat.S_IMODE(stat_result.st_mode)
    if stat.S_ISLNK(stat_result.st_mode):
        digest.update(f"symlink\0{relative}\0{mode:o}\0{os.readlink(path)}\0".encode("utf-8"))
        entry_count += 1
        continue
    if not stat.S_ISREG(stat_result.st_mode):
        continue
    digest.update(f"file\0{relative}\0{mode:o}\0".encode("utf-8"))
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    digest.update(b"\0")
    entry_count += 1

payload.update(
    {
        "available": True,
        "head": head,
        "tracked_diff_sha256": digest.hexdigest(),
        "tracked_diff_line_count": entry_count,
    }
)
output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

materialize_source_snapshot() {
  SOURCE_SNAPSHOT_ROOT="$(mktemp -d "$WORKSPACE_ROOT/.linux-desktop-exit-gate-source.XXXXXX")"

  python3 - "$REPO_ROOT" "$SOURCE_SNAPSHOT_ROOT" "$OUTPUT_BASE_ROOT" "$PROOF_PATH" "$SOURCE_SNAPSHOT_MANIFEST_PATH" "$SOURCE_SNAPSHOT_ENTRIES_PATH" <<'PY'
import hashlib
import json
import os
import pathlib
import shutil
import stat
import subprocess
import sys

repo_root = pathlib.Path(sys.argv[1]).resolve()
snapshot_root = pathlib.Path(sys.argv[2]).resolve()
output_base_root = pathlib.Path(sys.argv[3]).resolve()
canonical_proof_path = pathlib.Path(sys.argv[4]).resolve()
manifest_path = pathlib.Path(sys.argv[5]).resolve()
entries_path = pathlib.Path(sys.argv[6]).resolve()

GATE_INPUT_MARKERS = (
    "Chummer.Avalonia/",
    "Chummer.Blazor/",
    "Chummer.Blazor.Desktop/",
    "Chummer/chummer.ico",
    "Chummer/chummer6-icon-preview.png",
    "Chummer/changelog.txt",
    "Chummer.Desktop.Assets/",
    "Chummer.Desktop.Runtime/",
    "Chummer.Desktop.Runtime.Tests/",
    "Chummer.Tests/",
    "Chummer.Presentation/",
    "scripts/ai/",
    "scripts/build-desktop-installer.sh",
    "scripts/run-desktop-startup-smoke.sh",
    "scripts/materialize-linux-desktop-exit-gate.sh",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "NuGet.Config",
    "global.json",
)

SUPPLEMENTAL_SNAPSHOT_PATHS = (
    "Chummer.Desktop.Assets/",
)


def normalize_markers():
    markers = []
    for candidate in (output_base_root, canonical_proof_path):
        try:
            relative = candidate.resolve().relative_to(repo_root)
        except Exception:
            continue
        marker = relative.as_posix().rstrip("/")
        if marker:
            markers.append(marker)
    return markers


def is_excluded(relative_path: str, markers):
    for marker in markers:
        if relative_path == marker or relative_path.startswith(f"{marker}/"):
            return True
    return False


def is_gate_input(relative_path: str) -> bool:
    for marker in GATE_INPUT_MARKERS:
        if marker.endswith("/"):
            if relative_path.startswith(marker):
                return True
        elif relative_path == marker:
            return True
    return False


def iter_tracked_repo_entries(markers):
    try:
        cache_listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in cache_listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            if not is_gate_input(relative):
                continue
            seen.add(relative)
            entries.append(relative)
        try:
            untracked_listing = subprocess.run(
                ["git", "-C", str(repo_root), "ls-files", "-z", "--others", "--exclude-standard"],
                check=True,
                capture_output=True,
            ).stdout.decode("utf-8", errors="surrogateescape")
            for raw_item in untracked_listing.split("\0"):
                relative = raw_item.strip()
                if not relative or relative in seen or is_excluded(relative, markers):
                    continue
                if not is_gate_input(relative):
                    continue
                seen.add(relative)
                entries.append(relative)
        except Exception:
            pass
        return sorted(entries)
    except Exception:
        entries = []
        for path in sorted(repo_root.rglob("*")):
            if path == repo_root / ".git":
                continue
            try:
                relative = path.relative_to(repo_root).as_posix()
            except Exception:
                continue
            if relative == ".git" or relative.startswith(".git/") or is_excluded(relative, markers):
                continue
            if path.is_dir():
                continue
            if not is_gate_input(relative):
                continue
            entries.append(relative)
        if entries:
            return entries
    try:
        untracked_listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in untracked_listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            seen.add(relative)
            entries.append(relative)
        return sorted(entries)
    except Exception:
        entries = []
        for path in sorted(repo_root.rglob("*")):
            if path == repo_root / ".git":
                continue
            try:
                relative = path.relative_to(repo_root).as_posix()
            except Exception:
                continue
            if relative == ".git" or relative.startswith(".git/") or is_excluded(relative, markers):
                continue
            if path.is_dir():
                continue
            entries.append(relative)
        return entries


tracked_entries = iter_tracked_repo_entries(normalize_markers())
snapshot_root.mkdir(parents=True, exist_ok=True)
digest = hashlib.sha256()
entry_count = 0

for relative in tracked_entries:
    src_path = repo_root / relative
    dest_path = snapshot_root / relative
    try:
        stat_result = os.lstat(src_path)
    except FileNotFoundError:
        digest.update(f"missing\0{relative}\0".encode("utf-8"))
        entry_count += 1
        continue
    mode = stat.S_IMODE(stat_result.st_mode)
    if stat.S_ISLNK(stat_result.st_mode):
        dest_path.parent.mkdir(parents=True, exist_ok=True)
        target = os.readlink(src_path)
        os.symlink(target, dest_path)
        digest.update(f"symlink\0{relative}\0{mode:o}\0{target}\0".encode("utf-8"))
        entry_count += 1
        continue
    if not stat.S_ISREG(stat_result.st_mode):
        continue
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_path, dest_path)
    digest.update(f"file\0{relative}\0{mode:o}\0".encode("utf-8"))
    with src_path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    digest.update(b"\0")
    entry_count += 1

# Preserve buildability for required desktop assets that may be present
# outside tracked git input; do not fold these into tracked fingerprint hash.
for relative in SUPPLEMENTAL_SNAPSHOT_PATHS:
    src_path = repo_root / relative
    if not src_path.exists():
        continue
    dest_path = snapshot_root / relative
    if src_path.is_dir():
        shutil.copytree(src_path, dest_path, dirs_exist_ok=True)
        continue
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src_path, dest_path)

manifest = {
    "mode": "filesystem_copy",
    "repo_root": str(repo_root),
    "snapshot_root": str(snapshot_root),
    "entries_path": str(entries_path),
    "entry_count": entry_count,
    "worktree_sha256": digest.hexdigest(),
}
entries_path.write_text("".join(f"{relative}\n" for relative in tracked_entries), encoding="utf-8")
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
PY
}

refresh_source_snapshot_manifest() {
  python3 - "$SOURCE_SNAPSHOT_MANIFEST_PATH" <<'PY'
import hashlib
import json
import os
import pathlib
import stat
import sys

manifest_path = pathlib.Path(sys.argv[1])
if not manifest_path.is_file():
    raise SystemExit(0)

try:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(0)

snapshot_root = pathlib.Path(str(payload.get("snapshot_root") or "")).resolve()
entries_path = pathlib.Path(str(payload.get("entries_path") or "")).resolve()
finish_digest = ""
finish_entry_count = 0
ignored_extra_entry_count = 0
ignored_extra_entries = []


def is_ignorable_generated(relative: str) -> bool:
    if relative == "Chummer.Desktop.Assets" or relative.startswith("Chummer.Desktop.Assets/"):
        return True
    parts = tuple(part for part in pathlib.Path(relative).parts if part)
    return any(part in {"bin", "obj", "TestResults"} for part in parts)

if snapshot_root.is_dir():
    digest = hashlib.sha256()
    expected_entries = []
    expected_set = set()
    if entries_path.is_file():
        for raw_line in entries_path.read_text(encoding="utf-8").splitlines():
            relative = raw_line.strip()
            if not relative or relative in expected_set:
                continue
            expected_set.add(relative)
            expected_entries.append(relative)
    for relative in expected_entries:
        path = snapshot_root / relative
        try:
            stat_result = os.lstat(path)
        except FileNotFoundError:
            digest.update(f"missing\0{relative}\0".encode("utf-8"))
            finish_entry_count += 1
            continue
        mode = stat.S_IMODE(stat_result.st_mode)
        if stat.S_ISLNK(stat_result.st_mode):
            digest.update(f"symlink\0{relative}\0{mode:o}\0{os.readlink(path)}\0".encode("utf-8"))
            finish_entry_count += 1
            continue
        if not stat.S_ISREG(stat_result.st_mode):
            continue
        digest.update(f"file\0{relative}\0{mode:o}\0".encode("utf-8"))
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        digest.update(b"\0")
        finish_entry_count += 1
    for path in sorted(snapshot_root.rglob("*")):
        if path.is_dir():
            continue
        relative = path.relative_to(snapshot_root).as_posix()
        if relative in expected_set:
            continue
        ignored_extra_entry_count += 1
        if len(ignored_extra_entries) < 50:
            ignored_extra_entries.append(relative)
    finish_digest = digest.hexdigest()

payload["finish_worktree_sha256"] = finish_digest
payload["finish_entry_count"] = finish_entry_count
payload["ignored_extra_entry_count"] = ignored_extra_entry_count
payload["ignored_extra_entries_sample"] = ignored_extra_entries
payload["identity_stable"] = bool(
    finish_digest
    and str(payload.get("worktree_sha256") or "").strip() == finish_digest
    and int(payload.get("entry_count") or 0) == finish_entry_count
)
manifest_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

assert_source_snapshot_identity_stable() {
  python3 - "$SOURCE_SNAPSHOT_MANIFEST_PATH" <<'PY'
import json
import pathlib
import sys

manifest_path = pathlib.Path(sys.argv[1])
if not manifest_path.is_file():
    raise SystemExit(1)
try:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)
if not payload.get("identity_stable"):
    raise SystemExit(1)
PY
}

assert_repo_git_identity_stable() {
  python3 - "$GIT_START_PATH" "$GIT_FINISH_PATH" <<'PY'
import json
import pathlib
import sys

start_path = pathlib.Path(sys.argv[1])
finish_path = pathlib.Path(sys.argv[2])


def load(path: pathlib.Path):
    if not path.is_file():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}


start_payload = load(start_path)
finish_payload = load(finish_path)
if not start_payload.get("available") or not finish_payload.get("available"):
    raise SystemExit(0)
if (
    str(start_payload.get("head") or "").strip() != str(finish_payload.get("head") or "").strip()
    or str(start_payload.get("tracked_diff_sha256") or "").strip()
    != str(finish_payload.get("tracked_diff_sha256") or "").strip()
):
    raise SystemExit(1)
PY
}

write_proof() {
  local proof_status="$1"
  local reason="$2"
  local exit_code="${3:-0}"

  mkdir -p "$(dirname "$PROOF_PATH")"
  capture_git_metadata "$GIT_FINISH_PATH"
  refresh_source_snapshot_manifest

  python3 - "$RUN_PROOF_PATH" "$REPO_ROOT" "$OUTPUT_BASE_ROOT" "$PROOF_PATH" "$proof_status" "$reason" "$CURRENT_STAGE" "$exit_code" \
    "$APP_KEY" "$PROJECT_PATH" "$TEST_PROJECT_PATH" "$RID" "$LAUNCH_TARGET" "$VERSION" "$CHANNEL" "$FRAMEWORK" \
    "$READY_CHECKPOINT" "$RUN_ROOT" "$PUBLISH_DIR" "$DIST_DIR" "$ARCHIVE_PATH" "$INSTALLER_PATH" "$ARCHIVE_RECEIPT_PATH" "$INSTALLER_RECEIPT_PATH" \
    "$TEST_RESULTS_DIR" "$TEST_TRX_PATH" "$GIT_START_PATH" "$GIT_FINISH_PATH" "$SOURCE_SNAPSHOT_MANIFEST_PATH" \
    "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$USE_PROMOTED_INSTALLER" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$PROMOTED_INSTALLER_PATH" \
    "$FAILURE_REASONS_PATH" "$FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED" "$FLAGSHIP_UI_GATE_RECEIPT_PATH" "$FLAGSHIP_UI_GATE_SCREENSHOT_DIR" "$FLAGSHIP_UI_GATE_SCRIPT" <<'PY'
import datetime as dt
import hashlib
import json
import os
import pathlib
import platform
import shutil
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

(
    proof_path,
    repo_root,
    output_base_root,
    canonical_proof_path,
    proof_status,
    reason,
    stage,
    exit_code,
    app_key,
    project_path,
    test_project_path,
    rid,
    launch_target,
    version,
    channel,
    framework,
    ready_checkpoint,
    run_root,
    publish_dir,
    dist_dir,
    archive_path,
    installer_path,
    archive_receipt_path,
    installer_receipt_path,
    test_results_dir,
    test_trx_path,
    git_start_path,
    git_finish_path,
    source_snapshot_manifest_path,
    release_channel_path,
    local_desktop_files_root,
    use_promoted_installer,
    installer_smoke_artifact_path,
    promoted_installer_path,
    failure_reasons_path,
    flagship_ui_screenshot_gate_enabled,
    flagship_ui_gate_receipt_path,
    flagship_ui_gate_screenshot_dir,
    flagship_ui_gate_script,
) = sys.argv[1:]


def load_json(path_text: str):
    path = pathlib.Path(path_text)
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return None


def load_failure_reasons(path_text: str) -> list[str]:
    if not path_text:
        return []
    path = pathlib.Path(path_text)
    if not path.is_file():
        return []
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return []
    if isinstance(payload, list):
        source = payload
    elif isinstance(payload, dict):
        source = payload.get("reasons")
    else:
        source = None
    if not isinstance(source, list):
        return []
    reasons = []
    for item in source:
        value = str(item or "").strip()
        if value:
            reasons.append(value)
    return reasons


def dedupe_preserve_order(values: list[str]) -> list[str]:
    seen = set()
    ordered = []
    for value in values:
        if value in seen:
            continue
        seen.add(value)
        ordered.append(value)
    return ordered


def normalize_token(value: object) -> str:
    return str(value or "").strip().lower()


def sha256_file(path_text: str):
    path = pathlib.Path(path_text)
    if not path.is_file():
        return ""
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def path_metadata(path_text: str):
    path = pathlib.Path(path_text)
    return {
        "sha256": sha256_file(path_text),
        "bytes": path.stat().st_size if path.is_file() else 0,
        "executable": bool(path.is_file() and os.access(path, os.X_OK)),
    }


def parse_trx_summary(path_text: str):
    summary = {"total": 0, "passed": 0, "failed": 0, "skipped": 0}
    path = pathlib.Path(path_text)
    if not path.is_file():
        return summary
    try:
        root = ET.fromstring(path.read_text(encoding="utf-8"))
    except ET.ParseError:
        return summary
    counters = None
    for element in root.iter():
        if element.tag.endswith("Counters"):
            counters = element
            break
    if counters is None:
        return summary
    for key in summary:
        raw = counters.attrib.get(key)
        try:
            summary[key] = int(raw) if raw is not None else 0
        except ValueError:
            summary[key] = 0
    return summary


def read_git_metadata(repo_root_text: str, output_base_root_text: str, canonical_proof_path_text: str):
    payload = {
        "repo_root": repo_root_text,
        "available": False,
        "head": "",
        "tracked_diff_sha256": "",
        "tracked_diff_line_count": 0,
    }
    gate_input_markers = (
        "Chummer.Avalonia/",
        "Chummer.Desktop.Assets/",
        "Chummer.Desktop.Runtime/",
        "Chummer.Desktop.Runtime.Tests/",
        "Chummer.Tests/",
        "Chummer.Presentation/",
        "scripts/ai/",
        "scripts/build-desktop-installer.sh",
        "scripts/run-desktop-startup-smoke.sh",
        "scripts/materialize-linux-desktop-exit-gate.sh",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
        "NuGet.Config",
        "global.json",
    )
    try:
        head = subprocess.run(
            ["git", "-C", repo_root_text, "rev-parse", "HEAD"],
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()
    except Exception:
        return payload
    repo_root_path = pathlib.Path(repo_root_text).resolve()
    exclude_markers = []
    for candidate_text in (output_base_root_text, canonical_proof_path_text):
        candidate = pathlib.Path(candidate_text)
        try:
            relative = candidate.resolve().relative_to(repo_root_path)
        except Exception:
            continue
        marker = relative.as_posix().rstrip("/")
        if marker:
            exclude_markers.append(marker)
    def list_gate_inputs() -> list[str]:
        cache_listing = subprocess.run(
            ["git", "-C", repo_root_text, "ls-files", "-z", "--cached"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in cache_listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen:
                continue
            if any(relative == marker or relative.startswith(f"{marker}/") for marker in exclude_markers):
                continue
            if not any(
                relative.startswith(marker) if marker.endswith("/") else relative == marker
                for marker in gate_input_markers
            ):
                continue
            seen.add(relative)
            entries.append(relative)
        try:
            untracked_listing = subprocess.run(
                ["git", "-C", repo_root_text, "ls-files", "-z", "--others", "--exclude-standard"],
                check=True,
                capture_output=True,
            ).stdout.decode("utf-8", errors="surrogateescape")
            for raw_item in untracked_listing.split("\0"):
                relative = raw_item.strip()
                if not relative or relative in seen:
                    continue
                if any(relative == marker or relative.startswith(f"{marker}/") for marker in exclude_markers):
                    continue
                if not any(
                    relative.startswith(marker) if marker.endswith("/") else relative == marker
                    for marker in gate_input_markers
                ):
                    continue
                seen.add(relative)
                entries.append(relative)
        except Exception:
            pass
        entries.sort()
        return entries

    try:
        entries = list_gate_inputs()
        if not entries:
            raise ValueError("no gate-scoped entries")
    except Exception:
        return payload
    digest = hashlib.sha256()
    entry_count = 0
    for relative in entries:
        path = repo_root_path / relative
        try:
            stat_result = os.lstat(path)
        except FileNotFoundError:
            digest.update(f"missing\0{relative}\0".encode("utf-8"))
            entry_count += 1
            continue
        mode = stat.S_IMODE(stat_result.st_mode)
        if stat.S_ISLNK(stat_result.st_mode):
            digest.update(f"symlink\0{relative}\0{mode:o}\0{os.readlink(path)}\0".encode("utf-8"))
            entry_count += 1
            continue
        if not stat.S_ISREG(stat_result.st_mode):
            continue
        digest.update(f"file\0{relative}\0{mode:o}\0".encode("utf-8"))
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        digest.update(b"\0")
        entry_count += 1
    payload.update(
        {
            "available": True,
            "head": head,
            "tracked_diff_sha256": digest.hexdigest(),
            "tracked_diff_line_count": entry_count,
        }
    )
    return payload


archive_receipt = load_json(archive_receipt_path)
installer_receipt = load_json(installer_receipt_path)
test_summary = parse_trx_summary(test_trx_path)
git_start = load_json(git_start_path) or {"available": False}
git_finish = load_json(git_finish_path) or {"available": False}
source_snapshot = load_json(source_snapshot_manifest_path) or {}
current_git = read_git_metadata(repo_root, output_base_root, canonical_proof_path)
identity_stable = (
    bool(git_start.get("available"))
    and bool(git_finish.get("available"))
    and str(git_start.get("head") or "").strip() == str(git_finish.get("head") or "").strip()
    and str(git_start.get("tracked_diff_sha256") or "").strip()
    == str(git_finish.get("tracked_diff_sha256") or "").strip()
)
binary_path = str(pathlib.Path(publish_dir) / launch_target)
binary_metadata = path_metadata(binary_path)
archive_metadata = path_metadata(archive_path)
installer_metadata = path_metadata(installer_path)
normalized_status = str(proof_status or "").strip().lower()
reason_lines: list[str] = []
if normalized_status not in {"pass", "passed", "ready"}:
    reason_lines = [str(reason or "").strip()]
    reason_lines.extend(load_failure_reasons(failure_reasons_path))
    reason_lines = dedupe_preserve_order([line for line in reason_lines if line])
host_operating_system = str(platform.system() or "").strip()
host_operating_system_normalized = host_operating_system.lower()
host_supports_linux_startup_smoke = (
    host_operating_system_normalized == "linux"
    and bool(shutil.which("dpkg"))
    and bool(shutil.which("dpkg-deb"))
)
startup_smoke_receipt_exists = pathlib.Path(installer_receipt_path).is_file()
startup_smoke_external_blocker = (
    "missing_linux_host_capability"
    if (not startup_smoke_receipt_exists and not host_supports_linux_startup_smoke)
    else ""
)
release_channel_payload = load_json(release_channel_path) if release_channel_path else None
if not isinstance(release_channel_payload, dict):
    release_channel_payload = {}
release_channel_channel_id = normalize_token(
    release_channel_payload.get("channelId") or release_channel_payload.get("channel")
)
release_channel_version = str(
    release_channel_payload.get("version")
    or release_channel_payload.get("releaseVersion")
    or ""
).strip()
release_channel_linux_artifact = {}
for artifact in (release_channel_payload.get("artifacts") or []):
    if not isinstance(artifact, dict):
        continue
    if (
        normalize_token(artifact.get("platform")) == "linux"
        and normalize_token(artifact.get("kind")) == "installer"
        and normalize_token(artifact.get("head")) == normalize_token(app_key)
        and normalize_token(artifact.get("rid")) == normalize_token(rid)
    ):
        release_channel_linux_artifact = artifact
        break
flagship_ui_gate_receipt = load_json(flagship_ui_gate_receipt_path) or {}
flagship_ui_visual_review = (
    flagship_ui_gate_receipt.get("visualReviewEvidence")
    if isinstance(flagship_ui_gate_receipt, dict)
    else {}
) or {}
flagship_ui_workflow_coverage = (
    flagship_ui_visual_review.get("workflowScreenshotCoverage")
    if isinstance(flagship_ui_visual_review, dict)
    else []
) or []
flagship_ui_screenshot_files = sorted(path.name for path in pathlib.Path(flagship_ui_gate_screenshot_dir).glob("*.png"))
flagship_ui_status = normalize_token(flagship_ui_gate_receipt.get("status")) if isinstance(flagship_ui_gate_receipt, dict) else ""

payload = {
    "contract_name": "chummer6-ui.linux_desktop_exit_gate",
    "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": proof_status,
    "reason": reason,
    "reasons": reason_lines,
    "stage": stage,
    "exit_code": int(exit_code),
    "run_root": run_root,
    "head": {
        "app_key": app_key,
        "project_path": project_path,
        "launch_target": launch_target,
        "platform": "linux",
        "rid": rid,
        "version": version,
        "channel": channel,
        "ready_checkpoint": ready_checkpoint,
    },
    "channelId": release_channel_channel_id,
    "releaseVersion": release_channel_version,
    "checks": {
        "release_channel_id": release_channel_channel_id,
        "release_channel_version": release_channel_version,
        "release_channel_linux_artifact": release_channel_linux_artifact,
        "startup_smoke_receipt_found": startup_smoke_receipt_exists,
        "startup_smoke_receipt_path": installer_receipt_path,
        "startup_smoke_external_blocker": startup_smoke_external_blocker,
        "flagship_ui_screenshot_gate_status": flagship_ui_status,
        "flagship_ui_screenshot_gate_receipt_path": flagship_ui_gate_receipt_path,
        "flagship_ui_screenshot_count": len(flagship_ui_screenshot_files),
    },
    "build": {
        "output_base_root": output_base_root,
        "publish_dir": publish_dir,
        "dist_dir": dist_dir,
        "binary_path": binary_path,
        "binary_exists": pathlib.Path(binary_path).is_file(),
        "binary_sha256": binary_metadata["sha256"],
        "binary_bytes": binary_metadata["bytes"],
        "binary_executable": binary_metadata["executable"],
        "publish_exists": pathlib.Path(publish_dir).is_dir(),
        "self_contained": True,
        "single_file": True,
        "primary_package_kind": "deb",
        "fallback_package_kind": "archive",
        "archive_path": archive_path,
        "archive_exists": pathlib.Path(archive_path).is_file(),
        "archive_sha256": archive_metadata["sha256"],
        "archive_bytes": archive_metadata["bytes"],
        "installer_path": installer_path,
        "installer_exists": pathlib.Path(installer_path).is_file(),
        "installer_sha256": installer_metadata["sha256"],
        "installer_bytes": installer_metadata["bytes"],
    },
    "release_channel": {
        "path": release_channel_path,
        "local_desktop_files_root": local_desktop_files_root,
        "use_promoted_installer": str(use_promoted_installer).strip() == "1",
        "installer_smoke_artifact_path": installer_smoke_artifact_path,
        "promoted_installer_path": promoted_installer_path,
        "host_operating_system": host_operating_system,
        "host_operating_system_normalized": host_operating_system_normalized,
        "host_supports_linux_startup_smoke": host_supports_linux_startup_smoke,
        "startup_smoke_external_blocker": startup_smoke_external_blocker,
    },
    "startup_smoke": {
        "primary": {
            "package_kind": "deb",
            "artifact_path": installer_path,
            "receipt_path": installer_receipt_path,
            "status": "passed" if installer_receipt else ("missing" if pathlib.Path(installer_path).is_file() else "not_built"),
            "receipt": installer_receipt,
        },
        "fallback": {
            "package_kind": "archive",
            "artifact_path": archive_path,
            "receipt_path": archive_receipt_path,
            "status": "passed" if archive_receipt else ("missing" if pathlib.Path(archive_path).is_file() else "not_built"),
            "receipt": archive_receipt,
        },
    },
    "unit_tests": {
        "project_path": test_project_path,
        "framework": framework,
        "results_directory": test_results_dir,
        "trx_path": test_trx_path,
        "status": "passed"
        if pathlib.Path(test_trx_path).is_file() and test_summary["failed"] == 0 and test_summary["total"] > 0
        else ("missing" if not pathlib.Path(test_trx_path).is_file() else "failed"),
        "summary": test_summary,
        "assembly_name": "Chummer.Desktop.Runtime.Tests.dll",
    },
    "flagship_ui_screenshot_gate": {
        "enabled": str(flagship_ui_screenshot_gate_enabled).strip() == "1",
        "script": flagship_ui_gate_script,
        "receipt_path": flagship_ui_gate_receipt_path,
        "receipt_status": flagship_ui_status,
        "screenshot_directory": flagship_ui_gate_screenshot_dir,
        "screenshot_count": len(flagship_ui_screenshot_files),
        "screenshot_files": flagship_ui_screenshot_files,
        "workflow_screenshot_coverage_status": str(
            flagship_ui_visual_review.get("workflowScreenshotCoverageStatus")
            if isinstance(flagship_ui_visual_review, dict)
            else ""
        ).strip(),
        "required_workflow_family_ids": (
            flagship_ui_visual_review.get("requiredWorkflowFamilyIds")
            if isinstance(flagship_ui_visual_review, dict)
            else []
        ) or [],
        "workflow_screenshot_coverage": flagship_ui_workflow_coverage,
    },
    "git": {
        **current_git,
        "start": git_start,
        "finish": git_finish,
        "identity_stable": identity_stable,
    },
    "source_snapshot": source_snapshot,
    # Backward-compatible top-level fields consumed by Fleet supervisor audits.
    "current_git_available": bool(current_git.get("available")),
    "current_git_head": str(current_git.get("head") or ""),
    "current_tracked_diff_sha256": str(current_git.get("tracked_diff_sha256") or ""),
    "proof_git_available": bool(git_finish.get("available")),
    "proof_git_head": str(git_finish.get("head") or ""),
    "proof_git_start_head": str(git_start.get("head") or ""),
    "proof_git_finish_head": str(git_finish.get("head") or ""),
    "proof_git_start_tracked_diff_sha256": str(git_start.get("tracked_diff_sha256") or ""),
    "proof_git_finish_tracked_diff_sha256": str(git_finish.get("tracked_diff_sha256") or ""),
    "proof_git_identity_stable": bool(identity_stable),
    "proof_git_head_matches_current": str(git_finish.get("head") or "") == str(current_git.get("head") or ""),
    "proof_tracked_diff_sha256": str(git_finish.get("tracked_diff_sha256") or ""),
    "source_snapshot_entry_count": int(source_snapshot.get("entry_count") or 0),
    "source_snapshot_finish_entry_count": int(source_snapshot.get("finish_entry_count") or 0),
    "source_snapshot_worktree_sha256": str(source_snapshot.get("worktree_sha256") or ""),
    "source_snapshot_finish_worktree_sha256": str(source_snapshot.get("finish_worktree_sha256") or ""),
    "source_snapshot_identity_stable": bool(source_snapshot.get("identity_stable")),
}

pathlib.Path(proof_path).write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

publish_canonical_proof() {
  local lock_fd=9
  exec {lock_fd}>"$PUBLISH_LOCK_PATH"
  if command -v flock >/dev/null 2>&1; then
    flock "$lock_fd"
  fi

  python3 - "$RUN_PROOF_PATH" "$PROOF_PATH" "$LATEST_LINK" "$RUN_ROOT" <<'PY'
import json
import pathlib
import sys

new_path = pathlib.Path(sys.argv[1])
canonical_path = pathlib.Path(sys.argv[2])
latest_link_path = pathlib.Path(sys.argv[3])
run_root = pathlib.Path(sys.argv[4])


def load(path: pathlib.Path):
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return None


def proof_identity(payload):
    git = dict(payload.get("git") or {})
    return (
        str(git.get("head") or "").strip(),
        str(git.get("tracked_diff_sha256") or "").strip(),
    )


new_payload = load(new_path)
existing_payload = load(canonical_path)
publish = True

if new_payload and existing_payload:
    same_identity = proof_identity(new_payload) == proof_identity(existing_payload)
    existing_stage = str(existing_payload.get("stage") or "").strip()
    new_stage = str(new_payload.get("stage") or "").strip()
    early_infra_failure_stages = {"build_lock"}
    if same_identity and str(existing_payload.get("status") or "").strip() == "passed" and str(new_payload.get("status") or "").strip() != "passed":
        publish = False
    elif (
        existing_payload
        and str(existing_payload.get("status") or "").strip() == "passed"
        and str(new_payload.get("status") or "").strip() != "passed"
        and new_stage in early_infra_failure_stages
    ):
        publish = False

if publish:
    canonical_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = canonical_path.parent / f".{canonical_path.name}.{new_payload.get('stage') if new_payload else 'unknown'}.tmp"
    temp_path.write_text(new_path.read_text(encoding="utf-8"), encoding="utf-8")
    temp_path.replace(canonical_path)
    latest_link_path.parent.mkdir(parents=True, exist_ok=True)
    if latest_link_path.is_symlink() or latest_link_path.exists():
        latest_link_path.unlink()
    latest_link_path.symlink_to(run_root)
PY
}

acquire_build_lock() {
  if command -v flock >/dev/null 2>&1; then
    BUILD_LOCK_FD="8"
    eval "exec ${BUILD_LOCK_FD}>\"\$BUILD_LOCK_PATH\""
    flock "$BUILD_LOCK_FD"
    return
  fi

  BUILD_LOCK_DIR="${BUILD_LOCK_PATH}.lockdir"
  while ! mkdir "$BUILD_LOCK_DIR" 2>/dev/null; do
    sleep 1
  done
}

release_build_lock() {
  if [[ -n "$BUILD_LOCK_FD" ]]; then
    flock -u "$BUILD_LOCK_FD" || true
    eval "exec ${BUILD_LOCK_FD}>&-"
    BUILD_LOCK_FD=""
  fi

  if [[ -n "$BUILD_LOCK_DIR" ]]; then
    rmdir "$BUILD_LOCK_DIR" 2>/dev/null || true
    BUILD_LOCK_DIR=""
  fi
}

validate_flagship_ui_screenshot_gate() {
  python3 - "$FLAGSHIP_UI_GATE_RECEIPT_PATH" "$FLAGSHIP_UI_GATE_SCREENSHOT_DIR" <<'PY'
from __future__ import annotations

import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
screenshot_dir = pathlib.Path(sys.argv[2])
required_workflow_family_ids = [
    "create-open-import-save-save-as-print-export",
    "metatype-priorities-karma-entry",
    "attributes-skills-skill-groups-specializations-knowledge-languages",
    "qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources",
    "armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers",
    "cyberware-bioware-modular-hierarchies-nested-plugins",
    "magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms",
    "improvements-explain-result-parity",
    "recovery-reload-migration-roundtrips",
    "dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare",
]


def status_ok(value: object) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready"}


if not receipt_path.is_file():
    raise SystemExit(f"Flagship UI screenshot gate receipt is missing: {receipt_path}")
if not screenshot_dir.is_dir():
    raise SystemExit(f"Flagship UI screenshot directory is missing: {screenshot_dir}")

receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
if not status_ok(receipt.get("status")):
    raise SystemExit("Flagship UI screenshot gate receipt status is not passing.")

visual_review = receipt.get("visualReviewEvidence") or {}
expected_screenshots = [
    str(name or "").strip()
    for name in visual_review.get("expectedScreenshots") or []
    if str(name or "").strip()
]
png_files = {path.name for path in screenshot_dir.glob("*.png")}
missing_screenshots = [name for name in expected_screenshots if name not in png_files]
if missing_screenshots:
    raise SystemExit(
        "Flagship UI screenshot gate is missing expected PNGs: "
        + ", ".join(missing_screenshots)
    )
if len(png_files) < len(expected_screenshots):
    raise SystemExit("Flagship UI screenshot gate produced fewer PNG files than expected.")
if not status_ok(visual_review.get("workflowScreenshotCoverageStatus")):
    raise SystemExit("Flagship UI workflow screenshot coverage status is not passing.")

workflow_coverage = visual_review.get("workflowScreenshotCoverage") or []
if not isinstance(workflow_coverage, list):
    raise SystemExit("Flagship UI workflow screenshot coverage is not a list.")
coverage_by_id = {
    str(item.get("workflowFamilyId") or "").strip(): item
    for item in workflow_coverage
    if isinstance(item, dict)
}
missing_family_ids = [
    family_id
    for family_id in required_workflow_family_ids
    if family_id not in coverage_by_id
]
if missing_family_ids:
    raise SystemExit(
        "Flagship UI workflow screenshot coverage is missing families: "
        + ", ".join(missing_family_ids)
    )
for family_id in required_workflow_family_ids:
    coverage = coverage_by_id[family_id]
    screenshot_files = [
        str(name or "").strip()
        for name in coverage.get("screenshotFiles") or []
        if str(name or "").strip()
    ]
    if len(screenshot_files) < 2:
        raise SystemExit(f"Workflow family '{family_id}' has fewer than two screenshots.")
    if not str(coverage.get("legacyBehaviorLineage") or "").strip():
        raise SystemExit(f"Workflow family '{family_id}' is missing legacyBehaviorLineage.")
    missing_for_family = [name for name in screenshot_files if name not in png_files]
    if missing_for_family:
        raise SystemExit(
            f"Workflow family '{family_id}' references missing screenshots: "
            + ", ".join(missing_for_family)
        )
PY
}

on_error() {
  local exit_code=$?
  trap - ERR
  set +e
  write_proof "failed" "stage $CURRENT_STAGE failed" "$exit_code"
  publish_canonical_proof
  exit "$exit_code"
}

cleanup_snapshot() {
  if [[ -n "$SOURCE_SNAPSHOT_ROOT" && -d "$SOURCE_SNAPSHOT_ROOT" ]]; then
    rm -rf "$SOURCE_SNAPSHOT_ROOT"
  fi
  release_build_lock
  prune_old_run_roots
}

prune_old_run_roots() {
  if ! [[ "$RUN_RETENTION_COUNT" =~ ^[0-9]+$ ]] || [[ "$RUN_RETENTION_COUNT" -lt 1 ]]; then
    RUN_RETENTION_COUNT=40
  fi

  python3 - "$OUTPUT_BASE_ROOT" "$LATEST_LINK" "$RUN_ROOT" "$RUN_RETENTION_COUNT" <<'PY'
from __future__ import annotations

import pathlib
import shutil
import sys

output_base_root = pathlib.Path(sys.argv[1])
latest_link = pathlib.Path(sys.argv[2])
current_run_root = pathlib.Path(sys.argv[3]).resolve()
retention_count = int(sys.argv[4])

if not output_base_root.is_dir():
    raise SystemExit(0)

preserve = {current_run_root}
if latest_link.is_symlink():
    try:
        preserve.add(latest_link.resolve())
    except Exception:
        pass

run_roots = [
    path
    for path in output_base_root.iterdir()
    if path.is_dir() and path.name.startswith("run.")
]

ranked = sorted(
    run_roots,
    key=lambda path: path.stat().st_mtime,
    reverse=True,
)

kept_by_retention = {path.resolve() for path in ranked[:retention_count]}
keep = kept_by_retention.union(preserve)

for candidate in run_roots:
    resolved = candidate.resolve()
    if resolved in keep:
        continue
    shutil.rmtree(candidate, ignore_errors=True)
PY
}

trap on_error ERR
trap 'cleanup_snapshot' EXIT

mkdir -p "$PUBLISH_DIR" "$DIST_DIR" "$TEST_RESULTS_DIR" "$SMOKE_ARCHIVE_DIR" "$SMOKE_INSTALLER_DIR"
rm -f "$FAILURE_REASONS_PATH"

if [[ "$FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED" == "1" ]]; then
  CURRENT_STAGE="flagship_ui_screenshot_gate"
  bash "$FLAGSHIP_UI_GATE_SCRIPT"
  validate_flagship_ui_screenshot_gate
fi

capture_git_metadata "$GIT_START_PATH"

CURRENT_STAGE="source_snapshot"
materialize_source_snapshot

CURRENT_STAGE="build_lock"
acquire_build_lock

CURRENT_STAGE="unit_tests"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/test.sh" "$SOURCE_SNAPSHOT_ROOT/$TEST_PROJECT_PATH" -c Release -f "$FRAMEWORK" --logger "trx;LogFileName=$(basename "$TEST_TRX_PATH")" --results-directory "$TEST_RESULTS_DIR"
test -f "$TEST_TRX_PATH"

if [[ "$USE_PROMOTED_INSTALLER" == "1" && "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY:-0}" == "1" ]]; then
  CURRENT_STAGE="promoted_installer_shelf_probe"
  if [[ -z "$PROMOTED_INSTALLER_PATH" ]]; then
    PROMOTED_INSTALLER_PATH="$(resolve_promoted_installer_path || true)"
  fi
  PROMOTED_STARTUP_SMOKE_RECEIPT_PATH="$(resolve_promoted_startup_smoke_receipt_path || true)"
  if [[ -n "$PROMOTED_INSTALLER_PATH" && -f "$PROMOTED_INSTALLER_PATH" \
    && -n "$PROMOTED_STARTUP_SMOKE_RECEIPT_PATH" && -f "$PROMOTED_STARTUP_SMOKE_RECEIPT_PATH" ]]; then
    mkdir -p "$DIST_DIR" "$SMOKE_INSTALLER_DIR"
    cp "$PROMOTED_INSTALLER_PATH" "$INSTALLER_PATH"
    cp "$PROMOTED_STARTUP_SMOKE_RECEIPT_PATH" "$INSTALLER_RECEIPT_PATH"
    INSTALLER_SMOKE_ARTIFACT_PATH="$PROMOTED_INSTALLER_PATH"

    CURRENT_STAGE="promoted_installer_proof_integrity"
    python3 - "$RELEASE_CHANNEL_PATH" "$REPO_ROOT" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$INSTALLER_RECEIPT_PATH" "$USE_PROMOTED_INSTALLER" "$FAILURE_REASONS_PATH" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import pathlib
import sys

(
    release_channel_path_text,
    repo_root_text,
    local_desktop_files_root_text,
    app_key,
    rid,
    installer_smoke_artifact_path_text,
    installer_receipt_path_text,
    use_promoted_installer,
    failure_reasons_path_text,
) = sys.argv[1:]

release_channel_path = pathlib.Path(release_channel_path_text)
repo_root = pathlib.Path(repo_root_text)
local_desktop_files_root = pathlib.Path(local_desktop_files_root_text)
installer_smoke_artifact_path = pathlib.Path(installer_smoke_artifact_path_text)
installer_receipt_path = pathlib.Path(installer_receipt_path_text)
failure_reasons_path = pathlib.Path(failure_reasons_path_text)

reasons: list[str] = []


def normalize_token(value: object) -> str:
    return str(value or "").strip().lower()


def load_json(path: pathlib.Path) -> dict:
    try:
        loaded = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return loaded if isinstance(loaded, dict) else {}


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().lower()


def parse_iso(value: object) -> dt.datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


release_channel = load_json(release_channel_path)
expected_artifact = None
for item in release_channel.get("artifacts") or []:
    if not isinstance(item, dict):
        continue
    if (
        normalize_token(item.get("platform")) == "linux"
        and normalize_token(item.get("kind")) == "installer"
        and normalize_token(item.get("head")) == normalize_token(app_key)
        and normalize_token(item.get("rid")) == normalize_token(rid)
    ):
        expected_artifact = item
        break

if expected_artifact is None:
    reasons.append(f"Release channel does not publish a Linux installer artifact for {app_key} ({rid}).")
else:
    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_size = int(expected_artifact.get("sizeBytes") or 0)
    expected_version = str(release_channel.get("version") or expected_artifact.get("version") or "").strip()
    expected_channel = normalize_token(release_channel.get("channelId") or release_channel.get("channel"))
    shelf_path = local_desktop_files_root / expected_file_name if expected_file_name else pathlib.Path()
    if not expected_file_name:
        reasons.append(f"Promoted Linux artifact fileName is missing for {app_key} ({rid}).")
    elif not shelf_path.is_file():
        reasons.append(f"Promoted Linux installer file is missing from repo-local desktop shelf: {shelf_path}")
    else:
        if expected_size and shelf_path.stat().st_size != expected_size:
            reasons.append("Promoted Linux installer size does not match release-channel artifact size.")
        if expected_sha and sha256(shelf_path) != expected_sha:
            reasons.append("Promoted Linux installer sha256 does not match release-channel artifact sha256.")
    if not installer_smoke_artifact_path.is_file():
        reasons.append(f"Linux startup smoke installer artifact path is missing: {installer_smoke_artifact_path}")
    elif expected_sha and sha256(installer_smoke_artifact_path) != expected_sha:
        reasons.append("Linux startup smoke installer artifact bytes do not match promoted release-channel artifact bytes.")
    if str(use_promoted_installer).strip() == "1" and shelf_path.is_file():
        try:
            if installer_smoke_artifact_path.resolve() != shelf_path.resolve():
                reasons.append("Linux startup smoke installer artifact path does not resolve to promoted repo-local shelf bytes.")
        except Exception:
            reasons.append("Linux startup smoke installer artifact path could not be resolved for promoted shelf verification.")

    receipt = load_json(installer_receipt_path)
    if not receipt:
        reasons.append(f"Linux startup smoke receipt is missing or unreadable: {installer_receipt_path}")
    else:
        if normalize_token(receipt.get("status")) not in {"pass", "passed", "ready"}:
            reasons.append("Linux startup smoke receipt status is not passing.")
        if normalize_token(receipt.get("readyCheckpoint")) != "pre_ui_event_loop":
            reasons.append("Linux startup smoke receipt readyCheckpoint is not pre_ui_event_loop.")
        if normalize_token(receipt.get("headId")) != normalize_token(app_key):
            reasons.append("Linux startup smoke receipt headId does not match promoted head.")
        if normalize_token(receipt.get("platform")) != "linux":
            reasons.append("Linux startup smoke receipt platform is not linux.")
        if normalize_token(receipt.get("rid")) != normalize_token(rid):
            reasons.append("Linux startup smoke receipt rid does not match promoted RID.")
        if expected_channel and normalize_token(receipt.get("channelId") or receipt.get("channel")) != expected_channel:
            reasons.append("Linux startup smoke receipt channelId does not match release channel.")
        receipt_version = str(receipt.get("version") or receipt.get("releaseVersion") or "").strip()
        if expected_version and receipt_version != expected_version:
            reasons.append("Linux startup smoke receipt version does not match release channel version.")
        if expected_sha and normalize_token(receipt.get("artifactDigest")) != f"sha256:{expected_sha}":
            reasons.append("Linux startup smoke receipt artifactDigest does not match promoted installer bytes.")
        if not str(receipt.get("operatingSystem") or "").strip():
            reasons.append("Linux startup smoke receipt operatingSystem is missing.")
        if "linux" not in normalize_token(receipt.get("hostClass")).split("-"):
            reasons.append("Linux startup smoke receipt hostClass does not identify a Linux host.")
        recorded_at = parse_iso(
            receipt.get("completedAtUtc") or receipt.get("recordedAtUtc") or receipt.get("startedAtUtc")
        )
        if recorded_at is None:
            reasons.append("Linux startup smoke receipt timestamp is missing or invalid.")
        for label, key in (
            ("artifactInstallLaunchCapturePath", "launch"),
            ("artifactInstallWrapperCapturePath", "wrapper"),
            ("artifactInstallDesktopEntryCapturePath", "desktop entry"),
            ("artifactInstallVerificationPath", "verification"),
        ):
            value = str(receipt.get(label) or "").strip()
            if not value:
                reasons.append(f"Linux startup smoke receipt is missing {label}.")
                continue
            path = pathlib.Path(value)
            if not path.exists():
                reasons.append(f"Linux startup smoke {key} proof path does not exist: {value}")
            try:
                path.resolve().relative_to(repo_root.resolve())
            except Exception:
                reasons.append(f"Linux startup smoke {key} proof path is outside the UI repo root: {value}")

if reasons:
    failure_reasons_path.parent.mkdir(parents=True, exist_ok=True)
    failure_reasons_path.write_text(json.dumps({"reasons": reasons}, indent=2) + "\n", encoding="utf-8")
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

    CURRENT_STAGE="source_snapshot_identity"
    refresh_source_snapshot_manifest
    assert_source_snapshot_identity_stable

    CURRENT_STAGE="git_identity_stability"
    capture_git_metadata "$GIT_FINISH_PATH"
    if ! assert_repo_git_identity_stable; then
      GIT_IDENTITY_NOTE=" (post-run git identity drift detected outside the isolated source snapshot; source snapshot identity stayed stable)"
    fi

    CURRENT_STAGE="complete"
    write_proof "passed" "linux promoted installer shelf, startup smoke, and unit tests passed$GIT_IDENTITY_NOTE" "0"
    publish_canonical_proof
    echo "linux desktop exit gate passed; proof: $PROOF_PATH"
    exit 0
  fi
fi

CURRENT_STAGE="publish_linux_binary"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/with-package-plane.sh" publish "$SOURCE_SNAPSHOT_ROOT/$PROJECT_PATH" -c Release -r "$RID" --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:ChummerDesktopReleaseVersion="$VERSION" -p:ChummerDesktopReleaseChannel="$CHANNEL" -o "$PUBLISH_DIR" --nologo
test -f "$PUBLISH_DIR/$LAUNCH_TARGET"

CURRENT_STAGE="package_linux_artifacts"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/build-desktop-installer.sh" "$PUBLISH_DIR" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$DIST_DIR" "$VERSION"
test -f "$ARCHIVE_PATH"
test -f "$INSTALLER_PATH"
INSTALLER_SMOKE_ARTIFACT_PATH="$INSTALLER_PATH"

if [[ "$USE_PROMOTED_INSTALLER" == "1" ]]; then
  CURRENT_STAGE="resolve_promoted_installer"
  if [[ -z "$PROMOTED_INSTALLER_PATH" ]]; then
    PROMOTED_INSTALLER_PATH="$(resolve_promoted_installer_path)"
  fi
  if [[ -z "$PROMOTED_INSTALLER_PATH" || ! -f "$PROMOTED_INSTALLER_PATH" ]]; then
    echo "Linux promoted installer path could not be resolved for $APP_KEY $RID." >&2
    exit 1
  fi
  cp "$PROMOTED_INSTALLER_PATH" "$INSTALLER_PATH"
  INSTALLER_SMOKE_ARTIFACT_PATH="$PROMOTED_INSTALLER_PATH"
fi

CURRENT_STAGE="startup_smoke_archive"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" "$ARCHIVE_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_ARCHIVE_DIR" "$VERSION"
test -f "$ARCHIVE_RECEIPT_PATH"

CURRENT_STAGE="startup_smoke_installer"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_INSTALLER_DIR" "$VERSION"
test -f "$INSTALLER_RECEIPT_PATH"

CURRENT_STAGE="promoted_installer_proof_integrity"
python3 - "$RELEASE_CHANNEL_PATH" "$REPO_ROOT" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$INSTALLER_RECEIPT_PATH" "$USE_PROMOTED_INSTALLER" "$FAILURE_REASONS_PATH" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import pathlib
import platform
import shutil
import sys

(
    release_channel_path_text,
    repo_root_text,
    local_desktop_files_root_text,
    app_key,
    rid,
    installer_smoke_artifact_path_text,
    installer_receipt_path_text,
    use_promoted_installer,
    failure_reasons_path_text,
) = sys.argv[1:]

release_channel_path = pathlib.Path(release_channel_path_text)
repo_root = pathlib.Path(repo_root_text)
local_desktop_files_root = pathlib.Path(local_desktop_files_root_text)
installer_smoke_artifact_path = pathlib.Path(installer_smoke_artifact_path_text)
installer_receipt_path = pathlib.Path(installer_receipt_path_text)
failure_reasons_path = pathlib.Path(failure_reasons_path_text)

max_age_seconds = int(
    os.environ.get("CHUMMER_LINUX_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
)
max_future_skew_seconds = int(
    os.environ.get("CHUMMER_LINUX_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
reasons: list[str] = []
expected_channel = ""
expected_version = ""
host_operating_system = str(platform.system() or "").strip()
host_operating_system_normalized = host_operating_system.lower()
host_supports_linux_smoke = (
    host_operating_system_normalized == "linux"
    and bool(shutil.which("dpkg"))
    and bool(shutil.which("dpkg-deb"))
)


def normalize_token(value: object) -> str:
    return str(value or "").strip().lower()


def expected_host_class_platform_token(platform_name: str) -> str:
    normalized = normalize_token(platform_name)
    if normalized == "linux":
        return "linux"
    if normalized == "windows":
        return "win"
    if normalized == "macos":
        return "osx"
    return normalized


def host_class_matches_platform(host_class: str, platform_name: str) -> bool:
    normalized_host = normalize_token(host_class)
    expected_token = expected_host_class_platform_token(platform_name)
    if not normalized_host or not expected_token:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def path_uses_legacy_chummer5a_root(path: pathlib.Path) -> bool:
    normalized = str(path.resolve()).replace("\\", "/").lower()
    return "/chummer5a/" in normalized


def resolve_receipt_artifact_path(
    raw_candidates: list[str],
    repo_root: pathlib.Path,
    downloads_roots: list[pathlib.Path],
) -> tuple[str, list[str], pathlib.Path | None]:
    candidate_paths: list[pathlib.Path] = []
    for raw_value in raw_candidates:
        raw = str(raw_value or "").strip()
        if not raw:
            continue
        path = pathlib.Path(raw).expanduser()
        if path.is_absolute():
            candidate_paths.append(path)
            continue
        for root in downloads_roots:
            candidate_paths.append(root / path)
        candidate_paths.append(repo_root / path)

    deduped_candidates = list(dict.fromkeys(candidate_paths))
    resolved = next((path for path in deduped_candidates if path.is_file()), None)
    return (
        str(next((value for value in raw_candidates if str(value or "").strip()), "")).strip(),
        [str(path) for path in deduped_candidates],
        resolved or (deduped_candidates[0] if deduped_candidates else None),
    )

if not release_channel_path.is_file():
    reasons.append(f"Linux release-channel proof is missing: {release_channel_path}")
else:
    try:
        release_channel = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
    except Exception as ex:
        reasons.append(f"Linux release-channel proof is unreadable: {ex}")
        release_channel = {}

    status = str(release_channel.get("status") or "").strip().lower()
    expected_channel = str(release_channel.get("channelId") or release_channel.get("channel") or "").strip().lower()
    expected_version = str(release_channel.get("version") or "").strip()
    if status != "published":
        reasons.append("Linux release-channel proof status is not published.")
    if not expected_version:
        reasons.append("Linux release-channel proof version is missing.")

    expected_artifact = None
    for item in (release_channel.get("artifacts") or []):
        if not isinstance(item, dict):
            continue
        if (
            str(item.get("platform") or "").strip().lower() == "linux"
            and str(item.get("kind") or "").strip().lower() == "installer"
            and str(item.get("head") or "").strip().lower() == app_key.lower()
            and str(item.get("rid") or "").strip().lower() == rid.lower()
        ):
            expected_artifact = item
            break
    if expected_artifact is None:
        reasons.append(f"Release channel does not publish a Linux installer artifact for {app_key} ({rid}).")
    else:
        expected_file_name = str(expected_artifact.get("fileName") or "").strip()
        expected_size = int(expected_artifact.get("sizeBytes") or 0)
        expected_sha = str(expected_artifact.get("sha256") or "").strip().lower()
        expected_arch = "x64" if rid.endswith("x64") else "arm64" if rid.endswith("arm64") else ""
        if not expected_file_name:
            reasons.append(f"Promoted Linux artifact fileName is missing for {app_key} ({rid}).")
        if not expected_sha:
            reasons.append(f"Promoted Linux artifact sha256 is missing for {app_key} ({rid}).")

        promoted_shelf_artifact_path = local_desktop_files_root / expected_file_name if expected_file_name else pathlib.Path()
        if expected_file_name and not promoted_shelf_artifact_path.is_file():
            reasons.append(
                f"Promoted Linux installer file is missing from repo-local desktop shelf: {promoted_shelf_artifact_path}"
            )
        elif expected_file_name:
            promoted_shelf_artifact_size = promoted_shelf_artifact_path.stat().st_size
            promoted_shelf_artifact_sha = hashlib.sha256(promoted_shelf_artifact_path.read_bytes()).hexdigest().lower()
            if expected_size and promoted_shelf_artifact_size != expected_size:
                reasons.append("Promoted Linux installer size does not match release-channel artifact size.")
            if expected_sha and promoted_shelf_artifact_sha != expected_sha:
                reasons.append("Promoted Linux installer sha256 does not match release-channel artifact sha256.")

            if not installer_smoke_artifact_path.is_file():
                reasons.append(
                    f"Linux startup smoke installer artifact path is missing: {installer_smoke_artifact_path}"
                )
            else:
                smoke_artifact_sha = hashlib.sha256(installer_smoke_artifact_path.read_bytes()).hexdigest().lower()
                if expected_sha and smoke_artifact_sha != expected_sha:
                    reasons.append(
                        "Linux startup smoke installer artifact bytes do not match promoted release-channel artifact bytes."
                    )

            if str(use_promoted_installer).strip() == "1":
                try:
                    if installer_smoke_artifact_path.resolve() != promoted_shelf_artifact_path.resolve():
                        reasons.append(
                            "Linux startup smoke installer artifact path does not resolve to promoted repo-local shelf bytes."
                        )
                except Exception:
                    reasons.append(
                        "Linux startup smoke installer artifact path could not be resolved for promoted shelf verification."
                    )

        if not installer_receipt_path.is_file():
            reasons.append(f"Linux startup smoke receipt is missing: {installer_receipt_path}")
            if not host_supports_linux_smoke:
                reasons.append(
                    "Linux startup smoke requires a Linux host with dpkg and dpkg-deb; current host cannot run promoted Linux installer smoke."
                )
        else:
            if path_uses_legacy_chummer5a_root(installer_receipt_path):
                reasons.append("Linux startup smoke receipt was resolved from a legacy chummer5a path.")

            try:
                receipt = json.loads(installer_receipt_path.read_text(encoding="utf-8-sig"))
            except Exception as ex:
                reasons.append(f"Linux startup smoke receipt is unreadable: {ex}")
                receipt = {}

            receipt_status = str(receipt.get("status") or "").strip().lower()
            receipt_ready_checkpoint = str(receipt.get("readyCheckpoint") or "").strip().lower()
            receipt_head = str(receipt.get("headId") or "").strip().lower()
            receipt_platform = str(receipt.get("platform") or "").strip().lower()
            receipt_arch = str(receipt.get("arch") or "").strip().lower()
            receipt_rid = str(receipt.get("rid") or "").strip().lower()
            receipt_channel = str(receipt.get("channelId") or receipt.get("channel") or "").strip().lower()
            receipt_digest = str(receipt.get("artifactDigest") or "").strip().lower()
            receipt_release_version = str(receipt.get("releaseVersion") or "").strip()
            receipt_version = str(receipt.get("version") or receipt.get("releaseVersion") or "").strip()
            receipt_host_class = str(receipt.get("hostClass") or "").strip().lower()
            receipt_operating_system = str(receipt.get("operatingSystem") or "").strip()
            receipt_artifact_path, receipt_artifact_path_candidates, receipt_artifact_path_obj = resolve_receipt_artifact_path(
                [
                    receipt.get("artifactRelativePath"),
                    receipt.get("artifactPath"),
                ],
                repo_root,
                [
                    local_desktop_files_root.parent,
                    release_channel_path.parent,
                    release_channel_path.parent.parent,
                ],
            )
            receipt_recorded_at = (
                str(receipt.get("completedAtUtc") or "").strip()
                or str(receipt.get("recordedAtUtc") or "").strip()
                or str(receipt.get("startedAtUtc") or "").strip()
            )
            expected_digest = f"sha256:{expected_sha}" if expected_sha else ""

            if receipt_status not in {"pass", "passed", "ready"}:
                reasons.append("Linux startup smoke receipt status is not passing.")
            if receipt_ready_checkpoint != "pre_ui_event_loop":
                reasons.append("Linux startup smoke receipt readyCheckpoint is not pre_ui_event_loop.")
            if receipt_head != app_key.lower():
                reasons.append("Linux startup smoke receipt headId does not match promoted head.")
            if receipt_platform != "linux":
                reasons.append("Linux startup smoke receipt platform is not linux.")
            if not receipt_host_class:
                reasons.append("Linux startup smoke receipt hostClass is missing.")
            elif not host_class_matches_platform(receipt_host_class, "linux"):
                reasons.append("Linux startup smoke receipt hostClass does not identify a Linux host.")
            if not receipt_operating_system:
                reasons.append("Linux startup smoke receipt operatingSystem is missing.")
            if expected_arch and receipt_arch != expected_arch:
                reasons.append("Linux startup smoke receipt arch does not match promoted RID.")
            if not receipt_rid:
                reasons.append("Linux startup smoke receipt rid is missing.")
            elif receipt_rid != rid.lower():
                reasons.append("Linux startup smoke receipt rid does not match promoted RID.")
            if expected_channel and receipt_channel != expected_channel:
                reasons.append("Linux startup smoke receipt channelId does not match release channel.")
            if expected_version and not receipt_release_version:
                reasons.append("Linux startup smoke receipt releaseVersion is missing.")
            if (
                expected_version
                and receipt_release_version
                and receipt_release_version != expected_version
                and receipt_version != expected_version
            ):
                reasons.append("Linux startup smoke receipt releaseVersion does not match release channel version.")
            if expected_version and not receipt_version:
                reasons.append("Linux startup smoke receipt version is missing.")
            if expected_version and receipt_version and receipt_version != expected_version:
                reasons.append("Linux startup smoke receipt version does not match release channel version.")
            if expected_digest and receipt_digest != expected_digest:
                reasons.append("Linux startup smoke receipt artifactDigest does not match promoted installer bytes.")
            if not receipt_artifact_path:
                reasons.append("Linux startup smoke receipt artifactPath is missing.")
            else:
                if receipt_artifact_path_obj is None:
                    reasons.append(
                        "Linux startup smoke receipt artifactPath could not be resolved for promoted shelf verification."
                    )
                elif path_uses_legacy_chummer5a_root(receipt_artifact_path_obj):
                    reasons.append("Linux startup smoke receipt artifactPath points into a legacy chummer5a root.")
                elif str(use_promoted_installer).strip() == "1":
                    try:
                        if (
                            promoted_shelf_artifact_path.is_file()
                            and receipt_artifact_path_obj.resolve() != promoted_shelf_artifact_path.resolve()
                        ):
                            reasons.append(
                                "Linux startup smoke receipt artifactPath does not resolve to promoted installer shelf bytes."
                            )
                    except Exception:
                        reasons.append(
                            "Linux startup smoke receipt artifactPath could not be resolved for promoted shelf verification."
                        )
            if not receipt_recorded_at:
                reasons.append("Linux startup smoke receipt timestamp is missing.")
            else:
                normalized = receipt_recorded_at[:-1] + "+00:00" if receipt_recorded_at.endswith("Z") else receipt_recorded_at
                try:
                    recorded_at = dt.datetime.fromisoformat(normalized)
                    if recorded_at.tzinfo is None:
                        recorded_at = recorded_at.replace(tzinfo=dt.timezone.utc)
                    recorded_at = recorded_at.astimezone(dt.timezone.utc)
                    age_delta_seconds = int((dt.datetime.now(dt.timezone.utc) - recorded_at).total_seconds())
                    age_seconds = max(0, age_delta_seconds)
                    if age_delta_seconds < 0:
                        future_skew_seconds = abs(age_delta_seconds)
                        if future_skew_seconds > max_future_skew_seconds:
                            reasons.append(
                                f"Linux startup smoke receipt timestamp is in the future ({future_skew_seconds}s ahead)."
                            )
                    if age_seconds > max_age_seconds:
                        reasons.append(f"Linux startup smoke receipt is stale ({age_seconds}s old).")
                except ValueError:
                    reasons.append("Linux startup smoke receipt timestamp is invalid.")

if reasons:
    failure_reasons_path.parent.mkdir(parents=True, exist_ok=True)
    failure_reasons_path.write_text(json.dumps({"reasons": reasons}, indent=2) + "\n", encoding="utf-8")
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

CURRENT_STAGE="source_snapshot_identity"
refresh_source_snapshot_manifest
assert_source_snapshot_identity_stable

CURRENT_STAGE="git_identity_stability"
capture_git_metadata "$GIT_FINISH_PATH"
if ! assert_repo_git_identity_stable; then
  GIT_IDENTITY_NOTE=" (post-run git identity drift detected outside the isolated source snapshot; source snapshot identity stayed stable)"
fi

CURRENT_STAGE="complete"
write_proof "passed" "linux desktop build, startup smoke, and unit tests passed$GIT_IDENTITY_NOTE" "0"
publish_canonical_proof
echo "linux desktop exit gate passed; proof: $PROOF_PATH"
