#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

BUNDLE_DIR="${1:-$REPO_ROOT/dist}"
DEPLOY_DIR="${2:-$REPO_ROOT/Docker/Downloads}"
PORTAL_MANIFEST_PATH="${PORTAL_MANIFEST_PATH:-}"
PORTAL_DOWNLOADS_DIR="${PORTAL_DOWNLOADS_DIR:-}"
DEPLOY_MODE="${CHUMMER_PORTAL_DOWNLOADS_DEPLOY_ENABLED:-false}"
LIVE_VERIFY_TARGET="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL:-}"
REQUIRE_EXTERNAL_PUBLISH="${CHUMMER_DOWNLOADS_REQUIRE_EXTERNAL_PUBLISH:-false}"
MANIFEST_SOURCE="$BUNDLE_DIR/releases.json"
FILES_SOURCE="$BUNDLE_DIR/files"
RELEASE_PROOF_PATH="${RELEASE_PROOF_PATH:-}"
STARTUP_SMOKE_SOURCE="${STARTUP_SMOKE_SOURCE:-$BUNDLE_DIR/startup-smoke}"
PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-}"
SYNC_LIVE_DOWNLOADS_MIRRORS="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_SYNC_MIRRORS:-true}"
FORCE_NIGHTLY_PUBLISH="${CHUMMER_FORCE_NIGHTLY_PUBLISH:-0}"
ROOT_RELEASE_BLOCKERS_PATH="${CHUMMER_ROOT_RELEASE_BLOCKERS_PATH:-$REPO_ROOT/../RELEASE_BLOCKERS.generated.json}"
PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS:-86400}"
BUILD_PROVENANCE_VALIDATOR="${CHUMMER_RELEASE_BUILD_PROVENANCE_VALIDATOR:-}"
BUILD_PROVENANCE_MANIFEST_SOURCE="$BUNDLE_DIR/RELEASE_CHANNEL.generated.json"
BUILD_PROVENANCE_REQUIRED=0
BUILD_PROVENANCE_STAGE_ROOT=""
BUILD_PROVENANCE_VALIDATOR_RESOLVED=""
RELEASE_CANDIDATE_STAGE_ONLY="${CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY:-0}"
RELEASE_CANDIDATE_OUTPUT_DIR="${CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR:-}"

to_bool() {
  local value
  value="$(echo "${1:-}" | tr '[:upper:]' '[:lower:]')"
  [[ "$value" == "1" || "$value" == "true" || "$value" == "yes" || "$value" == "on" ]]
}

assert_legacy_release_shelf_target() {
  local target_dir="$1"
  local layout_marker="$target_dir/.release-shelf-layout-v1"
  local active_pointer="$target_dir/current.json"
  local writer_policy="$target_dir/.release-shelf-writer-policy.json"

  if [[ -e "$writer_policy" || -L "$writer_policy" ]]; then
    echo "Refusing filesystem publication into $target_dir: server-journal-v1 owns this shelf." >&2
    echo "Use the staged HTTP upload API." >&2
    return 1
  fi
  if [[ -e "$layout_marker" || -L "$layout_marker" || -e "$active_pointer" || -L "$active_pointer" ]]; then
    echo "Refusing legacy fixed-path release publication into $target_dir: immutable release shelf layout v1 is active." >&2
    echo "Use the generation-aware publisher; this writer must not mutate paths behind current.json." >&2
    return 1
  fi
}

# Fail before inspecting or staging a bundle when the destination is owned by
# the server-side durable activation journal.
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  assert_legacy_release_shelf_target "$DEPLOY_DIR"
fi

array_count() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || {
    printf '0\n'
    return 0
  }

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "set -- \"\${${array_name}[@]}\""
  local count="$#"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  printf '%s\n' "$count"
}

array_values_nul() {
  local array_name="${1:-}"
  [[ -n "$array_name" ]] || return 0

  local restore_nounset=0
  case "$-" in
    *u*)
      restore_nounset=1
      set +u
      ;;
  esac

  eval "printf '%s\\0' \"\${${array_name}[@]}\""
  local status="$?"

  if (( restore_nounset == 1 )); then
    set -u
  fi

  return "$status"
}

reject_lexical_symlink_components() {
  python3 - "$@" <<'PY'
from __future__ import annotations

import os
import stat
import sys
from pathlib import Path


for raw in sys.argv[1:]:
    path = Path(raw).absolute()
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            break
        except OSError as exc:
            print(f"Unable to inspect publication path component: {current} ({exc})", file=sys.stderr)
            raise SystemExit(1)
        if stat.S_ISLNK(metadata.st_mode):
            print(f"Publication path cannot contain symlink components: {current}", file=sys.stderr)
            raise SystemExit(1)
PY
}

resolve_release_candidate_output_dir() {
  python3 - "$1" "$BUNDLE_DIR" "$DEPLOY_DIR" "$PORTAL_DOWNLOADS_DIR" <<'PY'
from __future__ import annotations

import os
import stat
import sys
from pathlib import Path


def fail(message: str) -> None:
    print(f"Stage-only release candidate output is unsafe: {message}", file=sys.stderr)
    raise SystemExit(1)


raw_output = str(sys.argv[1] or "")
if not raw_output.strip():
    fail("CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR is required")
if "\n" in raw_output or "\r" in raw_output or "\x00" in raw_output:
    fail("output path contains control characters")

output = Path(raw_output).absolute()
bundle = Path(sys.argv[2]).absolute()
deploy = Path(sys.argv[3]).absolute()
portal = Path(sys.argv[4]).absolute()
if output == Path(output.anchor):
    fail("filesystem root cannot be used as the output directory")

current = Path(output.anchor)
for part in output.parts[1:]:
    current /= part
    try:
        metadata = current.lstat()
    except FileNotFoundError:
        break
    except OSError as exc:
        fail(f"cannot inspect {current} ({exc})")
    if stat.S_ISLNK(metadata.st_mode):
        fail(f"path contains a symlink component: {current}")

try:
    output.lstat()
except FileNotFoundError:
    pass
except OSError as exc:
    fail(f"cannot inspect requested output {output} ({exc})")
else:
    fail(f"requested output already exists: {output}")

parent = output.parent
try:
    parent_metadata = parent.lstat()
except OSError as exc:
    fail(f"output parent is unavailable: {parent} ({exc})")
if stat.S_ISLNK(parent_metadata.st_mode) or not stat.S_ISDIR(parent_metadata.st_mode):
    fail(f"output parent is not a regular directory: {parent}")

for label, protected_root in (
    ("input bundle", bundle),
    ("configured deploy directory", deploy),
    ("configured portal directory", portal),
):
    try:
        output.relative_to(protected_root)
    except ValueError:
        continue
    fail(f"output cannot be inside the {label}: {output}")

print(output)
PY
}

rewrite_release_candidate_stage_paths() {
  local candidate_root="$1"
  local final_root="$2"
  python3 - "$candidate_root" "$final_root" <<'PY'
from __future__ import annotations

import os
import stat
import sys
from pathlib import Path


candidate = Path(sys.argv[1]).absolute()
final = Path(sys.argv[2]).absolute()
source_token = os.fsencode(str(candidate))
target_token = os.fsencode(str(final))
text_suffixes = {".json", ".md", ".log", ".txt"}

for current_root, directory_names, file_names in os.walk(candidate, topdown=True, followlinks=False):
    current = Path(current_root)
    for name in directory_names:
        path = current / name
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
            print(f"Stage-only candidate contains an unsafe directory: {path}", file=sys.stderr)
            raise SystemExit(1)
    for name in file_names:
        path = current / name
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
            print(f"Stage-only candidate contains an unsafe file: {path}", file=sys.stderr)
            raise SystemExit(1)
        if path.suffix.lower() not in text_suffixes:
            continue
        payload = path.read_bytes()
        if source_token not in payload:
            continue
        rewritten = payload.replace(source_token, target_token)
        temporary = path.with_name(f".{path.name}.stage-path-rewrite-{os.getpid()}")
        with temporary.open("xb") as stream:
            stream.write(rewritten)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary, stat.S_IMODE(metadata.st_mode))
        os.replace(temporary, path)
PY
}

