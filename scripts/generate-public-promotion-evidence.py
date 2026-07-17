#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


INSTALLER_KINDS = {"installer", "dmg", "pkg", "msix"}
PASSING_STARTUP_SMOKE_STATUSES = {"pass", "passed", "ready"}
NATIVE_WINDOWS_EXECUTION_ENVIRONMENT = "native_windows"
WINDOWS_COMPATIBILITY_EXECUTION_ENVIRONMENTS = {"wine_compatibility", "windows_compatibility"}
NATIVE_WINDOWS_REQUIRED_CHANNELS = {"stable", "public_stable"}
STARTUP_SMOKE_READY_MARKER = "startup smoke ready:"
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
SAFE_RECEIPT_BASENAME_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,199}\.receipt\.json$")


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


def host_class_matches_platform(host_class: str, platform: str, operating_system: str = "") -> bool:
    normalized_host = normalize_token(host_class)
    normalized_operating_system = normalize_token(operating_system)
    expected_tokens = expected_host_class_platform_tokens(platform)
    if not normalized_host or not expected_tokens:
        return False
    host_tokens = [token for token in normalized_host.split("-") if token]
    if any(token in host_tokens for token in expected_tokens):
        return True
    if normalize_platform(platform) == "windows" and "windows" in normalized_operating_system and "wine" in normalized_host:
        return True
    return False


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

    for path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
        if path.is_symlink() or not path.is_file() or not SAFE_RECEIPT_BASENAME_RE.fullmatch(path.name):
            raise ValueError("startup-smoke receipt must be a regular file with a safe public basename")
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

    for path in sorted(signing_receipts_dir.glob("*.receipt.json")):
        if path.is_symlink() or not path.is_file() or not SAFE_RECEIPT_BASENAME_RE.fullmatch(path.name):
            raise ValueError("signing receipt must be a regular file with a safe public basename")
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


def companion_log_recorded_at(receipt: dict) -> datetime | None:
    source_path_raw = str(receipt.get("__sourcePath") or "").strip()
    if not source_path_raw:
        return None

    source_path = Path(source_path_raw)
    if source_path.name.endswith(".receipt.json"):
        companion_name = source_path.name[: -len(".receipt.json")] + ".log"
    else:
        companion_name = f"{source_path.name}.log"
    companion_path = source_path.with_name(companion_name)
    if not companion_path.is_file():
        return None

    try:
        contents = companion_path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    if STARTUP_SMOKE_READY_MARKER not in contents.lower():
        return None

    return datetime.fromtimestamp(companion_path.stat().st_mtime, tz=timezone.utc)


def receipt_recorded_at(receipt: dict) -> datetime | None:
    candidates = [
        parse_iso_utc(receipt.get("sourceUpdatedAtUtc")),
        parse_iso_utc(receipt.get("completedAtUtc")),
        parse_iso_utc(receipt.get("recordedAtUtc")),
        parse_iso_utc(receipt.get("startedAtUtc")),
        companion_log_recorded_at(receipt),
    ]
    valid_candidates = [candidate for candidate in candidates if candidate is not None]
    if not valid_candidates:
        return None
    return max(valid_candidates)


def incompatible_host_startup_smoke_receipt(receipt: dict) -> bool:
    status = normalize_token(receipt.get("status"))
    if status != "skipped":
        return False
    verification_disposition = normalize_token(receipt.get("verificationDisposition"))
    skip_class = normalize_token(receipt.get("skipClass"))
    return verification_disposition == "incompatible_host" or skip_class == "incompatible_host"


def native_windows_proof_required(manifest: dict, channel: str) -> bool:
    if normalize_token(channel) in NATIVE_WINDOWS_REQUIRED_CHANNELS:
        return True
    if manifest.get("requireNativeWindowsStartupProof") is True:
        return True
    return normalize_token(manifest.get("windowsStartupProofPolicy")) == "native_required"


