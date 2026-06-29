#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import time
import urllib.request
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
BASE_URL = os.environ.get("CHUMMER_BLAZOR_PWA_PUBLIC_EDGE_BASE_URL", "https://chummer.run/blazor").rstrip("/")
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_BLAZOR_PWA_PUBLIC_EDGE_PROOF_PATH",
        REPO_ROOT / ".codex-studio" / "published" / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json",
    )
)
USER_AGENT = "ChummerPwaPublicEdgeProof/1.0"


class HeadProbe(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.links: list[dict[str, str | None]] = []
        self.metas: list[dict[str, str | None]] = []
        self.bases: list[dict[str, str | None]] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = dict(attrs)
        if tag == "link":
            self.links.append(data)
        elif tag == "meta":
            self.metas.append(data)
        elif tag == "base":
            self.bases.append(data)


def fetch_text(path: str) -> dict[str, Any]:
    url = f"{BASE_URL}{path}"
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=30) as response:
        body = response.read().decode("utf-8", errors="replace")

    return {
        "url": url,
        "status_code": response.status,
        "content_type": response.headers.get("content-type") or "",
        "elapsed_ms": round((time.perf_counter() - started) * 1000),
        "body": body,
    }


def passed_check(check_id: str, assertion: str, url: str, facts: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": check_id,
        "url": url,
        "status": "passed",
        "assertion": assertion,
        "facts": facts,
    }


def failed_check(check_id: str, assertion: str, url: str, reason: str, facts: dict[str, Any] | None = None) -> dict[str, Any]:
    return {
        "id": check_id,
        "url": url,
        "status": "failed",
        "assertion": assertion,
        "reason": reason,
        "facts": facts or {},
    }


