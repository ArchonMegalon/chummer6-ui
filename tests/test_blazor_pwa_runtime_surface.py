from __future__ import annotations

import json
import os
import re
import runpy
import socket
import subprocess
import time
import urllib.request
from urllib.error import HTTPError
from contextlib import contextmanager
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
BLAZOR_PROJECT = REPO_ROOT / "Chummer.Blazor" / "Chummer.Blazor.csproj"
BLAZOR_DLL = REPO_ROOT / "Chummer.Blazor" / "bin" / "Debug" / "net10.0" / "Chummer.Blazor.dll"
APP_RAZOR = REPO_ROOT / "Chummer.Blazor" / "Components" / "App.razor"
SERVICE_WORKER = REPO_ROOT / "Chummer.Blazor" / "wwwroot" / "service-worker.js"
RELEASE_CONTRACT = REPO_ROOT / "Chummer.Blazor" / "Services" / "BuildPwaReleaseContract.cs"
RELEASE_FINALIZER = REPO_ROOT / "scripts" / "finalize-build-pwa-release.py"


def test_build_pwa_release_revision_is_runtime_derived_from_exact_published_bytes() -> None:
    app = APP_RAZOR.read_text(encoding="utf-8")
    worker = SERVICE_WORKER.read_text(encoding="utf-8")
    contract = RELEASE_CONTRACT.read_text(encoding="utf-8")
    finalizer = RELEASE_FINALIZER.read_text(encoding="utf-8")

    worker_paths_match = re.search(
        r"const RELEASE_CONTENT_PATHS = \[(.*?)\];",
        worker,
        flags=re.DOTALL,
    )
    contract_paths_match = re.search(
        r"AssetPaths = Array\.AsReadOnly\(new\[\]\s*\{(.*?)\}\);",
        contract,
        flags=re.DOTALL,
    )
    finalizer_paths_match = re.search(
        r"ASSET_PATHS = \((.*?)\)\n",
        finalizer,
        flags=re.DOTALL,
    )
    assert worker_paths_match and contract_paths_match and finalizer_paths_match
    worker_paths = tuple(re.findall(r"'([^']+)'", worker_paths_match.group(1)))
    contract_paths = tuple(re.findall(r'"([^"]+)"', contract_paths_match.group(1)))
    finalizer_paths = tuple(re.findall(r'"([^"]+)"', finalizer_paths_match.group(1)))
    assert worker_paths == contract_paths == finalizer_paths
    assert "Chummer.Blazor.styles.css" in worker_paths
    assert "_framework/blazor.web.js" in worker_paths
    assert "service-worker.js" in worker_paths

    assert "BuildPwaReleaseContentRevision =" not in app
    assert "BuildPwaReleaseContract.GetSnapshot(BuildPwaEnvironment)" in app
    assert app.count("integrity=\"@BuildStaticAssetIntegrity(") == 9
    assert "UseBuildPwaReleaseContract(pathBase)" in (
        REPO_ROOT / "Chummer.Blazor" / "Program.cs"
    ).read_text(encoding="utf-8")
    assert "new URL(self.location.href)" in worker
    assert "CHUMMER_BUILD_PWA_CACHE_GENERATION = 'v7'" in worker
    assert "buildRevisionCacheName(" in worker
    assert "deriveReleaseContentRevision(fetchedAssets)" in worker
    assert "cache: 'no-store'" in worker
    assert "RELEASE_CONTENT_PATHNAMES.get(url.pathname)" in worker
    assert "normalizedPath.endsWith" not in worker
    assert "X-Chummer-Build-Content-Revision" in worker
    assert "self.skipWaiting(" not in worker
    assert "self.clients.claim(" not in worker
    assert "updateViaCache: 'none'" in app


def test_build_pwa_publish_receipt_changes_on_one_byte(tmp_path: Path) -> None:
    finalizer = runpy.run_path(str(RELEASE_FINALIZER))
    asset_paths = finalizer["ASSET_PATHS"]
    build_receipt = finalizer["build_receipt"]
    for index, public_path in enumerate(asset_paths):
        target = tmp_path / public_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(f"asset-{index}:{public_path}".encode("utf-8"))

    first = build_receipt(tmp_path)
    changed = tmp_path / "_framework" / "blazor.web.js"
    changed.write_bytes(changed.read_bytes() + b"!")
    second = build_receipt(tmp_path)
    assert first["contentRevision"] != second["contentRevision"]
    first_framework = next(asset for asset in first["assets"] if asset["path"] == "_framework/blazor.web.js")
    second_framework = next(asset for asset in second["assets"] if asset["path"] == "_framework/blazor.web.js")
    assert first_framework["sha256"] != second_framework["sha256"]


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as handle:
        handle.bind(("127.0.0.1", 0))
        return int(handle.getsockname()[1])


