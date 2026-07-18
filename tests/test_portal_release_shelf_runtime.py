from __future__ import annotations

import json
import os
import socket
import subprocess
import time
import urllib.request
from contextlib import contextmanager
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PORTAL_PROJECT = REPO_ROOT / "Chummer.Portal" / "Chummer.Portal.csproj"
PORTAL_DOWNLOADS_DIR = REPO_ROOT / "Chummer.Portal" / "downloads"
PORTAL_RELEASES_FILE = PORTAL_DOWNLOADS_DIR / "releases.json"


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as handle:
        handle.bind(("127.0.0.1", 0))
        return int(handle.getsockname()[1])


def _http_get(url: str) -> str:
    with urllib.request.urlopen(url, timeout=5) as response:
        return response.read().decode("utf-8")


class _NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[override]
        return None

    def http_error_301(self, req, fp, code, msg, headers):  # type: ignore[override]
        return fp

    http_error_302 = http_error_303 = http_error_307 = http_error_308 = http_error_301


def _http_request(
    url: str,
    *,
    method: str = "GET",
    headers: dict[str, str] | None = None,
    follow_redirects: bool = True,
) -> tuple[int, dict[str, str], bytes]:
    request = urllib.request.Request(url, method=method, headers=headers or {})
    opener = urllib.request.build_opener() if follow_redirects else urllib.request.build_opener(_NoRedirectHandler())
    with opener.open(request, timeout=5) as response:
        return int(response.status), dict(response.headers.items()), response.read()


@contextmanager
def _running_portal():
    port = _find_free_port()
    base_url = f"http://127.0.0.1:{port}"
    log_path = REPO_ROOT / ".tmp" / f"portal-runtime-test-{port}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env["ASPNETCORE_URLS"] = base_url
    env["CHUMMER_PORTAL_RELEASES_DIR"] = str(PORTAL_DOWNLOADS_DIR)
    env["CHUMMER_PORTAL_RELEASES_FILE"] = str(PORTAL_RELEASES_FILE)
    env["CHUMMER_PORTAL_IMPLICIT_OWNER"] = "runtime-test@chummer.run"

    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(PORTAL_PROJECT),
                "--no-launch-profile",
            ],
            cwd=REPO_ROOT,
            env=env,
            stdout=log_file,
            stderr=subprocess.STDOUT,
        )

    try:
        deadline = time.time() + 45
        last_error = ""
        while time.time() < deadline:
            if process.poll() is not None:
                break

            try:
                _http_get(f"{base_url}/downloads/")
                yield base_url
                return
            except Exception as exc:  # pragma: no cover - only used on boot retry
                last_error = str(exc)
                time.sleep(0.5)

        log_text = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
        raise AssertionError(
            f"Portal did not become ready at {base_url}. Last error: {last_error}\n{log_text}"
        )
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)


