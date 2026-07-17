#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any


PASSING_STATUSES = {"pass", "passed", "ready"}
NATIVE_WINDOWS_EXECUTION_ENVIRONMENT = "native_windows"
WINDOWS_COMPATIBILITY_EXECUTION_ENVIRONMENTS = {"wine_compatibility", "windows_compatibility"}
NATIVE_WINDOWS_REQUIRED_CHANNELS = {"stable", "public_stable"}
BOOTSTRAP_PROGRESS_FAILURE_MARKERS = (
    "Payload download failed:",
    "Bundled curl download failed",
    "bundled curl download timed out",
    "bundled curl downloader did not start",
    "bundled curl completed without creating the payload file",
    "Chummer could not download the application files.",
)


def norm(value: Any) -> str:
    return str(value or "").strip().lower()


def native_windows_proof_required(
    release_payload: dict[str, Any],
    *,
    release_channel: str,
    explicit_requirement: bool,
) -> bool:
    if explicit_requirement or release_channel in NATIVE_WINDOWS_REQUIRED_CHANNELS:
        return True
    if release_payload.get("requireNativeWindowsStartupProof") is True:
        return True
    return norm(release_payload.get("windowsStartupProofPolicy")) == "native_required"


def validate_windows_execution_evidence(
    receipt: dict[str, Any],
    *,
    receipt_name: str,
    require_native_windows: bool,
) -> list[str]:
    errors: list[str] = []
    execution_environment = norm(receipt.get("executionEnvironment"))
    evidence = receipt.get("nativeHostEvidence")

    if execution_environment not in {
        NATIVE_WINDOWS_EXECUTION_ENVIRONMENT,
        *WINDOWS_COMPATIBILITY_EXECUTION_ENVIRONMENTS,
    }:
        errors.append(
            f"Windows installer startup-smoke receipt executionEnvironment is missing or unsupported: {receipt_name}"
        )
        return errors
    if not isinstance(evidence, dict):
        errors.append(
            f"Windows installer startup-smoke receipt nativeHostEvidence is missing or invalid: {receipt_name}"
        )
        return errors

    contract_name = str(evidence.get("contractName") or "").strip()
    evidence_status = norm(evidence.get("status"))
    is_native_windows = evidence.get("isNativeWindows")
    host_platform = norm(evidence.get("hostPlatform"))
    host_kernel = norm(evidence.get("hostKernel"))
    runner = norm(evidence.get("runner"))
    evidence_source = norm(evidence.get("evidenceSource"))

    if contract_name != "chummer6-ui.native_windows_host_evidence":
        errors.append(f"Windows installer startup-smoke receipt nativeHostEvidence contract is invalid: {receipt_name}")
    if not isinstance(is_native_windows, bool):
        errors.append(
            f"Windows installer startup-smoke receipt nativeHostEvidence.isNativeWindows must be boolean: {receipt_name}"
        )
    if not host_platform:
        errors.append(f"Windows installer startup-smoke receipt nativeHostEvidence hostPlatform is missing: {receipt_name}")
    if not host_kernel:
        errors.append(f"Windows installer startup-smoke receipt nativeHostEvidence hostKernel is missing: {receipt_name}")
    if not runner:
        errors.append(f"Windows installer startup-smoke receipt nativeHostEvidence runner is missing: {receipt_name}")
    if not evidence_source:
        errors.append(
            f"Windows installer startup-smoke receipt nativeHostEvidence evidenceSource is missing: {receipt_name}"
        )

    if execution_environment == NATIVE_WINDOWS_EXECUTION_ENVIRONMENT:
        if evidence_status != "verified" or is_native_windows is not True or host_platform != "windows":
            errors.append(
                f"Windows installer startup-smoke receipt native Windows evidence is internally inconsistent: {receipt_name}"
            )
        if "wine" in runner:
            errors.append(f"Windows installer startup-smoke receipt cannot classify Wine as native Windows: {receipt_name}")
        if not any(token in host_kernel for token in ("mingw", "msys", "cygwin", "windows")):
            errors.append(
                f"Windows installer startup-smoke receipt native Windows evidence has a non-Windows host kernel: {receipt_name}"
            )
    else:
        if evidence_status != "not_native" or is_native_windows is not False:
            errors.append(
                f"Windows installer startup-smoke receipt compatibility evidence is internally inconsistent: {receipt_name}"
            )
        if execution_environment == "wine_compatibility" and "wine" not in runner:
            errors.append(f"Windows installer startup-smoke receipt Wine evidence has a non-Wine runner: {receipt_name}")

    if require_native_windows and execution_environment != NATIVE_WINDOWS_EXECUTION_ENVIRONMENT:
        errors.append(
            f"Native Windows startup proof is required; compatibility execution cannot satisfy this release: {receipt_name}"
        )
    return errors


