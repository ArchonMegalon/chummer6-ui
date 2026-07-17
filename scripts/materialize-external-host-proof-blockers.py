#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import subprocess
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any
from urllib.parse import urlparse


UTC = dt.timezone.utc
REPO_ROOT = Path(__file__).resolve().parents[1]
CONTRACT_NAME = "chummer6-ui.external_host_proof_blockers"
PUBLIC_EDGE_BROWSER_CONTRACT_NAME = "chummer6-ui.blazor_public_edge_workbench_proof"
PUBLIC_EDGE_BROWSER_CONTRACT_DOC_PATH = REPO_ROOT / "docs" / "BLAZOR_PUBLIC_EDGE_WORKBENCH_PROOF.md"
PUBLIC_EDGE_BROWSER_STATUS_SUMMARY_PATH = REPO_ROOT / "scripts" / "print_blazor_public_edge_proof_status.py"
PUBLIC_EDGE_BROWSER_VERIFIER_PATH = REPO_ROOT / "scripts" / "verify_blazor_public_edge_workbench_proof.py"
PUBLIC_EDGE_BROWSER_VERIFIER_WRAPPER_PATH = (
    REPO_ROOT / "scripts" / "ai" / "milestones" / "blazor-public-edge-workbench-proof-check.sh"
)
DEFAULT_BROWSER_ROUTES = [
    "/app",
    "/app?command=character_roster",
    "/blazor/",
    "/blazor/health",
    "/blazor/home",
    "/blazor/app",
    "/blazor/workbench",
    "/blazor/workbench?workspace=ws-1",
    "/blazor/workbench?command=new_character",
    "/blazor/workbench?command=new_character_origin",
    "/blazor/workbench?command=character_roster",
    "/blazor/workbench?command=master_index",
    "/blazor/workbench?command=open_character",
    "/blazor/workbench?command=open_for_printing",
    "/blazor/workbench?command=open_for_export",
    "/blazor/preview?command=new_character",
    "/blazor/workbench?workspace=ws-1&command=save_character_as",
    "/blazor/workbench?workspace=ws-1&command=export_character&dialog_action=download",
    "/blazor/workbench?workspace=ws-1&command=print_character",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-contacts&control=contact_add&dialog_action=add",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add",
    "/blazor/workbench?workspace=ws-1&tab=tab-technomancer&control=complex_form_add&dialog_action=add",
]
EXPANDED_ROUTE_PROOF_MARKERS = {
    "public_startup_workbench_command_routes",
    "public_advanced_action_routes",
    "public_advanced_committed_action_routes",
}
DEFAULT_WINDOWS_EXIT_GATE = REPO_ROOT / ".codex-studio" / "published" / "UI_WINDOWS_DESKTOP_EXIT_GATE.generated.json"


def classify_route_entry_proof_shape(payload: dict[str, Any]) -> str:
    marker_ids = {
        str(item).strip()
        for item in (payload.get("route_proof_markers") or [])
        if str(item).strip()
    }
    if EXPANDED_ROUTE_PROOF_MARKERS.issubset(marker_ids):
        return "expanded"
    if EXPANDED_ROUTE_PROOF_MARKERS & marker_ids:
        return "partial-expanded"
    if marker_ids:
        return "core"
    return ""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize repo-local external host proof blocker summary from the canonical release channel."
    )
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--downloads-dir", required=True, type=Path)
    parser.add_argument("--startup-smoke-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--windows-exit-gate", type=Path, default=DEFAULT_WINDOWS_EXIT_GATE)
    parser.add_argument("--browser-proof-output", type=Path)
    parser.add_argument("--display-manifest", type=Path)
    parser.add_argument("--display-downloads-dir", type=Path)
    parser.add_argument("--display-startup-smoke-dir", type=Path)
    parser.add_argument("--base-url", default="https://chummer.run")
    parser.add_argument("--timeout-seconds", type=int, default=10)
    parser.add_argument("--max-receipt-age-seconds", type=int, default=604800)
    parser.add_argument("--skip-public-route-check", action="store_true")
    parser.add_argument(
        "--browser-route",
        dest="browser_routes",
        action="append",
        default=[],
        help="Additional hosted browser-workbench route to probe. Defaults are added automatically.",
    )
    return parser.parse_args()


