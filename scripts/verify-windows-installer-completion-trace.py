#!/usr/bin/env python3
from __future__ import annotations

import argparse
import os
import stat
import sys
from pathlib import Path


TRACE_FILE_NAME = "chummer-desktop-installer-progress.log"
TRACE_HEADER = "# Chummer installer trace"
EXTRACTION_MARKER = "Extracting application files"
COMPLETION_MARKER = "Install complete"
MAX_TRACE_BYTES = 1024 * 1024


class TraceContractError(ValueError):
    pass


def trace_path(value: str) -> Path:
    path = Path(value)
    portable_leaf = value.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1]
    if portable_leaf.casefold() != TRACE_FILE_NAME.casefold():
        raise TraceContractError("installer trace path has an unexpected file name")
    return path


def reset_trace(path: Path) -> None:
    try:
        path_stat = path.lstat()
    except FileNotFoundError:
        return
    if not stat.S_ISREG(path_stat.st_mode):
        raise TraceContractError("pre-existing installer trace is not a regular file")
    path.unlink()
    if path.exists() or path.is_symlink():
        raise TraceContractError("pre-existing installer trace could not be removed")


def decode_trace(raw: bytes) -> str:
    if raw.startswith(b"\xff\xfe") or raw.startswith(b"\xfe\xff"):
        return raw.decode("utf-16")
    if raw.startswith(b"\xef\xbb\xbf"):
        return raw.decode("utf-8-sig")
    sample = raw[:128]
    if len(sample) >= 4:
        odd_nuls = sample[1::2].count(0)
        even_nuls = sample[::2].count(0)
        if odd_nuls >= max(2, len(sample[1::2]) // 2) and even_nuls == 0:
            return raw.decode("utf-16-le")
        if even_nuls >= max(2, len(sample[::2]) // 2) and odd_nuls == 0:
            return raw.decode("utf-16-be")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("cp1252")


def verify_trace(path: Path, expected_install_root: str) -> None:
    if not expected_install_root.strip():
        raise TraceContractError("expected smoke install root is empty")
    try:
        path_stat = path.lstat()
    except FileNotFoundError as exc:
        raise TraceContractError("installer trace is not present") from exc
    if not stat.S_ISREG(path_stat.st_mode):
        raise TraceContractError("installer trace is not a regular file")

    with path.open("rb") as trace:
        opened_stat = os.fstat(trace.fileno())
        if not stat.S_ISREG(opened_stat.st_mode):
            raise TraceContractError("opened installer trace is not a regular file")
        if (
            path_stat.st_dev,
            path_stat.st_ino,
        ) != (
            opened_stat.st_dev,
            opened_stat.st_ino,
        ):
            raise TraceContractError("installer trace identity changed while opening")
        raw = trace.read(MAX_TRACE_BYTES + 1)
    if not raw:
        raise TraceContractError("installer trace is empty")
    if len(raw) > MAX_TRACE_BYTES:
        raise TraceContractError("installer trace exceeds the fixed size bound")

    lines = decode_trace(raw).splitlines()
    expected_target = f"Smoke install target: {expected_install_root}"
    required_lines = (
        TRACE_HEADER,
        expected_target,
        EXTRACTION_MARKER,
        COMPLETION_MARKER,
    )
    counts = {line: lines.count(line) for line in required_lines}
    if any(counts[line] != 1 for line in required_lines):
        raise TraceContractError(
            "installer trace does not contain exactly one current-run marker set"
        )

    positions = [lines.index(line) for line in required_lines]
    if positions != sorted(positions):
        raise TraceContractError(
            "installer trace current-run markers are out of order"
        )


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser()
    subparsers = result.add_subparsers(dest="command", required=True)

    reset = subparsers.add_parser("reset")
    reset.add_argument("--trace-path", required=True)

    verify = subparsers.add_parser("verify")
    verify.add_argument("--trace-path", required=True)
    verify.add_argument("--expected-install-root", required=True)
    return result


def main() -> int:
    args = parser().parse_args()
    try:
        path = trace_path(args.trace_path)
        if args.command == "reset":
            reset_trace(path)
        else:
            verify_trace(path, args.expected_install_root)
    except (OSError, UnicodeError, TraceContractError) as exc:
        print(f"installer completion trace contract failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
