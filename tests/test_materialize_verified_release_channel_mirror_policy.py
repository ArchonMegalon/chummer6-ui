from __future__ import annotations

import importlib.util
import hashlib
import os
from pathlib import Path

import pytest


SCRIPT = (
    Path(__file__).resolve().parents[1]
    / "scripts"
    / "materialize-verified-release-channel-mirror.py"
)
SPEC = importlib.util.spec_from_file_location("verified_release_channel_mirror", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def manifest(
    *,
    rollout_state: str,
    supportability_state: str,
    missing_platforms: list[str],
) -> dict:
    return {
        "rolloutState": rollout_state,
        "supportabilityState": supportability_state,
        "desktopTupleCoverage": {
            "missingRequiredPlatforms": missing_platforms,
            "missingRequiredPlatformHeadPairs": (
                ["avalonia:macos"] if missing_platforms else []
            ),
            "missingRequiredPlatformHeadRidTuples": (
                ["avalonia:osx-arm64:macos"] if missing_platforms else []
            ),
        },
    }


def test_honest_incomplete_preview_uses_external_request_aware_verification() -> None:
    payload = manifest(
        rollout_state="coverage_incomplete",
        supportability_state="review_required",
        missing_platforms=["macos"],
    )

    assert MODULE.requires_complete_desktop_coverage(payload) is False
    assert (
        MODULE.manifest_verifier_environment(payload)[
            "CHUMMER_VERIFY_REQUIRE_COMPLETE_DESKTOP_COVERAGE"
        ]
        == "0"
    )


def test_stable_or_complete_manifests_keep_strict_desktop_coverage() -> None:
    incomplete_stable = manifest(
        rollout_state="public_stable",
        supportability_state="gold_supported",
        missing_platforms=["macos"],
    )
    complete_preview = manifest(
        rollout_state="promoted_preview",
        supportability_state="preview_supported",
        missing_platforms=[],
    )

    assert MODULE.requires_complete_desktop_coverage(incomplete_stable) is True
    assert MODULE.requires_complete_desktop_coverage(complete_preview) is True


class ArtifactVerifier:
    @staticmethod
    def iter_manifest_download_entries(payload: dict):
        yield from payload["artifacts"]

    @staticmethod
    def normalize_file_name(item: dict) -> str:
        return str(item.get("fileName") or "")

    @staticmethod
    def parse_positive_int(value: object) -> int | None:
        return value if isinstance(value, int) and value >= 0 else None

    @staticmethod
    def normalize_sha256(value: object) -> str:
        token = str(value or "").lower()
        return token if len(token) == 64 else ""


def artifact_manifest(file_name: str, content: bytes) -> dict:
    return {
        "artifacts": [
            {
                "fileName": file_name,
                "sizeBytes": len(content),
                "sha256": hashlib.sha256(content).hexdigest(),
            }
        ]
    }


def test_manifest_artifact_sync_replaces_stale_bytes_atomically(tmp_path: Path) -> None:
    source = tmp_path / "source"
    target = tmp_path / "target"
    source.mkdir()
    target.mkdir()
    expected = b"current signed release artifact"
    (source / "desktop.deb").write_bytes(expected)
    (target / "desktop.deb").write_bytes(b"stale bytes")

    synchronized = MODULE.synchronize_manifest_artifacts(
        artifact_manifest("desktop.deb", expected),
        ArtifactVerifier,
        source,
        target,
    )

    assert synchronized == [target / "desktop.deb"]
    assert (target / "desktop.deb").read_bytes() == expected
    assert not list(target.glob(".*.mirror.tmp"))


def test_manifest_artifact_sync_rejects_symlink_target(tmp_path: Path) -> None:
    source = tmp_path / "source"
    target = tmp_path / "target"
    outside = tmp_path / "outside.deb"
    source.mkdir()
    target.mkdir()
    expected = b"current signed release artifact"
    (source / "desktop.deb").write_bytes(expected)
    outside.write_bytes(b"must stay unchanged")
    os.symlink(outside, target / "desktop.deb")

    with pytest.raises(SystemExit, match="no symlink traversal"):
        MODULE.synchronize_manifest_artifacts(
            artifact_manifest("desktop.deb", expected),
            ArtifactVerifier,
            source,
            target,
        )

    assert outside.read_bytes() == b"must stay unchanged"
    assert (target / "desktop.deb").is_symlink()


def test_startup_smoke_receipt_sync_replaces_truth_and_removes_stale_receipts(
    tmp_path: Path,
) -> None:
    source = tmp_path / "source"
    target = tmp_path / "target"
    source.mkdir()
    target.mkdir()
    current_name = "startup-smoke-avalonia-linux-x64.receipt.json"
    stale_name = "startup-smoke-retired.receipt.json"
    (source / current_name).write_text('{"artifactDigest":"sha256:current"}\n')
    (target / current_name).write_text('{"artifactDigest":"sha256:stale"}\n')
    (target / stale_name).write_text("{}\n")
    (target / "startup-smoke-avalonia-linux-x64.log").write_text("preserve log\n")

    synchronized = MODULE.synchronize_startup_smoke_receipts(source, target)

    assert synchronized == [target / current_name]
    assert (target / current_name).read_text() == '{"artifactDigest":"sha256:current"}\n'
    assert not (target / stale_name).exists()
    assert (target / "startup-smoke-avalonia-linux-x64.log").read_text() == "preserve log\n"
