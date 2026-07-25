#!/usr/bin/env python3
"""Bind an exact release-shelf DMG to a raw macOS startup-smoke receipt.

The desktop runtime, not this helper, produces the startup result.  This
helper validates that raw result and adds only the canonical release artifact
metadata used by the desktop exit-gate materializer.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
import tempfile
from datetime import UTC, datetime, timedelta
from pathlib import Path
from typing import Any, Mapping, Sequence


MAX_RECEIPT_BYTES = 1024 * 1024
MAX_ARTIFACT_BYTES = 4 * 1024 * 1024 * 1024
MAX_CLOCK_SKEW_SECONDS = 300
DEFAULT_MAX_RECEIPT_AGE_SECONDS = 3600
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
PORTABLE_TOKEN_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._+-]{0,127}$")
PASSING_STATUSES = frozenset({"pass", "passed"})
BINDER_METADATA_KEYS = frozenset(
    {
        "artifactPath",
        "artifactPathDisclosure",
        "artifactFileName",
        "fileName",
        "artifactRelativePath",
        "artifactSha256",
        "artifactDigest",
        "artifactId",
    }
)
RAW_ALLOWED_BINDER_KEYS = frozenset({"artifactDigest"})


class ContractError(RuntimeError):
    """The raw runtime receipt or shelf artifact cannot support the claim."""


def fail(message: str) -> None:
    raise ContractError(message)


def duplicate_rejecting_object(
    pairs: list[tuple[str, Any]],
) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            fail(f"startup receipt contains duplicate key {key!r}")
        value[key] = item
    return value


def require_string(
    value: object,
    label: str,
    pattern: re.Pattern[str] | None = None,
) -> str:
    if (
        not isinstance(value, str)
        or not value
        or len(value) > 4096
        or any(ord(character) < 32 for character in value)
        or (pattern is not None and pattern.fullmatch(value) is None)
    ):
        fail(f"{label} is invalid")
    return value


def parse_time(value: object, label: str) -> datetime:
    raw = require_string(value, label)
    normalized = raw[:-1] + "+00:00" if raw.endswith("Z") else raw
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError:
        fail(f"{label} is not RFC3339")
    if parsed.tzinfo is None:
        fail(f"{label} has no timezone")
    return parsed.astimezone(UTC)


def stable_regular_file_bytes(
    path: Path, *, maximum_bytes: int, label: str
) -> tuple[bytes, os.stat_result]:
    try:
        before = path.stat(follow_symlinks=False)
    except OSError as exc:
        fail(f"{label} cannot be inspected: {exc}")
    if (
        path.is_symlink()
        or not stat.S_ISREG(before.st_mode)
        or before.st_size < 1
        or before.st_size > maximum_bytes
    ):
        fail(f"{label} is not a bounded nonempty regular file")
    descriptor = -1
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY | int(getattr(os, "O_NOFOLLOW", 0)),
        )
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
            fail(f"{label} changed before it was opened")
        chunks: list[bytes] = []
        remaining = maximum_bytes + 1
        while remaining > 0:
            chunk = os.read(descriptor, min(1024 * 1024, remaining))
            if not chunk:
                break
            chunks.append(chunk)
            remaining -= len(chunk)
        data = b"".join(chunks)
        after = os.fstat(descriptor)
    except ContractError:
        raise
    except OSError as exc:
        fail(f"{label} cannot be read: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if len(data) > maximum_bytes:
        fail(f"{label} exceeds its byte boundary")
    if (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
        after.st_ctime_ns,
    ) != (
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
        before.st_ctime_ns,
    ):
        fail(f"{label} changed while it was read")
    return data, before


def load_receipt(path: Path) -> tuple[dict[str, Any], os.stat_result]:
    raw, receipt_stat = stable_regular_file_bytes(
        path,
        maximum_bytes=MAX_RECEIPT_BYTES,
        label="raw startup receipt",
    )
    try:
        decoded = raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        fail("raw startup receipt is not UTF-8")
    try:
        payload = json.loads(
            decoded,
            object_pairs_hook=duplicate_rejecting_object,
            parse_constant=lambda token: fail(
                f"raw startup receipt contains non-finite token {token!r}"
            ),
        )
    except json.JSONDecodeError as exc:
        fail(f"raw startup receipt is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail("raw startup receipt must be a JSON object")
    return payload, receipt_stat


def hash_artifact(path: Path) -> tuple[str, int]:
    if path.parent.name.casefold() != "files":
        fail("candidate DMG must be the exact direct child of a files shelf")
    if path.parent.is_symlink() or not path.parent.is_dir():
        fail("candidate DMG files shelf is linked or missing")
    try:
        before = path.stat(follow_symlinks=False)
    except OSError as exc:
        fail(f"candidate shelf DMG cannot be inspected: {exc}")
    if (
        path.is_symlink()
        or not stat.S_ISREG(before.st_mode)
        or before.st_size < 1
        or before.st_size > MAX_ARTIFACT_BYTES
    ):
        fail("candidate shelf DMG is not a bounded nonempty regular file")
    descriptor = -1
    digest = hashlib.sha256()
    try:
        descriptor = os.open(
            path,
            os.O_RDONLY | int(getattr(os, "O_NOFOLLOW", 0)),
        )
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
            fail("candidate shelf DMG changed before it was opened")
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
        after = os.fstat(descriptor)
    except ContractError:
        raise
    except OSError as exc:
        fail(f"candidate shelf DMG cannot be read: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    if (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
        after.st_ctime_ns,
    ) != (
        before.st_dev,
        before.st_ino,
        before.st_size,
        before.st_mtime_ns,
        before.st_ctime_ns,
    ):
        fail("candidate shelf DMG changed while it was hashed")
    return digest.hexdigest(), after.st_size


def validate_raw_receipt(
    payload: Mapping[str, Any],
    *,
    expected_app_key: str,
    expected_rid: str,
    expected_channel: str,
    expected_version: str,
    expected_launch_target: str,
    expected_host_class: str,
    expected_digest: str,
    now: datetime,
    max_age_seconds: int,
) -> None:
    unexpected_metadata = (
        set(payload) & BINDER_METADATA_KEYS
    ) - RAW_ALLOWED_BINDER_KEYS
    if unexpected_metadata:
        fail(
            "raw startup receipt already contains binder-owned metadata: "
            + ", ".join(sorted(unexpected_metadata))
        )

    status = require_string(payload.get("status"), "startup receipt status")
    if status.casefold() not in PASSING_STATUSES:
        fail("startup receipt status is not passing")
    exact_fields = {
        "headId": expected_app_key,
        "version": expected_version,
        "releaseVersion": expected_version,
        "channelId": expected_channel,
        "platform": "macos",
        "arch": "arm64" if expected_rid.endswith("arm64") else "x64",
        "rid": expected_rid,
        "readyCheckpoint": "pre_ui_event_loop",
        "hostClass": expected_host_class,
        "artifactDigest": f"sha256:{expected_digest}",
        "artifactDigestSource": "environment",
    }
    for key, expected in exact_fields.items():
        actual = require_string(
            payload.get(key), f"startup receipt {key}"
        )
        if actual != expected:
            fail(f"startup receipt {key} differs from the exact native run")

    operating_system = require_string(
        payload.get("operatingSystem"),
        "startup receipt operatingSystem",
    )
    if not any(
        token in operating_system.casefold()
        for token in ("macos", "mac os", "darwin")
    ):
        fail("startup receipt operatingSystem does not identify macOS")

    process_path = require_string(
        payload.get("processPath"), "startup receipt processPath"
    )
    process_leaf = process_path.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1]
    if process_leaf != expected_launch_target:
        fail("startup receipt processPath is not the launched app executable")

    started = parse_time(
        payload.get("startedAtUtc"), "startup receipt startedAtUtc"
    )
    recorded = parse_time(
        payload.get("recordedAtUtc"), "startup receipt recordedAtUtc"
    )
    completed = parse_time(
        payload.get("completedAtUtc"), "startup receipt completedAtUtc"
    )
    if not started <= recorded <= completed:
        fail("startup receipt timestamps are not ordered")
    if completed > now + timedelta(seconds=MAX_CLOCK_SKEW_SECONDS):
        fail("startup receipt completedAtUtc is too far in the future")
    if now - completed > timedelta(seconds=max_age_seconds):
        fail("startup receipt completedAtUtc is stale")


def write_json_atomic(
    path: Path, payload: Mapping[str, Any], receipt_stat: os.stat_result
) -> None:
    encoded = (
        json.dumps(
            payload,
            indent=2,
            ensure_ascii=True,
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        suffix=".tmp",
        dir=path.parent,
    )
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(temporary_name, stat.S_IMODE(receipt_stat.st_mode))
        current = path.stat(follow_symlinks=False)
        if (
            current.st_dev,
            current.st_ino,
            current.st_size,
            current.st_mtime_ns,
            current.st_ctime_ns,
        ) != (
            receipt_stat.st_dev,
            receipt_stat.st_ino,
            receipt_stat.st_size,
            receipt_stat.st_mtime_ns,
            receipt_stat.st_ctime_ns,
        ):
            fail("raw startup receipt changed before artifact binding")
        os.replace(temporary_name, path)
    finally:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass


def bind(args: argparse.Namespace) -> None:
    expected_sha256 = require_string(
        args.expected_sha256,
        "expected candidate SHA-256",
        SHA256_RE,
    )
    expected_size = args.expected_size
    if expected_size < 1 or expected_size > MAX_ARTIFACT_BYTES:
        fail("expected candidate size is invalid")
    for label, value in (
        ("app key", args.expected_app_key),
        ("RID", args.expected_rid),
        ("channel", args.expected_channel),
        ("version", args.expected_version),
    ):
        require_string(value, label, PORTABLE_TOKEN_RE)
    require_string(args.expected_file_name, "expected file name")
    require_string(args.expected_launch_target, "expected launch target")
    require_string(args.expected_host_class, "expected host class")
    if args.max_age_seconds < 1 or args.max_age_seconds > 24 * 60 * 60:
        fail("maximum receipt age is invalid")

    artifact_path = args.artifact.absolute()
    if artifact_path.name != args.expected_file_name:
        fail("candidate shelf DMG file name differs")
    actual_sha256, actual_size = hash_artifact(artifact_path)
    if (
        actual_sha256 != expected_sha256
        or actual_size != expected_size
    ):
        fail("candidate shelf DMG digest or size differs from candidate pins")

    receipt_path = args.receipt.absolute()
    payload, receipt_stat = load_receipt(receipt_path)
    raw_payload = dict(payload)
    validate_raw_receipt(
        payload,
        expected_app_key=args.expected_app_key,
        expected_rid=args.expected_rid,
        expected_channel=args.expected_channel,
        expected_version=args.expected_version,
        expected_launch_target=args.expected_launch_target,
        expected_host_class=args.expected_host_class,
        expected_digest=expected_sha256,
        now=datetime.now(UTC),
        max_age_seconds=args.max_age_seconds,
    )

    artifact_relative_path = f"files/{artifact_path.name}"
    payload.update(
        {
            "artifactPath": artifact_relative_path,
            "artifactPathDisclosure": "artifact_shelf_relative_path",
            "artifactFileName": artifact_path.name,
            "fileName": artifact_path.name,
            "artifactRelativePath": artifact_relative_path,
            "artifactSha256": expected_sha256,
            "artifactDigest": f"sha256:{expected_sha256}",
            "artifactId": (
                f"{args.expected_app_key}-{args.expected_rid}-installer"
            ),
        }
    )
    for key, value in raw_payload.items():
        if payload.get(key) != value:
            fail(f"binder changed raw startup receipt field {key}")
    if set(payload) - set(raw_payload) != (
        BINDER_METADATA_KEYS - RAW_ALLOWED_BINDER_KEYS
    ):
        fail("binder attempted to add fields outside canonical artifact metadata")
    write_json_atomic(receipt_path, payload, receipt_stat)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Validate a raw macOS DesktopStartupSmokeRuntime receipt and "
            "bind its exact release-shelf DMG."
        )
    )
    parser.add_argument("--receipt", required=True, type=Path)
    parser.add_argument("--artifact", required=True, type=Path)
    parser.add_argument("--expected-sha256", required=True)
    parser.add_argument("--expected-size", required=True, type=int)
    parser.add_argument("--expected-file-name", required=True)
    parser.add_argument("--expected-app-key", required=True)
    parser.add_argument("--expected-rid", required=True)
    parser.add_argument("--expected-channel", required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-launch-target", required=True)
    parser.add_argument("--expected-host-class", required=True)
    parser.add_argument(
        "--max-age-seconds",
        type=int,
        default=DEFAULT_MAX_RECEIPT_AGE_SECONDS,
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        bind(args)
    except ContractError as exc:
        print(f"macOS startup artifact binding blocked: {exc}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
