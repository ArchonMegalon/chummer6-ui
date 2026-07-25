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
def _running_portal(
    releases_dir: Path = PORTAL_DOWNLOADS_DIR,
    releases_file: Path = PORTAL_RELEASES_FILE,
):
    port = _find_free_port()
    base_url = f"http://127.0.0.1:{port}"
    log_path = REPO_ROOT / ".tmp" / f"portal-runtime-test-{port}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env["ASPNETCORE_ENVIRONMENT"] = "Development"
    env["DOTNET_ENVIRONMENT"] = "Development"
    env["ASPNETCORE_URLS"] = base_url
    env["CHUMMER_PORTAL_RELEASES_DIR"] = str(releases_dir)
    env["CHUMMER_PORTAL_RELEASES_FILE"] = str(releases_file)
    env["CHUMMER_PORTAL_IMPLICIT_OWNER"] = "runtime-test@chummer.run"

    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(PORTAL_PROJECT),
                "--no-launch-profile",
                "-p:ChummerUseLocalCompatibilityTree=true",
                (
                    "-p:ChummerLocalContractsProject="
                    + str(
                        (
                            REPO_ROOT
                            / "chummer-core-engine"
                            / "Chummer.Contracts"
                            / "Chummer.Contracts.csproj"
                        ).resolve()
                    )
                ),
            ],
            cwd=REPO_ROOT,
            env=env,
            stdout=log_file,
            stderr=subprocess.STDOUT,
        )

    try:
        deadline = time.time() + 90
        last_error = ""
        ready = False
        while time.time() < deadline:
            if process.poll() is not None:
                break

            try:
                _http_get(f"{base_url}/downloads/")
                ready = True
                break
            except Exception as exc:  # pragma: no cover - only used on boot retry
                last_error = str(exc)
                time.sleep(0.5)

        if not ready:
            log_text = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
            raise AssertionError(
                f"Portal did not become ready at {base_url}. Last error: {last_error}\n{log_text}"
            )

        yield base_url
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
    assert downloads_html.count('data-download-platform-card="') == 3
    assert 'data-download-platform-card="windows"' in downloads_html
    assert 'data-download-platform-card="linux"' in downloads_html
    assert 'data-download-platform-card="macos"' in downloads_html
    assert primary_download["fileName"] in downloads_html
    assert primary_download["url"] in downloads_html
    assert f'data-download-dispatch-url="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    expected_local_file = PORTAL_DOWNLOADS_DIR / "files" / primary_download["fileName"]
    expected_href = (
        f"/downloads/get/{primary_download['artifactId']}"
        if expected_local_file.is_file()
        else primary_download["url"]
    )
    assert f'href="{expected_href}"' in downloads_html
    assert f'data-download-install-route="/downloads/install/{primary_download["artifactId"]}"' in downloads_html
    assert 'data-download-action="download-artifact"' in downloads_html
    assert 'data-download-security-state="digest_published"' in downloads_html
    assert 'data-download-journey="clean-install"' in downloads_html
    assert 'data-download-journey="existing-install-update"' in downloads_html
    assert "Open <strong>Update Status</strong> inside Chummer" in downloads_html
    assert "proof-required" not in downloads_html
    assert "artifact id pending" not in downloads_html
    assert "docker compose" not in downloads_html
    assert 'data-download-manifest-link' in downloads_html

    assert manifest_version in status_html
    assert f"Platform coverage: {len(manifest_downloads)} of 3 desktop installers available." in status_html
    assert "data-portal-status-boundary=\"published-release-record\"" in status_html
    assert "Preview files are never counted as Stable downloads." in status_html

    assert 'data-portal-help-panel="handoff-guide"' in help_html
    assert 'aria-label="Help recovery actions"' in help_html
    assert 'data-portal-help-action="open-downloads"' in help_html
    assert 'data-portal-help-action="open-discord"' in help_html
    assert "/app?command=character_roster" in help_html

    assert 'data-portal-contact-action="open-discord"' in contact_html
    assert "The fastest human route is the Chummer Discord." in contact_html

    assert releases_json["version"] == manifest_version
    assert len(releases_json["downloads"]) == len(manifest_downloads)


