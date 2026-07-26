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
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
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
EXPECTED_DEBSIGS_VERSION = "0.1.26"
EXPECTED_DEBSIG_VERIFY_VERSION = "0.29"
MAX_PACKAGE_BYTES = 2 * 1024 * 1024 * 1024
MAX_JSON_BYTES = 4 * 1024 * 1024
MAX_KEY_BYTES = 1024 * 1024
MAX_PASSPHRASE_BYTES = 16 * 1024
MAX_TOOL_OUTPUT_BYTES = 128 * 1024
MAX_EXACT_INTEGER = 9_007_199_254_740_991
TAMPER_REJECTION_EXIT_CODE = 13

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
class Snapshot:
    path: Path
    sha256: str
    size_bytes: int
    data: bytes | None = None


@dataclass(frozen=True)
class Tool:
    name: str
    path: Path
    package_name: str
    expected_package_version: str


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
    digest = hashlib.sha256()
    data_parts: list[bytes] | None = [] if read_data else None
    size = 0
    try:
        descriptor = os.open(
            absolute,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        with os.fdopen(descriptor, "rb", closefd=True) as stream:
            opened = os.fstat(stream.fileno())
            if (
                opened.st_dev,
                opened.st_ino,
                opened.st_size,
            ) != (
                before.st_dev,
                before.st_ino,
                before.st_size,
            ):
                fail(f"{label} changed before it could be read")
            while True:
                chunk = stream.read(1024 * 1024)
                if not chunk:
                    break
                size += len(chunk)
                if size > maximum_bytes:
                    fail(f"{label} exceeded its fixed byte bound")
                digest.update(chunk)
                if data_parts is not None:
                    data_parts.append(chunk)
        after = absolute.lstat()
    except ContractError:
        raise
    except OSError as exc:
        fail(f"{label} could not be read safely: {exc}")
    if (
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
        before.st_ctime_ns,
    ) != (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
        after.st_ctime_ns,
    ) or size != before.st_size:
        fail(f"{label} changed while it was read")
    return Snapshot(
        absolute,
        digest.hexdigest(),
        size,
        b"".join(data_parts) if data_parts is not None else None,
    )


def _write_all(descriptor: int, data: bytes) -> None:
    offset = 0
    while offset < len(data):
        written = os.write(descriptor, data[offset:])
        if written < 1:
            fail("file write made no forward progress")
        offset += written


def write_new_bytes(path: Path, data: bytes, label: str) -> Snapshot:
    absolute = Path(os.path.abspath(path))
    absolute.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    descriptor = -1
    try:
        descriptor = os.open(
            absolute,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
        )
        _write_all(descriptor, data)
        os.fsync(descriptor)
    except OSError as exc:
        fail(f"{label} must be a new private regular file: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return snapshot(
        absolute, label, max(len(data), 1), read_data=False
    )


def write_new_json(path: Path, payload: Mapping[str, Any], label: str) -> Snapshot:
    return write_new_bytes(
        path,
        (
            json.dumps(payload, indent=2, sort_keys=True, ensure_ascii=False)
            + "\n"
        ).encode("utf-8"),
        label,
    )


def copy_new(source: Snapshot, destination: Path, label: str) -> Snapshot:
    absolute = Path(os.path.abspath(destination))
    absolute.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    source_descriptor = -1
    target_descriptor = -1
    try:
        source_descriptor = os.open(
            source.path,
            os.O_RDONLY
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
        )
        target_descriptor = os.open(
            absolute,
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | int(getattr(os, "O_CLOEXEC", 0))
            | int(getattr(os, "O_NOFOLLOW", 0)),
            0o600,
        )
        while True:
            chunk = os.read(source_descriptor, 1024 * 1024)
            if not chunk:
                break
            _write_all(target_descriptor, chunk)
        os.fsync(target_descriptor)
    except OSError as exc:
        fail(f"{label} could not be copied safely: {exc}")
    finally:
        if source_descriptor >= 0:
            os.close(source_descriptor)
        if target_descriptor >= 0:
            os.close(target_descriptor)
    copied = snapshot(absolute, label, MAX_PACKAGE_BYTES)
    if (
        copied.sha256 != source.sha256
        or copied.size_bytes != source.size_bytes
    ):
        fail(f"{label} differs from its input bytes")
    return copied


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
) -> subprocess.CompletedProcess[bytes]:
    try:
        completed = subprocess.run(
            list(command),
            env=dict(environment) if environment is not None else None,
            input=input_data,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            timeout=180,
        )
    except (OSError, subprocess.TimeoutExpired) as exc:
        fail(f"{label} could not execute: {exc}")
    stdout = _bounded_output(completed.stdout, f"{label} stdout")
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
    expires = str(primary.get("expires", ""))
    if expires:
        try:
            expires_at = int(expires, 10)
        except ValueError:
            fail("OpenPGP primary key expiry is malformed")
        if expires_at <= int(datetime.now(UTC).timestamp()):
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
    held = snapshot(path, "Debian archive", MAX_PACKAGE_BYTES, read_data=True)
    assert held.data is not None
    data = held.data
    if not data.startswith(b"!<arch>\n"):
        fail("candidate is not a Debian ar archive")
    offset = 8
    members: list[tuple[str, int, int]] = []
    while offset < len(data):
        if offset + 60 > len(data):
            fail("candidate ar archive has a truncated header")
        header = data[offset : offset + 60]
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
        if not name or size < 1 or end > len(data):
            fail("candidate ar archive member is invalid")
        members.append((name, start, size))
        offset = end + (size % 2)
    if offset != len(data):
        fail("candidate ar archive has trailing or truncated bytes")
    return members


