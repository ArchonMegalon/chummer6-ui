#!/usr/bin/env python3
"""Sign and independently verify the governed Linux Debian artifact.

The production ``sign`` command is intentionally narrow.  It accepts one
unsigned package, one exact export receipt, and secret OpenPGP material supplied
through named environment variables.  It creates one origin-signed package,
an explicit debsig-verify policy/public keyring, and the existing
``chummer6-ui.desktop_artifact_signing`` receipt.  Secret material is imported
only into an ephemeral GNUPGHOME and is never accepted on the command line.

The ``verify`` command does not need a private key.  It revalidates every byte
binding in the signing receipt, verifies the embedded origin signature with the
receipt-pinned policy and public keyring, and requires a controlled mutation of
the signed data member to fail with debsig-verify's bad-signature exit code.
"""

from __future__ import annotations

import argparse
import base64
import ctypes
import hashlib
import json
import lzma
import os
import re
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path, PurePosixPath
from typing import Any, Iterable, Mapping, Sequence


SIGNING_CONTRACT = "chummer6-ui.desktop_artifact_signing"
EXPORT_CONTRACT = "chummer6-ui.linux-native-candidate-export"
SIGNING_BACKEND = "debsigs-origin-openpgp"
VERIFY_BACKEND = "debsig-verify"
APP = "avalonia"
PLATFORM = "linux"
RID = "linux-x64"
ENVIRONMENT = "linux-deb-signing"
PRIVATE_KEY_ENV = "CHUMMER_LINUX_DEB_SIGNING_PRIVATE_KEY_B64"
PASSPHRASE_ENV = "CHUMMER_LINUX_DEB_SIGNING_PASSPHRASE_B64"
REPOSITORY = "ArchonMegalon/chummer6-ui"
WORKFLOW = ".github/workflows/linux-native-candidate-export.yml"
REF = "refs/heads/main"
ARTIFACT_FILE_NAME = "chummer-avalonia-linux-x64-installer.deb"
DEBIAN_BINARY_MEMBER = "debian-binary"
CONTROL_MEMBER = "control.tar.xz"
DATA_MEMBER = "data.tar.xz"
ORIGIN_SIGNATURE_MEMBER = "_gpgorigin"
POLICY_FILE_NAME = "chummer6-origin.pol"
KEYRING_FILE_NAME = "chummer6-origin.pgp"
SIGNING_RECEIPT_FILE_NAME = (
    "DESKTOP_ARTIFACT_SIGNING-linux-linux-x64.generated.json"
)
SIGNED_EXPORT_RECEIPT_FILE_NAME = (
    "LINUX_NATIVE_CANDIDATE_EXPORT.generated.json"
)
TRANSACTION_MANIFEST_FILE_NAME = (
    "LINUX_DEB_SIGNING_TRANSACTION.generated.json"
)
TRANSACTION_CONTRACT = "chummer6-ui.linux-deb-signing-transaction"
TRANSACTION_OUTPUT_KEYS = {
    "package",
    "policy",
    "publicKeyring",
    "signingReceipt",
    "signedExportReceipt",
}
EXPECTED_DEBSIGS_VERSION = "0.1.26"
EXPECTED_DEBSIG_VERIFY_VERSION = "0.29"
MAX_PACKAGE_BYTES = 2 * 1024 * 1024 * 1024
MAX_JSON_BYTES = 4 * 1024 * 1024
MAX_KEY_BYTES = 1024 * 1024
MAX_PASSPHRASE_BYTES = 16 * 1024
MAX_TOOL_OUTPUT_BYTES = 2 * 1024 * 1024
MAX_CONTROL_STREAM_BYTES = 64 * 1024 * 1024
MAX_DATA_STREAM_BYTES = MAX_PACKAGE_BYTES
MAX_XZ_MEMORY_BYTES = 256 * 1024 * 1024
XZ_OUTPUT_CHUNK_BYTES = 1024 * 1024
AGENT_TOOL_TIMEOUT_SECONDS = 10
AGENT_EXIT_TIMEOUT_SECONDS = 2
MINIMAL_TOOL_ENVIRONMENT = {
    "HOME": "/tmp",
    "LANG": "C.UTF-8",
    "LC_ALL": "C.UTF-8",
    "PATH": "/usr/bin:/bin",
}
MAX_EXACT_INTEGER = 9_007_199_254_740_991
TAMPER_REJECTION_EXIT_CODE = 13
MAX_RECEIPT_AGE_SECONDS = 24 * 60 * 60
MAX_SIGNATURE_RECEIPT_SKEW_SECONDS = 15 * 60

SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
FINGERPRINT_RE = re.compile(r"^[0-9A-F]{40}$")
LONG_KEY_ID_RE = re.compile(r"^[0-9A-F]{16}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
PORTABLE_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
LOGIN_RE = re.compile(
    r"^(?:github-actions\[bot\]|"
    r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?)$"
)
MEMBER_SEGMENT_RE = re.compile(r"^[A-Za-z0-9._+-]{1,255}$")
ZULU_RE = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}Z$"
)


class ContractError(RuntimeError):
    """An input cannot support a Linux package-signing claim."""


@dataclass(frozen=True)
class FileIdentity:
    device: int
    inode: int
    size_bytes: int
    modified_ns: int
    changed_ns: int
    mode: int
    owner_uid: int
    link_count: int


@dataclass(frozen=True)
class Snapshot:
    path: Path
    sha256: str
    size_bytes: int
    data: bytes | None = None
    identity: FileIdentity | None = None
    parent_identity: FileIdentity | None = None


@dataclass(frozen=True)
class AgentProcess:
    pid: int
    start_time_ticks: int
    socket_path: Path


@dataclass(frozen=True)
class Tool:
    name: str
    path: Path
    package_name: str
    expected_package_version: str


class EphemeralGpgHome:
    """Private GnuPG home whose agent is killed on every exit path."""

    def __init__(self, parent: Path) -> None:
        self._temporary = tempfile.TemporaryDirectory(
            prefix="chummer-linux-signing-", dir=parent
        )
        self.path = Path(self._temporary.name)

    def __enter__(self) -> Path:
        self.path.chmod(0o700)
        return self.path

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> bool:
        shutdown_error: ContractError | None = None
        process: AgentProcess | None = None
        try:
            try:
                process = _discover_ephemeral_agent(self.path)
            except ContractError:
                process = None
            primary_succeeded = False
            try:
                completed = run_tool(
                    [
                        "/usr/bin/gpgconf",
                        "--homedir",
                        str(self.path),
                        "--kill",
                        "all",
                    ],
                    label="ephemeral GnuPG agent shutdown",
                    environment=_gpg_environment(self.path),
                    expected_exit=None,
                    timeout_seconds=AGENT_TOOL_TIMEOUT_SECONDS,
                )
                primary_succeeded = (
                    completed.returncode == 0
                    and _confirm_agent_stopped(self.path, process)
                )
            except Exception:
                primary_succeeded = False
            if not primary_succeeded:
                fallback_succeeded = False
                try:
                    fallback_command = [
                        "/usr/bin/gpg-connect-agent",
                    ]
                    if process is not None:
                        fallback_command.extend(
                            [
                                "--raw-socket",
                                str(process.socket_path),
                            ]
                        )
                    else:
                        fallback_command.extend(
                            ["--homedir", str(self.path)]
                        )
                    fallback_command.extend(["KILLAGENT", "/bye"])
                    fallback = run_tool(
                        fallback_command,
                        label="ephemeral GnuPG agent fallback shutdown",
                        environment=_gpg_environment(self.path),
                        expected_exit=None,
                        timeout_seconds=AGENT_TOOL_TIMEOUT_SECONDS,
                    )
                    fallback_succeeded = (
                        fallback.returncode == 0
                        and _confirm_agent_stopped(self.path, process)
                    )
                except Exception:
                    fallback_succeeded = False
                if not fallback_succeeded:
                    forced = False
                    if process is not None:
                        try:
                            forced = _last_resort_agent_shutdown(
                                self.path, process
                            )
                        except ContractError:
                            forced = False
                    shutdown_error = ContractError(
                        "both primary and fallback ephemeral GnuPG agent "
                        "shutdown failed; last-resort termination "
                        + ("confirmed" if forced else "could not be proven")
                    )
        finally:
            try:
                self._temporary.cleanup()
            except Exception as error:
                cleanup_error = ContractError(
                    f"ephemeral GnuPG home cleanup failed: {error}"
                )
                shutdown_error = ContractError(
                    "; ".join(
                        str(item)
                        for item in (shutdown_error, cleanup_error)
                        if item is not None
                    )
                )
        try:
            _require_ephemeral_agent_scope_absent(
                self.path,
                process.socket_path if process is not None else None,
            )
        except ContractError as error:
            shutdown_error = ContractError(
                "; ".join(
                    str(item)
                    for item in (shutdown_error, error)
                    if item is not None
                )
            )
        if shutdown_error is not None:
            if isinstance(exc, BaseException):
                exc.add_note(str(shutdown_error))
            else:
                raise shutdown_error
        return False


TOOLS = {
    "debsigs": Tool(
        "debsigs", Path("/usr/bin/debsigs"), "debsigs", EXPECTED_DEBSIGS_VERSION
    ),
    "debsigVerify": Tool(
        "debsig-verify",
        Path("/usr/bin/debsig-verify"),
        "debsig-verify",
        EXPECTED_DEBSIG_VERIFY_VERSION,
    ),
    "gpg": Tool("gpg", Path("/usr/bin/gpg"), "gpg", ""),
    "gpgv": Tool("gpgv", Path("/usr/bin/gpgv"), "gpgv", ""),
}


def fail(message: str) -> None:
    raise ContractError(message)


def current_time() -> datetime:
    return datetime.now(UTC)


def canonical_json(value: Any) -> str:
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    )