def _download_row(
    platform: str,
    *,
    channel: str,
    version: str,
) -> dict[str, object]:
    platform_values = {
        "windows": ("win-x64", "x64", "exe"),
        "linux": ("linux-x64", "x64", "deb"),
        "macos": ("osx-arm64", "arm64", "dmg"),
    }
    rid, arch, file_format = platform_values[platform]
    artifact_id = f"avalonia-{rid}-installer"
    file_name = f"chummer-{artifact_id}.{file_format}"
    row: dict[str, object] = {
        "id": artifact_id,
        "artifactId": artifact_id,
        "head": "avalonia",
        "platform": platform,
        "platformId": f"{platform}-{arch}",
        "rid": rid,
        "arch": arch,
        "format": file_format,
        "kind": "installer",
        "flavor": "installer",
        "fileName": file_name,
        "url": f"https://chummer.run/downloads/files/{file_name}",
        "sha256": {"windows": "a", "linux": "b", "macos": "c"}[platform] * 64,
        "sizeBytes": {"windows": 2_900_000, "linux": 37_000_000, "macos": 48_000_000}[platform],
        "channel": channel,
        "channelId": channel,
        "version": version,
        "releaseVersion": version,
        "compatibilityState": "compatible",
        "installAccessClass": "open_public",
    }
    if platform == "macos":
        row["macosFlagshipEvidence"] = {
            "signingIdentity": {
                "developerIdApplicationIdentity": "Developer ID Application: Chummer (TEAMID1234)",
                "teamId": "TEAMID1234",
                "certificateSha256": "d" * 64,
                "certificateSpkiSha256": "e" * 64,
            },
            "notarization": {
                "status": "Accepted",
                "submissionId": "12345678-1234-4abc-8def-1234567890ab",
            },
        }
    return row


def _write_release_manifest(
    root: Path,
    *,
    channel: str,
    version: str,
    platforms: tuple[str, ...],
    release_profile: str | None = None,
) -> Path:
    root.mkdir(parents=True, exist_ok=True)
    payload: dict[str, object] = {
        "status": "published",
        "channel": channel,
        "channelId": channel,
        "rolloutState": channel,
        "version": version,
        "releaseVersion": version,
        "publishedAt": "2026-07-25T12:00:00Z",
        "downloads": [
            _download_row(platform, channel=channel, version=version)
            for platform in platforms
        ],
    }
    if release_profile:
        payload["releaseProfile"] = release_profile
    manifest_path = root / "releases.json"
    manifest_path.write_text(json.dumps(payload), encoding="utf-8")
    return manifest_path


def test_portal_runtime_renders_three_truthful_global_flagship_platform_cards(tmp_path: Path) -> None:
    release_root = tmp_path / "downloads"
    release_version = "run-20260725-120000"
    manifest_path = _write_release_manifest(
        release_root,
        channel="public_stable",
        version=release_version,
        platforms=("windows", "linux", "macos"),
        release_profile="global_flagship",
    )

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")

    assert downloads_html.count('data-download-availability="available"') == 3
    assert 'data-download-security-state="signed"' in downloads_html
    assert 'data-download-security-state="package_verified"' in downloads_html
    assert 'data-download-security-state="signed_notarized"' in downloads_html
    assert "Signed installer" in downloads_html
    assert "Native package and integrity verified" in downloads_html
    assert "Signed with Developer ID and notarized by Apple" in downloads_html
    assert "3 of 3 platforms available" in downloads_html
    assert f'data-portal-status-version="{release_version}"' in status_html
    assert "Platform coverage: 3 of 3 desktop installers available." in status_html


def test_portal_runtime_never_presents_preview_rows_as_stable_downloads(tmp_path: Path) -> None:
    release_root = tmp_path / "downloads"
    manifest_path = _write_release_manifest(
        release_root,
        channel="preview",
        version="preview-20260725-120000",
        platforms=("windows", "linux", "macos"),
    )

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")

    assert 'data-release-state="unavailable"' in downloads_html
    assert downloads_html.count('data-download-availability="available"') == 0
    assert downloads_html.count('data-download-action="download-unavailable"') == 3
    assert "No Stable desktop release is published right now." in downloads_html
    assert "https://chummer.run/downloads/files/" not in downloads_html
    assert 'data-portal-status-release-status="Unavailable"' in status_html


