#!/usr/bin/env python3
"""Append the stable, literal Windows bootstrap metadata trailer exactly once."""

from __future__ import annotations

import argparse
import hashlib
import os
import re
from pathlib import Path, PurePosixPath
from urllib.parse import urlparse


TRAILER_MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"
DEFINE_PATTERN = re.compile(r'^\s*!define\s+([A-Z0-9_]+)\s+"(.*)"\s*$')
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
STREAM_CHUNK_BYTES = 1024 * 1024


def decode_nsis_define(value: str) -> str:
    return value.replace('$\\"', '"').replace("$$", "$")


def read_defines(config_path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for raw_line in config_path.read_text(encoding="utf-8").splitlines():
        match = DEFINE_PATTERN.fullmatch(raw_line)
        if match:
            values[match.group(1)] = decode_nsis_define(match.group(2))
    return values


def require_value(defines: dict[str, str], name: str) -> str:
    value = defines.get(name, "").strip()
    if not value:
        raise ValueError(f"bootstrap config is missing {name}")
    return value


def build_trailer_from_defines(defines: dict[str, str]) -> bytes:
    payload_file_name = require_value(defines, "CHUMMER_PAYLOAD_FILE_NAME")
    payload_url = require_value(defines, "CHUMMER_PAYLOAD_URL")
    payload_sha256 = require_value(defines, "CHUMMER_PAYLOAD_SHA256").lower()
    payload_size_bytes = require_value(defines, "CHUMMER_PAYLOAD_SIZE_BYTES")
    acquisition_mode = require_value(defines, "CHUMMER_PAYLOAD_ACQUISITION_MODE").lower()

    parsed_url = urlparse(payload_url)
    if parsed_url.scheme not in {"http", "https", "file"}:
        raise ValueError("CHUMMER_PAYLOAD_URL must be an absolute http, https, or file URL")
    if parsed_url.scheme != "file" and not parsed_url.netloc:
        raise ValueError("CHUMMER_PAYLOAD_URL must include a network host")
    if not SHA256_PATTERN.fullmatch(payload_sha256):
        raise ValueError("CHUMMER_PAYLOAD_SHA256 must be a lowercase 64-character SHA-256")
    try:
        payload_size = int(payload_size_bytes)
    except ValueError as exc:
        raise ValueError("CHUMMER_PAYLOAD_SIZE_BYTES must be an integer") from exc
    if payload_size <= 0:
        raise ValueError("CHUMMER_PAYLOAD_SIZE_BYTES must be positive")
    if acquisition_mode not in {"download", "embedded"}:
        raise ValueError("CHUMMER_PAYLOAD_ACQUISITION_MODE must be download or embedded")
    if acquisition_mode == "embedded" and not defines.get("CHUMMER_EMBEDDED_PAYLOAD_PATH", "").strip():
        raise ValueError("embedded bootstrap config is missing CHUMMER_EMBEDDED_PAYLOAD_PATH")

    return TRAILER_MARKER + (
        f"payloadFileName={payload_file_name}\n"
        f"payloadDownloadUrl={payload_url}\n"
        f"payloadSha256={payload_sha256}\n"
        f"payloadSizeBytes={payload_size}\n"
        f"payloadAcquisitionMode={acquisition_mode}\n"
    ).encode("utf-8")


def build_trailer(config_path: Path) -> bytes:
    return build_trailer_from_defines(read_defines(config_path))


def require_regular_file(path: Path, label: str) -> None:
    if path.is_symlink() or not path.is_file():
        raise ValueError(f"{label} must be a regular non-symlink file: {path}")


def resolve_embedded_payload_path(config_path: Path, configured_path: str) -> Path:
    normalized = configured_path.strip().replace("\\", "/")
    container_path = PurePosixPath(normalized)
    try:
        relative_path = container_path.relative_to("/work")
    except ValueError as exc:
        raise ValueError("CHUMMER_EMBEDDED_PAYLOAD_PATH must be located beneath /work") from exc
    if not relative_path.parts or any(part in {"", ".", ".."} for part in relative_path.parts):
        raise ValueError("CHUMMER_EMBEDDED_PAYLOAD_PATH must name a file beneath /work")

    stage_root = config_path.parent.resolve()
    payload_path = stage_root.joinpath(*relative_path.parts)
    require_regular_file(payload_path, "embedded payload")
    try:
        payload_path.resolve().relative_to(stage_root)
    except ValueError as exc:
        raise ValueError("embedded payload must remain inside the bootstrap stage directory") from exc
    return payload_path


def validate_embedded_payload(config_path: Path, defines: dict[str, str]) -> str:
    acquisition_mode = require_value(defines, "CHUMMER_PAYLOAD_ACQUISITION_MODE").lower()
    if acquisition_mode != "embedded":
        return acquisition_mode

    configured_path = require_value(defines, "CHUMMER_EMBEDDED_PAYLOAD_PATH")
    payload_path = resolve_embedded_payload_path(config_path, configured_path)
    expected_sha256 = require_value(defines, "CHUMMER_PAYLOAD_SHA256").lower()
    expected_size_text = require_value(defines, "CHUMMER_PAYLOAD_SIZE_BYTES")
    try:
        expected_size = int(expected_size_text)
    except ValueError as exc:
        raise ValueError("CHUMMER_PAYLOAD_SIZE_BYTES must be an integer") from exc

    digest = hashlib.sha256()
    with payload_path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        if before.st_size != expected_size:
            raise ValueError(
                "embedded payload size does not match CHUMMER_PAYLOAD_SIZE_BYTES "
                f"(expected {expected_size}, actual {before.st_size})"
            )
        while chunk := stream.read(STREAM_CHUNK_BYTES):
            digest.update(chunk)
        after = os.fstat(stream.fileno())

    if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) != (
        after.st_dev,
        after.st_ino,
        after.st_size,
        after.st_mtime_ns,
    ):
        raise ValueError("embedded payload changed while it was being validated")
    actual_sha256 = digest.hexdigest()
    if actual_sha256 != expected_sha256:
        raise ValueError(
            "embedded payload SHA-256 does not match CHUMMER_PAYLOAD_SHA256 "
            f"(expected {expected_sha256}, actual {actual_sha256})"
        )
    return acquisition_mode


