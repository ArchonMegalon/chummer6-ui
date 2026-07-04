#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from datetime import datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from typing import Any
from urllib.parse import urljoin, urlparse


REPO_ROOT = Path(__file__).resolve().parents[1]
PUBLISHED = REPO_ROOT / ".codex-studio" / "published"
OUTPUT_PATH = Path(
    os.environ.get(
        "CHUMMER_PUBLIC_EDGE_FLAGSHIP_PROOF_PATH",
        PUBLISHED / "CHUMMER6_PUBLIC_EDGE_FLAGSHIP_INTEGRATION.generated.json",
    )
)
BASE_ORIGIN = os.environ.get("CHUMMER_PUBLIC_EDGE_FLAGSHIP_BASE_URL", "https://chummer.run").rstrip("/")
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "ChummerPublicEdgeFlagshipProof/1.0"
)
CONTRACT_NAME = "chummer6-ui.public_edge_flagship_integration"


class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[no-untyped-def]
        return None


class HeadProbe(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.links: list[dict[str, str | None]] = []
        self.metas: list[dict[str, str | None]] = []
        self.bases: list[dict[str, str | None]] = []
        self.iframes: list[dict[str, str | None]] = []
        self.anchors: list[dict[str, str | None]] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        data = dict(attrs)
        if tag == "link":
            self.links.append(data)
        elif tag == "meta":
            self.metas.append(data)
        elif tag == "base":
            self.bases.append(data)
        elif tag == "iframe":
            self.iframes.append(data)
        elif tag == "a":
            self.anchors.append(data)


def now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def normalize(value: object) -> str:
    return str(value or "").strip()


def fetch_url(
    url: str,
    *,
    method: str = "GET",
    follow_redirects: bool = True,
    limit: int | None = None,
    timeout: int = 30,
) -> dict[str, Any]:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT}, method=method)
    opener = urllib.request.build_opener() if follow_redirects else urllib.request.build_opener(NoRedirectHandler)
    started = time.perf_counter()
    try:
        with opener.open(request, timeout=timeout) as response:
            body = b"" if method == "HEAD" else response.read(limit)
            return {
                "url": url,
                "final_url": response.url,
                "status_code": response.status,
                "content_type": response.headers.get("content-type") or "",
                "location": response.headers.get("location") or "",
                "elapsed_ms": round((time.perf_counter() - started) * 1000),
                "body": body,
            }
    except urllib.error.HTTPError as error:
        body = b"" if method == "HEAD" else error.read(limit)
        return {
            "url": url,
            "final_url": error.url,
            "status_code": error.code,
            "content_type": error.headers.get("content-type") or "",
            "location": error.headers.get("location") or "",
            "elapsed_ms": round((time.perf_counter() - started) * 1000),
            "body": body,
        }
    except (TimeoutError, urllib.error.URLError, OSError) as error:
        return {
            "url": url,
            "final_url": url,
            "status_code": 0,
            "content_type": "",
            "location": "",
            "elapsed_ms": round((time.perf_counter() - started) * 1000),
            "error": str(error),
            "body": b"",
        }


def fetch_text(path: str, *, follow_redirects: bool = True, limit: int | None = None) -> dict[str, Any]:
    result = fetch_url(urljoin(f"{BASE_ORIGIN}/", path.lstrip("/")), follow_redirects=follow_redirects, limit=limit)
    result["text"] = result["body"].decode("utf-8", errors="replace")
    result.pop("body", None)
    return result


def fetch_json(path: str, *, follow_redirects: bool = True) -> tuple[dict[str, Any], dict[str, Any]]:
    result = fetch_text(path, follow_redirects=follow_redirects)
    try:
        payload = json.loads(result["text"])
    except json.JSONDecodeError:
        payload = {}
    return result, payload if isinstance(payload, dict) else {}


def parse_head(body: str) -> HeadProbe:
    probe = HeadProbe()
    probe.feed(body)
    return probe


def manifest_hrefs(body: str) -> list[str]:
    probe = parse_head(body)
    hrefs = {
        normalize(link.get("href"))
        for link in probe.links
        if "manifest" in normalize(link.get("rel")).lower()
    }
    return sorted(href for href in hrefs if href)


def base_hrefs(body: str) -> list[str]:
    return [normalize(item.get("href")) for item in parse_head(body).bases if normalize(item.get("href"))]


def has_viewport(body: str) -> bool:
    for meta in parse_head(body).metas:
        if normalize(meta.get("name")).lower() == "viewport":
            return "width=device-width" in normalize(meta.get("content")).lower()
    return False