def check_manifest() -> dict[str, Any]:
    result = fetch_text("/manifest.webmanifest")
    body = result["body"]
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
    }

    try:
        manifest = json.loads(body)
    except json.JSONDecodeError as error:
        return failed_check(
            "manifest_install_contract",
            "deployed manifest is installable and starts the PWA on the mobile Play surface",
            result["url"],
            f"manifest JSON parse failed: {error}",
            facts,
        )

    shortcuts = {str(item.get("short_name") or "") for item in manifest.get("shortcuts") or [] if isinstance(item, dict)}
    facts.update(
        {
            "name": manifest.get("name"),
            "short_name": manifest.get("short_name"),
            "start_url": manifest.get("start_url"),
            "scope": manifest.get("scope"),
            "display": manifest.get("display"),
            "shortcut_short_names": sorted(shortcuts),
            "icon_purposes": [item.get("purpose") for item in manifest.get("icons") or [] if isinstance(item, dict)],
            "screenshot_count": len(manifest.get("screenshots") or []),
        }
    )

    required = [
        (result["status_code"] == 200, "manifest did not return HTTP 200"),
        (manifest.get("name") == "Chummer Online", "manifest name mismatch"),
        (manifest.get("start_url") == "./app?source=pwa", "manifest start_url must target Play app"),
        (manifest.get("scope") == "./", "manifest scope must remain under /blazor/"),
        (manifest.get("display") == "standalone", "manifest display must be standalone"),
        (any((item.get("purpose") == "maskable") for item in manifest.get("icons") or [] if isinstance(item, dict)), "manifest missing maskable icon"),
        ({"Play", "Roster"}.issubset(shortcuts), "manifest shortcuts must include Play and Roster"),
        (len(manifest.get("screenshots") or []) >= 2, "manifest must include screenshots for store/install surfaces"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "manifest_install_contract",
            "deployed manifest is installable and starts the PWA on the mobile Play surface",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "manifest_install_contract",
        "deployed manifest is installable and starts the PWA on the mobile Play surface",
        result["url"],
        facts,
    )


def check_service_worker() -> dict[str, Any]:
    result = fetch_text("/service-worker.js")
    body = result["body"]
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "declares_static_cache": "CHUMMER_PWA_CACHE" in body,
        "handles_navigations_with_offline_fallback": "request.mode === 'navigate'" in body and "caches.match('./offline.html')" in body,
        "rejects_query_asset_cache": "url.search" in body,
        "excludes_api": "path.includes('/api/')" in body,
        "excludes_workspaces": "path.includes('/workspaces/')" in body,
        "excludes_sessions": "path.includes('/session/')" in body,
        "contains_stale_demo_workspace": "workspace=ws-1" in body,
    }
    required = [
        (result["status_code"] == 200, "service worker did not return HTTP 200"),
        (facts["declares_static_cache"], "service worker missing static cache declaration"),
        (facts["handles_navigations_with_offline_fallback"], "service worker missing navigation offline fallback"),
        (facts["rejects_query_asset_cache"], "service worker must reject query-string asset caching"),
        (facts["excludes_api"], "service worker must exclude API routes"),
        (facts["excludes_workspaces"], "service worker must exclude workspace routes"),
        (facts["excludes_sessions"], "service worker must exclude session routes"),
        (not facts["contains_stale_demo_workspace"], "service worker must not pin the stale demo workspace id"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "service_worker_static_privacy_contract",
            "deployed service worker caches only static shell assets and excludes runner/session data",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "service_worker_static_privacy_contract",
        "deployed service worker caches only static shell assets and excludes runner/session data",
        result["url"],
        facts,
    )


def check_offline_shell() -> dict[str, Any]:
    result = fetch_text("/offline.html")
    body = result["body"]
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "states_runner_data_not_cached": "Your runner data is not cached" in body,
        "mentions_black_ledger": "Black Ledger" in body,
        "mentions_heat": "heat" in body,
        "states_server_bound_living_world_data": "opt-in living-world data stays server-bound" in body,
    }
    required = [
        (result["status_code"] == 200, "offline shell did not return HTTP 200"),
        (facts["states_runner_data_not_cached"], "offline shell must state runner data is not cached"),
        (facts["mentions_black_ledger"], "offline shell must mention Black Ledger"),
        (facts["mentions_heat"], "offline shell must mention heat"),
        (facts["states_server_bound_living_world_data"], "offline shell must state living-world data stays server-bound"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "offline_living_world_boundary",
            "deployed offline shell communicates that opt-in living-world data is not cached locally",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "offline_living_world_boundary",
        "deployed offline shell communicates that opt-in living-world data is not cached locally",
        result["url"],
        facts,
    )


def check_app_head() -> dict[str, Any]:
    result = fetch_text("/app?_pwa_probe=1")
    body = result["body"]
    probe = HeadProbe()
    probe.feed(body)
    has_manifest = any(link.get("rel") == "manifest" and link.get("href") == "manifest.webmanifest" for link in probe.links)
    has_icon = any(link.get("rel") == "icon" and link.get("href") == "icons/chummer-pwa.svg" for link in probe.links)
    has_apple_icon = any(link.get("rel") == "apple-touch-icon" for link in probe.links)
    has_theme = any(meta.get("name") == "theme-color" and meta.get("content") == "#0f3b3e" for meta in probe.metas)
    has_mobile_capable = any(meta.get("name") == "mobile-web-app-capable" and meta.get("content") == "yes" for meta in probe.metas)
    has_ios_capable = any(meta.get("name") == "apple-mobile-web-app-capable" and meta.get("content") == "yes" for meta in probe.metas)
    has_registration = "navigator.serviceWorker.register(serviceWorkerScript, { scope: serviceWorkerScope })" in body
    has_pwa_state = "window.chummerPwa" in body
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "base_hrefs": [base.get("href") for base in probe.bases],
        "has_manifest_link": has_manifest,
        "has_svg_icon": has_icon,
        "has_apple_touch_icon": has_apple_icon,
        "has_theme_color": has_theme,
        "has_mobile_web_app_capable": has_mobile_capable,
        "has_apple_mobile_web_app_capable": has_ios_capable,
        "has_service_worker_registration": has_registration,
        "has_pwa_install_state_object": has_pwa_state,
    }
    required = [
        (result["status_code"] == 200, "app did not return HTTP 200"),
        (has_manifest, "app missing manifest link"),
        (has_icon, "app missing SVG icon link"),
        (has_apple_icon, "app missing apple touch icon"),
        (has_theme, "app missing theme-color meta"),
        (has_mobile_capable, "app missing mobile-web-app-capable meta"),
        (has_ios_capable, "app missing apple mobile web app meta"),
        (has_registration, "app missing service worker registration"),
        (has_pwa_state, "app missing PWA install state object"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "app_head_and_registration",
            "deployed Blazor app advertises installability and registers the scoped service worker",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "app_head_and_registration",
        "deployed Blazor app advertises installability and registers the scoped service worker",
        result["url"],
        facts,
    )


def main() -> int:
    checks = [check_manifest(), check_service_worker(), check_offline_shell(), check_app_head()]
    failures = [check for check in checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_pwa_public_edge_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "base_url": BASE_URL,
        "proof_tier": "hosted_pwa_public_edge_execution",
        "route_lane": "blazor_pwa_play_shell",
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt proves the deployed /blazor PWA shell contract, not app-store acceptance or offline runner-data parity.",
            "The service worker is required to cache only static shell assets and leave runner, workspace, API, Black Ledger, heat, and session data server-bound.",
            "The installed start URL remains the Play app surface; full character building stays outside the in-session PWA use case.",
        ],
    }
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(f"blazor_pwa_public_edge_proof:{receipt['status']} {OUTPUT_PATH}")
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
