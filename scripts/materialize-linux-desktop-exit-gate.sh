#!/usr/bin/env bash
set -euo pipefail
set -o errtrace

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKSPACE_ROOT="$(cd "$REPO_ROOT/.." && pwd)"

APP_KEY="avalonia"
PROJECT_PATH="Chummer.Avalonia/Chummer.Avalonia.csproj"
TEST_PROJECT_PATH="Chummer.Desktop.Runtime.Tests/Chummer.Desktop.Runtime.Tests.csproj"
RID="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_RID:-linux-x64}"
LAUNCH_TARGET="Chummer.Avalonia"
VERSION="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_VERSION:-local-hard-gate}"
CHANNEL="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_CHANNEL:-local-hard-gate}"
FRAMEWORK="net10.0"
READY_CHECKPOINT="pre_ui_event_loop"
OUTPUT_BASE_ROOT="${CHUMMER_LINUX_DESKTOP_EXIT_GATE_OUTPUT_ROOT:-$REPO_ROOT/.codex-studio/out/linux-desktop-exit-gate}"
PROOF_PATH="${CHUMMER_UI_LINUX_DESKTOP_EXIT_GATE_PATH:-$REPO_ROOT/.codex-studio/published/UI_LINUX_DESKTOP_EXIT_GATE.generated.json}"

mkdir -p "$OUTPUT_BASE_ROOT"
RUN_ROOT="$(mktemp -d "$OUTPUT_BASE_ROOT/run.XXXXXX")"
LATEST_LINK="$OUTPUT_BASE_ROOT/latest"
PUBLISH_LOCK_PATH="$OUTPUT_BASE_ROOT/publish.lock"
RUN_PROOF_PATH="$RUN_ROOT/UI_LINUX_DESKTOP_EXIT_GATE.generated.json"
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