def same_origin_target(current_path: str, href: str) -> str:
    href = normalize(href)
    if not href or href.startswith(("#", "mailto:", "tel:", "javascript:")):
        return ""
    parsed = urlparse(urljoin(f"{BASE_ORIGIN}{current_path}", href))
    if parsed.netloc != urlparse(BASE_ORIGIN).netloc:
        return ""
    path = parsed.path or "/"
    return f"{path}?{parsed.query}" if parsed.query else path


def is_public_product_navigation_target(target: str) -> bool:
    parsed = urlparse(target)
    if parsed.path == "/blazor/preview" and parsed.query:
        return False
    return True


def passed_check(check_id: str, assertion: str, facts: dict[str, Any]) -> dict[str, Any]:
    return {"id": check_id, "status": "passed", "assertion": assertion, "facts": facts}


def failed_check(check_id: str, assertion: str, reason: str, facts: dict[str, Any]) -> dict[str, Any]:
    return {"id": check_id, "status": "failed", "assertion": assertion, "reason": reason, "facts": facts}


def check_release_channel() -> dict[str, Any]:
    channel_result, channel = fetch_json("/downloads/RELEASE_CHANNEL.generated.json")
    releases_result, releases = fetch_json("/downloads/releases.json")
    artifact_rows = channel.get("artifacts") if isinstance(channel.get("artifacts"), list) else []
    download_rows = releases.get("downloads") if isinstance(releases.get("downloads"), list) else []
    artifact_ids = {normalize(row.get("artifactId") or row.get("id")) for row in artifact_rows if isinstance(row, dict)}
    download_ids = {normalize(row.get("artifactId") or row.get("id")) for row in download_rows if isinstance(row, dict)}
    expected_ids = {"avalonia-linux-x64-installer", "avalonia-win-x64-installer"}
    facts = {
        "release_channel_url": channel_result["url"],
        "release_channel_status_code": channel_result["status_code"],
        "releases_url": releases_result["url"],
        "releases_status_code": releases_result["status_code"],
        "status": channel.get("status"),
        "version": channel.get("version") or channel.get("releaseVersion"),
        "releases_version": releases.get("version") or releases.get("releaseVersion"),
        "channel": channel.get("channel") or channel.get("channelId"),
        "releases_channel": releases.get("channel") or releases.get("channelId"),
        "published_at": channel.get("publishedAt"),
        "artifact_ids": sorted(artifact_ids),
        "download_ids": sorted(download_ids),
    }
    failures = []
    if channel_result["status_code"] != 200 or releases_result["status_code"] != 200:
        failures.append("download release manifests must return HTTP 200")
    if channel.get("status") != "published" or releases.get("status") != "published":
        failures.append("download release manifests must be published")
    if facts["version"] != facts["releases_version"] or not facts["version"]:
        failures.append("release manifests must agree on a non-empty version")
    if facts["channel"] != facts["releases_channel"] or not facts["channel"]:
        failures.append("release manifests must agree on a non-empty channel")
    if not expected_ids.issubset(artifact_ids) or not expected_ids.issubset(download_ids):
        failures.append("release manifests must expose Linux and Windows Avalonia installer rows")
    return (
        failed_check("release_channel_contract", "live downloads manifests agree on the current published desktop preview", "; ".join(failures), facts)
        if failures
        else passed_check("release_channel_contract", "live downloads manifests agree on the current published desktop preview", facts)
    )


def check_download_routes() -> dict[str, Any]:
    page = fetch_text("/downloads/")
    _, channel = fetch_json("/downloads/RELEASE_CHANNEL.generated.json")
    version = normalize(channel.get("version") or channel.get("releaseVersion"))
    route_facts = []
    failures = []
    for artifact_id in ("avalonia-linux-x64-installer", "avalonia-win-x64-installer"):
        result = fetch_url(
            f"{BASE_ORIGIN}/downloads/install/{artifact_id}",
            method="HEAD",
            follow_redirects=False,
        )
        location = normalize(result.get("location"))
        route_facts.append(
            {
                "artifact_id": artifact_id,
                "status_code": result["status_code"],
                "location": location,
                "elapsed_ms": result["elapsed_ms"],
            }
        )
        if result["status_code"] != 302:
            failures.append(f"{artifact_id} install route must return HTTP 302")
        if location != f"/downloads/get/{artifact_id}":
            failures.append(f"{artifact_id} install route must redirect to governed get route")
    body = page["text"]
    facts = {
        "downloads_page_status_code": page["status_code"],
        "downloads_page_content_type": page["content_type"],
        "downloads_page_has_viewport": has_viewport(body),
        "downloads_page_mentions_version": bool(version and version in body),
        "downloads_page_mentions_linux": "avalonia-linux-x64-installer" in body,
        "downloads_page_mentions_windows": "avalonia-win-x64-installer" in body,
        "version": version,
        "install_routes": route_facts,
    }
    if page["status_code"] != 200:
        failures.append("downloads page must return HTTP 200")
    if not facts["downloads_page_has_viewport"]:
        failures.append("downloads page must expose a mobile viewport")
    if not facts["downloads_page_mentions_version"]:
        failures.append("downloads page must show the current version")
    if not facts["downloads_page_mentions_linux"] or not facts["downloads_page_mentions_windows"]:
        failures.append("downloads page must show Linux and Windows installer rows")
    return (
        failed_check("download_install_routes_contract", "downloads page and install routes expose the current nightly safely", "; ".join(failures), facts)
        if failures
        else passed_check("download_install_routes_contract", "downloads page and install routes expose the current nightly safely", facts)
    )