def validate_windows_execution_evidence(receipt: dict, require_native_windows: bool) -> tuple[bool, str]:
    execution_environment = normalize_token(receipt.get("executionEnvironment"))
    evidence = receipt.get("nativeHostEvidence")
    if execution_environment not in {
        NATIVE_WINDOWS_EXECUTION_ENVIRONMENT,
        *WINDOWS_COMPATIBILITY_EXECUTION_ENVIRONMENTS,
    }:
        return False, "startup-smoke receipt executionEnvironment is missing or unsupported"
    if not isinstance(evidence, dict):
        return False, "startup-smoke receipt nativeHostEvidence is missing or invalid"

    contract_name = str(evidence.get("contractName") or "").strip()
    evidence_status = normalize_token(evidence.get("status"))
    is_native_windows = evidence.get("isNativeWindows")
    host_platform = normalize_token(evidence.get("hostPlatform"))
    host_kernel = normalize_token(evidence.get("hostKernel"))
    runner = normalize_token(evidence.get("runner"))
    evidence_source = normalize_token(evidence.get("evidenceSource"))

    if contract_name != "chummer6-ui.native_windows_host_evidence":
        return False, "startup-smoke receipt nativeHostEvidence contract is invalid"
    if not isinstance(is_native_windows, bool):
        return False, "startup-smoke receipt nativeHostEvidence.isNativeWindows must be boolean"
    if not host_platform:
        return False, "startup-smoke receipt nativeHostEvidence hostPlatform is missing"
    if not host_kernel:
        return False, "startup-smoke receipt nativeHostEvidence hostKernel is missing"
    if not runner:
        return False, "startup-smoke receipt nativeHostEvidence runner is missing"
    if not evidence_source:
        return False, "startup-smoke receipt nativeHostEvidence evidenceSource is missing"

    if execution_environment == NATIVE_WINDOWS_EXECUTION_ENVIRONMENT:
        if evidence_status != "verified" or is_native_windows is not True or host_platform != "windows":
            return False, "startup-smoke receipt native Windows evidence is internally inconsistent"
        if "wine" in runner:
            return False, "startup-smoke receipt cannot classify Wine as native Windows"
        if not any(token in host_kernel for token in ("mingw", "msys", "cygwin", "windows")):
            return False, "startup-smoke receipt native Windows evidence has a non-Windows host kernel"
    else:
        if evidence_status != "not_native" or is_native_windows is not False:
            return False, "startup-smoke receipt compatibility evidence is internally inconsistent"
        if execution_environment == "wine_compatibility" and "wine" not in runner:
            return False, "startup-smoke receipt Wine evidence has a non-Wine runner"

    if require_native_windows and execution_environment != NATIVE_WINDOWS_EXECUTION_ENVIRONMENT:
        return False, "native Windows startup proof is required; compatibility execution is insufficient"
    return True, ""


def signing_receipt_generated_at(receipt: dict) -> datetime | None:
    return parse_iso_utc(receipt.get("generatedAt") or receipt.get("generated_at"))


def validate_receipt_for_artifact(
    receipt: dict,
    expected_platform: str,
    expected_rid: str,
    expected_digest: str,
    now_utc: datetime,
    require_native_windows: bool = False,
) -> tuple[bool, str]:
    status = normalize_token(receipt.get("status"))
    incompatible_host_skip = incompatible_host_startup_smoke_receipt(receipt)
    if status not in PASSING_STARTUP_SMOKE_STATUSES and not incompatible_host_skip:
        return False, "startup-smoke receipt status is neither passing nor an explicit incompatible-host skip"

    checkpoint = normalize_token(receipt.get("readyCheckpoint"))
    if not incompatible_host_skip and checkpoint != "pre_ui_event_loop":
        return False, "startup-smoke receipt missing pre_ui_event_loop checkpoint"

    digest = normalize_token(receipt.get("artifactDigest"))
    if expected_digest and digest and digest != f"sha256:{expected_digest}":
        return False, "startup-smoke receipt artifactDigest does not match manifest sha256"

    host_class = normalize_token(receipt.get("hostClass"))
    operating_system = str(receipt.get("operatingSystem") or "").strip()
    if not incompatible_host_skip:
        if not host_class:
            return False, "startup-smoke receipt hostClass is missing"
        if not host_class_matches_platform(host_class, expected_platform, operating_system):
            return False, f"startup-smoke receipt hostClass does not identify the {expected_platform} host"
    if not incompatible_host_skip and not operating_system:
        return False, "startup-smoke receipt operatingSystem is missing"

    if normalize_platform(expected_platform) == "windows":
        if incompatible_host_skip and require_native_windows:
            return False, "native Windows startup proof is required; an incompatible-host skip is insufficient"
        if not incompatible_host_skip:
            valid_execution_evidence, execution_reason = validate_windows_execution_evidence(
                receipt,
                require_native_windows,
            )
            if not valid_execution_evidence:
                return False, execution_reason

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

    return True, "incompatible-host skip accepted" if incompatible_host_skip else ""