CURRENT_STAGE="init"

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
        listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            if not is_gate_input(relative):
                continue
            seen.add(relative)
            entries.append(relative)
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
        listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in listing.split("\0"):
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
        listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in listing.split("\0"):
            relative = raw_item.strip()
            if not relative or relative in seen or is_excluded(relative, markers):
                continue
            if not is_gate_input(relative):
                continue
            seen.add(relative)
            entries.append(relative)
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
        listing = subprocess.run(
            ["git", "-C", str(repo_root), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in listing.split("\0"):
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


entries = iter_repo_entries(normalize_markers())
snapshot_root.mkdir(parents=True, exist_ok=True)
digest = hashlib.sha256()
entry_count = 0

for relative in entries:
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

manifest = {
    "mode": "filesystem_copy",
    "repo_root": str(repo_root),
    "snapshot_root": str(snapshot_root),
    "entries_path": str(entries_path),
    "entry_count": entry_count,
    "worktree_sha256": digest.hexdigest(),
}
entries_path.write_text("".join(f"{relative}\n" for relative in entries), encoding="utf-8")
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


def is_ignorable_generated(relative: str) -> bool:
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
        if relative in expected_set or is_ignorable_generated(relative):
            continue
        try:
            stat_result = os.lstat(path)
        except FileNotFoundError:
            continue
        mode = stat.S_IMODE(stat_result.st_mode)
        if stat.S_ISLNK(stat_result.st_mode):
            digest.update(f"extra-symlink\0{relative}\0{mode:o}\0{os.readlink(path)}\0".encode("utf-8"))
            finish_entry_count += 1
            continue
        if not stat.S_ISREG(stat_result.st_mode):
            continue
        digest.update(f"extra-file\0{relative}\0{mode:o}\0".encode("utf-8"))
        with path.open("rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        digest.update(b"\0")
        finish_entry_count += 1
    finish_digest = digest.hexdigest()

payload["finish_worktree_sha256"] = finish_digest
payload["finish_entry_count"] = finish_entry_count
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
    "$TEST_RESULTS_DIR" "$TEST_TRX_PATH" "$GIT_START_PATH" "$GIT_FINISH_PATH" "$SOURCE_SNAPSHOT_MANIFEST_PATH" <<'PY'
import datetime as dt
import hashlib
import json
import os
import pathlib
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
) = sys.argv[1:]


def load_json(path_text: str):
    path = pathlib.Path(path_text)
    if not path.is_file():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return None


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
    try:
        listing = subprocess.run(
            ["git", "-C", repo_root_text, "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            check=True,
            capture_output=True,
        ).stdout.decode("utf-8", errors="surrogateescape")
        entries = []
        seen = set()
        for raw_item in listing.split("\0"):
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
        entries.sort()
        if not entries:
            raise ValueError("no gate-scoped entries")
    except Exception:
        try:
            listing = subprocess.run(
                ["git", "-C", repo_root_text, "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                check=True,
                capture_output=True,
            ).stdout.decode("utf-8", errors="surrogateescape")
            entries = []
            seen = set()
            for raw_item in listing.split("\0"):
                relative = raw_item.strip()
                if not relative or relative in seen:
                    continue
                if any(relative == marker or relative.startswith(f"{marker}/") for marker in exclude_markers):
                    continue
                seen.add(relative)
                entries.append(relative)
            entries.sort()
        except Exception:
            entries = []
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

payload = {
    "contract_name": "chummer6-ui.linux_desktop_exit_gate",
    "generated_at": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
    "status": proof_status,
    "reason": reason,
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
    "git": {
        **read_git_metadata(repo_root, output_base_root, canonical_proof_path),
        "start": git_start,
        "finish": git_finish,
        "identity_stable": identity_stable,
    },
    "source_snapshot": source_snapshot,
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
    if same_identity and str(existing_payload.get("status") or "").strip() == "passed" and str(new_payload.get("status") or "").strip() != "passed":
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
}

trap on_error ERR
trap cleanup_snapshot EXIT

mkdir -p "$PUBLISH_DIR" "$DIST_DIR" "$TEST_RESULTS_DIR" "$SMOKE_ARCHIVE_DIR" "$SMOKE_INSTALLER_DIR"
capture_git_metadata "$GIT_START_PATH"

CURRENT_STAGE="source_snapshot"
materialize_source_snapshot

CURRENT_STAGE="unit_tests"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/test.sh" "$SOURCE_SNAPSHOT_ROOT/$TEST_PROJECT_PATH" -c Release -f "$FRAMEWORK" \
  --logger "trx;LogFileName=$(basename "$TEST_TRX_PATH")" \
  --results-directory "$TEST_RESULTS_DIR"
test -f "$TEST_TRX_PATH"

CURRENT_STAGE="publish_linux_binary"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/ai/with-package-plane.sh" publish "$SOURCE_SNAPSHOT_ROOT/$PROJECT_PATH" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:ChummerDesktopReleaseVersion="$VERSION" \
  -p:ChummerDesktopReleaseChannel="$CHANNEL" \
  -o "$PUBLISH_DIR" \
  --nologo
test -f "$PUBLISH_DIR/$LAUNCH_TARGET"

CURRENT_STAGE="package_linux_artifacts"
bash "$SOURCE_SNAPSHOT_ROOT/scripts/build-desktop-installer.sh" "$PUBLISH_DIR" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$DIST_DIR" "$VERSION"
test -f "$ARCHIVE_PATH"
test -f "$INSTALLER_PATH"

CURRENT_STAGE="startup_smoke_archive"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" \
bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" \
  "$ARCHIVE_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_ARCHIVE_DIR" "$VERSION"
test -f "$ARCHIVE_RECEIPT_PATH"

CURRENT_STAGE="startup_smoke_installer"
CHUMMER_DESKTOP_RELEASE_CHANNEL="$CHANNEL" \
bash "$SOURCE_SNAPSHOT_ROOT/scripts/run-desktop-startup-smoke.sh" \
  "$INSTALLER_PATH" "$APP_KEY" "$RID" "$LAUNCH_TARGET" "$SMOKE_INSTALLER_DIR" "$VERSION"
test -f "$INSTALLER_RECEIPT_PATH"

CURRENT_STAGE="source_snapshot_identity"
refresh_source_snapshot_manifest
assert_source_snapshot_identity_stable

CURRENT_STAGE="git_identity_stability"
capture_git_metadata "$GIT_FINISH_PATH"
assert_repo_git_identity_stable

CURRENT_STAGE="complete"
write_proof "passed" "linux desktop build, startup smoke, and unit tests passed" "0"
publish_canonical_proof
echo "linux desktop exit gate passed; proof: $PROOF_PATH"