def parse_int(value: Any) -> int:
    try:
        return int(value or 0)
    except Exception:
        return 0


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def extract_prefixed_line(text: str, prefix: str) -> str:
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if line.startswith(prefix):
            return line[len(prefix) :].strip()
    return ""


def normalize_windows_path(value: str) -> str:
    return value.strip().replace("/", "\\").rstrip("\\").lower()


def windows_path_file_name(value: str) -> str:
    normalized = value.strip().replace("/", "\\").rstrip("\\")
    if not normalized:
        return ""
    return normalized.rsplit("\\", 1)[-1].lower()


def expected_payload_acquisition_mode(row: dict[str, Any]) -> str:
    return norm(row.get("payloadAcquisitionMode")) or "download"


def startup_smoke_search_roots(
    *,
    release_channel_manifest: Path,
    startup_smoke_dir: Path,
) -> list[Path]:
    roots: list[Path] = [startup_smoke_dir]
    for source_path in (startup_smoke_dir, release_channel_manifest):
        roots.extend(list(source_path.parents[:5]))

    candidate_paths: list[Path] = []
    for root in roots:
        candidate_paths.append(root / "startup-smoke")
        candidate_paths.append(root / ".codex-studio" / "published" / "startup-smoke")
        candidate_paths.append(root / "Docker" / "Downloads" / "startup-smoke")
        candidate_paths.append(root / "Chummer.Portal" / "downloads" / "startup-smoke")

    ordered: list[Path] = []
    seen: set[Path] = set()
    for candidate in candidate_paths:
        resolved = candidate.resolve(strict=False)
        if resolved in seen or not resolved.is_dir():
            continue
        seen.add(resolved)
        ordered.append(resolved)
    return ordered


def resolve_windows_progress_log(
    *,
    release_channel_manifest: Path,
    startup_smoke_dir: Path,
    head: str,
    rid: str,
) -> Path:
    progress_log_name = f"windows-installer-progress-{head}-{rid}.log"
    for root in startup_smoke_search_roots(
        release_channel_manifest=release_channel_manifest,
        startup_smoke_dir=startup_smoke_dir,
    ):
        candidate = root / progress_log_name
        if candidate.is_file():
            return candidate
    return startup_smoke_dir / progress_log_name


def receipt_matches_current_release(receipt: dict[str, Any], *, release_version: str, release_channel: str) -> bool:
    receipt_version = str(receipt.get("releaseVersion") or receipt.get("version") or "").strip()
    receipt_channel = norm(receipt.get("channelId") or receipt.get("channel"))
    if release_version and receipt_version and receipt_version != release_version:
        return False
    if release_channel and receipt_channel and receipt_channel != release_channel:
        return False
    return True


def find_row(rows: list[dict[str, Any]], *, head: str, rid: str, platform: str, file_name: str) -> dict[str, Any] | None:
    normalized_platform = norm(platform)
    normalized_head = norm(head)
    normalized_rid = norm(rid)
    normalized_arch = normalized_rid.rsplit("-", 1)[-1] if "-" in normalized_rid else normalized_rid
    normalized_name = str(file_name or "").strip()
    for row in rows:
        row_platform = norm(row.get("platform"))
        row_platform_id = norm(row.get("platformId"))
        if row_platform_id:
            accepted_platform_ids = {
                normalized_platform,
                f"{normalized_platform}-{normalized_arch}",
            }
            if row_platform_id not in accepted_platform_ids:
                continue
        elif row_platform != normalized_platform:
            continue
        if norm(row.get("head")) != normalized_head:
            continue
        row_rid = norm(row.get("rid"))
        if row_rid:
            if row_rid != normalized_rid:
                continue
        elif norm(row.get("arch")) != normalized_arch:
            continue
        if str(row.get("fileName") or "").strip() != normalized_name:
            continue
        return row
    return None