def _http_request(url: str) -> tuple[int, dict[str, str], bytes]:
    try:
        with urllib.request.urlopen(url, timeout=10) as response:
            return int(response.status), dict(response.headers.items()), response.read()
    except HTTPError as exc:
        return int(exc.code), dict(exc.headers.items()), exc.read()


def _http_post(url: str, body: bytes, headers: dict[str, str] | None = None) -> tuple[int, dict[str, str], bytes]:
    request = urllib.request.Request(url, data=body, headers=headers or {}, method="POST")
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            return int(response.status), dict(response.headers.items()), response.read()
    except HTTPError as exc:
        return int(exc.code), dict(exc.headers.items()), exc.read()


def _ensure_blazor_built() -> None:
    result = subprocess.run(
        [
            "dotnet",
            "build",
            str(BLAZOR_PROJECT),
            "-f",
            "net10.0",
            "--no-restore",
            "-m:1",
            "-p:UseSharedCompilation=false",
            "-v",
            "minimal",
        ],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
        check=False,
        timeout=900,
    )
    assert result.returncode == 0, result.stdout + "\n" + result.stderr
    assert BLAZOR_DLL.is_file(), "Expected the Blazor runtime build to produce Chummer.Blazor.dll."


@contextmanager
def _running_blazor(path_base: str = "/blazor"):
    _ensure_blazor_built()

    port = _find_free_port()
    base_url = f"http://127.0.0.1:{port}"
    log_path = REPO_ROOT / ".tmp" / f"blazor-pwa-runtime-{port}.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env["ASPNETCORE_URLS"] = base_url
    env["ASPNETCORE_ENVIRONMENT"] = "Test"
    env["DOTNET_ENVIRONMENT"] = "Test"
    if path_base:
        env["CHUMMER_BLAZOR_PATH_BASE"] = path_base
    else:
        env.pop("CHUMMER_BLAZOR_PATH_BASE", None)
    env["CHUMMER_ANALYTICS_PROVIDER"] = "none"
    env["CHUMMER_API_BASE_URL"] = "http://127.0.0.1:65535"
    env["CHUMMER_BUILD_OWNER_CHANNEL_ALLOW_EPHEMERAL"] = "true"

    with log_path.open("w", encoding="utf-8") as log_file:
        process = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(BLAZOR_PROJECT),
                "--no-launch-profile",
                "--no-build",
            ],
            cwd=REPO_ROOT,
            env=env,
            stdout=log_file,
            stderr=subprocess.STDOUT,
        )

    try:
        deadline = time.time() + 240
        last_error = ""
        ready = False
        while time.time() < deadline:
            if process.poll() is not None:
                break

            try:
                status, _, body = _http_request(f"{base_url}{path_base}/health/live")
                payload = json.loads(body.decode("utf-8"))
                if (
                    status == 200
                    and payload["head"] == "blazor"
                    and payload["check"] == "liveness"
                ):
                    ready = True
                    break
            except Exception as exc:  # pragma: no cover - boot retry path
                last_error = str(exc)
                time.sleep(0.5)

        if not ready:
            log_text = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
            raise AssertionError(
                f"Blazor runtime did not become ready at {base_url}{path_base}. "
                f"Last error: {last_error}\n{log_text}"
            )

        yield base_url, path_base
    finally:
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)


