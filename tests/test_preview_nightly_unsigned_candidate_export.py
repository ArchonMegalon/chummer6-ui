from __future__ import annotations

import argparse
import importlib.util
import json
import os
import shutil
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


scope_fixtures = load(
    "unsigned_scope_fixture_for_export",
    ROOT / "tests" / "test_preview_nightly_unsigned_scope.py",
)
exporter = load(
    "preview_nightly_unsigned_candidate_export_for_tests",
    ROOT / "scripts" / "preview_nightly_unsigned_candidate_export.py",
)


def fixture(tmp_path: Path) -> dict[str, object]:
    source = scope_fixtures.fixture(tmp_path / "source")
    proposal = exporter.COMPOSITION.build_request(source["args"])
    composition_path = tmp_path / "source-composition.json"
    exporter.COMPOSITION.write_request(composition_path, proposal)
    candidate = tmp_path / "candidate"
    candidate.mkdir()
    for directory in exporter.CONTENT_DIRECTORIES:
        (candidate / directory).mkdir()
    source_by_relative = {
        exporter.COMPOSITION_PATH: composition_path,
        exporter.MANIFEST_PATH: source["publication"]
        / exporter.PUBLICATION_SCOPE.CANONICAL_MANIFEST_NAME,
        exporter.COMPATIBILITY_PATH: source["publication"]
        / exporter.PUBLICATION_SCOPE.COMPATIBILITY_MANIFEST_NAME,
        exporter.INSTALLER_PATH: source["publication"]
        / "files"
        / exporter.PUBLICATION_SCOPE.INSTALLER_NAME,
        exporter.PAYLOAD_PATH: source["publication"]
        / "files"
        / exporter.PUBLICATION_SCOPE.PAYLOAD_NAME,
        exporter.PAYLOAD_SIDECAR_PATH: source["publication"]
        / "files"
        / exporter.PUBLICATION_SCOPE.PAYLOAD_SIDECAR_NAME,
        exporter.PACKAGE_LOCK_PATH: source["package_lock"],
        exporter.PACKAGE_RECEIPT_PATH: source["receipt"],
        exporter.RETAINED_MANIFEST_PATH: source["retained"],
        exporter.NATIVE_LOCK_PATH: source["native_lock"],
    }
    for relative, path in source_by_relative.items():
        shutil.copy2(path, candidate / relative)
    manifest_sha = exporter.sha256_file(candidate / exporter.MANIFEST_PATH)
    output = tmp_path / "export"
    args = argparse.Namespace(
        candidate_root=candidate,
        output_root=output,
        expected_version=scope_fixtures.VERSION,
        expected_manifest_sha256=manifest_sha,
        source_sha=scope_fixtures.SOURCE_SHA,
        source_repository="ArchonMegalon/chummer6-ui",
        source_workflow=exporter.PRODUCER_WORKFLOW,
        source_run_id="123456",
        source_run_attempt="1",
        source_ref=exporter.PRODUCER_REF,
        source_actor="release-operator",
        runner_nonce="abcdef1234567890",
    )
    return {
        "args": args,
        "candidate": candidate,
        "output": output,
        "proposal": proposal,
        "source": source,
    }


