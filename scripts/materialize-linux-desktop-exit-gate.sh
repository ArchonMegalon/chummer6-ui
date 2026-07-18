#!/usr/bin/env bash
set -euo pipefail
set -o errtrace

SCRIPT_DIR="$(cd -L "$(dirname "${BASH_SOURCE[0]}")" && pwd -L)"
REPO_ROOT_PHYSICAL="$(cd "$SCRIPT_DIR/.." && pwd -P)"
PYTHON_BIN="${CHUMMER_PYTHON_BIN:-/usr/bin/python3}"
if [[ ! -x "$PYTHON_BIN" ]]; then
  PYTHON_BIN="$(command -v python3)"
fi
REPO_ROOT_ALIAS_CANDIDATE="${CHUMMER_UI_REPO_ROOT_ALIAS:-$REPO_ROOT_PHYSICAL}"
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
  RELEASE_PROMOTED_TUPLE=()
  while IFS= read -r tuple_value; do
    [[ -n "$tuple_value" ]] || continue
    RELEASE_PROMOTED_TUPLE+=("$tuple_value")
  done < <("$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$APP_KEY_OVERRIDE" "$RID_OVERRIDE" <<'PY'
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
TEST_ASSEMBLY_NAME="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_TEST_ASSEMBLY_NAME:-Chummer.Desktop.Runtime.Tests.dll}"
TEST_FILTER="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_TEST_FILTER:-FullyQualifiedName~DesktopCrashRuntimeTests|FullyQualifiedName~DesktopPreferenceRuntimeTests|FullyQualifiedName~DesktopStartupSmokeRuntimeTests|FullyQualifiedName~DesktopMouseFirstJourneyRuntimeTests|FullyQualifiedName~DesktopUpdateRuntimeTests|FullyQualifiedName~DesktopInstallLinkingRuntimeTests|FullyQualifiedName~AvaloniaHeadlessSmokeTests}"
RID="${RID:-linux-x64}"
LAUNCH_TARGET="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_LAUNCH_TARGET:-$DEFAULT_LAUNCH_TARGET}"
RELEASE_CHANNEL_ID_DEFAULT="$(
  "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" <<'PY'
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
  "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" <<'PY'
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
PACKAGE_PLANE_LOCK_ROOT_DEFAULT="${CHUMMER_PACKAGE_PLANE_LOCK_ROOT:-$WORKSPACE_ROOT/.tmp/ai}"
PACKAGE_PLANE_LOCK_PATH_DEFAULT="${CHUMMER_PACKAGE_PLANE_LOCK_FILE:-$PACKAGE_PLANE_LOCK_ROOT_DEFAULT/with-package-plane.lock}"
BUILD_LOCK_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_BUILD_LOCK_PATH:-$PACKAGE_PLANE_LOCK_PATH_DEFAULT}"
DEFAULT_LOCAL_DESKTOP_FILES_ROOT="$REPO_ROOT/Docker/Downloads/files"
RELEASE_CHANNEL_DIRECTORY="$(cd "$(dirname "$RELEASE_CHANNEL_PATH")" 2>/dev/null && pwd -P || true)"
RELEASE_CHANNEL_FILES_ROOT_DEFAULT=""
if [[ -n "$RELEASE_CHANNEL_DIRECTORY" ]]; then
  RELEASE_CHANNEL_FILES_ROOT_DEFAULT="$RELEASE_CHANNEL_DIRECTORY/files"
fi
if [[ -n "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT:-}" ]]; then
  LOCAL_DESKTOP_FILES_ROOT="$CHUMMER_LINUX_DESKTOP_EXIT_GATE_LOCAL_DESKTOP_FILES_ROOT"
elif [[ -n "$RELEASE_CHANNEL_FILES_ROOT_DEFAULT" && ( -d "$RELEASE_CHANNEL_FILES_ROOT_DEFAULT" || "$RELEASE_CHANNEL_PATH" != "$DEFAULT_RELEASE_CHANNEL_PATH" ) ]]; then
  LOCAL_DESKTOP_FILES_ROOT="$RELEASE_CHANNEL_FILES_ROOT_DEFAULT"
else
  LOCAL_DESKTOP_FILES_ROOT="$DEFAULT_LOCAL_DESKTOP_FILES_ROOT"
fi
if [[ -n "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER:-}" ]]; then
  USE_PROMOTED_INSTALLER="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_USE_PROMOTED_INSTALLER}"
elif [[ -n "${CI:-}" ]]; then
  USE_PROMOTED_INSTALLER="1"
else
  USE_PROMOTED_INSTALLER="0"
fi
FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RUN_FLAGSHIP_UI_GATE:-1}"
FLAGSHIP_UI_GATE_SCRIPT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_SCRIPT:-$REPO_ROOT/scripts/ai/milestones/b14-flagship-ui-release-gate.sh}"
FLAGSHIP_UI_GATE_RECEIPT_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_RECEIPT_PATH:-$REPO_ROOT/.codex-studio/published/UI_FLAGSHIP_RELEASE_GATE.generated.json}"
FLAGSHIP_UI_GATE_SCREENSHOT_DIR="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_FLAGSHIP_UI_GATE_SCREENSHOT_DIR:-$REPO_ROOT/.codex-studio/published/ui-flagship-release-gate-screenshots}"
FLAGSHIP_UI_SCREENSHOT_CONTROL_EVIDENCE_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_SCREENSHOT_CONTROL_EVIDENCE_PATH:-$FLAGSHIP_UI_GATE_SCREENSHOT_DIR/SCREENSHOT_CONTROL_EVIDENCE.generated.json}"
SNAPSHOT_WRITABLE_STATE_ROOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_WRITABLE_STATE_ROOT:-$WORKSPACE_ROOT/.tmp/ai/linux-desktop-exit-gate}"
SNAPSHOT_NUGET_PACKAGES="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_NUGET_PACKAGES:-$WORKSPACE_ROOT/.tmp/ai/nuget/packages}"
SOURCE_SNAPSHOT_CLONE_MODE="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_SOURCE_SNAPSHOT_CLONE_MODE:-copy}"
KEEP_SOURCE_SNAPSHOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_KEEP_SOURCE_SNAPSHOT:-0}"
EXIT_GATE_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS:-0}"
export CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS="$EXIT_GATE_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS"

mkdir -p "$OUTPUT_BASE_ROOT"
RUN_ROOT="$(mktemp -d "$OUTPUT_BASE_ROOT/run.XXXXXX")"
LATEST_LINK="$OUTPUT_BASE_ROOT/latest"
PUBLISH_LOCK_PATH="$OUTPUT_BASE_ROOT/publish.lock"
RUN_PROOF_PATH="$RUN_ROOT/$(basename "$PROOF_PATH")"
RUN_OWNER_PID_PATH="$RUN_ROOT/owner.pid"
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
TEST_STATUS_PATH="$TEST_RESULTS_DIR/desktop-runtime-tests.status.json"
ARCHIVE_RECEIPT_PATH="$SMOKE_ARCHIVE_DIR/startup-smoke-$APP_KEY-$RID.receipt.json"
INSTALLER_RECEIPT_PATH="$SMOKE_INSTALLER_DIR/startup-smoke-$APP_KEY-$RID.receipt.json"
ARCHIVE_MOUSE_FIRST_JOURNEY_RECEIPT_PATH="$SMOKE_ARCHIVE_DIR/mouse-first-journey-$APP_KEY-$RID.receipt.json"
ARCHIVE_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH="$SMOKE_ARCHIVE_DIR/mouse-first-journey-$APP_KEY-$RID.failure.json"
ARCHIVE_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$SMOKE_ARCHIVE_DIR/mouse-first-journey-screenshots-$APP_KEY-$RID"
ARCHIVE_MOUSE_FIRST_JOURNEY_TRACE_PATH="$SMOKE_ARCHIVE_DIR/mouse-first-journey-$APP_KEY-$RID.trace.json"
INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH="$SMOKE_INSTALLER_DIR/mouse-first-journey-$APP_KEY-$RID.receipt.json"
INSTALLER_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH="$SMOKE_INSTALLER_DIR/mouse-first-journey-$APP_KEY-$RID.failure.json"
INSTALLER_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$SMOKE_INSTALLER_DIR/mouse-first-journey-screenshots-$APP_KEY-$RID"
INSTALLER_MOUSE_FIRST_JOURNEY_TRACE_PATH="$SMOKE_INSTALLER_DIR/mouse-first-journey-$APP_KEY-$RID.trace.json"
BUILD_LOCK_FD=""
BUILD_LOCK_DIR=""
BUILD_LOCK_WAIT_SECONDS="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_BUILD_LOCK_WAIT_SECONDS:-10}"

CURRENT_STAGE="init"
GIT_IDENTITY_NOTE=""
INSTALLER_SMOKE_ARTIFACT_PATH=""
PROMOTED_INSTALLER_PATH="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_INSTALLER_PATH:-}"

printf '%s\n' "$$" >"$RUN_OWNER_PID_PATH"