def find_matching_receipt(
    artifact: dict,
    receipts: list[dict],
    now_utc: datetime,
    *,
    require_native_windows: bool = False,
) -> tuple[dict | None, str]:
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
    is_valid, reason = validate_receipt_for_artifact(
        candidate,
        expected_platform,
        expected_rid,
        expected_digest,
        now_utc,
        require_native_windows=require_native_windows,
    )
    if not is_valid:
        return None, reason

    return candidate, ""


def public_receipt_reference(receipt: dict | None, directory: str) -> str:
    if not receipt:
        return ""
    source_path = str((receipt or {}).get("__sourcePath") or "").strip()
    if not source_path:
        return ""
    basename = Path(source_path).name
    if not SAFE_RECEIPT_BASENAME_RE.fullmatch(basename):
        raise ValueError("receipt filename is unsafe for public evidence")
    return f"{directory}/{basename}"


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
    if unsigned_public_release_allowed() and channel != "preview":
        return "unsigned_public_release"
    return "skipped_preview" if channel == "preview" else "fail"


def allowed_mac_statuses(channel: str) -> tuple[str, str]:
    signing = env_override("CHUMMER_MAC_SIGNING_STATUS_OVERRIDE", "CHUMMER_MAC_SIGNING_STATUS")
    notarization = env_override("CHUMMER_MAC_NOTARIZATION_STATUS_OVERRIDE", "CHUMMER_MAC_NOTARIZATION_STATUS")
    if signing and notarization:
        return signing, notarization
    if unsigned_public_release_allowed() and channel != "preview":
        return signing or "unsigned_public_release", notarization or "unsigned_public_release"
    if channel == "preview":
        return signing or "skipped_preview", notarization or "skipped_preview"
    return signing or "fail", notarization or "fail"


def compute_promotion_status(platform: str, channel: str, startup_smoke_status: str, signing_status: str | None, notarization_status: str | None) -> str:
    if startup_smoke_status not in {"pass", "skipped_incompatible_host"}:
        return "fail"

    if platform == "windows":
        allowed = {"pass"}
        if channel == "preview":
            allowed.add("skipped_preview")
        if unsigned_public_release_allowed() and channel != "preview":
            allowed.add("unsigned_public_release")
        return "pass" if signing_status in allowed else "fail"

    if platform == "macos":
        allowed = {"pass"}
        if channel == "preview":
            allowed.add("skipped_preview")
        if unsigned_public_release_allowed() and channel != "preview":
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

    channel = (args.channel or manifest.get("channelId") or manifest.get("channel") or "").strip().lower()
    require_native_windows = native_windows_proof_required(manifest, channel)
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
            receipt, startup_smoke_reason = find_matching_receipt(
                artifact,
                receipts,
                now_utc,
                require_native_windows=platform == "windows" and require_native_windows,
            )
        if not installer:
            startup_smoke_status = "pass"
        elif receipt is None:
            startup_smoke_status = "fail"
        elif incompatible_host_startup_smoke_receipt(receipt):
            startup_smoke_status = "skipped_incompatible_host"
        else:
            startup_smoke_status = "pass"

        signing_status: str | None = None
        notarization_status: str | None = None
        signing_receipt_path = ""
        signing_receipt, signing_artifact = find_matching_signing_receipt(artifact, signing_receipts)
        if signing_receipt is not None:
            signing_status = normalize_token((signing_artifact or {}).get("signingStatus") or signing_receipt.get("signingStatus")) or None
            notarization_status = normalize_token((signing_artifact or {}).get("notarizationStatus") or signing_receipt.get("notarizationStatus")) or None
            signing_receipt_path = public_receipt_reference(signing_receipt, "signing")
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
                "installAccessClass": artifact.get("installAccessClass"),
                "promotionStatus": compute_promotion_status(platform, channel, startup_smoke_status, signing_status, notarization_status),
                "startupSmokeStatus": startup_smoke_status,
                "startupSmokeReason": startup_smoke_reason,
                "startupSmokeReceiptPath": public_receipt_reference(receipt, "startup-smoke"),
                "startupSmokeExecutionEnvironment": normalize_token((receipt or {}).get("executionEnvironment")),
                "nativeWindowsStartupProofRequired": platform == "windows" and require_native_windows,
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