def test_blazor_health_short_circuits_owner_cookie_pipeline_at_root_and_path_base() -> None:
    for configured_path_base in ("", "/blazor"):
        with _running_blazor(configured_path_base) as (base_url, path_base):
            readiness_deadline = time.time() + 15
            readiness_status = 0
            readiness_headers: dict[str, str] = {}
            readiness_body = b""
            while time.time() < readiness_deadline:
                readiness_status, readiness_headers, readiness_body = _http_request(
                    f"{base_url}{path_base}/health/ready"
                )
                if readiness_status == 200:
                    break
                time.sleep(0.1)

            assert readiness_status == 200, readiness_body.decode("utf-8", errors="replace")
            for relative_path, expected_check in (
                ("/health/live", "liveness"),
                ("/health/ready", "readiness"),
                ("/health", "readiness"),
            ):
                status, headers, body = _http_request(f"{base_url}{path_base}{relative_path}")
                payload = json.loads(body.decode("utf-8"))
                assert status == 200
                assert payload["check"] == expected_check
                assert headers["Cache-Control"] == "no-store"
                assert "Set-Cookie" not in headers

            app_status, app_headers, _ = _http_request(
                f"{base_url}{path_base}/app?source=health-test"
            )
            assert app_status == 200
            assert "Set-Cookie" in app_headers