def test_validates_and_exports_exact_windows_subset(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    receipt = exporter.export_candidate(values["args"])
    output = values["output"]
    exporter.require_exact_tree(
        output, exporter.OUTPUT_PATHS, "completed candidate export"
    )
    assert receipt["contractName"] == (
        "chummer6-ui.preview-nightly-unsigned-candidate-export"
    )
    assert receipt["release"] == {
        "channel": "preview",
        "version": scope_fixtures.VERSION,
    }
    assert receipt["platformScope"] == "windows_only"
    assert receipt["crossRunBitReproducible"] is False
    assert receipt["signature"] == {
        "policy": "preview_policy",
        "required": False,
        "status": "unsigned",
    }
    assert receipt["publicationAuthorized"] is False
    assert receipt["uploadAuthorized"] is False
    assert receipt["deployAuthorized"] is False
    assert receipt["githubArtifactTransport"] == "ephemeral_candidate_only"
    assert receipt["source"]["repository"] == exporter.SOURCE_REPOSITORY
    assert not any("linux" in path.name for path in output.rglob("*") if path.is_file())
    assert not any("macos" in path.name for path in output.rglob("*") if path.is_file())
    assert all((output / path).is_file() for path in exporter.OUTPUT_PATHS)
    proposal, inventory, replayed = exporter.validate_export_root(
        output,
        values["args"].expected_version,
        values["args"].expected_manifest_sha256,
        values["args"].source_sha,
    )
    assert proposal == values["proposal"]
    assert inventory["files"] == replayed["exportedContent"]
    assert all(
        set(row) == {"path", "sha256", "sizeBytes"}
        for row in inventory["files"]
    )
    assert replayed == receipt


def test_fork_source_repository_fails_closed(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["args"].source_repository = "example-fork/chummer6-ui"
    with pytest.raises(exporter.ExportError, match="repository authority"):
        exporter.export_candidate(values["args"])
    assert not values["output"].exists()


def test_export_replay_rejects_fork_repository_receipt(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    path = values["output"] / exporter.EXPORT_RECEIPT_PATH
    receipt = json.loads(path.read_text())
    receipt["source"]["repository"] = "example-fork/chummer6-ui"
    path.chmod(0o600)
    path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n")
    with pytest.raises(exporter.ExportError, match="source authority"):
        exporter.validate_export_root(
            values["output"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_candidate_validation_binds_composition_manifest_and_provenance(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    proposal = exporter.validate_candidate_root(
        values["candidate"],
        values["args"].expected_version,
        values["args"].expected_manifest_sha256,
        values["args"].source_sha,
    )
    assert proposal == values["proposal"]


@pytest.mark.parametrize(
    "relative",
    [
        exporter.INSTALLER_PATH,
        exporter.PAYLOAD_PATH,
        exporter.PAYLOAD_SIDECAR_PATH,
        exporter.PACKAGE_LOCK_PATH,
        exporter.PACKAGE_RECEIPT_PATH,
        exporter.RETAINED_MANIFEST_PATH,
        exporter.NATIVE_LOCK_PATH,
    ],
)
def test_any_bound_byte_tamper_fails_closed(tmp_path: Path, relative: str) -> None:
    values = fixture(tmp_path)
    path = values["candidate"] / relative
    path.write_bytes(path.read_bytes() + b"tamper")
    with pytest.raises(exporter.ExportError):
        exporter.validate_candidate_root(
            values["candidate"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_missing_payload_sidecar_fails_exact_export_boundary(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    (values["candidate"] / exporter.PAYLOAD_SIDECAR_PATH).unlink()
    with pytest.raises(exporter.ExportError, match="boundary"):
        exporter.validate_candidate_root(
            values["candidate"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_composition_source_sha_must_match_workflow_authority(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    with pytest.raises(exporter.ExportError, match="source SHA"):
        exporter.validate_candidate_root(
            values["candidate"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            "b" * 40,
        )


def test_extra_file_and_link_fail_exact_tree(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    extra = values["candidate"] / "extra.txt"
    extra.write_text("no", encoding="utf-8")
    with pytest.raises(exporter.ExportError, match="boundary"):
        exporter.validate_candidate_root(
            values["candidate"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )
    extra.unlink()
    os.symlink(exporter.COMPOSITION_PATH, extra)
    with pytest.raises(exporter.ExportError, match="symbolic link"):
        exporter.validate_candidate_root(
            values["candidate"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_existing_export_output_is_never_overwritten(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    values["output"].mkdir()
    marker = values["output"] / "marker"
    marker.write_text("retain", encoding="utf-8")
    with pytest.raises(exporter.ExportError, match="must be absent"):
        exporter.export_candidate(values["args"])
    assert marker.read_text() == "retain"


def test_export_receipt_has_no_signing_or_release_authority_claims(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    receipt = json.loads(
        (values["output"] / exporter.EXPORT_RECEIPT_PATH).read_text()
    )
    encoded = json.dumps(receipt, sort_keys=True).lower()
    for forbidden in (
        "authenticode",
        "signingreceipt",
        "nativecapture",
        "visualapproval",
        "humanapproval",
        "macossoak",
        "stable",
    ):
        assert forbidden not in encoded


def test_complete_export_replay_rejects_receipt_tamper(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    path = values["output"] / exporter.EXPORT_RECEIPT_PATH
    receipt = json.loads(path.read_text())
    receipt["uploadAuthorized"] = True
    path.chmod(0o600)
    path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n")
    with pytest.raises(exporter.ExportError, match="receipt posture"):
        exporter.validate_export_root(
            values["output"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_download_mode_normalization_does_not_change_transport_identity(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    for path in values["output"].rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    proposal, inventory, receipt = exporter.validate_export_root(
        values["output"],
        values["args"].expected_version,
        values["args"].expected_manifest_sha256,
        values["args"].source_sha,
    )
    assert proposal == values["proposal"]
    assert inventory["files"] == receipt["exportedContent"]


def test_normalized_download_still_rejects_byte_and_path_tamper(
    tmp_path: Path,
) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    for path in values["output"].rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    payload = values["output"] / exporter.PAYLOAD_PATH
    payload.write_bytes(payload.read_bytes() + b"tamper")
    with pytest.raises(exporter.ExportError):
        exporter.validate_export_root(
            values["output"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )
    payload.write_bytes(
        (values["candidate"] / exporter.PAYLOAD_PATH).read_bytes()
    )
    renamed = payload.with_name("renamed-payload.zip")
    payload.rename(renamed)
    with pytest.raises(exporter.ExportError, match="boundary"):
        exporter.validate_export_root(
            values["output"],
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )


def test_direct_import_reconstructs_exact_full_shelf(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    for path in values["output"].rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    output = tmp_path / "reconstructed-publication"
    proposal = exporter.reconstruct_publication(
        values["output"],
        values["source"]["incumbent"],
        output,
        values["args"].expected_version,
        values["args"].expected_manifest_sha256,
        values["args"].source_sha,
    )
    assert exporter.PUBLICATION_SCOPE.file_inventory(output) == proposal[
        "proposedShelfInventory"
    ]
    assert exporter.PUBLICATION_SCOPE.directory_modes(output) == proposal[
        "proposedDirectoryModes"
    ]


def test_direct_import_rejects_different_incumbent(tmp_path: Path) -> None:
    values = fixture(tmp_path)
    exporter.export_candidate(values["args"])
    note = values["source"]["incumbent"] / "operator-note.txt"
    note.write_bytes(b"changed after composition")
    with pytest.raises(exporter.ExportError, match="incumbent snapshot"):
        exporter.reconstruct_publication(
            values["output"],
            values["source"]["incumbent"],
            tmp_path / "reconstructed-publication",
            values["args"].expected_version,
            values["args"].expected_manifest_sha256,
            values["args"].source_sha,
        )
