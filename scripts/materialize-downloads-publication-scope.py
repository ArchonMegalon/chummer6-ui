#!/usr/bin/env python3
"""Write an explicit receipt for what kind of downloads publication happened."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import re
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit


RUN_UPLOAD_RECEIPT_SCHEMA = "chummer.release-upload-handoff/v1"
RUN_UPLOAD_RECEIPT_KEYS = {
    "schemaVersion",
    "apiOrigin",
    "sessionId",
    "expiresAtUtc",
    "candidate",
    "completion",
    "stateHistory",
}
RUN_CANDIDATE_KEYS = {
    "version",
    "canonicalManifestSha256",
    "inventorySha256",
    "fileCount",
    "totalBytes",
    "bundleIdentitySha256",
}
RUN_COMPLETION_KEYS = {
    "state",
    "requestStartedAtUtc",
    "lastUpdatedAtUtc",
    "lastHttpStatus",
    "lastProblemType",
    "traceId",
}
RUN_HISTORY_KEYS = {"state", "atUtc"}
RUN_COMPLETION_TRANSITIONS = {
    "created": {"uploaded"},
    "uploaded": {"request_started"},
    "request_started": {"outcome_unknown", "completed"},
    "outcome_unknown": {"outcome_unknown", "completed"},
    "completed": set(),
}
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
SESSION_ID_PATTERN = re.compile(r"[0-9a-f]{32}")


def _load_windows_transaction_module():
    module_name = "chummer6_ui_windows_publication_transaction_receipts"
    existing = sys.modules.get(module_name)
    if existing is not None:
        return existing
    path = Path(__file__).resolve().with_name(
        "windows_only_publication_transaction.py"
    )
    spec = importlib.util.spec_from_file_location(module_name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError("could not load Windows publication transaction contract")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def _require_sha256(value: object, *, label: str) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        raise ValueError(f"{label} must be a canonical lowercase SHA-256")
    return value


def _require_utc_timestamp(value: object, *, label: str) -> str:
    if not isinstance(value, str) or not value or len(value) > 64:
        raise ValueError(f"{label} must be a short UTC timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ValueError(f"{label} must be a valid UTC timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ValueError(f"{label} must include a UTC offset")
    if parsed.utcoffset().total_seconds() != 0:
        raise ValueError(f"{label} must be expressed in UTC")
    return value


def _require_canonical_origin(value: object, *, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} is required")
    parsed = urlsplit(value)
    try:
        port = parsed.port
    except ValueError as exc:
        raise ValueError(f"{label} has an invalid port") from exc
    if (
        parsed.scheme not in {"http", "https"}
        or not parsed.hostname
        or parsed.username is not None
        or parsed.password is not None
        or parsed.path not in {"", "/"}
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError(f"{label} must be a canonical HTTP(S) origin")
    default_port = 443 if parsed.scheme == "https" else 80
    authority = (
        parsed.hostname.lower()
        if port in {None, default_port}
        else f"{parsed.hostname.lower()}:{port}"
    )
    canonical = f"{parsed.scheme.lower()}://{authority}"
    if value != canonical:
        raise ValueError(f"{label} must use canonical origin spelling")
    return canonical


def _require_session_id(value: object, *, label: str) -> str:
    if not isinstance(value, str) or SESSION_ID_PATTERN.fullmatch(value) is None:
        raise ValueError(f"{label} must be a canonical lowercase 32-hex session ID")
    return value


def _load_json_object_exact(payload_bytes: bytes, *, label: str) -> dict[str, Any]:
    def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} contains duplicate JSON property {key!r}")
            result[key] = value
        return result

    try:
        payload = json.loads(
            payload_bytes.decode("utf-8"), object_pairs_hook=reject_duplicate_keys
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{label} must contain canonical UTF-8 JSON") from exc
    if not isinstance(payload, dict):
        raise ValueError(f"{label} must contain a JSON object")
    return payload


def _validate_run_candidate(
    candidate: object,
    *,
    release_version: str,
    frozen_canonical_manifest_sha256: str,
    frozen_inventory_sha256: str,
    frozen_file_count: int,
    frozen_total_bytes: int,
) -> dict[str, Any]:
    if not isinstance(candidate, dict) or set(candidate) != RUN_CANDIDATE_KEYS:
        raise ValueError("Run upload receipt candidate has an unexpected property set")

    version = candidate.get("version")
    if (
        not isinstance(version, str)
        or not (1 <= len(version) <= 160)
        or any(ord(character) < 0x21 or ord(character) > 0x7E for character in version)
    ):
        raise ValueError("Run upload receipt candidate version is invalid")
    canonical_manifest_sha256 = _require_sha256(
        candidate.get("canonicalManifestSha256"),
        label="Run candidate canonicalManifestSha256",
    )
    inventory_sha256 = _require_sha256(
        candidate.get("inventorySha256"), label="Run candidate inventorySha256"
    )
    bundle_identity_sha256 = _require_sha256(
        candidate.get("bundleIdentitySha256"),
        label="Run candidate bundleIdentitySha256",
    )
    file_count = candidate.get("fileCount")
    total_bytes = candidate.get("totalBytes")
    if (
        isinstance(file_count, bool)
        or not isinstance(file_count, int)
        or not (1 <= file_count <= 100_000)
    ):
        raise ValueError("Run upload receipt candidate fileCount is invalid")
    if (
        isinstance(total_bytes, bool)
        or not isinstance(total_bytes, int)
        or not (0 <= total_bytes < 2**63)
    ):
        raise ValueError("Run upload receipt candidate totalBytes is invalid")

    identity_fields = {
        field: candidate[field]
        for field in (
            "version",
            "canonicalManifestSha256",
            "inventorySha256",
            "fileCount",
            "totalBytes",
        )
    }
    identity_material = json.dumps(
        identity_fields, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")
    if hashlib.sha256(identity_material).hexdigest() != bundle_identity_sha256:
        raise ValueError(
            "Run candidate bundleIdentitySha256 does not bind the candidate summary"
        )

    frozen_manifest = _require_sha256(
        frozen_canonical_manifest_sha256,
        label="frozen generation canonical manifest SHA-256",
    )
    frozen_inventory = _require_sha256(
        frozen_inventory_sha256,
        label="frozen generation inventory SHA-256",
    )
    if (
        isinstance(frozen_file_count, bool)
        or not isinstance(frozen_file_count, int)
        or frozen_file_count < 1
    ):
        raise ValueError("frozen generation file count is invalid")
    if (
        isinstance(frozen_total_bytes, bool)
        or not isinstance(frozen_total_bytes, int)
        or frozen_total_bytes < 0
    ):
        raise ValueError("frozen generation total bytes is invalid")

    expected = {
        "version": release_version,
        "canonicalManifestSha256": frozen_manifest,
        "inventorySha256": frozen_inventory,
        "fileCount": frozen_file_count,
        "totalBytes": frozen_total_bytes,
    }
    actual = {field: candidate[field] for field in expected}
    if actual != expected:
        raise ValueError(
            "Run upload receipt candidate does not match the frozen generation binding"
        )
    return candidate


def _validate_activation_binding(
    binding: object,
    *,
    release_version: str,
    full_shelf_inventory_sha256: str,
    publication_scope_sha256: str,
    scope_decision_sha256: str,
    registry_prepare: object = None,
) -> dict[str, Any]:
    expected_keys = {
        "activationProofs",
        "contractName",
        "contractVersion",
        "fullShelfInventorySha256",
        "journal",
        "proposalSha256",
        "publicationScopeSha256",
        "rollbackPolicy",
        "runUploadPaths",
        "runUploadCandidate",
        "scopeDecisionSha256",
        "transactionId",
    }
    if isinstance(binding, dict) and "registryPrepare" in binding:
        expected_keys.add("registryPrepare")
    if (
        not isinstance(binding, dict)
        or set(binding) != expected_keys
        or binding.get("contractName")
        != "chummer6-ui.windows-only-publication-activation-binding"
        or binding.get("contractVersion") != 1
        or binding.get("rollbackPolicy")
        != (
            "rollback_all_activated_targets_unless_an_exact_commit_record_binds_the_"
            "journal_and_publication_receipt"
        )
    ):
        raise ValueError("Windows activation binding contract is invalid")
    transaction_id = binding.get("transactionId")
    if (
        not isinstance(transaction_id, str)
        or re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]{7,127}", transaction_id) is None
    ):
        raise ValueError("Windows activation binding transaction ID is invalid")
    expected_full_inventory = _require_sha256(
        full_shelf_inventory_sha256,
        label="Windows publication scope fullShelfInventorySha256",
    )
    if (
        _require_sha256(
            binding.get("fullShelfInventorySha256"),
            label="Windows activation fullShelfInventorySha256",
        )
        != expected_full_inventory
    ):
        raise ValueError("Windows activation binding targets a different full shelf")
    if (
        _require_sha256(
            binding.get("publicationScopeSha256"),
            label="Windows activation publicationScopeSha256",
        )
        != _require_sha256(
            publication_scope_sha256,
            label="exact finalized Windows publication scope SHA-256",
        )
    ):
        raise ValueError("Windows activation binding replays another publication scope")
    if (
        _require_sha256(
            binding.get("scopeDecisionSha256"),
            label="Windows activation scopeDecisionSha256",
        )
        != _require_sha256(
            scope_decision_sha256,
            label="finalized Windows scopeDecisionSha256",
        )
    ):
        raise ValueError("Windows activation binding replays another scope decision")
    _require_sha256(
        binding.get("proposalSha256"), label="Windows activation proposalSha256"
    )
    if registry_prepare is not None:
        transaction = _load_windows_transaction_module()
        expected_registry_sha = transaction.SCOPE.validate_registry_prepare_binding(
            registry_prepare
        )
        actual_registry = binding.get("registryPrepare")
        if (
            transaction.SCOPE.validate_registry_prepare_binding(actual_registry)
            != expected_registry_sha
            or actual_registry != registry_prepare
        ):
            raise ValueError(
                "Windows activation binding replays another Registry PREPARE transaction"
            )
    elif "registryPrepare" in binding:
        raise ValueError("Windows activation binding has unexplained Registry PREPARE data")
    journal = binding.get("journal")
    if not isinstance(journal, dict) or set(journal) != {"path", "sha256", "sizeBytes"}:
        raise ValueError("Windows activation journal reference is invalid")
    if not isinstance(journal.get("path"), str) or not Path(journal["path"]).is_absolute():
        raise ValueError("Windows activation journal path must be absolute")
    _require_sha256(journal.get("sha256"), label="Windows activation journal SHA-256")
    if (
        isinstance(journal.get("sizeBytes"), bool)
        or not isinstance(journal.get("sizeBytes"), int)
        or not (1 <= journal["sizeBytes"] <= 1024 * 1024)
    ):
        raise ValueError("Windows activation journal size is invalid")
    candidate = binding.get("runUploadCandidate")
    if not isinstance(candidate, dict):
        raise ValueError("Windows activation Run candidate is invalid")
    _validate_run_candidate(
        candidate,
        release_version=release_version,
        frozen_canonical_manifest_sha256=candidate.get("canonicalManifestSha256", ""),
        frozen_inventory_sha256=candidate.get("inventorySha256", ""),
        frozen_file_count=candidate.get("fileCount"),
        frozen_total_bytes=candidate.get("totalBytes"),
    )
    run_upload_paths = binding.get("runUploadPaths")
    try:
        run_upload_paths = _load_windows_transaction_module().validate_run_upload_paths(
            run_upload_paths
        )
    except (RuntimeError, ValueError) as exc:
        raise ValueError("Windows activation Run upload path allowlist is invalid") from exc
    if candidate.get("fileCount") != len(run_upload_paths):
        raise ValueError("Windows activation Run upload path allowlist is invalid")
    proofs = binding.get("activationProofs")
    if not isinstance(proofs, list) or not (1 <= len(proofs) <= 64):
        raise ValueError("Windows activation proof set is invalid")
    expected_proof_keys = {
        "fullShelfInventorySha256",
        "generationPath",
        "generationReceiptSha256",
        "incumbentInventorySha256",
        "index",
        "path",
        "preparedInventorySha256",
        "sha256",
        "sizeBytes",
        "target",
    }
    targets: set[str] = set()
    for index, proof in enumerate(proofs):
        if not isinstance(proof, dict) or set(proof) != expected_proof_keys:
            raise ValueError("Windows activation proof row is invalid")
        if proof.get("index") != index:
            raise ValueError("Windows activation proof order is invalid")
        if proof.get("fullShelfInventorySha256") != expected_full_inventory:
            raise ValueError("Windows activation proof targets another full shelf")
        for key in (
            "generationReceiptSha256",
            "incumbentInventorySha256",
            "preparedInventorySha256",
            "sha256",
        ):
            _require_sha256(proof.get(key), label=f"Windows activation proof {key}")
        for key in ("generationPath", "path", "target"):
            if not isinstance(proof.get(key), str) or not Path(proof[key]).is_absolute():
                raise ValueError(f"Windows activation proof {key} must be absolute")
        if proof["target"] in targets:
            raise ValueError("Windows activation proof target is replayed")
        targets.add(proof["target"])
        if (
            isinstance(proof.get("sizeBytes"), bool)
            or not isinstance(proof.get("sizeBytes"), int)
            or not (1 <= proof["sizeBytes"] <= 256 * 1024)
        ):
            raise ValueError("Windows activation proof size is invalid")
    return binding


def _validate_run_completion(
    completion: object, state_history: object
) -> None:
    if not isinstance(completion, dict) or set(completion) != RUN_COMPLETION_KEYS:
        raise ValueError("Run upload receipt completion has an unexpected property set")
    if completion.get("state") != "completed":
        raise ValueError("Run upload receipt is not terminally completed")
    request_started_at = _require_utc_timestamp(
        completion.get("requestStartedAtUtc"),
        label="Run completion requestStartedAtUtc",
    )
    last_updated_at = _require_utc_timestamp(
        completion.get("lastUpdatedAtUtc"), label="Run completion lastUpdatedAtUtc"
    )
    for field in ("lastHttpStatus", "lastProblemType", "traceId"):
        if completion.get(field) is not None and not isinstance(completion[field], str):
            raise ValueError(f"Run completion {field} must be a string or null")

    if not isinstance(state_history, list) or not (4 <= len(state_history) <= 33):
        raise ValueError("Run upload receipt stateHistory is invalid")
    states: list[str] = []
    timestamps: list[str] = []
    for row in state_history:
        if not isinstance(row, dict) or set(row) != RUN_HISTORY_KEYS:
            raise ValueError("Run upload receipt stateHistory row is invalid")
        state = row.get("state")
        if not isinstance(state, str) or state not in RUN_COMPLETION_TRANSITIONS:
            raise ValueError("Run upload receipt stateHistory state is invalid")
        states.append(state)
        timestamps.append(
            _require_utc_timestamp(row.get("atUtc"), label="Run stateHistory atUtc")
        )
    if states[0] != "created" or states[-1] != "completed":
        raise ValueError("Run upload receipt stateHistory is not terminally completed")
    for previous, current in zip(states, states[1:]):
        if current not in RUN_COMPLETION_TRANSITIONS[previous]:
            raise ValueError("Run upload receipt stateHistory transition is invalid")
    request_started_index = states.index("request_started")
    if request_started_at != timestamps[request_started_index]:
        raise ValueError("Run completion requestStartedAtUtc is not history-bound")
    if last_updated_at != timestamps[-1]:
        raise ValueError("Run completion lastUpdatedAtUtc is not history-bound")


def _validate_run_upload_receipt(
    payload: object,
    *,
    receipt_path: str,
    receipt_actual_sha256: str,
    expected_receipt_sha256: str,
    expected_api_origin: str,
    expected_session_id: str,
    release_version: str,
    frozen_canonical_manifest_sha256: str,
    frozen_inventory_sha256: str,
    frozen_file_count: int,
    frozen_total_bytes: int,
) -> dict[str, Any]:
    if not isinstance(payload, dict) or set(payload) != RUN_UPLOAD_RECEIPT_KEYS:
        raise ValueError("Run upload receipt has an unexpected top-level property set")
    if payload.get("schemaVersion") != RUN_UPLOAD_RECEIPT_SCHEMA:
        raise ValueError("Run upload receipt schemaVersion is not authoritative")
    actual_sha256 = _require_sha256(
        receipt_actual_sha256, label="Run upload receipt actual SHA-256"
    )
    expected_sha256 = _require_sha256(
        expected_receipt_sha256, label="expected Run upload receipt SHA-256"
    )
    if actual_sha256 != expected_sha256:
        raise ValueError("Run upload receipt SHA-256 does not match the expected binding")
    if not receipt_path.strip() or not Path(receipt_path).is_absolute():
        raise ValueError("Run upload receipt path must be absolute")

    api_origin = _require_canonical_origin(
        payload.get("apiOrigin"), label="Run upload receipt apiOrigin"
    )
    target_origin = _require_canonical_origin(
        expected_api_origin, label="expected Run upload target origin"
    )
    if api_origin != target_origin:
        raise ValueError("Run upload receipt apiOrigin does not match the target origin")
    session_id = _require_session_id(
        payload.get("sessionId"), label="Run upload receipt sessionId"
    )
    target_session_id = _require_session_id(
        expected_session_id, label="expected Run upload sessionId"
    )
    if session_id != target_session_id:
        raise ValueError("Run upload receipt sessionId does not match the expected session")

    expires_at = payload.get("expiresAtUtc")
    if expires_at is not None:
        _require_utc_timestamp(expires_at, label="Run upload receipt expiresAtUtc")
    _validate_run_candidate(
        payload.get("candidate"),
        release_version=release_version,
        frozen_canonical_manifest_sha256=frozen_canonical_manifest_sha256,
        frozen_inventory_sha256=frozen_inventory_sha256,
        frozen_file_count=frozen_file_count,
        frozen_total_bytes=frozen_total_bytes,
    )
    _validate_run_completion(payload.get("completion"), payload.get("stateHistory"))
    return payload


def _read_receipt_bytes(
    path_value: str, *, label: str, max_bytes: int
) -> tuple[Path, bytes]:
    path = Path(path_value)
    if path.is_symlink():
        raise ValueError(f"{label} must not be a symbolic link")
    resolved = path.resolve(strict=True)
    descriptor = os.open(
        resolved, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    )
    try:
        metadata = os.fstat(descriptor)
        if not stat.S_ISREG(metadata.st_mode):
            raise ValueError(f"{label} must be a regular file")
        if metadata.st_nlink != 1:
            raise ValueError(f"{label} must not be multiply linked")
        if (
            metadata.st_uid != os.geteuid()
            or stat.S_IMODE(metadata.st_mode) != 0o600
        ):
            raise ValueError(
                f"{label} must be owned by the current user with mode 0600"
            )
        if metadata.st_size > max_bytes:
            raise ValueError(f"{label} is unexpectedly large")
        with os.fdopen(descriptor, "rb", closefd=True) as handle:
            descriptor = -1
            payload = handle.read(max_bytes + 1)
        if len(payload) > max_bytes:
            raise ValueError(f"{label} is unexpectedly large")
        return resolved, payload
    finally:
        if descriptor >= 0:
            os.close(descriptor)


def _write_json_exclusive_atomic(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() or path.is_symlink():
        raise ValueError("Windows publication status already exists; refusing to overwrite it")
    rendered = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary_path = Path(temporary_name)
    linked = False
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb", closefd=True) as handle:
            handle.write(rendered)
            handle.flush()
            os.fsync(handle.fileno())
        os.link(temporary_path, path)
        linked = True
        directory_flags = os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
        directory_descriptor = os.open(path.parent, directory_flags)
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    except BaseException as exc:
        if linked:
            try:
                path.unlink()
                directory_descriptor = os.open(
                    path.parent,
                    os.O_RDONLY | getattr(os, "O_DIRECTORY", 0),
                )
                try:
                    os.fsync(directory_descriptor)
                finally:
                    os.close(directory_descriptor)
            except BaseException as rollback_exc:
                raise RuntimeError(
                    "publication receipt creation failed and durable cleanup failed"
                ) from rollback_exc
        raise
    finally:
        try:
            os.close(descriptor)
        except OSError:
            pass
        temporary_path.unlink(missing_ok=True)


def _unlink_durable(path: Path) -> None:
    path.unlink(missing_ok=True)
    directory_descriptor = os.open(
        path.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
    )
    try:
        os.fsync(directory_descriptor)
    finally:
        os.close(directory_descriptor)


def _write_json_replace_atomic(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.is_symlink() or (path.exists() and not path.is_file()):
        raise ValueError("Windows publication current receipt is not a regular file")
    previous = path.read_bytes() if path.exists() else None
    rendered = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode("utf-8")

    def install(value: bytes) -> None:
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{path.name}.", dir=path.parent
        )
        temporary_path = Path(temporary_name)
        try:
            os.fchmod(descriptor, 0o600)
            with os.fdopen(descriptor, "wb", closefd=True) as handle:
                descriptor = -1
                handle.write(value)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary_path, path)
        finally:
            if descriptor >= 0:
                os.close(descriptor)
            temporary_path.unlink(missing_ok=True)

    installed = False
    try:
        install(rendered)
        installed = True
        directory_descriptor = os.open(
            path.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
        )
        try:
            os.fsync(directory_descriptor)
        finally:
            os.close(directory_descriptor)
    except BaseException as exc:
        if installed:
            try:
                if previous is None:
                    path.unlink(missing_ok=True)
                else:
                    install(previous)
                directory_descriptor = os.open(
                    path.parent, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
                )
                try:
                    os.fsync(directory_descriptor)
                finally:
                    os.close(directory_descriptor)
            except BaseException as rollback_exc:
                raise RuntimeError(
                    "current publication receipt update failed and rollback failed"
                ) from rollback_exc
        raise


def build_receipt(
    *,
    deploy_dir: str,
    release_version: str,
    release_channel: str,
    promoted_artifact_count: int,
    deploy_mode: bool,
    live_verify_target: str,
    require_external_publish: bool,
    windows_publication_scope: dict[str, Any] | None = None,
    windows_publication_scope_sha256: str = "",
    run_upload_receipt_bytes: bytes | None = None,
    run_upload_receipt_path: str = "",
    expected_run_upload_receipt_sha256: str = "",
    expected_run_api_origin: str = "",
    expected_run_session_id: str = "",
    frozen_canonical_manifest_sha256: str = "",
    frozen_inventory_sha256: str = "",
    frozen_file_count: int | None = None,
    frozen_total_bytes: int | None = None,
    hub_postdeploy_receipt_bytes: bytes | None = None,
    hub_postdeploy_receipt_path: str = "",
    expected_hub_postdeploy_receipt_sha256: str = "",
    windows_activation_binding: dict[str, Any] | None = None,
) -> dict[str, Any]:
    live_verify_target = live_verify_target.strip()
    legacy_receipt = windows_publication_scope is None
    run_inputs = (
        run_upload_receipt_bytes is not None,
        bool(run_upload_receipt_path.strip()),
        bool(expected_run_upload_receipt_sha256),
        bool(expected_run_api_origin),
        bool(expected_run_session_id),
        bool(frozen_canonical_manifest_sha256),
        bool(frozen_inventory_sha256),
        frozen_file_count is not None,
        frozen_total_bytes is not None,
    )
    if any(run_inputs) and not all(run_inputs):
        raise ValueError("Run upload receipt and every frozen binding must be supplied together")
    run_verified = False
    run_receipt_actual_sha256 = ""
    run_candidate_binding: dict[str, Any] | None = None
    if all(run_inputs):
        if not isinstance(run_upload_receipt_bytes, bytes):
            raise ValueError("Run upload receipt bytes are invalid")
        if len(run_upload_receipt_bytes) > 64 * 1024:
            raise ValueError("Run upload receipt exceeds the enrolled size limit")
        run_receipt_actual_sha256 = hashlib.sha256(run_upload_receipt_bytes).hexdigest()
        run_upload_receipt = _load_json_object_exact(
            run_upload_receipt_bytes, label="Run upload receipt"
        )
        _validate_run_upload_receipt(
            run_upload_receipt,
            receipt_path=run_upload_receipt_path,
            receipt_actual_sha256=run_receipt_actual_sha256,
            expected_receipt_sha256=expected_run_upload_receipt_sha256,
            expected_api_origin=expected_run_api_origin,
            expected_session_id=expected_run_session_id,
            release_version=release_version,
            frozen_canonical_manifest_sha256=frozen_canonical_manifest_sha256,
            frozen_inventory_sha256=frozen_inventory_sha256,
            frozen_file_count=frozen_file_count,
            frozen_total_bytes=frozen_total_bytes,
        )
        run_candidate_binding = dict(run_upload_receipt["candidate"])
        run_verified = True

    hub_inputs = (
        hub_postdeploy_receipt_bytes is not None,
        bool(hub_postdeploy_receipt_path.strip()),
        bool(expected_hub_postdeploy_receipt_sha256),
    )
    if any(hub_inputs) and not all(hub_inputs):
        raise ValueError("Hub postdeploy receipt path and SHA-256 binding must be supplied together")
    hub_reference_digest_verified = False
    hub_receipt_actual_sha256 = ""
    if all(hub_inputs):
        if not isinstance(hub_postdeploy_receipt_bytes, bytes):
            raise ValueError("Hub postdeploy receipt bytes are invalid")
        if len(hub_postdeploy_receipt_bytes) > 1024 * 1024:
            raise ValueError("Hub postdeploy receipt exceeds the reference size limit")
        if not Path(hub_postdeploy_receipt_path).is_absolute():
            raise ValueError("Hub postdeploy receipt path must be absolute")
        hub_receipt_actual_sha256 = hashlib.sha256(
            hub_postdeploy_receipt_bytes
        ).hexdigest()
        hub_expected_sha256 = _require_sha256(
            expected_hub_postdeploy_receipt_sha256,
            label="expected Hub postdeploy receipt SHA-256",
        )
        if hub_receipt_actual_sha256 != hub_expected_sha256:
            raise ValueError(
                "Hub postdeploy receipt SHA-256 does not match the expected reference"
            )
        hub_reference_digest_verified = True

    # There is no enrolled Hub postdeploy schema in this consumer. A matching URL,
    # deploy mode, or opaque file digest cannot prove public-edge convergence.
    hub_convergence_verified = False
    external_verified = (
        deploy_mode and bool(live_verify_target)
        if legacy_receipt
        else run_verified and hub_convergence_verified
    )
    if external_verified:
        scope = "external_downloads_publish_verified"
        status = "passed"
        summary = (
            "Desktop artifacts were published through an external deploy lane and "
            "the configured live downloads endpoint was verified."
        )
    elif require_external_publish:
        scope = "local_downloads_shelf_only"
        status = "blocked"
        if legacy_receipt:
            summary = (
                "Only a local downloads shelf was updated. External desktop artifact "
                "publication was required but no verified external publish lane ran."
            )
        elif not run_verified:
            summary = (
                "External publication was required, but no exact completed Run upload "
                "receipt was verified against the frozen generation."
            )
        else:
            summary = (
                "The completed Run upload receipt was verified, but authoritative Hub "
                "postdeploy convergence remains unavailable because no exact Hub receipt "
                "schema is enrolled."
            )
    else:
        scope = "local_downloads_shelf_only"
        status = "passed"
        summary = (
            "A local downloads shelf was updated and verified. This is not an "
            "external desktop artifact upload."
        )

    receipt = {
        "schema": (
            "chummer.downloads.publication_scope.v2"
            if windows_publication_scope is not None
            else "chummer.downloads.publication_scope.v1"
        ),
        "generatedAt": _utc_now(),
        "status": status,
        "scope": scope,
        "releaseVersion": release_version,
        "releaseChannel": release_channel,
        "deployDir": deploy_dir,
        "promotedArtifactCount": promoted_artifact_count,
        "deployMode": deploy_mode,
        "liveVerifyTarget": live_verify_target,
        "externalArtifactPublishVerified": external_verified,
        "requireExternalPublish": require_external_publish,
        "summary": summary,
    }
    if windows_publication_scope is not None:
        if (
            windows_publication_scope.get("contractName")
            != "chummer6-ui.preview-nightly-windows-publication-scope"
            or windows_publication_scope.get("contractVersion") != 2
            or windows_publication_scope.get("status") != "validated"
            or windows_publication_scope.get("publicationEligible") is not False
            or windows_publication_scope.get("registryFinalizeEligible") is not True
            or windows_publication_scope.get("approvalIndependent") is not True
            or windows_publication_scope.get("authenticodeRequired") is not True
            or windows_publication_scope.get("uploadAuthorized") is not False
            or windows_publication_scope.get("deployAuthorized") is not False
        ):
            raise ValueError("invalid finalized Windows-only publication scope")
        activation_binding = _validate_activation_binding(
            windows_activation_binding,
            release_version=release_version,
            full_shelf_inventory_sha256=windows_publication_scope.get(
                "fullShelfInventorySha256", ""
            ),
            publication_scope_sha256=windows_publication_scope_sha256,
            scope_decision_sha256=windows_publication_scope.get(
                "scopeDecisionSha256", ""
            ),
            registry_prepare=windows_publication_scope.get("registryPrepare"),
        )
        if (
            run_candidate_binding is not None
            and activation_binding["runUploadCandidate"] != run_candidate_binding
        ):
            raise ValueError(
                "Run upload receipt candidate differs from the activated transaction"
            )
        receipt["windowsOnlyActivation"] = activation_binding
        receipt["transactionCommitRequired"] = True
        receipt["transactionCommitState"] = "awaiting_exact_commit_record"
        receipt["windowsOnlyPublicationScope"] = {
            field: windows_publication_scope[field]
            for field in (
                "fullShelfInventorySha256",
                "fullShelfManifestSha256",
                "incumbentSnapshotSha256",
                "nativeEvidenceSha256",
                "scopeDecisionSha256",
                "signingReceiptSha256",
            )
        }
        if windows_publication_scope.get("registryPrepare") is not None:
            registry_prepare = windows_publication_scope["registryPrepare"]
            receipt["windowsOnlyPublicationScope"].update(
                {
                    "registryPrepareCandidateReceiptSha256": registry_prepare[
                        "candidateReceiptSha256"
                    ],
                    "registryPrepareOutputInventorySha256": registry_prepare[
                        "outputInventorySha256"
                    ],
                    "registryPrepareSha256": _load_windows_transaction_module()
                    .SCOPE.canonical_sha256(registry_prepare),
                }
            )
        receipt["windowsOnlyPublicationScope"].update(
            {
                "approvalIndependent": True,
                "authenticodeRequired": True,
                "registryFinalizeEligible": True,
                "uiPublicationEligible": False,
                "producerDeployAuthorized": False,
                "producerUploadAuthorized": False,
                "publicationScopeSha256": windows_publication_scope_sha256,
                "publicationDeltaPlatforms": sorted(
                    {
                        row.get("platform")
                        for row in windows_publication_scope.get(
                            "publicationDeltaTuples", []
                        )
                        if isinstance(row, dict)
                    }
                ),
                "scope": "windows_only",
            }
        )
        if receipt["windowsOnlyPublicationScope"]["publicationDeltaPlatforms"] != [
            "windows"
        ]:
            raise ValueError("Windows-only publication delta includes another platform")
        if len(windows_publication_scope_sha256) != 64 or any(
            char not in "0123456789abcdef"
            for char in windows_publication_scope_sha256
        ):
            raise ValueError("Windows-only publication scope SHA-256 is invalid")
        receipt["windowsOnlyPublicationScope"].update(
            {
                "externalEvidenceAuthority": "exact_receipts_only",
                "liveVerifyTargetReferenceOnly": True,
                "runUploadReceiptContract": RUN_UPLOAD_RECEIPT_SCHEMA,
                "runUploadReceiptPath": run_upload_receipt_path if run_verified else "",
                "runUploadReceiptSha256": (
                    run_receipt_actual_sha256 if run_verified else ""
                ),
                "runUploadReceiptVerified": run_verified,
                "runCandidateBinding": run_candidate_binding,
                "frozenGenerationBindingVerified": run_verified,
                "runSessionId": expected_run_session_id if run_verified else "",
                "targetOrigin": expected_run_api_origin if run_verified else "",
                "hubPostdeployReceiptPath": (
                    hub_postdeploy_receipt_path
                    if hub_reference_digest_verified
                    else ""
                ),
                "hubPostdeployReceiptSha256": (
                    hub_receipt_actual_sha256
                    if hub_reference_digest_verified
                    else ""
                ),
                "hubPostdeployReferenceDigestVerified": hub_reference_digest_verified,
                "hubPostdeploySchemaEnrolled": False,
                "hubPostdeployBindingVerified": False,
                "hubConvergenceVerified": False,
                "hubPostdeployAuthorityReason": "no_exact_schema_enrolled",
            }
        )
    return receipt


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--abort-output")
    parser.add_argument("--current-output")
    parser.add_argument("--deploy-dir", required=True)
    parser.add_argument("--release-version", required=True)
    parser.add_argument("--release-channel", required=True)
    parser.add_argument("--promoted-artifact-count", required=True, type=int)
    parser.add_argument("--deploy-mode", action="store_true")
    parser.add_argument("--live-verify-target", default="")
    parser.add_argument("--require-external-publish", action="store_true")
    parser.add_argument("--windows-publication-scope")
    parser.add_argument("--windows-activation-journal")
    parser.add_argument("--run-upload-receipt")
    parser.add_argument("--expected-run-upload-receipt-sha256", default="")
    parser.add_argument("--expected-run-api-origin", default="")
    parser.add_argument("--expected-run-session-id", default="")
    parser.add_argument("--frozen-canonical-manifest-sha256", default="")
    parser.add_argument("--frozen-inventory-sha256", default="")
    parser.add_argument("--frozen-file-count", type=int)
    parser.add_argument("--frozen-total-bytes", type=int)
    parser.add_argument("--hub-postdeploy-receipt")
    parser.add_argument("--expected-hub-postdeploy-receipt-sha256", default="")
    args = parser.parse_args()

    windows_scope = None
    windows_scope_sha256 = ""
    windows_activation_binding = None
    if args.windows_publication_scope:
        scope_path = Path(args.windows_publication_scope)
        scope_bytes = scope_path.read_bytes()
        windows_scope_sha256 = hashlib.sha256(scope_bytes).hexdigest()
        windows_scope = json.loads(scope_bytes.decode("utf-8-sig"))
        if not isinstance(windows_scope, dict):
            raise SystemExit("Windows-only publication scope must be a JSON object")
        if not args.windows_activation_journal:
            raise SystemExit(
                "Windows-only publication requires an exact activation journal"
            )
        transaction = _load_windows_transaction_module()
        windows_activation_binding = transaction.activation_binding_from_journal(
            Path(args.windows_activation_journal), verify_targets=True
        )
    elif args.windows_activation_journal:
        raise SystemExit(
            "Windows activation journal requires a Windows-only publication scope"
        )

    run_receipt_bytes = None
    run_receipt_path = ""
    if args.run_upload_receipt:
        resolved_run_receipt, run_receipt_bytes = _read_receipt_bytes(
            args.run_upload_receipt,
            label="Run upload receipt",
            max_bytes=64 * 1024,
        )
        run_receipt_path = str(resolved_run_receipt)

    hub_receipt_bytes = None
    hub_receipt_path = ""
    if args.hub_postdeploy_receipt:
        resolved_hub_receipt, hub_receipt_bytes = _read_receipt_bytes(
            args.hub_postdeploy_receipt,
            label="Hub postdeploy receipt",
            max_bytes=1024 * 1024,
        )
        hub_receipt_path = str(resolved_hub_receipt)

    receipt = build_receipt(
        deploy_dir=args.deploy_dir,
        release_version=args.release_version,
        release_channel=args.release_channel,
        promoted_artifact_count=args.promoted_artifact_count,
        deploy_mode=args.deploy_mode,
        live_verify_target=args.live_verify_target,
        require_external_publish=args.require_external_publish,
        windows_publication_scope=windows_scope,
        windows_publication_scope_sha256=windows_scope_sha256,
        run_upload_receipt_bytes=run_receipt_bytes,
        run_upload_receipt_path=run_receipt_path,
        expected_run_upload_receipt_sha256=args.expected_run_upload_receipt_sha256,
        expected_run_api_origin=args.expected_run_api_origin,
        expected_run_session_id=args.expected_run_session_id,
        frozen_canonical_manifest_sha256=args.frozen_canonical_manifest_sha256,
        frozen_inventory_sha256=args.frozen_inventory_sha256,
        frozen_file_count=args.frozen_file_count,
        frozen_total_bytes=args.frozen_total_bytes,
        hub_postdeploy_receipt_bytes=hub_receipt_bytes,
        hub_postdeploy_receipt_path=hub_receipt_path,
        expected_hub_postdeploy_receipt_sha256=(
            args.expected_hub_postdeploy_receipt_sha256
        ),
        windows_activation_binding=windows_activation_binding,
    )

    output = Path(args.output)
    configured_outputs = [
        path.resolve(strict=False)
        for path in (
            output,
            *( [Path(args.abort_output)] if args.abort_output else [] ),
            *( [Path(args.current_output)] if args.current_output else [] ),
        )
    ]
    if len(configured_outputs) != len(set(configured_outputs)):
        raise ValueError("publication receipt output paths must be distinct")
    if windows_scope is not None and args.current_output:
        raise ValueError(
            "Windows-only current receipt may only be installed from an exact transaction commit record"
        )
    if windows_scope is not None and receipt["status"] == "blocked":
        if args.abort_output:
            abort_receipt = dict(receipt)
            abort_receipt["receiptDisposition"] = (
                "publication_aborted_shelf_rollback_required"
            )
            abort_receipt["transactionCommitState"] = (
                "aborted_shelf_rollback_required"
            )
            _write_json_exclusive_atomic(Path(args.abort_output), abort_receipt)
    elif windows_scope is not None:
        _write_json_exclusive_atomic(output, receipt)
        if args.current_output:
            try:
                _write_json_replace_atomic(Path(args.current_output), receipt)
            except BaseException:
                _unlink_durable(output)
                raise
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(
            json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )

    if receipt["status"] == "blocked":
        raise SystemExit(receipt["summary"])

    print(f"downloads_publication_scope:{receipt['scope']} {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
