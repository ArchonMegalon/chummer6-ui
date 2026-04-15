#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


PRIMARY_HEAD = "avalonia"
FALLBACK_HEADS = {"blazor-desktop"}
REQUIRED_PLATFORMS = ("linux", "macos", "windows")
PASSING_STARTUP_SMOKE_STATUSES = {"pass", "passed", "ready"}
STARTUP_SMOKE_MAX_AGE_SECONDS = int(
    os.environ.get("CHUMMER_AVALONIA_PRIMARY_ROUTE_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_AGE_SECONDS")
    or "86400"
)
STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS = int(
    os.environ.get("CHUMMER_AVALONIA_PRIMARY_ROUTE_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or os.environ.get("CHUMMER_DESKTOP_STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS")
    or "300"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Verify that Avalonia primary desktop route proof is complete for "
            "Windows, macOS, and Linux without accepting fallback-head receipts."
        )
    )
    parser.add_argument("--manifest", required=True, help="Path to RELEASE_CHANNEL.generated.json")
    parser.add_argument("--startup-smoke-dir", required=True, help="Directory containing startup-smoke receipts")
    parser.add_argument("--output", default="", help="Optional path for a generated proof packet")
    parser.add_argument("--generated-at", default="", help="RFC3339 timestamp override; defaults to now")
    return parser.parse_args()


def now_rfc3339() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def normalize(value: Any) -> str:
    return str(value or "").strip().lower()


def parse_iso_utc(value: Any) -> datetime | None:
    raw = str(value or "").strip()
    if not raw:
        return None
    if raw.endswith("Z"):
        raw = raw[:-1] + "+00:00"
    try:
        parsed = datetime.fromisoformat(raw)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def receipt_recorded_at(receipt: dict[str, Any]) -> datetime | None:
    return parse_iso_utc(
        receipt.get("completedAtUtc")
        or receipt.get("recordedAtUtc")
        or receipt.get("startedAtUtc")
    )


def expected_host_class_platform_token(platform: str) -> str:
    if platform == "windows":
        return "win"
    if platform == "macos":
        return "osx"
    return platform


def host_class_matches_platform(host_class: str, platform: str) -> bool:
    normalized_host = normalize(host_class)
    if not normalized_host:
        return False
    if platform == "windows":
        return "win" in normalized_host
    if platform == "macos":
        return any(token in normalized_host for token in ("osx", "darwin", "macos"))
    expected = expected_host_class_platform_token(platform)
    return expected in [token for token in normalized_host.split("-") if token]


def artifact_rid(artifact: dict[str, Any]) -> str:
    rid = normalize(artifact.get("rid"))
    if rid:
        return rid
    arch = normalize(artifact.get("arch"))
    platform = normalize(artifact.get("platform"))
    if platform == "windows" and arch in {"x64", "arm64"}:
        return f"win-{arch}"
    if platform == "macos" and arch in {"x64", "arm64"}:
        return f"osx-{arch}"
    if platform == "linux" and arch in {"x64", "arm64"}:
        return f"linux-{arch}"
    return ""


def is_desktop_install_artifact(artifact: dict[str, Any]) -> bool:
    return normalize(artifact.get("kind")) in {"installer", "dmg", "pkg", "msix"}


def load_receipts(startup_smoke_dir: Path) -> list[dict[str, Any]]:
    if not startup_smoke_dir.is_dir():
        return []

    receipts: list[dict[str, Any]] = []
    for path in startup_smoke_dir.rglob("startup-smoke-*.receipt.json"):
        try:
            payload = load_json(path)
        except (json.JSONDecodeError, OSError):
            continue
        if not isinstance(payload, dict):
            continue
        payload["__sourcePath"] = str(path)
        receipts.append(payload)
    return receipts