def _signed_payload_and_signature(path: Path) -> tuple[bytes, bytes]:
    held = snapshot(
        path, "signed Debian package payload", MAX_PACKAGE_BYTES, read_data=True
    )
    assert held.data is not None
    members = _ar_members(path)
    by_name = {
        name: held.data[start : start + size]
        for name, start, size in members
    }
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
    return (
        by_name[DEBIAN_BINARY_MEMBER]
        + by_name[CONTROL_MEMBER]
        + by_name[DATA_MEMBER],
        by_name[ORIGIN_SIGNATURE_MEMBER],
    )


def require_unsigned_deb(path: Path) -> None:
    members = _ar_members(path)
    names = [name for name, _, _ in members]
    if names != [DEBIAN_BINARY_MEMBER, CONTROL_MEMBER, DATA_MEMBER]:
        fail(
            "unsigned package must contain only canonical xz Debian members; "
            "the pinned debsigs implementation cannot sign zstd members"
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


def tampered_copy(source: Path, destination: Path) -> Snapshot:
    held = snapshot(
        source, "signed package for tamper check", MAX_PACKAGE_BYTES, read_data=True
    )
    assert held.data is not None
    data = bytearray(held.data)
    data_members = [
        (start, size)
        for name, start, size in _ar_members(source)
        if name == DATA_MEMBER
    ]
    if len(data_members) != 1:
        fail("signed package does not contain one data member")
    start, size = data_members[0]
    index = start + (size // 2)
    data[index] ^= 0x01
    return write_new_bytes(
        destination, bytes(data), "tampered signature-negative package"
    )


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
    payload, signature = _signed_payload_and_signature(package)
    with tempfile.TemporaryDirectory(
        prefix="chummer-gpgv-",
        dir=str(temporary_root) if temporary_root is not None else None,
    ) as raw:
        root = Path(raw)
        root.chmod(0o700)
        payload_path = root / "signed-payload.bin"
        signature_path = root / "origin-signature.pgp"
        write_new_bytes(
            payload_path, payload, "OpenPGP signed Debian payload"
        )
        write_new_bytes(
            signature_path, signature, "OpenPGP origin signature packet"
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
        hash_algorithm = int(fields[7], 10)
    except ValueError:
        fail("OpenPGP VALIDSIG timestamps or algorithm are malformed")
    now_timestamp = int(datetime.now(UTC).timestamp())
    if (
        created_timestamp < 1
        or created_timestamp > now_timestamp + 300
        or (expires_timestamp != 0 and expires_timestamp <= now_timestamp)
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
    require_text(receipt["generatedAt"], "Linux signing generatedAt", ZULU_RE)
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
    if (
        openpgp["createdAt"] != expected_created_at
        or openpgp["creationTimestamp"] > int(datetime.now(UTC).timestamp()) + 300
    ):
        fail("Linux signing OpenPGP creation time is inconsistent or future-dated")
    if (
        verifier["backend"] != VERIFY_BACKEND
        or verifier["policySha256"] != policy.sha256
        or verifier["publicKeyringSha256"] != keyring.sha256
        or verifier["positiveExitCode"] != 0
        or verifier["providerIndependent"] is not True
        or openpgp["fingerprint"] != signing
        or openpgp["primaryFingerprint"] != primary
        or openpgp["hashAlgorithm"] != "sha256"
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
    release_version = require_text(
        args.release_version, "release version", PORTABLE_ID_RE
    )
    fingerprint = require_text(
        args.expected_fingerprint,
        "expected signing fingerprint",
        FINGERPRINT_RE,
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
    require_unsigned_deb(unsigned.path)
    unsigned_export, _ = load_json(
        args.unsigned_export_receipt, "unsigned export receipt"
    )
    unsigned_export = validate_unsigned_export_receipt(
        unsigned_export,
        unsigned=unsigned,
        release_version=release_version,
        artifact_member_path=artifact_member,
        expected_source=source,
    )
    tools = collect_tool_records()
    private_key = _decode_secret_environment(
        args.private_key_env, MAX_KEY_BYTES, "OpenPGP private key"
    )
    passphrase = _decode_secret_environment(
        args.passphrase_env, MAX_PASSPHRASE_BYTES, "OpenPGP passphrase"
    )
    if any(character in passphrase for character in (b"\x00", b"\r", b"\n")):
        fail("OpenPGP passphrase must be one nonempty line")
    temporary_parent = Path(
        os.path.abspath(os.environ.get("RUNNER_TEMP", tempfile.gettempdir()))
    )
    if not temporary_parent.is_dir() or temporary_parent.is_symlink():
        fail("ephemeral signing parent is not a real directory")
    with tempfile.TemporaryDirectory(
        prefix="chummer-linux-signing-", dir=temporary_parent
    ) as raw_home:
        home = Path(raw_home)
        home.chmod(0o700)
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
        keyring_path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        if keyring_path.exists() or keyring_path.is_symlink():
            fail("public keyring output must not already exist")
        run_tool(
            [
                str(TOOLS["gpg"].path),
                "--batch",
                "--yes",
                "--output",
                str(keyring_path),
                "--export",
                primary_fingerprint,
            ],
            label="public OpenPGP key export",
            environment=environment,
        )
        keyring = snapshot(
            keyring_path, "public OpenPGP keyring", MAX_KEY_BYTES
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
        run_tool(
            ["/usr/bin/gpgconf", "--kill", "all"],
            label="ephemeral GnuPG agent shutdown",
            environment=environment,
        )
        signed = snapshot(
            signed.path, "origin-signed Debian package", MAX_PACKAGE_BYTES
        )
        if (
            signed.sha256 == unsigned.sha256
            or signed.size_bytes <= unsigned.size_bytes
        ):
            fail("origin signing did not create distinct signed package bytes")
        require_one_origin_signature(signed.path)
        crypto = verify_crypto(
            package=signed.path,
            policy=policy.path,
            keyring=keyring.path,
            signing_fingerprint=fingerprint,
            temporary_root=home,
        )
    generated_at = (
        datetime.now(UTC)
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
    validate_signing_receipt(
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
    return {
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


def _verify(args: argparse.Namespace) -> dict[str, Any]:
    package = snapshot(
        args.package, "signed Debian package", MAX_PACKAGE_BYTES
    )
    require_one_origin_signature(package.path)
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
    verifier_tool = tool_record(TOOLS["debsigVerify"])
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
        "tamperExitCode": crypto["tamperNegative"]["observedExitCode"],
        "verificationBinarySha256": verifier_tool["binarySha256"],
        "verificationPackageVersion": verifier_tool["packageVersion"],
    }


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
    sign.add_argument("--receipt", required=True, type=Path)
    sign.add_argument("--policy", required=True, type=Path)
    sign.add_argument("--public-keyring", required=True, type=Path)
    sign.add_argument("--release-version", required=True)
    sign.add_argument("--expected-fingerprint", required=True)
    sign.add_argument("--private-key-env", required=True)
    sign.add_argument("--passphrase-env", required=True)
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
    verify.add_argument("--policy", required=True, type=Path)
    verify.add_argument("--public-keyring", required=True, type=Path)
    verify.add_argument("--release-version", required=True)
    verify.add_argument("--expected-primary-fingerprint", required=True)
    verify.add_argument("--expected-public-keyring-sha256", required=True)
    return root


def main(argv: Iterable[str] | None = None) -> int:
    args = parser().parse_args(list(argv) if argv is not None else None)
    try:
        result = _sign(args) if args.command == "sign" else _verify(args)
    except ContractError as exc:
        print(f"linux-deb-signing:error: {exc}", file=sys.stderr)
        return 1
    for key in sorted(result):
        print(f"{_snake(key)}={result[key]}")
    print(f"linux-deb-signing:{args.command}:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
