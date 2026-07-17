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
from urllib.parse import urljoin, urlparse


REPO_ROOT = Path(__file__).resolve().parents[1]
BASE_URL = os.environ.get("CHUMMER_BLAZOR_PWA_PUBLIC_EDGE_BASE_URL", "https://chummer.run/blazor").rstrip("/")
BASE_ORIGIN = f"{urlparse(BASE_URL).scheme}://{urlparse(BASE_URL).netloc}"
PUBLIC_ENTRY_URL = os.environ.get(
    "CHUMMER_BLAZOR_PWA_PUBLIC_ENTRY_URL",
    urljoin(f"{BASE_URL}/", "../app") if urlparse(BASE_URL).path.rstrip("/") == "/blazor" else f"{BASE_URL}/app",
)
PWA_ALIAS_URL = os.environ.get(
    "CHUMMER_BLAZOR_PWA_ALIAS_URL",
    urljoin(f"{BASE_URL}/", "../pwa") if urlparse(BASE_URL).path.rstrip("/") == "/blazor" else f"{BASE_URL}/pwa",
)
MOBILE_PLAYER_URL = os.environ.get(
    "CHUMMER_BLAZOR_MOBILE_PLAYER_URL",
    f"{BASE_ORIGIN}/mobile",
)
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


def fetch_text_url(url: str) -> dict[str, Any]:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=30) as response:
        body = response.read().decode("utf-8", errors="replace")

    return {
        "url": url,
        "final_url": response.url,
        "status_code": response.status,
        "content_type": response.headers.get("content-type") or "",
        "elapsed_ms": round((time.perf_counter() - started) * 1000),
        "body": body,
    }


def fetch_text(path: str) -> dict[str, Any]:
    return fetch_text_url(f"{BASE_URL}{path}")


def fetch_bytes(path: str) -> dict[str, Any]:
    url = f"{BASE_URL}{path}"
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=30) as response:
        body = response.read()

    return {
        "url": url,
        "status_code": response.status,
        "content_type": response.headers.get("content-type") or "",
        "elapsed_ms": round((time.perf_counter() - started) * 1000),
        "body": body,
    }


def parse_manifest_hrefs(body: str) -> list[str]:
    probe = HeadProbe()
    probe.feed(body)
    hrefs = {
        str(link.get("href") or "").strip()
        for link in probe.links
        if "manifest" in str(link.get("rel") or "").lower()
    }
    return sorted(href for href in hrefs if href)


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


