#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import hashlib
import json
import os
import stat
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def load_verifier_module(repo_root: Path):
    verifier_path = repo_root.parent / "chummer-hub-registry" / "scripts" / "verify_public_release_channel.py"
    spec = importlib.util.spec_from_file_location("verify_public_release_channel", verifier_path)
    if spec is None or spec.loader is None:
        raise SystemExit(f"Could not load verifier module: {verifier_path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def refresh_generated_at_fields(payload: dict, generated_at: str) -> None:
    payload["generated_at"] = generated_at
    payload["generatedAt"] = generated_at


def normalize_verifier_owned_fields(payload: dict, verifier_module, generated_at: str) -> dict:
    normalized = json.loads(json.dumps(payload))
    refresh_generated_at_fields(normalized, generated_at)
    for artifact in normalized.get("artifacts") or []:
        if isinstance(artifact, dict):
            refresh_generated_at_fields(artifact, generated_at)

    release_proof = normalized.get("releaseProof")
    if isinstance(release_proof, dict):
        refresh_generated_at_fields(release_proof, generated_at)
        localization_gate = release_proof.get("uiLocalizationReleaseGate")
        if isinstance(localization_gate, dict):
            refresh_generated_at_fields(localization_gate, generated_at)
            for key in ("local_release_proof", "localReleaseProof"):
                nested = localization_gate.get(key)
                if isinstance(nested, dict):
                    refresh_generated_at_fields(nested, generated_at)

    normalized["desktopRouteTruth"] = verifier_module.expected_desktop_route_truth_rows(normalized)
    tuple_coverage = normalized.get("desktopTupleCoverage")
    if isinstance(tuple_coverage, dict):
        tuple_coverage["desktopRouteTruth"] = verifier_module.expected_desktop_route_truth_rows(normalized)
        tuple_coverage["externalProofRequests"] = verifier_module.expected_external_proof_request_rows(normalized)
    normalized["installAwareArtifactRegistry"] = verifier_module.expected_install_aware_artifact_registry_rows(normalized)
    normalized["desktopSurfaceRefs"] = verifier_module.expected_desktop_surface_ref_rows(normalized)
    normalized["artifactIdentityRegistry"] = verifier_module.expected_artifact_identity_registry_rows(normalized)
    normalized["artifactPublicationBindings"] = verifier_module.expected_artifact_publication_binding_rows(normalized)
    normalized["exchangeLineageRegistry"] = verifier_module.expected_exchange_lineage_registry_rows(normalized)
    normalized["publicTrustMetrics"] = verifier_module.expected_public_trust_metrics(normalized)
    normalized["registryBoundaryCoverage"] = verifier_module.expected_registry_boundary_coverage(normalized)
    return normalized


def requires_complete_desktop_coverage(payload: dict) -> bool:
    """Keep stable/publication mirrors strict while allowing honest preview gaps."""

    tuple_coverage = payload.get("desktopTupleCoverage")
    if not isinstance(tuple_coverage, dict):
        return True
    missing_fields = (
        "missingRequiredPlatforms",
        "missingRequiredPlatformHeadPairs",
        "missingRequiredPlatformHeadRidTuples",
    )
    coverage_incomplete = any(
        isinstance(tuple_coverage.get(field), list) and bool(tuple_coverage[field])
        for field in missing_fields
    )
    rollout_state = str(payload.get("rolloutState") or "").strip().lower()
    supportability_state = str(payload.get("supportabilityState") or "").strip().lower()
    return not (
        coverage_incomplete
        and rollout_state == "coverage_incomplete"
        and supportability_state == "review_required"
    )


def manifest_verifier_environment(payload: dict) -> dict[str, str]:
    environment = os.environ.copy()
    environment["CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE"] = (
        "1" if requires_complete_desktop_coverage(payload) else "0"
    )
    return environment


def _require_real_directory(path: Path, *, label: str) -> None:
    try:
        metadata = path.lstat()
    except FileNotFoundError as exc:
        raise SystemExit(f"{label} is missing: {path}") from exc
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
        raise SystemExit(f"{label} must be a real directory, not a symlink or special file: {path}")


def _open_regular_file_no_follow(path: Path, *, label: str):
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(path, flags)
    except OSError as exc:
        raise SystemExit(f"{label} must be a readable regular file with no symlink traversal: {path}") from exc
    metadata = os.fstat(descriptor)
    if not stat.S_ISREG(metadata.st_mode):
        os.close(descriptor)
        raise SystemExit(f"{label} must be a regular file: {path}")
    return descriptor, metadata


def synchronize_manifest_artifacts(
    payload: dict,
    verifier_module,
    source_files_dir: Path,
    target_files_dir: Path,
) -> list[Path]:
    """Atomically make a local mirror's declared artifact bytes match its manifest."""

    _require_real_directory(source_files_dir, label="release artifact source directory")
    if not target_files_dir.exists():
        target_files_dir.mkdir(parents=True)
    _require_real_directory(target_files_dir, label="release artifact mirror directory")

    synchronized: list[Path] = []
    seen_names: set[str] = set()
    for index, item in enumerate(verifier_module.iter_manifest_download_entries(payload)):
        file_name = verifier_module.normalize_file_name(item)
        if (
            not file_name
            or file_name in {".", ".."}
            or Path(file_name).name != file_name
            or "/" in file_name
            or "\\" in file_name
        ):
            raise SystemExit(f"manifest entry {index} has an unsafe artifact fileName: {file_name!r}")
        normalized_name = file_name.casefold()
        if normalized_name in seen_names:
            raise SystemExit(f"manifest declares the artifact fileName more than once: {file_name}")
        seen_names.add(normalized_name)

        expected_size = verifier_module.parse_positive_int(item.get("sizeBytes"))
        expected_sha = verifier_module.normalize_sha256(item.get("sha256"))
        if expected_size is None or not expected_sha:
            raise SystemExit(
                f"manifest artifact must declare exact sizeBytes and sha256 before mirror sync: {file_name}"
            )

        source_path = source_files_dir / file_name
        source_descriptor, source_metadata = _open_regular_file_no_follow(
            source_path,
            label="release artifact source",
        )
        target_path = target_files_dir / file_name
        if target_path.exists() or target_path.is_symlink():
            target_metadata = target_path.lstat()
            if stat.S_ISLNK(target_metadata.st_mode) or not stat.S_ISREG(target_metadata.st_mode):
                os.close(source_descriptor)
                raise SystemExit(
                    f"release artifact mirror target must be a regular file with no symlink traversal: {target_path}"
                )

        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{file_name}.",
            suffix=".mirror.tmp",
            dir=target_files_dir,
        )
        staged_path = Path(temporary_name)
        digest = hashlib.sha256()
        copied_size = 0
        try:
            with os.fdopen(source_descriptor, "rb") as source, os.fdopen(descriptor, "wb") as target:
                while True:
                    chunk = source.read(1024 * 1024)
                    if not chunk:
                        break
                    target.write(chunk)
                    digest.update(chunk)
                    copied_size += len(chunk)
                target.flush()
                os.fsync(target.fileno())
                os.fchmod(target.fileno(), 0o644)

            if source_metadata.st_size != expected_size or copied_size != expected_size:
                raise SystemExit(
                    f"release artifact source size mismatch for {file_name}: "
                    f"expected {expected_size}, actual {copied_size}"
                )
            actual_sha = digest.hexdigest().lower()
            if actual_sha != expected_sha:
                raise SystemExit(
                    f"release artifact source sha256 mismatch for {file_name}: "
                    f"expected {expected_sha}, actual {actual_sha}"
                )

            os.replace(staged_path, target_path)
            synchronized.append(target_path)
        finally:
            staged_path.unlink(missing_ok=True)

    directory_descriptor = os.open(
        target_files_dir,
        os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_CLOEXEC", 0),
    )
    try:
        os.fsync(directory_descriptor)
    finally:
        os.close(directory_descriptor)
    return synchronized


