from __future__ import annotations

import contextlib
import hashlib
import http.server
import json
import socketserver
import subprocess
import threading
import zipfile
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "verify-live-windows-bootstrap-payloads.py"


class QuietHandler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, format: str, *args: object) -> None:
        return


@contextlib.contextmanager
def serve_directory(root: Path):
    class Handler(QuietHandler):
        def __init__(self, *args: object, **kwargs: object) -> None:
            super().__init__(*args, directory=str(root), **kwargs)

    with socketserver.TCPServer(("127.0.0.1", 0), Handler) as server:
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://127.0.0.1:{server.server_address[1]}"
        finally:
            server.shutdown()
            thread.join(timeout=5)


def write_payload(path: Path, *, launch_executable: str = "Chummer.Avalonia.exe") -> bytes:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(launch_executable, b"placeholder")
    return path.read_bytes()


def write_manifest(root: Path, base_url: str, *, payload_sha256: str, payload_size: int) -> None:
    payload_name = "chummer-avalonia-win-x64-payload.zip"
    installer_name = "chummer-avalonia-win-x64-installer.exe"
    payload_url = f"{base_url}/files/{payload_name}"
    manifest = {
        "downloads": [
            {
                "artifactId": "avalonia-win-x64-installer",
                "fileName": installer_name,
                "url": f"{base_url}/files/{installer_name}",
                "sha256": "a" * 64,
                "sizeBytes": 123,
                "platform": "windows",
                "kind": "installer",
                "installerMode": "bootstrap",
                "payloadFileName": payload_name,
                "payloadDownloadUrl": payload_url,
                "payloadSha256": payload_sha256,
                "payloadSizeBytes": payload_size,
            }
        ]
    }
    (root / "RELEASE_CHANNEL.generated.json").write_text(json.dumps(manifest), encoding="utf-8")
    (root / "files" / f"{payload_name}.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": payload_size,
                "installerFileName": installer_name,
                "releaseVersion": "run-test",
            }
        ),
        encoding="utf-8",
    )


def run_verifier(manifest_url: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [
            "python3",
            str(SCRIPT),
            "--manifest-url",
            manifest_url,
            "--timeout",
            "10",
        ],
        text=True,
        capture_output=True,
        check=False,
    )


def test_live_windows_bootstrap_payload_gate_accepts_exact_live_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    payload_bytes = write_payload(files_dir / "chummer-avalonia-win-x64-payload.zip")
    (files_dir / "chummer-avalonia-win-x64-installer.exe").write_bytes(b"installer")
    with serve_directory(tmp_path) as base_url:
        write_manifest(
            tmp_path,
            base_url,
            payload_sha256=hashlib.sha256(payload_bytes).hexdigest(),
            payload_size=len(payload_bytes),
        )

        result = run_verifier(f"{base_url}/RELEASE_CHANNEL.generated.json")

    assert result.returncode == 0, result.stderr
    assert "live_windows_bootstrap_payloads:ok checked=1" in result.stdout


def test_live_windows_bootstrap_payload_gate_rejects_missing_live_payload(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    payload_path = files_dir / "chummer-avalonia-win-x64-payload.zip"
    payload_bytes = write_payload(payload_path)
    payload_path.unlink()
    with serve_directory(tmp_path) as base_url:
        write_manifest(
            tmp_path,
            base_url,
            payload_sha256=hashlib.sha256(payload_bytes).hexdigest(),
            payload_size=len(payload_bytes),
        )

        result = run_verifier(f"{base_url}/RELEASE_CHANNEL.generated.json")

    assert result.returncode != 0
    assert "live_windows_bootstrap_payloads:fail" in result.stderr
    assert "HTTP Error 404" in result.stderr


def test_live_windows_bootstrap_payload_gate_rejects_sha_drift(tmp_path: Path) -> None:
    files_dir = tmp_path / "files"
    files_dir.mkdir()
    payload_bytes = write_payload(files_dir / "chummer-avalonia-win-x64-payload.zip")
    with serve_directory(tmp_path) as base_url:
        write_manifest(
            tmp_path,
            base_url,
            payload_sha256="0" * 64,
            payload_size=len(payload_bytes),
        )

        result = run_verifier(f"{base_url}/RELEASE_CHANNEL.generated.json")

    assert result.returncode != 0
    assert "live payload sha256 mismatch" in result.stderr


def test_http_publish_script_runs_live_windows_payload_gate() -> None:
    text = (REPO_ROOT / "scripts" / "publish-download-bundle-http.sh").read_text(encoding="utf-8")

    assert 'VERIFY_WINDOWS_PAYLOADS="${CHUMMER_RELEASE_UPLOAD_VERIFY_WINDOWS_PAYLOADS:-1}"' in text
    assert 'python3 "$SCRIPT_DIR/verify-live-windows-bootstrap-payloads.py" \\' in text
    assert '--manifest-url "$VERIFY_URL"' in text
    assert text.index('bash "$SCRIPT_DIR/verify-releases-manifest.sh" "$VERIFY_URL"') < text.index(
        'python3 "$SCRIPT_DIR/verify-live-windows-bootstrap-payloads.py" \\'
    )
