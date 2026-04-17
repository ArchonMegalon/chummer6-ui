#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


INSTALLER_KINDS = {"installer", "dmg", "pkg", "msix"}
PASSING_STARTUP_SMOKE_STATUSES = {"pass", "passed", "ready"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_PUBLIC_PROMOTION_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "604800"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_PUBLIC_PROMOTION_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate release-evidence/public-promotion.json for desktop bundles.")
    parser.add_argument("--manifest", required=True, help="Path to RELEASE_CHANNEL.generated.json")
    parser.add_argument("--startup-smoke-dir", required=True, help="Path to the startup-smoke receipt directory")
    parser.add_argument("--output", required=True, help="Path to write public-promotion.json")
    parser.add_argument("--channel", default="", help="Release channel override; defaults to the manifest channel when omitted")
    parser.add_argument("--generated-at", default="", help="RFC3339 timestamp override; defaults to now")
    return parser.parse_args()


def now_rfc3339() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def normalize_token(value: Any) -> str:
    return str(value or "").strip().lower()


def normalize_platform(raw: str | None) -> str:
    return (raw or "").strip().lower()


def expected_host_class_platform_token(platform: str) -> str:
    normalized = normalize_platform(platform)
    if normalized == "windows":
        return "win"
    if normalized == "macos":
        return "osx"
    if normalized == "linux":
        return "linux"
    return normalized


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize_token(host_class)
    expected_token = expected_host_class_platform_token(platform)
    if not normalized_host or not expected_token:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return expected_token in host_tokens


def resolve_file_name(artifact: dict) -> str:
    file_name = (artifact.get("fileName") or "").strip()
    if file_name:
        return Path(file_name).name

    download_url = (artifact.get("downloadUrl") or "").strip()
    if not download_url:
        raise ValueError("artifact is missing fileName/downloadUrl")

    return Path(download_url).name


def is_installer_artifact(artifact: dict) -> bool:
    kind = (artifact.get("kind") or "").strip().lower()
    if kind:
        return kind in INSTALLER_KINDS

    file_name = resolve_file_name(artifact).lower()
    return file_name.endswith((".exe", ".deb", ".dmg", ".pkg", ".msix"))


def load_receipts(startup_smoke_dir: Path) -> list[dict]:
    receipts: list[dict] = []
    if not startup_smoke_dir.is_dir():
        return receipts

    for path in startup_smoke_dir.rglob("startup-smoke-*.receipt.json"):
        try:
            payload = load_json(path)
        except json.JSONDecodeError:
            continue

        if not payload.get("headId") or not payload.get("platform") or not payload.get("arch"):
            continue
        payload["__sourcePath"] = str(path)
        receipts.append(payload)
    return receipts


def parse_iso_utc(raw: Any) -> datetime | None:
    value = str(raw or "").strip()
    if not value:
        return None
    if value.endswith("Z"):
        value = value[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def receipt_recorded_at(receipt: dict) -> datetime | None:
    return parse_iso_utc(receipt.get("completedAtUtc") or receipt.get("recordedAtUtc") or receipt.get("startedAtUtc"))


def validate_receipt_for_artifact(
    receipt: dict,
    expected_platform: str,
    expected_rid: str,
    expected_digest: str,
    now_utc: datetime,
) -> tuple[bool, str]:
    status = normalize_token(receipt.get("status"))
    if status not in PASSING_STARTUP_SMOKE_STATUSES:
        return False, "startup-smoke receipt status is not passing"

    checkpoint = normalize_token(receipt.get("readyCheckpoint"))
    if checkpoint != "pre_ui_event_loop":
        return False, "startup-smoke receipt missing pre_ui_event_loop checkpoint"

    digest = normalize_token(receipt.get("artifactDigest"))
    if expected_digest and digest and digest != f"sha256:{expected_digest}":
        return False, "startup-smoke receipt artifactDigest does not match manifest sha256"

    host_class = normalize_token(receipt.get("hostClass"))
    if not host_class:
        return False, "startup-smoke receipt hostClass is missing"
    if not host_class_matches_platform(host_class, expected_platform):
        return False, f"startup-smoke receipt hostClass does not identify the {expected_platform} host"

    operating_system = str(receipt.get("operatingSystem") or "").strip()
    if not operating_system:
        return False, "startup-smoke receipt operatingSystem is missing"

    receipt_rid = normalize_token(receipt.get("rid"))
    if not receipt_rid:
        return False, "startup-smoke receipt rid is missing"
    if expected_rid and receipt_rid != expected_rid:
        return False, "startup-smoke receipt rid does not match manifest rid"

    timestamp = receipt_recorded_at(receipt)
    if timestamp is None:
        return False, "startup-smoke receipt is missing a valid completed/recorded timestamp"

    age_delta_seconds = int((now_utc - timestamp).total_seconds())
    if age_delta_seconds < 0:
        future_skew_seconds = abs(age_delta_seconds)
        if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
            return (
                False,
                f"startup-smoke receipt timestamp is in the future ({future_skew_seconds}s ahead)",
            )
    elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
        return False, f"startup-smoke receipt is stale ({age_delta_seconds}s old)"

    return True, ""


def find_matching_receipt(artifact: dict, receipts: list[dict], now_utc: datetime) -> tuple[dict | None, str]:
    expected_head = (artifact.get("head") or "").strip()
    expected_platform = normalize_platform(artifact.get("platform"))
    expected_rid = normalize_token(artifact.get("rid"))
    expected_arch = (artifact.get("arch") or "").strip().lower()
    expected_digest = normalize_token(artifact.get("sha256"))
    matching_receipts: list[dict] = []

    for receipt in receipts:
        if (receipt.get("headId") or "").strip().lower() != expected_head.lower():
            continue
        if normalize_platform(receipt.get("platform")) != expected_platform:
            continue
        if normalize_token(receipt.get("rid")) != expected_rid:
            continue
        if (receipt.get("arch") or "").strip().lower() != expected_arch:
            continue
        matching_receipts.append(receipt)

    if not matching_receipts:
        return None, "startup-smoke receipt missing"

    def receipt_sort_key(receipt: dict) -> tuple[int, str]:
        recorded = receipt_recorded_at(receipt)
        source = str(receipt.get("__sourcePath") or "")
        if recorded is None:
            return (0, source)
        return (int(recorded.timestamp()), source)

    matching_receipts.sort(key=receipt_sort_key, reverse=True)
    candidate = matching_receipts[0]
    is_valid, reason = validate_receipt_for_artifact(candidate, expected_platform, expected_rid, expected_digest, now_utc)
    if not is_valid:
        return None, reason

    return candidate, ""


def env_override(*names: str) -> str | None:
    for name in names:
        value = os.environ.get(name, "").strip()
        if value:
            return value
    return None


def allowed_windows_status(channel: str) -> str:
    override = env_override("CHUMMER_WINDOWS_SIGNING_STATUS_OVERRIDE", "CHUMMER_WINDOWS_SIGNING_STATUS")
    if override:
        return override
    return "skipped_preview" if channel == "preview" else "fail"


def allowed_mac_statuses(channel: str) -> tuple[str, str]:
    signing = env_override("CHUMMER_MAC_SIGNING_STATUS_OVERRIDE", "CHUMMER_MAC_SIGNING_STATUS")
    notarization = env_override("CHUMMER_MAC_NOTARIZATION_STATUS_OVERRIDE", "CHUMMER_MAC_NOTARIZATION_STATUS")
    if signing and notarization:
        return signing, notarization
    if channel == "preview":
        return signing or "skipped_preview", notarization or "skipped_preview"
    return signing or "fail", notarization or "fail"


def compute_promotion_status(platform: str, channel: str, startup_smoke_status: str, signing_status: str | None, notarization_status: str | None) -> str:
    if startup_smoke_status != "pass":
        return "fail"

    if platform == "windows":
        allowed = {"pass"}
        if channel == "preview":
            allowed.add("skipped_preview")
        return "pass" if signing_status in allowed else "fail"

    if platform == "macos":
        allowed = {"pass"}
        if channel == "preview":
            allowed.add("skipped_preview")
        return "pass" if signing_status in allowed and notarization_status in allowed else "fail"

    return "pass"


def main() -> int:
    args = parse_args()
    manifest_path = Path(args.manifest)
    startup_smoke_dir = Path(args.startup_smoke_dir)
    output_path = Path(args.output)

    manifest = load_json(manifest_path)
    artifacts = manifest.get("artifacts") or []
    if not isinstance(artifacts, list):
        raise SystemExit("manifest artifacts must be a list")

    channel = (args.channel or manifest.get("channelId") or "").strip().lower()
    generated_at = args.generated_at.strip() or now_rfc3339()
    receipts = load_receipts(startup_smoke_dir)
    now_utc = datetime.now(timezone.utc)

    evidence_artifacts: list[dict] = []
    for artifact in artifacts:
        if not isinstance(artifact, dict):
            continue

        platform = normalize_platform(artifact.get("platform"))
        installer = is_installer_artifact(artifact)
        startup_smoke_reason = ""
        receipt = None
        if installer:
            receipt, startup_smoke_reason = find_matching_receipt(artifact, receipts, now_utc)
        startup_smoke_status = "pass" if (not installer or receipt is not None) else "fail"

        signing_status: str | None = None
        notarization_status: str | None = None
        if platform == "windows":
            signing_status = allowed_windows_status(channel)
        elif platform == "macos":
            signing_status, notarization_status = allowed_mac_statuses(channel)

        evidence_artifacts.append(
            {
                "artifactId": artifact.get("artifactId"),
                "fileName": resolve_file_name(artifact),
                "platform": platform,
                "promotionStatus": compute_promotion_status(platform, channel, startup_smoke_status, signing_status, notarization_status),
                "startupSmokeStatus": startup_smoke_status,
                "startupSmokeReason": startup_smoke_reason,
                "startupSmokeReceiptPath": str((receipt or {}).get("__sourcePath") or ""),
                "signingStatus": signing_status,
                "notarizationStatus": notarization_status,
                "artifactSha256": artifact.get("sha256"),
                "artifactSizeBytes": artifact.get("sizeBytes"),
                "kind": artifact.get("kind"),
            }
        )

    payload = {
        "contractName": "chummer.run.desktop_release_publication",
        "generatedAt": generated_at,
        "artifacts": evidence_artifacts,
    }

    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