def receipt_matches_artifact(receipt: dict[str, Any], artifact: dict[str, Any]) -> bool:
    return (
        normalize(receipt.get("headId")) == PRIMARY_HEAD
        and normalize(receipt.get("platform")) == normalize(artifact.get("platform"))
        and normalize(receipt.get("rid")) == artifact_rid(artifact)
        and normalize(receipt.get("arch")) == normalize(artifact.get("arch"))
    )


def tuple_id_for_artifact(artifact: dict[str, Any]) -> str:
    head = normalize(artifact.get("head"))
    platform = normalize(artifact.get("platform"))
    rid = artifact_rid(artifact)
    return f"{head}:{platform}:{rid}" if head and platform and rid else ""


def validate_route_truth_row(
    route_truth_by_tuple: dict[str, dict[str, Any]],
    artifact: dict[str, Any],
) -> tuple[dict[str, Any], str]:
    platform = normalize(artifact.get("platform"))
    rid = artifact_rid(artifact)
    tuple_id = tuple_id_for_artifact(artifact)
    row = route_truth_by_tuple.get(tuple_id)
    proof = {
        "platform": platform,
        "head": PRIMARY_HEAD,
        "rid": rid,
        "tupleId": tuple_id,
        "status": "fail",
        "routeRole": "",
        "promotionState": "",
        "parityPosture": "",
        "publicInstallRoute": "",
        "fallbackAcceptedAsPrimary": False,
        "reason": "",
    }
    if row is None:
        reason = f"desktopRouteTruth is missing Avalonia primary row {tuple_id}"
        proof["reason"] = reason
        return proof, reason

    proof["routeRole"] = normalize(row.get("routeRole"))
    proof["promotionState"] = normalize(row.get("promotionState"))
    proof["parityPosture"] = normalize(row.get("parityPosture"))
    proof["publicInstallRoute"] = str(row.get("publicInstallRoute") or "").strip()

    if normalize(row.get("head")) != PRIMARY_HEAD:
        reason = f"desktopRouteTruth row {tuple_id} is not bound to Avalonia"
    elif normalize(row.get("platform")) != platform or normalize(row.get("rid")) != rid:
        reason = f"desktopRouteTruth row {tuple_id} does not match its installer tuple"
    elif normalize(row.get("routeRole")) != "primary":
        reason = f"desktopRouteTruth row {tuple_id} does not declare Avalonia as primary"
    elif normalize(row.get("promotionState")) != "promoted":
        reason = f"desktopRouteTruth row {tuple_id} is not promoted"
    elif normalize(row.get("parityPosture")) != "flagship_primary":
        reason = f"desktopRouteTruth row {tuple_id} does not carry flagship_primary parity posture"
    elif not str(row.get("routeRoleReason") or "").strip():
        reason = f"desktopRouteTruth row {tuple_id} is missing route-role rationale"
    elif not str(row.get("promotionReason") or "").strip():
        reason = f"desktopRouteTruth row {tuple_id} is missing promotion rationale"
    elif not str(row.get("publicInstallRoute") or "").strip():
        reason = f"desktopRouteTruth row {tuple_id} is missing public install route"
    else:
        reason = ""

    proof["status"] = "pass" if not reason else "fail"
    proof["reason"] = reason
    return proof, reason


def validate_fallback_route_truth_rows(route_truth_rows: list[dict[str, Any]]) -> list[str]:
    reasons: list[str] = []
    for row in route_truth_rows:
        if not isinstance(row, dict):
            continue
        head = normalize(row.get("head"))
        if head not in FALLBACK_HEADS:
            continue
        if normalize(row.get("routeRole")) == "primary":
            reasons.append(
                f"desktopRouteTruth fallback row {normalize(row.get('tupleId')) or head} must not be primary."
            )
        if normalize(row.get("parityPosture")) == "flagship_primary":
            reasons.append(
                f"desktopRouteTruth fallback row {normalize(row.get('tupleId')) or head} must not carry flagship_primary parity posture."
            )
    return reasons