def test_blazor_runtime_serves_pwa_shell_assets_and_opt_in_boundary_under_path_base() -> None:
    with _running_blazor() as (base_url, path_base):
        status, headers, app_body = _http_request(f"{base_url}{path_base}/app?source=pwa")
        assert status == 200
        app_html = app_body.decode("utf-8")
        release_match = re.search(r'"contentRevision":"([a-f0-9]{64})"', app_html)
        assert release_match is not None
        release_revision = release_match.group(1)

        assert '<meta name="viewport" content="width=device-width, initial-scale=1.0"' in app_html
        assert '<meta name="mobile-web-app-capable" content="yes"' in app_html
        assert '<meta name="apple-mobile-web-app-capable" content="yes"' in app_html
        assert '<base href="/blazor/"' in app_html
        assert f'<link rel="manifest" href="/blazor/manifest.webmanifest?build={release_revision}"' in app_html
        assert f'href="/blazor/icons/chummer-pwa.svg?build={release_revision}"' in app_html
        assert 'data-active-workflow="character-roster"' in app_html
        assert 'data-route-surface="roster"' in app_html
        assert 'data-control="none"' in app_html
        assert 'data-dialog-action="none"' in app_html
        assert 'data-fixture="blue"' in app_html
        assert 'data-legacy-runner="none"' in app_html
        assert 'href="app?command=new_character"' in app_html
        assert 'href="app?command=character_roster"' in app_html
        assert 'href="app?fixture=blue"' in app_html
        assert 'href="app?fixture=blue&amp;tab=tab-create"' in app_html
        assert 'href="app?fixture=blue&amp;tab=tab-technomancer"' in app_html
        assert 'href="app?fixture=blue&amp;tab=tab-info"' in app_html
        assert 'href="app?fixture=blue&amp;tab=tab-rules"' in app_html
        assert 'href="app?fixture=blue&amp;tab=tab-contacts"' in app_html
        assert 'href="app?fixture=blue&amp;command=save_character_as"' in app_html
        assert 'href="workbench?' not in app_html
        assert "navigator.serviceWorker.register(serviceWorkerScript," in app_html
        assert "updateViaCache: 'none'" in app_html
        assert app_html.count('integrity="sha256-') >= 9
        assert "chummer-build:service-worker-registration" in app_html
        assert "pwa.expectedAuthority" in app_html
        assert "expectedAuthority.scriptUrl" in app_html
        assert "expectedAuthority.scope" in app_html
        assert headers["Content-Type"].startswith("text/html")

        revisioned_status, revisioned_headers, _ = _http_request(
            f"{base_url}{path_base}/app.css?build={release_revision}"
        )
        assert revisioned_status == 200
        assert revisioned_headers["X-Chummer-Build-Content-Revision"] == release_revision
        mismatched_status, mismatched_headers, _ = _http_request(
            f"{base_url}{path_base}/app.css?build={'0' * 64}"
        )
        assert mismatched_status == 409
        assert mismatched_headers["Cache-Control"] == "no-store"

        startup_status, _, startup_body = _http_request(f"{base_url}{path_base}/app?command=new_character")
        assert startup_status == 200
        startup_html = startup_body.decode("utf-8")
        assert 'data-ssr-app-route-fallback="true"' in startup_html
        assert 'data-active-workflow="build-lab"' in startup_html
        assert 'data-command="new-character"' in startup_html
        assert 'data-chummer-app-startup-command="new_character"' in startup_html
        assert 'data-control="none"' in startup_html
        assert 'data-dialog-action="none"' in startup_html
        assert 'data-fixture="blue"' in startup_html
        assert 'data-legacy-runner="none"' in startup_html
        assert 'data-app-route-shared-shell="true"' in startup_html
        assert 'href="app?fixture=blue&amp;tab=tab-create"' in startup_html
        assert 'href="app?command=new_character_origin"' in startup_html
        assert 'href="app?command=open_character"' in startup_html
        assert 'href="app?fixture=blue"' in startup_html
        assert 'href="workbench?' not in startup_html
        assert "Build Lab shell" in startup_html
        assert "Your runners will appear here." not in startup_html

        alias_status, _, alias_body = _http_request(
            f"{base_url}{path_base}/online?workspace=preview-ws&tab=tab-contacts&control=contact_add&dialog_action=add"
        )
        assert alias_status == 200
        alias_html = alias_body.decode("utf-8")
        assert 'data-ssr-app-route-fallback="true"' in alias_html
        assert 'data-route-family="online-alias"' in alias_html
        assert 'data-route-segment="online"' in alias_html
        assert 'data-canonical-route="app"' in alias_html
        assert 'data-route-alias="online"' in alias_html
        assert 'data-active-workflow="contacts"' in alias_html
        assert 'data-command="none"' in alias_html
        assert 'data-tab="tab-contacts"' in alias_html
        assert 'data-workspace="preview-ws"' in alias_html
        assert 'data-control="contact-add"' in alias_html
        assert 'data-dialog-action="add"' in alias_html
        assert 'data-fixture="blue"' in alias_html
        assert 'data-legacy-runner="none"' in alias_html
        assert 'data-app-route-shared-shell="true"' in alias_html
        assert 'href="app?command=new_character"' in alias_html
        assert 'href="app?command=open_character"' in alias_html
        assert 'href="app?fixture=blue"' in alias_html
        assert 'href="workbench?' not in alias_html
        assert "Contacts shell" in alias_html
        assert "Open the requested runner context directly in the shared Chummer Online shell." in alias_html
        assert "Your runners will appear here." not in alias_html

        fixture_status, _, fixture_body = _http_request(f"{base_url}{path_base}/app?fixture=blue&tab=tab-create")
        assert fixture_status == 200
        fixture_html = fixture_body.decode("utf-8")
        assert 'data-ssr-app-route-fallback="true"' in fixture_html
        assert 'data-fixture="blue"' in fixture_html
        assert 'data-workspace="blue-workspace"' in fixture_html
        assert 'data-tab="tab-create"' in fixture_html
        assert 'data-control="none"' in fixture_html
        assert 'data-dialog-action="none"' in fixture_html
        assert 'href="app?fixture=blue&amp;tab=tab-create"' in fixture_html
        assert 'href="workbench?' not in fixture_html

        workbench_status, _, workbench_body = _http_request(
            f"{base_url}{path_base}/workbench?workspace=blue-workspace&command=new_character"
        )
        assert workbench_status == 200
        workbench_html = workbench_body.decode("utf-8")
        assert 'data-ssr-workbench-fallback="true"' in workbench_html
        assert 'data-route-segment="workbench"' in workbench_html
        assert 'data-workspace="blue-workspace"' in workbench_html
        assert 'data-app-menu-root="file"' in workbench_html
        file_menu_start = workbench_html.index('data-app-menu-summary="file"')
        file_menu_end = workbench_html.index("</button>", file_menu_start)
        file_menu_markup = workbench_html[file_menu_start:file_menu_end]
        assert 'role="menuitem"' in file_menu_markup
        assert "File" in file_menu_markup
        assert 'href="workbench?workspace=blue-workspace&amp;tab=tab-create"' in workbench_html

        rules_status, _, rules_body = _http_request(f"{base_url}{path_base}/app?fixture=blue&tab=tab-rules")
        assert rules_status == 200
        rules_html = rules_body.decode("utf-8")
        assert 'data-ssr-app-route-fallback="true"' in rules_html
        assert 'data-active-workflow="rules"' in rules_html
        assert 'data-tab="tab-rules"' in rules_html
        assert 'data-fixture="blue"' in rules_html
        assert 'data-control="none"' in rules_html
        assert 'data-dialog-action="none"' in rules_html
        assert "Rules shell" in rules_html
        assert "Open the shared rules-facing lane from Chummer Online so source and rules context stay on the app route." in rules_html
        assert "Chummer Online is opening directly into the rules lane so source and rules context stay on the app route." in rules_html
        assert 'href="app?fixture=blue"' in rules_html
        assert 'href="app?fixture=blue&amp;tab=tab-create"' in rules_html
        assert 'href="app?fixture=blue&amp;tab=tab-contacts"' in rules_html
        assert "Dossier shell" not in rules_html
        assert 'href="workbench?' not in rules_html

        manifest_status, manifest_headers, manifest_body = _http_request(f"{base_url}{path_base}/manifest.webmanifest")
        assert manifest_status == 200
        assert "application/manifest+json" in manifest_headers["Content-Type"]
        manifest = json.loads(manifest_body.decode("utf-8"))
        assert manifest["name"] == "Chummer Runner Builder"
        assert manifest["short_name"] == "Chummer Build"
        assert manifest["id"] == "./app"
        assert manifest["start_url"] == "./app?command=character_roster&source=pwa"
        assert manifest["scope"] == "./"
        assert any(
            shortcut["url"] == "./app?command=new_character&source=pwa-shortcut"
            for shortcut in manifest["shortcuts"]
        )

        worker_status, worker_headers, worker_body = _http_request(f"{base_url}{path_base}/service-worker.js")
        assert worker_status == 200
        worker = worker_body.decode("utf-8")
        assert "const CHUMMER_PWA_CACHE" in worker
        assert "CHUMMER_BUILD_PWA_CACHE_GENERATION = 'v7'" in worker
        assert "CHUMMER_BUILD_PWA_RELEASE_CONTENT_REVISION" in worker
        assert "new URL(self.location.href)" in worker
        assert "RELEASE_CONTENT_PATHNAMES.get(url.pathname)" in worker
        assert "deriveReleaseContentRevision(fetchedAssets)" in worker
        assert "cache: 'no-store'" in worker
        assert "X-Chummer-Build-Content-Revision" in worker
        assert "normalizedPath.endsWith" not in worker
        assert "self.skipWaiting(" not in worker
        assert "self.clients.claim(" not in worker
        assert "Promise.allSettled" in worker
        assert "cache.match(OFFLINE_URL)" in worker
        assert "caches.match(" not in worker
        assert "cache.addAll" not in worker
        assert "chummer6-hero-baseline.png" not in worker
        assert "javascript" in worker_headers["Content-Type"]

        offline_status, offline_headers, offline_body = _http_request(f"{base_url}{path_base}/offline.html")
        assert offline_status == 200
        offline_html = offline_body.decode("utf-8")
        assert "Your runner data is not cached" in offline_html
        assert "Chummer Build PWA" in offline_html
        assert "Retry Chummer Build" in offline_html
        assert "Black Ledger" in offline_html
        assert "heat" in offline_html
        assert "opt-in living-world data stays server-bound" in offline_html
        assert offline_headers["Content-Type"].startswith("text/html")

        for relative_path, expected_content_type in [
            ("icons/chummer-pwa.svg", "image/svg+xml"),
            ("icons/chummer-pwa-maskable.svg", "image/svg+xml"),
            ("icons/chummer-build-180.png", "image/png"),
            ("icons/chummer-build-192.png", "image/png"),
            ("icons/chummer-build-512.png", "image/png"),
            ("icons/chummer-build-maskable-512.png", "image/png"),
            ("media/chummer6/chummer6-hero-baseline.png", "image/png"),
        ]:
            asset_status, asset_headers, asset_body = _http_request(f"{base_url}{path_base}/{relative_path}")
            assert asset_status == 200
            assert expected_content_type in asset_headers["Content-Type"]
            assert len(asset_body) > 100


def test_blazor_runtime_exposes_interactive_server_negotiate_under_path_base() -> None:
    with _running_blazor() as (base_url, path_base):
        status, headers, body = _http_post(
            f"{base_url}{path_base}/_blazor/negotiate?negotiateVersion=1",
            b"{}",
            {"Content-Type": "application/json"},
        )
        assert status == 200, body.decode("utf-8", errors="replace")
        assert headers["Content-Type"].startswith("application/json")
        payload = json.loads(body.decode("utf-8"))
        assert payload["negotiateVersion"] == 1
        assert payload["connectionToken"]
