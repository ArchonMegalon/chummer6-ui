from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
APP_RAZOR = REPO_ROOT / "Chummer.Blazor" / "Components" / "App.razor"
WWWROOT = REPO_ROOT / "Chummer.Blazor" / "wwwroot"


def test_blazor_app_advertises_installable_pwa_surface() -> None:
    app = APP_RAZOR.read_text(encoding="utf-8")

    assert '<link rel="manifest" href="manifest.webmanifest" />' in app
    assert '<meta name="theme-color" content="#0f3b3e" />' in app
    assert "navigator.serviceWorker.register(serviceWorkerScript, { scope: serviceWorkerScope })" in app
    assert "window.chummerPwa" in app


def test_pwa_manifest_keeps_mobile_play_surface_as_start_url() -> None:
    manifest = json.loads((WWWROOT / "manifest.webmanifest").read_text(encoding="utf-8"))

    assert manifest["name"] == "Chummer Online"
    assert manifest["start_url"] == "./app?source=pwa"
    assert manifest["scope"] == "./"
    assert manifest["display"] == "standalone"
    assert any(icon["purpose"] == "maskable" for icon in manifest["icons"])
    assert {shortcut["short_name"] for shortcut in manifest["shortcuts"]} >= {"Play", "Roster"}


def test_service_worker_caches_only_static_shell_assets_not_runner_data() -> None:
    worker = (WWWROOT / "service-worker.js").read_text(encoding="utf-8")

    assert "CHUMMER_PWA_CACHE" in worker
    assert "caches.open(CHUMMER_PWA_CACHE)" in worker
    assert "request.mode === 'navigate'" in worker
    assert "caches.match('./offline.html')" in worker
    assert "path.includes('/api/')" in worker
    assert "path.includes('/workspaces/')" in worker
    assert "path.includes('/session/')" in worker
    assert "workspace=ws-1" not in worker


def test_offline_shell_states_living_world_data_is_not_cached() -> None:
    offline = (WWWROOT / "offline.html").read_text(encoding="utf-8")

    assert "Your runner data is not cached" in offline
    assert "Black Ledger" in offline
    assert "heat" in offline
    assert "opt-in living-world data stays server-bound" in offline
