from __future__ import annotations

import hashlib
import os
from pathlib import Path
import stat
import subprocess
import sys
import time
import zipfile

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
ZIP_BUILDER = REPO_ROOT / "scripts" / "build-reproducible-zip.py"


def build_zip(source: Path, target: Path, *, source_date_epoch: str | None = None) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    if source_date_epoch is None:
        environment.pop("SOURCE_DATE_EPOCH", None)
    else:
        environment["SOURCE_DATE_EPOCH"] = source_date_epoch
    return subprocess.run(
        [sys.executable, str(ZIP_BUILDER), str(source), str(target)],
        check=False,
        capture_output=True,
        text=True,
        env=environment,
    )


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def test_rebuild_is_byte_identical_after_source_mtimes_change(tmp_path: Path) -> None:
    source = tmp_path / "payload"
    (source / "z").mkdir(parents=True)
    (source / "z" / "last.txt").write_text("last\n", encoding="utf-8")
    (source / "first.txt").write_text("first\n", encoding="utf-8")
    first_zip = tmp_path / "first.zip"
    second_zip = tmp_path / "second.zip"

    assert build_zip(source, first_zip).returncode == 0
    future = time.time() + 86_400
    for path in source.rglob("*"):
        os.utime(path, (future, future), follow_symlinks=False)
    assert build_zip(source, second_zip).returncode == 0

    assert sha256(first_zip) == sha256(second_zip)
    assert first_zip.read_bytes() == second_zip.read_bytes()


def test_members_are_sorted_and_preserve_content_and_executable_posture(tmp_path: Path) -> None:
    source = tmp_path / "payload"
    source.mkdir()
    executable = source / "run.sh"
    executable.write_bytes(b"#!/bin/sh\nexit 0\n")
    executable.chmod(0o751)
    nested = source / "a" / "data.txt"
    nested.parent.mkdir()
    nested.write_bytes(b"payload\x00bytes")
    target = tmp_path / "payload.zip"

    result = build_zip(source, target)

    assert result.returncode == 0, result.stderr
    with zipfile.ZipFile(target) as archive:
        assert archive.namelist() == ["a/data.txt", "run.sh"]
        assert archive.read("a/data.txt") == b"payload\x00bytes"
        assert archive.read("run.sh") == b"#!/bin/sh\nexit 0\n"
        assert stat.S_IMODE(archive.getinfo("a/data.txt").external_attr >> 16) == 0o644
        assert stat.S_IMODE(archive.getinfo("run.sh").external_attr >> 16) == 0o755


def test_source_date_epoch_controls_one_canonical_even_second_timestamp(tmp_path: Path) -> None:
    source = tmp_path / "payload"
    source.mkdir()
    (source / "file.txt").write_text("content", encoding="utf-8")
    target = tmp_path / "payload.zip"

    result = build_zip(source, target, source_date_epoch="1704067201")

    assert result.returncode == 0, result.stderr
    with zipfile.ZipFile(target) as archive:
        assert archive.getinfo("file.txt").date_time == (2024, 1, 1, 0, 0, 0)


def test_symlink_is_rejected(tmp_path: Path) -> None:
    source = tmp_path / "payload"
    source.mkdir()
    real_file = source / "real.txt"
    real_file.write_text("content", encoding="utf-8")
    try:
        (source / "alias.txt").symlink_to(real_file)
    except (NotImplementedError, OSError):
        pytest.skip("symlinks are unavailable on this host")

    result = build_zip(source, tmp_path / "payload.zip")

    assert result.returncode != 0
    assert "symbolic links are not allowed" in result.stderr


def test_invalid_source_date_epoch_fails_closed(tmp_path: Path) -> None:
    source = tmp_path / "payload"
    source.mkdir()
    (source / "file.txt").write_text("content", encoding="utf-8")

    result = build_zip(source, tmp_path / "payload.zip", source_date_epoch="not-an-integer")

    assert result.returncode != 0
    assert "SOURCE_DATE_EPOCH must be an integer" in result.stderr
