#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any


PASSING_STATUSES = {"pass", "passed", "ready"}


def norm(value: Any) -> str:
    return str(value or "").strip().lower()


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
            if row_platform_id != normalized_platform:
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
) -> dict[str, Any]:
    payload = load_json(release_channel_manifest)
    downloads_payload = load_json(downloads_manifest) if downloads_manifest and downloads_manifest.is_file() else {}
    errors: list[str] = []
    artifact_rows = [item for item in payload.get("artifacts") or [] if isinstance(item, dict)]
    download_rows = [item for item in downloads_payload.get("downloads") or [] if isinstance(item, dict)]
    results: list[dict[str, Any]] = []

    release_version = str(payload.get("version") or payload.get("releaseVersion") or "").strip()
    release_channel = norm(payload.get("channelId") or payload.get("channel"))

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
            progress_log_path = startup_smoke_dir / f"windows-installer-progress-{head}-{rid}.log"
            if not progress_log_path.is_file():
                errors.append(
                    f"Windows bootstrap installer startup-smoke progress log is missing: {progress_log_path.name}"
                )
            else:
                progress_text = load_text(progress_log_path)
                required_markers = (
                    "Bootstrap temp root:",
                    "Payload download target:",
                    "Downloading application files",
                    "Verifying payload size",
                    "Verifying payload checksum",
                    "Extracting application files",
                    "Install complete",
                )
                for marker in required_markers:
                    if marker not in progress_text:
                        errors.append(
                            f"Windows bootstrap installer startup-smoke progress log is missing '{marker}': {progress_log_path.name}"
                        )
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
                payload_target = extract_prefixed_line(progress_text, "Payload download target:")
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
            if norm(receipt.get("bootstrapPayloadAcquisitionMode")) != "download":
                errors.append(
                    f"Windows bootstrap installer startup-smoke receipt did not exercise payload download mode: {receipt_path.name}"
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
            if norm(receipt.get("bootstrapPayloadAcquisitionMode")) != "download":
                errors.append(
                    f"Windows installer startup-smoke receipt exists for the current release but did not prove bootstrap payload download mode: {receipt_path.name}"
                )

    return {
        "status": "pass" if not errors else "fail",
        "errors": errors,
        "checkedArtifacts": results,
        "releaseVersion": release_version,
        "releaseChannel": release_channel,
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
    parser.add_argument("--output", type=Path, default=None)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    payload = verify(
        release_channel_manifest=args.release_channel,
        startup_smoke_dir=args.startup_smoke_dir,
        files_dir=args.files_dir,
        downloads_manifest=args.downloads_manifest,
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
