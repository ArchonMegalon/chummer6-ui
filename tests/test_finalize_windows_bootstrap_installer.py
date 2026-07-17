from __future__ import annotations

import hashlib
import subprocess
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "finalize-windows-bootstrap-installer.py"
MARKER = b"\nCHUMMER6_BOOTSTRAP_METADATA\n"


def _write_config(
    path: Path,
    *,
    acquisition_mode: str = "embedded",
    embedded_path: str = "/work/payload.zip",
    actual_payload: bytes = b"payload",
    expected_payload: bytes = b"payload",
) -> None:
    lines = [
        '!define CHUMMER_PAYLOAD_FILE_NAME "payload.zip"',
        '!define CHUMMER_PAYLOAD_URL "https://example.invalid/downloads/payload.zip"',
        f'!define CHUMMER_PAYLOAD_SHA256 "{hashlib.sha256(expected_payload).hexdigest()}"',
        f'!define CHUMMER_PAYLOAD_SIZE_BYTES "{len(expected_payload)}"',
        f'!define CHUMMER_PAYLOAD_ACQUISITION_MODE "{acquisition_mode}"',
    ]
    if embedded_path:
        lines.append(f'!define CHUMMER_EMBEDDED_PAYLOAD_PATH "{embedded_path}"')
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    if acquisition_mode == "embedded" and embedded_path.startswith("/work/"):
        payload_path = path.parent / embedded_path.removeprefix("/work/")
        payload_path.parent.mkdir(parents=True, exist_ok=True)
        payload_path.write_bytes(actual_payload)


def _run(installer: Path, config: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--installer",
            str(installer),
            "--config",
            str(config),
        ],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )


def test_finalizer_appends_exact_literal_metadata_once_at_eof(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config)

    first = _run(installer, config)

    assert first.returncode == 0, first.stderr
    assert "windows_bootstrap_metadata:finalized" in first.stdout
    expected_trailer = MARKER + (
        "payloadFileName=payload.zip\n"
        "payloadDownloadUrl=https://example.invalid/downloads/payload.zip\n"
        f"payloadSha256={hashlib.sha256(b'payload').hexdigest()}\n"
        "payloadSizeBytes=7\n"
        "payloadAcquisitionMode=embedded\n"
    ).encode()
    first_bytes = installer.read_bytes()
    assert first_bytes.endswith(expected_trailer)

    second = _run(installer, config)

    assert second.returncode == 0, second.stderr
    assert "windows_bootstrap_metadata:already_finalized" in second.stdout
    assert installer.read_bytes() == first_bytes


def test_finalizer_refuses_embedded_config_without_embedded_payload_path(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config, embedded_path="")

    result = _run(installer, config)

    assert result.returncode == 1
    assert "missing CHUMMER_EMBEDDED_PAYLOAD_PATH" in result.stderr
    assert installer.read_bytes() == b"fresh-nsis-output"


def test_finalizer_refuses_conflicting_existing_trailer(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output" + MARKER + b"payloadAcquisitionMode=download\n")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config)

    result = _run(installer, config)

    assert result.returncode == 1
    assert "conflicting bootstrap metadata trailer" in result.stderr


def test_finalizer_refuses_embedded_payload_with_wrong_size_before_appending(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config, actual_payload=b"payload-too-long")

    result = _run(installer, config)

    assert result.returncode == 1
    assert "embedded payload size does not match" in result.stderr
    assert installer.read_bytes() == b"fresh-nsis-output"


def test_finalizer_refuses_embedded_payload_with_wrong_sha_before_appending(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config, actual_payload=b"payloae")

    result = _run(installer, config)

    assert result.returncode == 1
    assert "embedded payload SHA-256 does not match" in result.stderr
    assert installer.read_bytes() == b"fresh-nsis-output"


def test_finalizer_supports_download_config_without_local_payload(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config, acquisition_mode="download", embedded_path="")

    result = _run(installer, config)

    assert result.returncode == 0, result.stderr
    assert installer.read_bytes().endswith(b"payloadAcquisitionMode=download\n")


def test_finalizer_refuses_duplicate_exact_trailers(tmp_path: Path) -> None:
    installer = tmp_path / "installer.exe"
    installer.write_bytes(b"fresh-nsis-output")
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config)

    first = _run(installer, config)
    assert first.returncode == 0, first.stderr
    exact_trailer = installer.read_bytes().split(MARKER, 1)[1]
    with installer.open("ab") as stream:
        stream.write(MARKER + exact_trailer)
    duplicated = installer.read_bytes()

    result = _run(installer, config)

    assert result.returncode == 1
    assert "more than one bootstrap metadata trailer" in result.stderr
    assert installer.read_bytes() == duplicated


def test_payload_only_preflight_validates_embedded_bytes_without_touching_installer(tmp_path: Path) -> None:
    config = tmp_path / "bootstrap-config.nsh"
    _write_config(config)

    result = subprocess.run(
        ["python3", str(SCRIPT), "--config", str(config), "--validate-payload-only"],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
    )

    assert result.returncode == 0, result.stderr
    assert "windows_bootstrap_payload:validated:embedded" in result.stdout
    assert not (tmp_path / "installer.exe").exists()