atomically_publish_release_candidate_stage_only() {
  local candidate_root="$1"
  local output_root="$2"
  python3 - "$candidate_root" "$output_root" <<'PY'
from __future__ import annotations

import ctypes
import errno
import hashlib
import os
import stat
import sys
from pathlib import Path


candidate = Path(sys.argv[1]).absolute()
output = Path(sys.argv[2]).absolute()


def fail(message: str) -> None:
    print(f"Stage-only release candidate publication failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def inventory(root: Path) -> dict[str, str]:
    if root.is_symlink() or not root.is_dir():
        fail(f"candidate tree is missing or symlinked: {root}")
    result: dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current = Path(current_root)
        for name in directory_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                fail(f"candidate contains a non-directory or symlink: {path}")
        for name in file_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                fail(f"candidate contains a non-regular file or symlink: {path}")
            digest = hashlib.sha256()
            descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            try:
                opened = os.fstat(descriptor)
                if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (metadata.st_dev, metadata.st_ino):
                    fail(f"candidate file changed before final inventory: {path}")
                while True:
                    chunk = os.read(descriptor, 1024 * 1024)
                    if not chunk:
                        break
                    digest.update(chunk)
                after = os.fstat(descriptor)
                if (opened.st_size, opened.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                    fail(f"candidate file changed during final inventory: {path}")
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
            result[path.relative_to(root).as_posix()] = digest.hexdigest()
    return result


try:
    output.lstat()
except FileNotFoundError:
    pass
except OSError as exc:
    fail(f"cannot inspect requested output {output} ({exc})")
else:
    fail(f"requested output already exists: {output}")

candidate_metadata = candidate.lstat()
parent_metadata = output.parent.lstat()
if stat.S_ISLNK(candidate_metadata.st_mode) or not stat.S_ISDIR(candidate_metadata.st_mode):
    fail(f"candidate is not a regular directory: {candidate}")
if stat.S_ISLNK(parent_metadata.st_mode) or not stat.S_ISDIR(parent_metadata.st_mode):
    fail(f"output parent is not a regular directory: {output.parent}")
if candidate_metadata.st_dev != parent_metadata.st_dev:
    fail("candidate and output parent are not on the same filesystem")

expected = inventory(candidate)
libc = ctypes.CDLL(None, use_errno=True)
renameat2 = getattr(libc, "renameat2", None)
if renameat2 is None:
    fail("the platform does not provide atomic no-replace renameat2")
renameat2.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
renameat2.restype = ctypes.c_int
old_parent_fd = os.open(candidate.parent, os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0))
new_parent_fd = os.open(output.parent, os.O_RDONLY | os.O_DIRECTORY | getattr(os, "O_NOFOLLOW", 0))
try:
    result = renameat2(
        old_parent_fd,
        os.fsencode(candidate.name),
        new_parent_fd,
        os.fsencode(output.name),
        1,  # RENAME_NOREPLACE
    )
    if result != 0:
        error = ctypes.get_errno()
        if error == errno.EEXIST:
            fail(f"requested output appeared before atomic publication: {output}")
        fail(f"atomic no-replace rename failed: {os.strerror(error)}")
    os.fsync(old_parent_fd)
    os.fsync(new_parent_fd)
finally:
    os.close(old_parent_fd)
    os.close(new_parent_fd)

published_metadata = output.lstat()
if (published_metadata.st_dev, published_metadata.st_ino) != (candidate_metadata.st_dev, candidate_metadata.st_ino):
    fail("published output identity differs from the validated candidate")
if inventory(output) != expected:
    fail("published output bytes differ from the validated candidate")
PY
}

resolve_release_build_provenance_validator() {
  local configured="$BUILD_PROVENANCE_VALIDATOR"
  local candidate=""
  local governed_validator=""

  for candidate in \
    "$REPO_ROOT/../chummer.run-services/scripts/release/verify_release_build_provenance_bundle.py" \
    "$REPO_ROOT/../chummer6-hub/scripts/release/verify_release_build_provenance_bundle.py"
  do
    if [[ -f "$candidate" && ! -L "$candidate" ]]; then
      governed_validator="$candidate"
      break
    fi
  done

  if [[ -z "$governed_validator" ]]; then
    echo "Desktop publication requires the governed portable release build provenance validator." >&2
    return 1
  fi
  reject_lexical_symlink_components \
    "$governed_validator" \
    "$(dirname "$governed_validator")/build_provenance_support.py"
  if [[ ! -f "$(dirname "$governed_validator")/build_provenance_support.py" ]]; then
    echo "Governed build provenance support module is missing beside: $governed_validator" >&2
    return 1
  fi

  if [[ -z "$configured" ]]; then
    printf '%s\n' "$governed_validator"
    return 0
  fi
  reject_lexical_symlink_components \
    "$configured" \
    "$(dirname "$configured")/build_provenance_support.py"
  if [[ ! -f "$configured" || ! -f "$(dirname "$configured")/build_provenance_support.py" ]]; then
    echo "Configured release build provenance validator or support module is missing: $configured" >&2
    return 1
  fi
  if ! python3 - \
    "$governed_validator" \
    "$configured" \
    "$(dirname "$governed_validator")/build_provenance_support.py" \
    "$(dirname "$configured")/build_provenance_support.py" <<'PY'
import hashlib
import sys
from pathlib import Path


def digest(path: str) -> str:
    hasher = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


if digest(sys.argv[1]) != digest(sys.argv[2]) or digest(sys.argv[3]) != digest(sys.argv[4]):
    print("Configured build provenance validator does not match the governed portable validator bytes.", file=sys.stderr)
    raise SystemExit(1)
PY
  then
    return 1
  fi
  printf '%s\n' "$governed_validator"
}

classify_release_build_provenance_requirement() {
  python3 - "$BUILD_PROVENANCE_MANIFEST_SOURCE" "$FILES_SOURCE" <<'PY'
from __future__ import annotations

import json
import os
import sys
from pathlib import Path


manifest_path = Path(sys.argv[1])
files_root = Path(sys.argv[2])


def is_desktop_artifact_name(value: object) -> bool:
    name = Path(str(value or "").strip()).name.lower()
    return name.startswith("chummer-") and (
        "-installer." in name or name.endswith("-payload.zip")
    )


desktop_files = []
if files_root.is_dir():
    desktop_files = sorted(
        path.name
        for path in files_root.iterdir()
        if path.is_file() and is_desktop_artifact_name(path.name)
    )

if not manifest_path.is_file() or manifest_path.is_symlink():
    if desktop_files:
        print(
            f"Desktop artifacts require a regular canonical source manifest before publication: {manifest_path}",
            file=sys.stderr,
        )
        raise SystemExit(2)
    raise SystemExit(1)

try:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
except (OSError, json.JSONDecodeError) as exc:
    print(f"Canonical source manifest is unreadable for build provenance: {manifest_path} ({exc})", file=sys.stderr)
    raise SystemExit(2)

rows = payload.get("artifacts") if isinstance(payload, dict) else None
if not isinstance(rows, list):
    print(f"Canonical source manifest artifacts must be a list: {manifest_path}", file=sys.stderr)
    raise SystemExit(2)

desktop_rows = []
for row in rows:
    if not isinstance(row, dict):
        continue
    platform = str(row.get("platform") or "").strip().lower()
    platform = {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(platform, platform)
    file_name = row.get("fileName") or row.get("downloadUrl") or row.get("url")
    head = str(row.get("head") or "").strip().lower()
    if platform in {"linux", "windows", "macos"} and head in {"avalonia", "blazor-desktop"}:
        desktop_rows.append(row)
    elif is_desktop_artifact_name(file_name):
        desktop_rows.append(row)

if desktop_rows:
    raise SystemExit(0)
if desktop_files:
    print(
        "Desktop artifact bytes are present but the canonical source manifest has no desktop rows; refusing unproven publication.",
        file=sys.stderr,
    )
    raise SystemExit(2)
raise SystemExit(1)
PY
}

copy_regular_tree_exact() {
  local source_root="$1"
  local target_root="$2"
  local lexical_root="${3:-$source_root}"

  python3 - "$source_root" "$target_root" "$lexical_root" <<'PY'
from __future__ import annotations

import hashlib
import os
import shutil
import stat
import sys
from pathlib import Path


source_root = Path(sys.argv[1])
target_root = Path(sys.argv[2])
lexical_root = Path(sys.argv[3])


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


for candidate in (lexical_root, source_root):
    if candidate.is_symlink():
        fail(f"Build provenance path cannot be a symlink: {candidate}")
if not source_root.is_dir():
    fail(f"Build provenance source directory is missing: {source_root}")

entries: list[tuple[Path, Path, os.stat_result]] = []
for current_root, directory_names, file_names in os.walk(source_root, topdown=True, followlinks=False):
    current = Path(current_root)
    for name in sorted(directory_names):
        path = current / name
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
            fail(f"Build provenance contains a non-directory or symlinked path: {path}")
    for name in sorted(file_names):
        path = current / name
        metadata = path.lstat()
        if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
            fail(f"Build provenance contains a non-regular or symlinked file: {path}")
        entries.append((path, path.relative_to(source_root), metadata))

if target_root.exists() or target_root.is_symlink():
    fail(f"Build provenance staging target already exists: {target_root}")
target_root.mkdir(parents=True, mode=0o700)

for source, relative, before in entries:
    destination = target_root / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    source_fd = os.open(source, flags)
    digest = hashlib.sha256()
    try:
        opened = os.fstat(source_fd)
        if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (before.st_dev, before.st_ino):
            fail(f"Build provenance source changed before copy: {source}")
        with os.fdopen(source_fd, "rb", closefd=False) as source_stream, destination.open("xb") as destination_stream:
            while True:
                chunk = source_stream.read(1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
                destination_stream.write(chunk)
            destination_stream.flush()
            os.fsync(destination_stream.fileno())
        after = os.fstat(source_fd)
        if (
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
            != (opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns)
        ):
            fail(f"Build provenance source changed during copy: {source}")
    finally:
        os.close(source_fd)
    if destination.stat().st_size != before.st_size:
        fail(f"Build provenance staging size mismatch: {relative}")
    staged_hasher = hashlib.sha256()
    with destination.open("rb") as staged_stream:
        while True:
            chunk = staged_stream.read(1024 * 1024)
            if not chunk:
                break
            staged_hasher.update(chunk)
    staged_digest = staged_hasher.hexdigest()
    if staged_digest != digest.hexdigest():
        fail(f"Build provenance staging digest mismatch: {relative}")
    os.chmod(destination, stat.S_IMODE(before.st_mode))
PY
}

stage_governed_build_provenance_validator() {
  local source_validator="$1"
  local target_dir="$2"
  python3 - "$source_validator" "$target_dir" <<'PY'
from __future__ import annotations

import hashlib
import os
import stat
import sys
from pathlib import Path


source_validator = Path(sys.argv[1])
source_support = source_validator.with_name("build_provenance_support.py")
target_dir = Path(sys.argv[2])
target_dir.mkdir(parents=True, mode=0o700)


def digest_regular(path: Path) -> str:
    metadata = path.lstat()
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
        raise ValueError(f"governed validator path is not a regular file: {path}")
    hasher = hashlib.sha256()
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        opened = os.fstat(descriptor)
        if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (metadata.st_dev, metadata.st_ino):
            raise ValueError(f"governed validator path changed before read: {path}")
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            hasher.update(chunk)
        after = os.fstat(descriptor)
        if (opened.st_size, opened.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
            raise ValueError(f"governed validator path changed during read: {path}")
    finally:
        os.close(descriptor)
    return hasher.hexdigest()


for source in (source_validator, source_support):
    expected_digest = digest_regular(source)
    destination = target_dir / source.name
    if destination.exists() or destination.is_symlink():
        raise ValueError(f"private validator staging path already exists: {destination}")
    source_descriptor = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        with destination.open("xb") as target_stream:
            while True:
                chunk = os.read(source_descriptor, 1024 * 1024)
                if not chunk:
                    break
                target_stream.write(chunk)
            target_stream.flush()
            os.fsync(target_stream.fileno())
    finally:
        os.close(source_descriptor)
    os.chmod(destination, 0o400)
    if digest_regular(source) != expected_digest or digest_regular(destination) != expected_digest:
        raise ValueError(f"governed validator bytes changed while staging: {source}")

print(target_dir / source_validator.name)
PY
}

compare_regular_tree_bytes() {
  local left_root="$1"
  local right_root="$2"
  python3 - "$left_root" "$right_root" <<'PY'
from __future__ import annotations

import hashlib
import os
import stat
import sys
from pathlib import Path


def inventory(root: Path) -> dict[str, str]:
    if not root.is_dir() or root.is_symlink():
        raise ValueError(f"tree root is missing or symlinked: {root}")
    result: dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current = Path(current_root)
        for name in directory_names:
            metadata = (current / name).lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                raise ValueError(f"tree contains a symlinked/non-directory path: {current / name}")
        for name in file_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                raise ValueError(f"tree contains a symlinked/non-regular file: {path}")
            hasher = hashlib.sha256()
            with path.open("rb") as stream:
                while True:
                    chunk = stream.read(1024 * 1024)
                    if not chunk:
                        break
                    hasher.update(chunk)
            result[path.relative_to(root).as_posix()] = hasher.hexdigest()
    return result


try:
    left = inventory(Path(sys.argv[1]))
    right = inventory(Path(sys.argv[2]))
except (OSError, ValueError) as exc:
    print(f"Build provenance tree comparison failed: {exc}", file=sys.stderr)
    raise SystemExit(1)
if left != right:
    print("Build provenance tree bytes differ from the validated source.", file=sys.stderr)
    raise SystemExit(1)
PY
}

verify_candidate_manifest_mac_identity_agreement() {
  local canonical_manifest="$1"
  local compatibility_manifest="$2"
  local files_root="$3"
  python3 - "$canonical_manifest" "$compatibility_manifest" "$files_root" <<'PY'
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path


canonical_path = Path(sys.argv[1])
compatibility_path = Path(sys.argv[2])
files_root = Path(sys.argv[3])


def fail(message: str) -> None:
    print(f"Build provenance candidate manifest disagreement: {message}", file=sys.stderr)
    raise SystemExit(1)


def load(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"{path} is unavailable or malformed ({exc})")
    if not isinstance(payload, dict):
        fail(f"{path} must contain an object")
    return payload


def file_name(row: dict) -> str:
    direct = str(row.get("fileName") or "").strip()
    if direct:
        return Path(direct).name
    return Path(str(row.get("downloadUrl") or row.get("url") or "").strip()).name


def is_mac(row: dict) -> bool:
    platform = str(row.get("platform") or "").strip().lower()
    platform = {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(platform, platform)
    name = file_name(row).lower()
    return platform == "macos" or "-osx-" in name or "-macos-" in name


def identity(row: dict) -> tuple[str, str, str, int]:
    artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
    name = file_name(row)
    digest = str(row.get("sha256") or "").strip().lower().removeprefix("sha256:")
    raw_size = row.get("sizeBytes")
    if not artifact_id or not name or len(digest) != 64 or not isinstance(raw_size, int) or isinstance(raw_size, bool):
        fail(f"incomplete Mac identity row: {artifact_id or name or '<unknown>'}")
    return artifact_id, name, digest, raw_size


canonical = load(canonical_path)
compatibility = load(compatibility_path)
canonical_rows = canonical.get("artifacts")
compatibility_rows = compatibility.get("downloads")
if not isinstance(canonical_rows, list) or not isinstance(compatibility_rows, list):
    fail("canonical artifacts and compatibility downloads must both be lists")
canonical_identities = sorted(identity(row) for row in canonical_rows if isinstance(row, dict) and is_mac(row))
compatibility_identities = sorted(identity(row) for row in compatibility_rows if isinstance(row, dict) and is_mac(row))
if canonical_identities != compatibility_identities:
    fail(
        f"canonical Mac identities {canonical_identities!r} do not match compatibility Mac identities "
        f"{compatibility_identities!r}"
    )
for _, name, expected_digest, expected_size in canonical_identities:
    artifact = files_root / name
    if artifact.is_symlink() or not artifact.is_file():
        fail(f"candidate Mac artifact is missing or symlinked: {artifact}")
    hasher = hashlib.sha256()
    with artifact.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    if artifact.stat().st_size != expected_size or hasher.hexdigest() != expected_digest:
        fail(f"candidate Mac artifact bytes do not match both manifests: {name}")
PY
}

verify_release_candidate_shelf_invariants() {
  local candidate_root="$1"
  local requested_channel="$2"
  shift 2

  python3 - "$candidate_root" "$requested_channel" "$@" <<'PY'
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path


CANONICAL_PLATFORM_FLOOR = ("linux", "windows", "macos")
DIGEST_RE = re.compile(r"^[0-9a-f]{64}$")
INSTALLER_SUFFIXES = (
    "-installer.deb",
    "-installer.exe",
    "-installer.msix",
    "-installer.dmg",
    "-installer.pkg",
)

candidate_root = Path(sys.argv[1]).absolute()
requested_channel = str(sys.argv[2] or "").strip().lower()
target_roots = [Path(value).absolute() for value in sys.argv[3:]]
canonical_path = candidate_root / "RELEASE_CHANNEL.generated.json"
compatibility_path = candidate_root / "releases.json"
files_root = candidate_root / "files"


def fail(message: str) -> None:
    print(f"Release candidate shelf invariant failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def load_required(path: Path) -> dict:
    if path.is_symlink() or not path.is_file():
        fail(f"required candidate manifest is missing or symlinked: {path}")
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"candidate manifest is unreadable: {path} ({exc})")
    if not isinstance(payload, dict):
        fail(f"candidate manifest must contain a JSON object: {path}")
    return payload


def load_optional(path: Path) -> dict:
    if path.is_symlink() or not path.is_file():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}
    return payload if isinstance(payload, dict) else {}


def normalized_platform(value: object) -> str:
    platform = str(value or "").strip().lower()
    return {"mac": "macos", "osx": "macos", "darwin": "macos"}.get(platform, platform)


def compatibility_platform(row: dict) -> str:
    for value in (row.get("platformId"), row.get("platform")):
        token = str(value or "").strip().lower()
        normalized = normalized_platform(token)
        if normalized in CANONICAL_PLATFORM_FLOOR:
            return normalized
        for platform in CANONICAL_PLATFORM_FLOOR:
            if token.startswith(f"{platform}-"):
                return platform
    return ""


def normalized_digest(value: object) -> str:
    return str(value or "").strip().lower().removeprefix("sha256:")


def manifest_version(payload: dict) -> str:
    return str(
        payload.get("version")
        or payload.get("releaseVersion")
        or payload.get("release_version")
        or ""
    ).strip()


def manifest_channel(payload: dict) -> str:
    return str(
        payload.get("channel")
        or payload.get("releaseChannel")
        or payload.get("release_channel")
        or ""
    ).strip().lower()


def file_name(row: dict) -> str:
    direct = str(row.get("fileName") or "").strip()
    raw = direct or str(row.get("downloadUrl") or row.get("url") or "").strip()
    return Path(raw).name if raw else ""


def is_installer(row: dict) -> bool:
    kind = str(row.get("kind") or "").strip().lower()
    name = file_name(row).lower()
    return kind == "installer" or name.endswith(INSTALLER_SUFFIXES)


def artifact_id(row: dict) -> str:
    return str(row.get("artifactId") or row.get("id") or "").strip()


def identity(row: dict, *, label: str) -> tuple[str, str, str, int]:
    row_id = artifact_id(row)
    declared_name = str(row.get("fileName") or "").strip()
    if declared_name and (
        declared_name in {".", ".."}
        or "/" in declared_name
        or "\\" in declared_name
        or Path(declared_name).name != declared_name
    ):
        fail(f"{label} installer {row_id or '<unknown>'} fileName must be a base name")
    name = file_name(row)
    digest = normalized_digest(row.get("sha256"))
    size = row.get("sizeBytes")
    if not row_id or not name:
        fail(f"{label} installer identity is missing artifact id or file name")
    if not DIGEST_RE.fullmatch(digest):
        fail(f"{label} installer {row_id} has an invalid sha256")
    if not isinstance(size, int) or isinstance(size, bool) or size < 0:
        fail(f"{label} installer {row_id} has an invalid sizeBytes")
    return row_id, name, digest, size


def installer_rows(payload: dict, key: str, *, label: str) -> list[dict]:
    rows = payload.get(key)
    if not isinstance(rows, list):
        fail(f"{label} {key} must be a list")
    return [row for row in rows if isinstance(row, dict) and is_installer(row)]


def indexed_identities(rows: list[dict], *, label: str) -> dict[str, tuple[dict, tuple[str, str, str, int]]]:
    indexed: dict[str, tuple[dict, tuple[str, str, str, int]]] = {}
    for row in rows:
        item = identity(row, label=label)
        row_id = item[0]
        if row_id in indexed:
            fail(f"{label} contains duplicate installer artifact id: {row_id}")
        indexed[row_id] = (row, item)
    if not indexed:
        fail(f"{label} contains no installer bindings")
    return indexed


def installer_tuples(rows: list[dict], *, label: str) -> set[tuple[str, str, str]]:
    tuples: set[tuple[str, str, str]] = set()
    for row in rows:
        head = str(row.get("head") or "").strip().lower()
        platform = normalized_platform(row.get("platform"))
        rid = str(row.get("rid") or "").strip().lower()
        if not head or not platform or not rid:
            fail(
                f"{label} installer {artifact_id(row) or file_name(row) or '<unknown>'} "
                "is missing head/platform/rid"
            )
        tuples.add((head, platform, rid))
    return tuples


canonical = load_required(canonical_path)
compatibility = load_required(compatibility_path)
canonical_version = manifest_version(canonical)
compatibility_version = manifest_version(compatibility)
canonical_channel = manifest_channel(canonical)
compatibility_channel = manifest_channel(compatibility)
if not canonical_version or canonical_version != compatibility_version:
    fail(
        "canonical and compatibility manifests must declare the same non-empty version "
        f"(canonical={canonical_version or 'missing'} compatibility={compatibility_version or 'missing'})"
    )
if not canonical_channel or canonical_channel != compatibility_channel:
    fail(
        "canonical and compatibility manifests must declare the same non-empty channel "
        f"(canonical={canonical_channel or 'missing'} compatibility={compatibility_channel or 'missing'})"
    )
if requested_channel and canonical_channel != requested_channel:
    fail(
        f"staged manifest channel {canonical_channel} does not match requested publication channel "
        f"{requested_channel}"
    )

canonical_rows = installer_rows(canonical, "artifacts", label="canonical manifest")
compatibility_rows = installer_rows(compatibility, "downloads", label="compatibility manifest")
canonical_identities = indexed_identities(canonical_rows, label="canonical manifest")
compatibility_identities = indexed_identities(compatibility_rows, label="compatibility manifest")
if set(canonical_identities) != set(compatibility_identities):
    fail(
        "canonical and compatibility installer artifact ids differ "
        f"(canonical={sorted(canonical_identities)} compatibility={sorted(compatibility_identities)})"
    )

for row_id, (canonical_row, canonical_identity) in canonical_identities.items():
    compatibility_row, compatibility_identity = compatibility_identities[row_id]
    if canonical_identity != compatibility_identity:
        fail(f"canonical and compatibility installer identity differs for {row_id}")
    canonical_head = str(canonical_row.get("head") or "").strip().lower()
    compatibility_head = str(compatibility_row.get("head") or "").strip().lower()
    if canonical_head and compatibility_head and canonical_head != compatibility_head:
        fail(f"canonical and compatibility head differs for {row_id}")
    canonical_platform = normalized_platform(canonical_row.get("platform"))
    compatibility_platform_id = compatibility_platform(compatibility_row)
    if (
        canonical_platform
        and compatibility_platform_id
        and canonical_platform != compatibility_platform_id
    ):
        fail(f"canonical and compatibility platform differs for {row_id}")
    canonical_rid = str(canonical_row.get("rid") or "").strip().lower()
    compatibility_rid = str(compatibility_row.get("rid") or "").strip().lower()
    if canonical_rid and compatibility_rid and canonical_rid != compatibility_rid:
        fail(f"canonical and compatibility rid differs for {row_id}")
    _, name, expected_digest, expected_size = canonical_identity
    artifact = files_root / name
    if artifact.is_symlink() or not artifact.is_file():
        fail(f"candidate installer bytes are missing or symlinked: {artifact}")
    if artifact.stat().st_size != expected_size:
        fail(f"candidate installer size does not match both manifests: {name}")
    hasher = hashlib.sha256()
    with artifact.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    if hasher.hexdigest() != expected_digest:
        fail(f"candidate installer digest does not match both manifests: {name}")

candidate_tuples = installer_tuples(canonical_rows, label="candidate")
coverage = canonical.get("desktopTupleCoverage")
if requested_channel == "public_stable":
    if not isinstance(coverage, dict):
        fail("public_stable candidate is missing desktopTupleCoverage")
    required_platforms = [
        normalized_platform(value) for value in coverage.get("requiredDesktopPlatforms", [])
    ]
    if tuple(required_platforms) != CANONICAL_PLATFORM_FLOOR:
        fail(
            "public_stable candidate requiredDesktopPlatforms must equal the canonical platform floor "
            f"{list(CANONICAL_PLATFORM_FLOOR)}"
        )
    if coverage.get("complete") is not True:
        fail("public_stable candidate desktopTupleCoverage.complete must be true")
    missing_lists = (
        "missingRequiredPlatforms",
        "missingRequiredHeads",
        "missingRequiredPlatformHeadPairs",
        "missingRequiredPlatformHeadRidTuples",
    )
    if any(coverage.get(key) not in (None, []) for key in missing_lists):
        fail("public_stable candidate desktopTupleCoverage still declares missing required tuples")
    missing_floor = [
        platform
        for platform in CANONICAL_PLATFORM_FLOOR
        if not any(head == "avalonia" and row_platform == platform for head, row_platform, _ in candidate_tuples)
    ]
    if missing_floor:
        fail(f"public_stable candidate is missing primary installer bindings for: {missing_floor}")


def protected_incumbent_tuples(target_root: Path) -> set[tuple[str, str, str]]:
    payload = load_optional(target_root / "RELEASE_CHANNEL.generated.json")
    coverage_payload = payload.get("desktopTupleCoverage") if isinstance(payload, dict) else None
    rows = payload.get("artifacts") if isinstance(payload, dict) else None
    if not isinstance(coverage_payload, dict) or coverage_payload.get("complete") is not True:
        return set()
    if not isinstance(rows, list):
        return set()
    required_platforms = {
        normalized_platform(value)
        for value in coverage_payload.get("requiredDesktopPlatforms", [])
        if normalized_platform(value)
    }
    incumbent_rows = [row for row in rows if isinstance(row, dict) and is_installer(row)]
    incumbent_tuples: set[tuple[str, str, str]] = set()
    for row in incumbent_rows:
        head = str(row.get("head") or "").strip().lower()
        platform = normalized_platform(row.get("platform"))
        rid = str(row.get("rid") or "").strip().lower()
        digest = normalized_digest(row.get("sha256"))
        size = row.get("sizeBytes")
        if (
            not artifact_id(row)
            or not head
            or not platform
            or not rid
            or not DIGEST_RE.fullmatch(digest)
            or not isinstance(size, int)
            or isinstance(size, bool)
            or size < 0
        ):
            return set()
        incumbent_tuples.add((head, platform, rid))
    incumbent_platforms = {platform for _, platform, _ in incumbent_tuples}
    if not required_platforms or not required_platforms.issubset(incumbent_platforms):
        return set()
    for key in (
        "missingRequiredPlatforms",
        "missingRequiredHeads",
        "missingRequiredPlatformHeadPairs",
        "missingRequiredPlatformHeadRidTuples",
    ):
        value = coverage_payload.get(key)
        if value not in (None, []):
            return set()
    return incumbent_tuples


protected: dict[Path, set[tuple[str, str, str]]] = {}
for target_root in target_roots:
    tuples = protected_incumbent_tuples(target_root)
    if tuples:
        protected[target_root] = tuples

for target_root, incumbent_tuples in protected.items():
    removed = sorted(incumbent_tuples - candidate_tuples)
    if removed:
        rendered = [f"{head}:{platform}:{rid}" for head, platform, rid in removed]
        fail(
            f"candidate would erase installer tuples from complete shelf {target_root}: {rendered}. "
            "Publish one coherent cross-platform candidate instead."
        )
PY
}

prepare_release_build_provenance() {
  local requirement_status=0
  local validator=""
  local source_root="$BUNDLE_DIR/proof/build-provenance/v1"
  local proof_root="$BUNDLE_DIR/proof"
  local build_provenance_root="$BUNDLE_DIR/proof/build-provenance"

  reject_lexical_symlink_components \
    "$BUNDLE_DIR" \
    "$BUILD_PROVENANCE_MANIFEST_SOURCE" \
    "$proof_root" \
    "$build_provenance_root" \
    "$source_root"
  if classify_release_build_provenance_requirement; then
    BUILD_PROVENANCE_REQUIRED=1
  else
    requirement_status=$?
    if (( requirement_status == 1 )); then
      BUILD_PROVENANCE_REQUIRED=0
      BUILD_PROVENANCE_STAGE_ROOT=""
      return 0
    fi
    return "$requirement_status"
  fi

  validator="$(resolve_release_build_provenance_validator)" || return 1
  for path in "$proof_root" "$build_provenance_root" "$source_root"; do
    if [[ -L "$path" ]]; then
      echo "Build provenance path cannot be a symlink: $path" >&2
      return 1
    fi
  done
  if [[ ! -d "$source_root" ]]; then
    echo "Desktop publication requires governed proof/build-provenance/v1 evidence: $source_root" >&2
    return 1
  fi

  validator="$(stage_governed_build_provenance_validator \
    "$validator" \
    "$sync_source_dir/governed-build-provenance-validator")" || return 1
  BUILD_PROVENANCE_VALIDATOR_RESOLVED="$validator"
  BUILD_PROVENANCE_STAGE_ROOT="$sync_source_dir/build-provenance-v1"
  copy_regular_tree_exact "$source_root" "$BUILD_PROVENANCE_STAGE_ROOT" "$proof_root"
  python3 -I "$validator" "$BUNDLE_DIR"
  compare_regular_tree_bytes "$source_root" "$BUILD_PROVENANCE_STAGE_ROOT"
}

preflight_release_build_provenance_target() {
  local target_root="$1"
  local path=""
  reject_lexical_symlink_components \
    "$target_root" \
    "$target_root/proof" \
    "$target_root/proof/build-provenance" \
    "$target_root/proof/build-provenance/v1"
  for path in \
    "$target_root/proof" \
    "$target_root/proof/build-provenance" \
    "$target_root/proof/build-provenance/v1"
  do
    if [[ -L "$path" ]]; then
      echo "Refusing to mutate a symlinked build provenance target: $path" >&2
      return 1
    fi
    if [[ -e "$path" && ! -d "$path" ]]; then
      echo "Refusing to mutate a non-directory build provenance target: $path" >&2
      return 1
    fi
  done
}

preflight_managed_release_target() {
  local target_root="$1"
  python3 - "$target_root" <<'PY'
from __future__ import annotations

import os
import stat
import sys
from pathlib import Path


target = Path(sys.argv[1]).absolute()
managed_files = (
    "releases.json",
    "RELEASE_CHANNEL.generated.json",
    "aur-packages.json",
    "PUBLICATION_SCOPE.generated.json",
    "RELEASE_BUILD_HANDOFF.generated.json",
    "RELEASE_BUILD_HANDOFF.generated.md",
    "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json",
    "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md",
    "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json",
    "QUARANTINED_INSTALLER_PROMOTION.generated.json",
)
managed_trees = ("files", "startup-smoke", "release-evidence")


def fail(message: str) -> None:
    print(f"Managed release target preflight failed: {message}", file=sys.stderr)
    raise SystemExit(1)


try:
    target_metadata = target.lstat()
except FileNotFoundError:
    raise SystemExit(0)
except OSError as exc:
    fail(f"cannot inspect target root {target} ({exc})")
if stat.S_ISLNK(target_metadata.st_mode) or not stat.S_ISDIR(target_metadata.st_mode):
    fail(f"target root is not a regular directory: {target}")

for name in managed_files:
    path = target / name
    try:
        metadata = path.lstat()
    except FileNotFoundError:
        continue
    except OSError as exc:
        fail(f"cannot inspect managed file {path} ({exc})")
    if stat.S_ISLNK(metadata.st_mode):
        fail(f"managed file is symlinked: {path}")
    if not stat.S_ISREG(metadata.st_mode):
        fail(f"managed file is not regular: {path}")

for name in managed_trees:
    root = target / name
    try:
        metadata = root.lstat()
    except FileNotFoundError:
        continue
    except OSError as exc:
        fail(f"cannot inspect managed tree {root} ({exc})")
    if stat.S_ISLNK(metadata.st_mode):
        fail(f"managed tree is symlinked: {root}")
    if not stat.S_ISDIR(metadata.st_mode):
        fail(f"managed tree is not a directory: {root}")
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current = Path(current_root)
        for child_name in directory_names:
            child = current / child_name
            child_metadata = child.lstat()
            if stat.S_ISLNK(child_metadata.st_mode) or not stat.S_ISDIR(child_metadata.st_mode):
                fail(f"managed tree contains an unsafe directory: {child}")
        for child_name in file_names:
            child = current / child_name
            child_metadata = child.lstat()
            if stat.S_ISLNK(child_metadata.st_mode) or not stat.S_ISREG(child_metadata.st_mode):
                fail(f"managed tree contains an unsafe file: {child}")
PY
}

sync_release_build_provenance_namespace() {
  local target_root="$1"
  local namespace_root="$target_root/proof/build-provenance"
  local target_v1="$namespace_root/v1"
  local staged_copy=""
  local backup=""

  preflight_release_build_provenance_target "$target_root"
  if (( BUILD_PROVENANCE_REQUIRED == 0 )); then
    rm -rf -- "$target_v1"
    rmdir "$namespace_root" 2>/dev/null || true
    return 0
  fi

  mkdir -p "$namespace_root"
  staged_copy="$namespace_root/.v1.publish-stage.$$"
  backup="$namespace_root/.v1.publish-backup.$$"
  rm -rf -- "$staged_copy" "$backup"
  if ! copy_regular_tree_exact "$BUILD_PROVENANCE_STAGE_ROOT" "$staged_copy"; then
    rm -rf -- "$staged_copy"
    return 1
  fi
  if ! compare_regular_tree_bytes "$BUILD_PROVENANCE_STAGE_ROOT" "$staged_copy"; then
    rm -rf -- "$staged_copy"
    return 1
  fi

  if [[ -e "$target_v1" ]]; then
    mv "$target_v1" "$backup"
  fi
  if ! mv "$staged_copy" "$target_v1"; then
    rm -rf -- "$staged_copy"
    if [[ -e "$backup" ]]; then
      mv "$backup" "$target_v1"
    fi
    return 1
  fi
  if ! compare_regular_tree_bytes "$BUILD_PROVENANCE_STAGE_ROOT" "$target_v1"; then
    rm -rf -- "$target_v1"
    if [[ -e "$backup" ]]; then
      mv "$backup" "$target_v1"
    fi
    return 1
  fi
  rm -rf -- "$backup"
}

transactionally_publish_release_candidate() {
  local candidate_root="$1"
  local validator_path="$2"
  local target_dir=""
  shift 2
  for target_dir in "$@"; do
    assert_legacy_release_shelf_target "$target_dir"
  done
  python3 - "$candidate_root" "$validator_path" "$@" <<'PY'
from __future__ import annotations

import hashlib
import os
import signal
import shutil
import stat
import subprocess
import sys
import uuid
from pathlib import Path


candidate = Path(sys.argv[1]).absolute()
validator_path = Path(sys.argv[2]).absolute()
raw_targets = [Path(value).absolute() for value in sys.argv[3:]]
targets: list[Path] = []
seen: set[str] = set()
for target in raw_targets:
    key = os.path.normcase(str(target))
    if key not in seen:
        targets.append(target)
        seen.add(key)


def fail(message: str) -> None:
    print(f"Transactional release candidate cutover failed: {message}", file=sys.stderr)
    raise RuntimeError(message)


def reject_lexical_symlinks(path: Path) -> None:
    current = Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            break
        if stat.S_ISLNK(metadata.st_mode):
            fail(f"path contains a symlink component: {current}")


def regular_tree_inventory(root: Path) -> dict[str, str]:
    if root.is_symlink() or not root.is_dir():
        fail(f"required regular tree is missing or symlinked: {root}")
    inventory: dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        current = Path(current_root)
        for name in directory_names:
            metadata = (current / name).lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
                fail(f"candidate tree contains a non-directory or symlink: {current / name}")
        for name in file_names:
            path = current / name
            metadata = path.lstat()
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(metadata.st_mode):
                fail(f"candidate tree contains a non-regular file or symlink: {path}")
            hasher = hashlib.sha256()
            flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
            descriptor = os.open(path, flags)
            try:
                opened = os.fstat(descriptor)
                if not stat.S_ISREG(opened.st_mode) or (opened.st_dev, opened.st_ino) != (metadata.st_dev, metadata.st_ino):
                    fail(f"candidate file changed before inventory: {path}")
                while True:
                    chunk = os.read(descriptor, 1024 * 1024)
                    if not chunk:
                        break
                    hasher.update(chunk)
                after = os.fstat(descriptor)
                if (opened.st_size, opened.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                    fail(f"candidate file changed during inventory: {path}")
            finally:
                os.close(descriptor)
            inventory[path.relative_to(root).as_posix()] = hasher.hexdigest()
    return inventory


def managed_path_snapshot(root: Path, relative: Path) -> tuple[str, object]:
    path = root / relative
    if path.is_symlink():
        fail(f"managed path is symlinked: {path}")
    if not path.exists():
        return "missing", None
    if path.is_dir():
        return "directory", regular_tree_inventory(path)
    if path.is_file():
        hasher = hashlib.sha256()
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(path, flags)
        try:
            metadata = os.fstat(descriptor)
            if not stat.S_ISREG(metadata.st_mode):
                fail(f"managed path is not a regular file: {path}")
            while True:
                chunk = os.read(descriptor, 1024 * 1024)
                if not chunk:
                    break
                hasher.update(chunk)
        finally:
            os.close(descriptor)
        return "file", hasher.hexdigest()
    fail(f"managed path is not regular: {path}")


def copy_regular_tree(source: Path, destination: Path) -> None:
    expected = regular_tree_inventory(source)
    shutil.copytree(source, destination, copy_function=shutil.copy2)
    if regular_tree_inventory(destination) != expected:
        fail(f"copied tree differs from candidate: {source}")


def replace_path_from_candidate(stage: Path, relative: Path, *, required: bool = False) -> None:
    source = candidate / relative
    destination = stage / relative
    if destination.is_symlink():
        fail(f"managed target path is symlinked: {destination}")
    if destination.is_dir():
        shutil.rmtree(destination)
    elif destination.exists():
        destination.unlink()
    if not source.exists():
        if required:
            fail(f"candidate is missing required path: {source}")
        return
    if source.is_symlink():
        fail(f"candidate managed path is symlinked: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        copy_regular_tree(source, destination)
    elif source.is_file():
        shutil.copy2(source, destination, follow_symlinks=False)
    else:
        fail(f"candidate managed path is not regular: {source}")


def apply_candidate(stage: Path) -> None:
    replace_path_from_candidate(stage, Path("releases.json"), required=True)
    replace_path_from_candidate(stage, Path("RELEASE_CHANNEL.generated.json"), required=True)
    replace_path_from_candidate(stage, Path("files"), required=True)
    replace_path_from_candidate(stage, Path("startup-smoke"))
    replace_path_from_candidate(stage, Path("aur-packages.json"))
    replace_path_from_candidate(stage, Path("PUBLICATION_SCOPE.generated.json"))
    replace_path_from_candidate(stage, Path("RELEASE_BUILD_HANDOFF.generated.json"))
    replace_path_from_candidate(stage, Path("RELEASE_BUILD_HANDOFF.generated.md"))
    replace_path_from_candidate(stage, Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"))
    replace_path_from_candidate(stage, Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"))
    replace_path_from_candidate(stage, Path("UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"))
    replace_path_from_candidate(stage, Path("QUARANTINED_INSTALLER_PROMOTION.generated.json"))
    replace_path_from_candidate(stage, Path("release-evidence"))

    candidate_v1 = candidate / "proof" / "build-provenance" / "v1"
    target_v1 = stage / "proof" / "build-provenance" / "v1"
    for path in (stage / "proof", stage / "proof" / "build-provenance", target_v1):
        if path.is_symlink():
            fail(f"managed target proof path is symlinked: {path}")
    if target_v1.is_dir():
        shutil.rmtree(target_v1)
    elif target_v1.exists():
        target_v1.unlink()
    if candidate_v1.is_dir() and not candidate_v1.is_symlink():
        target_v1.parent.mkdir(parents=True, exist_ok=True)
        copy_regular_tree(candidate_v1, target_v1)
    elif candidate_v1.exists() or candidate_v1.is_symlink():
        fail(f"candidate governed proof namespace is not a regular directory: {candidate_v1}")


reject_lexical_symlinks(candidate)
validator_enabled = str(sys.argv[2]).strip() != "-"
if validator_enabled:
    reject_lexical_symlinks(validator_path)
    if validator_path.is_symlink() or not validator_path.is_file():
        fail(f"private governed validator is unavailable: {validator_path}")
if not targets:
    fail("no publication targets were provided")
for target in targets:
    reject_lexical_symlinks(target)

transaction_id = uuid.uuid4().hex
fault_after_commits = int(os.environ.get("CHUMMER_RELEASE_TRANSACTION_FAULT_AFTER_COMMITS", "0") or "0")
stages: dict[Path, Path] = {}
backups: dict[Path, Path | None] = {}
committed: list[Path] = []


def abort_transaction(signum: int, _frame: object) -> None:
    raise RuntimeError(f"received signal {signum} during release cutover")


for signal_number in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
    signal.signal(signal_number, abort_transaction)

try:
    for target in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage = target.parent / f".{target.name}.release-stage-{transaction_id}"
        backup = target.parent / f".{target.name}.release-backup-{transaction_id}"
        if stage.exists() or backup.exists():
            fail(f"transaction path collision beside target: {target}")
        stages[target] = stage
        backups[target] = backup if target.exists() else None
        if target.exists():
            if target.is_symlink() or not target.is_dir():
                fail(f"publication target is not a regular directory: {target}")
            shutil.copytree(target, stage, symlinks=True, copy_function=shutil.copy2)
        else:
            stage.mkdir(mode=0o755)
        apply_candidate(stage)

    candidate_files = regular_tree_inventory(candidate / "files")
    candidate_proof_root = candidate / "proof" / "build-provenance" / "v1"
    candidate_proof = (
        regular_tree_inventory(candidate_proof_root)
        if candidate_proof_root.is_dir() and not candidate_proof_root.is_symlink()
        else None
    )
    exact_managed_paths = (
        Path("startup-smoke"),
        Path("aur-packages.json"),
        Path("PUBLICATION_SCOPE.generated.json"),
        Path("RELEASE_BUILD_HANDOFF.generated.json"),
        Path("RELEASE_BUILD_HANDOFF.generated.md"),
        Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"),
        Path("WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.md"),
        Path("UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"),
        Path("QUARANTINED_INSTALLER_PROMOTION.generated.json"),
        Path("release-evidence"),
    )
    candidate_managed_snapshots = {
        relative: managed_path_snapshot(candidate, relative)
        for relative in exact_managed_paths
    }
    if validator_enabled:
        candidate_validation = subprocess.run(
            [sys.executable, "-I", str(validator_path), str(candidate)],
            capture_output=True,
            text=True,
            check=False,
        )
        if candidate_validation.returncode != 0:
            fail(
                "private governed validator rejected the sealed candidate: "
                f"{candidate_validation.stderr.strip() or candidate_validation.stdout.strip()}"
            )
    for target, stage in stages.items():
        if regular_tree_inventory(stage / "files") != candidate_files:
            fail(f"staged target file bytes differ: {target}")
        stage_proof_root = stage / "proof" / "build-provenance" / "v1"
        stage_proof = (
            regular_tree_inventory(stage_proof_root)
            if stage_proof_root.is_dir() and not stage_proof_root.is_symlink()
            else None
        )
        if stage_proof != candidate_proof:
            fail(f"staged target proof bytes differ: {target}")
        for name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
            if (stage / name).read_bytes() != (candidate / name).read_bytes():
                fail(f"staged target manifest bytes differ: {target / name}")
        for relative, expected in candidate_managed_snapshots.items():
            if managed_path_snapshot(stage, relative) != expected:
                fail(f"staged target managed path differs: {target / relative}")
        if validator_enabled:
            stage_validation = subprocess.run(
                [sys.executable, "-I", str(validator_path), str(stage)],
                capture_output=True,
                text=True,
                check=False,
            )
            if stage_validation.returncode != 0:
                fail(
                    f"private governed validator rejected staged target {target}: "
                    f"{stage_validation.stderr.strip() or stage_validation.stdout.strip()}"
                )

    for target in targets:
        stage = stages[target]
        backup = backups[target]
        if backup is not None:
            os.replace(target, backup)
        try:
            os.replace(stage, target)
        except Exception:
            if backup is not None and backup.exists() and not target.exists():
                os.replace(backup, target)
            raise
        committed.append(target)
        if fault_after_commits > 0 and len(committed) >= fault_after_commits:
            fail(f"fault injection after {len(committed)} committed target(s)")

    for target in committed:
        if regular_tree_inventory(target / "files") != candidate_files:
            fail(f"committed target file bytes differ: {target}")
        target_proof_root = target / "proof" / "build-provenance" / "v1"
        target_proof = (
            regular_tree_inventory(target_proof_root)
            if target_proof_root.is_dir() and not target_proof_root.is_symlink()
            else None
        )
        if target_proof != candidate_proof:
            fail(f"committed target proof bytes differ: {target}")
        for name in ("releases.json", "RELEASE_CHANNEL.generated.json"):
            if (target / name).read_bytes() != (candidate / name).read_bytes():
                fail(f"committed target manifest bytes differ: {target / name}")
        for relative, expected in candidate_managed_snapshots.items():
            if managed_path_snapshot(target, relative) != expected:
                fail(f"committed target managed path differs: {target / relative}")
except BaseException as exc:
    for target in reversed(committed):
        backup = backups.get(target)
        failed = target.parent / f".{target.name}.release-failed-{transaction_id}"
        if target.exists():
            os.replace(target, failed)
        if backup is not None and backup.exists():
            os.replace(backup, target)
        if failed.exists():
            shutil.rmtree(failed)
    for stage in stages.values():
        if stage.exists():
            shutil.rmtree(stage)
    for backup in backups.values():
        if backup is not None and backup.exists():
            shutil.rmtree(backup)
    print(f"Transactional release candidate cutover rolled back: {exc}", file=sys.stderr)
    raise SystemExit(1)
else:
    for backup in backups.values():
        if backup is not None and backup.exists():
            shutil.rmtree(backup)
    print(f"transactional_release_candidate_targets={len(targets)}")
PY
}

refresh_release_build_handoff() {
  local bundle_dir="$1"
  local handoff_script="${CHUMMER_RELEASE_BUILD_HANDOFF_SCRIPT_PATH:-$SCRIPT_DIR/materialize_release_candidate_handoff.py}"

  if [[ ! -f "$bundle_dir/RELEASE_CHANNEL.generated.json" ]]; then
    return 0
  fi

  if [[ ! -f "$handoff_script" ]]; then
    echo "Skipping release build handoff refresh because the materializer is missing: $handoff_script" >&2
    return 0
  fi

  if ! python3 "$handoff_script" "$bundle_dir" >/dev/null; then
    echo "Skipping release build handoff refresh because materialization failed for bundle: $bundle_dir" >&2
    return 0
  fi
}

emit_windows_visual_proof_handoff_guidance() {
  python3 - "$@" <<'PY' || true
from __future__ import annotations

import json
import sys
from pathlib import Path


def load_json(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def normalize(value: object) -> str:
    return str(value or "").strip()


roots = [Path(item) for item in sys.argv[1:] if normalize(item)]
handoff_payload = {}
handoff_path = None

for root in roots:
    direct_path = root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json"
    if direct_path.is_file():
        handoff_payload = load_json(direct_path)
        handoff_path = direct_path
        break

for root in roots:
    if handoff_payload:
        break
    release_handoff_path = root / "RELEASE_BUILD_HANDOFF.generated.json"
    if release_handoff_path.is_file():
        release_handoff = load_json(release_handoff_path)
        candidate = release_handoff.get("windows_visual_proof_handoff")
        if isinstance(candidate, dict):
            handoff_payload = candidate
            candidate_path = normalize(candidate.get("json_path"))
            if candidate_path:
                handoff_path = Path(candidate_path)
            break

if not handoff_payload:
    raise SystemExit(0)

status = normalize(handoff_payload.get("status"))
summary = normalize(handoff_payload.get("summary"))
next_actions = handoff_payload.get("next_actions") if isinstance(handoff_payload.get("next_actions"), list) else []
json_path = normalize(handoff_payload.get("json_path")) or str(handoff_path or "")

if json_path:
    print(f"Windows visual proof handoff: {json_path}", file=sys.stderr)
if status:
    print(f"Windows visual proof status: {status}", file=sys.stderr)
if summary:
    print(f"Windows visual proof summary: {summary}", file=sys.stderr)
for index, action in enumerate(next_actions[:2], start=1):
    normalized_action = normalize(action)
    if normalized_action:
        print(f"Windows visual proof next action {index}: {normalized_action}", file=sys.stderr)
PY
}

forced_preview_nightly_visual_handoff_allowed() {
  local bundle_dir="$1"
  local deploy_dir="$2"
  local release_channel

  if ! to_bool "$FORCE_NIGHTLY_PUBLISH"; then
    return 1
  fi

  release_channel="$(echo "${RELEASE_CHANNEL:-preview}" | tr '[:upper:]' '[:lower:]')"
  if [[ "$release_channel" != "preview" ]]; then
    return 1
  fi

  python3 - "$bundle_dir" "$deploy_dir" <<'PY'
from __future__ import annotations

import json
import os
import sys
from pathlib import Path


ALLOWED_BLOCKER = "Windows visual proof is still outstanding for the staged installer bytes."
ALLOWED_COVERAGE_BLOCKER = "macOS tuple is missing entirely from the candidate bundle."


def load_json(path: Path) -> dict:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        return {}
    return payload if isinstance(payload, dict) else {}


def normalize(value: object) -> str:
    return str(value or "").strip().lower()


roots = [Path(item) for item in sys.argv[1:] if str(item or "").strip()]
handoff: dict = {}
for root in roots:
    candidate = load_json(root / "RELEASE_BUILD_HANDOFF.generated.json")
    if candidate:
        handoff = candidate
        break

if not handoff:
    raise SystemExit(1)

visual = handoff.get("windows_visual_proof_handoff")
if not isinstance(visual, dict):
    visual = {}
    for root in roots:
        visual = load_json(root / "WINDOWS_INSTALLER_VISUAL_PROOF_HANDOFF.generated.json")
        if visual:
            break

blockers = handoff.get("blockers")
stage_only = str(os.environ.get("CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY") or "").strip().lower() in {
    "1", "true", "yes", "on"
}
require_complete_coverage = str(
    os.environ.get("CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE") or "1"
).strip().lower() not in {"0", "false", "no", "off"}
allow_incomplete_preview_coverage = stage_only or not require_complete_coverage
if allow_incomplete_preview_coverage:
    if blockers != [ALLOWED_COVERAGE_BLOCKER, ALLOWED_BLOCKER]:
        raise SystemExit(1)
    if handoff.get("missing_required_platforms") != ["macos"]:
        raise SystemExit(1)
else:
    if blockers != [ALLOWED_BLOCKER]:
        raise SystemExit(1)
if normalize(handoff.get("channel")) != "preview":
    raise SystemExit(1)
if handoff.get("stage_proof_complete") is not False:
    raise SystemExit(1)
if normalize(visual.get("status")) != "ready_for_windows_host":
    raise SystemExit(1)
if visual.get("only_blocker_is_visual_proof") is not True:
    raise SystemExit(1)

print("ok")
PY
}

require_public_stable_root_blocker_clearance() {
  local release_channel="${1:-}"
  local normalized_release_channel=""

  normalized_release_channel="$(echo "$release_channel" | tr '[:upper:]' '[:lower:]')"
  if [[ "$normalized_release_channel" != "public_stable" ]]; then
    return 0
  fi

  python3 - "$ROOT_RELEASE_BLOCKERS_PATH" "$PUBLIC_STABLE_BLOCKERS_MAX_AGE_SECONDS" <<'PY'
from __future__ import annotations

import json
import sys
from datetime import UTC, datetime
from pathlib import Path


ALLOWED_BLOCKER = "release_posture:non_flagship_channel"


def fail(message: str) -> None:
    print(message, file=sys.stderr)
    raise SystemExit(1)


path = Path(sys.argv[1])
max_age_raw = str(sys.argv[2] if len(sys.argv) > 2 else "86400").strip()
try:
    max_age_seconds = int(max_age_raw)
except ValueError:
    fail(f"Public stable publication requires a numeric blocker-truth max age, got: {max_age_raw}")

if not path.is_file():
    fail(f"Public stable publication requires root release blocker truth, but the receipt is missing: {path}")

try:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
except Exception as exc:  # pragma: no cover - surfaced via stderr
    fail(f"Public stable publication requires readable root release blocker truth: {path} ({exc})")

generated_at_text = str(payload.get("generated_at") or payload.get("generated_at_utc") or "").strip()
if not generated_at_text:
    fail(f"Public stable publication requires timestamped root release blocker truth: {path}")
try:
    normalized_generated_at = generated_at_text.replace("Z", "+00:00")
    generated_at = datetime.fromisoformat(normalized_generated_at)
except ValueError as exc:
    fail(f"Public stable publication requires parseable root release blocker timestamp: {generated_at_text} ({exc})")
if generated_at.tzinfo is None:
    generated_at = generated_at.replace(tzinfo=UTC)
age_seconds = (datetime.now(UTC) - generated_at.astimezone(UTC)).total_seconds()
if max_age_seconds >= 0 and age_seconds > max_age_seconds:
    fail(
        "Public stable publication requires fresh root release blocker truth. "
        f"source={path} generated_at={generated_at_text} "
        f"age_seconds={int(age_seconds)} max_age_seconds={max_age_seconds}"
    )

def collect_blocker_ids(value: object) -> list[str] | None:
    if not isinstance(value, list):
        return None

    blocker_ids: list[str] = []
    for entry in value:
        if isinstance(entry, dict):
            blocker_id = str(entry.get("blocker_id") or entry.get("id") or "").strip()
        else:
            blocker_id = str(entry or "").strip()
        if blocker_id:
            blocker_ids.append(blocker_id)
    return blocker_ids


blocker_ids = collect_blocker_ids(payload.get("root_blocker_ids"))
if blocker_ids is None:
    blocker_ids = collect_blocker_ids(payload.get("root_blockers"))
if blocker_ids is None:
    blocker_ids = collect_blocker_ids(payload.get("blockers"))
if blocker_ids is None:
    fail(f"Public stable publication requires a blockers list in root release blocker truth: {path}")

if blocker_ids != [ALLOWED_BLOCKER]:
    fail(
        "Public stable publication is blocked by root release truth. "
        f"source={path} generated_at={generated_at_text or 'unknown'} "
        f"root_blocker_ids={','.join(blocker_ids) or '(none)'} "
        f"allowed_root_blocker_id={ALLOWED_BLOCKER}"
    )
PY
}

is_public_artifact() {
  local artifact_name
  artifact_name="$(basename "$1")"
  case "$artifact_name" in
    chummer-*-win-*-payload.zip.json)
      return 0
      ;;
    chummer-*-win-*-payload.zip)
      return 0
      ;;
    chummer-*-win-*.zip|chummer-*-win-*.tar.gz|chummer-*-win-*.exe)
      if [[ "$artifact_name" != *-installer.exe ]]; then
        return 1
      fi
      ;;
  esac
  return 0
}

verify_windows_installer_payload_gate() {
  if [[ ! -f "$SCRIPT_DIR/verify-windows-installer-payloads.py" ]]; then
    echo "Missing Windows installer payload gate: $SCRIPT_DIR/verify-windows-installer-payloads.py" >&2
    exit 1
  fi

  local -a gate_args=(--files-dir "$FILES_SOURCE" --require-embedded-bootstrap-metadata --require-manifest-row)
  local -a installer_candidates=()
  [[ -f "$MANIFEST_SOURCE" ]] && gate_args+=(--manifest "$MANIFEST_SOURCE")
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$BUNDLE_DIR" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  while IFS= read -r installer_path; do
    [[ -n "$installer_path" ]] || continue
    installer_candidates+=("$installer_path")
  done < <(find "$FILES_SOURCE" -maxdepth 1 -type f -name 'chummer-*-win-*-installer.exe' | sort)
  local installer_candidate_count
  installer_candidate_count="$(array_count installer_candidates)"
  if (( installer_candidate_count == 0 )); then
    gate_args+=(--allow-empty)
  else
    local installer_path=""
    for installer_path in "${installer_candidates[@]}"; do
      gate_args+=(--installer "$installer_path")
    done
  fi
  python3 "$SCRIPT_DIR/verify-windows-installer-payloads.py" "${gate_args[@]}"
}

verify_windows_desktop_exit_gate() {
  local gate_output
  local visual_proof_path="${CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH:-${BUNDLE_DIR}/WINDOWS_INSTALLER_VISUAL_PROOF.generated.json}"
  gate_output="$DEPLOY_DIR/UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"

  if [[ ! -f "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" ]]; then
    echo "Missing Windows desktop exit gate: $SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >&2
    exit 1
  fi

  if ! CHUMMER_WINDOWS_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    CHUMMER_RUN_SERVICES_RELEASE_CHANNEL_PATH="$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    CHUMMER_WINDOWS_LOCAL_DESKTOP_FILES_ROOT="$DEPLOY_DIR/files" \
    CHUMMER_WINDOWS_INSTALLER_VISUAL_PROOF_PATH="$visual_proof_path" \
    CHUMMER_UI_WINDOWS_DESKTOP_EXIT_GATE_PATH="$gate_output" \
    bash "$SCRIPT_DIR/materialize-windows-desktop-exit-gate.sh" >/dev/null
  then
    refresh_release_build_handoff "$DEPLOY_DIR"
    emit_windows_visual_proof_handoff_guidance "$BUNDLE_DIR" "$DEPLOY_DIR"
    if forced_preview_nightly_visual_handoff_allowed "$DEPLOY_DIR" "$BUNDLE_DIR" >/dev/null; then
      echo "Forced preview nightly publication continuing with Windows visual proof handoff only; stable promotion remains blocked." >&2
      return 0
    fi
    echo "Published downloads shelf failed Windows desktop exit gate verification. Use the Windows visual proof handoff above." >&2
    exit 1
  fi
  refresh_release_build_handoff "$DEPLOY_DIR"
}

strip_non_public_manifest_rows() {
  local manifest_path="$1"
  python3 - "$manifest_path" "$REPO_ROOT" <<'PY'
import importlib.util
import json
import sys
from pathlib import Path


def file_name_for(row: object) -> str:
    if not isinstance(row, dict):
        return ""
    file_name = str(row.get("fileName") or "").strip()
    if file_name:
        return file_name
    raw = str(row.get("downloadUrl") or row.get("url") or "").strip()
    return Path(raw).name if raw else ""


def is_public_file_name(file_name: str) -> bool:
    name = file_name.strip().lower()
    if not name:
        return False
    if name.endswith(
        (
            "-installer.deb",
            "-installer.exe",
            "-installer.msix",
            "-installer.dmg",
            "-installer.pkg",
        )
    ):
        return True
    if name.endswith(".tar.gz") and ("-osx-" in name or "-macos-" in name):
        return True
    if name.endswith((".zip", ".tar.gz")):
        return False
    if name.endswith(".exe") and not name.endswith("-installer.exe"):
        return False
    return False


path = Path(sys.argv[1])
repo_root = Path(sys.argv[2])
payload = json.loads(path.read_text(encoding="utf-8-sig"))
if not isinstance(payload, dict):
    raise SystemExit(0)

allowed_artifact_ids: set[str] = set()
for key in ("artifacts", "downloads"):
    rows = payload.get(key)
    if not isinstance(rows, list):
        continue
    filtered = []
    for row in rows:
        name = file_name_for(row)
        if not is_public_file_name(name):
            continue
        filtered.append(row)
        if isinstance(row, dict):
            artifact_id = str(row.get("artifactId") or row.get("id") or "").strip()
            if artifact_id:
                allowed_artifact_ids.add(artifact_id)
    payload[key] = filtered

for key in ("installAwareArtifactRegistry", "desktopSurfaceRefs", "artifactIdentityRegistry", "artifactPublicationBindings"):
    rows = payload.get(key)
    if not isinstance(rows, list) or not allowed_artifact_ids:
        continue
    payload[key] = [
        row for row in rows
        if isinstance(row, dict) and str(row.get("artifactId") or row.get("id") or "").strip() in allowed_artifact_ids
    ]

coverage = payload.get("desktopTupleCoverage")
if isinstance(coverage, dict) and allowed_artifact_ids:
    promoted_rows = [
        row for row in coverage.get("promotedInstallerTuples") or []
        if isinstance(row, dict) and str(row.get("artifactId") or "").strip() in allowed_artifact_ids
    ]
    promoted_rows.sort(key=lambda row: (
        str(row.get("platform") or "").strip(),
        str(row.get("head") or "").strip(),
        str(row.get("rid") or "").strip(),
        str(row.get("artifactId") or "").strip(),
    ))
    promoted_platforms = sorted({str(row.get("platform") or "").strip() for row in promoted_rows if str(row.get("platform") or "").strip()})
    promoted_heads = sorted({str(row.get("head") or "").strip() for row in promoted_rows if str(row.get("head") or "").strip()})
    promoted_pairs = {
        f"{str(row.get('head') or '').strip()}:{str(row.get('platform') or '').strip()}"
        for row in promoted_rows
        if str(row.get("head") or "").strip() and str(row.get("platform") or "").strip()
    }
    promoted_tuple_ids = {
        f"{str(row.get('head') or '').strip()}:{str(row.get('rid') or '').strip()}:{str(row.get('platform') or '').strip()}"
        for row in promoted_rows
        if str(row.get("head") or "").strip() and str(row.get("rid") or "").strip() and str(row.get("platform") or "").strip()
    }
    # Publication filtering must never redefine the product's required desktop
    # floor. Missing tuples stay visible as proof-required release truth.
    required_platforms = [
        str(platform or "").strip()
        for platform in (coverage.get("requiredDesktopPlatforms") or [])
        if str(platform or "").strip()
    ] or promoted_platforms
    required_heads = [
        str(head or "").strip()
        for head in (coverage.get("requiredDesktopHeads") or [])
        if str(head or "").strip()
    ] or promoted_heads
    required_head_rid_tuples = [
        str(tuple_id or "").strip()
        for tuple_id in (coverage.get("requiredDesktopPlatformHeadRidTuples") or [])
        if str(tuple_id or "").strip()
    ]
    if not required_head_rid_tuples:
        required_head_rid_tuples = sorted(promoted_tuple_ids)
    coverage["promotedInstallerTuples"] = promoted_rows
    coverage["requiredDesktopPlatforms"] = required_platforms
    coverage["requiredDesktopHeads"] = required_heads
    coverage["promotedPlatformHeads"] = {
        platform: sorted({
            str(row.get("head") or "").strip()
            for row in promoted_rows
            if str(row.get("platform") or "").strip() == platform and str(row.get("head") or "").strip()
        })
        for platform in required_platforms
    }
    coverage["requiredDesktopPlatformHeadRidTuples"] = sorted(required_head_rid_tuples)
    coverage["promotedPlatformHeadRidTuples"] = sorted(promoted_tuple_ids)
    coverage["missingRequiredPlatforms"] = [
        platform for platform in required_platforms
        if platform not in promoted_platforms
    ]
    coverage["missingRequiredHeads"] = [
        head for head in required_heads
        if head not in promoted_heads
    ]
    coverage["missingRequiredPlatformHeadPairs"] = [
        f"{head}:{platform}"
        for platform in required_platforms
        for head in required_heads
        if f"{head}:{platform}" not in promoted_pairs
    ]
    coverage["missingRequiredPlatformHeadRidTuples"] = [
        tuple_id for tuple_id in coverage["requiredDesktopPlatformHeadRidTuples"]
        if tuple_id not in promoted_tuple_ids
    ]
    coverage["externalProofRequests"] = [
        row for row in coverage.get("externalProofRequests") or []
        if isinstance(row, dict)
    ]
    route_truth = coverage.get("desktopRouteTruth")
    if isinstance(route_truth, list):
        coverage["desktopRouteTruth"] = [
            row for row in route_truth
            if not isinstance(row, dict)
            or not str(row.get("artifactId") or "").strip()
            or str(row.get("artifactId") or "").strip() in allowed_artifact_ids
        ]
    coverage["complete"] = not (
        coverage["missingRequiredPlatforms"]
        or coverage["missingRequiredHeads"]
        or coverage["missingRequiredPlatformHeadPairs"]
        or coverage["missingRequiredPlatformHeadRidTuples"]
    )

verifier_path = repo_root.parent / "chummer-hub-registry" / "scripts" / "verify_public_release_channel.py"
if verifier_path.is_file():
    spec = importlib.util.spec_from_file_location("verify_public_release_channel", verifier_path)
    if spec is not None and spec.loader is not None:
        verifier = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(verifier)
        coverage = payload.get("desktopTupleCoverage")
        if isinstance(coverage, dict) and hasattr(verifier, "expected_desktop_route_truth_rows"):
            coverage["desktopRouteTruth"] = verifier.expected_desktop_route_truth_rows(payload)
        if hasattr(verifier, "expected_public_trust_metrics"):
            payload["publicTrustMetrics"] = verifier.expected_public_trust_metrics(payload)
        payload["registryBoundaryCoverage"] = verifier.expected_registry_boundary_coverage(payload)

path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

if [[ -z "$PUBLIC_SKIP_STARTUP_SMOKE_FILTER" ]]; then
  if [[ "${RELEASE_CHANNEL:-preview}" =~ ^[Pp][Rr][Ee][Vv][Ii][Ee][Ww]$ ]]; then
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="true"
  else
    PUBLIC_SKIP_STARTUP_SMOKE_FILTER="false"
  fi
fi

bundle_manifest_matches_files() {
  local manifest_path="$1"
  local files_root="$2"
  python3 - "$manifest_path" "$files_root" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

manifest_path = Path(sys.argv[1])
files_root = Path(sys.argv[2])

payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
downloads = payload.get("downloads") or []
failures: list[str] = []
seen: set[str] = set()
sidecars = {
    "aur-packages.json",
    "chummer6-bin-aur-source.tar.gz",
    "chummer6-bin.PKGBUILD",
    "chummer6-bin.SRCINFO",
}

for artifact in downloads:
    if not isinstance(artifact, dict):
        continue
    url = str(artifact.get("url") or "").strip()
    file_name = Path(url).name if url else ""
    if not file_name:
        continue
    seen.add(file_name)
    file_path = files_root / file_name
    if not file_path.is_file():
        failures.append(f"manifest references missing file {file_name}")
        continue
    actual_size = file_path.stat().st_size
    expected_size = int(artifact.get("sizeBytes") or 0)
    if expected_size and expected_size != actual_size:
        failures.append(f"{file_name}: size {actual_size} != manifest {expected_size}")
    expected_sha = str(artifact.get("sha256") or "").strip().lower()
    if expected_sha:
        digest = hashlib.sha256(file_path.read_bytes()).hexdigest()
        if digest != expected_sha:
            failures.append(f"{file_name}: sha256 {digest} != manifest {expected_sha}")

for file_path in sorted(files_root.iterdir()):
    if not file_path.is_file():
        continue
    if file_path.name in sidecars:
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip"):
        continue
    if file_path.name.startswith("chummer-") and file_path.name.endswith("-payload.zip.json"):
        continue
    if file_path.name not in seen:
        failures.append(f"bundle contains extra file not present in manifest: {file_path.name}")

if failures:
    print("false")
    for failure in failures:
        print(failure)
else:
    print("true")
PY
}

if [[ -z "$PORTAL_MANIFEST_PATH" ]]; then
  if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
    PORTAL_MANIFEST_PATH="$DEPLOY_DIR/releases.json"
  elif [[ "$(realpath -m "$DEPLOY_DIR")" == "$(realpath -m "$REPO_ROOT/Docker/Downloads")" ]]; then
    PORTAL_MANIFEST_PATH="$REPO_ROOT/Chummer.Portal/downloads/releases.json"
  else
    PORTAL_MANIFEST_PATH="$DEPLOY_DIR/releases.json"
  fi
fi

if [[ -z "$PORTAL_DOWNLOADS_DIR" ]]; then
  PORTAL_DOWNLOADS_DIR="$(dirname "$PORTAL_MANIFEST_PATH")"
fi

if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  RELEASE_CANDIDATE_OUTPUT_DIR="$(resolve_release_candidate_output_dir "$RELEASE_CANDIDATE_OUTPUT_DIR")"
elif [[ -n "$RELEASE_CANDIDATE_OUTPUT_DIR" ]]; then
  echo "CHUMMER_RELEASE_CANDIDATE_OUTPUT_DIR requires CHUMMER_RELEASE_CANDIDATE_STAGE_ONLY=1." >&2
  exit 1
fi

if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  assert_legacy_release_shelf_target "$DEPLOY_DIR"
  if [[ "$(realpath -m "$PORTAL_DOWNLOADS_DIR")" != "$(realpath -m "$DEPLOY_DIR")" ]]; then
    assert_legacy_release_shelf_target "$PORTAL_DOWNLOADS_DIR"
  fi
fi

reject_lexical_symlink_components "$BUNDLE_DIR"
if [[ ! -d "$BUNDLE_DIR" ]]; then
  echo "Bundle directory not found: $BUNDLE_DIR" >&2
  exit 1
fi

if [[ ! -d "$FILES_SOURCE" ]]; then
  for fallback_files_source in \
    "$REPO_ROOT/Chummer.Portal/downloads/files" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/files" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads/files" \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published/files"
  do
    if [[ -d "$fallback_files_source" ]]; then
      FILES_SOURCE="$fallback_files_source"
      break
    fi
  done
fi

reject_lexical_symlink_components "$FILES_SOURCE"
if [[ ! -d "$FILES_SOURCE" ]]; then
  echo "Bundle is missing files directory: $FILES_SOURCE" >&2
  echo "Expected desktop-download-bundle layout: releases.json + files/chummer-*" >&2
  exit 1
fi

artifacts=()
while IFS= read -r artifact_path; do
  [[ -n "$artifact_path" ]] || continue
  artifacts+=("$artifact_path")
done < <(find "$FILES_SOURCE" -maxdepth 1 -type f \
  \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o \
     -name "chummer-avalonia-*.tar.gz" -o -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
     -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
     -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
     -name "chummer-avalonia-*-payload.zip.json" -o \
     -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
     -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
     -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
     -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
     -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" \) \
  | sort)

artifact_count="$(array_count artifacts)"
if (( artifact_count == 0 )); then
  echo "No desktop artifacts found under $FILES_SOURCE" >&2
  exit 1
fi

verify_windows_installer_payload_gate
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  refresh_release_build_handoff "$BUNDLE_DIR"
fi

if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  sync_source_dir="$(mktemp -d "$(dirname "$RELEASE_CANDIDATE_OUTPUT_DIR")/.${RELEASE_CANDIDATE_OUTPUT_DIR##*/}.candidate-build.XXXXXXXX")"
else
  sync_source_dir="$(mktemp -d)"
fi
cleanup() {
  rm -rf "$sync_source_dir"
}
trap cleanup EXIT

append_unique_downloads_mirror_dir() {
  local candidate="$1"
  local resolved_candidate=""
  local existing=""

  [[ -n "$candidate" ]] || return 0
  resolved_candidate="$(realpath -m "$candidate")"
  while IFS= read -r -d '' existing; do
    [[ -n "$existing" ]] || continue
    if [[ "$(realpath -m "$existing")" == "$resolved_candidate" ]]; then
      return 0
    fi
  done < <(array_values_nul live_downloads_mirror_dirs)
  live_downloads_mirror_dirs+=("$candidate")
}

deploy_dir_is_live_downloads_root() {
  local candidate="$1"
  local resolved_candidate=""
  local known_root=""

  resolved_candidate="$(realpath -m "$candidate")"
  for known_root in \
    "$REPO_ROOT/Docker/Downloads" \
    "$REPO_ROOT/Chummer.Portal/downloads" \
    "$REPO_ROOT/.codex-studio/published/portal" \
    "$REPO_ROOT/../chummer-presentation/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/.codex-studio/published/portal" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ "$resolved_candidate" == "$(realpath -m "$known_root")" ]]; then
      return 0
    fi
  done

  return 1
}

discover_live_downloads_mirror_dirs() {
  local include_optional_mirrors="${1:-true}"
  local configured="${CHUMMER_PUBLIC_EDGE_DOWNLOADS_MIRROR_DIRS:-}"
  local deploy_dir_physical=""
  local canonical_downloads_physical=""
  local portal_downloads_physical=""
  local candidate=""
  local generator_registry_root=""
  local generator_registry_downloads_root=""
  local generator_run_services_downloads_root="${RUN_SERVICES_DOWNLOADS_ROOT:-$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads}"
  local generator_presentation_mirror_root="${PRESENTATION_MIRROR_ROOT:-$REPO_ROOT}"
  local sibling_root=""

  if to_bool "$include_optional_mirrors" && [[ -n "$configured" ]]; then
    IFS=',' read -r -a configured_dirs <<<"$configured"
    for candidate in "${configured_dirs[@]}"; do
      candidate="${candidate#"${candidate%%[![:space:]]*}"}"
      candidate="${candidate%"${candidate##*[![:space:]]}"}"
      [[ -n "$candidate" ]] || continue
      append_unique_downloads_mirror_dir "$candidate"
    done
  fi

  if [[ -d "$(dirname "$generator_run_services_downloads_root")" ]]; then
    append_unique_downloads_mirror_dir "$generator_run_services_downloads_root"
  fi
  if [[ -n "${REGISTRY_RELEASES_MANIFEST_PATH:-}" ]]; then
    generator_registry_downloads_root="$(dirname "$REGISTRY_RELEASES_MANIFEST_PATH")"
  elif [[ -f "$SCRIPT_DIR/resolve-hub-registry-root.sh" ]]; then
    generator_registry_root="$(bash "$SCRIPT_DIR/resolve-hub-registry-root.sh")"
    generator_registry_downloads_root="$generator_registry_root/.codex-studio/published"
  fi
  if [[ -n "$generator_registry_downloads_root" && -d "$(dirname "$generator_registry_downloads_root")" ]]; then
    append_unique_downloads_mirror_dir "$generator_registry_downloads_root"
  fi
  if [[ -d "$generator_presentation_mirror_root" \
    && "$(realpath -m "$generator_presentation_mirror_root")" != "$(realpath -m "$REPO_ROOT")" ]]; then
    append_unique_downloads_mirror_dir "$generator_presentation_mirror_root/Docker/Downloads"
  fi

  if ! to_bool "$include_optional_mirrors"; then
    return 0
  fi

  deploy_dir_physical="$(realpath -m "$DEPLOY_DIR")"
  canonical_downloads_physical="$(realpath -m "$REPO_ROOT/Docker/Downloads")"
  portal_downloads_physical="$(realpath -m "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads")"

  if ! deploy_dir_is_live_downloads_root "$deploy_dir_physical"; then
    return 0
  fi

  if [[ "$deploy_dir_physical" != "$canonical_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/Docker/Downloads"
  fi

  if [[ "$deploy_dir_physical" != "$portal_downloads_physical" ]]; then
    append_unique_downloads_mirror_dir "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads"
  fi

  for sibling_root in \
    "$REPO_ROOT/../chummer-presentation/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/.codex-studio/published/portal" \
    "$REPO_ROOT/../chummer-hub-registry/.codex-studio/published" \
    "$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer6-hub/Chummer.Portal/downloads" \
    "$REPO_ROOT/../chummer-presentation/Docker/Downloads"
  do
    if [[ -d "$(dirname "$sibling_root")" ]]; then
      append_unique_downloads_mirror_dir "$sibling_root"
    fi
  done
}

resolve_aur_materializer() {
  local configured="${CHUMMER_AUR_MATERIALIZER:-}"
  local candidate=""

  if [[ -n "$configured" ]]; then
    if [[ -f "$configured" ]]; then
      printf '%s\n' "$configured"
      return 0
    fi
    echo "Configured AUR materializer is missing: $configured" >&2
    return 1
  fi

  for candidate in \
    "$REPO_ROOT/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer.run-services/scripts/materialize-aur-package.py" \
    "$REPO_ROOT/../chummer6-hub/scripts/materialize-aur-package.py"
  do
    if [[ -f "$candidate" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

remove_aur_sidecar() {
  rm -f \
    "$DEPLOY_DIR/aur-packages.json" \
    "$DEPLOY_DIR/files/chummer6-bin-aur-source.tar.gz" \
    "$DEPLOY_DIR/files/chummer6-bin.PKGBUILD" \
    "$DEPLOY_DIR/files/chummer6-bin.SRCINFO"
}

materialize_aur_sidecar() {
  local materializer=""

  if materializer="$(resolve_aur_materializer)"; then
    python3 "$materializer" \
      --manifest "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
      --files-root "$DEPLOY_DIR/files" \
      --output-root "$DEPLOY_DIR" \
      --downloads-prefix "${CHUMMER_PUBLIC_DOWNLOADS_PREFIX:-https://chummer.run/downloads/files}" \
      --optional >/dev/null
    return 0
  fi

  remove_aur_sidecar
  echo "AUR materializer not found; removed stale AUR sidecar files from $DEPLOY_DIR." >&2
}

sync_live_downloads_mirror_dir() {
  local target_dir="$1"
  local target_label="$2"
  local resolved_target_dir=""
  local resolved_deploy_dir=""
  local resolved_portal_dir=""
  local files_dir=""
  local startup_smoke_dir=""
  local source_path=""
  local file_name=""

  resolved_target_dir="$(realpath -m "$target_dir")"
  resolved_deploy_dir="$(realpath -m "$DEPLOY_DIR")"
  if [[ -n "$PORTAL_DOWNLOADS_DIR" ]]; then
    resolved_portal_dir="$(realpath -m "$PORTAL_DOWNLOADS_DIR")"
  else
    resolved_portal_dir="$resolved_deploy_dir"
  fi

  if [[ "$resolved_target_dir" == "$resolved_deploy_dir" || "$resolved_target_dir" == "$resolved_portal_dir" ]]; then
    return 0
  fi

  assert_legacy_release_shelf_target "$target_dir"

  mkdir -p "$target_dir"
  cp "$DEPLOY_DIR/releases.json" "$target_dir/releases.json"
  cp "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$target_dir/RELEASE_CHANNEL.generated.json"
  if [[ -f "$DEPLOY_DIR/aur-packages.json" ]]; then
    cp "$DEPLOY_DIR/aur-packages.json" "$target_dir/aur-packages.json"
  else
    rm -f "$target_dir/aur-packages.json"
  fi

  startup_smoke_dir="$target_dir/startup-smoke"
  mkdir -p "$startup_smoke_dir"
  find "$startup_smoke_dir" -maxdepth 1 -type f -name 'startup-smoke-*.receipt.json' -exec rm -f -- {} +
  if [[ -d "$DEPLOY_DIR/startup-smoke" ]] && find "$DEPLOY_DIR/startup-smoke" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp -f "$DEPLOY_DIR"/startup-smoke/* "$startup_smoke_dir"/
  fi

  files_dir="$target_dir/files"
  mkdir -p "$files_dir"
  rm -f \
    "$files_dir"/chummer6-bin-aur-source.tar.gz \
    "$files_dir"/chummer6-bin.PKGBUILD \
    "$files_dir"/chummer6-bin.SRCINFO
  find "$files_dir" -maxdepth 1 -type f \
    \( -name "chummer-avalonia-*.exe" -o -name "chummer-avalonia-*.zip" -o -name "chummer-avalonia-*.tar.gz" -o \
       -name "chummer-avalonia-*-installer.exe" -o -name "chummer-avalonia-*-installer.deb" -o \
       -name "chummer-avalonia-*-installer.pkg" -o -name "chummer-avalonia-*-installer.dmg" -o \
       -name "chummer-avalonia-*-installer.msix" -o -name "chummer-avalonia-*-payload.zip" -o \
       -name "chummer-avalonia-*-payload.zip.json" -o \
       -name "chummer-blazor-desktop-*.exe" -o -name "chummer-blazor-desktop-*.zip" -o \
       -name "chummer-blazor-desktop-*.tar.gz" -o -name "chummer-blazor-desktop-*-installer.exe" -o \
       -name "chummer-blazor-desktop-*-installer.deb" -o -name "chummer-blazor-desktop-*-installer.pkg" -o \
       -name "chummer-blazor-desktop-*-installer.dmg" -o -name "chummer-blazor-desktop-*-installer.msix" -o \
       -name "chummer-blazor-desktop-*-payload.zip" -o -name "chummer-blazor-desktop-*-payload.zip.json" -o \
       -name "chummer-6-*.exe" -o -name "chummer-6-*.zip" -o -name "chummer-6-*.tar.gz" -o -name "chummer-6-*-installer.exe" -o \
       -name "chummer-6-*-installer.deb" -o -name "chummer-6-*-installer.pkg" -o -name "chummer-6-*-installer.dmg" -o \
       -name "chummer-6-*-installer.msix" -o -name "chummer-6-*-payload.zip" -o \
       -name "chummer-6-*-payload.zip.json" \) \
    -delete

  while IFS= read -r -d '' file_name; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ ! -f "$source_path" ]]; then
      echo "promoted artifact missing from deploy root for $target_label mirror: $source_path" >&2
      exit 1
    fi
    cp "$source_path" "$files_dir/"
  done < <(array_values_nul promoted_file_names)
  for file_name in chummer6-bin-aur-source.tar.gz chummer6-bin.PKGBUILD chummer6-bin.SRCINFO; do
    source_path="$DEPLOY_DIR/files/$file_name"
    if [[ -f "$source_path" ]]; then
      cp "$source_path" "$files_dir/"
    fi
  done

  sync_release_build_provenance_namespace "$target_dir"
  if [[ -f "$staged_promotion_evidence_path" ]]; then
    mkdir -p "$target_dir/release-evidence"
    cp "$staged_promotion_evidence_path" "$target_dir/release-evidence/public-promotion.json"
  fi

  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$target_dir/RELEASE_CHANNEL.generated.json" >/dev/null
  echo "synced ${promoted_file_count} promoted artifact(s) -> $target_label mirror $target_dir"
}

while IFS= read -r -d '' artifact; do
  if is_public_artifact "$artifact"; then
    cp "$artifact" "$sync_source_dir/"
  fi
done < <(array_values_nul artifacts)

release_version="${RELEASE_VERSION:-}"
release_channel="${RELEASE_CHANNEL:-}"
release_published_at="${RELEASE_PUBLISHED_AT:-}"
default_published_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [[ -f "$MANIFEST_SOURCE" ]]; then
  manifest_meta=()
  while IFS= read -r line; do
    manifest_meta+=("$line")
  done < <(python3 - "$MANIFEST_SOURCE" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
print(str(data.get("version", "unpublished")))
print(str(data.get("channel", "docker")))
print(str(data.get("publishedAt", "")))
PY
)

  manifest_integrity=()
  while IFS= read -r line; do
    manifest_integrity+=("$line")
  done < <(bundle_manifest_matches_files "$MANIFEST_SOURCE" "$FILES_SOURCE")
  manifest_matches_files="${manifest_integrity[0]:-false}"

  if [[ "$manifest_matches_files" != "true" && -z "${RELEASE_VERSION:-}" ]]; then
    echo "Bundle files no longer match $MANIFEST_SOURCE, so reusing its release version would be dishonest." >&2
    printf '%s\n' "${manifest_integrity[@]:1}" >&2
    echo "Set RELEASE_VERSION and RELEASE_PUBLISHED_AT explicitly for this republish." >&2
    exit 1
  fi

  if [[ -z "$release_version" && -n "${manifest_meta[0]:-}" ]]; then
    release_version="${manifest_meta[0]}"
  fi
  if [[ -z "$release_channel" && -n "${manifest_meta[1]:-}" ]]; then
    release_channel="${manifest_meta[1]}"
  fi
  if [[ -z "$release_published_at" && -n "${manifest_meta[2]:-}" ]]; then
    release_published_at="${manifest_meta[2]}"
  fi
fi

release_version="${release_version:-unpublished}"
release_channel="${release_channel:-docker}"
release_published_at="${release_published_at:-$default_published_at}"
require_public_stable_root_blocker_clearance "$release_channel"
live_downloads_mirror_dirs=()
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  discover_live_downloads_mirror_dirs "$SYNC_LIVE_DOWNLOADS_MIRRORS"
fi
live_downloads_mirror_dir_count="$(array_count live_downloads_mirror_dirs)"

prepare_release_build_provenance
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  preflight_managed_release_target "$DEPLOY_DIR"
  preflight_managed_release_target "$PORTAL_DOWNLOADS_DIR"
  preflight_release_build_provenance_target "$DEPLOY_DIR"
  preflight_release_build_provenance_target "$PORTAL_DOWNLOADS_DIR"
  if (( live_downloads_mirror_dir_count > 0 )); then
    while IFS= read -r -d '' mirror_dir; do
      assert_legacy_release_shelf_target "$mirror_dir"
      preflight_managed_release_target "$mirror_dir"
      preflight_release_build_provenance_target "$mirror_dir"
    done < <(array_values_nul live_downloads_mirror_dirs)
  fi
fi

staged_release_root="$sync_source_dir/release-candidate"
staged_manifest_path="$staged_release_root/releases.json"
staged_canonical_manifest_path="$staged_release_root/RELEASE_CHANNEL.generated.json"
staged_promotion_evidence_path="$staged_release_root/release-evidence/public-promotion.json"
mkdir -p "$staged_release_root"

DOWNLOADS_DIR="$sync_source_dir" \
MANIFEST_PATH="$staged_manifest_path" \
CANONICAL_MANIFEST_PATH="$staged_canonical_manifest_path" \
CANONICAL_FILES_DIR="$staged_release_root/files" \
PORTAL_MANIFEST_PATH="$staged_manifest_path" \
PORTAL_CANONICAL_MANIFEST_PATH="$staged_canonical_manifest_path" \
PORTAL_DOWNLOADS_DIR="$staged_release_root" \
PROMOTION_EVIDENCE_PATH="$staged_promotion_evidence_path" \
QUARANTINE_PROMOTION_EVIDENCE_PATH="$staged_release_root/QUARANTINED_INSTALLER_PROMOTION.generated.json" \
RELEASE_VERSION="$release_version" \
RELEASE_CHANNEL="$release_channel" \
RELEASE_PUBLISHED_AT="$release_published_at" \
SOURCE_MANIFEST_PATH="$MANIFEST_SOURCE" \
RELEASE_PROOF_PATH="$RELEASE_PROOF_PATH" \
STARTUP_SMOKE_DIR="$STARTUP_SMOKE_SOURCE" \
CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS="${CHUMMER_PUBLIC_STARTUP_SMOKE_MAX_AGE_SECONDS:-}" \
CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_EXTERNAL_PROOF_BASE_URL="${CHUMMER_EXTERNAL_PROOF_BASE_URL:-https://chummer.run}" \
CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS="${CHUMMER_GENERATE_EXTERNAL_HOST_PROOF_BLOCKERS:-0}" \
CHUMMER_RELEASE_MANIFEST_STAGE_ONLY=1 \
bash "$SCRIPT_DIR/generate-releases-manifest.sh"

strip_non_public_manifest_rows "$staged_canonical_manifest_path"
strip_non_public_manifest_rows "$staged_manifest_path"
sync_release_build_provenance_namespace "$staged_release_root"
if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
  verify_candidate_manifest_mac_identity_agreement \
    "$staged_canonical_manifest_path" \
    "$staged_manifest_path" \
    "$staged_release_root/files"
  python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
fi

transactional_publish_target_dirs=()
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  transactional_publish_target_dirs=("$DEPLOY_DIR")
  if [[ "$(realpath -m "$PORTAL_DOWNLOADS_DIR")" != "$(realpath -m "$DEPLOY_DIR")" ]]; then
    transactional_publish_target_dirs+=("$PORTAL_DOWNLOADS_DIR")
  fi
  if (( live_downloads_mirror_dir_count > 0 )); then
    while IFS= read -r -d '' mirror_dir; do
      transactional_publish_target_dirs+=("$mirror_dir")
    done < <(array_values_nul live_downloads_mirror_dirs)
  fi
fi
verify_release_candidate_shelf_invariants \
  "$staged_release_root" \
  "$release_channel" \
  "${transactional_publish_target_dirs[@]}"
CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$staged_release_root"
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  while IFS= read -r -d '' target_dir; do
    preflight_managed_release_target "$target_dir"
  done < <(array_values_nul transactional_publish_target_dirs)
fi
final_deploy_dir="$DEPLOY_DIR"
final_portal_downloads_dir="$PORTAL_DOWNLOADS_DIR"
if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
  if to_bool "$DEPLOY_MODE" || [[ -n "$LIVE_VERIFY_TARGET" ]]; then
    echo "Mac legacy filesystem publication requires rollback-safe local cutover; external deploy/live verification must use the staged HTTP publisher." >&2
    exit 1
  fi
fi
DEPLOY_DIR="$staged_release_root"
PORTAL_DOWNLOADS_DIR="$staged_release_root"
PORTAL_MANIFEST_PATH="$staged_manifest_path"

promoted_file_names=()
while IFS= read -r file_name; do
  [[ -n "$file_name" ]] || continue
  promoted_file_names+=("$file_name")
done < <(python3 - "$staged_canonical_manifest_path" "$staged_release_root/files" <<'PY'
import json
import sys
from pathlib import Path

payload = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
source_root = Path(sys.argv[2])
seen = set()
for artifact in payload.get("artifacts") or []:
    if not isinstance(artifact, dict):
        continue
    names = []
    file_name = str(artifact.get("fileName") or "").strip()
    if not file_name:
        file_name = Path(str(artifact.get("downloadUrl") or "").strip()).name
    names.append(file_name)
    payload_name = str(artifact.get("payloadFileName") or "").strip()
    if not payload_name:
        payload_name = Path(str(artifact.get("payloadDownloadUrl") or "").strip()).name
    names.append(payload_name)
    if payload_name:
        payload_metadata_name = payload_name + ".json"
        if (source_root / payload_metadata_name).is_file():
            names.append(payload_metadata_name)
    for candidate in names:
        if candidate and candidate not in seen:
            print(candidate)
            seen.add(candidate)
PY
)
promoted_file_count="$(array_count promoted_file_names)"

materialize_aur_sidecar

if [[ -d "$STARTUP_SMOKE_SOURCE" ]]; then
  verified_startup_smoke_tmp="$(mktemp)"
  startup_smoke_registry_dir="$REPO_ROOT/../chummer-hub-registry/.codex-studio/published/startup-smoke"
  startup_smoke_ui_downloads_dir="$REPO_ROOT/Docker/Downloads/startup-smoke"
  startup_smoke_presentation_dir="$REPO_ROOT/../chummer-presentation/Docker/Downloads/startup-smoke"
  if ! python3 - "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" "$STARTUP_SMOKE_SOURCE" "$DEPLOY_DIR/files" "$startup_smoke_registry_dir" "$startup_smoke_ui_downloads_dir" "$startup_smoke_presentation_dir" >"$verified_startup_smoke_tmp" <<'PY'
import os
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PASSING_STATUSES = {"pass", "passed", "ready"}
INSTALL_MEDIA_KINDS = {"installer", "dmg", "pkg", "msix"}
STARTUP_SMOKE_READY_MARKER = "startup smoke ready:"
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_PUBLISH_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)
PUBLIC_SKIP_STARTUP_SMOKE_FILTER = (
    str(os.environ.get("CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)
ALLOW_SKIPPED_STARTUP_SMOKE = (
    str(os.environ.get("CHUMMER_ALLOW_SKIPPED_STARTUP_SMOKE") or "").strip().lower()
    in {"1", "true", "yes", "on"}
)

release_channel_path = Path(sys.argv[1])
startup_smoke_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])
fallback_roots = [Path(item) for item in sys.argv[4:] if str(item).strip()]

payload = json.loads(release_channel_path.read_text(encoding="utf-8-sig"))
artifacts = payload.get("artifacts") or []
errors: list[str] = []
verified_receipts: list[str] = []
seen: set[str] = set()

def normalize(value: Any) -> str:
    return str(value or "").strip().lower()

def expected_host_class_platform_tokens(platform: str) -> tuple[str, ...]:
    normalized = normalize(platform)
    if normalized == "windows":
        return ("win", "windows")
    if normalized == "macos":
        return ("osx", "macos")
    if normalized == "linux":
        return ("linux",)
    return (normalized,) if normalized else ()

def host_class_matches_platform(host_class: str, platform: str, operating_system: str = "") -> bool:
    normalized_host = normalize(host_class)
    normalized_os = normalize(operating_system)
    expected_tokens = expected_host_class_platform_tokens(platform)
    if not normalized_host or not expected_tokens:
        if normalize(platform) == "windows":
            return "windows" in normalized_os
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    if any(token in host_tokens for token in expected_tokens):
        return True
    if normalize(platform) == "windows":
        return "windows" in normalized_os and "wine" in normalized_host
    return False

def rid_to_arch(rid: str) -> str:
    token = normalize(rid)
    if token.startswith("win-") or token.startswith("linux-") or token.startswith("osx-"):
        _, _, arch = token.partition("-")
        return arch
    return token

def is_windows_incompatible_host_skip(receipt: dict[str, Any], platform: str, rid: str) -> bool:
    if normalize(receipt.get("status")) != "skipped":
        return False
    if normalize(platform) != "windows" and not normalize(rid).startswith("win-"):
        return False
    verification_disposition = normalize(receipt.get("verificationDisposition"))
    skip_class = normalize(receipt.get("skipClass"))
    return verification_disposition == "incompatible_host" or skip_class == "incompatible_host"

def parse_iso_utc(value: Any) -> datetime | None:
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

def companion_log_recorded_at(receipt_path: Path) -> datetime | None:
    if receipt_path.name.endswith(".receipt.json"):
        companion_name = receipt_path.name[: -len(".receipt.json")] + ".log"
    else:
        companion_name = f"{receipt_path.name}.log"
    candidate_roots = [receipt_path.parent, startup_smoke_root, *fallback_roots]
    seen: set[Path] = set()
    for root in candidate_roots:
        candidate = (root / companion_name).resolve(strict=False)
        if candidate in seen or not candidate.is_file():
            continue
        seen.add(candidate)
        try:
            contents = candidate.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        if STARTUP_SMOKE_READY_MARKER not in contents.lower():
            continue
        return datetime.fromtimestamp(candidate.stat().st_mtime, tz=timezone.utc)
    return None

def effective_recorded_at(receipt: dict[str, Any], receipt_path: Path) -> datetime | None:
    candidates = [
        parse_iso_utc(receipt.get("sourceUpdatedAtUtc")),
        parse_iso_utc(receipt.get("completedAtUtc")),
        parse_iso_utc(receipt.get("recordedAtUtc")),
        parse_iso_utc(receipt.get("startedAtUtc")),
        companion_log_recorded_at(receipt_path),
    ]
    valid_candidates = [candidate for candidate in candidates if candidate is not None]
    if not valid_candidates:
        return None
    return max(valid_candidates)

for artifact in artifacts:
    if not isinstance(artifact, dict):
        continue
    kind = normalize(artifact.get("kind"))
    if kind not in INSTALL_MEDIA_KINDS:
        continue
    head = normalize(artifact.get("head"))
    platform = normalize(artifact.get("platform"))
    rid = normalize(artifact.get("rid"))
    file_name = str(artifact.get("fileName") or "").strip()
    if not head or not platform or not rid or not file_name:
        errors.append(f"promoted install-medium artifact is missing required tuple fields (head/platform/rid/fileName): {artifact}")
        continue
    receipt_name = f"startup-smoke-{head}-{rid}.receipt.json"
    if receipt_name in seen:
        continue
    seen.add(receipt_name)
    receipt_path = startup_smoke_root / receipt_name
    if not receipt_path.is_file():
        errors.append(f"startup-smoke receipt missing for promoted install medium {head}/{platform}/{rid}: {receipt_name}")
        continue
    try:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8-sig"))
    except Exception as exc:  # pragma: no cover - shell guard
        errors.append(f"startup-smoke receipt is unreadable for promoted install medium {head}/{platform}/{rid}: {receipt_path} ({exc})")
        continue
    status = normalize(receipt.get("status"))
    incompatible_host_skip = is_windows_incompatible_host_skip(receipt, platform, rid)
    if status not in PASSING_STATUSES:
        if incompatible_host_skip or (ALLOW_SKIPPED_STARTUP_SMOKE and status == "skipped"):
            verified_receipts.append(str(receipt_path))
        else:
            errors.append(f"startup-smoke receipt status is not passing for promoted install medium {head}/{platform}/{rid}: {status or 'missing'}")
    checkpoint = normalize(receipt.get("readyCheckpoint"))
    if not incompatible_host_skip and checkpoint != "pre_ui_event_loop":
        errors.append(f"startup-smoke receipt readyCheckpoint is not pre_ui_event_loop for promoted install medium {head}/{platform}/{rid}.")
    receipt_head = normalize(receipt.get("headId"))
    receipt_platform = normalize(receipt.get("platform"))
    receipt_arch = normalize(receipt.get("arch"))
    receipt_rid = normalize(receipt.get("rid"))
    receipt_host_class = normalize(receipt.get("hostClass"))
    receipt_operating_system = str(receipt.get("operatingSystem") or "").strip()
    expected_arch = rid_to_arch(rid)
    if receipt_head != head:
        errors.append(f"startup-smoke receipt headId mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_head or 'missing'}")
    if receipt_platform != platform:
        errors.append(f"startup-smoke receipt platform mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_platform or 'missing'}")
    if not incompatible_host_skip:
        if not receipt_host_class:
            errors.append(f"startup-smoke receipt hostClass is missing for promoted install medium {head}/{platform}/{rid}.")
        elif not host_class_matches_platform(receipt_host_class, platform, receipt_operating_system):
            errors.append(f"startup-smoke receipt hostClass does not identify the {platform} host for promoted install medium {head}/{platform}/{rid}.")
        if not receipt_operating_system:
            errors.append(f"startup-smoke receipt operatingSystem is missing for promoted install medium {head}/{platform}/{rid}.")
    if expected_arch and receipt_arch != expected_arch:
        errors.append(f"startup-smoke receipt arch mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_arch or 'missing'}")
    if not receipt_rid:
        errors.append(f"startup-smoke receipt rid is missing for promoted install medium {head}/{platform}/{rid}.")
    elif receipt_rid != rid:
        errors.append(f"startup-smoke receipt rid mismatch for promoted install medium {head}/{platform}/{rid}: {receipt_rid}")
    promoted_file_path = files_root / file_name
    expected_sha = normalize(artifact.get("sha256"))
    if promoted_file_path.is_file():
        expected_sha = hashlib.sha256(promoted_file_path.read_bytes()).hexdigest().lower()
    expected_digest = f"sha256:{expected_sha}" if expected_sha else ""
    receipt_digest = normalize(receipt.get("artifactDigest"))
    if expected_digest and receipt_digest != expected_digest:
        errors.append(f"startup-smoke receipt artifactDigest mismatch for promoted install medium {head}/{platform}/{rid}.")
    recorded_at = effective_recorded_at(receipt, receipt_path)
    if recorded_at is None:
        errors.append(
            f"startup-smoke receipt timestamp is missing/invalid for promoted install medium {head}/{platform}/{rid}."
        )
    else:
        now_utc = datetime.now(timezone.utc)
        age_delta_seconds = int((now_utc - recorded_at).total_seconds())
        if age_delta_seconds < 0:
            future_skew_seconds = abs(age_delta_seconds)
            if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
                errors.append(
                    "startup-smoke receipt timestamp is in the future for promoted install medium "
                    f"{head}/{platform}/{rid}: {future_skew_seconds}s ahead (max {STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS}s)."
                )
        elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS and not PUBLIC_SKIP_STARTUP_SMOKE_FILTER:
            errors.append(
                "startup-smoke receipt is stale for promoted install medium "
                f"{head}/{platform}/{rid}: {age_delta_seconds}s old (max {STARTUP_SMOKE_MAX_AGE_SECONDS}s)."
            )
    verified_receipts.append(str(receipt_path))

if errors:
    for error in errors:
        print(error, file=sys.stderr)
    raise SystemExit(1)

for verified in sorted(verified_receipts):
    print(verified)
PY
  then
    rm -f "$verified_startup_smoke_tmp"
    exit 1
  fi
  verified_startup_smoke_receipts=()
  while IFS= read -r receipt_path; do
    verified_startup_smoke_receipts+=("$receipt_path")
  done <"$verified_startup_smoke_tmp"
  rm -f "$verified_startup_smoke_tmp"

  if ! python3 "$SCRIPT_DIR/verify-windows-bootstrap-startup-smoke.py" \
    --release-channel "$DEPLOY_DIR/RELEASE_CHANNEL.generated.json" \
    --downloads-manifest "$DEPLOY_DIR/releases.json" \
    --startup-smoke-dir "$STARTUP_SMOKE_SOURCE" \
    --files-dir "$DEPLOY_DIR/files" >/dev/null
  then
    exit 1
  fi

  startup_smoke_deploy_dir="$DEPLOY_DIR/startup-smoke"
  startup_smoke_stage_dir="$(mktemp -d)"
  startup_smoke_deploy_dir_real="$(realpath -m "$startup_smoke_deploy_dir")"
  deploy_files_dir_real="$(realpath -m "$DEPLOY_DIR/files")"
  mkdir -p "$startup_smoke_deploy_dir"
  startup_smoke_fallback_dir="$PORTAL_DOWNLOADS_DIR/startup-smoke"
  run_services_startup_smoke_dir="$REPO_ROOT/../chummer.run-services/Chummer.Portal/downloads/startup-smoke"
  registry_startup_smoke_dir="$REPO_ROOT/../chummer-hub-registry/.codex-studio/published/startup-smoke"
  python3 - "$startup_smoke_stage_dir" "$startup_smoke_deploy_dir_real" "$deploy_files_dir_real" "$release_channel" "$release_version" "$startup_smoke_fallback_dir" "$run_services_startup_smoke_dir" "$registry_startup_smoke_dir" "${verified_startup_smoke_receipts[@]}" <<'PY'
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

stage_root = Path(sys.argv[1])
final_root = Path(sys.argv[2])
files_root = Path(sys.argv[3])
release_channel = str(sys.argv[4]).strip()
release_version = str(sys.argv[5]).strip()
fallback_roots = [Path(item) for item in sys.argv[6:9] if str(item).strip()]
receipt_paths = [Path(item) for item in sys.argv[9:]]


def resolve_companion(source_root: Path, value: object) -> Path | None:
    raw = str(value or "").strip()
    if not raw:
        return None

    token = Path(raw)
    candidates: list[Path] = []
    if token.is_absolute():
        candidates.append(token)
    else:
        candidates.append(source_root / token)
    candidates.append(source_root / token.name)
    for fallback_root in fallback_roots:
        candidates.append(fallback_root / token.name)

    seen: set[Path] = set()
    for candidate in candidates:
        candidate = candidate.resolve(strict=False)
        if candidate in seen:
            continue
        seen.add(candidate)
        if candidate.is_file():
            return candidate
    return None


def copy_companion(source_root: Path, value: object) -> str:
    source_path = resolve_companion(source_root, value)
    if source_path is None:
        return ""

    stage_path = stage_root / source_path.name
    final_path = final_root / source_path.name
    if source_path.resolve() != stage_path.resolve():
        shutil.copy2(source_path, stage_path)
    return str(final_path)


def rewrite_install_verification(stage_verification_path: Path, source_root: Path) -> None:
    payload = json.loads(stage_verification_path.read_text(encoding="utf-8-sig"))
    for key in (
        "dpkgLogPath",
        "installedLaunchCapturePath",
        "wrapperCapturePath",
        "desktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied
    stage_verification_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


for receipt_path in receipt_paths:
    source_root = receipt_path.parent
    payload = json.loads(receipt_path.read_text(encoding="utf-8-sig"))

    if release_channel:
        payload["channelId"] = release_channel
        payload["channel"] = release_channel
    if release_version:
        payload["releaseVersion"] = release_version
        payload["version"] = release_version

    verification_dest = copy_companion(source_root, payload.get("artifactInstallVerificationPath"))
    if verification_dest:
        payload["artifactInstallVerificationPath"] = verification_dest
        rewrite_install_verification(stage_root / Path(verification_dest).name, source_root)

    for key in (
        "artifactInstallDpkgLogPath",
        "artifactInstallLaunchCapturePath",
        "artifactInstallWrapperCapturePath",
        "artifactInstallDesktopEntryCapturePath",
    ):
        copied = copy_companion(source_root, payload.get(key))
        if copied:
            payload[key] = copied

    artifact_name = Path(str(payload.get("artifactPath") or "").strip()).name
    published_artifact = files_root / artifact_name if artifact_name else None
    if published_artifact is not None and published_artifact.is_file():
        payload["artifactPath"] = str(published_artifact)

    copy_companion(source_root, receipt_path.name.replace(".receipt.json", ".log"))
    (stage_root / receipt_path.name).write_text(
        json.dumps(payload, indent=2) + "\n",
        encoding="utf-8",
    )
PY
  find "$startup_smoke_deploy_dir" -maxdepth 1 -type f \( \
    -name "startup-smoke-*.receipt.json" -o \
    -name "startup-smoke-*.log" -o \
    -name "install-verification-*.json" -o \
    -name "dpkg-*.log" -o \
    -name "installed-launch-*" -o \
    -name "installed-wrapper-*" -o \
    -name "installed-desktop-entry-*" -o \
    -name "windows-installer-progress-*.log" \
  \) -exec rm -f -- {} +
  if find "$startup_smoke_stage_dir" -mindepth 1 -maxdepth 1 -type f | grep -q .; then
    cp "$startup_smoke_stage_dir"/* "$startup_smoke_deploy_dir"/
  fi
  if [[ -d "$STARTUP_SMOKE_SOURCE" ]] && find "$STARTUP_SMOKE_SOURCE" -maxdepth 1 -type f -name 'windows-installer-progress-*.log' | grep -q .; then
    cp -f "$STARTUP_SMOKE_SOURCE"/windows-installer-progress-*.log "$startup_smoke_deploy_dir"/
  fi
  rm -rf "$startup_smoke_stage_dir"
fi

refresh_release_build_handoff "$DEPLOY_DIR"
verify_windows_desktop_exit_gate

if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" && to_bool "$DEPLOY_MODE"; then
  export CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION="${CHUMMER_PORTAL_DOWNLOADS_REQUIRE_PUBLISHED_VERSION:-true}"
  export CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS="${CHUMMER_PORTAL_DOWNLOADS_VERIFY_LINKS:-true}"
  if [[ -z "$LIVE_VERIFY_TARGET" ]]; then
    echo "Deployment mode requires CHUMMER_PORTAL_DOWNLOADS_VERIFY_URL for live manifest verification." >&2
    exit 1
  fi
fi

CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
  bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$DEPLOY_DIR"

if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" && [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$LIVE_VERIFY_TARGET"
fi

scope_args=(
  --output "$DEPLOY_DIR/PUBLICATION_SCOPE.generated.json"
  --deploy-dir "$final_deploy_dir"
  --release-version "$release_version"
  --release-channel "$release_channel"
  --promoted-artifact-count "$promoted_file_count"
)
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" && to_bool "$DEPLOY_MODE"; then
  scope_args+=(--deploy-mode)
fi
if ! to_bool "$RELEASE_CANDIDATE_STAGE_ONLY" && [[ -n "$LIVE_VERIFY_TARGET" ]]; then
  scope_args+=(--live-verify-target "$LIVE_VERIFY_TARGET")
fi
if to_bool "$REQUIRE_EXTERNAL_PUBLISH"; then
  scope_args+=(--require-external-publish)
fi
python3 "$SCRIPT_DIR/materialize-downloads-publication-scope.py" "${scope_args[@]}"

if to_bool "$RELEASE_CANDIDATE_STAGE_ONLY"; then
  rewrite_release_candidate_stage_paths "$staged_release_root" "$RELEASE_CANDIDATE_OUTPUT_DIR"
  verify_release_candidate_shelf_invariants "$staged_release_root" "$release_channel"
  CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE="${CHUMMER_RELEASE_REQUIRE_COMPLETE_DESKTOP_COVERAGE:-1}" \
  CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER="${CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER:-$PUBLIC_SKIP_STARTUP_SMOKE_FILTER}" \
    bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$staged_release_root"
  if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
    verify_candidate_manifest_mac_identity_agreement \
      "$staged_canonical_manifest_path" \
      "$staged_manifest_path" \
      "$staged_release_root/files"
    python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
  fi
  atomically_publish_release_candidate_stage_only \
    "$staged_release_root" \
    "$RELEASE_CANDIDATE_OUTPUT_DIR"
  printf 'release_candidate_stage_only=pass\n'
  printf 'release_candidate_stage_only_path=%s\n' "$RELEASE_CANDIDATE_OUTPUT_DIR"
  exit 0
fi

if (( BUILD_PROVENANCE_REQUIRED == 1 )); then
  verify_candidate_manifest_mac_identity_agreement \
    "$staged_canonical_manifest_path" \
    "$staged_manifest_path" \
    "$staged_release_root/files"
  python3 -I "$BUILD_PROVENANCE_VALIDATOR_RESOLVED" "$staged_release_root"
fi
transaction_validator="${BUILD_PROVENANCE_VALIDATOR_RESOLVED:--}"
transactionally_publish_release_candidate \
  "$staged_release_root" \
  "$transaction_validator" \
  "${transactional_publish_target_dirs[@]}"
DEPLOY_DIR="$final_deploy_dir"
PORTAL_DOWNLOADS_DIR="$final_portal_downloads_dir"

if to_bool "$DEPLOY_MODE"; then
  echo "Published ${promoted_file_count} desktop artifact(s) through verified external downloads lane: $LIVE_VERIFY_TARGET"
else
  echo "Updated local downloads shelf with ${promoted_file_count} desktop artifact(s): $DEPLOY_DIR"
fi