def check_public_entry_route(
    *,
    url: str,
    check_id: str,
    assertion: str,
    label: str,
) -> dict[str, Any]:
    result = fetch_text_url(url)
    body = result["body"]
    probe = HeadProbe()
    probe.feed(body)

    base_hrefs = [str(base.get("href") or "") for base in probe.bases]
    manifest_hrefs = [
        str(link.get("href") or "")
        for link in probe.links
        if str(link.get("rel") or "") == "manifest"
    ]
    icon_hrefs = [
        str(link.get("href") or "")
        for link in probe.links
        if str(link.get("rel") or "") == "icon"
    ]
    base_scope_urls = [urljoin(PUBLIC_ENTRY_URL, href) for href in base_hrefs]
    hosted_base_scope_url = next((url for url in base_scope_urls if url.rstrip("/") == BASE_URL), f"{BASE_URL}/")
    resolved_manifest_urls = [urljoin(hosted_base_scope_url, href) for href in manifest_hrefs]
    resolved_icon_urls = [urljoin(hosted_base_scope_url, href) for href in icon_hrefs]
    service_worker_registration_is_relative = (
        "const serviceWorkerScript = 'service-worker.js'" in body
        and "const serviceWorkerScope = './'" in body
    )
    viewport_content = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "viewport"
        ),
        "",
    )
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "base_hrefs": base_hrefs,
        "base_scope_urls": base_scope_urls,
        "manifest_hrefs": manifest_hrefs,
        "icon_hrefs": icon_hrefs,
        "resolved_manifest_urls": resolved_manifest_urls,
        "resolved_icon_urls": resolved_icon_urls,
        "service_worker_registration_is_relative": service_worker_registration_is_relative,
        "viewport_content": viewport_content,
        "has_pwa_install_state_object": "window.chummerPwa" in body,
    }
    required = [
        (result["status_code"] == 200, f"{label} did not return HTTP 200"),
        ("text/html" in str(result["content_type"]), f"{label} must return HTML"),
        ("/blazor/" in base_hrefs, f"{label} must keep base href scoped to /blazor/"),
        ("manifest.webmanifest" in manifest_hrefs, f"{label} missing relative manifest link"),
        ("icons/chummer-pwa.svg" in icon_hrefs, f"{label} missing relative PWA icon link"),
        (
            any(url.rstrip("/") == f"{BASE_URL}/manifest.webmanifest" for url in resolved_manifest_urls),
            f"{label} manifest must resolve under the hosted /blazor scope",
        ),
        (
            any(url.rstrip("/") == f"{BASE_URL}/icons/chummer-pwa.svg" for url in resolved_icon_urls),
            f"{label} icon must resolve under the hosted /blazor scope",
        ),
        (
            service_worker_registration_is_relative,
            f"{label} must keep service-worker script and scope relative to /blazor/",
        ),
        (viewport_content == "width=device-width, initial-scale=1.0", f"{label} viewport meta must stay mobile-width and initial-scale 1.0"),
        (facts["has_pwa_install_state_object"], f"{label} missing PWA install state object"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            check_id,
            assertion,
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        check_id,
        assertion,
        result["url"],
        facts,
    )


def check_clean_public_entry_route() -> dict[str, Any]:
    return check_public_entry_route(
        url=PUBLIC_ENTRY_URL,
        check_id="clean_public_entry_route_contract",
        assertion="clean /app route resolves installable PWA assets through the hosted /blazor scope",
        label="clean public app route",
    )


def check_pwa_alias_route() -> dict[str, Any]:
    result = fetch_text_url(PWA_ALIAS_URL)
    body = result["body"]
    probe = HeadProbe()
    probe.feed(body)

    base_hrefs = [str(base.get("href") or "") for base in probe.bases]
    manifest_hrefs = [
        str(link.get("href") or "")
        for link in probe.links
        if str(link.get("rel") or "") == "manifest"
    ]
    apple_touch_icons = [
        str(link.get("href") or "")
        for link in probe.links
        if str(link.get("rel") or "") == "apple-touch-icon"
    ]
    viewport_content = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "viewport"
        ),
        "",
    )
    theme_color = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "theme-color"
        ),
        "",
    )
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "base_hrefs": base_hrefs,
        "manifest_hrefs": manifest_hrefs,
        "apple_touch_icons": apple_touch_icons,
        "viewport_content": viewport_content,
        "theme_color": theme_color,
        "has_mobile_web_app_capable": any(
            meta.get("name") == "mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_apple_mobile_web_app_capable": any(
            meta.get("name") == "apple-mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_turn_companion_title": "<title>Chummer Mobile Turn Companion</title>" in body,
    }
    required = [
        (result["status_code"] == 200, "/pwa did not return HTTP 200"),
        ("text/html" in str(result["content_type"]), "/pwa must return HTML"),
        ("/" in base_hrefs, "/pwa must keep root base href for the Hub Web player companion"),
        ("/manifest.player.webmanifest" in manifest_hrefs, "/pwa must advertise the player companion manifest"),
        ("/icons/apple-touch-icon.png" in apple_touch_icons, "/pwa must expose the player touch icon"),
        (viewport_content == "width=device-width, initial-scale=1.0", "/pwa viewport meta must stay mobile-width and initial-scale 1.0"),
        (theme_color == "#0f1b26", "/pwa theme color must match the player companion shell"),
        (facts["has_mobile_web_app_capable"], "/pwa missing mobile-web-app-capable meta"),
        (facts["has_apple_mobile_web_app_capable"], "/pwa missing apple mobile web app meta"),
        (facts["has_turn_companion_title"], "/pwa title must identify the mobile turn companion"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "player_pwa_alias_route_contract",
            "/pwa serves the installable Hub Web player companion shell",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "player_pwa_alias_route_contract",
        "/pwa serves the installable Hub Web player companion shell",
        result["url"],
        facts,
    )


def check_mobile_player_shell_route() -> dict[str, Any]:
    result = fetch_text_url(MOBILE_PLAYER_URL)
    body = result["body"]
    body_lower = body.lower()
    probe = HeadProbe()
    probe.feed(body)

    base_hrefs = [str(base.get("href") or "") for base in probe.bases]
    manifest_hrefs = [
        str(link.get("href") or "")
        for link in probe.links
        if str(link.get("rel") or "") == "manifest"
    ]
    viewport_content = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "viewport"
        ),
        "",
    )
    theme_color = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "theme-color"
        ),
        "",
    )
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "base_hrefs": base_hrefs,
        "manifest_hrefs": manifest_hrefs,
        "viewport_content": viewport_content,
        "theme_color": theme_color,
        "has_mobile_web_app_capable": any(
            meta.get("name") == "mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_apple_mobile_web_app_capable": any(
            meta.get("name") == "apple-mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_turn_root": "data-turn-root" in body,
        "has_player_role_link": 'data-role-name="Player"' in body,
        "has_owner_route_link": 'id="turn-owner-route-link"' in body,
        "has_living_world_opt_in_boundary": 'data-living-world-opt-in-boundary="true"' in body,
        "mentions_black_ledger": "Black Ledger" in body,
        "mentions_heat": "heat" in body_lower,
        "mentions_health": "health" in body_lower or "physical" in body_lower,
        "mentions_ammo": "ammo" in body_lower or "magazine" in body_lower,
        "mentions_inventory": "inventory" in body_lower,
        "mentions_modifiers": "modifier" in body_lower,
        "shows_dice_odds": 'id="turn-odds-summary"' in body and "%" in body and "dice" in body_lower,
        "has_digital_roll_action": 'data-turn-kind="resolve-digital"' in body,
        "has_manual_roll_action": 'data-turn-kind="resolve-manual"' in body,
    }
    required = [
        (result["status_code"] == 200, "/mobile did not return HTTP 200"),
        ("text/html" in str(result["content_type"]), "/mobile must return HTML"),
        ("/" in base_hrefs, "/mobile must keep root base href for the player companion"),
        ("/manifest.player.webmanifest" in manifest_hrefs, "/mobile must advertise the player manifest"),
        (viewport_content == "width=device-width, initial-scale=1.0", "/mobile viewport meta must stay mobile-width and initial-scale 1.0"),
        (theme_color == "#0f1b26", "/mobile theme color must match the player companion shell"),
        (facts["has_mobile_web_app_capable"], "/mobile missing mobile-web-app-capable meta"),
        (facts["has_apple_mobile_web_app_capable"], "/mobile missing apple mobile web app meta"),
        (facts["has_turn_root"], "/mobile missing turn companion root"),
        (facts["has_player_role_link"], "/mobile missing player role route"),
        (facts["has_owner_route_link"], "/mobile missing owner route link"),
        (facts["has_living_world_opt_in_boundary"], "/mobile missing living-world opt-in boundary"),
        (facts["mentions_black_ledger"], "/mobile must mention Black Ledger"),
        (facts["mentions_heat"], "/mobile must mention heat"),
        (facts["mentions_health"], "/mobile must cover health or damage tracking"),
        (facts["mentions_ammo"], "/mobile must cover ammo tracking"),
        (facts["mentions_inventory"], "/mobile must cover inventory tracking"),
        (facts["mentions_modifiers"], "/mobile must cover modifiers"),
        (facts["shows_dice_odds"], "/mobile must show dice odds or percentage chance"),
        (facts["has_digital_roll_action"], "/mobile must expose digital roll action"),
        (facts["has_manual_roll_action"], "/mobile must expose manual roll action"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "mobile_player_shell_route_contract",
            "live /mobile player shell renders playtime tracking, dice odds, roll actions, and living-world opt-in boundaries",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "mobile_player_shell_route_contract",
        "live /mobile player shell renders playtime tracking, dice odds, roll actions, and living-world opt-in boundaries",
        result["url"],
        facts,
    )


def check_player_manifest() -> dict[str, Any]:
    url = f"{BASE_ORIGIN}/manifest.player.webmanifest"
    result = fetch_text_url(url)
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
            "player_manifest_install_contract",
            "player companion manifest installs the bounded mobile turn companion",
            result["url"],
            f"manifest JSON parse failed: {error}",
            facts,
        )

    shortcuts = {str(item.get("short_name") or "") for item in manifest.get("shortcuts") or [] if isinstance(item, dict)}
    facts.update(
        {
            "id": manifest.get("id"),
            "name": manifest.get("name"),
            "short_name": manifest.get("short_name"),
            "start_url": manifest.get("start_url"),
            "scope": manifest.get("scope"),
            "display": manifest.get("display"),
            "theme_color": manifest.get("theme_color"),
            "shortcut_short_names": sorted(shortcuts),
            "icon_purposes": [item.get("purpose") for item in manifest.get("icons") or [] if isinstance(item, dict)],
        }
    )
    required = [
        (result["status_code"] == 200, "player manifest did not return HTTP 200"),
        (manifest.get("id") == "/mobile/player", "player manifest id mismatch"),
        (manifest.get("name") == "Chummer Player Companion", "player manifest name mismatch"),
        (manifest.get("start_url") == "/mobile/player?role=Player", "player manifest start_url must target player role"),
        (manifest.get("scope") == "/mobile/", "player manifest scope must remain under /mobile/"),
        (manifest.get("display") == "standalone", "player manifest display must be standalone"),
        (manifest.get("theme_color") == "#0f1b26", "player manifest theme color mismatch"),
        ({"Player", "GM"}.issubset(shortcuts), "player manifest shortcuts must include Player and GM"),
        (
            any("maskable" in str(item.get("purpose") or "") for item in manifest.get("icons") or [] if isinstance(item, dict)),
            "player manifest missing maskable icon",
        ),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "player_manifest_install_contract",
            "player companion manifest installs the bounded mobile turn companion",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "player_manifest_install_contract",
        "player companion manifest installs the bounded mobile turn companion",
        result["url"],
        facts,
    )


def check_player_manifest_route_targets() -> dict[str, Any]:
    manifest_url = f"{BASE_ORIGIN}/manifest.player.webmanifest"
    manifest_result = fetch_text_url(manifest_url)
    facts: dict[str, Any] = {
        "manifest_url": manifest_result["url"],
        "manifest_status_code": manifest_result["status_code"],
        "manifest_content_type": manifest_result["content_type"],
        "manifest_elapsed_ms": manifest_result["elapsed_ms"],
    }
    try:
        manifest = json.loads(manifest_result["body"])
    except json.JSONDecodeError as error:
        return failed_check(
            "player_manifest_route_targets_contract",
            "player companion manifest start URL and shortcuts render their advertised mobile role shells",
            manifest_url,
            f"manifest JSON parse failed: {error}",
            facts,
        )

    route_targets: list[tuple[str, str, str]] = [
        ("start_url", str(manifest.get("start_url") or ""), "Player"),
    ]
    for shortcut in manifest.get("shortcuts") or []:
        if not isinstance(shortcut, dict):
            continue
        short_name = str(shortcut.get("short_name") or "")
        url = str(shortcut.get("url") or "")
        expected_role = "GameMaster" if short_name == "GM" else short_name
        route_targets.append((f"shortcut:{short_name}", url, expected_role))

    target_facts: list[dict[str, Any]] = []
    failures: list[str] = []
    seen_targets: set[str] = set()
    for target_id, route, expected_role in route_targets:
        if not route:
            failures.append(f"{target_id} is missing its route")
            continue
        if not route.startswith("/mobile/"):
            failures.append(f"{target_id} route must stay under /mobile/, got {route!r}")
            continue
        if route in seen_targets:
            continue
        seen_targets.add(route)

        result = fetch_text_url(f"{BASE_ORIGIN}{route}")
        body = result["body"]
        manifest_hrefs = parse_manifest_hrefs(body)
        expected_manifest_href = (
            "/manifest.gm.webmanifest" if expected_role == "GameMaster" else "/manifest.player.webmanifest"
        )
        target_fact = {
            "id": target_id,
            "route": route,
            "url": result["url"],
            "final_url": result.get("final_url") or result["url"],
            "status_code": result["status_code"],
            "content_type": result["content_type"],
            "elapsed_ms": result["elapsed_ms"],
            "expected_role": expected_role,
            "has_turn_root": "data-turn-root" in body,
            "has_expected_role": f'data-role="{expected_role}"' in body,
            "has_expected_role_link": f'data-role-name="{expected_role}"' in body,
            "manifest_hrefs": manifest_hrefs,
            "expected_manifest_href": expected_manifest_href,
            "has_expected_manifest_link": expected_manifest_href in manifest_hrefs,
            "has_living_world_opt_in_boundary": 'data-living-world-opt-in-boundary="true"' in body,
            "has_owner_route_link": 'id="turn-owner-route-link"' in body,
            "mentions_black_ledger": "Black Ledger" in body,
            "mentions_heat": "heat" in body.lower(),
        }
        target_facts.append(target_fact)

        if result["status_code"] != 200:
            failures.append(f"{target_id} route {route} did not return HTTP 200")
        if "text/html" not in str(result["content_type"]):
            failures.append(f"{target_id} route {route} must return HTML")
        if not target_fact["has_turn_root"]:
            failures.append(f"{target_id} route {route} missing turn companion root")
        if not target_fact["has_expected_role"]:
            failures.append(f"{target_id} route {route} missing data-role={expected_role}")
        if not target_fact["has_expected_role_link"]:
            failures.append(f"{target_id} route {route} missing role navigation for {expected_role}")
        if not target_fact["has_expected_manifest_link"]:
            failures.append(f"{target_id} route {route} missing expected manifest link {expected_manifest_href}")
        if not target_fact["has_living_world_opt_in_boundary"]:
            failures.append(f"{target_id} route {route} missing living-world opt-in boundary")
        if not target_fact["has_owner_route_link"]:
            failures.append(f"{target_id} route {route} missing owner route link")
        if not target_fact["mentions_black_ledger"]:
            failures.append(f"{target_id} route {route} must mention Black Ledger")
        if not target_fact["mentions_heat"]:
            failures.append(f"{target_id} route {route} must mention heat")

    facts.update(
        {
            "target_count": len(target_facts),
            "targets": target_facts,
        }
    )
    if failures:
        return failed_check(
            "player_manifest_route_targets_contract",
            "player companion manifest start URL and shortcuts render their advertised mobile role shells",
            manifest_url,
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "player_manifest_route_targets_contract",
        "player companion manifest start URL and shortcuts render their advertised mobile role shells",
        manifest_url,
        facts,
    )


def check_mobile_living_world_boundary() -> dict[str, Any]:
    pwa_url = f"{BASE_ORIGIN}/mobile/pwa.json"
    ledger_url = f"{BASE_ORIGIN}/mobile/pwa/ledger.json"
    pwa_result = fetch_text_url(pwa_url)
    ledger_result = fetch_text_url(ledger_url)
    facts = {
        "pwa_url": pwa_result["url"],
        "pwa_status_code": pwa_result["status_code"],
        "pwa_content_type": pwa_result["content_type"],
        "pwa_elapsed_ms": pwa_result["elapsed_ms"],
        "ledger_url": ledger_result["url"],
        "ledger_status_code": ledger_result["status_code"],
        "ledger_content_type": ledger_result["content_type"],
        "ledger_elapsed_ms": ledger_result["elapsed_ms"],
    }
    try:
        pwa_payload = json.loads(pwa_result["body"])
        ledger_payload = json.loads(ledger_result["body"])
    except json.JSONDecodeError as error:
        return failed_check(
            "mobile_pwa_living_world_boundary",
            "mobile PWA exposes living-world update discovery while keeping Black Ledger data opt-in",
            ledger_url,
            f"JSON parse failed: {error}",
            facts,
        )

    living_world_data = pwa_payload.get("living_world_data") if isinstance(pwa_payload.get("living_world_data"), dict) else {}
    facts.update(
        {
            "pwa_mode": pwa_payload.get("mode"),
            "pwa_status": pwa_payload.get("status"),
            "pwa_living_world_updates_route": pwa_payload.get("living_world_updates_route"),
            "pwa_living_world_data_mode": living_world_data.get("mode"),
            "pwa_living_world_data_update_route": living_world_data.get("update_route"),
            "ledger_mode": ledger_payload.get("mode"),
            "ledger_status": ledger_payload.get("status"),
            "ledger_status_label": ledger_payload.get("status_label"),
            "ledger_summary": ledger_payload.get("summary"),
            "ledger_legal_posture": ledger_payload.get("legal_posture"),
            "ledger_opt_in_route": ledger_payload.get("opt_in_route"),
            "ledger_updates_route": ledger_payload.get("updates_route"),
        }
    )
    required = [
        (pwa_result["status_code"] == 200, "/mobile/pwa.json did not return HTTP 200"),
        (ledger_result["status_code"] == 200, "/mobile/pwa/ledger.json did not return HTTP 200"),
        (pwa_payload.get("mode") == "nexus_pan_mobile_pwa", "mobile PWA discovery mode mismatch"),
        (pwa_payload.get("status") == "live", "mobile PWA discovery status must be live"),
        (
            pwa_payload.get("living_world_updates_route") == "/mobile/pwa/ledger.json",
            "mobile PWA discovery must point at the ledger update route",
        ),
        (
            living_world_data.get("update_route") == "/mobile/pwa/ledger.json",
            "mobile PWA living-world data must point at the ledger update route",
        ),
        (ledger_payload.get("mode") == "mobile_pwa_living_world", "ledger mode mismatch"),
        (ledger_payload.get("status") == "opt_in_required", "ledger must require opt-in on the public lane"),
        (ledger_payload.get("opt_in_route") == "/account", "ledger opt-in route mismatch"),
        (ledger_payload.get("updates_route") == "/mobile/pwa/ledger.json", "ledger updates route mismatch"),
        ("Black Ledger" in str(ledger_payload.get("summary") or ""), "ledger summary must mention Black Ledger"),
        ("No private run table state is published" in str(ledger_payload.get("legal_posture") or ""), "ledger legal posture must keep private run table state unpublished"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "mobile_pwa_living_world_boundary",
            "mobile PWA exposes living-world update discovery while keeping Black Ledger data opt-in",
            ledger_url,
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "mobile_pwa_living_world_boundary",
        "mobile PWA exposes living-world update discovery while keeping Black Ledger data opt-in",
        ledger_url,
        facts,
    )


def check_account_ledger_notifications_opt_in_boundary() -> dict[str, Any]:
    url = f"{BASE_ORIGIN}/account/ledger/notifications"
    result = fetch_text_url(url)
    body = result["body"]
    final_url = str(result.get("final_url") or result["url"])
    encoded_next_path = "%2Faccount%2Fledger%2Fnotifications"
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "final_url": final_url,
        "redirected_to_login": "/login?next=" in final_url and encoded_next_path in final_url,
        "has_login_route_marker": 'data-route-key="login"' in body,
        "has_auth_surface_marker": 'data-surface-class="surface-auth surface-minimal surface-auth-login"' in body,
        "has_open_chummer_title": "<title>Open Chummer \u00b7 Chummer</title>" in body,
        "has_email_first_copy": "Email first. Google if you prefer." in body,
        "preserves_ledger_notifications_next": encoded_next_path in body,
        "mentions_private_run_state": "private run table state" in body.lower(),
    }
    required = [
        (result["status_code"] == 200, "account ledger notifications boundary did not return HTTP 200 after redirect"),
        ("text/html" in str(result["content_type"]), "account ledger notifications boundary must resolve to HTML"),
        (facts["redirected_to_login"], "account ledger notifications must redirect unauthenticated users to login with the ledger notifications next path"),
        (facts["has_login_route_marker"], "account ledger notifications login boundary missing login route marker"),
        (facts["has_auth_surface_marker"], "account ledger notifications login boundary missing auth surface marker"),
        (facts["has_open_chummer_title"], "account ledger notifications login boundary missing Open Chummer title"),
        (facts["has_email_first_copy"], "account ledger notifications login boundary missing user-facing sign-in copy"),
        (facts["preserves_ledger_notifications_next"], "account ledger notifications login boundary must preserve the next route"),
        (not facts["mentions_private_run_state"], "unauthenticated account ledger notifications boundary must not leak private run table state"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "account_ledger_notifications_opt_in_boundary",
            "account Black Ledger notifications stay behind the signed-in opt-in boundary without exposing private table state",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "account_ledger_notifications_opt_in_boundary",
        "account Black Ledger notifications stay behind the signed-in opt-in boundary without exposing private table state",
        result["url"],
        facts,
    )


def check_static_assets() -> dict[str, Any]:
    required_assets = [
        ("/app.css", "text/css", 1000),
        ("/icons/chummer-pwa.svg", "image/svg+xml", 100),
        ("/icons/chummer-pwa-maskable.svg", "image/svg+xml", 100),
        ("/media/chummer6/chummer6-hero-baseline.png", "image/png", 100),
        ("/media/chummer6/karma-forge-baseline.png", "image/png", 100),
    ]
    asset_facts: list[dict[str, Any]] = []
    failures: list[str] = []
    responsive_css_seen = False

    for path, expected_content_type, minimum_size in required_assets:
        result = fetch_bytes(path)
        body = result["body"]
        content_type = str(result["content_type"])
        size_bytes = len(body)
        fact = {
            "path": path,
            "url": result["url"],
            "status_code": result["status_code"],
            "content_type": content_type,
            "size_bytes": size_bytes,
            "elapsed_ms": result["elapsed_ms"],
        }
        asset_facts.append(fact)

        if result["status_code"] != 200:
            failures.append(f"{path} did not return HTTP 200")
        if expected_content_type not in content_type:
            failures.append(f"{path} content-type must include {expected_content_type}")
        if size_bytes < minimum_size:
            failures.append(f"{path} is unexpectedly small ({size_bytes} bytes)")

        if path == "/app.css":
            css_text = body.decode("utf-8", errors="replace")
            responsive_css_seen = "@media" in css_text and "max-width" in css_text

    if not responsive_css_seen:
        failures.append("app.css must include deployed responsive mobile media queries")

    facts = {
        "asset_count": len(asset_facts),
        "assets": asset_facts,
        "responsive_css_seen": responsive_css_seen,
    }
    if failures:
        return failed_check(
            "static_asset_fetch_contract",
            "deployed PWA static assets are fetchable under /blazor and include responsive shell CSS",
            BASE_URL,
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "static_asset_fetch_contract",
        "deployed PWA static assets are fetchable under /blazor and include responsive shell CSS",
        BASE_URL,
        facts,
    )


def check_mobile_viewport_shell() -> dict[str, Any]:
    result = fetch_text("/app?source=pwa&viewport=mobile-proof")
    body = result["body"]
    probe = HeadProbe()
    probe.feed(body)
    viewport_content = next(
        (
            str(meta.get("content") or "")
            for meta in probe.metas
            if meta.get("name") == "viewport"
        ),
        "",
    )
    base_hrefs = [base.get("href") for base in probe.bases]
    facts = {
        "status_code": result["status_code"],
        "content_type": result["content_type"],
        "elapsed_ms": result["elapsed_ms"],
        "viewport_content": viewport_content,
        "base_hrefs": base_hrefs,
        "has_mobile_web_app_capable": any(
            meta.get("name") == "mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_apple_mobile_web_app_capable": any(
            meta.get("name") == "apple-mobile-web-app-capable" and meta.get("content") == "yes"
            for meta in probe.metas
        ),
        "has_pwa_install_state_object": "window.chummerPwa" in body,
    }
    required = [
        (result["status_code"] == 200, "mobile app route did not return HTTP 200"),
        ("text/html" in str(result["content_type"]), "mobile app route must return HTML"),
        (viewport_content == "width=device-width, initial-scale=1.0", "viewport meta must stay mobile-width and initial-scale 1.0"),
        ("/blazor/" in {str(item or "") for item in base_hrefs}, "base href must stay scoped to /blazor/"),
        (facts["has_mobile_web_app_capable"], "mobile route missing mobile-web-app-capable meta"),
        (facts["has_apple_mobile_web_app_capable"], "mobile route missing apple mobile web app meta"),
        (facts["has_pwa_install_state_object"], "mobile route missing PWA install state object"),
    ]
    failures = [message for passed, message in required if not passed]
    if failures:
        return failed_check(
            "mobile_viewport_shell_contract",
            "deployed PWA app route exposes a mobile viewport and scoped install shell under /blazor",
            result["url"],
            "; ".join(failures),
            facts,
        )

    return passed_check(
        "mobile_viewport_shell_contract",
        "deployed PWA app route exposes a mobile viewport and scoped install shell under /blazor",
        result["url"],
        facts,
    )


def main() -> int:
    checks = [
        check_manifest(),
        check_service_worker(),
        check_offline_shell(),
        check_app_head(),
        check_clean_public_entry_route(),
        check_pwa_alias_route(),
        check_mobile_player_shell_route(),
        check_player_manifest(),
        check_player_manifest_route_targets(),
        check_mobile_living_world_boundary(),
        check_account_ledger_notifications_opt_in_boundary(),
        check_static_assets(),
        check_mobile_viewport_shell(),
    ]
    failures = [check for check in checks if check["status"] != "passed"]
    receipt = {
        "contract_name": "chummer6-ui.blazor_pwa_public_edge_proof",
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": "failed" if failures else "passed",
        "base_url": BASE_URL,
        "public_entry_url": PUBLIC_ENTRY_URL,
        "pwa_alias_url": PWA_ALIAS_URL,
        "mobile_player_url": MOBILE_PLAYER_URL,
        "proof_tier": "hosted_pwa_public_edge_execution",
        "route_lane": "blazor_pwa_play_shell",
        "checks": checks,
        "failures": failures,
        "notes": [
            "This receipt proves the deployed /blazor PWA shell contract, not app-store acceptance or offline runner-data parity.",
            "The /pwa route aliases the Hub Web player companion PWA, and the live /mobile player shell is proven separately from the /blazor workbench shell.",
            "The account Black Ledger notifications route is required to resolve through the signed-in opt-in boundary before private heat or table state is visible.",
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