def validate_receipt(
    receipt: dict[str, Any],
    artifact: dict[str, Any],
    now_utc: datetime,
) -> tuple[bool, str]:
    status = normalize(receipt.get("status"))
    if status not in PASSING_STARTUP_SMOKE_STATUSES:
        return False, "startup-smoke receipt status is not passing"

    if normalize(receipt.get("readyCheckpoint")) != "pre_ui_event_loop":
        return False, "startup-smoke receipt missing pre_ui_event_loop checkpoint"

    platform = normalize(artifact.get("platform"))
    host_class = normalize(receipt.get("hostClass"))
    if not host_class:
        return False, "startup-smoke receipt hostClass is missing"
    if not host_class_matches_platform(host_class, platform):
        return False, f"startup-smoke receipt hostClass does not identify the {platform} host"

    if not str(receipt.get("operatingSystem") or "").strip():
        return False, "startup-smoke receipt operatingSystem is missing"

    expected_digest = normalize(artifact.get("sha256"))
    receipt_digest = normalize(receipt.get("artifactDigest"))
    if expected_digest and receipt_digest and receipt_digest != f"sha256:{expected_digest}":
        return False, "startup-smoke receipt artifactDigest does not match manifest sha256"

    timestamp = receipt_recorded_at(receipt)
    if timestamp is None:
        return False, "startup-smoke receipt is missing a valid completed/recorded timestamp"

    age_delta_seconds = int((now_utc - timestamp).total_seconds())
    if age_delta_seconds < 0:
        future_skew_seconds = abs(age_delta_seconds)
        if future_skew_seconds > STARTUP_SMOKE_MAX_FUTURE_SKEW_SECONDS:
            return False, f"startup-smoke receipt timestamp is in the future ({future_skew_seconds}s ahead)"
    elif age_delta_seconds > STARTUP_SMOKE_MAX_AGE_SECONDS:
        return False, f"startup-smoke receipt is stale ({age_delta_seconds}s old)"

    return True, ""


def select_receipt(
    artifact: dict[str, Any],
    receipts: list[dict[str, Any]],
    now_utc: datetime,
) -> tuple[dict[str, Any] | None, str]:
    candidates = [receipt for receipt in receipts if receipt_matches_artifact(receipt, artifact)]
    if not candidates:
        return None, "Avalonia startup-smoke receipt missing"

    candidates.sort(
        key=lambda receipt: (
            int((receipt_recorded_at(receipt) or datetime.fromtimestamp(0, timezone.utc)).timestamp()),
            str(receipt.get("__sourcePath") or ""),
        ),
        reverse=True,
    )
    selected = candidates[0]
    valid, reason = validate_receipt(selected, artifact, now_utc)
    if not valid:
        return None, reason
    return selected, ""


