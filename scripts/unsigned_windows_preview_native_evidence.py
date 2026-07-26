#!/usr/bin/env python3
"""Fail-closed native Windows evidence for an exact unsigned preview export.

This module has no publication, upload, deployment, signing, or dispatch code.
It authenticates an already-exported unsigned Windows preview candidate, binds
native startup and visual evidence to those exact bytes, and finalizes only an
accountable review receipt.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import importlib.util
import json
import os
import re
import shutil
import stat
import sys
from datetime import UTC, datetime
from pathlib import Path, PurePosixPath
from types import ModuleType
from typing import Any


def _load_module(name: str, file_name: str) -> ModuleType:
    existing = sys.modules.get(name)
    if existing is not None:
        if not isinstance(existing, ModuleType):
            raise RuntimeError(f"preloaded {name} module is malformed")
        return existing
    path = Path(__file__).resolve().with_name(file_name)
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"could not load {file_name}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


EXPORT = _load_module(
    "chummer6_ui_unsigned_preview_native_candidate_export",
    "preview_nightly_unsigned_candidate_export.py",
)
WINDOWS = _load_module(
    "chummer6_ui_unsigned_preview_native_windows_contract",
    "windows_native_evidence.py",
)

SOURCE_REPOSITORY = "ArchonMegalon/chummer6-ui"
SOURCE_REF = "refs/heads/main"
PRODUCER_WORKFLOW = (
    ".github/workflows/unsigned-windows-preview-nightly-candidate-export.yml"
)
CAPTURE_WORKFLOW = (
    ".github/workflows/unsigned-windows-preview-native-evidence-capture.yml"
)
FINALIZE_WORKFLOW = (
    ".github/workflows/unsigned-windows-preview-native-evidence-finalize.yml"
)
CAPTURE_ARTIFACT_PREFIX = "unsigned-windows-preview-native-evidence"
FINALIZED_ARTIFACT_PREFIX = "unsigned-windows-preview-native-evidence-finalized"
REVIEWER_ID = "ArchonMegalon"
REVIEWER_KIND = "authenticated_account_owner_delegated_operator"
SIGNING_REQUIREMENT = "preview_unsigned_allowed"
CAPTURE_CONTRACT = "chummer6-ui.unsigned-preview-native-windows-capture"
CAPTURE_INVENTORY_CONTRACT = (
    "chummer6-ui.unsigned-preview-native-windows-capture-inventory"
)
FINALIZATION_CONTRACT = (
    "chummer6-ui.unsigned-preview-native-windows-finalization"
)
FINALIZED_INVENTORY_CONTRACT = (
    "chummer6-ui.unsigned-preview-native-windows-finalized-inventory"
)
STARTUP_VISUAL_CONTRACT = (
    "chummer6-ui.unsigned-preview-windows-startup-visual"
)
AUTHENTICODE_CONTRACT = (
    "chummer6-ui.unsigned-preview-windows-authenticode-verification"
)
VISUAL_PROOF_CONTRACT = (
    "chummer6-ui.unsigned-preview-windows-installer-visual-proof"
)
CAPTURE_FILE = "UNSIGNED_WINDOWS_PREVIEW_NATIVE_CAPTURE.generated.json"
CAPTURE_INVENTORY_FILE = (
    "UNSIGNED_WINDOWS_PREVIEW_NATIVE_CAPTURE_INVENTORY.generated.json"
)
FINALIZATION_FILE = (
    "UNSIGNED_WINDOWS_PREVIEW_NATIVE_FINALIZATION.generated.json"
)
FINALIZED_INVENTORY_FILE = (
    "UNSIGNED_WINDOWS_PREVIEW_NATIVE_FINALIZED_INVENTORY.generated.json"
)
FINALIZED_EVIDENCE_FILE = (
    "UNSIGNED_WINDOWS_PREVIEW_NATIVE_FINALIZED_EVIDENCE.generated.json"
)
VISUAL_PROOF_FILE = (
    "UNSIGNED_WINDOWS_PREVIEW_VISUAL_PROOF-avalonia-win-x64.generated.json"
)
CANDIDATE_PROVENANCE_DIRECTORY = "candidate-provenance"
AUTHENTICODE_FILE = (
    "authenticode/"
    "AUTHENTICODE_VERIFICATION-avalonia-win-x64.generated.json"
)
STARTUP_VISUAL_RECEIPT = (
    "startup-visual/windows-application-avalonia-win-x64-startup.receipt.json"
)
STARTUP_SCREENSHOT = (
    "screenshots/windows-application-avalonia-win-x64-startup.png"
)
STARTUP_LOG = "startup-smoke/startup-smoke-avalonia-win-x64.log"
PAYLOAD_HTTP_LOG = (
    "startup-smoke/startup-smoke-payload-http-avalonia-win-x64.log"
)
HEAD = "avalonia"
RID = "win-x64"
CONTRACT_VERSION = 1
RERUN_POLICY = "same-actor-only"
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
POSITIVE_INTEGER_RE = re.compile(r"^[1-9][0-9]*$")
VERSION_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
LOGIN_RE = re.compile(
    r"^(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?"
    r"|github-actions\[bot\])$"
)
EXPECTED_REVIEW_KEYS = {
    "clipping",
    "completion",
    "contrast",
    "progress",
    "readability",
    "startup",
}
AUTHENTICODE_BINDING = {
    "signatureStatus": "unsigned",
    "signingRequired": False,
    "unsignedReason": "preview_policy",
}


class EvidenceError(RuntimeError):
    """A fail-closed unsigned preview native-evidence error."""


def fail(message: str) -> None:
    raise EvidenceError(message)


def require_text(value: object, pattern: re.Pattern[str], label: str) -> str:
    if not isinstance(value, str) or pattern.fullmatch(value) is None:
        fail(f"{label} is invalid")
    return value


def require_exact(value: object, expected: object, label: str) -> None:
    if value != expected or type(value) is not type(expected):
        fail(f"{label} differs")


def require_exact_keys(
    value: object, expected: set[str], label: str
) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != expected:
        fail(f"{label} has missing or extra fields")
    return value


def require_no_authority(payload: dict[str, Any], label: str) -> None:
    expected = {
        "deployAuthorized": False,
        "publicationAuthorized": False,
        "uiUploadAuthorized": False,
        "uploadAuthorized": False,
    }
    for key, value in expected.items():
        require_exact(payload.get(key), value, f"{label} {key}")


def parse_review_json(raw: str) -> dict[str, bool]:
    try:
        payload = json.loads(raw, object_pairs_hook=reject_duplicate_keys)
    except json.JSONDecodeError as exc:
        fail(f"accountable review JSON is invalid: {exc}")
    review = require_exact_keys(
        payload, EXPECTED_REVIEW_KEYS, "accountable review JSON"
    )
    for key in sorted(EXPECTED_REVIEW_KEYS):
        if review[key] is not True:
            fail(f"accountable review confirmation {key} must be true")
    return {key: True for key in sorted(EXPECTED_REVIEW_KEYS)}


def reject_duplicate_keys(
    pairs: list[tuple[str, object]],
) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            fail(f"JSON repeats key {key!r}")
        result[key] = value
    return result


def utc_now() -> str:
    return datetime.now(UTC).isoformat(timespec="microseconds").replace(
        "+00:00", "Z"
    )


def require_accountable_reviewer(
    reviewer_id: object,
    reviewer_kind: object,
    finalization_actor: object,
    finalization_triggering_actor: object,
) -> str:
    reviewer = require_text(reviewer_id, LOGIN_RE, "accountable reviewer")
    if reviewer != REVIEWER_ID:
        fail("accountable reviewer must be the sole pinned ArchonMegalon identity")
    require_exact(
        reviewer_kind, REVIEWER_KIND, "accountable reviewer kind"
    )
    require_exact(
        finalization_actor, reviewer, "finalization actor identity"
    )
    require_exact(
        finalization_triggering_actor,
        reviewer,
        "finalization triggering actor identity",
    )
    return reviewer


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    descriptor = -1
    try:
        descriptor = os.open(
            path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        )
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_nlink != 1:
            fail(f"evidence entry is not one regular file: {path}")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        fail(f"could not hash evidence entry {path}: {exc}")
    finally:
        if descriptor >= 0:
            os.close(descriptor)
    return digest.hexdigest()


def file_size(path: Path) -> int:
    try:
        metadata = path.lstat()
    except OSError as exc:
        fail(f"could not inspect evidence entry {path}: {exc}")
    if (
        path.is_symlink()
        or not stat.S_ISREG(metadata.st_mode)
        or metadata.st_nlink != 1
        or metadata.st_size < 1
    ):
        fail(f"evidence entry is not one non-empty regular file: {path}")
    return metadata.st_size


def portable_path(value: object, label: str) -> str:
    if not isinstance(value, str):
        fail(f"{label} must be an exact string")
    parsed = PurePosixPath(value)
    if (
        parsed.is_absolute()
        or parsed.as_posix() != value
        or "\\" in value
        or any(part in {"", ".", ".."} for part in parsed.parts)
    ):
        fail(f"{label} is not a canonical portable path")
    return value


def safe_file(root_value: Path, relative_value: str, label: str) -> Path:
    relative = portable_path(relative_value, label)
    root = root_value.resolve(strict=True)
    path = root.joinpath(*PurePosixPath(relative).parts)
    try:
        resolved = path.resolve(strict=True)
    except OSError as exc:
        fail(f"{label} is unavailable: {exc}")
    if resolved.parent != root and root not in resolved.parents:
        fail(f"{label} escapes its evidence root")
    file_size(path)
    return path


def read_json(path: Path, label: str) -> dict[str, Any]:
    try:
        raw = path.read_bytes()
        if raw.startswith(b"\xef\xbb\xbf") or b"\x00" in raw:
            fail(f"{label} is not canonical UTF-8 JSON")
        payload = json.loads(
            raw.decode("utf-8", errors="strict"),
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=lambda item: fail(
                f"{label} contains non-finite number {item}"
            ),
        )
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        fail(f"{label} is invalid JSON: {exc}")
    if not isinstance(payload, dict):
        fail(f"{label} must be a JSON object")
    return payload


def write_json_new(path: Path, payload: dict[str, Any]) -> None:
    data = (
        json.dumps(payload, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    descriptor = os.open(
        path,
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | getattr(os, "O_NOFOLLOW", 0),
        0o600,
    )
    try:
        view = memoryview(data)
        while view:
            written = os.write(descriptor, view)
            if written < 1:
                fail("evidence write made no progress")
            view = view[written:]
        os.fchmod(descriptor, 0o444)
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def binding(path: Path, relative: str | None = None) -> dict[str, Any]:
    result: dict[str, Any] = {
        "sha256": sha256_file(path),
        "sizeBytes": file_size(path),
    }
    if relative is not None:
        result["path"] = portable_path(relative, "evidence binding path")
    return result


def authenticode_binding(path: Path) -> dict[str, Any]:
    return {
        **binding(path, AUTHENTICODE_FILE),
        **AUTHENTICODE_BINDING,
    }


def exact_inventory(
    root_value: Path, *, exclude: set[str] | None = None
) -> list[dict[str, Any]]:
    root = root_value.resolve(strict=True)
    excluded = exclude or set()
    rows: list[dict[str, Any]] = []
    casefolded: set[str] = set()
    for current, directories, files in os.walk(
        root, topdown=True, followlinks=False
    ):
        current_path = Path(current)
        for name in sorted([*directories, *files]):
            path = current_path / name
            relative = portable_path(
                path.relative_to(root).as_posix(), "evidence inventory path"
            )
            folded = relative.casefold()
            if folded in casefolded:
                fail(f"evidence tree repeats or case-collides at {relative}")
            casefolded.add(folded)
            metadata = path.lstat()
            if path.is_symlink():
                fail(f"evidence tree contains a symbolic link: {relative}")
            if stat.S_ISDIR(metadata.st_mode):
                continue
            if (
                not stat.S_ISREG(metadata.st_mode)
                or metadata.st_nlink != 1
            ):
                fail(f"evidence tree contains a special or hard-linked entry: {relative}")
            if relative not in excluded:
                rows.append(
                    {
                        "path": relative,
                        "sha256": sha256_file(path),
                        "sizeBytes": file_size(path),
                    }
                )
    return sorted(rows, key=lambda row: str(row["path"]))


def validate_source(
    receipt: dict[str, Any],
    *,
    expected_run_id: str,
    expected_run_attempt: str,
    expected_actor: str,
    expected_sha: str,
) -> dict[str, Any]:
    source = require_exact_keys(
        receipt.get("source"),
        {
            "actor",
            "ref",
            "repository",
            "runAttempt",
            "runId",
            "sha",
            "workflow",
        },
        "candidate export source",
    )
    expected = {
        "actor": require_text(
            expected_actor, LOGIN_RE, "candidate producer actor"
        ),
        "ref": SOURCE_REF,
        "repository": SOURCE_REPOSITORY,
        "runAttempt": require_text(
            expected_run_attempt,
            POSITIVE_INTEGER_RE,
            "candidate producer run attempt",
        ),
        "runId": require_text(
            expected_run_id,
            POSITIVE_INTEGER_RE,
            "candidate producer run ID",
        ),
        "sha": require_text(
            expected_sha, COMMIT_RE, "candidate producer SHA"
        ),
        "workflow": PRODUCER_WORKFLOW,
    }
    require_exact(source, expected, "candidate export source")
    return source


def candidate_bindings(args: argparse.Namespace) -> dict[str, Any]:
    root = args.candidate_root.resolve(strict=True)
    version = require_text(
        args.expected_version, VERSION_RE, "candidate version"
    )
    manifest_sha = require_text(
        args.expected_manifest_sha256,
        SHA256_RE,
        "candidate manifest SHA-256",
    )
    inventory_sha = require_text(
        args.expected_inventory_sha256,
        SHA256_RE,
        "candidate inventory SHA-256",
    )
    source_sha = require_text(
        args.candidate_source_sha, COMMIT_RE, "candidate source SHA"
    )
    proposal, inventory, receipt = EXPORT.validate_export_root(
        root, version, manifest_sha, source_sha
    )
    source = validate_source(
        receipt,
        expected_run_id=args.candidate_run_id,
        expected_run_attempt=args.candidate_run_attempt,
        expected_actor=args.candidate_actor,
        expected_sha=source_sha,
    )
    inventory_path = root / EXPORT.CONTENT_INVENTORY_PATH
    if sha256_file(inventory_path) != inventory_sha:
        fail("candidate inventory differs from independently supplied SHA-256")
    expected_artifact_name = (
        f"unsigned-windows-preview-nightly-candidate-"
        f"{source['runId']}-{source['runAttempt']}"
    )
    require_exact(
        args.candidate_artifact_name,
        expected_artifact_name,
        "candidate artifact name",
    )
    artifact_id = require_text(
        args.candidate_artifact_id,
        POSITIVE_INTEGER_RE,
        "candidate artifact ID",
    )
    artifact_sha = require_text(
        args.candidate_artifact_sha256,
        SHA256_RE,
        "candidate artifact transport SHA-256",
    )
    installer = root / EXPORT.INSTALLER_PATH
    payload = root / EXPORT.PAYLOAD_PATH
    manifest = root / EXPORT.MANIFEST_PATH
    export_receipt = root / EXPORT.EXPORT_RECEIPT_PATH
    composition = root / EXPORT.COMPOSITION_PATH
    signature = receipt.get("signature")
    require_exact(
        signature,
        {"policy": "preview_policy", "required": False, "status": "unsigned"},
        "candidate unsigned signature posture",
    )
    require_no_authority(receipt, "candidate export receipt")
    return {
        "artifact": {
            "id": artifact_id,
            "name": expected_artifact_name,
            "sha256": artifact_sha,
        },
        "compositionRequest": binding(
            composition, EXPORT.COMPOSITION_PATH
        ),
        "contentInventory": binding(
            inventory_path, EXPORT.CONTENT_INVENTORY_PATH
        ),
        "exportReceipt": binding(
            export_receipt, EXPORT.EXPORT_RECEIPT_PATH
        ),
        "installer": {
            "fileName": installer.name,
            "path": EXPORT.INSTALLER_PATH,
            "sha256": sha256_file(installer),
            "sizeBytes": file_size(installer),
        },
        "manifest": {
            "path": EXPORT.MANIFEST_PATH,
            "sha256": sha256_file(manifest),
            "sizeBytes": file_size(manifest),
        },
        "payload": {
            "fileName": payload.name,
            "path": EXPORT.PAYLOAD_PATH,
            "sha256": sha256_file(payload),
            "sizeBytes": file_size(payload),
        },
        "platformScope": "windows_only",
        "release": {"channel": "preview", "version": version},
        "signature": signature,
        "source": source,
        "sourceSha": source_sha,
        "validatedInventoryFileCount": len(inventory["files"]),
        "validatedProposalSha256": EXPORT.sha256_file(composition),
        "validatedProposalSourceSha": proposal["sourceSha"],
    }


def capture_source(args: argparse.Namespace) -> dict[str, str]:
    source = {
        "actor": require_text(
            args.capture_actor, LOGIN_RE, "capture actor"
        ),
        "artifactName": args.output_artifact_name,
        "ref": args.capture_ref,
        "repository": args.capture_repository,
        "rerunPolicy": RERUN_POLICY,
        "runAttempt": args.capture_run_attempt,
        "runId": args.capture_run_id,
        "sha": args.capture_sha,
        "triggeringActor": require_text(
            args.capture_triggering_actor,
            LOGIN_RE,
            "capture triggering actor",
        ),
        "workflow": args.capture_workflow,
    }
    require_exact(
        source["repository"], SOURCE_REPOSITORY, "capture repository"
    )
    require_exact(source["workflow"], CAPTURE_WORKFLOW, "capture workflow")
    require_exact(source["ref"], SOURCE_REF, "capture ref")
    require_text(source["sha"], COMMIT_RE, "capture contract SHA")
    require_text(source["runId"], POSITIVE_INTEGER_RE, "capture run ID")
    require_text(
        source["runAttempt"],
        POSITIVE_INTEGER_RE,
        "capture run attempt",
    )
    require_exact(
        source["artifactName"],
        f"{CAPTURE_ARTIFACT_PREFIX}-{source['runId']}-{source['runAttempt']}",
        "capture artifact name",
    )
    require_exact(
        source["actor"],
        "github-actions[bot]",
        "capture automation actor",
    )
    if source["triggeringActor"] != source["actor"]:
        fail("capture workflow permits only same-actor reruns")
    return source


def evidence_paths() -> dict[str, str]:
    head = WINDOWS.head_paths(HEAD)
    return {
        "authenticodeVerification": AUTHENTICODE_FILE,
        "completionScreenshot": head["completionScreenshot"],
        "payloadHttpLog": PAYLOAD_HTTP_LOG,
        "progressLog": head["progressLog"],
        "progressScreenshot": head["progressScreenshot"],
        "startupLog": STARTUP_LOG,
        "startupReceipt": head["receipt"],
        "startupScreenshot": STARTUP_SCREENSHOT,
        "startupVisualReceipt": STARTUP_VISUAL_RECEIPT,
    }


def validate_unsigned_authenticode(
    evidence_root: Path,
    candidate: dict[str, Any],
    source: dict[str, str],
) -> dict[str, Any]:
    path = safe_file(
        evidence_root, AUTHENTICODE_FILE, "unsigned Authenticode receipt"
    )
    receipt = require_exact_keys(
        read_json(path, "unsigned Authenticode receipt"),
        {
            "artifact",
            "contractName",
            "contractVersion",
            "generatedAt",
            "nativeHostEvidence",
            "signatureStatus",
            "signingRequired",
            "source",
            "status",
            "unsignedReason",
            "verifier",
        },
        "unsigned Authenticode receipt",
    )
    require_exact(
        receipt.get("contractName"),
        AUTHENTICODE_CONTRACT,
        "unsigned Authenticode contract",
    )
    require_exact(
        receipt.get("contractVersion"),
        CONTRACT_VERSION,
        "unsigned Authenticode contract version",
    )
    require_exact(
        receipt.get("status"),
        "verified",
        "unsigned Authenticode receipt status",
    )
    for key, expected in AUTHENTICODE_BINDING.items():
        require_exact(
            receipt.get(key),
            expected,
            f"unsigned Authenticode receipt {key}",
        )
    require_exact(
        receipt.get("artifact"),
        {
            "fileName": candidate["installer"]["fileName"],
            "path": candidate["installer"]["path"],
            "sha256": candidate["installer"]["sha256"],
            "sizeBytes": candidate["installer"]["sizeBytes"],
        },
        "unsigned Authenticode artifact",
    )
    require_exact(
        receipt.get("source"), source, "unsigned Authenticode source"
    )
    require_exact(
        receipt.get("nativeHostEvidence"),
        {
            "contractName": WINDOWS.NATIVE_HOST_CONTRACT,
            "evidenceSource": "GitHub-hosted windows-latest",
            "hostPlatform": "windows",
            "isNativeWindows": True,
            "runner": "pwsh",
            "status": "verified",
        },
        "unsigned Authenticode native host evidence",
    )
    require_exact(
        receipt.get("verifier"),
        {
            "authenticodeStatus": "NotSigned",
            "implementation": (
                "scripts/verify_unsigned_windows_preview_authenticode.ps1"
            ),
            "platform": "windows",
            "securityDirectoryEmpty": True,
        },
        "unsigned Authenticode verifier",
    )
    return authenticode_binding(path)


def validate_startup_visual(
    evidence_root: Path,
    candidate: dict[str, Any],
    source: dict[str, str],
) -> dict[str, Any]:
    path = safe_file(
        evidence_root, STARTUP_VISUAL_RECEIPT, "startup visual receipt"
    )
    receipt = require_exact_keys(
        read_json(path, "startup visual receipt"),
        {
            "candidate",
            "contractName",
            "contractVersion",
            "generatedAtUtc",
            "installedExecutable",
            "nativeHostEvidence",
            "source",
            "startupScreenshot",
            "status",
        },
        "startup visual receipt",
    )
    require_exact(
        receipt.get("contractName"),
        STARTUP_VISUAL_CONTRACT,
        "startup visual contract",
    )
    require_exact(
        receipt.get("contractVersion"),
        CONTRACT_VERSION,
        "startup visual contract version",
    )
    require_exact(receipt.get("status"), "captured", "startup visual status")
    expected_candidate = {
        "installer": candidate["installer"],
        "payload": candidate["payload"],
        "release": candidate["release"],
        "signature": candidate["signature"],
        "sourceSha": candidate["sourceSha"],
    }
    require_exact(
        receipt.get("candidate"),
        expected_candidate,
        "startup visual candidate binding",
    )
    require_exact(receipt.get("source"), source, "startup visual source")
    native = require_exact_keys(
        receipt.get("nativeHostEvidence"),
        {
            "contractName",
            "evidenceSource",
            "hostPlatform",
            "isNativeWindows",
            "runner",
            "status",
        },
        "startup visual native host evidence",
    )
    expected_native = {
        "contractName": WINDOWS.NATIVE_HOST_CONTRACT,
        "evidenceSource": "GitHub-hosted windows-latest",
        "hostPlatform": "windows",
        "isNativeWindows": True,
        "runner": "pwsh",
        "status": "verified",
    }
    require_exact(native, expected_native, "startup visual native host evidence")
    executable = require_exact_keys(
        receipt.get("installedExecutable"),
        {
            "fileName",
            "payloadEntry",
            "sha256",
            "sizeBytes",
        },
        "startup visual installed executable",
    )
    require_exact(
        executable.get("fileName"),
        "Chummer.Avalonia.exe",
        "startup visual executable fileName",
    )
    portable_path(
        executable.get("payloadEntry"),
        "startup visual executable payload entry",
    )
    require_text(
        executable.get("sha256"),
        SHA256_RE,
        "startup visual executable SHA-256",
    )
    if (
        type(executable.get("sizeBytes")) is not int
        or executable["sizeBytes"] < 1
    ):
        fail("startup visual executable sizeBytes is invalid")
    screenshot_path = safe_file(
        evidence_root, STARTUP_SCREENSHOT, "startup screenshot"
    )
    width, height = WINDOWS.validate_png(
        screenshot_path, "startup screenshot"
    )
    expected_screenshot = {
        "height": height,
        "path": STARTUP_SCREENSHOT,
        "sha256": sha256_file(screenshot_path),
        "width": width,
    }
    require_exact(
        receipt.get("startupScreenshot"),
        expected_screenshot,
        "startup visual screenshot binding",
    )
    return {
        "receipt": binding(path, STARTUP_VISUAL_RECEIPT),
        "installedExecutable": executable,
        "screenshot": expected_screenshot,
    }


def validate_native_evidence(
    evidence_root: Path,
    candidate: dict[str, Any],
    source: dict[str, str],
    *,
    require_exact_root: bool = True,
) -> dict[str, Any]:
    paths = evidence_paths()
    required = set(paths.values())
    actual = {
        row["path"] for row in exact_inventory(evidence_root)
    }
    if require_exact_root and actual != required:
        fail("native evidence input differs from the exact bounded file set")
    if not require_exact_root and not required.issubset(actual):
        fail("native evidence artifact is missing a required bounded file")
    installer = {
        "fileName": candidate["installer"]["fileName"],
        "sha256": candidate["installer"]["sha256"],
        "sizeBytes": candidate["installer"]["sizeBytes"],
    }
    payload = {
        "fileName": candidate["payload"]["fileName"],
        "sha256": candidate["payload"]["sha256"],
        "sizeBytes": candidate["payload"]["sizeBytes"],
    }
    head = WINDOWS.validate_evidence_head(
        evidence_root,
        head=HEAD,
        version=candidate["release"]["version"],
        channel="preview",
        installer=installer,
        payload=payload,
        require_authenticode=False,
    )
    unsigned_authenticode = validate_unsigned_authenticode(
        evidence_root, candidate, source
    )
    head["authenticodeVerification"] = unsigned_authenticode
    startup_path = safe_file(
        evidence_root, STARTUP_SCREENSHOT, "startup screenshot"
    )
    startup_size = WINDOWS.validate_png(
        startup_path, "startup screenshot"
    )
    screenshot_rows = [
        {
            "height": startup_size[1],
            "path": STARTUP_SCREENSHOT,
            "role": "startup",
            "sha256": sha256_file(startup_path),
            "width": startup_size[0],
        },
        *head["screenshots"],
    ]
    digests = [row["sha256"] for row in screenshot_rows]
    if len(set(digests)) != len(digests):
        fail("startup, progress, and completion screenshots must be distinct")
    startup_visual = validate_startup_visual(
        evidence_root, candidate, source
    )
    return {
        "authenticodeVerification": unsigned_authenticode,
        "head": head,
        "startupVisual": startup_visual,
        "screenshots": screenshot_rows,
        "startupLog": binding(
            safe_file(evidence_root, STARTUP_LOG, "startup log"),
            STARTUP_LOG,
        ),
        "payloadHttpLog": binding(
            safe_file(
                evidence_root, PAYLOAD_HTTP_LOG, "payload HTTP log"
            ),
            PAYLOAD_HTTP_LOG,
        ),
    }


def copy_candidate_provenance(
    candidate_root: Path, evidence_root: Path
) -> list[dict[str, Any]]:
    target_root = evidence_root / CANDIDATE_PROVENANCE_DIRECTORY
    if target_root.exists() or target_root.is_symlink():
        fail("candidate provenance target must be absent")
    target_root.mkdir(mode=0o700)
    preserved = (
        EXPORT.CONTENT_INVENTORY_PATH,
        EXPORT.EXPORT_RECEIPT_PATH,
    )
    for relative in preserved:
        source = safe_file(
            candidate_root, relative, f"candidate provenance {relative}"
        )
        target = target_root / relative
        shutil.copyfile(source, target, follow_symlinks=False)
        os.chmod(target, 0o444, follow_symlinks=False)
    if {row["path"] for row in exact_inventory(target_root)} != set(
        preserved
    ):
        fail("preserved unsigned candidate provenance differs")
    rows = exact_inventory(target_root)
    return [
        {
            **row,
            "path": f"{CANDIDATE_PROVENANCE_DIRECTORY}/{row['path']}",
        }
        for row in rows
    ]


def validate_preserved_candidate_provenance(
    evidence_root: Path, candidate: dict[str, Any]
) -> tuple[dict[str, Any], dict[str, Any]]:
    provenance = evidence_root / CANDIDATE_PROVENANCE_DIRECTORY
    expected_paths = {
        EXPORT.CONTENT_INVENTORY_PATH,
        EXPORT.EXPORT_RECEIPT_PATH,
    }
    if {
        row["path"] for row in exact_inventory(provenance)
    } != expected_paths:
        fail("preserved candidate provenance has extra or missing files")
    inventory_path = safe_file(
        provenance,
        EXPORT.CONTENT_INVENTORY_PATH,
        "preserved candidate content inventory",
    )
    receipt_path = safe_file(
        provenance,
        EXPORT.EXPORT_RECEIPT_PATH,
        "preserved candidate export receipt",
    )
    require_exact(
        binding(inventory_path, EXPORT.CONTENT_INVENTORY_PATH),
        candidate["contentInventory"],
        "preserved candidate inventory binding",
    )
    require_exact(
        binding(receipt_path, EXPORT.EXPORT_RECEIPT_PATH),
        candidate["exportReceipt"],
        "preserved candidate export binding",
    )
    inventory = require_exact_keys(
        read_json(inventory_path, "preserved candidate content inventory"),
        {
            "contractName",
            "contractVersion",
            "crossRunBitReproducible",
            "files",
            "platformScope",
            "release",
            "signature",
            "sourceSha",
        },
        "preserved candidate content inventory",
    )
    require_exact(
        inventory.get("contractName"),
        EXPORT.CONTENT_INVENTORY_CONTRACT,
        "preserved candidate inventory contract",
    )
    require_exact(
        inventory.get("contractVersion"),
        EXPORT.CONTRACT_VERSION,
        "preserved candidate inventory version",
    )
    require_exact(
        inventory.get("crossRunBitReproducible"),
        False,
        "preserved candidate reproducibility posture",
    )
    require_exact(
        inventory.get("platformScope"),
        "windows_only",
        "preserved candidate platform scope",
    )
    require_exact(
        inventory.get("release"),
        candidate["release"],
        "preserved candidate release",
    )
    require_exact(
        inventory.get("signature"),
        candidate["signature"],
        "preserved candidate signature",
    )
    require_exact(
        inventory.get("sourceSha"),
        candidate["sourceSha"],
        "preserved candidate source SHA",
    )
    rows = inventory.get("files")
    if not isinstance(rows, list) or rows != sorted(
        rows, key=lambda row: str(row.get("path")) if isinstance(row, dict) else ""
    ):
        fail("preserved candidate inventory rows are not in exact order")
    indexed: dict[str, dict[str, Any]] = {}
    for row in rows:
        typed = require_exact_keys(
            row,
            {"path", "sha256", "sizeBytes"},
            "preserved candidate inventory row",
        )
        relative = portable_path(
            typed.get("path"), "preserved candidate inventory row path"
        )
        if relative in indexed:
            fail("preserved candidate inventory repeats a path")
        require_text(
            typed.get("sha256"),
            SHA256_RE,
            "preserved candidate inventory row SHA-256",
        )
        if (
            type(typed.get("sizeBytes")) is not int
            or typed["sizeBytes"] < 1
        ):
            fail("preserved candidate inventory row size is invalid")
        indexed[relative] = typed
    if len(indexed) != candidate["validatedInventoryFileCount"]:
        fail("preserved candidate inventory file count differs")
    for key, relative in (
        ("compositionRequest", EXPORT.COMPOSITION_PATH),
        ("installer", EXPORT.INSTALLER_PATH),
        ("manifest", EXPORT.MANIFEST_PATH),
        ("payload", EXPORT.PAYLOAD_PATH),
    ):
        expected = {
            "path": relative,
            "sha256": candidate[key]["sha256"],
            "sizeBytes": candidate[key]["sizeBytes"],
        }
        require_exact(
            indexed.get(relative),
            expected,
            f"preserved candidate {key} inventory row",
        )
    receipt = require_exact_keys(
        read_json(receipt_path, "preserved candidate export receipt"),
        {
            "compositionRequest",
            "contractName",
            "contractVersion",
            "crossRunBitReproducible",
            "deployAuthorized",
            "exportedContent",
            "githubArtifactTransport",
            "inventory",
            "platformScope",
            "publicationAuthorized",
            "release",
            "runnerNonce",
            "signature",
            "source",
            "status",
            "uiUploadAuthorized",
            "uploadAuthorized",
        },
        "preserved candidate export receipt",
    )
    require_exact(
        receipt.get("contractName"),
        EXPORT.EXPORT_CONTRACT,
        "preserved candidate export contract",
    )
    require_exact(
        receipt.get("contractVersion"),
        EXPORT.CONTRACT_VERSION,
        "preserved candidate export version",
    )
    require_exact(
        receipt.get("status"), "exported", "preserved candidate export status"
    )
    require_exact(
        receipt.get("crossRunBitReproducible"),
        False,
        "preserved candidate export reproducibility",
    )
    require_exact(
        receipt.get("githubArtifactTransport"),
        "ephemeral_candidate_only",
        "preserved candidate transport",
    )
    require_exact(
        receipt.get("platformScope"),
        "windows_only",
        "preserved candidate export platform scope",
    )
    require_exact(
        receipt.get("release"),
        candidate["release"],
        "preserved candidate export release",
    )
    require_exact(
        receipt.get("signature"),
        candidate["signature"],
        "preserved candidate export signature",
    )
    require_exact(
        receipt.get("source"),
        candidate["source"],
        "preserved candidate export source",
    )
    require_exact(
        receipt.get("exportedContent"),
        rows,
        "preserved candidate exported content",
    )
    require_exact(
        receipt.get("inventory"),
        candidate["contentInventory"],
        "preserved candidate export inventory binding",
    )
    require_exact(
        receipt.get("compositionRequest"),
        candidate["compositionRequest"],
        "preserved candidate composition binding",
    )
    require_no_authority(receipt, "preserved candidate export")
    return inventory, receipt


def capture(args: argparse.Namespace) -> dict[str, Any]:
    candidate = candidate_bindings(args)
    source = capture_source(args)
    evidence_root = args.evidence_root.resolve(strict=True)
    if not evidence_root.is_dir() or evidence_root.is_symlink():
        fail("evidence root must be one physical directory")
    native = validate_native_evidence(evidence_root, candidate, source)
    provenance = copy_candidate_provenance(
        args.candidate_root.resolve(strict=True), evidence_root
    )
    validate_preserved_candidate_provenance(evidence_root, candidate)
    policy = {
        "authenticodeRequired": False,
        "evidenceOnly": True,
        "releaseChannel": "preview",
        "signingRequirement": SIGNING_REQUIREMENT,
    }
    authority = {
        "deployAuthorized": False,
        "publicationAuthorized": False,
        "uiUploadAuthorized": False,
        "uploadAuthorized": False,
    }
    payload = {
        **authority,
        "candidate": candidate,
        "captureMode": "hosted_native_windows",
        "contractName": CAPTURE_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "generatedAt": utc_now(),
        "heads": [native["head"]],
        "authenticodeVerification": native["authenticodeVerification"],
        "nativeEvidence": native,
        "policy": policy,
        "preservedCandidateFiles": provenance,
        "source": source,
        "status": "captured",
    }
    write_json_new(evidence_root / CAPTURE_FILE, payload)
    inventory = {
        **authority,
        "captureManifest": binding(
            evidence_root / CAPTURE_FILE, CAPTURE_FILE
        ),
        "contractName": CAPTURE_INVENTORY_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "files": exact_inventory(
            evidence_root, exclude={CAPTURE_INVENTORY_FILE}
        ),
        "policy": policy,
        "status": "captured",
    }
    write_json_new(evidence_root / CAPTURE_INVENTORY_FILE, inventory)
    return {
        "capture_inventory_sha256": sha256_file(
            evidence_root / CAPTURE_INVENTORY_FILE
        ),
        "candidate_manifest_sha256": candidate["manifest"]["sha256"],
        "candidate_inventory_sha256": candidate["contentInventory"][
            "sha256"
        ],
    }


def verify_capture(
    capture_root: Path, expected_inventory_sha256: str
) -> tuple[dict[str, Any], dict[str, Any]]:
    root = capture_root.resolve(strict=True)
    inventory_path = safe_file(
        root, CAPTURE_INVENTORY_FILE, "capture inventory"
    )
    if sha256_file(inventory_path) != require_text(
        expected_inventory_sha256,
        SHA256_RE,
        "capture inventory SHA-256",
    ):
        fail("capture inventory differs from independently supplied SHA-256")
    inventory = require_exact_keys(
        read_json(inventory_path, "capture inventory"),
        {
            "captureManifest",
            "contractName",
            "contractVersion",
            "deployAuthorized",
            "files",
            "policy",
            "publicationAuthorized",
            "status",
            "uiUploadAuthorized",
            "uploadAuthorized",
        },
        "capture inventory",
    )
    require_exact(
        inventory.get("contractName"),
        CAPTURE_INVENTORY_CONTRACT,
        "capture inventory contract",
    )
    require_exact(
        inventory.get("contractVersion"),
        CONTRACT_VERSION,
        "capture inventory contract version",
    )
    require_exact(inventory.get("status"), "captured", "capture inventory status")
    require_no_authority(inventory, "capture inventory")
    actual = exact_inventory(root, exclude={CAPTURE_INVENTORY_FILE})
    require_exact(inventory.get("files"), actual, "capture inventory files")
    capture_path = safe_file(root, CAPTURE_FILE, "capture manifest")
    require_exact(
        inventory.get("captureManifest"),
        binding(capture_path, CAPTURE_FILE),
        "capture manifest inventory binding",
    )
    payload = read_json(capture_path, "capture manifest")
    require_exact(
        payload.get("contractName"),
        CAPTURE_CONTRACT,
        "capture manifest contract",
    )
    require_exact(
        payload.get("contractVersion"),
        CONTRACT_VERSION,
        "capture manifest contract version",
    )
    require_exact(payload.get("status"), "captured", "capture manifest status")
    require_no_authority(payload, "capture manifest")
    require_exact(
        payload.get("policy"),
        {
            "authenticodeRequired": False,
            "evidenceOnly": True,
            "releaseChannel": "preview",
            "signingRequirement": SIGNING_REQUIREMENT,
        },
        "capture policy",
    )
    if not isinstance(payload.get("generatedAt"), str):
        fail("capture generatedAt is missing")
    candidate = require_exact_keys(
        payload.get("candidate"),
        {
            "artifact",
            "compositionRequest",
            "contentInventory",
            "exportReceipt",
            "installer",
            "manifest",
            "payload",
            "platformScope",
            "release",
            "signature",
            "source",
            "sourceSha",
            "validatedInventoryFileCount",
            "validatedProposalSha256",
            "validatedProposalSourceSha",
        },
        "capture candidate",
    )
    require_exact(
        candidate.get("signature"),
        {"policy": "preview_policy", "required": False, "status": "unsigned"},
        "capture candidate signature posture",
    )
    validate_preserved_candidate_provenance(root, candidate)
    source = payload.get("source")
    if not isinstance(source, dict):
        fail("capture source binding is missing")
    native = validate_native_evidence(
        root, candidate, source, require_exact_root=False
    )
    require_exact(
        payload.get("nativeEvidence"),
        native,
        "capture native evidence",
    )
    require_exact(
        payload.get("heads"),
        [native["head"]],
        "capture heads",
    )
    require_exact(
        payload.get("authenticodeVerification"),
        native["authenticodeVerification"],
        "capture Authenticode binding",
    )
    return payload, inventory


def finalization_source(args: argparse.Namespace) -> dict[str, str]:
    source = {
        "actor": args.finalization_actor,
        "artifactName": args.finalization_artifact_name,
        "ref": args.finalization_ref,
        "repository": args.finalization_repository,
        "rerunPolicy": RERUN_POLICY,
        "runAttempt": args.finalization_run_attempt,
        "runId": args.finalization_run_id,
        "sha": args.finalization_sha,
        "triggeringActor": args.finalization_triggering_actor,
        "workflow": args.finalization_workflow,
    }
    require_exact(
        source["repository"], SOURCE_REPOSITORY, "finalization repository"
    )
    require_exact(
        source["workflow"], FINALIZE_WORKFLOW, "finalization workflow"
    )
    require_exact(source["ref"], SOURCE_REF, "finalization ref")
    require_text(source["sha"], COMMIT_RE, "finalization contract SHA")
    require_text(
        source["runId"], POSITIVE_INTEGER_RE, "finalization run ID"
    )
    require_text(
        source["runAttempt"],
        POSITIVE_INTEGER_RE,
        "finalization run attempt",
    )
    require_exact(
        source["artifactName"],
        f"{FINALIZED_ARTIFACT_PREFIX}-{source['runId']}-"
        f"{source['runAttempt']}",
        "finalized artifact name",
    )
    return source


def source_projection(source: dict[str, str]) -> dict[str, str]:
    return {
        key: source[key]
        for key in (
            "repository",
            "workflow",
            "runId",
            "runAttempt",
            "ref",
            "sha",
            "actor",
            "artifactName",
        )
    }


def visual_proof(
    capture_payload: dict[str, Any],
    finalization_source_value: dict[str, str],
    reviewer: str,
    capture_inventory_sha256: str,
    generated_at: str,
    confirmations: dict[str, str],
) -> dict[str, Any]:
    candidate = capture_payload["candidate"]
    capture_source_value = capture_payload["source"]
    native = capture_payload["nativeEvidence"]
    screenshot_rows = [
        {
            "role": row["role"],
            "path": row["path"],
            "sha256": row["sha256"],
        }
        for row in native["screenshots"]
    ]
    require_exact(
        [row["role"] for row in screenshot_rows],
        ["startup", "progress", "completion"],
        "visual proof screenshot roles",
    )
    return {
        "artifactDigest": f"sha256:{candidate['installer']['sha256']}",
        "artifactFileName": candidate["installer"]["fileName"],
        "authenticodeVerification": capture_payload[
            "authenticodeVerification"
        ],
        "captureBinding": {
            "artifactName": capture_source_value["artifactName"],
            "inventorySha256": capture_inventory_sha256,
            "ref": capture_source_value["ref"],
            "repository": capture_source_value["repository"],
            "rerunPolicy": capture_source_value["rerunPolicy"],
            "runAttempt": capture_source_value["runAttempt"],
            "runId": capture_source_value["runId"],
            "sha": capture_source_value["sha"],
            "triggeringActor": capture_source_value["triggeringActor"],
            "workflow": capture_source_value["workflow"],
        },
        "channel": "preview",
        "channelId": "preview",
        "checks": {
            "accountable_review_confirmed": True,
            "capture_mode": "hosted_native_windows",
        },
        "clippingReview": {"reviewer": reviewer, "status": "passed"},
        "contractName": VISUAL_PROOF_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "contrastReview": {"reviewer": reviewer, "status": "passed"},
        "finalizationBinding": finalization_source_value,
        "generatedAt": generated_at,
        "head": HEAD,
        "headId": HEAD,
        "platform": "windows",
        "readabilityReview": {"reviewer": reviewer, "status": "passed"},
        "releaseVersion": candidate["release"]["version"],
        "review": {
            "allowlistSource": (
                "repository variable plus protected environment"
            ),
            "authenticatedReviewer": reviewer,
            "captureActor": capture_source_value["actor"],
            "explicitConfirmations": confirmations,
        },
        "rid": RID,
        "screenshots": screenshot_rows,
        "status": "passed",
        "version": candidate["release"]["version"],
    }


def embedded_file_rows(
    root: Path, rows: list[dict[str, Any]]
) -> list[dict[str, Any]]:
    embedded: list[dict[str, Any]] = []
    for row in rows:
        path = safe_file(root, row["path"], "embedded native evidence file")
        raw = path.read_bytes()
        actual = {
            "path": row["path"],
            "sha256": hashlib.sha256(raw).hexdigest(),
            "sizeBytes": len(raw),
        }
        require_exact(actual, row, "embedded native evidence file binding")
        encoded = base64.b64encode(raw).decode("ascii")
        if base64.b64decode(encoded, validate=True) != raw:
            fail("embedded native evidence base64 round-trip differs")
        embedded.append({**row, "bytesBase64": encoded})
    return embedded


def compact_json_sha256(value: object) -> str:
    raw = json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def finalize(args: argparse.Namespace) -> dict[str, str]:
    capture_root = args.capture_root.resolve(strict=True)
    output_root = args.output_root.absolute()
    if output_root.exists() or output_root.is_symlink():
        fail("finalized output root must be absent")
    capture_payload, _ = verify_capture(
        capture_root, args.capture_inventory_sha256
    )
    source = capture_payload["source"]
    expected_capture = {
        "actor": require_text(
            args.expected_capture_actor,
            LOGIN_RE,
            "expected capture actor",
        ),
        "artifactName": args.expected_capture_artifact_name,
        "ref": args.expected_capture_ref,
        "repository": args.expected_capture_repository,
        "rerunPolicy": RERUN_POLICY,
        "runAttempt": args.expected_capture_run_attempt,
        "runId": args.expected_capture_run_id,
        "sha": args.expected_capture_sha,
        "triggeringActor": args.expected_capture_actor,
        "workflow": args.expected_capture_workflow,
    }
    require_exact(source, expected_capture, "authenticated capture source")
    require_exact(
        source["repository"],
        SOURCE_REPOSITORY,
        "authenticated capture repository",
    )
    require_exact(
        source["workflow"],
        CAPTURE_WORKFLOW,
        "authenticated capture workflow",
    )
    require_exact(
        source["ref"], SOURCE_REF, "authenticated capture ref"
    )
    require_text(
        source["sha"], COMMIT_RE, "authenticated capture contract SHA"
    )
    require_text(
        source["runId"],
        POSITIVE_INTEGER_RE,
        "authenticated capture run ID",
    )
    require_text(
        source["runAttempt"],
        POSITIVE_INTEGER_RE,
        "authenticated capture run attempt",
    )
    require_exact(
        source["artifactName"],
        f"{CAPTURE_ARTIFACT_PREFIX}-{source['runId']}-"
        f"{source['runAttempt']}",
        "authenticated capture artifact name",
    )
    if args.accountable_review_confirmed != "true":
        fail("accountable review confirmation is required")
    review = parse_review_json(args.review_json)
    finalized_source = finalization_source(args)
    reviewer = require_accountable_reviewer(
        args.reviewer_id,
        args.reviewer_kind,
        finalized_source["actor"],
        finalized_source["triggeringActor"],
    )
    if source["actor"].lower() == reviewer.lower():
        fail("capture actor cannot perform accountable final review")
    if finalized_source["sha"] != source["sha"]:
        fail(
            "capture and finalization must use the same exact main contract SHA"
        )
    shutil.copytree(capture_root, output_root, symlinks=False)
    if exact_inventory(
        output_root, exclude={FINALIZATION_FILE, FINALIZED_INVENTORY_FILE}
    ) != exact_inventory(capture_root):
        shutil.rmtree(output_root, ignore_errors=True)
        fail("capture bytes changed while copied for finalization")
    authority = {
        "deployAuthorized": False,
        "publicationAuthorized": False,
        "uiUploadAuthorized": False,
        "uploadAuthorized": False,
    }
    confirmations = {
        key: "passed" for key in sorted(EXPECTED_REVIEW_KEYS)
    }
    generated_at = utc_now()
    proof = visual_proof(
        capture_payload,
        finalized_source,
        reviewer,
        args.capture_inventory_sha256,
        generated_at,
        confirmations,
    )
    payload = {
        **authority,
        "accountableReviewConfirmed": True,
        "authenticodeVerification": capture_payload[
            "authenticodeVerification"
        ],
        "captureArtifact": {
            "id": require_text(
                args.expected_capture_artifact_id,
                POSITIVE_INTEGER_RE,
                "capture artifact ID",
            ),
            "name": source["artifactName"],
            "sha256": require_text(
                args.expected_capture_artifact_sha256,
                SHA256_RE,
                "capture artifact SHA-256",
            ),
        },
        "captureInventorySha256": require_text(
            args.capture_inventory_sha256,
            SHA256_RE,
            "capture inventory SHA-256",
        ),
        "captureSource": source,
        "confirmations": confirmations,
        "contractName": FINALIZATION_CONTRACT,
        "contractVersion": CONTRACT_VERSION,
        "finalizationSource": finalized_source,
        "generatedAt": generated_at,
        "policy": capture_payload["policy"],
        "proofs": [],
        "reviewer": reviewer,
        "reviewerKind": REVIEWER_KIND,
        "reviewerWasCaptureActor": False,
        "status": "passed",
    }
    try:
        write_json_new(output_root / VISUAL_PROOF_FILE, proof)
        payload["proofs"] = [
            {
                "headId": HEAD,
                "path": VISUAL_PROOF_FILE,
                "sha256": sha256_file(output_root / VISUAL_PROOF_FILE),
            }
        ]
        write_json_new(output_root / FINALIZATION_FILE, payload)
        inventory = {
            **authority,
            "captureInventorySha256": args.capture_inventory_sha256,
            "contractName": FINALIZED_INVENTORY_CONTRACT,
            "contractVersion": CONTRACT_VERSION,
            "files": exact_inventory(
                output_root,
                exclude={
                    FINALIZED_EVIDENCE_FILE,
                    FINALIZED_INVENTORY_FILE,
                },
            ),
            "finalization": binding(
                output_root / FINALIZATION_FILE, FINALIZATION_FILE
            ),
            "policy": capture_payload["policy"],
            "status": "passed",
        }
        write_json_new(output_root / FINALIZED_INVENTORY_FILE, inventory)
        outer_rows = sorted(
            [
                *inventory["files"],
                binding(
                    output_root / FINALIZED_INVENTORY_FILE,
                    FINALIZED_INVENTORY_FILE,
                ),
            ],
            key=lambda row: str(row["path"]),
        )
        candidate_inventory_path = safe_file(
            output_root / CANDIDATE_PROVENANCE_DIRECTORY,
            EXPORT.CONTENT_INVENTORY_PATH,
            "finalized candidate content inventory",
        )
        native_evidence = {
            "status": "passed",
            "captureGeneratedAtUtc": capture_payload["generatedAt"],
            "finalizationGeneratedAtUtc": generated_at,
            "reviewer": reviewer,
            "captureSource": source_projection(source),
            "finalizationSource": source_projection(finalized_source),
            "candidateContentInventorySha256": sha256_file(
                candidate_inventory_path
            ),
            "candidateContentInventory": read_json(
                candidate_inventory_path,
                "finalized candidate content inventory",
            ),
            "files": embedded_file_rows(output_root, outer_rows),
        }
        write_json_new(
            output_root / FINALIZED_EVIDENCE_FILE, native_evidence
        )
    except BaseException:
        shutil.rmtree(output_root, ignore_errors=True)
        raise
    return {
        "finalized_inventory_sha256": sha256_file(
            output_root / FINALIZED_INVENTORY_FILE
        ),
        "finalization_sha256": sha256_file(
            output_root / FINALIZATION_FILE
        ),
        "native_evidence_file_sha256": sha256_file(
            output_root / FINALIZED_EVIDENCE_FILE
        ),
        "native_evidence_sha256": compact_json_sha256(native_evidence),
    }


def add_candidate_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--candidate-root", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--expected-manifest-sha256", required=True)
    parser.add_argument("--expected-inventory-sha256", required=True)
    parser.add_argument("--candidate-source-sha", required=True)
    parser.add_argument("--candidate-run-id", required=True)
    parser.add_argument("--candidate-run-attempt", required=True)
    parser.add_argument("--candidate-actor", required=True)
    parser.add_argument("--candidate-artifact-id", required=True)
    parser.add_argument("--candidate-artifact-name", required=True)
    parser.add_argument("--candidate-artifact-sha256", required=True)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    preflight = subparsers.add_parser("preflight")
    add_candidate_arguments(preflight)

    capture_parser = subparsers.add_parser("capture")
    add_candidate_arguments(capture_parser)
    capture_parser.add_argument("--evidence-root", required=True, type=Path)
    capture_parser.add_argument("--capture-repository", required=True)
    capture_parser.add_argument("--capture-workflow", required=True)
    capture_parser.add_argument("--capture-run-id", required=True)
    capture_parser.add_argument("--capture-run-attempt", required=True)
    capture_parser.add_argument("--capture-ref", required=True)
    capture_parser.add_argument("--capture-sha", required=True)
    capture_parser.add_argument("--capture-actor", required=True)
    capture_parser.add_argument("--capture-triggering-actor", required=True)
    capture_parser.add_argument("--output-artifact-name", required=True)

    finalize_parser = subparsers.add_parser("finalize")
    finalize_parser.add_argument("--capture-root", required=True, type=Path)
    finalize_parser.add_argument("--output-root", required=True, type=Path)
    finalize_parser.add_argument("--capture-inventory-sha256", required=True)
    finalize_parser.add_argument(
        "--expected-capture-repository", required=True
    )
    finalize_parser.add_argument(
        "--expected-capture-workflow", required=True
    )
    finalize_parser.add_argument("--expected-capture-run-id", required=True)
    finalize_parser.add_argument(
        "--expected-capture-run-attempt", required=True
    )
    finalize_parser.add_argument("--expected-capture-ref", required=True)
    finalize_parser.add_argument("--expected-capture-sha", required=True)
    finalize_parser.add_argument("--expected-capture-actor", required=True)
    finalize_parser.add_argument(
        "--expected-capture-artifact-id", required=True
    )
    finalize_parser.add_argument(
        "--expected-capture-artifact-name", required=True
    )
    finalize_parser.add_argument(
        "--expected-capture-artifact-sha256", required=True
    )
    finalize_parser.add_argument("--accountable-review-confirmed", required=True)
    finalize_parser.add_argument("--review-json", required=True)
    finalize_parser.add_argument("--reviewer-id", required=True)
    finalize_parser.add_argument("--reviewer-kind", required=True)
    finalize_parser.add_argument("--finalization-repository", required=True)
    finalize_parser.add_argument("--finalization-workflow", required=True)
    finalize_parser.add_argument("--finalization-run-id", required=True)
    finalize_parser.add_argument("--finalization-run-attempt", required=True)
    finalize_parser.add_argument("--finalization-ref", required=True)
    finalize_parser.add_argument("--finalization-sha", required=True)
    finalize_parser.add_argument("--finalization-actor", required=True)
    finalize_parser.add_argument(
        "--finalization-triggering-actor", required=True
    )
    finalize_parser.add_argument(
        "--finalization-artifact-name", required=True
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        if args.command == "preflight":
            candidate = candidate_bindings(args)
            result = {
                "candidate_manifest": candidate["manifest"]["path"],
                "candidate_manifest_sha256": candidate["manifest"]["sha256"],
                "candidate_inventory_sha256": candidate["contentInventory"][
                    "sha256"
                ],
                "installer": candidate["installer"]["path"],
                "installer_sha256": candidate["installer"]["sha256"],
                "installer_size_bytes": str(
                    candidate["installer"]["sizeBytes"]
                ),
                "payload": candidate["payload"]["path"],
                "payload_sha256": candidate["payload"]["sha256"],
                "payload_size_bytes": str(candidate["payload"]["sizeBytes"]),
                "version": candidate["release"]["version"],
            }
        elif args.command == "capture":
            result = capture(args)
        else:
            result = finalize(args)
    except (
        EvidenceError,
        EXPORT.ExportError,
        WINDOWS.EvidenceError,
        OSError,
        ValueError,
    ) as exc:
        print(f"unsigned-windows-native-evidence:error: {exc}", file=sys.stderr)
        return 2
    for key, value in result.items():
        print(f"{key}={value}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