def verify(
    *,
    release_channel_manifest: Path,
    startup_smoke_dir: Path,
    files_dir: Path,
    downloads_manifest: Path | None = None,
    require_native_windows: bool = False,
) -> dict[str, Any]:
    payload = load_json(release_channel_manifest)
    downloads_payload = load_json(downloads_manifest) if downloads_manifest and downloads_manifest.is_file() else {}
    errors: list[str] = []
    artifact_rows = [item for item in payload.get("artifacts") or [] if isinstance(item, dict)]
    download_rows = [item for item in downloads_payload.get("downloads") or [] if isinstance(item, dict)]
    results: list[dict[str, Any]] = []

    release_version = str(payload.get("version") or payload.get("releaseVersion") or "").strip()
    release_channel = norm(payload.get("channelId") or payload.get("channel"))
    native_windows_required = native_windows_proof_required(
        payload,
        release_channel=release_channel,
        explicit_requirement=require_native_windows,
    )

    for artifact in artifact_rows:
        if norm(artifact.get("platform")) != "windows":
            continue
        if norm(artifact.get("kind")) not in {"installer", "msix"}:
            continue
        file_name = str(artifact.get("fileName") or "").strip()
        if not file_name.endswith(("-installer.exe", ".msix")):
            continue
        head = norm(artifact.get("head"))
        rid = norm(artifact.get("rid"))
        if not head or not rid:
            errors.append(f"Windows install artifact is missing head/rid: {artifact}")
            continue
        receipt_path = startup_smoke_dir / f"startup-smoke-{head}-{rid}.receipt.json"
        if not receipt_path.is_file():
            errors.append(f"Windows installer startup-smoke receipt is missing: {receipt_path.name}")
            continue
        try:
            receipt = load_json(receipt_path)
        except Exception as exc:
            errors.append(f"Windows installer startup-smoke receipt is unreadable: {receipt_path} ({exc})")
            continue
        status = norm(receipt.get("status"))
        if status not in PASSING_STATUSES:
            errors.append(f"Windows installer startup-smoke receipt is not passing: {receipt_path.name} status={status or 'missing'}")
        errors.extend(
            validate_windows_execution_evidence(
                receipt,
                receipt_name=receipt_path.name,
                require_native_windows=native_windows_required,
            )
        )
        if norm(receipt.get("readyCheckpoint")) != "pre_ui_event_loop":
            errors.append(f"Windows installer startup-smoke receipt did not reach pre_ui_event_loop: {receipt_path.name}")
        if norm(receipt.get("headId")) != head:
            errors.append(f"Windows installer startup-smoke receipt headId mismatch: {receipt_path.name}")
        if norm(receipt.get("rid")) != rid:
            errors.append(f"Windows installer startup-smoke receipt rid mismatch: {receipt_path.name}")
        if norm(receipt.get("platform")) != "windows":
            errors.append(f"Windows installer startup-smoke receipt platform mismatch: {receipt_path.name}")
        file_path = files_dir / file_name
        if not file_path.is_file():
            errors.append(f"Windows installer file referenced by manifest is missing: {file_name}")
            continue
        expected_digest = f"sha256:{sha256_file(file_path)}"
        if norm(receipt.get("artifactDigest")) != expected_digest:
            errors.append(f"Windows installer startup-smoke receipt artifactDigest mismatch: {receipt_path.name}")
        if norm(artifact.get("installerMode")) == "bootstrap":
            payload_file_name = str(artifact.get("payloadFileName") or "").strip()
            expected_acquisition_mode = expected_payload_acquisition_mode(artifact)
            if expected_acquisition_mode not in {"download", "embedded"}:
                errors.append(
                    f"Windows bootstrap installer manifest payloadAcquisitionMode is unsupported: {expected_acquisition_mode}"
                )
            progress_log_path = resolve_windows_progress_log(
                release_channel_manifest=release_channel_manifest,
                startup_smoke_dir=startup_smoke_dir,
                head=head,
                rid=rid,
            )
            if not progress_log_path.is_file():
                errors.append(
                    f"Windows bootstrap installer startup-smoke progress log is missing: {progress_log_path.name}"
                )
            else:
                progress_text = load_text(progress_log_path)
                required_markers = [
                    "Bootstrap temp root:",
                    "Verifying payload size",
                    "Verifying payload checksum",
                    "Extracting application files",
                    "Install complete",
                ]
                if expected_acquisition_mode == "embedded":
                    required_markers.extend(
                        (
                            "Payload acquisition mode: embedded",
                            "Payload acquisition target:",
                            "Using embedded payload",
                        )
                    )
                else:
                    required_markers.extend(("Payload download target:", "Downloading application files"))
                for marker in required_markers:
                    if marker not in progress_text:
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log is missing '{marker}': {progress_log_path.name}"
                        )
                for marker in BOOTSTRAP_PROGRESS_FAILURE_MARKERS:
                    if marker in progress_text:
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log contains failure marker '{marker}': {progress_log_path.name}"
                        )
                if expected_acquisition_mode == "download":
                    progress_metric_lines = [
                        line.strip()
                        for line in progress_text.splitlines()
                        if line.strip().startswith("Downloading application files -")
                    ]
                    if not progress_metric_lines:
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log is missing live download detail lines: {progress_log_path.name}"
                        )
                    elif not any("%" in line and "/s" in line for line in progress_metric_lines):
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log is missing a percent-and-speed download line: {progress_log_path.name}"
                        )
                bootstrap_temp_root = extract_prefixed_line(progress_text, "Bootstrap temp root:")
                if not bootstrap_temp_root:
                    errors.append(
                        f"Windows bootstrap installer startup-smoke progress log is missing the bootstrap temp root value: {progress_log_path.name}"
                    )
                payload_target_prefix = (
                    "Payload acquisition target:"
                    if expected_acquisition_mode == "embedded"
                    else "Payload download target:"
                )
                payload_target = extract_prefixed_line(progress_text, payload_target_prefix)
                if not payload_target:
                    errors.append(
                        f"Windows bootstrap installer startup-smoke progress log is missing the payload target value: {progress_log_path.name}"
                    )
                elif payload_target.startswith(("\\", "/")):
                    errors.append(
                        f"Windows bootstrap installer startup-smoke progress log captured a root-level payload target: {progress_log_path.name}"
                    )
                elif bootstrap_temp_root:
                    normalized_root = normalize_windows_path(bootstrap_temp_root)
                    normalized_target = normalize_windows_path(payload_target)
                    if normalized_root and not normalized_target.startswith(normalized_root + "\\"):
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log payload target is outside the bootstrap temp root: {progress_log_path.name}"
                        )
                if payload_target and payload_file_name:
                    target_file_name = windows_path_file_name(payload_target)
                    if target_file_name and target_file_name != payload_file_name.lower():
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log payload target file name does not match release metadata: {progress_log_path.name}"
                        )
            if norm(receipt.get("bootstrapPayloadAcquisitionMode")) != expected_acquisition_mode:
                errors.append(
                    "Windows bootstrap installer startup-smoke receipt did not exercise expected payload "
                    f"acquisition mode {expected_acquisition_mode}: {receipt_path.name}"
                )
            if payload_file_name and norm(receipt.get("bootstrapPayloadFileName")) != norm(payload_file_name):
                errors.append(
                    f"Windows bootstrap installer startup-smoke receipt payloadFileName mismatch: {receipt_path.name}"
                )
            payload_sha256 = norm(artifact.get("payloadSha256"))
            if payload_sha256 and norm(receipt.get("bootstrapPayloadSha256")) != payload_sha256:
                errors.append(
                    f"Windows bootstrap installer startup-smoke receipt payloadSha256 mismatch: {receipt_path.name}"
                )
            payload_size_bytes = parse_int(artifact.get("payloadSizeBytes"))
            if payload_size_bytes and parse_int(receipt.get("bootstrapPayloadSizeBytes")) != payload_size_bytes:
                errors.append(
                    f"Windows bootstrap installer startup-smoke receipt payloadSizeBytes mismatch: {receipt_path.name}"
                )
        results.append(
            {
                "fileName": file_name,
                "head": head,
                "rid": rid,
                "receipt": str(receipt_path),
                "progressLog": str(startup_smoke_dir / f"windows-installer-progress-{head}-{rid}.log"),
                "installerMode": norm(artifact.get("installerMode")),
                "payloadAcquisitionMode": (
                    expected_payload_acquisition_mode(artifact)
                    if norm(artifact.get("installerMode")) == "bootstrap"
                    else ""
                ),
                "executionEnvironment": norm(receipt.get("executionEnvironment")),
            }
        )

    for receipt_path in sorted(startup_smoke_dir.glob("startup-smoke-*.receipt.json")):
        try:
            receipt = load_json(receipt_path)
        except Exception as exc:
            errors.append(f"Windows installer startup-smoke receipt is unreadable: {receipt_path} ({exc})")
            continue
        if norm(receipt.get("platform")) != "windows":
            continue
        if norm(receipt.get("status")) not in PASSING_STATUSES:
            continue
        if not receipt_matches_current_release(receipt, release_version=release_version, release_channel=release_channel):
            continue
        head = norm(receipt.get("headId") or receipt.get("head"))
        rid = norm(receipt.get("rid"))
        file_name = str(receipt.get("artifactFileName") or receipt.get("fileName") or "").strip()
        if not head or not rid or not file_name:
            errors.append(f"Windows installer startup-smoke receipt is missing head/rid/file name metadata: {receipt_path.name}")
            continue
        file_path = files_dir / file_name
        if not file_path.is_file():
            errors.append(
                f"Windows installer startup-smoke receipt exists for the current release but matching stage bytes are missing: {file_name}"
            )
            continue
        expected_digest = f"sha256:{sha256_file(file_path)}"
        if norm(receipt.get("artifactDigest")) != expected_digest:
            errors.append(f"Windows installer startup-smoke receipt artifactDigest mismatch: {receipt_path.name}")
            continue
        manifest_row = find_row(
            artifact_rows,
            head=head,
            rid=rid,
            platform="windows",
            file_name=file_name,
        )
        if manifest_row is None:
            errors.append(
                f"Windows installer startup-smoke receipt exists for the current release but RELEASE_CHANNEL.generated.json omits the matching installer row: {file_name}"
            )
        downloads_row = find_row(
            download_rows,
            head=head,
            rid=rid,
            platform="windows",
            file_name=file_name,
        )
        if downloads_manifest and downloads_manifest.is_file() and downloads_row is None:
            errors.append(
                f"Windows installer startup-smoke receipt exists for the current release but releases.json omits the matching installer row: {file_name}"
            )
            continue
        reference_row = manifest_row or downloads_row
        if reference_row is not None and norm(reference_row.get("installerMode")) == "bootstrap":
            expected_acquisition_mode = expected_payload_acquisition_mode(reference_row)
            if norm(receipt.get("bootstrapPayloadAcquisitionMode")) != expected_acquisition_mode:
                errors.append(
                    "Windows installer startup-smoke receipt exists for the current release but did not prove "
                    f"bootstrap payload acquisition mode {expected_acquisition_mode}: {receipt_path.name}"
                )

    return {
        "status": "pass" if not errors else "fail",
        "errors": errors,
        "checkedArtifacts": results,
        "releaseVersion": release_version,
        "releaseChannel": release_channel,
        "nativeWindowsRequired": native_windows_required,
        "releaseChannelManifest": str(release_channel_manifest),
        "downloadsManifest": str(downloads_manifest) if downloads_manifest else "",
        "filesDir": str(files_dir),
        "startupSmokeDir": str(startup_smoke_dir),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verify Windows bootstrap startup-smoke receipts against staged installer bytes and release manifests.")
    parser.add_argument("--release-channel", required=True, type=Path)
    parser.add_argument("--startup-smoke-dir", required=True, type=Path)
    parser.add_argument("--files-dir", required=True, type=Path)
    parser.add_argument("--downloads-manifest", type=Path, default=None)
    parser.add_argument(
        "--require-native-windows",
        action="store_true",
        help="Require native Windows execution evidence even for a preview/nightly manifest.",
    )
    parser.add_argument("--output", type=Path, default=None)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    payload = verify(
        release_channel_manifest=args.release_channel,
        startup_smoke_dir=args.startup_smoke_dir,
        files_dir=args.files_dir,
        downloads_manifest=args.downloads_manifest,
        require_native_windows=args.require_native_windows,
    )
    if args.output is not None:
        args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    if payload["errors"]:
        for error in payload["errors"]:
            print(error, file=sys.stderr)
        return 1
    print(f"windows-bootstrap-startup-smoke:ok checked={len(payload['checkedArtifacts'])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