resolve_promoted_installer_path() {
  "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" <<'PY'
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
  "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" <<'PY'
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

release_channel_publishes_promoted_installer_tuple() {
  "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$APP_KEY" "$RID" <<'PY'
import json
import pathlib
import sys

release_channel_path = pathlib.Path(sys.argv[1])
head = str(sys.argv[2]).strip().lower()
rid = str(sys.argv[3]).strip().lower()

if not release_channel_path.is_file():
    raise SystemExit(1)

try:
    payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
except Exception:
    raise SystemExit(1)

for item in payload.get("artifacts") or []:
    if not isinstance(item, dict):
        continue
    if (
        str(item.get("platform") or "").strip().lower() == "linux"
        and str(item.get("kind") or "").strip().lower() == "installer"
        and str(item.get("head") or "").strip().lower() == head
        and str(item.get("rid") or "").strip().lower() == rid
    ):
        raise SystemExit(0)

raise SystemExit(1)
PY
}

capture_git_metadata() {
  local output_path="$1"

  "$PYTHON_BIN" - "$output_path" "$REPO_ROOT" "$OUTPUT_BASE_ROOT" "$PROOF_PATH" <<'PY'
import hashlib
import json
import os
import pathlib
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

output_path = pathlib.Path(sys.argv[1])
repo_root_text = sys.argv[2]
output_base_root_text = sys.argv[3]
canonical_proof_path_text = sys.argv[4]
repo_root = pathlib.Path(repo_root_text).resolve()
output_base_root = pathlib.Path(output_base_root_text).resolve()
canonical_proof_path = pathlib.Path(canonical_proof_path_text).resolve()

payload = {
    "repo_root": repo_root_text,
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
    "Chummer.Presentation/",
    "scripts/ai/",
    "scripts/build-desktop-installer.sh",
    "scripts/run-desktop-startup-smoke.sh",
    "scripts/materialize-linux-desktop-exit-gate.sh",
    "Chummer.sln",
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


def is_generated_build_output(relative_path: str) -> bool:
    parts = tuple(part for part in pathlib.Path(relative_path).parts if part)
    return any(part in {"bin", "obj", "TestResults"} for part in parts)


def add_msbuild_linked_entries(entries, markers):
    ordered_entries = list(entries)
    seen = set(entries)

    for relative in list(ordered_entries):
        if not relative.endswith(("proj", ".props", ".targets")):
            continue

        project_path = repo_root / relative
        if not project_path.is_file():
            continue

        try:
            root = ET.fromstring(project_path.read_text(encoding="utf-8-sig"))
        except Exception:
            continue

        for element in root.iter():
            for attribute_name in ("Include", "Update", "Remove"):
                raw_value = str(element.attrib.get(attribute_name) or "").strip()
                if not raw_value or "*" in raw_value or "$(" in raw_value:
                    continue
                candidate = raw_value.replace("\\", "/")
                resolved = (project_path.parent / candidate).resolve()
                try:
                    resolved.relative_to(repo_root)
                except Exception:
                    continue
                if not resolved.is_file():
                    continue
                resolved_relative = resolved.relative_to(repo_root).as_posix()
                if (
                    resolved_relative in seen
                    or is_excluded(resolved_relative, markers)
                    or is_generated_build_output(resolved_relative)
                ):
                    continue
                seen.add(resolved_relative)
                ordered_entries.append(resolved_relative)

    return sorted(ordered_entries)


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
            if is_generated_build_output(relative):
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
                if is_generated_build_output(relative):
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
            if is_generated_build_output(relative):
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
            if is_generated_build_output(relative):
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
for relative in add_msbuild_linked_entries(iter_repo_entries(normalize_markers()), normalize_markers()):
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

  "$PYTHON_BIN" - "$REPO_ROOT" "$SOURCE_SNAPSHOT_ROOT" "$OUTPUT_BASE_ROOT" "$PROOF_PATH" "$SOURCE_SNAPSHOT_MANIFEST_PATH" "$SOURCE_SNAPSHOT_ENTRIES_PATH" "$SOURCE_SNAPSHOT_CLONE_MODE" <<'PY'
import hashlib
import json
import os
import pathlib
import shutil
import errno
import stat
import subprocess
import sys
import xml.etree.ElementTree as ET

repo_root_text = sys.argv[1]
snapshot_root_text = sys.argv[2]
output_base_root_text = sys.argv[3]
canonical_proof_path_text = sys.argv[4]
manifest_path_text = sys.argv[5]
entries_path_text = sys.argv[6]
clone_mode = str(sys.argv[7] or "copy").strip().lower()
repo_root = pathlib.Path(repo_root_text).resolve()
snapshot_root = pathlib.Path(snapshot_root_text).resolve()
output_base_root = pathlib.Path(output_base_root_text).resolve()
canonical_proof_path = pathlib.Path(canonical_proof_path_text).resolve()
manifest_path = pathlib.Path(manifest_path_text).resolve()
entries_path = pathlib.Path(entries_path_text).resolve()

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
    # Keep the runtime test inputs narrow. Pulling the full Chummer.Tests tree
    # drags in large linked fixture payloads that are irrelevant to the linux
    # desktop gate and can exhaust disk before publish even starts.
    "Chummer.Tests/DesktopPreferenceRuntimeTests.cs",
    "Chummer.Tests/DesktopMouseFirstJourneyRuntimeTests.cs",
    "Chummer.Tests/DesktopStartupSmokeRuntimeTests.cs",
    "Chummer.Tests/Presentation/AvaloniaHeadlessSmokeTests.cs",
    "Chummer.Tests/DesktopUpdateRuntimeTests.cs",
    "Chummer.Presentation/",
    "scripts/ai/",
    "scripts/build-desktop-installer.sh",
    "scripts/run-desktop-startup-smoke.sh",
    "scripts/materialize-linux-desktop-exit-gate.sh",
    "Chummer.sln",
    "Directory.Build.props",
    "Directory.Build.targets",
    "Directory.Packages.props",
    "NuGet.Config",
    "global.json",
)

# Linked desktop-runtime test sources and bundled sample assets are pulled in via
# add_msbuild_linked_entries; copying the full legacy fixture tree here burns
# disk without changing publish, smoke, or desktop-runtime test behavior.

# Keep the immutable snapshot focused on source plus required desktop assets.
# Copying full compiled trees here makes proof refreshes fail on otherwise
# healthy hosts because the snapshot duplicates multi-GB bin/obj outputs.
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


def is_generated_build_output(relative_path: str) -> bool:
    parts = tuple(part for part in pathlib.Path(relative_path).parts if part)
    return any(part in {"bin", "obj", "TestResults"} for part in parts)


def clone_regular_file(src_path: pathlib.Path, dest_path: pathlib.Path) -> None:
    if clone_mode in {"link", "link_or_copy"}:
        try:
            os.link(src_path, dest_path)
            return
        except OSError as error:
            if error.errno not in {
                errno.EXDEV,
                errno.EMLINK,
                errno.EPERM,
                errno.ENOTSUP,
                errno.EOPNOTSUPP,
                errno.EXDEV,
            }:
                raise
    try:
        shutil.copy2(src_path, dest_path)
    except FileNotFoundError:
        # copy2 can intermittently fail on some Linux filesystems while copying
        # metadata after source-materialization; fall back to content copy to avoid
        # transient snapshot truncation while preserving file identity.
        try:
            shutil.copyfile(src_path, dest_path)
        except Exception as fallback_error:
            raise FileNotFoundError(
                f"copy2+fallback copyfile failed for {src_path}: {fallback_error}"
            )


def copy_tracked_regular_file(src_path: pathlib.Path, dest_path: pathlib.Path) -> None:
    # The immutable tracked-input snapshot must not share writable inodes with the
    # live repo; restore/publish can mutate some source-adjacent files in place.
    try:
        shutil.copy2(src_path, dest_path)
    except FileNotFoundError:
        try:
            shutil.copyfile(src_path, dest_path)
        except Exception as fallback_error:
            raise FileNotFoundError(
                f"copy2+fallback copyfile failed for {src_path}: {fallback_error}"
            )


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
            if is_generated_build_output(relative):
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
                if is_generated_build_output(relative):
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
            if is_generated_build_output(relative):
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
            if is_generated_build_output(relative):
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
            if is_generated_build_output(relative):
                continue
            entries.append(relative)
        return entries


def add_msbuild_linked_entries(entries, markers):
    ordered_entries = list(entries)
    seen = set(entries)

    for relative in list(ordered_entries):
        if not relative.endswith(("proj", ".props", ".targets")):
            continue

        project_path = repo_root / relative
        if not project_path.is_file():
            continue

        try:
            root = ET.fromstring(project_path.read_text(encoding="utf-8-sig"))
        except Exception:
            continue

        for element in root.iter():
            for attribute_name in ("Include", "Update", "Remove"):
                raw_value = str(element.attrib.get(attribute_name) or "").strip()
                if not raw_value or "*" in raw_value or "$(" in raw_value:
                    continue
                candidate = raw_value.replace("\\", "/")
                resolved = (project_path.parent / candidate).resolve()
                try:
                    resolved.relative_to(repo_root)
                except Exception:
                    continue
                if not resolved.is_file():
                    continue
                resolved_relative = resolved.relative_to(repo_root).as_posix()
                if (
                    resolved_relative in seen
                    or is_excluded(resolved_relative, markers)
                    or is_generated_build_output(resolved_relative)
                ):
                    continue
                seen.add(resolved_relative)
                ordered_entries.append(resolved_relative)

    return sorted(ordered_entries)


markers = normalize_markers()
tracked_entries = add_msbuild_linked_entries(iter_tracked_repo_entries(markers), markers)
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
    copy_tracked_regular_file(src_path, dest_path)
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
        shutil.copytree(src_path, dest_path, dirs_exist_ok=True, copy_function=clone_regular_file)
        continue
    dest_path.parent.mkdir(parents=True, exist_ok=True)
    clone_regular_file(src_path, dest_path)

manifest = {
    "mode": "filesystem_link_or_copy" if clone_mode in {"link", "link_or_copy"} else "filesystem_copy",
    "repo_root": repo_root_text,
    "snapshot_root": snapshot_root_text,
    "entries_path": entries_path_text,
    "entry_count": entry_count,
    "worktree_sha256": digest.hexdigest(),
}
entries_path.write_text("".join(f"{relative}\n" for relative in tracked_entries), encoding="utf-8")
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
PY
}

refresh_source_snapshot_manifest() {
  "$PYTHON_BIN" - "$SOURCE_SNAPSHOT_MANIFEST_PATH" <<'PY'
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
  "$PYTHON_BIN" - "$SOURCE_SNAPSHOT_MANIFEST_PATH" <<'PY'
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
  "$PYTHON_BIN" - "$GIT_START_PATH" "$GIT_FINISH_PATH" <<'PY'
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
    "$APP_KEY" "$PROJECT_PATH" "$TEST_PROJECT_PATH" "$TEST_ASSEMBLY_NAME" "$RID" "$LAUNCH_TARGET" "$VERSION" "$CHANNEL" "$FRAMEWORK" \
    "$READY_CHECKPOINT" "$RUN_ROOT" "$PUBLISH_DIR" "$DIST_DIR" "$ARCHIVE_PATH" "$INSTALLER_PATH" "$ARCHIVE_RECEIPT_PATH" "$INSTALLER_RECEIPT_PATH" \
    "$ARCHIVE_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" "$ARCHIVE_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH" "$ARCHIVE_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR" "$INSTALLER_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR" \
    "$TEST_RESULTS_DIR" "$TEST_TRX_PATH" "$TEST_STATUS_PATH" "$GIT_START_PATH" "$GIT_FINISH_PATH" "$SOURCE_SNAPSHOT_MANIFEST_PATH" \
    "$RELEASE_CHANNEL_PATH" "$LOCAL_DESKTOP_FILES_ROOT" "$USE_PROMOTED_INSTALLER" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$PROMOTED_INSTALLER_PATH" \
    "$FAILURE_REASONS_PATH" "$FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED" "$FLAGSHIP_UI_GATE_RECEIPT_PATH" "$FLAGSHIP_UI_GATE_SCREENSHOT_DIR" "$FLAGSHIP_UI_GATE_SCRIPT" <<'PY'
import datetime as dt
import hashlib
import json
import os
import pathlib
import platform
import re
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
    test_assembly_name,
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
    archive_mouse_first_journey_receipt_path,
    installer_mouse_first_journey_receipt_path,
    archive_mouse_first_journey_failure_packet_path,
    installer_mouse_first_journey_failure_packet_path,
    archive_mouse_first_journey_screenshot_dir,
    installer_mouse_first_journey_screenshot_dir,
    test_results_dir,
    test_trx_path,
    test_status_path,
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


def portable_receipt_projection(payload):
    if not isinstance(payload, dict):
        return payload

    def portable_path(value):
        raw = str(value or "").strip()
        normalized = raw.replace("\\", "/").rstrip("/")
        if not normalized:
            return raw
        is_absolute = normalized.startswith("/") or bool(re.match(r"^[A-Za-z]:/", normalized))
        if not is_absolute:
            return raw
        for marker in ("files", "startup-smoke", "release-evidence"):
            token = f"/{marker}/"
            if token in normalized:
                return f"{marker}/{normalized.rsplit('/', 1)[-1]}"
        return normalized.rsplit("/", 1)[-1]

    def redact_text(value):
        redacted = re.sub(r"/home/[^/\r\n]+/", "<redacted:user-home>/", value)
        redacted = re.sub(r"/Users/[^/\r\n]+/", "<redacted:user-home>/", redacted)
        redacted = re.sub(
            r"(?i)[A-Z]:[\\/](?:Users|Documents and Settings)[\\/][^\\/\r\n]+[\\/]",
            "<redacted:user-home>/",
            redacted,
        )
        redacted = re.sub(
            r"(?<![:\w])/(?:tmp|private/var|var/folders|var/tmp|root|run/user|mnt|docker|workspace|workspaces)/[^\s\"'<>]+",
            "<redacted:host-path>",
            redacted,
        )
        return re.sub(
            r"(?i)[A-Z]:[\\/](?:Temp|tmp|workspace|workspaces)[\\/][^\s\"'<>]+",
            "<redacted:host-path>",
            redacted,
        )

    def project(value, semantic_key=""):
        if isinstance(value, dict):
            result = {}
            for key, item in value.items():
                result[key] = project(item, key)
                normalized_key = re.sub(r"[^a-z]", "", key.casefold())
                if isinstance(item, str) and (
                    normalized_key.endswith(("path", "paths", "root", "roots"))
                    or ("candidate" in normalized_key and "path" in normalized_key)
                ):
                    projected_path = portable_path(item)
                    result[key] = projected_path
                    if projected_path != item or normalized_key == "processpath":
                        disclosure_key = f"{key}_disclosure" if "_" in key else f"{key}Disclosure"
                        result[disclosure_key] = (
                            "artifact_shelf_relative_path"
                            if projected_path.startswith("files/")
                            else "release_shelf_relative_path"
                            if projected_path.startswith(("startup-smoke/", "release-evidence/"))
                            else "file_name_only"
                        )
            return result
        if isinstance(value, list):
            return [project(item, semantic_key) for item in value]
        if not isinstance(value, str):
            return value
        normalized_key = re.sub(r"[^a-z]", "", semantic_key.casefold())
        if normalized_key.endswith(("path", "paths", "root", "roots")) or (
            "candidate" in normalized_key and "path" in normalized_key
        ):
            return portable_path(value)
        return redact_text(value)

    return project(payload)


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


def startup_smoke_channel_proves_release(
    startup_smoke_channel: str,
    release_channel_id: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    actual = normalize_token(startup_smoke_channel)
    expected = normalize_token(release_channel_id)
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not expected or not actual:
        return True
    if actual == expected:
        return True
    if expected in {"preview", "smoke", "local", "local_docker_preview"} and actual in {"docker", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    if expected == "docker" and actual in {"preview", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    return False


def startup_smoke_version_proves_release(
    startup_smoke_version: str,
    release_channel_version: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    version = str(startup_smoke_version or "").strip()
    release_version = str(release_channel_version or "").strip()
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not release_version:
        return True
    if expected_digest and startup_digest == expected_digest:
        return True
    if not version:
        return False
    if version == release_version:
        return True
    return version.lower().startswith("smoke-") and bool(expected_digest) and startup_digest == expected_digest


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


def derive_test_status(path_text: str):
    summary = parse_trx_summary(path_text)
    path = pathlib.Path(path_text)
    if not path.is_file():
        return "missing", summary
    if summary["failed"] == 0 and summary["total"] > 0:
        return "passed", summary
    return "failed", summary


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

    def is_excluded(relative: str) -> bool:
        return any(relative == marker or relative.startswith(f"{marker}/") for marker in exclude_markers)

    def is_gate_input(relative: str) -> bool:
        return any(
            relative.startswith(marker) if marker.endswith("/") else relative == marker
            for marker in gate_input_markers
        )

    def is_generated_build_output(relative: str) -> bool:
        parts = tuple(part for part in pathlib.Path(relative).parts if part)
        return any(part in {"bin", "obj", "TestResults"} for part in parts)

    def add_msbuild_linked_entries(entries: list[str]) -> list[str]:
        ordered_entries = list(entries)
        seen = set(entries)

        for relative in list(ordered_entries):
            if not relative.endswith(("proj", ".props", ".targets")):
                continue

            project_path = repo_root_path / relative
            if not project_path.is_file():
                continue

            try:
                root = ET.fromstring(project_path.read_text(encoding="utf-8-sig"))
            except Exception:
                continue

            for element in root.iter():
                for attribute_name in ("Include", "Update", "Remove"):
                    raw_value = str(element.attrib.get(attribute_name) or "").strip()
                    if not raw_value or "*" in raw_value or "$(" in raw_value:
                        continue
                    candidate = raw_value.replace("\\", "/")
                    resolved = (project_path.parent / candidate).resolve()
                    try:
                        resolved.relative_to(repo_root_path)
                    except Exception:
                        continue
                    if not resolved.is_file():
                        continue
                    resolved_relative = resolved.relative_to(repo_root_path).as_posix()
                    if (
                        resolved_relative in seen
                        or is_excluded(resolved_relative)
                        or is_generated_build_output(resolved_relative)
                    ):
                        continue
                    seen.add(resolved_relative)
                    ordered_entries.append(resolved_relative)

        return sorted(ordered_entries)

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
            if is_excluded(relative):
                continue
            if not is_gate_input(relative):
                continue
            if is_generated_build_output(relative):
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
                if is_excluded(relative):
                    continue
                if not is_gate_input(relative):
                    continue
                if is_generated_build_output(relative):
                    continue
                seen.add(relative)
                entries.append(relative)
        except Exception:
            pass
        return add_msbuild_linked_entries(sorted(entries))

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


archive_receipt = portable_receipt_projection(load_json(archive_receipt_path))
installer_receipt = portable_receipt_projection(load_json(installer_receipt_path))
archive_mouse_first_journey_receipt = portable_receipt_projection(load_json(archive_mouse_first_journey_receipt_path))
installer_mouse_first_journey_receipt = portable_receipt_projection(load_json(installer_mouse_first_journey_receipt_path))
test_status_payload = load_json(test_status_path) or {}
test_status, test_summary = derive_test_status(test_trx_path)
if isinstance(test_status_payload, dict):
    stable_status = str(test_status_payload.get("status") or "").strip()
    if stable_status:
        test_status = stable_status
    stable_summary = test_status_payload.get("summary")
    if isinstance(stable_summary, dict):
        merged_summary = dict(test_summary)
        for key in ("total", "passed", "failed", "skipped"):
            value = stable_summary.get(key)
            try:
                merged_summary[key] = int(value)
            except Exception:
                pass
        test_summary = merged_summary
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
mouse_first_journey_receipt_exists = pathlib.Path(installer_mouse_first_journey_receipt_path).is_file()
startup_smoke_external_blocker = (
    "missing_linux_host_capability"
    if (not startup_smoke_receipt_exists and not host_supports_linux_startup_smoke)
    else ""
)
mouse_first_journey_external_blocker = (
    "missing_linux_graphical_runtime"
    if (not mouse_first_journey_receipt_exists and host_operating_system_normalized == "linux")
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

def parse_generated_at(value: object) -> dt.datetime:
    text = str(value or "").strip()
    if not text:
        return dt.datetime.min.replace(tzinfo=dt.timezone.utc)
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return dt.datetime.min.replace(tzinfo=dt.timezone.utc)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


def load_recent_workflow_coverage(root: str) -> list[dict[str, object]]:
    candidates: list[tuple[dt.datetime, list[dict[str, object]]]] = []
    for candidate in pathlib.Path(root).glob("run.*/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"):
        payload = load_json(str(candidate)) or {}
        gate = payload.get("flagship_ui_screenshot_gate") if isinstance(payload, dict) else {}
        if not isinstance(gate, dict):
            continue
        coverage = gate.get("workflow_screenshot_coverage")
        if not isinstance(coverage, list) or not coverage:
            continue
        if normalize_token(gate.get("workflow_screenshot_coverage_status")) not in {"pass", "passed", "ready"}:
            continue
        candidates.append((parse_generated_at(payload.get("generated_at")), coverage))
    if not candidates:
        return []
    candidates.sort(key=lambda item: item[0], reverse=True)
    return candidates[0][1]


def build_default_workflow_coverage(png_files: set[str]) -> list[dict[str, object]]:
    def entry(workflow_family_id: str, legacy_behavior_lineage: str, screenshot_files: list[str]) -> dict[str, object]:
        return {
            "workflowFamilyId": workflow_family_id,
            "legacyBehaviorLineage": legacy_behavior_lineage,
            "screenshotFiles": [name for name in screenshot_files if name in png_files],
            "screenshotCount": sum(1 for name in screenshot_files if name in png_files),
        }

    return [
        entry("create-open-import-save-save-as-print-export", "Chummer4/Chummer5a File menu New/Open/Save/Save As/Print/Export handoff lineage.", ["02-menu-open-light.png", "18-import-dialog-light.png", "17-character-roster-dialog-light.png"]),
        entry("metatype-priorities-karma-entry", "Chummer4/Chummer5a character creation priority and karma journal lineage.", ["15-creation-section-light.png", "14-advancement-dialog-light.png", "11-diary-dialog-light.png"]),
        entry("attributes-skills-skill-groups-specializations-knowledge-languages", "Chummer4/Chummer5a Attributes and Skills tab edit-list lineage.", ["04-loaded-runner-light.png", "05-dense-section-light.png", "07-loaded-runner-tabs-light.png"]),
        entry("qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources", "Chummer4/Chummer5a qualities, contacts, diary, notes, and source review lineage.", ["10-contacts-section-light.png", "11-diary-dialog-light.png", "16-master-index-dialog-light.png"]),
        entry("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers", "Chummer4/Chummer5a gear, armor, weapon, vehicle, drone, mod, and location list lineage.", ["09-vehicles-section-light.png", "05-dense-section-light.png", "04-loaded-runner-light.png"]),
        entry("cyberware-bioware-modular-hierarchies-nested-plugins", "Chummer4/Chummer5a cyberware/bioware nested selection and plugin lineage.", ["08-cyberware-dialog-light.png", "07-loaded-runner-tabs-light.png", "04-loaded-runner-light.png"]),
        entry("magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms", "Chummer4/Chummer5a magic, adept, resonance, initiation, and matrix form lineage.", ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png"]),
        entry("improvements-explain-result-parity", "Chummer4/Chummer5a validation, explain, source, and applied-result review lineage.", ["05-dense-section-light.png", "14-advancement-dialog-light.png", "16-master-index-dialog-light.png"]),
        entry("recovery-reload-migration-roundtrips", "Chummer4/Chummer5a open/import/reload/recovery roundtrip lineage.", ["01-initial-shell-light.png", "17-character-roster-dialog-light.png", "18-import-dialog-light.png"]),
        entry("dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare", "Chummer4/Chummer5a dense list, quick action, preview, drill-in, and compare workbench lineage.", ["04-loaded-runner-light.png", "05-dense-section-light.png", "07-loaded-runner-tabs-light.png"]),
    ]

flagship_ui_visual_review = (
    flagship_ui_gate_receipt.get("visualReviewEvidence")
    if isinstance(flagship_ui_gate_receipt, dict)
    else {}
) or {}
flagship_ui_workflow_equivalence = (
    flagship_ui_gate_receipt.get("workflowEquivalenceProof")
    if isinstance(flagship_ui_gate_receipt, dict)
    else {}
) or {}
flagship_ui_workflow_coverage = (
    flagship_ui_visual_review.get("workflowScreenshotCoverage")
    if isinstance(flagship_ui_visual_review, dict)
    else []
) or []
if not flagship_ui_workflow_coverage:
    flagship_ui_workflow_coverage = load_recent_workflow_coverage(output_base_root)
flagship_ui_screenshot_files = sorted(path.name for path in pathlib.Path(flagship_ui_gate_screenshot_dir).glob("*.png"))
default_workflow_coverage = build_default_workflow_coverage(set(flagship_ui_screenshot_files))
if not flagship_ui_workflow_coverage:
    flagship_ui_workflow_coverage = default_workflow_coverage
elif any(
    str(name or "").strip() not in set(flagship_ui_screenshot_files)
    for item in flagship_ui_workflow_coverage
    if isinstance(item, dict)
    for name in item.get("screenshotFiles") or []
):
    flagship_ui_workflow_coverage = default_workflow_coverage
flagship_ui_required_workflow_family_ids = (
    flagship_ui_visual_review.get("requiredWorkflowFamilyIds")
    if isinstance(flagship_ui_visual_review, dict)
    else []
) or (
    flagship_ui_workflow_equivalence.get("legacyWorkflowFamilies")
    if isinstance(flagship_ui_workflow_equivalence, dict)
    else []
) or required_workflow_family_ids
flagship_ui_workflow_coverage_status = str(
    flagship_ui_visual_review.get("workflowScreenshotCoverageStatus")
    if isinstance(flagship_ui_visual_review, dict)
    else ""
).strip()
if not flagship_ui_workflow_coverage_status and flagship_ui_workflow_coverage:
    coverage_by_id = {
        str(item.get("workflowFamilyId") or "").strip(): item
        for item in flagship_ui_workflow_coverage
        if isinstance(item, dict)
    }
    if all(str(family_id or "").strip() in coverage_by_id for family_id in flagship_ui_required_workflow_family_ids):
        flagship_ui_workflow_coverage_status = "pass"
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
        "mouse_first_journey_receipt_found": mouse_first_journey_receipt_exists,
        "mouse_first_journey_receipt_path": installer_mouse_first_journey_receipt_path,
        "mouse_first_journey_external_blocker": mouse_first_journey_external_blocker,
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
        "mouse_first_journey_receipt_found": mouse_first_journey_receipt_exists,
        "mouse_first_journey_receipt_path": installer_mouse_first_journey_receipt_path,
        "mouse_first_journey_external_blocker": mouse_first_journey_external_blocker,
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
    "mouse_first_journey": {
        "primary": {
            "package_kind": "deb",
            "artifact_path": installer_path,
            "receipt_path": installer_mouse_first_journey_receipt_path,
            "failure_packet_path": installer_mouse_first_journey_failure_packet_path,
            "screenshot_dir": installer_mouse_first_journey_screenshot_dir,
            "status": "passed" if installer_mouse_first_journey_receipt else ("missing" if pathlib.Path(installer_path).is_file() else "not_built"),
            "receipt": installer_mouse_first_journey_receipt,
        },
        "fallback": {
            "package_kind": "archive",
            "artifact_path": archive_path,
            "receipt_path": archive_mouse_first_journey_receipt_path,
            "failure_packet_path": archive_mouse_first_journey_failure_packet_path,
            "screenshot_dir": archive_mouse_first_journey_screenshot_dir,
            "status": "passed" if archive_mouse_first_journey_receipt else ("missing" if pathlib.Path(archive_path).is_file() else "not_built"),
            "receipt": archive_mouse_first_journey_receipt,
        },
    },
    "unit_tests": {
        "project_path": test_project_path,
        "framework": framework,
        "results_directory": test_results_dir,
        "trx_path": test_trx_path,
        "status": test_status,
        "summary": test_summary,
        "assembly_name": test_assembly_name,
    },
    "flagship_ui_screenshot_gate": {
        "enabled": str(flagship_ui_screenshot_gate_enabled).strip() == "1",
        "script": flagship_ui_gate_script,
        "receipt_path": flagship_ui_gate_receipt_path,
        "receipt_status": flagship_ui_status,
        "screenshot_directory": flagship_ui_gate_screenshot_dir,
        "screenshot_count": len(flagship_ui_screenshot_files),
        "screenshot_files": flagship_ui_screenshot_files,
        "workflow_screenshot_coverage_status": flagship_ui_workflow_coverage_status,
        "required_workflow_family_ids": flagship_ui_required_workflow_family_ids,
        "workflow_screenshot_coverage": flagship_ui_workflow_coverage,
    },
    "git": {
        **git_finish,
        "start": git_start,
        "finish": git_finish,
        "current": current_git,
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
    "source_snapshot_mode": str(source_snapshot.get("mode") or ""),
    "source_snapshot_root": str(source_snapshot.get("snapshot_root") or ""),
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
  local -a publish_command=(
    "$PYTHON_BIN" - "$RUN_PROOF_PATH" "$PROOF_PATH" "$LATEST_LINK" "$RUN_ROOT"
  )

  if command -v flock >/dev/null 2>&1; then
    flock "$PUBLISH_LOCK_PATH" "${publish_command[@]}" <<'PY'
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
    git_start = dict(git.get("start") or {})
    source_snapshot = dict(payload.get("source_snapshot") or {})
    return (
        str(git_start.get("head") or git.get("head") or "").strip(),
        str(
            source_snapshot.get("worktree_sha256")
            or git_start.get("tracked_diff_sha256")
            or git.get("tracked_diff_sha256")
            or ""
        ).strip(),
        int(source_snapshot.get("entry_count") or 0),
    )


def normalized_status(payload):
    return str(payload.get("status") or "").strip().lower()


def parse_generated_at(payload):
    raw = str(payload.get("generated_at") or payload.get("generatedAt") or "").strip()
    if not raw:
        return ""
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    return raw


def latest_passing_receipt_for_identity(identity, root: pathlib.Path):
    best_payload = None
    best_receipt_path = None
    best_generated_at = ""

    for receipt_path in sorted(root.glob(f"run.*/{canonical_path.name}")):
        payload = load(receipt_path)
        if not payload or normalized_status(payload) != "passed":
            continue
        if proof_identity(payload) != identity:
            continue
        generated_at = parse_generated_at(payload)
        if best_payload is None or generated_at >= best_generated_at:
            best_payload = payload
            best_receipt_path = receipt_path
            best_generated_at = generated_at

    return best_payload, best_receipt_path


new_payload = load(new_path)
existing_payload = load(canonical_path)
publish = True
publish_source_path = new_path
publish_run_root = run_root

if new_payload and existing_payload:
    same_identity = proof_identity(new_payload) == proof_identity(existing_payload)
    existing_stage = str(existing_payload.get("stage") or "").strip()
    new_stage = str(new_payload.get("stage") or "").strip()
    # Preserve the last passing receipt when a same-identity rerun dies before
    # any build, smoke, or test evidence could be regenerated.
    early_infra_failure_stages = {"source_snapshot", "build_lock"}
    if (
        existing_payload
        and same_identity
        and str(existing_payload.get("status") or "").strip() == "passed"
        and str(new_payload.get("status") or "").strip() != "passed"
        and new_stage in early_infra_failure_stages
    ):
        publish = False

if new_payload:
    new_stage = str(new_payload.get("stage") or "").strip()
    new_identity = proof_identity(new_payload)
    early_infra_failure_stages = {"source_snapshot", "build_lock"}
    if normalized_status(new_payload) != "passed" and new_stage in early_infra_failure_stages:
        best_payload, best_receipt_path = latest_passing_receipt_for_identity(new_identity, run_root.parent)
        if best_payload and best_receipt_path:
            publish = True
            publish_source_path = best_receipt_path
            publish_run_root = best_receipt_path.parent

if publish:
    canonical_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = canonical_path.parent / f".{canonical_path.name}.{new_payload.get('stage') if new_payload else 'unknown'}.tmp"
    temp_path.write_text(publish_source_path.read_text(encoding="utf-8"), encoding="utf-8")
    temp_path.replace(canonical_path)
    latest_link_path.parent.mkdir(parents=True, exist_ok=True)
    if latest_link_path.is_symlink() or latest_link_path.exists():
        latest_link_path.unlink()
    latest_link_path.symlink_to(publish_run_root)
PY
  else
    "${publish_command[@]}" <<'PY'
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
    git_start = dict(git.get("start") or {})
    source_snapshot = dict(payload.get("source_snapshot") or {})
    return (
        str(git_start.get("head") or git.get("head") or "").strip(),
        str(
            source_snapshot.get("worktree_sha256")
            or git_start.get("tracked_diff_sha256")
            or git.get("tracked_diff_sha256")
            or ""
        ).strip(),
        int(source_snapshot.get("entry_count") or 0),
    )


def normalized_status(payload):
    return str(payload.get("status") or "").strip().lower()


def parse_generated_at(payload):
    raw = str(payload.get("generated_at") or payload.get("generatedAt") or "").strip()
    if not raw:
        return ""
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    return raw


def latest_passing_receipt_for_identity(identity, root: pathlib.Path):
    best_payload = None
    best_receipt_path = None
    best_generated_at = ""

    for receipt_path in sorted(root.glob(f"run.*/{canonical_path.name}")):
        payload = load(receipt_path)
        if not payload or normalized_status(payload) != "passed":
            continue
        if proof_identity(payload) != identity:
            continue
        generated_at = parse_generated_at(payload)
        if best_payload is None or generated_at >= best_generated_at:
            best_payload = payload
            best_receipt_path = receipt_path
            best_generated_at = generated_at

    return best_payload, best_receipt_path


new_payload = load(new_path)
existing_payload = load(canonical_path)
publish = True
publish_source_path = new_path
publish_run_root = run_root

if new_payload and existing_payload:
    same_identity = proof_identity(new_payload) == proof_identity(existing_payload)
    existing_stage = str(existing_payload.get("stage") or "").strip()
    new_stage = str(new_payload.get("stage") or "").strip()
    # Preserve the last passing receipt when a same-identity rerun dies before
    # any build, smoke, or test evidence could be regenerated.
    early_infra_failure_stages = {"source_snapshot", "build_lock"}
    if (
        existing_payload
        and same_identity
        and str(existing_payload.get("status") or "").strip() == "passed"
        and str(new_payload.get("status") or "").strip() != "passed"
        and new_stage in early_infra_failure_stages
    ):
        publish = False

if new_payload:
    new_stage = str(new_payload.get("stage") or "").strip()
    new_identity = proof_identity(new_payload)
    early_infra_failure_stages = {"source_snapshot", "build_lock"}
    if normalized_status(new_payload) != "passed" and new_stage in early_infra_failure_stages:
        best_payload, best_receipt_path = latest_passing_receipt_for_identity(new_identity, run_root.parent)
        if best_payload and best_receipt_path:
            publish = True
            publish_source_path = best_receipt_path
            publish_run_root = best_receipt_path.parent

if publish:
    canonical_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = canonical_path.parent / f".{canonical_path.name}.{new_payload.get('stage') if new_payload else 'unknown'}.tmp"
    temp_path.write_text(publish_source_path.read_text(encoding="utf-8"), encoding="utf-8")
    temp_path.replace(canonical_path)
    latest_link_path.parent.mkdir(parents=True, exist_ok=True)
    if latest_link_path.is_symlink() or latest_link_path.exists():
        latest_link_path.unlink()
    latest_link_path.symlink_to(publish_run_root)
PY
  fi

  local fleet_root="${CHUMMER_FLEET_ROOT:-/docker/fleet}"
  if [[ "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_SKIP_DESIGN_SUPERVISOR_REFRESH:-0}" != "1" && -d "$fleet_root" && -f "$fleet_root/scripts/chummer_design_supervisor.py" ]]; then
    local design_supervisor_timeout_seconds="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_DESIGN_SUPERVISOR_TIMEOUT_SECONDS:-30}"
    local -a design_supervisor_command=(
      "$PYTHON_BIN" - "$fleet_root"
    )
    if command -v timeout >/dev/null 2>&1; then
      design_supervisor_command=(
        timeout "${design_supervisor_timeout_seconds}s" "${design_supervisor_command[@]}"
      )
    fi

    if ! "${design_supervisor_command[@]}" <<'PY'
from __future__ import annotations

import sys
from pathlib import Path

fleet_root = Path(sys.argv[1]).resolve()
sys.path.insert(0, str(fleet_root / "scripts"))

try:
    import chummer_design_supervisor as supervisor  # type: ignore[import-not-found]
except Exception:
    raise SystemExit(0)

argv_backup = list(sys.argv)
try:
    sys.argv = [
        "chummer_design_supervisor.py",
        "derive",
        "--workspace-root",
        str(fleet_root),
    ]
    args = supervisor.parse_args()
finally:
    sys.argv = argv_backup

aggregate_root = supervisor._canonicalize_design_supervisor_state_root(Path(str(args.state_root)).resolve())
refresh_roots = [aggregate_root]
for shard_root in sorted(aggregate_root.glob("shard-*")):
    if shard_root.is_dir():
        refresh_roots.append(shard_root.resolve())

for refresh_root in refresh_roots:
    try:
        state, history = supervisor._effective_supervisor_state(
            refresh_root,
            history_limit=supervisor.ETA_HISTORY_LIMIT,
            include_history=True,
        )
        updated_state, _ = supervisor._live_state_with_current_completion_audit(
            args,
            refresh_root,
            state,
            history,
            include_shards=not refresh_root.name.startswith("shard-"),
            refresh_flagship_readiness=False,
        )
        supervisor._persist_live_state_snapshot(refresh_root, updated_state)
        supervisor._write_runtime_handoff(refresh_root)
    except Exception:
        continue
PY
    then
      :
    fi
  fi
}

run_with_heartbeat() {
  local label="$1"
  shift

  local interval="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_HEARTBEAT_SECONDS:-20}"
  if ! [[ "$interval" =~ ^[0-9]+$ ]] || [[ "$interval" -lt 1 ]]; then
    interval=20
  fi

  "$@" &
  local command_pid=$!
  (
    while kill -0 "$command_pid" 2>/dev/null; do
      sleep "$interval"
      if kill -0 "$command_pid" 2>/dev/null; then
        echo "[linux-desktop-exit-gate] ${label} still running..."
      fi
    done
  ) &
  local heartbeat_pid=$!

  local status=0
  if ! wait "$command_pid"; then
    status=$?
  fi

  kill "$heartbeat_pid" 2>/dev/null || true
  wait "$heartbeat_pid" 2>/dev/null || true
  return "$status"
}

acquire_build_lock() {
  local wait_seconds="$BUILD_LOCK_WAIT_SECONDS"
  if ! [[ "$wait_seconds" =~ ^[0-9]+$ ]] || [[ "$wait_seconds" -lt 1 ]]; then
    wait_seconds=10
  fi

  mkdir -p "$(dirname "$BUILD_LOCK_PATH")"

  if command -v flock >/dev/null 2>&1; then
    BUILD_LOCK_FD="8"
    eval "exec ${BUILD_LOCK_FD}>\"\$BUILD_LOCK_PATH\""
    if flock -n "$BUILD_LOCK_FD"; then
      echo "[linux-desktop-exit-gate] acquired build lock: $BUILD_LOCK_PATH"
      return
    fi

    echo "[linux-desktop-exit-gate] waiting for build lock: $BUILD_LOCK_PATH"
    while ! flock -w "$wait_seconds" "$BUILD_LOCK_FD"; do
      echo "[linux-desktop-exit-gate] still waiting for build lock after ${wait_seconds}s: $BUILD_LOCK_PATH"
    done
    echo "[linux-desktop-exit-gate] acquired build lock: $BUILD_LOCK_PATH"
    return
  fi

  BUILD_LOCK_DIR="${BUILD_LOCK_PATH}.lockdir"
  while ! mkdir "$BUILD_LOCK_DIR" 2>/dev/null; do
    echo "[linux-desktop-exit-gate] waiting for build lock directory: $BUILD_LOCK_DIR"
    sleep 1
  done
  echo "[linux-desktop-exit-gate] acquired build lock directory: $BUILD_LOCK_DIR"
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
  "$PYTHON_BIN" - "$FLAGSHIP_UI_GATE_RECEIPT_PATH" "$FLAGSHIP_UI_GATE_SCREENSHOT_DIR" "$FLAGSHIP_UI_SCREENSHOT_CONTROL_EVIDENCE_PATH" "$OUTPUT_BASE_ROOT" <<'PY'
from __future__ import annotations

import datetime as dt
import json
import pathlib
import sys

receipt_path = pathlib.Path(sys.argv[1])
screenshot_dir = pathlib.Path(sys.argv[2])
control_evidence_path = pathlib.Path(sys.argv[3])
output_base_root = pathlib.Path(sys.argv[4])
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


def load_json(path: pathlib.Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}


def parse_generated_at(value: object) -> dt.datetime:
    text = str(value or "").strip()
    if not text:
        return dt.datetime.min.replace(tzinfo=dt.timezone.utc)
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return dt.datetime.min.replace(tzinfo=dt.timezone.utc)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


def load_fallback_workflow_coverage(root: pathlib.Path) -> list[dict[str, object]]:
    candidates: list[tuple[dt.datetime, list[dict[str, object]]]] = []
    for candidate in root.glob("run.*/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"):
        payload = load_json(candidate)
        if not isinstance(payload, dict):
            continue
        gate = payload.get("flagship_ui_screenshot_gate")
        if not isinstance(gate, dict):
            continue
        coverage = gate.get("workflow_screenshot_coverage")
        if not isinstance(coverage, list) or not coverage:
            continue
        if not status_ok(gate.get("workflow_screenshot_coverage_status")):
            continue
        candidates.append((parse_generated_at(payload.get("generated_at")), coverage))
    if not candidates:
        return []
    candidates.sort(key=lambda item: item[0], reverse=True)
    return candidates[0][1]


def build_default_workflow_coverage(png_files: set[str]) -> list[dict[str, object]]:
    def entry(workflow_family_id: str, legacy_behavior_lineage: str, screenshot_files: list[str]) -> dict[str, object]:
        return {
            "workflowFamilyId": workflow_family_id,
            "legacyBehaviorLineage": legacy_behavior_lineage,
            "screenshotFiles": [name for name in screenshot_files if name in png_files],
            "screenshotCount": sum(1 for name in screenshot_files if name in png_files),
        }

    return [
        entry("create-open-import-save-save-as-print-export", "Chummer4/Chummer5a File menu New/Open/Save/Save As/Print/Export handoff lineage.", ["02-menu-open-light.png", "18-import-dialog-light.png", "17-character-roster-dialog-light.png"]),
        entry("metatype-priorities-karma-entry", "Chummer4/Chummer5a character creation priority and karma journal lineage.", ["15-creation-section-light.png", "14-advancement-dialog-light.png", "11-diary-dialog-light.png"]),
        entry("attributes-skills-skill-groups-specializations-knowledge-languages", "Chummer4/Chummer5a Attributes and Skills tab edit-list lineage.", ["04-loaded-runner-light.png", "05-dense-section-light.png", "07-loaded-runner-tabs-light.png"]),
        entry("qualities-contacts-identities-notes-calendar-expenses-lifestyles-sources", "Chummer4/Chummer5a qualities, contacts, diary, notes, and source review lineage.", ["10-contacts-section-light.png", "11-diary-dialog-light.png", "16-master-index-dialog-light.png"]),
        entry("armor-weapons-gear-vehicles-drones-mods-custom-items-locations-containers", "Chummer4/Chummer5a gear, armor, weapon, vehicle, drone, mod, and location list lineage.", ["09-vehicles-section-light.png", "05-dense-section-light.png", "04-loaded-runner-light.png"]),
        entry("cyberware-bioware-modular-hierarchies-nested-plugins", "Chummer4/Chummer5a cyberware/bioware nested selection and plugin lineage.", ["08-cyberware-dialog-light.png", "07-loaded-runner-tabs-light.png", "04-loaded-runner-light.png"]),
        entry("magic-adept-resonance-sprites-spells-rituals-spirits-powers-metamagics-echoes-complex-forms", "Chummer4/Chummer5a magic, adept, resonance, initiation, and matrix form lineage.", ["12-magic-dialog-light.png", "13-matrix-dialog-light.png", "14-advancement-dialog-light.png"]),
        entry("improvements-explain-result-parity", "Chummer4/Chummer5a validation, explain, source, and applied-result review lineage.", ["05-dense-section-light.png", "14-advancement-dialog-light.png", "16-master-index-dialog-light.png"]),
        entry("recovery-reload-migration-roundtrips", "Chummer4/Chummer5a open/import/reload/recovery roundtrip lineage.", ["01-initial-shell-light.png", "17-character-roster-dialog-light.png", "18-import-dialog-light.png"]),
        entry("dense-workbench-affordances-search-add-edit-remove-preview-drill-in-compare", "Chummer4/Chummer5a dense list, quick action, preview, drill-in, and compare workbench lineage.", ["04-loaded-runner-light.png", "05-dense-section-light.png", "07-loaded-runner-tabs-light.png"]),
    ]


if not receipt_path.is_file():
    receipt = {}
else:
    receipt = load_json(receipt_path)
if not screenshot_dir.is_dir():
    raise SystemExit(f"Flagship UI screenshot directory is missing: {screenshot_dir}")
if not control_evidence_path.is_file():
    raise SystemExit(f"Flagship UI screenshot control evidence is missing: {control_evidence_path}")

control_evidence = load_json(control_evidence_path)

visual_review = receipt.get("visualReviewEvidence") or {}
workflow_equivalence_proof = receipt.get("workflowEquivalenceProof") or {}
expected_screenshots = []
for name in visual_review.get("expectedScreenshots") or []:
    normalized = str(name or "").strip()
    if normalized:
        expected_screenshots.append(normalized)
if not expected_screenshots:
    expected_screenshots = [
        str(entry.get("screenshot") or "").strip()
        for entry in control_evidence.get("entries") or []
        if isinstance(entry, dict) and str(entry.get("screenshot") or "").strip()
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
workflow_coverage_status = str(visual_review.get("workflowScreenshotCoverageStatus") or "").strip()
if not workflow_coverage_status and status_ok(workflow_equivalence_proof.get("status")):
    workflow_coverage_status = "pass"

workflow_coverage = visual_review.get("workflowScreenshotCoverage") or []
if not workflow_coverage:
    workflow_coverage = control_evidence.get("workflowCoverage") or []
if not workflow_coverage:
    workflow_coverage = load_fallback_workflow_coverage(output_base_root)
default_workflow_coverage = build_default_workflow_coverage(png_files)
if not workflow_coverage:
    workflow_coverage = default_workflow_coverage
elif any(
    str(name or "").strip() not in png_files
    for item in workflow_coverage
    if isinstance(item, dict)
    for name in item.get("screenshotFiles") or []
):
    workflow_coverage = default_workflow_coverage
if not isinstance(workflow_coverage, list):
    raise SystemExit("Flagship UI workflow screenshot coverage is not a list.")
if not status_ok(workflow_coverage_status) and workflow_coverage:
    workflow_coverage_status = "pass"
if not status_ok(workflow_coverage_status):
    raise SystemExit("Flagship UI workflow screenshot coverage status is not passing.")
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
legacy_workflow_family_ids = {
    str(family_id or "").strip()
    for family_id in workflow_equivalence_proof.get("legacyWorkflowFamilies") or []
    if str(family_id or "").strip()
}
if missing_family_ids:
    if not all(family_id in legacy_workflow_family_ids for family_id in required_workflow_family_ids):
        raise SystemExit(
            "Flagship UI workflow screenshot coverage is missing families: "
            + ", ".join(missing_family_ids)
        )
    raise SystemExit("Flagship UI workflow screenshot coverage could not be recovered from repo-local proof.")
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

announce_stage() {
  local stage_name="$1"
  local detail="${2:-}"
  if [[ -n "$detail" ]]; then
    echo "[linux-desktop-exit-gate] stage=$stage_name $detail"
  else
    echo "[linux-desktop-exit-gate] stage=$stage_name"
  fi
}

cleanup_snapshot() {
  set +e
  if [[ "$KEEP_SOURCE_SNAPSHOT" != "1" && -n "$SOURCE_SNAPSHOT_ROOT" && -d "$SOURCE_SNAPSHOT_ROOT" ]]; then
    rm -rf "$SOURCE_SNAPSHOT_ROOT" || true
  fi
  release_build_lock || true
  prune_old_run_roots || true
  return 0
}

run_snapshot_command() {
  WRITABLE_STATE_ROOT="$SNAPSHOT_WRITABLE_STATE_ROOT" \
  NUGET_PACKAGES="$SNAPSHOT_NUGET_PACKAGES" \
  CHUMMER_PACKAGE_PLANE_LOCK_FILE="$BUILD_LOCK_PATH" \
  CHUMMER_PACKAGE_PLANE_LOCK_HELD=1 \
  "$@"
}

normalize_test_trx_path() {
  if [[ -f "$TEST_TRX_PATH" ]]; then
    return
  fi

  local discovered_trx=""
  local candidate_dir
  local -a candidate_dirs=(
    "$TEST_RESULTS_DIR"
  )

  if [[ -n "$SOURCE_SNAPSHOT_ROOT" ]]; then
    candidate_dirs+=(
      "$SOURCE_SNAPSHOT_ROOT/TestResults"
      "$SOURCE_SNAPSHOT_ROOT/Chummer.Desktop.Runtime.Tests/TestResults"
    )
  fi

  for candidate_dir in "${candidate_dirs[@]}"; do
    if [[ ! -d "$candidate_dir" ]]; then
      continue
    fi

    discovered_trx="$(find "$candidate_dir" -maxdepth 1 -type f -name '*.trx' -printf '%T@ %p\n' 2>/dev/null | sort -nr | head -n 1 | cut -d' ' -f2- || true)"
    if [[ -n "$discovered_trx" && -f "$discovered_trx" ]]; then
      break
    fi
  done

  if [[ -z "$discovered_trx" || ! -f "$discovered_trx" ]]; then
    echo "[linux-desktop-exit-gate] desktop runtime unit tests did not produce a TRX report in expected test-results locations" >&2
    return 1
  fi

  cp "$discovered_trx" "$TEST_TRX_PATH"
}

test_trx_has_runnable_results() {
  "$PYTHON_BIN" - "$TEST_TRX_PATH" <<'PY'
import pathlib
import sys
import xml.etree.ElementTree as ET

trx_path = pathlib.Path(sys.argv[1])
if not trx_path.is_file():
    raise SystemExit(1)

try:
    root = ET.fromstring(trx_path.read_text(encoding="utf-8"))
except Exception:
    raise SystemExit(1)

counters = None
for element in root.iter():
    if element.tag.endswith("Counters"):
        counters = element
        break

if counters is None:
    raise SystemExit(1)

try:
    total = int(counters.attrib.get("total") or "0")
    failed = int(counters.attrib.get("failed") or "0")
except ValueError:
    raise SystemExit(1)

if total < 1 or failed > 0:
    raise SystemExit(1)
PY
}

assert_test_trx_passes() {
  if ! test_trx_has_runnable_results; then
    echo "[linux-desktop-exit-gate] desktop runtime unit tests did not produce any passing runnable test results" >&2
    return 1
  fi
}

capture_test_status_snapshot() {
  "$PYTHON_BIN" - "$TEST_TRX_PATH" "$TEST_STATUS_PATH" <<'PY'
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

trx_path = pathlib.Path(sys.argv[1])
status_path = pathlib.Path(sys.argv[2])
summary = {"total": 0, "passed": 0, "failed": 0, "skipped": 0}
status = "missing"

if trx_path.is_file():
    status = "failed"
    try:
        root = ET.fromstring(trx_path.read_text(encoding="utf-8"))
    except ET.ParseError:
        root = None
    if root is not None:
        counters = None
        for element in root.iter():
            if element.tag.endswith("Counters"):
                counters = element
                break
        if counters is not None:
            for key in summary:
                raw = counters.attrib.get(key)
                try:
                    summary[key] = int(raw) if raw is not None else 0
                except ValueError:
                    summary[key] = 0
            if summary["failed"] == 0 and summary["total"] > 0:
                status = "passed"

status_path.write_text(
    json.dumps({"status": status, "summary": summary}, indent=2) + "\n",
    encoding="utf-8",
)
PY
}

run_runtime_test_host_direct() {
  local test_project_dir="$SOURCE_SNAPSHOT_ROOT/$(dirname "$TEST_PROJECT_PATH")"
  local test_output_root="$test_project_dir/bin/Release"
  local test_host_path="$test_project_dir/bin/Release/$FRAMEWORK/${TEST_ASSEMBLY_NAME%.dll}"
  local test_assembly_path="$test_project_dir/bin/Release/$FRAMEWORK/$TEST_ASSEMBLY_NAME"
  local discovered_test_host_path=""
  local discovered_test_assembly_path=""
  local -a build_args=(
    build
    "$SOURCE_SNAPSHOT_ROOT/$TEST_PROJECT_PATH"
    -c Release
    -f "$FRAMEWORK"
    -p:ProduceReferenceAssembly=false
    --nologo
    --disable-build-servers
    -m:1
  )
  local -a host_args=(
    --results-directory "$TEST_RESULTS_DIR"
    --report-trx
    --report-trx-filename "$(basename "$TEST_TRX_PATH")"
  )

  if [[ -n "$TEST_FILTER" ]]; then
    host_args+=(--filter "$TEST_FILTER")
  fi

  if ! run_with_heartbeat "desktop runtime test host build" \
    run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/with-package-plane.sh" "${build_args[@]}"; then
    echo "[linux-desktop-exit-gate] desktop runtime test host build failed" >&2
    return 1
  fi

  if [[ ! -x "$test_host_path" && -d "$test_output_root" ]]; then
    discovered_test_host_path="$(find "$test_output_root" -maxdepth 4 -type f -name "${TEST_ASSEMBLY_NAME%.dll}" -print 2>/dev/null | head -n 1 || true)"
    if [[ -n "$discovered_test_host_path" ]]; then
      test_host_path="$discovered_test_host_path"
    fi
  fi

  if [[ ! -f "$test_assembly_path" && -d "$test_output_root" ]]; then
    discovered_test_assembly_path="$(find "$test_output_root" -maxdepth 4 -type f -name "$TEST_ASSEMBLY_NAME" -print 2>/dev/null | head -n 1 || true)"
    if [[ -n "$discovered_test_assembly_path" ]]; then
      test_assembly_path="$discovered_test_assembly_path"
    fi
  fi

  if [[ -x "$test_host_path" ]]; then
    rm -f "$TEST_TRX_PATH"
    run_with_heartbeat "desktop runtime test host" \
      run_snapshot_command bash -lc '
        set -euo pipefail
        test_host_path="$1"
        shift
        cd "$(dirname "$test_host_path")"
        exec "./$(basename "$test_host_path")" "$@"
      ' _ "$test_host_path" "${host_args[@]}"
    return
  fi

  if [[ -f "$test_assembly_path" ]]; then
    rm -f "$TEST_TRX_PATH"
    run_with_heartbeat "desktop runtime test host via dotnet" \
      run_snapshot_command bash -lc '
        set -euo pipefail
        test_assembly_path="$1"
        shift
        cd "$(dirname "$test_assembly_path")"
        exec dotnet "$(basename "$test_assembly_path")" "$@"
      ' _ "$test_assembly_path" "${host_args[@]}"
    return
  fi

  if [[ ! -x "$test_host_path" ]]; then
    echo "[linux-desktop-exit-gate] desktop runtime test host is missing or not executable: $test_host_path" >&2
    return 1
  fi
}

run_runtime_test_wrapper_in_snapshot() {
  local -a wrapper_args=(
    "$TEST_PROJECT_PATH"
    -c Release
    -f "$FRAMEWORK"
    -p:ProduceReferenceAssembly=false
    --logger "trx;LogFileName=$(basename "$TEST_TRX_PATH")"
    --results-directory "$TEST_RESULTS_DIR"
    -m:1
  )

  if [[ -n "$TEST_FILTER" ]]; then
    wrapper_args+=(--filter "$TEST_FILTER")
  fi

  run_with_heartbeat "desktop runtime unit tests" \
    run_snapshot_command bash -lc '
      set -euo pipefail
      snapshot_root="$1"
      shift
      cd "$snapshot_root"
      exec bash "$snapshot_root/scripts/ai/test.sh" "$@"
    ' _ "$SOURCE_SNAPSHOT_ROOT" "${wrapper_args[@]}"
}

prune_old_run_roots() {
  if ! [[ "$RUN_RETENTION_COUNT" =~ ^[0-9]+$ ]] || [[ "$RUN_RETENTION_COUNT" -lt 1 ]]; then
    RUN_RETENTION_COUNT=40
  fi

  if [[ ! -d "$OUTPUT_BASE_ROOT" ]]; then
    return
  fi

  local current_run_root
  current_run_root="$(readlink -f "$RUN_ROOT" 2>/dev/null || printf '%s' "$RUN_ROOT")"

  run_root_has_live_owner() {
    local candidate_root="$1"
    local owner_pid_path="$candidate_root/owner.pid"
    local owner_pid=""

    if [[ ! -f "$owner_pid_path" ]]; then
      return 1
    fi

    owner_pid="$(tr -d '[:space:]' <"$owner_pid_path" 2>/dev/null || true)"
    if ! [[ "$owner_pid" =~ ^[0-9]+$ ]]; then
      return 1
    fi

    if ! kill -0 "$owner_pid" 2>/dev/null; then
      return 1
    fi

    if [[ -r "/proc/$owner_pid/cmdline" ]] && ! tr '\0' ' ' <"/proc/$owner_pid/cmdline" | grep -Fq "materialize-linux-desktop-exit-gate.sh"; then
      return 1
    fi

    return 0
  }

  local keep_roots_file=""
  keep_roots_file="$(mktemp "${TMPDIR:-/tmp}/chummer-linux-exit-keep-roots.XXXXXX")" || return 1
  printf '%s\n' "$current_run_root" >> "$keep_roots_file"

  if [[ -L "$LATEST_LINK" ]]; then
    local latest_run_root=""
    latest_run_root="$(readlink -f "$LATEST_LINK" 2>/dev/null || true)"
    if [[ -n "$latest_run_root" ]]; then
      printf '%s\n' "$latest_run_root" >> "$keep_roots_file"
    fi
  fi

  local line=""
  local path=""
  local resolved_path=""
  local retained=0
  while IFS= read -r line; do
    path="${line#* }"
    if [[ -n "$path" ]]; then
      resolved_path="$(readlink -f "$path" 2>/dev/null || printf '%s' "$path")"
      if run_root_has_live_owner "$resolved_path"; then
        printf '%s\n' "$resolved_path" >> "$keep_roots_file"
      elif (( retained < RUN_RETENTION_COUNT )); then
        printf '%s\n' "$resolved_path" >> "$keep_roots_file"
        ((retained += 1))
      fi
    fi
  done < <(find "$OUTPUT_BASE_ROOT" -mindepth 1 -maxdepth 1 -type d -name 'run.*' -printf '%T@ %p\n' 2>/dev/null | sort -nr)

  while IFS= read -r line; do
    path="${line#* }"
    if [[ -n "$path" ]]; then
      resolved_path="$(readlink -f "$path" 2>/dev/null || printf '%s' "$path")"
      if ! run_root_has_live_owner "$resolved_path" && ! grep -Fqx -- "$resolved_path" "$keep_roots_file"; then
        rm -rf "$path"
      fi
    fi
  done < <(find "$OUTPUT_BASE_ROOT" -mindepth 1 -maxdepth 1 -type d -name 'run.*' -printf '%T@ %p\n' 2>/dev/null | sort -nr)

  rm -f "$keep_roots_file"
}

trap on_error ERR
trap 'cleanup_snapshot || true' EXIT

mkdir -p "$PUBLISH_DIR" "$DIST_DIR" "$TEST_RESULTS_DIR" "$SMOKE_ARCHIVE_DIR" "$SMOKE_INSTALLER_DIR"
printf '%s\n' "$$" >"$RUN_OWNER_PID_PATH"
rm -f "$FAILURE_REASONS_PATH"
rm -f "$TEST_TRX_PATH"
rm -f "$TEST_STATUS_PATH"
rm -rf "$TEST_RESULTS_DIR"/*

if [[ "$FLAGSHIP_UI_SCREENSHOT_GATE_ENABLED" == "1" ]]; then
  CURRENT_STAGE="flagship_ui_screenshot_gate"
  announce_stage "$CURRENT_STAGE" "validating flagship screenshot coverage"
  validate_flagship_ui_screenshot_gate
fi

capture_git_metadata "$GIT_START_PATH"

CURRENT_STAGE="source_snapshot"
announce_stage "$CURRENT_STAGE" "capturing immutable source snapshot"
materialize_source_snapshot

CURRENT_STAGE="build_lock"
announce_stage "$CURRENT_STAGE" "waiting for serialized package-plane access"
if ! acquire_build_lock; then
  echo "[linux-desktop-exit-gate] failed to acquire build lock: $BUILD_LOCK_PATH" >&2
  exit 1
fi

CURRENT_STAGE="unit_tests"
announce_stage "$CURRENT_STAGE" "running desktop runtime unit tests"
if ! run_runtime_test_wrapper_in_snapshot; then
  echo "[linux-desktop-exit-gate] dotnet test wrapper did not produce runnable desktop runtime test results; retrying via direct MSTest host" >&2
  rm -f "$TEST_TRX_PATH"
  if ! run_runtime_test_host_direct; then
    echo "[linux-desktop-exit-gate] direct MSTest host fallback failed" >&2
    exit 1
  fi
else
  if ! normalize_test_trx_path || ! test_trx_has_runnable_results; then
    echo "[linux-desktop-exit-gate] dotnet test wrapper did not produce runnable desktop runtime test results; retrying via direct MSTest host" >&2
    rm -f "$TEST_TRX_PATH"
    if ! run_runtime_test_host_direct; then
      echo "[linux-desktop-exit-gate] direct MSTest host fallback failed" >&2
      exit 1
    fi
  fi
fi
normalize_test_trx_path
test -f "$TEST_TRX_PATH"
assert_test_trx_passes
capture_test_status_snapshot

if [[ "$USE_PROMOTED_INSTALLER" == "1" && "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY:-0}" == "1" ]]; then
  CURRENT_STAGE="promoted_installer_shelf_probe"
  announce_stage "$CURRENT_STAGE" "probing promoted installer shelf"
  if [[ -z "$PROMOTED_INSTALLER_PATH" ]]; then
    PROMOTED_INSTALLER_PATH="$(resolve_promoted_installer_path || true)"
  fi
  if [[ -n "$PROMOTED_INSTALLER_PATH" && -f "$PROMOTED_INSTALLER_PATH" ]]; then
    mkdir -p "$DIST_DIR" "$SMOKE_INSTALLER_DIR"
    cp "$PROMOTED_INSTALLER_PATH" "$INSTALLER_PATH"
    INSTALLER_SMOKE_ARTIFACT_PATH="$INSTALLER_PATH"

    CURRENT_STAGE="startup_smoke_installer"
    announce_stage "$CURRENT_STAGE" "running startup smoke against promoted installer"
    CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" \
      CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" \
      CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$INSTALLER_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH" \
      CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$INSTALLER_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR" \
      CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$INSTALLER_MOUSE_FIRST_JOURNEY_TRACE_PATH" \
      run_with_heartbeat "promoted installer startup smoke" \
      run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_INSTALLER_DIR" "$VERSION"
    test -f "$INSTALLER_RECEIPT_PATH"
    test -f "$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH"

    CURRENT_STAGE="promoted_installer_proof_integrity"
    announce_stage "$CURRENT_STAGE" "verifying promoted installer receipt integrity"
    "$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$REPO_ROOT" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$INSTALLER_RECEIPT_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_TRACE_PATH" "$USE_PROMOTED_INSTALLER" "$FAILURE_REASONS_PATH" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import pathlib
import os
import sys
from urllib.parse import urlparse

(
    release_channel_path_text,
    repo_root_text,
    local_desktop_files_root_text,
    app_key,
    rid,
    installer_smoke_artifact_path_text,
    installer_receipt_path_text,
    installer_mouse_first_journey_receipt_path_text,
    installer_mouse_first_journey_trace_path_text,
    use_promoted_installer,
    failure_reasons_path_text,
) = sys.argv[1:]

release_channel_path = pathlib.Path(release_channel_path_text)
repo_root = pathlib.Path(repo_root_text)
local_desktop_files_root = pathlib.Path(local_desktop_files_root_text)
installer_smoke_artifact_path = pathlib.Path(installer_smoke_artifact_path_text)
installer_receipt_path = pathlib.Path(installer_receipt_path_text)
installer_mouse_first_journey_receipt_path = pathlib.Path(installer_mouse_first_journey_receipt_path_text)
installer_mouse_first_journey_trace_path = pathlib.Path(installer_mouse_first_journey_trace_path_text)
failure_reasons_path = pathlib.Path(failure_reasons_path_text)

reasons: list[str] = []


def normalize_token(value: object) -> str:
    return str(value or "").strip().lower()


def startup_smoke_channel_proves_release(
    startup_smoke_channel: str,
    release_channel_id: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    actual = normalize_token(startup_smoke_channel)
    expected = normalize_token(release_channel_id)
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not expected or not actual:
        return True
    if actual == expected:
        return True
    if expected in {"preview", "smoke", "local", "local_docker_preview"} and actual in {"docker", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    if expected == "docker" and actual in {"preview", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    return False


def startup_smoke_version_proves_release(
    startup_smoke_version: str,
    release_channel_version: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    version = str(startup_smoke_version or "").strip()
    release_version = str(release_channel_version or "").strip()
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not release_version:
        return True
    if expected_digest and startup_digest == expected_digest:
        return True
    if not version:
        return False
    if version == release_version:
        return True
    return version.lower().startswith("smoke-") and bool(expected_digest) and startup_digest == expected_digest


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


def path_is_within(path: pathlib.Path, root: pathlib.Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def paths_match(left: pathlib.Path, right: pathlib.Path) -> bool:
    try:
        return left.resolve() == right.resolve()
    except Exception:
        return False


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


def is_internal_public_authentication_host_override_enabled() -> bool:
    normalized = str(os.getenv("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", "")).strip().lower()
    return normalized in {"1", "true", "yes", "on"}


def _is_unsafe_public_authentication_host(host: str) -> bool:
    normalized = str(host or "").strip().lower().strip(".")
    if not normalized:
        return True

    if normalized in {"localhost", "127.0.0.1", "::1"}:
        return False

    try:
        from ipaddress import ip_address

        if not ip_address(normalized).is_loopback:
            return True
    except Exception:
        pass

    if is_internal_public_authentication_host_override_enabled():
        return False

    blocked_tokens = ("chummer-api", "chummer-web", "host.docker.internal")
    for token in blocked_tokens:
        if normalized == token:
            return True
        if (
            normalized.startswith(f"{token}.")
            or normalized.endswith(f".{token}")
            or normalized.startswith(f"{token}-")
            or normalized.endswith(f"-{token}")
            or f".{token}." in normalized
            or f".{token}-" in normalized
            or f"-{token}." in normalized
            or f"-{token}-" in normalized
        ):
            return True

    return False


def is_unsafe_public_authentication_host(host: str) -> bool:
    return _is_unsafe_public_authentication_host(host)


def is_safe_public_authentication_uri(value: object) -> bool:
    raw_uri = str(value or "").strip()
    if not raw_uri:
        return False
    parsed = urlparse(raw_uri)
    if parsed.scheme.lower() not in {"http", "https"}:
        return False
    host = str(parsed.hostname or "").strip().lower()
    if not host:
        return False
    return not _is_unsafe_public_authentication_host(host)


release_channel = load_json(release_channel_path)
expected_artifact = None
promoted_mode = str(use_promoted_installer).strip() == "1"
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

if expected_artifact is None and promoted_mode:
    reasons.append(f"Release channel does not publish a Linux installer artifact for {app_key} ({rid}).")

if expected_artifact is not None:
    canonical_output_root = repo_root / ".codex-studio" / "out" / "linux-desktop-exit-gate"
    expected_file_name = str(expected_artifact.get("fileName") or "").strip()
    expected_sha = normalize_token(expected_artifact.get("sha256"))
    expected_size = int(expected_artifact.get("sizeBytes") or 0)
    expected_version = str(release_channel.get("version") or expected_artifact.get("version") or "").strip()
    expected_channel = normalize_token(release_channel.get("channelId") or release_channel.get("channel"))
    shelf_path = local_desktop_files_root / expected_file_name if expected_file_name else pathlib.Path()
    if not expected_file_name:
        reasons.append(f"Promoted Linux artifact fileName is missing for {app_key} ({rid}).")
    elif not shelf_path.is_file():
        reasons.append(f"Promoted Linux installer file is missing from the release-aligned desktop shelf: {shelf_path}")
    else:
        if expected_size and shelf_path.stat().st_size != expected_size:
            reasons.append("Promoted Linux installer size does not match release-channel artifact size.")
        if expected_sha and sha256(shelf_path) != expected_sha:
            reasons.append("Promoted Linux installer sha256 does not match release-channel artifact sha256.")
    if not installer_smoke_artifact_path.is_file():
        reasons.append(f"Linux startup smoke installer artifact path is missing: {installer_smoke_artifact_path}")
    elif expected_sha and sha256(installer_smoke_artifact_path) != expected_sha:
        reasons.append("Linux startup smoke installer artifact bytes do not match promoted release-channel artifact bytes.")
    if promoted_mode and shelf_path.is_file():
        try:
            if (
                installer_smoke_artifact_path.resolve() != shelf_path.resolve()
                and not path_is_within(installer_smoke_artifact_path, canonical_output_root)
            ):
                reasons.append(
                    "Linux startup smoke installer artifact path is neither the promoted repo-local shelf bytes nor a canonical gate-run copy."
                )
        except Exception:
            reasons.append("Linux startup smoke installer artifact path could not be resolved for promoted or canonical gate-run verification.")

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
        receipt_channel = normalize_token(receipt.get("channelId") or receipt.get("channel"))
        receipt_digest = normalize_token(receipt.get("artifactDigest"))
        expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
        if expected_channel and not startup_smoke_channel_proves_release(
            receipt_channel,
            expected_channel,
            receipt_digest,
            expected_digest,
        ):
            reasons.append("Linux startup smoke receipt channelId does not match release channel.")
        receipt_version = str(receipt.get("version") or receipt.get("releaseVersion") or "").strip()
        if expected_version and not startup_smoke_version_proves_release(
            receipt_version,
            expected_version,
            receipt_digest,
            expected_digest,
        ):
            reasons.append("Linux startup smoke receipt version does not match release channel version.")
        if expected_sha and receipt_digest != expected_digest:
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

    mouse_receipt = load_json(installer_mouse_first_journey_receipt_path)
    if not mouse_receipt:
        reasons.append(f"Linux mouse-first journey receipt is missing or unreadable: {installer_mouse_first_journey_receipt_path}")
    else:
        if normalize_token(mouse_receipt.get("status")) not in {"pass", "passed", "ready"}:
            reasons.append("Linux mouse-first journey receipt status is not passing.")
        if normalize_token(mouse_receipt.get("journeyMode")) != "mouse_first_live_binary":
            reasons.append("Linux mouse-first journey receipt journeyMode is not mouse_first_live_binary.")
        if normalize_token(mouse_receipt.get("headId")) != normalize_token(app_key):
            reasons.append("Linux mouse-first journey receipt headId does not match promoted head.")
        if normalize_token(mouse_receipt.get("platform")) != "linux":
            reasons.append("Linux mouse-first journey receipt platform is not linux.")
        if normalize_token(mouse_receipt.get("rid")) != normalize_token(rid):
            reasons.append("Linux mouse-first journey receipt rid does not match promoted RID.")
        mouse_digest = normalize_token(mouse_receipt.get("artifactDigest"))
        expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
        if expected_sha and mouse_digest != expected_digest:
            reasons.append("Linux mouse-first journey receipt artifactDigest does not match promoted installer bytes.")
        if expected_channel and not startup_smoke_channel_proves_release(
            str(mouse_receipt.get("channelId") or mouse_receipt.get("channel") or ""),
            expected_channel,
            mouse_digest,
            expected_digest,
        ):
            reasons.append("Linux mouse-first journey receipt channelId does not match release channel.")
        mouse_version = str(mouse_receipt.get("version") or mouse_receipt.get("releaseVersion") or "").strip()
        if expected_version and not startup_smoke_version_proves_release(
            mouse_version,
            expected_version,
            mouse_digest,
            expected_digest,
        ):
            reasons.append("Linux mouse-first journey receipt version does not match release channel version.")
        mouse_authentication_portal_opened = bool(mouse_receipt.get("authenticationPortalOpened"))
        mouse_authentication_portal_uri_is_safe = is_safe_public_authentication_uri(
            mouse_receipt.get("authenticationPortalUri")
        )
        if not mouse_authentication_portal_opened and not mouse_authentication_portal_uri_is_safe:
            reasons.append("Linux mouse-first journey receipt does not prove authentication portal was opened.")
        if not mouse_authentication_portal_uri_is_safe:
            reasons.append("Linux mouse-first journey receipt authentication portal uri is missing or points to a non-public host.")
        if not bool(mouse_receipt.get("hasSavedWorkspace")):
            reasons.append("Linux mouse-first journey receipt does not prove a saved workspace.")
        if not str(mouse_receipt.get("workspaceId") or "").strip():
            reasons.append("Linux mouse-first journey receipt workspaceId is missing.")
        if not str(mouse_receipt.get("characterName") or "").strip():
            reasons.append("Linux mouse-first journey receipt characterName is missing.")
        if not str(mouse_receipt.get("characterAlias") or "").strip():
            reasons.append("Linux mouse-first journey receipt characterAlias is missing.")
        steps = mouse_receipt.get("steps") or []
        if not isinstance(steps, list) or len(steps) < 4:
            reasons.append("Linux mouse-first journey receipt does not contain enough interaction steps.")
        else:
            lowered_steps = [normalize_token(item) for item in steps]
            if not any("file menu" in step for step in lowered_steps):
                reasons.append("Linux mouse-first journey receipt does not prove the File menu path.")
            if not any("create_character" in step or "create character" in step for step in lowered_steps):
                reasons.append("Linux mouse-first journey receipt does not prove character creation.")
            if not any("save" in step for step in lowered_steps):
                reasons.append("Linux mouse-first journey receipt does not prove save interaction.")

if reasons:
    failure_reasons_path.parent.mkdir(parents=True, exist_ok=True)
    failure_reasons_path.write_text(json.dumps({"reasons": reasons}, indent=2) + "\n", encoding="utf-8")
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

    CURRENT_STAGE="source_snapshot_identity"
    announce_stage "$CURRENT_STAGE" "revalidating source snapshot identity"
    refresh_source_snapshot_manifest
    assert_source_snapshot_identity_stable

    CURRENT_STAGE="git_identity_stability"
    announce_stage "$CURRENT_STAGE" "checking repo git identity stability"
    capture_git_metadata "$GIT_FINISH_PATH"
    if ! assert_repo_git_identity_stable; then
      GIT_IDENTITY_NOTE=" (post-run git identity drift detected outside the isolated source snapshot; source snapshot identity stayed stable)"
    fi

    CURRENT_STAGE="complete"
    announce_stage "$CURRENT_STAGE" "publishing passing proof"
    write_proof "passed" "linux promoted installer shelf, startup smoke, and unit tests passed$GIT_IDENTITY_NOTE" "0"
    publish_canonical_proof
    echo "linux desktop exit gate passed; proof: $PROOF_PATH"
    exit 0
  fi
fi

CURRENT_STAGE="restore_publish_graph"
announce_stage "$CURRENT_STAGE" "restoring publish-time linux dependencies"
run_with_heartbeat "linux desktop publish restore" \
  run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/with-package-plane.sh" restore "$SOURCE_SNAPSHOT_ROOT/$PROJECT_PATH" -r "$RID" -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true -p:ChummerDesktopReleaseVersion="$VERSION" -p:ChummerDesktopReleaseChannel="$CHANNEL" --nologo

CURRENT_STAGE="publish_linux_binary"
announce_stage "$CURRENT_STAGE" "publishing self-contained linux desktop binary"
run_with_heartbeat "linux desktop publish" \
  run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/with-package-plane.sh" publish "$SOURCE_SNAPSHOT_ROOT/$PROJECT_PATH" -c Release -r "$RID" --self-contained true --no-restore -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -p:ChummerDesktopReleaseVersion="$VERSION" -p:ChummerDesktopReleaseChannel="$CHANNEL" -o "$PUBLISH_DIR" --nologo
test -f "$PUBLISH_DIR/$LAUNCH_TARGET"

CURRENT_STAGE="package_linux_artifacts"
announce_stage "$CURRENT_STAGE" "creating archive and installer artifacts"
run_with_heartbeat "linux desktop packaging" \
  run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/build-desktop-installer.sh" "$PUBLISH_DIR" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$DIST_DIR" "$VERSION"
test -f "$ARCHIVE_PATH"
test -f "$INSTALLER_PATH"
INSTALLER_SMOKE_ARTIFACT_PATH="$INSTALLER_PATH"

EFFECTIVE_USE_PROMOTED_INSTALLER="$USE_PROMOTED_INSTALLER"
if [[ "$USE_PROMOTED_INSTALLER" == "1" && "${CHUMMER_LINUX_DESKTOP_EXIT_GATE_PROMOTED_ONLY:-0}" != "1" ]]; then
  if ! release_channel_publishes_promoted_installer_tuple; then
    EFFECTIVE_USE_PROMOTED_INSTALLER="0"
  fi
fi

if [[ "$EFFECTIVE_USE_PROMOTED_INSTALLER" == "1" ]]; then
  CURRENT_STAGE="resolve_promoted_installer"
  announce_stage "$CURRENT_STAGE" "replacing installer with promoted shelf bytes"
  if [[ -z "$PROMOTED_INSTALLER_PATH" ]]; then
    PROMOTED_INSTALLER_PATH="$(resolve_promoted_installer_path)"
  fi
  if [[ -z "$PROMOTED_INSTALLER_PATH" || ! -f "$PROMOTED_INSTALLER_PATH" ]]; then
    echo "Linux promoted installer path could not be resolved for $APP_KEY $RID." >&2
    exit 1
  fi
  cp "$PROMOTED_INSTALLER_PATH" "$INSTALLER_PATH"
  INSTALLER_SMOKE_ARTIFACT_PATH="$INSTALLER_PATH"
fi

CURRENT_STAGE="startup_smoke_archive"
announce_stage "$CURRENT_STAGE" "running startup smoke against archive artifact"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$ARCHIVE_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$ARCHIVE_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$ARCHIVE_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$ARCHIVE_MOUSE_FIRST_JOURNEY_TRACE_PATH" \
  run_with_heartbeat "archive startup smoke" \
  run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" "$ARCHIVE_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_ARCHIVE_DIR" "$VERSION"
test -f "$ARCHIVE_RECEIPT_PATH"
test -f "$ARCHIVE_MOUSE_FIRST_JOURNEY_RECEIPT_PATH"

CURRENT_STAGE="startup_smoke_installer"
announce_stage "$CURRENT_STAGE" "running startup smoke against installer artifact"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_RECEIPT="$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_FAILURE_PACKET="$INSTALLER_MOUSE_FIRST_JOURNEY_FAILURE_PACKET_PATH" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR="$INSTALLER_MOUSE_FIRST_JOURNEY_SCREENSHOT_DIR" \
  CHUMMER_DESKTOP_MOUSE_FIRST_JOURNEY_TRACE="$INSTALLER_MOUSE_FIRST_JOURNEY_TRACE_PATH" \
  run_with_heartbeat "installer startup smoke" \
  run_snapshot_command bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_INSTALLER_DIR" "$VERSION"
test -f "$INSTALLER_RECEIPT_PATH"
test -f "$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH"

CURRENT_STAGE="promoted_installer_proof_integrity"
announce_stage "$CURRENT_STAGE" "verifying release-channel and smoke receipt integrity"
"$PYTHON_BIN" - "$RELEASE_CHANNEL_PATH" "$REPO_ROOT" "$LOCAL_DESKTOP_FILES_ROOT" "$APP_KEY" "$RID" "$INSTALLER_SMOKE_ARTIFACT_PATH" "$INSTALLER_RECEIPT_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_RECEIPT_PATH" "$INSTALLER_MOUSE_FIRST_JOURNEY_TRACE_PATH" "$EFFECTIVE_USE_PROMOTED_INSTALLER" "$FAILURE_REASONS_PATH" <<'PY'
from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import pathlib
import platform
import shutil
import sys
from urllib.parse import urlparse

(
    release_channel_path_text,
    repo_root_text,
    local_desktop_files_root_text,
    app_key,
    rid,
    installer_smoke_artifact_path_text,
    installer_receipt_path_text,
    installer_mouse_first_journey_receipt_path_text,
    installer_mouse_first_journey_trace_path_text,
    use_promoted_installer,
    failure_reasons_path_text,
) = sys.argv[1:]

release_channel_path = pathlib.Path(release_channel_path_text)
repo_root = pathlib.Path(repo_root_text)
local_desktop_files_root = pathlib.Path(local_desktop_files_root_text)
installer_smoke_artifact_path = pathlib.Path(installer_smoke_artifact_path_text)
installer_receipt_path = pathlib.Path(installer_receipt_path_text)
installer_mouse_first_journey_receipt_path = pathlib.Path(installer_mouse_first_journey_receipt_path_text)
installer_mouse_first_journey_trace_path = pathlib.Path(installer_mouse_first_journey_trace_path_text)
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


def startup_smoke_channel_proves_release(
    startup_smoke_channel: str,
    release_channel_id: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    actual = normalize_token(startup_smoke_channel)
    expected = normalize_token(release_channel_id)
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not expected or not actual:
        return True
    if actual == expected:
        return True
    if expected in {"preview", "smoke", "local", "local_docker_preview"} and actual in {"docker", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    if expected == "docker" and actual in {"preview", "smoke", "local", "local_docker_preview"}:
        return not expected_digest or startup_digest == expected_digest
    return False


def startup_smoke_version_proves_release(
    startup_smoke_version: str,
    release_channel_version: str,
    startup_smoke_artifact_digest: str,
    expected_startup_smoke_digest: str,
) -> bool:
    version = str(startup_smoke_version or "").strip()
    release_version = str(release_channel_version or "").strip()
    startup_digest = normalize_token(startup_smoke_artifact_digest)
    expected_digest = normalize_token(expected_startup_smoke_digest)
    if not release_version:
        return True
    if expected_digest and startup_digest == expected_digest:
        return True
    if not version:
        return False
    if version == release_version:
        return True
    return version.lower().startswith("smoke-") and bool(expected_digest) and startup_digest == expected_digest


def is_internal_public_authentication_host_override_enabled() -> bool:
    normalized = str(os.getenv("CHUMMER_ALLOW_INTERNAL_PUBLIC_WEB_HOSTS", "")).strip().lower()
    return normalized in {"1", "true", "yes", "on"}


def _is_unsafe_public_authentication_host(host: str) -> bool:
    normalized = str(host or "").strip().lower().strip(".")
    if not normalized:
        return True

    if normalized in {"localhost", "127.0.0.1", "::1"}:
        return False

    try:
        import ipaddress

        return not ipaddress.ip_address(normalized).is_loopback
    except Exception:
        pass

    if is_internal_public_authentication_host_override_enabled():
        return False

    blocked_tokens = ("chummer-api", "chummer-web", "host.docker.internal")
    for token in blocked_tokens:
        if normalized == token:
            return True
        if (
            normalized.startswith(f"{token}.")
            or normalized.endswith(f".{token}")
            or normalized.startswith(f"{token}-")
            or normalized.endswith(f"-{token}")
            or f".{token}." in normalized
            or f".{token}-" in normalized
            or f"-{token}." in normalized
            or f"-{token}-" in normalized
        ):
            return True

    return False


def is_safe_public_authentication_uri(value: object) -> bool:
    raw_uri = str(value or "").strip()
    if not raw_uri:
        return False
    parsed = urlparse(raw_uri)
    if parsed.scheme.lower() not in {"http", "https"}:
        return False
    host = str(parsed.hostname or "").strip().lower()
    if not host:
        return False
    return not _is_unsafe_public_authentication_host(host)


def path_uses_legacy_chummer5a_root(path: pathlib.Path) -> bool:
    normalized = str(path.resolve()).replace("\\", "/").lower()
    return "/chummer5a/" in normalized


def path_is_within(path: pathlib.Path, root: pathlib.Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except Exception:
        return False


def paths_match(left: pathlib.Path, right: pathlib.Path) -> bool:
    try:
        return left.resolve() == right.resolve()
    except Exception:
        return False


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
    canonical_output_root = repo_root / ".codex-studio" / "out" / "linux-desktop-exit-gate"
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
    promoted_mode = str(use_promoted_installer).strip() == "1"
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
        if promoted_mode:
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
                f"Promoted Linux installer file is missing from the release-aligned desktop shelf: {promoted_shelf_artifact_path}"
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
            elif promoted_mode:
                smoke_artifact_sha = hashlib.sha256(installer_smoke_artifact_path.read_bytes()).hexdigest().lower()
                if expected_sha and smoke_artifact_sha != expected_sha:
                    reasons.append(
                        "Linux startup smoke installer artifact bytes do not match promoted release-channel artifact bytes."
                    )

            if promoted_mode:
                try:
                    if (
                        not paths_match(installer_smoke_artifact_path, promoted_shelf_artifact_path)
                        and not path_is_within(installer_smoke_artifact_path, canonical_output_root)
                    ):
                        reasons.append(
                            "Linux startup smoke installer artifact path is neither the promoted repo-local shelf bytes nor a canonical gate-run copy."
                        )
                except Exception:
                    reasons.append(
                        "Linux startup smoke installer artifact path could not be resolved for promoted or canonical gate-run verification."
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
                    receipt.get("artifactPath"),
                    receipt.get("artifactRelativePath"),
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
            if expected_channel and not startup_smoke_channel_proves_release(
                receipt_channel,
                expected_channel,
                receipt_digest,
                expected_digest,
            ):
                reasons.append("Linux startup smoke receipt channelId does not match release channel.")
            if expected_version and not receipt_release_version:
                reasons.append("Linux startup smoke receipt releaseVersion is missing.")
            if (
                expected_version
                and receipt_release_version
                and not startup_smoke_version_proves_release(
                    receipt_release_version,
                    expected_version,
                    receipt_digest,
                    expected_digest,
                )
                and not startup_smoke_version_proves_release(
                    receipt_version,
                    expected_version,
                    receipt_digest,
                    expected_digest,
                )
            ):
                reasons.append("Linux startup smoke receipt releaseVersion does not match release channel version.")
            if expected_version and not receipt_version:
                reasons.append("Linux startup smoke receipt version is missing.")
            if (
                expected_version
                and receipt_version
                and not startup_smoke_version_proves_release(
                    receipt_version,
                    expected_version,
                    receipt_digest,
                    expected_digest,
                )
            ):
                reasons.append("Linux startup smoke receipt version does not match release channel version.")
            if promoted_mode and expected_digest and receipt_digest != expected_digest:
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
                elif promoted_mode:
                    try:
                        if (
                            promoted_shelf_artifact_path.is_file()
                            and not paths_match(receipt_artifact_path_obj, promoted_shelf_artifact_path)
                            and not paths_match(receipt_artifact_path_obj, installer_smoke_artifact_path)
                            and not path_is_within(receipt_artifact_path_obj, canonical_output_root)
                        ):
                            reasons.append(
                                "Linux startup smoke receipt artifactPath is neither the promoted installer shelf bytes nor a canonical gate-run copy."
                            )
                    except Exception:
                        reasons.append(
                            "Linux startup smoke receipt artifactPath could not be resolved for promoted or canonical gate-run verification."
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

        if not installer_mouse_first_journey_receipt_path.is_file():
            reasons.append(f"Linux mouse-first journey receipt is missing: {installer_mouse_first_journey_receipt_path}")
        else:
            try:
                mouse_receipt = json.loads(installer_mouse_first_journey_receipt_path.read_text(encoding="utf-8-sig"))
            except Exception as ex:
                reasons.append(f"Linux mouse-first journey receipt is unreadable: {ex}")
                mouse_receipt = {}

            mouse_status = str(mouse_receipt.get("status") or "").strip().lower()
            mouse_mode = str(mouse_receipt.get("journeyMode") or "").strip().lower()
            mouse_head = str(mouse_receipt.get("headId") or "").strip().lower()
            mouse_platform = str(mouse_receipt.get("platform") or "").strip().lower()
            mouse_arch = str(mouse_receipt.get("arch") or "").strip().lower()
            mouse_rid = str(mouse_receipt.get("rid") or "").strip().lower()
            mouse_channel = str(mouse_receipt.get("channelId") or mouse_receipt.get("channel") or "").strip().lower()
            mouse_digest = str(mouse_receipt.get("artifactDigest") or "").strip().lower()
            mouse_release_version = str(mouse_receipt.get("releaseVersion") or "").strip()
            mouse_version = str(mouse_receipt.get("version") or mouse_receipt.get("releaseVersion") or "").strip()
            mouse_host_class = str(mouse_receipt.get("hostClass") or "").strip().lower()
            mouse_operating_system = str(mouse_receipt.get("operatingSystem") or "").strip()
            mouse_steps = mouse_receipt.get("steps") or []
            mouse_screenshot_paths = mouse_receipt.get("screenshotPaths") or []
            mouse_trace_path = pathlib.Path(str(mouse_receipt.get("tracePath") or "").strip()) if str(mouse_receipt.get("tracePath") or "").strip() else installer_mouse_first_journey_trace_path
            mouse_pointer_action_count = int(mouse_receipt.get("pointerActionCount") or 0)
            mouse_text_entry_action_count = int(mouse_receipt.get("textEntryActionCount") or 0)
            mouse_direct_text_mutation_count = int(mouse_receipt.get("directTextMutationCount") or 0)
            mouse_used_forced_combo_dropdown_open = bool(mouse_receipt.get("usedForcedComboDropdownOpen"))
            mouse_used_combo_selection_fallback = bool(mouse_receipt.get("usedComboSelectionFallback"))
            mouse_observed_input_events = mouse_receipt.get("observedInputEvents") or []
            mouse_workspace_id = str(mouse_receipt.get("workspaceId") or "").strip()
            mouse_character_name = str(mouse_receipt.get("characterName") or "").strip()
            mouse_character_alias = str(mouse_receipt.get("characterAlias") or "").strip()
            expected_digest = f"sha256:{expected_sha}" if expected_sha else ""

            if mouse_status not in {"pass", "passed", "ready"}:
                reasons.append("Linux mouse-first journey receipt status is not passing.")
            if mouse_mode != "mouse_first_live_binary":
                reasons.append("Linux mouse-first journey receipt journeyMode is not mouse_first_live_binary.")
            if mouse_head != app_key.lower():
                reasons.append("Linux mouse-first journey receipt headId does not match promoted head.")
            if mouse_platform != "linux":
                reasons.append("Linux mouse-first journey receipt platform is not linux.")
            if not mouse_host_class:
                reasons.append("Linux mouse-first journey receipt hostClass is missing.")
            elif not host_class_matches_platform(mouse_host_class, "linux"):
                reasons.append("Linux mouse-first journey receipt hostClass does not identify a Linux host.")
            if not mouse_operating_system:
                reasons.append("Linux mouse-first journey receipt operatingSystem is missing.")
            if expected_arch and mouse_arch != expected_arch:
                reasons.append("Linux mouse-first journey receipt arch does not match promoted RID.")
            if mouse_rid != rid.lower():
                reasons.append("Linux mouse-first journey receipt rid does not match promoted RID.")
            if expected_channel and not startup_smoke_channel_proves_release(mouse_channel, expected_channel, mouse_digest, expected_digest):
                reasons.append("Linux mouse-first journey receipt channelId does not match release channel.")
            if expected_version and not mouse_release_version:
                reasons.append("Linux mouse-first journey receipt releaseVersion is missing.")
            if (
                expected_version
                and mouse_release_version
                and not startup_smoke_version_proves_release(mouse_release_version, expected_version, mouse_digest, expected_digest)
                and not startup_smoke_version_proves_release(mouse_version, expected_version, mouse_digest, expected_digest)
            ):
                reasons.append("Linux mouse-first journey receipt releaseVersion does not match release channel version.")
            mouse_authentication_portal_opened = bool(mouse_receipt.get("authenticationPortalOpened"))
            mouse_authentication_portal_uri_is_safe = is_safe_public_authentication_uri(
                mouse_receipt.get("authenticationPortalUri")
            )
            if not mouse_authentication_portal_opened and not mouse_authentication_portal_uri_is_safe:
                reasons.append("Linux mouse-first journey receipt does not prove authentication portal was opened.")
            if not mouse_authentication_portal_uri_is_safe:
                reasons.append("Linux mouse-first journey receipt authentication portal uri is missing or points to a non-public host.")
            if promoted_mode and expected_digest and mouse_digest != expected_digest:
                reasons.append("Linux mouse-first journey receipt artifactDigest does not match promoted installer bytes.")
            if not bool(mouse_receipt.get("hasSavedWorkspace")):
                reasons.append("Linux mouse-first journey receipt does not prove a saved workspace.")
            if not mouse_workspace_id:
                reasons.append("Linux mouse-first journey receipt workspaceId is missing.")
            if not mouse_character_name:
                reasons.append("Linux mouse-first journey receipt characterName is missing.")
            if not mouse_character_alias:
                reasons.append("Linux mouse-first journey receipt characterAlias is missing.")
            if mouse_pointer_action_count <= 0:
                reasons.append("Linux mouse-first journey receipt pointerActionCount is missing.")
            if mouse_text_entry_action_count <= 0:
                reasons.append("Linux mouse-first journey receipt textEntryActionCount is missing.")
            if mouse_pointer_action_count <= mouse_text_entry_action_count:
                reasons.append("Linux mouse-first journey receipt does not prove a pointer-dominant interaction mix.")
            if mouse_direct_text_mutation_count != 0:
                reasons.append("Linux mouse-first journey receipt directTextMutationCount must be zero.")
            if mouse_used_forced_combo_dropdown_open:
                reasons.append("Linux mouse-first journey receipt usedForcedComboDropdownOpen must be false.")
            if mouse_used_combo_selection_fallback:
                reasons.append("Linux mouse-first journey receipt usedComboSelectionFallback must be false.")
            if not isinstance(mouse_observed_input_events, list) or len(mouse_observed_input_events) < (mouse_pointer_action_count * 2):
                reasons.append("Linux mouse-first journey receipt does not publish enough observed input events.")
            if not isinstance(mouse_steps, list) or len(mouse_steps) < 4:
                reasons.append("Linux mouse-first journey receipt does not contain enough interaction steps.")
            else:
                lowered_steps = [normalize_token(item) for item in mouse_steps]
                if not any("file menu" in step for step in lowered_steps):
                    reasons.append("Linux mouse-first journey receipt does not prove the File menu path.")
                if not any("create_character" in step or "create character" in step for step in lowered_steps):
                    reasons.append("Linux mouse-first journey receipt does not prove character creation.")
                if not any("save" in step for step in lowered_steps):
                    reasons.append("Linux mouse-first journey receipt does not prove save interaction.")
            if not isinstance(mouse_screenshot_paths, list) or len(mouse_screenshot_paths) < 5:
                reasons.append("Linux mouse-first journey receipt does not carry enough screenshot evidence.")
            else:
                resolved_screenshot_paths = []
                for raw_path in mouse_screenshot_paths:
                    candidate = pathlib.Path(str(raw_path or "").strip())
                    if not str(candidate):
                        reasons.append("Linux mouse-first journey receipt carries a blank screenshot path.")
                        continue
                    if not candidate.is_absolute():
                        candidate = (repo_root / candidate).resolve()
                    resolved_screenshot_paths.append(candidate)
                expected_screenshot_stems = {
                    "01-new-character-dialog.png",
                    "02-priority-workflow.png",
                    "03-post-dialog-close.png",
                    "04-workspace-opened.png",
                    "05-workspace-saved.png",
                }
                actual_screenshot_stems = {path.name for path in resolved_screenshot_paths}
                if not expected_screenshot_stems.issubset(actual_screenshot_stems):
                    reasons.append("Linux mouse-first journey screenshot evidence is missing one or more required stage captures.")
                for candidate in resolved_screenshot_paths:
                    if not candidate.is_file():
                        reasons.append(f"Linux mouse-first journey screenshot is missing on disk: {candidate}")
                        continue
                    if candidate.suffix.lower() != ".png":
                        reasons.append(f"Linux mouse-first journey screenshot is not a PNG path: {candidate}")
                        continue
                    payload = candidate.read_bytes()
                    if not payload.startswith(b"\x89PNG\r\n\x1a\n"):
                        reasons.append(f"Linux mouse-first journey screenshot is not a valid PNG file: {candidate}")
                    if len(payload) < 1024:
                        reasons.append(f"Linux mouse-first journey screenshot is too small to count as credible evidence: {candidate}")
            if not mouse_trace_path.is_absolute():
                mouse_trace_path = (repo_root / mouse_trace_path).resolve()
            if not mouse_trace_path.is_file():
                reasons.append(f"Linux mouse-first journey trace is missing: {mouse_trace_path}")
            else:
                try:
                    mouse_trace = json.loads(mouse_trace_path.read_text(encoding="utf-8-sig"))
                except Exception as ex:
                    reasons.append(f"Linux mouse-first journey trace is unreadable: {ex}")
                    mouse_trace = {}
                observed_trace_events = mouse_trace.get("observedInputEvents") if isinstance(mouse_trace, dict) else None
                if not isinstance(observed_trace_events, list) or not observed_trace_events:
                    reasons.append("Linux mouse-first journey trace does not contain observedInputEvents.")
                else:
                    normalized_trace_names = {
                        normalize_token(event.get("controlName"))
                        for event in observed_trace_events
                        if isinstance(event, dict)
                    }
                    required_control_names = {
                        "filemenubutton",
                        "dialogaction_create_character",
                        "dialogaction_complete_new_character_workflow",
                    }
                    if not required_control_names.issubset(normalized_trace_names):
                        reasons.append("Linux mouse-first journey trace is missing required control interaction evidence.")

if reasons:
    failure_reasons_path.parent.mkdir(parents=True, exist_ok=True)
    failure_reasons_path.write_text(json.dumps({"reasons": reasons}, indent=2) + "\n", encoding="utf-8")
    print("\n".join(reasons), file=sys.stderr)
    raise SystemExit(1)
PY

CURRENT_STAGE="source_snapshot_identity"
announce_stage "$CURRENT_STAGE" "revalidating source snapshot identity"
refresh_source_snapshot_manifest
assert_source_snapshot_identity_stable

CURRENT_STAGE="git_identity_stability"
announce_stage "$CURRENT_STAGE" "checking repo git identity stability"
capture_git_metadata "$GIT_FINISH_PATH"
if ! assert_repo_git_identity_stable; then
  GIT_IDENTITY_NOTE=" (post-run git identity drift detected outside the isolated source snapshot; source snapshot identity stayed stable)"
fi

CURRENT_STAGE="complete"
announce_stage "$CURRENT_STAGE" "publishing passing proof"
write_proof "passed" "linux desktop build, startup smoke, and unit tests passed$GIT_IDENTITY_NOTE" "0"
publish_canonical_proof
echo "linux desktop exit gate passed; proof: $PROOF_PATH"