def check_navigation_routes() -> dict[str, Any]:
    routes = {
        "/": {"needles": ["/app", "/downloads", "Build"]},
        "/app": {"needles": ["Chummer Online", 'base href="/blazor/"']},
        "/participate": {"needles": ["data-chummer-participate-frame", '<iframe']},
        "/status": {"needles": ["/app", "/downloads"]},
        "/help": {"needles": ["/app", "/downloads"]},
    }
    facts: list[dict[str, Any]] = []
    failures: list[str] = []
    for route, spec in routes.items():
        result = fetch_text(route)
        body = result["text"]
        route_fact = {
            "route": route,
            "status_code": result["status_code"],
            "content_type": result["content_type"],
            "elapsed_ms": result["elapsed_ms"],
            "has_viewport": has_viewport(body),
            "missing_needles": [needle for needle in spec["needles"] if needle not in body],
        }
        facts.append(route_fact)
        if result["status_code"] != 200:
            failures.append(f"{route} must return HTTP 200")
        if "text/html" not in result["content_type"]:
            failures.append(f"{route} must return HTML")
        if not route_fact["has_viewport"]:
            failures.append(f"{route} must expose a mobile viewport")
        if route_fact["missing_needles"]:
            failures.append(f"{route} missing expected markers: {', '.join(route_fact['missing_needles'])}")
    return (
        failed_check("public_navigation_contract", "public navigation routes remain reachable, mobile-safe, and product-shaped", "; ".join(failures), {"routes": facts})
        if failures
        else passed_check("public_navigation_contract", "public navigation routes remain reachable, mobile-safe, and product-shaped", {"routes": facts})
    )


