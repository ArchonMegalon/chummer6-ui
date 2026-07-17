#!/usr/bin/env python3
"""Build a byte-reproducible ZIP archive from a directory tree."""

from __future__ import annotations

import argparse
import datetime as dt
import os
from pathlib import Path, PurePosixPath
import stat
import tempfile
import zipfile


ZIP_MINIMUM = dt.datetime(1980, 1, 1, tzinfo=dt.timezone.utc)
ZIP_MAXIMUM = dt.datetime(2107, 12, 31, 23, 59, 58, tzinfo=dt.timezone.utc)


def canonical_zip_timestamp() -> tuple[int, int, int, int, int, int]:
    raw_epoch = os.environ.get("SOURCE_DATE_EPOCH")
    if raw_epoch is None:
        instant = ZIP_MINIMUM
    else:
        try:
            epoch = int(raw_epoch, 10)
        except ValueError as exc:
            raise ValueError("SOURCE_DATE_EPOCH must be an integer Unix timestamp") from exc
        try:
            instant = dt.datetime.fromtimestamp(epoch, tz=dt.timezone.utc)
        except (OverflowError, OSError, ValueError) as exc:
            raise ValueError("SOURCE_DATE_EPOCH is outside the supported timestamp range") from exc
        instant = min(max(instant, ZIP_MINIMUM), ZIP_MAXIMUM)

    return (
        instant.year,
        instant.month,
        instant.day,
        instant.hour,
        instant.minute,
        instant.second - (instant.second % 2),
    )


def archive_name(root: Path, path: Path) -> str:
    relative = path.relative_to(root)
    if any(part in {"", ".", ".."} or "\\" in part for part in relative.parts):
        raise ValueError(f"unsafe ZIP member path: {relative}")
    name = PurePosixPath(*relative.parts).as_posix()
    if not name or name.startswith("/"):
        raise ValueError(f"unsafe ZIP member path: {relative}")
    return name


def collect_regular_files(root: Path) -> list[tuple[str, Path]]:
    entries: list[tuple[str, Path]] = []
    for directory, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        directory_path = Path(directory)
        for name in sorted(directory_names):
            child = directory_path / name
            mode = child.lstat().st_mode
            if stat.S_ISLNK(mode):
                raise ValueError(f"symbolic links are not allowed in release ZIPs: {child}")
            if not stat.S_ISDIR(mode):
                raise ValueError(f"non-directory entry encountered while walking release ZIP: {child}")
        directory_names.sort()

        for name in sorted(file_names):
            child = directory_path / name
            mode = child.lstat().st_mode
            if stat.S_ISLNK(mode):
                raise ValueError(f"symbolic links are not allowed in release ZIPs: {child}")
            if not stat.S_ISREG(mode):
                raise ValueError(f"only regular files are allowed in release ZIPs: {child}")
            entries.append((archive_name(root, child), child))

    entries.sort(key=lambda entry: entry[0])
    return entries


def read_regular_file(path: Path) -> tuple[bytes, int]:
    flags = os.O_RDONLY
    flags |= getattr(os, "O_CLOEXEC", 0)
    flags |= getattr(os, "O_NOFOLLOW", 0)
    descriptor = os.open(path, flags)
    try:
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            raise ValueError(f"only regular files are allowed in release ZIPs: {path}")
        with os.fdopen(descriptor, "rb", closefd=False) as stream:
            return stream.read(), metadata.st_mode
    finally:
        os.close(descriptor)


def build_reproducible_zip(source: Path, target: Path) -> None:
    source = source.resolve(strict=True)
    if not source.is_dir():
        raise ValueError(f"ZIP source must be a directory: {source}")

    target = target.resolve(strict=False)
    try:
        target.relative_to(source)
    except ValueError:
        pass
    else:
        raise ValueError("ZIP target must be outside the source directory")

    entries = collect_regular_files(source)
    timestamp = canonical_zip_timestamp()
    target.parent.mkdir(parents=True, exist_ok=True)

    temporary_path: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            prefix=f".{target.name}.", suffix=".tmp", dir=target.parent, delete=False
        ) as temporary:
            temporary_path = Path(temporary.name)

        with zipfile.ZipFile(
            temporary_path,
            mode="w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=9,
            strict_timestamps=True,
        ) as archive:
            for name, path in entries:
                content, source_mode = read_regular_file(path)
                normalized_mode = 0o755 if source_mode & 0o111 else 0o644
                info = zipfile.ZipInfo(name, date_time=timestamp)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.create_system = 3
                info.external_attr = (stat.S_IFREG | normalized_mode) << 16
                info.extra = b""
                info.comment = b""
                archive.writestr(info, content, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)

        os.chmod(temporary_path, 0o644)
        os.replace(temporary_path, target)
        temporary_path = None
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="directory whose regular files become ZIP members")
    parser.add_argument("target", type=Path, help="output ZIP path (must be outside source)")
    args = parser.parse_args()

    try:
        build_reproducible_zip(args.source, args.target)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
