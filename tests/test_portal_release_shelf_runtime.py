from __future__ import annotations

import json
import os
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.request
from contextlib import contextmanager
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
PORTAL_PROJECT = REPO_ROOT / "Chummer.Portal" / "Chummer.Portal.csproj"
PORTAL_DOWNLOADS_DIR = REPO_ROOT / "Chummer.Portal" / "downloads"
PORTAL_RELEASES_FILE = PORTAL_DOWNLOADS_DIR / "releases.json"
PORTAL_PLAY_SURFACE_RECEIPT = (
    PORTAL_DOWNLOADS_DIR / "release-evidence" / "browser-lane" / "BLAZOR_PLAY_SURFACE_HORIZON.generated.json"
)


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
    downloads_dir: Path = PORTAL_DOWNLOADS_DIR,
    *,
    path_base: str = "",
    downloads_proxy_url: str = "",
):
    port = _find_free_port()
    base_url = f"http://127.0.0.1:{port}"
    log_path = REPO_ROOT / ".tmp" / f"portal-runtime-test-{port}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env["ASPNETCORE_URLS"] = base_url
    env["CHUMMER_PORTAL_RELEASES_DIR"] = str(downloads_dir)
    env["CHUMMER_PORTAL_RELEASES_FILE"] = str(downloads_dir / "releases.json")
    env["CHUMMER_PORTAL_IMPLICIT_OWNER"] = "runtime-test@chummer.run"
    env["CHUMMER_PORTAL_OWNER_SHARED_KEY"] = secrets.token_urlsafe(48)
    if path_base:
        env["CHUMMER_PORTAL_PATH_BASE"] = path_base
    if downloads_proxy_url:
        env["CHUMMER_PORTAL_DOWNLOADS_PROXY_URL"] = downloads_proxy_url

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
                _http_get(f"{base_url}{path_base}/downloads/")
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
    play_surface = json.loads(PORTAL_PLAY_SURFACE_RECEIPT.read_text(encoding="utf-8"))
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
        play_surface_json = json.loads(
            _http_get(f"{base_url}/downloads/release-evidence/browser-lane/BLAZOR_PLAY_SURFACE_HORIZON.generated.json")
        )

    assert 'data-download-list="published-artifacts"' in downloads_html
    assert 'data-self-host-downloads-panel="docker-operator"' in downloads_html
    assert primary_download["fileName"] in downloads_html
    assert primary_download["platform"] in downloads_html
    assert primary_download["url"] in downloads_html
    assert f'data-download-dispatch-url="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert f'href="/downloads/get/{primary_download["artifactId"]}"' in downloads_html
    assert f'data-download-install-route="/downloads/install/{primary_download["artifactId"]}"' in downloads_html
    assert 'data-download-link-mode="self-host-dispatch"' in downloads_html
    assert 'id="published-download-description"' in downloads_html
    assert 'data-download-description' in downloads_html
    assert "RELEASE_CHANNEL.generated.json" in downloads_html

    assert manifest_version in status_html
    assert f'Published files: <code>{len(manifest_downloads)}</code>' in status_html
    assert "data-portal-status-boundary=\"source-manifest-backed\"" in status_html
    assert 'data-portal-status-panel="play-surface-horizons"' in status_html
    assert 'data-portal-play-surface-grid="horizons"' in status_html
    assert 'data-portal-play-surface-grid="route-truth"' in status_html
    assert 'data-portal-play-surface-action="open-receipt"' in status_html
    assert "BLAZOR_PLAY_SURFACE_HORIZON.generated.json" in status_html
    assert play_surface["current_release_truth"]["current_execution_scope"] in status_html
    assert f'data-portal-play-surface-route="public-entry"' in status_html
    assert f'data-portal-play-surface-route="public-roster-entry"' in status_html
    assert f'data-portal-play-surface-route="hosted-app"' in status_html
    assert f'data-portal-play-surface-route="compatibility-route"' in status_html
    assert f'data-portal-play-surface-route="execution-route"' in status_html
    assert play_surface["current_release_truth"]["public_entry_route"] in status_html
    assert play_surface["current_release_truth"]["public_roster_entry_route"] in status_html
    assert play_surface["current_release_truth"]["hosted_app_route"] in status_html
    assert play_surface["current_release_truth"]["compatibility_route_base"] in status_html
    assert play_surface["current_release_truth"]["execution_route_base"] in status_html

    for horizon in play_surface["horizons"]:
        assert f'data-play-surface-horizon-id="{horizon["id"]}"' in status_html
        assert horizon["title"] in status_html
        assert horizon["headline"] in status_html
        for receipt in horizon.get("runtime_proven_receipts", []):
            assert receipt["label"] in status_html
            relative_path = receipt.get("public_download_relative_path")
            if relative_path:
                assert f'href="/downloads/{relative_path}"' in status_html
        for receipt in horizon.get("source_staged_receipts", []):
            assert receipt["label"] in status_html
            relative_path = receipt.get("public_download_relative_path")
            if relative_path:
                assert f'href="/downloads/{relative_path}"' in status_html
        for document in horizon.get("documentation_sources", []):
            assert document["label"] in status_html
            assert 'href="/docs/"' in status_html
        for claim in horizon.get("unproven_claims", []):
            assert claim in status_html
        for boundary in horizon.get("server_bound_boundaries", []):
            assert boundary in status_html

    assert 'data-play-surface-boundary-group="unproven-claims"' in status_html
    assert 'data-play-surface-boundary-group="server-bound-boundaries"' in status_html

    assert 'data-portal-help-panel="handoff-guide"' in help_html
    assert 'aria-label="Help recovery actions"' in help_html
    assert 'data-portal-help-action="open-downloads"' in help_html
    assert 'data-portal-help-action="open-discord"' in help_html
    assert "/app?command=character_roster" in help_html

    assert 'data-portal-contact-action="open-discord"' in contact_html
    assert "The fastest human route is the Chummer Discord." in contact_html

    assert releases_json["version"] == manifest_version
    assert len(releases_json["downloads"]) == len(manifest_downloads)
    assert play_surface_json["contract_name"] == play_surface["contract_name"]
    assert play_surface_json["horizons"][0]["id"] == play_surface["horizons"][0]["id"]


