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
PUBLIC_SKIP_STARTUP_SMOKE_FILTER = str(
    os.environ.get("CHUMMER_PUBLIC_SKIP_STARTUP_SMOKE_FILTER")
    or os.environ.get("CHUMMER_VERIFY_SKIP_STARTUP_SMOKE_FILTER")
    or ""
).strip().lower() in {"1", "true", "yes", "on"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate release-evidence/public-promotion.json for desktop bundles.")
    parser.add_argument("--manifest", required=True, help="Path to RELEASE_CHANNEL.generated.json")
    parser.add_argument("--startup-smoke-dir", required=True, help="Path to the startup-smoke receipt directory")
    parser.add_argument("--signing-receipts-dir", default="", help="Optional path to desktop signing receipt directory")
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


def expected_host_class_platform_tokens(platform: str) -> tuple[str, ...]:
    normalized = normalize_platform(platform)
    if normalized == "windows":
        return ("win", "windows")
    if normalized == "macos":
        return ("osx", "macos")
    if normalized == "linux":
        return ("linux",)
    return (normalized,) if normalized else ()


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize_token(host_class)
    expected_tokens = expected_host_class_platform_tokens(platform)
    if not normalized_host or not expected_tokens:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    return any(token in host_tokens for token in expected_tokens)


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


def load_signing_receipts(signing_receipts_dir: Path) -> list[dict]:
    receipts: list[dict] = []
    if not signing_receipts_dir.is_dir():
        return receipts

    for path in signing_receipts_dir.rglob("*.receipt.json"):
        try:
            payload = load_json(path)
        except json.JSONDecodeError:
            continue

        contract_name = str(payload.get("contractName") or payload.get("contract_name") or "").strip()
        if contract_name != "chummer6-ui.desktop_artifact_signing":
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


def signing_receipt_generated_at(receipt: dict) -> datetime | None:
    return parse_iso_utc(receipt.get("generatedAt") or receipt.get("generated_at"))


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
    elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS and not PUBLIC_SKIP_STARTUP_SMOKE_FILTER:
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


def find_matching_signing_receipt(artifact: dict, receipts: list[dict]) -> tuple[dict | None, dict | None]:
    expected_file_name = resolve_file_name(artifact).lower()
    expected_platform = normalize_platform(artifact.get("platform"))
    expected_rid = normalize_token(artifact.get("rid"))
    expected_digest = normalize_token(artifact.get("sha256"))
    candidates: list[tuple[int, float, dict, dict | None]] = []

    for receipt in receipts:
        receipt_platform = normalize_platform(receipt.get("platform"))
        if receipt_platform and receipt_platform != expected_platform:
            continue

        receipt_rid = normalize_token(receipt.get("rid"))
        if receipt_rid and expected_rid and receipt_rid != expected_rid:
            continue

        best_score = 0
        matched_artifact: dict | None = None
        for row in receipt.get("artifacts") or []:
            if not isinstance(row, dict):
                continue

            score = 0
            row_file_name = str(row.get("fileName") or "").strip().lower()
            if row_file_name and row_file_name == expected_file_name:
                score += 8

            row_digest = normalize_token(row.get("sha256"))
            if expected_digest and row_digest and row_digest == expected_digest:
                score += 5

            row_kind = normalize_token(row.get("kind"))
            artifact_kind = normalize_token(artifact.get("kind"))
            if row_kind and artifact_kind and row_kind == artifact_kind:
                score += 1

            if score > best_score:
                best_score = score
                matched_artifact = row

        if best_score == 0:
            top_level_signing = normalize_token(receipt.get("signingStatus"))
            top_level_notarization = normalize_token(receipt.get("notarizationStatus"))
            if top_level_signing or top_level_notarization:
                best_score = 1

        if best_score == 0:
            continue

        generated = signing_receipt_generated_at(receipt)
        generated_score = generated.timestamp() if generated is not None else 0.0
        candidates.append((best_score, generated_score, receipt, matched_artifact))

    if not candidates:
        return None, None

    candidates.sort(key=lambda item: (item[0], item[1], str(item[2].get("__sourcePath") or "")), reverse=True)
    _, _, receipt, matched_artifact = candidates[0]
    return receipt, matched_artifact


def env_override(*names: str) -> str | None:
    for name in names:
        value = os.environ.get(name, "").strip()
        if value:
            return value
    return None


def unsigned_public_release_allowed() -> bool:
    value = env_override("CHUMMER_ALLOW_UNSIGNED_PUBLIC_RELEASE")
    if value is None:
        return False
    return normalize_token(value) in {"1", "true", "yes", "on"}


def allowed_windows_status(channel: str) -> str:
    override = env_override("CHUMMER_WINDOWS_SIGNING_STATUS_OVERRIDE", "CHUMMER_WINDOWS_SIGNING_STATUS")
    if override:
        return override
    if unsigned_public_release_allowed() and channel not in {"preview", "docker"}:
        return "unsigned_public_release"
    return "skipped_preview" if channel == "preview" else "fail"


def allowed_mac_statuses(channel: str) -> tuple[str, str]:
    signing = env_override("CHUMMER_MAC_SIGNING_STATUS_OVERRIDE", "CHUMMER_MAC_SIGNING_STATUS")
    notarization = env_override("CHUMMER_MAC_NOTARIZATION_STATUS_OVERRIDE", "CHUMMER_MAC_NOTARIZATION_STATUS")
    if signing and notarization:
        return signing, notarization
    if unsigned_public_release_allowed() and channel not in {"preview", "docker"}:
        return signing or "unsigned_public_release", notarization or "unsigned_public_release"
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
        if unsigned_public_release_allowed() and channel not in {"preview", "docker"}:
            allowed.add("unsigned_public_release")
        return "pass" if signing_status in allowed else "fail"

    if platform == "macos":
        allowed = {"pass"}
        if channel == "preview":
            allowed.add("skipped_preview")
        if unsigned_public_release_allowed() and channel not in {"preview", "docker"}:
            allowed.add("unsigned_public_release")
        return "pass" if signing_status in allowed and notarization_status in allowed else "fail"

    return "pass"


def main() -> int:
    args = parse_args()
    manifest_path = Path(args.manifest)
    startup_smoke_dir = Path(args.startup_smoke_dir)
    signing_receipts_dir = Path(args.signing_receipts_dir).resolve() if args.signing_receipts_dir else Path()
    output_path = Path(args.output)

    manifest = load_json(manifest_path)
    artifacts = manifest.get("artifacts") or []
    if not isinstance(artifacts, list):
        raise SystemExit("manifest artifacts must be a list")

    channel = (args.channel or manifest.get("channelId") or "").strip().lower()
    generated_at = args.generated_at.strip() or now_rfc3339()
    receipts = load_receipts(startup_smoke_dir)
    signing_receipts = load_signing_receipts(signing_receipts_dir) if args.signing_receipts_dir else []
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
        signing_receipt_path = ""
        signing_receipt, signing_artifact = find_matching_signing_receipt(artifact, signing_receipts)
        if signing_receipt is not None:
            signing_status = normalize_token((signing_artifact or {}).get("signingStatus") or signing_receipt.get("signingStatus")) or None
            notarization_status = normalize_token((signing_artifact or {}).get("notarizationStatus") or signing_receipt.get("notarizationStatus")) or None
            signing_receipt_path = str(signing_receipt.get("__sourcePath") or "")
        if platform == "windows":
            if not signing_status:
                signing_status = allowed_windows_status(channel)
        elif platform == "macos":
            if not signing_status or not notarization_status:
                fallback_signing, fallback_notarization = allowed_mac_statuses(channel)
                signing_status = signing_status or fallback_signing
                notarization_status = notarization_status or fallback_notarization

        evidence_artifacts.append(
            {
                "artifactId": artifact.get("artifactId"),
                "fileName": resolve_file_name(artifact),
                "platform": platform,
                "promotionStatus": compute_promotion_status(platform, channel, startup_smoke_status, signing_status, notarization_status),
                "startupSmokeStatus": startup_smoke_status,
                "startupSmokeReason": startup_smoke_reason,
                "startupSmokeReceiptPath": str((receipt or {}).get("__sourcePath") or ""),
                "signingReceiptPath": signing_receipt_path,
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