def check_public_navigation_link_graph() -> dict[str, Any]:
    seed_routes = [
        "/",
        "/downloads/",
        "/status",
        "/help",
        "/participate",
        "/play",
        "/play/continuity",
        "/mobile",
        "/blazor/",
        "/blazor/app",
    ]
    seed_facts: list[dict[str, Any]] = []
    target_map: dict[str, set[str]] = {}
    excluded_targets: set[str] = set()
    failures: list[str] = []
    for seed in seed_routes:
        result = fetch_text(seed)
        body = result["text"]
        probe = parse_head(body)
        links: list[tuple[str, str]] = []
        for anchor in probe.anchors:
            target = same_origin_target(seed, normalize(anchor.get("href")))
            if target:
                if is_public_product_navigation_target(target):
                    links.append(("a", target))
                else:
                    excluded_targets.add(target)
        for iframe in probe.iframes:
            target = same_origin_target(seed, normalize(iframe.get("src")))
            if target:
                if is_public_product_navigation_target(target):
                    links.append(("iframe", target))
                else:
                    excluded_targets.add(target)

        unique_links = sorted(set(links), key=lambda item: (item[1], item[0]))
        target_map[seed] = {target for _, target in unique_links}
        seed_facts.append(
            {
                "route": seed,
                "status_code": result["status_code"],
                "content_type": result["content_type"],
                "has_viewport": has_viewport(body),
                "same_origin_anchor_iframe_link_count": len(unique_links),
                "same_origin_anchor_iframe_links": [
                    {"kind": kind, "target": target} for kind, target in unique_links
                ],
            }
        )
        if result["status_code"] != 200:
            failures.append(f"{seed} seed route must return HTTP 200 before link graph probing")
        if not unique_links:
            failures.append(f"{seed} seed route must expose at least one same-origin navigation link")

    observed_targets = sorted({target for targets in target_map.values() for target in targets})
    required_targets = {
        "/account",
        "/build",
        "/downloads",
        "/downloads/get/avalonia-linux-x64-installer",
        "/downloads/get/avalonia-win-x64-installer",
        "/help",
        "/participate/board?embed=1",
        "/play/continuity",
        "/play/continuity/history",
        "/mobile/player?sessionId=session-main&role=Player",
        "/mobile/gm?sessionId=session-main&role=GameMaster",
    }
    missing_required = sorted(required_targets - set(observed_targets))
    if missing_required:
        failures.append(f"public navigation graph is missing required targets: {', '.join(missing_required)}")

    def probe_target(target: str) -> dict[str, Any]:
        target_url = f"{BASE_ORIGIN}{target}"
        result = fetch_url(target_url, method="HEAD", follow_redirects=False, limit=512, timeout=10)
        method = "HEAD"
        if result["status_code"] == 405:
            result = fetch_url(target_url, method="GET", follow_redirects=False, limit=512, timeout=10)
            method = "GET"
        return {
            "target": target,
            "method": method,
            "status_code": int(result["status_code"]),
            "content_type": result["content_type"],
            "location": result["location"],
            "elapsed_ms": result["elapsed_ms"],
            "error": result.get("error") or "",
        }

    target_facts = []
    with ThreadPoolExecutor(max_workers=12) as executor:
        futures = {executor.submit(probe_target, target): target for target in sorted(required_targets)}
        for future in as_completed(futures):
            target_facts.append(future.result())
    target_facts.sort(key=lambda item: item["target"])
    for target_fact in target_facts:
        status_code = int(target_fact["status_code"])
        if status_code < 200 or status_code >= 400:
            detail = f" ({target_fact['error']})" if target_fact.get("error") else ""
            failures.append(f"{target_fact['target']} linked from public navigation returned HTTP {status_code}{detail}")

    facts = {
        "seed_route_count": len(seed_routes),
        "seed_routes": seed_facts,
        "discovered_unique_target_count": len(observed_targets),
        "probed_required_target_count": len(target_facts),
        "targets": target_facts,
        "excluded_dense_proof_target_count": len(excluded_targets),
        "excluded_dense_proof_targets_sample": sorted(excluded_targets)[:20],
        "required_targets": sorted(required_targets),
        "missing_required_targets": missing_required,
        "probe_policy": "discover same-origin anchors and iframes from public product routes; probe required navigation targets only; stylesheet/icon link tags are covered by static asset proof; dense /blazor/workbench and /blazor/preview command links stay under browser execution/horizon proof",
    }
    return (
        failed_check("public_navigation_link_graph_contract", "same-origin public navigation links resolve without broken public-edge targets", "; ".join(failures), facts)
        if failures
        else passed_check("public_navigation_link_graph_contract", "same-origin public navigation links resolve without broken public-edge targets", facts)
    )


def check_blazor_runtime() -> dict[str, Any]:
    route_facts = []
    failures = []
    for route in ("/blazor/", "/blazor/app"):
        result = fetch_text(route, limit=262_144)
        body = result["text"]
        fact = {
            "route": route,
            "status_code": result["status_code"],
            "content_type": result["content_type"],
            "elapsed_ms": result["elapsed_ms"],
            "has_viewport": has_viewport(body),
            "base_hrefs": base_hrefs(body),
            "has_chummer_online_title": "Chummer Online" in body,
        }
        route_facts.append(fact)
        if result["status_code"] != 200:
            failures.append(f"{route} must return HTTP 200")
        if not fact["has_viewport"]:
            failures.append(f"{route} must expose a mobile viewport")
        if "/blazor/" not in fact["base_hrefs"]:
            failures.append(f"{route} must keep base href /blazor/")
        if not fact["has_chummer_online_title"]:
            failures.append(f"{route} must identify Chummer Online")

    health_result, health = fetch_json("/blazor/health")
    analytics = health.get("analytics") if isinstance(health.get("analytics"), dict) else {}
    facts = {
        "routes": route_facts,
        "health_status_code": health_result["status_code"],
        "health_content_type": health_result["content_type"],
        "health_ok": health.get("ok"),
        "health_service": health.get("service"),
        "health_path_base": health.get("pathBase"),
        "analytics_session_replay_policy": analytics.get("sessionReplayPolicy"),
        "analytics_autocapture_policy": analytics.get("autocapturePolicy"),
        "analytics_sensitive_data_policy": analytics.get("sensitiveDataPolicy"),
        "workbench_route_boundary": "/blazor/workbench is covered by hosted route-entry and hosted execution receipts; this lightweight flagship check probes fast runtime readiness routes",
    }
    if health_result["status_code"] != 200:
        failures.append("/blazor/health must return HTTP 200")
    if health.get("ok") is not True or health.get("pathBase") != "/blazor":
        failures.append("/blazor/health must report a healthy /blazor runtime")
    if analytics.get("sessionReplayPolicy") != "disabled" or analytics.get("autocapturePolicy") != "disabled":
        failures.append("analytics posture must keep session replay and autocapture disabled")
    if analytics.get("sensitiveDataPolicy") != "route-and-workflow-metadata-only":
        failures.append("analytics posture must stay metadata-only")
    return (
        failed_check("blazor_runtime_contract", "Blazor runtime routes and health endpoint are live with privacy-safe analytics posture", "; ".join(failures), facts)
        if failures
        else passed_check("blazor_runtime_contract", "Blazor runtime routes and health endpoint are live with privacy-safe analytics posture", facts)
    )