def main() -> int:
    args = parse_args()
    manifest_path = Path(args.manifest)
    startup_smoke_dir = Path(args.startup_smoke_dir)
    manifest = load_json(manifest_path)
    if not isinstance(manifest, dict):
        raise SystemExit("manifest must be a JSON object")

    artifacts = manifest.get("artifacts") or []
    if not isinstance(artifacts, list):
        raise SystemExit("manifest artifacts must be a list")

    desktop_install_artifacts = [
        artifact for artifact in artifacts
        if isinstance(artifact, dict)
        and is_desktop_install_artifact(artifact)
        and normalize(artifact.get("platform")) in REQUIRED_PLATFORMS
    ]
    primary_artifacts = [
        artifact for artifact in desktop_install_artifacts
        if normalize(artifact.get("head")) == PRIMARY_HEAD
    ]
    fallback_artifacts = [
        artifact for artifact in desktop_install_artifacts
        if normalize(artifact.get("head")) in FALLBACK_HEADS
    ]

    primary_artifact_by_platform = {
        normalize(artifact.get("platform")): artifact for artifact in primary_artifacts
    }
    receipts = load_receipts(startup_smoke_dir)
    now_utc = datetime.now(timezone.utc)

    reasons: list[str] = []
    platform_proof: list[dict[str, Any]] = []
    for platform in REQUIRED_PLATFORMS:
        artifact = primary_artifact_by_platform.get(platform)
        if artifact is None:
            reasons.append(f"Avalonia primary installer artifact is missing for {platform}.")
            platform_proof.append(
                {
                    "platform": platform,
                    "head": PRIMARY_HEAD,
                    "status": "fail",
                    "reason": "Avalonia primary installer artifact missing",
                }
            )
            continue

        receipt, reason = select_receipt(artifact, receipts, now_utc)
        status = "pass" if receipt is not None else "fail"
        if reason:
            reasons.append(f"{platform} Avalonia primary route proof failed: {reason}.")
        platform_proof.append(
            {
                "platform": platform,
                "head": PRIMARY_HEAD,
                "rid": artifact_rid(artifact),
                "artifactId": artifact.get("artifactId"),
                "artifactSha256": artifact.get("sha256"),
                "status": status,
                "startupSmokeReceiptPath": str((receipt or {}).get("__sourcePath") or ""),
                "fallbackReceiptsAccepted": False,
                "reason": reason,
            }
        )

    tuple_coverage = manifest.get("desktopTupleCoverage")
    required_heads = []
    if isinstance(tuple_coverage, dict) and isinstance(tuple_coverage.get("requiredDesktopHeads"), list):
        required_heads = [normalize(value) for value in tuple_coverage.get("requiredDesktopHeads") or []]
    if PRIMARY_HEAD not in required_heads:
        reasons.append("desktopTupleCoverage.requiredDesktopHeads does not declare avalonia as the primary required head.")
    for fallback_head in sorted(FALLBACK_HEADS):
        if fallback_head in required_heads:
            reasons.append(
                f"desktopTupleCoverage.requiredDesktopHeads must not require fallback head {fallback_head} for primary-route proof."
            )

    route_truth_rows: list[dict[str, Any]] = []
    if isinstance(tuple_coverage, dict) and isinstance(tuple_coverage.get("desktopRouteTruth"), list):
        route_truth_rows = [
            row for row in tuple_coverage.get("desktopRouteTruth") or []
            if isinstance(row, dict)
        ]

    route_truth_proof: list[dict[str, Any]] = []
    if route_truth_rows:
        route_truth_by_tuple = {
            normalize(row.get("tupleId")): row for row in route_truth_rows
            if normalize(row.get("tupleId"))
        }
        for platform in REQUIRED_PLATFORMS:
            artifact = primary_artifact_by_platform.get(platform)
            if artifact is None:
                continue
            proof, reason = validate_route_truth_row(route_truth_by_tuple, artifact)
            route_truth_proof.append(proof)
            if reason:
                reasons.append(reason)
        reasons.extend(validate_fallback_route_truth_rows(route_truth_rows))
    else:
        reasons.append("desktopTupleCoverage.desktopRouteTruth is missing; primary-route proof must cite route-role truth.")

    payload = {
        "contractName": "chummer6-ui.avalonia_primary_route_proof",
        "generatedAt": args.generated_at.strip() or now_rfc3339(),
        "status": "pass" if not reasons else "fail",
        "primaryHead": PRIMARY_HEAD,
        "requiredPlatforms": list(REQUIRED_PLATFORMS),
        "fallbackHeadsExcludedFromPrimaryProof": sorted(FALLBACK_HEADS),
        "fallbackInstallArtifactCount": len(fallback_artifacts),
        "fallbackReceiptCount": sum(1 for receipt in receipts if normalize(receipt.get("headId")) in FALLBACK_HEADS),
        "platformProof": platform_proof,
        "routeTruthProof": route_truth_proof,
        "reasons": reasons,
        "blockingFindings": reasons,
        "blockingFindingsCount": len(reasons),
    }

    if args.output:
        output_path = Path(args.output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    if reasons:
        print(json.dumps(payload, indent=2))
        return 1

    if not args.output:
        print(json.dumps(payload, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