def utc_now_iso() -> str:
    return dt.datetime.now(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def format_utc_iso(value: dt.datetime | None) -> str:
    if value is None:
        return ""
    return value.astimezone(UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise SystemExit(f"expected JSON object in {path}")
    return payload


def load_optional_json(path: Path | None) -> dict[str, Any]:
    if path is None or not path.is_file():
        return {}
    try:
        return load_json(path)
    except Exception:
        return {}


def norm(value: Any) -> str:
    return str(value or "").strip().lower()


def parse_utc(value: Any) -> dt.datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    try:
        parsed = dt.datetime.fromisoformat(text)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=UTC)
    return parsed.astimezone(UTC)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().lower()


def normalize_sha256(value: Any) -> str:
    normalized = norm(value).replace("sha256:", "")
    return normalized


def check_public_route(*, base_url: str, route: str, timeout_seconds: int) -> dict[str, Any]:
    route = str(route or "").strip()
    if not route:
        return {
            "checked": False,
            "url": "",
            "http_status": None,
            "ok": False,
            "error": "missing_route",
        }
    url = f"{base_url.rstrip('/')}/{route.lstrip('/')}"
    timeout = max(timeout_seconds, 1)
    route_text = route.lower()
    if "dialog_action=add" in route_text:
        timeout = max(timeout, 30)
    elif "control=" in route_text:
        timeout = max(timeout, 20)
    curl_user_agent = (
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
        "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36"
    )
    for attempt in range(2):
        effective_timeout = timeout if attempt == 0 else max(timeout * 2, timeout + 5)
        try:
            completed = subprocess.run(
                [
                    "curl",
                    "--silent",
                    "--show-error",
                    "--location",
                    "--output",
                    "/dev/null",
                    "--write-out",
                    "%{http_code}",
                    "--user-agent",
                    curl_user_agent,
                    "--header",
                    "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
                    "--header",
                    "Accept-Language: en-US,en;q=0.9",
                    "--header",
                    "Cache-Control: no-cache",
                    "--header",
                    "Pragma: no-cache",
                    "--max-time",
                    str(effective_timeout),
                    url,
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        except Exception as exc:  # pragma: no cover - defensive
            return {
                "checked": True,
                "url": url,
                "http_status": None,
                "ok": False,
                "error": str(exc),
            }

        status_text = (completed.stdout or "").strip()
        status = int(status_text) if status_text.isdigit() else None
        if completed.returncode == 0 and status is not None:
            return {
                "checked": True,
                "url": url,
                "http_status": status,
                "ok": 200 <= status < 400,
                "error": "",
            }

        stderr_text = (completed.stderr or "").strip() or f"curl_exit_{completed.returncode}"
        if attempt == 0 and ("timed out" in stderr_text.lower() or completed.returncode == 28):
            continue
        return {
            "checked": True,
            "url": url,
            "http_status": status,
            "ok": False,
            "error": stderr_text,
        }

    return {
        "checked": True,
        "url": url,
        "http_status": None,
        "ok": False,
        "error": "timed out",
    }


def installer_access_class(
    *, manifest: dict[str, Any], tuple_id: str, artifact_id: str, installer_name: str
) -> str:
    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        return ""

    tuple_parts = tuple_id.split(":")
    tuple_head = norm(tuple_parts[0]) if len(tuple_parts) > 0 else ""
    tuple_rid = norm(tuple_parts[1]) if len(tuple_parts) > 1 else ""
    tuple_platform = norm(tuple_parts[2]) if len(tuple_parts) > 2 else ""
    expected_artifact_id = norm(artifact_id)
    expected_installer_name = norm(installer_name)

    for item in artifacts:
        if not isinstance(item, dict):
            continue
        item_artifact_id = norm(item.get("artifactId") or item.get("id"))
        item_head = norm(item.get("head"))
        item_rid = norm(item.get("rid"))
        item_platform = norm(item.get("platform"))
        item_file_name = norm(item.get("fileName"))
        if expected_artifact_id and item_artifact_id == expected_artifact_id:
            return norm(item.get("installAccessClass"))
        if expected_installer_name and item_file_name == expected_installer_name:
            return norm(item.get("installAccessClass"))
        if tuple_head and tuple_rid and tuple_platform:
            if item_head == tuple_head and item_rid == tuple_rid and item_platform == tuple_platform:
                return norm(item.get("installAccessClass"))

    return ""


def find_installer_artifact(*, manifest: dict[str, Any], tuple_id: str, artifact_id: str) -> dict[str, Any] | None:
    tuple_parts = tuple_id.split(":")
    tuple_head = norm(tuple_parts[0]) if len(tuple_parts) > 0 else ""
    tuple_rid = norm(tuple_parts[1]) if len(tuple_parts) > 1 else ""
    tuple_platform = norm(tuple_parts[2]) if len(tuple_parts) > 2 else ""

    artifacts = manifest.get("artifacts")
    if not isinstance(artifacts, list):
        return None

    expected_artifact_id = norm(artifact_id)
    for item in artifacts:
        if not isinstance(item, dict):
            continue
        item_artifact_id = norm(item.get("artifactId") or item.get("id"))
        item_head = norm(item.get("head"))
        item_rid = norm(item.get("rid"))
        item_platform = norm(item.get("platform"))
        if expected_artifact_id and item_artifact_id == expected_artifact_id:
            return item
        if tuple_head and tuple_rid and tuple_platform:
            if item_head == tuple_head and item_rid == tuple_rid and item_platform == tuple_platform:
                return item
    return None


def fallback_install_access_class(*, tuple_id: str, route: str, required_host: str) -> str:
    route_token = str(route or "").strip().lower()
    if not route_token.startswith("/downloads/install/"):
        return ""

    tuple_parts = tuple_id.split(":")
    tuple_platform = norm(tuple_parts[2]) if len(tuple_parts) > 2 else ""
    host_token = norm(required_host)
    if tuple_platform == "macos" or host_token == "macos":
        return "account_required"

    return ""


def find_public_install_route(*, manifest: dict[str, Any], tuple_id: str, artifact: dict[str, Any] | None) -> str:
    desktop_truth = ((manifest.get("desktopTupleCoverage") or {}).get("desktopRouteTruth") or [])
    if isinstance(desktop_truth, list):
        for row in desktop_truth:
            if not isinstance(row, dict):
                continue
            if str(row.get("tupleId") or "").strip() == tuple_id:
                route = str(row.get("publicInstallRoute") or "").strip()
                if route:
                    return route

    if isinstance(artifact, dict):
        download_url = str(artifact.get("downloadUrl") or "").strip()
        if download_url:
            parsed = urlparse(download_url)
            if parsed.path:
                return parsed.path
    return ""


def boolish(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    token = norm(value)
    return token in {"1", "true", "yes", "on"}


def materialize_windows_visual_proof_backlog_row(
    *,
    manifest: dict[str, Any],
    windows_exit_gate: dict[str, Any],
    skip_public_route_check: bool,
    base_url: str,
    timeout_seconds: int,
) -> dict[str, Any] | None:
    if not isinstance(windows_exit_gate, dict):
        return None

    checks = windows_exit_gate.get("checks")
    if not isinstance(checks, dict):
        checks = {}

    gate_channel = norm(checks.get("release_channel_id") or windows_exit_gate.get("channelId") or windows_exit_gate.get("channel"))
    gate_version = str(
        checks.get("release_channel_version")
        or windows_exit_gate.get("releaseVersion")
        or windows_exit_gate.get("version")
        or ""
    ).strip()
    manifest_channel = norm(manifest.get("channelId") or manifest.get("channel"))
    manifest_version = str(manifest.get("version") or "").strip()
    if manifest_channel and gate_channel and manifest_channel != gate_channel:
        return None
    if manifest_version and gate_version and manifest_version != gate_version:
        return None

    blocking_mode = norm(windows_exit_gate.get("blockingMode") or windows_exit_gate.get("blocking_mode"))
    reasons = [str(item).strip() for item in (windows_exit_gate.get("reasons") or []) if str(item).strip()]
    if blocking_mode != "external_only":
        return None
    if not reasons or not all("windows installer visual proof" in item.lower() for item in reasons):
        return None

    current_capture_pending = boolish(checks.get("windows_installer_visual_proof_current_capture_pending"))
    only_visual_blocker = boolish(checks.get("windows_installer_visual_proof_handoff_only_blocker_is_visual_proof"))
    external_artifact_required = boolish(checks.get("windows_installer_visual_proof_handoff_external_artifact_required"))
    if not (current_capture_pending or only_visual_blocker or external_artifact_required):
        return None

    expected_head = str(checks.get("expected_windows_head") or "avalonia").strip()
    expected_rid = str(checks.get("expected_windows_rid") or "win-x64").strip()
    tuple_id = f"{expected_head}:{expected_rid}:windows"
    expected_artifact_id = str(
        ((checks.get("release_channel_windows_artifact") or {}).get("artifactId"))
        or "avalonia-win-x64-installer"
    ).strip()
    artifact = find_installer_artifact(manifest=manifest, tuple_id=tuple_id, artifact_id=expected_artifact_id)
    route = find_public_install_route(manifest=manifest, tuple_id=tuple_id, artifact=artifact)
    access_class = installer_access_class(
        manifest=manifest,
        tuple_id=tuple_id,
        artifact_id=expected_artifact_id,
        installer_name=str(checks.get("expected_windows_file_name") or "").strip(),
    )
    if not access_class:
        access_class = fallback_install_access_class(tuple_id=tuple_id, route=route, required_host="windows")

    if skip_public_route_check or not route:
        route_probe = {
            "checked": False,
            "url": "",
            "http_status": None,
            "ok": False,
            "error": "skipped" if skip_public_route_check else "missing_route",
        }
    else:
        route_probe = check_public_route(
            base_url=base_url,
            route=route,
            timeout_seconds=timeout_seconds,
        )
        route_probe["installAccessClass"] = access_class
        route_probe["authExpected"] = False
        route_probe["authChallengeAccepted"] = False

    installer_path = Path(str(checks.get("windows_installer_path") or "").strip())
    installer_present = installer_path.is_file() if str(installer_path) else boolish(checks.get("installer_exists"))
    installer_sha = ""
    if installer_present and str(installer_path) and installer_path.is_file():
        installer_sha = sha256_file(installer_path)
    else:
        installer_sha = normalize_sha256(checks.get("installer_sha256"))

    blocker_codes = ["windows_visual_proof_capture_pending"]
    blocker_messages = reasons[:]
    if boolish(checks.get("windows_installer_visual_proof_handoff_current_visual_proof_stale")):
        blocker_codes.append("windows_visual_proof_stale")
        blocker_messages.append(
            "Current Windows installer visual proof is stale for the promoted release and must be recaptured on a Windows host."
        )

    return {
        "tupleId": tuple_id,
        "requiredHost": "windows",
        "expectedPublicInstallRoute": route,
        "requiredProofs": ["windows_installer_visual_proof"],
        "expectedArtifactId": expected_artifact_id,
        "installAccessClass": access_class,
        "expectedInstallerRelativePath": f"files/{str(checks.get('expected_windows_file_name') or '').strip()}",
        "expectedStartupSmokeReceiptPath": "",
        "installerPresent": installer_present,
        "installerSha256": installer_sha,
        "expectedInstallerSha256": normalize_sha256(checks.get("installer_sha256")),
        "startupSmokeReceiptPresent": False,
        "startupSmokeReceiptRecordedAtUtc": "",
        "startupSmokeReceiptAgeSeconds": None,
        "startupSmokeReceiptStatus": "",
        "startupSmokeReceiptChannelId": "",
        "startupSmokeReceiptVersion": "",
        "publicRouteProbe": route_probe,
        "blockerCodes": blocker_codes,
        "blockerMessages": blocker_messages,
        "ready": False,
    }


def main() -> int:
    args = parse_args()
    now_utc = dt.datetime.now(UTC)
    manifest = load_json(args.manifest)
    coverage = manifest.get("desktopTupleCoverage")
    if not isinstance(coverage, dict):
        raise SystemExit(f"desktopTupleCoverage missing from {args.manifest}")

    external_requests = coverage.get("externalProofRequests")
    if not isinstance(external_requests, list):
        external_requests = []
    missing_tuples = coverage.get("missingRequiredPlatformHeadRidTuples")
    if not isinstance(missing_tuples, list):
        missing_tuples = []
    windows_exit_gate = load_optional_json(args.windows_exit_gate)

    release_channel = norm(manifest.get("channelId") or manifest.get("channel"))
    release_version = str(manifest.get("version") or "").strip()
    release_published_at = parse_utc(
        manifest.get("publishedAt")
        or manifest.get("published_at")
        or manifest.get("generatedAt")
        or manifest.get("generated_at")
    )

    blockers: list[dict[str, Any]] = []
    unresolved_hosts: list[str] = []
    unresolved_tuples: list[str] = []
    for row in external_requests:
        if not isinstance(row, dict):
            continue
        tuple_id = str(row.get("tupleId") or "").strip()
        required_host = norm(row.get("requiredHost"))
        expected_artifact_id = str(row.get("expectedArtifactId") or "").strip()
        installer_name = str(row.get("expectedInstallerFileName") or "").strip()
        expected_installer_sha = norm(row.get("expectedInstallerSha256"))
        receipt_rel = str(row.get("expectedStartupSmokeReceiptPath") or "").strip().replace("\\", "/")
        route = str(row.get("expectedPublicInstallRoute") or "").strip()
        access_class = installer_access_class(
            manifest=manifest,
            tuple_id=tuple_id,
            artifact_id=expected_artifact_id,
            installer_name=installer_name,
        )
        if not access_class:
            access_class = fallback_install_access_class(
                tuple_id=tuple_id,
                route=route,
                required_host=required_host,
            )
        installer_path = args.downloads_dir / installer_name if installer_name else args.downloads_dir / ""
        receipt_path = args.startup_smoke_dir / Path(receipt_rel).name if receipt_rel else args.startup_smoke_dir / ""

        blocker_codes: list[str] = []
        blocker_messages: list[str] = []

        installer_present = installer_path.is_file()
        installer_sha = sha256_file(installer_path) if installer_present else ""
        if not installer_present:
            blocker_codes.append("installer_missing")
            blocker_messages.append(f"installer missing at {installer_path}")
        elif expected_installer_sha and installer_sha != expected_installer_sha:
            blocker_codes.append("installer_hash_mismatch")
            blocker_messages.append(
                f"installer hash mismatch for {installer_path.name}: actual={installer_sha} expected={expected_installer_sha}"
            )

        receipt_present = receipt_path.is_file()
        receipt_payload: dict[str, Any] | None = None
        if not receipt_present:
            blocker_codes.append("receipt_missing")
            blocker_messages.append(f"startup smoke receipt missing at {receipt_path}")
            receipt_age_seconds = None
        else:
            try:
                loaded = load_json(receipt_path)
                receipt_payload = loaded
            except Exception as exc:  # pragma: no cover - defensive
                blocker_codes.append("receipt_invalid")
                blocker_messages.append(f"startup smoke receipt invalid JSON at {receipt_path}: {exc}")
                receipt_age_seconds = None
            else:
                recorded_at = parse_utc(
                    loaded.get("recordedAtUtc")
                    or loaded.get("recorded_at")
                    or loaded.get("completedAtUtc")
                    or loaded.get("completed_at")
                    or loaded.get("generatedAt")
                    or loaded.get("generated_at")
                )
                if recorded_at is None:
                    receipt_age_seconds = None
                    blocker_codes.append("receipt_missing_timestamp")
                    blocker_messages.append("startup smoke receipt is missing a valid recorded timestamp")
                else:
                    receipt_age_seconds = max(0, int((now_utc - recorded_at).total_seconds()))
                    if args.max_receipt_age_seconds > 0 and receipt_age_seconds > args.max_receipt_age_seconds:
                        blocker_codes.append("receipt_stale")
                        blocker_messages.append(
                            "startup smoke receipt is stale "
                            f"(age_seconds={receipt_age_seconds}, max_age_seconds={args.max_receipt_age_seconds})"
                        )
                    if release_published_at is not None and recorded_at < release_published_at:
                        blocker_codes.append("receipt_precedes_release_publication")
                        blocker_messages.append(
                            "startup smoke receipt was captured before the current release channel was published "
                            f"(receipt_recorded_at={format_utc_iso(recorded_at)}, "
                            f"release_published_at={format_utc_iso(release_published_at)})"
                        )
                receipt_channel = norm(loaded.get("channelId") or loaded.get("channel"))
                if release_channel and receipt_channel and receipt_channel != release_channel:
                    blocker_codes.append("receipt_channel_mismatch")
                    blocker_messages.append(
                        f"startup smoke receipt channel mismatch (actual={receipt_channel}, expected={release_channel})"
                    )
                receipt_version = str(loaded.get("version") or loaded.get("releaseVersion") or "").strip()
                if release_version and receipt_version and receipt_version != release_version:
                    blocker_codes.append("receipt_version_mismatch")
                    blocker_messages.append(
                        f"startup smoke receipt version mismatch (actual={receipt_version}, expected={release_version})"
                    )
                if expected_installer_sha:
                    digest = norm(loaded.get("artifactSha256") or loaded.get("artifactDigest"))
                    digest = digest.replace("sha256:", "")
                    if digest and digest != expected_installer_sha:
                        blocker_codes.append("receipt_digest_mismatch")
                        blocker_messages.append(
                            "startup smoke receipt digest mismatch "
                            f"(actual={digest}, expected={expected_installer_sha})"
                        )

        if args.skip_public_route_check:
            route_probe = {
                "checked": False,
                "url": "",
                "http_status": None,
                "ok": False,
                "error": "skipped",
            }
        else:
            route_probe = check_public_route(
                base_url=args.base_url,
                route=route,
                timeout_seconds=args.timeout_seconds,
            )
            auth_gated_route = access_class in {"account_required", "account_recommended"}
            auth_challenge = route_probe.get("http_status") in {401, 403}
            route_probe["installAccessClass"] = access_class
            route_probe["authExpected"] = auth_gated_route
            route_probe["authChallengeAccepted"] = bool(auth_gated_route and auth_challenge)
            if route_probe.get("checked") and auth_gated_route and auth_challenge:
                route_probe["ok"] = True
                route_probe["error"] = ""
            if route_probe.get("checked") and not bool(route_probe.get("ok")):
                blocker_codes.append("public_route_unhealthy")
                blocker_messages.append(
                    "public install route unhealthy "
                    f"(status={route_probe.get('http_status')}, error={route_probe.get('error')})"
                )

        if blocker_codes:
            unresolved_tuples.append(tuple_id)
            if required_host and required_host not in unresolved_hosts:
                unresolved_hosts.append(required_host)

        blockers.append(
            {
                "tupleId": tuple_id,
                "requiredHost": required_host,
                "expectedPublicInstallRoute": route,
                "requiredProofs": row.get("requiredProofs") if isinstance(row.get("requiredProofs"), list) else [],
                "expectedArtifactId": expected_artifact_id,
                "installAccessClass": access_class,
                "expectedInstallerRelativePath": str(row.get("expectedInstallerRelativePath") or "").strip(),
                "expectedStartupSmokeReceiptPath": receipt_rel,
                "installerPresent": installer_present,
                "installerSha256": installer_sha,
                "expectedInstallerSha256": expected_installer_sha,
                "startupSmokeReceiptPresent": receipt_present,
                "startupSmokeReceiptRecordedAtUtc": str(
                    (receipt_payload or {}).get("recordedAtUtc")
                    or (receipt_payload or {}).get("recorded_at")
                    or (receipt_payload or {}).get("completedAtUtc")
                    or (receipt_payload or {}).get("completed_at")
                    or (receipt_payload or {}).get("generatedAt")
                    or (receipt_payload or {}).get("generated_at")
                    or ""
                ).strip(),
                "startupSmokeReceiptAgeSeconds": receipt_age_seconds,
                "startupSmokeReceiptStatus": norm((receipt_payload or {}).get("status")),
                "startupSmokeReceiptChannelId": norm(
                    (receipt_payload or {}).get("channelId") or (receipt_payload or {}).get("channel")
                ),
                "startupSmokeReceiptVersion": str(
                    (receipt_payload or {}).get("version") or (receipt_payload or {}).get("releaseVersion") or ""
                ).strip(),
                "publicRouteProbe": route_probe,
                "blockerCodes": blocker_codes,
                "blockerMessages": blocker_messages,
                "ready": len(blocker_codes) == 0,
            }
        )

    existing_tuple_ids = {
        str(row.get("tupleId") or "").strip()
        for row in blockers
        if isinstance(row, dict) and str(row.get("tupleId") or "").strip()
    }
    windows_visual_proof_row = materialize_windows_visual_proof_backlog_row(
        manifest=manifest,
        windows_exit_gate=windows_exit_gate,
        skip_public_route_check=args.skip_public_route_check,
        base_url=args.base_url,
        timeout_seconds=args.timeout_seconds,
    )
    if windows_visual_proof_row is not None:
        tuple_id = str(windows_visual_proof_row.get("tupleId") or "").strip()
        required_host = norm(windows_visual_proof_row.get("requiredHost"))
        if tuple_id and tuple_id not in existing_tuple_ids:
            blockers.append(windows_visual_proof_row)
            if required_host and required_host not in unresolved_hosts:
                unresolved_hosts.append(required_host)
            if tuple_id not in unresolved_tuples:
                unresolved_tuples.append(tuple_id)

    should_materialize_browser_proof = args.browser_proof_output is not None
    browser_routes: list[str] = []
    if should_materialize_browser_proof:
        for route in [*DEFAULT_BROWSER_ROUTES, *args.browser_routes]:
            route_text = str(route or "").strip()
            if route_text and route_text not in browser_routes:
                browser_routes.append(route_text)

    browser_route_probes: list[dict[str, Any]] = []
    browser_route_blockers: list[dict[str, Any]] = []
    for route in browser_routes:
        if args.skip_public_route_check:
            probe = {
                "checked": False,
                "url": "",
                "http_status": None,
                "ok": False,
                "error": "skipped",
            }
        else:
            probe = check_public_route(
                base_url=args.base_url,
                route=route,
                timeout_seconds=args.timeout_seconds,
            )
        probe["route"] = route
        browser_route_probes.append(probe)
        if probe.get("checked") and not bool(probe.get("ok")):
            browser_route_blockers.append(
                {
                    "route": route,
                    "url": probe.get("url") or "",
                    "http_status": probe.get("http_status"),
                    "error": probe.get("error") or "",
                }
            )

    payload = {
        "contract_name": CONTRACT_NAME,
        "generated_at": utc_now_iso(),
        "manifest_path": str((args.display_manifest or args.manifest).resolve()),
        "downloads_dir": str((args.display_downloads_dir or args.downloads_dir).resolve()),
        "startup_smoke_dir": str((args.display_startup_smoke_dir or args.startup_smoke_dir).resolve()),
        "base_url": args.base_url,
        "timeout_seconds": args.timeout_seconds,
        "max_receipt_age_seconds": args.max_receipt_age_seconds,
        "release_published_at": format_utc_iso(release_published_at),
        "status": "blocked" if blockers and unresolved_tuples else "ready",
        "missing_required_platform_head_rid_tuples": missing_tuples,
        "unresolved_hosts": unresolved_hosts,
        "unresolved_tuples": unresolved_tuples,
        "browser_workbench_routes": browser_routes,
        "browser_route_probe_count": len(browser_route_probes),
        "browser_route_blocker_count": len(browser_route_blockers),
        "browser_route_probes": browser_route_probes,
        "browser_route_blockers": browser_route_blockers,
        "external_proof_request_count": len(blockers),
        "external_proof_requests": blockers,
    }
    if should_materialize_browser_proof and browser_route_blockers:
        payload["status"] = "blocked"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if args.browser_proof_output is not None:
        browser_payload = {
            "contract_name": PUBLIC_EDGE_BROWSER_CONTRACT_NAME,
            "generated_at": payload["generated_at"],
            "status": "passed" if not browser_route_blockers else "blocked",
            "base_url": args.base_url,
            "proof_shape": "expanded",
            "runtime_required": True,
            "route_probe_executed": not args.skip_public_route_check,
            "portal_route_probe_script": "scripts/e2e-public-edge.cjs",
            "route_proof_markers": [
                "public_chummer_app_route",
                "public_chummer_app_roster_route",
                "public_blazor_root_redirect",
                "public_blazor_home_roster_entry",
                "public_blazor_health",
                "public_workbench_route",
                "public_workspace_restore_route",
                "public_startup_deep_link_route",
                "public_startup_workbench_command_routes",
                "public_result_continuation_routes",
                "public_action_continuation_routes",
                "public_committed_action_route",
                "public_advanced_action_routes",
                "public_advanced_committed_action_routes",
            ],
            "proof_routes": browser_routes,
            "workflow_proofs": [
                "blazor_root_redirect",
                "workbench_route",
                "workspace_resume_route_shape",
                "new_character_deep_link_route_shape",
                "startup_command_route_shapes",
                "result_continuation_route_shapes",
                "action_continuation_route_shapes",
                "committed_action_route_shape",
                "advanced_action_route_shapes",
                "advanced_committed_action_route_shapes",
            ],
            "route_probe_count": len(browser_route_probes),
            "route_probe_failures": browser_route_blockers,
            "route_probes": browser_route_probes,
            "source_receipt": str(args.output.resolve()),
            "notes": [
                "Hosted public-edge browser proof is distinct from the Docker self-host workbench receipt.",
                "Public product navigation remains /app, /blazor/ redirects into the roster-first app?command=character_roster browser workflow, /blazor/app is the hosted app path, /blazor/home carries the roster-first orientation entry, and /blazor/workbench is the canonical proof-compatible route base.",
                "This receipt currently proves hosted /blazor route-entry posture and route health, not full browser workflow execution.",
            ],
        }
        args.browser_proof_output.parent.mkdir(parents=True, exist_ok=True)
        args.browser_proof_output.write_text(json.dumps(browser_payload, indent=2) + "\n", encoding="utf-8")

    execution_receipt_path = args.output.parent / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.generated.json"
    execution_runner_path = REPO_ROOT / "scripts" / "e2e-public-edge-execution.sh"
    execution_status_summary_path = REPO_ROOT / "scripts" / "print_blazor_public_edge_proof_status.py"
    execution_verifier_path = REPO_ROOT / "scripts" / "verify_blazor_public_edge_execution_proof.py"
    execution_contract_doc_path = REPO_ROOT / "docs" / "BLAZOR_PUBLIC_EDGE_EXECUTION_PROOF.md"
    execution_receipt_status = ""
    execution_receipt_contract = ""
    execution_receipt_error = ""
    if execution_receipt_path.is_file():
        try:
            execution_payload = load_json(execution_receipt_path)
        except Exception:
            execution_payload = {}
        execution_receipt_status = str(execution_payload.get("status") or "").strip().lower()
        execution_receipt_contract = str(execution_payload.get("contract_name") or "").strip()
        execution_receipt_error = str(execution_payload.get("error") or "").strip()
    payload["browser_execution_proof_target"] = str(execution_receipt_path.resolve())
    payload["browser_execution_proof_status"] = execution_receipt_status
    payload["browser_execution_proof_contract"] = execution_receipt_contract
    payload["browser_execution_proof_error"] = execution_receipt_error
    payload["browser_execution_proof_runner"] = str(execution_runner_path)
    payload["browser_execution_proof_status_summary"] = str(execution_status_summary_path)
    payload["browser_execution_proof_verifier"] = str(execution_verifier_path)
    payload["browser_execution_proof_contract_doc"] = str(execution_contract_doc_path)
    if args.browser_proof_output is not None:
        route_receipt_status = ""
        route_receipt_contract = ""
        if args.browser_proof_output.is_file():
            try:
                route_payload = load_json(args.browser_proof_output)
            except Exception:
                route_payload = {}
            route_receipt_status = str(route_payload.get("status") or "").strip().lower()
            route_receipt_contract = str(route_payload.get("contract_name") or "").strip()
        payload["browser_route_entry_proof_target"] = str(args.browser_proof_output.resolve())
        payload["browser_route_entry_proof_status"] = route_receipt_status
        payload["browser_route_entry_proof_contract"] = route_receipt_contract
        payload["browser_route_entry_proof_shape"] = classify_route_entry_proof_shape(route_payload)
        payload["browser_route_entry_proof_status_summary"] = str(PUBLIC_EDGE_BROWSER_STATUS_SUMMARY_PATH)
        payload["browser_route_entry_proof_verifier"] = str(PUBLIC_EDGE_BROWSER_VERIFIER_PATH)
        payload["browser_route_entry_proof_verifier_wrapper"] = str(
            PUBLIC_EDGE_BROWSER_VERIFIER_WRAPPER_PATH
        )
        payload["browser_route_entry_proof_contract_doc"] = str(PUBLIC_EDGE_BROWSER_CONTRACT_DOC_PATH)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
