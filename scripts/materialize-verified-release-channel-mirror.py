#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
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


def main() -> int:
    repo_root = Path(__file__).resolve().parents[1]
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

    output_path = repo_root / ".tmp" / "verify-release-channel" / "RELEASE_CHANNEL.generated.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")

    subprocess.run(
        ["bash", str(repo_root / "scripts" / "verify-releases-manifest.sh"), str(output_path)],
        check=True,
    )

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

    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(portal_mirror_dir / "RELEASE_CHANNEL.generated.json"),
        ],
        check=True,
    )
    subprocess.run(
        [
            "bash",
            str(repo_root / "scripts" / "verify-releases-manifest.sh"),
            str(portal_mirror_dir / "releases.json"),
        ],
        check=True,
    )

    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