def synchronize_startup_smoke_receipts(
    source_receipts_dir: Path,
    target_receipts_dir: Path,
) -> list[Path]:
    """Mirror receipt truth used to authenticate the promoted artifact digests."""

    _require_real_directory(source_receipts_dir, label="startup-smoke receipt source directory")
    if not target_receipts_dir.exists():
        target_receipts_dir.mkdir(parents=True)
    _require_real_directory(target_receipts_dir, label="startup-smoke receipt mirror directory")

    source_names: set[str] = set()
    synchronized: list[Path] = []
    for source_path in sorted(source_receipts_dir.iterdir(), key=lambda path: path.name):
        if not source_path.name.startswith("startup-smoke-") or not source_path.name.endswith(
            ".receipt.json"
        ):
            continue
        source_names.add(source_path.name)
        source_descriptor, _ = _open_regular_file_no_follow(
            source_path,
            label="startup-smoke receipt source",
        )
        target_path = target_receipts_dir / source_path.name
        if target_path.exists() or target_path.is_symlink():
            target_metadata = target_path.lstat()
            if stat.S_ISLNK(target_metadata.st_mode) or not stat.S_ISREG(target_metadata.st_mode):
                os.close(source_descriptor)
                raise SystemExit(
                    f"startup-smoke receipt mirror target must be a regular file with no symlink traversal: {target_path}"
                )

        descriptor, temporary_name = tempfile.mkstemp(
            prefix=f".{source_path.name}.",
            suffix=".mirror.tmp",
            dir=target_receipts_dir,
        )
        staged_path = Path(temporary_name)
        try:
            with os.fdopen(source_descriptor, "rb") as source, os.fdopen(descriptor, "wb") as target:
                while True:
                    chunk = source.read(1024 * 1024)
                    if not chunk:
                        break
                    target.write(chunk)
                target.flush()
                os.fsync(target.fileno())
                os.fchmod(target.fileno(), 0o644)
            os.replace(staged_path, target_path)
            synchronized.append(target_path)
        finally:
            staged_path.unlink(missing_ok=True)

    for target_path in sorted(target_receipts_dir.iterdir(), key=lambda path: path.name):
        if (
            not target_path.name.startswith("startup-smoke-")
            or not target_path.name.endswith(".receipt.json")
            or target_path.name in source_names
        ):
            continue
        target_metadata = target_path.lstat()
        if stat.S_ISLNK(target_metadata.st_mode) or not stat.S_ISREG(target_metadata.st_mode):
            raise SystemExit(
                f"stale startup-smoke receipt mirror entry must be a regular file with no symlink traversal: {target_path}"
            )
        target_path.unlink()

    directory_descriptor = os.open(
        target_receipts_dir,
        os.O_RDONLY | getattr(os, "O_DIRECTORY", 0) | getattr(os, "O_CLOEXEC", 0),
    )
    try:
        os.fsync(directory_descriptor)
    finally:
        os.close(directory_descriptor)
    return synchronized


