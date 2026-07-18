#!/usr/bin/env python3
"""Fail-closed contracts for native-Windows capture and human finalization.

This module deliberately has no network, release, or publication code.  The
preflight command validates every candidate byte binding before executable use.
The capture command repeats that validation, validates evidence already produced
on a native Windows runner, and inventories it.  The finalize command revalidates
that immutable capture, authenticates an independent reviewer, and emits the
visual-proof JSON consumed by the preview-nightly stage contract.
"""

from __future__ import annotations

import argparse
import binascii
import hashlib
import json
import re
import shutil
import struct
import sys
import zlib
from datetime import UTC, datetime
from pathlib import Path
from typing import Any


CAPTURE_CONTRACT = "chummer6-ui.preview-nightly-native-windows-capture"
CAPTURE_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-native-windows-capture-inventory"
FINALIZATION_CONTRACT = "chummer6-ui.preview-nightly-native-windows-finalization"
FINALIZED_INVENTORY_CONTRACT = "chummer6-ui.preview-nightly-native-windows-finalized-inventory"
VISUAL_PROOF_CONTRACT = "chummer6-ui.windows_installer_visual_proof"
NATIVE_HOST_CONTRACT = "chummer6-ui.native_windows_host_evidence"
CAPTURE_FILE = "WINDOWS_NATIVE_CAPTURE.generated.json"
CAPTURE_INVENTORY_FILE = "WINDOWS_NATIVE_CAPTURE_INVENTORY.generated.json"
FINALIZATION_FILE = "WINDOWS_NATIVE_EVIDENCE_FINALIZATION.generated.json"
FINALIZED_INVENTORY_FILE = "WINDOWS_NATIVE_FINALIZED_INVENTORY.generated.json"
CAPTURE_WORKFLOW = ".github/workflows/windows-native-evidence-capture.yml"
FINALIZE_WORKFLOW = ".github/workflows/windows-native-evidence-finalize.yml"
HEADS = ("avalonia", "blazor-desktop")
RID = "win-x64"
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
PORTABLE_RE = re.compile(r"^[A-Za-z0-9.][A-Za-z0-9._/@+-]{0,255}$")
REVIEWER_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,38})$")
FULL_REF_RE = re.compile(r"^refs/(?:heads|tags)/[A-Za-z0-9.][A-Za-z0-9._/@+-]{0,238}$")
PASSING = {"pass", "passed", "ready"}
PROGRESS_MARKERS = (
    "Bootstrap temp root:",
    "Payload download target:",
    "Downloading application files",
    "Verifying payload size",
    "Verifying payload checksum",
    "Extracting application files",
    "Install complete",
)
PROGRESS_FAILURE_MARKERS = (
    "Payload download failed:",
    "Bundled curl download failed",
    "bundled curl download timed out",
    "bundled curl downloader did not start",
    "bundled curl completed without creating the payload file",
    "Chummer could not download the application files.",
)


class ContractError(RuntimeError):
    pass


def fail(message: str) -> None:
    raise ContractError(message)


def norm(value: object) -> str:
    return str(value or "").strip().lower()


def require_portable(value: str, label: str) -> str:
    value = str(value or "").strip()
    if not PORTABLE_RE.fullmatch(value):
        fail(f"{label} is missing or is not a portable identifier")
    return value


def require_sha256(value: str, label: str) -> str:
    value = norm(value).removeprefix("sha256:")
    if not SHA256_RE.fullmatch(value):
        fail(f"{label} must be an exact lowercase SHA-256")
    return value


def require_commit(value: str, label: str) -> str:
    value = str(value or "")
    if not COMMIT_RE.fullmatch(value):
        fail(f"{label} must be an exact 40-character commit SHA")
    return value


def require_full_ref(value: str, label: str) -> str:
    value = str(value or "")
    components = value.split("/")[2:]
    if (
        not FULL_REF_RE.fullmatch(value)
        or not components
        or "//" in value
        or ".." in value
        or "@{" in value
        or value.endswith(("/", ".", ".lock"))
        or any(component.startswith(".") for component in components)
        or any(component.lower().endswith(".lock") for component in components)
    ):
        fail(f"{label} must be an exact full refs/heads/... or refs/tags/... ref")
    return value