def test_portal_runtime_renders_release_shelf_help_and_status_from_local_manifest() -> None:
    manifest = json.loads(PORTAL_RELEASES_FILE.read_text(encoding="utf-8"))
    manifest_version = manifest["version"]
    manifest_downloads = manifest["downloads"]

    assert manifest_downloads, "Expected the checked-in local releases manifest to expose at least one download row."

    primary_download = manifest_downloads[0]

    with _running_portal() as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")
        help_html = _http_get(f"{base_url}/help")
        contact_html = _http_get(f"{base_url}/contact")
        releases_json = json.loads(_http_get(f"{base_url}/downloads/releases.json"))

    assert 'data-download-list="published-artifacts"' in downloads_html
    assert 'data-self-host-downloads-panel="docker-operator"' in downloads_html
    assert primary_download["fileName"] in downloads_html
    assert primary_download["platform"] in downloads_html
    assert primary_download["url"] in downloads_html
    assert f'data-download-dispatch-url="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert f'href="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert f'data-download-install-route="/downloads/install/{primary_download["artifactId"]}"' in downloads_html
    assert 'data-download-link-mode="self-host-dispatch"' in downloads_html
    assert "Published artifacts stay on this self-hosted edge when local bytes are mounted here." in downloads_html
    assert "RELEASE_CHANNEL.generated.json" in downloads_html
    assert 'data-self-host-release-manifest="/downloads/releases.json"' in downloads_html

    assert manifest_version in status_html
    assert f'Published files: <code>{len(manifest_downloads)}</code>' in status_html
    assert "data-portal-status-boundary=\"source-manifest-backed\"" in status_html

    assert 'data-portal-help-panel="handoff-guide"' in help_html
    assert 'aria-label="Help recovery actions"' in help_html
    assert 'data-portal-help-action="open-downloads"' in help_html
    assert 'data-portal-help-action="open-discord"' in help_html
    assert "/app?command=character_roster" in help_html

    assert 'data-portal-contact-action="open-discord"' in contact_html
    assert "The fastest human route is the Chummer Discord." in contact_html

    assert releases_json["version"] == manifest_version
    assert len(releases_json["downloads"]) == len(manifest_downloads)


def test_portal_runtime_home_links_to_truthful_contact_handoff() -> None:
    with _running_portal() as base_url:
        home_html = _http_get(f"{base_url}/")

    assert 'href="/contact"' in home_html
    assert 'data-portal-home-route="contact"' in home_html
    assert "Contact support" in home_html


def test_portal_runtime_keeps_open_public_installer_handoffs_on_the_self_hosted_edge() -> None:
    manifest = json.loads(PORTAL_RELEASES_FILE.read_text(encoding="utf-8"))
    primary_download = next(
        row for row in manifest["downloads"] if row.get("installAccessClass") == "open_public"
    )
    artifact_id = primary_download["artifactId"]
    expected_dispatch = f"/downloads/get/{artifact_id}"

    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/downloads/install/{artifact_id}",
            follow_redirects=False,
        )
        get_status, get_headers, _ = _http_request(
            f"{base_url}{expected_dispatch}",
            headers={"Range": "bytes=0-0"},
        )

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == expected_dispatch
    assert get_status in {200, 206}
    assert primary_download["fileName"] in get_headers.get("Content-Disposition", "")


def test_portal_runtime_redirects_public_app_route_to_hosted_blazor_app_and_preserves_query() -> None:
    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/app?command=character_roster",
            follow_redirects=False,
        )
        slash_status, slash_headers, _ = _http_request(
            f"{base_url}/app/?command=new_character_origin",
            follow_redirects=False,
        )
        openapi = json.loads(_http_get(f"{base_url}/openapi/v1.json"))

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == "/blazor/app?command=character_roster"
    assert slash_status in {301, 302, 303, 307, 308}
    assert slash_headers.get("Location") == "/blazor/app?command=new_character_origin"
    assert isinstance(openapi.get("paths", {}).get("/app"), dict)
    assert isinstance(openapi.get("paths", {}).get("/blazor/app"), dict)


def test_portal_runtime_redirects_public_online_alias_to_hosted_blazor_app_and_preserves_query() -> None:
    with _running_portal() as base_url:
        status, headers, _ = _http_request(
            f"{base_url}/online?command=character_roster",
            follow_redirects=False,
        )
        slash_status, slash_headers, _ = _http_request(
            f"{base_url}/online/?command=new_character_origin",
            follow_redirects=False,
        )
        openapi = json.loads(_http_get(f"{base_url}/openapi/v1.json"))

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == "/blazor/app?command=character_roster"
    assert slash_status in {301, 302, 303, 307, 308}
    assert slash_headers.get("Location") == "/blazor/app?command=new_character_origin"
    assert isinstance(openapi.get("paths", {}).get("/online"), dict)
    assert isinstance(openapi.get("paths", {}).get("/blazor/app"), dict)