def check_api_session_continuity() -> dict[str, Any]:
    api_result, api = fetch_json("/api/health")
    play = fetch_text("/play")
    continuity = fetch_text("/play/continuity")
    history_result, history = fetch_json("/play/continuity/history")
    session_redirect = fetch_text("/session", follow_redirects=False)
    mobile_pwa_result, mobile_pwa = fetch_json("/mobile/pwa.json")

    receipts = history.get("receipts") if isinstance(history.get("receipts"), list) else []
    receipt_ids = [
        normalize(item.get("receiptId"))
        for item in receipts
        if isinstance(item, dict) and normalize(item.get("receiptId"))
    ]
    facts = {
        "api_health_status_code": api_result["status_code"],
        "api_health_ok": api.get("ok"),
        "api_health_service": api.get("service"),
        "api_health_status": api.get("status"),
        "play_status_code": play["status_code"],
        "play_has_viewport": has_viewport(play["text"]),
        "play_mentions_continuity": "continuity" in play["text"].lower(),
        "play_mentions_black_ledger": "Black Ledger" in play["text"],
        "play_mentions_heat": "heat" in play["text"].lower(),
        "continuity_status_code": continuity["status_code"],
        "continuity_has_viewport": has_viewport(continuity["text"]),
        "continuity_mentions_nexus_pan": "NEXUS-PAN" in continuity["text"],
        "continuity_mentions_claimed_install": "claimed install" in continuity["text"].lower(),
        "continuity_mentions_private_boundary": "private" in continuity["text"].lower(),
        "continuity_links_history": "/play/continuity/history" in continuity["text"],
        "history_status_code": history_result["status_code"],
        "history_receipt_ids": receipt_ids,
        "history_boundary": history.get("boundary"),
        "history_summary": history.get("summary"),
        "session_status_code": session_redirect["status_code"],
        "session_location": session_redirect["location"],
        "mobile_pwa_status_code": mobile_pwa_result["status_code"],
        "mobile_pwa_continuity_route": mobile_pwa.get("continuity_route"),
        "mobile_pwa_receipt_index_route": mobile_pwa.get("receipt_index_route"),
    }
    failures = []
    if api_result["status_code"] != 200 or api.get("ok") is not True or api.get("status") != "pass":
        failures.append("/api/health must report a passing public API dependency")
    if play["status_code"] != 200 or not facts["play_has_viewport"]:
        failures.append("/play must return a mobile-safe public player entry")
    if not facts["play_mentions_continuity"] or not facts["play_mentions_black_ledger"] or not facts["play_mentions_heat"]:
        failures.append("/play must surface continuity, Black Ledger, and heat entry copy")
    if continuity["status_code"] != 200 or not facts["continuity_has_viewport"]:
        failures.append("/play/continuity must return a mobile-safe public continuity page")
    if not facts["continuity_mentions_nexus_pan"] or not facts["continuity_mentions_claimed_install"]:
        failures.append("/play/continuity must expose NEXUS-PAN claimed-install continuity posture")
    if not facts["continuity_mentions_private_boundary"] or not facts["continuity_links_history"]:
        failures.append("/play/continuity must expose the private-data boundary and receipt history link")
    if history_result["status_code"] != 200 or len(receipt_ids) < 3:
        failures.append("/play/continuity/history must return live continuity receipt rows")
    if "Public continuity stays aggregate" not in normalize(history.get("boundary")):
        failures.append("/play/continuity/history must state the aggregate public continuity boundary")
    if session_redirect["status_code"] != 302 or session_redirect["location"] != "/play":
        failures.append("/session must redirect anonymous public entry to /play")
    if mobile_pwa.get("continuity_route") != "/play/continuity":
        failures.append("/mobile/pwa.json must advertise /play/continuity")
    if mobile_pwa.get("receipt_index_route") != "/play/continuity/history":
        failures.append("/mobile/pwa.json must advertise /play/continuity/history")
    return (
        failed_check("api_session_continuity_contract", "public API health and session continuity routes stay live, mobile-safe, and aggregate-bound", "; ".join(failures), facts)
        if failures
        else passed_check("api_session_continuity_contract", "public API health and session continuity routes stay live, mobile-safe, and aggregate-bound", facts)
    )