def test_portal_runtime_fails_closed_for_malformed_release_manifest(tmp_path: Path) -> None:
    release_root = tmp_path / "downloads"
    release_root.mkdir(parents=True)
    manifest_path = release_root / "releases.json"
    manifest_path.write_text('{"status":"published","downloads":[', encoding="utf-8")

    with _running_portal(release_root, manifest_path) as base_url:
        downloads_html = _http_get(f"{base_url}/downloads/")
        status_html = _http_get(f"{base_url}/status")

    assert 'data-release-state="unavailable"' in downloads_html
    assert downloads_html.count('data-download-availability="available"') == 0
    assert downloads_html.count('data-download-action="download-unavailable"') == 3
    assert "Release information could not be loaded." in downloads_html
    assert 'data-portal-status-release-status="Unavailable"' in status_html


def test_portal_runtime_withholds_global_macos_when_bound_evidence_is_invalid(
    tmp_path: Path,
) -> None:
    release_root = tmp_path / "downloads"
    release_version = "run-20260725-130000"
    manifest_path = _write_release_manifest(
        release_root,
        channel="public_stable",
        version=release_version,
        platforms=("windows", "linux", "macos"),
        release_profile="global_flagship",
    )
    valid_payload = json.loads(manifest_path.read_text(encoding="utf-8"))

    with _running_portal(release_root, manifest_path) as base_url:
        for evidence_case in (
            "absent",
            "malformed_identity",
            "wrong_team_binding",
            "missing_certificate_hash",
            "missing_spki_hash",
            "non_accepted_status",
            "malformed_submission_id",
        ):
            payload = json.loads(json.dumps(valid_payload))
            macos_row = next(
                row for row in payload["downloads"] if row["platform"] == "macos"
            )
            evidence = macos_row["macosFlagshipEvidence"]
            signing_identity = evidence["signingIdentity"]
            notarization = evidence["notarization"]
            macos_row["signingStatus"] = "passed"
            macos_row["notarizationStatus"] = "Accepted"

            if evidence_case == "absent":
                macos_row.pop("macosFlagshipEvidence")
            elif evidence_case == "malformed_identity":
                signing_identity["developerIdApplicationIdentity"] = "Developer ID Application: Chummer"
            elif evidence_case == "wrong_team_binding":
                signing_identity["teamId"] = "WRONGID123"
            elif evidence_case == "missing_certificate_hash":
                signing_identity.pop("certificateSha256")
            elif evidence_case == "missing_spki_hash":
                signing_identity.pop("certificateSpkiSha256")
            elif evidence_case == "non_accepted_status":
                notarization["status"] = "Rejected"
            elif evidence_case == "malformed_submission_id":
                notarization["submissionId"] = "NOT-A-LOWERCASE-UUID"

            manifest_path.write_text(json.dumps(payload), encoding="utf-8")
            downloads_html = _http_get(f"{base_url}/downloads/")

            assert (
                downloads_html.count('data-download-availability="available"') == 2
            ), evidence_case
            assert (
                'data-download-platform-card="macos" data-download-platform="macos" '
                'data-download-availability="unavailable"'
            ) in downloads_html, evidence_case
            assert (
                "Signed with Developer ID and notarized by Apple" not in downloads_html
            ), evidence_case


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
            follow_redirects=False,
        )

    assert status in {301, 302, 303, 307, 308}
    assert headers.get("Location") == expected_dispatch
    expected_local_file = PORTAL_DOWNLOADS_DIR / "files" / primary_download["fileName"]
    if expected_local_file.is_file():
        assert get_status in {200, 206}
        assert primary_download["fileName"] in get_headers.get("Content-Disposition", "")
    else:
        assert get_status in {301, 302, 303, 307, 308}
        assert get_headers.get("Location") == primary_download["url"]


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
