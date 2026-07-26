from __future__ import annotations

import contextlib
import hashlib
import json
import os
import subprocess
import threading
import zipfile
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest


REPO_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = REPO_ROOT / "scripts" / "publish-download-bundle-http.sh"


def write_bundle(root: Path) -> Path:
    files_dir = root / "files"
    files_dir.mkdir(parents=True, exist_ok=True)
    payload_name = "chummer-avalonia-win-x64-payload.zip"
    installer_name = "chummer-avalonia-win-x64-installer.exe"
    payload_path = files_dir / payload_name
    with zipfile.ZipFile(payload_path, "w") as archive:
        archive.writestr("Chummer.Avalonia.exe", b"placeholder")
    payload_bytes = payload_path.read_bytes()
    payload_sha256 = hashlib.sha256(payload_bytes).hexdigest()
    payload_url = f"https://example.invalid/downloads/files/{payload_name}"
    installer_path = files_dir / installer_name
    installer_path.write_bytes(
        b"installer-stub\n"
        + (b"installer-padding" * 200)
        + b"\nCHUMMER6_BOOTSTRAP_METADATA\n"
        + f"payloadDownloadUrl={payload_url}\n".encode()
        + f"payloadSha256={payload_sha256}\n".encode()
        + f"payloadSizeBytes={len(payload_bytes)}\n".encode()
    )
    artifact = {
        "artifactId": "avalonia-win-x64-installer",
        "head": "avalonia",
        "platform": "windows",
        "rid": "win-x64",
        "kind": "installer",
        "fileName": installer_name,
        "downloadUrl": f"https://example.invalid/downloads/files/{installer_name}",
        "sha256": hashlib.sha256(installer_path.read_bytes()).hexdigest(),
        "sizeBytes": installer_path.stat().st_size,
        "installerMode": "bootstrap",
        "payloadFileName": payload_name,
        "payloadDownloadUrl": payload_url,
        "payloadSha256": payload_sha256,
        "payloadSizeBytes": len(payload_bytes),
    }
    common = {"version": "run-test", "channel": "preview"}
    (root / "releases.json").write_text(
        json.dumps({**common, "downloads": [artifact]}),
        encoding="utf-8",
    )
    (root / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps({**common, "artifacts": [artifact]}),
        encoding="utf-8",
    )
    (files_dir / f"{payload_name}.json").write_text(
        json.dumps(
            {
                "contractName": "chummer6-ui.windows_bootstrap_payload",
                "fileName": payload_name,
                "downloadUrl": payload_url,
                "sha256": payload_sha256,
                "sizeBytes": len(payload_bytes),
                "installerFileName": installer_name,
                "releaseVersion": "run-test",
            }
        ),
        encoding="utf-8",
    )
    (files_dir / "notes.txt").write_text("release lane proof\n", encoding="utf-8")
    return root


def write_empty_bundle(root: Path) -> Path:
    files_dir = root / "files"
    files_dir.mkdir(parents=True, exist_ok=True)
    (root / "releases.json").write_text(json.dumps({"downloads": []}), encoding="utf-8")
    (root / "RELEASE_CHANNEL.generated.json").write_text(json.dumps({"artifacts": []}), encoding="utf-8")
    return root


@dataclass
class UploadRecorder:
    fail_sessions: bool = False
    session_posts: int = 0
    file_posts: int = 0
    chunk_posts: int = 0
    complete_posts: int = 0
    bundle_posts: int = 0
    paths: list[str] = field(default_factory=list)
    get_paths: list[str] = field(default_factory=list)
    auth_headers: list[str] = field(default_factory=list)
    session_payload_overrides: dict[str, str] = field(default_factory=dict)


@contextlib.contextmanager
def serve_upload_api(recorder: UploadRecorder):
    class Handler(BaseHTTPRequestHandler):
        def do_POST(self) -> None:  # noqa: N802
            recorder.paths.append(self.path)
            recorder.auth_headers.append(self.headers.get("Authorization", ""))
            body_length = int(self.headers.get("Content-Length", "0") or "0")
            if body_length > 0:
                self.rfile.read(body_length)

            if self.path == "/api/internal/releases/upload-sessions":
                recorder.session_posts += 1
                if recorder.fail_sessions:
                    self.send_response(500)
                    self.send_header("Content-Type", "application/json")
                    self.end_headers()
                    self.wfile.write(b'{"error":"session unavailable"}')
                    return

                payload = {
                    "sessionId": "session-1",
                    "filesUrl": "/api/internal/releases/upload-sessions/session-1/files",
                    "chunksUrl": "/api/internal/releases/upload-sessions/session-1/chunks",
                    "completeUrl": "/api/internal/releases/upload-sessions/session-1/complete",
                }
                payload.update(recorder.session_payload_overrides)
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps(payload).encode("utf-8"))
                return

            if self.path.endswith("/files"):
                recorder.file_posts += 1
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(b"{}")
                return

            if self.path.endswith("/chunks"):
                recorder.chunk_posts += 1
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(b"{}")
                return

            if self.path.endswith("/complete"):
                recorder.complete_posts += 1
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(b'{"status":"accepted","mode":"session"}')
                return

            if self.path == "/api/internal/releases/bundles":
                recorder.bundle_posts += 1
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(b'{"status":"accepted","mode":"direct"}')
                return

            self.send_response(404)
            self.end_headers()

        def do_GET(self) -> None:  # noqa: N802
            recorder.get_paths.append(self.path)
            self.send_response(200)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.end_headers()
            self.wfile.write(b"ok")

        def log_message(self, format: str, *args: object) -> None:  # noqa: A003
            return

    with ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://127.0.0.1:{server.server_address[1]}"
        finally:
            server.shutdown()
            thread.join(timeout=5)