def main() -> int:
    repo_root = Path(__file__).absolute().parents[1]
    registry_root = (repo_root / "scripts" / "resolve-hub-registry-root.sh").resolve()
    resolved_registry_root = subprocess.run(
        ["bash", str(registry_root)],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if not resolved_registry_root:
        raise SystemExit("Could not resolve hub-registry root.")

    canonical_manifest = Path(resolved_registry_root) / ".codex-studio" / "published" / "RELEASE_CHANNEL.generated.json"
    if not canonical_manifest.is_file():
        raise SystemExit(f"Canonical release channel is missing: {canonical_manifest}")

    payload = json.loads(canonical_manifest.read_text(encoding="utf-8-sig"))
    source_generated_at = str(payload.get("generatedAt") or payload.get("generated_at") or "").strip()
    now = utc_now()
    verifier_module = load_verifier_module(repo_root)
    payload = normalize_verifier_owned_fields(payload, verifier_module, now)
    payload["generated_at"] = now
    payload["generatedAt"] = now
    payload["verifiedAt"] = now
    payload["verifiedFromPath"] = str(canonical_manifest)
    payload["verifiedFromGeneratedAt"] = source_generated_at
    verifier_environment = manifest_verifier_environment(payload)

    output_path = repo_root / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{output_path.name}.",
        suffix=".tmp",
        dir=output_path.parent,
        text=True,
    )
    staged_output_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
            handle.write(json.dumps(payload, indent=2) + "\n")
            handle.flush()
            os.fsync(handle.fileno())

        subprocess.run(
            [
                "bash",
                str(repo_root / "scripts" / "verify-releases-manifest.sh"),
                str(staged_output_path),
            ],
            check=True,
            env=verifier_environment,
        )
        os.replace(staged_output_path, output_path)
    finally:
        staged_output_path.unlink(missing_ok=True)

    portal_mirror_dir = repo_root / ".codex-studio" / "published" / "portal"
    portal_mirror_dir.mkdir(parents=True, exist_ok=True)
    materializer_path = Path(resolved_registry_root) / "scripts" / "materialize_public_release_channel.py"
    mirror_command = [
        "python3",
        str(materializer_path),
        "--manifest",
        str(output_path),
        "--downloads-dir",
        str(repo_root / "Docker" / "Downloads" / "files"),
        "--startup-smoke-dir",
        str(repo_root / "Docker" / "Downloads" / "startup-smoke"),
        "--output",
        str(portal_mirror_dir / "RELEASE_CHANNEL.generated.json"),
        "--compat-output",
        str(portal_mirror_dir / "releases.json"),
    ]
    if os.environ.get("CHUMMER_VERIFIED_RELEASE_MIRROR_REFILTER_STARTUP_SMOKE", "0").strip().lower() not in {
        "1",
        "true",
        "yes",
        "on",
    }:
        mirror_command.append("--skip-startup-smoke-filter")
    subprocess.run(
        mirror_command,
        check=True,
        stdout=subprocess.DEVNULL,
    )
    portal_payload = json.loads(
        (portal_mirror_dir / "RELEASE_CHANNEL.generated.json").read_text(encoding="utf-8-sig")
    )
    synchronize_manifest_artifacts(
        portal_payload,
        verifier_module,
        repo_root / "Docker" / "Downloads" / "files",
        portal_mirror_dir / "files",
    )
    synchronize_startup_smoke_receipts(
        repo_root / "Docker" / "Downloads" / "startup-smoke",
        portal_mirror_dir / "startup-smoke",
    )

    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(portal_mirror_dir / "RELEASE_CHANNEL.generated.json"),
        ],
        check=True,
        env=verifier_environment,
    )
    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(portal_mirror_dir / "releases.json"),
        ],
        check=True,
        env=verifier_environment,
    )

    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