def check_pwa_mobile_shells() -> dict[str, Any]:
    route_specs = [
        ("/mobile", "Player", "/manifest.player.webmanifest"),
        ("/mobile/gm?role=GameMaster", "GameMaster", "/manifest.gm.webmanifest"),
    ]
    facts = []
    failures = []
    for route, expected_role, expected_manifest in route_specs:
        result = fetch_text(route)
        body = result["text"]
        fact = {
            "route": route,
            "status_code": result["status_code"],
            "content_type": result["content_type"],
            "elapsed_ms": result["elapsed_ms"],
            "expected_role": expected_role,
            "has_viewport": has_viewport(body),
            "has_turn_root": "data-turn-root" in body,
            "has_expected_role": f'data-role="{expected_role}"' in body,
            "manifest_hrefs": manifest_hrefs(body),
            "expected_manifest_href": expected_manifest,
            "mentions_black_ledger": "Black Ledger" in body,
            "mentions_heat": "heat" in body.lower(),
            "has_living_world_opt_in_boundary": 'data-living-world-opt-in-boundary="true"' in body,
        }
        facts.append(fact)
        if result["status_code"] != 200:
            failures.append(f"{route} must return HTTP 200")
        if not fact["has_viewport"] or not fact["has_turn_root"] or not fact["has_expected_role"]:
            failures.append(f"{route} must render the expected mobile role shell")
        if expected_manifest not in fact["manifest_hrefs"]:
            failures.append(f"{route} must link {expected_manifest}")
        if not fact["mentions_black_ledger"] or not fact["mentions_heat"] or not fact["has_living_world_opt_in_boundary"]:
            failures.append(f"{route} must show Black Ledger, heat, and opt-in boundary copy")

    pwa_redirect = fetch_text("/pwa", follow_redirects=False)
    player_manifest_result, player_manifest = fetch_json("/manifest.player.webmanifest")
    gm_manifest_result, gm_manifest = fetch_json("/manifest.gm.webmanifest")
    summary = {
        "routes": facts,
        "pwa_alias_status_code": pwa_redirect["status_code"],
        "pwa_alias_location": pwa_redirect["location"],
        "player_manifest_status_code": player_manifest_result["status_code"],
        "gm_manifest_status_code": gm_manifest_result["status_code"],
        "player_manifest_start_url": player_manifest.get("start_url"),
        "gm_manifest_start_url": gm_manifest.get("start_url"),
    }
    if pwa_redirect["status_code"] not in {200, 302}:
        failures.append("/pwa must render or redirect to the mobile shell")
    if pwa_redirect["status_code"] == 302 and pwa_redirect["location"] != "/mobile":
        failures.append("/pwa redirect must target /mobile")
    if player_manifest.get("start_url") != "/mobile/player?role=Player":
        failures.append("player manifest start_url must target the Player mobile shell")
    if gm_manifest.get("start_url") != "/mobile/gm?role=GameMaster":
        failures.append("GM manifest start_url must target the GM mobile shell")
    return (
        failed_check("pwa_mobile_role_shell_contract", "PWA mobile Player and GM shells are installable, role-correct, and living-world bounded", "; ".join(failures), summary)
        if failures
        else passed_check("pwa_mobile_role_shell_contract", "PWA mobile Player and GM shells are installable, role-correct, and living-world bounded", summary)
    )