def run_publish(bundle_root: Path, base_url: str, *, extra_env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    env = os.environ.copy()
    env.update(
        {
            "CHUMMER_RELEASE_UPLOAD_URL": f"{base_url}/api/internal/releases/bundles",
            "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL": f"{base_url}/api/internal/releases/upload-sessions",
            "CHUMMER_RELEASE_UPLOAD_CANONICAL_ORIGIN": base_url,
            "CHUMMER_RELEASE_UPLOAD_TEST_ALLOW_INSECURE_LOCALHOST": "1",
            "CHUMMER_RELEASE_UPLOAD_TOKEN": "test-token",
            "CHUMMER_RELEASE_UPLOAD_NON_INTERACTIVE": "1",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_MANIFEST": "0",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_WINDOWS_PAYLOADS": "0",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_ROUTES": "0",
        }
    )
    if extra_env:
        env.update(extra_env)
    return subprocess.run(
        ["bash", str(SCRIPT), str(bundle_root)],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )


def test_publish_download_bundle_http_uses_upload_sessions_when_available(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    recorder = UploadRecorder()

    with serve_upload_api(recorder) as base_url:
        result = run_publish(bundle_root, base_url)

    assert result.returncode == 0, result.stderr or result.stdout
    assert "windows_installer_payload_gate:ok checked=1" in result.stdout
    assert "Publishing 6 bundle files from" in result.stdout
    assert "Upload accepted." in result.stdout
    assert recorder.session_posts == 1
    assert recorder.file_posts == 6
    assert recorder.chunk_posts == 0
    assert recorder.complete_posts == 1
    assert recorder.bundle_posts == 0


def test_publish_download_bundle_http_falls_back_to_direct_bundle_upload(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    recorder = UploadRecorder(fail_sessions=True)

    with serve_upload_api(recorder) as base_url:
        result = run_publish(
            bundle_root,
            base_url,
            extra_env={"CHUMMER_RELEASE_UPLOAD_ALLOW_DIRECT_FALLBACK": "1"},
        )

    assert result.returncode == 0, result.stderr or result.stdout
    assert "Upload session creation failed; falling back to direct bundle upload." in result.stderr
    assert "Upload accepted." in result.stdout
    assert recorder.session_posts == 1
    assert recorder.file_posts == 0
    assert recorder.chunk_posts == 0
    assert recorder.complete_posts == 0
    assert recorder.bundle_posts == 1


def test_publish_download_bundle_http_disables_direct_fallback_by_default(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    recorder = UploadRecorder(fail_sessions=True)

    with serve_upload_api(recorder) as base_url:
        result = run_publish(bundle_root, base_url)

    assert result.returncode != 0
    assert "falling back to direct bundle upload" not in result.stderr
    assert recorder.session_posts == 1
    assert recorder.bundle_posts == 0


def test_publish_download_bundle_http_rejects_bundle_without_windows(tmp_path: Path) -> None:
    bundle_root = write_empty_bundle(tmp_path / "bundle")
    recorder = UploadRecorder()

    with serve_upload_api(recorder) as base_url:
        result = run_publish(bundle_root, base_url)

    assert result.returncode != 0
    assert "no Windows installers found" in result.stderr
    assert recorder.session_posts == 0


def test_publish_download_bundle_http_uses_chunk_uploads_for_large_files(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    (bundle_root / "files" / "notes.txt").write_bytes(b"x" * 150)
    recorder = UploadRecorder()

    with serve_upload_api(recorder) as base_url:
        result = run_publish(
            bundle_root,
            base_url,
            extra_env={
                "CHUMMER_RELEASE_UPLOAD_DIRECT_LIMIT_BYTES": "64",
                "CHUMMER_RELEASE_UPLOAD_CHUNK_BYTES": "50",
            },
        )

    assert result.returncode == 0, result.stderr or result.stdout
    assert "Upload accepted." in result.stdout
    assert recorder.session_posts == 1
    assert recorder.file_posts == 0
    assert recorder.chunk_posts >= 1
    assert recorder.complete_posts == 1
    assert recorder.bundle_posts == 0


def test_publish_download_bundle_http_reads_bearer_token_from_token_file(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    token_file = tmp_path / "upload-token.txt"
    token_file.write_text("file-token\n", encoding="utf-8")
    recorder = UploadRecorder()

    with serve_upload_api(recorder) as base_url:
        result = run_publish(
            bundle_root,
            base_url,
            extra_env={
                "CHUMMER_RELEASE_UPLOAD_TOKEN": "",
                "CHUMMER_RELEASE_UPLOAD_TOKEN_FILE": str(token_file),
            },
        )

    assert result.returncode == 0, result.stderr or result.stdout
    assert recorder.session_posts == 1
    assert recorder.auth_headers
    assert all(header == "Bearer file-token" for header in recorder.auth_headers if header)


def test_publish_download_bundle_http_verifies_routes_after_upload(tmp_path: Path) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    recorder = UploadRecorder()

    with serve_upload_api(recorder) as base_url:
        result = run_publish(
            bundle_root,
            base_url,
            extra_env={
                "CHUMMER_RELEASE_UPLOAD_VERIFY_ROUTES": "1",
                "CHUMMER_PUBLIC_BASE_URL": base_url,
            },
        )

    assert result.returncode == 0, result.stderr or result.stdout
    assert "Live publish verification completed." in result.stdout
    assert result.stdout.count("Verified route:") == 8
    assert recorder.get_paths == [
        "/downloads/install/avalonia-osx-arm64-installer",
        "/downloads/install/blazor-desktop-osx-arm64-installer",
        "/downloads/install/avalonia-win-x64-installer",
        "/downloads/install/blazor-desktop-win-x64-installer",
        "/downloads/install/avalonia-win-x64-installer/proof",
        "/downloads/install/blazor-desktop-win-x64-installer/proof",
        "/downloads/proof/windows/chummer-avalonia-win-x64-installer.exe",
        "/downloads/proof/windows/chummer-blazor-desktop-win-x64-installer.exe",
    ]


@pytest.mark.parametrize(
    "malicious_url",
    [
        "https://attacker.invalid/steal",
        "//attacker.invalid/steal",
        "http://chummer.run/api/internal/releases/upload-sessions/session-1/files",
        "https://user:password@chummer.run/api/internal/releases/upload-sessions/session-1/files",
        "https://chummer.run:443/api/internal/releases/upload-sessions/session-1/files",
        "https://chummer.run/api/internal/releases/upload-sessions/session-1/files#leak",
        "https://chummer.run/api/internal/releases/upload-sessions/session-1/files?next=evil",
        "/api/internal/releases/upload-sessions/%2e%2e/files",
        "/api/internal/releases/upload-sessions/session-1%2ffiles",
        "/api/internal/releases/upload-sessions/session-1/../files",
        "/api/internal/releases/upload-sessions/other-session/files",
    ],
)
def test_publish_download_bundle_http_rejects_untrusted_session_urls_before_bearer_use(
    tmp_path: Path,
    malicious_url: str,
) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    recorder = UploadRecorder(
        session_payload_overrides={"filesUrl": malicious_url}
    )

    with serve_upload_api(recorder) as base_url:
        result = run_publish(bundle_root, base_url)

    assert result.returncode != 0
    assert recorder.session_posts == 1
    assert recorder.file_posts == 0
    assert recorder.chunk_posts == 0
    assert recorder.complete_posts == 0
    assert "Invalid upload session filesUrl" in result.stderr


def test_publish_download_bundle_http_rejects_noncanonical_base_before_token_use(
    tmp_path: Path,
) -> None:
    bundle_root = write_bundle(tmp_path / "bundle")
    env = os.environ.copy()
    env.update(
        {
            "CHUMMER_RELEASE_UPLOAD_URL": "https://attacker.invalid/api/internal/releases/bundles",
            "CHUMMER_RELEASE_UPLOAD_SESSIONS_URL": "https://attacker.invalid/api/internal/releases/upload-sessions",
            "CHUMMER_RELEASE_UPLOAD_TOKEN": "must-not-leak",
            "CHUMMER_RELEASE_UPLOAD_NON_INTERACTIVE": "1",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_MANIFEST": "0",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_WINDOWS_PAYLOADS": "0",
            "CHUMMER_RELEASE_UPLOAD_VERIFY_ROUTES": "0",
        }
    )
    result = subprocess.run(
        ["bash", str(SCRIPT), str(bundle_root)],
        cwd=REPO_ROOT,
        env=env,
        capture_output=True,
        text=True,
        check=False,
    )
    assert result.returncode != 0
    assert "exact canonical upload origin" in result.stderr
    assert "must-not-leak" not in (result.stdout + result.stderr)
