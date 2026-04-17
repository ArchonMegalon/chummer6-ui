#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


UTC = dt.timezone.utc
CONTRACT_NAME = "chummer6-ui.external_host_proof_blockers"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize repo-local external host proof blocker summary from the canonical release channel."
    )
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--downloads-dir", required=True, type=Path)
    parser.add_argument("--startup-smoke-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--base-url", default="https://chummer.run")
    parser.add_argument("--timeout-seconds", type=int, default=10)
    parser.add_argument("--max-receipt-age-seconds", type=int, default=604800)
    parser.add_argument("--skip-public-route-check", action="store_true")
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
    request = urllib.request.Request(url=url, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=max(timeout_seconds, 1)) as response:
            status = int(response.status)
            return {
                "checked": True,
                "url": url,
                "http_status": status,
                "ok": 200 <= status < 400,
                "error": "",
            }
    except urllib.error.HTTPError as exc:
        return {
            "checked": True,
            "url": url,
            "http_status": int(exc.code),
            "ok": False,
            "error": f"http_{int(exc.code)}",
        }
    except Exception as exc:  # pragma: no cover - best-effort network probe
        return {
            "checked": True,
            "url": url,
            "http_status": None,
            "ok": False,
            "error": str(exc),
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

    payload = {
        "contract_name": CONTRACT_NAME,
        "generated_at": utc_now_iso(),
        "manifest_path": str(args.manifest),
        "downloads_dir": str(args.downloads_dir),
        "startup_smoke_dir": str(args.startup_smoke_dir),
        "base_url": args.base_url,
        "timeout_seconds": args.timeout_seconds,
        "max_receipt_age_seconds": args.max_receipt_age_seconds,
        "release_published_at": format_utc_iso(release_published_at),
        "status": "blocked" if blockers and unresolved_tuples else "ready",
        "missing_required_platform_head_rid_tuples": missing_tuples,
        "unresolved_hosts": unresolved_hosts,
        "unresolved_tuples": unresolved_tuples,
        "external_proof_request_count": len(blockers),
        "external_proof_requests": blockers,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
