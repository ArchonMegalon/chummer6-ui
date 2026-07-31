#!/usr/bin/env python3
"""Create and verify immutable user-journey evidence bundles."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import stat
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


BUNDLE_CONTRACT = "chummer6-ui.user_journey_evidence_bundle"
POINTER_CONTRACT = "chummer6-ui.user_journey_evidence_bundle_pointer"
SCHEMA_VERSION = 1
JSON_LIMIT = 2 * 1024 * 1024
SCREENSHOT_LIMIT = 32 * 1024 * 1024
ARTIFACT_LIMIT = 256 * 1024 * 1024
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
MIN_SCREENSHOT_BYTES = 1024
SINGLETON_ROLES = frozenset(
    {
        "trace",
        "linux_gate",
        "flagship_gate",
        "staged_audit",
        "source_receipt",
        "release_candidate",
        "candidate_artifact",
        "tested_installer",
        "mouse_trace",
    }
)
MULTI_ROLES = frozenset({"workflow_screenshot", "mouse_screenshot"})
ALL_ROLES = SINGLETON_ROLES | MULTI_ROLES
MOUSE_SCREENSHOT_COMPATIBILITY_ALIAS_NAME_GROUPS = (
    frozenset({
        "01-new-character-dialog.png",
        "file_new_character_visible_workspace-before.png",
    }),
    frozenset({
        "03-post-dialog-close.png",
        "04-workspace-opened.png",
        "file_new_character_visible_workspace-after.png",
    }),
    frozenset({
        "05-workspace-saved.png",
        "minimal_character_build_save_reload-before.png",
    }),
)


class BundleError(RuntimeError):
    """Evidence cannot be promoted or verified safely."""


@dataclass(frozen=True)
class VerifiedEntry:
    role: str
    declared_path: str
    path: Path
    data: bytes
    sha256: str
    size_bytes: int


@dataclass(frozen=True)
class VerifiedBundle:
    bundle_id: str
    manifest_path: Path
    manifest_sha256: str
    entries: tuple[VerifiedEntry, ...]

    def single(self, role: str) -> VerifiedEntry:
        matches = self.many(role)
        if len(matches) != 1:
            raise BundleError(f"bundle role {role!r} must have exactly one entry")
        return matches[0]

    def many(self, role: str) -> tuple[VerifiedEntry, ...]:
        return tuple(entry for entry in self.entries if entry.role == role)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _normalized_digest(value: Any) -> str:
    text = str(value or "").strip().lower()
    if text.startswith("sha256:"):
        text = text[7:]
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        return ""
    return text


def _status_ok(value: Any) -> bool:
    return str(value or "").strip().lower() in {"pass", "passed", "ready", "published"}


def _stable_bytes(path: Path, label: str, maximum: int) -> bytes:
    absolute = Path(os.path.abspath(path))
    if not absolute.name:
        raise BundleError(f"{label} must name a regular file: {path}")
    current = Path(absolute.anchor)
    reparse_attribute = int(getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
    for component in absolute.parts[1:]:
        current /= component
        try:
            state = os.stat(current, follow_symlinks=False)
        except FileNotFoundError as exc:
            raise BundleError(f"{label} is missing: {path}") from exc
        except OSError as exc:
            raise BundleError(f"unable to inspect {label} safely: {path}") from exc
        attributes = int(getattr(state, "st_file_attributes", 0))
        if stat.S_ISLNK(state.st_mode) or (reparse_attribute and attributes & reparse_attribute):
            raise BundleError(f"{label} must not traverse symlink or reparse-point components: {path}")

    flags = os.O_RDONLY
    for optional in ("O_CLOEXEC", "O_NOFOLLOW", "O_NONBLOCK"):
        flags |= int(getattr(os, optional, 0))
    try:
        descriptor = os.open(absolute, flags)
    except OSError as exc:
        raise BundleError(f"{label} must be a regular non-symlink file: {path}") from exc
    try:
        before = os.fstat(descriptor)
        path_before = os.stat(absolute, follow_symlinks=False)
        identity_before = (
            before.st_dev,
            before.st_ino,
            before.st_mode,
            before.st_size,
            before.st_mtime_ns,
        )
        path_identity_before = (
            path_before.st_dev,
            path_before.st_ino,
            path_before.st_mode,
            path_before.st_size,
            path_before.st_mtime_ns,
        )
        if not stat.S_ISREG(before.st_mode) or identity_before != path_identity_before:
            raise BundleError(f"{label} must be a regular non-symlink file: {path}")
        if before.st_size < 1 or before.st_size > maximum:
            raise BundleError(f"{label} exceeds its byte safety bound: {path}")
        chunks: list[bytes] = []
        total = 0
        while total <= maximum:
            chunk = os.read(descriptor, min(64 * 1024, maximum + 1 - total))
            if not chunk:
                break
            chunks.append(chunk)
            total += len(chunk)
        data = b"".join(chunks)
        after = os.fstat(descriptor)
        path_after = os.stat(absolute, follow_symlinks=False)
        identity_after = (
            after.st_dev,
            after.st_ino,
            after.st_mode,
            after.st_size,
            after.st_mtime_ns,
        )
        path_identity_after = (
            path_after.st_dev,
            path_after.st_ino,
            path_after.st_mode,
            path_after.st_size,
            path_after.st_mtime_ns,
        )
        if (
            identity_before != identity_after
            or identity_after != path_identity_after
            or len(data) != after.st_size
            or len(data) > maximum
        ):
            raise BundleError(f"{label} changed while being read: {path}")
        return data
    finally:
        os.close(descriptor)


def _json_object(path: Path, label: str) -> tuple[dict[str, Any], bytes]:
    raw = _stable_bytes(path, label, JSON_LIMIT)
    try:
        payload = json.loads(raw.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise BundleError(f"{label} must contain valid UTF-8 JSON: {path}") from exc
    if not isinstance(payload, dict):
        raise BundleError(f"{label} must contain a JSON object: {path}")
    return payload, raw


def _canonical_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def _json_bytes(value: Any) -> bytes:
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def _safe_relative(value: str, label: str) -> PurePosixPath:
    if not value or "\\" in value:
        raise BundleError(f"{label} must be a normalized relative POSIX path")
    relative = PurePosixPath(value)
    if (
        relative.is_absolute()
        or value in {".", ".."}
        or any(part in {"", ".", ".."} for part in relative.parts)
        or relative.as_posix() != value
    ):
        raise BundleError(f"{label} must be a normalized relative POSIX path: {value}")
    return relative


def _declared_path(value: Any, *, base: Path, label: str) -> tuple[str, Path]:
    text = str(value or "").strip()
    if not text or "\\" in text:
        raise BundleError(f"{label} is missing or unsafe")
    posix = PurePosixPath(text)
    if any(part in {".", ".."} for part in posix.parts):
        raise BundleError(f"{label} contains dot path segments: {text}")
    declared = Path(text)
    return text, declared if declared.is_absolute() else base / declared


def _normalized_absolute(value: str | Path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(value)))


def _credible_png(data: bytes) -> bool:
    return (
        len(data) >= MIN_SCREENSHOT_BYTES
        and data.startswith(PNG_SIGNATURE)
        and len(data) >= 33
        and int.from_bytes(data[8:12], "big") == 13
        and data[12:16] == b"IHDR"
        and int.from_bytes(data[16:20], "big") > 0
        and int.from_bytes(data[20:24], "big") > 0
    )


def _entry_row(role: str, declared_path: str, relative_path: str, data: bytes) -> dict[str, Any]:
    _safe_relative(relative_path, f"{role} bundle path")
    return {
        "role": role,
        "declared_path": declared_path,
        "path": relative_path,
        "sha256": _sha256(data),
        "size_bytes": len(data),
    }


def _atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = ""
    try:
        with tempfile.NamedTemporaryFile(
            "wb", dir=path.parent, prefix=f".{path.name}.", suffix=".tmp", delete=False
        ) as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
            temporary = handle.name
        os.replace(temporary, path)
    finally:
        if temporary and os.path.exists(temporary):
            os.unlink(temporary)


def _resolve_manifest_path(pointer_path: Path, value: Any) -> Path:
    relative = _safe_relative(str(value or "").strip(), "bundle pointer manifest_path")
    candidate = pointer_path.parent.joinpath(*relative.parts)
    try:
        candidate.relative_to(pointer_path.parent)
    except ValueError as exc:
        raise BundleError("bundle pointer manifest_path escapes its published root") from exc
    return candidate


def _verify_manifest(
    manifest_path: Path,
    *,
    expected_bundle_id: str = "",
    expected_manifest_sha256: str = "",
) -> VerifiedBundle:
    manifest, manifest_bytes = _json_object(manifest_path, "bundle manifest")
    manifest_sha256 = _sha256(manifest_bytes)
    if expected_manifest_sha256 and manifest_sha256 != expected_manifest_sha256:
        raise BundleError("bundle manifest digest does not match pointer")
    if manifest.get("contract_name") != BUNDLE_CONTRACT or manifest.get("schema_version") != SCHEMA_VERSION:
        raise BundleError("bundle manifest contract or schema is invalid")
    bundle_id = _normalized_digest(manifest.get("bundle_id"))
    if not bundle_id or (expected_bundle_id and bundle_id != expected_bundle_id):
        raise BundleError("bundle manifest id does not match pointer")
    if manifest_path.parent.name != bundle_id:
        raise BundleError("bundle directory basename does not match bundle id")
    raw_entries = manifest.get("entries")
    if not isinstance(raw_entries, list):
        raise BundleError("bundle manifest entries must be an array")
    identity = {
        "contract_name": manifest.get("contract_name"),
        "schema_version": manifest.get("schema_version"),
        "entries": raw_entries,
    }
    if _sha256(_canonical_bytes(identity)) != bundle_id:
        raise BundleError("bundle manifest identity digest is invalid")

    verified: list[VerifiedEntry] = []
    seen_paths: set[str] = set()
    role_counts: dict[str, int] = {}
    declared_by_role: set[tuple[str, str]] = set()
    expected_keys = {"role", "declared_path", "path", "sha256", "size_bytes"}
    for index, raw_entry in enumerate(raw_entries):
        if not isinstance(raw_entry, dict) or set(raw_entry) != expected_keys:
            raise BundleError(f"bundle entry {index} has an invalid shape")
        role = str(raw_entry.get("role") or "").strip()
        declared = str(raw_entry.get("declared_path") or "").strip()
        relative_text = str(raw_entry.get("path") or "").strip()
        expected_digest = _normalized_digest(raw_entry.get("sha256"))
        expected_size = raw_entry.get("size_bytes")
        if role not in ALL_ROLES or not declared or not expected_digest:
            raise BundleError(f"bundle entry {index} has invalid typed bindings")
        if isinstance(expected_size, bool) or not isinstance(expected_size, int) or expected_size < 1:
            raise BundleError(f"bundle entry {index} has invalid size_bytes")
        relative = _safe_relative(relative_text, f"bundle entry {index} path")
        if relative_text in seen_paths or (role, declared) in declared_by_role:
            raise BundleError(f"bundle entry {index} duplicates a path or role declaration")
        seen_paths.add(relative_text)
        declared_by_role.add((role, declared))
        role_counts[role] = role_counts.get(role, 0) + 1
        path = manifest_path.parent.joinpath(*relative.parts)
        data = _stable_bytes(
            path,
            f"bundle entry {role}",
            ARTIFACT_LIMIT if role in {"candidate_artifact", "tested_installer"} else (
                SCREENSHOT_LIMIT if role.endswith("screenshot") else JSON_LIMIT
            ),
        )
        if len(data) != expected_size or _sha256(data) != expected_digest:
            raise BundleError(f"bundle entry digest or size mismatch: {role}:{declared}")
        verified.append(
            VerifiedEntry(
                role=role,
                declared_path=declared,
                path=path,
                data=data,
                sha256=expected_digest,
                size_bytes=expected_size,
            )
        )

    for role in SINGLETON_ROLES:
        if role_counts.get(role) != 1:
            raise BundleError(f"bundle role {role!r} must have exactly one entry")
    if role_counts.get("workflow_screenshot") != 10:
        raise BundleError("bundle must have exactly ten workflow screenshots")
    if not 5 <= role_counts.get("mouse_screenshot", 0) <= 20:
        raise BundleError("bundle must have between five and twenty mouse-first screenshots")
    if set(role_counts) != ALL_ROLES:
        raise BundleError("bundle contains an unknown or missing role")
    return VerifiedBundle(bundle_id, manifest_path, manifest_sha256, tuple(verified))


def verify_bundle(pointer_path: Path) -> VerifiedBundle:
    pointer_path = Path(pointer_path)
    pointer, _ = _json_object(pointer_path, "bundle pointer")
    if (
        pointer.get("contract_name") != POINTER_CONTRACT
        or pointer.get("schema_version") != SCHEMA_VERSION
        or pointer.get("status") != "published"
    ):
        raise BundleError("bundle pointer contract, schema, or status is invalid")
    bundle_id = _normalized_digest(pointer.get("bundle_id"))
    manifest_sha256 = _normalized_digest(pointer.get("manifest_sha256"))
    if not bundle_id or not manifest_sha256:
        raise BundleError("bundle pointer digest bindings are missing or malformed")
    manifest_path = _resolve_manifest_path(pointer_path, pointer.get("manifest_path"))
    return _verify_manifest(
        manifest_path,
        expected_bundle_id=bundle_id,
        expected_manifest_sha256=manifest_sha256,
    )


def create_bundle(
    published_root: Path,
    trace_path: Path,
    linux_gate_path: Path,
    flagship_gate_path: Path,
    staged_audit_path: Path,
    release_candidate_path: Path,
) -> dict[str, Any]:
    published_root = Path(os.path.abspath(published_root))
    trace_path = Path(trace_path)
    linux_gate_path = Path(linux_gate_path)
    flagship_gate_path = Path(flagship_gate_path)
    staged_audit_path = Path(staged_audit_path)
    release_candidate_path = Path(release_candidate_path)
    trace, trace_bytes = _json_object(trace_path, "staged trace")
    gate, gate_bytes = _json_object(linux_gate_path, "staged Linux gate")
    flagship, flagship_bytes = _json_object(flagship_gate_path, "staged flagship gate")
    audit, audit_bytes = _json_object(staged_audit_path, "staged owning audit")
    candidate, candidate_bytes = _json_object(release_candidate_path, "release candidate")
    evidence = audit.get("evidence") if isinstance(audit.get("evidence"), dict) else {}
    if not _status_ok(audit.get("status")) or evidence.get("release_candidate_binding_status") != "pass":
        raise BundleError("staged owning audit and candidate binding must pass")
    for label, raw, key in (
        ("trace", trace_bytes, "trace_sha256"),
        ("trace after audit", trace_bytes, "trace_sha256_after_audit"),
        ("Linux gate", gate_bytes, "linux_gate_sha256"),
        ("release candidate", candidate_bytes, "release_candidate_sha256"),
    ):
        if _normalized_digest(evidence.get(key)) != _sha256(raw):
            raise BundleError(f"staged {label} bytes do not match the owning audit")
    flagship_binding = _normalized_digest(evidence.get("flagship_gate_sha256"))
    if not flagship_binding or flagship_binding != _sha256(flagship_bytes):
        raise BundleError("staged flagship gate bytes do not match the owning audit")
    if evidence.get("mouse_first_evidence_binding_status") != "pass":
        raise BundleError("staged mouse-first evidence binding is not passing")
    if not _status_ok(trace.get("status")) or not _status_ok(gate.get("status")) or not _status_ok(flagship.get("status")):
        raise BundleError("trace, Linux gate, and flagship gate must all pass")

    mouse_first = gate.get("mouse_first_journey") if isinstance(gate.get("mouse_first_journey"), dict) else {}
    primary = mouse_first.get("primary") if isinstance(mouse_first.get("primary"), dict) else {}
    embedded_receipt = primary.get("receipt") if isinstance(primary.get("receipt"), dict) else {}
    source_receipt_declared, source_receipt_path = _declared_path(
        primary.get("receipt_path"), base=linux_gate_path.parent, label="source receipt path"
    )
    source_receipt, source_receipt_bytes = _json_object(source_receipt_path, "source mouse receipt")
    if source_receipt != embedded_receipt:
        raise BundleError("source mouse receipt bytes do not match the embedded receipt")
    if _normalized_absolute(trace.get("source_mouse_receipt_path") or "") != _normalized_absolute(source_receipt_declared):
        raise BundleError("trace and Linux gate source receipt declarations disagree")
    if _normalized_digest(trace.get("source_mouse_receipt_sha256")) != _sha256(source_receipt_bytes):
        raise BundleError("trace source receipt digest does not match source bytes")

    artifacts = candidate.get("artifacts") if isinstance(candidate.get("artifacts"), list) else []
    linux_artifacts = [
        row
        for row in artifacts
        if isinstance(row, dict)
        and str(row.get("head") or "").lower() == "avalonia"
        and str(row.get("platform") or "").lower() == "linux"
        and str(row.get("rid") or "").lower() == "linux-x64"
        and str(row.get("kind") or "").lower() == "installer"
    ]
    if len(linux_artifacts) != 1:
        raise BundleError("release candidate must contain one Avalonia linux-x64 installer")
    artifact = linux_artifacts[0]
    artifact_name = str(artifact.get("fileName") or "").strip()
    if not artifact_name or Path(artifact_name).name != artifact_name:
        raise BundleError("release candidate installer fileName is unsafe")
    artifact_digest = _normalized_digest(artifact.get("sha256"))
    artifact_size = artifact.get("sizeBytes")
    if not artifact_digest or isinstance(artifact_size, bool) or not isinstance(artifact_size, int):
        raise BundleError("release candidate installer digest or size is invalid")
    gate_release = gate.get("release_channel") if isinstance(gate.get("release_channel"), dict) else {}
    if _normalized_absolute(gate_release.get("path") or "") != _normalized_absolute(release_candidate_path):
        raise BundleError("Linux gate release candidate declaration disagrees with promotion input")
    candidate_artifact_path = release_candidate_path.parent / "files" / artifact_name
    if _normalized_absolute(gate_release.get("local_desktop_files_root") or "") != _normalized_absolute(candidate_artifact_path.parent):
        raise BundleError("Linux gate candidate files root disagrees with release candidate")
    tested_declared, tested_installer_path = _declared_path(
        gate_release.get("installer_smoke_artifact_path"),
        base=linux_gate_path.parent,
        label="tested installer path",
    )
    candidate_artifact_bytes = _stable_bytes(candidate_artifact_path, "candidate artifact", ARTIFACT_LIMIT)
    tested_installer_bytes = _stable_bytes(tested_installer_path, "tested installer", ARTIFACT_LIMIT)
    if (
        _sha256(candidate_artifact_bytes) != artifact_digest
        or _sha256(tested_installer_bytes) != artifact_digest
        or len(candidate_artifact_bytes) != artifact_size
        or len(tested_installer_bytes) != artifact_size
        or candidate_artifact_bytes != tested_installer_bytes
    ):
        raise BundleError("candidate artifact and independently read tested installer bytes disagree")

    screenshot_root_text = str(
        primary.get("screenshot_dir")
        or embedded_receipt.get("screenshotDirectory")
        or evidence.get("screenshot_dir")
        or ""
    ).strip()
    if not screenshot_root_text:
        raise BundleError("workflow screenshot root is not bound by staged evidence")
    screenshot_root = Path(screenshot_root_text)
    workflows = trace.get("workflows")
    if not isinstance(workflows, list) or len(workflows) != 5:
        raise BundleError("trace must contain exactly five workflows")

    sources: list[tuple[str, str, str, bytes]] = [
        ("trace", str(trace_path), "evidence/trace.json", trace_bytes),
        ("linux_gate", str(linux_gate_path), "evidence/linux-gate.json", gate_bytes),
        ("flagship_gate", str(flagship_gate_path), "evidence/flagship-gate.json", flagship_bytes),
        ("staged_audit", str(staged_audit_path), "evidence/staged-audit.json", audit_bytes),
        ("source_receipt", source_receipt_declared, "evidence/source-receipt.json", source_receipt_bytes),
        ("release_candidate", str(release_candidate_path), "evidence/release-candidate.json", candidate_bytes),
        ("candidate_artifact", str(candidate_artifact_path), f"artifacts/candidate/{artifact_name}", candidate_artifact_bytes),
        ("tested_installer", tested_declared, f"artifacts/tested/{artifact_name}", tested_installer_bytes),
    ]
    screenshot_digests: set[str] = set()
    seen_workflow_paths: set[str] = set()
    for workflow in workflows:
        if not isinstance(workflow, dict):
            raise BundleError("trace workflow rows must be JSON objects")
        paths = workflow.get("screenshots")
        hashes = workflow.get("screenshot_sha256")
        if not isinstance(paths, list) or len(paths) != 2 or not isinstance(hashes, dict):
            raise BundleError("each trace workflow must bind exactly two screenshot hashes")
        for raw_path in paths:
            declared = str(raw_path or "").strip()
            relative = _safe_relative(declared, "workflow screenshot path")
            if declared in seen_workflow_paths:
                raise BundleError("workflow screenshot paths must be unique")
            seen_workflow_paths.add(declared)
            data = _stable_bytes(
                screenshot_root.joinpath(*relative.parts), "workflow screenshot", SCREENSHOT_LIMIT
            )
            digest = _sha256(data)
            if not _credible_png(data) or _normalized_digest(hashes.get(declared)) != digest:
                raise BundleError(f"workflow screenshot is not credible or digest-bound: {declared}")
            if digest in screenshot_digests:
                raise BundleError("workflow screenshot content must be unique")
            screenshot_digests.add(digest)
            sources.append(
                ("workflow_screenshot", declared, f"workflow-screenshots/{relative.as_posix()}", data)
            )
    if len(seen_workflow_paths) != 10:
        raise BundleError("trace must bind exactly ten workflow screenshots")

    mouse_paths = source_receipt.get("screenshotPaths")
    if not isinstance(mouse_paths, list) or not 5 <= len(mouse_paths) <= 20:
        raise BundleError("source receipt must bind between five and twenty mouse-first screenshots")
    staged_mouse_reviews = evidence.get("mouse_first_screenshot_reviews")
    if not isinstance(staged_mouse_reviews, list) or len(staged_mouse_reviews) != len(mouse_paths):
        raise BundleError("staged audit mouse-first screenshot inventory is incomplete")
    staged_mouse_reviews_by_path = {
        str(row.get("declared_path") or "").strip(): row
        for row in staged_mouse_reviews
        if isinstance(row, dict) and str(row.get("declared_path") or "").strip()
    }
    if len(staged_mouse_reviews_by_path) != len(mouse_paths):
        raise BundleError("staged audit mouse-first screenshot declarations are not unique")
    mouse_directory_text = str(source_receipt.get("screenshotDirectory") or "").strip()
    mouse_directory = Path(mouse_directory_text) if mouse_directory_text else source_receipt_path.parent
    seen_mouse_declarations: set[str] = set()
    mouse_screenshot_digests: set[str] = set()
    mouse_digest_declarations: dict[str, list[str]] = {}
    for index, raw_path in enumerate(mouse_paths):
        declared, resolved = _declared_path(
            raw_path, base=mouse_directory, label="mouse-first screenshot path"
        )
        if declared in seen_mouse_declarations:
            raise BundleError("mouse-first screenshot paths must be unique")
        seen_mouse_declarations.add(declared)
        data = _stable_bytes(resolved, "mouse-first screenshot", SCREENSHOT_LIMIT)
        digest = _sha256(data)
        if not _credible_png(data):
            raise BundleError("mouse-first screenshots must be credible")
        staged_review = staged_mouse_reviews_by_path.get(declared)
        if (
            not isinstance(staged_review, dict)
            or _normalized_digest(staged_review.get("sha256")) != digest
            or staged_review.get("size_bytes") != len(data)
            or staged_review.get("is_png") is not True
        ):
            raise BundleError(
                f"mouse-first screenshot bytes do not match the staged owning audit: {declared}"
            )
        mouse_screenshot_digests.add(digest)
        mouse_digest_declarations.setdefault(digest, []).append(declared)
        sources.append(
            ("mouse_screenshot", declared, f"mouse-first-screenshots/{index:02d}-{resolved.name}", data)
        )
    compatibility_alias_groups: list[list[str]] = []
    unexpected_duplicate_groups: list[list[str]] = []
    for duplicate_paths in mouse_digest_declarations.values():
        if len(duplicate_paths) < 2:
            continue
        duplicate_names = frozenset(Path(path).name for path in duplicate_paths)
        normalized_group = sorted(duplicate_paths)
        if any(
            duplicate_names.issubset(allowed_group)
            for allowed_group in MOUSE_SCREENSHOT_COMPATIBILITY_ALIAS_NAME_GROUPS
        ):
            compatibility_alias_groups.append(normalized_group)
        else:
            unexpected_duplicate_groups.append(normalized_group)
    compatibility_alias_groups.sort()
    unexpected_duplicate_groups.sort()
    audit_compatibility_alias_groups = evidence.get("mouse_first_compatibility_alias_groups")
    audit_unexpected_duplicate_groups = evidence.get("mouse_first_unexpected_duplicate_groups")
    if not isinstance(audit_compatibility_alias_groups, list) or not isinstance(
        audit_unexpected_duplicate_groups, list
    ):
        raise BundleError("staged audit does not publish mouse-first compatibility alias review")
    normalized_audit_compatibility_groups = sorted(
        sorted(str(path or "").strip() for path in group)
        for group in audit_compatibility_alias_groups
        if isinstance(group, list)
    )
    if (
        len(mouse_screenshot_digests) < 5
        or unexpected_duplicate_groups
        or audit_unexpected_duplicate_groups
        or normalized_audit_compatibility_groups != compatibility_alias_groups
    ):
        raise BundleError(
            "mouse-first screenshot content must be distinct except for exact audited compatibility aliases"
        )
    mouse_trace_declared, mouse_trace_path = _declared_path(
        source_receipt.get("tracePath"), base=source_receipt_path.parent, label="mouse-first trace path"
    )
    mouse_trace, mouse_trace_bytes = _json_object(mouse_trace_path, "mouse-first trace")
    if "status" in mouse_trace and not _status_ok(mouse_trace.get("status")):
        raise BundleError("mouse-first trace is not passing")
    staged_mouse_trace_review = evidence.get("mouse_first_trace_review")
    if (
        not isinstance(staged_mouse_trace_review, dict)
        or str(staged_mouse_trace_review.get("declared_path") or "").strip()
        != mouse_trace_declared
        or _normalized_digest(staged_mouse_trace_review.get("sha256"))
        != _sha256(mouse_trace_bytes)
        or staged_mouse_trace_review.get("size_bytes") != len(mouse_trace_bytes)
        or staged_mouse_trace_review.get("valid_json_object") is not True
    ):
        raise BundleError("mouse-first trace bytes do not match the staged owning audit")
    sources.append(("mouse_trace", mouse_trace_declared, "mouse-first/trace.json", mouse_trace_bytes))

    entry_rows = sorted(
        (_entry_row(role, declared, relative, data) for role, declared, relative, data in sources),
        key=lambda row: (row["role"], row["declared_path"], row["path"]),
    )
    identity = {
        "contract_name": BUNDLE_CONTRACT,
        "schema_version": SCHEMA_VERSION,
        "entries": entry_rows,
    }
    bundle_id = _sha256(_canonical_bytes(identity))
    manifest = {**identity, "bundle_id": bundle_id}
    manifest_bytes = _json_bytes(manifest)
    bundles_root = published_root / "user-journey-tester-bundles"
    bundle_dir = bundles_root / bundle_id
    manifest_path = bundle_dir / "BUNDLE_MANIFEST.generated.json"
    source_by_relative = {relative: data for _, _, relative, data in sources}
    bundles_root.mkdir(parents=True, exist_ok=True)
    if not bundle_dir.exists():
        temporary_dir = bundles_root / f".{bundle_id}.{uuid.uuid4().hex}.tmp"
        temporary_dir.mkdir()
        try:
            for relative, data in source_by_relative.items():
                destination = temporary_dir.joinpath(*_safe_relative(relative, "bundle output path").parts)
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(data)
            (temporary_dir / manifest_path.name).write_bytes(manifest_bytes)
            os.replace(temporary_dir, bundle_dir)
        finally:
            if temporary_dir.exists():
                shutil.rmtree(temporary_dir)
    else:
        existing_manifest = _stable_bytes(manifest_path, "existing bundle manifest", JSON_LIMIT)
        if existing_manifest != manifest_bytes:
            raise BundleError("immutable bundle id already exists with different manifest bytes")
        for relative, data in source_by_relative.items():
            existing = _stable_bytes(
                bundle_dir.joinpath(*_safe_relative(relative, "bundle output path").parts),
                "existing bundle entry",
                ARTIFACT_LIMIT,
            )
            if existing != data:
                raise BundleError("immutable bundle id already exists with different evidence bytes")

    verified = _verify_manifest(manifest_path, expected_bundle_id=bundle_id)
    pointer_path = published_root / "USER_JOURNEY_TESTER_EVIDENCE_BUNDLE.generated.json"
    manifest_relative = manifest_path.relative_to(published_root).as_posix()
    pointer = {
        "contract_name": POINTER_CONTRACT,
        "schema_version": SCHEMA_VERSION,
        "status": "published",
        "bundle_id": bundle_id,
        "manifest_path": manifest_relative,
        "manifest_sha256": verified.manifest_sha256,
    }
    _atomic_write(pointer_path, _json_bytes(pointer))
    verify_bundle(pointer_path)
    return {
        "pointer_path": str(pointer_path),
        "manifest_path": str(manifest_path),
        "bundle_dir": str(bundle_dir),
        "bundle_id": bundle_id,
    }


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("create")
    create.add_argument("--published-root", required=True, type=Path)
    create.add_argument("--trace", required=True, type=Path)
    create.add_argument("--linux-gate", required=True, type=Path)
    create.add_argument("--flagship-gate", required=True, type=Path)
    create.add_argument("--staged-audit", required=True, type=Path)
    create.add_argument("--release-candidate", required=True, type=Path)
    verify = commands.add_parser("verify")
    verify.add_argument("--pointer", required=True, type=Path)
    return parser


def main(argv: Iterable[str] | None = None) -> int:
    args = _parser().parse_args(list(argv) if argv is not None else None)
    try:
        if args.command == "create":
            result = create_bundle(
                args.published_root,
                args.trace,
                args.linux_gate,
                args.flagship_gate,
                args.staged_audit,
                args.release_candidate,
            )
        else:
            bundle = verify_bundle(args.pointer)
            result = {
                "pointer_path": str(args.pointer),
                "manifest_path": str(bundle.manifest_path),
                "bundle_dir": str(bundle.manifest_path.parent),
                "bundle_id": bundle.bundle_id,
                "manifest_sha256": bundle.manifest_sha256,
                "entry_count": len(bundle.entries),
            }
    except BundleError as exc:
        raise SystemExit(f"[USER-JOURNEY-BUNDLE] FAIL: {exc}") from exc
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