def marker_offsets(installer_path: Path) -> list[int]:
    offsets: list[int] = []
    overlap = b""
    bytes_consumed = 0
    with installer_path.open("rb") as stream:
        while chunk := stream.read(STREAM_CHUNK_BYTES):
            window = overlap + chunk
            window_offset = bytes_consumed - len(overlap)
            cursor = 0
            while True:
                marker_offset = window.find(TRAILER_MARKER, cursor)
                if marker_offset < 0:
                    break
                absolute_offset = window_offset + marker_offset
                if not offsets or offsets[-1] != absolute_offset:
                    offsets.append(absolute_offset)
                cursor = marker_offset + 1
            bytes_consumed += len(chunk)
            overlap = window[-(len(TRAILER_MARKER) - 1) :]
    return offsets


def read_suffix(path: Path, size: int) -> bytes:
    if path.stat().st_size < size:
        return b""
    with path.open("rb") as stream:
        stream.seek(-size, os.SEEK_END)
        return stream.read()


def finalize(installer_path: Path, config_path: Path) -> str:
    require_regular_file(installer_path, "installer")
    require_regular_file(config_path, "bootstrap config")
    defines = read_defines(config_path)
    trailer = build_trailer_from_defines(defines)
    validate_embedded_payload(config_path, defines)

    offsets = marker_offsets(installer_path)
    expected_offset = installer_path.stat().st_size - len(trailer)
    exact_eof_trailer = expected_offset >= 0 and read_suffix(installer_path, len(trailer)) == trailer
    if offsets:
        if offsets == [expected_offset] and exact_eof_trailer:
            return "already_finalized"
        if exact_eof_trailer and expected_offset in offsets:
            raise ValueError("installer contains more than one bootstrap metadata trailer")
        raise ValueError("installer already has a conflicting bootstrap metadata trailer")

    original_size = installer_path.stat().st_size
    with installer_path.open("ab") as stream:
        stream.write(trailer)
        stream.flush()
        os.fsync(stream.fileno())

    if marker_offsets(installer_path) != [original_size] or read_suffix(installer_path, len(trailer)) != trailer:
        raise ValueError("installer finalization did not produce exactly one metadata trailer at EOF")
    return "finalized"


def validate_payload_only(config_path: Path) -> str:
    require_regular_file(config_path, "bootstrap config")
    defines = read_defines(config_path)
    build_trailer_from_defines(defines)
    return validate_embedded_payload(config_path, defines)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--installer", type=Path)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--validate-payload-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        config_path = Path(os.path.abspath(args.config))
        if args.validate_payload_only:
            if args.installer is not None:
                raise ValueError("--installer cannot be combined with --validate-payload-only")
            mode = validate_payload_only(config_path)
            print(f"windows_bootstrap_payload:validated:{mode}: {args.config}")
            return 0
        if args.installer is None:
            raise ValueError("--installer is required unless --validate-payload-only is used")
        installer_path = Path(os.path.abspath(args.installer))
        status = finalize(installer_path, config_path)
    except (OSError, ValueError) as exc:
        print(f"windows_bootstrap_metadata:fail: {exc}", file=__import__("sys").stderr)
        return 1
    print(f"windows_bootstrap_metadata:{status}: {args.installer}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