def test_portal_runtime_floors_stale_raw_canonical_manifest_before_static_serving(tmp_path: Path) -> None:
    downloads_dir = tmp_path / "downloads"
    downloads_dir.mkdir()
    (downloads_dir / "releases.json").write_text(
        json.dumps({"status": "published", "version": "run-static-floor", "downloads": []}) + "\n",
        encoding="utf-8",
    )
    source_manifest = {
        "contractName": "chummer.release_channel.v1",
        "status": "published",
        "version": "run-static-floor",
        "publishedAt": "2026-07-13T12:34:56Z",
        "channelId": "preview",
        "rolloutState": "promoted_preview",
        "rolloutReason": "Current release shelf passed the local release run before publication.",
        "supportabilityState": "preview_supported",
        "artifacts": [
            {
                "artifactId": "avalonia-osx-arm64-installer",
                "fileName": "chummer-avalonia-osx-arm64-installer.dmg",
                "sha256": "a" * 64,
                "sizeBytes": 123,
            }
        ],
        "publicTrustMetrics": {
            "proofFreshness": {"status": "stale", "summary": "fixture remains stale"},
            "releaseChannel": {
                "rolloutState": "promoted_preview",
                "supportabilityState": "preview_supported",
                "posture": "preview",
            },
        },
        "registryBoundaryCoverage": {
            "releaseChannel": {
                "rolloutState": "promoted_preview",
                "supportabilityState": "preview_supported",
                "publicTrustPosture": "preview",
            }
        },
    }
    (downloads_dir / "RELEASE_CHANNEL.generated.json").write_text(
        json.dumps(source_manifest, indent=2) + "\n",
        encoding="utf-8",
    )

    with _running_portal(
        downloads_dir,
        path_base="/portal",
        downloads_proxy_url="http://127.0.0.1:9",
    ) as base_url:
        status, headers, body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )
        head_status, head_headers, head_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json",
            method="HEAD",
        )

        missing_manifest = json.loads(json.dumps(source_manifest))
        missing_manifest["publicTrustMetrics"]["proofFreshness"]["status"] = "missing"
        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_text(
            json.dumps(missing_manifest, indent=2) + "\n",
            encoding="utf-8",
        )
        _, _, missing_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )

        unknown_manifest = json.loads(json.dumps(source_manifest))
        unknown_manifest["publicTrustMetrics"]["proofFreshness"]["status"] = "unrecognized"
        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_text(
            json.dumps(unknown_manifest, indent=2) + "\n",
            encoding="utf-8",
        )
        _, _, unknown_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )

        absent_manifest = json.loads(json.dumps(source_manifest))
        del absent_manifest["publicTrustMetrics"]["proofFreshness"]
        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_text(
            json.dumps(absent_manifest, indent=2) + "\n",
            encoding="utf-8",
        )
        _, _, absent_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )

        fresh_manifest = json.loads(json.dumps(source_manifest))
        fresh_manifest["publicTrustMetrics"]["proofFreshness"]["status"] = "fresh"
        fresh_bytes = (json.dumps(fresh_manifest, indent=2) + "\n").encode("utf-8")
        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_bytes(fresh_bytes)
        _, _, fresh_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )

        unpublished_manifest = json.loads(json.dumps(source_manifest))
        unpublished_manifest["status"] = "unpublished"
        unpublished_manifest["rolloutState"] = "unpublished"
        unpublished_manifest["supportabilityState"] = "unpublished"
        unpublished_manifest["publicTrustMetrics"]["releaseChannel"]["rolloutState"] = "unpublished"
        unpublished_manifest["publicTrustMetrics"]["releaseChannel"]["supportabilityState"] = "unpublished"
        unpublished_manifest["registryBoundaryCoverage"]["releaseChannel"]["rolloutState"] = "unpublished"
        unpublished_manifest["registryBoundaryCoverage"]["releaseChannel"]["supportabilityState"] = "unpublished"
        unpublished_bytes = (json.dumps(unpublished_manifest, indent=2) + "\n").encode("utf-8")
        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_bytes(unpublished_bytes)
        _, _, unpublished_body = _http_request(
            f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json"
        )

        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_text("{not-json\n", encoding="utf-8")
        try:
            _http_request(f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json")
            raise AssertionError("Malformed canonical manifest should fail closed.")
        except urllib.error.HTTPError as error:
            malformed_status = int(error.code)
            malformed_headers = dict(error.headers.items())
            malformed_body = error.read()

        (downloads_dir / "RELEASE_CHANNEL.generated.json").write_bytes(
            b'{"status":"published","note":"\xff"}\n'
        )
        try:
            _http_request(f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json")
            raise AssertionError("Invalid UTF-8 in the canonical manifest should fail closed.")
        except urllib.error.HTTPError as error:
            invalid_utf8_status = int(error.code)
            invalid_utf8_headers = dict(error.headers.items())
            invalid_utf8_body = error.read()
        try:
            _http_request(
                f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json",
                method="HEAD",
            )
            raise AssertionError("Invalid UTF-8 HEAD requests should fail closed.")
        except urllib.error.HTTPError as error:
            invalid_utf8_head_status = int(error.code)
            invalid_utf8_head_headers = dict(error.headers.items())
            invalid_utf8_head_body = error.read()

        (downloads_dir / "RELEASE_CHANNEL.generated.json").unlink()
        try:
            _http_request(f"{base_url}/portal/downloads/RELEASE_CHANNEL.generated.json")
            raise AssertionError("Missing canonical manifest should fail closed before proxy fallback.")
        except urllib.error.HTTPError as error:
            missing_file_status = int(error.code)
            missing_file_headers = dict(error.headers.items())
            missing_file_body = error.read()

    served_manifest = json.loads(body)
    missing_served_manifest = json.loads(missing_body)
    unknown_served_manifest = json.loads(unknown_body)
    absent_served_manifest = json.loads(absent_body)
    assert status == 200
    assert served_manifest["version"] == source_manifest["version"]
    assert served_manifest["publishedAt"] == source_manifest["publishedAt"]
    assert served_manifest["artifacts"] == source_manifest["artifacts"]
    assert served_manifest["publicTrustMetrics"]["proofFreshness"] == source_manifest["publicTrustMetrics"]["proofFreshness"]
    assert served_manifest["supportabilityState"] == "review_required"
    assert served_manifest["rolloutState"] == "public_release_review_required"
    assert served_manifest["rolloutReason"] == (
        "Current shelf is published, but release posture stays review-required because stale or incomplete "
        "proof receipts must be refreshed before widening launch-readiness claims."
    )
    assert served_manifest["publicTrustMetrics"]["releaseChannel"]["supportabilityState"] == "review_required"
    assert served_manifest["publicTrustMetrics"]["releaseChannel"]["posture"] == "blocked"
    assert served_manifest["registryBoundaryCoverage"]["releaseChannel"]["supportabilityState"] == "review_required"
    assert served_manifest["registryBoundaryCoverage"]["releaseChannel"]["publicTrustPosture"] == "blocked"
    assert missing_served_manifest["supportabilityState"] == "review_required"
    assert missing_served_manifest["publicTrustMetrics"]["releaseChannel"]["supportabilityState"] == "review_required"
    assert missing_served_manifest["publicTrustMetrics"]["releaseChannel"]["posture"] == "blocked"
    assert missing_served_manifest["registryBoundaryCoverage"]["releaseChannel"]["supportabilityState"] == "review_required"
    assert missing_served_manifest["registryBoundaryCoverage"]["releaseChannel"]["publicTrustPosture"] == "blocked"
    assert unknown_served_manifest["publicTrustMetrics"]["proofFreshness"]["status"] == "unrecognized"
    assert unknown_served_manifest["supportabilityState"] == "review_required"
    assert unknown_served_manifest["registryBoundaryCoverage"]["releaseChannel"]["publicTrustPosture"] == "blocked"
    assert absent_served_manifest["publicTrustMetrics"]["proofFreshness"]["status"] == "missing"
    assert absent_served_manifest["supportabilityState"] == "review_required"
    assert absent_served_manifest["registryBoundaryCoverage"]["releaseChannel"]["publicTrustPosture"] == "blocked"
    assert fresh_body == fresh_bytes
    assert unpublished_body == unpublished_bytes
    assert head_status == 200
    assert head_body == b""
    assert int(head_headers["Content-Length"]) > 0
    assert malformed_status == 503
    assert json.loads(malformed_body) == {"status": "manifest_unavailable"}
    assert invalid_utf8_status == 503
    assert json.loads(invalid_utf8_body) == {"status": "manifest_unavailable"}
    assert invalid_utf8_head_status == 503
    assert invalid_utf8_head_body == b""
    assert int(invalid_utf8_head_headers["Content-Length"]) > 0
    assert missing_file_status == 503
    assert json.loads(missing_file_body) == {"status": "manifest_unavailable"}
    assert "no-store" in headers["Cache-Control"]
    normalized_headers = {key.lower(): value for key, value in headers.items()}
    assert normalized_headers["cdn-cache-control"] == "no-store"
    assert normalized_headers["cloudflare-cdn-cache-control"] == "no-store"
    malformed_normalized_headers = {key.lower(): value for key, value in malformed_headers.items()}
    assert "no-store" in malformed_normalized_headers["cache-control"]
    assert malformed_normalized_headers["cdn-cache-control"] == "no-store"
    assert malformed_normalized_headers["cloudflare-cdn-cache-control"] == "no-store"
    invalid_utf8_normalized_headers = {key.lower(): value for key, value in invalid_utf8_headers.items()}
    assert "no-store" in invalid_utf8_normalized_headers["cache-control"]
    assert invalid_utf8_normalized_headers["cdn-cache-control"] == "no-store"
    assert invalid_utf8_normalized_headers["cloudflare-cdn-cache-control"] == "no-store"
    invalid_utf8_head_normalized_headers = {key.lower(): value for key, value in invalid_utf8_head_headers.items()}
    assert "no-store" in invalid_utf8_head_normalized_headers["cache-control"]
    assert invalid_utf8_head_normalized_headers["cdn-cache-control"] == "no-store"
    assert invalid_utf8_head_normalized_headers["cloudflare-cdn-cache-control"] == "no-store"
    missing_file_normalized_headers = {key.lower(): value for key, value in missing_file_headers.items()}
    assert "no-store" in missing_file_normalized_headers["cache-control"]
    assert missing_file_normalized_headers["cdn-cache-control"] == "no-store"
    assert missing_file_normalized_headers["cloudflare-cdn-cache-control"] == "no-store"


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


def test_portal_runtime_serves_public_play_ledger_and_participation_routes_with_documented_boundaries() -> None:
    with _running_portal() as base_url:
        play_html = _http_get(f"{base_url}/play")
        ledger_map_html = _http_get(f"{base_url}/ledger/map")
        ledger_factions_html = _http_get(f"{base_url}/ledger/factions")
        ledger_newsroom_html = _http_get(f"{base_url}/ledger/newsroom")
        participate_html = _http_get(f"{base_url}/participate")
        openapi = json.loads(_http_get(f"{base_url}/openapi/v1.json"))

        ledger_status, ledger_headers, _ = _http_request(f"{base_url}/ledger", follow_redirects=False)
        roadmap_status, roadmap_headers, _ = _http_request(f"{base_url}/roadmap", follow_redirects=False)
        session_status, session_headers, _ = _http_request(f"{base_url}/session/", follow_redirects=False)
        coach_status, coach_headers, _ = _http_request(f"{base_url}/coach/", follow_redirects=False)

    paths = openapi["paths"]

    assert "Open mobile and PWA" in play_html
    assert "Open continuity" in play_html
    assert "Opt-in posture" in play_html
    assert "data-portal-play-action='open-mobile-pwa'" in play_html
    assert "data-portal-play-action='open-continuity'" in play_html
    assert 'href="/app"' in play_html
    assert 'href="/session/"' in play_html

    assert "Black Ledger command map" in ledger_map_html
    assert "Fictional campaign pressure, package heat, and closeout movement." in ledger_map_html
    assert "data-portal-ledger-action='open-factions'" in ledger_map_html
    assert "data-portal-ledger-action='open-newsroom'" in ledger_map_html
    assert "data-portal-ledger-action='open-play'" in ledger_map_html

    assert "Black Ledger factions" in ledger_factions_html
    assert "Heat nearby" in ledger_factions_html
    assert "data-portal-ledger-factions-action='open-map'" in ledger_factions_html
    assert "data-portal-ledger-factions-action='open-newsroom'" in ledger_factions_html

    assert "Black Ledger newsroom" in ledger_newsroom_html
    assert "Turn packaging" in ledger_newsroom_html
    assert "data-portal-ledger-newsroom-action='open-map'" in ledger_newsroom_html
    assert "data-portal-ledger-newsroom-action='open-factions'" in ledger_newsroom_html

    assert 'data-portal-participate-frame' in participate_html
    assert 'title="Chummer participation board"' in participate_html
    assert "/participate/frame" in participate_html
    assert "data-portal-participate-action='open-status'" in participate_html
    assert "The framed board keeps the Chummer surface while the upstream board stays authoritative." in participate_html

    assert ledger_status in {301, 302, 303, 307, 308}
    assert ledger_headers.get("Location") == "/ledger/map"
    assert roadmap_status in {301, 302, 303, 307, 308}
    assert roadmap_headers.get("Location") == "/participate"
    assert session_status in {301, 302, 303, 307, 308}
    assert session_headers.get("Location") == "/play"
    assert coach_status in {301, 302, 303, 307, 308}
    assert coach_headers.get("Location") == "/status"

    assert "/play" in paths
    assert "/ledger" in paths
    assert "/ledger/map" in paths
    assert "/ledger/factions" in paths
    assert "/ledger/newsroom" in paths
    assert "/participate" in paths
    assert "/roadmap" in paths
    assert "/session/" in paths
    assert "/coach/" in paths
    assert paths["/ledger/factions"]["get"]["summary"] == "Open the Black Ledger faction files surface"
    assert paths["/ledger/newsroom"]["get"]["summary"] == "Open the Black Ledger newsroom surface"


def test_portal_handoff_docs_name_public_play_ledger_participation_and_continuity_routes() -> None:
    proof_doc = (REPO_ROOT / "docs" / "BLAZOR_PORTAL_INSTALLER_HANDOFF_PROOF.md").read_text(encoding="utf-8")
    docs_index = (REPO_ROOT / "docs" / "BLAZOR_WEB_CLIENT_DOCS_INDEX.md").read_text(encoding="utf-8")

    for token in [
        "/play",
        "/ledger",
        "/ledger/map",
        "/ledger/factions",
        "/ledger/newsroom",
        "/participate",
        "/roadmap",
        "/session/",
        "/coach/",
    ]:
        assert token in proof_doc

    assert "public living-world entry surfaces" in proof_doc
    assert "continuity fallbacks" in proof_doc

    for token in [
        "/play",
        "/participate",
        "/session/",
        "/coach/",
    ]:
        assert token in docs_index

    assert "Black Ledger public route family" in docs_index