def check_living_world_opt_in() -> dict[str, Any]:
    pwa_result, pwa = fetch_json("/mobile/pwa.json")
    ledger_result, ledger = fetch_json("/mobile/pwa/ledger.json")
    account_redirect = fetch_text("/account/ledger/notifications", follow_redirects=False)
    facts = {
        "pwa_status_code": pwa_result["status_code"],
        "pwa_mode": pwa.get("mode"),
        "pwa_status": pwa.get("status"),
        "pwa_living_world_updates_route": pwa.get("living_world_updates_route"),
        "ledger_status_code": ledger_result["status_code"],
        "ledger_mode": ledger.get("mode"),
        "ledger_status": ledger.get("status"),
        "ledger_status_label": ledger.get("status_label"),
        "ledger_summary": ledger.get("summary"),
        "ledger_legal_posture": ledger.get("legal_posture"),
        "ledger_opt_in_route": ledger.get("opt_in_route"),
        "account_notifications_status_code": account_redirect["status_code"],
        "account_notifications_location": account_redirect["location"],
    }
    failures = []
    if pwa_result["status_code"] != 200 or ledger_result["status_code"] != 200:
        failures.append("mobile PWA and ledger JSON routes must return HTTP 200")
    if pwa.get("status") != "live" or pwa.get("living_world_updates_route") != "/mobile/pwa/ledger.json":
        failures.append("mobile PWA JSON must advertise the living-world ledger route")
    if ledger.get("status") != "opt_in_required":
        failures.append("ledger route must remain opt-in required on the public lane")
    if "Black Ledger" not in normalize(ledger.get("summary")):
        failures.append("ledger route must mention Black Ledger")
    if "No private run table state is published" not in normalize(ledger.get("legal_posture")):
        failures.append("ledger route must state no private run table state is published")
    if account_redirect["status_code"] != 302 or not normalize(account_redirect["location"]).startswith("/login?next="):
        failures.append("account ledger notifications must redirect anonymous users to login")
    return (
        failed_check("living_world_opt_in_contract", "Black Ledger, heat, and account notification routes stay opt-in bounded on the public lane", "; ".join(failures), facts)
        if failures
        else passed_check("living_world_opt_in_contract", "Black Ledger, heat, and account notification routes stay opt-in bounded on the public lane", facts)
    )


def check_static_assets() -> dict[str, Any]:
    asset_paths = [
        "/blazor/app.css",
        "/blazor/icons/chummer-pwa.svg",
        "/blazor/icons/chummer-pwa-maskable.svg",
        "/blazor/media/chummer6/chummer6-hero-baseline.png",
        "/blazor/manifest.webmanifest",
        "/blazor/service-worker.js",
        "/blazor/offline.html",
    ]
    assets = []
    failures = []
    for path in asset_paths:
        result = fetch_url(f"{BASE_ORIGIN}{path}", limit=2_000_000)
        body = result["body"]
        asset = {
            "path": path,
            "status_code": result["status_code"],
            "content_type": result["content_type"],
            "size_bytes": len(body),
            "elapsed_ms": result["elapsed_ms"],
        }
        if path.endswith("app.css"):
            text = body.decode("utf-8", errors="replace")
            asset["has_responsive_media"] = "@media" in text
        if path.endswith("service-worker.js"):
            text = body.decode("utf-8", errors="replace")
            asset["excludes_api"] = "path.includes('/api/')" in text
            asset["excludes_workspaces"] = "path.includes('/workspaces/')" in text
            asset["excludes_sessions"] = "path.includes('/session/')" in text
        if path.endswith("offline.html"):
            text = body.decode("utf-8", errors="replace")
            asset["states_runner_data_not_cached"] = "Your runner data is not cached" in text
            asset["mentions_black_ledger"] = "Black Ledger" in text
            asset["mentions_heat"] = "heat" in text
        assets.append(asset)
        if result["status_code"] != 200:
            failures.append(f"{path} must return HTTP 200")
        if len(body) <= 0:
            failures.append(f"{path} must not be empty")
    sw = next(item for item in assets if item["path"].endswith("service-worker.js"))
    offline = next(item for item in assets if item["path"].endswith("offline.html"))
    css = next(item for item in assets if item["path"].endswith("app.css"))
    if not css.get("has_responsive_media"):
        failures.append("PWA CSS must retain responsive media rules")
    if not sw.get("excludes_api") or not sw.get("excludes_workspaces") or not sw.get("excludes_sessions"):
        failures.append("service worker must exclude API, workspace, and session data")
    if not offline.get("states_runner_data_not_cached") or not offline.get("mentions_black_ledger") or not offline.get("mentions_heat"):
        failures.append("offline shell must state runner/living-world privacy boundaries")
    return (
        failed_check("static_asset_and_offline_boundary_contract", "static assets, service worker, and offline shell stay deployed and privacy bounded", "; ".join(failures), {"assets": assets})
        if failures
        else passed_check("static_asset_and_offline_boundary_contract", "static assets, service worker, and offline shell stay deployed and privacy bounded", {"assets": assets})
    )


def load_local_receipt(path: Path) -> dict[str, Any]:
    if not path.is_file():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError:
        return {}
    return payload if isinstance(payload, dict) else {}