def exact_dict(value: Any, keys: set[str], label: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != keys:
        actual = set(value) if isinstance(value, dict) else set()
        fail(
            f"{label} has missing keys {sorted(keys - actual)} or "
            f"extra keys {sorted(actual - keys)}"
        )
    return value


def require_text(
    value: Any, label: str, pattern: re.Pattern[str] | None = None
) -> str:
    if not isinstance(value, str) or not value:
        fail(f"{label} must be a non-empty string")
    if pattern is not None and pattern.fullmatch(value) is None:
        fail(f"{label} has an invalid format")
    return value


def require_sha256(value: Any, label: str) -> str:
    return require_text(value, label, SHA256_RE)


def require_positive_integer(value: Any, label: str) -> int:
    if type(value) is not int or value < 1 or value > MAX_EXACT_INTEGER:
        fail(f"{label} must be an exact positive JSON integer")
    return value


def require_positive_integer_text(value: Any, label: str) -> str:
    text = require_text(value, label, POSITIVE_INTEGER_RE)
    if int(text) > MAX_EXACT_INTEGER:
        fail(f"{label} exceeds the exact integer range")
    return text


def parse_zulu(value: Any, label: str) -> datetime:
    text = require_text(value, label, ZULU_RE)
    try:
        return datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(
            tzinfo=UTC
        )
    except ValueError:
        fail(f"{label} is not a real whole-second UTC timestamp")


def safe_member(value: Any, label: str) -> str:
    raw = require_text(value, label)
    if "\\" in raw or "\x00" in raw:
        fail(f"{label} must be a portable POSIX path")
    path = PurePosixPath(raw)
    if (
        path.is_absolute()
        or not path.parts
        or any(
            part in {"", ".", ".."} or MEMBER_SEGMENT_RE.fullmatch(part) is None
            for part in path.parts
        )
        or path.as_posix() != raw
    ):
        fail(f"{label} must be a canonical traversal-free member path")
    return raw


def _file_identity(metadata: os.stat_result) -> FileIdentity:
    return FileIdentity(
        device=metadata.st_dev,
        inode=metadata.st_ino,
        size_bytes=metadata.st_size,
        modified_ns=metadata.st_mtime_ns,
        changed_ns=metadata.st_ctime_ns,
        mode=metadata.st_mode,
        owner_uid=metadata.st_uid,
        link_count=metadata.st_nlink,
    )


def _same_identity(left: FileIdentity, right: FileIdentity) -> bool:
    return left == right


def _same_directory_object(
    left: FileIdentity, right: FileIdentity
) -> bool:
    return (
        left.device,
        left.inode,
        left.mode,
        left.owner_uid,
        left.link_count,
    ) == (
        right.device,
        right.inode,
        right.mode,
        right.owner_uid,
        right.link_count,
    )


def _same_directory_anchor(
    left: FileIdentity, right: FileIdentity
) -> bool:
    return (
        left.device,
        left.inode,
        left.mode,
        left.owner_uid,
    ) == (
        right.device,
        right.inode,
        right.mode,
        right.owner_uid,
    )


def _snapshot_descriptor(
    path: Path,
    descriptor: int,
    label: str,
    maximum_bytes: int,
    *,
    read_data: bool = False,
    expected_identity: FileIdentity | None = None,
    private_output: bool = False,
) -> Snapshot:
    try:
        before = os.fstat(descriptor)
    except OSError as exc:
        fail(f"{label} descriptor cannot be inspected: {exc}")
    before_identity = _file_identity(before)
    if (
        not stat.S_ISREG(before.st_mode)
        or before.st_nlink != 1
        or before.st_size < 1
        or before.st_size > maximum_bytes
    ):
        fail(f"{label} must be one bounded, non-linked regular file")
    if expected_identity is not None and not _same_identity(
        before_identity, expected_identity
    ):
        fail(f"{label} descriptor differs from its immutable snapshot")
    if private_output and (
        before.st_uid != os.geteuid()
        or stat.S_IMODE(before.st_mode) & 0o077
    ):
        fail(f"{label} is not a caller-owned private output")
    digest = hashlib.sha256()
    data_parts: list[bytes] | None = [] if read_data else None
    size = 0
    while size < before.st_size:
        try:
            chunk = os.pread(
                descriptor,
                min(1024 * 1024, before.st_size - size),
                size,
            )
        except OSError as exc:
            fail(f"{label} descriptor could not be read safely: {exc}")
        if not chunk:
            fail(f"{label} ended before its declared size")
        size += len(chunk)
        if size > maximum_bytes:
            fail(f"{label} exceeded its fixed byte bound")
        digest.update(chunk)
        if data_parts is not None:
            data_parts.append(chunk)
    try:
        after_identity = _file_identity(os.fstat(descriptor))
    except OSError as exc:
        fail(f"{label} descriptor could not be rechecked: {exc}")
    if (
        not _same_identity(before_identity, after_identity)
        or size != before_identity.size_bytes
    ):
        fail(f"{label} changed while its held descriptor was read")
    return Snapshot(
        Path(os.path.abspath(path)),
        digest.hexdigest(),
        size,
        b"".join(data_parts) if data_parts is not None else None,
        before_identity,
    )


def snapshot(
    path: Path,
    label: str,
    maximum_bytes: int,
    *,
    read_data: bool = False,
) -> Snapshot:
    absolute = Path(os.path.abspath(path))
    try:
        before = absolute.lstat()
    except OSError as exc:
        fail(f"{label} cannot be inspected: {exc}")
    if (
        stat.S_ISLNK(before.st_mode)
        or not stat.S_ISREG(before.st_mode)
        or before.st_nlink != 1
        or before.st_size < 1
        or before.st_size > maximum_bytes
    ):
        fail(f"{label} must be one bounded, non-linked regular file")
    before_identity = _file_identity(before)
    descriptor = -1
    try:
        descriptor = os.open(
            absolute,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        held = _snapshot_descriptor(
            absolute,
            descriptor,
            label,
            maximum_bytes,
            read_data=read_data,
            expected_identity=before_identity,
        )
        after_identity = _file_identity(absolute.lstat())
    except ContractError:
        raise
    except OSError as exc:
        fail(f"{label} could not be read safely: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if not _same_identity(before_identity, after_identity):
        fail(f"{label} changed while it was read")
    return held


def _write_all(descriptor: int, data: bytes) -> None:
    offset = 0
    while offset < len(data):
        written = os.write(descriptor, data[offset:])
        if written < 1:
            fail("file write made no forward progress")
        offset += written


def _require_secure_parent(
    metadata: os.stat_result, label: str
) -> FileIdentity:
    mode = stat.S_IMODE(metadata.st_mode)
    sticky_root_directory = (
        metadata.st_uid == 0
        and bool(metadata.st_mode & stat.S_ISVTX)
    )
    if (
        not stat.S_ISDIR(metadata.st_mode)
        or metadata.st_uid not in {0, os.geteuid()}
        or (
            mode & 0o022
            and not sticky_root_directory
        )
    ):
        fail(
            f"{label} must be caller/root-owned and not group/world "
            "writable (except a root-owned sticky directory)"
        )
    return _file_identity(metadata)


def _open_secure_parent_descriptor(
    path: Path, *, create_missing: bool
) -> tuple[Path, int, str, FileIdentity]:
    """Traverse an absolute parent with dirfd + no-symlink semantics."""

    absolute = Path(os.path.abspath(path))
    parts = absolute.parent.parts
    if not absolute.is_absolute() or not parts or parts[0] != "/":
        fail("output path is not absolute")
    flags = (
        os.O_RDONLY
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_DIRECTORY", 0))
        | int(getattr(os, "O_NOFOLLOW", 0))
    )
    descriptor = os.open("/", flags)
    try:
        _require_secure_parent(os.fstat(descriptor), "output root")
        for component in parts[1:]:
            if component in {"", ".", ".."}:
                fail("output parent contains a non-canonical component")
            if create_missing:
                try:
                    os.mkdir(component, mode=0o700, dir_fd=descriptor)
                except FileExistsError:
                    pass
            next_descriptor = -1
            try:
                next_descriptor = os.open(
                    component, flags, dir_fd=descriptor
                )
                metadata = os.fstat(next_descriptor)
                _require_secure_parent(
                    metadata, f"output parent component {component!r}"
                )
            except BaseException:
                if next_descriptor >= 0:
                    os.close(next_descriptor)
                raise
            os.close(descriptor)
            descriptor = next_descriptor
        return (
            absolute,
            descriptor,
            absolute.name,
            _file_identity(os.fstat(descriptor)),
        )
    except OSError as exc:
        os.close(descriptor)
        fail(f"output parent cannot be traversed safely: {exc}")
    except BaseException:
        os.close(descriptor)
        raise


def _secure_parent_descriptor(
    path: Path,
) -> tuple[Path, int, str, FileIdentity]:
    return _open_secure_parent_descriptor(path, create_missing=True)


def _bounded_proc_bytes(path: Path, maximum: int, label: str) -> bytes:
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        data = os.read(descriptor, maximum + 1)
    except OSError as exc:
        fail(f"{label} could not be inspected: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if not data or len(data) > maximum:
        fail(f"{label} is empty or exceeds its fixed byte bound")
    return data


def _inspect_agent_process(
    pid: int, home: Path, socket_path: Path
) -> AgentProcess:
    if pid < 2:
        fail("ephemeral GnuPG agent PID is invalid")
    proc = Path("/proc") / str(pid)
    try:
        metadata = proc.stat()
    except OSError as exc:
        fail(f"ephemeral GnuPG agent process cannot be inspected: {exc}")
    if metadata.st_uid != os.geteuid():
        fail("ephemeral GnuPG agent process has the wrong owner")
    cmdline = _bounded_proc_bytes(
        proc / "cmdline", 64 * 1024, "ephemeral GnuPG agent command line"
    )
    try:
        arguments = [
            item.decode("utf-8", errors="strict")
            for item in cmdline.rstrip(b"\0").split(b"\0")
        ]
    except UnicodeDecodeError:
        fail("ephemeral GnuPG agent command line is not UTF-8")
    expected_home = str(Path(os.path.abspath(home)))
    home_bound = any(
        (
            value == "--homedir"
            and index + 1 < len(arguments)
            and arguments[index + 1] == expected_home
        )
        or value == f"--homedir={expected_home}"
        for index, value in enumerate(arguments)
    )
    if (
        not arguments
        or Path(arguments[0]).name != "gpg-agent"
        or not home_bound
    ):
        fail("ephemeral GnuPG agent process identity is not governed")
    try:
        command_name = _bounded_proc_bytes(
            proc / "comm",
            256,
            "ephemeral GnuPG agent process name",
        ).decode("ascii", errors="strict").strip()
    except UnicodeDecodeError:
        fail("ephemeral GnuPG agent process name is not ASCII")
    if command_name != "gpg-agent":
        fail("ephemeral GnuPG agent process name is not gpg-agent")
    raw_stat = _bounded_proc_bytes(
        proc / "stat", 64 * 1024, "ephemeral GnuPG agent process stat"
    )
    try:
        stat_text = raw_stat.decode("ascii", errors="strict")
        closing = stat_text.rindex(")")
        fields = stat_text[closing + 2 :].split()
        start_time = int(fields[19], 10)
    except (UnicodeDecodeError, ValueError, IndexError):
        fail("ephemeral GnuPG agent process stat is malformed")
    if start_time < 1:
        fail("ephemeral GnuPG agent start time is invalid")
    return AgentProcess(pid, start_time, socket_path)


def _discover_ephemeral_agent(home: Path) -> AgentProcess | None:
    socket_result = run_tool(
        [
            "/usr/bin/gpgconf",
            "--homedir",
            str(home),
            "--list-dirs",
            "agent-socket",
        ],
        label="ephemeral GnuPG agent socket discovery",
        environment=_gpg_environment(home),
        expected_exit=None,
        timeout_seconds=AGENT_TOOL_TIMEOUT_SECONDS,
    )
    if socket_result.returncode != 0:
        fail("ephemeral GnuPG agent socket discovery failed")
    socket_text = _bounded_output(
        socket_result.stdout, "ephemeral GnuPG agent socket path"
    ).strip()
    if (
        not socket_text
        or "\n" in socket_text
        or "\r" in socket_text
        or "\0" in socket_text
    ):
        fail("ephemeral GnuPG agent socket path is malformed")
    socket_path = Path(socket_text)
    if not socket_path.is_absolute():
        fail("ephemeral GnuPG agent socket path is not absolute")
    try:
        socket_metadata = socket_path.lstat()
    except FileNotFoundError:
        return None
    except OSError as exc:
        fail(f"ephemeral GnuPG agent socket cannot be inspected: {exc}")
    if (
        not stat.S_ISSOCK(socket_metadata.st_mode)
        or socket_metadata.st_uid != os.geteuid()
    ):
        fail("ephemeral GnuPG agent socket is not a caller-owned socket")
    _, parent_descriptor, basename, _ = (
        _open_secure_parent_descriptor(
            socket_path, create_missing=False
        )
    )
    try:
        linked = os.stat(
            basename,
            dir_fd=parent_descriptor,
            follow_symlinks=False,
        )
    except OSError as exc:
        fail(f"ephemeral GnuPG agent socket link changed: {exc}")
    finally:
        os.close(parent_descriptor)
    if (
        linked.st_dev != socket_metadata.st_dev
        or linked.st_ino != socket_metadata.st_ino
        or not stat.S_ISSOCK(linked.st_mode)
    ):
        fail("ephemeral GnuPG agent socket changed during discovery")
    pid_result = run_tool(
        [
            "/usr/bin/gpg-connect-agent",
            "--raw-socket",
            str(socket_path),
            "GETINFO pid",
            "/bye",
        ],
        label="ephemeral GnuPG agent PID discovery",
        environment=_gpg_environment(home),
        expected_exit=None,
        timeout_seconds=AGENT_TOOL_TIMEOUT_SECONDS,
    )
    if pid_result.returncode != 0:
        fail("ephemeral GnuPG agent PID discovery failed")
    pid_text = _bounded_output(
        pid_result.stdout, "ephemeral GnuPG agent PID"
    )
    matches = re.findall(r"(?m)^D ([1-9][0-9]*)$", pid_text)
    if len(matches) != 1:
        fail("ephemeral GnuPG agent PID response is malformed")
    return _inspect_agent_process(int(matches[0], 10), home, socket_path)


def _agent_start_time(pid: int) -> int | None:
    proc = Path("/proc") / str(pid)
    try:
        metadata = proc.stat()
    except FileNotFoundError:
        return None
    except OSError as exc:
        fail(f"ephemeral GnuPG agent process cannot be rechecked: {exc}")
    if metadata.st_uid != os.geteuid():
        return None
    raw_stat = _bounded_proc_bytes(
        proc / "stat",
        64 * 1024,
        "ephemeral GnuPG agent process stat recheck",
    )
    try:
        stat_text = raw_stat.decode("ascii", errors="strict")
        closing = stat_text.rindex(")")
        fields = stat_text[closing + 2 :].split()
        return int(fields[19], 10)
    except (UnicodeDecodeError, ValueError, IndexError):
        fail("ephemeral GnuPG agent process stat recheck is malformed")


def _same_agent_process(_home: Path, expected: AgentProcess) -> bool:
    return (
        _agent_start_time(expected.pid)
        == expected.start_time_ticks
    )


def _wait_for_agent_exit(
    home: Path, process: AgentProcess, timeout_seconds: int
) -> bool:
    deadline = time.monotonic() + timeout_seconds
    while _same_agent_process(home, process):
        if time.monotonic() >= deadline:
            return False
        time.sleep(0.05)
    return True


def _confirm_agent_stopped(
    home: Path, process: AgentProcess | None
) -> bool:
    if process is not None:
        if not _wait_for_agent_exit(
            home, process, AGENT_EXIT_TIMEOUT_SECONDS
        ):
            return False
    try:
        return _discover_ephemeral_agent(home) is None
    except ContractError:
        return False


def _process_uses_ephemeral_agent_home(pid: int, home: Path) -> bool:
    proc = Path("/proc") / str(pid)
    try:
        metadata = proc.stat()
    except FileNotFoundError:
        return False
    except OSError as exc:
        fail(f"process scope cannot be inspected after agent cleanup: {exc}")
    if metadata.st_uid != os.geteuid():
        return False
    descriptor = -1
    try:
        descriptor = os.open(
            proc / "cmdline",
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        command_line = os.read(descriptor, 64 * 1024 + 1)
    except FileNotFoundError:
        return False
    except OSError as exc:
        if not proc.exists():
            return False
        fail(f"process command line cannot be inspected after cleanup: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if not command_line or len(command_line) > 64 * 1024:
        return False
    try:
        arguments = [
            item.decode("utf-8", errors="strict")
            for item in command_line.rstrip(b"\0").split(b"\0")
        ]
    except UnicodeDecodeError:
        return False
    expected_home = str(Path(os.path.abspath(home)))
    return (
        bool(arguments)
        and Path(arguments[0]).name == "gpg-agent"
        and any(
            (
                value == "--homedir"
                and index + 1 < len(arguments)
                and arguments[index + 1] == expected_home
            )
            or value == f"--homedir={expected_home}"
            for index, value in enumerate(arguments)
        )
    )


def _ephemeral_agent_pids_for_home(home: Path) -> list[int]:
    pids: list[int] = []
    try:
        entries = list(os.scandir("/proc"))
    except OSError as exc:
        fail(f"process table cannot be inspected after agent cleanup: {exc}")
    for entry in entries:
        if not entry.name.isdigit():
            continue
        pid = int(entry.name, 10)
        if pid > 1 and _process_uses_ephemeral_agent_home(pid, home):
            pids.append(pid)
    return sorted(pids)


def _require_ephemeral_agent_scope_absent(
    home: Path, socket_path: Path | None
) -> None:
    if os.path.lexists(home):
        fail("ephemeral GnuPG home remains after cleanup")
    if socket_path is not None and os.path.lexists(socket_path):
        fail("ephemeral GnuPG agent socket remains after cleanup")
    if _ephemeral_agent_pids_for_home(home):
        fail("replacement GnuPG agent remains bound to the ephemeral home")


def _last_resort_agent_shutdown(
    home: Path, process: AgentProcess
) -> bool:
    if not _same_agent_process(home, process):
        return True
    pid_descriptor = -1
    try:
        if hasattr(os, "pidfd_open"):
            pid_descriptor = os.pidfd_open(process.pid, 0)
        if not _same_agent_process(home, process):
            return True
        if (
            pid_descriptor >= 0
            and hasattr(signal, "pidfd_send_signal")
        ):
            signal.pidfd_send_signal(
                pid_descriptor, signal.SIGTERM, None, 0
            )
        else:
            os.kill(process.pid, signal.SIGTERM)
        if _wait_for_agent_exit(
            home, process, AGENT_EXIT_TIMEOUT_SECONDS
        ):
            return True
        if not _same_agent_process(home, process):
            return True
        if (
            pid_descriptor >= 0
            and hasattr(signal, "pidfd_send_signal")
        ):
            signal.pidfd_send_signal(
                pid_descriptor, signal.SIGKILL, None, 0
            )
        else:
            os.kill(process.pid, signal.SIGKILL)
        return _wait_for_agent_exit(
            home, process, AGENT_EXIT_TIMEOUT_SECONDS
        )
    except OSError:
        return not _same_agent_process(home, process)
    finally:
        if pid_descriptor >= 0:
            os.close(pid_descriptor)


def _post_private_output_write(
    absolute: Path,
    parent_descriptor: int,
    basename: str,
    output_descriptor: int,
) -> None:
    """Fault-injection hook; production intentionally performs no action."""


def _finalize_private_output(
    *,
    absolute: Path,
    parent_descriptor: int,
    parent_identity: FileIdentity,
    basename: str,
    output_descriptor: int,
    label: str,
    maximum_bytes: int,
) -> Snapshot:
    reopened_parent = -1
    try:
        parent_after_write = _file_identity(os.fstat(parent_descriptor))
    except OSError as exc:
        fail(f"{label} output parent could not be inspected: {exc}")
    if not _same_directory_object(parent_identity, parent_after_write):
        fail(f"{label} output parent identity changed during creation")
    held = _snapshot_descriptor(
        absolute,
        output_descriptor,
        label,
        maximum_bytes,
        private_output=True,
    )
    _post_private_output_write(
        absolute, parent_descriptor, basename, output_descriptor
    )
    try:
        held_after_hook = _file_identity(os.fstat(output_descriptor))
    except OSError as exc:
        fail(f"{label} output descriptor could not be rechecked: {exc}")
    if (
        held.identity is None
        or not _same_identity(held.identity, held_after_hook)
    ):
        fail(f"{label} output link or parent changed after creation")
    rechecked = _snapshot_descriptor(
        absolute,
        output_descriptor,
        label,
        maximum_bytes,
        expected_identity=held.identity,
        private_output=True,
    )
    if (
        rechecked.sha256 != held.sha256
        or rechecked.size_bytes != held.size_bytes
    ):
        fail(f"{label} output bytes changed after held-fd snapshot")
    try:
        held_parent_link = _file_identity(
            os.stat(
                basename,
                dir_fd=parent_descriptor,
                follow_symlinks=False,
            )
        )
        final_parent_identity = _file_identity(
            os.fstat(parent_descriptor)
        )
        (
            reopened_absolute,
            reopened_parent,
            reopened_basename,
            reopened_parent_identity,
        ) = _open_secure_parent_descriptor(
            absolute, create_missing=False
        )
        reopened_link = _file_identity(
            os.stat(
                reopened_basename,
                dir_fd=reopened_parent,
                follow_symlinks=False,
            )
        )
    except OSError as exc:
        fail(f"{label} output link could not be rechecked: {exc}")
    finally:
        if reopened_parent >= 0:
            os.close(reopened_parent)
    if (
        held.identity is None
        or reopened_absolute != absolute
        or reopened_basename != basename
        or not _same_identity(held.identity, held_parent_link)
        or not _same_identity(held.identity, reopened_link)
        or not _same_identity(
            parent_after_write, final_parent_identity
        )
        or not _same_directory_object(
            parent_after_write, reopened_parent_identity
        )
    ):
        fail(f"{label} output link or parent changed after creation")
    return Snapshot(
        held.path,
        held.sha256,
        held.size_bytes,
        held.data,
        held.identity,
        final_parent_identity,
    )


def write_new_bytes(path: Path, data: bytes, label: str) -> Snapshot:
    (
        absolute,
        parent_descriptor,
        basename,
        parent_identity,
    ) = _secure_parent_descriptor(path)
    descriptor = -1
    try:
        descriptor = os.open(
            basename,
            os.O_RDWR
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
            dir_fd=parent_descriptor,
        )
        _write_all(descriptor, data)
        os.fsync(descriptor)
        written = _finalize_private_output(
            absolute=absolute,
            parent_descriptor=parent_descriptor,
            parent_identity=parent_identity,
            basename=basename,
            output_descriptor=descriptor,
            label=label,
            maximum_bytes=max(len(data), 1),
        )
        if (
            written.size_bytes != len(data)
            or written.sha256 != hashlib.sha256(data).hexdigest()
        ):
            fail(f"{label} differs from its intended bytes")
        return written
    except OSError as exc:
        fail(f"{label} must be a new private regular file: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        os.close(parent_descriptor)


def write_new_json(path: Path, payload: Mapping[str, Any], label: str) -> Snapshot:
    return write_new_bytes(
        path,
        (
            json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False)
            + "\n"
        ).encode("utf-8"),
        label,
    )


def _pre_private_input_copy(source: Snapshot) -> None:
    """Fault-injection hook; production intentionally performs no action."""


def copy_new(source: Snapshot, destination: Path, label: str) -> Snapshot:
    if source.identity is None:
        fail(f"{label} input lacks a held file identity")
    (
        absolute,
        parent_descriptor,
        basename,
        parent_identity,
    ) = _secure_parent_descriptor(destination)
    source_descriptor = -1
    target_descriptor = -1
    try:
        _pre_private_input_copy(source)
        source_descriptor = os.open(
            source.path,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        source_before = _file_identity(os.fstat(source_descriptor))
        if not _same_identity(source_before, source.identity):
            fail(f"{label} input changed before safe copying")
        target_descriptor = os.open(
            basename,
            os.O_RDWR
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
            dir_fd=parent_descriptor,
        )
        copied_digest = hashlib.sha256()
        copied_size = 0
        while True:
            chunk = os.read(source_descriptor, 1024 * 1024)
            if not chunk:
                break
            copied_size += len(chunk)
            if copied_size > source.size_bytes:
                fail(f"{label} input exceeded its immutable size")
            copied_digest.update(chunk)
            _write_all(target_descriptor, chunk)
        os.fsync(target_descriptor)
        source_after = _file_identity(os.fstat(source_descriptor))
        try:
            source_link = _file_identity(source.path.lstat())
        except OSError as exc:
            fail(f"{label} input link could not be rechecked: {exc}")
        if (
            not _same_identity(source_before, source_after)
            or not _same_identity(source_before, source_link)
            or copied_size != source.size_bytes
            or copied_digest.hexdigest() != source.sha256
        ):
            fail(f"{label} input changed while it was copied")
        copied = _finalize_private_output(
            absolute=absolute,
            parent_descriptor=parent_descriptor,
            parent_identity=parent_identity,
            basename=basename,
            output_descriptor=target_descriptor,
            label=label,
            maximum_bytes=MAX_PACKAGE_BYTES,
        )
        if (
            copied.sha256 != source.sha256
            or copied.size_bytes != source.size_bytes
        ):
            fail(f"{label} differs from its input bytes")
        return copied
    except OSError as exc:
        fail(f"{label} could not be copied safely: {exc}")
    finally:
        if source_descriptor >= 0:
            os.close(source_descriptor)
        if target_descriptor >= 0:
            os.close(target_descriptor)
        os.close(parent_descriptor)


def _revalidate_private_output(
    original: Snapshot,
    label: str,
    maximum_bytes: int,
    *,
    allow_parent_metadata_change: bool = False,
) -> Snapshot:
    if original.identity is None or original.parent_identity is None:
        fail(f"{label} lacks its held output or parent identity")
    current = snapshot(
        original.path, f"{label} end seal", maximum_bytes
    )
    if (
        current.identity is None
        or current.sha256 != original.sha256
        or current.size_bytes != original.size_bytes
        or not _same_identity(current.identity, original.identity)
    ):
        fail(f"{label} changed before its final output seal")
    (
        absolute,
        parent_descriptor,
        basename,
        parent_identity,
    ) = _open_secure_parent_descriptor(
        original.path, create_missing=False
    )
    try:
        linked = _file_identity(
            os.stat(
                basename,
                dir_fd=parent_descriptor,
                follow_symlinks=False,
            )
        )
    except OSError as exc:
        fail(f"{label} output link cannot be end-sealed: {exc}")
    finally:
        os.close(parent_descriptor)
    if (
        absolute != original.path
        or not (
            _same_directory_anchor(
                parent_identity, original.parent_identity
            )
            if allow_parent_metadata_change
            else _same_identity(
                parent_identity, original.parent_identity
            )
        )
        or not _same_identity(linked, original.identity)
    ):
        fail(f"{label} output or parent changed before final return")
    return Snapshot(
        current.path,
        current.sha256,
        current.size_bytes,
        current.data,
        current.identity,
        parent_identity,
    )


def _bind_private_output_parent(
    snapshot_value: Snapshot, label: str
) -> Snapshot:
    if snapshot_value.identity is None:
        fail(f"{label} lacks a held output identity")
    if (
        snapshot_value.identity.owner_uid != os.geteuid()
        or stat.S_IMODE(snapshot_value.identity.mode) & 0o077
    ):
        fail(f"{label} is not a caller-owned private output")
    (
        absolute,
        parent_descriptor,
        basename,
        parent_identity,
    ) = _open_secure_parent_descriptor(
        snapshot_value.path, create_missing=False
    )
    try:
        linked = _file_identity(
            os.stat(
                basename,
                dir_fd=parent_descriptor,
                follow_symlinks=False,
            )
        )
    except OSError as exc:
        fail(f"{label} output link cannot be bound safely: {exc}")
    finally:
        os.close(parent_descriptor)
    if (
        absolute != snapshot_value.path
        or not _same_identity(linked, snapshot_value.identity)
    ):
        fail(f"{label} output changed before parent binding")
    return Snapshot(
        snapshot_value.path,
        snapshot_value.sha256,
        snapshot_value.size_bytes,
        snapshot_value.data,
        snapshot_value.identity,
        parent_identity,
    )


def _post_output_transaction_commit(
    outputs: Mapping[str, Snapshot], manifest: Snapshot
) -> None:
    """Fault-injection hook; production intentionally performs no action."""


def _transaction_output_members(args: argparse.Namespace) -> dict[str, str]:
    members = {
        "package": safe_member(
            args.artifact_member_path, "transaction artifact member path"
        ),
        "policy": safe_member(
            args.policy_member_path, "transaction policy member path"
        ),
        "publicKeyring": safe_member(
            args.public_keyring_member_path,
            "transaction public keyring member path",
        ),
        "signingReceipt": safe_member(
            args.signing_receipt_member_path,
            "transaction signing receipt member path",
        ),
        "signedExportReceipt": SIGNED_EXPORT_RECEIPT_FILE_NAME,
    }
    if (
        PurePosixPath(members["package"]).name != ARTIFACT_FILE_NAME
        or PurePosixPath(members["policy"]).name != POLICY_FILE_NAME
        or PurePosixPath(members["publicKeyring"]).name != KEYRING_FILE_NAME
        or PurePosixPath(members["signingReceipt"]).name
        != SIGNING_RECEIPT_FILE_NAME
    ):
        fail("transaction output member paths are not canonical")
    return members


def _transaction_payload(
    *,
    outputs: Mapping[str, Snapshot],
    members: Mapping[str, str],
) -> dict[str, Any]:
    if set(outputs) != TRANSACTION_OUTPUT_KEYS or set(members) != (
        TRANSACTION_OUTPUT_KEYS
    ):
        fail("Linux signing transaction requires exactly five outputs")
    return {
        "contractName": TRANSACTION_CONTRACT,
        "contractVersion": 1,
        "digestAlgorithm": "sha256",
        "outputs": {
            key: {
                "memberPath": members[key],
                "sha256": outputs[key].sha256,
                "sizeBytes": outputs[key].size_bytes,
            }
            for key in sorted(TRANSACTION_OUTPUT_KEYS)
        },
    }


def validate_transaction_manifest(
    payload: Any,
    *,
    outputs: Mapping[str, Snapshot],
    members: Mapping[str, str],
) -> dict[str, Any]:
    manifest = exact_dict(
        payload,
        {
            "contractName",
            "contractVersion",
            "digestAlgorithm",
            "outputs",
        },
        "Linux signing transaction manifest",
    )
    expected = _transaction_payload(
        outputs=outputs,
        members=members,
    )
    manifest_outputs = manifest.get("outputs")
    if (
        type(manifest.get("contractVersion")) is not int
        or not isinstance(manifest_outputs, dict)
        or any(
            not isinstance(manifest_outputs.get(key), dict)
            or type(manifest_outputs[key].get("sizeBytes")) is not int
            for key in TRANSACTION_OUTPUT_KEYS
        )
        or manifest != expected
    ):
        fail(
            "Linux signing transaction manifest differs from all five "
            "rehash-bound outputs"
        )
    return manifest


def commit_output_transaction(
    *,
    outputs: Mapping[str, tuple[Snapshot, str, int]],
    manifest_path: Path,
    members: Mapping[str, str],
) -> tuple[dict[str, Snapshot], Snapshot]:
    if set(outputs) != {
        "package",
        "policy",
        "public_keyring",
        "receipt",
        "signed_export_receipt",
    }:
        fail("Linux signing transaction requires exactly five outputs")
    if Path(os.path.abspath(manifest_path)).name != (
        TRANSACTION_MANIFEST_FILE_NAME
    ):
        fail("Linux signing transaction manifest filename is not canonical")
    first_internal = {
        key: _revalidate_private_output(
            snapshot_value,
            label,
            maximum,
            allow_parent_metadata_change=True,
        )
        for key, (snapshot_value, label, maximum) in outputs.items()
    }
    first = {
        "package": first_internal["package"],
        "policy": first_internal["policy"],
        "publicKeyring": first_internal["public_keyring"],
        "signingReceipt": first_internal["receipt"],
        "signedExportReceipt": first_internal["signed_export_receipt"],
    }
    payload = _transaction_payload(
        outputs=first,
        members=members,
    )
    manifest = write_new_json(
        manifest_path,
        payload,
        "commit-last Linux signing transaction manifest",
    )
    _post_output_transaction_commit(first, manifest)
    second_internal = {
        key: _revalidate_private_output(
            snapshot_value,
            label,
            maximum,
            allow_parent_metadata_change=True,
        )
        for key, (snapshot_value, label, maximum) in outputs.items()
    }
    second = {
        "package": second_internal["package"],
        "policy": second_internal["policy"],
        "publicKeyring": second_internal["public_keyring"],
        "signingReceipt": second_internal["receipt"],
        "signedExportReceipt": second_internal["signed_export_receipt"],
    }
    validate_transaction_manifest(
        payload,
        outputs=second,
        members=members,
    )
    manifest_after = _revalidate_private_output(
        manifest,
        "commit-last Linux signing transaction manifest",
        MAX_JSON_BYTES,
    )
    return second, manifest_after


def _canonical_transaction_members(
    signing_fingerprint: str,
) -> dict[str, str]:
    fingerprint = require_text(
        signing_fingerprint,
        "transaction primary fingerprint",
        FINGERPRINT_RE,
    )
    long_key_id = fingerprint[-16:]
    return {
        "package": f"files/{ARTIFACT_FILE_NAME}",
        "policy": (
            f"signing/policies/{long_key_id}/{POLICY_FILE_NAME}"
        ),
        "publicKeyring": (
            f"signing/keyrings/{long_key_id}/{KEYRING_FILE_NAME}"
        ),
        "signingReceipt": f"signing/{SIGNING_RECEIPT_FILE_NAME}",
        "signedExportReceipt": SIGNED_EXPORT_RECEIPT_FILE_NAME,
    }


def _transaction_input_snapshots(
    args: argparse.Namespace,
) -> dict[str, Snapshot]:
    return {
        "package": snapshot(
            args.package, "transaction package", MAX_PACKAGE_BYTES
        ),
        "policy": snapshot(
            args.policy, "transaction policy", MAX_JSON_BYTES
        ),
        "publicKeyring": snapshot(
            args.public_keyring,
            "transaction public keyring",
            MAX_KEY_BYTES,
        ),
        "signingReceipt": snapshot(
            args.receipt, "transaction signing receipt", MAX_JSON_BYTES
        ),
        "signedExportReceipt": snapshot(
            args.signed_export_receipt,
            "transaction signed export receipt",
            MAX_JSON_BYTES,
        ),
    }


def _tree_regular_members(root: Path) -> set[str]:
    members: set[str] = set()
    pending = [Path(os.path.abspath(root))]
    visited_directories = 0
    while pending:
        directory = pending.pop()
        visited_directories += 1
        if visited_directories > 64:
            fail("published transaction tree exceeds its directory bound")
        try:
            entries = list(os.scandir(directory))
        except OSError as exc:
            fail(f"published transaction tree cannot be inspected: {exc}")
        for entry in entries:
            try:
                metadata = entry.stat(follow_symlinks=False)
            except OSError as exc:
                fail(f"published transaction member cannot be inspected: {exc}")
            path = Path(entry.path)
            if stat.S_ISDIR(metadata.st_mode):
                pending.append(path)
                continue
            if (
                not stat.S_ISREG(metadata.st_mode)
                or metadata.st_nlink != 1
                or metadata.st_uid != os.geteuid()
                or stat.S_IMODE(metadata.st_mode) & 0o077
            ):
                fail(
                    "published transaction tree contains a non-private "
                    "or non-regular member"
                )
            try:
                relative = path.relative_to(root).as_posix()
            except ValueError:
                fail("published transaction member escapes its root")
            members.add(relative)
            if len(members) > 6:
                fail("published transaction tree contains extra files")
    return members


def _fsync_directory_tree(root: Path) -> None:
    directories = [path for path, dirs, _files in os.walk(root) for path in (
        Path(path),
    )]
    for directory in sorted(
        directories, key=lambda value: len(value.parts), reverse=True
    ):
        descriptor = -1
        try:
            descriptor = os.open(
                directory,
                os.O_RDONLY
                | int(getattr(os, "O_CLOEXEC", 0))
                | int(getattr(os, "O_DIRECTORY", 0))
                | int(getattr(os, "O_NOFOLLOW", 0)),
            )
            os.fsync(descriptor)
        except OSError as exc:
            fail(f"transaction directory cannot be durably staged: {exc}")
        finally:
            if descriptor >= 0:
                os.close(descriptor)


def _rename_directory_noreplace(
    parent_descriptor: int, source_name: str, destination_name: str
) -> None:
    if not source_name or not destination_name:
        fail("transaction publish names are empty")
    libc = ctypes.CDLL(None, use_errno=True)
    renameat2 = getattr(libc, "renameat2", None)
    if renameat2 is None:
        fail("atomic no-replace transaction publication is unavailable")
    renameat2.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    renameat2.restype = ctypes.c_int
    result = renameat2(
        parent_descriptor,
        os.fsencode(source_name),
        parent_descriptor,
        os.fsencode(destination_name),
        1,
    )
    if result != 0:
        error_number = ctypes.get_errno()
        fail(
            "atomic no-replace transaction publication failed: "
            f"{os.strerror(error_number)}"
        )


def _stage_output_set(args: argparse.Namespace) -> dict[str, Any]:
    expected_manifest_sha256 = require_sha256(
        args.expected_transaction_manifest_sha256,
        "authenticated transaction manifest SHA-256",
    )
    members = _canonical_transaction_members(
        args.expected_primary_fingerprint
    )
    manifest_payload, manifest = load_json(
        args.transaction_manifest,
        "commit-last Linux signing transaction manifest",
    )
    if manifest.sha256 != expected_manifest_sha256:
        fail("transaction manifest differs from authenticated authority")
    sources = _transaction_input_snapshots(args)
    validate_transaction_manifest(
        manifest_payload, outputs=sources, members=members
    )
    output_root = Path(os.path.abspath(args.output_root))
    (
        _absolute,
        parent_descriptor,
        output_name,
        parent_identity,
    ) = _open_secure_parent_descriptor(
        output_root, create_missing=False
    )
    if os.path.lexists(output_root):
        os.close(parent_descriptor)
        fail("published transaction output root already exists")
    staged_root: Path | None = None
    try:
        with tempfile.TemporaryDirectory(
            prefix=f".{output_name}.stage-",
            dir=output_root.parent,
        ) as raw_stage:
            staged_root = Path(raw_stage)
            staged_root.chmod(0o700)
            stage_metadata = _file_identity(staged_root.lstat())
            linked_stage = _file_identity(
                os.stat(
                    staged_root.name,
                    dir_fd=parent_descriptor,
                    follow_symlinks=False,
                )
            )
            if (
                not stat.S_ISDIR(stage_metadata.mode)
                or not _same_identity(stage_metadata, linked_stage)
                or stage_metadata.owner_uid != os.geteuid()
                or stat.S_IMODE(stage_metadata.mode) & 0o077
            ):
                fail("transaction staging root is not private and held")
            staged = {
                key: copy_new(
                    sources[key],
                    staged_root.joinpath(
                        *PurePosixPath(members[key]).parts
                    ),
                    f"staged transaction {key}",
                )
                for key in sorted(TRANSACTION_OUTPUT_KEYS)
            }
            staged_manifest = copy_new(
                manifest,
                staged_root / TRANSACTION_MANIFEST_FILE_NAME,
                "staged commit-last transaction manifest",
            )
            validate_transaction_manifest(
                manifest_payload, outputs=staged, members=members
            )
            expected_files = set(members.values()) | {
                TRANSACTION_MANIFEST_FILE_NAME
            }
            if _tree_regular_members(staged_root) != expected_files:
                fail("staged transaction tree is not the exact six-file set")
            _fsync_directory_tree(staged_root)
            parent_before_publish = _file_identity(
                os.fstat(parent_descriptor)
            )
            _rename_directory_noreplace(
                parent_descriptor, staged_root.name, output_name
            )
            os.fsync(parent_descriptor)
            staged_root = None
        parent_after = _file_identity(os.fstat(parent_descriptor))
        if (
            not _same_directory_object(
                parent_before_publish, parent_after
            )
            or not _same_directory_anchor(
                parent_identity, parent_after
            )
        ):
            fail("transaction publication parent object changed")
    finally:
        os.close(parent_descriptor)
    published = {
        key: snapshot(
            output_root.joinpath(*PurePosixPath(members[key]).parts),
            f"published transaction {key}",
            (
                MAX_PACKAGE_BYTES
                if key == "package"
                else MAX_KEY_BYTES
                if key == "publicKeyring"
                else MAX_JSON_BYTES
            ),
        )
        for key in sorted(TRANSACTION_OUTPUT_KEYS)
    }
    published_manifest_payload, published_manifest = load_json(
        output_root / TRANSACTION_MANIFEST_FILE_NAME,
        "published commit-last transaction manifest",
    )
    if (
        published_manifest.sha256 != staged_manifest.sha256
        or published_manifest.size_bytes != staged_manifest.size_bytes
        or _tree_regular_members(output_root)
        != set(members.values()) | {TRANSACTION_MANIFEST_FILE_NAME}
    ):
        fail("published transaction tree differs after atomic commit")
    validate_transaction_manifest(
        published_manifest_payload,
        outputs=published,
        members=members,
    )
    return {
        "publishedRoot": str(output_root),
        "transactionManifestSha256": published_manifest.sha256,
        "transactionManifestSizeBytes": published_manifest.size_bytes,
    }


def require_unchanged_inputs(
    inputs: Mapping[str, tuple[Snapshot, str, int]],
) -> None:
    for before, label, maximum in inputs.values():
        after = snapshot(before.path, f"{label} final recheck", maximum)
        if (
            before.identity is None
            or after.identity is None
            or before.sha256 != after.sha256
            or before.size_bytes != after.size_bytes
            or not _same_identity(before.identity, after.identity)
        ):
            fail(f"{label} changed across the governed operation")


def _post_lifecycle_package_stage(
    staged: Mapping[str, Snapshot],
) -> None:
    """Fault-injection hook; production intentionally performs no action."""


def _stage_lifecycle_packages_for_current_user(
    args: argparse.Namespace,
    *,
    temporary_parent: Path = Path("/var/tmp"),
) -> dict[str, Any]:
    expected = {
        "candidate": (
            require_sha256(
                args.expected_candidate_sha256,
                "lifecycle candidate SHA-256",
            ),
            int(
                require_positive_integer_text(
                    args.expected_candidate_size,
                    "lifecycle candidate size",
                )
            ),
        ),
        "n_minus_one": (
            require_sha256(
                args.expected_n_minus_one_sha256,
                "lifecycle N-1 SHA-256",
            ),
            int(
                require_positive_integer_text(
                    args.expected_n_minus_one_size,
                    "lifecycle N-1 size",
                )
            ),
        ),
    }
    limits = {
        "candidate": MAX_PACKAGE_BYTES,
        "n_minus_one": MAX_PACKAGE_BYTES,
    }
    external = {
        "candidate": snapshot(
            args.candidate,
            "external lifecycle candidate package",
            MAX_PACKAGE_BYTES,
        ),
        "n_minus_one": snapshot(
            args.n_minus_one,
            "external lifecycle N-1 package",
            MAX_PACKAGE_BYTES,
        ),
    }
    for key, before in external.items():
        if (before.sha256, before.size_bytes) != expected[key]:
            fail(
                f"external lifecycle {key.replace('_', ' ')} package "
                "differs from authenticated authority"
            )
    parent = Path(os.path.abspath(temporary_parent))
    if (
        parent != temporary_parent
        or not parent.is_dir()
        or parent.is_symlink()
    ):
        fail("lifecycle protected-package parent is unsafe")
    protected_root = Path(
        tempfile.mkdtemp(
            prefix="chummer-linux-lifecycle-packages.",
            dir=parent,
        )
    )
    try:
        protected_root.chmod(0o700)
        root_metadata = protected_root.stat(follow_symlinks=False)
        if (
            not stat.S_ISDIR(root_metadata.st_mode)
            or root_metadata.st_uid != os.geteuid()
            or stat.S_IMODE(root_metadata.st_mode) != 0o700
        ):
            fail("lifecycle protected-package root is unsafe")
        staged = {
            "candidate": copy_new(
                external["candidate"],
                protected_root / "candidate.deb",
                "protected lifecycle candidate package",
            ),
            "n_minus_one": copy_new(
                external["n_minus_one"],
                protected_root / "n-minus-one.deb",
                "protected lifecycle N-1 package",
            ),
        }
        _post_lifecycle_package_stage(staged)
        require_unchanged_inputs(
            {
                key: (
                    staged[key],
                    f"protected lifecycle {key.replace('_', ' ')} package",
                    limits[key],
                )
                for key in staged
            }
        )
        require_unchanged_inputs(
            {
                key: (
                    external[key],
                    f"external lifecycle {key.replace('_', ' ')} package",
                    limits[key],
                )
                for key in external
            }
        )
        return {
            "candidatePath": staged["candidate"].path,
            "nMinusOnePath": staged["n_minus_one"].path,
            "protectedRoot": protected_root,
        }
    except BaseException:
        shutil.rmtree(protected_root, ignore_errors=True)
        raise


def _stage_lifecycle_packages(
    args: argparse.Namespace,
) -> dict[str, Any]:
    if os.geteuid() != 0:
        fail("lifecycle protected-package staging requires root")
    return _stage_lifecycle_packages_for_current_user(args)


def load_json(path: Path, label: str) -> tuple[dict[str, Any], Snapshot]:
    held = snapshot(path, label, MAX_JSON_BYTES, read_data=True)
    assert held.data is not None
    try:
        value = json.loads(
            held.data.decode("utf-8"),
            object_pairs_hook=_reject_duplicates,
            parse_constant=lambda token: fail(
                f"{label} contains non-finite JSON token {token}"
            ),
        )
    except (UnicodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is not exact UTF-8 JSON: {exc}")
    if not isinstance(value, dict):
        fail(f"{label} must contain an object")
    return value, held


def _reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON contains duplicate key {key!r}")
        result[key] = value
    return result


def _bounded_output(data: bytes, label: str) -> str:
    if len(data) > MAX_TOOL_OUTPUT_BYTES:
        fail(f"{label} output exceeded its fixed byte bound")
    try:
        return data.decode("utf-8", errors="strict")
    except UnicodeDecodeError:
        fail(f"{label} output is not UTF-8")


def run_tool(
    command: Sequence[str],
    *,
    label: str,
    environment: Mapping[str, str] | None = None,
    input_data: bytes | None = None,
    expected_exit: int | None = 0,
    allow_binary_stdout: bool = False,
    timeout_seconds: int = 180,
) -> subprocess.CompletedProcess[bytes]:
    if timeout_seconds < 1 or timeout_seconds > 180:
        fail(f"{label} timeout is outside its fixed bound")
    child_environment = (
        dict(MINIMAL_TOOL_ENVIRONMENT)
        if environment is None
        else dict(environment)
    )
    for secret_name in (PRIVATE_KEY_ENV, PASSPHRASE_ENV):
        if secret_name in child_environment:
            fail(f"{label} child environment contains signing secret material")
    try:
        completed = subprocess.run(
            list(command),
            env=child_environment,
            input=input_data,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            timeout=timeout_seconds,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        fail(f"{label} could not execute: {exc}")
    if len(completed.stdout) > MAX_TOOL_OUTPUT_BYTES:
        fail(f"{label} stdout output exceeded its fixed byte bound")
    stdout = (
        ""
        if allow_binary_stdout
        else _bounded_output(completed.stdout, f"{label} stdout")
    )
    stderr = _bounded_output(completed.stderr, f"{label} stderr")
    if expected_exit is not None and completed.returncode != expected_exit:
        detail = (stderr or stdout).strip()
        if len(detail) > 1000:
            detail = detail[:1000] + "..."
        fail(
            f"{label} exited {completed.returncode}, expected "
            f"{expected_exit}: {detail}"
        )
    return completed


def _dpkg_package_version(package_name: str) -> str:
    completed = run_tool(
        [
            "/usr/bin/dpkg-query",
            "-W",
            "-f=${db:Status-Status}\\n${Version}\\n",
            package_name,
        ],
        label=f"{package_name} package version",
    )
    lines = _bounded_output(
        completed.stdout, f"{package_name} package version"
    ).splitlines()
    if len(lines) != 2 or lines[0] != "installed" or not lines[1]:
        fail(f"{package_name} is not an exact installed package")
    return lines[1]


def tool_record(tool: Tool) -> dict[str, Any]:
    held = snapshot(tool.path, f"{tool.name} binary", 64 * 1024 * 1024)
    version = _dpkg_package_version(tool.package_name)
    if (
        tool.expected_package_version
        and version != tool.expected_package_version
    ):
        fail(
            f"{tool.package_name} package version {version!r} differs from "
            f"the pinned {tool.expected_package_version!r}"
        )
    return {
        "binarySha256": held.sha256,
        "packageName": tool.package_name,
        "packageVersion": version,
    }


def collect_tool_records() -> dict[str, Any]:
    if Path("/usr/bin/ar").is_symlink() or not Path("/usr/bin/ar").is_file():
        fail("the system ar implementation is unavailable")
    return {name: tool_record(tool) for name, tool in TOOLS.items()}


def _source(
    *,
    repository: str,
    workflow: str,
    run_id: str,
    run_attempt: str,
    ref: str,
    sha: str,
    actor: str,
) -> dict[str, str]:
    if repository != REPOSITORY or workflow != WORKFLOW or ref != REF:
        fail("signing source is not the governed main-branch workflow")
    require_positive_integer_text(run_id, "source runId")
    require_positive_integer_text(run_attempt, "source runAttempt")
    require_text(sha, "source sha", COMMIT_RE)
    require_text(actor, "source actor", LOGIN_RE)
    return {
        "actor": actor,
        "environment": ENVIRONMENT,
        "ref": ref,
        "repository": repository,
        "runAttempt": run_attempt,
        "runId": run_id,
        "sha": sha,
        "workflow": workflow,
    }


def _validate_source(value: Any, label: str) -> dict[str, str]:
    source = exact_dict(
        value,
        {
            "actor",
            "environment",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        label,
    )
    expected = _source(
        repository=source["repository"],
        workflow=source["workflow"],
        run_id=source["runId"],
        run_attempt=source["runAttempt"],
        ref=source["ref"],
        sha=source["sha"],
        actor=source["actor"],
    )
    if source != expected:
        fail(f"{label} differs from governed source authority")
    return expected


def _artifact_binding(value: Any, label: str) -> dict[str, Any]:
    row = exact_dict(
        value, {"fileName", "memberPath", "sha256", "sizeBytes"}, label
    )
    if row["fileName"] != ARTIFACT_FILE_NAME:
        fail(f"{label}.fileName is not the governed Linux artifact")
    member = safe_member(row["memberPath"], f"{label}.memberPath")
    if PurePosixPath(member).name != ARTIFACT_FILE_NAME:
        fail(f"{label}.memberPath basename differs")
    require_sha256(row["sha256"], f"{label}.sha256")
    require_positive_integer(row["sizeBytes"], f"{label}.sizeBytes")
    return row


def _file_binding(value: Any, label: str) -> dict[str, Any]:
    row = exact_dict(
        value, {"memberPath", "sha256", "sizeBytes"}, label
    )
    safe_member(row["memberPath"], f"{label}.memberPath")
    require_sha256(row["sha256"], f"{label}.sha256")
    require_positive_integer(row["sizeBytes"], f"{label}.sizeBytes")
    return row


def validate_unsigned_export_receipt(
    payload: Any,
    *,
    unsigned: Snapshot,
    release_version: str,
    artifact_member_path: str,
    expected_source: Mapping[str, str],
) -> dict[str, Any]:
    receipt = exact_dict(
        payload,
        {
            "artifact",
            "contractName",
            "contractVersion",
            "generatedAt",
            "livePredecessorAuthority",
            "nonPublishing",
            "package",
            "releaseVersion",
            "source",
            "status",
        },
        "unsigned export receipt",
    )
    if (
        receipt["contractName"] != EXPORT_CONTRACT
        or receipt["contractVersion"] != 2
        or receipt["status"] != "exported"
        or receipt["nonPublishing"] is not True
        or receipt["releaseVersion"] != release_version
    ):
        fail("unsigned export receipt contract or release identity is invalid")
    artifact = _artifact_binding(
        receipt["artifact"], "unsigned export receipt artifact"
    )
    if (
        artifact["memberPath"] != artifact_member_path
        or artifact["sha256"] != unsigned.sha256
        or artifact["sizeBytes"] != unsigned.size_bytes
    ):
        fail("unsigned export receipt does not bind the exact unsigned package")
    package = exact_dict(
        receipt["package"],
        {"architecture", "name", "version"},
        "unsigned export receipt package",
    )
    if package["architecture"] != "amd64" or package["name"] != "chummer6-avalonia":
        fail("unsigned export receipt Debian package identity is invalid")
    require_text(package["version"], "unsigned export receipt package version")
    authority = exact_dict(
        receipt["livePredecessorAuthority"],
        {
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
        },
        "unsigned export live-predecessor authority",
    )
    for key, value in authority.items():
        require_sha256(value, f"unsigned export live-predecessor {key}")
    raw_source = exact_dict(
        receipt["source"],
        {"actor", "ref", "repository", "runAttempt", "runId", "sha", "workflow"},
        "unsigned export source",
    )
    comparable = dict(expected_source)
    comparable.pop("environment")
    if raw_source != comparable:
        fail("unsigned export source differs from protected signing source")
    require_text(receipt["generatedAt"], "unsigned export generatedAt", ZULU_RE)
    return receipt


def policy_bytes(signing_fingerprint: str, keyring_file_name: str) -> bytes:
    require_text(
        signing_fingerprint, "policy signing fingerprint", FINGERPRINT_RE
    )
    if keyring_file_name != KEYRING_FILE_NAME:
        fail("policy keyring filename is not canonical")
    return (
        '<?xml version="1.0"?>\n'
        '<!DOCTYPE Policy SYSTEM "https://www.debian.org/debsig/1.0/policy.dtd">\n'
        '<Policy xmlns="https://www.debian.org/debsig/1.0/">\n'
        f'  <Origin Name="Chummer6" id="{signing_fingerprint}" '
        'Description="Chummer6 governed Linux release origin"/>\n'
        "  <Selection>\n"
        f'    <Required Type="origin" File="{keyring_file_name}" '
        f'id="{signing_fingerprint}"/>\n'
        "  </Selection>\n"
        '  <Verification MinOptional="0">\n'
        f'    <Required Type="origin" File="{keyring_file_name}" '
        f'id="{signing_fingerprint}"/>\n'
        "  </Verification>\n"
        "</Policy>\n"
    ).encode("utf-8")


def _gpg_environment(home: Path) -> dict[str, str]:
    return {
        "GNUPGHOME": str(home),
        "HOME": str(home),
        "LANG": "C.UTF-8",
        "LC_ALL": "C.UTF-8",
        "PATH": "/usr/bin:/bin",
    }


def _parse_key_inventory(
    output: bytes, *, secret: bool
) -> tuple[str, set[str], dict[str, str], list[dict[str, str]]]:
    text = _bounded_output(output, "GnuPG fingerprint listing")
    primary_tag = "sec" if secret else "pub"
    secondary_tag = "ssb" if secret else "sub"
    pending: tuple[str, dict[str, str]] | None = None
    primaries: list[str] = []
    fingerprints: set[str] = set()
    primary_record: dict[str, str] | None = None
    secondary_records: list[dict[str, str]] = []
    for line in text.splitlines():
        fields = line.split(":")
        if len(fields) >= 12 and fields[0] in {primary_tag, secondary_tag}:
            pending = (
                fields[0],
                {
                    "algorithm": fields[3],
                    "bits": fields[2],
                    "capabilities": fields[11],
                    "expires": fields[6],
                    "keyId": fields[4],
                    "validity": fields[1],
                },
            )
            continue
        if len(fields) >= 10 and fields[0] == "fpr" and pending is not None:
            fingerprint = fields[9]
            require_text(
                fingerprint, "GnuPG listed fingerprint", FINGERPRINT_RE
            )
            fingerprints.add(fingerprint)
            tag, record = pending
            record["fingerprint"] = fingerprint
            if tag == primary_tag:
                primaries.append(fingerprint)
                primary_record = record
            else:
                secondary_records.append(record)
            pending = None
    if len(primaries) != 1 or not fingerprints or primary_record is None:
        fail("OpenPGP material must contain exactly one primary key")
    return primaries[0], fingerprints, primary_record, secondary_records


def _require_usable_primary_key(
    primary: Mapping[str, str],
    secondaries: Sequence[Mapping[str, str]],
    expected_fingerprint: str,
) -> None:
    if primary.get("fingerprint") != expected_fingerprint:
        fail("expected full fingerprint is not the OpenPGP primary key")
    if primary.get("validity") in {"d", "e", "i", "n", "q", "r"}:
        fail("OpenPGP primary signing key is disabled, expired, or revoked")
    if "s" not in str(primary.get("capabilities", "")):
        fail("OpenPGP primary key is not usable for signing")
    try:
        algorithm = int(str(primary.get("algorithm", "")), 10)
        bits = int(str(primary.get("bits", "")), 10)
    except ValueError:
        fail("OpenPGP primary key algorithm or strength is malformed")
    if not (
        (algorithm in {1, 2, 3} and bits >= 3072)
        or (algorithm == 22 and bits >= 255)
    ):
        fail(
            "OpenPGP primary key must be RSA-3072+ or Ed25519-class"
        )
    expires = str(primary.get("expires", ""))
    if expires:
        try:
            expires_at = int(expires, 10)
        except ValueError:
            fail("OpenPGP primary key expiry is malformed")
        if expires_at <= int(current_time().timestamp()):
            fail("OpenPGP primary signing key has expired")
    if any(
        "s" in str(record.get("capabilities", ""))
        and record.get("validity") not in {"d", "e", "i", "n", "q", "r"}
        for record in secondaries
    ):
        fail(
            "OpenPGP key contains a usable signing subkey; the governed lane "
            "requires one unambiguous primary signing key"
        )


def inspect_keyring(
    keyring: Path, expected_primary: str, expected_signing: str
) -> None:
    with tempfile.TemporaryDirectory(prefix="chummer-keyring-inspect-") as raw:
        home = Path(raw)
        home.chmod(0o700)
        environment = _gpg_environment(home)
        completed = run_tool(
            [
                str(TOOLS["gpg"].path),
                "--batch",
                "--no-default-keyring",
                "--keyring",
                str(Path(os.path.abspath(keyring))),
                "--with-colons",
                "--fingerprint",
                "--fingerprint",
                "--list-keys",
            ],
            label="public keyring inspection",
            environment=environment,
        )
        (
            primary,
            fingerprints,
            primary_record,
            secondary_records,
        ) = _parse_key_inventory(completed.stdout, secret=False)
        if primary != expected_primary or expected_signing not in fingerprints:
            fail("public keyring fingerprints differ from the signing receipt")
        _require_usable_primary_key(
            primary_record, secondary_records, expected_primary
        )


def _ar_members(path: Path) -> list[tuple[str, int, int]]:
    absolute = Path(os.path.abspath(path))
    try:
        before = absolute.lstat()
    except OSError as exc:
        fail(f"Debian archive cannot be inspected: {exc}")
    if (
        stat.S_ISLNK(before.st_mode)
        or not stat.S_ISREG(before.st_mode)
        or before.st_nlink != 1
        or before.st_size < 1
        or before.st_size > MAX_PACKAGE_BYTES
    ):
        fail("Debian archive must be one bounded, non-linked regular file")
    descriptor = os.open(
        absolute,
        os.O_RDONLY
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_NOFOLLOW", 0)),
    )
    try:
        opened = os.fstat(descriptor)
        if (
            opened.st_dev,
            opened.st_ino,
            opened.st_size,
        ) != (
            before.st_dev,
            before.st_ino,
            before.st_size,
        ):
            fail("Debian archive changed before parsing")
        if os.pread(descriptor, 8, 0) != b"!<arch>\n":
            fail("candidate is not a Debian ar archive")
        offset = 8
        members: list[tuple[str, int, int]] = []
        while offset < opened.st_size:
            header = os.pread(descriptor, 60, offset)
            if len(header) != 60:
                fail("candidate ar archive has a truncated header")
            if header[58:60] != b"`\n":
                fail("candidate ar archive header is malformed")
            try:
                name = header[:16].decode("ascii").strip()
                size = int(header[48:58].decode("ascii").strip(), 10)
            except (UnicodeDecodeError, ValueError):
                fail("candidate ar archive metadata is malformed")
            if name.endswith("/"):
                name = name[:-1]
            start = offset + 60
            end = start + size
            if not name or size < 1 or end > opened.st_size:
                fail("candidate ar archive member is invalid")
            members.append((name, start, size))
            offset = end + (size % 2)
        if offset != opened.st_size:
            fail("candidate ar archive has trailing or truncated bytes")
        after = os.fstat(descriptor)
        if (
            opened.st_dev,
            opened.st_ino,
            opened.st_size,
            opened.st_mtime_ns,
            opened.st_ctime_ns,
        ) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
            after.st_ctime_ns,
        ):
            fail("Debian archive changed while parsing")
        return members
    finally:
        os.close(descriptor)


def _require_xz_stream_integrity(
    descriptor: int,
    *,
    start: int,
    compressed_size: int,
    maximum_output_bytes: int,
    label: str,
) -> int:
    try:
        decompressor = lzma.LZMADecompressor(
            format=lzma.FORMAT_XZ,
            memlimit=MAX_XZ_MEMORY_BYTES,
        )
    except lzma.LZMAError as exc:
        fail(f"{label} XZ decompressor could not initialize: {exc}")
    compressed_offset = 0
    output_size = 0
    while True:
        if decompressor.needs_input:
            if compressed_offset >= compressed_size:
                if not decompressor.eof:
                    fail(f"{label} XZ stream is truncated")
                break
            data = os.pread(
                descriptor,
                min(
                    1024 * 1024,
                    compressed_size - compressed_offset,
                ),
                start + compressed_offset,
            )
            if not data:
                fail(f"{label} XZ member ended before its declared size")
            compressed_offset += len(data)
        else:
            data = b""
        try:
            output = decompressor.decompress(
                data, max_length=XZ_OUTPUT_CHUNK_BYTES
            )
        except lzma.LZMAError as exc:
            fail(f"{label} XZ integrity validation failed: {exc}")
        output_size += len(output)
        if output_size > maximum_output_bytes:
            fail(f"{label} decompressed stream exceeds its fixed byte bound")
        if decompressor.eof:
            if (
                decompressor.unused_data
                or compressed_offset != compressed_size
            ):
                fail(f"{label} XZ stream has trailing or concatenated bytes")
            break
        if not output and not data and not decompressor.needs_input:
            fail(f"{label} XZ decompression made no forward progress")
    if output_size < 1:
        fail(f"{label} XZ stream decompressed to an empty payload")
    return output_size


def require_xz_member_integrity(
    package: Snapshot, *, signed: bool = False
) -> dict[str, int]:
    if package.identity is None:
        fail("unsigned Debian package lacks a held file identity")
    members = _ar_members(package.path)
    by_name = {name: (start, size) for name, start, size in members}
    expected_members = [
        DEBIAN_BINARY_MEMBER,
        CONTROL_MEMBER,
        DATA_MEMBER,
    ]
    if signed:
        expected_members.append(ORIGIN_SIGNATURE_MEMBER)
    if list(by_name) != expected_members:
        fail(
            f"{'signed' if signed else 'unsigned'} Debian package XZ "
            "member layout is not canonical"
        )
    descriptor = -1
    try:
        descriptor = os.open(
            package.path,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        before = _file_identity(os.fstat(descriptor))
        if not _same_identity(before, package.identity):
            fail("unsigned Debian package changed before XZ validation")
        control_start, control_size = by_name[CONTROL_MEMBER]
        data_start, data_size = by_name[DATA_MEMBER]
        result = {
            CONTROL_MEMBER: _require_xz_stream_integrity(
                descriptor,
                start=control_start,
                compressed_size=control_size,
                maximum_output_bytes=MAX_CONTROL_STREAM_BYTES,
                label="Debian control member",
            ),
            DATA_MEMBER: _require_xz_stream_integrity(
                descriptor,
                start=data_start,
                compressed_size=data_size,
                maximum_output_bytes=MAX_DATA_STREAM_BYTES,
                label="Debian data member",
            ),
        }
        after = _file_identity(os.fstat(descriptor))
        linked = _file_identity(package.path.lstat())
    except ContractError:
        raise
    except OSError as exc:
        fail(f"unsigned Debian package XZ validation failed safely: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if (
        not _same_identity(before, after)
        or not _same_identity(before, linked)
    ):
        fail("unsigned Debian package changed during XZ validation")
    return result


def _write_ar_member_to(
    source_descriptor: int,
    start: int,
    size: int,
    destination_descriptor: int,
) -> None:
    offset = 0
    while offset < size:
        chunk = os.pread(
            source_descriptor, min(1024 * 1024, size - offset), start + offset
        )
        if not chunk:
            fail("Debian archive member ended before its declared size")
        _write_all(destination_descriptor, chunk)
        offset += len(chunk)


def extract_signed_payload_and_signature(
    path: Path, payload_path: Path, signature_path: Path
) -> None:
    held_source = snapshot(
        path, "signed Debian package extraction source", MAX_PACKAGE_BYTES
    )
    members = _ar_members(path)
    after_members = snapshot(
        path,
        "signed Debian package after archive parsing",
        MAX_PACKAGE_BYTES,
    )
    if (
        after_members.sha256 != held_source.sha256
        or after_members.size_bytes != held_source.size_bytes
    ):
        fail("signed Debian package changed before member extraction")
    by_name = {name: (start, size) for name, start, size in members}
    expected = [
        DEBIAN_BINARY_MEMBER,
        CONTROL_MEMBER,
        DATA_MEMBER,
        ORIGIN_SIGNATURE_MEMBER,
    ]
    if [name for name, _, _ in members] != expected:
        fail(
            "signed Debian package must use the exact xz member layout "
            "supported by the pinned debsigs implementation"
        )
    source = os.open(
        Path(os.path.abspath(path)),
        os.O_RDONLY
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_NOFOLLOW", 0)),
    )
    (
        payload_absolute,
        payload_parent,
        payload_name,
        payload_parent_identity,
    ) = _secure_parent_descriptor(payload_path)
    (
        signature_absolute,
        signature_parent,
        signature_name,
        signature_parent_identity,
    ) = _secure_parent_descriptor(signature_path)
    payload_descriptor = -1
    signature_descriptor = -1
    try:
        source_identity = _file_identity(os.fstat(source))
        if (
            held_source.identity is None
            or not _same_identity(source_identity, held_source.identity)
        ):
            fail("signed Debian package changed before member extraction")
        payload_descriptor = os.open(
            payload_name,
            os.O_RDWR
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
            dir_fd=payload_parent,
        )
        for name in (
            DEBIAN_BINARY_MEMBER,
            CONTROL_MEMBER,
            DATA_MEMBER,
        ):
            start, size = by_name[name]
            _write_ar_member_to(
                source, start, size, payload_descriptor
            )
        os.fsync(payload_descriptor)
        signature_descriptor = os.open(
            signature_name,
            os.O_RDWR
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
            dir_fd=signature_parent,
        )
        signature_start, signature_size = by_name[ORIGIN_SIGNATURE_MEMBER]
        if signature_size > MAX_KEY_BYTES:
            fail("OpenPGP origin signature exceeds its fixed byte bound")
        _write_ar_member_to(
            source,
            signature_start,
            signature_size,
            signature_descriptor,
        )
        os.fsync(signature_descriptor)
        source_after = _file_identity(os.fstat(source))
        try:
            source_link = _file_identity(held_source.path.lstat())
        except OSError as exc:
            fail(f"signed Debian package link could not be rechecked: {exc}")
        if (
            not _same_identity(source_identity, source_after)
            or not _same_identity(source_identity, source_link)
        ):
            fail("signed Debian package changed during member extraction")
        _finalize_private_output(
            absolute=payload_absolute,
            parent_descriptor=payload_parent,
            parent_identity=payload_parent_identity,
            basename=payload_name,
            output_descriptor=payload_descriptor,
            label="OpenPGP signed Debian payload",
            maximum_bytes=MAX_PACKAGE_BYTES,
        )
        _finalize_private_output(
            absolute=signature_absolute,
            parent_descriptor=signature_parent,
            parent_identity=signature_parent_identity,
            basename=signature_name,
            output_descriptor=signature_descriptor,
            label="OpenPGP origin signature packet",
            maximum_bytes=MAX_KEY_BYTES,
        )
    finally:
        for descriptor in (
            payload_descriptor,
            signature_descriptor,
            source,
            payload_parent,
            signature_parent,
        ):
            if descriptor >= 0:
                os.close(descriptor)


def require_unsigned_deb(path: Path) -> None:
    members = _ar_members(path)
    names = [name for name, _, _ in members]
    if names != [DEBIAN_BINARY_MEMBER, CONTROL_MEMBER, DATA_MEMBER]:
        fail(
            "unsigned package must contain only canonical xz Debian members; "
            "the pinned debsigs implementation cannot sign zstd members"
        )
    _, start, size = next(
        row for row in members if row[0] == DEBIAN_BINARY_MEMBER
    )
    descriptor = os.open(
        Path(os.path.abspath(path)),
        os.O_RDONLY
        | int(getattr(os, "O_CLOEXEC", 0))
        | int(getattr(os, "O_NOFOLLOW", 0)),
    )
    try:
        debian_binary = os.pread(descriptor, size, start)
    finally:
        os.close(descriptor)
    if debian_binary != b"2.0\n":
        fail("unsigned package debian-binary member is not exact format 2.0")


def normalize_debian_version(release_version: str) -> str:
    normalized = re.sub(
        r"[^0-9A-Za-z.+~:-]+", "-", release_version.strip()
    ).strip(".-:+~") or "0~local"
    if not normalized[0].isdigit():
        normalized = f"0~{normalized}"
    return normalized


def validate_debian_metadata(
    path: Path,
    package: Mapping[str, Any],
    release_version: str,
) -> None:
    expected = {
        "Architecture": "amd64",
        "Package": "chummer6-avalonia",
        "Version": normalize_debian_version(release_version),
    }
    if package != {
        "architecture": expected["Architecture"],
        "name": expected["Package"],
        "version": expected["Version"],
    }:
        fail(
            "unsigned export package metadata is not derived from the "
            "governed release version"
        )
    environment = {
        "HOME": "/tmp",
        "LANG": "C.UTF-8",
        "LC_ALL": "C.UTF-8",
        "PATH": "/usr/bin:/bin",
    }
    run_tool(
        ["/usr/bin/dpkg-deb", "--info", str(Path(os.path.abspath(path)))],
        label="Debian package integrity inspection",
        environment=environment,
    )
    for field, expected_value in expected.items():
        completed = run_tool(
            [
                "/usr/bin/dpkg-deb",
                "-f",
                str(Path(os.path.abspath(path))),
                field,
            ],
            label=f"Debian package {field} inspection",
            environment=environment,
        )
        actual = _bounded_output(
            completed.stdout, f"Debian package {field}"
        ).strip()
        if actual != expected_value:
            fail(
                f"Debian package {field} differs from the governed "
                "unsigned export"
            )


def require_one_origin_signature(path: Path) -> None:
    names = [name for name, _, _ in _ar_members(path)]
    if names != [
        DEBIAN_BINARY_MEMBER,
        CONTROL_MEMBER,
        DATA_MEMBER,
        ORIGIN_SIGNATURE_MEMBER,
    ]:
        fail("signed package must contain exactly one canonical origin signature")


def require_signed_prefix_matches_unsigned(
    unsigned: Snapshot, signed: Snapshot
) -> None:
    if unsigned.identity is None or signed.identity is None:
        fail("signed-prefix validation requires held package identities")
    if signed.size_bytes <= unsigned.size_bytes:
        fail("signed package is not larger than its unsigned source")
    unsigned_descriptor = -1
    signed_descriptor = -1
    try:
        flags = (
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0))
        )
        unsigned_descriptor = os.open(unsigned.path, flags)
        signed_descriptor = os.open(signed.path, flags)
        unsigned_before = _file_identity(os.fstat(unsigned_descriptor))
        signed_before = _file_identity(os.fstat(signed_descriptor))
        if (
            not _same_identity(unsigned_before, unsigned.identity)
            or not _same_identity(signed_before, signed.identity)
        ):
            fail("package identity changed before signed-prefix validation")
        offset = 0
        while offset < unsigned.size_bytes:
            length = min(
                1024 * 1024, unsigned.size_bytes - offset
            )
            unsigned_chunk = os.pread(
                unsigned_descriptor, length, offset
            )
            signed_chunk = os.pread(signed_descriptor, length, offset)
            if (
                len(unsigned_chunk) != length
                or signed_chunk != unsigned_chunk
            ):
                fail(
                    "signed package does not preserve the exact authenticated "
                    "unsigned package prefix"
                )
            offset += length
        unsigned_after = _file_identity(os.fstat(unsigned_descriptor))
        signed_after = _file_identity(os.fstat(signed_descriptor))
        unsigned_link = _file_identity(unsigned.path.lstat())
        signed_link = _file_identity(signed.path.lstat())
    except ContractError:
        raise
    except OSError as exc:
        fail(f"signed-prefix validation failed safely: {exc}")
    finally:
        if unsigned_descriptor >= 0:
            os.close(unsigned_descriptor)
        if signed_descriptor >= 0:
            os.close(signed_descriptor)
    if (
        not _same_identity(unsigned_before, unsigned_after)
        or not _same_identity(unsigned_before, unsigned_link)
        or not _same_identity(signed_before, signed_after)
        or not _same_identity(signed_before, signed_link)
    ):
        fail("package changed during signed-prefix validation")


def tampered_copy(source: Path, destination: Path) -> Snapshot:
    held = snapshot(
        source, "signed package for tamper check", MAX_PACKAGE_BYTES
    )
    data_members = [
        (start, size)
        for name, start, size in _ar_members(source)
        if name == DATA_MEMBER
    ]
    if len(data_members) != 1:
        fail("signed package does not contain one data member")
    start, size = data_members[0]
    mutation_index = start + (size // 2)
    (
        absolute,
        parent_descriptor,
        basename,
        parent_identity,
    ) = _secure_parent_descriptor(destination)
    source_descriptor = -1
    target_descriptor = -1
    offset = 0
    mutated = False
    result: Snapshot | None = None
    try:
        source_descriptor = os.open(
            held.path,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        source_before = _file_identity(os.fstat(source_descriptor))
        if (
            held.identity is None
            or not _same_identity(source_before, held.identity)
        ):
            fail("signed package changed before tamper copy")
        target_descriptor = os.open(
            basename,
            os.O_RDWR
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
            dir_fd=parent_descriptor,
        )
        while True:
            chunk = os.read(source_descriptor, 1024 * 1024)
            if not chunk:
                break
            mutable = bytearray(chunk)
            if offset <= mutation_index < offset + len(mutable):
                mutable[mutation_index - offset] ^= 0x01
                mutated = True
            _write_all(target_descriptor, bytes(mutable))
            offset += len(mutable)
            if offset > held.size_bytes:
                fail("tampered package copy exceeded its exact input size")
        os.fsync(target_descriptor)
        source_after = _file_identity(os.fstat(source_descriptor))
        try:
            source_link = _file_identity(held.path.lstat())
        except OSError as exc:
            fail(f"signed package link could not be rechecked: {exc}")
        if (
            not _same_identity(source_before, source_after)
            or not _same_identity(source_before, source_link)
        ):
            fail("signed package changed during tamper copy")
        if not mutated or offset != held.size_bytes:
            fail(
                "tamper mutation did not affect exactly one authenticated byte"
            )
        result = _finalize_private_output(
            absolute=absolute,
            parent_descriptor=parent_descriptor,
            parent_identity=parent_identity,
            basename=basename,
            output_descriptor=target_descriptor,
            label="tampered signature-negative package",
            maximum_bytes=MAX_PACKAGE_BYTES,
        )
    finally:
        for descriptor in (
            source_descriptor,
            target_descriptor,
            parent_descriptor,
        ):
            if descriptor >= 0:
                os.close(descriptor)
    assert result is not None
    if result.sha256 == held.sha256 or result.size_bytes != held.size_bytes:
        fail("tampered package does not preserve exact size with changed bytes")
    return result


def verification_layout(
    policy: Path,
    keyring: Path,
    signing_fingerprint: str,
) -> tuple[Path, Path]:
    long_key_id = signing_fingerprint[-16:]
    policy = Path(os.path.abspath(policy))
    keyring = Path(os.path.abspath(keyring))
    if (
        policy.name != POLICY_FILE_NAME
        or keyring.name != KEYRING_FILE_NAME
        or policy.parent.name != long_key_id
        or keyring.parent.name != long_key_id
        or policy.parent.parent.name != "policies"
        or keyring.parent.parent.name != "keyrings"
        or policy.parent.parent.parent != keyring.parent.parent.parent
    ):
        fail(
            "policy and keyring paths must use signing/"
            "{policies,keyrings}/<long-key-id>/ canonical layout"
        )
    return policy.parent.parent, keyring.parent.parent


def verify_openpgp_signature(
    *,
    package: Path,
    keyring: Path,
    expected_fingerprint: str,
    temporary_root: Path | None = None,
) -> dict[str, Any]:
    with tempfile.TemporaryDirectory(
        prefix="chummer-gpgv-",
        dir=str(temporary_root) if temporary_root is not None else None,
    ) as raw:
        root = Path(raw)
        root.chmod(0o700)
        payload_path = root / "signed-payload.bin"
        signature_path = root / "origin-signature.pgp"
        extract_signed_payload_and_signature(
            package, payload_path, signature_path
        )
        completed = run_tool(
            [
                str(TOOLS["gpgv"].path),
                "--status-fd=1",
                "--keyring",
                str(Path(os.path.abspath(keyring))),
                str(signature_path),
                str(payload_path),
            ],
            label="independent OpenPGP payload verification",
            environment={
                "HOME": str(root),
                "LANG": "C.UTF-8",
                "LC_ALL": "C.UTF-8",
                "PATH": "/usr/bin:/bin",
            },
        )
    valid_rows = [
        line.removeprefix("[GNUPG:] VALIDSIG ").split()
        for line in _bounded_output(
            completed.stdout, "independent OpenPGP status"
        ).splitlines()
        if line.startswith("[GNUPG:] VALIDSIG ")
    ]
    if len(valid_rows) != 1 or len(valid_rows[0]) < 10:
        fail("independent OpenPGP verification did not emit one VALIDSIG")
    fields = valid_rows[0]
    fingerprint = require_text(
        fields[0], "OpenPGP VALIDSIG fingerprint", FINGERPRINT_RE
    )
    primary_fingerprint = require_text(
        fields[9], "OpenPGP VALIDSIG primary fingerprint", FINGERPRINT_RE
    )
    if (
        fingerprint != expected_fingerprint
        or primary_fingerprint != expected_fingerprint
    ):
        fail("OpenPGP signature was not made by the pinned primary key")
    try:
        created_timestamp = int(fields[2], 10)
        expires_timestamp = int(fields[3], 10)
        public_key_algorithm = int(fields[6], 10)
        hash_algorithm = int(fields[7], 10)
    except ValueError:
        fail("OpenPGP VALIDSIG timestamps or algorithm are malformed")
    now_timestamp = int(current_time().timestamp())
    if (
        created_timestamp < 1
        or created_timestamp > now_timestamp + 300
        or (expires_timestamp != 0 and expires_timestamp <= now_timestamp)
        or public_key_algorithm not in {1, 2, 3, 22}
        or hash_algorithm != 8
    ):
        fail(
            "OpenPGP signature is future-dated, expired, or not SHA-256"
        )
    created_at = (
        datetime.fromtimestamp(created_timestamp, UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )
    return {
        "createdAt": created_at,
        "creationTimestamp": created_timestamp,
        "fingerprint": fingerprint,
        "hashAlgorithm": "sha256",
        "primaryFingerprint": primary_fingerprint,
        "publicKeyAlgorithm": (
            "ed25519" if public_key_algorithm == 22 else "rsa"
        ),
    }


def verify_crypto(
    *,
    package: Path,
    policy: Path,
    keyring: Path,
    signing_fingerprint: str,
    temporary_root: Path | None = None,
) -> dict[str, Any]:
    policies_root, keyrings_root = verification_layout(
        policy, keyring, signing_fingerprint
    )
    environment = {
        "HOME": str(Path(os.path.abspath(temporary_root or policy.parent))),
        "LANG": "C.UTF-8",
        "LC_ALL": "C.UTF-8",
        "PATH": "/usr/bin:/bin",
    }
    positive = run_tool(
        [
            str(TOOLS["debsigVerify"].path),
            "--policies-dir",
            str(policies_root),
            "--keyrings-dir",
            str(keyrings_root),
            "--use-policy",
            policy.name,
            str(Path(os.path.abspath(package))),
        ],
        label="Debian origin signature verification",
        environment=environment,
    )
    if positive.returncode != 0:
        fail("Debian origin signature did not verify")
    openpgp = verify_openpgp_signature(
        package=package,
        keyring=keyring,
        expected_fingerprint=signing_fingerprint,
        temporary_root=temporary_root,
    )
    with tempfile.TemporaryDirectory(
        prefix="chummer-debsig-tamper-",
        dir=str(temporary_root) if temporary_root is not None else None,
    ) as raw:
        tampered = Path(raw) / ARTIFACT_FILE_NAME
        tampered_copy(package, tampered)
        negative = run_tool(
            [
                str(TOOLS["debsigVerify"].path),
                "--policies-dir",
                str(policies_root),
                "--keyrings-dir",
                str(keyrings_root),
                "--use-policy",
                policy.name,
                str(tampered),
            ],
            label="tampered Debian origin signature verification",
            environment=environment,
            expected_exit=None,
        )
    if negative.returncode != TAMPER_REJECTION_EXIT_CODE:
        fail(
            "tampered Debian package was not rejected as a bad signature "
            f"(exit={negative.returncode})"
        )
    return {
        "backend": VERIFY_BACKEND,
        "openPgpSignature": openpgp,
        "positiveExitCode": 0,
        "providerIndependent": True,
        "tamperNegative": {
            "expectedExitCode": TAMPER_REJECTION_EXIT_CODE,
            "mutation": "data-member-byte-flip",
            "observedExitCode": negative.returncode,
            "status": "rejected",
        },
    }


def _validate_tool_row(
    value: Any,
    label: str,
    *,
    expected_name: str,
    expected_version: str | None,
) -> dict[str, str]:
    row = exact_dict(
        value, {"binarySha256", "packageName", "packageVersion"}, label
    )
    require_sha256(row["binarySha256"], f"{label}.binarySha256")
    if row["packageName"] != expected_name:
        fail(f"{label}.packageName is invalid")
    require_text(row["packageVersion"], f"{label}.packageVersion")
    if expected_version is not None and row["packageVersion"] != expected_version:
        fail(f"{label}.packageVersion differs from the release pin")
    return row


def validate_signing_receipt(
    payload: Any,
    *,
    package: Snapshot,
    policy: Snapshot,
    keyring: Snapshot,
    release_version: str | None = None,
) -> dict[str, Any]:
    receipt = exact_dict(
        payload,
        {
            "app",
            "artifactSignatures",
            "artifacts",
            "contractName",
            "contractVersion",
            "digestAlgorithm",
            "generatedAt",
            "platform",
            "releaseChannel",
            "releaseVersion",
            "rid",
            "signer",
            "signingBackend",
            "signingStatus",
            "source",
            "tools",
            "verificationMaterial",
        },
        "Linux signing receipt",
    )
    expected_scalars = {
        "app": APP,
        "contractName": SIGNING_CONTRACT,
        "contractVersion": 2,
        "digestAlgorithm": "sha256",
        "platform": PLATFORM,
        "releaseChannel": "stable",
        "rid": RID,
        "signingBackend": SIGNING_BACKEND,
        "signingStatus": "pass",
    }
    for key, expected in expected_scalars.items():
        if type(receipt.get(key)) is not type(expected) or receipt.get(key) != expected:
            fail(f"Linux signing receipt {key} is invalid")
    generated_at = parse_zulu(
        receipt["generatedAt"], "Linux signing generatedAt"
    )
    now = current_time()
    if (
        generated_at > now.replace(microsecond=0)
        or (now - generated_at).total_seconds() > MAX_RECEIPT_AGE_SECONDS
    ):
        fail("Linux signing receipt is future-dated or stale")
    require_text(
        receipt["releaseVersion"], "Linux signing releaseVersion", PORTABLE_ID_RE
    )
    if release_version is not None and receipt["releaseVersion"] != release_version:
        fail("Linux signing receipt releaseVersion differs")
    signer = exact_dict(
        receipt["signer"],
        {"longKeyId", "primaryFingerprint", "signingFingerprint"},
        "Linux signing signer",
    )
    primary = require_text(
        signer["primaryFingerprint"], "signer primaryFingerprint", FINGERPRINT_RE
    )
    signing = require_text(
        signer["signingFingerprint"], "signer signingFingerprint", FINGERPRINT_RE
    )
    long_key_id = require_text(
        signer["longKeyId"], "signer longKeyId", LONG_KEY_ID_RE
    )
    if long_key_id != signing[-16:]:
        fail("signer longKeyId is not derived from the full signing fingerprint")
    if signing != primary:
        fail("Linux origin signatures must use the pinned primary key")
    _validate_source(receipt["source"], "Linux signing source")
    tools = exact_dict(
        receipt["tools"],
        {"debsigVerify", "debsigs", "gpg", "gpgv"},
        "Linux signing tools",
    )
    _validate_tool_row(
        tools["debsigs"],
        "Linux signing tools.debsigs",
        expected_name="debsigs",
        expected_version=EXPECTED_DEBSIGS_VERSION,
    )
    _validate_tool_row(
        tools["debsigVerify"],
        "Linux signing tools.debsigVerify",
        expected_name="debsig-verify",
        expected_version=EXPECTED_DEBSIG_VERIFY_VERSION,
    )
    _validate_tool_row(
        tools["gpg"],
        "Linux signing tools.gpg",
        expected_name="gpg",
        expected_version=None,
    )
    _validate_tool_row(
        tools["gpgv"],
        "Linux signing tools.gpgv",
        expected_name="gpgv",
        expected_version=None,
    )
    materials = exact_dict(
        receipt["verificationMaterial"],
        {"policy", "publicKeyring"},
        "Linux signing verificationMaterial",
    )
    policy_binding = _file_binding(
        materials["policy"], "Linux signing verification policy"
    )
    keyring_binding = _file_binding(
        materials["publicKeyring"], "Linux signing public keyring"
    )
    if (
        PurePosixPath(policy_binding["memberPath"]).parts
        != ("signing", "policies", long_key_id, POLICY_FILE_NAME)
        or policy_binding["sha256"] != policy.sha256
        or policy_binding["sizeBytes"] != policy.size_bytes
        or PurePosixPath(keyring_binding["memberPath"]).parts
        != ("signing", "keyrings", long_key_id, KEYRING_FILE_NAME)
        or keyring_binding["sha256"] != keyring.sha256
        or keyring_binding["sizeBytes"] != keyring.size_bytes
    ):
        fail("Linux signing verification material differs from exact bytes")
    artifacts = receipt["artifacts"]
    if not isinstance(artifacts, list) or len(artifacts) != 1:
        fail("Linux signing receipt must contain one artifact")
    artifact = exact_dict(
        artifacts[0],
        {"fileName", "kind", "sha256", "signingStatus"},
        "Linux signing artifact row",
    )
    if artifact != {
        "fileName": ARTIFACT_FILE_NAME,
        "kind": "installer",
        "sha256": package.sha256,
        "signingStatus": "pass",
    }:
        fail("Linux signing artifact row differs from the signed package")
    signatures = receipt["artifactSignatures"]
    if not isinstance(signatures, list) or len(signatures) != 1:
        fail("Linux signing receipt must contain one artifactSignature")
    signature = exact_dict(
        signatures[0],
        {
            "artifactFileName",
            "artifactSha256",
            "artifactSizeBytes",
            "cryptographicVerification",
            "digestAlgorithm",
            "signatureType",
            "signer",
            "verifier",
        },
        "Linux signing artifactSignature",
    )
    if (
        signature["artifactFileName"] != ARTIFACT_FILE_NAME
        or signature["artifactSha256"] != package.sha256
        or signature["artifactSizeBytes"] != package.size_bytes
        or signature["cryptographicVerification"] != "passed"
        or signature["digestAlgorithm"] != "sha256"
        or signature["signatureType"] != "origin"
        or signature["signer"] != signer
    ):
        fail("Linux signing artifactSignature identity is invalid")
    verifier = exact_dict(
        signature["verifier"],
        {
            "backend",
            "openPgpSignature",
            "policySha256",
            "positiveExitCode",
            "providerIndependent",
            "publicKeyringSha256",
            "tamperNegative",
        },
        "Linux signing artifactSignature verifier",
    )
    tamper = exact_dict(
        verifier["tamperNegative"],
        {
            "expectedExitCode",
            "mutation",
            "observedExitCode",
            "status",
        },
        "Linux signing tamperNegative",
    )
    openpgp = exact_dict(
        verifier["openPgpSignature"],
        {
            "createdAt",
            "creationTimestamp",
            "fingerprint",
            "hashAlgorithm",
            "primaryFingerprint",
            "publicKeyAlgorithm",
        },
        "Linux signing OpenPGP signature",
    )
    require_text(
        openpgp["createdAt"], "Linux signing OpenPGP createdAt", ZULU_RE
    )
    require_positive_integer(
        openpgp["creationTimestamp"],
        "Linux signing OpenPGP creationTimestamp",
    )
    expected_created_at = (
        datetime.fromtimestamp(openpgp["creationTimestamp"], UTC)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )
    signature_created_at = datetime.fromtimestamp(
        openpgp["creationTimestamp"], UTC
    ).replace(microsecond=0)
    if (
        openpgp["createdAt"] != expected_created_at
        or signature_created_at > generated_at
        or (generated_at - signature_created_at).total_seconds()
        > MAX_SIGNATURE_RECEIPT_SKEW_SECONDS
    ):
        fail(
            "Linux signing OpenPGP creation time is inconsistent with the "
            "governed receipt freshness window"
        )
    if (
        verifier["backend"] != VERIFY_BACKEND
        or verifier["policySha256"] != policy.sha256
        or verifier["publicKeyringSha256"] != keyring.sha256
        or verifier["positiveExitCode"] != 0
        or verifier["providerIndependent"] is not True
        or openpgp["fingerprint"] != signing
        or openpgp["primaryFingerprint"] != primary
        or openpgp["hashAlgorithm"] != "sha256"
        or openpgp["publicKeyAlgorithm"] not in {"rsa", "ed25519"}
        or tamper
        != {
            "expectedExitCode": TAMPER_REJECTION_EXIT_CODE,
            "mutation": "data-member-byte-flip",
            "observedExitCode": TAMPER_REJECTION_EXIT_CODE,
            "status": "rejected",
        }
    ):
        fail("Linux signing cryptographic verifier evidence is invalid")
    return {
        "generatedAt": receipt["generatedAt"],
        "releaseVersion": receipt["releaseVersion"],
        "signer": {
            "longKeyId": long_key_id,
            "primaryFingerprint": primary,
            "signingFingerprint": signing,
        },
        "source": receipt["source"],
        "tools": tools,
        "verificationMaterial": materials,
        "openPgpSignature": openpgp,
    }


def validate_signed_export_receipt(
    payload: Any,
    *,
    signed: Snapshot,
    signing_receipt: Snapshot,
    policy: Snapshot,
    keyring: Snapshot,
    signing_projection: Mapping[str, Any],
    release_version: str,
    unsigned_export: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    receipt = exact_dict(
        payload,
        {
            "artifact",
            "contractName",
            "contractVersion",
            "generatedAt",
            "livePredecessorAuthority",
            "nonPublishing",
            "package",
            "publicKeyring",
            "releaseVersion",
            "signingReceipt",
            "source",
            "status",
            "unsignedArtifact",
            "verificationPolicy",
        },
        "signed Linux export receipt",
    )
    if (
        receipt["contractName"] != EXPORT_CONTRACT
        or receipt["contractVersion"] != 3
        or receipt["status"] != "signed"
        or receipt["nonPublishing"] is not True
        or receipt["releaseVersion"] != release_version
        or receipt["generatedAt"] != signing_projection["generatedAt"]
    ):
        fail("signed Linux export contract or release identity is invalid")
    artifact = _artifact_binding(
        receipt["artifact"], "signed Linux export artifact"
    )
    if artifact != {
        "fileName": ARTIFACT_FILE_NAME,
        "memberPath": f"files/{ARTIFACT_FILE_NAME}",
        "sha256": signed.sha256,
        "sizeBytes": signed.size_bytes,
    }:
        fail("signed Linux export does not bind exact signed package bytes")
    signer = signing_projection["signer"]
    long_key_id = signer["longKeyId"]
    expected_bindings = {
        "signingReceipt": (
            f"signing/{SIGNING_RECEIPT_FILE_NAME}",
            signing_receipt,
        ),
        "verificationPolicy": (
            f"signing/policies/{long_key_id}/{POLICY_FILE_NAME}",
            policy,
        ),
        "publicKeyring": (
            f"signing/keyrings/{long_key_id}/{KEYRING_FILE_NAME}",
            keyring,
        ),
    }
    for key, (member_path, held) in expected_bindings.items():
        row = _file_binding(receipt[key], f"signed Linux export {key}")
        if row != {
            "memberPath": member_path,
            "sha256": held.sha256,
            "sizeBytes": held.size_bytes,
        }:
            fail(f"signed Linux export {key} binding differs")
    source = exact_dict(
        receipt["source"],
        {"actor", "ref", "repository", "runAttempt", "runId", "sha", "workflow"},
        "signed Linux export source",
    )
    expected_source = dict(signing_projection["source"])
    expected_source.pop("environment")
    if source != expected_source:
        fail("signed Linux export source differs from signing authority")
    package = exact_dict(
        receipt["package"],
        {"architecture", "name", "version"},
        "signed Linux export package",
    )
    if package != {
        "architecture": "amd64",
        "name": "chummer6-avalonia",
        "version": normalize_debian_version(release_version),
    }:
        fail("signed Linux export package identity is invalid")
    authority = exact_dict(
        receipt["livePredecessorAuthority"],
        {
            "liveReleaseChannelSha256",
            "nMinusOneReleaseSha256",
            "selectedTupleSha256",
        },
        "signed Linux export live-predecessor authority",
    )
    for key, value in authority.items():
        require_sha256(
            value, f"signed Linux export live-predecessor {key}"
        )
    unsigned = _artifact_binding(
        receipt["unsignedArtifact"], "signed Linux export unsignedArtifact"
    )
    if unsigned["sha256"] == signed.sha256:
        fail("signed Linux export unsigned and signed package digests collide")
    if unsigned_export is not None:
        if (
            unsigned != unsigned_export["artifact"]
            or package != unsigned_export["package"]
            or authority != unsigned_export["livePredecessorAuthority"]
        ):
            fail(
                "signed Linux export does not preserve exact unsigned "
                "export authority"
            )
    return receipt


def _decode_secret_environment(name: str, maximum: int, label: str) -> bytes:
    encoded = os.environ.get(name)
    if not encoded or len(encoded) > maximum * 2:
        fail(f"{label} environment value is missing or outside its bound")
    try:
        decoded = base64.b64decode(encoded, validate=True)
    except ValueError:
        fail(f"{label} environment value is not canonical base64")
    if not decoded or len(decoded) > maximum:
        fail(f"{label} decoded bytes are outside their fixed bound")
    return decoded


def _sign(args: argparse.Namespace) -> dict[str, Any]:
    expected_unsigned_sha256 = require_sha256(
        args.expected_unsigned_package_sha256,
        "authenticated unsigned package SHA-256",
    )
    expected_unsigned_size = int(
        require_positive_integer_text(
            args.expected_unsigned_package_size,
            "authenticated unsigned package size",
        )
    )
    expected_unsigned_receipt_sha256 = require_sha256(
        args.expected_unsigned_export_receipt_sha256,
        "authenticated unsigned export receipt SHA-256",
    )
    external_unsigned = snapshot(
        args.input_package,
        "external unsigned Debian package",
        MAX_PACKAGE_BYTES,
    )
    if external_unsigned.path.name != ARTIFACT_FILE_NAME:
        fail("unsigned Debian package filename is not canonical")
    if (
        external_unsigned.sha256 != expected_unsigned_sha256
        or external_unsigned.size_bytes != expected_unsigned_size
    ):
        fail("unsigned package differs from authenticated external authority")
    external_receipt = snapshot(
        args.unsigned_export_receipt,
        "external unsigned export receipt",
        MAX_JSON_BYTES,
    )
    if external_receipt.sha256 != expected_unsigned_receipt_sha256:
        fail(
            "unsigned export receipt differs from authenticated external "
            "authority"
        )
    temporary_parent = Path(
        os.path.abspath(os.environ.get("RUNNER_TEMP", tempfile.gettempdir()))
    )
    if not temporary_parent.is_dir() or temporary_parent.is_symlink():
        fail("ephemeral signing parent is not a real directory")
    _, temporary_parent_descriptor, _, _ = (
        _open_secure_parent_descriptor(
            temporary_parent / "input-stage", create_missing=False
        )
    )
    os.close(temporary_parent_descriptor)
    outputs: dict[str, Snapshot]
    result: dict[str, Any]
    with tempfile.TemporaryDirectory(
        prefix="chummer-linux-sign-input-", dir=temporary_parent
    ) as raw_stage:
        stage = Path(raw_stage)
        stage.chmod(0o700)
        staged_unsigned = copy_new(
            external_unsigned,
            stage / "files" / ARTIFACT_FILE_NAME,
            "private unsigned Debian package",
        )
        staged_receipt = copy_new(
            external_receipt,
            stage / "unsigned" / "LINUX_NATIVE_UNSIGNED_EXPORT.json",
            "private unsigned export receipt",
        )
        private_args = argparse.Namespace(**vars(args))
        private_args.input_package = staged_unsigned.path
        private_args.unsigned_export_receipt = staged_receipt.path
        result, outputs = _sign_private(private_args)
    require_unchanged_inputs(
        {
            "package": (
                external_unsigned,
                "external unsigned Debian package",
                MAX_PACKAGE_BYTES,
            ),
            "receipt": (
                external_receipt,
                "external unsigned export receipt",
                MAX_JSON_BYTES,
            ),
        }
    )
    sealed, transaction_manifest = commit_output_transaction(
        outputs={
            "package": (
                outputs["package"],
                "origin-signed Debian package",
                MAX_PACKAGE_BYTES,
            ),
            "policy": (
                outputs["policy"],
                "debsig verification policy",
                MAX_JSON_BYTES,
            ),
            "public_keyring": (
                outputs["public_keyring"],
                "public OpenPGP keyring",
                MAX_KEY_BYTES,
            ),
            "receipt": (
                outputs["receipt"],
                "Linux signing receipt",
                MAX_JSON_BYTES,
            ),
            "signed_export_receipt": (
                outputs["signed_export_receipt"],
                "signed Linux export receipt",
                MAX_JSON_BYTES,
            ),
        },
        manifest_path=args.transaction_manifest,
        members=_transaction_output_members(args),
    )
    result.update(
        {
            "artifactSha256": sealed["package"].sha256,
            "artifactSizeBytes": sealed["package"].size_bytes,
            "policySha256": sealed["policy"].sha256,
            "policySizeBytes": sealed["policy"].size_bytes,
            "publicKeyringSha256": sealed["publicKeyring"].sha256,
            "publicKeyringSizeBytes": sealed["publicKeyring"].size_bytes,
            "signedExportReceiptSha256": sealed[
                "signedExportReceipt"
            ].sha256,
            "signedExportReceiptSizeBytes": sealed[
                "signedExportReceipt"
            ].size_bytes,
            "signingReceiptSha256": sealed["signingReceipt"].sha256,
            "signingReceiptSizeBytes": sealed["signingReceipt"].size_bytes,
            "transactionManifestSha256": transaction_manifest.sha256,
            "transactionManifestSizeBytes": (
                transaction_manifest.size_bytes
            ),
        }
    )
    return result


def _sign_private(
    args: argparse.Namespace,
) -> tuple[dict[str, Any], dict[str, Snapshot]]:
    release_version = require_text(
        args.release_version, "release version", PORTABLE_ID_RE
    )
    fingerprint = require_text(
        args.expected_fingerprint,
        "expected signing fingerprint",
        FINGERPRINT_RE,
    )
    expected_keyring_sha256 = require_sha256(
        args.expected_public_keyring_sha256,
        "protected public keyring SHA-256",
    )
    expected_unsigned_sha256 = require_sha256(
        args.expected_unsigned_package_sha256,
        "authenticated unsigned package SHA-256",
    )
    expected_unsigned_size = int(
        require_positive_integer_text(
            args.expected_unsigned_package_size,
            "authenticated unsigned package size",
        )
    )
    expected_unsigned_receipt_sha256 = require_sha256(
        args.expected_unsigned_export_receipt_sha256,
        "authenticated unsigned export receipt SHA-256",
    )
    artifact_member = safe_member(
        args.artifact_member_path, "artifact member path"
    )
    if PurePosixPath(artifact_member).name != ARTIFACT_FILE_NAME:
        fail("artifact member path basename is invalid")
    signing_receipt_member = safe_member(
        args.signing_receipt_member_path, "signing receipt member path"
    )
    policy_member = safe_member(args.policy_member_path, "policy member path")
    keyring_member = safe_member(
        args.public_keyring_member_path, "public keyring member path"
    )
    if (
        PurePosixPath(signing_receipt_member).name
        != SIGNING_RECEIPT_FILE_NAME
        or PurePosixPath(policy_member).name != POLICY_FILE_NAME
        or PurePosixPath(keyring_member).name != KEYRING_FILE_NAME
    ):
        fail("signing material member filenames are not canonical")
    source = _source(
        repository=args.source_repository,
        workflow=args.source_workflow,
        run_id=args.source_run_id,
        run_attempt=args.source_run_attempt,
        ref=args.source_ref,
        sha=args.source_sha,
        actor=args.source_actor,
    )
    unsigned = snapshot(
        args.input_package, "unsigned Debian package", MAX_PACKAGE_BYTES
    )
    if unsigned.path.name != ARTIFACT_FILE_NAME:
        fail("unsigned Debian package filename is not canonical")
    if (
        unsigned.sha256 != expected_unsigned_sha256
        or unsigned.size_bytes != expected_unsigned_size
    ):
        fail("unsigned package differs from authenticated external authority")
    require_unsigned_deb(unsigned.path)
    require_xz_member_integrity(unsigned)
    unsigned_export, unsigned_export_snapshot = load_json(
        args.unsigned_export_receipt, "unsigned export receipt"
    )
    if unsigned_export_snapshot.sha256 != expected_unsigned_receipt_sha256:
        fail(
            "unsigned export receipt differs from authenticated external "
            "authority"
        )
    unsigned_export = validate_unsigned_export_receipt(
        unsigned_export,
        unsigned=unsigned,
        release_version=release_version,
        artifact_member_path=artifact_member,
        expected_source=source,
    )
    validate_debian_metadata(
        unsigned.path,
        unsigned_export["package"],
        release_version,
    )
    tools = collect_tool_records()
    private_key = _decode_secret_environment(
        PRIVATE_KEY_ENV, MAX_KEY_BYTES, "OpenPGP private key"
    )
    passphrase = _decode_secret_environment(
        PASSPHRASE_ENV, MAX_PASSPHRASE_BYTES, "OpenPGP passphrase"
    )
    if any(character in passphrase for character in (b"\x00", b"\r", b"\n")):
        fail("OpenPGP passphrase must be one nonempty line")
    temporary_parent = Path(
        os.path.abspath(os.environ.get("RUNNER_TEMP", tempfile.gettempdir()))
    )
    if not temporary_parent.is_dir() or temporary_parent.is_symlink():
        fail("ephemeral signing parent is not a real directory")
    with EphemeralGpgHome(temporary_parent) as home:
        passphrase_path = home / "passphrase"
        write_new_bytes(passphrase_path, passphrase, "OpenPGP passphrase")
        config = (
            "batch\n"
            "no-tty\n"
            "pinentry-mode loopback\n"
            "digest-algo SHA256\n"
            f"passphrase-file {passphrase_path}\n"
        ).encode("utf-8")
        write_new_bytes(home / "gpg.conf", config, "ephemeral GnuPG config")
        environment = _gpg_environment(home)
        run_tool(
            [
                str(TOOLS["gpg"].path),
                "--batch",
                "--import-options",
                "import-minimal",
                "--import",
            ],
            label="private OpenPGP key import",
            environment=environment,
            input_data=private_key,
        )
        listed = run_tool(
            [
                str(TOOLS["gpg"].path),
                "--batch",
                "--with-colons",
                "--fingerprint",
                "--fingerprint",
                "--list-secret-keys",
            ],
            label="private OpenPGP fingerprint inspection",
            environment=environment,
        )
        (
            primary_fingerprint,
            secret_fingerprints,
            primary_record,
            secondary_records,
        ) = _parse_key_inventory(listed.stdout, secret=True)
        if fingerprint not in secret_fingerprints:
            fail("expected full signing fingerprint is absent from the private key")
        _require_usable_primary_key(
            primary_record, secondary_records, fingerprint
        )
        keyring_path = Path(os.path.abspath(args.public_keyring))
        exported_public = run_tool(
            [
                str(TOOLS["gpg"].path),
                "--batch",
                "--export",
                primary_fingerprint,
            ],
            label="public OpenPGP key export",
            environment=environment,
            allow_binary_stdout=True,
        )
        if (
            not exported_public.stdout
            or len(exported_public.stdout) > MAX_KEY_BYTES
        ):
            fail("public OpenPGP key export is empty or exceeds its bound")
        keyring = write_new_bytes(
            keyring_path,
            exported_public.stdout,
            "public OpenPGP keyring",
        )
        if keyring.sha256 != expected_keyring_sha256:
            fail(
                "exported public keyring differs from protected signing "
                "authority"
            )
        inspect_keyring(keyring.path, primary_fingerprint, fingerprint)
        policy_path = Path(os.path.abspath(args.policy))
        policy = write_new_bytes(
            policy_path,
            policy_bytes(fingerprint, keyring_path.name),
            "debsig verification policy",
        )
        verification_layout(policy.path, keyring.path, fingerprint)
        signed = copy_new(
            unsigned, args.output_package, "origin-signed Debian package"
        )
        run_tool(
            [
                str(TOOLS["debsigs"].path),
                "--sign=origin",
                f"--default-key={fingerprint}!",
                str(signed.path),
            ],
            label="Debian origin signing",
            environment=environment,
        )
        signed_after = snapshot(
            signed.path, "origin-signed Debian package", MAX_PACKAGE_BYTES
        )
        signed = _bind_private_output_parent(
            signed_after,
            "origin-signed Debian package",
        )
        if (
            signed.sha256 == unsigned.sha256
            or signed.size_bytes <= unsigned.size_bytes
        ):
            fail("origin signing did not create distinct signed package bytes")
        require_one_origin_signature(signed.path)
        require_signed_prefix_matches_unsigned(unsigned, signed)
        crypto = verify_crypto(
            package=signed.path,
            policy=policy.path,
            keyring=keyring.path,
            signing_fingerprint=fingerprint,
            temporary_root=home,
        )
    generated_at = (
        current_time()
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )
    signer = {
        "longKeyId": fingerprint[-16:],
        "primaryFingerprint": primary_fingerprint,
        "signingFingerprint": fingerprint,
    }
    policy_binding = {
        "memberPath": policy_member,
        "sha256": policy.sha256,
        "sizeBytes": policy.size_bytes,
    }
    keyring_binding = {
        "memberPath": keyring_member,
        "sha256": keyring.sha256,
        "sizeBytes": keyring.size_bytes,
    }
    receipt_payload = {
        "app": APP,
        "artifactSignatures": [
            {
                "artifactFileName": ARTIFACT_FILE_NAME,
                "artifactSha256": signed.sha256,
                "artifactSizeBytes": signed.size_bytes,
                "cryptographicVerification": "passed",
                "digestAlgorithm": "sha256",
                "signatureType": "origin",
                "signer": signer,
                "verifier": {
                    **crypto,
                    "policySha256": policy.sha256,
                    "publicKeyringSha256": keyring.sha256,
                },
            }
        ],
        "artifacts": [
            {
                "fileName": ARTIFACT_FILE_NAME,
                "kind": "installer",
                "sha256": signed.sha256,
                "signingStatus": "pass",
            }
        ],
        "contractName": SIGNING_CONTRACT,
        "contractVersion": 2,
        "digestAlgorithm": "sha256",
        "generatedAt": generated_at,
        "platform": PLATFORM,
        "releaseChannel": "stable",
        "releaseVersion": release_version,
        "rid": RID,
        "signer": signer,
        "signingBackend": SIGNING_BACKEND,
        "signingStatus": "pass",
        "source": source,
        "tools": tools,
        "verificationMaterial": {
            "policy": policy_binding,
            "publicKeyring": keyring_binding,
        },
    }
    receipt = write_new_json(
        args.receipt, receipt_payload, "Linux signing receipt"
    )
    signing_projection = validate_signing_receipt(
        receipt_payload,
        package=signed,
        policy=policy,
        keyring=keyring,
        release_version=release_version,
    )
    signing_binding = {
        "memberPath": signing_receipt_member,
        "sha256": receipt.sha256,
        "sizeBytes": receipt.size_bytes,
    }
    signed_export_payload = {
        "artifact": {
            "fileName": ARTIFACT_FILE_NAME,
            "memberPath": artifact_member,
            "sha256": signed.sha256,
            "sizeBytes": signed.size_bytes,
        },
        "contractName": EXPORT_CONTRACT,
        "contractVersion": 3,
        "generatedAt": generated_at,
        "livePredecessorAuthority": unsigned_export[
            "livePredecessorAuthority"
        ],
        "nonPublishing": True,
        "package": unsigned_export["package"],
        "publicKeyring": keyring_binding,
        "releaseVersion": release_version,
        "signingReceipt": signing_binding,
        "source": {
            key: value for key, value in source.items() if key != "environment"
        },
        "status": "signed",
        "unsignedArtifact": unsigned_export["artifact"],
        "verificationPolicy": policy_binding,
    }
    signed_export = write_new_json(
        args.signed_export_receipt,
        signed_export_payload,
        "signed Linux export receipt",
    )
    validate_signed_export_receipt(
        signed_export_payload,
        signed=signed,
        signing_receipt=receipt,
        policy=policy,
        keyring=keyring,
        signing_projection=signing_projection,
        release_version=release_version,
        unsigned_export=unsigned_export,
    )
    result = {
        "artifactSha256": signed.sha256,
        "artifactSizeBytes": signed.size_bytes,
        "policySha256": policy.sha256,
        "policySizeBytes": policy.size_bytes,
        "publicKeyringSha256": keyring.sha256,
        "publicKeyringSizeBytes": keyring.size_bytes,
        "signedExportReceiptSha256": signed_export.sha256,
        "signedExportReceiptSizeBytes": signed_export.size_bytes,
        "signingFingerprint": fingerprint,
        "signingReceiptSha256": receipt.sha256,
        "signingReceiptSizeBytes": receipt.size_bytes,
    }
    return result, {
        "package": signed,
        "policy": policy,
        "public_keyring": keyring,
        "receipt": receipt,
        "signed_export_receipt": signed_export,
    }


def _verify_held(
    args: argparse.Namespace, package: Snapshot
) -> dict[str, Any]:
    require_one_origin_signature(package.path)
    require_xz_member_integrity(package, signed=True)
    policy = snapshot(
        args.policy, "debsig verification policy", MAX_JSON_BYTES
    )
    keyring = snapshot(
        args.public_keyring, "public OpenPGP keyring", MAX_KEY_BYTES
    )
    expected_fingerprint = require_text(
        args.expected_primary_fingerprint,
        "independently pinned primary fingerprint",
        FINGERPRINT_RE,
    )
    expected_keyring_sha256 = require_sha256(
        args.expected_public_keyring_sha256,
        "independently pinned public keyring SHA-256",
    )
    if keyring.sha256 != expected_keyring_sha256:
        fail("public keyring bytes differ from independent lifecycle authority")
    expected_transaction_sha256 = require_sha256(
        args.expected_transaction_manifest_sha256,
        "independently pinned transaction manifest SHA-256",
    )
    transaction_payload, transaction_manifest = load_json(
        args.transaction_manifest,
        "commit-last Linux signing transaction manifest",
    )
    if transaction_manifest.sha256 != expected_transaction_sha256:
        fail(
            "transaction manifest differs from independent lifecycle "
            "authority"
        )
    expected_signed_export_sha256 = require_sha256(
        args.expected_signed_export_receipt_sha256,
        "independently pinned signed export receipt SHA-256",
    )
    signed_export_payload, signed_export = load_json(
        args.signed_export_receipt, "signed Linux export receipt"
    )
    if signed_export.sha256 != expected_signed_export_sha256:
        fail(
            "signed export receipt differs from independent lifecycle "
            "authority"
        )
    receipt_payload, receipt = load_json(
        args.receipt, "Linux signing receipt"
    )
    projection = validate_signing_receipt(
        receipt_payload,
        package=package,
        policy=policy,
        keyring=keyring,
        release_version=args.release_version,
    )
    signer = projection["signer"]
    if signer["primaryFingerprint"] != expected_fingerprint:
        fail(
            "signing receipt fingerprint differs from independent "
            "lifecycle authority"
        )
    validate_signed_export_receipt(
        signed_export_payload,
        signed=package,
        signing_receipt=receipt,
        policy=policy,
        keyring=keyring,
        signing_projection=projection,
        release_version=args.release_version,
    )
    validate_transaction_manifest(
        transaction_payload,
        outputs={
            "package": package,
            "policy": policy,
            "publicKeyring": keyring,
            "signingReceipt": receipt,
            "signedExportReceipt": signed_export,
        },
        members={
            "package": signed_export_payload["artifact"]["memberPath"],
            "policy": signed_export_payload["verificationPolicy"][
                "memberPath"
            ],
            "publicKeyring": signed_export_payload["publicKeyring"][
                "memberPath"
            ],
            "signingReceipt": signed_export_payload["signingReceipt"][
                "memberPath"
            ],
            "signedExportReceipt": SIGNED_EXPORT_RECEIPT_FILE_NAME,
        },
    )
    validate_debian_metadata(
        package.path,
        signed_export_payload["package"],
        args.release_version,
    )
    expected_policy = policy_bytes(
        signer["signingFingerprint"], keyring.path.name
    )
    held_policy = snapshot(
        policy.path,
        "debsig verification policy",
        MAX_JSON_BYTES,
        read_data=True,
    )
    if held_policy.data != expected_policy:
        fail("debsig verification policy bytes are not canonical")
    inspect_keyring(
        keyring.path,
        signer["primaryFingerprint"],
        signer["signingFingerprint"],
    )
    live_tools = collect_tool_records()
    if live_tools != projection["tools"]:
        fail(
            "live verifier tool packages or binary hashes differ from the "
            "signing receipt"
        )
    verifier_tool = live_tools["debsigVerify"]
    crypto = verify_crypto(
        package=package.path,
        policy=policy.path,
        keyring=keyring.path,
        signing_fingerprint=signer["signingFingerprint"],
    )
    if crypto["openPgpSignature"] != projection["openPgpSignature"]:
        fail("live OpenPGP signature metadata differs from the signing receipt")
    return {
        "artifactSha256": package.sha256,
        "artifactSizeBytes": package.size_bytes,
        "policySha256": policy.sha256,
        "policySizeBytes": policy.size_bytes,
        "primaryFingerprint": signer["primaryFingerprint"],
        "publicKeyringSha256": keyring.sha256,
        "publicKeyringSizeBytes": keyring.size_bytes,
        "signingFingerprint": signer["signingFingerprint"],
        "signingReceiptSha256": receipt.sha256,
        "signingReceiptSizeBytes": receipt.size_bytes,
        "signedExportReceiptSha256": signed_export.sha256,
        "signedExportReceiptSizeBytes": signed_export.size_bytes,
        "tamperExitCode": crypto["tamperNegative"]["observedExitCode"],
        "transactionManifestSha256": transaction_manifest.sha256,
        "transactionManifestSizeBytes": transaction_manifest.size_bytes,
        "verificationBinarySha256": verifier_tool["binarySha256"],
        "verificationPackageVersion": verifier_tool["packageVersion"],
    }


def _verify(args: argparse.Namespace) -> dict[str, Any]:
    expected_fingerprint = require_text(
        args.expected_primary_fingerprint,
        "independently pinned primary fingerprint",
        FINGERPRINT_RE,
    )
    external = {
        "package": snapshot(
            args.package, "signed Debian package", MAX_PACKAGE_BYTES
        ),
        "policy": snapshot(
            args.policy, "debsig verification policy", MAX_JSON_BYTES
        ),
        "public_keyring": snapshot(
            args.public_keyring, "public OpenPGP keyring", MAX_KEY_BYTES
        ),
        "receipt": snapshot(
            args.receipt, "Linux signing receipt", MAX_JSON_BYTES
        ),
        "signed_export_receipt": snapshot(
            args.signed_export_receipt,
            "signed Linux export receipt",
            MAX_JSON_BYTES,
        ),
        "transaction_manifest": snapshot(
            args.transaction_manifest,
            "commit-last Linux signing transaction manifest",
            MAX_JSON_BYTES,
        ),
    }
    limits = {
        "package": MAX_PACKAGE_BYTES,
        "policy": MAX_JSON_BYTES,
        "public_keyring": MAX_KEY_BYTES,
        "receipt": MAX_JSON_BYTES,
        "signed_export_receipt": MAX_JSON_BYTES,
        "transaction_manifest": MAX_JSON_BYTES,
    }
    with tempfile.TemporaryDirectory(
        prefix="chummer-keyless-verification-"
    ) as raw:
        private_root = Path(raw)
        private_root.chmod(0o700)
        long_key_id = expected_fingerprint[-16:]
        destinations = {
            "package": private_root / ARTIFACT_FILE_NAME,
            "policy": (
                private_root
                / "signing"
                / "policies"
                / long_key_id
                / POLICY_FILE_NAME
            ),
            "public_keyring": (
                private_root
                / "signing"
                / "keyrings"
                / long_key_id
                / KEYRING_FILE_NAME
            ),
            "receipt": private_root / SIGNING_RECEIPT_FILE_NAME,
            "signed_export_receipt": (
                private_root / SIGNED_EXPORT_RECEIPT_FILE_NAME
            ),
            "transaction_manifest": (
                private_root / TRANSACTION_MANIFEST_FILE_NAME
            ),
        }
        held = {
            key: copy_new(
                external[key],
                destination,
                f"private verification {key.replace('_', ' ')}",
            )
            for key, destination in destinations.items()
        }
        private_args = argparse.Namespace(**vars(args))
        for key, value in held.items():
            setattr(private_args, key, value.path)
        result = _verify_held(private_args, held["package"])
        require_unchanged_inputs(
            {
                key: (
                    held[key],
                    f"private verification {key.replace('_', ' ')}",
                    limits[key],
                )
                for key in held
            }
        )
    for key, before in external.items():
        after = snapshot(
            before.path,
            f"{key.replace('_', ' ')} after keyless verification",
            limits[key],
        )
        if (
            after.sha256 != before.sha256
            or after.size_bytes != before.size_bytes
            or before.identity is None
            or after.identity is None
            or not _same_identity(after.identity, before.identity)
        ):
            fail(
                f"{key.replace('_', ' ')} changed during keyless "
                "verification"
            )
    return result


def _snake(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()


def parser() -> argparse.ArgumentParser:
    root = argparse.ArgumentParser(description=__doc__)
    commands = root.add_subparsers(dest="command", required=True)
    sign = commands.add_parser("sign")
    sign.add_argument("--input-package", required=True, type=Path)
    sign.add_argument("--output-package", required=True, type=Path)
    sign.add_argument("--unsigned-export-receipt", required=True, type=Path)
    sign.add_argument("--signed-export-receipt", required=True, type=Path)
    sign.add_argument("--transaction-manifest", required=True, type=Path)
    sign.add_argument("--receipt", required=True, type=Path)
    sign.add_argument("--policy", required=True, type=Path)
    sign.add_argument("--public-keyring", required=True, type=Path)
    sign.add_argument("--release-version", required=True)
    sign.add_argument("--expected-fingerprint", required=True)
    sign.add_argument("--expected-public-keyring-sha256", required=True)
    sign.add_argument("--expected-unsigned-package-sha256", required=True)
    sign.add_argument("--expected-unsigned-package-size", required=True)
    sign.add_argument(
        "--expected-unsigned-export-receipt-sha256", required=True
    )
    sign.add_argument("--artifact-member-path", required=True)
    sign.add_argument("--signing-receipt-member-path", required=True)
    sign.add_argument("--policy-member-path", required=True)
    sign.add_argument("--public-keyring-member-path", required=True)
    sign.add_argument("--source-repository", required=True)
    sign.add_argument("--source-workflow", required=True)
    sign.add_argument("--source-run-id", required=True)
    sign.add_argument("--source-run-attempt", required=True)
    sign.add_argument("--source-ref", required=True)
    sign.add_argument("--source-sha", required=True)
    sign.add_argument("--source-actor", required=True)
    verify = commands.add_parser("verify")
    verify.add_argument("--package", required=True, type=Path)
    verify.add_argument("--receipt", required=True, type=Path)
    verify.add_argument("--signed-export-receipt", required=True, type=Path)
    verify.add_argument("--transaction-manifest", required=True, type=Path)
    verify.add_argument("--policy", required=True, type=Path)
    verify.add_argument("--public-keyring", required=True, type=Path)
    verify.add_argument("--release-version", required=True)
    verify.add_argument("--expected-primary-fingerprint", required=True)
    verify.add_argument("--expected-public-keyring-sha256", required=True)
    verify.add_argument(
        "--expected-signed-export-receipt-sha256", required=True
    )
    verify.add_argument(
        "--expected-transaction-manifest-sha256", required=True
    )
    stage = commands.add_parser("stage-output-set")
    stage.add_argument("--package", required=True, type=Path)
    stage.add_argument("--receipt", required=True, type=Path)
    stage.add_argument(
        "--signed-export-receipt", required=True, type=Path
    )
    stage.add_argument("--policy", required=True, type=Path)
    stage.add_argument("--public-keyring", required=True, type=Path)
    stage.add_argument(
        "--transaction-manifest", required=True, type=Path
    )
    stage.add_argument("--output-root", required=True, type=Path)
    stage.add_argument("--expected-primary-fingerprint", required=True)
    stage.add_argument(
        "--expected-transaction-manifest-sha256", required=True
    )
    lifecycle_stage = commands.add_parser("stage-lifecycle-packages")
    lifecycle_stage.add_argument("--candidate", required=True, type=Path)
    lifecycle_stage.add_argument(
        "--expected-candidate-sha256", required=True
    )
    lifecycle_stage.add_argument("--expected-candidate-size", required=True)
    lifecycle_stage.add_argument("--n-minus-one", required=True, type=Path)
    lifecycle_stage.add_argument(
        "--expected-n-minus-one-sha256", required=True
    )
    lifecycle_stage.add_argument(
        "--expected-n-minus-one-size", required=True
    )
    return root


def main(argv: Iterable[str] | None = None) -> int:
    args = parser().parse_args(list(argv) if argv is not None else None)
    try:
        if args.command == "sign":
            result = _sign(args)
        elif args.command == "verify":
            result = _verify(args)
        elif args.command == "stage-output-set":
            result = _stage_output_set(args)
        else:
            result = _stage_lifecycle_packages(args)
    except ContractError as exc:
        print(f"linux-deb-signing:error: {exc}", file=sys.stderr)
        return 1
    for key in sorted(result):
        print(f"{_snake(key)}={result[key]}")
    print(f"linux-deb-signing:{args.command}:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