def read_json(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid JSON object at {path}: {exc}")
    if not isinstance(payload, dict):
        fail(f"expected a JSON object at {path}")
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_file(root: Path, relative: str, label: str) -> Path:
    if not relative or Path(relative).is_absolute():
        fail(f"{label} must be an evidence-root-relative path")
    root = root.resolve()
    unresolved = root / relative
    if unresolved.is_symlink():
        fail(f"{label} cannot be a symlink")
    candidate = unresolved.resolve()
    try:
        candidate.relative_to(root)
    except ValueError:
        fail(f"{label} escapes its evidence root")
    if not candidate.is_file():
        fail(f"{label} is missing or is not a regular file: {relative}")
    return candidate


def validate_png(path: Path, label: str) -> tuple[int, int]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        fail(f"{label} is not a PNG")
    offset = 8
    ihdr: tuple[int, int, int, int, int] | None = None
    compressed = bytearray()
    saw_iend = False
    while offset < len(data):
        if offset + 12 > len(data):
            fail(f"{label} has a truncated PNG chunk")
        length = struct.unpack(">I", data[offset : offset + 4])[0]
        chunk_type = data[offset + 4 : offset + 8]
        end = offset + 12 + length
        if length > 64 * 1024 * 1024 or end > len(data):
            fail(f"{label} has an invalid PNG chunk length")
        chunk_data = data[offset + 8 : offset + 8 + length]
        expected_crc = struct.unpack(">I", data[offset + 8 + length : end])[0]
        actual_crc = binascii.crc32(chunk_type + chunk_data) & 0xFFFFFFFF
        if actual_crc != expected_crc:
            fail(f"{label} has a corrupt PNG chunk")
        if offset == 8 and chunk_type != b"IHDR":
            fail(f"{label} does not begin with IHDR")
        if chunk_type == b"IHDR":
            if ihdr is not None or length != 13:
                fail(f"{label} has an invalid IHDR")
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(
                ">IIBBBBB", chunk_data
            )
            if not (320 <= width <= 16384 and 200 <= height <= 16384):
                fail(f"{label} dimensions are outside 320x200..16384x16384")
            if compression != 0 or filtering != 0 or interlace != 0:
                fail(f"{label} uses unsupported PNG encoding")
            allowed_depths = {0: {1, 2, 4, 8, 16}, 2: {8, 16}, 3: {1, 2, 4, 8}, 4: {8, 16}, 6: {8, 16}}
            if bit_depth not in allowed_depths.get(color_type, set()):
                fail(f"{label} uses an invalid PNG color/depth combination")
            ihdr = (width, height, bit_depth, color_type, interlace)
        elif chunk_type == b"IDAT":
            if ihdr is None or saw_iend:
                fail(f"{label} has an out-of-order IDAT")
            compressed.extend(chunk_data)
            if len(compressed) > 64 * 1024 * 1024:
                fail(f"{label} compressed pixels exceed the evidence limit")
        elif chunk_type == b"IEND":
            if length != 0 or saw_iend:
                fail(f"{label} has an invalid IEND")
            saw_iend = True
            if end != len(data):
                fail(f"{label} has trailing bytes after IEND")
        offset = end
    if ihdr is None or not compressed or not saw_iend:
        fail(f"{label} is missing required PNG chunks")
    width, height, bit_depth, color_type, _ = ihdr
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[color_type]
    row_bytes = (width * channels * bit_depth + 7) // 8
    expected_pixel_bytes = height * (row_bytes + 1)
    try:
        decoder = zlib.decompressobj()
        pixels = decoder.decompress(bytes(compressed), expected_pixel_bytes + 1)
        if decoder.unconsumed_tail or len(pixels) > expected_pixel_bytes:
            fail(f"{label} expands beyond its declared PNG dimensions")
        if not decoder.eof:
            remaining = expected_pixel_bytes + 1 - len(pixels)
            pixels += decoder.flush(remaining)
    except zlib.error as exc:
        fail(f"{label} has invalid compressed pixels: {exc}")
    if not decoder.eof or decoder.unused_data or len(pixels) != expected_pixel_bytes:
        fail(f"{label} has an invalid decompressed pixel length")
    if any(pixels[row * (row_bytes + 1)] > 4 for row in range(height)):
        fail(f"{label} contains an invalid PNG row filter")
    return width, height


def manifest_installer_row(manifest: dict[str, Any], head: str) -> dict[str, Any]:
    rows = manifest.get("artifacts")
    if not isinstance(rows, list):
        fail("candidate manifest artifacts must be a list")
    matches = [
        row
        for row in rows
        if isinstance(row, dict)
        and norm(row.get("head") or row.get("headId")) == head
        and norm(row.get("platform")) == "windows"
        and norm(row.get("rid")) == RID
        and norm(row.get("kind")) == "installer"
    ]
    if len(matches) != 1:
        fail(f"candidate manifest must contain exactly one {head}/{RID} Windows installer")
    return matches[0]


def validate_receipt(
    receipt: dict[str, Any], *, head: str, version: str, channel: str, installer: dict[str, Any], payload: dict[str, Any]
) -> None:
    if norm(receipt.get("status")) not in PASSING:
        fail(f"{head} startup receipt is not passing")
    expected = {
        "headId": head,
        "platform": "windows",
        "rid": RID,
        "channelId": channel,
        "releaseVersion": version,
        "artifactFileName": installer["fileName"],
        "artifactDigest": f"sha256:{installer['sha256']}",
        "bootstrapPayloadAcquisitionMode": "download",
        "bootstrapPayloadFileName": payload["fileName"],
        "bootstrapPayloadSha256": payload["sha256"],
    }
    for key, value in expected.items():
        if norm(receipt.get(key)) != norm(value):
            fail(f"{head} startup receipt {key} does not match the exact capture binding")
    try:
        payload_size = int(receipt.get("bootstrapPayloadSizeBytes"))
    except (TypeError, ValueError):
        fail(f"{head} startup receipt bootstrapPayloadSizeBytes is invalid")
    if payload_size != payload["sizeBytes"]:
        fail(f"{head} startup receipt bootstrapPayloadSizeBytes mismatch")
    if norm(receipt.get("readyCheckpoint")) != "pre_ui_event_loop":
        fail(f"{head} startup receipt did not reach pre_ui_event_loop")
    if norm(receipt.get("executionEnvironment")) != "native_windows":
        fail(f"{head} startup receipt is not native Windows evidence")
    native = receipt.get("nativeHostEvidence")
    if not isinstance(native, dict):
        fail(f"{head} startup receipt nativeHostEvidence is missing")
    if str(native.get("contractName") or "").strip() != NATIVE_HOST_CONTRACT:
        fail(f"{head} startup receipt native host contract is invalid")
    if norm(native.get("status")) != "verified" or native.get("isNativeWindows") is not True:
        fail(f"{head} startup receipt native host evidence is not verified")
    if norm(native.get("hostPlatform")) != "windows":
        fail(f"{head} startup receipt hostPlatform is not Windows")
    for key in ("hostKernel", "runner", "evidenceSource"):
        if not str(native.get(key) or "").strip():
            fail(f"{head} startup receipt nativeHostEvidence.{key} is missing")
    if "wine" in norm(native.get("runner")):
        fail(f"{head} startup receipt cannot classify Wine as native Windows")


def validate_progress(path: Path, head: str) -> None:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    for marker in PROGRESS_MARKERS:
        if marker not in text:
            fail(f"{head} progress log is missing marker: {marker}")
    for marker in PROGRESS_FAILURE_MARKERS:
        if marker.lower() in text.lower():
            fail(f"{head} progress log contains failure marker: {marker}")


def head_paths(head: str) -> dict[str, str]:
    return {
        "receipt": f"startup-smoke/startup-smoke-{head}-{RID}.receipt.json",
        "progressLog": f"startup-smoke/windows-installer-progress-{head}-{RID}.log",
        "progressScreenshot": f"screenshots/windows-installer-{head}-{RID}-progress.png",
        "completionScreenshot": f"screenshots/windows-installer-{head}-{RID}-completion.png",
    }


def validate_evidence_head(
    evidence_root: Path,
    *,
    head: str,
    version: str,
    channel: str,
    installer: dict[str, Any],
    payload: dict[str, Any],
) -> dict[str, Any]:
    paths = head_paths(head)
    receipt_path = safe_file(evidence_root, paths["receipt"], f"{head} startup receipt")
    progress_path = safe_file(evidence_root, paths["progressLog"], f"{head} progress log")
    progress_png = safe_file(evidence_root, paths["progressScreenshot"], f"{head} progress screenshot")
    completion_png = safe_file(evidence_root, paths["completionScreenshot"], f"{head} completion screenshot")
    validate_receipt(
        read_json(receipt_path), head=head, version=version, channel=channel, installer=installer, payload=payload
    )
    validate_progress(progress_path, head)
    progress_size = validate_png(progress_png, f"{head} progress screenshot")
    completion_size = validate_png(completion_png, f"{head} completion screenshot")
    screenshot_digests = (sha256_file(progress_png), sha256_file(completion_png))
    if screenshot_digests[0] == screenshot_digests[1]:
        fail(f"{head} progress and completion screenshots are digest-identical")
    return {
        "headId": head,
        "rid": RID,
        "installer": installer,
        "payload": payload,
        "receipt": {"path": paths["receipt"], "sha256": sha256_file(receipt_path)},
        "progressLog": {"path": paths["progressLog"], "sha256": sha256_file(progress_path)},
        "screenshots": [
            {
                "role": "progress",
                "path": paths["progressScreenshot"],
                "sha256": screenshot_digests[0],
                "width": progress_size[0],
                "height": progress_size[1],
            },
            {
                "role": "completion",
                "path": paths["completionScreenshot"],
                "sha256": screenshot_digests[1],
                "width": completion_size[0],
                "height": completion_size[1],
            },
        ],
    }


def exact_inventory(root: Path, *, exclude: set[str]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for path in sorted(root.rglob("*")):
        if path.is_symlink():
            fail(f"capture evidence cannot contain symlinks: {path}")
        if not path.is_file():
            continue
        relative = path.relative_to(root).as_posix()
        if relative in exclude:
            continue
        rows.append({"path": relative, "sha256": sha256_file(path), "sizeBytes": path.stat().st_size})
    return rows


def parse_allowlist(raw: str) -> list[str]:
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as exc:
        fail(f"reviewer allowlist must be a JSON array: {exc}")
    if not isinstance(parsed, list) or not parsed:
        fail("reviewer allowlist must be a non-empty JSON array")
    values: list[str] = []
    for value in parsed:
        reviewer = str(value or "").strip()
        if not REVIEWER_RE.fullmatch(reviewer):
            fail("reviewer allowlist contains an invalid GitHub login")
        if reviewer.lower() in {item.lower() for item in values}:
            fail("reviewer allowlist contains a duplicate GitHub login")
        values.append(reviewer)
    return values


def head_binding(args: argparse.Namespace, head: str, manifest: dict[str, Any], candidate_root: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    prefix = head.replace("-", "_")
    installer_rel = getattr(args, f"{prefix}_installer")
    payload_rel = getattr(args, f"{prefix}_payload")
    installer_path = safe_file(candidate_root, installer_rel, f"{head} installer")
    payload_path = safe_file(candidate_root, payload_rel, f"{head} payload")
    installer_sha = require_sha256(getattr(args, f"{prefix}_installer_sha256"), f"{head} installer SHA-256")
    payload_sha = require_sha256(getattr(args, f"{prefix}_payload_sha256"), f"{head} payload SHA-256")
    if sha256_file(installer_path) != installer_sha:
        fail(f"{head} installer bytes do not match the dispatched SHA-256")
    if sha256_file(payload_path) != payload_sha:
        fail(f"{head} payload bytes do not match the dispatched SHA-256")
    row = manifest_installer_row(manifest, head)
    expected_row = {
        "fileName": installer_path.name,
        "sha256": installer_sha,
        "installerMode": "bootstrap",
        "payloadAcquisitionMode": "download",
        "payloadFileName": payload_path.name,
        "payloadSha256": payload_sha,
    }
    for key, value in expected_row.items():
        if norm(row.get(key)) != norm(value):
            fail(f"candidate manifest {head} {key} does not match the dispatched bytes")
    if int(row.get("sizeBytes") or -1) != installer_path.stat().st_size:
        fail(f"candidate manifest {head} installer size mismatch")
    if int(row.get("payloadSizeBytes") or -1) != payload_path.stat().st_size:
        fail(f"candidate manifest {head} payload size mismatch")
    installer = {
        "relativePath": installer_rel,
        "fileName": installer_path.name,
        "sha256": installer_sha,
        "sizeBytes": installer_path.stat().st_size,
    }
    payload = {
        "relativePath": payload_rel,
        "fileName": payload_path.name,
        "sha256": payload_sha,
        "sizeBytes": payload_path.stat().st_size,
    }
    return installer, payload


def validate_candidate_bindings(
    args: argparse.Namespace,
) -> tuple[Path, str, str, str, dict[str, tuple[dict[str, Any], dict[str, Any]]]]:
    candidate_root = args.candidate_root.resolve()
    if not candidate_root.is_dir():
        fail("candidate-root must already exist")
    version = require_portable(args.version, "version")
    channel = require_portable(args.channel, "channel")
    if norm(channel) not in {"preview", "nightly"}:
        fail("native evidence capture is restricted to preview/nightly channels")
    manifest_path = safe_file(candidate_root, args.candidate_manifest, "candidate manifest")
    manifest_sha = require_sha256(args.candidate_manifest_sha256, "candidate manifest SHA-256")
    if sha256_file(manifest_path) != manifest_sha:
        fail("candidate manifest bytes do not match the dispatched SHA-256")
    manifest = read_json(manifest_path)
    if str(manifest.get("version") or "").strip() != version:
        fail("candidate manifest version does not match the dispatched version")
    if norm(manifest.get("channelId") or manifest.get("channel")) != norm(channel):
        fail("candidate manifest channel does not match the dispatched channel")
    bindings: dict[str, tuple[dict[str, Any], dict[str, Any]]] = {
        head: head_binding(args, head, manifest, candidate_root) for head in HEADS
    }
    if len({binding[0]["sha256"] for binding in bindings.values()}) != len(HEADS):
        fail("the two Windows heads cannot bind digest-identical installers")
    return candidate_root, version, channel, manifest_sha, bindings


def preflight(args: argparse.Namespace) -> None:
    _, _, _, manifest_sha, bindings = validate_candidate_bindings(args)
    print(f"candidate_manifest_sha256={manifest_sha}")
    for head in HEADS:
        print(f"{head}_installer_sha256={bindings[head][0]['sha256']}")
        print(f"{head}_payload_sha256={bindings[head][1]['sha256']}")


def capture(args: argparse.Namespace) -> None:
    candidate_root, version, channel, manifest_sha, bindings = validate_candidate_bindings(args)
    evidence_root = args.evidence_root.resolve()
    if not evidence_root.is_dir():
        fail("evidence-root must already exist")
    source = {
        "repository": require_portable(args.source_repository, "capture source repository"),
        "workflow": require_portable(args.source_workflow, "capture source workflow"),
        "runId": require_portable(args.source_run_id, "capture source run ID"),
        "runAttempt": require_portable(args.source_run_attempt, "capture source run attempt"),
        "ref": require_full_ref(args.source_ref, "capture source ref"),
        "sha": require_commit(args.source_sha, "capture source SHA"),
        "actor": require_portable(args.source_actor, "capture source actor"),
        "artifactName": require_portable(args.output_artifact_name, "capture artifact name"),
    }
    if source["workflow"] != CAPTURE_WORKFLOW:
        fail(f"capture source workflow must be {CAPTURE_WORKFLOW}")
    if source["artifactName"] != f"windows-native-evidence-{source['runId']}-{source['runAttempt']}":
        fail("capture artifact name is not exactly bound to its run ID and attempt")
    candidate = {
        "repository": require_portable(args.candidate_repository, "candidate repository"),
        "workflow": require_portable(args.candidate_workflow, "candidate workflow"),
        "runId": require_portable(args.candidate_run_id, "candidate run ID"),
        "ref": require_full_ref(args.candidate_ref, "candidate ref"),
        "sha": require_commit(args.candidate_sha, "candidate SHA"),
        "artifactName": require_portable(args.candidate_artifact_name, "candidate artifact name"),
        "manifestPath": args.candidate_manifest,
        "manifestSha256": manifest_sha,
    }
    heads = [
        validate_evidence_head(
            evidence_root,
            head=head,
            version=version,
            channel=channel,
            installer=bindings[head][0],
            payload=bindings[head][1],
        )
        for head in HEADS
    ]
    all_screenshot_digests = [shot["sha256"] for row in heads for shot in row["screenshots"]]
    if len(set(all_screenshot_digests)) != len(all_screenshot_digests):
        fail("all per-head progress/completion screenshots must be distinct captures")
    capture_payload = {
        "contractName": CAPTURE_CONTRACT,
        "contractVersion": 1,
        "status": "captured",
        "captureMode": "interactive",
        "generatedAt": datetime.now(UTC).isoformat().replace("+00:00", "Z"),
        "version": version,
        "channelId": channel,
        "source": source,
        "candidate": candidate,
        "heads": heads,
    }
    write_json(evidence_root / CAPTURE_FILE, capture_payload)
    rows = exact_inventory(evidence_root, exclude={CAPTURE_INVENTORY_FILE})
    inventory = {
        "contractName": CAPTURE_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "captureContract": CAPTURE_CONTRACT,
        "captureManifestSha256": sha256_file(evidence_root / CAPTURE_FILE),
        "files": rows,
    }
    write_json(evidence_root / CAPTURE_INVENTORY_FILE, inventory)
    print(f"capture_inventory_sha256={sha256_file(evidence_root / CAPTURE_INVENTORY_FILE)}")


def verify_inventory(capture_root: Path, expected_sha: str) -> dict[str, Any]:
    inventory_path = safe_file(capture_root, CAPTURE_INVENTORY_FILE, "capture inventory")
    if sha256_file(inventory_path) != require_sha256(expected_sha, "capture inventory SHA-256"):
        fail("capture inventory bytes do not match the independently supplied SHA-256")
    inventory = read_json(inventory_path)
    if inventory.get("contractName") != CAPTURE_INVENTORY_CONTRACT or inventory.get("contractVersion") != 1:
        fail("capture inventory contract is invalid")
    rows = inventory.get("files")
    if not isinstance(rows, list) or not rows:
        fail("capture inventory files must be a non-empty list")
    actual = exact_inventory(capture_root, exclude={CAPTURE_INVENTORY_FILE})
    if rows != actual:
        fail("capture artifact inventory does not exactly match its files")
    capture_path = safe_file(capture_root, CAPTURE_FILE, "capture manifest")
    if norm(inventory.get("captureManifestSha256")) != sha256_file(capture_path):
        fail("capture manifest digest does not match the capture inventory")
    return inventory


def require_confirmation(value: str, label: str) -> None:
    if norm(value) != "true":
        fail(f"explicit {label} confirmation is required")


def finalize(args: argparse.Namespace) -> None:
    capture_root = args.capture_root.resolve()
    output_root = args.output_root.resolve()
    if not capture_root.is_dir():
        fail("capture-root must exist")
    if output_root.exists():
        fail("output-root must not already exist")
    inventory_sha = require_sha256(args.capture_inventory_sha256, "capture inventory SHA-256")
    verify_inventory(capture_root, inventory_sha)
    capture_payload = read_json(safe_file(capture_root, CAPTURE_FILE, "capture manifest"))
    if capture_payload.get("contractName") != CAPTURE_CONTRACT or capture_payload.get("contractVersion") != 1:
        fail("capture manifest contract is invalid")
    if norm(capture_payload.get("status")) != "captured" or norm(capture_payload.get("captureMode")) != "interactive":
        fail("capture manifest is not an interactive machine capture")
    source = capture_payload.get("source")
    if not isinstance(source, dict):
        fail("capture manifest source binding is missing")
    expected_source = {
        "repository": args.expected_repository,
        "workflow": args.expected_workflow,
        "runId": args.expected_run_id,
        "runAttempt": args.expected_run_attempt,
        "ref": require_full_ref(args.expected_ref, "expected capture ref"),
        "sha": require_commit(args.expected_sha, "expected capture SHA"),
        "actor": args.expected_capture_actor,
        "artifactName": args.expected_artifact_name,
    }
    for key, value in expected_source.items():
        if str(source.get(key) or "").strip() != str(value or "").strip():
            fail(f"capture source {key} does not match authenticated workflow-run metadata")
    reviewer = str(args.reviewer_id or "").strip()
    if not REVIEWER_RE.fullmatch(reviewer):
        fail("authenticated reviewer is not a valid GitHub login")
    allowlist = parse_allowlist(args.reviewer_allowlist_json)
    if reviewer.lower() not in {value.lower() for value in allowlist}:
        fail("authenticated reviewer is not in the pinned reviewer allowlist")
    if reviewer.lower() == str(source.get("actor") or "").strip().lower():
        fail("capture actor cannot review or finalize their own capture")
    require_confirmation(args.human_review_confirmed, "human review")
    finalization_source = {
        "repository": require_portable(args.finalization_repository, "finalization repository"),
        "workflow": require_portable(args.finalization_workflow, "finalization workflow"),
        "runId": require_portable(args.finalization_run_id, "finalization run ID"),
        "runAttempt": require_portable(args.finalization_run_attempt, "finalization run attempt"),
        "ref": require_full_ref(args.finalization_ref, "finalization ref"),
        "sha": require_commit(args.finalization_sha, "finalization SHA"),
        "actor": require_portable(args.finalization_actor, "finalization actor"),
        "artifactName": require_portable(args.finalization_artifact_name, "finalization artifact name"),
    }
    if finalization_source["workflow"] != FINALIZE_WORKFLOW:
        fail(f"finalization workflow must be {FINALIZE_WORKFLOW}")
    if finalization_source["artifactName"] != (
        f"windows-native-evidence-finalized-{finalization_source['runId']}-{finalization_source['runAttempt']}"
    ):
        fail("finalization artifact name is not exactly bound to its run ID and attempt")
    if finalization_source["actor"].lower() != reviewer.lower():
        fail("finalization actor must be the authenticated reviewer")
    if finalization_source["repository"] != source["repository"]:
        fail("capture and finalization repositories must match")
    if finalization_source["sha"] != source["sha"]:
        fail("capture and finalization workflow SHAs must match")
    confirmations: dict[str, dict[str, str]] = {}
    for head in HEADS:
        prefix = head.replace("-", "_")
        confirmations[head] = {}
        for check in ("readability", "contrast", "clipping"):
            value = getattr(args, f"{prefix}_{check}")
            require_confirmation(value, f"{head} {check}")
            confirmations[head][check] = "passed"
    version = require_portable(capture_payload.get("version"), "capture version")
    channel = require_portable(capture_payload.get("channelId"), "capture channel")
    rows = capture_payload.get("heads")
    if not isinstance(rows, list) or [norm(row.get("headId")) for row in rows if isinstance(row, dict)] != list(HEADS):
        fail("capture manifest must contain the two exact Windows heads in canonical order")
    validated: list[dict[str, Any]] = []
    for row, head in zip(rows, HEADS, strict=True):
        installer = row.get("installer")
        payload = row.get("payload")
        if not isinstance(installer, dict) or not isinstance(payload, dict):
            fail(f"capture manifest {head} byte binding is invalid")
        installer["sha256"] = require_sha256(installer.get("sha256"), f"{head} installer SHA-256")
        payload["sha256"] = require_sha256(payload.get("sha256"), f"{head} payload SHA-256")
        validated_row = validate_evidence_head(
            capture_root,
            head=head,
            version=version,
            channel=channel,
            installer=installer,
            payload=payload,
        )
        if row != validated_row:
            fail(f"capture manifest {head} evidence metadata does not match the captured files")
        validated.append(validated_row)
    all_digests = [shot["sha256"] for row in validated for shot in row["screenshots"]]
    if len(set(all_digests)) != len(all_digests):
        fail("capture contains reused or digest-identical screenshots")
    shutil.copytree(capture_root, output_root, symlinks=False)
    generated_at = datetime.now(UTC).isoformat().replace("+00:00", "Z")
    proof_rows: list[dict[str, str]] = []
    for row in validated:
        head = row["headId"]
        screenshots = [
            {"role": shot["role"], "path": shot["path"], "sha256": shot["sha256"]}
            for shot in row["screenshots"]
        ]
        proof = {
            "contractName": VISUAL_PROOF_CONTRACT,
            "contractVersion": 1,
            "status": "passed",
            "generatedAt": generated_at,
            "version": version,
            "releaseVersion": version,
            "channel": channel,
            "channelId": channel,
            "platform": "windows",
            "head": head,
            "headId": head,
            "rid": RID,
            "artifactFileName": row["installer"]["fileName"],
            "artifactDigest": f"sha256:{row['installer']['sha256']}",
            "screenshots": screenshots,
            "checks": {"capture_mode": "interactive", "human_review_confirmed": True},
            "readabilityReview": {"status": "passed", "reviewer": reviewer},
            "contrastReview": {"status": "passed", "reviewer": reviewer},
            "clippingReview": {"status": "passed", "reviewer": reviewer},
            "review": {
                "authenticatedReviewer": reviewer,
                "captureActor": source["actor"],
                "allowlistSource": "repository variable plus protected environment",
                "explicitConfirmations": confirmations[head],
            },
            "finalizationBinding": finalization_source,
            "captureBinding": {
                "repository": source["repository"],
                "workflow": source["workflow"],
                "runId": source["runId"],
                "runAttempt": source["runAttempt"],
                "ref": source["ref"],
                "sha": source["sha"],
                "artifactName": source["artifactName"],
                "inventorySha256": inventory_sha,
            },
        }
        proof_name = f"WINDOWS_INSTALLER_VISUAL_PROOF-{head}-{RID}.generated.json"
        proof_path = output_root / proof_name
        write_json(proof_path, proof)
        proof_rows.append({"headId": head, "path": proof_name, "sha256": sha256_file(proof_path)})
    finalization = {
        "contractName": FINALIZATION_CONTRACT,
        "contractVersion": 1,
        "status": "passed",
        "generatedAt": generated_at,
        "captureInventorySha256": inventory_sha,
        "captureSource": source,
        "finalizationSource": finalization_source,
        "reviewer": reviewer,
        "reviewerWasCaptureActor": False,
        "humanReviewConfirmed": True,
        "proofs": proof_rows,
    }
    write_json(output_root / FINALIZATION_FILE, finalization)
    finalized_inventory = {
        "contractName": FINALIZED_INVENTORY_CONTRACT,
        "contractVersion": 1,
        "captureInventorySha256": inventory_sha,
        "files": exact_inventory(output_root, exclude={FINALIZED_INVENTORY_FILE}),
    }
    write_json(output_root / FINALIZED_INVENTORY_FILE, finalized_inventory)
    print(f"finalized_evidence_root={output_root}")
    print(f"finalized_inventory_sha256={sha256_file(output_root / FINALIZED_INVENTORY_FILE)}")


def add_binding_args(parser: argparse.ArgumentParser, head: str) -> None:
    prefix = head.replace("-", "_")
    parser.add_argument(f"--{head}-installer", dest=f"{prefix}_installer", required=True)
    parser.add_argument(f"--{head}-installer-sha256", dest=f"{prefix}_installer_sha256", required=True)
    parser.add_argument(f"--{head}-payload", dest=f"{prefix}_payload", required=True)
    parser.add_argument(f"--{head}-payload-sha256", dest=f"{prefix}_payload_sha256", required=True)


def add_candidate_args(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--candidate-root", required=True, type=Path)
    parser.add_argument("--candidate-manifest", required=True)
    parser.add_argument("--candidate-manifest-sha256", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--channel", required=True)
    for head in HEADS:
        add_binding_args(parser, head)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    preflight_parser = subparsers.add_parser(
        "preflight", help="validate exact candidate bytes before any installer executes"
    )
    add_candidate_args(preflight_parser)
    preflight_parser.set_defaults(handler=preflight)

    capture_parser = subparsers.add_parser("capture", help="validate and inventory native machine evidence")
    add_candidate_args(capture_parser)
    capture_parser.add_argument("--evidence-root", required=True, type=Path)
    for name in (
        "source-repository", "source-workflow", "source-run-id", "source-run-attempt", "source-ref",
        "source-sha", "source-actor", "output-artifact-name", "candidate-repository", "candidate-workflow",
        "candidate-run-id", "candidate-ref", "candidate-sha", "candidate-artifact-name",
    ):
        capture_parser.add_argument(f"--{name}", required=True)
    capture_parser.set_defaults(handler=capture)

    finalize_parser = subparsers.add_parser("finalize", help="independently review and materialize visual proofs")
    finalize_parser.add_argument("--capture-root", required=True, type=Path)
    finalize_parser.add_argument("--output-root", required=True, type=Path)
    finalize_parser.add_argument("--capture-inventory-sha256", required=True)
    finalize_parser.add_argument("--human-review-confirmed", required=True)
    for name in (
        "expected-repository", "expected-workflow", "expected-run-id", "expected-run-attempt", "expected-ref",
        "expected-sha", "expected-capture-actor", "expected-artifact-name", "reviewer-id",
        "reviewer-allowlist-json",
    ):
        finalize_parser.add_argument(f"--{name}", required=True)
    for name in (
        "finalization-repository", "finalization-workflow", "finalization-run-id", "finalization-run-attempt",
        "finalization-ref", "finalization-sha", "finalization-actor", "finalization-artifact-name",
    ):
        finalize_parser.add_argument(f"--{name}", required=True)
    for head in HEADS:
        prefix = head.replace("-", "_")
        for check in ("readability", "contrast", "clipping"):
            finalize_parser.add_argument(f"--{head}-{check}", dest=f"{prefix}_{check}", required=True)
    finalize_parser.set_defaults(handler=finalize)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        args.handler(args)
    except ContractError as exc:
        print(f"windows-native-evidence:error: {exc}", file=sys.stderr)
        return 1
    print(f"windows-native-evidence:{args.command}:ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