def check_receipt_horizons() -> dict[str, Any]:
    receipt_specs = [
        ("hosted_route_entry", PUBLISHED / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.generated.json", {"passed", "ready"}),
        ("hosted_execution_horizon", PUBLISHED / "BLAZOR_PUBLIC_EDGE_EXECUTION_HORIZON.generated.json", {"passed"}),
        ("hosted_pwa_play_shell", PUBLISHED / "BLAZOR_PWA_PUBLIC_EDGE_PROOF.generated.json", {"passed"}),
        ("aggregate_browser_lane", PUBLISHED / "BLAZOR_BROWSER_LANE_PROOF_SET.generated.json", {"passed"}),
        ("external_host_blockers", PUBLISHED / "UI_EXTERNAL_HOST_PROOF_BLOCKERS.generated.json", {"ready"}),
    ]
    receipts = []
    failures = []
    for receipt_id, path, allowed_statuses in receipt_specs:
        payload = load_local_receipt(path)
        status = normalize(payload.get("status")).lower()
        generated = normalize(payload.get("generated_at") or payload.get("generatedAt"))
        receipt = {
            "id": receipt_id,
            "path": str(path),
            "present": bool(payload),
            "contract_name": payload.get("contract_name") or payload.get("contractName"),
            "status": status,
            "generated_at": generated,
        }
        receipts.append(receipt)
        if not payload:
            failures.append(f"{receipt_id} receipt is missing")
        elif status not in allowed_statuses:
            failures.append(f"{receipt_id} receipt status {status!r} is not allowed")
    facts = {
        "receipts": receipts,
        "horizons": [
            {
                "id": "near_term_stabilization",
                "status": "preview_deployed",
                "evidence": [
                    "release_channel_contract",
                    "download_install_routes_contract",
                    "public_navigation_contract",
                    "public_navigation_link_graph_contract",
                    "blazor_runtime_contract",
                    "api_session_continuity_contract",
                ],
                "stable_promotion_blockers": ["windows_visual_proof_missing"],
            },
            {
                "id": "mid_term_pwa_session_utility",
                "status": "pwa_shell_and_opt_in_boundary_proven",
                "evidence": [
                    "api_session_continuity_contract",
                    "pwa_mobile_role_shell_contract",
                    "living_world_opt_in_contract",
                    "static_asset_and_offline_boundary_contract",
                ],
                "remaining_work": [
                    "authenticated in-session inventory, health, ammo, modifier, and dice utility workflow proof",
                    "full-scope hosted browser execution matrix",
                ],
            },
            {
                "id": "long_term_living_world_expansion",
                "status": "public_opt_in_boundary_proven_expansion_not_claimed",
                "evidence": ["living_world_opt_in_contract", "blazor_runtime_contract"],
                "remaining_work": [
                    "authenticated Black Ledger opt-in flow",
                    "GM-governed heat/session update execution proof",
                    "stable/gold Windows visual proof",
                ],
            },
        ],
        "claim_boundaries": {
            "does_not_claim_stable_gold": True,
            "does_not_claim_full_desktop_parity": True,
            "does_not_claim_authenticated_living_world_execution": True,
        },
    }
    return (
        failed_check("receipt_horizon_contract", "local receipts define the working horizons without upgrading preview evidence to stable or full parity", "; ".join(failures), facts)
        if failures
        else passed_check("receipt_horizon_contract", "local receipts define the working horizons without upgrading preview evidence to stable or full parity", facts)
    )


def main() -> int:
    checks = [
        check_release_channel(),
        check_download_routes(),
        check_navigation_routes(),
        check_public_navigation_link_graph(),
        check_blazor_runtime(),
        check_api_session_continuity(),
        check_pwa_mobile_shells(),
        check_living_world_opt_in(),
        check_static_assets(),
        check_receipt_horizons(),
    ]
    failures = [f"{check['id']}: {check.get('reason')}" for check in checks if check.get("status") != "passed"]
    payload = {
        "contract_name": CONTRACT_NAME,
        "generated_at": now_iso(),
        "status": "failed" if failures else "passed",
        "base_url": BASE_ORIGIN,
        "scope": "live-public-edge-preview-integration-not-stable-gold",
        "checks": checks,
        "required_check_ids": [check["id"] for check in checks],
        "promotion_boundaries": {
            "nightly_preview_is_not_stable": True,
            "windows_visual_proof_required_for_stable_gold": True,
            "authenticated_living_world_execution_not_claimed": True,
            "full_desktop_parity_not_claimed_by_this_receipt": True,
        },
        "failures": failures,
    }
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    if failures:
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 1
    print(f"chummer6_public_edge_flagship_integration:ok {OUTPUT_PATH}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
